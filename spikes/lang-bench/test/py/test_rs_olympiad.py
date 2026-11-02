"""The olympiad path: one run of `bash run.sh` per case, stdout compared as a stream against the expected bytes (B9), stderr to a file,
wall time judged by the rule that a wall over the limit is a time limit even when the program exited by itself."""
import os
import subprocess
import tempfile
import time
import unittest
from unittest import mock

import runner_path  # noqa: F401
import rs_exec as ex
import rs_launch as rl
import rs_pump
import rs_olympiad as ro
import rs_report as rp
from rs_track import Tracker

ANSWER = b"7 22 11 34 17 52 26 13 40 20 10 5 16 8 4 2 1\n"


class Workspace:
    """A temporary workspace whose run.sh is the stub under test."""

    def __init__(self, test, run_sh, files=None):
        holder = tempfile.TemporaryDirectory()
        test.addCleanup(holder.cleanup)
        self.dir = os.path.join(holder.name, "ws")
        os.mkdir(self.dir)
        self.scratch = os.path.join(holder.name, "scratch")
        os.mkdir(self.scratch)
        for name, data in {"run.sh": run_sh, **(files or {})}.items():
            with open(os.path.join(self.dir, name), "wb") as f:
                f.write(data if isinstance(data, bytes) else data.encode())

    def run(self, case, **overrides):
        """Runs one case. The limit is generous unless a test names it: the machine these tests run on is often loaded, and a wall time is
        not what most of them are about (the default of Config is the floor, asserted on its own below)."""
        return ro.run_case(case, ex.Config(workspace=self.dir, scratch=self.scratch, **{"limit_ms": 20_000, **overrides}))


REPLAY = "cat answer.bin\n"


def case(expected=ANSWER, stdin=b"7\n", ident="s03"):
    return ro.Case(ident, stdin, expected)


class VerdictTests(unittest.TestCase):
    def test_A_replay_stub_passes_with_one_entry(self):
        result = Workspace(self, REPLAY, {"answer.bin": ANSWER}).run(case())
        self.assertEqual((result.status, result.entries, result.reason), ("PASS", 1, ""))

    def test_A_stub_that_drops_the_final_line_feed_fails_at_the_byte_it_dropped(self):
        result = Workspace(self, "printf '%s' \"$(cat answer.bin)\"\n", {"answer.bin": ANSWER}).run(case())
        self.assertEqual((result.status, result.reason), ("FAIL", "output ends early"))
        self.assertEqual(result.first_mismatch, {"byte": len(ANSWER) - 1})
        self.assertIn("  first difference at byte 44", result.lines)

    def test_A_wrong_byte_in_the_middle_is_named_by_its_offset(self):
        result = Workspace(self, REPLAY, {"answer.bin": ANSWER.replace(b"22", b"23")}).run(case())
        self.assertEqual((result.reason, result.first_mismatch), ("output differs", {"byte": 3}))

    def test_Extra_output_after_the_expected_bytes_is_a_difference_at_their_end(self):
        result = Workspace(self, REPLAY, {"answer.bin": ANSWER + b"x"}).run(case())
        self.assertEqual((result.reason, result.first_mismatch), ("output differs", {"byte": len(ANSWER)}))

    def test_An_exit_status_other_than_zero_fails_whatever_was_written(self):
        result = Workspace(self, "cat answer.bin; exit 3\n", {"answer.bin": ANSWER}).run(case())
        self.assertEqual((result.status, result.reason), ("FAIL", "exit status 3"))

    def test_A_program_killed_by_a_signal_is_named_by_it(self):
        result = Workspace(self, "cat answer.bin; kill -SEGV $$\n", {"answer.bin": ANSWER}).run(case())
        self.assertEqual((result.status, result.reason), ("FAIL", "killed by signal SIGSEGV"))

    def test_The_input_is_the_programs_standard_input_byte_for_byte(self):
        result = Workspace(self, "cat\n").run(case(expected=b"a\r\n\x00\xffb", stdin=b"a\r\n\x00\xffb"))
        self.assertEqual(result.status, "PASS")

    def test_Empty_input_and_empty_expected_output_pass(self):
        self.assertEqual(Workspace(self, ":\n").run(case(expected=b"", stdin=b"")).status, "PASS")


class StreamingTests(unittest.TestCase):
    def test_A_long_expected_output_is_compared_streaming(self):
        expected = bytes(range(256)) * 80
        result = Workspace(self, REPLAY, {"answer.bin": expected}).run(case(expected=expected), capture_cap=1024)
        self.assertEqual((result.status, result.entries), ("PASS", 1), "20 KiB against a 1 KiB capture: the stream decides, not the capture")

    def test_A_capped_capture_is_never_a_PASS(self):
        expected = b"a" * 4096
        naive = lambda kept, exp: kept == exp[:len(kept)]
        late_garbage = b"a" * 4096 + b"zzzz"
        short = b"a" * 2048
        self.assertTrue(naive(late_garbage[:1024], expected) and naive(short[:1024], expected), "the refuted compare of the capture alone passes both")
        for printed, reason in ((late_garbage, "output differs"), (short, "output ends early")):
            result = Workspace(self, REPLAY, {"answer.bin": printed}).run(case(expected=expected), capture_cap=1024)
            self.assertEqual((result.status, result.reason), ("FAIL", reason))

    def test_A_difference_beyond_the_capture_is_still_named_by_its_byte_and_says_the_bytes_were_not_kept(self):
        expected = b"a" * 4096
        printed = b"a" * 3000 + b"b" + b"a" * 1095
        result = Workspace(self, REPLAY, {"answer.bin": printed}).run(case(expected=expected), capture_cap=1024)
        self.assertEqual(result.first_mismatch, {"byte": 3000})
        actual_line = [l for l in result.lines if l.lstrip().startswith("actual")][0]
        self.assertNotIn("(not kept)", actual_line, "a small window of the actual bytes around the difference is kept past the cap")
        self.assertIn("a" * 20 + "b" + "a" * 20, actual_line)
        self.assertLess(len(actual_line), 200)

    def test_A_difference_far_past_the_cap_in_a_later_chunk_shows_the_bytes_before_it_too(self):
        expected = b"a" * 200_000
        printed = b"a" * 100_000 + b"b" + b"a" * 99_999
        result = Workspace(self, REPLAY, {"answer.bin": printed}).run(case(expected=expected), capture_cap=1024)
        self.assertEqual(result.first_mismatch, {"byte": 100_000})
        actual_line = [l for l in result.lines if l.lstrip().startswith("actual")][0]
        self.assertIn("a" * 20 + "b", actual_line)

    def test_An_output_that_ends_early_past_the_cap_shows_the_last_bytes_it_wrote(self):
        expected = b"a" * 4096
        printed = b"a" * 3000
        result = Workspace(self, REPLAY, {"answer.bin": printed}).run(case(expected=expected), capture_cap=1024)
        self.assertEqual((result.reason, result.first_mismatch), ("output ends early", {"byte": 3000}))
        actual_line = [l for l in result.lines if l.lstrip().startswith("actual")][0]
        self.assertNotIn("(not kept)", actual_line)
        self.assertIn("a" * 30, actual_line)

    def test_A_program_that_prints_a_wrong_byte_and_never_stops_is_cut_off_at_once(self):
        started = time.monotonic()
        result = Workspace(self, "yes x\n").run(case(expected=b"y\n" * 10), limit_ms=20_000)
        self.assertEqual((result.status, result.reason), ("FAIL", "output differs"))
        self.assertLess(time.monotonic() - started, 5)

    def test_A_chatty_standard_error_goes_to_a_file_and_cannot_block_the_program(self):
        run_sh = "head -c 5000000 /dev/zero >&2; cat answer.bin\n"
        result = Workspace(self, run_sh, {"answer.bin": ANSWER}).run(case(), limit_ms=20_000)
        self.assertEqual(result.status, "PASS")
        self.assertEqual(result.lifecycle["stderr"]["bytes"], 5_000_000)

    def test_A_program_that_never_reads_a_large_input_does_not_deadlock_the_runner(self):
        result = Workspace(self, "cat answer.bin\n", {"answer.bin": ANSWER}).run(case(stdin=b"x" * 4_000_000), limit_ms=20_000)
        self.assertEqual(result.status, "PASS")

    def test_A_program_that_echoes_a_large_input_while_the_runner_writes_it_does_not_deadlock(self):
        data = os.urandom(3_000_000)
        result = Workspace(self, "cat\n").run(case(expected=data, stdin=data), limit_ms=20_000)
        self.assertEqual(result.status, "PASS")


class CaptureLimitTests(unittest.TestCase):
    """What was kept of an output is capped; a verdict never rests on the capture, and the report and the JSON say when the cap was passed."""

    def test_An_exact_output_over_the_cap_is_a_PASS_by_the_stream_and_the_report_says_the_cap_was_passed(self):
        expected = bytes(range(256)) * 16
        result = Workspace(self, REPLAY, {"answer.bin": expected}).run(case(expected=expected), capture_cap=1024)
        self.assertEqual(result.status, "PASS")
        self.assertIn("  output limit: 4096 bytes read, the first 1024 kept for the report", result.lines)
        self.assertEqual(result.lifecycle["capture"], {"known": True, "truncated": True, "keptBytes": 1024, "totalBytes": 4096})

    def test_A_wrong_output_over_the_cap_is_a_FAIL_by_byte_mismatch_with_the_same_note(self):
        expected = b"a" * 8192
        result = Workspace(self, REPLAY, {"answer.bin": b"a" * 4000 + b"b" + b"a" * 4000}).run(case(expected=expected), capture_cap=1024)
        self.assertEqual((result.status, result.reason), ("FAIL", "output differs"))
        self.assertTrue(any(l.startswith("  output limit:") for l in result.lines))
        self.assertEqual(result.lifecycle["capture"]["truncated"], True)

    def test_An_output_under_the_cap_has_no_note_and_says_it_was_not_truncated(self):
        result = Workspace(self, REPLAY, {"answer.bin": ANSWER}).run(case(), capture_cap=1024)
        self.assertEqual([l for l in result.lines if "output limit" in l], [])
        self.assertEqual(result.lifecycle["capture"], {"known": True, "truncated": False, "keptBytes": len(ANSWER), "totalBytes": len(ANSWER)})

    def test_The_JSON_record_of_a_case_carries_the_capture_beside_the_lifecycle(self):
        result = Workspace(self, REPLAY, {"answer.bin": ANSWER}).run(case())
        self.assertIn("capture", rp.case_record(result)["lifecycle"])

    def test_A_capture_taken_up_to_the_cap_is_still_not_what_decides(self):
        """The refuted approach: compare what was kept. 4096 expected a's, the program prints them then more: only the stream sees the extra."""
        result = Workspace(self, REPLAY, {"answer.bin": b"a" * 4096 + b"zzzz"}).run(case(expected=b"a" * 4096), capture_cap=1024)
        self.assertEqual((result.status, result.reason), ("FAIL", "output differs"))


class LifecycleTests(unittest.TestCase):
    def test_A_group_kill_returns_only_when_nothing_of_the_group_is_left(self):
        lifecycle = ex.PlainLifecycle()
        left = 0
        for _ in range(60):
            proc = subprocess.Popen(["bash", "-c", "sleep 61 & sleep 61"], start_new_session=True)
            time.sleep(0.005)
            lifecycle.kill(proc)
            proc.wait()
            left += lifecycle.leftover(proc)
        self.assertEqual(left, 0, "SIGKILL is delivered at once but the children are gone a moment later: kill must wait for them")


class PlainSettleTests(unittest.TestCase):
    """C4: a group signal is never sent to a leader that was already reaped (its pid may be someone else's by now)."""

    def test_A_straggler_of_the_plain_path_is_group_killed_before_the_leader_is_reaped(self):
        marker = "54.%d" % (os.getpid() % 1000 + 1000)
        seen, started = [], []
        real, real_popen = ex.signal_group, subprocess.Popen

        def popen(*args, **kwargs):
            proc = real_popen(*args, **kwargs)
            started.append(proc)
            return proc

        def recording(pid, sig):
            seen.extend(p.returncode for p in started if p.pid == pid)
            return real(pid, sig)

        with mock.patch.object(ex, "signal_group", recording), mock.patch.object(subprocess, "Popen", popen):
            result = Workspace(self, f"sleep {marker} &\ncat answer.bin\n", {"answer.bin": ANSWER}).run(case())
        self.assertEqual(result.lifecycle["leftover"], {"known": True, "found": True})
        self.assertTrue(seen and all(code is None for code in seen), f"the group signal found returncodes {seen}")
        self.assertEqual(ProcessTests.alive(self, marker), [], "and the straggler is gone")

    def test_A_clean_case_gets_no_group_signal_at_all(self):
        seen = []
        with mock.patch.object(ex, "signal_group", lambda pid, sig: seen.append(pid)):
            result = Workspace(self, "cat answer.bin\n", {"answer.bin": ANSWER}).run(case())
        self.assertEqual((result.status, result.lifecycle["leftover"], seen), ("PASS", {"known": True, "found": False}, []))


class LauncherFdTests(unittest.TestCase):
    def test_The_launchers_pidfd_is_unregistered_once_launcher_gone_has_handled_it(self):
        proc = subprocess.Popen(["true"], stdin=subprocess.PIPE, stdout=subprocess.PIPE, start_new_session=True)
        self.addCleanup(proc.wait)
        self.addCleanup(lambda: [s.close() for s in (proc.stdin, proc.stdout) if s and not s.closed])
        fd = ex.pidfd_of(proc)
        self.addCleanup(ex.close_fd, fd)
        launched = rl.Launched(proc, fd, ex.now_ns(), None, None, None, Tracker(proc, ex.PlainLifecycle(), box=False))
        pump = rs_pump.Pump(launched, b"", ro.Comparer(b"", 1024), os.devnull)
        self.addCleanup(pump.close)
        self.assertIn(fd, pump.sel.get_map())
        pump.launcher_gone()
        self.assertNotIn(fd, pump.sel.get_map(), "it would fire again at every later wake-up")
        pump.launcher_gone()  # handled twice is harmless


class PumpTests(unittest.TestCase):
    def pump(self, argv, data, expected):
        proc = subprocess.Popen(argv, stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.DEVNULL, start_new_session=True)
        self.addCleanup(lambda: [s.close() for s in (proc.stdin, proc.stdout) if s and not s.closed])
        proc.wait()
        launched = rl.Launched(proc, None, ex.now_ns(), None, None, None, Tracker(proc, ex.PlainLifecycle(), box=False))
        return proc, rs_pump.Pump(launched, data, ro.Comparer(expected, 1024), os.devnull)

    def test_Output_already_in_the_pipe_when_the_exit_is_seen_is_read_before_the_verdict(self):
        proc, pump = self.pump(["printf", "%0100d", "7"], b"", b"0" * 99 + b"7")
        self.assertEqual(pump.program_exited(), "exited")
        self.assertTrue(pump.comparer.exact, "without the drain the 100 bytes are never read")

    def test_Input_written_to_a_program_that_has_already_gone_is_dropped_quietly_not_raised(self):
        proc, pump = self.pump(["true"], b"x" * 100_000, b"")
        pump.write_input()
        self.assertEqual(len(pump.pending), 0)


class TimeTests(unittest.TestCase):
    def test_A_program_still_running_at_the_limit_is_a_time_limit_and_is_killed_with_its_group(self):
        started = time.monotonic()
        marker = "62.%d" % (os.getpid() % 1000 + 1000)
        result = Workspace(self, f"sleep {marker} & sleep {marker}\n").run(case(expected=b""), limit_ms=200, basis="registered")
        self.assertEqual((result.status, result.reason), ("FAIL", rp.time_text(result.wall_ms, 200, "registered")))
        self.assertLess(time.monotonic() - started, 5)
        self.assertEqual(ProcessTests.alive(self, marker), [], "the group was killed, children included")
        self.assertFalse(result.lifecycle["leftover"]["known"], "a group the runner killed is not a leftover finding")

    def test_Without_a_limits_file_the_limit_is_the_floor_and_the_verdict_says_so(self):
        result = Workspace(self, "sleep 2\n").run(case(expected=b""), limit_ms=ex.FLOOR_MS)
        self.assertTrue(result.reason.endswith("(against the floor)"), result.reason)
        self.assertEqual((ex.Config("w", "s").limit_ms, ex.Config("w", "s").basis), (ex.FLOOR_MS, "floor"))

    def test_A_program_that_exits_by_itself_a_little_after_the_limit_is_a_time_limit_not_a_pass(self):
        result = Workspace(self, "sleep 0.12; cat answer.bin\n", {"answer.bin": ANSWER}).run(case(), limit_ms=100)
        self.assertEqual(result.status, "FAIL")
        self.assertTrue(result.reason.startswith("time limit"), result.reason)

    def test_A_program_well_inside_the_limit_passes_and_records_its_wall_time(self):
        result = Workspace(self, REPLAY, {"answer.bin": ANSWER}).run(case(), limit_ms=20_000)
        self.assertEqual(result.status, "PASS")
        self.assertTrue(0 < result.wall_ms < 20_000)


class ProcessTests(unittest.TestCase):
    def test_A_launch_prefix_goes_before_bash_run_sh_with_the_case_filled_in(self):
        result = Workspace(self, 'printf "%s\\n" "$CASE_ID"\n').run(case(expected=b"s07\n", ident="s07"), prefix=["env", "CASE_ID={case}"])
        self.assertEqual(result.status, "PASS")

    def test_A_background_child_that_outlives_the_program_is_found_and_killed(self):
        result = Workspace(self, "sleep 60 &\ncat answer.bin\n", {"answer.bin": ANSWER}).run(case(), limit_ms=20_000)
        self.assertEqual(result.status, "PASS", "the verdict is about the output and the status")
        self.assertEqual(result.lifecycle["leftover"], {"known": True, "found": True})

    def test_A_leftover_is_reported_as_a_note_line_and_the_PASS_stays(self):
        marker = "63.%d" % (os.getpid() % 1000 + 1000)
        result = Workspace(self, f"sleep {marker} &\ncat answer.bin\n", {"answer.bin": ANSWER}).run(case(), limit_ms=20_000)
        self.assertEqual(result.status, "PASS")
        notes = [l for l in result.lines if l.lstrip().startswith("leftover:")]
        self.assertEqual(len(notes), 1, result.lines)
        self.assertIn("still running after the exit", notes[0])
        self.assertIn("leftover: ", "\n".join(rp.render_case(result)))

    def test_A_clean_exit_prints_no_leftover_line(self):
        result = Workspace(self, REPLAY, {"answer.bin": ANSWER}).run(case())
        self.assertEqual([l for l in result.lines if "leftover" in l], [])

    def test_A_failing_case_with_a_leftover_reports_both(self):
        marker = "64.%d" % (os.getpid() % 1000 + 1000)
        result = Workspace(self, f"sleep {marker} &\nexit 3\n").run(case())
        self.assertEqual(result.status, "FAIL")
        self.assertTrue(any(l.lstrip().startswith("leftover:") for l in result.lines))

    def test_A_leftover_child_does_not_survive_the_case(self):
        marker = "61.%d" % (os.getpid() % 1000 + 1000)
        Workspace(self, f"sleep {marker} &\ncat answer.bin\n", {"answer.bin": ANSWER}).run(case(), limit_ms=20_000)
        self.assertEqual(self.alive(marker), [], "the sleeper outlived its case")

    def alive(self, marker):
        found = []
        for pid in filter(str.isdigit, os.listdir("/proc")):
            try:
                with open(f"/proc/{pid}/cmdline", "rb") as f:
                    found += [pid] if marker.encode() in f.read() else []
            except OSError:
                continue
        return found

    def test_The_build_runs_bash_build_sh_in_the_workspace_and_reports_its_status(self):
        ws = Workspace(self, "", {"build.sh": "echo built > built.txt\n"})
        done = ex.build(ws.dir, ws.scratch)
        self.assertEqual((done.ran, done.status, done.timed_out), (True, 0, False))
        self.assertTrue(os.path.exists(os.path.join(ws.dir, "built.txt")))

    def test_A_failing_build_is_reported_with_the_tail_of_its_output(self):
        ws = Workspace(self, "", {"build.sh": "echo oops >&2; exit 4\n"})
        done = ex.build(ws.dir, ws.scratch)
        self.assertEqual((done.status, done.timed_out), (4, False))
        self.assertIn("oops", done.tail)

    def test_A_build_that_outlasts_its_ceiling_is_killed_with_its_group(self):
        ws = Workspace(self, "", {"build.sh": "sleep 30 & sleep 30\n"})
        started = time.monotonic()
        done = ex.build(ws.dir, ws.scratch, ceiling_ms=300)
        self.assertTrue(done.timed_out)
        self.assertLess(time.monotonic() - started, 5)

    def build_with_recorded_pidfd(self, ws, wait=None, ceiling_ms=60_000):
        """Runs ex.build with the pidfd it opens recorded; returns (the fds opened, the error the build raised or None)."""
        opened, real = [], ex.pidfd_of

        def recording(proc):
            fd = real(proc)
            opened.append(fd)
            self.build_pgid = proc.pid
            return fd

        saved = (ex.pidfd_of, ex._exited)
        ex.pidfd_of = recording
        if wait is not None:
            ex._exited = wait
        try:
            ex.build(ws.dir, ws.scratch, ceiling_ms=ceiling_ms)
            return opened, None
        except BaseException as e:  # noqa: B902  (the point: even a BaseException)
            return opened, e
        finally:
            ex.pidfd_of, ex._exited = saved

    def is_closed(self, fd):
        try:
            os.fstat(fd)
            return False
        except OSError:
            return True

    def test_The_pidfd_of_a_build_is_closed_when_it_ends_normally_when_it_times_out_and_when_it_is_interrupted(self):
        def stopped(proc, pidfd, deadline):
            raise KeyboardInterrupt()

        for name, build_sh, wait, ceiling in (("normal", ":\n", None, 60_000), ("timeout", "sleep 61\n", None, 200), ("interrupted", "sleep 61\n", stopped, 60_000)):
            opened, error = self.build_with_recorded_pidfd(Workspace(self, "", {"build.sh": build_sh}), wait, ceiling)
            self.assertEqual(len(opened), 1, name)
            self.assertTrue(self.is_closed(opened[0]), f"the build's pidfd was left open ({name})")
            self.assertEqual(isinstance(error, KeyboardInterrupt), name == "interrupted", name)

    def test_A_build_that_times_out_or_is_interrupted_is_stopped_through_the_group_kill_that_waits_until_the_group_is_gone(self):
        def stopped(proc, pidfd, deadline):
            raise KeyboardInterrupt()

        for name, wait, ceiling in (("timeout", None, 150), ("interrupted", stopped, 60_000)):
            calls = []
            real = ex.kill_group
            ex.kill_group = lambda proc: (calls.append(proc.pid), real(proc))
            try:
                ws = Workspace(self, "", {"build.sh": "sleep 61 &\nsleep 61 &\nwait\n"})
                self.build_with_recorded_pidfd(ws, wait, ceiling)
            finally:
                ex.kill_group = real
            self.assertEqual(calls, [self.build_pgid], f"the {name} build was not stopped through kill_group")
            self.assertFalse(self.group_exists(self.build_pgid), f"the {name} build's group was not gone when it returned")

    def test_The_group_kill_returns_only_when_nothing_of_the_group_is_left(self):
        left = 0
        for _ in range(60):
            proc = subprocess.Popen(["bash", "-c", "sleep 61 & sleep 61"], start_new_session=True)
            time.sleep(0.005)
            ex.kill_group(proc)
            left += self.group_exists(proc.pid)
        self.assertEqual(left, 0)

    def group_exists(self, pgid):
        try:
            os.killpg(pgid, 0)
        except ProcessLookupError:
            return False
        return True

    def test_Without_a_pidfd_the_wait_polls_every_tick_and_not_every_five_milliseconds(self):
        slept = []

        class Alive:
            def poll(self):
                return None

        real = ex.time.sleep
        ex.time.sleep = slept.append
        try:
            self.assertFalse(ex._exited(Alive(), None, 1000))
            self.assertFalse(ex._exited(Alive(), None, 20))
        finally:
            ex.time.sleep = real
        self.assertEqual(slept, [ex.TICK_S, 0.02], "a full tick, or what is left of the wait when that is less")
        self.assertEqual(ex.TICK_S, 0.05)

    def test_Without_a_pidfd_a_program_that_exits_is_still_noticed_within_a_tick(self):
        proc = subprocess.Popen(["bash", "-c", "exit 3"])
        started = time.monotonic()
        self.assertTrue(ex.wait_exit(proc, None, ex.now_ms() + 5000))
        self.assertLess(time.monotonic() - started, 1.0)
        self.assertEqual(proc.returncode, 3)

    def test_A_case_id_with_anything_but_letters_digits_underscore_and_hyphen_is_refused_before_it_reaches_an_argv(self):
        for bad in ("", "a b", "a;b", "$(touch x)", "a/b", "..", "a\nb", "s01\u00e9", "a\x00b", "x'y", '"q"'):
            with self.assertRaises(ValueError, msg=repr(bad)):
                ex.launch_argv(["env", "X={case}"], bad)
        for good in ("s01", "h001", "a_b-c", "S1"):
            self.assertEqual(ex.launch_argv(["env", "X={case}"], good), ["env", f"X={good}", "bash", "run.sh"])

    def test_A_case_id_is_checked_even_when_no_prefix_uses_it(self):
        with self.assertRaises(ValueError):
            ex.launch_argv(None, "a;b")

    def test_The_launch_argv_fills_the_case_into_every_prefix_element(self):
        self.assertEqual(ex.launch_argv(["a", "{case}-x", "{case}"], "s1"), ["a", "s1-x", "s1", "bash", "run.sh"])
        self.assertEqual(ex.launch_argv(None, "s1"), ["bash", "run.sh"])

    def test_A_workspace_without_run_sh_fails_the_case_with_bashs_status(self):
        ws = Workspace(self, "")
        os.remove(os.path.join(ws.dir, "run.sh"))
        result = ws.run(case(expected=b""))
        self.assertEqual((result.status, result.reason), ("FAIL", "exit status 127"))


if __name__ == "__main__":
    unittest.main()
