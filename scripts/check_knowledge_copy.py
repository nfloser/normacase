"""Every Knowledge Pack file of the frozen reference must exist unchanged in knowledge/.

knowledge/ may add presentation metadata (labels) that the reference kept elsewhere.
"""
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
reference = ROOT / "legacy" / "knowledge"
errors = []
for source in sorted(p for p in reference.rglob("*") if p.is_file()):
    copy = ROOT / "knowledge" / source.relative_to(reference)
    if not copy.exists():
        errors.append(f"missing: {copy.relative_to(ROOT)}")
    elif copy.read_bytes() != source.read_bytes():
        errors.append(f"changed: {copy.relative_to(ROOT)}")
print("\n".join(errors) or "knowledge/: all reference files present and unchanged")
sys.exit(1 if errors else 0)
