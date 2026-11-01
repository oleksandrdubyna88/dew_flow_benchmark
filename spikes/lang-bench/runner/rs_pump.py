"""rs_pump: moves bytes between a case's program and the runner without ever blocking on either, and finds the case's end (B6, P3).

Input goes out, output comes in (compared as a stream by the olympiad path), and the end is awaited on descriptors, never polled: the
program's own pidfd, and the launch status pipe. The look into the program's cgroup (rs_track.py) is a wake-up of this same loop, so the
program's stdin and stdout flow from `child-pid` on whatever the look finds.

The end of a case is the instant (`time.monotonic_ns`) of the FIRST of: the program's pidfd turning readable, bwrap's `exit-code` record being
read from the status pipe. The launcher's own exit is never program time (its teardown is the box's, not the program's): in a box with
neither a pid nor a record the case still ends when the launcher goes, but its end instant is unknown (`exit_ns` None, wall unknown). On the
plain path there is no box and the launcher is the program.
"""
import os
import selectors

import rs_exec as ex
import rs_status

NS_PER_S = 1_000_000_000


class _Nothing:
    """The comparer of a program whose output nobody keeps (the empty program)."""
    mismatch = None

    def feed(self, chunk):
        return None


class Pump:
    def __init__(self, launched, data, comparer, stderr_path):
        self.launched, self.proc, self.tracker = launched, launched.proc, launched.tracker
        self.comparer, self.stderr_path = comparer or _Nothing(), stderr_path
        self.pending, self.exit_ns, self.sel = memoryview(data), None, selectors.DefaultSelector()
        self.watching, self.pipe_open = None, launched.pipe_fd is not None
        self._wire_streams()
        self._wire_ends()

    # ---- wiring --------------------------------------------------------------------------------------------------------------------

    def _wire_streams(self):
        if self.proc.stdout is not None:
            os.set_blocking(self.proc.stdout.fileno(), False)
            self.sel.register(self.proc.stdout, selectors.EVENT_READ, self.read_output)
        self._start_input()

    def _wire_ends(self):
        if self.launched.launcher_fd is not None:
            self.sel.register(self.launched.launcher_fd, selectors.EVENT_READ, self.launcher_gone)
        if self.pipe_open:
            self.sel.register(self.launched.pipe_fd, selectors.EVENT_READ, self.status_ready)
        self._watch_program()

    def _start_input(self):
        if self.proc.stdin is None:
            return
        if not len(self.pending):
            return self.proc.stdin.close()
        os.set_blocking(self.proc.stdin.fileno(), False)
        self.sel.register(self.proc.stdin, selectors.EVENT_WRITE, self.write_input)

    def _watch_program(self):
        """Register the program's own pidfd once the tracker holds one (a pid that is the launcher's own is awaited through the launcher's)."""
        fd = self.tracker.pidfd
        if fd is not None and self.watching is None:
            self.watching = fd
            self.sel.register(fd, selectors.EVENT_READ, self.program_exited)

    # ---- input and output ------------------------------------------------------------------------------------------------------------

    def write_input(self):
        try:
            self.pending = self.pending[os.write(self.proc.stdin.fileno(), self.pending[:ex.CHUNK]):]
        except BlockingIOError:
            return None
        except BrokenPipeError:
            self.pending = self.pending[len(self.pending):]
        return self._input_done() if not len(self.pending) else None

    def _input_done(self):
        self.sel.unregister(self.proc.stdin)
        self.proc.stdin.close()

    def read_output(self):
        try:
            chunk = os.read(self.proc.stdout.fileno(), ex.CHUNK)
        except BlockingIOError:
            return None
        if not chunk:
            self.sel.unregister(self.proc.stdout)
            return None
        self.comparer.feed(chunk)
        return "mismatch" if self.comparer.mismatch is not None else None

    def drain(self):
        """What the program wrote before it exited is in the pipe already: read it, but never wait for an end a stray child may hold open."""
        while self.comparer.mismatch is None and self._more():
            pass
        return "mismatch" if self.comparer.mismatch is not None else "exited"

    def _more(self):
        if self.proc.stdout is None:
            return False
        try:
            chunk = os.read(self.proc.stdout.fileno(), ex.CHUNK)
        except (BlockingIOError, ValueError, OSError):
            return False
        self.comparer.feed(chunk)
        return bool(chunk)

    # ---- the end ----------------------------------------------------------------------------------------------------------------------

    def _ended_now(self):
        if self.exit_ns is None:
            self.exit_ns = ex.now_ns()

    def program_exited(self):
        self._ended_now()
        return self.drain()

    def _pull(self):
        """Read the status pipe; at its end (nobody holds the write end) stop watching it."""
        if self.pipe_open and not rs_status.pull(self.launched.pipe_fd, self.launched.reader):
            self.pipe_open = False
            self.sel.unregister(self.launched.pipe_fd)

    def status_ready(self):
        self._pull()
        if self.launched.reader.exit_code is not None:
            self._ended_now()
            return self.drain()
        return None

    def launcher_gone(self):
        """The launcher exited. On the plain path that is the program's exit; in a box it is only the box's teardown."""
        self._pull()
        if not self.tracker.box or self.launched.reader.exit_code is not None:
            self._ended_now()
        return self.drain()

    def flooded(self):
        try:
            return os.path.getsize(self.stderr_path) > ex.STDERR_KILL_BYTES
        except OSError:
            return False

    # ---- the loop ---------------------------------------------------------------------------------------------------------------------

    def _look(self):
        if self.tracker.tracking():
            self.tracker.look()
            self._watch_program()

    def step(self, timeout_s):
        for key, _ in self.sel.select(timeout_s):
            reason = key.data()
            if reason:
                return reason
        self._look()
        return "flood" if self.flooded() else self._polled()

    def _polled(self):
        if self.launched.launcher_fd is None and self.proc.poll() is not None:
            return self.launcher_gone()
        return None

    def _tick(self):
        if self.tracker.seeking():
            return ex.SEEK_TICK_S
        return ex.TRACK_TICK_S if self.tracker.tracking() else ex.TICK_S

    def run(self, deadline_ns):
        """The reason the case ended: 'exited', 'mismatch', 'flood' or 'timeout' at `deadline_ns` (a monotonic_ns instant)."""
        while True:
            remaining = deadline_ns - ex.now_ns()
            if remaining <= 0:
                return "timeout"
            reason = self.step(min(remaining / NS_PER_S, self._tick()))
            if reason:
                return reason

    def close(self):
        self.sel.close()
        for stream in (self.proc.stdin, self.proc.stdout):
            if stream and not stream.closed:
                stream.close()
