"""A case whose program runs in a cgroup the layer made (E1.S8a part ii-a; B1, B4): the memory read, the leftover evidence, the program's pid,
and the kill through `cgroup.kill`, always. The real-scope tests need `systemd-run --user --scope` and bwrap and skip with their reason without
them (they fail under BENCH_REQUIRE_SANDBOX=1)."""
import json
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
    holder = os.path.join(ws.root, "holder.pid")
    config = {"prefix": scope.prefix(ws.dir, status_fd=STATUS_FD, holder=holder), "status_fd": STATUS_FD, "cgroup": scope.cgroup, "wrapper_file": holder,
              **overrides}
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
        # the hidden child is nothing but a copy of bash named bwrap, waiting on a FIFO it holds open itself: no other process gives it away
        script = "cp /usr/bin/bash ./bwrap\nmkfifo hold\n./bwrap -c 'read -t 60 x' <> hold &\ncat answer.bin\n"
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
        fork = result.lifecycle["fork"]
        self.assertIn(fork.get("found"), (False, None), "a short tool is never fork; on a machine too loaded to look in time it is unknown")
        self.assertEqual(fork.get("count", 1), 1)
        self.assertIn(result.lifecycle["signalled"]["comm"], ("bash", "cat", None), "bash before the exec, cat after, unknown if never seen")

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

    def test_A_cgroup_kill_that_cannot_be_written_kills_the_launchers_group_too_and_the_empty_check_decides(self):
        seconds = "48.%d" % (os.getpid() % 1000 + 1000)
        ws = Workspace(self, f"sleep {seconds}\n")
        cgroup = fake_cgroup(ws, files={"memory.peak": "1", "memory.events": CLEAN_EVENTS})
        os.mkdir(os.path.join(cgroup, "cgroup.kill"))  # a directory: the write fails
        result = ws.run(case(expected=b""), cgroup=cgroup, limit_ms=300)
        self.assertTrue(result.reason.startswith("time limit"), "the cgroup lists nothing: the fallback found it empty, so the case stands")
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


class SettleOrderTests(unittest.TestCase):
    """C0, C3/C9, C13: the launcher is never reaped before the kill and its fallback are done, a cgroup that vanishes mid-case is exit 3, and a kill
    that cannot be written falls back to the pids the cgroup lists."""

    def test_The_kill_reads_the_scope_first_so_that_a_dying_scope_afterwards_is_empty_and_not_unreadable(self):
        ws = Workspace(self, "")
        directory = fake_cgroup(ws)
        proc = subprocess.Popen(["true"])
        proc.wait()
        calls = []
        with mock.patch.object(cg, "wait_empty", lambda directory, ceiling=0, was_readable=False: calls.append(was_readable)):
            rs_scope.ScopeLifecycle(directory).kill(proc)
        self.assertEqual(calls, [True], "it had read cgroup.procs before the kill: ENOENT after it is the end of the cgroup")

    def test_Waiting_for_the_launcher_never_reaps_it_even_at_the_ceiling(self):
        proc = subprocess.Popen(["true"])
        self.addCleanup(proc.wait)
        pidfd = ex.pidfd_open(proc.pid)
        self.addCleanup(ex.close_fd, pidfd)
        select_until_readable(pidfd)
        with mock.patch.object(ex, "GONE_CEILING_MS", 0):
            self.assertTrue(rs_scope._wait_launcher(proc))
        self.assertIsNone(proc.returncode, "a reaped leader's pid may be someone else's: the group kill must still find it unreaped")

    def test_A_straggler_of_a_launcher_that_exited_is_group_killed_while_the_leader_is_not_yet_reaped(self):
        seconds = "50.%d" % (os.getpid() % 1000 + 1000)
        ws = Workspace(self, f"sleep {seconds} &\ncat answer.bin\n", FILES)
        cgroup = fake_cgroup(ws, files={"memory.peak": "1", "memory.events": CLEAN_EVENTS})
        os.mkdir(os.path.join(cgroup, "cgroup.kill"))  # the write fails: the fallback is what runs
        seen = []
        real = ex.signal_group
        launcher = {}

        def recording(pid, sig):
            seen.append(launcher["proc"].returncode if "proc" in launcher else "no proc")
            return real(pid, sig)

        real_start = rl.start

        def start(*args, **kwargs):
            started = real_start(*args, **kwargs)
            launcher["proc"] = started.proc
            return started

        with mock.patch.object(ex, "signal_group", recording), mock.patch.object(rl, "start", start):
            result = ws.run(case(), cgroup=cgroup)
        self.assertEqual(result.status, "PASS")
        self.assertTrue(seen and all(code is None for code in seen), f"signal_group saw returncodes {seen}: it must always be sent to an unreaped leader")
        self.assertTrue(wait_until(lambda: cmdline_alive(seconds) == []), f"left alive: {cmdline_alive(seconds)}")

    def test_A_cgroup_that_vanishes_after_child_pid_is_an_environment_failure_and_nothing_is_left(self):
        seconds = "51.%d" % (os.getpid() % 1000 + 1000)
        ws = Workspace(self, f"exec sleep {seconds}\n")
        cgroup = fake_cgroup(ws, files={"memory.peak": "1", "memory.events": CLEAN_EVENTS})
        prefix = box_prefix(fake_box(self), cgroup, after_report=f'rm -r "{cgroup}"')
        with self.assertRaises(rs_scope.CgroupLost) as raised:
            ws.run(case(expected=b""), prefix=prefix, status_fd=STATUS_FD, cgroup=cgroup, limit_ms=400)
        self.assertIsInstance(raised.exception, ex.EnvironmentProblem)
        self.assertTrue(wait_until(lambda: cmdline_alive(seconds) == []), f"left alive: {cmdline_alive(seconds)}")

    def test_A_cgroup_that_vanishes_after_child_pid_is_exit_3_on_the_command_line(self):
        import test_run_samples as cli
        box = cli.Box(self, run_sh="exec sleep 52.%d\n" % (os.getpid() % 1000 + 1000))
        cgroups = os.path.join(box.root, "cg-{case}")
        for case_id in ("s03", "empty-program"):
            os.makedirs(os.path.join(box.root, f"cg-{case_id}"))
            with open(os.path.join(box.root, f"cg-{case_id}", "cgroup.procs"), "w"):
                pass
        prefix = box_prefix(fake_box(self), None, after_report=f'rm -r "{box.root}/cg-{{case}}"')  # `{case}` is the prefix's own placeholder
        code, out, err = box.cli("--launch-prefix", json.dumps(prefix), "--launch-status", str(STATUS_FD), "--cgroup-of", cgroups)
        self.assertEqual(code, 3, err)
        self.assertIn("cgroup", err)
        self.assertNotIn("PASS", out)

    def test_A_kill_that_cannot_be_written_falls_back_to_the_pids_the_cgroup_lists_and_checks_it_is_empty(self):
        seconds = "53.%d" % (os.getpid() % 1000 + 1000)
        ws = Workspace(self, "", FILES)
        cgroup = fake_cgroup(ws, files={"memory.peak": "1", "memory.events": CLEAN_EVENTS})
        os.mkdir(os.path.join(cgroup, "cgroup.kill"))
        # a straggler in a session of its own (the launcher's group does not reach it) that lists itself in the fake cgroup
        ws.write(ws.dir, "run.sh", f'setsid sleep {seconds} &\necho $! > "{cgroup}/cgroup.procs"\ncat answer.bin\n')
        result = ws.run(case(), cgroup=cgroup)
        self.assertEqual((result.status, result.lifecycle["leftover"]), ("PASS", {"known": True, "found": True}))
        self.assertTrue(wait_until(lambda: cmdline_alive(seconds) == []), f"the listed pid was not killed: {cmdline_alive(seconds)}")

    def test_A_pid_the_fallback_cannot_kill_is_a_cleanup_failure_after_the_check(self):
        ws = Workspace(self, "", FILES)
        cgroup = fake_cgroup(ws, files={"memory.peak": "1", "memory.events": CLEAN_EVENTS})
        os.mkdir(os.path.join(cgroup, "cgroup.kill"))
        stubborn = subprocess.Popen(["sleep", "30"], start_new_session=True)
        self.addCleanup(lambda: (stubborn.kill(), stubborn.wait()))
        ws.write(ws.dir, "run.sh", f'echo {stubborn.pid} > "{cgroup}/cgroup.procs"\ncat answer.bin\n')
        with mock.patch.object(cg.os, "kill", side_effect=PermissionError):
            with self.assertRaises(ex.CleanupFailed) as raised:
                ws.run(case(), cgroup=cgroup)
        self.assertIn("still in", str(raised.exception), "named by the check that found the cgroup not empty")


def select_until_readable(fd):
    import selectors
    with selectors.DefaultSelector() as sel:
        sel.register(fd, selectors.EVENT_READ)
        sel.select(10)


if __name__ == "__main__":
    unittest.main()
