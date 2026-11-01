"""rs_report: the lines the runner prints (FORMAT v2 5.5) and the JSON result it writes.

Everything here returns text or plain dicts; nothing prints. The JSON result is
`{ format: 1, runner, grammar: 2, kind, build, launch, emptyProgramMs, cases: [{ id, status, reason, rIds, entries, firstMismatch,
lifecycle, wallMs, launchMs }], python }`; what was not measured is `{ known: false, why }`, never zero. `wallMs` is the program's wall from
the case clock (the launcher's `child-pid` with `--launch-status`, else the spawn), `launchMs` the time from the spawn to that line,
`emptyProgramMs` an empty program timed through the same prefix once per run, `launch` is `{ failed, stderr }` (a launch that failed
is exit 3 and no case verdict). `lifecycle.signalled` says which pid the runner knows as the program's and how it was found.
"""
import sys
from dataclasses import dataclass
from typing import Optional

import rs_fields
import rs_lex as lex
from rs_transcript import abbreviate, canonical, render_actual

RUNNER_VERSION = "0.2.0"
GRAMMAR = 2
RESULT_FORMAT = 1
DASH = "—"
WINDOW = 30
LABEL_WIDTH = 8


@dataclass(frozen=True)
class CaseResult:
    id: str
    status: str
    reason: str = ""
    entries: int = 0
    r_ids: tuple = ()
    lines: tuple = ()
    first_mismatch: Optional[dict] = None
    lifecycle: Optional[dict] = None
    wall_ms: int = 0
    comparison: Optional[str] = None
    launch_ms: Optional[int] = None


def render_case(result):
    """The report lines of one case: `PASS <id> - <k> entries`, or `FAIL <id> - <reason>` and its detail lines."""
    if result.status == "PASS":
        return [f"PASS {result.id} {DASH} {result.entries} entries", *result.lines]
    return [f"FAIL {result.id} {DASH} {result.reason}", *result.lines]


def summary_line(results):
    passed = sum(1 for r in results if r.status == "PASS")
    return f"{passed} of {len(results)} cases passed, {len(results) - passed} failed"


# ---- an expected entry as one line ---------------------------------------------------------------------------------------------

def _part_text(part):
    if isinstance(part, lex.Lit):
        return canonical(part.data)[1:-1]
    return "${" + part.name + "}" if isinstance(part, lex.Var) else rs_fields.field_text(part)


def _literal_bytes(item):
    return b"".join(p.data for p in item.parts) if all(isinstance(p, lex.Lit) for p in item.parts) else None


def _quoted(item):
    data = _literal_bytes(item)
    return abbreviate(data) if data is not None else '"' + "".join(_part_text(p) for p in item.parts) + '"'


def _item_text(item):
    return _quoted(item) + (f"*{item.count}" if item.count > 1 else "")


def expr_text(expr):
    return " ".join(_item_text(i) for i in expr.items)


def _recv_text(entry):
    return f"recv {entry.conn} json {entry.body[2]}" if entry.body is not None else f"recv {entry.conn} {expr_text(entry.expr)}"


_EXPECTED_TEXT = {
    "ready": lambda e: "ready", "quiet": lambda e: f"quiet {e.conn}", "closed": lambda e: f"closed {e.conn}", "exit": lambda e: f"exit {e.status}",
    "recv": _recv_text, "reply": lambda e: f"reply {e.conn} {e.status}",
}


def expected_text(entry):
    return _EXPECTED_TEXT[entry.kind](entry)


# ---- a transcript verdict -----------------------------------------------------------------------------------------------------

def _detail_line(verdict):
    if verdict.part in ("", "entry"):
        return [f"  {verdict.detail}"] if verdict.detail else []
    return [f"  {verdict.part}: {verdict.detail}"]


def _step_ref(verdict, in_file):
    actual = verdict.actual
    return f"{in_file}:{actual.step_line}" if actual is not None and actual.step_line else ""


def _entry_lines(verdict, in_file, out_file):
    step, ref = _step_ref(verdict, in_file), f"{out_file}:{verdict.expected.line}"
    column = 2 + LABEL_WIDTH + max(len(step), len(ref)) + 3
    lines = [f"  {'step':<{LABEL_WIDTH}}{step:<{column - 2 - LABEL_WIDTH}}{verdict.actual.step_text}"] if step else []
    lines.append(f"  {'expect':<{LABEL_WIDTH}}{ref:<{column - 2 - LABEL_WIDTH}}{expected_text(verdict.expected)}")
    if verdict.actual is not None:
        lines.append(f"  {'actual':<{column - 2}}" + render_actual(verdict.actual))
    return lines + _detail_line(verdict)


def render_verdict(case_id, verdict, in_file, out_file):
    """The report lines of a failed transcript comparison (5.5): the head, then step, expect, actual and the difference."""
    head = f"FAIL {case_id} {DASH} {verdict.reason}"
    if verdict.part == "check":
        return [head]
    if verdict.part == "count":
        return [head] + ([f"  {verdict.detail}"] if verdict.detail else [])
    return [head] + _entry_lines(verdict, in_file, out_file)


# ---- an olympiad output -------------------------------------------------------------------------------------------------------

def _window(data, byte):
    start, end = max(0, byte - WINDOW), byte + WINDOW
    return ("..." if start else "") + canonical(data[start:end]) + ("..." if end < len(data) else "")


def _kept_window(window, byte):
    """The window of actual bytes kept past the cap, positioned at the difference, or a note when none was kept."""
    if window is None:
        return "(not kept)"
    start, data = window
    shown = _window(data, byte - start)
    return shown if start == 0 or shown.startswith("...") else "..." + shown


def output_difference_lines(out_name, expected, kept, byte, window=None):
    """The bytes of the expected and the actual output around the first difference, and its position. `window` is (offset, bytes) of the
    actual output around it, kept whatever the cap, for a difference past what `kept` holds."""
    shown = _window(kept, byte) if byte <= len(kept) else _kept_window(window, byte)
    return [f"  {'expect':<{LABEL_WIDTH}}{out_name}   {_window(expected, byte)}",
            f"  {'actual':<{LABEL_WIDTH}}{'':<{len(out_name)}}   {shown}",
            f"  first difference at byte {byte}"]


def seconds_text(ms):
    return f"{ms // 1000}.{ms % 1000:03d} s"


def time_text(wall_ms, limit_ms, basis):
    which = "against the floor" if basis == "floor" else "the registered limit"
    return f"time limit: {seconds_text(wall_ms)} over {seconds_text(limit_ms)} ({which})"


# ---- the JSON result --------------------------------------------------------------------------------------------------------------

NOT_CERTIFIED = {"known": False, "why": "not certified"}


def leftover_lines(leftover):
    """The note line of a program that left a process running after its exit (killed by the runner); part ii makes it the lifecycle finding."""
    return ("  leftover: process still running after the exit (killed)",) if leftover else ()


def memory_line(memory):
    return f"  memory: peak {memory['peakBytes']} bytes" if memory.get("known") and "peakBytes" in memory else "  memory: not certified"


def capture_record(truncated, kept, total):
    """What was kept of the output for the report: the verdict never rests on it, but a reader must know when the cap was passed."""
    return {"known": True, "truncated": bool(truncated), "keptBytes": kept, "totalBytes": total}


def capture_lines(capture):
    """The note line of an output that passed the capture cap (a PASS by the stream stays a PASS: the line says only that the rest was not kept)."""
    if not capture["truncated"]:
        return ()
    return (f"  output limit: {capture['totalBytes']} bytes read, the first {capture['keptBytes']} kept for the report",)


def _unknown(value, why):
    return value or {"known": False, "why": why}


PLAIN_ROAD = {"kill": "killpg", "leftoverEvidence": "process group (killpg 0)"}


def signalled_record(program):
    """Which pid the runner knows as the program's and how it found it. An olympiad program is never signalled (`signal` is null): the
    pid is what the exit was awaited on."""
    if program is None:
        return {"signal": None, "pid": None, "found": "not looked for"}
    return {"signal": None, "pid": program.pid, "found": program.found}


def fork_record(program):
    """The finding `fork`: more than one candidate in the program's cgroup where D38 promises exactly one. Unknown when it was not counted."""
    if program is None or program.count is None:
        return {"known": False, "why": "the processes of the program's cgroup were not counted"}
    return {"known": True, "found": program.count > 1, "count": program.count}


def fork_lines(program):
    return (f"  fork: {program.count} processes in the program's cgroup where one was expected",) if program is not None and (program.count or 0) > 1 else ()


def plain_lifecycle(leftover, stderr=None, memory=None, capture=None, road=None, program=None):
    """The lifecycle record of a case: what was not observed is unknown, not zero. `road` names the way the leftovers were found and the
    kill was done (the plain path: the process group; the scope path: `cgroup.procs` and `cgroup.kill`), so a reader knows which road ran."""
    road = road or PLAIN_ROAD
    return {
        "stop": {"known": False, "why": "this program is not signalled"},
        "signalled": signalled_record(program),
        "fork": fork_record(program),
        "leftover": {"known": True, "found": bool(leftover)} if leftover is not None else _unknown(None, "not checked"),
        "leftoverEvidence": road["leftoverEvidence"],
        "kill": road["kill"],
        "memory": memory or NOT_CERTIFIED,
        "stderr": _unknown(stderr, "not captured"),
        "capture": _unknown(capture, "not captured"),
    }


def _launch_ms(value):
    return value if value is not None else {"known": False, "why": "no launch status: the case clock starts at the spawn"}


def case_record(result):
    return {
        "id": result.id, "status": result.status, "reason": result.reason, "rIds": list(result.r_ids), "comparison": result.comparison, "entries": result.entries,
        "firstMismatch": result.first_mismatch, "lifecycle": result.lifecycle, "wallMs": result.wall_ms,
        "launchMs": _launch_ms(result.launch_ms),
    }


NO_LAUNCH = {"failed": False, "stderr": ""}
NO_EMPTY = {"known": False, "why": "no empty program was timed"}


def run_record(kind, build, cases, launch=None, empty_program_ms=None):
    """The JSON result. `launch` is `{failed, stderr}`; `empty_program_ms` is milliseconds or None (then `{known: false, why}`)."""
    return {
        "format": RESULT_FORMAT, "runner": RUNNER_VERSION, "grammar": GRAMMAR, "kind": kind, "build": build,
        "launch": launch or NO_LAUNCH, "emptyProgramMs": NO_EMPTY if empty_program_ms is None else empty_program_ms,
        "cases": cases, "python": sys.version,
    }
