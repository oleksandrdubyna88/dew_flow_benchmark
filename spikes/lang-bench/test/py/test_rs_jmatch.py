"""An actual JSON body against an expected `body json` value (FORMAT v2 5.2 to 5.4, V2, V6): paths, types, templates, $unordered."""
import unittest
from datetime import datetime, timezone

import runner_path  # noqa: F401
import rs_fields as rf
import rs_json
import rs_jmatch as jm

CLOCK = datetime(2026, 1, 2, 3, 4, 5, tzinfo=timezone.utc)


def check(expected, actual, variables=None):
    return jm.match_json_body(rs_json.parse(expected).value, actual.encode("utf-8"), variables or {}, CLOCK)


class ValueTests(unittest.TestCase):
    def test_Objects_match_in_any_member_order_and_arrays_in_order(self):
        self.assertTrue(check('{"a":1,"b":[1,2]}', '{"b":[1,2],"a":1}').ok)
        self.assertEqual(check("[1,2]", "[2,1]").diff.path, "$[0]")

    def test_Numbers_match_by_exact_value_so_two_matches_two_point_zero_and_minus_zero_matches_zero(self):
        self.assertTrue(check('{"n":2}', '{"n":2.0}').ok)
        self.assertTrue(check('{"n":-0}', '{"n":0}').ok)
        self.assertFalse(check('{"n":9007199254740993}', '{"n":9007199254740992}').ok)

    def test_The_first_difference_names_its_path_and_both_values(self):
        result = check('{"orders":[{"id":1,"qty":2}]}', '{"orders":[{"id":1,"qty":3}]}')
        self.assertEqual(result.diff.text(), "body $.orders[0].qty: expected 2, actual 3")

    def test_A_member_name_that_is_not_an_identifier_is_written_in_brackets(self):
        self.assertEqual(jm.member_path("$", "a-b"), '$["a-b"]')
        self.assertEqual(jm.member_path("$.x", "ok_1"), "$.x.ok_1")

    def test_A_missing_member_and_an_extra_member_are_both_differences(self):
        self.assertIn("no such member", check('{"a":1,"b":2}', '{"a":1}').diff.text())
        self.assertIn("no such member", check('{"a":1}', '{"a":1,"b":2}').diff.text())

    def test_A_value_of_another_kind_is_a_difference_and_true_is_not_one(self):
        self.assertFalse(check('{"a":1}', '{"a":true}').ok)
        self.assertFalse(check('{"a":true}', '{"a":1}').ok)
        self.assertFalse(check('{"a":null}', '{"a":false}').ok)
        self.assertFalse(check('{"a":"1"}', '{"a":1}').ok)
        self.assertFalse(check('{"a":[1]}', '{"a":{"0":1}}').ok)

    def test_An_empty_expected_object_does_not_match_an_array_or_a_string(self):
        self.assertFalse(check('{"a":{}}', '{"a":[]}').ok)
        self.assertFalse(check('{"a":{}}', '{"a":""}').ok)
        self.assertFalse(check('{"a":[]}', '{"a":{}}').ok)

    def test_An_array_of_another_length_is_a_difference_naming_both_lengths(self):
        self.assertEqual(check("[1,2]", "[1]").diff.text(), "body $: expected an array of 2, actual an array of 1")


class BodyReadingTests(unittest.TestCase):
    def test_An_invalid_UTF_8_JSON_body_is_bad_utf8(self):
        result = jm.match_json_body(rs_json.parse('{"a":1}').value, b'{"a":"\xff"}', {}, CLOCK)
        self.assertEqual((result.ok, result.kind), (False, "bad-utf8"))

    def test_A_body_with_a_repeated_member_name_is_bad_json_and_names_the_member(self):
        result = check('{"a":1}', '{"a":1,"a":1}')
        self.assertEqual((result.ok, result.kind), (False, "bad-json"))
        self.assertIn('"a"', result.message)

    def test_A_body_with_NaN_or_Infinity_is_bad_json_and_names_the_token(self):
        for token in ("NaN", "Infinity", "-Infinity"):
            result = check('{"a":1}', '{"a":%s}' % token)
            self.assertEqual((result.ok, result.kind), (False, "bad-json"), token)
            self.assertIn(token, result.message)


class TemplateTests(unittest.TestCase):
    def test_A_variable_that_is_a_whole_string_matches_a_value_equal_to_the_variables_and_keeps_its_type(self):
        variables = {"n": rf.Bound(b"2", rs_json.parse("2").value), "s": rf.Bound(b"abc")}
        self.assertTrue(check('{"n":"${n}","s":"${s}"}', '{"n":2.0,"s":"abc"}', variables).ok)
        self.assertFalse(check('{"n":"${n}"}', '{"n":"2"}', variables).ok, "a captured number is not a string")
        self.assertFalse(check('{"s":"${s}"}', '{"s":"abd"}', variables).ok)

    def test_A_variable_difference_shows_the_variable_and_its_value(self):
        variables = {"order": rf.Bound(b"0b6f")}
        result = check('{"id":"${order}"}', '{"id":"5d2a"}', variables)
        self.assertEqual(result.diff.text(), 'body $.id: expected "${order}" = "0b6f", actual "5d2a"')

    def test_An_unbound_variable_is_a_difference_naming_it_and_not_a_crash(self):
        self.assertIn("ghost", check('{"id":"${ghost}"}', '{"id":"x"}').diff.text())

    def test_A_field_that_is_a_whole_string_matches_by_its_rule_for_a_number_or_a_string(self):
        self.assertTrue(check('{"q":"${:int}","u":"${:uint}","id":"${:uuid}","t":"${:token}","h":"${:hex}"}',
                              '{"q":-3,"u":4,"id":"0b6f1c2a-1111-2222-3333-444455556666","t":"a.b","h":"0f"}').ok)
        self.assertFalse(check('{"q":"${:int}"}', '{"q":1.5}').ok)
        self.assertFalse(check('{"u":"${:uint}"}', '{"u":-1}').ok)
        self.assertFalse(check('{"q":"${:int}"}', '{"q":"7"}').ok, "in JSON an int field is a number, not a string")
        self.assertFalse(check('{"id":"${:uuid}"}', '{"id":7}').ok)

    def test_A_timestamp_window_and_a_numeric_tolerance_apply_in_JSON(self):
        self.assertTrue(check('{"at":"${:ts~10s}"}', '{"at":"2026-01-02T03:04:10Z"}').ok)
        self.assertFalse(check('{"at":"${:ts~10s}"}', '{"at":"2026-01-02T04:04:10Z"}').ok)
        self.assertTrue(check('{"p":"${:num 1.5 0.25}"}', '{"p":1.74}').ok)
        self.assertFalse(check('{"p":"${:num 1.5 0.25}"}', '{"p":1.76}').ok)

    def test_A_field_with_a_bind_keeps_the_typed_value_for_a_later_check(self):
        result = check('{"id":"${:uuid=u}","n":"${:int=count}"}', '{"id":"0b6f1c2a-1111-2222-3333-444455556666","n":5}')
        binds = dict(result.binds)
        self.assertEqual(binds["count"].json, rs_json.parse("5").value)
        self.assertEqual(binds["u"].raw, b"0b6f1c2a-1111-2222-3333-444455556666")

    def test_A_string_that_mixes_text_and_templates_is_matched_as_a_string(self):
        variables = {"order": rf.Bound(b"abc")}
        self.assertTrue(check('{"loc":"/orders/${order}?t=${:uint}"}', '{"loc":"/orders/abc?t=77"}', variables).ok)
        self.assertFalse(check('{"loc":"/orders/${order}?t=${:uint}"}', '{"loc":"/orders/abc?t=x"}', variables).ok)

    def test_A_mixed_template_against_a_number_is_a_difference_and_not_a_crash(self):
        self.assertFalse(check('{"a":"x${:int}"}', '{"a":5}').ok)
        self.assertFalse(check('{"a":"x${y}"}', '{"a":5}', {"y": rf.Bound(b"1")}).ok)

    def test_A_field_with_a_bind_inside_a_mixed_string_binds_what_it_matched(self):
        result = check('{"loc":"/orders/${:uuid=u}"}', '{"loc":"/orders/0b6f1c2a-1111-2222-3333-444455556666"}')
        self.assertEqual({n: b.raw for n, b in result.binds}, {"u": b"0b6f1c2a-1111-2222-3333-444455556666"})

    def test_A_template_in_a_string_matches_a_lone_surrogate_escape_by_its_characters(self):
        self.assertTrue(check('{"a":"\\ud800${:int}"}', '{"a":"\\ud8007"}').ok)


class TrialTests(unittest.TestCase):
    def test_A_trial_context_is_an_isolated_copy_so_no_binding_or_variable_leaks_between_candidates(self):
        ctx = jm.Ctx({"v": rf.Bound(b"1")}, CLOCK)
        trial = ctx.trial()
        trial.binds.append(("x", rf.Bound(b"2")))
        trial.variables["w"] = rf.Bound(b"3")
        self.assertEqual((ctx.binds, sorted(ctx.variables)), ([], ["v"]))
        self.assertIsNot(trial.variables, ctx.variables)

    def test_Candidates_tried_in_the_assignment_search_leave_no_binding_in_the_context(self):
        result = check('{"l":{"$unordered":[{"id":"${:uint=a}","t":"x"},{"id":"${:uint=b}","t":"y"},{"id":"${:uint=c}","t":"z"}]}}',
                       '{"l":[{"id":3,"t":"z"},{"id":1,"t":"x"},{"id":2,"t":"y"}]}')
        self.assertTrue(result.ok)
        self.assertEqual({n: b.raw for n, b in result.binds}, {"a": b"1", "b": b"2", "c": b"3"})
        self.assertEqual(len(result.binds), 3, "each binding once: candidate evaluations left nothing behind")


class HugeNumberTests(unittest.TestCase):
    def test_A_number_field_over_a_5000_digit_number_is_compared_and_does_not_crash(self):
        digits = "1" * 5000
        self.assertTrue(check('["${:num %s 0}"]' % digits, "[%s]" % digits).ok)
        self.assertFalse(check('["${:num %s 0}"]' % digits, "[%s2]" % digits[:-1]).ok)

    def test_A_number_field_over_one_and_a_thousand_and_one_zeros_is_one(self):
        self.assertTrue(check('["${:num 1 0}"]', "[1." + "0" * 1001 + "]").ok, "the 1000-exponent cap made this a wrong FAIL")

    def test_A_number_too_large_to_compare_is_a_FAIL_that_says_so(self):
        for text in ("[1e999999999999]", "[1e-999999999999]", "[" + "1" * 300_000 + "]"):
            result = check('["${:num 1 0.5}"]', text)
            self.assertFalse(result.ok)
            self.assertIn("too large to compare exactly", result.diff.text(), text[:12])

    def test_Plain_values_of_that_size_still_compare_by_exact_value_without_a_crash(self):
        self.assertTrue(check("[1e999999999999]", "[1E+999999999999]").ok)
        self.assertFalse(check("[1e999999999999]", "[1e999999999998]").ok)

    def test_An_integer_field_over_a_huge_exponent_number_does_not_crash(self):
        self.assertTrue(check('["${:int}"]', "[1e999999999999]").ok)
        self.assertFalse(check('["${:uint}"]', "[-1e999999999999]").ok)


class UnorderedTests(unittest.TestCase):
    def test_An_unordered_array_matches_its_elements_in_any_order(self):
        self.assertTrue(check('{"l":{"$unordered":[1,2,3]}}', '{"l":[3,1,2]}').ok)

    def test_An_unordered_array_needs_a_one_to_one_assignment_so_a_repeated_element_cannot_match_twice(self):
        self.assertFalse(check('{"l":{"$unordered":[1,1,2]}}', '{"l":[1,2,2]}').ok)

    def test_The_assignment_is_found_even_when_the_first_choice_would_strand_a_later_element(self):
        expected = '{"l":{"$unordered":[{"k":"${:int}"},{"k":1}]}}'
        self.assertTrue(check(expected, '{"l":[{"k":1},{"k":2}]}').ok, "a greedy pass gives the field element the 1 and strands {k:1}")

    def test_An_unordered_array_of_another_length_or_kind_is_a_difference(self):
        self.assertFalse(check('{"l":{"$unordered":[1,2]}}', '{"l":[1]}').ok)
        self.assertFalse(check('{"l":{"$unordered":[1]}}', '{"l":[1,2]}').ok, "an extra actual element is not ignored")
        self.assertFalse(check('{"l":{"$unordered":[1,2]}}', '{"l":{"a":1}}').ok)

    def test_An_element_that_nothing_matches_is_named(self):
        result = check('{"l":{"$unordered":[1,9]}}', '{"l":[1,2]}')
        self.assertIn("9", result.diff.text())
        self.assertEqual(result.diff.path, "$.l")

    def test_Fields_inside_unordered_elements_bind_only_for_the_pairs_that_were_assigned(self):
        result = check('{"l":{"$unordered":[{"id":"${:uint=a}","t":"x"},{"id":"${:uint=b}","t":"y"}]}}',
                       '{"l":[{"id":7,"t":"y"},{"id":8,"t":"x"}]}')
        self.assertTrue(result.ok)
        self.assertEqual({n: b.raw for n, b in result.binds}, {"a": b"8", "b": b"7"})


if __name__ == "__main__":
    unittest.main()
