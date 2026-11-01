"""Entries compared with observations, the whole transcript compared, the V6 checks, and FORMAT v2's worked examples (section 6)."""
import os
import unittest
from datetime import datetime, timezone

import runner_path  # noqa: F401
import rs_fields as rf
import rs_json
import rs_match as rm
import rs_transcript as rt
from rs_transcript import Actual

FIXTURES = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "fixtures", "runner", "format")
CLOCK = datetime(2026, 1, 2, 3, 4, 5, tzinfo=timezone.utc)
UUID = b"0b6f1c2a-1111-2222-3333-444455556666"


def transcript(text):
    parsed, problems = rt.parse_transcript(("transcript 2\n" + text.rstrip("\n") + "\n").encode(), "t.out")
    assert not problems, problems
    return parsed


def entry(text):
    return rt.expand(transcript(text))[0]


def reply(conn, status, headers=(), body=b""):
    return Actual("reply", conn, status=status, headers=tuple(headers), body=body, at=CLOCK)


def recv(conn, data):
    return Actual("recv", conn, data=data, at=CLOCK)


def compare(text, actual, variables=None, ended_by=""):
    t = transcript(text)
    return rm.compare(rt.expand(t), rt.checks_of(t), actual, variables, ended_by)


class EntryTests(unittest.TestCase):
    def test_Ready_quiet_closed_and_exit_match_by_kind_connection_and_status(self):
        self.assertTrue(rm.match_entry(entry("ready"), Actual("ready")).ok)
        self.assertTrue(rm.match_entry(entry("quiet c1"), Actual("quiet", "c1")).ok)
        self.assertFalse(rm.match_entry(entry("quiet c1"), Actual("quiet", "c2")).ok)
        self.assertTrue(rm.match_entry(entry("closed c1"), Actual("closed", "c1")).ok)
        self.assertTrue(rm.match_entry(entry("exit 0"), Actual("exit", status=0)).ok)
        self.assertFalse(rm.match_entry(entry("exit 0"), Actual("exit", status=1)).ok)

    def test_An_entry_that_is_only_ever_actual_never_matches_any_expected_entry(self):
        for kind in ("not-ready", "refused", "timeout", "eof", "noise", "open", "bad-reply", "early-exit", "exit-timeout", "exit-signal",
                     "capture-failed", "overflow"):
            for text in ("ready", "quiet c1", 'recv c1 ""', "exit 0", "closed c1"):
                self.assertFalse(rm.match_entry(entry(text), Actual(kind, "c1")).ok, (kind, text))

    def test_A_recv_matches_its_bytes_and_a_difference_names_the_first_differing_byte(self):
        self.assertTrue(rm.match_entry(entry('recv c1 "$5\\r\\nmango\\r\\n"'), recv("c1", b"$5\r\nmango\r\n")).ok)
        result = rm.match_entry(entry('recv c1 "$5\\r\\nmango\\r\\n"'), recv("c1", b"$5\r\nmelon\r\n"))
        self.assertEqual((result.ok, result.detail), (False, "first difference at byte 5"))

    def test_A_recv_that_is_a_prefix_or_an_extension_of_the_expected_bytes_differs_at_the_length_of_the_shorter(self):
        self.assertEqual(rm.match_entry(entry('recv c1 "abc"'), recv("c1", b"ab")).detail, "first difference at byte 2")
        self.assertEqual(rm.match_entry(entry('recv c1 "abc"'), recv("c1", b"abcd")).detail, "first difference at byte 3")

    def test_A_recv_field_binds_what_it_matched_and_a_repeat_variable_fills_the_name(self):
        result = rm.match_entry(rt.expand(transcript('repeat 2 i\nrecv c1 "${:uint=n${i}}"\nend\n'))[1], recv("c1", b"77"))
        self.assertEqual({n: b.raw for n, b in result.binds}, {"n1": b"77"})

    def test_A_loop_variable_is_a_value_inside_the_expanded_entry(self):
        expanded = rt.expand(transcript('repeat 3 i\nrecv c1 "k${i}"\nend\n'))
        self.assertTrue(rm.match_entry(expanded[2], recv("c1", b"k2")).ok)
        self.assertFalse(rm.match_entry(expanded[2], recv("c1", b"k1")).ok)

    def test_A_star_connection_matches_any_connection(self):
        self.assertTrue(rm.match_entry(rt.expand(transcript('unordered\nrecv * "a"\nrecv * "b"\nend\n'))[0], recv("zz", b"a")).ok)

    def test_An_unbound_variable_is_a_mismatch_that_names_it(self):
        result = rm.match_entry(entry('recv c1 "${ghost}"'), recv("c1", b"x"))
        self.assertFalse(result.ok)
        self.assertIn("ghost", result.detail)


class JsonRecvTests(unittest.TestCase):
    """`recv <conn> json <JSON>`: the reply's bytes, read as UTF-8, are one JSON value that matches the expected one (5.2)."""

    def test_A_reply_that_is_the_expected_value_matches_whatever_its_whitespace_and_its_final_line_feed(self):
        expected = entry('recv c json {"a": [1, 2], "b": null}')
        for data in (b'{"b":null,"a":[1,2]}\n', b'  {"a" : [1,2.0], "b" : null}\r\n', b'\n{"a":[1,2],"b":null}'):
            self.assertTrue(rm.match_entry(expected, recv("c", data)).ok, data)

    def test_A_byte_compare_would_refuse_the_same_reply_and_that_is_the_point(self):
        self.assertFalse(rm.match_entry(entry('recv c "{\\"a\\":1}\\n"'), recv("c", b'{ "a": 1 }\n')).ok)
        self.assertTrue(rm.match_entry(entry('recv c json {"a":1}'), recv("c", b'{ "a": 1 }\n')).ok)

    def test_A_difference_names_the_path_and_both_values(self):
        result = rm.match_entry(entry('recv c json {"a":{"b":[1,2]}}'), recv("c", b'{"a":{"b":[1,3]}}\n'))
        self.assertEqual((result.ok, result.part, result.detail), (False, "json $.a.b[1]", "expected 2, actual 3"))

    def test_Exact_decimal_rules_apply_two_is_two_point_zero_and_large_integers_stay_distinct(self):
        self.assertTrue(rm.match_entry(entry("recv c json [2]"), recv("c", b"[2.0]\n")).ok)
        self.assertFalse(rm.match_entry(entry("recv c json [9007199254740993]"), recv("c", b"[9007199254740992]\n")).ok)

    def test_Fields_and_variables_work_inside_the_JSON_and_bind(self):
        result = rm.match_entry(entry('recv c json {"id":"${:uuid=o}","n":"${:int=k}"}'), recv("c", b'{"id":"%s","n":7}\n' % UUID))
        self.assertEqual({n: b.raw for n, b in result.binds}, {"o": UUID, "k": b"7"})

    def test_A_reply_that_is_not_JSON_is_bad_json_or_bad_utf8_at_its_entry(self):
        expected = entry('recv c json {"a":1}')
        for data, kind in ((b"{", "bad-json"), (b'{"a":1,"a":1}', "bad-json"), (b'{"a":NaN}', "bad-json"), (b'{"a":"\xff"}', "bad-utf8"), (b"", "bad-json"), (b'{"a":1} {"a":1}', "bad-json")):
            result = rm.match_entry(expected, recv("c", data))
            self.assertEqual((result.ok, result.kind), (False, kind), data)

    def test_Lines_of_standard_output_each_compare_as_their_own_JSON_value(self):
        text = 'recv stdout json {"n":1}\nrecv stdout json {"n":2}\n'
        verdict = compare(text, [recv("stdout", b'{"n": 1}\n'), recv("stdout", b'{"n":2}\n')])
        self.assertTrue(verdict.ok)
        verdict = compare(text, [recv("stdout", b'{"n": 1}\n'), recv("stdout", b'{"n":3}\n')])
        self.assertEqual((verdict.reason, verdict.part), ("entry 2 of 2 differs", "json $.n"))

    def test_The_report_line_of_a_JSON_recv_entry_shows_the_JSON(self):
        import rs_report
        self.assertEqual(rs_report.expected_text(entry('recv c json {"a": 1}')), 'recv c json {"a": 1}')


class ReplyTests(unittest.TestCase):
    def test_A_reply_matches_on_status_and_listed_headers_and_body(self):
        expected = entry('reply a 201\nheader "location" "/orders/7"\nbody json {"id":7}')
        actual = reply("a", 201, [(b"Location", b"/orders/7"), (b"X-Other", b"ignored")], b'{"id": 7.0}')
        self.assertTrue(rm.match_entry(expected, actual).ok)

    def test_A_different_status_is_named_with_both_codes(self):
        result = rm.match_entry(entry("reply a 201\nbody empty"), reply("a", 200))
        self.assertEqual((result.part, result.detail), ("status", "expected 201, actual 200"))

    def test_A_header_is_compared_without_regard_to_the_case_of_its_name_and_joins_repeated_values(self):
        expected = entry('reply a 200\nheader "set-cookie" "a=1, b=2"\nbody empty')
        actual = reply("a", 200, [(b"SET-COOKIE", b"  a=1 "), (b"Set-Cookie", b"\tb=2")])
        self.assertTrue(rm.match_entry(expected, actual).ok)

    def test_A_missing_header_and_a_different_header_value_are_named(self):
        expected = entry('reply a 200\nheader "location" "/x"\nbody empty')
        result = rm.match_entry(expected, reply("a", 200))
        self.assertEqual(result.part, "header location")
        self.assertIn("absent", result.detail)
        result = rm.match_entry(expected, reply("a", 200, [(b"location", b"/y")]))
        self.assertEqual((result.part, result.detail), ("header location", 'expected "/x", actual "/y"'))

    def test_A_header_the_entry_does_not_list_is_not_compared(self):
        self.assertTrue(rm.match_entry(entry("reply a 200\nbody empty"), reply("a", 200, [(b"x", b"y")])).ok)

    def test_Body_empty_requires_zero_bytes_and_a_string_body_compares_bytes(self):
        self.assertTrue(rm.match_entry(entry("reply a 204\nbody empty"), reply("a", 204)).ok)
        self.assertFalse(rm.match_entry(entry("reply a 204\nbody empty"), reply("a", 204, body=b" ")).ok)
        self.assertTrue(rm.match_entry(entry('reply a 200\nbody "ok\\n"'), reply("a", 200, body=b"ok\n")).ok)
        result = rm.match_entry(entry('reply a 200\nbody "ok\\n"'), reply("a", 200, body=b"ok"))
        self.assertEqual((result.part, result.detail), ("body", "first difference at byte 2"))

    def test_A_JSON_body_difference_names_the_path_and_both_values(self):
        result = rm.match_entry(entry('reply a 200\nbody json {"items":[{"qty":2}]}'), reply("a", 200, body=b'{"items":[{"qty":3}]}'))
        self.assertEqual((result.part, result.detail), ("body $.items[0].qty", "expected 2, actual 3"))

    def test_An_invalid_UTF_8_JSON_body_is_bad_utf8(self):
        result = rm.match_entry(entry('reply a 200\nbody json {"a":"x"}'), reply("a", 200, body=b'{"a":"\xff"}'))
        self.assertEqual((result.ok, result.kind, result.part), (False, "bad-utf8", "body"))

    def test_A_JSON_body_that_is_not_JSON_or_repeats_a_member_or_holds_NaN_is_bad_json_naming_the_cause(self):
        expected = entry('reply a 200\nbody json {"a":1}')
        for body, needle in ((b"{", "Expecting"), (b'{"a":1,"a":2}', '"a"'), (b'{"a":NaN}', "NaN"), (b'{"a":Infinity}', "Infinity")):
            result = rm.match_entry(expected, reply("a", 200, body=body))
            self.assertEqual((result.ok, result.kind), (False, "bad-json"), body)
            self.assertIn(needle, result.detail)

    def test_Fields_in_a_JSON_body_bind_typed_values(self):
        result = rm.match_entry(entry('reply a 200\nbody json {"id":"${:uuid=o}"}'), reply("a", 200, body=b'{"id":"%s"}' % UUID))
        self.assertEqual({n: b.raw for n, b in result.binds}, {"o": UUID})

    def test_A_timestamp_window_is_measured_against_the_time_the_entry_was_recorded(self):
        expected = entry('reply a 200\nbody json {"at":"${:ts~10s}"}')
        late = Actual("reply", "a", status=200, body=b'{"at":"2026-01-02T03:04:10Z"}', at=datetime(2026, 1, 2, 9, 0, 0, tzinfo=timezone.utc))
        self.assertFalse(rm.match_entry(expected, late).ok)
        self.assertTrue(rm.match_entry(expected, reply("a", 200, body=b'{"at":"2026-01-02T03:04:10Z"}')).ok)


class CompareTests(unittest.TestCase):
    def test_Matching_transcripts_pass_with_their_entry_count(self):
        verdict = compare('ready\nrecv c1 "+OK\\r\\n"\nexit 0\n', [Actual("ready"), recv("c1", b"+OK\r\n"), Actual("exit", status=0)])
        self.assertEqual((verdict.ok, verdict.entries), (True, 3))

    def test_The_first_differing_entry_is_named_by_its_position_and_the_total(self):
        verdict = compare('ready\nrecv c1 "a"\nrecv c1 "b"\nexit 0\n', [Actual("ready"), recv("c1", b"a"), recv("c1", b"x"), Actual("exit", status=0)])
        self.assertEqual((verdict.ok, verdict.reason, verdict.index), (False, "entry 3 of 4 differs", 3))
        self.assertEqual(verdict.detail, "first difference at byte 0")

    def test_A_body_that_cannot_be_read_as_JSON_is_the_verdict_bad_utf8_or_bad_json_at_its_entry(self):
        text = 'ready\nreply a 200\nbody json {"a":1}\n'
        verdict = compare(text, [Actual("ready"), reply("a", 200, body=b'{"a":"\xff"}')])
        self.assertEqual((verdict.ok, verdict.reason, verdict.index), (False, "bad-utf8", 2))
        verdict = compare(text, [Actual("ready"), reply("a", 200, body=b'{"a":NaN}')])
        self.assertEqual((verdict.reason, verdict.index), ("bad-json", 2))
        self.assertIn("NaN", verdict.detail)

    def test_Fewer_actual_entries_than_expected_is_a_count_difference_with_the_step_that_ended_the_scenario(self):
        verdict = compare('ready\nrecv c1 "a"\nexit 0\n', [Actual("ready"), recv("c1", b"a")], ended_by="step s.in:9 recv c1")
        self.assertEqual((verdict.reason, verdict.detail), ("expected 3 entries, actual 2", "step s.in:9 recv c1"))

    def test_More_actual_entries_than_expected_is_a_count_difference_too(self):
        verdict = compare("ready\n", [Actual("ready"), recv("c1", b"a")])
        self.assertEqual(verdict.reason, "expected 1 entries, actual 2")

    def test_An_entry_that_differs_is_reported_before_a_count_difference_that_follows_from_it(self):
        verdict = compare('ready\nrecv c1 "a"\nexit 0\n', [Actual("ready"), Actual("noise", "c1", data=b"x")])
        self.assertEqual(verdict.reason, "entry 2 of 3 differs")

    def test_A_bound_value_is_visible_to_a_later_entry(self):
        text = 'recv c1 "${:uint=n}"\nrecv c1 "n=${n}"\n'
        self.assertTrue(compare(text, [recv("c1", b"42"), recv("c1", b"n=42")]).ok)
        self.assertFalse(compare(text, [recv("c1", b"42"), recv("c1", b"n=43")]).ok)

    def test_A_script_variable_given_to_the_comparison_is_visible_to_every_entry(self):
        self.assertTrue(compare('recv c1 "${v}"\n', [recv("c1", b"x")], {"v": rf.Bound(b"x")}).ok)

    def test_An_unordered_group_matches_the_same_entries_in_any_order(self):
        text = 'unordered\nrecv c1 "a"\nrecv c2 "b"\nend\nexit 0\n'
        self.assertTrue(compare(text, [recv("c2", b"b"), recv("c1", b"a"), Actual("exit", status=0)]).ok)

    def test_An_unordered_group_needs_a_one_to_one_assignment_and_names_the_entry_nothing_matched(self):
        text = 'unordered\nrecv c1 "a"\nrecv c1 "a"\nend\n'
        verdict = compare(text, [recv("c1", b"a"), recv("c1", b"b")])
        self.assertEqual((verdict.ok, verdict.index), (False, 2))
        self.assertIn("no assignment", verdict.detail)

    def test_An_unordered_group_with_a_star_connection_finds_the_assignment_a_greedy_pass_would_miss(self):
        text = 'unordered\nrecv * "a"\nrecv c1 "a"\nend\n'
        self.assertTrue(compare(text, [recv("c1", b"a"), recv("c2", b"a")]).ok)

    def test_Binds_made_inside_an_unordered_group_belong_to_the_entry_each_was_assigned(self):
        text = 'unordered\nrecv * "k${:uint=a}"\nrecv c2 "m${:uint=b}"\nend\nrecv c1 "${a}-${b}"\n'
        verdict = compare(text, [recv("c2", b"m9"), recv("c1", b"k7"), recv("c1", b"7-9")])
        self.assertTrue(verdict.ok, verdict)

    def test_A_group_cut_short_by_the_end_of_the_observations_is_a_count_difference(self):
        verdict = compare('unordered\nrecv c1 "a"\nrecv c1 "b"\nend\n', [recv("c1", b"a")])
        self.assertEqual(verdict.reason, "expected 2 entries, actual 1")


class CheckTests(unittest.TestCase):
    def tokens(self, *values):
        return [recv("c1", v) for v in values]

    def test_Check_distinct_over_a_glob_names_the_first_two_equal_values_in_binding_order(self):
        text = 'repeat 3 i\nrecv c1 "${:token=r${i}}"\nend\ncheck distinct r*\n'
        verdict = compare(text, self.tokens(b"aa", b"bb", b"aa"))
        self.assertEqual((verdict.ok, verdict.reason), (False, "check: r0 and r2 are equal"))

    def test_Check_distinct_passes_when_every_value_differs(self):
        text = 'repeat 3 i\nrecv c1 "${:token=r${i}}"\nend\ncheck distinct r*\n'
        self.assertTrue(compare(text, self.tokens(b"aa", b"bb", b"cc")).ok)

    def test_Check_distinct_compares_JSON_values_exactly_so_two_and_two_point_zero_are_equal(self):
        text = 'reply a 200\nbody json {"n":"${:num 2 0=x}"}\nreply a 200\nbody json {"n":"${:num 2 0=y}"}\ncheck distinct x y\n'
        verdict = compare(text, [reply("a", 200, body=b'{"n":2}'), reply("a", 200, body=b'{"n":2.0}')])
        self.assertEqual(verdict.reason, "check: x and y are equal")

    def test_Check_equal_compares_a_bound_number_with_a_decimal_or_with_another_name_exactly(self):
        base = 'recv c1 "${:num 0 100=a}"\nrecv c1 "${:num 0 100=b}"\n'
        self.assertTrue(compare(base + "check equal a 3.0\n", self.tokens(b"3", b"4")).ok)
        self.assertTrue(compare(base + "check equal a b\n", self.tokens(b"3", b"3")).ok)
        self.assertFalse(compare(base + "check equal a b\n", self.tokens(b"3", b"3.00")).ok, "bytes are compared as bytes; a JSON number is the exact-decimal case")
        verdict = compare(base + "check equal a b\n", self.tokens(b"3", b"4"))
        self.assertEqual(verdict.reason, "check: a (3) is not equal to b (4)")
        self.assertFalse(compare(base + "check equal a 9007199254740993\n", self.tokens(b"9007199254740992", b"1")).ok)

    def test_A_check_over_a_value_that_is_not_a_number_fails_naming_it(self):
        verdict = compare('recv c1 "${:token=t}"\ncheck equal t 3\n', self.tokens(b"abc"))
        self.assertEqual(verdict.reason, "check: t is abc, expected 3")

    def test_A_check_over_a_name_nothing_bound_fails_instead_of_passing(self):
        verdict = compare('recv c1 "x"\ncheck distinct r*\n', self.tokens(b"x"))
        self.assertFalse(verdict.ok)
        self.assertIn("r*", verdict.reason)

    def test_A_check_is_evaluated_after_the_entries_so_an_entry_difference_comes_first(self):
        text = 'recv c1 "${:token=a}"\nrecv c1 "${:token=b}"\ncheck distinct a b\n'
        verdict = compare(text, self.tokens(b"same", b"same"))
        self.assertEqual(verdict.reason, "check: a and b are equal")
        verdict = compare(text.replace('"${:token=b}"', '"nope"'), self.tokens(b"same", b"same"))
        self.assertTrue(verdict.reason.startswith("entry 2"))


class CheckNamespaceTests(unittest.TestCase):
    """5.5: one namespace for checks - the script's captures and the transcript's bindings alike - and `equal` compares values, not numbers only."""

    def tokens(self, *values):
        return [recv("c1", v) for v in values]

    def test_Check_equal_of_two_names_holding_the_same_token_passes(self):
        text = 'recv c1 "${:token=a}\\n"\nrecv c1 "${:token=b}\\n"\ncheck equal a b\n'
        self.assertTrue(compare(text, self.tokens(b"abc\n", b"abc\n")).ok)
        verdict = compare(text, self.tokens(b"abc\n", b"abd\n"))
        self.assertEqual(verdict.reason, "check: a (abc) is not equal to b (abd)")

    def test_Check_equal_of_two_names_compares_JSON_values_exactly_and_bytes_otherwise(self):
        text = 'reply a 200\nbody json {"x":"${:num 2 0=p}","y":"${:num 2 0=q}"}\ncheck equal p q\n'
        self.assertTrue(compare(text, [reply("a", 200, body=b'{"x":2,"y":2.0}')]).ok)
        self.assertFalse(compare(text.replace("q}", "q}").replace("num 2 0=q", "num 3 1=q"), [reply("a", 200, body=b'{"x":2,"y":3}')]).ok)

    def test_Check_distinct_sees_a_capture_the_script_bound(self):
        text = 'recv c1 "${:token=b}\\n"\ncheck distinct id b\n'
        self.assertTrue(compare(text, self.tokens(b"x\n"), {"id": rf.Bound(b"y")}).ok)
        verdict = compare(text, self.tokens(b"x\n"), {"id": rf.Bound(b"x")})
        self.assertEqual(verdict.reason, "check: id and b are equal")

    def test_A_glob_stands_for_captures_and_bindings_alike_in_one_namespace(self):
        text = 'recv c1 "${:token=r1}\\n"\ncheck distinct r*\n'
        self.assertTrue(compare(text, self.tokens(b"y\n"), {"r0": rf.Bound(b"x")}).ok)
        verdict = compare(text, self.tokens(b"x\n"), {"r0": rf.Bound(b"x")})
        self.assertEqual(verdict.reason, "check: r0 and r1 are equal")

    def test_Check_equal_of_a_name_and_a_decimal_reports_the_value_and_the_expectation(self):
        text = 'recv c1 "${:uint=total}\\n"\ncheck equal total 64\n'
        self.assertTrue(compare(text, self.tokens(b"64\n")).ok)
        self.assertEqual(compare(text, self.tokens(b"63\n")).reason, "check: total is 63, expected 64")

    def test_Check_equal_of_a_JSON_number_and_a_decimal_is_exact_and_a_JSON_string_is_never_a_number(self):
        text = 'reply a 200\nbody json {"v":"${:num 5 1=a}"}\ncheck equal a 5\n'
        self.assertTrue(compare(text, [reply("a", 200, body=b'{"v":5.0}')]).ok)
        text = 'reply a 200\nbody json {"v":"${:token=a}"}\ncheck equal a 5\n'
        self.assertEqual(compare(text, [reply("a", 200, body=b'{"v":"5"}')]).reason, 'check: a is "5", expected 5')

    def test_Check_equal_of_a_name_and_a_string_matches_bytes_or_a_JSON_string_and_never_a_number(self):
        raw = 'recv c1 "${:token=a}"\ncheck equal a "abc"\n'
        self.assertTrue(compare(raw, self.tokens(b"abc")).ok)
        self.assertEqual(compare(raw, self.tokens(b"abd")).reason, 'check: a is "abd", expected "abc"')
        text = 'reply a 200\nbody json {"v":"${:token=a}"}\ncheck equal a "abc"\n'
        self.assertTrue(compare(text, [reply("a", 200, body=b'{"v":"abc"}')]).ok)
        number = 'reply a 200\nbody json {"v":"${:int=a}"}\ncheck equal a "5"\n'
        self.assertFalse(compare(number, [reply("a", 200, body=b'{"v":5}')]).ok, "a string never matches a number")

    def test_A_string_with_characters_beyond_ASCII_compares_by_its_UTF_8_bytes_and_by_JSON_characters(self):
        text = 'recv c1 "x"\ncheck equal a "caf\\xc3\\xa9"\n'
        as_json = rf.Bound("caf\u00e9".encode(), rs_json.parse('"caf\\u00e9"').value)
        self.assertTrue(compare(text, self.tokens(b"x"), {"a": as_json}).ok)
        self.assertTrue(compare(text, self.tokens(b"x"), {"a": rf.Bound("caf\u00e9".encode())}).ok)
        self.assertFalse(compare(text, self.tokens(b"x"), {"a": rf.Bound(b"cafe")}).ok)

    def test_A_check_name_that_nothing_bound_fails_instead_of_passing(self):
        self.assertIn("ghost", compare('recv c1 "x"\ncheck equal ghost 1\n', self.tokens(b"x")).reason)


class FormatExampleTests(unittest.TestCase):
    def load(self, name):
        with open(os.path.join(FIXTURES, name), "rb") as f:
            parsed, problems = rt.parse_transcript(f.read(), name)
        assert not problems, problems
        return parsed

    def x2_correct(self):
        lazy = b"$16384\r\n" + b"v" * 16384 + b"\r\n"
        out = [Actual("ready"), Actual("quiet", "c1"), recv("c1", b"+OK\r\n"), recv("c1", b"$5\r\nmango\r\n"), recv("c1", b"+PONG\r\n"),
               recv("c1", b"$5\r\nmango\r\n"), recv("c1", b"+OK\r\n"), recv("c1", b"+PONG\r\n")]
        return out + [recv("lazy", lazy)] * 64 + [Actual("exit", status=0)]

    def test_The_daemon_example_passes_for_a_correct_toy(self):
        t = self.load("x2.out")
        verdict = rm.compare(rt.expand(t), rt.checks_of(t), self.x2_correct())
        self.assertEqual((verdict.ok, verdict.entries), (True, 73))

    def test_A_toy_that_answers_a_split_frame_early_fails_at_entry_2_with_noise(self):
        t = self.load("x2.out")
        actual = self.x2_correct()
        actual[1] = Actual("noise", "c1", data=b"+OK\r\n")
        verdict = rm.compare(rt.expand(t), rt.checks_of(t), actual)
        self.assertEqual((verdict.index, verdict.reason, verdict.actual.kind), (2, "entry 2 of 73 differs", "noise"))

    def test_A_toy_that_stops_serving_a_second_client_fails_at_entry_8_with_a_timeout(self):
        t = self.load("x2.out")
        actual = self.x2_correct()
        actual[7] = Actual("timeout", "c1", data=b"")
        verdict = rm.compare(rt.expand(t), rt.checks_of(t), actual)
        self.assertEqual((verdict.index, verdict.actual.kind), (8, "timeout"))

    def x3_actual(self, second_id, second_location):
        first = b'{"id":"%s","item":"tea","qty":2,"at":"2026-01-02T03:04:05Z"}' % UUID
        second = b'{"id":"%s","item":"tea","qty":2,"at":"2026-01-02T03:04:05Z"}' % second_id
        loc = lambda i: [(b"Location", b"/orders/" + i)]
        other = b"1c6f1c2a-1111-2222-3333-444455556666"
        cake = b'{"id":"%s","item":"cake","qty":1,"at":"2026-01-02T03:04:05Z"}' % other
        listing = b'{"orders":[%s,%s]}' % (cake, first)
        return [Actual("ready"), reply("a", 201, loc(UUID), first), reply("b", 201, loc(second_location), second), reply("a", 200, body=first),
                reply("b", 201, loc(other), cake), reply("a", 200, body=listing), Actual("exit", status=0)]

    def x3_variables(self):
        text = lambda b: rf.Bound(b, rs_json.parse('"%s"' % b.decode()).value)
        return {"order": text(UUID), "other": text(b"1c6f1c2a-1111-2222-3333-444455556666"), "at": text(b"2026-01-02T03:04:05Z")}

    def test_The_web_example_passes_when_both_racing_requests_name_one_order(self):
        t = self.load("x3.out")
        verdict = rm.compare(rt.expand(t), rt.checks_of(t), self.x3_actual(UUID, UUID), self.x3_variables())
        self.assertTrue(verdict.ok, verdict)

    def test_A_toy_that_lets_two_racing_requests_make_two_orders_fails_at_entry_3_on_the_id(self):
        t = self.load("x3.out")
        second = b"5d2a1c2a-1111-2222-3333-444455556666"
        verdict = rm.compare(rt.expand(t), rt.checks_of(t), self.x3_actual(second, UUID), self.x3_variables())
        self.assertEqual((verdict.reason, verdict.part), ("entry 3 of 7 differs", "body $.id"))
        self.assertEqual(verdict.detail, f'expected "${{order}}" = "{UUID.decode()}", actual "{second.decode()}"')


if __name__ == "__main__":
    unittest.main()
