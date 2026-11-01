"""The runner's command line (E1.S8a part i): kind, samples, build, --parse-only, limits, exit codes 0/1/3/4/5, the JSON result, encodings."""
import importlib.util
import io
import json
import os
import py_compile
import shutil
import subprocess
import sys
import tempfile
import unittest

import runner_path  # noqa: F401
import rs_samples as sm
import run_samples as rs

ENTRY = os.path.join(runner_path.RUNNER_DIR, "run_samples.py")
FORMAT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "fixtures", "runner", "format")
ANSWER = b"7 22 11 34 17 52 26 13 40 20 10 5 16 8 4 2 1\n"
OLYMPIAD = "a program that reads standard input once and writes standard output once"
DAEMON = "a long-running program that serves until it is stopped by SIGTERM"
WEB = "a long-running HTTP server that serves until it is stopped by SIGTERM"


def statement(kind_phrase):
    return f"# Task 03 — Toy\n\n| | |\n|---|---|\n| Kind | {kind_phrase} |\n| Gate verdict | not yet gated |\n"


def read_file(path, mode="rb"):
    with open(path, mode) as f:
        return f.read()


def read_json(path):
    return json.loads(read_file(path, "r"))


class Box:
    """A workspace and a samples directory in a temporary folder."""

    def __init__(self, test, kind=OLYMPIAD, run_sh="cat answer.bin\n", build_sh=":\n", pairs=None):
        holder = tempfile.TemporaryDirectory()
        test.addCleanup(holder.cleanup)
        self.root = holder.name
        self.ws, self.samples, self.out = (os.path.join(self.root, n) for n in ("ws", "samples", "out"))
        for d in (self.ws, self.samples, self.out):
            os.mkdir(d)
        self.write(self.ws, "STATEMENT.md", statement(kind))
        self.write(self.ws, "run.sh", run_sh)
        self.write(self.ws, "build.sh", build_sh)
        self.write(self.ws, "answer.bin", ANSWER)
        for name, data in (pairs if pairs is not None else {"s03.in": b"7\n", "s03.out": ANSWER}).items():
            self.write(self.samples, name, data)

    def write(self, folder, name, data):
        with open(os.path.join(folder, name), "wb") as f:
            f.write(data if isinstance(data, bytes) else data.encode("utf-8"))

    def cli(self, *args, env=None, samples=True, entry=ENTRY, python_flags=("-I", "-B")):
        argv = [sys.executable, *python_flags, entry, *args] + ([self.samples] if samples else [])
        full_env = {"PATH": os.environ["PATH"], "TMPDIR": tempfile.gettempdir(), **(env or {})}
        done = subprocess.run(argv, capture_output=True, cwd=self.ws, env=full_env, timeout=120)
        return done.returncode, done.stdout.decode("utf-8", "backslashreplace"), done.stderr.decode("utf-8", "backslashreplace")


class CacheTests(unittest.TestCase):
    """A bytecode cache planted in the runner's tree must never be read: the pin hashes sources, an unchecked-hash .pyc would run instead."""

    def runner_copy(self):
        holder = tempfile.TemporaryDirectory()
        self.addCleanup(holder.cleanup)
        copy = os.path.join(holder.name, "runner")
        shutil.copytree(runner_path.RUNNER_DIR, copy, ignore=shutil.ignore_patterns("__pycache__"))
        return copy

    def plant(self, copy, module, source_suffix):
        """An unchecked-hash cache of `module` with `source_suffix` appended to its source: the source on disk stays the pinned one."""
        path = os.path.join(copy, module + ".py")
        evil = os.path.join(os.path.dirname(copy), module + "_evil.py")
        with open(path, "r", encoding="utf-8") as f, open(evil, "w", encoding="utf-8") as out:
            out.write(f.read() + source_suffix)
        py_compile.compile(evil, cfile=self.beside(path), doraise=True, invalidation_mode=py_compile.PycInvalidationMode.UNCHECKED_HASH)

    def beside(self, path):
        """The cache path next to a source, whatever prefix this process (which imported run_samples) has set for its own imports."""
        prefix, sys.pycache_prefix = sys.pycache_prefix, None
        try:
            return importlib.util.cache_from_source(path)
        finally:
            sys.pycache_prefix = prefix

    def test_A_planted_unchecked_hash_cache_is_not_run_in_place_of_the_pinned_source(self):
        copy = self.runner_copy()
        self.plant(copy, "rs_olympiad", "\n_judge = lambda *a, **k: None\n")
        box = Box(self, run_sh="printf 'wrong\\n'\n")
        code, out, err = box.cli(entry=os.path.join(copy, "run_samples.py"), python_flags=("-I",))
        self.assertEqual(code, 1, (out, err))
        self.assertIn("FAIL s03 \u2014 output differs", out)

    def test_The_planted_cache_would_win_if_it_were_read_and_that_is_the_point(self):
        copy = self.runner_copy()
        self.plant(copy, "rs_olympiad", "\n_judge = lambda *a, **k: None\n")
        done = subprocess.run([sys.executable, "-I", "-B", "-c", "import sys; sys.path.insert(0, sys.argv[1]); import rs_olympiad; print(rs_olympiad._judge.__name__)", copy],
                              capture_output=True, text=True)
        self.assertEqual(done.stdout.strip(), "<lambda>", "the plain import reads the planted cache: this is the attack, and why run_samples must not")

    def test_The_entry_points_the_cache_prefix_at_a_fresh_folder_before_any_sibling_is_imported(self):
        code, out, err = Box(self).cli("--parse-only")
        self.assertEqual((code, err), (0, ""))
        probe = subprocess.run([sys.executable, "-I", "-B", "-c", "import sys, runpy; sys.path.insert(0, sys.argv[1]); import run_samples; print(sys.pycache_prefix)", runner_path.RUNNER_DIR],
                               capture_output=True, text=True)
        self.assertNotIn(probe.stdout.strip(), ("None", ""), "importing the entry sets sys.pycache_prefix")


class OlympiadTests(unittest.TestCase):
    def test_The_format_olympiad_example_with_a_replay_stub_prints_PASS_s03_1_entries_and_exits_0(self):
        code, out, err = Box(self).cli()
        self.assertEqual((code, err), (0, ""))
        self.assertIn("PASS s03 — 1 entries", out.splitlines())

    def test_A_stub_that_drops_the_final_line_feed_prints_FAIL_s03_with_the_first_differing_byte_and_exits_1(self):
        box = Box(self, run_sh="printf '%s' \"$(cat answer.bin)\"\n")
        code, out, _ = box.cli()
        self.assertEqual(code, 1)
        self.assertIn("FAIL s03 — output ends early", out)
        self.assertIn("first difference at byte 44", out)

    def test_Every_case_says_its_memory_is_not_certified_without_a_cgroup(self):
        _, out, _ = Box(self).cli()
        self.assertIn("memory: not certified", out)

    def test_Without_a_limits_file_the_time_limit_is_the_floor_and_every_time_verdict_says_so(self):
        box = Box(self, run_sh="sleep 1.3\n")
        code, out, _ = box.cli()
        self.assertEqual(code, 1)
        self.assertIn("against the floor", out)

    def test_A_limits_file_sets_the_olympiad_time_limit_and_the_verdict_names_it_as_registered(self):
        box = Box(self, run_sh="sleep 1.3\ncat answer.bin\n")
        box.write(box.root, "limits.json", '{"timeS": 3}')
        code, out, _ = box.cli("--limits", os.path.join(box.root, "limits.json"))
        self.assertEqual((code, "PASS s03" in out), (0, True))
        box.write(box.root, "limits.json", '{"timeS": 1.2}')
        code, out, _ = box.cli("--limits", os.path.join(box.root, "limits.json"))
        self.assertEqual(code, 1)
        self.assertIn("the registered limit", out)

    def test_The_cases_run_in_id_order_and_only_selects_one(self):
        pairs = {"s02.in": b"", "s02.out": ANSWER, "s01.in": b"", "s01.out": ANSWER}
        box = Box(self, pairs=pairs)
        _, out, _ = box.cli()
        self.assertEqual([l.split(" ")[1] for l in out.splitlines() if l.startswith("PASS")], ["s01", "s02"])
        _, out, _ = box.cli("--only", "s02")
        self.assertEqual([l.split(" ")[1] for l in out.splitlines() if l.startswith("PASS")], ["s02"])

    def test_The_kind_is_read_from_the_Kind_row_of_the_statement_unless_it_is_given(self):
        box = Box(self, kind=DAEMON)
        self.assertEqual(box.cli("--kind", "olympiad")[0], 0)
        self.assertEqual(Box(self, kind="something else").cli()[0], 4)

    def test_A_failing_build_fails_every_case_and_exits_1_while_no_build_skips_it(self):
        box = Box(self, build_sh="echo broken >&2; exit 2\n")
        code, out, _ = box.cli()
        self.assertEqual(code, 1)
        self.assertIn("FAIL s03 — build failed", out)
        self.assertIn("broken", out)
        self.assertEqual(box.cli("--no-build")[0], 0)

    def test_A_launch_prefix_is_put_before_bash_run_sh_with_the_case_filled_in(self):
        box = Box(self, run_sh='printf "%s\\n" "$PICKED"\n', pairs={"s03.in": b"", "s03.out": b"s03\n"})
        code, out, _ = box.cli("--launch-prefix", json.dumps(["env", "PICKED={case}"]))
        self.assertEqual(code, 0, out)


class ParseOnlyTests(unittest.TestCase):
    def daemon_box(self, test, script=None):
        pairs = {"x2.in": script if script is not None else read_file(os.path.join(FORMAT, "x2.in")), "x2.out": read_file(os.path.join(FORMAT, "x2.out"))}
        return Box(test, kind=DAEMON, run_sh="touch started\n", pairs=pairs)

    def test_Parse_only_parses_and_validates_every_pair_and_starts_nothing(self):
        box = self.daemon_box(self)
        code, out, err = box.cli("--parse-only")
        self.assertEqual((code, err), (0, ""))
        self.assertIn("1 pairs parsed", out)
        self.assertFalse(os.path.exists(os.path.join(box.ws, "started")), "nothing may be started and no build run")

    def test_The_format_examples_of_both_kinds_parse(self):
        for kind, name in ((DAEMON, "x2"), (WEB, "x3")):
            pairs = {f"{name}.in": read_file(os.path.join(FORMAT, f"{name}.in")), f"{name}.out": read_file(os.path.join(FORMAT, f"{name}.out"))}
            self.assertEqual(Box(self, kind=kind, pairs=pairs).cli("--parse-only")[0], 0, name)

    def test_Scenario_1_is_exit_4_naming_the_file_and_line_and_nothing_is_started(self):
        box = self.daemon_box(self, script=b"scenario 1\ndefault within 2s\n")
        code, out, err = box.cli("--parse-only")
        self.assertEqual(code, 4)
        self.assertRegex(err, r"x2\.in:1: .*scenario 2")
        self.assertFalse(os.path.exists(os.path.join(box.ws, "started")))

    def test_A_malformed_sample_is_exit_4_in_a_full_run_too_before_anything_starts(self):
        box = self.daemon_box(self, script=b"scenario 2\ndefault within 2s\nstart ready port within 1s\nsleep 1s within 2s\nwait-exit\n")
        code, _, err = box.cli()
        self.assertEqual(code, 4)
        self.assertRegex(err, r"x2\.in:4: ")
        self.assertFalse(os.path.exists(os.path.join(box.ws, "started")))

    def test_A_transcript_that_does_not_correspond_to_its_script_is_exit_4(self):
        box = self.daemon_box(self)
        box.write(box.samples, "x2.out", "transcript 2\nready\nexit 0\n")
        code, _, err = box.cli("--parse-only")
        self.assertEqual(code, 4)
        self.assertIn("x2.out", err)

    def test_A_daemon_or_web_run_without_parse_only_is_refused_until_part_two_exists(self):
        box = self.daemon_box(self)
        code, _, err = box.cli()
        self.assertEqual(code, 5)
        self.assertIn("daemon", err)

    def test_An_olympiad_parse_only_checks_the_pairs_exist_and_runs_nothing(self):
        box = Box(self, run_sh="touch started\n")
        code, out, _ = box.cli("--parse-only")
        self.assertEqual(code, 0)
        self.assertFalse(os.path.exists(os.path.join(box.ws, "started")))


class ConfigurationTests(unittest.TestCase):
    def test_A_sample_without_its_pair_a_unknown_flag_and_an_unknown_only_are_exit_4(self):
        self.assertEqual(Box(self, pairs={"s01.in": b""}).cli()[0], 4)
        self.assertEqual(Box(self).cli("--no-such-flag")[0], 4)
        self.assertEqual(Box(self).cli("--only", "s99")[0], 4)
        self.assertEqual(Box(self).cli("--kind", "robot")[0], 4)

    def test_No_samples_directory_argument_or_a_missing_directory_is_exit_4(self):
        self.assertEqual(Box(self).cli(samples=False)[0], 4)
        box = Box(self)
        done = subprocess.run([sys.executable, "-I", "-B", ENTRY, os.path.join(box.root, "nowhere")], capture_output=True, cwd=box.ws)
        self.assertEqual(done.returncode, 4)

    def test_A_missing_workspace_is_exit_3_because_that_is_the_environment(self):
        box = Box(self)
        self.assertEqual(box.cli("--workspace", os.path.join(box.root, "nowhere"))[0], 3)

    def test_A_machine_without_bash_is_exit_3_because_that_is_the_environment(self):
        code, _, err = Box(self).cli(env={"PATH": "/nonexistent"})
        self.assertEqual(code, 3)
        self.assertIn("cannot start", err)

    def test_A_limits_file_with_an_unknown_key_a_value_below_the_floor_or_bad_JSON_is_exit_4(self):
        box = Box(self)
        for text in ('{"bogus": 1}', '{"timeS": 0.5}', "{", '{"timeS": "2"}', '{"timeS": NaN}', '{"readyS": 0}'):
            box.write(box.root, "limits.json", text)
            self.assertEqual(box.cli("--limits", os.path.join(box.root, "limits.json"))[0], 4, text)

    def test_A_launch_prefix_that_is_not_a_JSON_list_of_strings_is_exit_4(self):
        for text in ("not json", '{"a":1}', "[1, 2]", '"x"'):
            self.assertEqual(Box(self).cli("--launch-prefix", text)[0], 4, text)

    def test_The_options_that_belong_to_part_two_are_refused_instead_of_silently_ignored(self):
        for flag, value in (("--cgroup-of", "/x/{case}"), ("--launch-status", "3"), ("--case-view", "/v/{case}")):
            code, _, err = Box(self).cli(flag, value)
            self.assertEqual(code, 4, flag)
            self.assertIn("not supported", err)

    def test_A_cases_file_attaches_its_comparison_and_requirement_ids_to_each_case_in_the_JSON_result(self):
        box = Box(self)
        box.write(box.samples, "cases.txt", "# manifest\ns03 bytes R1 R4\n")
        out_json = os.path.join(box.out, "r.json")
        self.assertEqual(box.cli("--json", out_json)[0], 0)
        record = read_json(out_json)
        self.assertEqual((record["cases"][0]["rIds"], record["cases"][0]["comparison"]), (["R1", "R4"], "bytes"))

    def test_A_case_with_no_manifest_line_has_no_comparison_in_the_record(self):
        box = Box(self)
        out_json = os.path.join(box.out, "r.json")
        box.cli("--json", out_json)
        self.assertIsNone(read_json(out_json)["cases"][0]["comparison"])

    def manifest(self, text, ids=("s03",)):
        with tempfile.TemporaryDirectory() as folder:
            with open(os.path.join(folder, "cases.txt"), "w", encoding="utf-8") as f:
                f.write(text)
            return sm.cases_manifest(folder, set(ids))

    def test_Every_comparison_form_of_the_format_is_accepted_and_kept(self):
        forms = ["bytes", "normalised", "json", "relational", "json+relational", "normalised+json", "normalised+relational", "normalised+json+relational", "relational+json"]
        for form in forms:
            manifest, problems = self.manifest(f"s03 {form} R1 R2\n")
            self.assertEqual(problems, [], form)
            self.assertEqual(manifest, {"s03": (form, ("R1", "R2"))}, form)

    def test_A_comparison_that_is_not_one_of_the_forms_is_refused_naming_it(self):
        for form in ("R1", "byte", "bytes+json", "json+bytes", "json+json", "json+", "+json", "json++relational", "JSON", "exact"):
            manifest, problems = self.manifest(f"s03 {form} R1\n")
            self.assertEqual([p.line for p in problems], [1], form)
            self.assertIn("comparison", problems[0].message, form)

    def test_A_line_without_a_comparison_or_without_an_R_id_is_refused(self):
        for text in ("s03\n", "s03 bytes\n", "s03 R1\n", "s03 bytes X1\n", "s03 bytes R1 R1x\n"):
            self.assertTrue(self.manifest(text)[1], text)

    def test_A_malformed_cases_line_a_repeated_id_or_an_id_with_no_pair_is_exit_4(self):
        for text in ("s03\n", "s03 bytes R1\ns03 bytes R2\n", "s03 bytes X1\n", "s77 bytes R1\n", "s03 R1\n"):
            box = Box(self)
            box.write(box.samples, "cases.txt", text)
            self.assertEqual(box.cli()[0], 4, text)


class ResultTests(unittest.TestCase):
    def test_The_JSON_result_has_the_documented_shape(self):
        box = Box(self)
        out_json = os.path.join(box.out, "r.json")
        box.cli("--json", out_json)
        record = read_json(out_json)
        self.assertEqual((record["format"], record["grammar"], record["kind"]), (1, 2, "olympiad"))
        self.assertEqual(sorted(record), ["build", "cases", "format", "grammar", "kind", "python", "runner"])
        case = record["cases"][0]
        self.assertEqual((case["id"], case["status"], case["entries"]), ("s03", "PASS", 1))
        self.assertEqual(case["lifecycle"]["memory"], {"known": False, "why": "not certified"})
        self.assertEqual(record["build"]["ran"], True)

    def test_Without_a_build_the_record_says_it_did_not_run(self):
        box = Box(self)
        out_json = os.path.join(box.out, "r.json")
        box.cli("--no-build", "--json", out_json)
        self.assertEqual(read_json(out_json)["build"], {"ran": False})


class EncodingTests(unittest.TestCase):
    def test_A_non_UTF_8_locale_does_not_turn_a_FAIL_into_a_crash(self):
        # -X utf8=0: in the C locale Python 3.14 would otherwise switch to UTF-8 mode by itself and the stream would be UTF-8 without any help.
        box = Box(self, run_sh="printf 'wrong\\n'\n")
        box.write(box.samples, "éè-note.txt", "ignored")
        locales = ["C", "POSIX", "de_DE.utf8", "en_US.ISO-8859-1", "de_DE.ISO-8859-1"]
        for locale in locales:
            code, out, err = box.cli(env={"LC_ALL": locale, "LANG": locale}, python_flags=("-I", "-B", "-X", "utf8=0"))
            self.assertEqual(code, 1, (locale, err))
            self.assertNotIn("Traceback", err, locale)
            self.assertIn("FAIL s03 — output differs", out, locale)

    def test_A_file_name_that_is_not_UTF_8_is_reported_without_a_crash_whatever_the_locale(self):
        box = Box(self, pairs={})
        os.close(os.open(os.path.join(box.samples.encode(), b"bad\xff.in"), os.O_CREAT | os.O_WRONLY))
        for locale in ("C", "de_DE.utf8"):
            code, _, err = box.cli(env={"LC_ALL": locale})
            self.assertEqual(code, 4, (locale, err))
            self.assertNotIn("Traceback", err)
            self.assertIn("bad\\xff.in", err)

    def test_The_streams_are_reconfigured_to_UTF_8_with_backslashreplace_whatever_they_were(self):
        ascii_out = io.TextIOWrapper(io.BytesIO(), encoding="ascii", errors="strict")
        rs.configure_streams(ascii_out)
        ascii_out.write("— \udcff")
        ascii_out.flush()
        self.assertEqual(ascii_out.buffer.getvalue(), "— ".encode("utf-8") + b"\\udcff")

    def test_The_encoding_of_the_runner_does_not_depend_on_the_environment_variables_that_isolated_mode_ignores(self):
        code, out, _ = Box(self).cli(env={"PYTHONIOENCODING": "ascii", "PYTHONUTF8": "0"})
        self.assertEqual(code, 0)
        self.assertIn("PASS s03 —", out)


if __name__ == "__main__":
    unittest.main()
