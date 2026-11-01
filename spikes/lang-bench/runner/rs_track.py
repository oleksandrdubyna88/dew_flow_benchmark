"""rs_track: which process is the program, and what else the case leaves in its cgroup (B1, D38).

With a launch status (the box is bwrap) the launcher is NEVER the program. The program is the process of `cgroup.procs` whose PARENT is a
bwrap process: `bash run.sh` execs the program, so it is the one child of bwrap's init (bwrap is told by identity, rs_cgroup.py). A tool or
subshell that bash starts has bash for a parent and is never taken for the program. Two such processes are the finding `fork`. The look is
a wake-up of the pump (every millisecond until the program shows, then every 20 ms), so it never delays the program's stdin or stdout.
Without a launch status there is no box: the launcher execs the program and is the program.

`fork` is judged over the whole case from what is NOT bwrap, not the launcher, not the wrapper and not the program: the most such processes
seen in two successive looks (a tool that is gone a moment later is no fork) and what the leftover read finds after the program's exit.
It is reported as observed and never decides which pid is the program.
"""
import time

import rs_cgroup as cg
import rs_exec as ex

FOUND = "cgroup.procs: the process whose parent is a bwrap process"
LAUNCHER = "the launcher's own pid (no launch status: the launcher execs the program)"
NOT_YET = "no process with a bwrap parent was seen in cgroup.procs"


def _length(found):
    return 0 if found is None else len(found)


class Tracker:
    """One case's view of its cgroup. `pid` is the program's host pid or None; `pidfd` pins it once it is known."""

    def __init__(self, proc, lifecycle, box):
        self.proc, self.directory = proc, lifecycle.directory
        self.identity, self.wrapper_file, self.box = lifecycle.identity, lifecycle.wrapper_file, box
        self.pid = None if box else proc.pid
        self.found = NOT_YET if box else LAUNCHER
        self.pidfd, self.comm, self.many = None, None, 0
        self.reads, self.extra_max, self.previous, self.reader = 0, 0, frozenset(), None

    def _listed(self):
        return cg.procs(self.directory) if self.directory else None

    def attach(self, reader):
        """The status reader of the case: once bwrap has written `exit-code` the program is gone and nothing is taken for it any more."""
        self.reader = reader

    def _ended(self):
        return self.reader is not None and self.reader.exit_code is not None

    def _unsettled(self):
        return self.box and self.directory is not None and self.pid is None

    def seeking(self):
        return self._unsettled() and self.many == 0 and not self._ended()

    def tracking(self):
        return self.directory is not None

    def _others(self, pids):
        """The pids that are neither bwrap, nor the launcher, nor the wrapper, nor a process that has already exited."""
        skip = {self.proc.pid, cg.wrapper_pid(self.wrapper_file)}
        return [pid for pid in pids if self._counts(pid, skip)]

    def _counts(self, pid, skip):
        return pid not in skip and not cg.is_bwrap(pid, self.identity) and not cg.is_zombie(pid)

    def _still(self, pid):
        """After the pidfd pins the pid: it is still in the cgroup and its parent is still a bwrap process (a recycled pid is neither)."""
        pids = cg.procs(self.directory)
        return pids is not None and pid in pids and cg.is_bwrap(cg.ppid_of(pid), self.identity)

    def _adopt(self, pid):
        fd = ex.pidfd_open(pid)
        if fd is not None and self._still(pid):
            self.pid, self.pidfd, self.found, self.comm = pid, fd, FOUND, cg.comm_of(pid)
        else:
            ex.close_fd(fd)

    def _owned(self, others):
        return [pid for pid in others if cg.is_bwrap(cg.ppid_of(pid), self.identity)]

    def _choose(self, others):
        owned = self._owned(others)
        if len(owned) > 1:
            self.many = len(owned)
        elif owned:
            self._adopt(owned[0])

    def _count(self, others):
        extras = frozenset(pid for pid in others if pid != self.pid) if self.pid is not None else frozenset()
        self.extra_max = max(self.extra_max, len(extras & self.previous))
        self.previous = extras

    def look(self):
        """One read of `cgroup.procs`: choose the program while it is not known, and count what else is there."""
        pids = self._listed()
        if pids is None:
            return
        self.reads += 1
        others = self._others(pids)
        if self.seeking():
            self._choose(others)
        self._count(others)
        self._refresh_comm()

    def _refresh_comm(self):
        """The program's name as last seen alive: bash before it execs, the program after (evidence for the record, never for a decision)."""
        if self.pid is not None:
            self.comm = cg.comm_of(self.pid) or self.comm

    def leftover(self):
        """What is in the cgroup besides bwrap, the launcher, the wrapper and the program (read after the program's exit, before the kill);
        None when `cgroup.procs` cannot be read."""
        pids = self._listed()
        if pids is None:
            return None
        return self._persisting([pid for pid in self._others(pids) if pid != self.pid])

    def _listed_or_empty(self):
        found = self._listed()
        return [] if found is None else found

    def _persisting(self, first):
        """The ones of `first` that are still in the cgroup a moment later: a process caught in the middle of exiting (its exe is already
        unreadable, so it looks like no bwrap) is gone by then, a hidden child is not."""
        if not first:
            return first
        time.sleep(ex.RECHECK_S)
        still = self._others(self._listed_or_empty())
        return [pid for pid in first if pid in still]

    def signalled(self):
        return {"signal": None, "pid": self.pid, "found": self.found, "comm": self.comm, "launcher": self.proc.pid}

    def fork(self, leftover):
        if self.many:
            return {"known": True, "found": True, "count": self.many}
        if not self.reads or self.pid is None:
            return {"known": False, "why": "the program's pid was not settled by looking in cgroup.procs"}
        count = 1 + max(self.extra_max, _length(leftover))
        return {"known": True, "found": count > 1, "count": count}

    def close(self):
        ex.close_fd(self.pidfd)
