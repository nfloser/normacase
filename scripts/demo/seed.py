"""Fachsystem-Attrappe: liefert die fiktiven Vorgänge aus demo/cases über die Intake-API ein.

Zeigt die Headless-Integration: Client-Credentials-Token holen, Vorgänge idempotent einliefern.
Mehrfaches Ausführen ist sicher (Idempotency-Key = Vorgangsnummer).

Umgebung: NORMACASE_API, NORMACASE_TOKEN_URL, NORMACASE_CLIENT_ID, NORMACASE_CLIENT_SECRET, NORMACASE_CASES.
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
CASES = Path(os.environ.get("NORMACASE_CASES", "demo/cases"))
SYSTEM = "fachsystem-demo"


def token() -> str:
    body = urllib.parse.urlencode({"grant_type": "client_credentials", "client_id": CLIENT_ID,
                                   "client_secret": CLIENT_SECRET}).encode()
    for _ in range(30):
        try:
            with urllib.request.urlopen(urllib.request.Request(TOKEN_URL, data=body), timeout=10) as r:
                return json.load(r)["access_token"]
        except (urllib.error.URLError, ConnectionError):
            time.sleep(2)
    raise SystemExit("Anmeldedienst nicht erreichbar")


def b64(path: Path) -> str:
    return base64.b64encode(path.read_bytes()).decode()


def main() -> int:
    access = token()
    created = replayed = 0
    for folder in sorted(p for p in CASES.iterdir() if (p / "case.json").exists()):
        meta = json.loads((folder / "case.json").read_text(encoding="utf-8"))
        request = {
            "knowledgeRelease": meta["knowledgeRelease"],
            "externalReference": {"system": SYSTEM, "caseId": meta["caseNumber"]},
            "subject": {"displayName": meta["displayName"]},
            "case": json.loads((folder / "input.json").read_text(encoding="utf-8")),
            "documents": [{"id": d["id"], "title": d["title"], "mediaType": d["mediaType"],
                           "contentBase64": b64(folder / d["file"]),
                           "pagesBase64": [b64(folder / p) for p in d["pages"]]} for d in meta["documents"]],
        }
        req = urllib.request.Request(f"{API}/api/v1/intake", data=json.dumps(request).encode(), method="POST", headers={
            "Authorization": f"Bearer {access}", "Content-Type": "application/json",
            "Idempotency-Key": "demo-" + meta["caseNumber"]})
        try:
            with urllib.request.urlopen(req, timeout=30) as r:
                outcome = json.load(r).get("outcome")
                if outcome != meta["expectedOutcome"]:
                    print(f"{meta['caseNumber']}: erwartet {meta['expectedOutcome']}, erhalten {outcome}", file=sys.stderr)
                    return 1
                created += r.status == 201
                replayed += r.status == 200
        except urllib.error.HTTPError as e:
            print(f"{meta['caseNumber']}: HTTP {e.code} {e.read().decode(errors='replace')[:300]}", file=sys.stderr)
            return 1
    print(f"Fachsystem-Attrappe: {created} Vorgänge eingeliefert, {replayed} bereits vorhanden.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
