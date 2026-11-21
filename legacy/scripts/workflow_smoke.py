"""Exercise a real local workflow process without printing run content."""
import json
from urllib.error import HTTPError
from urllib.request import Request, urlopen


def exercise_workflow(base):
    def post(path, payload):
        request = Request(base + path, data=json.dumps(payload).encode("utf-8"),
                          headers={"Content-Type": "application/json"})
        with urlopen(request, timeout=10) as response:
            assert response.headers["Cache-Control"] == "no-store"
            return json.load(response)

    initial = post("/api/workflows/synthetic.demo-f/start", {
        "workflowId": "synthetic.review", "workflowVersion": 1,
        "runId": "run-synthetic-smoke", "caseId": "case-synthetic-smoke",
        "actorId": "synthetic-creator", "recordedAtUtc": "2026-10-03T12:00:00Z",
        "reason": "Synthetischer Start",
    })
    assert initial["view"]["stateLabel"] == "In Vorbereitung"
    assert initial["view"]["revision"] == "0"

    def advance(run, revision, transition):
        return post("/api/workflows/advance", {
            "runJson": run["runJson"], "expectedRevision": revision,
            "transitionId": transition, "actorId": "synthetic-reviewer",
            "recordedAtUtc": "2026-10-03T12:01:00Z", "reason": "Synthetische Prüfung",
        })

    review = advance(initial, 0, "submit")
    assert review["view"]["stateLabel"] == "In Prüfung"
    restored = post("/api/workflows/verify", json.loads(review["runJson"]))
    assert restored["runJson"] == review["runJson"]
    complete = advance(restored, 1, "finish")
    assert complete["view"]["stateLabel"] == "Abgeschlossen"
    assert complete["view"]["terminal"] is True
    assert complete["view"]["transitions"] == []
    assert len(complete["view"]["history"]) == 3

    try:
        advance(initial, 99, "submit")
        raise AssertionError("Stale workflow revision accepted")
    except HTTPError as error:
        assert error.code == 409

    altered = json.loads(initial["runJson"])
    altered["run"]["history"][0]["snapshot"]["source"]["title"] = "substituted synthetic source"
    try:
        post("/api/workflows/verify", altered)
        raise AssertionError("Substituted workflow source accepted")
    except HTTPError as error:
        assert error.code == 409
