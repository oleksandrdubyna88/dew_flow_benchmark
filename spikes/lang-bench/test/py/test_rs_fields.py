"""The normalised fields of FORMAT v2 5.3 and the backtracking matcher of one expected string against actual bytes (V6 binds)."""
import time
import unittest
from datetime import datetime, timedelta, timezone
from decimal import Decimal

import runner_path  # noqa: F401
import rs_fields as rf
import rs_lex as lex

CLOCK = datetime(2026, 1, 2, 3, 4, 5, tzinfo=timezone.utc)


def parts(text):
    cur = lex.Cursor(f'"{text}"', "t", 1, [], allow_fields=True)
    return cur.take().value.parts


def match(template, data, variables=None):
    resolved, error = rf.resolve(lex.StrExpr((lex.StrItem(tuple(parts(template))),)), variables or {})
    assert error is None, error
    return rf.match_template(resolved, data, variables or {}, CLOCK)


class FieldTests(unittest.TestCase):
    def test_Each_field_accepts_its_form_and_refuses_a_near_miss(self):
        cases = {
            "int": ((b"0", b"-12", b"7"), (b"01", b"+1", b"1.0", b"")),
            "uint": ((b"0", b"42"), (b"-1", b"007", b"")),
            "hex": ((b"0a9f", b"7"), (b"A", b"xyz", b"")),
            "uuid": ((b"0b6f1c2a-1111-2222-3333-444455556666", b"0B6F1C2A-1111-2222-3333-444455556666"),
                     (b"0b6f1c2a11112222333344445555666", b"g" * 36)),
            "token": ((b"a.b_c~d-9", b"A"), (b"a b", b"a/b", b"")),
            "ts": ((b"2026-01-02T03:04:05Z", b"2026-01-02T03:04:05.123456789Z"),
                   (b"2026-01-02 03:04:05Z", b"2026-13-02T03:04:05Z", b"2026-01-02T03:04:05")),
        }
        for kind, (accepted, refused) in cases.items():
            self.assertEqual([rf.field_accepts(lex.Field(kind), s, CLOCK) for s in accepted], [True] * len(accepted), kind)
            self.assertEqual([rf.field_accepts(lex.Field(kind), s, CLOCK) for s in refused], [False] * len(refused), kind)

    def test_A_timestamp_window_is_measured_from_the_runner_clock_in_both_directions(self):
        window = lex.Field("ts~", (10_000,))
        near, far = b"2026-01-02T03:04:14Z", b"2026-01-02T03:04:16Z"
        self.assertTrue(rf.field_accepts(window, near, CLOCK))
        self.assertFalse(rf.field_accepts(window, far, CLOCK))
        self.assertTrue(rf.field_accepts(window, b"2026-01-02T03:03:56Z", CLOCK))
        self.assertFalse(rf.field_accepts(window, b"2026-01-02T03:03:54Z", CLOCK))

    def test_A_numeric_tolerance_is_exact_and_includes_its_edge(self):
        field = lex.Field("num", (Decimal("1.5"), Decimal("0.25")))
        self.assertTrue(rf.field_accepts(field, b"1.75", CLOCK))
        self.assertTrue(rf.field_accepts(field, b"1.25", CLOCK))
        self.assertFalse(rf.field_accepts(field, b"1.7500000000000000000001", CLOCK))
        self.assertFalse(rf.field_accepts(field, b"abc", CLOCK))

    def test_A_timestamp_parses_to_a_UTC_time_and_a_non_date_does_not(self):
        self.assertEqual(rf.parse_ts(b"2026-01-02T03:04:05.5Z"), CLOCK.replace(microsecond=500000))
        self.assertIsNone(rf.parse_ts(b"2026-02-30T03:04:05Z"))

    def test_The_text_of_a_field_is_written_back_as_it_is_in_a_transcript(self):
        self.assertEqual(rf.field_text(lex.Field("uint")), "${:uint}")
        self.assertEqual(rf.field_text(lex.Field("ts~", (10_000,))), "${:ts~10000ms}")
        self.assertEqual(rf.field_text(lex.Field("int", (), (lex.Lit(b"r"), lex.Var("i")))), "${:int=r${i}}")


class TimeDigitTests(unittest.TestCase):
    """`${:ts.<d>}`: a time with exactly d fraction digits, d from 0 to 9; `${:ts.<d>~<window>}` also within the window of the clock."""

    def accepts(self, field_text, run):
        field = parts(field_text)[0]
        return rf.field_accepts(field, run, CLOCK)

    def test_A_time_with_exactly_d_fraction_digits_matches_and_every_other_count_does_not(self):
        times = {0: b"2026-01-02T03:04:05Z", 1: b"2026-01-02T03:04:05.1Z", 3: b"2026-01-02T03:04:05.123Z", 9: b"2026-01-02T03:04:05.123456789Z"}
        for d in (0, 1, 3, 9):
            for have, run in times.items():
                self.assertEqual(self.accepts("${:ts.%d}" % d, run), d == have, (d, have))

    def test_Ts_zero_means_no_fraction_and_no_dot(self):
        self.assertFalse(self.accepts("${:ts.0}", b"2026-01-02T03:04:05.Z"))
        self.assertFalse(self.accepts("${:ts.3}", b"2026-01-02T03:04:05.Z"))

    def test_The_digit_count_field_still_needs_a_real_time(self):
        self.assertFalse(self.accepts("${:ts.3}", b"2026-02-30T03:04:05.123Z"))

    def test_The_digit_count_and_the_window_apply_together(self):
        self.assertTrue(self.accepts("${:ts.3~10s}", b"2026-01-02T03:04:10.000Z"))
        self.assertFalse(self.accepts("${:ts.3~10s}", b"2026-01-02T03:04:10Z"), "wrong digit count")
        self.assertFalse(self.accepts("${:ts.3~10s}", b"2026-01-02T03:05:10.000Z"), "outside the window")

    def test_The_digit_count_fields_match_inside_a_string_with_a_fixed_length_so_the_shortest_run_is_the_only_run(self):
        self.assertTrue(match("at ${:ts.3}!", b"at 2026-01-02T03:04:05.123Z!").ok)
        self.assertFalse(match("at ${:ts.3}!", b"at 2026-01-02T03:04:05.12Z!").ok)
        self.assertEqual(rf.field_text(parts("${:ts.3~10s=w}")[0]), "${:ts.3~10000ms=w}")


class MatchTests(unittest.TestCase):
    def test_A_literal_string_matches_only_the_same_bytes_and_names_the_first_difference(self):
        self.assertTrue(match("0002|ok", b"0002|ok").ok)
        result = match("0004|kiwi", b"0004|kiwx")
        self.assertEqual((result.ok, result.furthest), (False, 8), "the first difference is at byte 8 (counting from 0): 'i' against 'x'")

    def test_A_field_matches_the_shortest_run_that_lets_the_rest_match(self):
        result = match("${:token}.${:token}", b"a.b.c")
        self.assertTrue(result.ok)
        result = match("${:int=a}${:int=b}", b"123")
        self.assertEqual([(n, v.raw) for n, v in result.binds], [("a", b"1"), ("b", b"23")])

    def test_A_field_between_literals_may_hold_a_run_that_contains_the_next_literal_when_that_is_the_only_fit(self):
        result = match("<${:token=t}>x", b"<a>x>x"[:5] + b"")
        self.assertFalse(result.ok, "a token cannot hold '>'")
        result = match("${:token=t}.x", b"a.b.x")
        self.assertEqual([(n, v.raw) for n, v in result.binds], [("t", b"a.b")])

    def test_A_bound_field_names_the_bytes_it_matched_and_a_repeat_variable_fills_its_name(self):
        variables = {"i": rf.Bound(b"2")}
        result = match("${:uuid=r${i}}", b"0b6f1c2a-1111-2222-3333-444455556666", variables)
        self.assertEqual([(n, v.raw) for n, v in result.binds], [("r2", b"0b6f1c2a-1111-2222-3333-444455556666")])

    def test_A_variable_is_replaced_by_its_bytes_before_matching(self):
        variables = {"order": rf.Bound(b"abc")}
        self.assertTrue(match("/orders/${order}", b"/orders/abc", variables).ok)
        self.assertFalse(match("/orders/${order}", b"/orders/abd", variables).ok)

    def test_An_unbound_variable_is_reported_by_name_and_is_not_a_crash(self):
        _, error = rf.resolve(lex.StrExpr((lex.StrItem(tuple(parts("${ghost}"))),)), {})
        self.assertIn("ghost", error)

    def test_A_repeated_string_is_written_out_and_a_long_literal_matches_quickly(self):
        expr = lex.StrExpr((lex.StrItem((lex.Lit(b"$16384\r\n"),)), lex.StrItem((lex.Lit(b"v"),), 16384), lex.StrItem((lex.Lit(b"\r\n"),))))
        resolved, _ = rf.resolve(expr, {})
        self.assertEqual(len(resolved), 1)
        started = time.monotonic()
        self.assertTrue(rf.match_template(resolved, b"$16384\r\n" + b"v" * 16384 + b"\r\n", {}, CLOCK).ok)
        self.assertLess(time.monotonic() - started, 1.0)

    def test_Fields_that_cannot_match_do_not_make_the_matcher_exponential(self):
        template = "${:hex}" * 12 + "z"
        started = time.monotonic()
        result = match(template, b"a" * 2000)
        self.assertFalse(result.ok)
        self.assertIn("too complex", result.note)
        self.assertLess(time.monotonic() - started, 5.0)

    def test_A_memo_of_failed_positions_lets_an_ambiguous_failure_end_cleanly_inside_the_step_budget(self):
        result = match("${:hex}" * 8 + "!", b"a" * 40 + b"z")
        self.assertEqual((result.ok, result.note), (False, ""), "without the memo this search is abandoned as too complex")

    def test_A_string_of_more_than_two_hundred_parts_is_refused_instead_of_recursing_without_end(self):
        _, error = rf.resolve(lex.StrExpr((lex.StrItem(tuple(parts("${:int}")), 300),)), {})
        self.assertIn("too many parts", error)

    def test_A_timestamp_field_in_a_string_uses_the_clock_it_is_given(self):
        self.assertTrue(match("at ${:ts~10s}", b"at 2026-01-02T03:04:10Z").ok)
        self.assertFalse(match("at ${:ts~10s}", b"at 2026-01-02T03:05:10Z").ok)

    def test_A_numeric_field_with_a_huge_exponent_is_refused_without_building_a_huge_fraction(self):
        self.assertIsNone(rf.fraction_of(Decimal("1E+999999999")))
        self.assertFalse(rf.within_tolerance(Decimal("1E+999999999"), Decimal("1"), Decimal("1")))


if __name__ == "__main__":
    unittest.main()
