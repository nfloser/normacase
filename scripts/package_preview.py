"""Assemble a synthetic self-contained NormaCase preview directory."""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import shutil


def copy_directory(source: Path, target: Path) -> None:
    if not source.is_dir():
        raise SystemExit(f"Missing directory: {source}")
    shutil.copytree(source, target)


def write_text(path: Path, text: str, executable: bool = False) -> None:
    path.write_text(text, encoding="utf-8", newline="\\n")
    if executable:
        path.chmod(path.stat().st_mode | 0o111)


def build_manifest(root: Path) -> str:
    lines: list[str] = []
    for path in sorted(
        (item for item in root.rglob("*") if item.is_file()),
        key=lambda item: item.relative_to(root).as_posix(),
    ):
        if path.name == "SHA256SUMS.txt":
            continue
        digest = hashlib.sha256(path.read_bytes()).hexdigest()
        lines.append(f"{digest}  {path.relative_to(root).as_posix()}")
    return "\\n".join(lines) + "\\n"


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--rid", required=True, choices=("linux-x64", "win-x64"))
    parser.add_argument("--source-commit", required=True)
    parser.add_argument("--platform-version", required=True)
    parser.add_argument("--api-dir", type=Path, required=True)
    parser.add_argument("--cli-dir", type=Path, required=True)
    parser.add_argument("--repo-root", type=Path, required=True)
    parser.add_argument("--output-dir", type=Path, required=True)
    args = parser.parse_args()

    root = args.output_dir.resolve()
    if root.exists():
        shutil.rmtree(root)
    root.mkdir(parents=True)

    copy_directory(args.api_dir.resolve(), root / "api")
    copy_directory(args.cli_dir.resolve(), root / "cli")
    copy_directory(args.repo_root.resolve() / "knowledge", root / "knowledge")
    (root / "examples").mkdir()
    copy_directory(
        args.repo_root.resolve() / "examples" / "cases",
        root / "examples" / "cases",
    )

    build_info = {
        "formatVersion": 1,
        "sourceCommit": args.source_commit,
        "platformVersion": args.platform_version,
        "runtimeIdentifier": args.rid,
        "syntheticOnly": True,
    }
    write_text(
        root / "build-info.json",
        json.dumps(build_info, indent=2, ensure_ascii=False) + "\\n",
    )

    start_instruction = (
        "Doppelklick auf start-normacase.cmd oder im Terminal start-normacase.cmd."
        if args.rid == "win-x64"
        else "Im Terminal ./start-normacase.sh ausführen."
    )
    readme = f"""# NormaCase – synthetische Preview

Dieses Archiv ist eine synthetische technische Vorschau. Es ist nicht für echte
Patienten-/Sozialdaten, produktive Begutachtungen oder fachlich freigegebene
Entscheidungen bestimmt.

Plattformstand: {args.platform_version}
Quellcommit: {args.source_commit}
Zielsystem: {args.rid}

## Start

{start_instruction}

Danach ist die deutsche Prüfwerkstatt ausschließlich lokal unter
http://localhost:5080/ erreichbar. Beenden mit Strg+C im Serverfenster.

Es werden keine Cloud-Dienste, externen Fonts, Telemetrie oder Runtime-
Internetverbindungen benötigt. Eine lokale PostgreSQL-Instanz wird für diese
synthetische Preview nicht benötigt.

## CLI

Das CLI liegt unter cli/ und benötigt ebenfalls keine installierte .NET-Runtime.
Für reproduzierbare Snapshot-Aufrufe muss --platform-version exakt
{args.platform_version} sein.

Beispiel Linux:
./cli/NormaCase.Cli snapshot --pack knowledge/demo-e/pack.json --case examples/cases/demo-e-partial.json --platform-version {args.platform_version}

Beispiel Windows:
cli\\NormaCase.Cli.exe snapshot --pack knowledge\\demo-e\\pack.json --case examples\\cases\\demo-e-partial.json --platform-version {args.platform_version}

## Inhalt und Integrität

- api/: self-contained lokaler ASP.NET-Core-Dienst inklusive deutscher Workbench
- cli/: self-contained Offline-CLI
- knowledge/: fünf synthetische Knowledge Packs samt Präsentationsmetadaten
- examples/cases/: synthetische Beispielfälle
- build-info.json: exakter Quell-/Plattformstand
- SHA256SUMS.txt: SHA-256 jedes Bundle-Inhalts

Die Prüfsummen erkennen Übertragungs-/Dateifehler, sind aber keine digitale
Signatur und beweisen keine Herkunft oder fachliche Freigabe.
"""
    write_text(root / "README-DE.md", readme)

    if args.rid == "win-x64":
        write_text(
            root / "start-normacase.cmd",
            """@echo off
setlocal
cd /d "%~dp0"
echo NormaCase synthetische Preview
echo Lokal: http://localhost:5080/
echo Beenden: Strg+C
echo.
"api\\NormaCase.Api.exe"
""",
        )
        write_text(
            root / "normacase-cli.cmd",
            """@echo off
setlocal
cd /d "%~dp0"
"cli\\NormaCase.Cli.exe" %*
""",
        )
    else:
        write_text(
            root / "start-normacase.sh",
            """#!/usr/bin/env sh
set -eu
cd "$(dirname "$0")"
printf '%s\\n' 'NormaCase synthetische Preview'
printf '%s\\n' 'Lokal: http://localhost:5080/'
printf '%s\\n\\n' 'Beenden: Strg+C'
exec ./api/NormaCase.Api
""",
            executable=True,
        )
        write_text(
            root / "normacase-cli.sh",
            """#!/usr/bin/env sh
set -eu
cd "$(dirname "$0")"
exec ./cli/NormaCase.Cli "$@"
""",
            executable=True,
        )

    write_text(root / "SHA256SUMS.txt", build_manifest(root))


if __name__ == "__main__":
    main()
