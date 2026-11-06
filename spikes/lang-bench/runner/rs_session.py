"""rs_session: one daemon or web case (FORMAT v2 sections 4 and 5): start `bash run.sh` with `PORT` and the scenario's environment,
drive the scenario's steps against it, record the actual transcript, and judge it against the expected one.

The program is started through the same launch prefix, case clock, pump and lifecycle as an olympiad program (rs_launch, rs_pump,
rs_scope); the environment is put in front of `bash run.sh` with `env`, so it reaches a program inside a box whose own environment the
layer fixed. One thread drives everything: the sockets of the scenario are registered in the pump's selector, so a wait for a reply also
reads the program's standard output and watches its end. `together` groups write from threads released by a barrier.
The connections are `rs_net.NetMixin`, the captures `rs_capture.CaptureMixin`, the shared pieces `rs_base`.

A harness failure is never a verdict: a runner fault (RunnerFault, or any exception that is not an expected observation of the program)
leaves the runner with exit 5 and no result for the case. What a program does wrong is an entry of the actual transcript.
"""
import os
import shutil
import signal
import socket
from datetime import datetime

import rs_abort
import rs_capture as rcap
import rs_conn as rc
import rs_end
import rs_evidence as rev
import rs_exec as ex
import rs_fields as rf
import rs_launch as rl
import rs_match
import rs_model as rm
import rs_net
import rs_pump
import rs_report as rp
import rs_scope
import rs_script
import rs_transcript as rt
import rs_view
from rs_base import ACTUAL_ONLY, DEFAULT_GRACE_MS, PROBE_MS, TEST_BOUND_MS, TWO_S, UTC, Abort, Case, Collector, RunnerFault, free_port, ms
from rs_base import Gate, NOT_LISTENING_YET, PEER_CLOSED  # noqa: F401 - names that callers and tests reach through rs_session
from rs_report import CaseResult


def _chunked_of(node):
    """The connections a node reads in chunks: a `recv` with `chunk`, or those inside a repeat."""
    if isinstance(node, rm.Repeat):
        return _chunked_conns(node.body, set())
    return {node.conn} if _is_chunk_recv(node) else set()


def _is_chunk_recv(node):
    return isinstance(node, rm.Step) and node.op == "recv" and "chunk" in node.args


def _chunked_conns(nodes, out):
    for n in nodes:
        out |= _chunked_of(n)
    return out


def _reads_stdout_step(node):
    return isinstance(node, rm.Step) and node.conn == "stdout" and node.op in ("recv", "quiet", "closed")


def _reads_stdout(nodes):
    return any((isinstance(n, rm.Repeat) and _reads_stdout(n.body)) or _reads_stdout_step(n) for n in nodes)


def _grace_of(top):
    """The grace period G: the `within` of the scenario's `wait-exit`, else the default."""
    waits = [n for n in top if _is_wait_exit(n)]
    return waits[0].args.get("within", DEFAULT_GRACE_MS) if waits else DEFAULT_GRACE_MS


def _is_wait_exit(node):
    return isinstance(node, rm.Step) and node.op == "wait_exit"


def _parsed(case):
    """The scenario and the expected transcript of a pair, or a RunnerFault (the plan refuses a pair that does not parse)."""
    scenario, problems = rs_script.parse_script(case.script, case.in_name)
    transcript, more = rt.parse_transcript(case.expected, case.out_name)
    if any((problems, more, scenario is None, transcript is None)):
        raise RunnerFault(f"{case.id}: the pair does not parse (the plan should have refused it)")
    return scenario, transcript


class Session(rcap.CaptureMixin, rs_net.NetMixin):
    def __init__(self, case, config):
        self.case, self.config = case, config
        self.scenario, self.transcript = _parsed(case)
        self.script_lines = case.script.decode("utf-8", "replace").split("\n")
        self.entries, self.variables, self.conns = [], {}, {}
        self.cur, self.triggered, self.killed, self.bound_hit, self.flooded = None, False, False, False, False
        self.aborted, self.ended_by, self.signal_sent, self.grace_deadline = None, "", None, None
        self.status, self.t_ms = None, self.scenario.start.within_ms
        self.stdout = rc.Stream("stdout")
        self.stdout_read = _reads_stdout(self.scenario.body)
        self.lazy_names = _chunked_conns(self.scenario.body, set())
        top = list(self.scenario.body)
        self.trigger_node = next((n for n in top if rs_end.is_trigger(n)), None)
        self.grace_ms = _grace_of(top)
        self.next_probe, self.port = 0, free_port()
        self.lifecycle, self.launched, self.pump, self.fixtures_dir = None, None, None, None
        self.last_open_error = None

    # ---- records ---------------------------------------------------------------------------------------------------------------

    def text_of(self, expr):
        parts, error = rf.resolve(expr, self.variables)
        if error:
            raise RunnerFault(f"{self.case.in_name}:{self.cur.line if self.cur else 0}: {error}")
        return b"".join(p.data for p in parts)

    def position(self):
        """The line of the step being run and its text (0 and an empty text outside a step)."""
        line = self.cur.line if self.cur is not None else 0
        return line, (self.script_lines[line - 1].strip() if line else "")

    def add(self, kind, conn="", why="", **more):
        """Record an observation. An actual-only entry (5.1) never matches an expected one, so the scenario ends at it: the verdict and its
        first mismatch are the same as if it had gone on, and a hung program costs one deadline, not one per remaining step."""
        line, text = self.position()
        entry = rt.Actual(kind, conn, at=datetime.now(UTC), step_line=line, step_text=text, **more)
        self.entries.append(entry)
        if kind in ACTUAL_ONLY:
            raise Abort(entry, why or f"{kind} at {self.case.in_name}:{line}")
        return entry

    abort = add

    # ---- the wait: the one loop every waiting step uses -------------------------------------------------------------------------

    def refresh(self):
        self.stdout.eof = self.pump.exit_ns is not None

    def boxed_status(self):
        """The status of a program in a box: the launcher is reaped (the kill goes through the cgroup)."""
        proc = self.launched.proc
        if not ex.wait_gone(proc, TWO_S):
            raise RunnerFault("the program exited but its launcher did not")
        return self.launched.status_code(proc.wait())

    def plain_status(self):
        """The status of a program on the plain path: the launcher is NOT reaped here (its pid must stay reserved until the group signal of the
        settle, or a child left behind could not be killed): waitid with WNOWAIT reads the status without reaping."""
        info = os.waitid(os.P_PID, self.launched.proc.pid, os.WEXITED | os.WNOWAIT)
        return info.si_status if info.si_code == os.CLD_EXITED else -info.si_status

    def exit_status(self):
        """The program's status once it has exited."""
        if self.status is None:
            self.status = self.boxed_status() if self.launched.tracker.box else self.plain_status()
        return self.status

    def check_bound(self, now):
        if now - self.launched.clock_ns > ms(TEST_BOUND_MS):
            self.bound_hit = True
            raise Abort(None, "test bound: 600 s reached", "test bound: 600 s reached")

    def running(self):
        """The program has not exited and has not been killed."""
        return self.pump.exit_ns is None and not self.killed

    def grace_over(self, now):
        return self.grace_deadline is not None and now >= self.grace_deadline

    def early_exit_due(self):
        """The program has exited and the end trigger has not been given (4.4): an early exit."""
        return not self.triggered and self.pump.exit_ns is not None and not self.killed

    def check_conn_overflow(self):
        for conn in self.conns.values():
            if conn.overflow:
                self.abort("overflow", conn.name)

    def check_overflow(self):
        if self.stdout_read and self.stdout.overflow:
            self.abort("overflow", "stdout")
        self.check_conn_overflow()

    def guard(self, now):
        self.check_bound(now)
        if self.grace_over(now) and self.running():
            self.kill_program()
        if self.early_exit_due():
            self.abort("early-exit", status=self.exit_status(), why="the program exited before the end trigger")
        self.check_overflow()

    def pull_lazy(self, lazy, limit):
        got = False
        for stream in lazy:
            if isinstance(stream, rc.Conn) and stream.pull(limit):
                got = True
        return got

    def pull_and_idle(self, lazy, limit, pause_ms):
        if self.pull_lazy(lazy, limit) and pause_ms:
            self.idle(pause_ms)

    def stop_at(self, deadline_ns):
        """The instant the pump may wait until: the deadline, or the grace deadline when that comes first."""
        return deadline_ns if self.grace_deadline is None or self.killed else min(deadline_ns, self.grace_deadline)

    def pump_once(self, now, deadline_ns, poll_s, lazy):
        reason = self.pump.step(min(max(self.stop_at(deadline_ns) - now, 0) / 1e9, 0.005 if lazy else poll_s))
        if reason == "flood":
            self.flooded = True
            raise Abort(None, "standard error overflow", "standard error overflow")

    def wait_round(self, cond, deadline_ns, poll_s, lazy, limit, pause_ms):
        """One round of a wait: True when `cond()` holds, False when the deadline has passed, None to go on."""
        self.refresh()
        self.pull_and_idle(lazy, limit, pause_ms)
        if cond():
            return True
        now = ex.now_ns()
        self.guard(now)
        if now >= deadline_ns:
            return False
        self.pump_once(now, deadline_ns, poll_s, lazy)
        return None

    def wait_until(self, cond, deadline_ns, poll_s=0.05, lazy=(), chunk=None, pause_ms=0):
        """Run the pump until `cond()` holds (True) or the deadline passes (False). A step that cannot wait further raises Abort."""
        limit = chunk or rc.CHUNK
        while True:
            done = self.wait_round(cond, deadline_ns, poll_s, lazy, limit, pause_ms)
            if done is not None:
                return done

    def idle(self, ms_):
        self.wait_until(lambda: False, ex.now_ns() + ms(ms_))

    # ---- the program -------------------------------------------------------------------------------------------------------------

    def kill_program(self):
        self.killed = True
        self.lifecycle.kill(self.launched.proc)

    def term_plain(self, proc):
        if proc.returncode is None:
            proc.send_signal(signal.SIGTERM)

    def term_boxed(self, tracker):
        if not self.wait_known_pid(tracker):
            raise RunnerFault("the program's pid was not found in the box's cgroup: SIGTERM cannot be sent")
        if tracker.pidfd is None:
            os.kill(tracker.pid, signal.SIGTERM)
            return
        try:
            signal.pidfd_send_signal(tracker.pidfd, signal.SIGTERM)
        except ProcessLookupError:
            pass

    def term(self):
        """SIGTERM to the program's host pid (the launcher on the plain path, the pid the tracker found in a box)."""
        tracker = self.launched.tracker
        if tracker.box:
            self.term_boxed(tracker)
        else:
            self.term_plain(self.launched.proc)
        self.signal_sent = "TERM"

    def wait_known_pid(self, tracker):
        deadline = ex.now_ns() + ms(5000)
        while tracker.pid is None and ex.now_ns() < deadline and self.pump.exit_ns is None:
            self.pump.step(0.005)
        return self.pid_or_exit(tracker)

    def pid_or_exit(self, tracker):
        return tracker.pid is not None or self.pump.exit_ns is not None

    def fire_trigger(self):
        self.triggered = True
        self.grace_deadline = ex.now_ns() + ms(self.grace_ms)

    # ---- set-up and start -------------------------------------------------------------------------------------------------------

    def prepare(self, cwd):
        """Resolve `env` and `file` lines in file order: (extra environment as argv words, fixtures)."""
        words, fixtures = [], {}
        for item in self.scenario.setup:
            self.cur = rm.Step("setup", item.line)
            data = self.text_of(item.value)
            if item.kind == "env":
                words.append(item.name.encode() + b"=" + data)
            else:
                fixtures[item.name] = data
                self.variables[item.name] = rf.Bound(os.path.abspath(os.path.join(cwd, rs_view.SCENARIO_DIR, item.name)).encode())
        self.cur = None
        return words, fixtures

    def launch(self):
        cwd = rs_view.start_dir(self.config, self.case.id)
        words, fixtures = self.prepare(cwd)
        if fixtures:
            rs_view.write_fixtures(cwd, fixtures)
            self.fixtures_dir = None if self.config.case_view else os.path.join(cwd, rs_view.SCENARIO_DIR)
        program = ("env", b"PORT=" + str(self.port).encode(), *words, *ex.RUN_PROGRAM)
        self.lifecycle = rs_scope.lifecycle_for(self.config, self.case.id)
        stderr_path = os.path.join(self.config.scratch, f"{self.case.id}.stderr")
        self.stderr_path = stderr_path
        self.launched = rl.start(self.config, self.case.id, self.lifecycle, stderr_path, cwd, program=program)
        self.pump = rs_pump.Pump(self.launched, b"", Collector(self.stdout), stderr_path, keep_stdin=True)

    def probe_port(self):
        now = ex.now_ns()
        if now < self.next_probe:
            return False
        self.next_probe = now + ms(PROBE_MS)
        return self.port_accepts()

    def port_accepts(self):
        """One readiness probe: FORMAT's `ready port`. Only the program's not listening yet is False; a host error of the socket is a RunnerFault."""
        try:
            with socket.create_connection(("127.0.0.1", self.port), timeout=1):
                return True
        except NOT_LISTENING_YET:
            return False
        except OSError as e:
            raise RunnerFault(f"{self.case.in_name}: the host could not probe the port: {e!r}")

    def take_line(self, line):
        """True, with the line (and what came before it) taken out of the standard output so far, when `line` has been written whole."""
        buf, at = self.stdout.buf, 0
        while True:
            end = buf.find(b"\n", at)
            if end < 0:
                del buf[:at]
                return False
            if bytes(buf[at:end]) == line:
                del buf[:end + 1]
                return True
            at = end + 1

    def stdout_line(self, line):
        return lambda: self.take_line(line)

    def start(self):
        st = self.scenario.start
        self.cur = rm.Step("start", st.line)
        self.launch()
        cond = self.probe_port if st.ready == "port" else self.stdout_line(self.text_of(st.text))
        if not self.wait_until(cond, ex.now_ns() + ms(st.within_ms), poll_s=0.01):
            self.abort("not-ready", why="not ready in time")
        self.add("ready")

    # ---- the other steps ---------------------------------------------------------------------------------------------------------

    def s_sleep(self, step):
        self.idle(step.args["for"])

    def s_signal(self, step):
        if step is self.trigger_node:
            self.fire_trigger()
        self.term()

    def exit_entry(self, code):
        if code < 0:
            self.add("exit-signal", name=signal.Signals(-code).name)
        else:
            self.add("exit", status=code)

    def s_wait_exit(self, step):
        deadline = self.grace_deadline or ex.now_ns() + ms(step.args["within"])
        self.wait_until(lambda: self.pump.exit_ns is not None, deadline)
        if self.killed or self.pump.exit_ns is None:
            self.add("exit-timeout")
            return
        self.exit_entry(self.exit_status())

    # ---- the script ---------------------------------------------------------------------------------------------------------------

    def run_repeat(self, node):
        for i in range(node.count):
            self.variables[node.name] = rf.Bound(str(i).encode())
            self.run_nodes(node.body)
        self.variables.pop(node.name, None)

    def run_nodes(self, nodes):
        for node in nodes:
            if isinstance(node, rm.Repeat):
                self.run_repeat(node)
            elif isinstance(node, rm.Together):
                self.cur = node.steps[0]
                self.together(node)
            else:
                self.cur = node
                getattr(self, "s_" + node.op)(node)

    def term_quietly(self):
        try:
            self.term()
        except RunnerFault:
            pass

    def wait_for_exit(self, grace_ms):
        deadline = ex.now_ns() + ms(grace_ms)
        while self.pump.exit_ns is None and ex.now_ns() < deadline:
            self.pump.step(0.02)

    def stop_program(self):
        """The scenario ended before the program did: SIGTERM, a grace period, then the kill."""
        if self.pump.exit_ns is not None or self.killed:
            return
        self.term_quietly()
        self.wait_for_exit(self.grace_ms)
        if self.pump.exit_ns is None:
            self.kill_program()

    def run_script(self):
        try:
            self.start()
            self.run_nodes(self.scenario.body)
        except Abort as stop:
            self.aborted = stop
            self.ended_by = stop.why
            if stop.reason is not None:
                self.ended_by = stop.reason
        finally:
            self.cur = None

    # ---- judging ------------------------------------------------------------------------------------------------------------------

    def memory_failure(self, memory):
        """The verdict of a program the memory limit ended (a kernel kill, or the runtime's own abort), or None."""
        if memory.get("oomKill"):
            return "memory limit", None, [f"  memory.events: oom_kill {memory['oomKill']}"]
        if memory.get("runtimeAbort"):
            return "memory limit", None, [f"  the {memory['runtimeAbort']} runtime's own out-of-memory abort (exit status 134)"]
        return None

    def bound_failure(self):
        """The verdict of a case the runner's bounds ended (standard error overflow, the test bound), or None."""
        if self.flooded:
            return "standard error overflow", None, []
        if self.bound_hit:
            return "test bound: 600 s reached", None, []
        return None

    def mismatch_of(self, verdict):
        """(reason, first mismatch, detail lines) of a transcript that differs; the mismatch keeps the evidence of what was seen."""
        lines = rp.render_verdict(self.case.id, verdict, self.case.in_name, self.case.out_name)
        mismatch = {"entry": verdict.index, "of": verdict.total, "part": verdict.part, "detail": verdict.detail}
        mismatch["evidence"] = rev.evidence(verdict, self.entries)
        return verdict.reason, mismatch, lines[1:]

    def late_failure(self, leftover):
        """The verdict of a transcript that matches but whose scenario ended early or whose program left a process behind, or None."""
        if self.aborted is not None:
            return f"the scenario ended early: {self.ended_by}", None, []
        if leftover:
            return "leftover: a process the program started is still running after the exit", None, []
        return None

    def judge(self, memory, leftover):
        """(reason, first mismatch dict, detail lines) of a failed case, or None for a pass."""
        failed = self.memory_failure(memory) or self.bound_failure()
        if failed:
            return failed
        verdict = rs_match.compare_transcript(self.transcript, self.entries, self.variables, self.ended_by)
        if not verdict.ok:
            return self.mismatch_of(verdict)
        return self.late_failure(leftover)

    def stderr_record(self):
        size = os.path.getsize(self.stderr_path) if os.path.exists(self.stderr_path) else 0
        return {"known": True, "bytes": size, "truncated": size > ex.CAPTURE_CAP, "tail": ex.read_tail(self.stderr_path)}

    def lifecycle_record(self, settled, memory):
        capture = rp.capture_record(False, len(self.stdout.buf), len(self.stdout.buf))
        record = rp.plain_lifecycle(settled.leftover, self.stderr_record(), memory, capture, self.lifecycle.road, self.launched.tracker)
        record["stop"] = {"known": True, "signal": self.signal_sent, "graceMs": self.grace_ms, "killed": self.killed}
        record["signalled"] = {**record["signalled"], "signal": self.signal_sent}
        return record

    def verdict_parts(self, failed, tail):
        """(status, reason, entries, report lines, first mismatch) of a judged case."""
        if failed is None:
            return "PASS", "", len(self.entries), tuple(tail), None
        return "FAIL", failed[0], 0, tuple(failed[2]) + tuple(tail), failed[1]

    def finish(self):
        exited = self.pump.exit_ns is not None and not self.killed
        settled = self.lifecycle.settle(self.launched, "exited" if exited else "timeout")
        abort_facts = rs_abort.abort_facts(settled.code, self.stderr_path, settled.memory)
        memory = rp.memory_record({**settled.memory, **abort_facts})
        record = self.lifecycle_record(settled, memory)
        failed = self.judge(memory, settled.leftover)
        tail = (rp.memory_line(memory),) + rp.abort_lines(memory) + rp.leftover_lines(settled.leftover) + rp.fork_lines(record["fork"])
        status, reason, entries, lines, mismatch = self.verdict_parts(failed, tail)
        wall = ex.ceil_ms(ex.now_ns() - self.launched.clock_ns)
        return CaseResult(self.case.id, status, reason, entries, self.case.r_ids, lines, mismatch, record, wall, self.case.comparison, self.launched.launch_ms)

    def close_all(self):
        for conn in self.conns.values():
            self.unwatch(conn)
            if not conn.closed:
                try:
                    conn.sock.close()
                except OSError:
                    pass

    def run_and_finish(self):
        try:
            self.run_script()
            if self.pump is None:
                raise RunnerFault("the program was never started")
            self.stop_program()
            self.close_all()
            return self.finish()
        except BaseException:  # the runner itself failed or is being stopped: never leave the program under test running
            self.abandon()
            raise

    def abandon(self):
        if self.launched is not None:
            self.close_all()
            _stop_quietly(self)

    def close_handles(self):
        if self.pump is not None:
            self.pump.close()
        if self.launched is not None:
            self.launched.close()

    def remove_fixtures(self):
        if self.fixtures_dir:
            shutil.rmtree(self.fixtures_dir, ignore_errors=True)

    def run(self):
        try:
            try:
                return self.run_and_finish()
            finally:
                self.close_handles()
        finally:
            self.remove_fixtures()


def _stop_quietly(session):
    try:
        session.lifecycle.kill(session.launched.proc)
    except ex.EnvironmentProblem:
        pass


def run_case(case, config):
    """Run one daemon or web scenario and judge it -> CaseResult. Raises LaunchFailed (no verdict) when no sandbox was reported, RunnerFault on a
    harness failure."""
    return Session(case, config).run()
