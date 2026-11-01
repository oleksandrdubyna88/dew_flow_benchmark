"""The lexical rules of FORMAT v2 section 4.1: lines, tokens, durations, strings and their escapes, variables, normalised fields."""
import unittest
from decimal import Decimal

import runner_path  # noqa: F401
import rs_lex as lex


def cursor(text, allow_fields=False):
    problems = []
    return lex.Cursor(text, "t.in", 1, problems, allow_fields), problems


def first_string(text, allow_fields=False):
    cur, problems = cursor(text, allow_fields)
    tok = cur.take()
    return tok, problems


def joined(tok):
    return b"".join(p.data for p in tok.value.parts)


class LineTests(unittest.TestCase):
    def test_A_file_is_split_into_numbered_lines_and_its_last_line_feed_ends_a_line_not_an_empty_one(self):
        problems = []
        self.assertEqual(lex.split_lines(b"scenario 2\n\n# c\n", "t.in", problems), [(1, "scenario 2"), (2, ""), (3, "# c")])
        self.assertEqual(problems, [])

    def test_A_file_without_a_final_line_feed_is_a_problem_naming_its_last_line(self):
        problems = []
        lex.split_lines(b"a\nb", "t.in", problems)
        self.assertEqual([(p.line, "line feed" in p.message) for p in problems], [(2, True)])

    def test_A_byte_order_mark_a_tab_and_a_carriage_return_are_problems_with_their_line(self):
        problems = []
        lex.split_lines("﻿a\n\tb\nc\r\n".encode("utf-8"), "t.in", problems)
        self.assertEqual(sorted(p.line for p in problems), [1, 2, 3])

    def test_A_tab_inside_a_string_is_a_problem_too_because_a_raw_control_character_is_never_allowed(self):
        problems = []
        lex.split_lines(b'send c "a\tb"\n', "t.in", problems)
        self.assertEqual([p.line for p in problems], [1])

    def test_Invalid_UTF_8_is_a_problem_on_the_line_that_holds_it_and_no_line_is_returned(self):
        problems = []
        self.assertEqual(lex.split_lines(b"a\nb\xff\n", "t.in", problems), [])
        self.assertEqual([p.line for p in problems], [2])

    def test_A_blank_line_and_a_comment_line_are_recognised_but_a_trailing_comment_is_not_one(self):
        self.assertTrue(lex.is_blank_or_comment("   "))
        self.assertTrue(lex.is_blank_or_comment("   # note"))
        self.assertFalse(lex.is_blank_or_comment("sleep 1s # note"))


class TokenTests(unittest.TestCase):
    def test_Tokens_are_classified_as_words_integers_durations_lists_and_json_paths(self):
        cur, problems = cursor("recv c1 16 250ms 1,9 $.orders[0].id wait-exit")
        kinds = []
        while not cur.at_end():
            kinds.append(cur.take().kind)
        self.assertEqual(kinds, ["word", "word", "int", "dur", "list", "path", "word"])
        self.assertEqual(problems, [])

    def test_A_duration_is_an_integer_and_a_unit_and_at_most_600_seconds(self):
        self.assertEqual(lex.duration_ms("250ms"), 250)
        self.assertEqual(lex.duration_ms("2s"), 2000)
        self.assertEqual(lex.duration_ms("600s"), 600_000)
        self.assertIsNone(lex.duration_ms("601s"))
        self.assertIsNone(lex.duration_ms("2m"))
        self.assertIsNone(lex.duration_ms("-1s"))

    def test_The_rest_of_a_line_is_returned_raw_for_json_text_and_never_lexed(self):
        cur, problems = cursor('body json {"a": "b c", "n": [1, 2.0]}')
        cur.take()
        cur.take()
        self.assertEqual(cur.rest(), '{"a": "b c", "n": [1, 2.0]}')
        self.assertEqual(problems, [])

    def test_Peeking_a_token_consumes_nothing_and_leaves_no_problem_behind(self):
        cur, problems = cursor('"unclosed')
        self.assertIsNone(cur.peek_kind())
        self.assertEqual((cur.pos, problems), (0, []))


class StringTests(unittest.TestCase):
    def test_Every_escape_of_the_format_stands_for_its_bytes(self):
        tok, problems = first_string(r'"\\ \" \$ \r \n \t \0 \x41 \xff \xAb"')
        self.assertEqual(problems, [])
        self.assertEqual(joined(tok), b'\\ " $ \r \n \t \0 A \xff \xab')

    def test_A_character_outside_ASCII_stands_for_its_UTF_8_bytes(self):
        tok, _ = first_string('"café €"')
        self.assertEqual(joined(tok), "café €".encode("utf-8"))

    def test_An_escaped_dollar_before_a_brace_is_three_literal_bytes_not_a_variable(self):
        tok, _ = first_string(r'"\${name}"')
        self.assertEqual(tok.value.parts, (lex.Lit(b"${name}"),))

    def test_A_dollar_not_followed_by_a_brace_is_an_ordinary_byte(self):
        tok, _ = first_string('"$5 and $"')
        self.assertEqual(joined(tok), b"$5 and $")

    def test_A_variable_is_a_part_between_two_literals(self):
        tok, _ = first_string('"/orders/${order}/x"')
        self.assertEqual(tok.value.parts, (lex.Lit(b"/orders/"), lex.Var("order"), lex.Lit(b"/x")))

    def test_An_unknown_escape_a_short_hex_escape_and_an_unclosed_string_are_problems(self):
        for text in ['"\\q"', '"\\x4"', '"\\xzz"', '"abc', '"${Bad}"', '"${open"']:
            tok, problems = first_string(text)
            self.assertIsNone(tok, text)
            self.assertEqual(len(problems), 1, text)

    def test_A_string_followed_directly_by_a_star_and_a_count_is_repeated(self):
        tok, _ = first_string('"v"*16384')
        self.assertEqual((tok.value.count, joined(tok)), (16384, b"v"))

    def test_A_repeat_count_of_zero_a_missing_count_or_one_past_ten_million_is_a_problem(self):
        for text in ['"v"*0', '"v"*', '"v"*10000001', '"v"*x']:
            tok, problems = first_string(text)
            self.assertIsNone(tok, text)
            self.assertEqual(len(problems), 1, text)

    def test_Adjacent_strings_are_joined_into_one_expression_and_a_non_string_ends_it(self):
        cur, problems = cursor('"$16384\\r\\n" "v"*3 "\\r\\n" within 2s')
        expr = lex.take_str_expr(cur)
        self.assertEqual([i.count for i in expr.items], [1, 3, 1])
        self.assertEqual(cur.take().text, "within")
        self.assertEqual(problems, [])

    def test_A_string_expression_without_a_string_is_a_problem(self):
        cur, problems = cursor("within 2s")
        self.assertIsNone(lex.take_str_expr(cur))
        self.assertEqual(len(problems), 1)

    def test_A_plain_string_refuses_a_repeat_count_because_a_header_name_is_exactly_one_string(self):
        cur, problems = cursor('"x-a"*2')
        self.assertIsNone(lex.take_single_string(cur))
        self.assertEqual(len(problems), 1)

    def test_The_variables_a_string_expression_uses_are_listed_in_order(self):
        cur, _ = cursor('"${a}" "x" "${b}${a}"')
        self.assertEqual(lex.vars_of(lex.take_str_expr(cur)), ["a", "b", "a"])


class FieldTests(unittest.TestCase):
    def fields(self, text):
        tok, problems = first_string(f'"{text}"', allow_fields=True)
        return (None if tok is None else tok.value.parts), problems

    def test_Every_normalised_field_of_the_closed_list_is_recognised_in_a_transcript_string(self):
        for kind in ("int", "uint", "hex", "uuid", "token", "ts"):
            parts, problems = self.fields(f"${{:{kind}}}")
            self.assertEqual((parts, problems), ((lex.Field(kind),), []), kind)

    def test_A_timestamp_window_and_a_numeric_tolerance_carry_their_arguments_exactly(self):
        parts, _ = self.fields("${:ts~10s}")
        self.assertEqual(parts, (lex.Field("ts~", (10_000,)),))
        parts, _ = self.fields("${:num 1.50 0.25}")
        self.assertEqual(parts, (lex.Field("num", (Decimal("1.50"), Decimal("0.25"))),))

    def test_A_field_may_bind_what_it_matched_and_the_name_may_carry_a_repeat_variable(self):
        parts, _ = self.fields("${:uint=r${i}}")
        self.assertEqual(parts, (lex.Field("uint", (), (lex.Lit(b"r"), lex.Var("i"))),))
        parts, _ = self.fields("${:uuid=order}")
        self.assertEqual(parts, (lex.Field("uuid", (), (lex.Lit(b"order"),)),))

    def test_A_field_outside_the_closed_list_or_with_a_bad_bound_name_is_a_problem(self):
        for text in ["${:float}", "${:uint=Bad}", "${:uint=}", "${:num 1.5}", "${:ts~601s}", "${:ts~}", "${:int x}"]:
            parts, problems = self.fields(text)
            self.assertIsNone(parts, text)
            self.assertEqual(len(problems), 1, text)

    def test_A_normalised_field_in_a_scenario_script_is_a_problem_because_only_a_transcript_has_them(self):
        tok, problems = first_string('"${:int}"', allow_fields=False)
        self.assertIsNone(tok)
        self.assertEqual(len(problems), 1)

    def test_A_field_between_literals_keeps_the_order_of_its_parts(self):
        parts, _ = self.fields("a${:hex}b")
        self.assertEqual(parts, (lex.Lit(b"a"), lex.Field("hex"), lex.Lit(b"b")))


class KeywordTests(unittest.TestCase):
    def test_The_keyword_set_holds_the_script_and_transcript_words_a_name_may_not_use(self):
        for word in ("scenario", "wait-exit", "TERM", "unordered", "check", "stdin"):
            self.assertIn(word, lex.KEYWORDS)
        self.assertNotIn("order", lex.KEYWORDS)


if __name__ == "__main__":
    unittest.main()
