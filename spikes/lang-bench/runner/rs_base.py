"""rs_base: what the pieces of a daemon or web case (rs_session, rs_net, rs_capture) share: the two ways a case can stop (a runner fault, which is
no verdict, and an abort, which is the scenario ending at an observation), the case itself, the sink for standard output, the writers' gate of a
start-together group, and the few constants and helpers."""
import socket
from datetime import timezone

import rs_exec as ex

UTC = timezone.utc
NS = ex.NS_PER_MS
PROBE_MS = 50
TEST_BOUND_MS = ex.TEST_CEILING_MS
DEFAULT_GRACE_MS = 5000
TWO_S = 2000
ACTUAL_ONLY = frozenset({"not-ready", "refused", "timeout", "eof", "noise", "open", "bad-reply", "overflow", "early-exit", "exit-timeout",
                         "exit-signal", "capture-failed"})
# What a connect says of a program that is not (yet) accepting: the only errors that are the program's answer.
NOT_LISTENING_YET = (ConnectionRefusedError, ConnectionResetError, ConnectionAbortedError, TimeoutError)
# What a write says when the program closed the connection (EPIPE, ECONNRESET): the peer's closing, as FORMAT models it.
# Any other error of a write is the host's own.
PEER_CLOSED = (BrokenPipeError, ConnectionResetError)
# What a read says when the program ended the connection (a reset, an abort, a broken pipe, a timeout of the peer's end):
# the end of the stream, as FORMAT models it. Any other error of a read is the host's own.
PEER_ENDED = (ConnectionResetError, ConnectionAbortedError, BrokenPipeError, TimeoutError)


class RunnerFault(Exception):
    """The harness could not do what it must (not the program's fault): exit 5, no verdict."""


class Abort(Exception):
    """The scenario ends here. `entry` is the last observation (or None), `why` says what ended it."""

    def __init__(self, entry=None, why="", reason=None):
        super().__init__(why)
        self.entry, self.why, self.reason = entry, why, reason


class Gate:
    """What the writers of a start-together group share: the barrier that releases them, the deadline, and the lists they report into."""

    def __init__(self, barrier, deadline):
        self.barrier, self.deadline = barrier, deadline
        self.errors, self.done, self.unsent = [], [], []


class Case:
    def __init__(self, id, script, expected, in_name, out_name, r_ids=(), comparison=None):
        self.id, self.script, self.expected, self.in_name, self.out_name = id, script, expected, in_name, out_name
        self.r_ids, self.comparison = r_ids, comparison


class Collector:
    """The comparer slot of the pump for standard output: keeps every byte for the steps that read it."""
    mismatch = None

    def __init__(self, stream):
        self.stream = stream

    def feed(self, chunk):
        self.stream.append(chunk)


def ms(n):
    return n * NS


def free_port():
    with socket.socket() as probe:
        probe.bind(("127.0.0.1", 0))
        return probe.getsockname()[1]


def nth(buf, delim, k):
    """The end offset of the k-th non-overlapping occurrence of `delim` in `buf`, or None."""
    if buf.count(delim) < k:
        return None
    at = 0
    for _ in range(k):
        at = buf.find(delim, at) + len(delim)
    return at
