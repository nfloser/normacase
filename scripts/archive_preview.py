"""Create a preview archive and a SHA-256 sidecar."""
from __future__ import annotations

import argparse
import hashlib
from pathlib import Path
import tarfile
import zipfile


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--bundle-dir", type=Path, required=True)
    parser.add_argument("--archive", type=Path, required=True)
    args = parser.parse_args()

    bundle = args.bundle_dir.resolve()
    archive = args.archive.resolve()
    archive.parent.mkdir(parents=True, exist_ok=True)
    if archive.exists():
        archive.unlink()

    if archive.name.endswith(".tar.gz"):
        with tarfile.open(archive, "w:gz") as output:
            output.add(bundle, arcname=bundle.name)
    elif archive.suffix == ".zip":
        with zipfile.ZipFile(
            archive,
            "w",
            compression=zipfile.ZIP_DEFLATED,
            compresslevel=9,
        ) as output:
            for path in sorted(bundle.rglob("*")):
                if path.is_file():
                    output.write(
                        path,
                        (Path(bundle.name) / path.relative_to(bundle)).as_posix(),
                    )
    else:
        raise SystemExit("Archive must end in .zip or .tar.gz")

    digest = hashlib.sha256(archive.read_bytes()).hexdigest()
    sidecar = archive.with_name(archive.name + ".sha256")
    sidecar.write_text(
        f"{digest}  {archive.name}\n",
        encoding="utf-8",
        newline="\n",
    )


if __name__ == "__main__":
    main()
