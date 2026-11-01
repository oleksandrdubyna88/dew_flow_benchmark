"""The runner stopped by a signal while a launcher starts or a case runs in a real scope (E1.S8a part ii-a): exit 5, no verdict, nothing left.
And the command line end to end through a real transient scope, bwrap's real status lines and the program's cgroup."""
import json
import os
import signal
import subprocess
import sys
import unittest

import runner_path  # noqa: F401
from box_support import STATUS_FD, Scope, sleepers, wait_until
from test_run_samples import ENTRY, Box, read_json


def run_cli(test, box, *args):
    proc = subprocess.Popen([sys.executable, "-I", "-B", ENTRY, *args, box.samples], cwd=box.ws, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
                            env={"PATH": os.environ["PATH"], "TMPDIR": os.environ.get("TMPDIR", "/tmp"), "HOME": os.environ.get("HOME", "/"),
                                 "XDG_RUNTIME_DIR": os.environ.get("XDG_RUNTIME_DIR", ""), "DBUS_SESSION_BUS_ADDRESS": os.environ.get("DBUS_SESSION_BUS_ADDRESS", "")})
    test.addCleanup(lambda: proc.poll() is None and proc.kill())
    return proc


class InterruptTests(unittest.TestCase):
    def test_A_SIGTERM_while_the_launcher_has_not_yet_reported_is_exit_5_and_the_launcher_is_not_left_running(self):
        box = Box(self)
        seconds = "68.%d" % (os.getpid() % 1000 + 1000)
        marker = os.path.join(box.root, "launching")
        stuck = ["sh", "-c", f"touch {marker}\nexec sleep {seconds}", "sh"]
        proc = run_cli(self, box, "--launch-prefix", json.dumps(stuck), "--launch-status", str(STATUS_FD), "--limits", self.limits(box))
        self.assertTrue(wait_until(lambda: os.path.exists(marker), 15), "the launcher never started")
        proc.send_signal(signal.SIGTERM)
        out, err = proc.communicate(timeout=30)
        self.assertEqual((proc.returncode, out), (5, b""), err)
        self.assertIn(b"interrupted", err)
        self.assertTrue(wait_until(lambda: not sleepers(seconds)), "the launcher was left running")

    def limits(self, box):
        box.write(box.root, "limits.json", '{"timeS": 120}')
        return os.path.join(box.root, "limits.json")


class RealScopeTests(unittest.TestCase):
    def cli_args(self, box, scope, out_json=None):
        box.write(box.root, "limits.json", '{"timeS": 30}')
        return ["--launch-prefix", json.dumps(scope.prefix(box.ws, per_case=True, status_fd=STATUS_FD)), "--launch-status", str(STATUS_FD),
                "--cgroup-of", scope.cgroup_template, "--limits", os.path.join(box.root, "limits.json"), *(["--json", out_json] if out_json else [])]

    def test_The_command_line_through_a_real_scope_and_bwrap_passes_and_records_what_it_saw(self):
        scope = Scope(self)
        box = Box(self)
        out_json = os.path.join(box.out, "r.json")
        proc = run_cli(self, box, *self.cli_args(box, scope, out_json))
        out, err = proc.communicate(timeout=120)
        self.assertEqual((proc.returncode, b"PASS s03" in out), (0, True), err)
        record = read_json(out_json)
        case = record["cases"][0]
        self.assertIsInstance(case["launchMs"], int)
        self.assertIsInstance(record["emptyProgramMs"], int)
        self.assertEqual(case["lifecycle"]["signalled"]["found"], "cgroup.procs: the process whose parent is a bwrap process")
        self.assertNotEqual(case["lifecycle"]["signalled"]["pid"], case["lifecycle"]["signalled"]["launcher"])
        self.assertIsInstance(record["emptyProgram"]["pid"], int, "the control waited on the program's own pid too")
        self.assertEqual((case["lifecycle"]["kill"], case["lifecycle"]["leftover"]), ("cgroup.kill", {"known": True, "found": False}))
        self.assertEqual(case["lifecycle"]["memory"]["known"], True)

    def test_A_SIGTERM_to_the_runner_while_a_program_runs_in_its_scope_is_exit_5_and_the_scope_is_empty(self):
        scope = Scope(self)
        box = Box(self, run_sh="touch started.marker\nexec sleep 66\n")
        proc = run_cli(self, box, *self.cli_args(box, scope))
        self.assertTrue(wait_until(lambda: os.path.exists(os.path.join(box.ws, "started.marker")), 30), "the case never started")
        proc.send_signal(signal.SIGTERM)
        out, err = proc.communicate(timeout=60)
        self.assertEqual((proc.returncode, out), (5, b""), err)
        self.assertTrue(wait_until(lambda: scope.pids() == []), f"left in the scope: {scope.pids()}")


if __name__ == "__main__":
    unittest.main()
