"""rs_scope: the lifecycle of a case whose program runs in a cgroup the layer made (`--cgroup-of`): leftovers, memory and the kill are read
and done through that cgroup, never through the launcher's process group (B1, B4).

The runner never measures or kills a cgroup this case does not own: the directory must not exist, or hold no process, before the spawn
(else `CgroupBusy`, exit 3). The order at the end of a case is fixed: the program's end is seen, `cgroup.procs` is read (the leftover
evidence), `memory.peak` and `memory.events` are read, the launcher gets a bounded moment to write its `exit-code` and go, and then
`cgroup.kill` is ALWAYS written, a leftover found or not, and `cgroup.procs` must read empty or the cleanup has failed. A kill first would
hide a lingering child (`cgroup.kill` does not reset `memory.peak`: the peak is read first so that it is read while the cgroup is still there). Where the cgroup is missing or the kill cannot be written, the launcher's process group is killed
as well.
"""
import os

import rs_cgroup as cg
import rs_exec as ex


class CgroupBusy(ex.EnvironmentProblem):
    """The cgroup directory named for this case already holds a process (or cannot be read): it is not this case's to measure or kill."""


class CgroupInvalid(ex.EnvironmentProblem):
    """The directory named for this case is not a cgroup2 node: nothing read from it or written to it means anything."""


class CgroupLost(ex.EnvironmentProblem):
    """The cgroup directory vanished after the launcher reported `child-pid`: the layer's scope is gone, so nothing can be measured or killed
    through it. Exit 3, never a case status."""


NEVER_SEEN = {"known": False, "why": "no process of the box was ever seen in the cgroup: its counters are not this case's"}


def _wait_launcher(proc):
    """True when the launcher has gone, within the ceiling, by itself. Nothing is killed and nothing is reaped here."""
    return ex.wait_gone(proc, ex.GONE_CEILING_MS)


def _reap_launcher(proc):
    """The launcher's status once it has gone. A launcher the cgroup kill did not reach (it is not in the cgroup) gets its own process group
    killed; CleanupFailed when that group stays."""
    if not _wait_launcher(proc):
        ex.kill_group(proc)
    return proc.wait()


class ScopeLifecycle:
    """The same methods as `PlainLifecycle`, over one cgroup directory. `identity` is the bwrap binary's (device, inode)."""

    def __init__(self, directory, identity=None, wrapper_file=None, box=False):
        self.directory, self.identity, self.wrapper_file, self.box = directory, identity, wrapper_file, box
        self.confirmed = False
        self.road = {"kill": "cgroup.kill", "leftoverEvidence": "cgroup.procs minus bwrap, the launcher and the wrapper, read after the exit and before the kill"}

    def memory(self, case_id=None):
        return cg.memory(self.directory)

    def present(self):
        """Whether the cgroup directory exists."""
        return os.path.isdir(self.directory)

    def check_free(self):
        """Before the spawn the directory must not exist, or be a cgroup2 node that holds no process and whose counters are zero (they are
        not reset between cases, and a stale `oom_kill` would be a verdict the program never earned: refused, exit 3)."""
        if not self.present():
            return
        if not cg.is_cgroup2(self.directory):
            raise CgroupInvalid(f"{self.directory} is not a cgroup2 node (no cgroup.controllers): nothing read from it or written to it means anything")
        self._refuse_if_used()

    def _refuse_if_used(self):
        held, stale = cg.procs(self.directory), cg.stale_counters(self.directory)
        if held is None or held:
            raise CgroupBusy(f"the cgroup {self.directory} already holds a process or cannot be read: it is not this case's to measure or kill")
        if stale:
            raise CgroupBusy(f"the cgroup {self.directory} is not new: {', '.join(stale)} (counters are not reset between cases)")

    def mismatch(self, child_pid):
        """After child-pid: why the box is NOT in this cgroup, or None. A box already gone is not a mismatch (it only leaves the cgroup
        unconfirmed, so its memory stays unknown)."""
        if not cg.is_cgroup2(self.directory):
            return f"{self.directory} is not a cgroup2 node"
        inside = cg.member_of(child_pid, self.directory)
        self.confirmed = inside is True
        return f"the box (child-pid {child_pid}) is not in the cgroup {self.directory}" if inside is False else None

    def kill(self, proc):
        """`1` into `cgroup.kill`, the launcher reaped, and then no pid left in the cgroup or CleanupFailed. A cgroup that is missing, or one
        whose kill cannot be written, gets the launcher's process group killed as well."""
        was_readable = cg.procs(self.directory) is not None
        try:
            cg.kill(self.directory)
        except ex.CleanupFailed as failure:
            return self._fallback(proc, failure, was_readable)
        _reap_launcher(proc)
        cg.wait_empty(self.directory, was_readable=was_readable)

    def _fallback(self, proc, failure, was_readable=False):
        """The cgroup kill could not be written. The launcher's group first, while the leader is not yet reaped; then, if the directory
        exists, every pid it lists, and a check that it is empty (CleanupFailed when it is not). In a box a directory that is gone is
        CgroupLost: a scope that vanished after child-pid is not a case status."""
        if proc.returncode is None:
            ex.kill_group(proc)
        if self.present():
            cg.kill_pids(self.directory)
            return cg.wait_empty(self.directory, was_readable=was_readable)
        if self.box:
            raise CgroupLost(f"the cgroup {self.directory} is gone after child-pid ({failure}): its processes were stopped through the launcher's group")
        raise failure

    def _seen(self, launched):
        """A counter counts only for a cgroup the box was seen in; without a box (the plain path) the directory is taken as it is."""
        return not self.box or self.confirmed or launched.tracker.seen

    def settle(self, launched, end):
        exited = end == "exited"
        leftover = launched.tracker.leftover() if exited else None
        memory = self.memory() if self._seen(launched) else NEVER_SEEN
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
    return ScopeLifecycle(config.cgroup.replace("{case}", ex.checked_case_id(case_id)), cg.bwrap_identity(config.prefix), wrapper, config.status_fd is not None)
