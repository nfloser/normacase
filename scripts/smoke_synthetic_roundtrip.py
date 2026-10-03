"""Synthetic clean-install/restart/restore rehearsal. Never use a patient database.

Caller supplies NORMACASE_POSTGRES_TEST_CONNECTION to a dedicated synthetic DB.
create: creates fresh cases and writes an exact expected-state manifest.
verify: replays the manifest against the caller-selected restored database.
No database deletion, connection-string output, or credential persistence.
"""
import base64
import json
import os
from pathlib import Path
import secrets
import subprocess
import sys
import time
import urllib.error
import urllib.request
import uuid

ROOT = Path(__file__).resolve().parents[1]
BASE = "http://127.0.0.1:5080"


def request(path, payload=None, expected=200, token=None, content_type="application/json"):
    headers = {"Content-Type": content_type}
    if token:
        headers["Authorization"] = "Bearer " + token
    body = None if payload is None else (payload.encode("utf-8") if isinstance(payload, str) else json.dumps(payload).encode("utf-8"))
    try:
        with urllib.request.urlopen(urllib.request.Request(BASE + path, body, headers), timeout=10) as response:
            status, data = response.status, response.read()
    except urllib.error.HTTPError as error:
        status, data = error.code, error.read()
    if status != expected:
        raise RuntimeError(f"Synthetic HTTP acceptance failed: {path} expected {expected}, received {status}")
    return json.loads(data)


def run(mode, manifest_path):
    connection = os.environ.get("NORMACASE_POSTGRES_TEST_CONNECTION")
    if not connection:
        raise RuntimeError("Dedicated synthetic PostgreSQL configuration required")
    manifest_path = manifest_path.resolve()
    outbound_directory = manifest_path.parent / "synthetic-outbound"
    outbound_directory.mkdir(parents=True, exist_ok=True)
    token = base64.b64encode(secrets.token_bytes(32)).decode("ascii")
    env = os.environ.copy()
    env.update({"SyntheticReview__Enabled": "true", "SyntheticReview__PersistenceEnabled": "true",
                "SyntheticReview__Credential": token, "ConnectionStrings__SyntheticReview": connection,
                "SyntheticReview__OutboundDirectory": str(outbound_directory)})
    command = ["dotnet", str(ROOT / "src/NormaCase.Api/bin/Release/net10.0/NormaCase.Api.dll")]
    process = subprocess.Popen(command, env=env, cwd=ROOT, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    try:
        for _ in range(100):
            if process.poll() is not None:
                raise RuntimeError("Synthetic host exited during startup")
            try:
                request("/api/review-session", token=token)
                break
            except (urllib.error.URLError, TimeoutError):
                time.sleep(0.1)
        else:
            raise RuntimeError("Synthetic host did not become ready")
        if mode == "create":
            entries = []
            for suffix, case_name in [("accepted", "demo-g-supported"), ("overridden", "demo-g-not-supported"),
                                      ("incomplete", "demo-g-incomplete"), ("review", "demo-g-review")]:
                payload = {"formatVersion": 1, "order": "smoke-" + uuid.uuid4().hex, "message": "input-" + suffix,
                           "revision": "1", "input": json.loads((ROOT / "examples/cases" / (case_name + ".json")).read_text())}
                request("/api/review/intake/json", payload, expected=401)
                intake = request("/api/review/intake/json", payload, token=token)
                case = intake["workCase"]
                case_id = case["caseId"]
                if intake["acceptance"] != "ACCEPTED":
                    raise RuntimeError("Clean synthetic case was not accepted")
                duplicate = request("/api/review/intake/json", payload, token=token)
                assert duplicate["acceptance"] == "DUPLICATE"
                export = {"messageId": "result-" + payload["order"], "correlationId": "correlation-" + payload["order"],
                          "destinationId": "synthetic-inbox", "expectedCaseRevision": "1",
                          "expectedProcessRevision": "1", "expectedAuditRevision": "1"}
                request(f"/api/review/work-cases/{case_id}/outbound", export, expected=403, token=token)
                result = None
                if suffix in ("accepted", "overridden"):
                    review = {"expectedCaseRevision": "1", "expectedProcessRevision": "1", "expectedAuditRevision": "1",
                              "disposition": "ACCEPT_SYSTEM_RESULT", "reason": "Synthetische Wiederherstellungsprobe"}
                    if suffix == "overridden":
                        review.update(disposition="OVERRIDE", overrideOutcome="SUPPORTED")
                    case = request(f"/api/review/work-cases/{case_id}/reviews", review, token=token)
                    request(f"/api/review/work-cases/{case_id}/outbound", export, expected=409, token=token)
                    export.update(expectedProcessRevision="2", expectedAuditRevision="2")
                    receipt = request(f"/api/review/work-cases/{case_id}/outbound", export, token=token)
                    result = receipt["resultJson"]
                    assert json.loads(result)["result"]["correlationId"] == export["correlationId"]
                    assert request(f"/api/review/work-cases/{case_id}/outbound", export, token=token) == receipt
                    file_export = dict(export, destinationId="synthetic-file")
                    assert request(f"/api/review/work-cases/{case_id}/outbound", file_export, token=token)["resultJson"] == result
                else:
                    assert not case["allowedActions"]
                entries.append({"payload": payload, "case": case, "export": export, "result": result})
            manifest_path.write_text(json.dumps(entries, ensure_ascii=False, indent=2), encoding="utf-8")
        elif mode == "verify":
            entries = json.loads(manifest_path.read_text(encoding="utf-8"))
            for entry in entries:
                historical = request("/api/review/intake/json", entry["payload"], token=token)
                assert historical["acceptance"] == "DUPLICATE"
                assert historical["workCase"] == entry["case"]
                case_id = entry["case"]["caseId"]
                if entry["result"] is None:
                    request(f"/api/review/work-cases/{case_id}/outbound", entry["export"], expected=403, token=token)
                else:
                    for destination in ("synthetic-inbox", "synthetic-file"):
                        result = request(f"/api/review/work-cases/{case_id}/outbound", dict(entry["export"], destinationId=destination), token=token)
                        assert result["isCommitted"] and result["resultJson"] == entry["result"]
                    assert entry["result"] in [p.read_text(encoding="utf-8") for p in outbound_directory.glob("*.json")]
        else:
            raise RuntimeError("Mode must be create or verify")
        queues = request("/api/review/work-queues", token=token)
        ids = {item["caseId"] for queue in queues["queues"] for item in queue["items"]}
        assert all(entry["case"]["caseId"] in ids for entry in entries)
        print(f"Synthetic {mode} rehearsal passed: four routing outcomes, original history, two outbound destinations")
    finally:
        process.terminate()
        try:
            process.wait(timeout=10)
        except subprocess.TimeoutExpired:
            process.kill()
            process.wait(timeout=10)


if __name__ == "__main__":
    if len(sys.argv) != 3:
        raise SystemExit("Usage: smoke_synthetic_roundtrip.py create|verify SYNTHETIC_MANIFEST_PATH")
    run(sys.argv[1], Path(sys.argv[2]))
