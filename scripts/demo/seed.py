"""Fachsystem-Attrappe: liefert alle fiktiven Demo-Vorgänge über die Intake-API ein.

Quellen:
- demo/cases: die ausgearbeiteten Vorgänge mit Befundberichten (Pflegegrad, Krankenbeförderung)
- legacy/examples/document-cases: die Fälle der bisherigen NormaCase-Demo
  - 22 Referenzfälle (NC-2026-41xx): Krankenfahrt und Pflege mit Regelwerk, Reha, Onkologie, Arbeitsunfall,
    Überleitung, Arzneimittel und Hilfsmittel als Unterlagensichtung ohne Regelwerk
  - 95 Massenfälle als Plattformfälle (System "plattform-demo")
  - 5 technische Eingänge, die mit Prefer: handling=lenient in der technischen Klärung landen

Zeigt die Headless-Integration: Client-Credentials-Token holen, Vorgänge idempotent einliefern.
Mehrfaches Ausführen ist sicher (Idempotency-Key = Vorgangsnummer). Weicht ein Ergebnis von der Erwartung ab,
bricht das Skript ab.

Umgebung: NORMACASE_API, NORMACASE_TOKEN_URL, NORMACASE_CLIENT_ID, NORMACASE_CLIENT_SECRET, NORMACASE_CASES,
NORMACASE_LEGACY_CASES. Nur Standardbibliothek, damit der Container ohne Paketinstallation läuft.
"""
import base64
import json
import os
import re
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
LEGACY = Path(os.environ.get("NORMACASE_LEGACY_CASES", "legacy/examples/document-cases"))
SYSTEM = "fachsystem-demo"
PLATFORM_SYSTEM = "plattform-demo"

# Regelwerke der bisherigen Demo und ihre Releases in dieser Plattform.
RELEASES = {
    "kt-rl-8-3": "kt-rl-8-3-2025-08-06-public-reference.1",
    "pflege-adult-score": "pflege-adult-score-2026-10-01-public-reference.1",
    "demo-g": "demo-g-2026.1",
}
# Technische Eingänge: ein Regelwerk, das es nicht gibt, bzw. Falldaten, die nicht zum Regelwerk passen.
UNKNOWN_RELEASE = "demo-g-2025.0"


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


def own_cases():
    for folder in sorted(p for p in CASES.iterdir() if (p / "case.json").exists()):
        meta = json.loads((folder / "case.json").read_text(encoding="utf-8"))
        yield {
            "number": meta["caseNumber"], "system": SYSTEM, "displayName": meta["displayName"],
            "release": meta["knowledgeRelease"], "case": json.loads((folder / "input.json").read_text(encoding="utf-8")),
            "documents": [document(d["id"], d["title"], d["mediaType"], folder / d["file"], [folder / p for p in d["pages"]],
                                   d.get("category"), d.get("issuedOn"), d.get("references"))
                          for d in meta["documents"]],
            "expected": (meta["expectedOutcome"], None), "lenient": False,
        }


def document(doc_id, title, media_type, file, pages, category=None, issued_on=None, references=None) -> dict:
    out = {"id": doc_id, "title": title, "mediaType": media_type, "contentBase64": b64(file)}
    if pages:
        out["pagesBase64"] = [b64(p) for p in pages]
    if category:
        out["category"] = category
    if issued_on:
        out["issuedOn"] = issued_on
    if references:
        out["references"] = references
    return out


# Die bisherige Demo kennt keine Dokumentart; der Titel genügt für eine grobe Zuordnung in der Fallakte.
LEGACY_CATEGORIES = (("ORDER", ("auftrag", "antrag", "verordnung")),
                     ("PROOF", ("bescheid", "ausweis", "nachweis", "bestätigung")),
                     ("CARE", ("pflege",)),
                     ("MEDICAL", ("befund", "arzt", "bericht", "diagnose", "medikation", "therapie", "gutachten")))


def legacy_category(title: str) -> str:
    lower = title.lower()
    return next((cat for cat, words in LEGACY_CATEGORIES if any(w in lower for w in words)), "OTHER")


def legacy_documents(folder: Path, docs: list) -> list:
    return [document(d["id"], d["title"], d["mediaType"], folder / d["filename"],
                     [folder / p["filename"] for p in d.get("previews", [])], legacy_category(d["title"]))
            for d in docs]


def legacy_cases():
    if not (LEGACY / "catalog.json").exists():
        return
    catalog = json.loads((LEGACY / "catalog.json").read_text(encoding="utf-8"))
    volume = 0
    for c in catalog["cases"]:
        folder = LEGACY / c["caseId"]
        case_input = json.loads((folder / "input.json").read_text(encoding="utf-8"))
        docs = legacy_documents(folder, c["documents"])
        pack = c.get("packPath")
        if c["caseId"].startswith("reference-"):
            # Titel: "NC-2026-4101 · Marlene Berg · Krankenfahrt: ambulante Behandlung"
            number, person, subject = (part.strip() for part in c["title"].split("·", 2))
            first, last = person.rsplit(" ", 1)
            item = {"number": number, "system": SYSTEM, "displayName": f"{last}, {first} · {subject}",
                    "documents": docs, "lenient": False}
            if pack:
                item.update(release=RELEASES[pack], case=case_input, expected=(c["expectedOutcome"], None))
            else:
                item.update(release=None, case=None, expected=("NOT_ASSESSED", "DOCUMENT_REVIEW"))
            yield item
            continue
        # Massen- und Technikfälle der bisherigen Demo als Plattformfälle.
        kind = re.sub(r"^demo-(volume-)?", "", c["caseId"])
        if c["caseId"].startswith("demo-volume-"):
            volume += 1
            number = f"PF-2026-{volume:04d}"
        else:
            number = "PF-2026-" + {"g-supported": "0901", "g-not-supported": "0902", "g-incomplete": "0903",
                                    "g-review": "0904", "technical": "0905"}[kind]
        item = {"number": number, "system": PLATFORM_SYSTEM, "displayName": f"Plattformfall {number[-4:]} · {c['title']}",
                "documents": docs}
        if pack:
            item.update(release=RELEASES[pack], case=case_input, expected=(c["expectedOutcome"], None), lenient=False)
        elif c["caseId"] == "demo-technical":
            # Falldaten mit einer Angabe, die das Regelwerk nicht kennt; als Eingang des Fachsystems sichtbar.
            item.update(number="NC-2026-4190", system=SYSTEM,
                        displayName="Übermittlung mit unbekannter Angabe · technischer Eingang")
            bad = dict(case_input)
            bad["facts"] = {**case_input.get("facts", {}), "uebermittlungs_pruefsumme": {"kind": "TRUTH", "truth": "YES"}}
            item.update(release=RELEASES["demo-g"], case=bad, expected=("NOT_ASSESSED", "TECHNICAL_CLARIFICATION"),
                        lenient=True)
        else:
            item.update(release=UNKNOWN_RELEASE, case=case_input, expected=("NOT_ASSESSED", "TECHNICAL_CLARIFICATION"),
                        lenient=True)
        yield item


def submit(access: str, item: dict):
    request = {
        "externalReference": {"system": item["system"], "caseId": item["number"]},
        "subject": {"displayName": item["displayName"]},
        "documents": item["documents"],
    }
    if item["release"]:
        request["knowledgeRelease"] = item["release"]
        request["case"] = item["case"]
    headers = {"Authorization": f"Bearer {access}", "Content-Type": "application/json",
               "Idempotency-Key": "demo-" + item["number"]}
    if item["lenient"]:
        headers["Prefer"] = "handling=lenient"
    req = urllib.request.Request(f"{API}/api/v1/intake", data=json.dumps(request).encode(), method="POST", headers=headers)
    with urllib.request.urlopen(req, timeout=60) as r:
        return r.status, json.load(r)


def main() -> int:
    access = token()
    created = replayed = 0
    items = list(own_cases()) + list(legacy_cases())
    for item in items:
        try:
            status, body = submit(access, item)
        except urllib.error.HTTPError as e:
            print(f"{item['number']}: HTTP {e.code} {e.read().decode(errors='replace')[:300]}", file=sys.stderr)
            return 1
        outcome, state = item["expected"]
        if body.get("outcome") != outcome or (state and body.get("state") != state):
            print(f"{item['number']}: erwartet {outcome}/{state or '-'}, erhalten {body.get('outcome')}/{body.get('state')}",
                  file=sys.stderr)
            return 1
        created += status == 201
        replayed += status == 200
    print(f"Fachsystem-Attrappe: {created} Vorgänge eingeliefert, {replayed} bereits vorhanden ({len(items)} gesamt).")
    return 0


if __name__ == "__main__":
    sys.exit(main())
