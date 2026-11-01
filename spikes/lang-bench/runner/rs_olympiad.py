"""rs_olympiad: one olympiad case. `bash run.sh` gets the case's input on standard input; its standard output is compared as a STREAM
against the expected bytes and stops at the first difference (B9); standard error goes to a file, never a pipe. What is kept of the
output for the report is capped, and a capped capture is never what decides: a PASS needs the stream to have matched every expected
byte and nothing more. A wall time over the limit is a time limit even when the program exited by itself.
"""
import os
import signal
from typing import NamedTuple, Optional

import rs_exec as ex
import rs_launch as rl
import rs_pump
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
    wall_ns: Optional[int]
    returncode: int
    leftover: object
    memory: dict
    launched: object
    road: dict


def _launch(case, config, lifecycle, stderr_path):
    """Start the case in its own view when `--case-view` is given (fixtures written into it first), else in the workspace."""
    cwd = rs_view.start_dir(config, case.id)
    if config.case_view:
        rs_view.write_fixtures(cwd, case.fixtures)
    return rl.start(config, case.id, lifecycle, stderr_path, cwd)


def _wall_ns(pump, launched, ended):
    """The program's wall in ns: from the case clock to the end instant the pump took. A case cut off by the deadline ends now; one whose
    end instant is unknown (a box that gave neither a pid nor an exit-code record) has no wall."""
    if ended == "timeout":
        return ex.now_ns() - launched.clock_ns
    return None if pump.exit_ns is None else pump.exit_ns - launched.clock_ns


def _stop_quietly(lifecycle, proc):
    """Stop the program while another exception is on its way (an interrupt): a scope that is already gone must not turn that exception into
    CgroupLost, so the interrupt keeps its exit. A cleanup that fails otherwise (CleanupFailed) still speaks."""
    try:
        lifecycle.kill(proc)
    except ex.EnvironmentProblem:
        pass


def _execute(case, config, comparer, stderr_path):
    lifecycle = rs_scope.lifecycle_for(config, case.id)
    launched, pump = _launch(case, config, lifecycle, stderr_path), None
    try:
        pump = rs_pump.Pump(launched, case.stdin, comparer, stderr_path)
        end = pump.run(launched.clock_ns + config.limit_ms * ex.NS_PER_MS)
        wall = _wall_ns(pump, launched, end)
        settled = lifecycle.settle(launched, end)
        return Outcome(end, wall, settled.code, settled.leftover, settled.memory, launched, lifecycle.road)
    except BaseException:  # the runner itself is being stopped (a signal): never leave the program under test running
        _stop_quietly(lifecycle, launched.proc)
        raise
    finally:
        if pump is not None:
            pump.close()
        launched.close()


# ---- judging --------------------------------------------------------------------------------------------------------------------

def _exit_reason(code):
    return f"exit status {code}" if code > 0 else f"killed by signal {signal.Signals(-code).name}"


def _over_time(case, config, out, comparer):
    """The wall rule: a wall over the limit (in ns, so exactly the limit is not over it) is a time limit even when the program exited by
    itself. A case the deadline cut off is one too; a wall that is unknown (no end instant) cannot be over."""
    over = out.end == "timeout" or (out.wall_ns is not None and out.wall_ns > config.limit_ms * ex.NS_PER_MS)
    return (rp.time_text(ex.ceil_ms(out.wall_ns), config.limit_ms, config.basis), None, []) if over else None


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
    """(reason, first mismatch dict or None, detail lines) of a failed case, or None for a pass: memory, time, then output, then status."""
    for check in (_out_of_memory, _over_time, _mismatched, _flooded, _bad_exit, _short):
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


def _wall_ms(out):
    return None if out.wall_ns is None else ex.ceil_ms(out.wall_ns)


def run_case(case, config):
    """Run `bash run.sh` for one olympiad case and judge it -> CaseResult. Raises LaunchFailed (no verdict) when no sandbox was reported."""
    stderr_path = os.path.join(config.scratch, f"{case.id}.stderr")
    comparer = Comparer(case.expected, config.capture_cap)
    out = _execute(case, config, comparer, stderr_path)
    capture = rp.capture_record(comparer.truncated, len(comparer.kept), comparer.total)
    record = rp.plain_lifecycle(out.leftover, _stderr_record(stderr_path), out.memory, capture, out.road, out.launched.tracker)
    failed = _judge(case, config, out, comparer)
    tail = rp.capture_lines(capture) + (rp.memory_line(out.memory),) + rp.leftover_lines(out.leftover) + rp.fork_lines(record["fork"])
    verdict = ("PASS", "", 1, None, []) if failed is None else ("FAIL", failed[0], 0, failed[1], failed[2])
    lines = tuple(verdict[4]) + tail
    return CaseResult(case.id, verdict[0], verdict[1], verdict[2], case.r_ids, lines, verdict[3], record, _wall_ms(out), case.comparison, out.launched.launch_ms)
