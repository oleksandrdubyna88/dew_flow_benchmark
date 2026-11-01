"""No group signal ever goes to a reaped leader (N8), an interrupt keeps its own exit (N9), and the group scan is cheap and cannot raise (D4, D6)."""
import os
import shutil
import subprocess
import unittest
from unittest import mock

import runner_path  # noqa: F401
import rs_exec as ex
import rs_olympiad as ro
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
    def test_An_interrupt_after_settle_on_a_scope_that_is_already_gone_stays_the_interrupt_and_is_not_CgroupLost(self):
        ws = Workspace(self, "cat answer.bin\n", FILES)
        cgroup = fake_cgroup(ws)

        def interrupt_after_the_scope_went(*args, **kwargs):
            shutil.rmtree(cgroup)
            raise KeyboardInterrupt

        with mock.patch.object(ro, "Outcome", interrupt_after_the_scope_went):
            with self.assertRaises(KeyboardInterrupt):
                ws.run(case(), prefix=box_prefix(fake_box(self), cgroup), status_fd=STATUS_FD, cgroup=cgroup)


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

    def test_D1_a_cgroup_that_never_existed_is_a_cleanup_failure_too_and_nothing_is_left(self):
        seconds = "59.%d" % (os.getpid() % 1000 + 1000)
        with self.assertRaises(ex.CleanupFailed):
            Workspace(self, f"sleep {seconds} & sleep {seconds}\n").run(case(expected=b""), cgroup="/no/such/cgroup", limit_ms=300)
        self.assertTrue(wait_until(lambda: sleepers(seconds) == []))


if __name__ == "__main__":
    unittest.main()
