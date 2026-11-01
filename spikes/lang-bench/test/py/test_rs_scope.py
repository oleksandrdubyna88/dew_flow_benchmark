"""A case whose program runs in a cgroup the layer made (E1.S8a part ii-a; B1, B4): the memory read, the leftover evidence, the program's pid,
and the kill through `cgroup.kill`, always. The real-scope tests need `systemd-run --user --scope` and bwrap and skip with their reason without
them (they fail under BENCH_REQUIRE_SANDBOX=1)."""
import os
import signal
import subprocess
import unittest
from unittest import mock

import runner_path  # noqa: F401
import rs_cgroup as cg
import rs_exec as ex
import rs_launch as rl
import rs_scope
from box_support import ANSWER, STATUS_FD, Scope, Workspace, box_prefix, case, fake_box, fake_cgroup, reporting_prefix, wait_until

REPLAY = "cat answer.bin\n"
FILES = {"answer.bin": ANSWER}
OOM_EVENTS = "low 0\nhigh 0\nmax 4\noom 1\noom_kill 1\noom_group_kill 0\n"
CLEAN_EVENTS = "oom 0\noom_kill 0\n"


def comm(pid):
    try:
        with open(f"/proc/{pid}/comm") as f:
            return f.read().strip()
    except OSError:
        return None


def alive(pid):
    return comm(pid) is not None and not _zombie(pid)


def _zombie(pid):
    try:
        with open(f"/proc/{pid}/stat") as f:
            return f.read().rsplit(")", 1)[1].split()[0] == "Z"
    except OSError:
        return True


def cmdline_alive(seconds):
    found = []
    for pid in filter(str.isdigit, os.listdir("/proc")):
        try:
            with open(f"/proc/{pid}/cmdline", "rb") as f:
                found += [pid] if f"sleep\0{seconds}".encode() in f.read() else []
        except OSError:
            continue
    return found


def run_in_scope(test, run_sh, files=None, expected=b"", **overrides):
    """One case through a real scope and bwrap, with the status descriptor: (result, scope, workspace)."""
    scope = Scope(test)
    ws = Workspace(test, run_sh, files or FILES)
    config = {"prefix": scope.prefix(ws.dir, status_fd=STATUS_FD), "status_fd": STATUS_FD, "cgroup": scope.cgroup, **overrides}
    return ws.run(case(expected=expected), **config), scope, ws


class KillTests(unittest.TestCase):
    def test_A_group_kill_would_miss_a_program_behind_new_session_and_that_is_the_point(self):
        # the refuted approach, on its own scope: the launcher's group is killed, the program behind --new-session is not
        refuted = Scope(self)
        ws = Workspace(self, "exec sleep 60\n")
        box = subprocess.Popen([*refuted.prefix(ws.dir), "bash", "run.sh"], start_new_session=True, stdin=subprocess.DEVNULL, stdout=subprocess.DEVNULL)
        self.addCleanup(box.wait)
        self.assertTrue(wait_until(lambda: any(comm(p) == "sleep" for p in refuted.pids())), "the program is running inside the scope")
        program = next(p for p in refuted.pids() if comm(p) == "sleep")
        os.killpg(box.pid, signal.SIGKILL)
        box.wait()
        self.assertTrue(alive(program), "killpg of the launcher's group did not reach the program: it is behind a session of its own")
        cg.kill(refuted.cgroup)
        self.assertTrue(wait_until(lambda: not alive(program)), "cgroup.kill reaches it")
        # the runner's way, on a scope of its own: the kill at the time limit is cgroup.kill and nothing is left
        result, scope, _ = run_in_scope(self, "exec sleep 60\n", limit_ms=500)
        self.assertTrue(result.reason.startswith("time limit"), result.reason)
        self.assertEqual(result.lifecycle["kill"], "cgroup.kill")
        self.assertTrue(wait_until(lambda: scope.pids() == []), f"nothing of the case may be left in its scope: {scope.pids()}")

    def test_The_program_pid_is_the_child_of_bwrap_and_not_the_launcher_and_its_comm_is_the_programs(self):
        for run_sh, expected_comm in ((REPLAY + "sleep 0.3\n", "bash"), ("exec sleep 60\n", "sleep")):
            result, scope, _ = run_in_scope(self, run_sh, limit_ms=1500, expected=ANSWER if run_sh.startswith("cat") else b"")
            signalled = result.lifecycle["signalled"]
            self.assertEqual(signalled["found"], "cgroup.procs: the process whose parent is a bwrap process", run_sh)
            self.assertIsInstance(signalled["pid"], int)
            self.assertNotEqual(signalled["pid"], signalled["launcher"], "the launcher is never the program")
            self.assertEqual(signalled["comm"], expected_comm, f"{run_sh!r}: the program's own name as last seen alive")

    def test_A_child_that_outlives_the_program_is_the_leftover_evidence_and_is_killed_through_the_scope(self):
        result, scope, _ = run_in_scope(self, "sleep 60 &\ncat answer.bin\n", expected=ANSWER)
        self.assertEqual(result.status, "PASS", "a leftover is a finding, the verdict stays with the output")
        self.assertEqual(result.lifecycle["leftover"], {"known": True, "found": True})
        self.assertIn("  leftover: process still running after the exit (killed)", result.lines)
        self.assertTrue(wait_until(lambda: scope.pids() == []), f"left in the scope: {scope.pids()}")

    def test_A_hidden_child_that_is_a_copy_of_bash_named_bwrap_is_a_leftover_and_is_killed(self):
        script = "cp /usr/bin/bash ./bwrap\n./bwrap -c 'sleep 60; true' &\ncat answer.bin\n"
        result, scope, _ = run_in_scope(self, script, expected=ANSWER)
        self.assertEqual(result.lifecycle["leftover"], {"known": True, "found": True}, "a name is no identity: the copy is not bwrap and hides nothing")
        self.assertIn("  leftover: process still running after the exit (killed)", result.lines)
        self.assertTrue(wait_until(lambda: scope.pids() == []), f"left in the scope: {scope.pids()}")

    def test_A_clean_program_leaves_no_leftover_and_the_scope_is_empty(self):
        result, scope, _ = run_in_scope(self, REPLAY, expected=ANSWER)
        self.assertEqual(result.lifecycle["leftover"], {"known": True, "found": False})
        self.assertNotIn("leftover", " ".join(result.lines))
        self.assertTrue(wait_until(lambda: scope.pids() == []))

    def test_The_peak_is_read_from_the_scope_before_any_kill(self):
        result, scope, _ = run_in_scope(self, "sleep 60\n", limit_ms=500)
        memory = result.lifecycle["memory"]
        self.assertEqual(memory["known"], True, memory)
        self.assertGreater(memory["peakBytes"], 0, "read while the program's scope was still alive")
        self.assertIn(f"  memory: peak {memory['peakBytes']} bytes", result.lines)


class ForkTests(unittest.TestCase):
    """`fork` is observed over the whole case and never decides the pid."""

    def test_A_run_sh_that_does_not_exec_and_runs_a_tool_for_a_while_is_fork(self):
        result, _, _ = run_in_scope(self, "sleep 0.5\ncat answer.bin\n", expected=ANSWER, limit_ms=5000)
        self.assertEqual(result.status, "PASS")
        self.assertEqual(result.lifecycle["fork"], {"known": True, "found": True, "count": 2})
        self.assertEqual(result.lifecycle["signalled"]["comm"], "bash", "the pid is still bash's: fork never blocks the pid choice")
        self.assertIn("  fork: 2 processes in the program's cgroup where one was expected", result.lines)

    def test_A_run_sh_that_starts_a_short_tool_and_then_execs_is_not_fork(self):
        result, _, _ = run_in_scope(self, "true\n( true )\nexec cat answer.bin\n", expected=ANSWER, limit_ms=5000)
        self.assertEqual(result.status, "PASS")
        self.assertEqual(result.lifecycle["fork"], {"known": True, "found": False, "count": 1})
        self.assertIn(result.lifecycle["signalled"]["comm"], ("bash", "cat"), "bash before the exec, cat after")

    def test_A_program_that_exits_at_once_is_judged_by_its_output_and_is_not_fork_and_its_wall_is_not_stretched(self):
        result, _, _ = run_in_scope(self, "exec cat answer.bin\n", expected=ANSWER, limit_ms=5000)
        self.assertEqual((result.status, result.reason), ("PASS", ""))
        self.assertIn(result.lifecycle["fork"].get("found"), (False, None), "a program too quick to look at is unknown or clean, never fork")
        self.assertLess(result.wall_ms, 4000)


class FakeCgroupTests(unittest.TestCase):
    def test_An_oom_kill_in_memory_events_is_memory_limit_whatever_the_exit_status(self):
        ws = Workspace(self, REPLAY, FILES)
        cgroup = fake_cgroup(ws, files={"memory.peak": "7340032", "memory.events": OOM_EVENTS})
        result = ws.run(case(), cgroup=cgroup)
        self.assertEqual((result.status, result.reason), ("FAIL", "memory limit"), "the program exited 0 with the exact output: still not a pass")
        self.assertIn("  memory.events: oom_kill 1", result.lines)
        self.assertEqual(result.lifecycle["memory"]["peakBytes"], 7340032)

    def test_An_oom_kill_is_judged_even_when_memory_peak_cannot_be_read(self):
        ws = Workspace(self, REPLAY, FILES)
        result = ws.run(case(), cgroup=fake_cgroup(ws, files={"memory.events": OOM_EVENTS}))
        self.assertEqual((result.status, result.reason), ("FAIL", "memory limit"))
        self.assertEqual(result.lifecycle["memory"]["known"], False)

    def test_memory_limit_comes_before_time_limit_when_both_are_true(self):
        ws = Workspace(self, "sleep 5\n")
        result = ws.run(case(expected=b""), cgroup=fake_cgroup(ws, files={"memory.peak": "9", "memory.events": OOM_EVENTS}), limit_ms=300)
        self.assertEqual(result.reason, "memory limit", "the kernel killed it for memory: that is what the case failed on")

    def test_A_cgroup_without_an_oom_kill_does_not_change_a_pass_and_its_peak_is_kept(self):
        ws = Workspace(self, REPLAY, FILES)
        result = ws.run(case(), cgroup=fake_cgroup(ws, files={"memory.peak": "1048576", "memory.events": CLEAN_EVENTS}))
        self.assertEqual(result.status, "PASS")
        self.assertEqual(result.lifecycle["memory"], {"known": True, "peakBytes": 1048576, "events": {"oom": 0, "oom_kill": 0}, "oomKill": 0})
        self.assertIn("  memory: peak 1048576 bytes", result.lines)

    def test_The_cgroup_template_is_filled_with_the_case_id(self):
        ws = Workspace(self, REPLAY, FILES)
        for ident in ("s01", "s02"):
            fake_cgroup(ws, f"cg-{ident}", {"memory.peak": ident[1:], "memory.events": CLEAN_EVENTS})
        peaks = [ws.run(case(ident=ident), cgroup=os.path.join(ws.root, "cg-{case}")).lifecycle["memory"]["peakBytes"] for ident in ("s01", "s02")]
        self.assertEqual(peaks, [1, 2])

    def test_A_cgroup_that_does_not_exist_says_unknown_and_never_zero_on_the_plain_path(self):
        result = Workspace(self, REPLAY, FILES).run(case(), cgroup="/no/such/cgroup")
        self.assertEqual(result.status, "PASS")
        self.assertEqual(result.lifecycle["memory"]["known"], False)
        self.assertEqual(result.lifecycle["leftover"]["known"], False)
        self.assertIn("  memory: not certified", result.lines)

    def test_The_kill_is_written_after_every_case_whether_or_not_a_leftover_was_found(self):
        ws = Workspace(self, REPLAY, FILES)
        cgroup = fake_cgroup(ws, files={"memory.peak": "1", "memory.events": CLEAN_EVENTS})
        result = ws.run(case(), cgroup=cgroup)
        self.assertEqual(result.lifecycle["leftover"], {"known": True, "found": False})
        with open(os.path.join(cgroup, "cgroup.kill"), "rb") as f:
            self.assertEqual(f.read(), b"1", "cgroup.kill is written even for a clean case: a child no read could see goes with it")

    def test_A_cgroup_that_does_not_exist_gets_the_launchers_group_killed_at_the_time_limit(self):
        seconds = "47.%d" % (os.getpid() % 1000 + 1000)
        result = Workspace(self, f"sleep {seconds} & sleep {seconds}\n").run(case(expected=b""), cgroup="/no/such/cgroup", limit_ms=300)
        self.assertTrue(result.reason.startswith("time limit"), result.reason)
        self.assertTrue(wait_until(lambda: cmdline_alive(seconds) == []), f"left alive: {cmdline_alive(seconds)}")

    def test_A_cgroup_kill_that_cannot_be_written_kills_the_launchers_group_too_and_is_still_a_cleanup_failure(self):
        seconds = "48.%d" % (os.getpid() % 1000 + 1000)
        ws = Workspace(self, f"sleep {seconds}\n")
        cgroup = fake_cgroup(ws, files={"memory.peak": "1", "memory.events": CLEAN_EVENTS})
        os.mkdir(os.path.join(cgroup, "cgroup.kill"))  # a directory: the write fails
        with self.assertRaises(ex.CleanupFailed):
            ws.run(case(expected=b""), cgroup=cgroup, limit_ms=300)
        self.assertTrue(wait_until(lambda: cmdline_alive(seconds) == []), f"left alive: {cmdline_alive(seconds)}")

    def test_A_cgroup_that_holds_a_foreign_process_before_the_spawn_is_refused_and_nothing_is_started(self):
        foreign = subprocess.Popen(["sleep", "30"])
        self.addCleanup(lambda: (foreign.kill(), foreign.wait()))
        ws = Workspace(self, "touch started.marker\n")
        cgroup = fake_cgroup(ws, files={"cgroup.procs": f"{foreign.pid}\n"})
        with self.assertRaises(rs_scope.CgroupBusy):
            ws.run(case(expected=b""), cgroup=cgroup)
        self.assertFalse(os.path.exists(os.path.join(ws.dir, "started.marker")))
        self.assertTrue(isinstance(rs_scope.CgroupBusy("x"), ex.EnvironmentProblem))
        self.assertTrue(alive(foreign.pid), "a process this case does not own is never killed")

    def test_A_cgroup_directory_that_cannot_be_read_is_not_this_cases_to_measure(self):
        ws = Workspace(self, "touch started.marker\n")
        with self.assertRaises(rs_scope.CgroupBusy):
            ws.run(case(expected=b""), cgroup=ws.make_dir("cg-without-procs"))
        self.assertFalse(os.path.exists(os.path.join(ws.dir, "started.marker")))

    def test_In_a_box_a_cgroup_that_does_not_exist_after_child_pid_is_a_launch_failure_and_nothing_is_left(self):
        seconds = "49.%d" % (os.getpid() % 1000 + 1000)
        ws = Workspace(self, f"sleep {seconds}\n")
        prefix = reporting_prefix()
        with self.assertRaises(rl.LaunchFailed) as raised:
            ws.run(case(expected=b""), prefix=prefix, status_fd=STATUS_FD, cgroup="/no/such/cgroup")
        self.assertIn("does not exist after child-pid", raised.exception.reason)
        self.assertTrue(wait_until(lambda: cmdline_alive(seconds) == []), f"left alive: {cmdline_alive(seconds)}")

    def test_The_program_is_found_through_a_fake_box_and_the_exit_comes_from_its_pidfd(self):
        ws = Workspace(self, REPLAY, FILES)
        cgroup = fake_cgroup(ws, files={"memory.peak": "1", "memory.events": CLEAN_EVENTS})
        result = ws.run(case(), prefix=box_prefix(fake_box(self), cgroup), status_fd=STATUS_FD, cgroup=cgroup)
        self.assertEqual((result.status, result.lifecycle["signalled"]["comm"]), ("PASS", "bash"))
        self.assertNotEqual(result.lifecycle["signalled"]["pid"], result.lifecycle["signalled"]["launcher"])


if __name__ == "__main__":
    unittest.main()
