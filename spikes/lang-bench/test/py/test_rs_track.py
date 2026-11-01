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
        self.box = subprocess.Popen([self.box_path, "-c", script])
        test.addCleanup(lambda: (self.box.kill(), self.box.wait()))
        assert wait_until(lambda: os.path.exists(self.pidfile) and len(self.children()) == children), "the children never started"
        test.addCleanup(self.stop_children)

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


class ForkTests(unittest.TestCase):
    def test_Extras_count_only_when_they_persist_across_two_looks_and_never_before_the_program_is_known(self):
        family, launcher = Family(self, 1), launcher_of(self)
        extra = launcher_of(self)
        family.list(family.children()[0])
        tracker = family.tracker(launcher)
        tracker.look()
        family.list(family.children()[0], extra.pid)
        tracker.look()
        self.assertEqual(tracker.extra_max, 0, "seen once: a tool that is gone a moment later")
        tracker.look()
        self.assertEqual(tracker.extra_max, 1, "seen twice: it stays")
        self.assertEqual(tracker.fork([]), {"known": True, "found": True, "count": 2})

    def test_The_leftover_read_counts_whatever_it_finds(self):
        family, launcher = Family(self, 1), launcher_of(self)
        family.list(family.children()[0])
        tracker = family.tracker(launcher)
        tracker.look()
        self.assertEqual(tracker.fork([123]), {"known": True, "found": True, "count": 2})
        self.assertEqual(tracker.fork([]), {"known": True, "found": False, "count": 1})
        self.assertEqual(tracker.fork(None), {"known": True, "found": False, "count": 1})


class PlainTests(unittest.TestCase):
    def test_Without_a_launch_status_the_launcher_is_the_program(self):
        launcher = launcher_of(self)
        tracker = tr.Tracker(launcher, ex.PlainLifecycle(), box=False)
        self.assertEqual((tracker.pid, tracker.found, tracker.seeking(), tracker.tracking()), (launcher.pid, tr.LAUNCHER, False, False))
        self.assertEqual(tracker.fork([])["known"], False, "no cgroup: nothing was counted")


if __name__ == "__main__":
    unittest.main()
