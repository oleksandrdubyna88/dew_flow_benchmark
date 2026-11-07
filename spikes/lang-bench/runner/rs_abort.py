"""rs_abort: a runtime's own out-of-memory abort. A runtime that aborts itself on running out of memory is a memory-limit verdict, like a kernel kill.
Under the memory view the execution layer gives a program (its own cgroup, and the first runtime's hard limit equal to the case's limit)
a genuinely over-limit program usually ends in its runtime's abort before the kernel's oom_kill.
Both are `memory limit`, but only WITH evidence: `memory.peak` is at least half of `memory.max` (both are in the record).

  First runtime:  exit status 134 (SIGABRT, as bwrap reports it) AND a stderr line exactly `Out of memory.`,
        OR an `Unhandled exception.` line with `System.OutOfMemoryException` anywhere in the stderr tail (also one wrapped in an `AggregateException`)
  Second runtime: exit status 134 AND a stderr line that contains that runtime's own out-of-memory message (the last constant below)

It is never inferred from exit status 134 alone (an `abort()` with another message is a runtime failure), never from a message alone (a program that prints
the line and exits 1 is not an abort), and never without the peak (a program that fakes the line at a low peak is a runtime failure). The end of the stderr
file is read, whatever its size: the second runtime prints its stack trace after its message.
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


def _line_kind(stderr_path):
    """The runtime's label (what `_kind` returns) or None: whose out-of-memory line ends the stderr file, whatever the exit status."""
    text = ex.read_tail(stderr_path, SCAN_BYTES)
    return _kind([line.rstrip("\r") for line in text.split("\n")], text)


def _facts(abort, below, child):
    return {"runtimeAbort": abort, "abortBelowEvidence": below, "childAbortLine": child}


NO_ABORT = _facts(None, False, False)


def abort_facts(returncode, stderr_path, memory):
    """What the stderr line and the exit status say about a memory abort (a memory abort): `{runtimeAbort, abortBelowEvidence, childAbortLine}`.

    - exit 134 and a runtime's line WITH evidence (`memory.peak` at least half of `memory.max`): `runtimeAbort` is that runtime's label;
    - exit 134 and the line WITHOUT evidence (the peak below the threshold, or not read): a runtime error, `abortBelowEvidence` is true (never
      silent);
    - the line with exit 0, or with ANY exit status other than 134 (exit 1 included, the commonest case): a CHILD's abort the program went on from (or
      failed on), `childAbortLine` is true; the case is judged on its output and status, and a non-zero one carries no `abortBelowEvidence`. A kernel
      `oom_kill` is `memory limit` by its own cause, so no flag explains it.
    """
    kind = _line_kind(stderr_path)
    if kind is None:
        return NO_ABORT
    killed = bool(memory.get("oomKill"))
    return _own(kind, memory, killed) if returncode == ABORT_STATUS else _facts(None, False, not killed)


def _own(kind, memory, killed):
    """The facts of an exit 134 with a runtime's line: the abort when the evidence is there, else the flag (unless the kernel's kill explains it)."""
    evidence = has_evidence(memory)
    return _facts(kind if evidence else None, not evidence and not killed, False)


def runtime_abort(returncode, stderr_path, memory):
    """The runtime's label or None: which runtime's own out-of-memory abort the program ended in (None without the exit status, the message or the evidence)."""
    return abort_facts(returncode, stderr_path, memory)["runtimeAbort"]
