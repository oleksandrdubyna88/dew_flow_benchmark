"""rs_cgroup: what the runner reads from, and does to, the cgroup2 directory the execution layer names with `--cgroup-of` (B1, B4).

Every pid here is a HOST pid, read from the host's view of `cgroup.procs`; a pid seen inside the box (1 or 2) is never used. The program
is the pid in `cgroup.procs` that is neither a `bwrap` process (`/proc/<pid>/comm`) nor the layer's wrapper (the pid it writes to
`wrapper.pid` in its out directory, when there is one). `cgroup.kill` is the kill at the time limit: behind `--new-session` the program
is in a group of its own and `os.killpg` of the launcher's group would miss it. Nothing here signals one pid.
"""
import os
import time

import rs_exec as ex

BOX_COMM = "bwrap"
WRAPPER_FILE = "wrapper.pid"
EVENT_NAMES = ("low", "high", "max", "oom", "oom_kill", "oom_group_kill")


def _text(path):
    """The text of a small file, or None when it cannot be read."""
    try:
        with open(path, "rb") as f:
            return f.read(1 << 20).decode("ascii", "replace")
    except OSError:
        return None


def _whole_number(text):
    token = (text or "").strip()
    return int(token) if token.isascii() and token.isdigit() else None


def _pids_of(text):
    numbers = map(_whole_number, text.splitlines())
    return [number for number in numbers if number is not None]


def procs(directory):
    """Every pid listed in `cgroup.procs`, or None when the file cannot be read (absent is not an empty list)."""
    text = _text(os.path.join(directory, "cgroup.procs"))
    return None if text is None else _pids_of(text)


def comm_of(pid):
    """`/proc/<pid>/comm` without its line feed, or None when the process is gone."""
    text = _text(f"/proc/{pid}/comm")
    return text.rstrip("\n") if text is not None else None


def wrapper_pid(out_dir):
    """The pid in `<out_dir>/wrapper.pid`, or None when there is no such file (the delegated scope has no wrapper)."""
    return _whole_number(_text(os.path.join(out_dir, WRAPPER_FILE))) if out_dir else None


def _is_program(pid, wrapper):
    return pid != wrapper and comm_of(pid) not in (None, BOX_COMM)


def program_candidates(directory, wrapper=None):
    """The pids of `cgroup.procs` that are neither bwrap nor the wrapper nor already gone, or None when the file cannot be read."""
    pids = procs(directory)
    return None if pids is None else [pid for pid in pids if _is_program(pid, wrapper)]


def _pairs(text):
    return [pair for pair in map(str.split, text.splitlines()) if len(pair) == 2]


def _events(text):
    values = {name: _whole_number(value) for name, value in _pairs(text)}
    return values if values and None not in values.values() else None


def memory(directory):
    """The memory record of a case: `{known: true, peakBytes, events, oomKill}`, or `{known: false, why}` when a file cannot be read."""
    peak = _whole_number(_text(os.path.join(directory, "memory.peak")))
    events = _events(_text(os.path.join(directory, "memory.events")) or "")
    if peak is None or events is None:
        return {"known": False, "why": "memory.peak or memory.events could not be read from the cgroup"}
    return {"known": True, "peakBytes": peak, "events": events, "oomKill": events.get("oom_kill", 0)}


def kill(directory):
    """Write `1` to `cgroup.kill`: every process of the cgroup gets SIGKILL, whatever its group or session."""
    try:
        with open(os.path.join(directory, "cgroup.kill"), "wb") as f:
            f.write(b"1")
    except OSError as e:
        raise ex.CleanupFailed(f"cgroup.kill of {directory} could not be written: {type(e).__name__}: {e}") from e


def wait_empty(directory, ceiling_ms=ex.GONE_CEILING_MS):
    """Wait, backing off from 1 ms to 50 ms, until `cgroup.procs` lists nothing; CleanupFailed when pids are left at the ceiling."""
    deadline, delay = ex.now_ms() + ceiling_ms, ex.POLL_FIRST_S
    while procs(directory) and ex.now_ms() < deadline:
        time.sleep(delay)
        delay = min(delay * 2, ex.POLL_LAST_S)
    left = procs(directory)
    if left:
        raise ex.CleanupFailed(f"a process of the case could not be killed: {len(left)} still in {directory} after {ceiling_ms} ms")
