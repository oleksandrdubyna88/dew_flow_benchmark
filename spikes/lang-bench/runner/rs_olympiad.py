"""rs_olympiad: one olympiad case. `bash run.sh` gets the case's input on standard input; its standard output is compared as a STREAM
against the expected bytes and stops at the first difference (B9); standard error goes to a file, never a pipe. What is kept of the
output for the report is capped, and a capped capture is never what decides: a PASS needs the stream to have matched every expected
byte and nothing more. A wall time over the limit is a time limit even when the program exited by itself.
"""
import os
import selectors
import signal
from typing import NamedTuple

import rs_exec as ex
import rs_launch as rl
import rs_report as rp
import rs_scope
import rs_view
from rs_report import CaseResult


class Case(NamedTuple):
    id: str
    stdin: bytes
    expected: bytes
    r_ids: tuple = ()
    comparison: object = None
    fixtures: object = None


def _common_prefix(a, b):
    n = min(len(a), len(b))
    if a[:n] == b[:n]:
        return n
    return next(i for i in range(n) if a[i] != b[i])


class Comparer:
    """The streaming comparison: `feed` each chunk as it arrives; `mismatch` is the byte of the first difference or None."""

    def __init__(self, expected, cap):
        self.expected, self.cap = expected, cap
        self.offset, self.total, self.mismatch, self.kept = 0, 0, None, bytearray()
        self.tail, self.window = b"", None

    def feed(self, chunk):
        if self.mismatch is None:
            self._compare(chunk)
        self.total += len(chunk)
        self.tail = (self.tail + chunk)[-rp.WINDOW:]
        self.kept += chunk[: max(0, self.cap - len(self.kept))]

    def _compare(self, chunk):
        window = self.expected[self.offset:self.offset + len(chunk)]
        same = _common_prefix(chunk, window)
        if same == len(chunk):
            self.offset += same
        else:
            self.mismatch = self.offset + same
            self.window = self._around(chunk, same)

    def _around(self, chunk, at):
        """(offset, bytes) of the actual output around the first difference, kept whatever the cap: what the tail and this chunk hold."""
        joined = self.tail + chunk
        index = len(self.tail) + at
        start = max(0, index - rp.WINDOW)
        return self.total - len(self.tail) + start, joined[start:index + rp.WINDOW]

    def actual_window(self):
        """The small window kept for the report: around the first difference, or the last bytes written when the output ended early."""
        return self.window if self.window is not None else (self.total - len(self.tail), self.tail)

    @property
    def truncated(self):
        return self.total > len(self.kept)

    @property
    def exact(self):
        return self.mismatch is None and self.offset == len(self.expected)


class Outcome(NamedTuple):
    end: str
    wall_ms: int
    returncode: int
    leftover: object
    memory: dict
    launched: object
    road: dict


class _Pump:
    """Moves bytes between the program and the runner without ever blocking on either: input out, output in, exit on a pidfd."""

    def __init__(self, proc, pidfd, data, comparer, stderr_path):
        self.proc, self.pidfd, self.comparer, self.stderr_path = proc, pidfd, comparer, stderr_path
        self.pending, self.exit_ms, self.eof = memoryview(data), None, False
        self.sel = selectors.DefaultSelector()
        os.set_blocking(proc.stdout.fileno(), False)
        self.sel.register(proc.stdout, selectors.EVENT_READ, self.read_output)
        self._start_input()
        if pidfd is not None:
            self.sel.register(pidfd, selectors.EVENT_READ, self.exited)

    def _start_input(self):
        if not len(self.pending):
            return self.proc.stdin.close()
        os.set_blocking(self.proc.stdin.fileno(), False)
        self.sel.register(self.proc.stdin, selectors.EVENT_WRITE, self.write_input)

    def write_input(self):
        try:
            self.pending = self.pending[os.write(self.proc.stdin.fileno(), self.pending[:ex.CHUNK]):]
        except BlockingIOError:
            return None
        except BrokenPipeError:
            self.pending = self.pending[len(self.pending):]
        return self._input_done() if not len(self.pending) else None

    def _input_done(self):
        self.sel.unregister(self.proc.stdin)
        self.proc.stdin.close()

    def read_output(self):
        try:
            chunk = os.read(self.proc.stdout.fileno(), ex.CHUNK)
        except BlockingIOError:
            return None
        if not chunk:
            self.sel.unregister(self.proc.stdout)
            return None
        self.comparer.feed(chunk)
        return "mismatch" if self.comparer.mismatch is not None else None

    def exited(self):
        self.exit_ms = ex.now_ms()
        return self.drain()

    def drain(self):
        """What the program wrote before it exited is in the pipe already: read it, but never wait for an end a stray child may hold open."""
        while self.comparer.mismatch is None and self._more():
            pass
        return "mismatch" if self.comparer.mismatch is not None else "exited"

    def _more(self):
        try:
            chunk = os.read(self.proc.stdout.fileno(), ex.CHUNK)
        except (BlockingIOError, ValueError, OSError):
            return False
        self.comparer.feed(chunk)
        return bool(chunk)

    def flooded(self):
        try:
            return os.path.getsize(self.stderr_path) > ex.STDERR_KILL_BYTES
        except OSError:
            return False

    def step(self, timeout_s):
        for key, _ in self.sel.select(timeout_s):
            reason = key.data()
            if reason:
                return reason
        return "flood" if self.flooded() else self._polled()

    def _polled(self):
        if self.pidfd is None and self.proc.poll() is not None:
            return self.exited()
        return None

    def run(self, started_ms, limit_ms):
        while True:
            remaining = started_ms + limit_ms - ex.now_ms()
            if remaining <= 0:
                return "timeout"
            reason = self.step(min(remaining / 1000, ex.TICK_S))
            if reason:
                return reason

    def close(self):
        self.sel.close()
        for stream in (self.proc.stdin, self.proc.stdout):
            if stream and not stream.closed:
                stream.close()


def _launch(case, config, lifecycle, stderr_path):
    """Start the case in its own view when `--case-view` is given (fixtures written into it first), else in the workspace."""
    cwd = config.workspace
    if config.case_view:
        cwd = rs_view.require(rs_view.view_of(config.case_view, case.id))
        rs_view.write_fixtures(cwd, case.fixtures)
    return rl.start(config, case.id, lifecycle, stderr_path, cwd)


def _execute(case, config, comparer, stderr_path):
    lifecycle = rs_scope.lifecycle_for(config, case.id)
    launched = _launch(case, config, lifecycle, stderr_path)
    pump = _Pump(launched.proc, launched.pidfd, case.stdin, comparer, stderr_path)
    try:
        end = pump.run(launched.clock_ms, config.limit_ms)
        wall = (pump.exit_ms or ex.now_ms()) - launched.clock_ms
        settled = lifecycle.settle(launched, end)
        return Outcome(end, wall, settled.code, settled.leftover, settled.memory, launched, lifecycle.road)
    except BaseException:  # the runner itself is being stopped (a signal): never leave the program under test running
        lifecycle.kill(launched.proc)
        raise
    finally:
        pump.close()
        launched.close()


# ---- judging --------------------------------------------------------------------------------------------------------------------

def _exit_reason(code):
    return f"exit status {code}" if code > 0 else f"killed by signal {signal.Signals(-code).name}"


def _over_time(case, config, out, comparer):
    over = out.end == "timeout" or out.wall_ms > config.limit_ms
    return (rp.time_text(out.wall_ms, config.limit_ms, config.basis), None, []) if over else None


def _out_of_memory(case, config, out, comparer):
    """The kernel killed a process of the program's cgroup for memory: a failure whatever the exit status (a parent that exits 0 over an
    OOM-killed child is never a pass)."""
    return ("memory limit", None, [f"  memory.events: oom_kill {out.memory['oomKill']}"]) if out.memory.get("oomKill") else None


def _mismatched(case, config, out, comparer):
    return ("output differs", *_difference(case, comparer, comparer.mismatch)) if comparer.mismatch is not None else None


def _flooded(case, config, out, comparer):
    return ("standard error overflow", None, []) if out.end == "flood" else None


def _bad_exit(case, config, out, comparer):
    return (_exit_reason(out.returncode), None, []) if out.returncode != 0 else None


def _short(case, config, out, comparer):
    return None if comparer.exact else ("output ends early", *_difference(case, comparer, comparer.offset))


def _judge(case, config, out, comparer):
    """(reason, first mismatch dict or None, detail lines) of a failed case, or None for a pass: time, memory, then output, then status."""
    for check in (_over_time, _out_of_memory, _mismatched, _flooded, _bad_exit, _short):
        failed = check(case, config, out, comparer)
        if failed is not None:
            return failed
    return None


def _difference(case, comparer, byte):
    lines = rp.output_difference_lines(f"{case.id}.out", case.expected, bytes(comparer.kept), byte, comparer.actual_window())
    return {"byte": byte}, lines


def _stderr_record(path):
    size = os.path.getsize(path) if os.path.exists(path) else 0
    return {"known": True, "bytes": size, "truncated": size > ex.CAPTURE_CAP, "tail": ex.read_tail(path)}


def run_case(case, config):
    """Run `bash run.sh` for one olympiad case and judge it -> CaseResult. Raises LaunchFailed (no verdict) when no sandbox was reported."""
    stderr_path = os.path.join(config.scratch, f"{case.id}.stderr")
    comparer = Comparer(case.expected, config.capture_cap)
    out = _execute(case, config, comparer, stderr_path)
    capture = rp.capture_record(comparer.truncated, len(comparer.kept), comparer.total)
    program = out.launched.program
    record = rp.plain_lifecycle(out.leftover, _stderr_record(stderr_path), out.memory, capture, out.road, program)
    failed = _judge(case, config, out, comparer)
    tail = rp.capture_lines(capture) + (rp.memory_line(out.memory),) + rp.leftover_lines(out.leftover) + rp.fork_lines(program)
    launch_ms = out.launched.launch_ms
    if failed is None:
        return CaseResult(case.id, "PASS", "", 1, case.r_ids, tail, None, record, out.wall_ms, case.comparison, launch_ms)
    reason, mismatch, lines = failed
    return CaseResult(case.id, "FAIL", reason, 0, case.r_ids, tuple(lines) + tail, mismatch, record, out.wall_ms, case.comparison, launch_ms)
