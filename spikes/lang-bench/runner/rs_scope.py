"""rs_scope: the lifecycle of a case whose program runs in a cgroup the layer made (`--cgroup-of`): leftovers, memory and the kill are read
and done through that cgroup, never through the launcher's process group (B1, B4).

The order at the end of a case is fixed: the program's exit is seen, `cgroup.procs` is read (the leftover evidence: what is neither a
bwrap process, nor the wrapper, nor the program itself), `memory.peak` and `memory.events` are read, and only THEN anything is killed
(`cgroup.kill`); a kill first would hide a lingering child and zero the peak. The launcher is reaped last, by a bounded wait.
"""
import os
import time

import rs_cgroup as cg
import rs_exec as ex


def _reap_launcher(proc):
    """The launcher's status once it has gone; CleanupFailed when it is still there after the ceiling."""
    if proc.returncode is not None:
        return proc.returncode
    pidfd = ex.pidfd_of(proc)
    try:
        gone = ex.wait_exit(proc, pidfd, ex.now_ms() + ex.GONE_CEILING_MS)
    finally:
        if pidfd is not None:
            ex.close_fd(pidfd)
    if not gone:
        raise ex.CleanupFailed(f"the launcher {proc.pid} of the case could not be stopped: it is still there after {ex.GONE_CEILING_MS} ms")
    return proc.wait()


def _done_seeking(found, proc, deadline_ms):
    """A candidate showed, the launcher is gone, or the time is up. An unreadable `cgroup.procs` is not an end: without a launch status the
    layer may not have made the cgroup yet, a moment after the spawn."""
    return bool(found) or proc.poll() is not None or ex.now_ms() >= deadline_ms


class ScopeLifecycle:
    """The same methods as `PlainLifecycle`, over one cgroup directory."""

    def __init__(self, directory, out_dir=None):
        self.directory, self.out_dir = directory, out_dir
        self.road = {"kill": "cgroup.kill", "leftoverEvidence": "cgroup.procs minus bwrap and wrapper, read after the exit and before the kill"}

    def memory(self, case_id=None):
        return cg.memory(self.directory)

    def present(self):
        """Whether the cgroup directory exists yet (without a launch status the layer makes it a moment after the spawn)."""
        return os.path.isdir(self.directory)

    def kill(self, proc):
        """`1` into `cgroup.kill`, the launcher reaped, and then no pid left in the cgroup or CleanupFailed."""
        cg.kill(self.directory)
        _reap_launcher(proc)
        cg.wait_empty(self.directory)

    def candidates(self):
        """The program's candidate pids now (neither bwrap nor the wrapper), or None when `cgroup.procs` cannot be read."""
        return cg.program_candidates(self.directory, cg.wrapper_pid(self.out_dir))

    def program_of(self, proc):
        """The program's host pid: the one candidate in `cgroup.procs`, read until one shows, the launcher is gone or SEEK_MS has passed.
        Two or more is the finding `fork` (D38: run.sh execs the program), and then no pid is chosen: the launcher's exit is awaited."""
        deadline = ex.now_ms() + ex.SEEK_MS
        found = self.candidates()
        while not _done_seeking(found, proc, deadline):
            time.sleep(ex.POLL_FIRST_S)
            found = self.candidates()
        if found is None:
            return ex.Program(None, "cgroup.procs could not be read", None)
        if len(found) == 1:
            return ex.Program(found[0], "cgroup.procs minus bwrap and wrapper", 1)
        return ex.Program(None, f"cgroup.procs held {len(found)} candidates", len(found))

    def leftover(self, proc, program=None):
        """The pids that are still in the cgroup besides the program itself, or None when `cgroup.procs` cannot be read."""
        found = self.candidates()
        return None if found is None else [pid for pid in found if pid != program]

    def settle(self, launched, end):
        exited = end == "exited"
        leftover = self.leftover(launched.proc, launched.program_pid) if exited else None
        memory = self.memory()
        if not exited or leftover:
            self.kill(launched.proc)
        returncode = _reap_launcher(launched.proc)
        return ex.Settled(launched.status_code(returncode), leftover, memory)


def lifecycle_for(config, case_id):
    """The lifecycle of one case: through its cgroup when `--cgroup-of` is given (`{case}` filled in), else the plain one."""
    if not config.cgroup:
        return config.lifecycle
    return ScopeLifecycle(config.cgroup.replace("{case}", ex.checked_case_id(case_id)), config.out_dir)
