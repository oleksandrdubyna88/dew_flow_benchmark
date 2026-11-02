"""rs_abort: a runtime's own out-of-memory abort (plan change 5.1, parent D15's OP-K; evidence added in fix batch 1, O7). Under the memory view the execution
layer gives a program (its own cgroup, and .NET's hard limit equal to the case's limit) a genuinely over-limit program usually ends in its runtime's abort
before the kernel's oom_kill. Both are `memory limit`, but only WITH evidence: `memory.peak` is at least half of `memory.max` (both are in the record).

  .NET: exit status 134 (SIGABRT, as bwrap reports it) AND a stderr line exactly `Out of memory.`, OR an `Unhandled exception.` line with
        `System.OutOfMemoryException` anywhere in the stderr tail (also one wrapped in an `AggregateException`)
  V8:   exit status 134 AND a stderr line that contains `JavaScript heap out of memory`

It is never inferred from exit status 134 alone (an `abort()` with another message is a runtime failure), never from a message alone (a program that prints
the line and exits 1 is not an abort), and never without the peak (a program that fakes the line at a low peak is a runtime failure). The end of the stderr
file is read, whatever its size: V8 prints its stack trace after its message.
"""
import rs_exec as ex

ABORT_STATUS = 134
SCAN_BYTES = 64 * 1024
DOTNET_LINE = "Out of memory."
UNHANDLED = "Unhandled exception."
DOTNET_EXCEPTION = "System.OutOfMemoryException"
V8_TEXT = "JavaScript heap out of memory"


def has_evidence(memory):
    """True when `memory.peak` and a numeric `memory.max` were read and the peak is at least half of the limit."""
    peak, limit = memory.get("peakBytes"), memory.get("maxBytes")
    return bool(memory.get("known")) and peak is not None and limit is not None and peak * 2 >= limit


def _dotnet(lines, text):
    if DOTNET_LINE in lines:
        return True
    return any(line.startswith(UNHANDLED) for line in lines) and DOTNET_EXCEPTION in text


def _kind(lines, text):
    if _dotnet(lines, text):
        return "dotnet"
    return "v8" if any(V8_TEXT in line for line in lines) else None


def runtime_abort(returncode, stderr_path, memory):
    """`"dotnet"`, `"v8"` or None: which runtime's own out-of-memory abort the program ended in (None without the exit status, the message or the evidence)."""
    if returncode != ABORT_STATUS or not has_evidence(memory):
        return None
    text = ex.read_tail(stderr_path, SCAN_BYTES)
    return _kind([line.rstrip("\r") for line in text.split("\n")], text)
