"""rs_status: the launch status the layer's bwrap writes with `--json-status-fd N` (B5), read here as `--launch-status N`.

bwrap writes one JSON object per line: `{"child-pid": P}` right after it clones the sandbox (before its mounts are made), later
`{"exit-code": C}`. The runner makes one
pipe per case and puts its write end at descriptor number N in the launcher (the prefix names the same N for bwrap), so a case's lines
cannot be mixed with another case's. `child-pid` is the launch-success signal and the clock start: it is NOT the pid authority (the
program's pid comes from `cgroup.procs`, and a pid seen inside the box is never used). `exit-code` is the program's status. A line that
is not a JSON object is dropped; the buffer is bounded.
"""
import fcntl
import os
import time
from decimal import Decimal

import rs_json

MAX_BUFFER = 64 * 1024
MAX_LINE = 64 * 1024
FIRST_USABLE_FD = 3


def check_number(number):
    """None when `number` can carry the status (3 or more and not open in the runner), else the reason it cannot."""
    if number < FIRST_USABLE_FD:
        return f"--launch-status {number} is a standard stream: use 3 or more"
    try:
        fcntl.fcntl(number, fcntl.F_GETFD)
    except OSError:
        return None
    return f"--launch-status {number} is already open in the runner: name a free descriptor number"


def _away(fd, number):
    """`fd` itself, or a copy above `number` when `fd` sits on the number that must stay free."""
    if fd != number:
        return fd
    moved = fcntl.fcntl(fd, fcntl.F_DUPFD_CLOEXEC, number + 1)
    os.close(fd)
    return moved


def open_pipe(number):
    """(read fd, number): a pipe whose write end now sits, inheritable, at descriptor `number`. Call `release(number)` after the spawn."""
    read_fd, write_fd = os.pipe()
    read_fd, write_fd = _away(read_fd, number), _away(write_fd, number)
    os.dup2(write_fd, number, inheritable=True)
    os.close(write_fd)
    os.set_blocking(read_fd, False)
    return read_fd


def release(number):
    """Close the runner's own copy of the write end, so that the pipe ends when the launcher chain closes theirs."""
    os.close(number)


class StatusReader:
    """Collects the lines of one case: `child_pid` and `exit_code` stay None until their line is whole. `problem` names the first thing that
    breaks the rules (P2): a line that is not one JSON object or a record that is neither before `child-pid`, a doubled or invalid
    `child-pid` or `exit-code`. Before `child-pid` a problem is a failed launch; after it the lines that are not status records are the case
    and are ignored, and the launch is judged on what the runner had read when it took the clock."""

    def __init__(self):
        self.partial, self.skipping = b"", False
        self.child_pid, self.exit_code, self.problem = None, None, None
        self.child_seen_ns = self.exit_seen_ns = None  # the instants (time.monotonic_ns) at which those records were read

    def feed(self, data):
        """Consume the complete lines of this read and keep only the last incomplete one (at most MAX_LINE bytes): nothing already read is
        split again."""
        *lines, self.partial = (self.partial + self._past_overflow(data)).split(b"\n")
        for line in lines:
            self._line(line)
        if len(self.partial) > MAX_LINE:
            self._overflow()

    def _past_overflow(self, data):
        """While the rest of an over-long line goes by, nothing is kept: only what follows its line feed counts."""
        if not self.skipping:
            return data
        _, newline, rest = data.partition(b"\n")
        self.skipping = not newline
        return rest

    def _overflow(self):
        """A line longer than MAX_LINE: before `child-pid` the protocol is broken; after it, it is junk of the case and is dropped."""
        if self.child_pid is None:
            self._note(f"a status line longer than {MAX_LINE} bytes")
        self.partial, self.skipping = b"", True

    def _note(self, message):
        self.problem = self.problem or message

    def _line(self, line):
        if not line.strip():
            return
        record = _object(line)
        if record is None:
            return self._note("a line that is not one JSON object") if self.child_pid is None else None
        self._keys(record)

    def _keys(self, record):
        if not ({"child-pid", "exit-code"} & record.keys()) and self.child_pid is None:
            self._note("an unknown record before child-pid")
        for key, attr in (("child-pid", "child_pid"), ("exit-code", "exit_code")):
            self._set_if(record, key, attr)

    def _set_if(self, record, key, attr):
        if key in record:
            self._set(attr, record[key], key)

    def _set(self, attr, raw, name):
        number = _whole(raw)
        if getattr(self, attr) is not None or number is None:
            return self._note(f"{name} is doubled or not a whole number")
        setattr(self, attr, number)
        setattr(self, "child_seen_ns" if attr == "child_pid" else "exit_seen_ns", time.monotonic_ns())


def _object(line):
    parsed = rs_json.parse(line)
    return parsed.value if parsed.ok and isinstance(parsed.value, dict) else None


def _whole(value):
    """An integer from a JSON number that is a whole number, else None (the parser makes every number a Decimal; a bool is none)."""
    if not isinstance(value, Decimal):
        return None
    return int(value) if value == value.to_integral_value() else None


def pull(read_fd, reader):
    """Read what is in the pipe into the reader; False at the end of the pipe (nobody holds its write end any more)."""
    try:
        data = os.read(read_fd, MAX_BUFFER)
    except BlockingIOError:
        return True
    reader.feed(data)
    return bool(data)
