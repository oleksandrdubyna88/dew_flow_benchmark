"""A cgroup the runner can trust (E1.S8a part ii-a round 4: N2, N3, N6, N7): the directory is a cgroup2 node, it is the box's own, its counters
start at zero, `cgroup.kill` is opened and never created, and a listing that cannot be understood is never read as dead.
The fake directories of the other tests are let through by `Workspace.run`; every test here runs with `real=True`."""
import os
import subprocess
import time
import unittest
from unittest import mock

import runner_path  # noqa: F401
import rs_cgroup as cg
import rs_exec as ex
import rs_launch as rl
import rs_scope
from box_support import ANSWER, STATUS_FD, Scope, Workspace, case, fake_box, box_prefix, fake_cgroup, fake_cgroup2, require_scope, sleepers, wait_until
from test_rs_cgroup import Folder

FILES = {"answer.bin": ANSWER}


class CgroupKindTests(unittest.TestCase):
    def test_A_directory_that_is_not_a_cgroup2_node_is_refused_before_the_spawn(self):
        ws = Workspace(self, "touch started.marker\n")
        plain = ws.make_dir("not-a-cgroup")
        ws.write(plain, "cgroup.procs", "")
        with self.assertRaises(rs_scope.CgroupInvalid) as raised:
            ws.run(case(expected=b""), cgroup=plain, real=True)
        self.assertIsInstance(raised.exception, ex.EnvironmentProblem)
        self.assertFalse(os.path.exists(os.path.join(ws.dir, "started.marker")))

    def test_A_cgroup2_node_is_a_directory_with_controllers_under_the_cgroup2_mount(self):
        self.assertTrue(cg.is_cgroup2("/sys/fs/cgroup"))
        self.assertFalse(cg.is_cgroup2(Folder(self, {"cgroup.procs": ""}).dir), "no controllers file")
        self.assertFalse(cg.is_cgroup2(Folder(self, {"cgroup.controllers": "memory pids\n"}).dir), "a controllers file outside the mount is a file, not a cgroup")

    def test_E3_E9_a_directory_outside_the_cgroup2_mount_that_holds_a_controllers_file_is_refused_before_the_spawn_and_nothing_is_written_to_it(self):
        ws = Workspace(self, "touch started.marker\n")
        outside = ws.make_dir("outside")
        ws.write(outside, "cgroup.controllers", "memory\n")
        before = sorted(os.listdir(outside))
        with self.assertRaises(rs_scope.CgroupInvalid):
            ws.run(case(expected=b""), cgroup=outside, real=True)
        self.assertEqual(sorted(os.listdir(outside)), before, "nothing was written to it")
        self.assertFalse(os.path.exists(os.path.join(ws.dir, "started.marker")))

    def test_E3_the_mount_root_comes_from_mountinfo_and_a_mount_that_is_not_cgroup2_is_no_root(self):
        info = ("36 35 0:30 / /sys/fs/cgroup rw,nosuid - cgroup2 cgroup2 rw\n"
                "40 35 0:31 / /sys/fs/cgroup/legacy rw - cgroup cgroup rw,cpu\n"
                "41 35 0:32 / /mnt/with\\040space rw - cgroup2 cgroup2 rw\n")
        with mock.patch.object(cg, "_mountinfo", return_value=info):
            self.assertEqual(cg.cgroup2_root("/sys/fs/cgroup/user.slice/x"), "/sys/fs/cgroup")
            self.assertIsNone(cg.cgroup2_root("/sys/fs/cgroup/legacy/sub"), "the nearest mount is a v1 hierarchy")
            self.assertIsNone(cg.cgroup2_root("/tmp/elsewhere"))
            self.assertEqual(cg.cgroup2_root("/mnt/with space/a"), "/mnt/with space")

    def test_G4_a_malformed_mountinfo_line_is_skipped_and_never_an_index_error(self):
        info = ("\n"
                "garbage\n"
                "36 35 0:30 / /sys/fs/cgroup rw\n"  # no ' - ' part
                "36 35 - cgroup2 cgroup2 rw\n"  # too few fields before it
                "36 35 0:30 / /sys/fs/cgroup rw - \n"  # nothing after it
                " - \n"
                "36 35 0:30 / /sys/fs/cgroup rw -cgroup2\n"
                "41 35 0:32 / /mnt/ok rw - cgroup2 cgroup2 rw\n")
        with mock.patch.object(cg, "_mountinfo", return_value=info):
            self.assertEqual(cg._mounts(), [("/mnt/ok", "cgroup2", "/")], "only the well-formed line is a mount")
            self.assertIsNone(cg.cgroup2_root("/sys/fs/cgroup/x"))

    def test_G9_a_cgroup2_that_is_mounted_as_a_subtree_is_no_root_and_the_runner_fails_closed(self):
        subtree = "36 35 0:30 /user.slice/box /sys/fs/cgroup rw - cgroup2 cgroup2 rw\n"
        with mock.patch.object(cg, "_mountinfo", return_value=subtree):
            self.assertIsNone(cg.cgroup2_root("/sys/fs/cgroup/x"), "the paths of /proc/<pid>/cgroup would not be relative to this mount point")

    def test_G9_a_directory_under_no_cgroup2_mount_is_reported_as_exactly_that_before_and_after_the_spawn(self):
        outside = Folder(self, {"cgroup.controllers": "memory\n", "cgroup.procs": ""}).dir
        with self.assertRaises(rs_scope.CgroupInvalid) as raised:
            rs_scope.ScopeLifecycle(outside).check_free()
        self.assertIn("is not under a cgroup2 mount", str(raised.exception))
        self.assertNotIn("no cgroup.controllers", str(raised.exception), "the controllers file is there: that is not what is wrong")
        box = subprocess.Popen(["sleep", "30"])
        self.addCleanup(lambda: (box.kill(), box.wait()))
        reason = rs_scope.ScopeLifecycle(outside, box=True).mismatch(box.pid)
        self.assertIn("is not under a cgroup2 mount", reason)
        self.assertNotIn("it is in", reason, "not a mismatch with an empty root")
        bare = Folder(self, {"cgroup.procs": ""}).dir
        self.assertIn("no cgroup.controllers", self.refusal(bare), "and a directory with no controllers file keeps saying so")

    def refusal(self, directory):
        try:
            rs_scope.ScopeLifecycle(directory).check_free()
        except rs_scope.CgroupInvalid as refused:
            return str(refused)
        return ""

    def test_E3_a_path_with_dots_or_a_link_is_resolved_before_it_is_compared(self):
        with mock.patch.object(cg, "_proc_cgroup", return_value="0::/a/b\n"), mock.patch.object(cg, "_mountinfo", return_value="1 0 0:1 / /sys/fs/cgroup rw - cgroup2 c rw\n"):
            self.assertTrue(cg.in_cgroup(1, "/sys/fs/cgroup/a/x/../b"))
            self.assertFalse(cg.in_cgroup(1, "/sys/fs/cgroup/a/b/../c"))

    def test_cgroup_kill_is_opened_and_never_created(self):
        folder = Folder(self).dir
        with self.assertRaises(ex.CleanupFailed):
            cg.kill(folder)
        self.assertFalse(os.path.exists(os.path.join(folder, "cgroup.kill")), "a file made by the runner is no kill switch")


class StaleCounterTests(unittest.TestCase):
    """N3: an existing cgroup keeps its counters. The runner refuses non-zero ones before the spawn (exit 3) rather than judge a case by a
    counter an earlier case left."""

    def refused(self, files):
        ws = Workspace(self, "touch started.marker\n", FILES)
        cgroup = fake_cgroup(ws, files=files)
        with fake_cgroup2(), self.assertRaises(rs_scope.CgroupBusy) as raised:
            ws.run(case(expected=b""), cgroup=cgroup, real=True)
        self.assertFalse(os.path.exists(os.path.join(ws.dir, "started.marker")), "nothing was started")
        return str(raised.exception)

    def test_An_oom_kill_left_by_an_earlier_case_is_refused_and_does_not_become_a_memory_limit(self):
        self.assertIn("oom_kill", self.refused({"cgroup.controllers": "", "memory.events": "oom 1\noom_kill 1\n"}))

    def test_A_non_zero_peak_left_by_an_earlier_case_is_refused(self):
        self.assertIn("memory.peak", self.refused({"cgroup.controllers": "", "memory.peak": "4096"}))

    def test_Zero_counters_and_absent_counters_are_fine(self):
        for files in ({"cgroup.controllers": "", "memory.peak": "0", "memory.events": "oom_kill 0\n"}, {"cgroup.controllers": ""}):
            ws = Workspace(self, "cat answer.bin\n", FILES)
            with fake_cgroup2():
                rs_scope.ScopeLifecycle(fake_cgroup(ws, files=files)).check_free()

    def test_The_counters_are_read_through_one_function_that_names_what_it_found(self):
        self.assertEqual(cg.stale_counters(Folder(self, {"memory.peak": "9", "memory.events": "oom_kill 2\n"}).dir), ["memory.peak is 9", "oom_kill is 2"])
        self.assertEqual(cg.stale_counters(Folder(self, {"memory.peak": "0", "memory.events": "oom_kill 0\n"}).dir), [])
        self.assertEqual(cg.stale_counters(Folder(self).dir), [])


class EmptinessTests(unittest.TestCase):
    """N7: what cannot be read is alive, never dead; `populated` in cgroup.events decides when it is there."""

    def test_A_pid_of_zero_or_a_pid_whose_stat_cannot_be_read_is_alive_and_the_cgroup_is_not_empty(self):
        for text in ("0\n", "4194303\n"):
            self.assertFalse(cg.empty(Folder(self, {"cgroup.procs": text}).dir), text)

    def test_Only_a_stat_that_says_zombie_is_dead(self):
        self.assertFalse(cg.is_zombie(os.getpid()))
        self.assertFalse(cg.is_zombie(0))
        self.assertFalse(cg.is_zombie(4194303))
        corpse = subprocess.Popen(["true"])
        self.addCleanup(corpse.wait)
        self.assertTrue(wait_until(lambda: cg.is_zombie(corpse.pid)))

    def test_populated_zero_in_cgroup_events_is_empty_whatever_the_listing_says_and_populated_one_is_not(self):
        self.assertTrue(cg.empty(Folder(self, {"cgroup.events": "populated 0\nfrozen 0\n", "cgroup.procs": f"{os.getpid()}\n"}).dir))
        self.assertFalse(cg.empty(Folder(self, {"cgroup.events": "populated 1\nfrozen 0\n", "cgroup.procs": ""}).dir))
        self.assertTrue(cg.empty(Folder(self, {"cgroup.events": "garbage\n", "cgroup.procs": ""}).dir), "an events file that cannot be read falls back to the listing")


class BoxPathTests(unittest.TestCase):
    """F3: the box must be IN the named cgroup, not in a descendant of it; and an existing cgroup is free only when `populated` says 0."""

    def lifecycle(self, observed, directory="/sys/fs/cgroup/run/box.scope"):
        life = rs_scope.ScopeLifecycle(directory, box=True)
        box = self.box = subprocess.Popen(["sleep", "30"])  # a real process: its pidfd is what the judgement pins first
        self.addCleanup(lambda: (box.kill(), box.wait()))  # this box, not the last one a second call put in self.box
        patches = (mock.patch.object(cg, "_proc_cgroup", return_value=f"0::{observed}\n"), mock.patch.object(cg, "is_cgroup2", return_value=True),
                   mock.patch.object(cg, "_mountinfo", return_value="1 0 0:1 / /sys/fs/cgroup rw - cgroup2 c rw\n"))
        for patch in patches:
            patch.start()
            self.addCleanup(patch.stop)
        return life

    def test_A_box_in_a_strict_descendant_of_the_named_cgroup_is_a_mismatch(self):
        self.assertIsNone(self.lifecycle("/run/box.scope").mismatch(self.box.pid))
        reason = self.lifecycle("/run/box.scope/sub").mismatch(self.box.pid)
        self.assertIn("is not in the cgroup", reason)
        self.assertIn("/run/box.scope/sub", reason, "E8: the observed path is named")

    def test_A_box_in_the_parent_or_a_neighbour_is_a_mismatch_too(self):
        for observed in ("/run", "/run/box.scope2", "/run/other.scope"):
            self.assertIsNotNone(self.lifecycle(observed).mismatch(self.box.pid), observed)

    def test_F5_the_pidfd_of_child_pid_is_opened_before_membership_is_judged_and_the_box_is_stopped_through_that_pidfd(self):
        box = subprocess.Popen(["sleep", "30"])
        self.addCleanup(lambda: (box.kill(), box.wait()))
        life = self.lifecycle("/run/elsewhere")
        order, real_open = [], ex.pidfd_open

        def opening(pid):
            order.append("pidfd_open")
            return real_open(pid)

        def judging(pid, directory):
            order.append("in_cgroup")
            return False

        launcher = subprocess.Popen(["sleep", "30"], start_new_session=True)
        self.addCleanup(lambda: (launcher.kill(), launcher.wait()))
        reader = type("R", (), {"child_pid": box.pid})()
        with mock.patch.object(ex, "pidfd_open", opening), mock.patch.object(cg, "in_cgroup", judging), \
                mock.patch.object(os, "kill", side_effect=AssertionError("os.kill: a pid, not a pidfd")):
            with self.assertRaises(rl.LaunchFailed):
                rl._confirm_box(launcher, reader, life, "s03", os.devnull)
        self.assertEqual(order, ["pidfd_open", "in_cgroup"], "a pid that is judged first may be someone else's by the time it is signalled")
        self.assertTrue(wait_until(lambda: box.poll() is not None), "the box was killed through its pidfd")

    def test_populated_one_in_cgroup_events_makes_an_existing_cgroup_busy_whatever_cgroup_procs_says(self):
        ws = Workspace(self, "cat answer.bin\n", FILES)
        busy = fake_cgroup(ws, files={"cgroup.events": "populated 1\n"})
        with fake_cgroup2(), self.assertRaises(rs_scope.CgroupBusy):
            rs_scope.ScopeLifecycle(busy).check_free()

    def test_populated_zero_makes_it_free_even_when_a_zombie_is_still_listed(self):
        ws = Workspace(self, "cat answer.bin\n", FILES)
        corpse = subprocess.Popen(["true"])
        self.addCleanup(corpse.wait)
        self.assertTrue(wait_until(lambda: cg.is_zombie(corpse.pid)))
        free = fake_cgroup(ws, files={"cgroup.events": "populated 0\n", "cgroup.procs": f"{corpse.pid}\n"})
        with fake_cgroup2():
            rs_scope.ScopeLifecycle(free).check_free()


class MembershipTests(unittest.TestCase):
    def test_A_pid_belongs_to_the_cgroup_or_a_descendant_of_it_and_not_to_a_neighbour_with_the_same_prefix(self):
        with mock.patch.object(cg, "_proc_cgroup", return_value="0::/user.slice/app.slice/box.scope/prog\n"), \
                mock.patch.object(cg, "_mountinfo", return_value="1 0 0:1 / /sys/fs/cgroup rw - cgroup2 c rw\n"):
            self.assertTrue(cg.member_of(1, "/sys/fs/cgroup/user.slice/app.slice/box.scope"))
            self.assertTrue(cg.member_of(1, "/sys/fs/cgroup/user.slice/app.slice/box.scope/prog"))
            self.assertFalse(cg.member_of(1, "/sys/fs/cgroup/user.slice/app.slice/box.sco"))
            self.assertFalse(cg.member_of(1, "/sys/fs/cgroup/user.slice/app.slice/other.scope"))
        with mock.patch.object(cg, "_proc_cgroup", return_value=None), mock.patch.object(cg, "_mountinfo", return_value="1 0 0:1 / /sys/fs/cgroup rw - cgroup2 c rw\n"):
            self.assertIsNone(cg.member_of(1, "/sys/fs/cgroup/x"), "a process that is gone belongs to nobody the runner can tell")

    def test_Memory_is_unknown_when_no_process_of_the_box_was_ever_seen_in_the_cgroup(self):
        ws = Workspace(self, "cat answer.bin\n", FILES)
        cgroup = fake_cgroup(ws, files={"memory.peak": "0", "memory.events": "oom_kill 0\n"})
        ws.write(ws.dir, "run.sh", f'printf "oom_kill 1\\n" > "{cgroup}/memory.events"\ncat answer.bin\n')
        with fake_cgroup2(box_is_inside=None):
            result = ws.run(case(), prefix=box_prefix(fake_box(self)), status_fd=STATUS_FD, cgroup=cgroup, real=True)
        self.assertEqual(result.status, "PASS", "a counter of a cgroup the box was never seen in is not this case's: no memory limit")
        self.assertEqual(result.lifecycle["memory"]["known"], False)
        self.assertNotIn("oomKill", result.lifecycle["memory"])


def foreign_cgroup(test, holder_seconds=40):
    """A real, empty cgroup2 node with the memory controller (its parent is a delegated scope held alive by a sleeper): a cgroup the program
    of a case does NOT run in."""
    owner = Scope(test)
    holder = subprocess.Popen(["systemd-run", "--user", "--scope", "--quiet", "--collect", f"--unit={owner.unit}", "-p", "Delegate=yes", "--", "sleep", str(holder_seconds)])
    test.addCleanup(holder.wait)
    test.addCleanup(owner.stop)  # runs first: the sleeper goes with its scope instead of being waited for
    test.assertTrue(wait_until(lambda: os.path.isdir(owner.cgroup)))
    test.assertTrue(wait_until(lambda: any(cg.comm_of(pid) == "sleep" for pid in cg.procs(owner.cgroup) or [])), "the sleeper has replaced systemd-run in the scope")
    hold = os.path.join(owner.cgroup, "hold")
    os.mkdir(hold)  # the scope itself may hold no process once it has controllers for its children: the sleeper moves down
    with open(owner.cgroup + "/cgroup.procs") as f:
        for pid in f.read().split():
            with open(hold + "/cgroup.procs", "w") as out:
                out.write(pid)
    with open(owner.cgroup + "/cgroup.subtree_control", "w") as f:
        f.write("+memory")
    other = os.path.join(owner.cgroup, "other")
    os.mkdir(other)
    return other


class PlainForeignCgroupTests(unittest.TestCase):
    """F1: on the plain path (no box, no child-pid) a cgroup the program does not run in must not give `memory {known: true, peakBytes: 0}`."""

    def test_A_cgroup_the_launcher_does_not_run_in_gives_unknown_memory_and_not_a_zero_peak(self):
        require_scope(self)
        other = foreign_cgroup(self)
        ws = Workspace(self, "python3 -c 'x = bytearray(100 * 1024 * 1024)'\ncat answer.bin\n", FILES)
        result = ws.run(case(), cgroup=other, real=True)
        self.assertEqual(result.status, "PASS")
        self.assertEqual(result.lifecycle["memory"]["known"], False, f"absent is not zero: {result.lifecycle['memory']}")
        self.assertNotIn("peakBytes", result.lifecycle["memory"])
        self.assertIn("  memory: not certified", result.lines)

    def test_On_the_plain_path_a_cgroup_the_launcher_is_seen_in_keeps_its_memory(self):
        ws = Workspace(self, "cat answer.bin\n", FILES)
        cgroup = fake_cgroup(ws, final={"memory.peak": "4096", "memory.events": "oom_kill 0\n"})
        result = ws.run(case(), cgroup=cgroup)  # Workspace.run lets the fake folder pass for the launcher's own cgroup
        self.assertEqual((result.lifecycle["memory"]["known"], result.lifecycle["memory"]["peakBytes"]), (True, 4096))


class WrongCgroupTests(unittest.TestCase):
    """N2: a cgroup that exists, is empty and is NOT the box's own (the reviewer's wrong_cg scenario, on real cgroups)."""

    def test_A_box_that_is_not_in_the_named_cgroup_is_a_launch_failure_and_nothing_is_left_alive(self):
        seconds = "55.%d" % (os.getpid() % 1000 + 1000)
        scope, owner = Scope(self), Scope(self)
        holder = subprocess.Popen(["systemd-run", "--user", "--scope", "--quiet", "--collect", f"--unit={owner.unit}", "-p", "Delegate=yes", "--", "sleep", "40"])
        self.addCleanup(holder.wait)
        self.addCleanup(owner.stop)
        self.assertTrue(wait_until(lambda: os.path.isdir(owner.cgroup)), "the delegated scope that owns the wrong cgroup")
        wrong = os.path.join(owner.cgroup, "wrong")
        os.mkdir(wrong)  # a real, empty cgroup2 node: it passes every check of the directory itself
        ws = Workspace(self, f"exec sleep {seconds}\n")
        holder_file = os.path.join(ws.root, "holder.pid")
        with self.assertRaises(rl.LaunchFailed) as raised:
            ws.run(case(expected=b""), prefix=scope.prefix(ws.dir, status_fd=STATUS_FD, holder=holder_file), status_fd=STATUS_FD, cgroup=wrong,
                   wrapper_file=holder_file, real=True)
        self.assertIn("not in the cgroup", raised.exception.reason)
        self.assertIn(wrong, raised.exception.reason, "E8: it names the cgroup that was expected")
        self.assertIn(scope.unit, raised.exception.reason, "E8: and the one the box was seen in, from /proc/<child-pid>/cgroup")
        self.assertEqual(raised.exception.stderr.strip(), "", "even though the prefix wrote nothing on standard error")
        self.assertTrue(wait_until(lambda: sleepers(seconds) == []), f"alive after the refusal: {sleepers(seconds)}")
        self.assertTrue(wait_until(lambda: scope.pids() == []), f"left in the box's own scope: {scope.pids()}")

    def test_The_wrong_cgroup_is_never_written_to(self):
        seconds = "56.%d" % (os.getpid() % 1000 + 1000)
        scope = Scope(self)
        ws = Workspace(self, f"exec sleep {seconds}\n")
        wrong = fake_cgroup(ws, "wrong", files={"cgroup.kill": "untouched", "cgroup.controllers": ""})
        holder_file = os.path.join(ws.root, "holder.pid")
        with mock.patch.object(cg, "is_cgroup2", return_value=True), mock.patch.object(cg, "cgroup2_root", return_value="/"):
            with self.assertRaises(rl.LaunchFailed):  # the folder passes for a node; the box's real cgroup is another path
                ws.run(case(expected=b""), prefix=scope.prefix(ws.dir, status_fd=STATUS_FD, holder=holder_file), status_fd=STATUS_FD, cgroup=wrong,
                       wrapper_file=holder_file, real=True)
        with open(os.path.join(wrong, "cgroup.kill")) as f:
            self.assertEqual(f.read(), "untouched", "the kill of a case never goes to a cgroup that is not the box's")
        self.assertTrue(wait_until(lambda: sleepers(seconds) == []))


if __name__ == "__main__":
    unittest.main()
