"""rs_cgroup: what the runner reads from, and does to, the cgroup2 directory the execution layer names with `--cgroup-of` (B1, B4).

Every pid here is a HOST pid, read from the host's view of `cgroup.procs`; a pid seen inside the box (1 or 2) is never used. A bwrap
process is known by IDENTITY, never by its name: `/proc/<pid>/exe` is the very file (device and inode) of the bwrap binary the launch prefix
names, so a program that copies bash to `./bwrap` hides nothing. The program is the process whose parent is a bwrap process (`bash run.sh`
execs it: D38) and the launcher pid is never a candidate (rs_track.py). `cgroup.kill` is the kill: behind `--new-session` the program is in a
group of its own and `os.killpg` of the launcher's group would miss it. Nothing here signals one pid.
"""
import errno
import os
import re
import shutil
import signal
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
    numbers = [_whole_number(line) for line in text.splitlines() if line.strip()]
    return None if None in numbers else numbers


def procs(directory):
    """Every pid listed in `cgroup.procs`, or None when the file cannot be read or holds a line that is no pid (absent is not an empty list,
    and a file that cannot be understood is never an empty one)."""
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
    """True only for a process whose stat SAYS it has exited and waits to be reaped. A stat that cannot be read (pid 0, a pid that is not
    there, a refused read) is not proof of death: what cannot be read is alive."""
    text = _text(f"/proc/{pid}/stat")
    fields = text.rsplit(")", 1)[-1].split() if text else []
    return bool(fields) and fields[0] in ("Z", "X")


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
    """Write `1` to `cgroup.kill`: every process of the cgroup gets SIGKILL, whatever its group or session. The file is opened and never
    created: a file made by the runner would be no kill switch and would only look like one."""
    try:
        fd = os.open(os.path.join(directory, "cgroup.kill"), os.O_WRONLY)
        with os.fdopen(fd, "wb") as f:
            f.write(b"1")
    except OSError as e:
        raise ex.CleanupFailed(f"cgroup.kill of {directory} could not be written: {type(e).__name__}: {e}") from e


PIDFD_BATCH = 64  # the most pidfds held open at once by `kill_pids`


def _pidfd_open(pid):
    """A pidfd for `pid`, or None when the process is gone (or the kernel has none). Running out of descriptors (EMFILE, ENFILE) is raised."""
    try:
        return os.pidfd_open(pid)
    except OSError as e:
        if e.errno in (errno.EMFILE, errno.ENFILE):
            raise
        return None
    except AttributeError:
        return None


def _pin(pids):
    """({pid: pidfd} for the pids that could be pinned, in order, the pids not tried): the pinning stops at the first pid for which the
    process has no descriptor left."""
    held = {}
    for index, pid in enumerate(pids):
        try:
            fd = _pidfd_open(pid)
        except OSError:
            return held, pids[index:]
        if fd is not None:
            held[pid] = fd
    return held, []


def _send_kill(fd):
    """SIGKILL through the pidfd. A process the runner may not signal is left to the emptiness check that follows."""
    try:
        signal.pidfd_send_signal(fd, signal.SIGKILL)
    except (ProcessLookupError, PermissionError):
        pass


def _still_listed(directory, held):
    """The pids of `held` that `cgroup.procs` lists NOW (one read): a pid that left the cgroup in between may be someone else's by now."""
    listed = procs(directory) or []
    return [pid for pid in held if pid in listed]


def _kill_held(directory, held):
    """Signal, through its pidfd, each pid of the batch that is still listed (the listing is read ONCE), and close every pidfd."""
    try:
        for pid in _still_listed(directory, held):
            _send_kill(held[pid])
    finally:
        for fd in held.values():
            ex.close_fd(fd)


def kill_pids(directory):
    """SIGKILL to each host pid `cgroup.procs` lists, each through a pidfd, in batches of at most `PIDFD_BATCH`: the fallback when `cgroup.kill`
    cannot be written. A batch that runs out of descriptors signals what it holds and closes it, and what it did not try goes on in the next."""
    pending = procs(directory) or []
    while pending:
        held, untried = _pin(pending[:PIDFD_BATCH])
        _kill_held(directory, held)
        pending = untried + pending[PIDFD_BATCH:] if held else pending[PIDFD_BATCH:]


def _mountinfo():
    return _text("/proc/self/mountinfo") or ""


def _unescape(path):
    """A mount point as /proc/self/mountinfo writes it: a space, a tab, a line feed and a backslash are octal escapes."""
    return re.sub(r"\\([0-7]{3})", lambda found: chr(int(found.group(1), 8)), path)


def _mounts():
    """(mount point, file system type, root inside that file system) of every well-formed line of /proc/self/mountinfo: a line with too few
    fields, or no ' - ' part, is skipped."""
    found = []
    for line in _mountinfo().splitlines():
        head, _, tail = line.partition(" - ")
        fields, kind = head.split(), tail.split()
        if len(fields) > 4 and kind:
            found.append((_unescape(fields[4]), kind[0], _unescape(fields[3])))
    return found


def _under(path, mount):
    return mount == "/" or path == mount or path.startswith(mount + "/")


def _nearest_mount(real):
    above = [entry for entry in _mounts() if _under(real, entry[0])]
    return max(above, key=lambda entry: len(entry[0]), default=None)


def cgroup2_root(directory):
    """The mount point of the cgroup2 hierarchy `directory` (resolved, links followed) lies under, or None: the nearest mount above it decides,
    and one that is not cgroup2 (a v1 hierarchy, a tmpfs, a plain folder under /) is no root. A cgroup2 mounted as a SUBTREE (its mountinfo
    root is not '/') is no root either: the paths of `/proc/<pid>/cgroup` are not relative to its mount point. It fails closed."""
    nearest = _nearest_mount(os.path.realpath(directory))
    return nearest[0] if nearest and nearest[1] == "cgroup2" and nearest[2] == "/" else None


def is_cgroup2(directory):
    """True when the directory is a cgroup2 node: it holds `cgroup.controllers` AND lies under a cgroup2 mount (a controllers file in a plain
    folder is only a file)."""
    return os.path.exists(os.path.join(directory, "cgroup.controllers")) and cgroup2_root(directory) is not None


def _proc_cgroup(pid):
    return _text(f"/proc/{pid}/cgroup")


def _v2_lines(text):
    return [line[3:].strip() for line in text.splitlines() if line.startswith("0::")]


def pid_cgroup_path(pid):
    """The cgroup2 path (from the mount root) `/proc/<pid>/cgroup` gives, or None when the process is gone or has no v2 line."""
    lines = _v2_lines(_proc_cgroup(pid) or "")
    return lines[0] if lines else None


def _cgroup_path(directory):
    """The path of `directory` from its cgroup2 mount root, both resolved ('' for the root itself), or None when it is under none."""
    root = cgroup2_root(directory)
    if root is None:
        return None
    here = os.path.relpath(os.path.realpath(directory), os.path.realpath(root))
    return "" if here == "." else "/" + here


def member_of(pid, directory):
    """True when `pid` is in the cgroup `directory` or a descendant of it, False when it is somewhere else, None when it cannot be told (the
    process is gone: a program that ended before it was looked at). Normalised cgroup-relative paths are compared, never raw strings."""
    path, here = pid_cgroup_path(pid), _cgroup_path(directory)
    if path is None:
        return None
    return here is not None and (path == here or path.startswith(here + "/"))


def in_cgroup(pid, directory):
    """Like `member_of`, but for the box itself: it must be IN the named cgroup, not in a descendant of it."""
    path, here = pid_cgroup_path(pid), _cgroup_path(directory)
    return None if path is None else here is not None and path == here


def stale_counters(directory):
    """What an earlier case left in the counters of an existing cgroup (they are not reset): `memory.peak` and `oom_kill` above zero."""
    peak = _whole_number(_text(os.path.join(directory, "memory.peak")))
    events = _events_of(directory) or {}
    found = [f"memory.peak is {peak}"] if peak else []
    return found + ([f"oom_kill is {events['oom_kill']}"] if events.get("oom_kill") else [])


def _read_errno(directory):
    """The errno with which `cgroup.procs` fails to read, or None when it reads."""
    try:
        with open(os.path.join(directory, "cgroup.procs"), "rb") as f:
            f.read(1)
    except OSError as e:
        return e.errno
    return None


def _gone(directory, was_readable):
    """True when the cgroup is gone or on its way: its directory is missing, the kernel took it offline (ENODEV on every read), or it had
    been readable and its files are gone while the directory lingers (a dying cgroup keeps the directory long after its last process)."""
    if not os.path.isdir(directory):
        return True
    failed = _read_errno(directory)
    return failed == errno.ENODEV or (was_readable and failed == errno.ENOENT)


def populated_of(directory):
    """`populated` of `cgroup.events` (1 while the cgroup or a descendant holds a live process), or None when the file does not say."""
    value = dict(_pairs(_text(os.path.join(directory, "cgroup.events")) or "")).get("populated")
    return _whole_number(value)


def _all_dead(listed):
    return listed is not None and all(is_zombie(pid) for pid in listed)


def empty(directory, was_readable=False):
    """True when nothing is alive in the cgroup: it is gone (above), or `cgroup.events` says `populated 0`, or (when it says nothing) every
    listed pid is a zombie. A file that cannot be read or understood, in a cgroup that never was readable, is not empty."""
    if _gone(directory, was_readable):
        return True
    populated = populated_of(directory)
    return _all_dead(procs(directory)) if populated is None else populated == 0


def _why_unreadable(directory):
    """How `cgroup.procs` fails to read, by errno name, or that its content is no list of pids."""
    try:
        with open(os.path.join(directory, "cgroup.procs"), "rb") as f:
            f.read(1)
    except OSError as e:
        return errno.errorcode.get(e.errno, str(e.errno))
    return "content that is no list of pids"


def _left(directory):
    left = procs(directory)
    return f"cgroup.procs cannot be read ({_why_unreadable(directory)})" if left is None else f"{len(left)} still in {directory}"


def wait_empty(directory, ceiling_ms=ex.GONE_CEILING_MS, was_readable=False):
    """Wait, backing off from 1 ms to 50 ms, until one look finds the cgroup empty; CleanupFailed when none does by the ceiling, whether pids
    are left or `cgroup.procs` cannot be understood. `was_readable`: the caller read `cgroup.procs` before, so its files vanishing is the
    cgroup dying. The first look that finds it empty decides: a dying cgroup answers differently from one read to the next, and a second look
    to confirm the first would contradict it (that was a flake)."""
    deadline, delay = ex.now_ms() + ceiling_ms, ex.POLL_FIRST_S
    while not empty(directory, was_readable):
        if ex.now_ms() >= deadline:
            raise ex.CleanupFailed(f"a process of the case could not be killed: {_left(directory)} after {ceiling_ms} ms")
        time.sleep(delay)
        delay = min(delay * 2, ex.POLL_LAST_S)
