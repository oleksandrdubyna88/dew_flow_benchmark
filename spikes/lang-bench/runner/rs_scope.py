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


class CgroupMissing(ex.EnvironmentProblem):
    """`--cgroup-of` names a directory that does not exist and never did (on the plain path, where no launcher creates it): the launcher's
    group was stopped, and nothing can be measured through it. Exit 3."""


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
        self.confirmed, self.box_fd, self.existed = False, None, False
        self.road = {"kill": "cgroup.kill", "leftoverEvidence": "cgroup.procs minus bwrap, the launcher and the wrapper, read after the exit and before the kill"}

    def memory(self, case_id=None):
        return cg.memory(self.directory)

    def present(self):
        """Whether the cgroup directory exists."""
        return os.path.isdir(self.directory)

    def check_free(self):
        """Before the spawn the directory must not exist, or be a cgroup2 node that holds no process and whose counters are zero (they are
        not reset between cases, and a stale `oom_kill` would be a verdict the program never earned: refused, exit 3)."""
        self.existed = self.present()
        if not self.existed:
            return
        if not cg.is_cgroup2(self.directory):
            raise CgroupInvalid(f"{self.directory} is not a cgroup2 node (no cgroup.controllers): nothing read from it or written to it means anything")
        self._refuse_if_used()

    def _refuse_if_used(self):
        held, stale = self._holds_a_process(), cg.stale_counters(self.directory)
        if held:
            raise CgroupBusy(f"the cgroup {self.directory} already holds a process or cannot be read: it is not this case's to measure or kill")
        if stale:
            raise CgroupBusy(f"the cgroup {self.directory} is not new: {', '.join(stale)} (counters are not reset between cases)")

    def _holds_a_process(self):
        """`populated` of cgroup.events when it says (1: a live process is in it or below it; 0: none, whatever zombies are still listed),
        else the listing, where one that cannot be read counts as held."""
        populated = cg.populated_of(self.directory)
        if populated is not None:
            return populated == 1
        held = cg.procs(self.directory)
        return held is None or bool(held)

    def mismatch(self, child_pid):
        """After child-pid: why the box is NOT in this cgroup, or None. The pidfd of `child-pid` is opened FIRST and kept (`box_fd`): the
        membership is judged only while it says the box is still running, and a box that has to be stopped is stopped through it. A box
        already gone is not a mismatch (it only leaves the cgroup unconfirmed, so its memory stays unknown). The box must be IN the named
        cgroup, not in a descendant of it."""
        if not cg.is_cgroup2(self.directory):
            return f"{self.directory} is not a cgroup2 node"
        self.box_fd = ex.pidfd_open(child_pid)
        inside = self._inside(child_pid)
        self.confirmed = inside is True
        return None if inside is not False else self._where(child_pid)

    def _inside(self, child_pid):
        """True or False while the box (pinned by `box_fd`) still runs, None when it is gone and cannot be judged."""
        running = self.box_fd is not None and not ex.pidfd_ready(self.box_fd)
        return cg.in_cgroup(child_pid, self.directory) if running else None

    def _where(self, child_pid):
        """The reason, naming the cgroup that was expected and the one the box was seen in (from /proc/<child-pid>/cgroup)."""
        root, seen = cg.cgroup2_root(self.directory) or "", cg.pid_cgroup_path(child_pid)
        return f"the box (child-pid {child_pid}) is not in the cgroup {self.directory}: it is in {root.rstrip('/')}{seen}"

    def release_box(self):
        ex.close_fd(self.box_fd)
        self.box_fd = None

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
        raise self._missing(failure, was_readable)

    def _missing(self, failure, was_readable):
        """What a cgroup that is not there at the kill is: one that was there (listed before the spawn or just before the kill) and went is
        the runner's cleanup failing to hold it (CleanupFailed, exit 5); one that never existed is the environment (exit 3)."""
        if was_readable or self.existed:
            return failure
        return CgroupMissing(f"the cgroup {self.directory} does not exist ({failure}): the launcher's group was stopped")

    def _seen(self, launched):
        """A counter counts only for a cgroup the case was seen in. With a box: the box was confirmed in it, or some process was listed in it.
        On the plain path the LAUNCHER must have been seen in it (listed in `cgroup.procs`, or `/proc/<pid>/cgroup` names it): a cgroup the
        program does not run in has counters of 0 that mean nothing, and absent is not zero."""
        if self.box:
            return self.confirmed or launched.tracker.seen
        return launched.tracker.saw_launcher or cg.member_of(launched.proc.pid, self.directory) is True

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
