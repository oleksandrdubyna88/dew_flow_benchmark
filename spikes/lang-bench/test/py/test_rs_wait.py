"""Every wait is `os.pidfd_open` + `select` with the exact remaining time, and the wall rule decides (E1.S8a part ii-a; B6, the pilot's F1)."""
import os
import time
import unittest
from unittest import mock

import runner_path  # noqa: F401
import rs_exec as ex
import rs_olympiad as ro
import rs_report as rp
from box_support import ANSWER, Workspace, case

REPLAY = "cat answer.bin\n"
FILES = {"answer.bin": ANSWER}
LIMIT_MS = 110
PAST_THE_LIMIT_MS = 20


def polling_accepts(limit_ms, exit_ms, tick_ms=50):
    """The refuted wait: look at the program every `tick_ms` (what a poll does), and take 'it has exited' as 'it was in time'. The ticks
    here are 0, 50, 100, 150 ms: an exit at 135 ms is first seen at 150 ms, after the 110 ms limit, and is accepted."""
    at = 0
    while True:
        if exit_ms <= at:
            return True
        if at >= limit_ms:
            return False
        at += tick_ms


def outcome(end, wall_ms):
    return ro.Outcome(end, wall_ms, 0, False, rp.NOT_CERTIFIED, None, {})


class WaitTests(unittest.TestCase):
    def test_A_polling_wait_would_accept_an_exit_fifty_ms_past_the_limit_and_that_is_the_point(self):
        exit_at = LIMIT_MS + PAST_THE_LIMIT_MS + 5  # the stub's sleep, plus what bash needs to start
        self.assertTrue(polling_accepts(LIMIT_MS, exit_at), "the refuted wait calls a 135 ms exit in time against a 110 ms limit")
        stub = f"sleep 0.{LIMIT_MS + PAST_THE_LIMIT_MS:03d}\ncat answer.bin\n"
        result = Workspace(self, stub, FILES).run(case(), limit_ms=LIMIT_MS)
        self.assertEqual(result.status, "FAIL", "the exact wait ends the case at the limit; the stub that needs 20 ms more is not a pass")
        self.assertTrue(result.reason.startswith("time limit"), result.reason)

    def test_The_wait_ends_at_the_limit_and_not_a_tick_later(self):
        started = time.monotonic()
        result = Workspace(self, "sleep 20\n").run(case(), limit_ms=300)
        elapsed_ms = (time.monotonic() - started) * 1000
        self.assertTrue(result.reason.startswith("time limit"))
        self.assertLess(result.wall_ms - 300, 40, f"the wall of a program stopped at its limit is the limit, not {result.wall_ms} ms")
        self.assertLess(elapsed_ms, 2500)

    def test_An_exit_the_runner_notices_after_the_limit_is_a_time_limit_by_the_wall_rule(self):
        original = ro._Pump.step

        def late(pump, timeout_s):
            time.sleep(0.16)  # the runner is late by a whole tick: the stub exited 20 ms past the limit and is found exited
            return original(pump, timeout_s)

        stub = f"sleep 0.{LIMIT_MS + PAST_THE_LIMIT_MS:03d}\ncat answer.bin\n"
        with mock.patch.object(ro._Pump, "step", late):
            result = Workspace(self, stub, FILES).run(case(), limit_ms=LIMIT_MS)
        self.assertEqual(result.status, "FAIL", "the program exited by itself and its output is exact, but its wall is over the limit")
        self.assertTrue(result.reason.startswith("time limit"), result.reason)
        self.assertGreater(result.wall_ms, LIMIT_MS)

    def test_The_wall_rule_judges_a_wall_over_the_limit_whatever_ended_the_wait(self):
        config = ex.Config("w", "s", limit_ms=LIMIT_MS)
        over = ro._over_time(case(), config, outcome("exited", LIMIT_MS + PAST_THE_LIMIT_MS), None)
        self.assertIsNotNone(over, "a wall of 130 ms against a 110 ms limit")
        self.assertTrue(over[0].startswith("time limit: 0.130 s over 0.110 s"))
        self.assertIsNone(ro._over_time(case(), config, outcome("exited", LIMIT_MS), None), "exactly the limit is not over it")
        self.assertIsNotNone(ro._over_time(case(), config, outcome("timeout", 1), None))

    def test_A_launcher_that_outlives_the_program_does_not_stretch_the_wall(self):
        ws = Workspace(self, "sleep 0.05\ncat answer.bin\n", FILES)
        cgroup = ws.make_dir("cg")
        with open(os.path.join(cgroup, "cgroup.procs"), "w"):
            pass
        # the launcher: the program is a subshell that writes its own pid to cgroup.procs and execs run.sh; the launcher then lingers 0.5 s
        prefix = ["bash", "-c", f'( echo $BASHPID > "{cgroup}/cgroup.procs"; exec "$@" )\nsleep 0.5', "bash"]
        started = time.monotonic()
        result = ws.run(case(), prefix=prefix, cgroup=cgroup, limit_ms=400)
        self.assertEqual((result.status, result.reason), ("PASS", ""), f"a 50 ms program under a 400 ms limit; wall {result.wall_ms} ms")
        self.assertLess(result.wall_ms, 300, "the wall is the program's own, awaited on the program's pid, not the launcher's 500 ms")
        self.assertGreaterEqual((time.monotonic() - started) * 1000, 500, "the launcher did linger: the test would otherwise prove nothing")
        self.assertEqual(result.lifecycle["signalled"]["found"], "cgroup.procs minus bwrap and wrapper")
        self.assertIsNotNone(result.lifecycle["signalled"]["pid"])

    def test_Every_wait_in_the_runner_is_a_pidfd_and_a_select_and_never_Popen_wait_with_a_timeout(self):
        import ast
        import shape_scan as scan
        offenders = []
        for path in scan.runner_files():
            for node in ast.walk(scan.parse(path)):
                if isinstance(node, ast.Call) and isinstance(node.func, ast.Attribute) and node.func.attr == "wait" and (node.args or node.keywords):
                    offenders.append(f"{os.path.basename(path)}:{node.lineno}")
        self.assertEqual(offenders, [], "a .wait(...) with an argument is a timeout wait")

    def test_The_scan_for_a_timeout_wait_still_finds_one(self):
        import ast
        tree = ast.parse("proc.wait(timeout=1)\nproc.wait()\n")
        hits = [n.lineno for n in ast.walk(tree) if isinstance(n, ast.Call) and isinstance(n.func, ast.Attribute) and n.func.attr == "wait" and (n.args or n.keywords)]
        self.assertEqual(hits, [1])


if __name__ == "__main__":
    unittest.main()
