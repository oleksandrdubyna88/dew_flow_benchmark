"""The scenario script (.in) parser and its validation, FORMAT v2 section 4 and decisions V1, V8, V9."""
import dataclasses
import os
import unittest

import runner_path  # noqa: F401
import rs_lex as lex
import rs_script as rs

FIXTURES = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "fixtures", "runner", "format")
HEAD = "scenario 2\ndefault within 2s\nstart ready port within 10s\nconnect c1 tcp\nconnect h http\n"


def parse(text, file="t.in"):
    return rs.parse_script(text.encode("utf-8"), file)


def body(*lines):
    return parse(HEAD + "\n".join(lines) + "\nwait-exit\n")


def messages(problems):
    return [p.message for p in problems]


def fixture(name):
    with open(os.path.join(FIXTURES, name), "rb") as f:
        return f.read()


class ExampleTests(unittest.TestCase):
    def test_The_daemon_example_of_the_format_parses_into_its_head_and_steps(self):
        scenario, problems = rs.parse_script(fixture("x2.in"), "x2.in")
        self.assertEqual(problems, [])
        self.assertEqual((scenario.default_within_ms, scenario.start.ready, scenario.start.within_ms), (2000, "port", 10_000))
        ops = [n.op for n in scenario.body if isinstance(n, rs.Step)]
        self.assertEqual(ops[:6], ["connect", "send", "quiet", "send", "recv", "send"])
        self.assertEqual(scenario.body[-1].op, "wait_exit")

    def test_The_web_example_parses_a_together_group_requests_with_headers_and_captures(self):
        scenario, problems = rs.parse_script(fixture("x3.in"), "x3.in")
        self.assertEqual(problems, [])
        group = [n for n in scenario.body if isinstance(n, rs.Together)][0]
        self.assertEqual([s.conn for s in group.steps], ["a", "b"])
        request = group.steps[0]
        self.assertEqual((request.args["method"], len(request.args["headers"]), request.args["body"][0]), ("POST", 2, "json"))
        recv = [n for n in scenario.body if isinstance(n, rs.Step) and n.op == "recv"][0]
        self.assertEqual([c.name for c in recv.args["capture"]], ["order", "at"])

    def test_The_parsed_scenario_is_frozen(self):
        scenario, _ = rs.parse_script(fixture("x3.in"), "x3.in")
        with self.assertRaises(dataclasses.FrozenInstanceError):
            scenario.default_within_ms = 1


class HeadTests(unittest.TestCase):
    def test_Scenario_1_is_a_format_error_naming_the_file_and_line_and_nothing_is_parsed_further(self):
        scenario, problems = parse("scenario 1\ndefault within 2s\n", "old.in")
        self.assertIsNone(scenario)
        self.assertEqual([(p.file, p.line) for p in problems], [("old.in", 1)])
        self.assertIn("scenario 2", problems[0].message)

    def test_The_first_statement_must_be_the_scenario_version_and_the_second_the_default_deadline(self):
        self.assertEqual(len(parse("default within 2s\n")[1]), 1)
        self.assertEqual([p.line for p in parse("scenario 2\nstart ready port within 1s\n")[1]], [2])

    def test_Exactly_one_start_comes_after_the_env_and_file_lines_and_before_any_step(self):
        head = "scenario 2\ndefault within 2s\n"
        self.assertTrue(parse(head + "connect c1 tcp\n")[1])
        self.assertTrue(parse(head + "start ready port within 1s\nstart ready port within 1s\nwait-exit\n")[1])
        self.assertTrue(parse(head + "start ready port within 1s\nenv A \"x\"\nwait-exit\n")[1])

    def test_Env_and_file_lines_may_come_in_any_order_and_a_file_variable_is_usable_after_its_line(self):
        scenario, problems = parse(
            'scenario 2\ndefault within 2s\nenv A "1"\nfile f "data"\nenv B "${f}"\nfile g "x"\nstart ready port within 1s\nwait-exit\n')
        self.assertEqual(problems, [])
        self.assertEqual([(s.kind, s.name) for s in scenario.setup], [("env", "A"), ("file", "f"), ("env", "B"), ("file", "g")])

    def test_An_env_line_for_PORT_is_refused_because_the_runner_chooses_the_port(self):
        _, problems = parse('scenario 2\ndefault within 2s\nenv PORT "1"\nstart ready port within 1s\nwait-exit\n')
        self.assertEqual(len(problems), 1)

    def test_Readiness_on_stdout_takes_a_line_and_a_deadline(self):
        scenario, problems = parse('scenario 2\ndefault within 2s\nstart ready stdout "listening" within 3s\nwait-exit\n')
        self.assertEqual(problems, [])
        self.assertEqual((scenario.start.ready, scenario.start.within_ms), ("stdout", 3000))

    def test_A_deadline_above_600_seconds_is_a_problem(self):
        self.assertTrue(parse("scenario 2\ndefault within 601s\n")[1])


class DeadlineTests(unittest.TestCase):
    def test_A_within_on_sleep_is_a_format_error_with_its_line_because_sleep_has_no_deadline(self):
        _, problems = body("sleep 1s within 2s")
        self.assertEqual([p.line for p in problems], [6])
        self.assertIn("deadline", problems[0].message)

    def test_A_sleep_counts_as_a_plain_duration_step(self):
        scenario, problems = body("sleep 250ms")
        self.assertEqual(problems, [])
        self.assertEqual(scenario.body[2].args["for"], 250)


class SplitTests(unittest.TestCase):
    def test_A_split_offset_counts_the_joined_bytes_not_the_first_string(self):
        _, problems = body('send c1 "ab" "cdef" split 3 pause 1ms')
        self.assertEqual(problems, [], "offset 3 is inside the six joined bytes although the first string has two")
        _, problems = body('send c1 "ab" "cdef" split 6 pause 1ms')
        self.assertEqual(len(problems), 1, "offset 6 is the length: not inside")

    def test_A_split_offset_after_repetition_is_counted_on_the_repeated_bytes(self):
        self.assertEqual(body('send c1 "ab"*3 split 5 pause 1ms')[1], [])
        self.assertEqual(len(body('send c1 "ab"*3 split 6 pause 1ms')[1]), 1)

    def test_A_split_offset_past_the_end_is_refused_with_file_and_line(self):
        scenario, problems = body('send c1 "abc" split 9 pause 1ms')
        self.assertIsNone(scenario)
        self.assertEqual([(p.file, p.line) for p in problems], [("t.in", 6)])

    def test_Offsets_must_be_strictly_increasing_and_above_zero(self):
        for offsets in ("0", "2,2", "3,2"):
            self.assertEqual(len(body(f'send c1 "abcdef" split {offsets} pause 1ms')[1]), 1, offsets)
        self.assertEqual(body('send c1 "abcdef" split 1,2,5 pause 1ms')[1], [])

    def test_A_split_with_a_variable_is_checked_when_its_length_is_known_and_not_before(self):
        scenario, problems = parse('scenario 2\ndefault within 2s\nfile f "x"\nstart ready port within 1s\nconnect c1 tcp\n'
                                   'send c1 "${f}" split 9 pause 1ms\nwait-exit\n')
        self.assertEqual(problems, [], "the length of a file path is only known when the file is written")

    def test_A_split_needs_its_pause_and_a_pause_needs_its_split(self):
        self.assertEqual(len(body('send c1 "abc" split 1')[1]), 1)
        self.assertEqual(len(body('send c1 "abc" pause 1ms')[1]), 1)

    def test_The_joined_length_of_a_string_expression_is_known_only_without_variables(self):
        cur = lex.Cursor('"ab"*3 "c"', "t", 1, [])
        self.assertEqual(rs.static_length(lex.take_str_expr(cur)), 7)
        cur = lex.Cursor('"ab" "${v}"', "t", 1, [])
        self.assertIsNone(rs.static_length(lex.take_str_expr(cur)))


class NameTests(unittest.TestCase):
    def test_A_name_bound_twice_is_an_error_naming_the_second_line(self):
        _, problems = parse("scenario 2\ndefault within 2s\nfile f \"x\"\nfile f \"y\"\nstart ready port within 1s\nwait-exit\n")
        self.assertEqual([p.line for p in problems], [4])

    def test_A_capture_name_bound_twice_or_a_capture_inside_a_repeat_is_an_error(self):
        two = ("request h GET \"/\"", "recv h capture a json $.x capture a json $.y")
        self.assertEqual(len(body(*two)[1]), 1)
        inside = ("request h GET \"/\"", "repeat 2 i", "request h GET \"/\"", "recv h capture a json $.x", "end")
        self.assertTrue(body(*inside)[1])

    def test_A_variable_used_before_it_is_bound_is_an_error(self):
        _, problems = body('send c1 "${later}"')
        self.assertEqual(len(problems), 1)
        self.assertIn("later", problems[0].message)

    def test_A_repeat_variable_is_bound_only_inside_its_repeat(self):
        self.assertEqual(body("repeat 2 i", 'send c1 "${i}"', "end")[1], [])
        self.assertEqual(len(body("repeat 2 i", "sleep 1ms", "end", 'send c1 "${i}"')[1]), 1)

    def test_A_repeat_variable_may_not_shadow_a_live_name_but_two_sequential_repeats_may_share_one(self):
        self.assertEqual(body("repeat 2 i", "sleep 1ms", "end", "repeat 2 i", "sleep 1ms", "end")[1], [])
        self.assertEqual(len(body("repeat 2 i", "repeat 2 i", "sleep 1ms", "end", "end")[1]), 1)

    def test_A_name_that_is_a_keyword_is_refused(self):
        self.assertTrue(parse(HEAD + "connect end tcp\nwait-exit\n")[1])
        self.assertTrue(body("repeat 2 split", "sleep 1ms", "end")[1])

    def test_A_connection_name_must_be_declared_once_and_before_its_use(self):
        self.assertEqual(len(body("connect c1 tcp")[1]), 1)
        self.assertEqual(len(body('send nobody "x"')[1]), 1)
        self.assertEqual(len(body("connect stdin tcp")[1]), 1)


class StepTests(unittest.TestCase):
    def test_A_send_goes_to_a_tcp_or_http_connection_or_stdin_and_a_request_only_to_an_http_one(self):
        self.assertEqual(body('send c1 "x"', 'send stdin "x"', 'send h "x"')[1], [])
        self.assertEqual(len(body('send stdout "x"')[1]), 1)
        self.assertEqual(len(body('request c1 GET "/"')[1]), 1)

    def test_A_recv_on_tcp_or_stdout_needs_a_mode_and_on_http_takes_none(self):
        self.assertEqual(body("recv c1 bytes 4", 'recv c1 until "x" count 2', "recv c1 lines 3", "recv c1 eof", "recv stdout lines 1")[1], [])
        self.assertEqual(len(body("recv c1")[1]), 1)
        self.assertEqual(len(body("request h GET \"/\"", "recv h bytes 4")[1]), 1)
        self.assertEqual(body('request h GET "/"', "recv h")[1], [])

    def test_Chunk_and_pause_go_together_on_a_recv(self):
        self.assertEqual(body("recv c1 bytes 16 chunk 4 pause 2ms within 5s")[1], [])
        self.assertEqual(len(body("recv c1 bytes 16 chunk 4")[1]), 1)

    def test_A_json_capture_needs_an_http_reply_and_a_regex_capture_works_on_any(self):
        self.assertEqual(len(body("recv c1 bytes 1 capture a json $.x")[1]), 1)
        self.assertEqual(body("recv c1 bytes 1 capture a regex \"x(y)\" as hex")[1], [])
        self.assertEqual(len(body("recv c1 bytes 1 capture a regex \"x\" as float")[1]), 1)

    def test_Expect_quiet_and_closed_close_shutdown_signal_and_wait_exit_have_their_arguments(self):
        scenario, problems = body("expect c1 quiet for 200ms", "expect c1 closed within 1s", "shutdown c1", "close c1", "close stdin", "signal TERM")
        self.assertEqual(problems, [])
        self.assertEqual([n.op for n in scenario.body[2:]], ["quiet", "closed", "shutdown", "close", "close", "signal", "wait_exit"])

    def test_A_signal_other_than_TERM_is_a_problem(self):
        self.assertEqual(len(body("signal KILL")[1]), 1)

    def test_Shutdown_is_for_tcp_connections_only(self):
        self.assertEqual(len(body("shutdown h")[1]), 1)

    def test_The_last_statement_must_be_wait_exit(self):
        _, problems = parse(HEAD + "sleep 1ms\n")
        self.assertEqual(len(problems), 1)

    def test_A_statement_with_an_extra_token_is_a_problem_naming_it(self):
        _, problems = body("close c1 now")
        self.assertIn("now", problems[0].message)

    def test_An_unknown_keyword_a_trailing_comment_and_a_misspelled_option_are_problems(self):
        self.assertTrue(body("frobnicate c1")[1])
        self.assertTrue(body("sleep 1s # why")[1])
        self.assertTrue(body("recv c1 bytes 1 whithin 2s")[1])


class RequestTests(unittest.TestCase):
    def test_A_request_collects_its_header_lines_in_order_and_at_most_one_body(self):
        scenario, problems = body('request h POST "/a"', 'header "x-a" "1"', 'header "x-b" "2" "3"', 'body json {"k": 1}')
        self.assertEqual(problems, [])
        request = scenario.body[2]
        self.assertEqual((request.args["method"], len(request.args["headers"]), request.args["body"][0]), ("POST", 2, "json"))
        self.assertEqual(request.args["body"][1], '{"k": 1}')

    def test_A_string_body_and_no_body_are_both_allowed_and_a_second_body_is_not(self):
        self.assertEqual(body('request h POST "/"', 'body "abc"')[1], [])
        self.assertEqual(body('request h GET "/"')[1], [])
        self.assertEqual(len(body('request h POST "/"', 'body "a"', 'body "b"')[1]), 1)

    def test_A_header_or_body_line_outside_a_request_is_a_problem(self):
        self.assertEqual(len(body('header "a" "b"')[1]), 1)
        self.assertEqual(len(body('send c1 "x"', 'body "a"')[1]), 1)

    def test_Json_text_without_a_variable_must_be_JSON_and_with_a_variable_is_checked_after_substitution(self):
        self.assertEqual(len(body('request h POST "/"', "body json {nope}")[1]), 1)
        self.assertEqual(len(body('request h POST "/"', 'body json {"a":1,"a":2}')[1]), 1)
        self.assertEqual(body("request h GET \"/\"", "recv h capture v json $.x", 'request h POST "/"', 'body json {"a":${v}}')[1], [])

    def test_A_method_must_be_capital_letters(self):
        self.assertEqual(len(body('request h get "/"')[1]), 1)


class GroupTests(unittest.TestCase):
    def test_A_together_group_holds_two_or_more_sends_or_requests_on_different_connections(self):
        scenario, problems = body("together", 'send c1 "a"', 'request h GET "/"', "end")
        self.assertEqual(problems, [])
        self.assertIsInstance(scenario.body[2], rs.Together)

    def test_A_together_group_with_one_step_the_same_connection_twice_or_another_step_kind_is_refused(self):
        self.assertTrue(body("together", 'send c1 "a"', "end")[1])
        self.assertTrue(body("together", 'send c1 "a"', 'send c1 "b"', "end")[1])
        self.assertTrue(body("together", 'send c1 "a"', "recv c1 eof", "end")[1])

    def test_A_together_group_holds_no_repeat_but_a_repeat_may_hold_a_together_group(self):
        self.assertTrue(body("together", "repeat 2 i", 'send c1 "a"', "end", 'request h GET "/"', "end")[1])
        self.assertEqual(body("repeat 2 i", "together", 'send c1 "a"', 'request h GET "/"', "end", "end")[1], [])

    def test_A_repeat_count_is_one_to_one_hundred_thousand(self):
        for count in ("0", "100001"):
            self.assertTrue(body(f"repeat {count} i", "sleep 1ms", "end")[1], count)
        self.assertEqual(body("repeat 100000 i", "sleep 1ms", "end")[1], [])

    def test_An_end_without_a_group_and_a_group_without_its_end_are_problems(self):
        self.assertTrue(body("end")[1])
        self.assertTrue(parse(HEAD + "repeat 2 i\nsleep 1ms\nwait-exit\n")[1])

    def test_Nested_repeats_that_expand_past_the_step_ceiling_are_refused(self):
        self.assertTrue(body("repeat 100000 i", "repeat 100000 j", "sleep 1ms", "end", "end")[1])


def ending(*lines, head=HEAD):
    """A script whose end is the trigger, the wait-exit and `lines` (what stands after the wait-exit)."""
    return parse(head + "signal TERM\nwait-exit within 5s\n" + "".join(l + "\n" for l in lines))


STDOUT_HEAD = 'scenario 2\ndefault within 1s\nstart ready stdout "ok" within 10s\n'


class FormatV21Tests(unittest.TestCase):
    """Statements v2.1 allows: steps after the end, `expect stdout closed`, `send` on an http connection."""

    def test_Reads_and_waits_may_stand_after_wait_exit_where_v2_1_allows_them(self):
        scenario, problems = ending("recv c1 lines 1", "expect c1 quiet for 10ms", "sleep 5ms", "close c1", head=HEAD)
        self.assertEqual(problems, [])
        self.assertEqual([n.op for n in scenario.body][-6:], ["signal", "wait_exit", "recv", "quiet", "sleep", "close"])

    def test_Standard_output_lines_written_when_the_program_stops_are_read_after_the_wait_exit(self):
        scenario, problems = parse(STDOUT_HEAD + "signal TERM\nwait-exit within 5s\nrecv stdout lines 1\n")
        self.assertEqual(problems, [])
        self.assertEqual(scenario.body[-1].op, "recv")

    def test_A_send_after_the_wait_exit_is_refused_because_nothing_more_is_sent_to_the_program(self):
        for later in ('send c1 "x"', "connect d tcp", 'request h GET "/"', "signal TERM", "shutdown c1"):
            _, problems = ending(later)
            self.assertEqual(len(problems), 1, later)
            self.assertEqual(problems[0].line, 8, later)

    def test_Expect_closed_applies_to_standard_output_as_well_as_to_connections(self):
        scenario, problems = parse(STDOUT_HEAD + "close stdin\nexpect stdout closed\nwait-exit within 5s\n")
        self.assertEqual(problems, [])
        self.assertEqual([(e.kind, e.conn) for e in rs.produced_entries(scenario)], [("ready", ""), ("closed", "stdout"), ("exit", "")])

    def test_Expect_closed_does_not_apply_to_stdin(self):
        self.assertEqual(len(parse(STDOUT_HEAD + "expect stdin closed\nclose stdin\nwait-exit within 5s\n")[1]), 1)

    def test_Expect_quiet_applies_to_standard_output(self):
        self.assertEqual(parse(STDOUT_HEAD + "expect stdout quiet for 10ms\nclose stdin\nwait-exit within 5s\n")[1], [])

    def test_A_send_on_an_http_connection_is_a_raw_request_and_its_reply_is_read_by_recv(self):
        scenario, problems = body('send h "GET / HTTP/1.1\\r\\nHost: x\\r\\n\\r\\n" split 4 pause 1ms', "recv h")
        self.assertEqual(problems, [])
        self.assertEqual([(e.kind, e.conn) for e in rs.produced_entries(scenario)], [("ready", ""), ("reply", "h"), ("exit", "")])

    def test_A_send_to_standard_output_and_a_request_on_a_tcp_connection_are_still_refused(self):
        self.assertEqual(len(body('send stdout "x"')[1]), 1)
        self.assertEqual(len(body('request c1 GET "/"')[1]), 1)


class ProducedEntryTests(unittest.TestCase):
    def test_The_entries_a_script_produces_follow_its_steps_with_repeats_expanded(self):
        scenario, _ = rs.parse_script(fixture("x3.in"), "x3.in")
        kinds = [(e.kind, e.conn) for e in rs.produced_entries(scenario)]
        self.assertEqual(kinds, [("ready", ""), ("reply", "a"), ("reply", "b"), ("reply", "a"), ("reply", "b"), ("reply", "a"), ("exit", "")])

    def test_A_lines_recv_produces_one_entry_per_line(self):
        scenario, _ = body("recv c1 lines 3")
        self.assertEqual([e.kind for e in rs.produced_entries(scenario)], ["ready", "recv", "recv", "recv", "exit"])

    def test_A_repeat_produces_its_body_count_times(self):
        scenario, _ = body("repeat 3 i", "expect c1 quiet for 1ms", "end")
        self.assertEqual([e.kind for e in rs.produced_entries(scenario)], ["ready"] + ["quiet"] * 3 + ["exit"])


if __name__ == "__main__":
    unittest.main()
