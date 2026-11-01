"""rs_cgroup: what the runner reads from, and does to, the cgroup2 directory the execution layer names with `--cgroup-of` (B1, B4).

Every pid here is a HOST pid, read from the host's view of `cgroup.procs`; a pid seen inside the box (1 or 2) is never used. A bwrap
process is known by IDENTITY, never by its name: `/proc/<pid>/exe` is the very file (device and inode) of the bwrap binary the launch prefix
names, so a program that copies bash to `./bwrap` hides nothing. The program is the process whose parent is a bwrap process (`bash run.sh`
execs it: D38) and the launcher pid is never a candidate (rs_track.py). `cgroup.kill` is the kill: behind `--new-session` the program is in a
group of its own and `os.killpg` of the launcher's group would miss it. Nothing here signals one pid.
"""
import os
import shutil
import time

import rs_exec as ex

BOX_NAME = "bwrap"
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
    """`/proc/<pid>/comm` without its line feed, or None when the process is gone. Evidence for the record only: never used to tell bwrap."""
    text = _text(f"/proc/{pid}/comm")
    return text.rstrip("\n") if text is not None else None


def _file_id(path):
    """(device, inode) of what `path` leads to, or None."""
    try:
        status = os.stat(path)
    except OSError:
        return None
    return (status.st_dev, status.st_ino)


def _box_path(element):
    return element if "/" in element else shutil.which(element)


def _first_box(prefix):
    return next((element for element in prefix if os.path.basename(element) == BOX_NAME), None)


def bwrap_identity(prefix):
    """(device, inode) of the bwrap binary the launch prefix names (the first element whose base name is `bwrap`, looked up on PATH when it
    has no directory), or None when the prefix has no bwrap. What `/proc/<pid>/exe` must equal for a process to be a bwrap process."""
    named = _first_box(prefix or [])
    path = _box_path(named) if named else None
    return _file_id(path) if path else None


def is_bwrap(pid, identity):
    """True when `pid` runs the very file `identity` names. A process that is gone, or one whose exe cannot be read, is not."""
    return pid is not None and identity is not None and _file_id(f"/proc/{pid}/exe") == identity


def is_zombie(pid):
    """True for a process that has exited and waits to be reaped (or is gone): it is no program and no leftover, and it has no exe to read."""
    text = _text(f"/proc/{pid}/stat")
    fields = text.rsplit(")", 1)[-1].split() if text else []
    return not fields or fields[0] in ("Z", "X")


def ppid_of(pid):
    """The parent pid from `/proc/<pid>/stat`, or None when the process is gone."""
    text = _text(f"/proc/{pid}/stat")
    fields = text.rsplit(")", 1)[-1].split() if text else []
    return _whole_number(fields[1]) if len(fields) > 1 else None


def wrapper_pid(path):
    """The pid in the file `--wrapper-pid-file` names, or None when no file was named or it holds no pid."""
    return _whole_number(_text(path)) if path else None


def _pairs(text):
    return [pair for pair in map(str.split, text.splitlines()) if len(pair) == 2]


def _events(text):
    values = {name: _whole_number(value) for name, value in _pairs(text)}
    return values if values and None not in values.values() else None


def _events_of(directory):
    return _events(_text(os.path.join(directory, "memory.events")) or "")


def _kept(**named):
    return {key: value for key, value in named.items() if value is not None}


def memory(directory):
    """The memory record of a case. `known` is true only when `memory.peak` and `memory.events` were both read; whichever was read is kept
    on its own, so an `oom_kill` is judged from `memory.events` even when `memory.peak` cannot be read."""
    peak = _whole_number(_text(os.path.join(directory, "memory.peak")))
    events = _events_of(directory)
    read = _kept(peakBytes=peak, events=events, oomKill=None if events is None else events.get("oom_kill", 0))
    known = peak is not None and events is not None
    return {"known": True, **read} if known else {"known": False, "why": "memory.peak or memory.events could not be read from the cgroup", **read}


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
