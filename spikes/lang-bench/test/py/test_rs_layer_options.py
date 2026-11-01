"""The layer's options as the command line takes them (E1.S8a part ii-a): what is refused with exit 4 before anything runs, what the host must
have (exit 3, P1), a cgroup the case does not own (exit 3, P5), and the signals the runner stops on."""
import errno
import io
import json
import os
import signal
import subprocess
import sys
import time
import unittest
from unittest import mock

import runner_path  # noqa: F401
import rs_exec as ex
import run_samples as rs
from box_support import sleepers, wait_until
from test_run_samples import ENTRY, Box, read_json

PAIRS = {"s01.in": b"", "s01.out": b"", "s02.in": b"", "s02.out": b""}


def run_in_process(box, *args):
    """rs.run with its streams captured: (exit code, stdout, stderr)."""
    out, err, real = io.StringIO(), io.StringIO(), (sys.stdout, sys.stderr)
    sys.stdout, sys.stderr = out, err
    try:
        code = rs.run([box.samples, "--workspace", box.ws, *args])
    finally:
        sys.stdout, sys.stderr = real
    return code, out.getvalue(), err.getvalue()


class RefusedTests(unittest.TestCase):
    def test_A_cgroup_with_a_launch_prefix_but_no_launch_status_is_exit_4(self):
        box = Box(self)
        code, _, err = box.cli("--launch-prefix", json.dumps(["env"]), "--cgroup-of", os.path.join(box.root, "cg"))
        self.assertEqual(code, 4, err)
        self.assertIn("--cgroup-of with --launch-prefix needs --launch-status", err)

    def test_A_cgroup_without_a_prefix_stays_allowed_and_a_prefix_with_both_is_not_refused_by_this_rule(self):
        box = Box(self)
        cg = os.path.join(box.root, "cg")
        os.mkdir(cg)
        for name, text in (("memory.peak", "1"), ("memory.events", "oom_kill 0\n"), ("cgroup.procs", "")):
            with open(os.path.join(cg, name), "w") as f:
                f.write(text)
        self.assertEqual(box.cli("--cgroup-of", cg)[0], 0)

    def test_With_more_than_one_case_a_template_without_case_is_exit_4_for_each_of_the_three_flags(self):
        box = Box(self, pairs={**PAIRS})
        for flag in ("--cgroup-of", "--case-view"):
            code, _, err = box.cli(flag, os.path.join(box.root, "one-for-all"))
            self.assertEqual(code, 4, flag)
            self.assertIn(f"{flag} needs {{case}} in its template: 2 cases would share one", err)
        code, _, err = box.cli("--cgroup-of", os.path.join(box.root, "{case}"), "--wrapper-pid-file", os.path.join(box.root, "wrapper.pid"))
        self.assertEqual(code, 4)
        self.assertIn("--wrapper-pid-file needs {case}", err)

    def test_With_one_case_a_template_without_case_is_not_refused_for_that(self):
        box = Box(self)
        code, _, err = box.cli("--case-view", os.path.join(box.root, "views"))
        self.assertNotIn("needs {case}", err)

    def test_A_wrapper_pid_file_without_a_cgroup_is_exit_4(self):
        box = Box(self)
        code, _, err = box.cli("--wrapper-pid-file", os.path.join(box.root, "{case}.pid"))
        self.assertEqual(code, 4)
        self.assertIn("--wrapper-pid-file needs --cgroup-of", err)

    def test_A_sample_that_takes_the_case_id_of_the_empty_program_control_is_exit_4(self):
        box = Box(self, pairs={"empty-program.in": b"", "empty-program.out": b""})
        code, _, err = box.cli()
        self.assertEqual(code, 4, err)
        self.assertIn("'empty-program' is reserved", err)


class HostTests(unittest.TestCase):
    def test_A_missing_pidfd_open_is_exit_3_naming_it_and_the_runner_does_not_crash(self):
        box = Box(self)
        for failing in (mock.patch.object(os, "pidfd_open", side_effect=OSError(errno.ENOSYS, "no pidfd")), mock.patch.object(os, "pidfd_open", None)):
            with failing:
                code, out, err = run_in_process(box)
            self.assertEqual(code, 3, err)
            self.assertIn("os.pidfd_open is not available", err)
            self.assertNotIn("Traceback", err)
            self.assertEqual(out, "", "no verdict")

    def test_A_kernel_older_than_5_14_is_exit_3_naming_the_version(self):
        with mock.patch.object(rs.layer, "_kernel", return_value=(5, 13)):
            code, _, err = run_in_process(Box(self))
        self.assertEqual(code, 3)
        self.assertIn("Linux 5.14 or newer", err)

    def test_A_host_without_cgroup_v2_is_exit_3_only_when_a_cgroup_is_asked_for(self):
        box = Box(self)
        real_exists = os.path.exists
        with mock.patch.object(rs.layer.os.path, "exists", lambda p: False if p == "/sys/fs/cgroup/cgroup.controllers" else real_exists(p)):
            code, _, err = run_in_process(box, "--cgroup-of", os.path.join(box.root, "cg"))
            plain, _, _ = run_in_process(box)
        self.assertEqual(code, 3)
        self.assertIn("cgroup v2", err)
        self.assertEqual(plain, 0, "a plain run needs no cgroup")

    def test_A_host_that_has_everything_is_not_refused(self):
        self.assertEqual(rs.layer.host_problems(mock.Mock(cgroup_of=None)), [])


class OwnershipTests(unittest.TestCase):
    def test_A_cgroup_that_holds_a_foreign_process_before_the_spawn_is_exit_3_on_the_command_line_and_nothing_starts(self):
        foreign = subprocess.Popen(["sleep", "30"])
        self.addCleanup(lambda: (foreign.kill(), foreign.wait()))
        box = Box(self, run_sh="touch started.marker\n")
        cg = os.path.join(box.root, "cg")
        os.mkdir(cg)
        for name, text in (("memory.peak", "1"), ("memory.events", "oom_kill 0\n"), ("cgroup.procs", f"{foreign.pid}\n")):
            with open(os.path.join(cg, name), "w") as f:
                f.write(text)
        code, out, err = box.cli("--cgroup-of", cg)
        self.assertEqual(code, 3, err)
        self.assertIn("already holds a process", err)
        self.assertFalse(os.path.exists(os.path.join(box.ws, "started.marker")))
        self.assertEqual(foreign.poll(), None, "a process the case does not own is never killed")


class SignalTests(unittest.TestCase):
    """SIGHUP stops the runner like SIGTERM and SIGINT: exit 5, no verdict, nothing left."""

    def test_A_SIGHUP_to_the_runner_while_a_program_runs_is_exit_5_with_no_verdict_and_nothing_left(self):
        box = Box(self, run_sh="touch started.marker\nexec sleep 69.%d\n" % (os.getpid() % 1000 + 1000))
        box.write(box.root, "limits.json", '{"timeS": 120}')
        proc = subprocess.Popen([sys.executable, "-I", "-B", ENTRY, "--limits", os.path.join(box.root, "limits.json"), box.samples], cwd=box.ws,
                                stdout=subprocess.PIPE, stderr=subprocess.PIPE, env={"PATH": os.environ["PATH"], "TMPDIR": os.environ.get("TMPDIR", "/tmp")})
        self.addCleanup(lambda: proc.poll() is None and proc.kill())
        self.assertTrue(wait_until(lambda: os.path.exists(os.path.join(box.ws, "started.marker")), 15))
        proc.send_signal(signal.SIGHUP)
        out, err = proc.communicate(timeout=30)
        self.assertEqual((proc.returncode, out), (5, b""), err)
        self.assertIn(b"interrupted by SIGHUP", err)
        seconds = "69.%d" % (os.getpid() % 1000 + 1000)
        self.assertTrue(wait_until(lambda: sleepers(seconds) == []), "nothing of the case is left")


if __name__ == "__main__":
    unittest.main()
