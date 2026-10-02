"""Smoke-test an extracted self-contained NormaCase preview archive."""
from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
import subprocess
import tarfile
import tempfile
import time
import urllib.error
import urllib.request
import zipfile


BASE_URL = "http://127.0.0.1:5080"
EXACT_DECIMAL = "123456789.1234567890123456789"


def extract_archive(archive: Path, target: Path) -> Path:
    if archive.name.endswith(".tar.gz"):
        with tarfile.open(archive, "r:gz") as source:
            source.extractall(target, filter="data")
    elif archive.suffix == ".zip":
        with zipfile.ZipFile(archive, "r") as source:
            source.extractall(target)
    else:
        raise AssertionError("Unsupported preview archive")

    roots = [path for path in target.iterdir() if path.is_dir()]
    assert len(roots) == 1, "Archive must contain exactly one preview root"
    return roots[0]


def verify_archive_checksum(archive: Path) -> None:
    sidecar = archive.with_name(archive.name + ".sha256")
    expected, name = sidecar.read_text(encoding="utf-8").strip().split("  ", 1)
    assert name == archive.name
    assert hashlib.sha256(archive.read_bytes()).hexdigest() == expected


def verify_bundle_manifest(root: Path) -> None:
    entries = root.joinpath("SHA256SUMS.txt").read_text(encoding="utf-8").splitlines()
    assert entries
    for entry in entries:
        expected, relative = entry.split("  ", 1)
        path = root / Path(relative)
        assert path.is_file(), f"Manifest file missing: {relative}"
        assert hashlib.sha256(path.read_bytes()).hexdigest() == expected


def http_text(
    path: str,
    body: str | None = None,
) -> str:
    request = urllib.request.Request(
        BASE_URL + path,
        data=None if body is None else body.encode("utf-8"),
        headers={} if body is None else {"Content-Type": "application/json"},
        method="GET" if body is None else "POST",
    )
    with urllib.request.urlopen(request, timeout=5) as response:
        assert response.status == 200
        return response.read().decode("utf-8")


def wait_for_api(process: subprocess.Popen[bytes]) -> None:
    deadline = time.monotonic() + 30
    while time.monotonic() < deadline:
        if process.poll() is not None:
            raise AssertionError("Self-contained API exited during startup")
        try:
            http_text("/api/packs")
            return
        except (urllib.error.URLError, ConnectionError, TimeoutError):
            time.sleep(0.25)
    raise AssertionError("Self-contained API did not become ready")


def run_cli(
    executable: Path,
    root: Path,
    *args: str,
) -> subprocess.CompletedProcess[str]:
    environment = os.environ.copy()
    environment["DOTNET_ROOT"] = str(root / ".missing-dotnet-runtime")
    environment["DOTNET_MULTILEVEL_LOOKUP"] = "0"
    return subprocess.run(
        [str(executable), *args],
        cwd=root,
        env=environment,
        capture_output=True,
        text=True,
        timeout=30,
        check=False,
    )


def exercise(root: Path) -> None:
    info = json.loads(root.joinpath("build-info.json").read_text(encoding="utf-8"))
    assert info["syntheticOnly"] is True
    rid = info["runtimeIdentifier"]
    platform_version = info["platformVersion"]
    assert platform_version
    assert len(info["sourceCommit"]) == 40

    for demo in ("demo-a", "demo-b", "demo-c", "demo-d", "demo-e"):
        assert (root / "knowledge" / demo / "pack.json").is_file()
    assert len(list((root / "examples" / "cases").glob("*.json"))) >= 5

    windows = rid == "win-x64"
    api = root / "api" / ("NormaCase.Api.exe" if windows else "NormaCase.Api")
    cli = root / "cli" / ("NormaCase.Cli.exe" if windows else "NormaCase.Cli")
    launcher = root / ("start-normacase.cmd" if windows else "start-normacase.sh")
    assert api.is_file() and cli.is_file() and launcher.is_file()

    environment = os.environ.copy()
    environment["DOTNET_ROOT"] = str(root / ".missing-dotnet-runtime")
    environment["DOTNET_MULTILEVEL_LOOKUP"] = "0"

    process = subprocess.Popen(
        [str(api)],
        cwd=root,
        env=environment,
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
    )
    try:
        wait_for_api(process)

        catalog = json.loads(http_text("/api/packs"))
        assert len(catalog) == 5
        assert {item["packId"] for item in catalog} == {
            "synthetic.demo-a",
            "synthetic.demo-b",
            "synthetic.demo-c",
            "synthetic.demo-d",
            "synthetic.demo-e",
        }

        page = http_text("/")
        assert "NormaCase" in page
        assert "Synthetische" in page or "Prüf" in page

        case_json = (
            '{"formatVersion":1,"assessmentDate":"2026-10-02","facts":'
            '{"score":{"kind":"NUMBER","number":' + EXACT_DECIMAL + '},'
            '"band_value":{"kind":"NUMBER","number":30}}}'
        )

        assessment = http_text(
            "/api/assessments/synthetic.demo-b",
            case_json,
        )
        assert EXACT_DECIMAL in assessment
        assert platform_version in assessment

        capture = json.loads(
            http_text(
                "/api/snapshots/synthetic.demo-b",
                case_json,
            )
        )
        snapshot_json = capture["snapshotJson"]
        assessment_json = capture["assessmentJson"]
        assert EXACT_DECIMAL in snapshot_json
        assert platform_version in snapshot_json
        assert platform_version in assessment_json

        replay = json.loads(
            http_text(
                "/api/snapshots/replay",
                snapshot_json,
            )
        )
        assert replay["assessmentJson"] == assessment_json
    finally:
        process.terminate()
        try:
            process.wait(timeout=10)
        except subprocess.TimeoutExpired:
            process.kill()
            process.wait(timeout=5)

    with tempfile.TemporaryDirectory() as temp_dir:
        case_path = Path(temp_dir) / "case.json"
        snapshot_path = Path(temp_dir) / "snapshot.json"
        case_path.write_text(case_json, encoding="utf-8")

        capture_cli = run_cli(
            cli,
            root,
            "snapshot",
            "--pack",
            str(root / "knowledge" / "demo-b" / "pack.json"),
            "--case",
            str(case_path),
            "--platform-version",
            platform_version,
        )
        assert capture_cli.returncode == 0, "Self-contained CLI snapshot failed"
        assert not capture_cli.stderr
        assert EXACT_DECIMAL in capture_cli.stdout
        assert platform_version in capture_cli.stdout
        snapshot_path.write_text(capture_cli.stdout, encoding="utf-8")

        replay_cli = run_cli(
            cli,
            root,
            "replay",
            "--snapshot",
            str(snapshot_path),
            "--platform-version",
            platform_version,
            "--json",
        )
        assert replay_cli.returncode == 0, "Self-contained CLI replay failed"
        assert not replay_cli.stderr
        captured_document = json.loads(capture_cli.stdout)
        assert json.loads(replay_cli.stdout) == captured_document["assessment"]


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--archive", type=Path, required=True)
    args = parser.parse_args()

    archive = args.archive.resolve()
    verify_archive_checksum(archive)
    with tempfile.TemporaryDirectory() as directory:
        root = extract_archive(archive, Path(directory))
        verify_bundle_manifest(root)
        exercise(root)

    print("Self-contained synthetic preview archive passed API/CLI snapshot smoke tests.")


if __name__ == "__main__":
    main()
