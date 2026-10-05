"""Stage exact synthetic native releases; no secrets, implicit migrations or database deletion."""
import argparse
from contextlib import contextmanager
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import socket
import subprocess
import tempfile
import uuid
from check_preview import extract_verified, verify_archive_sidecar

MESSAGES = json.loads(Path(__file__).with_name("installation.de.json").read_text(encoding="utf-8"))

def identity(manifest, rid, commit):
    if (not re.fullmatch(r"[0-9a-f]{40}", commit) or rid not in {"linux-x64", "win-x64"}
            or manifest.get("formatVersion") != 1 or manifest.get("sourceCommit") != commit
            or manifest.get("runtimeIdentifier") != rid
            or manifest.get("validationLevel") != "SYNTHETIC"
            or manifest.get("platformVersion") != "0.1.0-preview+" + commit):
        raise ValueError("invalid_release_identity")

@contextmanager
def lock(root, name):
    root.mkdir(parents=True, exist_ok=True)
    path = root / (name + ".lock")
    descriptor = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
    try:
        os.close(descriptor)
        yield
    finally:
        path.unlink()

def verify_release(root, commit):
    if not re.fullmatch(r"[0-9a-f]{40}", commit):
        raise ValueError("invalid_commit")
    release = root / "releases" / commit
    if release.is_symlink():
        raise ValueError("invalid_release_path")
    paths = list(release.rglob("*"))
    if any(path.is_symlink() for path in paths):
        raise ValueError("invalid_release_path")
    manifest = json.loads((release / "preview.json").read_text(encoding="utf-8"))
    identity(manifest, manifest.get("runtimeIdentifier"), commit)
    actual = {path.relative_to(release).as_posix() for path in paths if path.is_file()}
    expected = manifest["files"]
    if actual != set(expected) | {"preview.json"}:
        raise ValueError("release_inventory_changed")
    for name, expected_hash in expected.items():
        if not isinstance(expected_hash, str) or not re.fullmatch(r"[0-9a-f]{64}", expected_hash):
            raise ValueError("invalid_hash")
        with (release / name).open("rb") as source:
            if hashlib.file_digest(source, "sha256").hexdigest() != expected_hash:
                raise ValueError("release_content_changed")
    return release, manifest

def stage(root, archive, rid, commit):
    if not re.fullmatch(r"[0-9a-f]{40}", commit) or rid not in {"linux-x64", "win-x64"}:
        raise ValueError("invalid_release_identity")
    verify_archive_sidecar(archive)
    with lock(root, "installation"):
        releases = root / "releases"
        releases.mkdir(exist_ok=True)
        destination = releases / commit
        if destination.exists():
            raise FileExistsError("release_already_staged")
        temporary = Path(tempfile.mkdtemp(prefix="staging-", dir=releases))
        try:
            manifest = extract_verified(archive, temporary)
            identity(manifest, rid, commit)
            os.replace(temporary, destination)
        finally:
            if temporary.exists():
                shutil.rmtree(temporary)
        verify_release(root, commit)
    return commit

def read_current(root):
    path = root / "current.json"
    if not path.exists():
        return None
    value = json.loads(path.read_text(encoding="utf-8"))
    if (value.get("formatVersion") != 1 or set(value) != {"formatVersion", "sourceCommit", "previousCommit"}
            or not re.fullmatch(r"[0-9a-f]{40}", value.get("sourceCommit", ""))
            or value.get("previousCommit") is not None and not re.fullmatch(r"[0-9a-f]{40}", value["previousCommit"])):
        raise ValueError("invalid_active_pointer")
    return value

def stopped():
    with socket.socket() as probe:
        probe.settimeout(0.25)
        if probe.connect_ex(("127.0.0.1", 5080)) == 0:
            raise ValueError("service_is_running")

def activate(root, commit):
    with lock(root, "installation"), lock(root, "service"):
        stopped()
        verify_release(root, commit)
        current = read_current(root)
        value = {"formatVersion": 1, "sourceCommit": commit,
                 "previousCommit": current["sourceCommit"] if current else None}
        temporary = root / ("pointer-" + uuid.uuid4().hex + ".tmp")
        try:
            with temporary.open("x", encoding="utf-8") as output:
                json.dump(value, output)
                output.flush()
                os.fsync(output.fileno())
            os.replace(temporary, root / "current.json")
        finally:
            temporary.unlink(missing_ok=True)
    return value

def run(root):
    with lock(root, "service"):
        with lock(root, "installation"):
            value = read_current(root)
            if value is None:
                raise ValueError("no_active_release")
            release, manifest = verify_release(root, value["sourceCommit"])
            expected = "win-x64" if os.name == "nt" else "linux-x64"
            if manifest["runtimeIdentifier"] != expected:
                raise ValueError("wrong_runtime")
            executable = release / "api" / ("NormaCase.Api.exe" if os.name == "nt" else "NormaCase.Api")
        # Credentials stay solely in the caller-controlled environment.
        return subprocess.call([str(executable)], cwd=executable.parent, env=os.environ.copy())

def main():
    parser = argparse.ArgumentParser(description=MESSAGES["description"])
    sub = parser.add_subparsers(dest="operation", required=True)
    for name in ("stage", "activate", "run", "status"):
        command = sub.add_parser(name, help=MESSAGES[name])
        command.add_argument("--root", type=Path, required=True, help=MESSAGES["root"])
        if name in ("stage", "activate"):
            command.add_argument("--commit", required=True, help=MESSAGES["commit"])
        if name == "stage":
            command.add_argument("--archive", type=Path, required=True, help=MESSAGES["archive"])
            command.add_argument("--rid", choices=["linux-x64", "win-x64"], required=True, help=MESSAGES["rid"])
    args = parser.parse_args()
    root = args.root.resolve()
    try:
        if args.operation == "stage":
            stage(root, args.archive.resolve(), args.rid, args.commit)
            print(MESSAGES["staged"], args.commit)
        elif args.operation == "activate":
            activate(root, args.commit)
            print(MESSAGES["activated"], args.commit)
        elif args.operation == "run":
            return run(root)
        else:
            value = read_current(root)
            print(json.dumps(value) if value else MESSAGES["none"])
        return 0
    except (OSError, ValueError, KeyError, TypeError):
        print(MESSAGES["failed"])
        return 1

if __name__ == "__main__":
    raise SystemExit(main())
