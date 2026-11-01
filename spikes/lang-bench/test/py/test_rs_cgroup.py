"""What the runner reads from the cgroup the layer names (E1.S8a part ii-a; B1, B4): memory.peak, memory.events, cgroup.procs, wrapper.pid,
and the kill by `cgroup.kill`. Fake files stand in for the cgroup here; the real scope is in test_rs_scope.py."""
import os
import subprocess
import tempfile
import unittest

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
    """A real process whose /proc comm is `name` (a link to sleep under that name), stopped when the test ends."""
    holder = tempfile.TemporaryDirectory()
    test.addCleanup(holder.cleanup)
    link = os.path.join(holder.name, name)
    os.symlink("/usr/bin/sleep", link)
    proc = subprocess.Popen([link, "30"])
    test.addCleanup(lambda: (proc.kill(), proc.wait()))
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

    def test_A_file_that_cannot_be_read_is_unknown_and_never_zero(self):
        for files in ({}, {"memory.peak": "5"}, {"memory.events": "oom_kill 0"}, {"memory.peak": "x", "memory.events": "oom_kill 0"},
                      {"memory.peak": "5", "memory.events": "oom_kill many"}, {"memory.peak": "5", "memory.events": ""}):
            record = cg.memory(Folder(self, files).dir)
            self.assertEqual(record["known"], False, files)
            self.assertIn("why", record)
            self.assertNotIn("peakBytes", record)


class ProcsTests(unittest.TestCase):
    def test_The_pids_of_cgroup_procs_are_read_in_order(self):
        self.assertEqual(cg.procs(Folder(self, {"cgroup.procs": "12\n7\n300\n"}).dir), [12, 7, 300])

    def test_An_unreadable_cgroup_procs_is_none_and_an_empty_one_is_an_empty_list(self):
        self.assertIsNone(cg.procs(Folder(self).dir))
        self.assertEqual(cg.procs(Folder(self, {"cgroup.procs": ""}).dir), [])

    def test_The_program_is_what_is_neither_bwrap_nor_the_wrapper_nor_gone(self):
        program, box, wrapper = sleeper(self), sleeper(self, "bwrap"), sleeper(self, "wrapper-sh")
        folder = Folder(self, {"cgroup.procs": f"{box}\n{wrapper}\n{program}\n4194303\n"})
        self.assertEqual(cg.program_candidates(folder.dir, wrapper), [program])
        self.assertEqual(cg.program_candidates(folder.dir, None), [wrapper, program], "without a wrapper pid the wrapper is a candidate")
        self.assertIsNone(cg.program_candidates(Folder(self).dir))

    def test_The_wrapper_pid_is_read_from_the_out_directory_when_the_file_is_there(self):
        self.assertEqual(cg.wrapper_pid(Folder(self, {"wrapper.pid": "812\n"}).dir), 812)
        self.assertIsNone(cg.wrapper_pid(Folder(self).dir))
        self.assertIsNone(cg.wrapper_pid(Folder(self, {"wrapper.pid": "not a pid"}).dir))
        self.assertIsNone(cg.wrapper_pid(None))


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
