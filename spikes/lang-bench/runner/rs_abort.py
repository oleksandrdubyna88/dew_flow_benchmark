"""rs_abort: a runtime's own out-of-memory abort (plan change 5.1, parent D15's OP-K). Under the memory view the execution layer gives a program
(its own cgroup, and .NET's hard limit equal to the case's limit) a genuinely over-limit program usually ends in its runtime's abort before the
kernel's oom_kill. Both are `memory limit`:

  .NET: exit status 134 (SIGABRT, as bwrap reports it) AND a stderr line that is exactly `Out of memory.`
  V8:   exit status 134 AND a stderr line that contains `JavaScript heap out of memory`

It is never inferred from exit status 134 alone (an `abort()` with another message is a runtime failure) and never from the message alone (a
program that prints the line and exits 1 is not an abort). The end of the stderr file is read, whatever its size: V8 prints a stack trace after
its message.
"""
import rs_exec as ex

ABORT_STATUS = 134
SCAN_BYTES = 64 * 1024
DOTNET_LINE = "Out of memory."
V8_TEXT = "JavaScript heap out of memory"


def _kind(lines):
    if DOTNET_LINE in lines:
        return "dotnet"
    return "v8" if any(V8_TEXT in line for line in lines) else None


def runtime_abort(returncode, stderr_path):
    """`"dotnet"`, `"v8"` or None: which runtime's own out-of-memory abort the program ended in."""
    if returncode != ABORT_STATUS:
        return None
    return _kind([line.rstrip("\r") for line in ex.read_tail(stderr_path, SCAN_BYTES).split("\n")])
