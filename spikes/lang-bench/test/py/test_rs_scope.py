"""A case whose program runs in a cgroup the layer made (E1.S8a part ii-a; B1, B4): the memory read, the leftover evidence, the program's pid,
and the kill at the time limit through `cgroup.kill`. The real-scope tests need `systemd-run --user --scope` and bwrap and skip with their reason
without them (they fail under BENCH_REQUIRE_SANDBOX=1)."""
import os
import signal
import subprocess
import unittest

import runner_path  # noqa: F401
import rs_cgroup as cg
import rs_exec as ex
import rs_report as rp
import rs_scope
from box_support import ANSWER, Scope, Workspace, case, wait_until
from test_rs_cgroup import Folder, sleeper

REPLAY = "cat answer.bin\n"
FILES = {"answer.bin": ANSWER}
OOM_EVENTS = "low 0\nhigh 0\nmax 4\noom 1\noom_kill 1\noom_group_kill 0\n"


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
        scope = Scope(self)
        result = ws.run(case(expected=b""), prefix=scope.prefix(ws.dir), cgroup=scope.cgroup, limit_ms=400)
        self.assertTrue(result.reason.startswith("time limit"), result.reason)
        self.assertEqual(result.lifecycle["kill"], "cgroup.kill")
        self.assertTrue(wait_until(lambda: scope.pids() == []), f"nothing of the case may be left in its scope: {scope.pids()}")

    def test_The_program_pid_comes_from_cgroup_procs_and_is_not_a_bwrap_process(self):
        scope = Scope(self)
        ws = Workspace(self, REPLAY, FILES)
        result = ws.run(case(), prefix=scope.prefix(ws.dir), cgroup=scope.cgroup)
        self.assertEqual(result.status, "PASS", result.lines)
        signalled = result.lifecycle["signalled"]
        self.assertEqual(signalled["found"], "cgroup.procs minus bwrap and wrapper")
        self.assertIsInstance(signalled["pid"], int)
        self.assertEqual(result.lifecycle["fork"], {"known": True, "found": False, "count": 1})
        self.assertEqual(result.lifecycle["leftoverEvidence"], "cgroup.procs minus bwrap and wrapper, read after the exit and before the kill")

    def test_A_child_that_outlives_the_program_is_the_leftover_evidence_and_is_killed_through_the_scope(self):
        scope = Scope(self)
        ws = Workspace(self, "sleep 60 &\ncat answer.bin\n", FILES)
        result = ws.run(case(), prefix=scope.prefix(ws.dir), cgroup=scope.cgroup)
        self.assertEqual(result.status, "PASS", "a leftover is a finding, the verdict stays with the output")
        self.assertEqual(result.lifecycle["leftover"], {"known": True, "found": True})
        self.assertIn("  leftover: process still running after the exit (killed)", result.lines)
        self.assertTrue(wait_until(lambda: scope.pids() == []), f"left in the scope: {scope.pids()}")

    def test_A_clean_program_leaves_no_leftover_and_the_scope_is_empty(self):
        scope = Scope(self)
        ws = Workspace(self, REPLAY, FILES)
        result = ws.run(case(), prefix=scope.prefix(ws.dir), cgroup=scope.cgroup)
        self.assertEqual(result.lifecycle["leftover"], {"known": True, "found": False})
        self.assertNotIn("leftover", " ".join(result.lines))

    def test_The_peak_is_read_from_the_scope_before_any_kill(self):
        scope = Scope(self)
        ws = Workspace(self, "sleep 60\n")
        result = ws.run(case(expected=b""), prefix=scope.prefix(ws.dir), cgroup=scope.cgroup, limit_ms=300)
        memory = result.lifecycle["memory"]
        self.assertEqual(memory["known"], True, memory)
        self.assertGreater(memory["peakBytes"], 0, "read while the program's scope was still alive")
        self.assertIn(f"  memory: peak {memory['peakBytes']} bytes", result.lines)


class FakeCgroupTests(unittest.TestCase):
    def test_An_oom_kill_in_memory_events_is_memory_limit_whatever_the_exit_status(self):
        cgroup = Folder(self, {"memory.peak": "7340032", "memory.events": OOM_EVENTS, "cgroup.procs": ""}).dir
        result = Workspace(self, REPLAY, FILES).run(case(), cgroup=cgroup)
        self.assertEqual((result.status, result.reason), ("FAIL", "memory limit"), "the program exited 0 with the exact output: still not a pass")
        self.assertIn("  memory.events: oom_kill 1", result.lines)
        self.assertEqual(result.lifecycle["memory"]["peakBytes"], 7340032)

    def test_A_cgroup_without_an_oom_kill_does_not_change_a_pass_and_its_peak_is_kept(self):
        cgroup = Folder(self, {"memory.peak": "1048576", "memory.events": "oom 0\noom_kill 0\n", "cgroup.procs": ""}).dir
        result = Workspace(self, REPLAY, FILES).run(case(), cgroup=cgroup)
        self.assertEqual(result.status, "PASS")
        self.assertEqual(result.lifecycle["memory"], {"known": True, "peakBytes": 1048576, "events": {"oom": 0, "oom_kill": 0}, "oomKill": 0})
        self.assertIn("  memory: peak 1048576 bytes", result.lines)

    def test_The_cgroup_template_is_filled_with_the_case_id(self):
        root = Folder(self).dir
        for ident in ("s01", "s02"):
            os.mkdir(os.path.join(root, ident))
            for name, text in (("memory.peak", ident[1:]), ("memory.events", "oom_kill 0\n"), ("cgroup.procs", "")):
                with open(os.path.join(root, ident, name), "w") as f:
                    f.write(text)
        ws = Workspace(self, REPLAY, FILES)
        peaks = [ws.run(case(ident=ident), cgroup=os.path.join(root, "{case}")).lifecycle["memory"]["peakBytes"] for ident in ("s01", "s02")]
        self.assertEqual(peaks, [1, 2])

    def test_A_cgroup_that_cannot_be_read_says_unknown_and_never_zero(self):
        result = Workspace(self, REPLAY, FILES).run(case(), cgroup=os.path.join(Folder(self).dir, "gone"))
        self.assertEqual(result.status, "PASS")
        self.assertEqual(result.lifecycle["memory"]["known"], False)
        self.assertEqual(result.lifecycle["leftover"]["known"], False)
        self.assertIn("  memory: not certified", result.lines)
        self.assertEqual(result.lifecycle["signalled"]["pid"], None)

    def test_One_candidate_is_the_program_and_two_are_the_finding_fork_with_no_pid_chosen(self):
        first, second = sleeper(self), sleeper(self)
        launcher = subprocess.Popen(["sleep", "30"])
        self.addCleanup(lambda: (launcher.kill(), launcher.wait()))
        one = rs_scope.ScopeLifecycle(Folder(self, {"cgroup.procs": f"{first}\n"}).dir)
        self.assertEqual(one.program_of(launcher), ex.Program(first, "cgroup.procs minus bwrap and wrapper", 1))
        two = rs_scope.ScopeLifecycle(Folder(self, {"cgroup.procs": f"{first}\n{second}\n"}).dir)
        program = two.program_of(launcher)
        self.assertEqual(program, ex.Program(None, "cgroup.procs held 2 candidates", 2))
        self.assertEqual(rp.fork_record(program), {"known": True, "found": True, "count": 2})
        self.assertEqual(rp.fork_lines(program), ("  fork: 2 processes in the program's cgroup where one was expected",))
        self.assertEqual(rp.signalled_record(program), {"signal": None, "pid": None, "found": "cgroup.procs held 2 candidates"})

    def test_The_wrapper_pid_beside_the_json_result_is_not_taken_for_the_program(self):
        wrapper = sleeper(self, "wrapper-sh")
        out_dir = Folder(self, {"wrapper.pid": f"{wrapper}\n"}).dir
        cgroup = Folder(self, {"memory.peak": "1", "memory.events": "oom_kill 0\n", "cgroup.procs": f"{wrapper}\n"}).dir
        self.assertEqual(rs_scope.ScopeLifecycle(cgroup, out_dir).candidates(), [])
        self.assertEqual(rs_scope.ScopeLifecycle(cgroup, None).candidates(), [wrapper])


if __name__ == "__main__":
    unittest.main()
