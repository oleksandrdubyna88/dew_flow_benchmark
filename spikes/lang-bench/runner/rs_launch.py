"""rs_launch: starting a case's program through the launch prefix and finding out whether it really started (B5, B6, B1).

With `--launch-status N` the launcher (the layer's `systemd-run … bwrap --json-status-fd N … bash run.sh`) must report `child-pid` before
it exits: the line is the START OF THE CASE CLOCK and the proof that the launcher started; it does not say the box is complete. bwrap writes it right after it clones the sandbox, BEFORE the
box's mounts are made, so a few milliseconds of box set-up are inside the case clock; the plan fixes the start at `child-pid` and it is not
moved. No `child-pid` before the launcher exits (or within LAUNCH_CEILING_MS), a malformed or doubled status line before it, an unknown
record before it, or a cgroup that does not exist after it, is `LaunchFailed` with the tail of the prefix's standard error: exit 3, never a
case verdict. After `child-pid` everything is the case. The program's pid is found by the pump (rs_track.py), never here.
`launchMs` is the time from the spawn to `child-pid`; `emptyProgramMs` is an empty program timed through the same prefix and clock.

A launcher is registered in ACTIVE from the moment it exists, with the signals the runner stops on deferred around the spawn, so that
`abandon_active` can stop it whatever instruction the interrupt lands on.
"""
import contextlib
import selectors
import signal
import subprocess
from typing import NamedTuple, Optional

import rs_exec as ex
import rs_pump
import rs_status
import rs_track
import rs_view

EMPTY_PROGRAM = ("bash", "-c", ":")
STOPPING_SIGNALS = (signal.SIGTERM, signal.SIGINT, signal.SIGHUP)
ACTIVE = []


class LaunchFailed(Exception):
    """The launcher did not report `child-pid` as it must. `stderr` is the bounded tail of what the prefix wrote."""

    def __init__(self, case_id, reason, stderr):
        super().__init__(f"launch failed for case {case_id}: {reason}")
        self.case_id, self.reason, self.stderr = case_id, reason, stderr


class Launched:
    """One started case: the launcher, its pidfd, the clock start, the status pipe and the tracker of the program's pid."""

    def __init__(self, proc, launcher_fd, clock_ns, launch_ms, reader, pipe_fd, tracker):
        self.proc, self.launcher_fd, self.clock_ns, self.launch_ms = proc, launcher_fd, clock_ns, launch_ms
        self.reader, self.pipe_fd, self.tracker = reader, pipe_fd, tracker

    def drain_status(self):
        """Read what the status pipe still holds (the `exit-code` line may arrive after the last wake-up)."""
        if self.pipe_fd is not None:
            rs_status.pull(self.pipe_fd, self.reader)

    def status_code(self, returncode):
        """The program's status: bwrap's `exit-code` line when there is one, else the launcher's own."""
        self.drain_status()
        code = self.reader.exit_code if self.reader is not None else None
        return returncode if code is None else code

    def close(self):
        """Close what the case holds, once: a second call finds nothing (a closed number may be somebody else's by now)."""
        _forget(self.proc)
        self.tracker.close()
        ex.close_fd(self.launcher_fd)
        ex.close_fd(self.pipe_fd)
        self.launcher_fd = self.pipe_fd = None
        _close_streams(self.proc)


class EmptyRun(NamedTuple):
    """The control of F1: an empty program through the same prefix. `wall_ms` is None when its end instant is unknown."""
    wall_ms: Optional[int]
    launch_ms: Optional[int]
    program: dict


# ---- the launcher, registered and never left running -----------------------------------------------------------------------------

def _forget(proc):
    ACTIVE[:] = [entry for entry in ACTIVE if entry[0] is not proc]


@contextlib.contextmanager
def _deferred_signals():
    """The stopping signals wait until the launcher is registered: an interrupt can land between the Popen and the name it is bound to."""
    before = signal.pthread_sigmask(signal.SIG_BLOCK, STOPPING_SIGNALS)
    try:
        yield
    finally:
        signal.pthread_sigmask(signal.SIG_SETMASK, before)


def _owns_cgroup(lifecycle):
    return lifecycle.directory is not None and lifecycle.present()


def _abandon(proc, lifecycle):
    """Stop a launcher whose case will not go on: its group first while the leader is not yet reaped (a reaped leader's pid may be someone
    else's by now), then the cgroup once it exists (a program behind `--new-session` is not in the group)."""
    reaped = proc.returncode is not None
    if not reaped:
        ex.signal_group(proc.pid, signal.SIGKILL)
    if _owns_cgroup(lifecycle):
        lifecycle.kill(proc)
    elif not reaped:
        proc.wait()


def abandon_active():
    """What the runner does when it is stopped: every launcher still registered is stopped, and forgotten."""
    for proc, lifecycle in list(ACTIVE):
        _abandon(proc, lifecycle)
        _forget(proc)


def _spawn(argv, cwd, stderr_path, status_fd, streams):
    """The launcher in a session of its own, with the V11 environment and the status descriptor at its number."""
    try:
        with open(stderr_path, "wb") as err:
            return subprocess.Popen(argv, cwd=cwd, stdin=streams[0], stdout=streams[1], stderr=err, start_new_session=True,
                                    env=ex.program_env(), pass_fds=() if status_fd is None else (status_fd,))
    finally:
        if status_fd is not None:
            rs_status.release(status_fd)


def _spawn_registered(config, argv, cwd, stderr_path, streams, lifecycle):
    """(Popen, read end of the status pipe or None): registered in ACTIVE before any stopping signal can be handled."""
    with _deferred_signals():
        pipe_fd = None if config.status_fd is None else rs_status.open_pipe(config.status_fd)
        try:
            proc = _spawn(argv, cwd, stderr_path, config.status_fd, streams)
        except BaseException:
            ex.close_fd(pipe_fd)
            raise
        ACTIVE.append((proc, lifecycle))
    return proc, pipe_fd


# ---- the launch status -----------------------------------------------------------------------------------------------------------

def _wake(sel, remaining_ms):
    """What woke the selector, as the names given at `register`, after at most the exact remaining time."""
    return {key.data for key, _ in sel.select(remaining_ms / 1000)}


def _read_status(sel, woken, reader, pipe_fd):
    if "status" in woken and not rs_status.pull(pipe_fd, reader):
        sel.unregister(pipe_fd)  # nobody holds the write end any more: the pipe would wake the selector for ever


def _on_exit(reader, pipe_fd):
    rs_status.pull(pipe_fd, reader)  # a line written just before the exit is in the pipe already
    return "exited" if reader.child_pid is None else "started"


def _after_wake(reader, woken, pipe_fd):
    if reader.problem is not None:
        return "bad"
    return _on_exit(reader, pipe_fd) if "exit" in woken else None


def _await_child_pid(pidfd, reader, pipe_fd, deadline_ms):
    """'started' once the `child-pid` line is whole, 'bad' at a line that breaks the status rules before it, 'exited' when the launcher exits
    without it, 'late' at the deadline."""
    with selectors.DefaultSelector() as sel:
        sel.register(pipe_fd, selectors.EVENT_READ, "status")
        sel.register(pidfd, selectors.EVENT_READ, "exit")
        while reader.child_pid is None:
            remaining = deadline_ms - ex.now_ms()
            if remaining <= 0:
                return "late"
            woken = _wake(sel, remaining)
            _read_status(sel, woken, reader, pipe_fd)
            verdict = _after_wake(reader, woken, pipe_fd)
            if verdict:
                return verdict
    return "started"


def _status_problem(how, reader):
    if how == "late":
        return f"no child-pid within {ex.LAUNCH_CEILING_MS} ms (the launcher was killed)"
    if how == "exited":
        return "the launcher exited before it reported child-pid"
    return None if reader.problem is None else f"the launch status is malformed: {reader.problem}"


def _cgroup_missing(lifecycle):
    return lifecycle.directory is not None and not lifecycle.present()


def _problem(how, reader, lifecycle):
    """Why the launch is no launch, or None."""
    found = _status_problem(how, reader)
    if found is None and _cgroup_missing(lifecycle):
        return f"the cgroup {lifecycle.directory} does not exist after child-pid"
    return found


def _stop_unconfirmed(proc, child_pid):
    """Stop a box that is not in the cgroup it was said to be in, WITHOUT writing to that cgroup (it is not the box's): SIGKILL through a
    pidfd of child-pid (the box's init: its pid namespace goes with it), then the launcher's own group while its leader is unreaped."""
    fd = ex.pidfd_open(child_pid)
    try:
        if fd is not None:
            signal.pidfd_send_signal(fd, signal.SIGKILL)
    finally:
        ex.close_fd(fd)
    if proc.returncode is None:
        ex.kill_group(proc)


def _confirm_box(proc, reader, lifecycle, case_id, stderr_path):
    """After child-pid the box must belong to the cgroup named for it (`/proc/<child-pid>/cgroup`), else it is stopped and the launch failed."""
    reason = lifecycle.mismatch(reader.child_pid)
    if reason is not None:
        _stop_unconfirmed(proc, reader.child_pid)
        raise LaunchFailed(case_id, reason, ex.read_tail(stderr_path))


def _clock(proc, lifecycle, launcher_fd, pipe_fd, case_id, stderr_path):
    """(StatusReader, instant of `child-pid` in ns), or (None, None) without `--launch-status`; LaunchFailed when no child-pid was reported."""
    if pipe_fd is None:
        return None, None
    if launcher_fd is None:
        raise OSError("os.pidfd_open is not available: the launch status cannot be awaited")
    reader = rs_status.StatusReader()
    how = _await_child_pid(launcher_fd, reader, pipe_fd, ex.now_ms() + ex.LAUNCH_CEILING_MS)
    problem = _problem(how, reader, lifecycle)
    if problem is not None:
        _abandon(proc, lifecycle)
        raise LaunchFailed(case_id, problem, ex.read_tail(stderr_path))
    _confirm_box(proc, reader, lifecycle, case_id, stderr_path)
    return reader, reader.child_seen_ns


def launch_ms(spawned, reported):
    """The time from the spawn to `child-pid`, rounded up like the wall; None without a launch status."""
    return None if reported is None else ex.ceil_ms(reported - spawned)


def _begun(proc, launcher_fd, pipe_fd, spawned, case_id, lifecycle, stderr_path):
    reader, reported = _clock(proc, lifecycle, launcher_fd, pipe_fd, case_id, stderr_path)
    tracker = rs_track.Tracker(proc, lifecycle, box=pipe_fd is not None)
    tracker.attach(reader)
    clock = spawned if reported is None else reported
    launch = launch_ms(spawned, reported)
    return Launched(proc, launcher_fd, clock, launch, reader, pipe_fd, tracker)


def _close_streams(proc):
    for stream in (proc.stdin, proc.stdout):
        if stream is not None and not stream.closed:
            stream.close()


def _release(proc, launcher_fd, pipe_fd):
    """Close everything the runner holds for a launcher that will not become a case: its descriptors and the ends of its pipes."""
    _forget(proc)
    ex.close_fd(launcher_fd)
    ex.close_fd(pipe_fd)
    _close_streams(proc)


def _begin_or_abandon(proc, launcher_fd, pipe_fd, spawned, case_id, lifecycle, stderr_path):
    try:
        return _begun(proc, launcher_fd, pipe_fd, spawned, case_id, lifecycle, stderr_path)
    except LaunchFailed:  # stopped and reaped already
        _release(proc, launcher_fd, pipe_fd)
        raise
    except BaseException:  # the runner itself is being stopped: never leave a launcher running
        _abandon(proc, lifecycle)
        _release(proc, launcher_fd, pipe_fd)
        raise


def start(config, case_id, lifecycle, stderr_path, cwd, streams=(subprocess.PIPE, subprocess.PIPE), program=ex.RUN_PROGRAM):
    """Spawn the launcher for one case and wait for its `child-pid` when `--launch-status` is given -> Launched.
    Raises EnvironmentProblem (the cgroup is not this case's), LaunchFailed (nothing of it is left running) or OSError when the launcher
    cannot be started at all."""
    lifecycle.check_free()
    argv = ex.launch_argv(config.prefix, case_id, program)
    spawned = ex.now_ns()
    proc, pipe_fd = _spawn_registered(config, argv, cwd, stderr_path, streams, lifecycle)
    return _begin_or_abandon(proc, ex.pidfd_of(proc), pipe_fd, spawned, case_id, lifecycle, stderr_path)


def _empty_wall(end, pump, launched):
    return None if end != "exited" or pump.exit_ns is None else ex.ceil_ms(pump.exit_ns - launched.clock_ns)


def time_empty(config, lifecycle_for, stderr_path):
    """An empty program (`bash -c :`) timed through the same prefix, clock and pump: the control F1 asks for -> EmptyRun. Its `{case}` is
    EMPTY_CASE, so a layer whose prefix binds a per-case view or cgroup must provide one by that name (no sample may have that id). The
    record says which pid the wait was on, and the launch time."""
    lifecycle = lifecycle_for(config, ex.EMPTY_CASE)
    streams = (subprocess.DEVNULL, subprocess.DEVNULL)
    launched = start(config, ex.EMPTY_CASE, lifecycle, stderr_path, rs_view.start_dir(config, ex.EMPTY_CASE), streams, EMPTY_PROGRAM)
    pump = None
    try:
        pump = rs_pump.Pump(launched, b"", None, stderr_path)
        end = pump.run(launched.clock_ns + ex.LAUNCH_CEILING_MS * ex.NS_PER_MS)
        wall = _empty_wall(end, pump, launched)
        lifecycle.settle(launched, end)
        return EmptyRun(wall, launched.launch_ms, launched.tracker.signalled())
    except BaseException:
        _abandon(launched.proc, lifecycle)
        raise
    finally:
        if pump is not None:
            pump.close()
        launched.close()
