"""Synthetic regression for immutable staging and fail-closed pointer switches."""
import hashlib
import json
from pathlib import Path
import socket
import tempfile
import unittest
import zipfile
import manage_installation as installation

class InstallationTests(unittest.TestCase):
    def bundle(self, root, commit):
        archive=root/(commit+".zip")
        content=b"synthetic native fixture"
        manifest={"formatVersion":1,"sourceCommit":commit,"runtimeIdentifier":"linux-x64",
                  "platformVersion":"0.1.0-preview+"+commit,"validationLevel":"SYNTHETIC",
                  "files":{"api/NormaCase.Api":hashlib.sha256(content).hexdigest()}}
        with zipfile.ZipFile(archive,"w") as package:
            package.writestr("api/NormaCase.Api",content)
            package.writestr("preview.json",json.dumps(manifest))
        archive.with_suffix(".zip.sha256").write_text(hashlib.sha256(archive.read_bytes()).hexdigest()+"  "+archive.name+"\n")
        return archive

    def test_staging_retains_old_version_and_active_pointer(self):
        with tempfile.TemporaryDirectory() as temporary:
            root=Path(temporary);target=root/"installed"
            for commit in ("a"*40,"b"*40):
                installation.stage(target,self.bundle(root,commit),"linux-x64",commit)
            installation.activate(target,"a"*40)
            first=(target/"current.json").read_bytes()
            self.assertTrue((target/"releases"/("b"*40)).is_dir())
            self.assertEqual(first,(target/"current.json").read_bytes())
            installation.activate(target,"b"*40)
            self.assertEqual("a"*40,installation.read_current(target)["previousCommit"])
            self.assertTrue((target/"releases"/("a"*40)).is_dir())

    def test_repeated_activation_preserves_exact_pointer_and_previous_release(self):
        with tempfile.TemporaryDirectory() as temporary:
            root=Path(temporary);target=root/"installed"
            for commit in ("a"*40,"b"*40):
                installation.stage(target,self.bundle(root,commit),"linux-x64",commit)
                installation.activate(target,commit)
                original=(target/"current.json").read_bytes()
                expected=installation.read_current(target)
                self.assertEqual(expected,installation.activate(target,commit))
                self.assertEqual(original,(target/"current.json").read_bytes())
            self.assertEqual("a"*40,installation.read_current(target)["previousCommit"])

    def test_wrong_commit_tampering_and_live_service_never_replace_active_pointer(self):
        with tempfile.TemporaryDirectory() as temporary:
            root=Path(temporary);target=root/"installed";commit="a"*40
            installation.stage(target,self.bundle(root,commit),"linux-x64",commit)
            installation.activate(target,commit)
            original=(target/"current.json").read_bytes()
            with self.assertRaises(ValueError):
                installation.stage(target,self.bundle(root,"b"*40),"linux-x64","c"*40)
            with installation.lock(target,"service"):
                with self.assertRaises(FileExistsError):
                    installation.activate(target,commit)
            with socket.socket() as listener:
                listener.bind(("127.0.0.1",5080));listener.listen()
                with self.assertRaises(ValueError):
                    installation.activate(target,commit)
            (target/"releases"/commit/"api/NormaCase.Api").write_bytes(b"tampered")
            with self.assertRaises(ValueError):
                installation.activate(target,commit)
            self.assertEqual(original,(target/"current.json").read_bytes())

    def test_bad_archive_hash_never_stages_a_release(self):
        with tempfile.TemporaryDirectory() as temporary:
            root=Path(temporary);target=root/"installed";commit="a"*40
            archive=self.bundle(root,commit)
            archive.write_bytes(archive.read_bytes()+b"changed")
            with self.assertRaises(ValueError):
                installation.stage(target,archive,"linux-x64",commit)
            self.assertFalse((target/"releases"/commit).exists())

if __name__=="__main__":
    unittest.main()
