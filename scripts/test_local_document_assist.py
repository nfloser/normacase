"""No-model, no-network security and contract tests for the local assistant."""
import importlib.util
import json
from pathlib import Path
import unittest
from unittest.mock import patch

spec = importlib.util.spec_from_file_location("local_document_assist", Path(__file__).with_name("local_document_assist.py"))
assist = importlib.util.module_from_spec(spec)
spec.loader.exec_module(assist)
TEXT = "Schulungsakte - vollstaendig synthetisch.\nArztbrief vom 02.10.2026 liegt vor.\nEin Therapiebericht fehlt.\n"
GOOD = json.dumps({"hints": [{"kind": "document_mentioned", "value": "Arztbrief",
                             "quote": "Arztbrief vom 02.10.2026 liegt vor."}]})


class LocalAssistTests(unittest.TestCase):
    def test_verified_quote_is_only_a_human_review_hint(self):
        hints = assist.validate_hints(TEXT, GOOD)
        self.assertEqual([{"kind": "document_mentioned", "value": "Arztbrief",
                           "quote": "Arztbrief vom 02.10.2026 liegt vor.",
                           "sourceStart": TEXT.index("Arztbrief"),
                           "reviewState": "NEEDS_HUMAN_REVIEW"}], hints)
        self.assertNotIn("fact", hints[0])
        self.assertNotIn("outcome", hints[0])

    def test_empty_valid_hint_list_is_safe(self):
        self.assertEqual([], assist.validate_hints(TEXT, '{"hints":[]}'))

    def test_model_must_not_invent_quoted_evidence(self):
        for reply in [
            '{"hints":[{"kind":"document_mentioned","value":"Laborwert","quote":"Laborwert ist normal."}]}',
            '{"hints":[{"kind":"document_mentioned","value":"Diagnose","quote":"Arztbrief vom 02.10.2026 liegt vor."}]}',
            '{"hints":[{"kind":"approve","value":"Arztbrief","quote":"Arztbrief vom 02.10.2026 liegt vor."}]}',
            '{"hints":[{"kind":"document_mentioned","value":"Arztbrief","quote":"Arztbrief vom 02.10.2026 liegt vor.","outcome":"SUPPORTED"}]}',
            '{"hints":[],"automaticApproval":true}', '{"hints":[],"hints":[]}',
            '{"hints":[{"kind":[],"value":"x","quote":"Arztbrief vom 02.10.2026 liegt vor."}]}',
            '{"hints":"bad"}', '{"hints":[null]}',
            '\u0060\u0060\u0060json\n{"hints":[]}\n\u0060\u0060\u0060',
            json.dumps({"hints": [{"kind": "date_mentioned", "value": "02.10.2026",
                                   "quote": "Arztbrief vom 02.10.2026 liegt vor."}]*9}),
        ]:
            with self.subTest(reply=reply[:75]), self.assertRaises(assist.AssistantRejected):
                assist.validate_hints(TEXT, reply)

    def test_surrogate_unicode_is_handled_without_tracebacks(self):
        with self.assertRaises(assist.AssistantRejected):
            assist.validate_hints(TEXT, "\ud800")
        with self.assertRaises(assist.AssistantRejected):
            assist.build_request("\ud800")

    def test_document_and_response_are_bounded(self):
        for text in ["", "a" * 17_000]:
            with self.assertRaises(assist.AssistantRejected):
                assist.build_request(text)
        with self.assertRaises(assist.AssistantRejected):
            assist.validate_hints(TEXT, "a" * 70_000)

    def test_propose_has_no_side_effect_and_no_server_required(self):
        calls = []
        def fake_query(payload, *, token):
            calls.append((json.loads(payload), token))
            return GOOD
        hints = assist.propose_synthetic_hints(TEXT, query=fake_query, token="x"*32)
        self.assertEqual("NEEDS_HUMAN_REVIEW", hints[0]["reviewState"])
        self.assertEqual("normacase-assist", calls[0][0]["model"])
        self.assertEqual(False, calls[0][0]["stream"])
        self.assertEqual(1, len(calls))
        self.assertEqual("x"*32, calls[0][1])

    def test_disabled_cli_does_not_touch_transport_or_fixture(self):
        with patch.object(assist, "query_local_model", side_effect=AssertionError("network called")), \
             patch.object(Path, "read_text", side_effect=AssertionError("file read")), \
             self.assertRaises(SystemExit) as stopped:
            assist.main([])
        self.assertEqual(2, stopped.exception.code)

    def test_authentication_required_before_connection(self):
        for secret in ["", "short", "x"*32 + "\r"]:
            with self.subTest(secret=secret[:5]), self.assertRaises(assist.AssistantRejected):
                assist.query_local_model(b"{}", token=secret)

    def test_cli_failure_is_generic_without_document_or_key(self):
        with patch.object(assist, "propose_synthetic_hints", side_effect=assist.AssistantRejected("sensitive input")):
            with patch("sys.stderr") as stderr:
                self.assertEqual(2, assist.main(["--enable-synthetic-assist"]))
                self.assertNotIn("sensitive input", " ".join(map(str, stderr.write.call_args_list)))


if __name__ == "__main__":
    unittest.main()
