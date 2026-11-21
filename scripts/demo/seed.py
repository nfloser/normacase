"""Synthetisches Fachsystem: liefert die Fallakten aus legacy/examples/document-cases über die Intake-API ein.

Zeigt die Headless-Integration: Client-Credentials-Token holen, Vorgänge idempotent einliefern.
Mehrfaches Ausführen ist sicher (Idempotency-Key = Fall-ID).

Umgebung: NORMACASE_API (z. B. http://backend:8080), NORMACASE_TOKEN_URL, NORMACASE_CLIENT_ID,
NORMACASE_CLIENT_SECRET, NORMACASE_CASES (Pfad zu document-cases), NORMACASE_KNOWLEDGE, NORMACASE_LIMIT.
Nur Standardbibliothek, damit der Container ohne Paketinstallation läuft.
"""
import base64
import json
import os
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path

API = os.environ.get("NORMACASE_API", "http://localhost:8080")
TOKEN_URL = os.environ.get("NORMACASE_TOKEN_URL", "http://localhost:8081/realms/normacase/protocol/openid-connect/token")
CLIENT_ID = os.environ.get("NORMACASE_CLIENT_ID", "normacase-host")
CLIENT_SECRET = os.environ.get("NORMACASE_CLIENT_SECRET", "synthetic-host-secret")
CASES = Path(os.environ.get("NORMACASE_CASES", "legacy/examples/document-cases"))
KNOWLEDGE = Path(os.environ.get("NORMACASE_KNOWLEDGE", "knowledge"))
LIMIT = int(os.environ.get("NORMACASE_LIMIT", "40"))


def token() -> str:
    body = urllib.parse.urlencode({"grant_type": "client_credentials", "client_id": CLIENT_ID,
                                   "client_secret": CLIENT_SECRET}).encode()
    for attempt in range(30):
        try:
            with urllib.request.urlopen(urllib.request.Request(TOKEN_URL, data=body), timeout=10) as r:
                return json.load(r)["access_token"]
        except (urllib.error.URLError, ConnectionError):
            time.sleep(2)
    raise SystemExit("Anmeldedienst nicht erreichbar")


def releases() -> dict:
    result = {}
    for pack in KNOWLEDGE.rglob("pack.json"):
        manifest = json.loads(pack.read_text(encoding="utf-8"))["manifest"]
        result[pack.parent.name] = manifest["releaseId"]
    return result


def documents(folder: Path, entry: dict) -> list:
    docs = []
    for d in entry.get("documents", []):
        content = (folder / d["filename"]).read_bytes()
        pages = [base64.b64encode((folder / p["filename"]).read_bytes()).decode()
                 for p in sorted(d.get("previews", []), key=lambda p: p["page"])]
        docs.append({"id": d["id"], "title": d["title"], "mediaType": d["mediaType"],
                     "contentBase64": base64.b64encode(content).decode(), "pagesBase64": pages})
    return docs


def main() -> int:
    catalog = json.loads((CASES / "catalog.json").read_text(encoding="utf-8"))
    known = releases()
    access = token()
    created = replayed = 0
    entries = sorted((c for c in catalog["cases"] if c.get("packPath")), key=lambda c: c["packPath"] == "demo-g")
    for number, entry in enumerate(entries[:LIMIT], start=1):
        folder = CASES / entry["caseId"]
        request = {
            "knowledgeRelease": known[entry["packPath"]],
            "externalReference": {"system": "synthetisches-fachsystem", "caseId": entry["caseId"].upper()},
            "subject": {"displayName": f"Synthetische Person {number:02d} · {entry['title']}"},
            "case": json.loads((folder / "input.json").read_text(encoding="utf-8")),
            "documents": documents(folder, entry),
        }
        data = json.dumps(request).encode()
        req = urllib.request.Request(f"{API}/api/v1/intake", data=data, method="POST", headers={
            "Authorization": f"Bearer {access}", "Content-Type": "application/json",
            "Idempotency-Key": "seed-" + entry["caseId"]})
        try:
            with urllib.request.urlopen(req, timeout=30) as r:
                if r.status == 201:
                    created += 1
                else:
                    replayed += 1
        except urllib.error.HTTPError as e:
            print(f"{entry['caseId']}: HTTP {e.code} {e.read().decode(errors='replace')[:300]}", file=sys.stderr)
            return 1
    print(f"Synthetisches Fachsystem: {created} Vorgänge eingeliefert, {replayed} bereits vorhanden.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
