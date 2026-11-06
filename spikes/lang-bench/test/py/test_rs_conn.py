"""The reader of one HTTP response and a connection's reads (rs_conn) with their results fixed: each buffer below was read by the module before it was split into small functions, in all four combinations of
`eof` and `head`, and the results are exactly these. Pure functions and a fake socket; nothing starts."""
import errno
import unittest

import runner_path  # noqa: F401
import rs_conn as rc
from rs_base import RunnerFault

GOLDEN = [
    (b'HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nhello', [('ok', 200, ((b'Content-Length', b'5'),), b'hello', 43), ('ok', 200, ((b'Content-Length', b'5'),), b'', 38), ('ok', 200, ((b'Content-Length', b'5'),), b'hello', 43), ('ok', 200, ((b'Content-Length', b'5'),), b'', 38)]),
    (b'HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nhel', [('more',), ('ok', 200, ((b'Content-Length', b'5'),), b'', 38), ('more',), ('ok', 200, ((b'Content-Length', b'5'),), b'', 38)]),
    (b'HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nhelloEXTRA', [('ok', 200, ((b'Content-Length', b'5'),), b'hello', 43), ('ok', 200, ((b'Content-Length', b'5'),), b'', 38), ('ok', 200, ((b'Content-Length', b'5'),), b'hello', 43), ('ok', 200, ((b'Content-Length', b'5'),), b'', 38)]),
    (b'HTTP/1.1 100 Continue\r\n\r\nHTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok', [('ok', 200, ((b'Content-Length', b'2'),), b'ok', 65), ('ok', 200, ((b'Content-Length', b'2'),), b'', 63), ('ok', 200, ((b'Content-Length', b'2'),), b'ok', 65), ('ok', 200, ((b'Content-Length', b'2'),), b'', 63)]),
    (b'HTTP/1.1 204 No Content\r\nX-A: b\r\n\r\n', [('ok', 204, ((b'X-A', b'b'),), b'', 35), ('ok', 204, ((b'X-A', b'b'),), b'', 35), ('ok', 204, ((b'X-A', b'b'),), b'', 35), ('ok', 204, ((b'X-A', b'b'),), b'', 35)]),
    (b'HTTP/1.1 304 Not Modified\r\nContent-Length: 9\r\n\r\n', [('ok', 304, ((b'Content-Length', b'9'),), b'', 48), ('ok', 304, ((b'Content-Length', b'9'),), b'', 48), ('ok', 304, ((b'Content-Length', b'9'),), b'', 48), ('ok', 304, ((b'Content-Length', b'9'),), b'', 48)]),
    (b'HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n5\r\nhello\r\n0\r\n\r\n', [('ok', 200, ((b'Transfer-Encoding', b'chunked'),), b'hello', 62), ('ok', 200, ((b'Transfer-Encoding', b'chunked'),), b'', 47), ('ok', 200, ((b'Transfer-Encoding', b'chunked'),), b'hello', 62), ('ok', 200, ((b'Transfer-Encoding', b'chunked'),), b'', 47)]),
    (b'HTTP/1.1 200 OK\r\nTransfer-Encoding: gzip, chunked\r\n\r\n3\r\nabc\r\n4;x=1\r\ndefg\r\n0\r\nT: v\r\n\r\n', [('ok', 200, ((b'Transfer-Encoding', b'gzip, chunked'),), b'abcdefg', 85), ('ok', 200, ((b'Transfer-Encoding', b'gzip, chunked'),), b'', 53), ('ok', 200, ((b'Transfer-Encoding', b'gzip, chunked'),), b'abcdefg', 85), ('ok', 200, ((b'Transfer-Encoding', b'gzip, chunked'),), b'', 53)]),
    (b'HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n5\r\nhello\r\n', [('more',), ('ok', 200, ((b'Transfer-Encoding', b'chunked'),), b'', 47), ('more',), ('ok', 200, ((b'Transfer-Encoding', b'chunked'),), b'', 47)]),
    (b'HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n5\r\nhello', [('more',), ('ok', 200, ((b'Transfer-Encoding', b'chunked'),), b'', 47), ('more',), ('ok', 200, ((b'Transfer-Encoding', b'chunked'),), b'', 47)]),
    (b'HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n3\r\nabcde\r\n', [('bad', 'bad chunk end'), ('ok', 200, ((b'Transfer-Encoding', b'chunked'),), b'', 47), ('bad', 'bad chunk end'), ('ok', 200, ((b'Transfer-Encoding', b'chunked'),), b'', 47)]),
    (b'HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\nzz\r\n', [('bad', 'bad chunk size'), ('ok', 200, ((b'Transfer-Encoding', b'chunked'),), b'', 47), ('bad', 'bad chunk size'), ('ok', 200, ((b'Transfer-Encoding', b'chunked'),), b'', 47)]),
    (b'HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n0\r\n', [('more',), ('ok', 200, ((b'Transfer-Encoding', b'chunked'),), b'', 47), ('more',), ('ok', 200, ((b'Transfer-Encoding', b'chunked'),), b'', 47)]),
    (b'HTTP/1.1 200 OK\r\nContent-Length: x\r\n\r\n', [('bad', 'bad content-length'), ('ok', 200, ((b'Content-Length', b'x'),), b'', 38), ('bad', 'bad content-length'), ('ok', 200, ((b'Content-Length', b'x'),), b'', 38)]),
    (b'HTTP/1.1 200 OK\r\n\r\nuntil the end', [('more',), ('ok', 200, (), b'', 19), ('ok', 200, (), b'until the end', 32), ('ok', 200, (), b'', 19)]),
    (b'HTTP/1.1 200 OK\r\nBad header\r\n\r\n', [('bad', 'bad header line'), ('bad', 'bad header line'), ('bad', 'bad header line'), ('bad', 'bad header line')]),
    (b'HTTP/1.1 200 OK\r\nX-A: b', [('more',), ('more',), ('more',), ('more',)]),
    (b'HTTP/', [('more',), ('more',), ('more',), ('more',)]),
    (b'HTT', [('more',), ('more',), ('more',), ('more',)]),
    (b'', [('more',), ('more',), ('more',), ('more',)]),
    (b'GET / HTTP/1.1\r\n\r\n', [('bad', 'bad status line'), ('bad', 'bad status line'), ('bad', 'bad status line'), ('bad', 'bad status line')]),
    (b'HTX', [('bad', 'not an HTTP response'), ('bad', 'not an HTTP response'), ('bad', 'not an HTTP response'), ('bad', 'not an HTTP response')]),
    (b'HTTP/1.1 20 OK\r\n\r\n', [('bad', 'bad status line'), ('bad', 'bad status line'), ('bad', 'bad status line'), ('bad', 'bad status line')]),
    (b'HTTP/1.1 200 OK\r\nSet-Cookie: a=1\r\nSet-Cookie: b=2\r\nContent-Length: 0\r\n\r\n', [('ok', 200, ((b'Set-Cookie', b'a=1'), (b'Set-Cookie', b'b=2'), (b'Content-Length', b'0')), b'', 72), ('ok', 200, ((b'Set-Cookie', b'a=1'), (b'Set-Cookie', b'b=2'), (b'Content-Length', b'0')), b'', 72), ('ok', 200, ((b'Set-Cookie', b'a=1'), (b'Set-Cookie', b'b=2'), (b'Content-Length', b'0')), b'', 72), ('ok', 200, ((b'Set-Cookie', b'a=1'), (b'Set-Cookie', b'b=2'), (b'Content-Length', b'0')), b'', 72)]),
]


class ParseTests(unittest.TestCase):
    def test_Every_buffer_is_read_as_it_was_read_before_the_functions_were_split(self):
        for buf, expected in GOLDEN:
            got = [rc.parse_response(bytearray(buf), eof, head) for eof in (False, True) for head in (False, True)]
            self.assertEqual(got, expected, buf)

    def test_A_prefix_of_a_response_is_never_bad_and_reading_it_does_not_consume_the_buffer(self):
        buf = bytearray(b"HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nhello")
        for cut in range(len(buf)):
            self.assertEqual(rc.parse_response(buf[:cut], False)[0], rc.CONTINUE, cut)
        self.assertEqual(rc.parse_response(buf, False)[0], rc.DONE, 'the whole response is read')


class FakeSock:
    def __init__(self, outcome):
        self.outcome = outcome

    def recv(self, limit):
        if isinstance(self.outcome, BaseException):
            raise self.outcome
        return self.outcome


class PullTests(unittest.TestCase):
    def pulled(self, outcome, **state):
        conn = rc.Conn("c", "tcp", FakeSock(outcome), None, False)
        for k, v in state.items():
            setattr(conn, k, v)
        return conn, conn.pull()

    def test_Bytes_that_arrive_are_kept(self):
        conn, got = self.pulled(b"abc")
        self.assertEqual((got, bytes(conn.buf), conn.eof), (True, b"abc", False))

    def test_Nothing_yet_is_not_the_end(self):
        conn, got = self.pulled(BlockingIOError())
        self.assertEqual((got, conn.eof), (False, False))

    def test_An_end_of_stream_and_the_peers_own_ways_of_ending_are_the_end_of_the_connection(self):
        for outcome in (b"", ConnectionResetError(), ConnectionAbortedError(), BrokenPipeError(), TimeoutError()):
            conn, got = self.pulled(outcome)
            self.assertEqual((got, conn.eof), (False, True), repr(outcome))

    def test_Any_other_error_of_a_read_is_the_hosts_own_and_a_runner_fault_never_an_end_of_the_connection(self):
        for outcome in (OSError(errno.EBADF, "bad descriptor"), OSError(errno.ENOMEM, "no memory"), OSError(errno.EIO, "i/o error"), PermissionError(errno.EACCES, "denied")):
            conn = rc.Conn("c", "tcp", FakeSock(outcome), None, False)
            with self.assertRaises(RunnerFault, msg=repr(outcome)) as raised:
                conn.pull()
            self.assertIn("could not read", str(raised.exception))
            self.assertIn("c", str(raised.exception))
            self.assertFalse(conn.eof, repr(outcome))

    def test_A_connection_already_at_its_end_or_closed_is_not_read_again(self):
        for state in ({"eof": True}, {"closed": True}):
            conn, got = self.pulled(b"never read", **state)
            self.assertEqual((got, bytes(conn.buf)), (False, b""), state)


if __name__ == "__main__":
    unittest.main()
