"""The expected transcript (.out) parser, its validation, and its one-to-one correspondence with the script (FORMAT v2 section 5, V1, V6)."""
import hashlib
import os
import unittest

import runner_path  # noqa: F401
import rs_lex as lex
import rs_pair as rp
import rs_script as rs
import rs_transcript as rt

FIXTURES = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "fixtures", "runner", "format")


def parse(text, file="t.out"):
    return rt.parse_transcript(text.encode("utf-8"), file)


def fixture(name):
    with open(os.path.join(FIXTURES, name), "rb") as f:
        return f.read()


def ok(text):
    transcript, problems = parse(text)
    assert not problems, problems
    return transcript


def kinds(transcript):
    return [(e.kind, e.conn) for e in rt.expand(transcript)]


class ExampleTests(unittest.TestCase):
    def test_The_daemon_example_transcript_parses_and_expands_to_its_entries(self):
        transcript, problems = rt.parse_transcript(fixture("x2.out"), "x2.out")
        self.assertEqual(problems, [])
        entries = rt.expand(transcript)
        self.assertEqual(len(entries), 73, "ready, 2 + 2 + 1 + 2 entries of the first four steps, 64 slow-reader replies, the exit")
        self.assertEqual([e.kind for e in entries[:4]], ["ready", "quiet", "recv", "recv"])
        self.assertEqual(entries[-1].kind, "exit")

    def test_The_web_example_transcript_parses_replies_with_headers_and_json_bodies_and_an_unordered_array(self):
        transcript, problems = rt.parse_transcript(fixture("x3.out"), "x3.out")
        self.assertEqual(problems, [])
        replies = [e for e in rt.expand(transcript) if e.kind == "reply"]
        self.assertEqual([(r.conn, r.status) for r in replies], [("a", 201), ("b", 201), ("a", 200), ("b", 201), ("a", 200)])
        self.assertEqual(len(replies[0].headers), 1)
        self.assertEqual(replies[0].body[0], "json")

    def test_The_examples_correspond_one_to_one_with_their_scripts(self):
        for name in ("x2", "x3"):
            scenario, p1 = rs.parse_script(fixture(name + ".in"), name + ".in")
            transcript, p2 = rt.parse_transcript(fixture(name + ".out"), name + ".out")
            self.assertEqual(p1 + p2 + rp.validate_pair(scenario, transcript), [], name)

    # sha256 of the four blocks of FORMAT v2.1 (docs/format-v2, commit 66ad032) section 6.2 and 6.3, as the format shows them.
    FORMAT_BLOCKS = {
        "x2.in": "3db5762eb3f629c4075f772e247a455e4642ce9f7a435f28fa87310824aaa843",
        "x2.out": "52cb9f0985e4aef0d3d716b47dbed92bbda50ec1199ee726832ddd3d68649262",
        "x3.in": "9a977cbb0aabb37620f84c89a8763cd5135ec1ee90596d62d4f000858286b04d",
        "x3.out": "bd5c1447df8bde327a783011b6590dd4febcfde37b39cdb0cd2b8d3837df5fd8",
    }

    def test_The_fixtures_are_byte_equal_to_the_worked_examples_of_FORMAT_v2_1(self):
        for name, digest in self.FORMAT_BLOCKS.items():
            self.assertEqual(hashlib.sha256(fixture(name)).hexdigest(), digest, f"{name} is not the block FORMAT v2.1 shows")

    def test_The_fixture_check_notices_a_changed_byte(self):
        changed = fixture("x2.in").replace(b"scenario 2", b"scenario 3", 1)
        self.assertNotEqual(hashlib.sha256(changed).hexdigest(), self.FORMAT_BLOCKS["x2.in"])

    def test_The_parsed_transcript_is_frozen(self):
        transcript, _ = rt.parse_transcript(fixture("x3.out"), "x3.out")
        with self.assertRaises(Exception):
            transcript.file = "x"


class HeadTests(unittest.TestCase):
    def test_Transcript_1_is_a_format_error_naming_the_file_and_line(self):
        transcript, problems = parse("transcript 1\nready\n", "old.out")
        self.assertIsNone(transcript)
        self.assertEqual([(p.file, p.line) for p in problems], [("old.out", 1)])
        self.assertIn("transcript 2", problems[0].message)

    def test_The_first_statement_must_be_the_transcript_version(self):
        self.assertEqual(len(parse("ready\n")[1]), 1)

    def test_An_empty_transcript_of_the_right_version_is_valid_and_has_no_entries(self):
        self.assertEqual(rt.expand(ok("transcript 2\n")), [])

    def test_Blank_lines_comments_and_indentation_mean_nothing(self):
        self.assertEqual(kinds(ok("transcript 2\n\n# note\n   ready\n    quiet c1\n")), [("ready", ""), ("quiet", "c1")])


class EntryTests(unittest.TestCase):
    def test_Every_entry_kind_of_the_format_parses(self):
        text = 'transcript 2\nready\nrecv c1 "a"\nreply a 200\n  header "x" "y"\n  body empty\nquiet c1\nclosed c1\nexit 0\n'
        self.assertEqual([k for k, _ in kinds(ok(text))], ["ready", "recv", "reply", "quiet", "closed", "exit"])

    def test_A_reply_takes_exactly_one_body_line_of_the_three_forms(self):
        for body, form in (('body "x"', "str"), ("body empty", "empty"), ('body json {"a":1}', "json")):
            self.assertEqual(rt.expand(ok(f"transcript 2\nreply a 200\n{body}\n"))[0].body[0], form)
        self.assertEqual(len(parse("transcript 2\nreply a 200\n")[1]), 1)
        self.assertEqual(len(parse('transcript 2\nreply a 200\nbody empty\nbody "x"\n')[1]), 1)

    def test_A_header_or_body_line_outside_a_reply_is_a_problem(self):
        self.assertEqual(len(parse('transcript 2\nheader "a" "b"\n')[1]), 1)
        self.assertEqual(len(parse('transcript 2\nquiet c1\nbody empty\n')[1]), 1)

    def test_A_status_must_be_a_three_digit_code(self):
        self.assertEqual(len(parse("transcript 2\nreply a 99\nbody empty\n")[1]), 1)
        self.assertEqual(len(parse("transcript 2\nreply a 600\nbody empty\n")[1]), 1)

    def test_An_entry_that_is_only_ever_actual_is_not_a_keyword_of_an_expected_transcript(self):
        for word in ("not-ready", "refused c1", "timeout c1 \"\"", "noise c1 \"\"", "early-exit 1", "exit-timeout", "capture-failed x"):
            self.assertEqual(len(parse(f"transcript 2\n{word}\n")[1]), 1, word)

    def test_Exit_takes_a_status_number_and_nothing_else(self):
        self.assertEqual(len(parse("transcript 2\nexit\n")[1]), 1)
        self.assertEqual(len(parse("transcript 2\nexit 0 0\n")[1]), 1)

    def test_A_star_connection_is_only_allowed_inside_an_unordered_group(self):
        self.assertEqual(len(parse('transcript 2\nrecv * "a"\n')[1]), 1)
        self.assertEqual(parse('transcript 2\nunordered\nrecv * "a"\nrecv c1 "b"\nend\n')[1], [])


class GroupTests(unittest.TestCase):
    def test_An_unordered_group_holds_two_or_more_entries_and_does_not_nest(self):
        self.assertEqual(len(parse('transcript 2\nunordered\nrecv c1 "a"\nend\n')[1]), 1)
        self.assertEqual(len(parse('transcript 2\nunordered\nrecv c1 "a"\nunordered\nrecv c1 "b"\nrecv c1 "c"\nend\nend\n')[1]), 1)
        self.assertEqual(len(parse('transcript 2\nunordered\nrecv c1 "a"\nrecv c1 "b"\n')[1]), 1)

    def test_More_than_64_entries_in_an_unordered_group_is_a_format_error(self):
        lines = "".join(f'recv c1 "{i}"\n' for i in range(65))
        self.assertEqual(len(parse("transcript 2\nunordered\n" + lines + "end\n")[1]), 1)
        lines = "".join(f'recv c1 "{i}"\n' for i in range(64))
        self.assertEqual(parse("transcript 2\nunordered\n" + lines + "end\n")[1], [])

    def test_A_repeat_stands_for_its_body_written_count_times(self):
        transcript = ok('transcript 2\nrepeat 3 i\nrecv c1 "a"\nquiet c1\nend\nexit 0\n')
        self.assertEqual(kinds(transcript), [("recv", "c1"), ("quiet", "c1")] * 3 + [("exit", "")])

    def test_A_repeat_variable_is_bound_in_each_expanded_entry(self):
        entries = rt.expand(ok('transcript 2\nrepeat 3 i\nrecv c1 "${i}"\nend\n'))
        self.assertEqual([e.loop for e in entries], [(("i", 0),), (("i", 1),), (("i", 2),)])

    def test_A_repeat_count_is_one_to_one_hundred_thousand_and_an_end_without_a_group_is_a_problem(self):
        self.assertEqual(len(parse('transcript 2\nrepeat 0 i\nquiet c1\nend\n')[1]), 1)
        self.assertEqual(len(parse("transcript 2\nend\n")[1]), 1)

    def test_The_expanded_entry_count_is_known_without_expanding(self):
        transcript = ok('transcript 2\nrepeat 1000 i\nrepeat 1000 j\nquiet c1\nend\nend\n')
        self.assertEqual(rt.entry_count(transcript), 1_000_000)


class ChecksAndFieldsTests(unittest.TestCase):
    def test_A_check_is_parsed_and_produces_no_entry(self):
        text = 'transcript 2\nrecv c1 "${:uint=a}"\nrecv c1 "${:uint=b}"\ncheck distinct a b\ncheck equal a 3\ncheck equal a b\n'
        transcript = ok(text)
        self.assertEqual(len(rt.expand(transcript)), 2)
        checks = rt.checks_of(transcript)
        self.assertEqual([(c.op, c.names, c.other) for c in checks], [("distinct", ("a", "b"), None), ("equal", ("a",), "3"), ("equal", ("a",), "b")])

    def test_A_check_distinct_may_name_a_prefix_glob(self):
        checks = rt.checks_of(ok('transcript 2\nrepeat 3 i\nrecv c1 "${:uint=r${i}}"\nend\ncheck distinct r*\n'))
        self.assertEqual(checks[0].names, ("r*",))

    def test_A_malformed_check_is_a_problem(self):
        for text in ("check", "check distinct", "check equal a", "check same a b", "check equal a b c", "check distinct A"):
            self.assertEqual(len(parse("transcript 2\n" + text + "\n")[1]), 1, text)

    def test_A_bound_name_is_bound_once_and_a_static_name_may_not_bind_inside_a_repeat(self):
        self.assertEqual(len(parse('transcript 2\nrecv c1 "${:uint=a}"\nrecv c1 "${:uint=a}"\n')[1]), 1)
        self.assertEqual(len(parse('transcript 2\nrepeat 2 i\nrecv c1 "${:uint=a}"\nend\n')[1]), 1)
        self.assertEqual(parse('transcript 2\nrepeat 2 i\nrecv c1 "${:uint=a${i}}"\nend\n')[1], [])

    def test_A_variable_used_before_a_bind_or_never_bound_is_a_problem_when_the_pair_is_validated(self):
        scenario, _ = rs.parse_script(fixture("x3.in"), "x3.in")
        transcript = ok('transcript 2\nready\nrecv c1 "${nobody}"\n')
        problems = rp.validate_pair(scenario, transcript)
        self.assertTrue(any("nobody" in p.message for p in problems))

    def test_A_bound_name_that_a_script_variable_already_uses_is_a_problem(self):
        scenario, _ = rs.parse_script(fixture("x3.in"), "x3.in")
        transcript = ok('transcript 2\nready\nreply a 201\nbody json {"id":"${:uuid=order}"}\n')
        self.assertTrue(any("order" in p.message for p in rp.validate_pair(scenario, transcript)))

    def test_A_check_over_a_name_nothing_binds_is_a_problem_when_the_pair_is_validated(self):
        scenario, _ = rs.parse_script(fixture("x2.in"), "x2.in")
        transcript = ok('transcript 2\nready\ncheck distinct ghost\n')
        self.assertTrue(any("ghost" in p.message for p in rp.validate_pair(scenario, transcript)))


class JsonBodyTests(unittest.TestCase):
    def test_The_expected_JSON_must_be_JSON_without_a_repeated_member_name_or_NaN(self):
        for body in ("{nope}", '{"a":1,"a":2}', "NaN", ""):
            self.assertEqual(len(parse(f"transcript 2\nreply a 200\nbody json {body}\n")[1]), 1, body)

    def test_The_member_name_dollar_unordered_must_stand_alone_with_an_array(self):
        self.assertEqual(parse('transcript 2\nreply a 200\nbody json {"x":{"$unordered":[1,2]}}\n')[1], [])
        for body in ('{"$unordered":1}', '{"$unordered":[1],"b":2}', '{"$unordered":[1],"$unordered":[2]}'):
            self.assertEqual(len(parse(f"transcript 2\nreply a 200\nbody json {body}\n")[1]), 1, body)

    def test_A_malformed_variable_or_field_inside_an_expected_JSON_string_is_a_problem(self):
        self.assertEqual(len(parse('transcript 2\nreply a 200\nbody json {"a":"${Bad}"}\n')[1]), 1)
        self.assertEqual(len(parse('transcript 2\nreply a 200\nbody json {"a":"${:float}"}\n')[1]), 1)
        self.assertEqual(parse('transcript 2\nreply a 200\nbody json {"a":"x${y}${:int}$5"}\n')[1], [])

    def test_The_string_templates_of_an_expected_JSON_string_are_exposed_for_the_matcher(self):
        self.assertEqual(lex.template_parts("a${v}${:hex}$5"), [lex.Lit(b"a"), lex.Var("v"), lex.Field("hex"), lex.Lit(b"$5")])
        self.assertIsNone(lex.template_parts("${"))
        self.assertEqual(lex.template_parts("\ud800"), [lex.Lit("\ud800".encode("utf-8", "surrogatepass"))])


class CorrespondenceTests(unittest.TestCase):
    def pair(self, script_body, transcript_body):
        head = "scenario 2\ndefault within 2s\nstart ready port within 1s\nconnect c1 tcp\n"
        scenario, problems = rs.parse_script((head + script_body + "signal TERM\nwait-exit within 5s\n").encode(), "t.in")
        assert not problems, problems
        transcript, problems = parse("transcript 2\nready\n" + transcript_body + "exit 0\n")
        assert not problems, problems
        return rp.validate_pair(scenario, transcript)

    def test_A_transcript_with_fewer_entries_than_the_script_produces_is_a_problem_naming_both_counts(self):
        problems = self.pair("recv c1 bytes 1\nrecv c1 bytes 1\n", 'recv c1 "a"\n')
        self.assertEqual(len(problems), 1)
        self.assertIn("4", problems[0].message)
        self.assertIn("3", problems[0].message)

    def test_An_entry_of_another_kind_or_connection_than_the_script_produces_is_a_problem_at_the_transcript_line(self):
        problems = self.pair("recv c1 bytes 1\n", "quiet c1\n")
        self.assertEqual([p.line for p in problems], [3])
        problems = self.pair("recv c1 bytes 1\n", 'recv c2 "a"\n')
        self.assertEqual(len(problems), 1)

    def test_An_unordered_group_corresponds_to_the_same_entries_in_any_order(self):
        problems = self.pair("connect c2 tcp\nrecv c1 bytes 1\nrecv c2 bytes 1\n", 'unordered\nrecv c2 "b"\nrecv c1 "a"\nend\n')
        self.assertEqual(problems, [])
        problems = self.pair("connect c2 tcp\nrecv c1 bytes 1\nrecv c2 bytes 1\n", 'unordered\nrecv c2 "b"\nrecv c2 "a"\nend\n')
        self.assertEqual(len(problems), 1)

    def test_A_star_connection_in_an_unordered_group_matches_any_connection(self):
        problems = self.pair("connect c2 tcp\nrecv c1 bytes 1\nrecv c2 bytes 1\n", 'unordered\nrecv * "b"\nrecv * "a"\nend\n')
        self.assertEqual(problems, [])

    def test_A_star_taken_first_must_give_way_so_that_every_entry_of_the_group_is_assigned(self):
        problems = self.pair("connect c2 tcp\nrecv c1 bytes 1\nrecv c2 bytes 1\n", 'unordered\nrecv * "b"\nrecv c1 "a"\nend\n')
        self.assertEqual(problems, [], "a greedy assignment gives the star the first connection and strands the second entry")

    def test_A_repeat_in_the_transcript_corresponds_to_a_repeat_in_the_script(self):
        problems = self.pair("repeat 3 i\nrecv c1 bytes 1\nend\n", 'repeat 3 i\nrecv c1 "a"\nend\n')
        self.assertEqual(problems, [])
        problems = self.pair("repeat 3 i\nrecv c1 bytes 1\nend\n", 'repeat 2 i\nrecv c1 "a"\nend\n')
        self.assertEqual(len(problems), 1)


class FormatV21Tests(unittest.TestCase):
    def test_A_JSON_recv_entry_holds_one_expected_JSON_value_with_its_text(self):
        entry = rt.expand(ok('transcript 2\nrecv c json {"a": [1, 2.0], "t": "${:token=k}"}\n'))[0]
        self.assertEqual((entry.kind, entry.conn, entry.body[0], entry.expr), ("recv", "c", "json", None))
        self.assertEqual(entry.body[2], '{"a": [1, 2.0], "t": "${:token=k}"}')

    def test_A_JSON_recv_entry_must_hold_usable_JSON(self):
        for text in ("recv c json", "recv c json {nope}", 'recv c json {"a":1,"a":2}', "recv c json NaN", 'recv c json {"$unordered":1}', 'recv c json {"a":"${:float}"}'):
            self.assertEqual(len(parse("transcript 2\n" + text + "\n")[1]), 1, text)

    def test_A_JSON_recv_entry_binds_and_uses_names_like_a_JSON_body(self):
        self.assertEqual(len(parse('transcript 2\nrecv c json {"a":"${:int=n}"}\nrecv c json {"a":"${:int=n}"}\n')[1]), 1)
        scenario, _ = rs.parse_script(b'scenario 2\ndefault within 1s\nstart ready port within 1s\nconnect c tcp\nrecv c lines 1\nsignal TERM\nwait-exit within 5s\n', "t.in")
        transcript = ok('transcript 2\nready\nrecv c json {"a":"${:int=n}"}\nexit 0\ncheck equal n 1\n')
        self.assertEqual(rp.validate_pair(scenario, transcript), [])
        transcript = ok('transcript 2\nready\nrecv c json {"a":"${ghost}"}\nexit 0\n')
        self.assertTrue(any("ghost" in p.message for p in rp.validate_pair(scenario, transcript)))

    def test_A_time_field_with_a_digit_count_parses_with_and_without_a_window(self):
        cases = {"${:ts.0}": ("ts.", (0,)), "${:ts.3}": ("ts.", (3,)), "${:ts.9}": ("ts.", (9,)),
                 "${:ts.3~10s}": ("ts.~", (3, 10_000)), "${:ts~10s}": ("ts~", (10_000,)), "${:ts}": ("ts", ())}
        for text, (kind, args) in cases.items():
            entry = rt.expand(ok(f'transcript 2\nrecv c "{text}"\n'))[0]
            field = entry.expr.items[0].parts[0]
            self.assertEqual((field.kind, field.args), (kind, args), text)

    def test_A_time_field_with_a_bad_digit_count_is_a_problem(self):
        for text in ("${:ts.}", "${:ts.10}", "${:ts.x}", "${:ts.3~}", "${:ts.3~601s}", "${:ts.-1}", "${:ts~10s.3}"):
            self.assertEqual(len(parse(f'transcript 2\nrecv c "{text}"\n')[1]), 1, text)

    def test_A_time_field_may_bind_and_a_capture_may_name_a_digit_count_type(self):
        self.assertEqual(parse('transcript 2\nrecv c "${:ts.3=when}"\n')[1], [])
        script = lambda t: rs.parse_script(f'scenario 2\ndefault within 1s\nstart ready port within 1s\nconnect h http\nrequest h GET "/"\nrecv h capture a json $.t as {t}\nsignal TERM\nwait-exit within 5s\n'.encode(), "t.in")[1]
        self.assertEqual(script("ts.3"), [])
        self.assertEqual(script("ts"), [])
        self.assertEqual(len(script("ts.12")), 1)

    def test_Check_equal_takes_a_string_and_keeps_its_bytes(self):
        checks = rt.checks_of(ok('transcript 2\nrecv c "${:token=a}"\ncheck equal a "ab\\r\\xff"\n'))
        self.assertEqual((checks[0].op, checks[0].names, checks[0].other, checks[0].literal), ("equal", ("a",), None, b"ab\r\xff"))

    def test_A_check_string_is_literal_text_only(self):
        for text in ('check equal a "${b}"', 'check equal a "${:int}"', 'check equal a "x" "y" extra', "check equal a \"unclosed"):
            self.assertTrue(parse('transcript 2\nrecv c "${:token=a}"\n' + text + "\n")[1], text)

    def test_Adjacent_strings_of_a_check_are_joined_like_any_string_expression(self):
        checks = rt.checks_of(ok('transcript 2\nrecv c "${:token=a}"\ncheck equal a "ab" "c"*2\n'))
        self.assertEqual(checks[0].literal, b"abcc")


class UnorderedBindingTests(unittest.TestCase):
    """A binding made inside an unordered group depends on the order the replies arrive in: the checks that would judge it are refused."""

    GROUP = 'transcript 2\nunordered\n  recv * "${:int=a}\\n"\n  recv * "${:int=b}\\n"\nend\n'

    def refused(self, tail, head=None):
        transcript, problems = parse((head or self.GROUP) + tail)
        self.assertIsNone(transcript, tail)
        self.assertEqual(len(problems), 1, (tail, problems))
        return problems[0]

    def test_Check_equal_naming_a_binding_of_the_group_is_refused_with_file_and_line(self):
        problem = self.refused("check equal a 1\n")
        self.assertEqual((problem.file, problem.line), ("t.out", 6))
        self.assertIn("unordered group", problem.message)

    def test_The_arrival_order_that_made_it_pass_for_one_order_and_fail_for_the_other_is_no_longer_judged(self):
        """The review's case: replies 1 then 2 pass `check equal a 1`, replies 2 then 1 fail it, for the very same program."""
        self.assertEqual(len(rt.parse_transcript((self.GROUP + "check equal a 1\n").encode(), "t.out")[1]), 1)

    def test_Check_equal_naming_a_group_binding_on_either_side_is_refused(self):
        head = self.GROUP + 'recv c "${:int=z}\\n"\n'
        self.refused("check equal z b\n", head)
        self.refused("check equal z a\n", head)
        self.refused('check equal b "2"\n', head)

    def test_Check_distinct_over_part_of_the_group_is_refused_and_over_all_of_it_is_not(self):
        self.refused("check distinct a\n")
        self.assertEqual(parse(self.GROUP + "check distinct a b\n")[1], [])
        self.assertEqual(parse(self.GROUP + "check distinct b a\n")[1], [])

    def test_Check_distinct_naming_the_whole_group_and_other_bindings_is_allowed_and_a_part_with_them_is_not(self):
        head = self.GROUP + 'recv c "${:int=z}\\n"\n'
        self.assertEqual(parse(head + "check distinct a b z\n")[1], [])
        self.refused("check distinct a z\n", head)

    def test_A_check_over_bindings_outside_the_group_is_allowed_whatever_the_group_binds(self):
        head = self.GROUP + 'recv c "${:int=y}\\n"\nrecv c "${:int=z}\\n"\n'
        self.assertEqual(parse(head + "check equal y z\ncheck distinct y z\ncheck equal y 3\n")[1], [])

    def test_A_glob_is_judged_by_the_names_it_stands_for(self):
        group = 'transcript 2\nunordered\n  recv * "${:int=r1}\\n"\n  recv * "${:int=r2}\\n"\nend\nrecv c "${:int=r3}\\n"\n'
        self.assertEqual(parse(group + "check distinct r*\n")[1], [], "r* is the whole group and r3")
        self.refused("check distinct r1 r3\n", group)
        self.refused("check distinct r1*\n", group)

    def test_Names_with_a_repeat_variable_inside_a_group_are_judged_by_their_literal_head(self):
        head = 'transcript 2\nrepeat 2 i\n  unordered\n    recv * "${:int=a${i}}\\n"\n    recv * "${:int=b${i}}\\n"\n  end\nend\n'
        self.assertEqual(parse(head + "check distinct a* b*\n")[1], [])
        self.refused("check distinct a*\n", head)
        self.refused("check equal a0 1\n", head)

    def test_Check_equal_is_refused_even_when_the_group_binds_only_that_name_because_the_assignment_still_decides_it(self):
        head = 'transcript 2\nunordered\n  recv * "${:int=a}\\n"\n  recv * "x\\n"\nend\n'
        self.refused("check equal a 1\n", head)
        self.assertEqual(parse(head + "check distinct a\n")[1], [], "a distinct over the group's one binding names all of it")

    def test_A_group_that_binds_nothing_leaves_every_check_alone(self):
        head = 'transcript 2\nunordered\n  recv * "x"\n  recv * "y"\nend\nrecv c "${:int=a}\\n"\nrecv c "${:int=b}\\n"\n'
        self.assertEqual(parse(head + "check equal a b\ncheck distinct a b\ncheck equal a 1\n")[1], [])

    def test_An_unordered_group_with_bindings_and_no_check_on_them_is_fine(self):
        self.assertEqual(parse(self.GROUP)[1], [])


class ArrivalMessageTests(unittest.TestCase):
    """The refusal says which binding of the group the check names and which bindings the group makes, so the transcript can be repaired."""

    def message(self, text):
        transcript, problems = parse(text)
        self.assertIsNone(transcript)
        self.assertEqual(len(problems), 1, problems)
        return problems[0].message

    GROUP = 'transcript 2\nunordered\n  recv * "${:int=a}\\n"\n  recv * "${:int=b}\\n"\n  recv * "${:int=c}\\n"\nend\n'

    def test_Check_equal_names_the_binding_it_used_and_lists_the_bindings_of_the_group(self):
        message = self.message(self.GROUP + "check equal b 2\n")
        self.assertIn("check equal names `b`", message)
        self.assertIn("(the group binds a, b, c)", message)

    def test_Check_distinct_over_part_of_the_group_names_what_it_took_and_what_it_left_out(self):
        message = self.message(self.GROUP + "check distinct a c\n")
        self.assertIn("check distinct names `a`, `c`", message)
        self.assertIn("(the group binds a, b, c)", message)
        self.assertIn("leaves out `b`", message)

    def test_Check_equal_against_a_name_names_only_the_binding_that_belongs_to_the_group(self):
        head = self.GROUP + 'recv d "${:int=z}\\n"\n'
        message = self.message(head + "check equal z a\n")
        self.assertIn("names `a`", message)
        self.assertNotIn("`z`", message)

    def test_A_glob_is_named_as_written_and_a_repeat_name_is_listed_with_its_variable(self):
        head = 'transcript 2\nrepeat 2 i\n  unordered\n    recv * "${:int=a${i}}\\n"\n    recv * "${:int=b${i}}\\n"\n  end\nend\n'
        message = self.message(head + "check distinct a*\n")
        self.assertIn("check distinct names `a*`", message)
        self.assertIn("(the group binds a${i}, b${i})", message)
        self.assertIn("leaves out `b${i}`", message)

    def test_A_JSON_array_is_described_the_same_way(self):
        message = self.message('transcript 2\nreply c 200\n  body json {"$unordered":["${:int=a}","${:int=b}"]}\ncheck equal a 1\n')
        self.assertIn("check equal names `a`", message)
        self.assertIn("(the group binds a, b)", message)
        self.assertIn("unordered JSON array", message)

    def test_The_repair_is_still_told(self):
        self.assertIn("name all of the group's bindings in one check distinct, or none", self.message(self.GROUP + "check equal a 1\n"))


class JsonUnorderedBindingTests(unittest.TestCase):
    """The same rule for bindings made inside a JSON {"$unordered": [...]} array: which element takes which value depends on arrival order."""

    HEAD = 'transcript 2\nreply c 200\n  body json {"$unordered":["${:int=a}","${:int=b}"]}\n'

    def refused(self, tail, head=None):
        transcript, problems = parse((head or self.HEAD) + tail)
        self.assertIsNone(transcript, tail)
        self.assertEqual(len(problems), 1, (tail, problems))
        return problems[0]

    def test_Check_equal_naming_a_binding_inside_a_JSON_unordered_array_is_refused_with_file_and_line(self):
        problem = self.refused("check equal a 1\n")
        self.assertEqual((problem.file, problem.line), ("t.out", 4))
        self.assertIn("unordered", problem.message)

    def test_The_review_case_replies_1_2_failed_and_2_1_passed_for_the_same_program_and_is_no_longer_judged(self):
        self.assertEqual(len(rt.parse_transcript((self.HEAD + "check equal a 1\n").encode(), "t.out")[1]), 1)

    def test_Check_distinct_over_part_of_the_array_is_refused_and_over_all_of_it_is_not(self):
        head = 'transcript 2\nreply c 200\n  body json {"$unordered":[{"id":"${:int=a}","k":"${:token}"},{"id":"${:int=b}","k":"${:token}"},{"id":"${:int=c}","k":"${:token}"}]}\n'
        self.refused("check distinct a\n", head)
        self.refused("check distinct a b\n", head)
        self.assertEqual(parse(head + "check distinct a b c\n")[1], [])
        self.assertEqual(parse(head + "check distinct c b a\n")[1], [])

    def test_Check_equal_naming_a_binding_of_the_array_on_either_side_is_refused(self):
        head = self.HEAD + 'recv d "${:int=z}\\n"\n'
        self.refused("check equal z a\n", head)
        self.refused('check equal b "2"\n', head)

    def test_A_JSON_recv_entry_is_judged_like_a_reply(self):
        head = 'transcript 2\nrecv c json {"$unordered":["${:int=a}","${:int=b}"]}\n'
        self.refused("check equal a 1\n", head)
        self.assertEqual(parse(head + "check distinct a b\n")[1], [])

    def test_Bindings_outside_the_array_and_arrays_that_bind_nothing_leave_every_check_alone(self):
        head = 'transcript 2\nreply c 200\n  body json {"id":"${:int=y}","l":{"$unordered":[1,2]}}\nrecv d "${:int=z}\\n"\n'
        self.assertEqual(parse(head + "check equal y z\ncheck distinct y z\ncheck equal y 3\n")[1], [])

    def test_A_binding_beside_the_array_in_the_same_object_is_not_part_of_the_group(self):
        head = 'transcript 2\nreply c 200\n  body json {"id":"${:int=y}","l":{"$unordered":["${:int=a}","${:int=b}"]}}\n'
        self.assertEqual(parse(head + "check equal y 3\ncheck distinct a b\n")[1], [])
        self.refused("check distinct y a\n", head)

    def test_A_nested_array_is_a_group_of_its_own_and_part_of_the_outer_one(self):
        head = 'transcript 2\nreply c 200\n  body json {"$unordered":[{"x":{"$unordered":["${:int=a}","${:int=b}"]}},"${:int=c}"]}\n'
        self.assertEqual(parse(head + "check distinct a b c\n")[1], [])
        self.refused("check distinct a b\n", head)

    def test_A_group_of_entries_and_a_JSON_array_inside_it_are_both_judged(self):
        head = 'transcript 2\nunordered\n  reply * 200\n    body json {"$unordered":["${:int=a}","${:int=b}"]}\n  recv * "x"\nend\n'
        self.refused("check equal a 1\n", head)


class CanonicalTests(unittest.TestCase):
    def test_Strings_are_written_canonically_printable_ASCII_as_is_and_the_rest_escaped(self):
        self.assertEqual(rt.canonical(b'ab "q" \\ \r\n\t\0 \xff\x01'), '"ab \\"q\\" \\\\ \\r\\n\\t\\0 \\xff\\x01"')

    def test_A_dollar_is_escaped_only_where_a_brace_follows_it(self):
        self.assertEqual(rt.canonical(b"$5 ${x} $"), '"$5 \\${x} $"')

    def test_A_non_ASCII_byte_sequence_is_written_as_hex_escapes_even_when_it_is_valid_UTF_8(self):
        self.assertEqual(rt.canonical("é".encode("utf-8")), '"\\xc3\\xa9"')

    def test_The_canonical_text_of_any_bytes_lexes_back_to_the_same_bytes(self):
        data = bytes(range(256)) + b"${a}$"
        cur = lex.Cursor(rt.canonical(data), "t", 1, [])
        self.assertEqual(b"".join(p.data for p in cur.take().value.parts), data)

    def test_A_long_string_is_shortened_to_its_first_60_and_last_40_bytes_with_the_count_between(self):
        shown = rt.abbreviate(b"x" * 300)
        self.assertEqual(shown.count("x"), 100)
        self.assertIn("200", shown)
        self.assertEqual(rt.abbreviate(b"x" * 120), rt.canonical(b"x" * 120))


if __name__ == "__main__":
    unittest.main()
