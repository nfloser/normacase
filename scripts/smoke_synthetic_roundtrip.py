"""Synthetic clean-install/restart/restore rehearsal. Never use a patient database.

Caller supplies NORMACASE_POSTGRES_TEST_CONNECTION to a dedicated synthetic DB.
create: creates fresh cases and writes an exact expected-state manifest.
verify: replays the manifest against the caller-selected restored database.
No database deletion, connection-string output, or credential persistence.
"""
import base64
import json
import hashlib
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
    scenarios = [("accepted", "demo-g-supported"), ("overridden", "demo-g-not-supported"),
                 ("incomplete", "demo-g-incomplete"), ("review", "demo-g-review")]
    payloads = ([{"formatVersion": 1, "order": "smoke-" + uuid.uuid4().hex, "message": "input-" + suffix,
                  "revision": "1", "input": json.loads((ROOT / "examples/cases" / (case_name + ".json")).read_text())}
                 for suffix, case_name in scenarios] if mode == "create" else
                [entry["payload"] for entry in json.loads(manifest_path.read_text(encoding="utf-8"))])
    env = os.environ.copy()
    env.update({"SyntheticReview__Enabled": "true", "SyntheticReview__PersistenceEnabled": "true",
                "SyntheticReview__Users__reviewer__Credential": token, "ConnectionStrings__SyntheticReview": connection,
                "SyntheticReview__OutboundDirectory": str(outbound_directory)})
    env.pop("SyntheticReview__Credential", None)
    for index, action in enumerate(["READ", "ACCEPT", "OVERRIDE", "INTAKE", "EXPORT", "CORRECT", "CLARIFY"]):
        env[f"SyntheticReview__Users__reviewer__Actions__{index}"] = action
    for index, payload in enumerate(payloads):
        case_id = "synthetic-intake-" + hashlib.sha256(("synthetic-json:" + payload["order"]).encode()).hexdigest()
        env[f"SyntheticReview__Users__reviewer__CaseIds__{index}"] = case_id
    migration_connection = os.environ.get("NORMACASE_POSTGRES_MIGRATION_TEST_CONNECTION")
    if migration_connection:
        env["ConnectionStrings__SyntheticReviewMigrations"] = migration_connection
    executable = os.environ.get("NORMACASE_API_TEST_EXECUTABLE")
    command = ([str(Path(executable).resolve())] if executable else
               ["dotnet", str(ROOT / "src/NormaCase.Api/bin/Release/net10.0/NormaCase.Api.dll")])
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
        installation_root = os.environ.get("NORMACASE_INSTALLATION_TEST_ROOT")
        refused_commit = os.environ.get("NORMACASE_REFUSED_SWITCH_TEST_COMMIT")
        if installation_root and refused_commit:
            from manage_installation import activate
            active_pointer = Path(installation_root) / "current.json"
            original_pointer = active_pointer.read_bytes()
            try:
                activate(Path(installation_root), refused_commit)
            except ValueError:
                pass
            else:
                raise AssertionError("Active synthetic native service allowed a release switch")
            assert active_pointer.read_bytes() == original_pointer
        if mode == "create":
            entries = []
            for (suffix, _), payload in zip(scenarios, payloads):
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
        elif mode == "upgrade":
            entries = json.loads(manifest_path.read_text(encoding="utf-8"))
            supported = (ROOT / "examples/cases/demo-g-supported.json").read_text(encoding="utf-8")
            for entry in entries:
                case_id = entry["case"]["caseId"]
                before = request(f"/api/review/work-cases/{case_id}", token=token)
                original_record = request(f"/api/review/work-cases/{case_id}/history", token=token)
                state = before["stateId"]
                policy = ("synthetic-information-completion" if state == "waiting-information" else
                          "synthetic-manual-correction" if state == "manual-review" else "synthetic-reviewed-correction")
                question_id = None
                if state in ("waiting-information", "manual-review"):
                    question_id = "question-" + uuid.uuid4().hex
                    request(f"/api/review/work-cases/{case_id}/clarifications", {
                        "clarificationId": question_id,
                        "policyId": "synthetic-missing-information" if state == "waiting-information" else "synthetic-review-questions",
                        "policyVersion": "1", "expectedCaseRevision": before["caseRevision"],
                        "expectedProcessRevision": before["processRevision"], "expectedAuditRevision": before["auditRevision"],
                        "reason": "Synthetische Aktualisierungsprobe",
                        "requestedFields": ["request_complete"] if state == "waiting-information" else [],
                        "requestedEvidence": ["supporting_document"] if state == "manual-review" else []
                    }, token=token)
                corrected = request(f"/api/review/work-cases/{case_id}/corrections", {
                    "correctionId": "correction-" + uuid.uuid4().hex,
                    "messageId": "corrected-input-" + entry["payload"]["order"], "upstreamRevision": "2",
                    "policyId": policy, "policyVersion": "1", "expectedCaseRevision": before["caseRevision"],
                    "expectedProcessRevision": before["processRevision"], "expectedAuditRevision": before["auditRevision"],
                    "reason": "Synthetische neue Revision nach Versionswechsel", "inputJson": supported,
                    "clarificationId": question_id
                }, token=token)["workCase"]
                assert corrected["caseRevision"] == "2" and len(corrected["audit"]) == 1
                assert corrected["stateId"] == "awaiting-approval"
                expected_platform = os.environ.get("NORMACASE_EXPECTED_PLATFORM_VERSION")
                if expected_platform:
                    assert json.loads(corrected["assessmentJson"])["platformVersion"] == expected_platform
                    assert json.loads(before["assessmentJson"])["platformVersion"] != expected_platform
                current = request(f"/api/review/work-cases/{case_id}/reviews", {
                    "expectedCaseRevision": "2", "expectedProcessRevision": "1", "expectedAuditRevision": "1",
                    "disposition": "ACCEPT_SYSTEM_RESULT", "reason": "Synthetische erneute Freigabe nach Aktualisierung"
                }, token=token)
                export = dict(entry["export"], messageId="corrected-result-" + entry["payload"]["order"],
                              expectedCaseRevision="2", expectedProcessRevision="2", expectedAuditRevision="2")
                current_result = request(f"/api/review/work-cases/{case_id}/outbound", export, token=token)["resultJson"]
                assert request(f"/api/review/work-cases/{case_id}/outbound", dict(export,destinationId="synthetic-file"), token=token)["resultJson"] == current_result
                entry["updated"] = {"case": current, "export": export, "result": current_result,
                                    "originalHistory": original_record,
                                    "clarifications": request(f"/api/review/work-cases/{case_id}/clarifications", token=token)}
            manifest_path.write_text(json.dumps(entries, ensure_ascii=False, indent=2), encoding="utf-8")
        elif mode == "verify":
            entries = json.loads(manifest_path.read_text(encoding="utf-8"))
            for entry in entries:
                historical = request("/api/review/intake/json", entry["payload"], token=token)
                assert historical["acceptance"] == "DUPLICATE"
                assert historical["workCase"] == entry["case"]
                case_id = entry["case"]["caseId"]
                if "updated" in entry:
                    updated = entry["updated"]
                    assert request(f"/api/review/work-cases/{case_id}", token=token) == updated["case"]
                    assert request(f"/api/review/work-cases/{case_id}/clarifications", token=token) == updated["clarifications"]
                    for original in updated["originalHistory"]["entries"]:
                        assert request(f"/api/review/work-cases/{case_id}/history/{original['version']}",token=token) == original
                    for destination in ("synthetic-inbox","synthetic-file"):
                        if entry["result"] is not None:
                            old = request(f"/api/review/work-cases/{case_id}/outbound/{destination}/{entry['export']['messageId']}",token=token)
                            assert old["resultJson"] == entry["result"]
                        current = request(f"/api/review/work-cases/{case_id}/outbound/{destination}/{updated['export']['messageId']}",token=token)
                        assert current["resultJson"] == updated["result"] and current["isCommitted"]
                    assert updated["result"] in [p.read_text(encoding="utf-8") for p in outbound_directory.glob("*.json")]
                elif entry["result"] is None:
                    request(f"/api/review/work-cases/{case_id}/outbound", entry["export"], expected=403, token=token)
                else:
                    for destination in ("synthetic-inbox", "synthetic-file"):
                        result = request(f"/api/review/work-cases/{case_id}/outbound", dict(entry["export"], destinationId=destination), token=token)
                        assert result["isCommitted"] and result["resultJson"] == entry["result"]
                    assert entry["result"] in [p.read_text(encoding="utf-8") for p in outbound_directory.glob("*.json")]
        else:
            raise RuntimeError("Mode must be create, upgrade or verify")
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
        raise SystemExit("Usage: smoke_synthetic_roundtrip.py create|upgrade|verify SYNTHETIC_MANIFEST_PATH")
    run(sys.argv[1], Path(sys.argv[2]))
