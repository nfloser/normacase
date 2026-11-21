"""Bounded failed native startup and exact recovery, using only synthetic configuration."""
import base64
import os
from pathlib import Path
import secrets
import subprocess
import sys
import time

def check(executable):
    clean={key:value for key,value in os.environ.items()
           if not key.lower().startswith(("syntheticreview__","syntheticreview:",
                                          "connectionstrings__syntheticreview","connectionstrings:syntheticreview"))}
    environment={**clean,
        "SyntheticReview__Enabled":"true","SyntheticReview__PersistenceEnabled":"true",
        "SyntheticReview__Users__failure__Credential":base64.b64encode(secrets.token_bytes(32)).decode("ascii"),
        "SyntheticReview__Users__failure__Actions__0":"READ",
        "SyntheticReview__Users__failure__CaseIds__0":"demo-g-supported",
        "ConnectionStrings__SyntheticReview":"Host=127.0.0.1;Port=1;Database=synthetic_unavailable;Username=synthetic;Timeout=2;Pooling=false"}
    environment.pop("SyntheticReview__Credential",None)
    process=subprocess.Popen([str(executable.resolve())],cwd=executable.parent,env=environment,
                             stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL)
    try:
        try:
            code=process.wait(timeout=15)
        except subprocess.TimeoutExpired:
            raise RuntimeError("Synthetic invalid installation did not fail within its bound")
        if code==0:
            raise RuntimeError("Synthetic unavailable database unexpectedly started")
        print("Synthetic native startup failure was bounded; no valid database was touched")
    finally:
        if process.poll() is None:
            process.kill();process.wait(timeout=5)

if __name__=="__main__":
    if len(sys.argv)!=2:
        raise SystemExit("Usage: smoke_failed_startup.py EXACT_SYNTHETIC_NATIVE_EXECUTABLE")
    check(Path(sys.argv[1]).resolve())
