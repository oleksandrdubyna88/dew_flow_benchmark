"""No group signal ever goes to a reaped leader (N8), an interrupt keeps its own exit (N9), and the group scan is cheap and cannot raise (D4, D6)."""
import contextlib
import io
import os
import shutil
import signal
import subprocess
import sys
import tempfile
import time
import unittest
from unittest import mock

import runner_path  # noqa: F401
import rs_exec as ex
import rs_launch as rl
import rs_olympiad as ro
import rs_stop
import rs_scope
from box_support import ANSWER, STATUS_FD, Workspace, box_prefix, case, fake_box, fake_cgroup, sleepers, wait_until

FILES = {"answer.bin": ANSWER}


class ReapedLeaderTests(unittest.TestCase):
    def recording(self):
        """(signals seen as the returncode of the leader at the moment of each group signal, a context that records them)."""
        seen, started, real, real_popen = [], [], ex.signal_group, subprocess.Popen

        def popen(*args, **kwargs):
            proc = real_popen(*args, **kwargs)
            started.append(proc)
            return proc

        def recording(pid, sig):
            seen.extend(p.returncode for p in started if p.pid == pid)
            return real(pid, sig)

        return seen, (mock.patch.object(ex, "signal_group", recording), mock.patch.object(subprocess, "Popen", popen))

    def test_The_build_signals_the_group_of_a_straggler_before_it_reaps_the_leader(self):
        marker = "57.%d" % (os.getpid() % 1000 + 1000)
        ws = Workspace(self, "")
        ws.write(ws.dir, "build.sh", f"sleep {marker} &\n")
        seen, (signalling, popen) = self.recording()
        with signalling, popen:
            done = ex.build(ws.dir, ws.scratch)
        self.assertEqual(done.status, 0)
        self.assertTrue(seen and all(code is None for code in seen), f"the build's group signal found returncodes {seen}")
        self.assertTrue(wait_until(lambda: sleepers(marker) == []), "the straggler of the build is gone")

    def test_A_build_without_a_straggler_gets_no_group_signal(self):
        ws = Workspace(self, "")
        ws.write(ws.dir, "build.sh", ":\n")
        seen = []
        with mock.patch.object(ex, "signal_group", lambda pid, sig: seen.append(pid)):
            self.assertEqual(ex.build(ws.dir, ws.scratch).status, 0)
        self.assertEqual(seen, [])

    def test_kill_group_does_not_signal_a_leader_that_is_already_reaped(self):
        proc = subprocess.Popen(["true"], start_new_session=True)
        proc.wait()
        with mock.patch.object(ex, "signal_group") as sent:
            ex.kill_group(proc)
        self.assertEqual(sent.call_count, 0)

    def test_kill_group_signals_a_running_leader(self):
        proc = subprocess.Popen(["sleep", "30"], start_new_session=True)
        self.addCleanup(proc.wait)
        with mock.patch.object(ex, "signal_group", wraps=ex.signal_group) as sent:
            ex.kill_group(proc)
        self.assertEqual(sent.call_count, 1)

    def test_An_interrupt_after_the_case_ended_does_not_signal_the_reaped_launcher(self):
        ws = Workspace(self, "cat answer.bin\n", FILES)
        seen, (signalling, popen) = self.recording()

        def interrupt(*args, **kwargs):
            raise KeyboardInterrupt

        with signalling, popen, mock.patch.object(ro, "Outcome", interrupt):
            with self.assertRaises(KeyboardInterrupt):
                ws.run(case())
        self.assertTrue(all(code is None for code in seen), f"a group signal found a reaped leader: {seen}")


class InterruptKeepsItsExitTests(unittest.TestCase):
    def test_F9_the_swallowed_problem_leaves_a_one_line_note_on_standard_error(self):
        ws = Workspace(self, "cat answer.bin\n", FILES)
        cgroup = fake_cgroup(ws)

        def interrupt_after_the_scope_went(*args, **kwargs):
            shutil.rmtree(cgroup)
            raise KeyboardInterrupt

        err = io.StringIO()
        with mock.patch.object(ro, "Outcome", interrupt_after_the_scope_went), contextlib.redirect_stderr(err):
            with self.assertRaises(KeyboardInterrupt):
                ws.run(case(), prefix=box_prefix(fake_box(self), cgroup), status_fd=STATUS_FD, cgroup=cgroup)
        lines = err.getvalue().splitlines()
        self.assertEqual(len(lines), 1, lines)
        self.assertIn("while stopping", lines[0])
        self.assertIn("CgroupLost", lines[0])

    def test_An_interrupt_after_settle_on_a_scope_that_is_already_gone_stays_the_interrupt_and_is_not_CgroupLost(self):
        ws = Workspace(self, "cat answer.bin\n", FILES)
        cgroup = fake_cgroup(ws)

        def interrupt_after_the_scope_went(*args, **kwargs):
            shutil.rmtree(cgroup)
            raise KeyboardInterrupt

        with mock.patch.object(ro, "Outcome", interrupt_after_the_scope_went), contextlib.redirect_stderr(io.StringIO()):
            with self.assertRaises(KeyboardInterrupt):
                ws.run(case(), prefix=box_prefix(fake_box(self), cgroup), status_fd=STATUS_FD, cgroup=cgroup)


class StopSignalTests(unittest.TestCase):
    """E4: the first thing the handler does is shut the door on every other stopping signal, all at once."""

    def test_Inside_the_handler_every_stopping_signal_is_already_blocked_when_the_first_one_is_set_to_ignore(self):
        masks = []
        real = signal.signal

        def recording(sig, handler):
            masks.append(set(signal.pthread_sigmask(signal.SIG_BLOCK, [])))
            return real(sig, handler)

        before = signal.pthread_sigmask(signal.SIG_BLOCK, [])
        self.addCleanup(signal.pthread_sigmask, signal.SIG_SETMASK, before)
        saved = {sig: signal.getsignal(sig) for sig in rl.STOPPING_SIGNALS}
        self.addCleanup(lambda: [real(sig, handler) for sig, handler in saved.items()])
        with mock.patch.object(signal, "signal", recording):
            with self.assertRaises(rs_stop.Interrupted):
                rs_stop._on_signal(signal.SIGTERM, None)
        self.assertTrue(masks and set(rl.STOPPING_SIGNALS) <= masks[0], f"the first signal.signal call saw the mask {masks[:1]}")
        self.assertEqual(len(masks), len(rl.STOPPING_SIGNALS))

    def test_Two_stopping_signals_sent_back_to_back_run_the_teardown_once(self):
        note = os.path.join(tempfile.mkdtemp(dir=os.environ.get("TMPDIR")), "teardowns")
        script = (
            "import os, sys, time\n"
            f"sys.path.insert(0, {os.path.dirname(os.path.abspath(rs_stop.__file__))!r})\n"
            "import rs_launch as rl, rs_stop\n"
            f"def teardown():\n    open({note!r}, 'a').write('x'); time.sleep(0.3)\n"
            "rl.abandon_active = teardown\n"
            "rs_stop.install_handlers()\n"
            "try:\n    print('ready', flush=True)\n    time.sleep(30)\nexcept rs_stop.Interrupted as i:\n    rs_stop.stopped(i)\n"
        )
        proc = subprocess.Popen([sys.executable, "-I", "-B", "-c", script], stdout=subprocess.PIPE, stderr=subprocess.PIPE)
        self.addCleanup(lambda: proc.poll() is None and proc.kill())
        self.assertEqual(proc.stdout.readline(), b"ready\n")
        proc.send_signal(signal.SIGTERM)
        proc.send_signal(signal.SIGINT)
        proc.send_signal(signal.SIGHUP)
        proc.communicate(timeout=30)
        with open(note) as f:
            self.assertEqual(f.read(), "x", "one teardown, whatever came after the first signal")


class GroupScanTests(unittest.TestCase):
    def test_A_stat_whose_group_field_is_not_a_number_is_unknown_and_never_an_error(self):
        self.assertEqual(ex._stat_fields("123 (a b) S 1 77 3"), ("S", 77))
        self.assertIsNone(ex._stat_fields("123 (a b) S 1 xx 3"))
        self.assertIsNone(ex._stat_fields("123 (a b) S"))
        self.assertIsNone(ex._stat_fields(""))

    def test_A_group_that_is_gone_is_found_gone_without_scanning_proc(self):
        with mock.patch.object(ex.os, "listdir", side_effect=AssertionError("/proc was scanned")):
            self.assertFalse(ex.group_alive(4194303))

    def test_A_group_that_exists_is_scanned_because_a_zombie_leader_may_be_its_only_member(self):
        zombie = subprocess.Popen(["true"], start_new_session=True)
        self.addCleanup(zombie.wait)
        self.assertTrue(wait_until(lambda: ex._group_of(zombie.pid) == ("Z", zombie.pid)), "an unreaped leader that exited is a zombie")
        self.assertFalse(ex.group_alive(zombie.pid), "a zombie leader alone is no member that is alive")
        live = subprocess.Popen(["sleep", "30"], start_new_session=True)
        self.addCleanup(lambda: (live.kill(), live.wait()))
        self.assertTrue(ex.group_alive(live.pid))


class PacedScanTests(unittest.TestCase):
    """E6: waiting for a group to go scans /proc at a backed-off pace (1 ms up to 20 ms), never on every poll."""

    def test_The_wait_for_a_live_group_scans_proc_a_few_times_and_not_every_millisecond(self):
        proc = subprocess.Popen(["sleep", "30"], start_new_session=True)
        self.addCleanup(lambda: (proc.kill(), proc.wait()))
        scans = []
        real = os.listdir
        with mock.patch.object(ex, "GONE_CEILING_MS", 300), mock.patch.object(ex.os, "listdir", lambda path="." : (scans.append(path), real(path))[1]):
            ex._wait_group_gone(proc)
        proc_scans = [p for p in scans if p == "/proc"]
        self.assertGreaterEqual(len(proc_scans), 5, "the group is alive: every poll looks at /proc")
        self.assertLessEqual(len(proc_scans), 25, f"{len(proc_scans)} scans in 300 ms: a poll every millisecond would make 300")

    def test_A_group_whose_only_member_is_a_zombie_is_gone_for_the_wait(self):
        proc = subprocess.Popen(["true"], start_new_session=True)
        self.addCleanup(proc.wait)
        self.assertTrue(wait_until(lambda: ex._group_of(proc.pid) == ("Z", proc.pid)))
        started = time.monotonic()
        ex._wait_group_gone(proc)
        self.assertLess(time.monotonic() - started, 0.5)
        self.assertFalse(ex.group_present(proc))


class ScopeFallbackTests(unittest.TestCase):
    def test_D0_a_kill_that_cannot_be_written_still_reaps_the_launcher_and_the_status_is_the_programs_own(self):
        ws = Workspace(self, "exit 3\n")
        cgroup = fake_cgroup(ws)
        os.remove(os.path.join(cgroup, "cgroup.kill"))
        os.mkdir(os.path.join(cgroup, "cgroup.kill"))
        result = ws.run(case(expected=b""), cgroup=cgroup)
        self.assertEqual((result.status, result.reason), ("FAIL", "exit status 3"), "not None and not a default: the launcher was reaped")

    def test_D1_a_cgroup_that_is_gone_before_the_kill_is_a_cleanup_failure_on_the_plain_path(self):
        seconds = "58.%d" % (os.getpid() % 1000 + 1000)
        ws = Workspace(self, "")
        cgroup = fake_cgroup(ws)
        ws.write(ws.dir, "run.sh", f'sleep {seconds} &\nrm -r "{cgroup}"\ncat answer.bin\n')
        ws.write(ws.dir, "answer.bin", ANSWER)
        with self.assertRaises(ex.CleanupFailed):
            ws.run(case(), cgroup=cgroup)
        self.assertTrue(wait_until(lambda: sleepers(seconds) == []), "and the launcher's group was killed first")

    def test_F4_a_cgroup_that_never_existed_is_an_environment_problem_and_not_a_cleanup_failure_and_nothing_is_left(self):
        seconds = "59.%d" % (os.getpid() % 1000 + 1000)
        with self.assertRaises(rs_scope.CgroupMissing) as raised, contextlib.redirect_stderr(io.StringIO()):
            Workspace(self, f"sleep {seconds} & sleep {seconds}\n").run(case(expected=b""), cgroup="/no/such/cgroup", limit_ms=300)
        self.assertIsInstance(raised.exception, ex.EnvironmentProblem)
        self.assertNotIsInstance(raised.exception, ex.CleanupFailed, "exit 3 (the environment), not exit 5 (the runner failed)")
        self.assertTrue(wait_until(lambda: sleepers(seconds) == []), "the launcher's group was killed first")


if __name__ == "__main__":
    unittest.main()
