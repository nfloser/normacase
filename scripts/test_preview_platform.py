"""Platform selection must distinguish native Apple Silicon from Intel macOS."""
import unittest
from unittest.mock import patch
from preview_platform import SUPPORTED_RIDS, host_rid, runtime_library

class PlatformTests(unittest.TestCase):
    def test_native_host_selection(self):
        for system, machine, expected in [('Darwin','arm64','osx-arm64'),('Darwin','x86_64','osx-x64'),('Windows','AMD64','win-x64'),('Linux','x86_64','linux-x64')]:
            with self.subTest(system=system,machine=machine), patch('preview_platform.platform.system',return_value=system),patch('preview_platform.platform.machine',return_value=machine):
                self.assertEqual(expected,host_rid())
    def test_unknown_hosts_fail_closed(self):
        for system,machine in [('Linux','aarch64'),('Windows','ARM64'),('Darwin','unknown'),('FreeBSD','x86_64')]:
            with self.subTest(system=system,machine=machine),patch('preview_platform.platform.system',return_value=system),patch('preview_platform.platform.machine',return_value=machine):
                with self.assertRaises(ValueError):host_rid()
    def test_correct_shared_library(self):
        self.assertEqual({'linux-x64','win-x64','osx-arm64','osx-x64'},set(SUPPORTED_RIDS))
        for rid,library in [('osx-arm64','libhostfxr.dylib'),('osx-x64','libhostfxr.dylib'),('linux-x64','libhostfxr.so'),('win-x64','hostfxr.dll')]:
            self.assertEqual(library,runtime_library(rid))
        with self.assertRaises(ValueError):runtime_library('osx-unknown')

if __name__=='__main__':unittest.main()
