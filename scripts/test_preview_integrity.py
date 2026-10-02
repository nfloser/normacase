"""Synthetic corruption and extraction regressions for preview packages."""
import hashlib
import json
from pathlib import Path
import tempfile
import unittest
import zipfile

from check_preview import extract_verified


class PreviewIntegrityTests(unittest.TestCase):
    def make_bundle(self, directory, extra=None, digest=None):
        archive = directory / "preview.zip"
        content = b"synthetic fixture"
        manifest = {"files": {"api/fixture": digest or hashlib.sha256(content).hexdigest()}}
        with zipfile.ZipFile(archive, "w") as bundle:
            bundle.writestr("preview.json", json.dumps(manifest))
            bundle.writestr("api/fixture", content)
            if extra is not None:
                # ZipInfo normalizes the native Windows separator at construction.
                # Preserve malicious bytes as they would arrive in an imported ZIP.
                entry = zipfile.ZipInfo(extra)
                entry.filename = extra
                bundle.writestr(entry, "synthetic addition")
        target = directory / "target"
        target.mkdir()
        return archive, target

    def test_valid_inventory_is_verified(self):
        with tempfile.TemporaryDirectory() as temporary:
            archive, target = self.make_bundle(Path(temporary))
            extract_verified(archive, target)
            self.assertEqual(b"synthetic fixture", (target / "api/fixture").read_bytes())

    def test_changed_bytes_are_rejected(self):
        with tempfile.TemporaryDirectory() as temporary:
            archive, target = self.make_bundle(Path(temporary), digest="0" * 64)
            with self.assertRaisesRegex(ValueError, "integrity"):
                extract_verified(archive, target)

    def test_unlisted_file_is_rejected(self):
        with tempfile.TemporaryDirectory() as temporary:
            archive, target = self.make_bundle(Path(temporary), extra="api/unlisted")
            with self.assertRaisesRegex(ValueError, "inventory"):
                extract_verified(archive, target)

    def test_unsafe_paths_are_rejected_before_extraction(self):
        for path in ["../outside", "/outside", "C:/outside", "api\\outside"]:
            with self.subTest(path=path), tempfile.TemporaryDirectory() as temporary:
                directory = Path(temporary)
                archive, target = self.make_bundle(directory, extra=path)
                with self.assertRaisesRegex(ValueError, "path|inventory"):
                    extract_verified(archive, target)
                self.assertEqual([], list(target.iterdir()))
                self.assertFalse((directory / "outside").exists())

    def test_symlink_is_rejected_before_extraction(self):
        with tempfile.TemporaryDirectory() as temporary:
            archive, target = self.make_bundle(Path(temporary))
            with zipfile.ZipFile(archive, "a") as bundle:
                link = zipfile.ZipInfo("link")
                link.create_system = 3
                link.external_attr = 0o120777 << 16
                bundle.writestr(link, "../outside")
            with self.assertRaisesRegex(ValueError, "path"):
                extract_verified(archive, target)
            self.assertEqual([], list(target.iterdir()))


if __name__ == "__main__":
    unittest.main()
