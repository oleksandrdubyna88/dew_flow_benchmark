"""The report of FORMAT v2 5.5 and the JSON result: one line per case, the first mismatch under a FAIL, abbreviated strings."""
import unittest

import runner_path  # noqa: F401
import rs_match as rm
import rs_report as rp
import rs_transcript as rt
from rs_transcript import Actual

# The layout of FORMAT 5.6's example (head, step, expect and actual in columns, the first difference), over the toy of FORMAT 6.2.
FORMAT_EXAMPLE = """FAIL s02 — entry 4 of 9 differs
  step    s02.in:14   recv c1 bytes 9
  expect  s02.out:6   recv c1 "0004|kiwi"
  actual              recv c1 "0004|kiwx"
  first difference at byte 8"""


def verdict_for(expected_text, actual):
    parsed, problems = rt.parse_transcript(("transcript 2\n" + expected_text + "\n").encode(), "s02.out")
    assert not problems, problems
    return rm.compare_transcript(parsed, actual)


class RenderTests(unittest.TestCase):
    def test_A_pass_is_one_line_with_its_entry_count(self):
        result = rp.CaseResult("s03", "PASS", entries=1)
        self.assertEqual(rp.render_case(result), ["PASS s03 — 1 entries"])

    def test_The_mismatch_report_has_the_layout_of_the_format_example_line_for_line(self):
        expected = 'ready\nquiet c1\nrecv c1 "a"\n# the fourth entry is on line 6\nrecv c1 "0004|kiwi"\n' + "quiet c1\n" * 5
        actual = [Actual("ready"), Actual("quiet", "c1"), Actual("recv", "c1", data=b"a"),
                  Actual("recv", "c1", data=b"0004|kiwx", step_line=14, step_text="recv c1 bytes 9")]
        verdict = verdict_for(expected, actual + [Actual("quiet", "c1")] * 5)
        self.assertEqual("\n".join(rp.render_verdict("s02", verdict, "s02.in", "s02.out")), FORMAT_EXAMPLE)

    def test_A_reply_difference_names_the_part_and_a_JSON_difference_its_path(self):
        parsed, _ = rt.parse_transcript(b'transcript 2\nreply a 200\nbody json {"qty":2}\n', "t.out")
        verdict = rm.compare_transcript(parsed, [Actual("reply", "a", status=200, body=b'{"qty":3}')])
        lines = rp.render_verdict("x", verdict, "x.in", "t.out")
        self.assertEqual(lines[0], "FAIL x — entry 1 of 1 differs")
        self.assertIn("  body $.qty: expected 2, actual 3", lines)

    def test_A_count_difference_names_both_counts_and_the_step_that_ended_the_scenario(self):
        parsed, _ = rt.parse_transcript(b'transcript 2\nready\nexit 0\n', "t.out")
        verdict = rm.compare_transcript(parsed, [Actual("ready")], ended_by="step x.in:9 wait-exit")
        self.assertEqual(rp.render_verdict("x", verdict, "x.in", "t.out"), ["FAIL x — expected 2 entries, actual 1", "  step x.in:9 wait-exit"])

    def test_A_check_failure_is_one_line_and_produces_no_entry_lines(self):
        parsed, _ = rt.parse_transcript(b'transcript 2\nrecv c1 "${:token=a}"\nrecv c1 "${:token=b}"\ncheck distinct a b\n', "t.out")
        verdict = rm.compare_transcript(parsed, [Actual("recv", "c1", data=b"x"), Actual("recv", "c1", data=b"x")])
        self.assertEqual(rp.render_verdict("x", verdict, "x.in", "t.out"), ["FAIL x — check: a and b are equal"])

    def test_A_string_over_120_bytes_is_shown_as_its_first_60_and_last_40_with_the_count_between(self):
        parsed, _ = rt.parse_transcript(('transcript 2\nrecv c1 "' + "a" * 300 + '"\n').encode(), "t.out")
        verdict = rm.compare_transcript(parsed, [Actual("recv", "c1", data=b"b" * 300)])
        text = "\n".join(rp.render_verdict("x", verdict, "x.in", "t.out"))
        self.assertIn("(200 bytes)", text)
        self.assertNotIn("a" * 61, text)

    def test_The_bytes_around_the_first_difference_of_an_olympiad_output_are_shown_not_the_start_of_a_long_output(self):
        expected, actual = b"x" * 500 + b"A" + b"y" * 500, b"x" * 500 + b"B" + b"y" * 500
        lines = rp.output_difference_lines("s01.out", expected, actual[:700], 500)
        text = "\n".join(lines)
        self.assertIn("first difference at byte 500", text)
        self.assertEqual([line.count("...") for line in lines[:2]], [2, 2], "the window is cut on both sides and says so")
        self.assertIn("A", text)
        self.assertLess(max(len(line) for line in lines), 200)
        self.assertIn("(not kept)", rp.output_difference_lines("s01.out", expected, actual[:200], 500)[1], "a capture cut before the difference says so")

    def test_A_time_verdict_says_which_limit_it_was_judged_against(self):
        self.assertEqual(rp.time_text(1020, 1000, "floor"), "time limit: 1.020 s over 1.000 s (against the floor)")
        self.assertEqual(rp.time_text(2500, 2000, "registered"), "time limit: 2.500 s over 2.000 s (the registered limit)")


class JsonTests(unittest.TestCase):
    def test_A_case_record_carries_the_fields_of_the_result_format_and_unknown_is_never_zero(self):
        result = rp.CaseResult("s01", "PASS", entries=1, r_ids=("R1", "R2"), wall_ms=12, lifecycle=rp.plain_lifecycle(False))
        record = rp.case_record(result)
        self.assertEqual(sorted(record), ["comparison", "entries", "firstMismatch", "id", "lifecycle", "rIds", "reason", "status", "wallMs"])
        self.assertEqual(record["lifecycle"]["memory"], {"known": False, "why": "not certified"})
        self.assertEqual((record["rIds"], record["wallMs"], record["firstMismatch"]), (["R1", "R2"], 12, None))

    def test_The_run_record_names_the_runner_the_grammar_the_kind_the_build_and_python(self):
        record = rp.run_record("olympiad", {"ran": False}, [])
        self.assertEqual((record["format"], record["grammar"], record["kind"], record["cases"]), (1, 2, "olympiad", []))
        self.assertEqual(record["runner"], rp.RUNNER_VERSION)
        self.assertIn("python", record)

    def test_The_summary_counts_cases_and_failures(self):
        results = [rp.CaseResult("a", "PASS"), rp.CaseResult("b", "FAIL", "x")]
        self.assertEqual(rp.summary_line(results), "1 of 2 cases passed, 1 failed")


if __name__ == "__main__":
    unittest.main()
