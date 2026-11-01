"""rs_scope: the lifecycle of a case whose program runs in a cgroup the layer made (`--cgroup-of`): leftovers, memory and the kill are read
and done through that cgroup, never through the launcher's process group (B1, B4).

The runner never measures or kills a cgroup this case does not own: the directory must not exist, or hold no process, before the spawn
(else `CgroupBusy`, exit 3). The order at the end of a case is fixed: the program's end is seen, `cgroup.procs` is read (the leftover
evidence), `memory.peak` and `memory.events` are read, the launcher gets a bounded moment to write its `exit-code` and go, and then
`cgroup.kill` is ALWAYS written, a leftover found or not, and `cgroup.procs` must read empty or the cleanup has failed. A kill first would
hide a lingering child and zero the peak. Where the cgroup is missing or the kill cannot be written, the launcher's process group is killed
as well.
"""
import os

import rs_cgroup as cg
import rs_exec as ex


class CgroupBusy(ex.EnvironmentProblem):
    """The cgroup directory named for this case already holds a process (or cannot be read): it is not this case's to measure or kill."""


def _wait_launcher(proc):
    """True when the launcher has gone, within the ceiling, by itself. Nothing is killed here."""
    if proc.returncode is not None:
        return True
    pidfd = ex.pidfd_of(proc)
    try:
        return ex.wait_exit(proc, pidfd, ex.now_ms() + ex.GONE_CEILING_MS)
    finally:
        ex.close_fd(pidfd)


def _reap_launcher(proc):
    """The launcher's status once it has gone. A launcher the cgroup kill did not reach (it is not in the cgroup) gets its own process group
    killed; CleanupFailed when that group stays."""
    if not _wait_launcher(proc):
        ex.kill_group(proc)
    return proc.wait()


class ScopeLifecycle:
    """The same methods as `PlainLifecycle`, over one cgroup directory. `identity` is the bwrap binary's (device, inode)."""

    def __init__(self, directory, identity=None, wrapper_file=None):
        self.directory, self.identity, self.wrapper_file = directory, identity, wrapper_file
        self.road = {"kill": "cgroup.kill", "leftoverEvidence": "cgroup.procs minus bwrap, the launcher and the wrapper, read after the exit and before the kill"}

    def memory(self, case_id=None):
        return cg.memory(self.directory)

    def present(self):
        """Whether the cgroup directory exists."""
        return os.path.isdir(self.directory)

    def check_free(self):
        """The directory must not exist, or hold no process, before the spawn."""
        if not self.present():
            return
        held = cg.procs(self.directory)
        if held is None or held:
            raise CgroupBusy(f"the cgroup {self.directory} already holds a process or cannot be read: it is not this case's to measure or kill")

    def kill(self, proc):
        """`1` into `cgroup.kill`, the launcher reaped, and then no pid left in the cgroup or CleanupFailed. A cgroup that is missing, or one
        whose kill cannot be written, gets the launcher's process group killed as well."""
        try:
            cg.kill(self.directory)
        except ex.CleanupFailed as failure:
            return self._fallback(proc, failure)
        _reap_launcher(proc)
        cg.wait_empty(self.directory)

    def _fallback(self, proc, failure):
        if proc.returncode is None:
            ex.kill_group(proc)
        if self.present():
            raise failure

    def settle(self, launched, end):
        exited = end == "exited"
        leftover = launched.tracker.leftover() if exited else None
        memory = self.memory()
        if exited:
            _wait_launcher(launched.proc)
        launched.drain_status()
        self.kill(launched.proc)
        return ex.Settled(launched.status_code(launched.proc.returncode), leftover, memory)


def lifecycle_for(config, case_id):
    """The lifecycle of one case: through its cgroup when `--cgroup-of` is given (`{case}` filled in), else the plain one."""
    if not config.cgroup:
        return config.lifecycle
    wrapper = config.wrapper_file.replace("{case}", ex.checked_case_id(case_id)) if config.wrapper_file else None
    return ScopeLifecycle(config.cgroup.replace("{case}", ex.checked_case_id(case_id)), cg.bwrap_identity(config.prefix), wrapper)
