"""Offline inventory validation. This tool cannot authorize a productive deployment."""
import argparse
from collections import Counter
from datetime import date
import json
from pathlib import Path, PurePosixPath
import re
import sys
from urllib.parse import urlsplit

ROOT = Path(__file__).resolve().parents[1]
DEFAULT_REGISTER = ROOT / "docs/security/requirements.v1.json"
REQUIRED_IDS = {f"SEC-{number:02d}" for number in range(1, 24)}
STATUSES = {"OPEN", "INSTITUTION_REVIEW", "SYNTHETIC_TEST_COVERAGE"}
MAX_BYTES = 512 * 1024


def _object(value, keys):
    if not isinstance(value, dict) or set(value) != set(keys):
        raise ValueError("Invalid object shape")


def _text(value):
    if not isinstance(value, str) or not value.strip() or len(value) > 4096:
        raise ValueError("Invalid bounded text")


def _list(value):
    if not isinstance(value, list) or not value or len(value) > 256:
        raise ValueError("Invalid bounded list")


def _unique(items):
    ids = []
    for item in items:
        if not isinstance(item, dict):
            raise ValueError("Invalid record")
        _text(item.get("id"))
        ids.append(item["id"])
    if len(ids) != len(set(ids)):
        raise ValueError("Duplicate identity")
    return set(ids)


def check_register(register, root=ROOT):
    """Verify link integrity and conservative status semantics, never test success."""
    _object(register, ("schema_version", "reviewed_on", "scope", "sources", "requirements"))
    if type(register["schema_version"]) is not int or register["schema_version"] != 1:
        raise ValueError("Unsupported schema")
    if register["scope"] != "SYNTHETIC_DEVELOPMENT_ONLY":
        raise ValueError("Unsupported scope")
    _text(register["reviewed_on"])
    date.fromisoformat(register["reviewed_on"])
    _list(register["sources"])
    _list(register["requirements"])
    source_ids = _unique(register["sources"])
    if not REQUIRED_IDS.issubset(_unique(register["requirements"])):
        raise ValueError("Missing baseline requirement")
    for source in register["sources"]:
        _object(source, ("id", "title", "version", "url", "kind", "review"))
        for field in ("title", "version", "url"):
            _text(source[field])
        url = urlsplit(source["url"])
        if url.scheme != "https" or not url.hostname or url.username or url.password:
            raise ValueError("Invalid source location")
        if source["kind"] not in {"LEGAL", "GUIDANCE", "STANDARD", "PROJECT_POLICY"}:
            raise ValueError("Invalid source classification")
        if source["review"] not in {"RETRIEVED_TEXT", "METADATA_ONLY", "REPOSITORY_POLICY"}:
            raise ValueError("Invalid source review")
    root = root.resolve()
    for requirement in register["requirements"]:
        _object(requirement, ("id", "requirement", "sources", "threat", "boundary", "owner", "status", "evidence", "remaining", "follow_up"))
        if not re.fullmatch(r"SEC-[0-9]{2,3}", requirement["id"]):
            raise ValueError("Invalid control identity")
        for field in ("requirement", "threat", "boundary", "remaining", "follow_up"):
            _text(requirement[field])
        if requirement["status"] not in STATUSES or requirement["owner"] not in {"NORMACASE", "OPERATOR", "SHARED"}:
            raise ValueError("Invalid responsibility or status")
        _list(requirement["sources"])
        for reference in requirement["sources"]:
            _object(reference, ("id", "section"))
            if reference["id"] not in source_ids:
                raise ValueError("Unresolved source")
            _text(reference["section"])
        _list(requirement["evidence"])
        has_test = False
        for evidence in requirement["evidence"]:
            _object(evidence, ("path", "anchor"))
            _text(evidence["path"])
            path = PurePosixPath(evidence["path"])
            if path.is_absolute() or ".." in path.parts or "\\" in evidence["path"] or ":" in evidence["path"]:
                raise ValueError("Invalid evidence path")
            local = (root / path).resolve()
            if not local.is_relative_to(root) or not local.is_file():
                raise ValueError("Missing local evidence")
            if not isinstance(evidence["anchor"], str) or len(evidence["anchor"]) > 256:
                raise ValueError("Invalid evidence anchor")
            if evidence["anchor"] and evidence["anchor"] not in local.read_text(encoding="utf-8"):
                raise ValueError("Stale evidence anchor")
            has_test |= bool(evidence["anchor"]) and (path.parts[0] == "tests" or "e2e" in path.parts or path.name.startswith("test_"))
        if requirement["status"] == "SYNTHETIC_TEST_COVERAGE" and not has_test:
            raise ValueError("Missing test reference")


def readiness_report(register):
    """The synthetic boundary is intrinsic, not a caller-supplied approval flag."""
    check_register(register)
    return {
        "deployment_status": "BLOCKED",
        "patient_data_authorized": False,
        "scope": "SYNTHETIC_DEVELOPMENT_ONLY",
        "status_counts": dict(sorted(Counter(item["status"] for item in register["requirements"]).items())),
        "blockers": [item["id"] for item in register["requirements"]],
        "message": "Keine Freigabe für echte Patientendaten. Synthetische Tests ersetzen keine institutionelle Prüfung.",
    }


def _pairs(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError("Duplicate JSON property")
        result[key] = value
    return result


def main(argv=None):
    parser = argparse.ArgumentParser(description="Sicherheitsnachweise prüfen; keine produktive Freigabe.")
    parser.add_argument("command", choices=("validate", "readiness"))
    parser.add_argument("--register", type=Path, default=DEFAULT_REGISTER)
    args = parser.parse_args(argv)
    try:
        with args.register.open("rb") as stream:
            raw = stream.read(MAX_BYTES + 1)
        if len(raw) > MAX_BYTES:
            raise ValueError("Oversized inventory")
        register = json.loads(raw.decode("utf-8"), object_pairs_hook=_pairs)
        check_register(register)
        if args.command == "validate":
            print("Anforderungskatalog und Nachweisverweise sind konsistent; keine Betriebsfreigabe.")
            return 0
        print(json.dumps(readiness_report(register), ensure_ascii=False, indent=2))
        return 2
    except (OSError, ValueError, TypeError, KeyError, RecursionError):
        # Never echo an untrusted document, path or parser/exception detail.
        print("Sicherheitskatalog ungültig oder nicht lesbar. Keine Freigabe.", file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
