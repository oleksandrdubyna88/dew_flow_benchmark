"""rs_exec: what every program run shares: the build, the launch argv, the program's environment (V11), the process group of the plain
path, and the wait.

The execution layer (E1.S8b) hands the runner a launch prefix, a launch status descriptor, a cgroup and a case view; `rs_scope.py` is the
lifecycle that reads and kills through that cgroup, `PlainLifecycle` here is the one with no box. Wall time is `time.monotonic_ns()` and
every wait for an exit is `os.pidfd_open` + `select` with the exact remaining time, never `Popen.wait(timeout=...)`'s polling. One clock,
`time.monotonic_ns`, serves every start, end and deadline of a case.
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
SEEK_TICK_S = 0.001
TRACK_TICK_S = 0.02
RECHECK_S = 0.02
EMPTY_CASE = "empty-program"
RUN_PROGRAM = ("bash", "run.sh")
POLL_FIRST_S = 0.001
POLL_LAST_S = 0.05


class EnvironmentProblem(Exception):
    """Something the runner does not own is wrong (a view that is missing, a cgroup that holds a foreign process): exit 3, never a verdict."""


class Settled(NamedTuple):
    """What a case's end leaves: the program's status, the leftover finding (None: not checked) and its memory record."""
    code: int
    leftover: object
    memory: dict


class PlainLifecycle:
    """How the plain path (no `--cgroup-of`) finds leftovers, kills and reads memory: through the process group of the launcher."""
    road = {"kill": "killpg", "leftoverEvidence": "process group (killpg 0)"}
    directory = identity = wrapper_file = None

    def memory(self, case_id=None):
        return rs_report.NOT_CERTIFIED

    def present(self):
        """Whether what this lifecycle kills through exists (a process group always does)."""
        return True

    def check_free(self):
        """A process group is the launcher's own: there is nothing a foreign process could be holding."""

    def mismatch(self, child_pid):
        """No cgroup, nothing to confirm."""
        return None

    def kill(self, proc):
        kill_group(proc)

    def leftover(self, proc, program=None):
        """True when something of the program's process group is still alive after the program was reaped."""
        return group_present(proc)

    def _leftover_after_exit(self, proc):
        wait_gone(proc, GONE_CEILING_MS)
        return group_alive(proc.pid)

    def settle(self, launched, end):
        """The case's end. The launcher is NOT reaped until the group signal is done: its pid is reserved while it is unreaped, and a reaped
        leader's pid may be someone else's. A launcher that has exited is a zombie that holds the group's id; what is alive in the group
        besides it is the leftover."""
        proc, exited = launched.proc, end == "exited"
        leftover = self._leftover_after_exit(proc) if exited else None
        if leftover or not exited:
            self.kill(proc)
        return Settled(launched.status_code(proc.wait()), leftover, self.memory())


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
    wrapper_file: Optional[str] = None


class BuildResult(NamedTuple):
    ran: bool
    status: Optional[int] = None
    wall_ms: int = 0
    timed_out: bool = False
    tail: str = ""


NS_PER_MS = 1_000_000


def now_ns():
    return time.monotonic_ns()


def now_ms():
    return now_ns() // NS_PER_MS


def ceil_ms(ns):
    """Whole milliseconds, rounded UP: the one rounding of every figure a result reports (wall, launch, empty program), so that subtracting
    one from another (the F1 control) never mixes two roundings."""
    return -(-ns // NS_PER_MS)


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


def _stat_fields(text):
    """(state, process group id) from the text of /proc/<pid>/stat, or None when it cannot be understood (never an error)."""
    fields = text.rsplit(")", 1)[-1].split()
    if len(fields) < 3 or not fields[2].lstrip("-").isdigit():
        return None
    return fields[0], int(fields[2])


def _group_of(pid):
    """The state and process group id of `pid` from /proc, or None when it is gone or unreadable."""
    try:
        with open(f"/proc/{pid}/stat", "rb") as f:
            return _stat_fields(f.read().decode("ascii", "replace"))
    except OSError:
        return None


def _group_exists(pgid):
    """A signal 0 to the group: False when no process of it exists at all."""
    try:
        os.killpg(pgid, 0)
    except ProcessLookupError:
        return False
    except PermissionError:
        pass
    return True


def _scan_alive(pgid):
    found = (_group_of(name) for name in os.listdir("/proc") if name.isdigit())
    return any(_alive_in(entry, pgid) for entry in found)


def group_alive(pgid):
    """True when a process of the group `pgid` is alive and not a zombie (the unreaped leader, once exited, is a zombie and does not count).
    A group that no longer exists is found gone by a signal 0, without scanning /proc; /proc is scanned only when the group exists, because
    a zombie leader may be its only member."""
    return _group_exists(pgid) and _scan_alive(pgid)


def _alive_in(entry, pgid):
    return entry is not None and entry[1] == pgid and entry[0] not in ("Z", "X")


def wait_gone(proc, timeout_ms):
    """True when the process has exited within `timeout_ms`, awaited on its pidfd: it is NOT reaped (no poll, no wait)."""
    if proc.returncode is not None:
        return True
    pidfd = pidfd_of(proc)
    try:
        return _exited(proc, pidfd, timeout_ms) if pidfd is not None else proc.poll() is not None
    finally:
        close_fd(pidfd)


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
    children go a moment later. If the group is still there at the ceiling (2 s) the cleanup has failed: CleanupFailed. A leader that is
    already reaped is not signalled (its pid may be someone else's by now)."""
    if proc.returncode is None:
        signal_group(proc.pid, signal.SIGKILL)
    proc.wait()
    _wait_group_gone(proc)
    if group_present(proc):
        raise CleanupFailed(f"a process of the case could not be killed: its group {proc.pid} is still there after {GONE_CEILING_MS} ms")


def _wait_group_gone(proc):
    deadline, delay = now_ms() + GONE_CEILING_MS, POLL_FIRST_S
    while group_present(proc) and now_ms() < deadline:
        time.sleep(delay)
        delay = min(delay * 2, POLL_LAST_S)


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
    try:
        done = wait_gone(proc, ceiling_ms)  # awaited on a pidfd, and not reaped: the group signal below needs the leader's pid reserved
    except BaseException:  # the runner itself is being stopped (a signal): never leave the build running
        kill_group(proc)
        raise
    status = _end_build(proc, done)
    return BuildResult(True, status, now_ms() - started, not done, read_tail(log))


def _end_build(proc, done):
    """The build's status. Stragglers are signalled before the leader is reaped, never after."""
    if not done or group_alive(proc.pid):
        kill_group(proc)
    return proc.wait()


def close_fd(fd):
    if fd is not None:
        os.close(fd)
