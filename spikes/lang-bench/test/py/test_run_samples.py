"""The runner's command line (E1.S8a part i): kind, samples, build, --parse-only, limits, exit codes 0/1/3/4/5, the JSON result, encodings."""
import importlib.util
import io
import json
import os
import py_compile
import re
import shutil
import signal
import subprocess
import sys
import tempfile
import time
import unittest

import runner_path  # noqa: F401
import rs_exec as ex
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

    def cli(self, *args, env=None, samples=True, entry=ENTRY, python_flags=("-I", "-B"), generous=True):
        """Runs the CLI. A generous time limit is given unless the test names one (or passes generous=False to meet the floor): the machine is
        often loaded and a wall time is not what most of these tests are about."""
        if generous and "--limits" not in args:
            self.write(self.root, "generous-limits.json", '{"timeS": 60}')
            args = (*args, "--limits", os.path.join(self.root, "generous-limits.json"))
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


class ProgressTests(unittest.TestCase):
    """Progress goes to standard error as it happens; standard output stays the report and nothing else."""

    def lines(self, err):
        return [l for l in err.splitlines() if l.strip()]

    def test_A_full_run_says_when_the_build_and_each_case_start_and_end(self):
        pairs = {"s01.in": b"", "s01.out": ANSWER, "s02.in": b"", "s02.out": b"other\n"}
        code, out, err = Box(self, pairs=pairs).cli()
        got = [re.sub(r" \([^)]*\)$", "", l) for l in self.lines(err)]
        self.assertEqual(got, ["run_samples: build started", "run_samples: build finished", "run_samples: s01 started", "run_samples: s01 PASS",
                               "run_samples: s02 started", "run_samples: s02 FAIL"])

    def test_The_build_line_carries_the_exit_status_and_the_case_line_its_wall_time(self):
        _, _, err = Box(self, build_sh="exit 2\n").cli()
        self.assertRegex(err, r"run_samples: build finished \(exit 2, \d+ ms\)")
        _, _, err = Box(self).cli()
        self.assertRegex(err, r"run_samples: s03 PASS \(\d+ ms\)")

    def test_Standard_output_holds_only_the_report_and_no_progress(self):
        _, out, _ = Box(self).cli()
        self.assertNotIn("run_samples:", out)
        self.assertEqual(out.splitlines()[0], "PASS s03 — 1 entries")

    def test_No_build_means_no_build_lines_and_parse_only_means_no_progress_at_all(self):
        box = Box(self)
        self.assertNotIn("build", box.cli("--no-build")[2])
        self.assertEqual(box.cli("--parse-only")[2], "")

    def test_A_failing_build_says_so_and_starts_no_case(self):
        _, _, err = Box(self, build_sh="exit 1\n").cli()
        self.assertIn("build finished (exit 1", err)
        self.assertNotIn("s03 started", err)

    def test_The_lines_arrive_while_the_case_is_still_running_not_at_the_end(self):
        box = Box(self, run_sh="sleep 3\ncat answer.bin\n")
        box.write(box.root, "limits.json", '{"timeS": 30}')
        proc = subprocess.Popen([sys.executable, "-I", "-B", ENTRY, "--limits", os.path.join(box.root, "limits.json"), box.samples], cwd=box.ws,
                                stdout=subprocess.PIPE, stderr=subprocess.PIPE, env={"PATH": os.environ["PATH"], "TMPDIR": tempfile.mkdtemp(dir=box.root)})
        self.addCleanup(lambda: proc.poll() is None and proc.kill())
        first, second, third = proc.stderr.readline(), proc.stderr.readline(), proc.stderr.readline()
        self.assertEqual(first, b"run_samples: build started\n")
        self.assertIn(b"build finished", second)
        self.assertEqual(third, b"run_samples: s03 started\n")
        self.assertIsNone(proc.poll(), "the case is still running when its start line has been read")
        proc.communicate(timeout=30)


class InterruptTests(unittest.TestCase):
    """A runner killed by a signal while it captures has no verdict: exit 5, no PASS line, no report, nothing left running or in the temp dir."""

    def start(self, box, marker, seconds):
        box.write(box.ws, "run.sh", f"touch {marker}\nexec sleep {seconds}\n")
        box.write(box.root, "limits.json", '{"timeS": 120}')
        tmp = tempfile.mkdtemp(dir=box.root)
        self.tmp = tmp
        proc = subprocess.Popen([sys.executable, "-I", "-B", ENTRY, "--limits", os.path.join(box.root, "limits.json"), box.samples],
                                cwd=box.ws, stdout=subprocess.PIPE, stderr=subprocess.PIPE, env={"PATH": os.environ["PATH"], "TMPDIR": tmp})
        self.addCleanup(lambda: proc.poll() is None and proc.kill())
        deadline = time.monotonic() + 15
        while not os.path.exists(marker) and time.monotonic() < deadline:
            time.sleep(0.02)
        self.assertTrue(os.path.exists(marker), "the case never started")
        return proc

    def alive(self, seconds):
        found = []
        for pid in filter(str.isdigit, os.listdir("/proc")):
            try:
                found += [pid] if f"sleep\0{seconds}".encode() in read_file(f"/proc/{pid}/cmdline") else []
            except OSError:
                continue
        return found

    def interrupted(self, sig):
        box = Box(self)
        seconds = "66.%d" % (os.getpid() % 1000 + 1000)
        proc = self.start(box, os.path.join(box.root, "started"), seconds)
        proc.send_signal(sig)
        out, err = proc.communicate(timeout=30)
        self.assertEqual(proc.returncode, 5, (sig, err))
        self.assertNotIn(b"PASS", out)
        self.assertNotIn(b"FAIL", out)
        self.assertEqual(out, b"", "no complete report, no summary line")
        self.assertIn(b"interrupted", err)
        deadline = time.monotonic() + 5
        while self.alive(seconds) and time.monotonic() < deadline:
            time.sleep(0.05)
        self.assertEqual(self.alive(seconds), [], "the program under test was left running")
        self.assertEqual(os.listdir(self.tmp), [], "the runner's temp folders were left behind")

    def test_A_SIGTERM_to_the_runner_while_it_captures_is_exit_5_with_no_verdict_and_nothing_left(self):
        self.interrupted(signal.SIGTERM)

    def test_A_SIGINT_to_the_runner_while_it_captures_is_exit_5_with_no_verdict_and_nothing_left(self):
        self.interrupted(signal.SIGINT)

    def test_A_signal_during_the_build_is_exit_5_too(self):
        box = Box(self)
        marker = os.path.join(box.root, "building")
        seconds = "67.%d" % (os.getpid() % 1000 + 1000)
        box.write(box.ws, "build.sh", f"touch {marker}\nexec sleep {seconds}\n")
        proc = subprocess.Popen([sys.executable, "-I", "-B", ENTRY, box.samples], cwd=box.ws, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
                                env={"PATH": os.environ["PATH"], "TMPDIR": tempfile.mkdtemp(dir=box.root)})
        self.addCleanup(lambda: proc.poll() is None and proc.kill())
        deadline = time.monotonic() + 15
        while not os.path.exists(marker) and time.monotonic() < deadline:
            time.sleep(0.02)
        proc.send_signal(signal.SIGTERM)
        out, err = proc.communicate(timeout=30)
        self.assertEqual((proc.returncode, out), (5, b""))
        deadline = time.monotonic() + 5
        while self.alive(seconds) and time.monotonic() < deadline:
            time.sleep(0.05)
        self.assertEqual(self.alive(seconds), [], "the build was left running")


class OutputLimitTests(unittest.TestCase):
    def test_An_exact_output_over_the_16_MiB_capture_cap_passes_by_the_stream_and_the_report_and_the_JSON_say_so(self):
        size = 16 * 1024 * 1024 + 1000
        box = Box(self, run_sh=f"head -c {size} /dev/zero\n", pairs={"s01.in": b"", "s01.out": b"\0" * size})
        out_json = os.path.join(box.out, "r.json")
        code, out, _ = box.cli("--json", out_json, "--limits", self.limits(box))
        self.assertEqual(code, 0, out)
        self.assertIn("PASS s01", out)
        self.assertIn(f"output limit: {size} bytes read, the first {16 * 1024 * 1024} kept for the report", out)
        capture = read_json(out_json)["cases"][0]["lifecycle"]["capture"]
        self.assertEqual((capture["truncated"], capture["keptBytes"], capture["totalBytes"]), (True, 16 * 1024 * 1024, size))

    def limits(self, box):
        box.write(box.root, "limits.json", '{"timeS": 60}')
        return os.path.join(box.root, "limits.json")


class ExpectedBytesTests(unittest.TestCase):
    """The program under test runs in the same file system as the samples: whatever it writes there must not become the expected output."""

    def two_cases(self, run_sh=None, build_sh=":\n"):
        box = Box(self, pairs={"s01.in": b"", "s01.out": b"right-one\n", "s02.in": b"", "s02.out": b"right-two\n"})
        script = run_sh or f"printf 'wrong\\n'\nprintf 'wrong\\n' > {box.samples}/s02.out\nprintf 'wrong\\n' > {box.samples}/s01.out\n"
        box.write(box.ws, "run.sh", script)
        box.write(box.ws, "build.sh", build_sh)
        return box

    def test_A_run_sh_that_rewrites_the_expected_output_of_a_later_case_still_gets_FAIL_on_that_case(self):
        box = self.two_cases()
        code, out, _ = box.cli()
        self.assertEqual(code, 1)
        self.assertIn("FAIL s01 \u2014 output differs", out)
        self.assertIn("FAIL s02 \u2014 output differs", out, "s02 was compared with what s01's run wrote into s02.out")

    def test_A_build_sh_that_rewrites_the_expected_output_does_not_change_it_either(self):
        box = self.two_cases(run_sh="printf 'wrong\\n'\n")
        box.write(box.ws, "build.sh", f"printf 'wrong\\n' > {box.samples}/s01.out\nprintf 'wrong\\n' > {box.samples}/s02.out\n")
        code, out, _ = box.cli()
        self.assertEqual(code, 1)
        self.assertEqual([l.split(" ")[0] for l in out.splitlines() if l[:4] in ("FAIL", "PASS")], ["FAIL", "FAIL"])

    def test_A_stub_that_rewrites_the_expected_output_to_its_own_output_is_not_a_PASS(self):
        """The refuted approach: read each .out just before its case runs, so an earlier case's program can rewrite it."""
        box = self.two_cases()
        box.cli()
        self.assertEqual(read_file(os.path.join(box.samples, "s02.out")), b"wrong\n", "the stub did rewrite the file: the attack is real")

    def test_A_run_sh_that_rewrites_cases_txt_does_not_change_the_comparison_or_the_requirement_ids_of_the_record(self):
        box = self.two_cases(run_sh="printf 'right-one\\n'\n")
        box.write(box.samples, "cases.txt", "s01 bytes R1\ns02 bytes R2\n")
        box.write(box.ws, "run.sh", f"printf 'right-one\\n'\nprintf 's01 json R9\\ns02 json R9\\n' > {box.samples}/cases.txt\n")
        out_json = os.path.join(box.out, "r.json")
        box.cli("--json", out_json)
        cases = {c["id"]: c for c in read_json(out_json)["cases"]}
        self.assertEqual((cases["s01"]["comparison"], cases["s01"]["rIds"], cases["s02"]["rIds"]), ("bytes", ["R1"], ["R2"]))


class NotRegularTests(unittest.TestCase):
    """A sample is a regular file. A FIFO, a directory, a device or a link to one is named for what it is, and never opened (a FIFO would block)."""

    def box_with(self, make):
        box = Box(self, pairs={"s01.in": b"7\n"})
        make(os.path.join(box.samples, "s01.out"))
        return box

    def refused(self, box, name="s01.out"):
        code, out, err = box.cli("--parse-only")
        self.assertEqual((code, out), (4, ""))
        self.assertIn(f"{name} is not a regular file", err)
        self.assertNotIn("has no matching", err)

    def test_A_FIFO_as_an_out_file_is_named_not_a_regular_file_and_does_not_hang_the_runner(self):
        self.refused(self.box_with(os.mkfifo))

    def test_A_FIFO_as_an_in_file_is_named_too(self):
        box = Box(self, pairs={"s01.out": b"7\n"})
        os.mkfifo(os.path.join(box.samples, "s01.in"))
        self.refused(box, "s01.in")

    def test_A_directory_and_a_link_to_a_FIFO_are_not_regular_files_either(self):
        self.refused(self.box_with(os.mkdir))
        def link_to_fifo(path):
            os.mkfifo(path + ".fifo-target")
            os.symlink(path + ".fifo-target", path)
        self.refused(self.box_with(link_to_fifo))

    def test_A_FIFO_in_a_full_run_is_exit_4_before_anything_is_built_or_run(self):
        box = self.box_with(os.mkfifo)
        box.write(box.ws, "build.sh", "touch built\n")
        code, out, err = box.cli()
        self.assertEqual(code, 4)
        self.assertFalse(os.path.exists(os.path.join(box.ws, "built")))

    def test_A_missing_pair_member_is_still_reported_as_missing_and_a_regular_file_is_fine(self):
        box = Box(self, pairs={"s01.in": b"7\n"})
        code, _, err = box.cli("--parse-only")
        self.assertEqual(code, 4)
        self.assertIn("has no matching .out", err)
        self.assertEqual(Box(self).cli("--parse-only")[0], 0)


class CleanupFailureTests(unittest.TestCase):
    """A process of the case that could not be killed is a failure of the runner's own cleanup: exit 5, no verdict of any case."""

    def run_with_group_that_never_goes(self, box, *args):
        saved = (ex.group_present, ex.GONE_CEILING_MS)
        ex.group_present, ex.GONE_CEILING_MS = (lambda proc: True), 30
        out, err, real = io.StringIO(), io.StringIO(), (sys.stdout, sys.stderr)
        sys.stdout, sys.stderr = out, err
        try:
            code = rs.run([box.samples, "--workspace", box.ws, *args])
        finally:
            (sys.stdout, sys.stderr), (ex.group_present, ex.GONE_CEILING_MS) = real, saved
        return code, out.getvalue(), err.getvalue()

    def test_A_group_that_is_still_there_after_the_kill_ceiling_is_exit_5_and_no_verdict_is_printed(self):
        code, out, err = self.run_with_group_that_never_goes(Box(self, run_sh="sleep 61 &\ncat answer.bin\n"))
        self.assertEqual(code, 5, err)
        self.assertEqual(out, "", "no PASS, no FAIL, no summary: the case's processes could not be stopped")
        self.assertIn("a process of the case could not be killed", err)
        self.assertNotIn("Traceback", err)

    def test_The_kill_function_itself_raises_when_the_ceiling_passes_with_the_group_still_there(self):
        proc = subprocess.Popen(["sleep", "61"], start_new_session=True)
        self.addCleanup(lambda: (os.killpg(proc.pid, 9), proc.wait()) if proc.poll() is None else None)
        saved = (ex.group_present, ex.GONE_CEILING_MS)
        ex.group_present, ex.GONE_CEILING_MS = (lambda p: True), 20
        try:
            with self.assertRaises(ex.CleanupFailed) as raised:
                ex.kill_group(proc)
        finally:
            ex.group_present, ex.GONE_CEILING_MS = saved
        self.assertIn("a process of the case could not be killed", str(raised.exception))

    def test_A_build_whose_group_will_not_go_is_exit_5_too(self):
        box = Box(self, build_sh="sleep 61 &\nwait\n")
        saved_ceiling = ex.BUILD_CEILING_MS
        ex.BUILD_CEILING_MS = 100
        try:
            code, out, err = self.run_with_group_that_never_goes(box)
        finally:
            ex.BUILD_CEILING_MS = saved_ceiling
        self.assertEqual((code, out), (5, ""))
        self.assertIn("could not be killed", err)

    def test_The_wait_for_the_group_backs_off_from_1_ms_to_50_ms_and_does_not_spin(self):
        clock, slept = [0], []
        saved = (ex.group_present, ex.now_ms, ex.time.sleep)
        proc = subprocess.Popen(["sleep", "61"], start_new_session=True)
        self.addCleanup(lambda: (os.killpg(proc.pid, 9), proc.wait()) if proc.poll() is None else None)

        def sleep(seconds):
            slept.append(seconds)
            clock[0] += seconds * 1000

        ex.group_present, ex.now_ms, ex.time.sleep = (lambda p: True), (lambda: clock[0]), sleep
        try:
            with self.assertRaises(ex.CleanupFailed):
                ex.kill_group(proc)
        finally:
            ex.group_present, ex.now_ms, ex.time.sleep = saved
        self.assertEqual(slept[0], 0.001)
        self.assertEqual(max(slept), 0.05)
        self.assertEqual(slept, sorted(slept), "the delay only grows")
        self.assertEqual(slept[:6], [0.001, 0.002, 0.004, 0.008, 0.016, 0.032])
        self.assertLess(len(slept), 60, f"{len(slept)} polls in the 2 s ceiling: a 1 ms spin would make 2000")

    def test_A_group_that_goes_in_time_is_no_failure(self):
        proc = subprocess.Popen(["sleep", "61"], start_new_session=True)
        ex.kill_group(proc)
        self.assertFalse(ex.group_present(proc))


class SampleSizeTests(unittest.TestCase):
    """Every sample is held in memory for the whole run (the program under test must not be able to change what it is compared with): bounded."""

    def run_in_process(self, box, ceiling):
        saved, sm.MAX_SAMPLE_BYTES = sm.MAX_SAMPLE_BYTES, ceiling
        err, real = io.StringIO(), (sys.stdout, sys.stderr)
        sys.stdout, sys.stderr = io.StringIO(), err
        try:
            code = rs.run([box.samples, "--workspace", box.ws])
        finally:
            (sys.stdout, sys.stderr), sm.MAX_SAMPLE_BYTES = real, saved
        return code, err.getvalue()

    def test_Sizes_are_written_in_binary_units_with_one_decimal_and_without_a_float(self):
        for size, text in ((0, "0 B"), (1000, "1000 B"), (1024, "1.0 KiB"), (1536 * 1024, "1.5 MiB"), (1 << 20, "1.0 MiB"), (110 * (1 << 20), "110.0 MiB"),
                           (1 << 30, "1.0 GiB"), (3 * (1 << 30) + (1 << 29), "3.5 GiB")):
            self.assertEqual(sm.human(size), text, size)

    def test_The_too_large_message_gives_human_units_names_the_largest_files_and_suggests_only(self):
        big = 3 * 1024
        pairs = {"s01.in": b"", "s01.out": b"a" * big, "s02.in": b"x" * 2048, "s02.out": b"b" * 100, "s03.in": b"", "s03.out": b"c" * 10,
                 "s04.in": b"", "s04.out": b"d" * 5}
        box = Box(self, pairs=pairs)
        code, err = self.run_in_process(box, 1024)
        self.assertEqual(code, 4)
        self.assertIn("the ceiling is 1.0 KiB", err)
        self.assertNotIn("bytes of .in", err, "units, not a long number of bytes")
        self.assertRegex(err, r"largest: s01\.out 3\.0 KiB, s02\.in 2\.0 KiB, s02\.out 100 B")
        self.assertNotIn("s03.out", err.split("largest:")[1].split("\n")[0], "only the three largest")
        self.assertIn("--only", err)
        self.assertIn("--only s01", err)

    def test_The_message_with_a_default_ceiling_says_GiB(self):
        self.assertIn("1.0 GiB", sm.too_large_message([], 1 << 30))

    def test_The_ceiling_is_one_GiB(self):
        self.assertEqual(sm.MAX_SAMPLE_BYTES, 1 << 30)

    def test_Samples_over_the_ceiling_are_exit_4_and_nothing_is_run(self):
        box = Box(self, run_sh="touch started\n", pairs={"s01.in": b"x" * 600, "s01.out": b"y" * 600, "s02.in": b"", "s02.out": b"z" * 10})
        code, err = self.run_in_process(box, 1000)
        self.assertEqual(code, 4)
        self.assertIn("the samples are too large to hold", err)
        self.assertFalse(os.path.exists(os.path.join(box.ws, "started")))

    def test_Samples_exactly_at_the_ceiling_are_held_and_one_byte_more_is_not(self):
        box = Box(self, pairs={"s01.in": b"", "s01.out": b"a" * 10})
        self.assertEqual(self.run_in_process(box, 10)[0], 1)
        code, err = self.run_in_process(box, 9)
        self.assertEqual(code, 4)

    def test_Only_the_chosen_cases_count_toward_the_ceiling(self):
        box = Box(self, pairs={"s01.in": b"", "s01.out": b"a" * 10, "s02.in": b"", "s02.out": b"b" * 5000})
        saved, sm.MAX_SAMPLE_BYTES = sm.MAX_SAMPLE_BYTES, 100
        try:
            args = rs.build_parser().parse_args([box.samples, "--workspace", box.ws, "--only", "s01"])
            plan = rs.make_plan(args)
        finally:
            sm.MAX_SAMPLE_BYTES = saved
        self.assertIsInstance(plan, rs.Plan)

    def pair_of(self, box, name):
        return sm.Pair(name, os.path.join(box.samples, name + ".in"), os.path.join(box.samples, name + ".out"), name + ".in", name + ".out")

    def test_A_sample_that_cannot_be_opened_is_cannot_read_for_each_file_and_not_a_size_of_zero(self):
        gone = sm.Pair("s09", "/nonexistent/s09.in", "/nonexistent/s09.out", "s09.in", "s09.out")
        _, problems = sm.load_all([gone])
        self.assertEqual(len(problems), 2, "one for each of the two files")
        self.assertTrue(all("cannot read" in p.message for p in problems))
        self.assertIn("s09.in", problems[0].message)
        self.assertIn("s09.out", problems[1].message)

    def test_A_FIFO_swapped_in_after_the_listing_is_refused_by_what_was_opened_and_does_not_block(self):
        box = Box(self, pairs={"s01.in": b"", "s01.out": b"x"})
        os.remove(os.path.join(box.samples, "s01.out"))
        os.mkfifo(os.path.join(box.samples, "s01.out"))
        loaded, problems = sm.load_all([self.pair_of(box, "s01")])
        self.assertEqual([p.message for p in problems], ["s01.out is not a regular file"])

    def test_A_file_is_read_for_at_most_the_budget_left_plus_one_byte_whatever_its_size(self):
        box = Box(self, pairs={"s01.in": b"", "s01.out": b"x" * 3_000_000})
        real, total = os.read, []

        def counting(fd, n):
            data = real(fd, n)
            total.append(len(data))
            return data

        saved, sm.MAX_SAMPLE_BYTES, os.read = sm.MAX_SAMPLE_BYTES, 1000, counting
        try:
            loaded, problems = sm.load_all([self.pair_of(box, "s01")])
        finally:
            os.read, sm.MAX_SAMPLE_BYTES = real, saved
        self.assertEqual(len(problems), 1)
        self.assertIn("too large to hold", problems[0].message)
        self.assertEqual(sum(total), 1001, "the budget and one byte more: the size limit rests on what was read")

    def test_The_budget_is_shared_by_all_the_samples_so_many_small_ones_can_pass_it(self):
        pairs = {f"s{n:02d}.{ext}": b"x" * 100 for n in range(1, 6) for ext in ("in", "out")}
        box = Box(self, pairs=pairs)
        code, err = self.run_in_process(box, 999)
        self.assertEqual(code, 4)
        self.assertIn("too large", err)
        self.assertNotEqual(self.run_in_process(box, 1000)[0], 4)

    def test_A_sample_that_vanishes_after_the_listing_is_exit_4_cannot_read(self):
        box = Box(self)
        saved = sm.discover

        def vanishing(samples_dir):
            pairs, problems = saved(samples_dir)
            os.remove(pairs[0].out_path)
            return pairs, problems

        sm.discover = vanishing
        try:
            code, err = self.run_in_process(box, 1 << 30)
        finally:
            sm.discover = saved
        self.assertEqual(code, 4)
        self.assertIn("cannot read", err)

    def test_A_pair_that_cannot_be_read_is_a_configuration_problem_and_not_a_crash(self):
        box = Box(self)
        os.chmod(os.path.join(box.samples, "s03.out"), 0)
        try:
            code, err = self.run_in_process(box, 1 << 30)
        finally:
            os.chmod(os.path.join(box.samples, "s03.out"), 0o644)
        self.assertEqual(code, 4, err)
        self.assertIn("cannot read", err)


class TemporaryFolderTests(unittest.TestCase):
    """The runner makes a folder for its bytecode prefix (and one for the run): whichever way a run ends normally, none is left behind."""

    def leftovers(self, box, *args):
        tmp = tempfile.mkdtemp(dir=box.root)
        box.cli(*args, env={"TMPDIR": tmp})
        return sorted(os.listdir(tmp))

    def test_A_full_run_leaves_nothing_in_the_temp_folder(self):
        self.assertEqual(self.leftovers(Box(self)), [])

    def test_A_failing_run_a_parse_only_run_and_a_configuration_error_leave_nothing_either(self):
        self.assertEqual(self.leftovers(Box(self, run_sh="printf wrong\\n")), [])
        self.assertEqual(self.leftovers(Box(self), "--parse-only"), [])
        self.assertEqual(self.leftovers(Box(self), "--no-such-flag"), [])
        self.assertEqual(self.leftovers(Box(self, pairs={})), [])

    def test_The_prefix_folder_is_removed_by_the_runner_when_the_interpreter_exits(self):
        probe = subprocess.run([sys.executable, "-I", "-B", "-c", "import sys, tempfile, os; sys.path.insert(0, sys.argv[1]); import run_samples; "
                                "print(sys.pycache_prefix)", runner_path.RUNNER_DIR], capture_output=True, text=True, env={"TMPDIR": tempfile.mkdtemp(dir=Box(self).root)})
        self.assertFalse(os.path.exists(probe.stdout.strip()), "the prefix folder outlived the interpreter")


class OlympiadTests(unittest.TestCase):
    def test_The_format_olympiad_example_with_a_replay_stub_prints_PASS_s03_1_entries_and_exits_0(self):
        code, out, err = Box(self).cli()
        self.assertEqual(code, 0)
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
        code, out, _ = box.cli(generous=False)
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

    def test_A_sample_whose_id_could_not_be_a_case_id_is_exit_4_and_nothing_is_run_even_with_a_launch_prefix(self):
        for name in ("a b", "x;y", "$(id)", "-rf", "caf\u00e9"):
            box = Box(self, pairs={f"{name}.in": b"", f"{name}.out": b""})
            code, out, err = box.cli("--launch-prefix", json.dumps(["env", "X={case}"]))
            self.assertEqual((code, out), (4, ""), name)
            self.assertIn("not a sample id", err)

    def test_A_normalised_olympiad_case_is_exit_4_never_a_silent_byte_comparison(self):
        forms = ("normalised", "normalised+json", "json+normalised", "normalised+relational", "json", "relational", "json+relational", "relational+json")
        for comparison in forms:
            box = Box(self)
            box.write(box.samples, "cases.txt", f"s03 {comparison} R1\n")
            code, out, err = box.cli()
            self.assertEqual(code, 4, comparison)
            self.assertIn("not supported for olympiad", err)
            self.assertIn("s03", err)
            self.assertEqual(out, "", "nothing was run")

    def test_A_normalised_olympiad_case_is_refused_in_parse_only_too_and_a_bytes_case_is_not(self):
        box = Box(self)
        for comparison in ("normalised", "json", "relational", "json+relational"):
            box.write(box.samples, "cases.txt", f"s03 {comparison} R1\n")
            self.assertEqual(box.cli("--parse-only")[0], 4, comparison)
        box.write(box.samples, "cases.txt", "s03 bytes R1\n")
        self.assertEqual(box.cli("--parse-only")[0], 0)
        self.assertEqual(box.cli()[0], 0)

    def test_Every_comparison_of_a_daemon_or_web_task_is_not_refused_by_this_rule(self):
        for comparison in ("bytes", "normalised", "json", "relational", "json+relational"):
            pairs = {"x2.in": read_file(os.path.join(FORMAT, "x2.in")), "x2.out": read_file(os.path.join(FORMAT, "x2.out")),
                     "cases.txt": f"x2 {comparison} R1\n".encode()}
            self.assertEqual(Box(self, kind=DAEMON, pairs=pairs).cli("--parse-only")[0], 0, comparison)

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
        self.assertEqual(sorted(record), ["build", "cases", "emptyProgram", "emptyProgramMs", "format", "grammar", "kind", "launch", "python", "runner"])
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
