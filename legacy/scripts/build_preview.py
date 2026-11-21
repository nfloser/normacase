"""Package self-contained synthetic previews after the bundled frontend build."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import tempfile
import zipfile

from preview_platform import SUPPORTED_RIDS

ROOT = Path(__file__).resolve().parents[1]


def digest(path):
    with path.open("rb") as source:
        return hashlib.file_digest(source, "sha256").hexdigest()


def build(rid, commit, output):
    if rid not in SUPPORTED_RIDS or not re.fullmatch(r"[0-9a-f]{40}", commit):
        raise ValueError("A supported runtime and complete source commit are required")
    if not (ROOT / "src/NormaCase.Api/wwwroot/index.html").is_file():
        raise ValueError("Build the bundled frontend before packaging")
    angular = ROOT / "angular-pilot/dist/angular-pilot/browser"
    if not (angular / "index.html").is_file():
        raise ValueError("Build the Angular workplace before packaging")
    output = output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    archive = output / ("normacase-synthetic-preview-" + rid + ".zip")
    if archive.exists():
        raise FileExistsError("Preview archive already exists")
    platform = "0.1.0-preview+" + commit
    with tempfile.TemporaryDirectory(prefix="normacase-publish-") as temporary:
        bundle = Path(temporary)
        for project, folder in [("Api", "api"), ("Cli", "cli")]:
            subprocess.run([
                "dotnet", "publish", str(ROOT / "src" / ("NormaCase." + project)),
                "--configuration", "Release", "--runtime", rid, "--self-contained", "true",
                "--output", str(bundle / folder), "-p:PublishSingleFile=false",
                "-p:PublishTrimmed=false", "-p:DebugType=None", "-p:DebugSymbols=false",
                "-p:Version=0.1.0-preview", "-p:InformationalVersion=" + platform,
                "-p:IncludeSourceRevisionInInformationalVersion=false",
            ], cwd=ROOT, check=True)
        # The Angular workplace is served by the same loopback host beside the existing workbench.
        shutil.copytree(angular, bundle / "api" / "wwwroot" / "angular-pilot")
        shutil.copytree(ROOT / "knowledge", bundle / "knowledge")
        shutil.copytree(ROOT / "examples", bundle / "examples")
        # Package only runtime knowledge, not engineering instructions.
        for instructions in (bundle / "knowledge").rglob("AGENTS.md"):
            instructions.unlink()
        shutil.copy2(ROOT / "docs/development/PREVIEW_START.de.md", bundle / "START.de.md")
        shutil.copy2(ROOT / "docs/development/PITCH_DEMO.de.md", bundle / "PITCH-DEMO.de.md")
        shutil.copytree(ROOT / "ops", bundle / "ops")
        shutil.copytree(ROOT / "docs", bundle / "docs")
        shutil.copy2(ROOT / "compose.synthetic-review.yml", bundle / "compose.synthetic-review.yml")
        scripts = bundle / "scripts"
        scripts.mkdir()
        for name in ("manage_installation.py", "installation.de.json", "check_preview.py", "workflow_smoke.py", "preview_platform.py"):
            shutil.copy2(ROOT / "scripts" / name, scripts / name)
        shutil.copy2(ROOT / "docs/development/OPERATIONAL_INSTALLATION.de.md", bundle / "BETRIEB.de.md")
        launchers = ROOT / "scripts" / "preview-launchers"
        if rid == "win-x64":
            # cmd.exe expects CRLF line endings; normalize regardless of the checkout settings.
            for name, target in [("Pruefwerkstatt-starten.cmd", bundle / "Pruefwerkstatt-starten.cmd"),
                                 ("browser-oeffnen.cmd", scripts / "browser-oeffnen.cmd")]:
                text = (launchers / name).read_text(encoding="utf-8").replace("\r\n", "\n")
                if not text.isascii():
                    raise ValueError("Windows launchers must stay ASCII for every console code page")
                target.write_bytes(text.replace("\n", "\r\n").encode("ascii"))
        else:
            script = bundle / "pruefwerkstatt-starten.sh"
            script.write_text((launchers / "pruefwerkstatt-starten.sh").read_text(encoding="utf-8").replace("\r\n", "\n"),
                              encoding="utf-8", newline="\n")
            script.chmod(0o755)
        files = {path.relative_to(bundle).as_posix(): digest(path)
                 for path in sorted(bundle.rglob("*")) if path.is_file()}
        manifest = {"formatVersion": 1, "runtimeIdentifier": rid,
                    "sourceCommit": commit, "platformVersion": platform,
                    "validationLevel": "SYNTHETIC", "files": files}
        (bundle / "preview.json").write_text(
            json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        with zipfile.ZipFile(archive, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=6) as package:
            for path in sorted(bundle.rglob("*")):
                if path.is_file():
                    package.write(path, path.relative_to(bundle).as_posix())
    archive.with_suffix(".zip.sha256").write_text(
        digest(archive) + "  " + archive.name + "\n", encoding="ascii")
    print("Created synthetic preview: " + archive.name)


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--rid", choices=SUPPORTED_RIDS, required=True)
    parser.add_argument("--commit", required=True)
    parser.add_argument("--output", type=Path, default=ROOT / "artifacts")
    arguments = parser.parse_args()
    build(arguments.rid, arguments.commit, arguments.output)
