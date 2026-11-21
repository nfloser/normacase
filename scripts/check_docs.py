"""Check that relative Markdown links outside legacy/ point to existing files."""
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
LINK = re.compile(r"\]\(([^)#\s]+)(?:#[^)]*)?\)")

errors = []
for md in ROOT.rglob("*.md"):
    rel = md.relative_to(ROOT)
    if rel.parts[0] in {"legacy", ".git"} or {"node_modules", "target", "dist"} & set(rel.parts):
        continue
    for target in LINK.findall(md.read_text(encoding="utf-8")):
        if "://" in target or target.startswith("mailto:"):
            continue
        if not (md.parent / target).exists():
            errors.append(f"{rel}: broken link {target}")

print("\n".join(errors) or "docs: all relative links resolve")
sys.exit(1 if errors else 0)
