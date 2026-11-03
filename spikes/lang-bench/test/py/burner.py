"""CPU burners for the load tests (W1/N7, fix batch 4). A burner is never an unbounded process: it ends at its bound (60 s at most), it carries a death signal tied to the process that started it
(`PR_SET_PDEATHSIG` = SIGKILL, set before anything else, and a check that its parent is still that process: the parent may have died before the signal was set), and its argv[0] is an owned
name (`testsToDelete-<owner>-bench-burner-<parent pid>-<n>`), so a person or a janitor can tell what it is and whose. A test starts burners only through `spawn`."""
import os
import subprocess
import sys

MAX_SECONDS = 60

SCRIPT = """
import ctypes, os, signal, sys, time
parent, seconds = int(sys.argv[1]), min(float(sys.argv[2]), %d)
ctypes.CDLL(None, use_errno=True).prctl(1, signal.SIGKILL)  # PR_SET_PDEATHSIG: the kernel kills this process when its parent dies
os.nice(10)  # the burner runs at a nice level below the process under test: it is load on the CPUs, not priority over the program (the suite's own workers run at the lowered level of `run-tests.mjs`)
if os.getppid() != parent:  # the parent died (or never was) before the signal was set: it is not this burner's parent, so it does not burn
    sys.exit(0)
end = time.monotonic() + seconds
while time.monotonic() < end:
    pass
""" % MAX_SECONDS


def count(cpus):
    """How many burners: one per CPU, at least two and at most eight."""
    return max(2, min(8, cpus if cpus is not None else (os.cpu_count() or 2)))


def spawn(test, number, seconds=MAX_SECONDS):
    """`number` burners owned by this process and bound to `seconds` (never more than `MAX_SECONDS`); each is killed and reaped when `test` ends."""
    seconds = min(seconds, MAX_SECONDS)
    owner = f"u{os.getuid()}"
    procs = [subprocess.Popen([f"testsToDelete-{owner}-bench-burner-{os.getpid()}-{n}", "-I", "-c", SCRIPT, str(os.getpid()), str(seconds)],
                              executable=sys.executable, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL) for n in range(number)]
    test.addCleanup(lambda: [(p.kill(), p.wait()) for p in procs])
    return procs
