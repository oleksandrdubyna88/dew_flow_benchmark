"""rs_launch: starting a case's program through the launch prefix and finding out whether it really started (B5, B6, B1).

With `--launch-status N` the launcher (the layer's `systemd-run … bwrap --json-status-fd N … bash run.sh`) must report `child-pid` before
it exits: the line marks the sandbox ready and is the START OF THE CASE CLOCK, so the set-up of the box is not in the wall time. No
`child-pid` before the launcher exits (or within LAUNCH_CEILING_MS) is `LaunchFailed` with the tail of the prefix's standard error, which
the caller turns into exit 3: never a case verdict. With `--cgroup-of` the program's host pid is the one pid of `cgroup.procs` that is not
bwrap or the wrapper; the exit is then awaited on a pidfd of THAT pid (a grandchild of the runner, so `Popen.wait` could not do it).
`launchMs` is the time from the spawn to `child-pid`; `emptyProgramMs` is an empty program timed through the same prefix and clock.
"""
import selectors
import signal
import subprocess
import rs_exec as ex
import rs_status

EMPTY_PROGRAM = ("bash", "-c", ":")


class LaunchFailed(Exception):
    """The launcher did not report a running sandbox. `stderr` is the bounded tail of what the prefix wrote."""

    def __init__(self, case_id, reason, stderr):
        super().__init__(f"launch failed for case {case_id}: {reason}")
        self.case_id, self.reason, self.stderr = case_id, reason, stderr


class Launched:
    """One started case: the launcher, the descriptor its exit is awaited on, the clock start, and what the launch observed."""

    def __init__(self, proc, pidfds, clock_ms, launch_ms, reader, pipe_fd, program):
        self.proc, self.pidfds, self.clock_ms, self.launch_ms = proc, pidfds, clock_ms, launch_ms
        self.reader, self.pipe_fd, self.program = reader, pipe_fd, program
        self.program_pid = program.pid
        self.pidfd = pidfds[0] if pidfds else None

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
        for fd in self.pidfds + ((self.pipe_fd,) if self.pipe_fd is not None else ()):
            ex.close_fd(fd)


def _spawn(argv, cwd, stderr_path, status_fd, streams):
    """The launcher in a session of its own, with the V11 environment and the status descriptor at its number."""
    with open(stderr_path, "wb") as err:
        try:
            return subprocess.Popen(argv, cwd=cwd, stdin=streams[0], stdout=streams[1], stderr=err, start_new_session=True,
                                    env=ex.program_env(), pass_fds=() if status_fd is None else (status_fd,))
        finally:
            if status_fd is not None:
                rs_status.release(status_fd)


def _wake(sel, remaining_ms):
    """What woke the selector, as the names given at `register`, after at most the exact remaining time."""
    return {key.data for key, _ in sel.select(remaining_ms / 1000)}


def _read_status(sel, woken, reader, pipe_fd):
    if "status" in woken and not rs_status.pull(pipe_fd, reader):
        sel.unregister(pipe_fd)  # nobody holds the write end any more: the pipe would wake the selector for ever


def _on_exit(reader, pipe_fd):
    rs_status.pull(pipe_fd, reader)  # a line written just before the exit is in the pipe already
    return "exited" if reader.child_pid is None else "started"


def _await_child_pid(pidfd, reader, pipe_fd, deadline_ms):
    """'started' once the `child-pid` line is whole, 'exited' when the launcher exits without it, 'late' at the deadline."""
    with selectors.DefaultSelector() as sel:
        sel.register(pipe_fd, selectors.EVENT_READ, "status")
        sel.register(pidfd, selectors.EVENT_READ, "exit")
        while reader.child_pid is None:
            remaining = deadline_ms - ex.now_ms()
            if remaining <= 0:
                return "late"
            woken = _wake(sel, remaining)
            _read_status(sel, woken, reader, pipe_fd)
            if "exit" in woken:
                return _on_exit(reader, pipe_fd)
    return "started"


def _failure(case_id, how, proc, stderr_path):
    """Stop what is left of a launcher that never reported `child-pid`, and name the failure with the tail of the prefix's stderr."""
    if how == "late":
        ex.kill_group(proc)
    proc.wait()
    reason = "the launcher exited before the sandbox reported child-pid" if how == "exited" else \
        f"no child-pid within {ex.LAUNCH_CEILING_MS} ms (the launcher was killed)"
    return LaunchFailed(case_id, reason, ex.read_tail(stderr_path))


def _clock(proc, launcher_fd, pipe_fd, case_id, stderr_path):
    """(StatusReader, instant of `child-pid`), or (None, None) without `--launch-status`; LaunchFailed when no sandbox was reported."""
    if pipe_fd is None:
        return None, None
    if launcher_fd is None:
        raise OSError("os.pidfd_open is not available: the launch status cannot be awaited")
    reader = rs_status.StatusReader()
    how = _await_child_pid(launcher_fd, reader, pipe_fd, ex.now_ms() + ex.LAUNCH_CEILING_MS)
    if how != "started":
        raise _failure(case_id, how, proc, stderr_path)
    return reader, ex.now_ms()


def _pidfds(program, proc, launcher_fd):
    """The descriptors the exit can be awaited on, the program's own first (it is a grandchild behind a box, not the runner's child)."""
    own = ex.pidfd_open(program.pid) if program.pid not in (None, proc.pid) else None
    return tuple(fd for fd in (own, launcher_fd) if fd is not None)


def _clock_and_launch(spawned, reported):
    """(clock start, launchMs): the case clock starts at `child-pid`; without one it starts at the spawn and the launch time is unknown."""
    return (spawned, None) if reported is None else (reported, reported - spawned)


def _begun(proc, launcher_fd, pipe_fd, spawned, case_id, lifecycle, stderr_path):
    reader, reported = _clock(proc, launcher_fd, pipe_fd, case_id, stderr_path)
    program = lifecycle.program_of(proc)
    clock, launch_ms = _clock_and_launch(spawned, reported)
    return Launched(proc, _pidfds(program, proc, launcher_fd), clock, launch_ms, reader, pipe_fd, program)


def _release(proc, launcher_fd, pipe_fd):
    """Close everything the runner holds for a launcher that will not become a case: its descriptors and the ends of its pipes."""
    ex.close_fd(launcher_fd)
    ex.close_fd(pipe_fd)
    for stream in (proc.stdin, proc.stdout):
        if stream is not None:
            stream.close()


def _abandon(proc, lifecycle):
    """Stop a launcher whose case will not start because the runner is being stopped: its group first (before the sandbox exists nothing is
    elsewhere), then, once the cgroup exists, the cgroup (a program behind `--new-session` is not in that group)."""
    ex.signal_group(proc.pid, signal.SIGKILL)
    if lifecycle.present():
        lifecycle.kill(proc)
    else:
        proc.wait()


def _spawn_with_status(config, argv, cwd, stderr_path, streams):
    """(Popen, read end of the status pipe or None): the pipe is closed again when the launcher cannot be started."""
    pipe_fd = None if config.status_fd is None else rs_status.open_pipe(config.status_fd)
    try:
        return _spawn(argv, cwd, stderr_path, config.status_fd, streams), pipe_fd
    except BaseException:
        ex.close_fd(pipe_fd)
        raise


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
    Raises LaunchFailed (nothing of it is left running) or OSError when the launcher cannot be started at all."""
    argv = ex.launch_argv(config.prefix, case_id, program)
    spawned = ex.now_ms()
    proc, pipe_fd = _spawn_with_status(config, argv, cwd, stderr_path, streams)
    return _begin_or_abandon(proc, ex.pidfd_of(proc), pipe_fd, spawned, case_id, lifecycle, stderr_path)


def time_empty(config, lifecycle_for, stderr_path):
    """An empty program (`bash -c :`) timed through the same prefix and the same clock: the control F1 asks for. Its `{case}` is
    EMPTY_CASE, so a layer whose prefix binds a per-case view or scope must provide one by that name. Milliseconds, or None when it did
    not end within LAUNCH_CEILING_MS."""
    lifecycle = lifecycle_for(config, ex.EMPTY_CASE)
    launched = start(config, ex.EMPTY_CASE, lifecycle, stderr_path, config.workspace, (subprocess.DEVNULL, subprocess.DEVNULL), EMPTY_PROGRAM)
    try:
        done = ex.wait_exit(launched.proc, launched.pidfd, launched.clock_ms + ex.LAUNCH_CEILING_MS)
        wall = ex.now_ms() - launched.clock_ms
        lifecycle.settle(launched, "exited" if done else "timeout")
        return wall if done else None
    except BaseException:
        lifecycle.kill(launched.proc)
        raise
    finally:
        launched.close()
