"""Regression tests for the security evidence inventory; synthetic metadata only."""
import copy
import json
from pathlib import Path
import subprocess
import sys
import unittest

from check_security_readiness import DEFAULT_REGISTER, ROOT, check_register, readiness_report


class SecurityReadinessTests(unittest.TestCase):
    def setUp(self):
        self.register = json.loads(DEFAULT_REGISTER.read_text(encoding="utf-8"))

    def test_retained_inventory_links_are_valid(self):
        check_register(self.register, ROOT)

    def test_current_inventory_never_authorizes_patient_data(self):
        report = readiness_report(self.register)
        self.assertFalse(report["patient_data_authorized"])
        self.assertEqual("BLOCKED", report["deployment_status"])
        self.assertEqual(len(self.register["requirements"]), len(report["blockers"]))

    def test_even_all_synthetic_tests_cannot_grant_production_approval(self):
        for requirement in self.register["requirements"]:
            requirement["status"] = "SYNTHETIC_TEST_COVERAGE"
            requirement["evidence"].append({"path": "tests/NormaCase.Application.Tests/CaseReviewServiceTests.cs", "anchor": "Authorization_denial_never_appends_or_transitions"})
        self.assertFalse(readiness_report(self.register)["patient_data_authorized"])

    def test_missing_required_control_is_rejected(self):
        self.register["requirements"].pop()
        with self.assertRaises(ValueError):
            check_register(self.register, ROOT)

    def test_duplicate_ids_are_rejected(self):
        self.register["requirements"].append(copy.deepcopy(self.register["requirements"][0]))
        with self.assertRaises(ValueError):
            check_register(self.register, ROOT)

    def test_additional_control_cannot_echo_arbitrary_text_as_an_id(self):
        additional = copy.deepcopy(self.register["requirements"][0])
        additional["id"] = "sensitive-canary"
        self.register["requirements"].append(additional)
        with self.assertRaises(ValueError):
            readiness_report(self.register)

    def test_unknown_status_and_approval_fields_are_rejected(self):
        for change in ({"status": "PRODUCTION_APPROVED"}, {"approved": True}, {"owner": "nobody"}):
            with self.subTest(change=change):
                altered = copy.deepcopy(self.register)
                altered["requirements"][0].update(change)
                with self.assertRaises(ValueError):
                    check_register(altered, ROOT)

    def test_missing_source_or_version_is_rejected(self):
        for field in ("version", "url"):
            altered = copy.deepcopy(self.register)
            altered["sources"][0][field] = ""
            with self.assertRaises(ValueError):
                check_register(altered, ROOT)
        self.register["requirements"][0]["sources"][0]["id"] = "invented"
        with self.assertRaises(ValueError):
            check_register(self.register, ROOT)

    def test_missing_remaining_work_is_rejected(self):
        self.register["requirements"][0]["remaining"] = ""
        with self.assertRaises(ValueError):
            check_register(self.register, ROOT)

    def test_evidence_cannot_escape_repository_or_fetch_network(self):
        for path in ("../SECURITY.md", "/etc/passwd", "https://example.org/evidence", "missing.txt"):
            with self.subTest(path=path):
                altered = copy.deepcopy(self.register)
                altered["requirements"][0]["evidence"] = [{"path": path, "anchor": ""}]
                with self.assertRaises(ValueError):
                    check_register(altered, ROOT)

    def test_stale_test_anchor_is_rejected(self):
        self.register["requirements"][0]["evidence"] = [{"path": "SECURITY.md", "anchor": "nonexistent_test_method"}]
        with self.assertRaises(ValueError):
            check_register(self.register, ROOT)

    def test_coverage_needs_a_test_reference(self):
        self.register["requirements"][0]["status"] = "SYNTHETIC_TEST_COVERAGE"
        self.register["requirements"][0]["evidence"] = [{"path": "SECURITY.md", "anchor": ""}]
        with self.assertRaises(ValueError):
            check_register(self.register, ROOT)

    def test_readiness_command_blocks_but_catalog_validation_passes(self):
        for command, code in (("validate", 0), ("readiness", 2)):
            result = subprocess.run([sys.executable, str(ROOT / "scripts/check_security_readiness.py"), command], capture_output=True, text=True)
            self.assertEqual(code, result.returncode, result.stderr)
            self.assertNotIn("Traceback", result.stderr)

    def test_invalid_file_does_not_echo_its_contents(self):
        import tempfile
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "invalid.json"
            path.write_text('{"sensitive-canary":', encoding="utf-8")
            result = subprocess.run([sys.executable, str(ROOT / "scripts/check_security_readiness.py"), "readiness", "--register", str(path)], capture_output=True, text=True)
            self.assertEqual(1, result.returncode)
            self.assertNotIn("sensitive-canary", result.stdout + result.stderr)

    def test_duplicate_properties_and_oversized_files_fail_closed(self):
        import tempfile
        for content in ('{"schema_version":1,"schema_version":1}', ' ' * (512 * 1024 + 1)):
            with self.subTest(size=len(content)), tempfile.TemporaryDirectory() as directory:
                path = Path(directory) / "invalid.json"
                path.write_text(content, encoding="utf-8")
                result = subprocess.run([sys.executable, str(ROOT / "scripts/check_security_readiness.py"), "readiness", "--register", str(path)], capture_output=True, text=True)
                self.assertEqual(1, result.returncode)
                self.assertNotIn("Traceback", result.stdout + result.stderr)

    def test_unknown_schema_and_production_scope_are_rejected(self):
        for change in ({"schema_version": True}, {"schema_version": 2}, {"scope": "PRODUCTION"}):
            altered = copy.deepcopy(self.register)
            altered.update(change)
            with self.subTest(change=change), self.assertRaises(ValueError):
                check_register(altered, ROOT)


if __name__ == "__main__":
    unittest.main()
