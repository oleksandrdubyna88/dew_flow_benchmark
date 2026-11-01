"""rs_report: the lines the runner prints (FORMAT v2 5.5) and the JSON result it writes.

Everything here returns text or plain dicts; nothing prints. The JSON result is
`{ format: 1, runner, grammar: 2, kind, build, cases: [{ id, status, reason, rIds, entries, firstMismatch, lifecycle, wallMs }], python }`;
what was not measured is `{ known: false, why }`, never zero.
"""
import sys
from dataclasses import dataclass
from typing import Optional

import rs_fields
import rs_lex as lex
from rs_transcript import abbreviate, canonical, render_actual

RUNNER_VERSION = "0.1.0"
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


def output_difference_lines(out_name, expected, kept, byte):
    """The bytes of the expected and the actual output around the first difference, and its position."""
    shown = _window(kept, byte) if byte <= len(kept) else "(not kept)"
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


def memory_line(memory):
    return f"  memory: peak {memory['peakBytes']} bytes" if memory.get("known") and "peakBytes" in memory else "  memory: not certified"


def plain_lifecycle(leftover, stderr=None, memory=None):
    """The lifecycle record of a case run on the plain path (no cgroup): what was not observed is unknown, not zero."""
    return {
        "stop": {"known": False, "why": "this program is not signalled"},
        "leftover": {"known": True, "found": bool(leftover)} if leftover is not None else {"known": False, "why": "not checked"},
        "leftoverEvidence": "process group (killpg 0)",
        "memory": memory or NOT_CERTIFIED,
        "stderr": stderr or {"known": False, "why": "not captured"},
    }


def case_record(result):
    return {
        "id": result.id, "status": result.status, "reason": result.reason, "rIds": list(result.r_ids), "comparison": result.comparison, "entries": result.entries,
        "firstMismatch": result.first_mismatch, "lifecycle": result.lifecycle, "wallMs": result.wall_ms,
    }


def run_record(kind, build, cases):
    return {"format": RESULT_FORMAT, "runner": RUNNER_VERSION, "grammar": GRAMMAR, "kind": kind, "build": build, "cases": cases, "python": sys.version}
