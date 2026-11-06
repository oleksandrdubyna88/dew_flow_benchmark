"""rs_conn: the byte streams a daemon or web scenario reads (a TCP connection, an HTTP/1.1 client connection, the program's standard output)
and the reader of one HTTP response (FORMAT v2 4.3).

A `Stream` holds the bytes that arrived and no step has read yet (`buf`), and whether the other side ended (`eof`). A `Conn` is a stream
with a socket. A `lazy` connection is read only by the step that reads it (a `rcvbuf` or a `chunk` slow reader: reading it eagerly would
defeat the backpressure the scenario asks for). `parse_response` reads one whole response from a buffer without consuming it.
"""
import re

from rs_base import PEER_ENDED, RunnerFault

CHUNK = 65536
CAP = 256 * 1024 * 1024  # 4.6: unread bytes of one connection
HEAD_LIMIT = 1024 * 1024
BODYLESS = (204, 304)
STATUS_LINE = re.compile(rb"HTTP/1\.[01] ([0-9]{3})(?: [^\r\n]*)?\r\n")
HEADER_LINE = re.compile(rb"([^\x00-\x20:\x7f]+):[ \t]*([^\r\n]*?)[ \t]*\r\n")
CONTINUE = "more"
BAD = "bad"
DONE = "ok"


class Stream:
    def __init__(self, name):
        self.name, self.buf, self.eof, self.overflow, self.lazy = name, bytearray(), False, False, False

    def take(self, n):
        data = bytes(self.buf[:n])
        del self.buf[:n]
        return data

    def take_all(self):
        return self.take(len(self.buf))

    def append(self, data):
        """Keep the bytes up to the bound of 4.6 (256 MiB unread); the bytes that would pass it are not kept and the stream is `overflow`
        (a connection or a read stdout then ends the scenario with the entry `overflow <conn>`)."""
        room = CAP - len(self.buf)
        if len(data) > room:
            self.overflow = True
            data = data[:max(room, 0)]
        self.buf += data


class Conn(Stream):
    """One client connection of the scenario. `request` is the last HTTP request written (for the one reconnect of 4.3)."""

    def __init__(self, name, kind, sock, rcvbuf, lazy):
        super().__init__(name)
        self.kind, self.sock, self.rcvbuf, self.lazy = kind, sock, rcvbuf, lazy
        self.request, self.method, self.retried, self.closed, self.write_failed = None, b"", False, False, False

    def _finished(self):
        return self.eof or self.closed

    def _recv(self, limit):
        """The bytes read; `b""` when the connection ended (closed, or ended by the peer: reset, aborted, broken, timed out);
        None when nothing is there yet. Any other error of the read is the host's own and a RunnerFault: never the end of the connection."""
        try:
            return self.sock.recv(limit)
        except BlockingIOError:
            return None
        except PEER_ENDED:
            return b""
        except OSError as e:
            raise RunnerFault(f"the host could not read {self.name}: {e!r}")

    def pull(self, limit=CHUNK):
        """One non-blocking read. True when bytes arrived. A closed or reset connection is `eof`."""
        if self._finished():
            return False
        data = self._recv(limit)
        if data is None:
            return False
        if not data:
            self.eof = True
            return False
        self.append(data)
        return True


# ---- one HTTP response -------------------------------------------------------------------------------------------------------------

def _prefix_cannot_be_http(buf, start):
    """True when the first bytes there cannot begin `HTTP/`."""
    first = bytes(buf[start:start + 5])
    return bool(first) and not (b"HTTP/".startswith(first) or first == b"HTTP/")


def _unfinished_head(buf, start):
    """What a head that has no empty line yet is: BAD when it cannot be a response or is over the limit, else CONTINUE."""
    if _prefix_cannot_be_http(buf, start):
        return BAD, "not an HTTP response"
    return (BAD, "the response head is over 1 MiB") if len(buf) - start > HEAD_LIMIT else (CONTINUE,)


def _header_lines(block, at):
    """([(name, value)], None) for the header lines of `block` from `at`, or (None, the problem)."""
    headers = []
    while at < len(block):
        h = HEADER_LINE.match(block, at)
        if h is None:
            return None, "bad header line"
        headers.append((h.group(1), h.group(2)))
        at = h.end()
    return headers, None


def _head(buf, start):
    """(status, headers, end of head) of the response head at `start`, CONTINUE when it is not whole yet, or (BAD, message)."""
    end = buf.find(b"\r\n\r\n", start)
    if end < 0:
        return _unfinished_head(buf, start)
    block = bytes(buf[start:end + 2])
    line = STATUS_LINE.match(block)
    if line is None:
        return BAD, "bad status line"
    headers, problem = _header_lines(block, line.end())
    if problem:
        return BAD, problem
    return DONE, int(line.group(1)), tuple(headers), end + 4


def _values(headers, name):
    return [v for n, v in headers if n.lower() == name]


def _chunk_size(buf, at):
    """(DONE, size, the offset after the size line), CONTINUE when the line is not whole yet, or (BAD, message)."""
    eol = buf.find(b"\r\n", at)
    if eol < 0:
        return (CONTINUE,)
    size_text = bytes(buf[at:eol]).split(b";")[0].strip()
    if not re.fullmatch(rb"[0-9a-fA-F]+", size_text):
        return BAD, "bad chunk size"
    return DONE, int(size_text, 16), eol + 2


def _trailers(buf, at):
    """(DONE, the offset after the empty line that ends the trailers) or CONTINUE."""
    while True:
        eol = buf.find(b"\r\n", at)
        if eol < 0:
            return (CONTINUE,)
        if eol == at:
            return DONE, eol + 2
        at = eol + 2


def _chunk_data(buf, at, size):
    """(DONE, the chunk's bytes, the offset after its line feed), CONTINUE when it is not whole yet, or (BAD, message)."""
    if len(buf) < at + size + 2:
        return (CONTINUE,)
    if buf[at + size:at + size + 2] != b"\r\n":
        return BAD, "bad chunk end"
    return DONE, buf[at:at + size], at + size + 2


def _next_chunk(buf, at):
    """(DONE, bytes, end) for the next chunk; (DONE, None, end) for the last one with its trailers; CONTINUE or (BAD, message)."""
    sized = _chunk_size(buf, at)
    if sized[0] != DONE:
        return sized
    size, at = sized[1], sized[2]
    if size == 0:
        end = _trailers(buf, at)
        return (DONE, None, end[1]) if end[0] == DONE else end
    return _chunk_data(buf, at, size)


def _chunked(buf, at):
    """(body, end) of a chunked body starting at `at`, CONTINUE when incomplete, or (BAD, message)."""
    body = bytearray()
    while True:
        chunk = _next_chunk(buf, at)
        if chunk[0] != DONE:
            return chunk
        if chunk[1] is None:
            return DONE, bytes(body), chunk[2]
        body += chunk[1]
        at = chunk[2]


def _is_chunked(headers):
    encodings = [v.lower() for v in _values(headers, b"transfer-encoding")]
    return bool(encodings) and encodings[-1].split(b",")[-1].strip() == b"chunked"


def _length_body(buf, at, lengths):
    if not re.fullmatch(rb"[0-9]+", lengths[0]):
        return BAD, "bad content-length"
    n = int(lengths[0])
    return (DONE, bytes(buf[at:at + n]), at + n) if len(buf) >= at + n else (CONTINUE,)


def _to_the_end(buf, at, eof):
    return (DONE, bytes(buf[at:]), len(buf)) if eof else (CONTINUE,)


def _bodyless(status, head):
    return head or status in BODYLESS


def _body(buf, at, status, headers, eof, head):
    """(DONE, body, end), CONTINUE or (BAD, message) for the body that starts at `at`."""
    if _bodyless(status, head):
        return DONE, b"", at
    if _is_chunked(headers):
        return _chunked(buf, at)
    lengths = _values(headers, b"content-length")
    return _length_body(buf, at, lengths) if lengths else _to_the_end(buf, at, eof)


def _finish(buf, h, eof, head):
    """The whole response whose head is `h`."""
    b = _body(buf, h[3], h[1], h[2], eof, head)
    return (DONE, h[1], h[2], b[1], b[2]) if b[0] == DONE else b


def parse_response(buf, eof, head=False):
    """Read one response from the start of `buf` without consuming it: (DONE, status, headers, body, used) | (CONTINUE,) | (BAD, message).
    An interim 1xx response is skipped (it counts in `used`). `headers` holds every occurrence as (name, value) bytes in arrival order."""
    start = 0
    while True:
        h = _head(buf, start)
        if h[0] != DONE:
            return h
        if not 100 <= h[1] < 200:
            return _finish(buf, h, eof, head)
        start = h[3]
