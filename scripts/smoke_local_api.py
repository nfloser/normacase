"""Smoke-test the actual synthetic local HTTP executable; print no case content."""
import json
import os
from pathlib import Path
import subprocess
import time
from urllib.error import URLError, HTTPError
from urllib.request import Request, urlopen

process = subprocess.Popen(
    ["dotnet", "src/NormaCase.Api/bin/Release/net10.0/NormaCase.Api.dll"],
    stdout=subprocess.DEVNULL,
    stderr=subprocess.DEVNULL,
    env={**os.environ, "ASPNETCORE_ENVIRONMENT": "Production"},
)
try:
    for attempt in range(80):
        try:
            with urlopen("http://localhost:5080/api/packs", timeout=1) as response:
                catalog = json.load(response)
                assert len(catalog) == 4
                assert all(pack["validationLevel"] == "SYNTHETIC" for pack in catalog)
                assert response.headers["Cache-Control"] == "no-store"
            break
        except URLError:
            if process.poll() is not None:
                raise RuntimeError("Local API terminated before readiness.")
            time.sleep(0.25)
    else:
        raise RuntimeError("Local API did not become ready.")

    request = Request(
        "http://localhost:5080/api/assessments/synthetic.demo-d",
        data=Path("examples/cases/demo-d-supported.json").read_bytes(),
        headers={"Content-Type": "application/json"},
    )
    with urlopen(request, timeout=5) as response:
        result = json.load(response)
        assert result["assessment"]["outcome"] == "SUPPORTED"
        assert result["assessment"]["assessmentDate"] == "2026-10-02"
        assert response.headers["X-Content-Type-Options"] == "nosniff"

    denied = Request("http://localhost:5080/api/packs", headers={"Origin": "https://example.invalid"})
    try:
        urlopen(denied, timeout=5)
        raise AssertionError("Cross-origin request was accepted.")
    except HTTPError as error:
        assert error.code == 403
    print("Local API smoke check passed.")
finally:
    process.terminate()
    try:
        process.wait(timeout=5)
    except subprocess.TimeoutExpired:
        process.kill()
        process.wait(timeout=5)
