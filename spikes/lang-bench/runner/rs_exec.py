"""rs_exec: what every program run shares: the build, the launch argv, the program's environment (V11), the process group of the plain
path, and the wait.

The execution layer (E1.S8b) hands the runner a launch prefix, a launch status descriptor, a cgroup and a case view; `rs_scope.py` is the
lifecycle that reads and kills through that cgroup, `PlainLifecycle` here is the one with no box. Wall time is `time.monotonic_ns()` and
every wait for an exit is `os.pidfd_open` + `select` with the exact remaining time, never `Popen.wait(timeout=...)`'s polling.
"""
import os
import re
import selectors
import signal
import subprocess
import time
from dataclasses import dataclass, field
from typing import NamedTuple, Optional

import rs_report

CAPTURE_CAP = 16 * 1024 * 1024
STDERR_KILL_BYTES = 256 * 1024 * 1024
BUILD_CEILING_MS = 600_000
FLOOR_MS = 1000
CHUNK = 65536
TICK_S = 0.05
TAIL_BYTES = 2048
GONE_CEILING_MS = 2000
TEST_CEILING_MS = 600_000
LAUNCH_CEILING_MS = 60_000
SEEK_MS = 1000
EMPTY_CASE = "empty-program"
RUN_PROGRAM = ("bash", "run.sh")
POLL_FIRST_S = 0.001
POLL_LAST_S = 0.05


class Program(NamedTuple):
    """The program's host pid (None when it could not be told), how it was found, and how many candidates there were."""
    pid: Optional[int]
    found: str
    count: Optional[int]


class Settled(NamedTuple):
    """What a case's end leaves: the program's status, the leftover finding (None: not checked) and its memory record."""
    code: int
    leftover: object
    memory: dict


class PlainLifecycle:
    """How the plain path (no `--cgroup-of`) finds leftovers, kills and reads memory: through the process group of the launcher."""
    road = {"kill": "killpg", "leftoverEvidence": "process group (killpg 0)"}

    def memory(self, case_id=None):
        return rs_report.NOT_CERTIFIED

    def present(self):
        """Whether what this lifecycle kills through exists (a process group always does)."""
        return True

    def kill(self, proc):
        kill_group(proc)

    def leftover(self, proc, program=None):
        """True when something of the program's process group is still alive after the program was reaped."""
        return group_present(proc)

    def program_of(self, proc):
        """No box, no cgroup: the launcher execs the program (D38), so the launcher's pid is the program's."""
        return Program(proc.pid, "launcher pid (no --cgroup-of)", 1)

    def settle(self, launched, end):
        """The case's end: kill what is still running, reap the launcher, then look for what the program left behind."""
        proc = launched.proc
        if end != "exited":
            self.kill(proc)
        code = launched.status_code(proc.wait())
        leftover = self.leftover(proc) if end == "exited" else None
        if leftover:
            self.kill(proc)
        return Settled(code, leftover, self.memory())


@dataclass
class Config:
    workspace: str
    scratch: str
    prefix: Optional[list] = None
    limit_ms: int = FLOOR_MS
    basis: str = "floor"
    capture_cap: int = CAPTURE_CAP
    lifecycle: object = field(default_factory=PlainLifecycle)
    status_fd: Optional[int] = None
    cgroup: Optional[str] = None
    case_view: Optional[str] = None
    out_dir: Optional[str] = None


class BuildResult(NamedTuple):
    ran: bool
    status: Optional[int] = None
    wall_ms: int = 0
    timed_out: bool = False
    tail: str = ""


def now_ms():
    return time.monotonic_ns() // 1_000_000


CASE_ID = re.compile(r"[A-Za-z0-9_-]+\Z")


def checked_case_id(case_id):
    """The case id, which is letters, digits, underscore and hyphen only: anything else raises ValueError before it can reach an argv, a
    path or a cgroup name (the plan refuses such samples with exit 4)."""
    if not CASE_ID.match(case_id):
        raise ValueError(f"case id {case_id!r} is not letters, digits, underscore and hyphen")
    return case_id


def launch_argv(prefix, case_id, program=RUN_PROGRAM):
    """The prefix with `{case}` filled in, then `bash run.sh` (the layer's box and scope come before it)."""
    checked_case_id(case_id)
    return [part.replace("{case}", case_id) for part in prefix or []] + list(program)


def program_env(extra=None):
    """V11: the program's environment is the runner's own (which the layer sets: D15's base plus the documented per-stack variables), plus
    `extra` (`PORT` and the scenario's `env` lines for the daemon and web kinds; nothing for an olympiad case). Nothing else is added or
    removed here: a copy taken at the launch, so a later change of the runner's own is not seen by a program already started."""
    return {**os.environ, **(extra or {})}


def signal_group(pid, sig):
    try:
        os.killpg(pid, sig)
    except ProcessLookupError:
        pass


def group_present(proc):
    """True while anything of the process group `proc` leads is alive (a reaped leader's group still counts)."""
    try:
        os.killpg(proc.pid, 0)
    except ProcessLookupError:
        return False
    return True


class CleanupFailed(RuntimeError):
    """The runner could not stop a process of the case: a failure of the runner's own cleanup, never a verdict about the program."""


def kill_group(proc):
    """SIGKILL to the whole group, reap the leader, then wait until nothing of the group is left: the signal is delivered at once but the
    children go a moment later. If the group is still there at the ceiling (2 s) the cleanup has failed: CleanupFailed."""
    signal_group(proc.pid, signal.SIGKILL)
    proc.wait()
    deadline, delay = now_ms() + GONE_CEILING_MS, POLL_FIRST_S
    while group_present(proc) and now_ms() < deadline:
        time.sleep(delay)
        delay = min(delay * 2, POLL_LAST_S)
    if group_present(proc):
        raise CleanupFailed(f"a process of the case could not be killed: its group {proc.pid} is still there after {GONE_CEILING_MS} ms")


def pidfd_open(pid):
    """A pollable descriptor that turns readable when the process `pid` exits, or None where the kernel has none or the process is gone."""
    try:
        return os.pidfd_open(pid)
    except (AttributeError, OSError):
        return None


def pidfd_of(proc):
    """The pidfd of a launcher not yet reaped (a reaped one's pid may belong to someone else by now), else None."""
    return pidfd_open(proc.pid) if proc.returncode is None else None


def wait_exit(proc, pidfd, deadline_ms):
    """True when the program exited before `deadline_ms` (on the monotonic clock), False at the deadline."""
    while True:
        remaining = deadline_ms - now_ms()
        if remaining <= 0:
            return proc.poll() is not None
        if _exited(proc, pidfd, remaining):
            return True


def _exited(proc, pidfd, remaining_ms):
    if pidfd is None:
        time.sleep(min(remaining_ms / 1000, TICK_S))
        return proc.poll() is not None
    with selectors.DefaultSelector() as sel:
        sel.register(pidfd, selectors.EVENT_READ)
        return bool(sel.select(remaining_ms / 1000))


def read_tail(path, limit=TAIL_BYTES):
    """The last `limit` bytes of a file as text that cannot raise, or ''."""
    try:
        with open(path, "rb") as f:
            f.seek(0, os.SEEK_END)
            f.seek(max(0, f.tell() - limit))
            return f.read().decode("utf-8", "backslashreplace")
    except OSError:
        return ""


def build(workspace, scratch, ceiling_ms=None):
    """Run `bash build.sh` in the workspace: output to a file in `scratch`, the whole group killed at the ceiling (BUILD_CEILING_MS)."""
    ceiling_ms = BUILD_CEILING_MS if ceiling_ms is None else ceiling_ms
    log = os.path.join(scratch, "build.log")
    started = now_ms()
    with open(log, "wb") as out:
        proc = subprocess.Popen(["bash", "build.sh"], cwd=workspace, stdin=subprocess.DEVNULL, stdout=out, stderr=out, start_new_session=True)
    pidfd = pidfd_of(proc)
    try:
        done = wait_exit(proc, pidfd, started + ceiling_ms)
    except BaseException:  # the runner itself is being stopped (a signal): never leave the build running
        kill_group(proc)
        raise
    finally:
        close_fd(pidfd)
    if not done:
        kill_group(proc)
    status = proc.wait()
    signal_group(proc.pid, signal.SIGKILL)
    return BuildResult(True, status, now_ms() - started, not done, read_tail(log))


def close_fd(fd):
    if fd is not None:
        os.close(fd)
