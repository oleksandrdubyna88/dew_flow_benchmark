"""Every wait is `os.pidfd_open` + `select` with the exact remaining time, and the wall rule decides on one clock (E1.S8a part ii-a; B6, P3, the
pilot's F1). The margins here are wide on purpose: the machine may be loaded by other runs."""
import ast
import os
import time
import unittest
from unittest import mock

import runner_path  # noqa: F401
import rs_exec as ex
import rs_olympiad as ro
import rs_pump
import rs_report as rp
from box_support import ANSWER, Workspace, box_prefix, case, fake_box, fake_cgroup, reporting_prefix

REPLAY = "cat answer.bin\n"
FILES = {"answer.bin": ANSWER}
LIMIT_MS = 310
PAST_THE_LIMIT_MS = 20
STUB = f"sleep 0.{LIMIT_MS + PAST_THE_LIMIT_MS:03d}\ncat answer.bin\n"
NS = ex.NS_PER_MS


def polling_accepts(limit_ms, exit_ms, tick_ms=50):
    """The refuted wait: look at the program every `tick_ms` (what a poll does), and take 'it has exited' as 'it was in time'. The ticks
    here are 0, 50, ... 300, 350 ms: an exit at 335 ms is first seen at 350 ms, after the 310 ms limit, and is accepted."""
    at = 0
    while True:
        if exit_ms <= at:
            return True
        if at >= limit_ms:
            return False
        at += tick_ms


def outcome(end, wall_ns):
    return ro.Outcome(end, wall_ns, 0, False, rp.NOT_CERTIFIED, None, {})


class WaitTests(unittest.TestCase):
    def test_A_polling_wait_would_accept_an_exit_fifty_ms_past_the_limit_and_that_is_the_point(self):
        self.assertTrue(polling_accepts(LIMIT_MS, LIMIT_MS + PAST_THE_LIMIT_MS + 5), "the refuted wait calls a 335 ms exit in time against 310 ms")
        result = Workspace(self, STUB, FILES).run(case(), limit_ms=LIMIT_MS)
        self.assertEqual((result.status, result.reason[:10]), ("FAIL", "time limit"), "the stub needs 20 ms more than the limit: no pass")
        original = rs_pump.Pump.step

        def late(pump, timeout_s):
            time.sleep((LIMIT_MS + 100) / 1000)  # the runner wakes a whole tick late: the stub is found already exited
            return original(pump, timeout_s)

        with mock.patch.object(rs_pump.Pump, "step", late):
            result = Workspace(self, STUB, FILES).run(case(), limit_ms=LIMIT_MS)
        self.assertEqual((result.status, result.reason[:10]), ("FAIL", "time limit"), "exited by itself, exact output, wall over the limit")
        self.assertGreater(result.wall_ms, LIMIT_MS)

    def test_The_wait_ends_at_the_limit_and_not_when_the_program_chooses(self):
        started = time.monotonic()
        result = Workspace(self, "sleep 60\n").run(case(), limit_ms=300)
        self.assertTrue(result.reason.startswith("time limit"))
        self.assertGreaterEqual(result.wall_ms, 300)
        self.assertLess(time.monotonic() - started, 30, "ended at the limit, not when the 60 s sleep ended")

    def test_The_wall_rule_is_exact_in_nanoseconds_equal_to_the_limit_passes_and_one_millisecond_over_is_a_time_limit(self):
        config = ex.Config("w", "s", limit_ms=LIMIT_MS)
        self.assertIsNone(ro._over_time(case(), config, outcome("exited", LIMIT_MS * NS), None), "exactly the limit is not over it")
        self.assertIsNone(ro._over_time(case(), config, outcome("exited", LIMIT_MS * NS - 1), None))
        over = ro._over_time(case(), config, outcome("exited", (LIMIT_MS + 1) * NS), None)
        self.assertIsNotNone(over, "limit + 1 ms")
        self.assertEqual(over[0], "time limit: 0.311 s over 0.310 s (against the floor)")
        self.assertIsNotNone(ro._over_time(case(), config, outcome("exited", LIMIT_MS * NS + 1), None), "one nanosecond over is over")
        self.assertIsNotNone(ro._over_time(case(), config, outcome("timeout", LIMIT_MS * NS + 5), None))
        self.assertIsNone(ro._over_time(case(), config, outcome("exited", None), None), "an unknown wall cannot be over the limit")

    def test_An_end_instant_injected_into_a_real_run_passes_at_the_limit_and_fails_one_millisecond_after(self):
        ws = Workspace(self, REPLAY, FILES)
        for offset_ms, status in ((LIMIT_MS, "PASS"), (LIMIT_MS + 1, "FAIL")):
            def end_at(pump, offset_ms=offset_ms):
                pump.exit_ns = pump.launched.clock_ns + offset_ms * NS

            with mock.patch.object(rs_pump.Pump, "_ended_now", end_at):
                result = ws.run(case(), limit_ms=LIMIT_MS)
            self.assertEqual(result.status, status, f"the program ended {offset_ms} ms after the clock start, the limit is {LIMIT_MS} ms")
            self.assertEqual(result.wall_ms, offset_ms)

    def test_The_launchers_teardown_after_the_exit_code_record_is_not_program_time(self):
        started = time.monotonic()
        result = Workspace(self, "sleep 0.05\ncat answer.bin\n", FILES).run(case(), prefix=reporting_prefix(linger=3), status_fd=7, limit_ms=2000)
        self.assertEqual((result.status, result.reason), ("PASS", ""), f"a 50 ms program under a 2 s limit; wall {result.wall_ms} ms")
        self.assertLess(result.wall_ms, 2000, "the end is the exit-code record, not the launcher's exit 3 s later")
        self.assertGreaterEqual(time.monotonic() - started, 3, "the launcher did linger: the test would otherwise prove nothing")

    def test_A_launcher_that_outlives_the_program_does_not_stretch_the_wall_when_the_pid_is_found(self):
        ws = Workspace(self, "sleep 0.05\ncat answer.bin\n", FILES)
        cgroup = fake_cgroup(ws)
        started = time.monotonic()
        result = ws.run(case(), prefix=box_prefix(fake_box(self), cgroup, linger=1), status_fd=7, cgroup=cgroup, limit_ms=500)
        self.assertEqual((result.status, result.reason), ("PASS", ""), f"a 50 ms program under a 500 ms limit; wall {result.wall_ms} ms")
        self.assertLess(result.wall_ms, 500, "the end is the program's pidfd, not the launcher's exit a second later")
        self.assertGreaterEqual(time.monotonic() - started, 1, "the launcher did linger: the test would otherwise prove nothing")
        self.assertEqual(result.lifecycle["signalled"]["found"], "cgroup.procs: the process whose parent is a bwrap process")

    def test_A_case_with_neither_a_pid_nor_an_exit_code_record_has_no_wall_and_is_judged_by_its_output(self):
        silent = ["sh", "-c", 'printf \'{"child-pid": 5}\\n\' >&7\n"$@"', "sh"]
        result = Workspace(self, REPLAY, FILES).run(case(), prefix=silent, status_fd=7)
        self.assertEqual((result.status, result.wall_ms), ("PASS", None))
        self.assertEqual(rp.case_record(result)["wallMs"]["known"], False)

    def test_Every_wait_in_the_runner_is_a_pidfd_and_a_select_and_never_Popen_wait_with_a_timeout(self):
        import shape_scan as scan
        offenders = []
        for path in scan.runner_files():
            for node in ast.walk(scan.parse(path)):
                if isinstance(node, ast.Call) and isinstance(node.func, ast.Attribute) and node.func.attr == "wait" and (node.args or node.keywords):
                    offenders.append(f"{os.path.basename(path)}:{node.lineno}")
        self.assertEqual(offenders, [], "a .wait(...) with an argument is a timeout wait")

    def test_The_scan_for_a_timeout_wait_still_finds_one(self):
        tree = ast.parse("proc.wait(timeout=1)\nproc.wait()\n")
        hits = [n.lineno for n in ast.walk(tree) if isinstance(n, ast.Call) and isinstance(n.func, ast.Attribute) and n.func.attr == "wait" and (n.args or n.keywords)]
        self.assertEqual(hits, [1])


if __name__ == "__main__":
    unittest.main()
