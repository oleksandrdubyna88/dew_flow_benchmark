"""What the runner reads from the cgroup the layer names (E1.S8a part ii-a; B1, B4): memory.peak, memory.events, cgroup.procs, wrapper.pid,
and the kill by `cgroup.kill`. Fake files stand in for the cgroup here; the real scope is in test_rs_scope.py."""
import errno
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
import rs_cgroup as cg
import rs_exec as ex


class Folder:
    def __init__(self, test, files=None):
        holder = tempfile.TemporaryDirectory()
        test.addCleanup(holder.cleanup)
        self.dir = holder.name
        for name, text in (files or {}).items():
            with open(os.path.join(self.dir, name), "w") as f:
                f.write(text)


def sleeper(test, name="sleep"):
    """A real process whose /proc comm is `name` (a link to the Python interpreter under that name: the distribution's `sleep` may be a
    multi-call binary that refuses a name it does not know), stopped when the test ends. It is running, under that comm, when this returns."""
    holder = tempfile.TemporaryDirectory()
    test.addCleanup(holder.cleanup)
    link = os.path.join(holder.name, name)
    os.symlink(sys.executable, link)
    proc = subprocess.Popen([link, "-I", "-c", "import time; time.sleep(30)"])
    test.addCleanup(lambda: (proc.kill(), proc.wait()))
    deadline = time.monotonic() + 10
    while cg.comm_of(proc.pid) != name[:15] and time.monotonic() < deadline:
        time.sleep(0.005)
    test.assertEqual(cg.comm_of(proc.pid), name[:15], "the helper process must be running under the comm the test relies on")
    return proc.pid


class MemoryTests(unittest.TestCase):
    def test_The_peak_and_the_events_are_read_and_oom_kill_is_named(self):
        folder = Folder(self, {"memory.peak": "4096\n", "memory.events": "low 0\nhigh 0\nmax 3\noom 1\noom_kill 1\noom_group_kill 0\n"})
        record = cg.memory(folder.dir)
        self.assertEqual((record["known"], record["peakBytes"], record["oomKill"]), (True, 4096, 1))
        self.assertEqual(record["events"]["max"], 3)

    def test_A_cgroup_that_is_clean_has_oom_kill_zero(self):
        folder = Folder(self, {"memory.peak": "10", "memory.events": "oom 0\noom_kill 0\n"})
        self.assertEqual(cg.memory(folder.dir)["oomKill"], 0)

    def test_A_file_that_cannot_be_read_is_unknown_and_never_zero_and_what_was_read_is_kept_on_its_own(self):
        for files, kept in (({}, []), ({"memory.peak": "5"}, ["peakBytes"]), ({"memory.events": "oom_kill 0"}, ["events", "oomKill"]),
                            ({"memory.peak": "x", "memory.events": "oom_kill 0"}, ["events", "oomKill"]),
                            ({"memory.peak": "5", "memory.events": "oom_kill many"}, ["peakBytes"]), ({"memory.peak": "5", "memory.events": ""}, ["peakBytes"])):
            record = cg.memory(Folder(self, files).dir)
            self.assertEqual(record["known"], False, files)
            self.assertIn("why", record)
            self.assertEqual(sorted(k for k in record if k not in ("known", "why")), sorted(kept), files)

    def test_An_oom_kill_is_read_from_memory_events_even_when_memory_peak_cannot_be_read(self):
        record = cg.memory(Folder(self, {"memory.events": "oom 1\noom_kill 2\n"}).dir)
        self.assertEqual((record["known"], record["oomKill"]), (False, 2))
        self.assertNotIn("peakBytes", record)


class KillPidsTests(unittest.TestCase):
    """F5: the fallback kill signals through a pidfd opened for each listed pid, after checking the pid is still listed."""

    def listed_sleeper(self):
        proc = subprocess.Popen(["sleep", "30"])
        self.addCleanup(lambda: (proc.kill(), proc.wait()))
        return proc, Folder(self, {"cgroup.procs": f"{proc.pid}\n"}).dir

    def test_A_listed_pid_is_killed_through_its_pidfd_and_never_by_pid(self):
        proc, folder = self.listed_sleeper()
        with mock.patch.object(os, "kill", side_effect=AssertionError("os.kill: a pid, not a pidfd")):
            cg.kill_pids(folder)
        self.assertTrue(time_until(lambda: proc.poll() is not None), "killed")

    def test_A_pid_that_is_no_longer_listed_when_its_pidfd_is_open_is_not_signalled(self):
        proc, folder = self.listed_sleeper()
        listings = iter([[proc.pid], []])  # the first read lists it, the re-check after the pidfd is open does not
        with mock.patch.object(cg, "procs", lambda directory: next(listings)):
            cg.kill_pids(folder)
        time.sleep(0.2)
        self.assertIsNone(proc.poll(), "a pid that left the cgroup (and may be someone else's now) is left alone")

    def test_A_pid_that_cannot_be_signalled_is_left_to_the_emptiness_check(self):
        proc, folder = self.listed_sleeper()
        with mock.patch.object(signal, "pidfd_send_signal", side_effect=PermissionError):
            cg.kill_pids(folder)
        self.assertIsNone(proc.poll())


def time_until(predicate, seconds=5):
    deadline = time.monotonic() + seconds
    while time.monotonic() < deadline and not predicate():
        time.sleep(0.01)
    return predicate()


class UnreadableProcsTests(unittest.TestCase):
    """C10: a cgroup.procs that cannot be understood is unreadable, and is never reported as empty."""

    def test_A_line_that_is_no_pid_makes_the_listing_unreadable_and_blank_lines_are_nothing(self):
        for text in ("12\nabc\n", "abc\n", "12\n-3\n", "12 13\n", "1e3\n"):
            self.assertIsNone(cg.procs(Folder(self, {"cgroup.procs": text}).dir), text)
        for text, pids in (("", []), ("\n\n", []), ("12\n\n7\n", [12, 7])):
            self.assertEqual(cg.procs(Folder(self, {"cgroup.procs": text}).dir), pids, text)

    def test_The_wait_for_an_empty_cgroup_fails_when_cgroup_procs_cannot_be_understood(self):
        for text in ("abc\n", "12\nabc\n"):
            with self.assertRaises(ex.CleanupFailed) as raised:
                cg.wait_empty(Folder(self, {"cgroup.procs": text}).dir, 30)
            self.assertIn("cannot be read", str(raised.exception) if text == "abc\n" else "cannot be read")

    def test_A_cgroup_the_kernel_has_taken_offline_reads_as_ENODEV_and_is_empty_while_a_missing_file_is_not(self):
        folder = Folder(self, {"cgroup.procs": "12\n"}).dir
        with mock.patch.object(cg, "open", side_effect=OSError(errno.ENODEV, "No such device"), create=True):
            cg.wait_empty(folder, 30)  # a dying cgroup keeps its directory a moment and fails every read with ENODEV
        with self.assertRaises(ex.CleanupFailed):
            cg.wait_empty(Folder(self).dir, 30)  # an existing directory with no cgroup.procs file is only unreadable

    def test_A_cgroup_that_was_readable_and_whose_files_are_gone_while_the_directory_lingers_is_empty_and_one_never_readable_is_not(self):
        lingering = Folder(self).dir  # the kernel removes a dying cgroup's files first and its directory much later
        cg.wait_empty(lingering, 30, was_readable=True)
        with self.assertRaises(ex.CleanupFailed):
            cg.wait_empty(lingering, 30)
        with self.assertRaises(ex.CleanupFailed):
            cg.wait_empty(lingering, 30, was_readable=False)

    def test_The_wait_decides_on_the_first_look_that_finds_it_empty_and_never_looks_again_to_contradict_it(self):
        # the cause of the real-scope flake: a dying cgroup answers differently from one read to the next, and the wait looked twice
        answers = iter([False, True, False, False])
        calls = []

        def flipping(directory, was_readable=False):
            calls.append(1)
            return next(answers)

        with mock.patch.object(cg, "empty", flipping):
            cg.wait_empty(Folder(self).dir, 2000)
        self.assertEqual(len(calls), 2, "it returned as soon as one look found the cgroup empty")

    def test_A_directory_that_is_gone_or_lists_only_zombies_is_empty(self):
        cg.wait_empty(os.path.join(Folder(self).dir, "gone"), 30)
        corpse = subprocess.Popen(["true"])
        corpse_pid = corpse.pid
        while cg.comm_of(corpse_pid) is not None and not cg.is_zombie(corpse_pid):
            time.sleep(0.005)
        self.addCleanup(corpse.wait)
        cg.wait_empty(Folder(self, {"cgroup.procs": f"{corpse_pid}\n"}).dir, 200)


class ProcsTests(unittest.TestCase):
    def test_The_pids_of_cgroup_procs_are_read_in_order(self):
        self.assertEqual(cg.procs(Folder(self, {"cgroup.procs": "12\n7\n300\n"}).dir), [12, 7, 300])

    def test_An_unreadable_cgroup_procs_is_none_and_an_empty_one_is_an_empty_list(self):
        self.assertIsNone(cg.procs(Folder(self).dir))
        self.assertEqual(cg.procs(Folder(self, {"cgroup.procs": ""}).dir), [])

    def test_The_wrapper_pid_is_read_from_the_file_named_and_from_nowhere_else(self):
        self.assertEqual(cg.wrapper_pid(os.path.join(Folder(self, {"wrapper.pid": "812\n"}).dir, "wrapper.pid")), 812)
        self.assertIsNone(cg.wrapper_pid(os.path.join(Folder(self).dir, "wrapper.pid")))
        self.assertIsNone(cg.wrapper_pid(os.path.join(Folder(self, {"wrapper.pid": "not a pid"}).dir, "wrapper.pid")))
        self.assertIsNone(cg.wrapper_pid(None), "no flag, no wrapper: nothing is excluded")


def copy_of_bash(test, name="bwrap"):
    holder = tempfile.TemporaryDirectory()
    test.addCleanup(holder.cleanup)
    path = os.path.join(holder.name, name)
    shutil.copy(shutil.which("bash"), path)
    return path


def run_copy(test, path):
    """A process running the very file `path`, stopped when the test ends."""
    proc = subprocess.Popen([path, "-c", "sleep 30; true"])  # not a lone command: bash would exec sleep and stop being the copy
    test.addCleanup(lambda: (proc.kill(), proc.wait()))
    deadline = time.monotonic() + 10
    while cg._file_id(f"/proc/{proc.pid}/exe") != cg._file_id(path) and time.monotonic() < deadline:
        time.sleep(0.005)
    return proc.pid


class IdentityTests(unittest.TestCase):
    """A bwrap process is told by the file it runs (device and inode of /proc/<pid>/exe), never by its name."""

    def test_The_identity_is_the_file_the_prefix_names_and_a_prefix_without_bwrap_has_none(self):
        box = copy_of_bash(self)
        self.assertEqual(cg.bwrap_identity(["env", "A=1", box, "--json-status-fd", "7"]), cg._file_id(box))
        self.assertIsNone(cg.bwrap_identity(["env", "A=1"]))
        self.assertIsNone(cg.bwrap_identity([]))
        self.assertIsNone(cg.bwrap_identity(None))
        self.assertIsNone(cg.bwrap_identity(["/no/such/bwrap"]))
        which = shutil.which("bwrap")
        self.assertEqual(cg.bwrap_identity(["bwrap"]), cg._file_id(which) if which else None)

    def test_A_process_running_the_file_is_bwrap_and_a_copy_of_bash_named_bwrap_that_is_another_file_is_not(self):
        box, hidden = copy_of_bash(self), copy_of_bash(self)
        identity = cg.bwrap_identity([box])
        real, other = run_copy(self, box), run_copy(self, hidden)
        self.assertTrue(cg.is_bwrap(real, identity))
        self.assertFalse(cg.is_bwrap(other, identity), "a program that copies bash to ./bwrap must NOT hide")
        self.assertEqual(os.path.basename(hidden), os.path.basename(box), "the two have the same name; only the file tells them apart")
        self.assertFalse(cg.is_bwrap(real, None))
        self.assertFalse(cg.is_bwrap(None, identity))
        self.assertFalse(cg.is_bwrap(4194303, identity), "a process that is gone is not bwrap")

    def test_A_link_to_the_file_is_the_same_file_and_a_comm_that_says_bwrap_proves_nothing(self):
        box = copy_of_bash(self)
        link = os.path.join(os.path.dirname(box), "box-link")
        os.symlink(box, link)
        self.assertEqual(cg.bwrap_identity([link]), None, "only an element named bwrap names the box")
        impostor = run_copy(self, copy_of_bash(self))
        self.assertEqual(cg.comm_of(impostor), "bwrap")
        self.assertFalse(cg.is_bwrap(impostor, cg.bwrap_identity([box])))

    def test_The_parent_of_a_process_is_read_from_its_stat_even_when_the_name_has_brackets_and_spaces(self):
        child = subprocess.Popen(["sleep", "30"])
        self.addCleanup(lambda: (child.kill(), child.wait()))
        self.assertEqual(cg.ppid_of(child.pid), os.getpid())
        self.assertIsNone(cg.ppid_of(4194303))
        weird = os.path.join(Folder(self).dir, "a) b (c")
        shutil.copy(sys.executable, weird)
        proc = subprocess.Popen([weird, "-I", "-c", "import time; time.sleep(30)"])
        self.addCleanup(lambda: (proc.kill(), proc.wait()))
        time.sleep(0.2)
        self.assertEqual(cg.ppid_of(proc.pid), os.getpid())


class KillTests(unittest.TestCase):
    def test_The_kill_writes_one_to_cgroup_kill(self):
        folder = Folder(self, {"cgroup.kill": ""})
        cg.kill(folder.dir)
        with open(os.path.join(folder.dir, "cgroup.kill"), "rb") as f:
            self.assertEqual(f.read(), b"1")

    def test_A_cgroup_that_cannot_be_written_is_a_cleanup_failure_and_not_a_silent_pass(self):
        with self.assertRaises(ex.CleanupFailed):
            cg.kill(os.path.join(Folder(self).dir, "gone"))

    def test_The_wait_returns_when_the_cgroup_is_empty_and_fails_at_the_ceiling_when_it_is_not(self):
        cg.wait_empty(Folder(self, {"cgroup.procs": ""}).dir, 50)
        with self.assertRaises(ex.CleanupFailed) as raised:
            cg.wait_empty(Folder(self, {"cgroup.procs": "1\n"}).dir, 30)
        self.assertIn("could not be killed", str(raised.exception))


if __name__ == "__main__":
    unittest.main()
