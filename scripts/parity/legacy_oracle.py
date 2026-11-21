"""List every synthetic case with its Knowledge Pack for the parity oracle.

The frozen .NET reference (scripts/parity/oracle) evaluates this list; the
Java rule engine must reproduce the results (ADR 0005).

Usage: python scripts/parity/legacy_oracle.py > cases.json   (paths relative to the repo root)
"""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
LEGACY = ROOT / "legacy"


def entries():
    for case in sorted((LEGACY / "examples" / "cases").glob("demo-*.json")):
        pack = "-".join(case.stem.split("-")[:2])
        yield case.stem, LEGACY / "knowledge" / pack, case
    catalog = json.loads((LEGACY / "examples" / "document-cases" / "catalog.json").read_text(encoding="utf-8"))
    for entry in catalog["cases"]:
        pack = entry.get("packPath")
        if not pack:
            continue
        pack_dir = LEGACY / "knowledge" / pack
        if not pack_dir.exists():
            pack_dir = LEGACY / "knowledge" / "public-reference" / pack
        yield "document-" + entry["caseId"], pack_dir, LEGACY / "examples" / "document-cases" / entry["caseId"] / "input.json"


print(json.dumps([
    {"name": name, "pack": str((pack / "pack.json").relative_to(ROOT)), "case": str(case.relative_to(ROOT))}
    for name, pack, case in entries()
], indent=2))
