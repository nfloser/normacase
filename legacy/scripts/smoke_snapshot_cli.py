"""Exercise the real synthetic snapshot executable without logging snapshot content."""
import json
import pathlib
import subprocess
import tempfile

command = ["dotnet", "src/NormaCase.Cli/bin/Release/net10.0/NormaCase.Cli.dll"]

def run(*args):
    return subprocess.run(command + list(args), capture_output=True, text=True, timeout=30)

with tempfile.TemporaryDirectory() as directory:
    capture = run("snapshot", "--pack", "knowledge/demo-e/pack.json", "--case",
                  "examples/cases/demo-e-partial.json", "--platform-version", "ci")
    assert capture.returncode == 0 and not capture.stderr, "Snapshot capture failed"
    original = json.loads(capture.stdout)
    path = pathlib.Path(directory) / "snapshot.json"
    path.write_text(capture.stdout, encoding="utf-8")
    replay = run("replay", "--snapshot", str(path), "--platform-version", "ci", "--json")
    assert replay.returncode == 0 and not replay.stderr, "Snapshot replay failed"
    assert json.loads(replay.stdout) == original["assessment"], "Replay changed the assessment"
    wrong = run("replay", "--snapshot", str(path), "--platform-version", "wrong-private-version")
    assert wrong.returncode == 4 and not wrong.stdout, "Wrong platform was accepted"
    assert "wrong-private-version" not in wrong.stderr and str(path) not in wrong.stderr
    original["input"]["assessmentDate"] = "2025-01-01"
    path.write_text(json.dumps(original), encoding="utf-8")
    altered = run("replay", "--snapshot", str(path), "--platform-version", "ci")
    assert altered.returncode == 2 and not altered.stdout, "Altered snapshot was accepted"

print("Synthetic snapshot capture/replay and private rejection checks passed.")
