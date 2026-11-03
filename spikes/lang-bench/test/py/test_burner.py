"""W1/N7 (fix batch 4): a test's CPU burner never outlives the test. Each burner has a bounded lifetime, a death signal tied to the process that started it (PR_SET_PDEATHSIG), a check that
the parent is still that process, and an owned argv marker; a test file that starts one does it only through `burner.spawn`. A run of `while True: pass` once outlived a killed test run by 51 minutes."""
import os
import re
import signal
import subprocess
import sys
import textwrap
import time
import unittest

import runner_path  # noqa: F401
import burner

HERE = os.path.dirname(os.path.abspath(__file__))


def alive(pid):
    try:
        with open(f"/proc/{pid}/stat") as f:
            return f.read().rsplit(")", 1)[1].split()[0] not in ("Z", "X")
    except OSError:
        return False


def burning(pid):
    """True once the process has used some CPU time: it is past its start-up checks and is really in its loop."""
    try:
        with open(f"/proc/{pid}/stat") as f:
            return int(f.read().rsplit(")", 1)[1].split()[11]) > 0
    except OSError:
        return False


def argv0_of(pid):
    """argv[0] of a live process, read again until the exec has happened (a process that is still starting shows an empty or the parent's command line for a moment)."""
    end = time.monotonic() + 10
    name = ""
    while time.monotonic() < end and not name.startswith("testsToDelete-"):
        time.sleep(0.02)
        try:
            with open(f"/proc/{pid}/cmdline", "rb") as f:
                name = f.read().split(b"\0")[0].decode()
        except OSError:
            name = ""
    return name


def still_alive(pids):
    return [pid for pid in pids if alive(pid)]


def wait_dead(pids, seconds):
    end = time.monotonic() + seconds
    left = still_alive(pids)
    while left and time.monotonic() < end:
        time.sleep(0.05)
        left = still_alive(left)
    return left


class BurnerTests(unittest.TestCase):
    def test_The_number_of_burners_is_at_most_eight_and_at_least_two(self):
        self.assertEqual(burner.count(1), 2)
        self.assertEqual(burner.count(4), 4)
        self.assertEqual(burner.count(64), 8)
        self.assertEqual(burner.count(None), 2 if (os.cpu_count() or 2) < 2 else min(8, os.cpu_count()))

    def test_A_burner_carries_an_owned_argv_marker_and_ends_by_itself_at_its_bound(self):
        procs = burner.spawn(self, 2, seconds=4)
        for proc in procs:
            self.assertRegex(argv0_of(proc.pid), r"^testsToDelete-[uw][0-9]+-bench-burner-", "the owned name: a janitor and a person can tell what it is")
        self.assertEqual(wait_dead([p.pid for p in procs], 30), [], "a burner stops at its bound even when its parent is alive and well (the wait is generous: a loaded host starts it late)")
        self.assertTrue(all(p.wait(timeout=1) is not None for p in procs))

    def test_The_default_bound_is_short(self):
        self.assertLessEqual(burner.MAX_SECONDS, 120, "a burner is never an unbounded process")

    def test_Burners_die_within_a_second_of_their_parent_being_killed_abruptly_and_that_is_the_point(self):
        script = textwrap.dedent(f"""
            import sys, time
            sys.path.insert(0, {HERE!r})
            import unittest, burner
            class Holder(unittest.TestCase):
                def runTest(self):
                    procs = burner.spawn(self, 3, seconds=100)
                    print(" ".join(str(p.pid) for p in procs), flush=True)
                    time.sleep(100)
            case = Holder()
            result = unittest.TestResult()
            case.run(result)
        """)
        parent = subprocess.Popen([sys.executable, "-I", "-c", script], stdout=subprocess.PIPE, text=True)
        self.addCleanup(lambda: (parent.kill(), parent.wait()))
        pids = [int(p) for p in parent.stdout.readline().split()]
        parent.stdout.close()
        self.assertEqual(len(pids), 3)
        end = time.monotonic() + 10
        while time.monotonic() < end and not all(burning(pid) for pid in pids):
            time.sleep(0.05)
        self.assertTrue(all(alive(pid) and burning(pid) for pid in pids), "they run, and burn, while the parent lives (past the start-up check that could end them early)")
        os.kill(parent.pid, signal.SIGKILL)
        parent.wait()
        left = wait_dead(pids, 2)
        for pid in left:
            os.kill(pid, signal.SIGKILL)
        self.assertEqual(left, [], "no burner outlives its killed parent: the death signal reaches it at once, long before its 100 s bound")

    def test_A_burner_started_after_its_parent_is_already_gone_exits_at_once(self):
        # the check in the burner: the parent it was told about is not its parent now (it was reparented), so it ends before it burns
        proc = subprocess.Popen([sys.executable, "-I", "-c", burner.SCRIPT, str(os.getpid() + 1_000_000), "60"], stdout=subprocess.DEVNULL)
        self.addCleanup(lambda: (proc.kill(), proc.wait()))
        self.assertEqual(wait_dead([proc.pid], 3), [], "the parent in its argv is not its parent: it exits without burning")


UNBOUNDED = (re.compile(r"""-c["']?\s*,?\s*["']?.*while\s+(True|1)\s*:"""), re.compile(r"sleep\s+[0-9]{4,}"))


def unbounded_lines(text):
    """`(line number, line)` of every line of `text` that starts a helper process with no end: a `-c` script that loops on `while True`, or a `sleep` of 1000 s or more. The ONE matcher both tests use."""
    return [(number, line.strip()[:100]) for number, line in enumerate(text.splitlines(), 1) if any(pattern.search(line) for pattern in UNBOUNDED)]


class NoUnboundedHelperTests(unittest.TestCase):
    """W1: no test file starts a process that has no end. A `while True` loop in a `-c` script, or a `sleep` of an hour or more, is refused here."""

    def test_No_python_test_starts_an_unbounded_helper_process(self):
        offenders = []
        for name in sorted(os.listdir(HERE)):
            if not name.endswith(".py") or name in ("test_burner.py", "burner.py"):
                continue
            with open(os.path.join(HERE, name), encoding="utf-8") as f:
                offenders += [f"{name}:{number}: {line}" for number, line in unbounded_lines(f.read())]
        self.assertEqual(offenders, [], "an unbounded helper (use `burner.spawn`, or a bounded sleep)")

    def test_The_scan_finds_a_planted_unbounded_helper_of_each_kind(self):
        planted = "\n".join([
            "burner = subprocess.Popen([sys.executable, '-I', '-c', 'while True: pass'])",
            'burner = subprocess.Popen([sys.executable, "-c", "while True: pass"])',
            'proc = subprocess.Popen(["bash", "-c", "sleep 3600"])',
            "fine = subprocess.Popen(['sleep', '30'])",
            "bounded = 'end = time.monotonic() + 60\\nwhile time.monotonic() < end: pass'",
        ])
        self.assertEqual([number for number, _ in unbounded_lines(planted)], [1, 2, 3], "the two loops and the long sleep are found; a bounded loop and a short sleep are not")


if __name__ == "__main__":
    unittest.main()
