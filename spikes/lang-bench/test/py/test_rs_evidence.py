"""What a rejected daemon or web case keeps (rs_evidence): the observations that explain the mismatch, each bounded like the report's lines. It is a program's output: the record
carries it to the series folder and nowhere else. It never changes a verdict: the verdict fields of `compare` are golden below, taken before the evidence existed. Invented content only."""
import unittest

import runner_path  # noqa: F401
import rs_evidence as ev
import rs_session as ss
from rs_transcript import Actual
from test_rs_match import compare, recv, reply

ORDERED = ("ready\nrecv c1 \"a\"\nrecv c1 \"b\"\nexit 0\n", [Actual("ready"), recv("c1", b"a"), recv("c1", b"x"), Actual("exit", status=0)])
GROUP = ("ready\nunordered\nrecv c1 \"a\"\nrecv c2 \"b\"\nend\nexit 0\n", [Actual("ready"), recv("c1", b"a"), recv("c2", b"zzz"), Actual("exit", status=0)])
COUNT = ("ready\nrecv c1 \"a\"\nexit 0\n", [Actual("ready"), recv("c1", b"a")])
PASSING = ("ready\nrecv c1 \"a\"\n", [Actual("ready"), recv("c1", b"a")])
REPLY = ("ready\nreply c1 200\nbody empty\n", [Actual("ready"), reply("c1", 404, [(b"X-A", b"1")], b"nope")])

# (ok, reason, index, total, part, detail, kind, entries, actual is None): the verdicts of the code before the evidence was added
GOLDEN = {
    "ordered": (False, "entry 3 of 4 differs", 3, 4, "", "first difference at byte 0", "", 0, False),
    "group": (False, "entry 3 of 4 differs", 3, 4, "entry", "no assignment of the group's observations matches it", "", 0, True),
    "count": (False, "expected 3 entries, actual 2", 0, 3, "count", "why", "", 0, True),
    "passing": (True, "", 0, 0, "", "", "", 2, True),
    "reply": (False, "entry 2 of 2 differs", 2, 2, "status", "expected 200, actual 404", "", 0, False),
}
CASES = {"ordered": ORDERED, "group": GROUP, "count": COUNT, "passing": PASSING, "reply": REPLY}


def verdict(name):
    text, actual = CASES[name]
    return compare(text, actual, ended_by="why")


def fields(v):
    return (v.ok, v.reason, v.index, v.total, v.part, v.detail, v.kind, v.entries, v.actual is None)


class VerdictTests(unittest.TestCase):
    def test_The_verdict_of_every_kind_of_mismatch_is_what_it_was_before_the_evidence(self):
        for name, golden in GOLDEN.items():
            self.assertEqual(fields(verdict(name)), golden, name)

    def test_A_group_verdict_carries_the_observations_of_its_group_and_still_has_no_single_actual(self):
        v = verdict("group")
        self.assertIsNone(v.actual)
        self.assertEqual([(a.conn, a.data) for a in v.group], [("c1", b"a"), ("c2", b"zzz")])


class EvidenceTests(unittest.TestCase):
    def test_An_ordered_mismatch_keeps_the_expected_entry_and_the_observation_that_differed(self):
        e = ev.evidence(verdict("ordered"), CASES["ordered"][1])
        self.assertEqual(e["expected"], 'recv c1 "b"')
        self.assertEqual([(o["kind"], o["conn"], o["data"]) for o in e["observed"]], [("recv", "c1", '"x"')])
        self.assertEqual((e["group"], e["omitted"]), (False, 0))

    def test_An_unordered_group_keeps_all_its_observations_with_their_connection_names(self):
        e = ev.evidence(verdict("group"), CASES["group"][1])
        self.assertEqual(e["group"], True)
        self.assertEqual([(o["conn"], o["data"]) for o in e["observed"]], [("c1", '"a"'), ("c2", '"zzz"')])

    def test_A_reply_keeps_its_status_headers_and_body_which_the_report_line_leaves_out(self):
        o = ev.evidence(verdict("reply"), CASES["reply"][1])["observed"][0]
        self.assertEqual((o["status"], o["body"], o["headers"]), (404, '"nope"', [['"X-A"', '"1"']]))

    def test_A_count_mismatch_keeps_the_last_observations_of_the_session(self):
        e = ev.evidence(verdict("count"), CASES["count"][1])
        self.assertEqual([o["kind"] for o in e["observed"]], ["ready", "recv"])
        self.assertIsNone(e["expected"])

    def test_Every_string_of_the_evidence_is_bounded_like_the_reports_lines(self):
        big = Actual("recv", "c1", data=b"k" * 5000, step_text="s" * 5000)
        o = ev.observation(big)
        self.assertLess(len(o["data"]), 200)
        self.assertIn("(4900 bytes)", o["data"])
        self.assertLessEqual(len(o["stepText"]), ev.MAX_STEP_TEXT)
        many = tuple(Actual("recv", f"c{i}", data=b"x") for i in range(ev.MAX_OBSERVATIONS + 30))
        v = verdict("group")._replace(group=many)
        e = ev.evidence(v, [])
        self.assertEqual((len(e["observed"]), e["omitted"]), (ev.MAX_OBSERVATIONS, 30))
        headers = tuple((b"h%d" % i, b"v") for i in range(ev.MAX_HEADERS + 5))
        self.assertEqual(len(ev.observation(Actual("reply", "c", headers=headers))["headers"]), ev.MAX_HEADERS)

    def test_A_passing_verdict_keeps_no_evidence(self):
        self.assertIsNone(ev.evidence(verdict("passing"), CASES["passing"][1]))


SCRIPT = b"scenario 2\ndefault within 1s\nstart ready port within 3s\nconnect c tcp\nsignal TERM\nwait-exit within 1s\n"


def judged(actual, transcript_text):
    s = ss.Session(ss.Case("c1", SCRIPT, ("transcript 2\n" + transcript_text).encode(), "c1.in", "c1.out"), config=None)
    s.entries, s.ended_by = actual, "why"
    return s.judge({}, False)


class SessionTests(unittest.TestCase):
    def test_The_judgement_of_a_case_keeps_its_reason_and_lines_and_adds_only_the_evidence(self):
        reason, mismatch, lines = judged(CASES["ordered"][1], CASES["ordered"][0])
        self.assertEqual(reason, "entry 3 of 4 differs")
        self.assertEqual({k: mismatch[k] for k in ("entry", "of", "part", "detail")}, {"entry": 3, "of": 4, "part": "", "detail": "first difference at byte 0"})
        self.assertEqual(set(mismatch), {"entry", "of", "part", "detail", "evidence"})
        self.assertEqual(mismatch["evidence"]["expected"], 'recv c1 "b"')
        self.assertTrue(any("expect" in line for line in lines))

    def test_A_matching_session_is_judged_a_pass_as_before(self):
        self.assertIsNone(judged(CASES["passing"][1], CASES["passing"][0]))


if __name__ == "__main__":
    unittest.main()
