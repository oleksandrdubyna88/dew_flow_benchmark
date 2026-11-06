"""The network side of a daemon or web case (rs_session, rs_conn) with fake sockets and no program: a program that dies on a request is a reject of that case, never a failure of
the harness; what only the host can get wrong is a RunnerFault; a start-together group never loses bytes silently. Nothing here starts a process."""
import errno
import select
import types
import unittest
from dataclasses import replace

import runner_path  # noqa: F401
import rs_conn as rc
import rs_exec as ex
import rs_session as ss

SCRIPT = b"scenario 2\ndefault within 1s\nstart ready port within 3s\nconnect c tcp\nsignal TERM\nwait-exit within 1s\n"
TRANSCRIPT = b"transcript 2\nready\nexit 0\n"


def session(default_within_ms=None):
    s = ss.Session(ss.Case("c1", SCRIPT, TRANSCRIPT, "c1.in", "c1.out"), config=None)
    if default_within_ms is not None:
        s.scenario = replace(s.scenario, default_within_ms=default_within_ms)

    def wait_until(cond, deadline_ns, **kwargs):
        while True:
            if cond():
                return True
            if ex.now_ns() >= deadline_ns:
                return False
    s.wait_until = wait_until
    s.watch = s.unwatch = lambda conn: None
    s.pump = types.SimpleNamespace(send_input=lambda data: None, input_drained=lambda: True)
    return s


class FakeSock:
    """A socket whose sends follow a script: an int is the number of bytes taken, an exception is raised, and the last entry repeats."""

    def __init__(self, *script):
        self.script, self.sent, self.closed = list(script), bytearray(), False

    def send(self, data):
        step = self.script[0] if len(self.script) == 1 else self.script.pop(0)
        if isinstance(step, BaseException):
            raise step
        taken = len(data) if step is None else min(step, len(data))
        self.sent += bytes(data[:taken])
        return taken

    def fileno(self):
        return -1

    def close(self):
        self.closed = True


def http_conn(sock=None):
    conn = rc.Conn("c", "http", sock or FakeSock(None), None, False)
    conn.request, conn.method, conn.eof = b"GET / HTTP/1.1\r\n\r\n", b"GET", True
    return conn


def opener(*outcomes):
    calls = iter(outcomes)

    def open_socket(rcvbuf):
        outcome = next(calls)
        if isinstance(outcome, BaseException):
            raise outcome
        return outcome
    open_socket.calls = calls
    return open_socket


STEP = types.SimpleNamespace(args={})


class ReconnectTests(unittest.TestCase):
    def test_A_program_that_died_on_a_request_leaves_the_connection_at_its_end_and_the_case_records_eof(self):
        s = session()
        conn = http_conn()
        s.open_socket = opener(ConnectionRefusedError())
        with self.assertRaises(ss.Abort):
            s.recv_http(STEP, conn, ex.now_ns() + 10 ** 9, [])
        self.assertEqual([e.kind for e in s.entries], ["eof"], "the second close is the program's answer")
        self.assertTrue(conn.retried and conn.eof)

    def test_A_reconnect_that_finds_a_listener_writes_the_request_again_once(self):
        s = session()
        old, new = FakeSock(None), FakeSock(None)
        conn = http_conn(old)
        s.open_socket = opener(new)
        s.reconnect(conn, ex.now_ns() + 10 ** 9)
        self.assertEqual(bytes(new.sent), conn.request)
        self.assertTrue(old.closed and conn.retried and not conn.eof)

    def test_Nothing_is_sent_after_the_end_trigger_so_a_closed_connection_is_not_reconnected(self):
        s = session()
        s.triggered = True
        conn = http_conn()
        s.open_socket = opener()  # a call would raise StopIteration
        with self.assertRaises(ss.Abort):
            s.recv_http(STEP, conn, ex.now_ns() + 10 ** 9, [])
        self.assertEqual([e.kind for e in s.entries], ["eof"])
        self.assertFalse(conn.retried)

    def test_A_host_error_while_reconnecting_is_a_runner_fault_and_no_verdict(self):
        s = session()
        s.open_socket = opener(OSError(errno.EMFILE, "too many open files"))
        with self.assertRaises(ss.RunnerFault):
            s.reconnect(http_conn(), ex.now_ns() + 10 ** 9)
        self.assertEqual(s.entries, [])


class ConnectWordTests(unittest.TestCase):
    def kind_at_deadline(self, *errors):
        s = session()
        s.open_socket = opener(*errors)
        with self.assertRaises(ss.Abort):
            s.connect_by(ex.now_ns() - 1, None, "c")
        return s.entries[-1].kind

    def test_A_connect_not_answered_at_its_deadline_is_a_timeout_and_a_refused_one_is_refused(self):
        self.assertEqual(self.kind_at_deadline(TimeoutError()), "timeout")
        self.assertEqual(self.kind_at_deadline(ConnectionRefusedError()), "refused")

    def test_The_word_is_that_of_the_last_attempt(self):
        s = session()
        s.open_socket = opener(TimeoutError(), ConnectionRefusedError())
        s.try_open(None)
        with self.assertRaises(ss.Abort):
            s.connect_by(ex.now_ns() - 1, None, "c")
        self.assertEqual(s.entries[-1].kind, "refused")


class WriteTests(unittest.TestCase):
    def conn(self, *script):
        return rc.Conn("c", "tcp", FakeSock(*script), None, False)

    def test_A_peer_that_closed_is_write_failed_not_a_failure(self):
        for error in (BrokenPipeError(), ConnectionResetError()):
            s = session()
            conn = self.conn(error)
            s.write(conn, b"abc", ex.now_ns() + 10 ** 9)
            self.assertTrue(conn.write_failed, type(error).__name__)

    def test_Any_other_error_of_a_write_is_the_hosts_and_a_runner_fault(self):
        for code in (errno.ENOBUFS, errno.ENOMEM, errno.EBADF, errno.EACCES):
            s = session()
            with self.assertRaises(ss.RunnerFault, msg=errno.errorcode[code]):
                s.write(self.conn(OSError(code, "host")), b"abc", ex.now_ns() + 10 ** 9)

    def test_A_send_is_a_write_so_it_follows_the_same_rule(self):
        s = session()
        conn = self.conn(OSError(errno.ENOBUFS, "no buffers"))
        s.conns["c"] = conn
        s.text_of = lambda expr: b"payload"
        step = types.SimpleNamespace(conn="c", args={"data": None, "within": 1000}, line=4)
        s.deadline_of = lambda step: ex.now_ns() + 10 ** 9
        with self.assertRaises(ss.RunnerFault):
            s.s_send(step)


class TogetherTests(unittest.TestCase):
    def group(self, s, *jobs):
        s.prepared = lambda step: step.job
        return types.SimpleNamespace(steps=[types.SimpleNamespace(job=job, line=i + 5) for i, job in enumerate(jobs)])

    def test_Bytes_still_unsent_at_the_deadline_end_the_group_with_a_timeout_naming_the_connection(self):
        s = session(default_within_ms=100)
        a, b = rc.Conn("a", "tcp", FakeSock(None), None, False), rc.Conn("b", "tcp", FakeSock(BlockingIOError()), None, False)
        real = select.select
        select.select = lambda *args: ([], [], [])
        try:
            with self.assertRaises(ss.Abort):
                s.together(self.group(s, (a, b"aaaa"), (b, b"bbbb")))
        finally:
            select.select = real
        self.assertEqual([(e.kind, e.conn) for e in s.entries], [("timeout", "b")])
        self.assertEqual(bytes(a.sock.sent), b"aaaa", "the other writer completed")

    def test_A_group_whose_writes_all_complete_ends_without_an_entry(self):
        s = session(default_within_ms=1000)
        a, b = rc.Conn("a", "tcp", FakeSock(None), None, False), rc.Conn("b", "tcp", FakeSock(3), None, False)
        s.together(self.group(s, (a, b"aaaa"), (b, b"bbbbbb")))
        self.assertEqual((bytes(a.sock.sent), bytes(b.sock.sent), s.entries), (b"aaaa", b"bbbbbb", []))

    def test_A_peer_that_closed_in_the_group_is_write_failed_and_ends_no_group(self):
        s = session(default_within_ms=1000)
        a, b = rc.Conn("a", "tcp", FakeSock(None), None, False), rc.Conn("b", "tcp", FakeSock(BrokenPipeError()), None, False)
        s.together(self.group(s, (a, b"aaaa"), (b, b"bbbb")))
        self.assertTrue(b.write_failed)
        self.assertEqual(s.entries, [])

    def test_A_host_error_in_a_writer_is_a_runner_fault(self):
        s = session(default_within_ms=1000)
        a, b = rc.Conn("a", "tcp", FakeSock(None), None, False), rc.Conn("b", "tcp", FakeSock(OSError(errno.ENOBUFS, "x")), None, False)
        with self.assertRaises(ss.RunnerFault):
            s.together(self.group(s, (a, b"aaaa"), (b, b"bbbb")))

    def test_A_group_with_send_stdin_queues_the_input_and_never_gives_a_false_timeout(self):
        s = session(default_within_ms=1000)
        queued = []
        s.pump = types.SimpleNamespace(send_input=queued.append, input_drained=lambda: True)
        a = rc.Conn("a", "tcp", FakeSock(None), None, False)
        s.together(self.group(s, ("stdin", b"line\n"), (a, b"aaaa")))
        self.assertEqual((queued, bytes(a.sock.sent), s.entries), ([b"line\n"], b"aaaa", []))


class BufferCapTests(unittest.TestCase):
    def test_A_stream_keeps_bytes_up_to_the_cap_and_never_past_it(self):
        real = rc.CAP
        rc.CAP = 10
        try:
            stream = rc.Stream("c")
            stream.append(b"1234567")
            self.assertFalse(stream.overflow)
            stream.append(b"89abcdef")
            self.assertTrue(stream.overflow)
            self.assertEqual(bytes(stream.buf), b"123456789a", "exactly the cap is kept")
            stream.append(b"more")
            self.assertEqual(len(stream.buf), 10)
        finally:
            rc.CAP = real

    def test_A_single_read_that_alone_passes_the_cap_is_cut_at_it(self):
        real = rc.CAP
        rc.CAP = 4
        try:
            stream = rc.Stream("c")
            stream.append(b"abcdefgh")
            self.assertEqual((bytes(stream.buf), stream.overflow), (b"abcd", True))
        finally:
            rc.CAP = real

    def test_Bytes_that_exactly_fill_the_cap_are_not_an_overflow(self):
        real = rc.CAP
        rc.CAP = 4
        try:
            stream = rc.Stream("c")
            stream.append(b"abcd")
            self.assertEqual((bytes(stream.buf), stream.overflow), (b"abcd", False))
        finally:
            rc.CAP = real


if __name__ == "__main__":
    unittest.main()
