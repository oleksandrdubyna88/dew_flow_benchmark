"""Which process is the program (E1.S8a part ii-a; B1, D38, P4): the one whose PARENT is a bwrap process, by identity. The launcher pid is never a
candidate, a hidden copy of bash named bwrap hides nothing, a short tool bash starts is no candidate, and `fork` never decides the pid."""
import os
import subprocess
import tempfile
import time
import unittest

import runner_path  # noqa: F401
import rs_cgroup as cg
import rs_exec as ex
import rs_scope
import rs_track as tr
from unittest import mock
from box_support import wait_until
from test_rs_cgroup import Folder, copy_of_bash


class Family:
    """A fake box (a copy of bash running `sleep` children) and the fake cgroup that lists them."""

    def __init__(self, test, children=1, box=None):
        self.test = test
        self.box_path = box or copy_of_bash(test)
        self.dir = Folder(test, {"cgroup.procs": ""}).dir
        self.pidfile = os.path.join(self.dir, "children")
        script = f"for i in $(seq {children}); do sleep 30 & echo $! >> {self.pidfile}; done; wait"
        self.box = subprocess.Popen([self.box_path, "-c", script], stderr=subprocess.DEVNULL)  # a killed child makes bash print a line the Node parser would take for a result
        test.addCleanup(lambda: (self.box.kill(), self.box.wait()))
        assert wait_until(lambda: os.path.exists(self.pidfile) and len(self.children()) == children and self.exec_done(), 60), "the children never started"
        test.addCleanup(self.stop_children)

    def exec_done(self):
        """A forked child still runs its parent's file (it would look like bwrap); wait until every one is the `sleep` it execs."""
        return all(cg.comm_of(pid) == "sleep" for pid in self.children())

    def children(self):
        with open(self.pidfile) as f:
            return [int(line) for line in f.read().split()]

    def stop_children(self):
        for pid in self.children():
            try:
                os.kill(pid, 9)
            except OSError:
                pass

    def list(self, *pids):
        with open(os.path.join(self.dir, "cgroup.procs"), "w") as f:
            f.write("".join(f"{pid}\n" for pid in pids))

    def tracker(self, launcher, identity=None, wrapper_file=None):
        lifecycle = rs_scope.ScopeLifecycle(self.dir, identity or cg.bwrap_identity([self.box_path]), wrapper_file)
        tracker = tr.Tracker(launcher, lifecycle, box=True)
        self.test.addCleanup(tracker.close)
        return tracker


def launcher_of(test):
    proc = subprocess.Popen(["sleep", "30"])
    test.addCleanup(lambda: (proc.kill(), proc.wait()))
    return proc


class ChoiceTests(unittest.TestCase):
    def test_The_program_is_the_child_of_a_bwrap_process_and_the_box_and_the_launcher_are_not_candidates(self):
        family, launcher = Family(self, 1), launcher_of(self)
        family.list(launcher.pid, family.box.pid, family.children()[0])
        tracker = family.tracker(launcher)
        self.assertTrue(tracker.seeking())
        tracker.look()
        self.assertEqual((tracker.pid, tracker.found, tracker.comm), (family.children()[0], tr.FOUND, "sleep"))
        self.assertIsNotNone(tracker.pidfd)
        self.assertFalse(tracker.seeking())

    def test_The_launcher_pid_is_never_a_candidate_even_when_its_parent_is_a_bwrap_process(self):
        family = Family(self, 1)
        child = family.children()[0]
        launcher = subprocess.Popen(["sleep", "30"])
        self.addCleanup(lambda: (launcher.kill(), launcher.wait()))
        family.list(family.box.pid, child)
        tracker = family.tracker(type("L", (), {"pid": child})())  # the launcher IS the only process with a bwrap parent
        tracker.look()
        self.assertIsNone(tracker.pid, "the launcher's own pid is excluded whatever its parent")
        self.assertEqual(tracker.found, tr.NOT_YET)

    def test_A_process_whose_parent_is_not_bwrap_is_never_the_program_such_as_a_tool_bash_started(self):
        family, launcher = Family(self, 1), launcher_of(self)
        tool = subprocess.Popen(["sleep", "30"])  # its parent is this test, not a bwrap process
        self.addCleanup(lambda: (tool.kill(), tool.wait()))
        family.list(tool.pid)
        tracker = family.tracker(launcher)
        tracker.look()
        self.assertEqual((tracker.pid, tracker.many), (None, 0), "no candidate has a bwrap parent: no pid is chosen, and no fork is claimed")

    def test_A_hidden_copy_of_bash_named_bwrap_is_not_a_bwrap_parent(self):
        hidden = copy_of_bash(self)
        family, launcher = Family(self, 1, box=hidden), launcher_of(self)
        family.list(family.box.pid, family.children()[0])
        tracker = family.tracker(launcher, identity=cg.bwrap_identity([copy_of_bash(self)]))  # the real box is another file
        tracker.look()
        self.assertIsNone(tracker.pid, "a program that copies bash to ./bwrap hides nothing and is not the box")
        self.assertEqual(tracker.leftover(), [family.box.pid, family.children()[0]], "both stay visible as what they are")

    def test_Two_candidates_with_a_bwrap_parent_are_the_finding_fork_and_no_pid_is_chosen(self):
        family, launcher = Family(self, 2), launcher_of(self)
        family.list(*family.children())
        tracker = family.tracker(launcher)
        tracker.look()
        self.assertEqual((tracker.pid, tracker.many), (None, 2))
        self.assertEqual(tracker.fork([]), {"known": True, "found": True, "count": 2})
        self.assertFalse(tracker.seeking(), "it stops looking: the launcher's end is what is awaited")

    def test_No_candidate_at_all_is_unknown_and_never_a_fork(self):
        family, launcher = Family(self, 1), launcher_of(self)
        tracker = family.tracker(launcher)
        tracker.look()
        self.assertIsNone(tracker.pid)
        self.assertEqual(tracker.fork([])["known"], False)
        self.assertEqual(tracker.signalled()["pid"], None)

    def test_The_wrapper_named_by_the_flag_is_not_a_leftover_and_without_the_flag_it_is(self):
        family, launcher = Family(self, 1), launcher_of(self)
        wrapper = subprocess.Popen(["sleep", "30"])
        self.addCleanup(lambda: (wrapper.kill(), wrapper.wait()))
        file = os.path.join(family.dir, "wrapper.pid")
        with open(file, "w") as f:
            f.write(f"{wrapper.pid}\n")
        family.list(wrapper.pid, family.children()[0])
        self.assertEqual(family.tracker(launcher, wrapper_file=file).leftover(), [family.children()[0]])
        self.assertEqual(family.tracker(launcher).leftover(), [wrapper.pid, family.children()[0]])

    def test_A_pid_that_is_no_longer_the_bwrap_child_after_the_pidfd_is_taken_is_not_adopted_and_the_descriptor_is_closed(self):
        family, launcher = Family(self, 1), launcher_of(self)
        family.list(family.children()[0])
        tracker = family.tracker(launcher)
        before = sorted(os.listdir("/proc/self/fd"))
        tracker._still = lambda pid: False  # the pid was recycled between the listing and the pidfd
        tracker.look()
        self.assertEqual((tracker.pid, tracker.pidfd), (None, None))
        self.assertEqual(sorted(os.listdir("/proc/self/fd")), before, "the pidfd taken for the check is closed again")


class OrphanTests(unittest.TestCase):
    """A real-scope flake: the program (bash) was gone before the first look and its orphan `sleep`, now a child of bwrap's init, was adopted as
    the program, so the leftover read excluded the very leftover it was there to find."""

    def test_A_process_taken_for_the_program_that_is_still_alive_when_the_program_has_ended_is_a_leftover_and_not_the_pid(self):
        family, launcher = Family(self, 1), launcher_of(self)
        child = family.children()[0]
        family.list(child)
        tracker = family.tracker(launcher)
        tracker.look()
        self.assertEqual(tracker.pid, child)
        self.assertEqual(tracker.leftover(), [child], "alive after the program ended: it was an orphan, not the program")
        self.assertIsNone(tracker.signalled()["pid"])
        self.assertIn("orphan", tracker.signalled()["found"])
        self.assertEqual(tracker.fork(tracker.leftover()), {"known": True, "found": True, "count": 2})

    def test_F7_the_end_of_the_program_is_judged_by_its_pidfd_and_not_by_whether_proc_has_its_pid(self):
        family, launcher = Family(self, 1), launcher_of(self)
        child = family.children()[0]
        family.list(child)
        tracker = family.tracker(launcher)
        tracker.look()
        os.kill(child, 9)
        self.assertTrue(wait_until(lambda: cg.is_zombie(child) or not os.path.exists(f"/proc/{child}")))
        # the pid is "alive" to /proc (a recycled pid looks exactly like this) but the pidfd says the program exited
        with mock.patch.object(cg, "is_zombie", return_value=False), mock.patch.object(os.path, "exists", return_value=True):
            self.assertEqual(tracker.leftover(), [], "the pidfd decides: the program ended")
        self.assertEqual(tracker.signalled()["pid"], child)

    def test_F7_a_process_whose_pidfd_is_still_quiet_is_alive_whatever_proc_says(self):
        family, launcher = Family(self, 1), launcher_of(self)
        child = family.children()[0]
        family.list(child)
        tracker = family.tracker(launcher)
        tracker.look()
        with mock.patch.object(os.path, "exists", return_value=False):  # /proc "has no such pid" (a refused read looks like this)
            self.assertEqual(tracker.leftover(), [child])
        self.assertTrue(tracker.orphan)

    def test_The_program_that_really_ended_is_not_its_own_leftover(self):
        family, launcher = Family(self, 1), launcher_of(self)
        child = family.children()[0]
        family.list(child)
        tracker = family.tracker(launcher)
        tracker.look()
        os.kill(child, 9)
        self.assertTrue(wait_until(lambda: not os.path.exists(f"/proc/{child}") or cg.is_zombie(child)))
        self.assertEqual(tracker.leftover(), [])
        self.assertEqual(tracker.signalled()["pid"], child)


class ForkTests(unittest.TestCase):
    def looks_at(self, tracker, family, times_ms):
        """Look at the listed times of a clock that is ours: the extra process is in the cgroup from the first look on."""
        clock = [0]
        with mock.patch.object(ex, "now_ns", lambda: clock[0] * ex.NS_PER_MS):
            for at in times_ms:
                clock[0] = at
                tracker.look()

    def test_F2_an_extra_counts_only_when_it_is_seen_in_looks_at_least_100_ms_apart(self):
        family, launcher, extra = Family(self, 1), launcher_of(self), launcher_of(self)
        tracker = family.tracker(launcher)
        family.list(family.children()[0])
        self.looks_at(tracker, family, [0])
        family.list(family.children()[0], extra.pid)
        self.looks_at(tracker, family, [1, 2, 20, 40, 99])
        self.assertEqual(tracker.extra_max, 0, "seen in looks 98 ms apart: a tool that was only slow to be scheduled")
        self.looks_at(tracker, family, [101])
        self.assertEqual(tracker.extra_max, 1, "seen 100 ms apart: it stays")
        self.assertEqual(tracker.fork([]), {"known": True, "found": True, "count": 2})

    def test_F2_an_extra_that_left_and_came_back_starts_its_age_again(self):
        family, launcher, extra = Family(self, 1), launcher_of(self), launcher_of(self)
        tracker = family.tracker(launcher)
        family.list(family.children()[0])
        self.looks_at(tracker, family, [0])
        family.list(family.children()[0], extra.pid)
        self.looks_at(tracker, family, [10])
        family.list(family.children()[0])
        self.looks_at(tracker, family, [60])
        family.list(family.children()[0], extra.pid)
        self.looks_at(tracker, family, [120])
        self.assertEqual(tracker.extra_max, 0, "it was gone at 60 ms: its age is 0 again at 120 ms")

    def test_Extras_are_never_counted_before_the_program_is_known(self):
        family, launcher, extra = Family(self, 1), launcher_of(self), launcher_of(self)
        tracker = family.tracker(launcher)
        family.list(extra.pid)  # no child of bwrap listed: the program is not known
        self.looks_at(tracker, family, [0, 500])
        self.assertEqual((tracker.pid, tracker.extra_max), (None, 0))

    def test_The_leftover_read_counts_whatever_it_finds(self):
        family, launcher = Family(self, 1), launcher_of(self)
        family.list(family.children()[0])
        tracker = family.tracker(launcher)
        tracker.look()
        self.assertEqual(tracker.fork([123]), {"known": True, "found": True, "count": 2})
        self.assertEqual(tracker.fork([]), {"known": True, "found": False, "count": 1})
        self.assertEqual(tracker.fork(None), {"known": True, "found": False, "count": 1})


class SeekPaceTests(unittest.TestCase):
    def test_The_look_backs_off_from_1_ms_to_the_tracking_tick_while_the_program_is_not_found(self):
        import rs_launch as rl
        import rs_pump
        launcher = launcher_of(self)
        tracker = tr.Tracker(launcher, rs_scope.ScopeLifecycle(Folder(self, {"cgroup.procs": ""}).dir, None, None), box=True)
        launched = rl.Launched(launcher, None, ex.now_ns(), None, None, None, tracker)
        pump = rs_pump.Pump(launched, b"", None, os.devnull)
        self.addCleanup(pump.close)
        ticks = [pump._tick() for _ in range(8)]
        self.assertEqual(ticks[:6], [0.001, 0.002, 0.004, 0.008, 0.016, ex.TRACK_TICK_S])
        self.assertEqual(set(ticks[5:]), {ex.TRACK_TICK_S}, "and stays there")
        self.assertEqual(ticks, sorted(ticks))


class PlainTests(unittest.TestCase):
    def test_Without_a_launch_status_the_launcher_is_the_program(self):
        launcher = launcher_of(self)
        tracker = tr.Tracker(launcher, ex.PlainLifecycle(), box=False)
        self.assertEqual((tracker.pid, tracker.found, tracker.seeking(), tracker.tracking()), (launcher.pid, tr.LAUNCHER, False, False))
        self.assertEqual(tracker.fork([])["known"], False, "no cgroup: nothing was counted")


if __name__ == "__main__":
    unittest.main()
