"""The olympiad path: one run of `bash run.sh` per case, stdout compared as a stream against the expected bytes (B9), stderr to a file,
wall time judged by the rule that a wall over the limit is a time limit even when the program exited by itself."""
import os
import subprocess
import tempfile
import time
import unittest

import runner_path  # noqa: F401
import rs_exec as ex
import rs_olympiad as ro
import rs_report as rp

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
        return ro.run_case(case, ex.Config(workspace=self.dir, scratch=self.scratch, **overrides))


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


class PumpTests(unittest.TestCase):
    def pump(self, argv, data, expected):
        proc = subprocess.Popen(argv, stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.DEVNULL, start_new_session=True)
        self.addCleanup(lambda: [s.close() for s in (proc.stdin, proc.stdout) if s and not s.closed])
        proc.wait()
        return proc, ro._Pump(proc, None, data, ro.Comparer(expected, 1024), os.devnull)

    def test_Output_already_in_the_pipe_when_the_exit_is_seen_is_read_before_the_verdict(self):
        proc, pump = self.pump(["printf", "%0100d", "7"], b"", b"0" * 99 + b"7")
        self.assertEqual(pump.exited(), "exited")
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
        result = Workspace(self, "sleep 2\n").run(case(expected=b""))
        self.assertTrue(result.reason.endswith("(against the floor)"), result.reason)

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
