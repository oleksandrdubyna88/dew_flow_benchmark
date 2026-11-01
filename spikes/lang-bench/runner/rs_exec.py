"""rs_exec: running a program for one case on the plain path (no box, no cgroup): the build, the launch argv, the process group, the wait.

The execution layer (E1.S8b) hands the runner a launch prefix and a cgroup; part ii of this story replaces `PlainLifecycle` with one
that signals the program's host pid and kills through the scope. Everything the olympiad and the daemon paths share about a process
is here. Wall time is `time.monotonic_ns()` and the exit is awaited on a pidfd, never on `Popen.wait(timeout=...)`'s polling.
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


class PlainLifecycle:
    """How the plain path finds leftovers, kills and reads memory. Part ii supplies the scope-based one with the same three methods."""

    def memory(self, case_id):
        return rs_report.NOT_CERTIFIED

    def kill(self, proc):
        kill_group(proc)

    def leftover(self, proc):
        """True when something of the program's process group is still alive after the program was reaped."""
        return group_present(proc)


@dataclass
class Config:
    workspace: str
    scratch: str
    prefix: Optional[list] = None
    limit_ms: int = FLOOR_MS
    basis: str = "floor"
    capture_cap: int = CAPTURE_CAP
    lifecycle: object = field(default_factory=PlainLifecycle)


class BuildResult(NamedTuple):
    ran: bool
    status: Optional[int] = None
    wall_ms: int = 0
    timed_out: bool = False
    tail: str = ""


def now_ms():
    return time.monotonic_ns() // 1_000_000


CASE_ID = re.compile(r"[A-Za-z0-9_-]+\Z")


def launch_argv(prefix, case_id):
    """The prefix with `{case}` filled in, then `bash run.sh` (the layer's box and scope come before it). A case id is letters, digits,
    underscore and hyphen only: anything else raises ValueError before it can reach an argv (the plan refuses such samples with exit 4)."""
    if not CASE_ID.match(case_id):
        raise ValueError(f"case id {case_id!r} is not letters, digits, underscore and hyphen")
    return [part.replace("{case}", case_id) for part in prefix or []] + ["bash", "run.sh"]


def _signal_group(pid, sig):
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
    _signal_group(proc.pid, signal.SIGKILL)
    proc.wait()
    deadline = now_ms() + GONE_CEILING_MS
    while group_present(proc) and now_ms() < deadline:
        time.sleep(0.001)
    if group_present(proc):
        raise CleanupFailed(f"a process of the case could not be killed: its group {proc.pid} is still there after {GONE_CEILING_MS} ms")


def pidfd_of(proc):
    """A pollable descriptor that turns readable when the process exits, or None where the kernel has none."""
    try:
        return os.pidfd_open(proc.pid)
    except (AttributeError, OSError):
        return None


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
        _close(pidfd)
    if not done:
        kill_group(proc)
    status = proc.wait()
    _signal_group(proc.pid, signal.SIGKILL)
    return BuildResult(True, status, now_ms() - started, not done, read_tail(log))


def _close(fd):
    if fd is not None:
        os.close(fd)
