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
        if rid == "win-x64":
            launcher = '@echo off\r\nchcp 65001 >nul\r\ncd /d "%~dp0api"\r\necho NormaCase - synthetische Pruefwerkstatt\r\necho Browser: http://localhost:5080\r\necho Beenden: Strg+C. Nur synthetische Daten verwenden.\r\nNormaCase.Api.exe\r\nset "exitCode=%errorlevel%"\r\nif not "%exitCode%"=="0" (\r\n  echo Der lokale Dienst konnte nicht gestartet werden. Ist Port 5080 bereits belegt?\r\n  pause\r\n)\r\nexit /b %exitCode%\r\n'
            (bundle / "Pruefwerkstatt-starten.cmd").write_bytes(launcher.encode("utf-8"))
        else:
            launcher = '#!/bin/sh\nset -eu\ncd -- "$(dirname -- "$0")/api"\nprintf "%s\\n" "NormaCase - synthetische Prüfwerkstatt" "Browser: http://localhost:5080" "Beenden: Strg+C. Nur synthetische Daten verwenden."\nexec ./NormaCase.Api\n'
            script = bundle / "pruefwerkstatt-starten.sh"
            script.write_text(launcher, encoding="utf-8")
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
