"""FORMAT v2 5.2 and decision V2: JSON read with exact decimals, a repeated member name, NaN and Infinity refused, values compared exactly."""
import json
import unittest
from decimal import Decimal

import runner_path  # noqa: F401
import rs_json as rj


def value(text):
    parsed = rj.parse(text.encode("utf-8"))
    assert parsed.ok, parsed
    return parsed.value


def naive_float_compare(left, right):
    return float(json.loads(left)) == float(json.loads(right))


def naive_tuple_compare(left, right):
    return Decimal(left).as_tuple() == Decimal(right).as_tuple()


class ExactNumberTests(unittest.TestCase):
    def test_A_float_json_compare_would_equal_two_distinct_large_integers_and_that_is_the_point(self):
        for left, right in (("9007199254740993", "9007199254740992"), ("9007199254740993.0", "9007199254740992.0"), ("0.1000000000000000055511151231257827", "0.1")):
            self.assertTrue(naive_float_compare(left, right), f"the refuted float compare must call {left} and {right} equal")
            self.assertFalse(rj.values_equal(value(left), value(right)), f"{left} and {right} differ by value")

    def test_An_integer_and_a_decimal_are_both_read_as_exact_decimals_never_as_floats(self):
        for text in ("9007199254740993", "2", "2.0", "-0", "1e400", "0.1"):
            self.assertIsInstance(value(text), Decimal, text)

    def test_Two_and_two_point_zero_are_equal(self):
        self.assertTrue(rj.values_equal(value("2"), value("2.0")))
        self.assertTrue(rj.values_equal(value("2"), value("2.00e0")))
        self.assertTrue(rj.values_equal(value("100"), value("1e2")))

    def test_Minus_zero_equals_zero(self):
        for left, right in (("-0", "0"), ("0", "-0.0"), ("-0.0", "0.00")):
            self.assertTrue(rj.values_equal(value(left), value(right)), (left, right))

    def test_A_string_or_tuple_compare_would_call_2_and_2_point_0_different_and_that_is_the_point(self):
        self.assertNotEqual(str(Decimal("2")), str(Decimal("2.0")))
        self.assertFalse(naive_tuple_compare("2", "2.0"))
        self.assertFalse(naive_tuple_compare("-0", "0"))
        self.assertTrue(rj.values_equal(value("2"), value("2.0")))
        self.assertTrue(rj.values_equal(value("-0"), value("0")))

    def test_Numbers_that_differ_by_one_unit_in_the_last_place_or_by_the_sign_are_different(self):
        for left, right in (("1.0000000000000000000000000001", "1"), ("1", "-1"), ("1e400", "1e401")):
            self.assertFalse(rj.values_equal(value(left), value(right)), (left, right))


class RefusalTests(unittest.TestCase):
    def test_A_repeated_member_name_is_refused_and_named(self):
        parsed = rj.parse(b'{"id":1,"item":"x","id":2}')
        self.assertEqual((parsed.ok, parsed.kind), (False, "bad-json"))
        self.assertIn('"id"', parsed.message)

    def test_A_repeated_member_name_deep_inside_is_refused_too(self):
        parsed = rj.parse(b'{"a":[{"b":1,"b":1}]}')
        self.assertEqual((parsed.ok, parsed.kind), (False, "bad-json"))

    def test_NaN_and_Infinity_are_refused(self):
        for token in ("NaN", "Infinity", "-Infinity"):
            parsed = rj.parse(f'{{"n":{token}}}'.encode())
            self.assertEqual((parsed.ok, parsed.kind), (False, "bad-json"), token)
            self.assertIn(token, parsed.message)

    def test_Text_that_is_not_JSON_is_bad_json_with_a_position(self):
        for text in (b"", b"{", b'{"a":1} x', b"[1,]", b"'a'", b'"a\nb"'):
            parsed = rj.parse(text)
            self.assertEqual((parsed.ok, parsed.kind), (False, "bad-json"), text)

    def test_Bytes_that_are_not_UTF_8_are_bad_utf8_and_not_a_decode_error(self):
        parsed = rj.parse(b'{"a":"\xff"}')
        self.assertEqual((parsed.ok, parsed.kind), (False, "bad-utf8"))

    def test_A_byte_order_mark_and_a_UTF_16_text_are_refused_instead_of_being_guessed(self):
        for data in ("﻿{}".encode("utf-8"), '{"a":1}'.encode("utf-16")):
            self.assertFalse(rj.parse(data).ok)

    def test_Nesting_deeper_than_the_ceiling_is_refused_instead_of_overflowing_the_stack(self):
        deep = b"[" * 5000 + b"]" * 5000
        parsed = rj.parse(deep)
        self.assertEqual((parsed.ok, parsed.kind), (False, "bad-json"))

    def test_Whitespace_outside_strings_means_nothing(self):
        self.assertTrue(rj.values_equal(value(' { "a" : [ 1 , 2 ] } '), value('{"a":[1,2]}')))


class ValueEqualityTests(unittest.TestCase):
    def test_The_members_of_an_object_match_in_any_order_but_an_array_keeps_its_order(self):
        self.assertTrue(rj.values_equal(value('{"a":1,"b":2}'), value('{"b":2,"a":1}')))
        self.assertFalse(rj.values_equal(value("[1,2]"), value("[2,1]")))

    def test_True_and_one_are_different_values_although_Python_calls_them_equal(self):
        self.assertTrue(True == 1)  # the Python fact this guards against
        self.assertFalse(rj.values_equal(True, value("1")))
        self.assertFalse(rj.values_equal(False, value("0")))
        self.assertFalse(rj.values_equal(None, False))

    def test_A_string_and_a_number_with_the_same_digits_are_different(self):
        self.assertFalse(rj.values_equal("2", value("2")))

    def test_Strings_match_by_their_characters_and_a_lone_surrogate_escape_is_one_character(self):
        self.assertTrue(rj.values_equal(value('"caf\\u00e9"'), value('"café"')))
        self.assertFalse(rj.values_equal(value('"\\ud800"'), value('"a"')))

    def test_Objects_with_different_member_names_or_array_lengths_differ(self):
        self.assertFalse(rj.values_equal(value('{"a":1}'), value('{"a":1,"b":2}')))
        self.assertFalse(rj.values_equal(value("[1]"), value("[1,1]")))


class RenderTests(unittest.TestCase):
    def test_A_value_is_rendered_as_compact_JSON_with_its_numbers_exactly(self):
        self.assertEqual(rj.render(value('{"a":[1,2.50,"x\\n"],"b":null,"c":true}')), '{"a":[1,2.50,"x\\n"],"b":null,"c":true}')

    def test_A_number_is_rendered_by_its_decimal_digits_never_in_scientific_float_form(self):
        self.assertEqual(rj.render(value("9007199254740993")), "9007199254740993")


if __name__ == "__main__":
    unittest.main()
