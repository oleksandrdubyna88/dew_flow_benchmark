"""rs_net: the connections of a daemon or web scenario (FORMAT v2 4.3 and 4.4) as a mixin of `rs_session.Session`: opening and retrying a connect,
writing (a plain write, a split `send`, an HTTP request, a start-together group), and reading (`recv` in its modes, one HTTP response, `expect quiet`,
`expect closed`, `close`, `shutdown`). It uses the session's `conns`, `entries`, `pump`, `scenario`, `wait_until`, `add` and `abort`.

What the program does is an observation (`add` records it, and an actual-only one ends the scenario); what only the host can get wrong is a
`RunnerFault`: no verdict."""
import re
import select
import selectors
import socket
import threading

import rs_conn as rc
import rs_exec as ex
from rs_base import Gate, NOT_LISTENING_YET, PEER_CLOSED, PROBE_MS, RunnerFault, ms, nth


class NetMixin:
    # ---- connections ------------------------------------------------------------------------------------------------------------

    def open_socket(self, rcvbuf):
        sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        if rcvbuf:
            sock.setsockopt(socket.SOL_SOCKET, socket.SO_RCVBUF, rcvbuf)
        sock.settimeout(2)
        sock.connect(("127.0.0.1", self.port))
        sock.setsockopt(socket.IPPROTO_TCP, socket.TCP_NODELAY, 1)
        sock.setblocking(False)
        return sock

    def watch(self, conn):
        if conn.lazy:
            return
        self.pump.sel.register(conn.sock, selectors.EVENT_READ, lambda c=conn: self.on_readable(c))

    def on_readable(self, conn):
        conn.pull()
        if conn.eof:
            self.unwatch(conn)
        return None

    def unwatch(self, conn):
        try:
            self.pump.sel.unregister(conn.sock)
        except (KeyError, ValueError):
            pass

    def try_open(self, rcvbuf):
        """A new connection to the program, or None when the program is not listening YET (refused, a full accept queue, a reset while it starts).
        Any other error of the socket (no descriptors, no ports, no memory, no route) is the host's own and is a RunnerFault: it is never `refused`."""
        try:
            return self.open_socket(rcvbuf)
        except NOT_LISTENING_YET as e:
            self.last_open_error = e
            return None
        except OSError as e:
            raise RunnerFault(f"{self.case.in_name}:{self.cur.line if self.cur else 0}: the host could not open a connection: {e!r}")

    def refusal_word(self):
        """What a connect that was not answered by its deadline is called: `timeout` when the last attempt got no answer at all, else `refused`."""
        return "timeout" if isinstance(self.last_open_error, TimeoutError) else "refused"

    def connect_by(self, deadline, rcvbuf, name):
        """Retry until the program accepts. At the deadline the program has refused (an observation of the program, 5.1), or, when the last
        attempt was not answered at all, the connect timed out: that is `timeout`, not `refused`."""
        while True:
            sock = self.try_open(rcvbuf)
            if sock is not None:
                return sock
            if ex.now_ns() >= deadline:
                self.abort(self.refusal_word(), name)
            self.wait_until(lambda: False, min(deadline, ex.now_ns() + ms(PROBE_MS)))

    def s_connect(self, step):
        a = step.args
        sock = self.connect_by(ex.now_ns() + ms(a.get("within", self.t_ms)), a.get("rcvbuf"), step.conn)
        conn = rc.Conn(step.conn, a["kind"], sock, a.get("rcvbuf"), bool(a.get("rcvbuf")) or step.conn in self.lazy_names)
        self.conns[step.conn] = conn
        self.watch(conn)

    def reconnect(self, conn, deadline):
        """The one reconnect of 4.3. When the program no longer listens, there is nothing to reconnect to: the connection stays at its end
        (the caller records `eof`, a second close), as the program's answer, never a failure of the harness."""
        conn.retried = True
        sock = self.try_open(conn.rcvbuf)
        if sock is None:
            return
        self.unwatch(conn)
        conn.sock.close()
        conn.sock = sock
        conn.eof, conn.write_failed = False, False
        self.watch(conn)
        self.write(conn, conn.request, deadline)

    # ---- writing ----------------------------------------------------------------------------------------------------------------

    def where(self):
        return f"{self.case.in_name}:{self.cur.line if self.cur else 0}"

    def write(self, conn, data, deadline):
        if conn == "stdin":
            return self.write_stdin(data, deadline)
        view, sent = memoryview(data), 0
        while sent < len(view) and not conn.write_failed:
            sent += self.send_some(conn, view[sent:sent + rc.CHUNK], deadline)

    def write_stdin(self, data, deadline):
        self.pump.send_input(data)
        if not self.wait_until(self.pump.input_drained, deadline):
            self.abort("timeout", "stdin", why="stdin was not written in time")

    def send_some(self, conn, piece, deadline):
        """The bytes one send took: 0 when the socket is full (a short wait follows) or the peer closed. A write that cannot finish by the
        deadline is a `timeout`; an error of the host is a RunnerFault."""
        try:
            return conn.sock.send(piece)
        except BlockingIOError:
            self.wait_writable(conn, deadline)
        except PEER_CLOSED:
            conn.write_failed = True
        except OSError as e:
            raise RunnerFault(f"{self.where()}: the host could not write to {conn.name}: {e!r}")
        return 0

    def wait_writable(self, conn, deadline):
        if not self.wait_until(lambda: False, min(deadline, ex.now_ns() + ms(5))) and ex.now_ns() >= deadline:
            self.abort("timeout", conn.name, why="send timed out")

    def deadline_of(self, step):
        return ex.now_ns() + ms(step.args.get("within", self.scenario.default_within_ms))

    def target(self, name):
        if name == "stdin":
            return "stdin"
        if name not in self.conns:
            raise RunnerFault(f"{self.case.in_name}:{self.cur.line}: no connection {name} is open")
        return self.conns[name]

    def s_send(self, step):
        data, deadline = self.text_of(step.args["data"]), self.deadline_of(step)
        target = self.target(step.conn)
        if target != "stdin":
            target.request = None  # raw bytes: the one reconnect of 4.3 is for a request step only
        offsets = step.args.get("split")
        if not offsets:
            return self.write(target, data, deadline)
        self.send_split(target, data, deadline, offsets, step.args["pause"])

    def send_split(self, target, data, deadline, offsets, pause):
        cuts = [0, *offsets, len(data)]
        for i in range(len(cuts) - 1):
            self.write(target, data[cuts[i]:cuts[i + 1]], deadline)
            if i < len(cuts) - 2:
                self.idle(pause)

    def request_body(self, a, defaults):
        """The request's body (None when it has none); a body adds the `Content-Length` default."""
        if a["body"] is None:
            return None
        body = self.json_body(a["body"][1]) if a["body"][0] == "json" else self.text_of(a["body"][1])
        defaults.append([b"Content-Length", str(len(body)).encode()])
        return body

    def merge_headers(self, headers, defaults):
        """The `header` lines: one whose name equals a default's (ASCII case folded) replaces it in place; the others are returned, in order."""
        extra = []
        for name_expr, value_expr in headers:
            name, value = self.text_of(name_expr), self.text_of(value_expr)
            slot = default_named(defaults, name)
            if slot is not None:
                slot[0:2] = [name, value]
            else:
                extra.append([name, value])
        return extra

    def request_bytes(self, step):
        a = step.args
        target = self.text_of(a["target"])
        defaults = [[b"Host", f"127.0.0.1:{self.port}".encode()]]
        body = self.request_body(a, defaults)
        extra = self.merge_headers(a["headers"], defaults)
        lines = [a["method"].encode() + b" " + target + b" HTTP/1.1"] + [n + b": " + v for n, v in defaults + extra]
        return b"\r\n".join(lines) + b"\r\n\r\n" + (body or b"")

    def json_body(self, text):
        raw = text.encode("utf-8")
        return re.sub(rb"\$\{([a-z][a-z0-9_]*)\}", lambda m: self.variables[m.group(1).decode()].raw, raw)

    def prepared(self, step):
        """(connection, bytes) of a send or request step, with an HTTP request remembered for the one reconnect."""
        conn = self.target(step.conn)
        if step.op == "send":
            if conn != "stdin":
                conn.request = None  # raw bytes: the reconnect of 4.3 is for a request step only
            return conn, self.text_of(step.args["data"])
        data = self.request_bytes(step)
        conn.request, conn.method, conn.retried = data, step.args["method"].encode(), False
        return conn, data

    def s_request(self, step):
        conn, data = self.prepared(step)
        self.write(conn, data, self.deadline_of(step))

    # ---- a start-together group (4.4) -------------------------------------------------------------------------------------------

    def group_send(self, conn, piece):
        """One send of a writer of the group: the bytes taken; 0 when the socket is full (a short wait on it) or the peer closed."""
        try:
            return conn.sock.send(piece)
        except BlockingIOError:
            select.select([], [conn.sock], [], 0.01)
        except PEER_CLOSED:
            conn.write_failed = True
        return 0

    def still_writing(self, conn, sent, total, gate):
        return sent < total and not conn.write_failed and ex.now_ns() < gate.deadline

    def send_all(self, conn, data, gate):
        """Write until every byte is out, the peer closed, or the deadline passes. True when bytes are left unsent and the peer did not close."""
        view, sent = memoryview(data), 0
        while self.still_writing(conn, sent, len(view), gate):
            sent += self.group_send(conn, view[sent:sent + rc.CHUNK])
        return sent < len(view) and not conn.write_failed

    def together_worker(self, conn, data, gate):
        """One writer of a start-together group: released by the barrier, it writes until every byte is out or the deadline passes. A peer that closed
        (EPIPE, ECONNRESET) is `write_failed`; any other error is the host's and ends the group as a RunnerFault. Bytes still unsent at the deadline are
        listed in `gate.unsent`, so the group ends with the same `timeout` as a `send` that did not finish."""
        try:
            gate.barrier.wait()
            if self.send_all(conn, data, gate):
                gate.unsent.append(conn.name)
        except BaseException as e:  # noqa: BLE001 - re-raised in the driving thread
            gate.errors.append(e)
        finally:
            gate.done.append(conn.name)

    def start_writers(self, sockets, gate):
        threads = [threading.Thread(target=self.together_worker, args=(conn, data, gate), daemon=True) for conn, data in sockets]
        for t in threads:
            t.start()
        return threads

    def release_and_wait(self, gate, jobs, writers):
        gate.barrier.wait()
        for conn, data in jobs:
            if conn == "stdin":  # stdin is a connection of the group too (4.4): queued at the instant of the release, written by the pump
                self.pump.send_input(data)
        if not self.wait_until(lambda: len(gate.done) == writers and self.pump.input_drained(), gate.deadline + ms(1000)):
            self.abort("timeout", job_name(jobs[0][0]), why="a write of the group did not complete")

    def group_result(self, gate):
        if gate.errors:
            raise RunnerFault(f"a writer of the together group failed: {gate.errors[0]!r}")
        if gate.unsent:
            self.abort("timeout", gate.unsent[0], why="a write of the group did not complete")

    def together(self, node):
        jobs = [self.prepared(step) for step in self.mark(node.steps)]
        sockets = [(conn, data) for conn, data in jobs if conn != "stdin"]
        gate = Gate(threading.Barrier(len(sockets) + 1), ex.now_ns() + ms(self.scenario.default_within_ms))
        threads = self.start_writers(sockets, gate)
        try:
            self.release_and_wait(gate, jobs, len(sockets))
        finally:
            join_all(threads)
        self.group_result(gate)

    def mark(self, steps):
        """The steps of a group, the current step following each one (for the position a failure is reported at)."""
        for step in steps:
            self.cur = step
            yield step

    # ---- reading ----------------------------------------------------------------------------------------------------------------

    def stream_of(self, name):
        return self.stdout if name == "stdout" else self.target(name)

    def finder(self, mode):
        kind = mode[0]
        if kind == "bytes":
            n = mode[1]
            return lambda buf: n if len(buf) >= n else None
        if kind == "lines":
            return lambda buf: nth(buf, b"\n", mode[1])
        if kind == "until":
            delim, k = self.text_of(mode[1]), mode[2]
            return lambda buf: nth(buf, delim, k)
        return lambda buf: None

    def reply_end(self, end, stream, mode):
        """Where the reply ends: `end` when the mode found it, the end of the buffer for `eof` mode at the end of the stream, else None."""
        return len(stream.buf) if end is None and stream.eof and mode[0] == "eof" else end

    def read_reply(self, stream, step, deadline, lazy):
        """Wait for the reply of a `recv` on a tcp connection or stdout and take its bytes; a reply that is not complete is the observation
        `eof` or `timeout` with the bytes so far."""
        a, mode, state = step.args, step.args["mode"], {}
        find = self.finder(mode)

        def cond():
            state["end"] = find(stream.buf)
            return state["end"] is not None or stream.eof
        self.wait_until(cond, deadline, lazy=lazy, chunk=a.get("chunk"), pause_ms=a.get("pause", 0))
        end = self.reply_end(state.get("end"), stream, mode)
        if end is None:
            self.add("eof" if stream.eof else "timeout", stream.name, data=stream.take_all())
        return stream.take(end)

    def record_reply(self, stream, mode, data):
        if mode[0] != "lines":
            self.add("recv", stream.name, data=data)
            return
        for line in re.findall(rb"[^\n]*\n", data):
            self.add("recv", stream.name, data=line)

    def s_recv(self, step):
        stream = self.stream_of(step.conn)
        deadline = self.deadline_of(step)
        lazy = [stream] if stream.lazy else []
        if isinstance(stream, rc.Conn) and stream.kind == "http":
            return self.recv_http(step, stream, deadline, lazy)
        data = self.read_reply(stream, step, deadline, lazy)
        self.record_reply(stream, step.args["mode"], data)
        self.capture(step, data, None)

    def closed_unanswered(self, conn):
        """A request met a connection the program had closed before a single byte of the response arrived."""
        return conn.eof and not conn.buf and conn.request is not None

    def may_reconnect(self, conn):
        # at most once per request (4.3), and nothing more is sent after the end trigger (4.4), so no reconnect either
        return self.closed_unanswered(conn) and not conn.retried and not self.triggered

    def http_ready(self, conn, head, state, deadline):
        """The condition of the wait for one HTTP response: True when the response is whole or bad, False while it goes on (after the one
        reconnect), the stream's end otherwise."""
        got = rc.parse_response(bytes(conn.buf), conn.eof, head)
        state["got"] = got
        if got[0] != rc.CONTINUE:
            return True
        if self.may_reconnect(conn):
            self.reconnect(conn, deadline)
            return False
        return conn.eof

    def recv_http(self, step, conn, deadline, lazy):
        state, head = {}, conn.method == b"HEAD"
        ready = lambda: self.http_ready(conn, head, state, deadline)  # noqa: E731
        self.wait_until(ready, deadline, lazy=lazy, chunk=step.args.get("chunk"), pause_ms=step.args.get("pause", 0))
        got = state["got"]
        if got[0] == rc.BAD:
            self.add("bad-reply", conn.name, got[1], data=conn.take_all())
        if got[0] == rc.CONTINUE:
            self.add("eof" if conn.eof else "timeout", conn.name, data=conn.take_all())
        _, status, headers, body, used = got
        del conn.buf[:used]
        self.add("reply", conn.name, status=status, headers=headers, body=body)
        self.capture(step, body, body)

    # ---- the other steps --------------------------------------------------------------------------------------------------------

    def s_quiet(self, step):
        stream = self.stream_of(step.conn)
        end = ex.now_ns() + ms(step.args["for"])
        lazy = [stream] if stream.lazy else []
        if self.wait_until(lambda: bool(stream.buf) or stream.eof, end, lazy=lazy):
            kind = "noise" if stream.buf else "eof"
            self.add(kind, stream.name, data=stream.take_all())
        else:
            self.add("quiet", stream.name)

    def s_closed(self, step):
        stream = self.stream_of(step.conn)
        lazy = [stream] if stream.lazy else []
        if not self.wait_until(lambda: bool(stream.buf) or stream.eof, self.deadline_of(step), lazy=lazy):
            self.add("open", stream.name)
        elif stream.buf:
            self.add("noise", stream.name, data=stream.take_all())
        else:
            self.add("closed", stream.name)

    def s_close(self, step):
        if step.conn == "stdin":
            if step is self.trigger_node:
                self.fire_trigger()
            return self.pump.close_input()
        conn = self.target(step.conn)
        self.unwatch(conn)
        conn.closed = conn.eof = True
        conn.sock.close()

    def s_shutdown(self, step):
        try:
            self.target(step.conn).sock.shutdown(socket.SHUT_WR)
        except OSError:
            pass


def default_named(defaults, name):
    """The default header whose name equals `name`, ASCII letters compared without regard to case, or None."""
    return next((d for d in defaults if d[0].lower() == name.lower()), None)


def job_name(conn):
    """The name of a group job's connection (`stdin` is a plain string)."""
    return conn if conn == "stdin" else conn.name


def join_all(threads):
    for t in threads:
        t.join(1)
