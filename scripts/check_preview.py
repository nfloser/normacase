"""Exercise extracted preview executables; never print assessment contents."""
import argparse
from decimal import Decimal
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import re
import subprocess
import tempfile
import time
from urllib.error import HTTPError, URLError
from urllib.request import Request, urlopen
import zipfile
from workflow_smoke import exercise_workflow


def verify_archive_sidecar(archive):
    sidecar = archive.with_suffix(archive.suffix + ".sha256")
    if not sidecar.is_file():
        raise ValueError("Archive checksum sidecar missing")
    parts = sidecar.read_text(encoding="ascii").strip().split("  ", 1)
    if len(parts) != 2 or parts[1] != archive.name:
        raise ValueError("Archive checksum sidecar invalid")
    if hashlib.sha256(archive.read_bytes()).hexdigest() != parts[0]:
        raise ValueError("Archive checksum mismatch")


WINDOWS_RESERVED_NAMES = {
    "CON", "PRN", "AUX", "NUL", "CONIN$", "CONOUT$",
    *(f"COM{number}" for number in range(1, 10)),
    *(f"LPT{number}" for number in range(1, 10)),
}


def validate_bundle_paths(bundle):
    infos = bundle.infolist()
    names = [info.filename for info in infos]
    if len(names) != len(set(names)):
        raise ValueError("Duplicate bundle entries")

    folded_paths = set()
    for info in infos:
        name = info.filename
        path = PurePosixPath(name)
        parts = path.parts
        if (not name or name != info.orig_filename or path.is_absolute()
                or name != path.as_posix() or "\\" in name or ":" in name
                or info.is_dir()
                or (info.external_attr >> 16) & 0o170000 == 0o120000):
            raise ValueError("Invalid bundle path")
        for part in parts:
            base = part.split(".", 1)[0].upper()
            if (not part or part in {".", ".."} or part.endswith((" ", "."))
                    or any(ord(character) < 32 for character in part)
                    or base in WINDOWS_RESERVED_NAMES):
                raise ValueError("Invalid bundle path")

        folded = "/".join(part.casefold() for part in parts)
        if folded in folded_paths:
            raise ValueError("Bundle extraction path collision")
        folded_paths.add(folded)

    for folded in folded_paths:
        parts = folded.split("/")
        for index in range(1, len(parts)):
            if "/".join(parts[:index]) in folded_paths:
                raise ValueError("Bundle extraction path collision")
    return names


def extract_verified(archive, target):
    with zipfile.ZipFile(archive) as bundle:
        names = validate_bundle_paths(bundle)

        if "preview.json" not in names:
            raise ValueError("Bundle manifest missing")
        manifest = json.loads(bundle.read("preview.json").decode("utf-8"))
        expected = manifest["files"]
        if set(names) != set(expected) | {"preview.json"}:
            raise ValueError("Bundle file inventory mismatch")
        for name, digest in expected.items():
            if hashlib.sha256(bundle.read(name)).hexdigest() != digest:
                raise ValueError("Bundle integrity mismatch")

        bundle.extractall(target)
        # zipfile does not restore executable bits on Unix.
        if os.name != "nt":
            for info in bundle.infolist():
                mode = (info.external_attr >> 16) & 0o777
                if mode:
                    (target / info.filename).chmod(mode)
    return manifest


def check(archive, rid, commit):
    verify_archive_sidecar(archive)
    with tempfile.TemporaryDirectory(prefix="NormaCase preview ") as temporary:
        root = Path(temporary) / "extracted bundle"
        root.mkdir()
        manifest = extract_verified(archive, root)
        assert manifest["formatVersion"] == 1
        assert manifest["runtimeIdentifier"] == rid
        assert manifest["sourceCommit"] == commit
        platform = "0.1.0-preview+" + commit
        assert manifest["platformVersion"] == platform
        suffix = ".exe" if rid == "win-x64" else ""
        runtime = "hostfxr.dll" if rid == "win-x64" else "libhostfxr.so"
        assert (root / "api" / runtime).is_file(), "API runtime missing"
        assert (root / "cli" / runtime).is_file(), "CLI runtime missing"
        for folder, name in [("api", "Api"), ("cli", "Cli")]:
            options = json.loads((root / folder / ("NormaCase." + name + ".runtimeconfig.json"))
                                 .read_text(encoding="utf-8"))["runtimeOptions"]
            assert options.get("includedFrameworks"), "Self-contained runtime configuration missing"
            assert "framework" not in options and "frameworks" not in options
        environment = {**os.environ, "DOTNET_ROOT": str(root / "absent-runtime"),
                       "DOTNET_ROOT_X64": str(root / "absent-runtime"),
                       "DOTNET_MULTILEVEL_LOOKUP": "0", "ASPNETCORE_ENVIRONMENT": "Production"}
        cli = root / "cli" / ("NormaCase.Cli" + suffix)

        def run(*args):
            result = subprocess.run([str(cli), *map(str, args)], cwd=temporary,
                                    env=environment, capture_output=True,
                                    encoding="utf-8", timeout=30)
            assert result.returncode == 0 and not result.stderr, "Bundled CLI failed"
            return result.stdout

        assert "synthet" in run("--help").lower(), "German CLI help missing"
        for demo in "abcdefg":
            validation = run(
                "validate",
                "--pack",
                root / "knowledge" / ("demo-" + demo) / "pack.json",
            )
            assert "Strukturprüfung abgeschlossen" in validation
            assert "keine fachliche Freigabe" in validation
        case = root / "examples" / "cases" / "demo-b-supported.json"
        precise = case.read_text(encoding="utf-8").replace(
            '"number": 15', '"number": 123456789.1234567890123456789')
        private_case = Path(temporary) / "synthetic precise case.json"
        private_case.write_text(precise, encoding="utf-8")
        pack = root / "knowledge" / "demo-b" / "pack.json"
        captured = run("snapshot", "--pack", pack, "--case", private_case,
                       "--platform-version", platform)
        snapshot = json.loads(captured, parse_float=Decimal)
        assert snapshot["input"]["facts"]["score"]["number"] == Decimal(
            "123456789.1234567890123456789"), "CLI decimal changed"
        saved = Path(temporary) / "synthetic snapshot.json"
        saved.write_text(captured, encoding="utf-8")
        replayed = run("replay", "--snapshot", saved, "--platform-version", platform, "--json")
        assert json.loads(replayed, parse_float=Decimal) == snapshot["assessment"]

        api = root / "api" / ("NormaCase.Api" + suffix)
        process = subprocess.Popen([str(api)], cwd=temporary, env=environment,
                                   stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        base = "http://localhost:5080"
        try:
            for _ in range(120):
                try:
                    with urlopen(base + "/api/packs", timeout=1) as response:
                        catalog = json.load(response)
                        assert response.headers["Cache-Control"] == "no-store"
                    break
                except URLError:
                    if process.poll() is not None:
                        raise RuntimeError("Bundled API terminated before readiness")
                    time.sleep(0.25)
            else:
                raise RuntimeError("Bundled API did not become ready")
            assert {p["packId"] for p in catalog} == {
                "synthetic.demo-" + letter for letter in "abcdefg"}
            assert all(p["validationLevel"] == "SYNTHETIC" and
                       p["presentation"]["locale"] == "de-DE" for p in catalog)
            with urlopen(base, timeout=5) as response:
                html = response.read().decode("utf-8")
                assert "default-src 'self'" in response.headers["Content-Security-Policy"]
            assert '<html lang="de"' in html
            assets = re.findall(r'(?:src|href)="(/assets/[^\"]+)"', html)
            assert len(assets) >= 2, "Bundled browser assets missing"
            for asset in assets:
                with urlopen(base + asset, timeout=5) as response:
                    assert len(response.read()) > 0
                    assert response.headers["X-Content-Type-Options"] == "nosniff"

            def post(path, text):
                request = Request(base + path, data=text.encode("utf-8"),
                                  headers={"Content-Type": "application/json"})
                with urlopen(request, timeout=10) as response:
                    return response.read().decode("utf-8")

            scenario = json.loads((root / "examples/scenarios/pitch-demo-v1.json").read_text(encoding="utf-8"))
            for step in scenario["steps"]:
                pitch_case = root / "examples/cases" / step["caseFile"]
                pitch_text = pitch_case.read_text(encoding="utf-8")
                api_result = json.loads(post("/api/assessments/" + scenario["packId"], pitch_text))
                cli_result = json.loads(run("evaluate", "--pack", root / "knowledge/demo-g/pack.json",
                                            "--case", pitch_case, "--platform-version", platform, "--json"))
                assert api_result == cli_result, "Named preview pitch CLI/API result mismatch"
                assert api_result["platformVersion"] == platform
                assert api_result["assessment"]["outcome"] == step["expectedOutcome"]
                assert api_result["assessment"]["missingRequiredFields"] == step["expectedMissingRequiredFields"]
                assert api_result["assessment"]["knowledgeRelease"] == scenario["releaseId"]

            api_capture = json.loads(post("/api/snapshots/synthetic.demo-b", precise))
            api_snapshot = json.loads(api_capture["snapshotJson"], parse_float=Decimal)
            assert api_snapshot["assessment"]["platformVersion"] == platform
            assert api_snapshot["input"]["facts"]["score"]["number"] == Decimal(
                "123456789.1234567890123456789"), "API decimal changed"
            verified = json.loads(post("/api/snapshots/replay", api_capture["snapshotJson"]))
            assert verified["assessmentJson"] == api_capture["assessmentJson"]
            # Snapshots remain interchangeable between bundled CLI and API.
            assert json.loads(post("/api/snapshots/replay", captured))["assessmentJson"] == replayed.strip()
            exercise_workflow(base)
            denied = Request(base + "/api/packs", headers={"Origin": "https://example.invalid"})
            try:
                urlopen(denied, timeout=5)
                raise AssertionError("Cross-origin request accepted")
            except HTTPError as error:
                assert error.code == 403
        finally:
            process.terminate()
            try:
                process.wait(timeout=5)
            except subprocess.TimeoutExpired:
                process.kill()
                process.wait(timeout=5)
    print("Self-contained preview integrity, local assets and precise CLI/API replay passed.")


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--archive", type=Path, required=True)
    parser.add_argument("--rid", choices=["linux-x64", "win-x64"], required=True)
    parser.add_argument("--commit", required=True)
    arguments = parser.parse_args()
    check(arguments.archive, arguments.rid, arguments.commit)
