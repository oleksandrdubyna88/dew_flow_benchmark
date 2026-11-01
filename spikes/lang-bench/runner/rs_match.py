"""rs_match: expected transcript entries compared with what the runner observed (FORMAT v2 5.2 to 5.5, V6).

`match_entry` compares one expected entry with one observation; `compare` walks the expanded transcript in order (an `unordered`
group by one-to-one assignment), binds what fields matched, and then evaluates the V6 `check` lines in file order. The first
difference becomes a `Verdict`; the report module (rs_report) only prints it.
"""
import re
from datetime import datetime
from decimal import Decimal
from typing import Any, NamedTuple, Optional

import rs_assign
import rs_fields as rf
import rs_jmatch
import rs_json
import rs_transcript as rt
from rs_transcript import abbreviate

DECIMAL_TEXT = re.compile(rb"-?[0-9]+(\.[0-9]+)?\Z")


class Result(NamedTuple):
    ok: bool
    part: str = ""
    detail: str = ""
    kind: str = ""
    binds: tuple = ()


class Verdict(NamedTuple):
    ok: bool
    reason: str = ""
    index: int = 0
    total: int = 0
    expected: Any = None
    actual: Any = None
    part: str = ""
    detail: str = ""
    kind: str = ""
    entries: int = 0


OK = Result(True)


def _fail(part, detail, kind=""):
    return Result(False, part, detail, kind)


def _template_result(parts, data, variables, clock):
    found = rf.match_template(parts, data, variables, clock)
    if found.ok:
        return Result(True, binds=found.binds)
    return _fail("", found.note or f"first difference at byte {found.furthest}")


def _bytes_result(expr, data, variables, clock):
    parts, error = rf.resolve(expr, variables)
    return _fail("", error) if error else _template_result(parts, data, variables, clock)


# ---- one entry ------------------------------------------------------------------------------------------------------------------

def _m_plain(exp, act, variables, clock):
    return OK


def _m_exit(exp, act, variables, clock):
    return OK if act.status == exp.status else _fail("", f"expected exit {exp.status}, actual {act.status}")


def _json_result(found, label):
    """A Result from a JSON body comparison: the path of the first difference goes in `part` after `label`."""
    if found.ok:
        return Result(True, binds=found.binds)
    if found.diff is None:
        return _fail(label, found.message, found.kind)
    return _fail(f"{label} {found.diff.path}", f"expected {found.diff.expected}, actual {found.diff.actual}")


def _m_recv(exp, act, variables, clock):
    if exp.body is not None:
        return _json_result(rs_jmatch.match_json_body(exp.body[1], act.data, variables, clock), "json")
    return _bytes_result(exp.expr, act.data, variables, clock)


def _literal_name(expr, variables):
    parts, error = rf.resolve(expr, variables)
    return (b"".join(p.data for p in parts if hasattr(p, "data")), error)


def _header_values(act, name):
    return [v.strip(b" \t") for n, v in act.headers if n.lower() == name.lower()]


def _one_header(exp_name, exp_value, act, variables, clock):
    name, error = _literal_name(exp_name, variables)
    label = f"header {name.decode('utf-8', 'replace').lower()}"
    if error:
        return _fail(label, error)
    values = _header_values(act, name)
    if not values:
        return _fail(label, f"expected {_shown(exp_value, variables)}, actual (absent)")
    result = _bytes_result(exp_value, b", ".join(values), variables, clock)
    shown = f"expected {_shown(exp_value, variables)}, actual {abbreviate(b', '.join(values))}"
    return result if result.ok else _fail(label, shown)


def _shown(expr, variables):
    parts, error = rf.resolve(expr, variables)
    return error or abbreviate(b"".join(p.data if hasattr(p, "data") else b"?" for p in parts))


def _headers_result(exp, act, variables, clock):
    binds = ()
    for name, value in exp.headers:
        result = _one_header(name, value, act, variables, clock)
        if not result.ok:
            return result
        binds += result.binds
    return Result(True, binds=binds)


def _body_empty(exp, act, variables, clock):
    return OK if not act.body else _fail("body", f"expected an empty body, actual {abbreviate(act.body)}")


def _body_str(exp, act, variables, clock):
    result = _bytes_result(exp.body[1], act.body, variables, clock)
    return result if result.ok else result._replace(part="body")


def _body_json(exp, act, variables, clock):
    return _json_result(rs_jmatch.match_json_body(exp.body[1], act.body, variables, clock), "body")


_BODIES = {"empty": _body_empty, "str": _body_str, "json": _body_json}


def _m_reply(exp, act, variables, clock):
    if act.status != exp.status:
        return _fail("status", f"expected {exp.status}, actual {act.status}")
    headers = _headers_result(exp, act, variables, clock)
    if not headers.ok:
        return headers
    body = _BODIES[exp.body[0]](exp, act, variables, clock)
    return body._replace(binds=headers.binds + body.binds) if body.ok else body


_MATCHERS = {"ready": _m_plain, "quiet": _m_plain, "closed": _m_plain, "exit": _m_exit, "recv": _m_recv, "reply": _m_reply}


def _same_slot(exp, act):
    return act.kind == exp.kind and exp.conn in ("*", act.conn)


def _with_loop(entry, variables):
    return {**variables, **{name: rf.Bound(str(i).encode("ascii")) for name, i in entry.loop}}


def match_entry(exp, act, variables=None, clock=None):
    """Compare one expected entry with one observation -> Result(ok, part, detail, kind, binds)."""
    if not _same_slot(exp, act):
        return _fail("entry", "")
    return _MATCHERS[exp.kind](exp, act, _with_loop(exp, variables or {}), _clock_of(clock, act))


def _clock_of(clock, act):
    return clock or act.at or datetime.now(rf.UTC)


# ---- the whole transcript ---------------------------------------------------------------------------------------------------------

class _Run:
    def __init__(self, expected, actual, variables):
        self.exp, self.act, self.variables, self.bound = expected, actual, dict(variables or {}), {}

    def take(self, binds):
        for name, value in binds:
            self.bound[name] = value
            self.variables[name] = value

    def differs(self, i, result, actual):
        total = len(self.exp)
        reason = result.kind or f"entry {i + 1} of {total} differs"
        return Verdict(False, reason, i + 1, total, self.exp[i], actual, result.part, result.detail, result.kind)

    def ordered(self, i):
        result = match_entry(self.exp[i], self.act[i], self.variables)
        if result.ok:
            self.take(result.binds)
            return None
        return self.differs(i, result, self.act[i])

    def group(self, start, size):
        rows, cols = self.exp[start:start + size], self.act[start:start + size]
        owner = rs_assign.assign(size, size, lambda r, c: match_entry(rows[r], cols[c], self.variables).ok)
        lost = rs_assign.unmatched_rows(size, owner)
        if lost:
            return self.differs(start + lost[0], _fail("entry", "no assignment of the group's observations matches it"), None)
        for c, r in enumerate(owner):
            self.take(match_entry(rows[r], cols[c], self.variables).binds)
        return None

    def slot(self, start, size):
        return self.ordered(start) if self.exp[start].group is None else self.group(start, size)

    def entries(self):
        start = 0
        for size in rt.slot_sizes(self.exp):
            if start + size > len(self.act):
                break
            verdict = self.slot(start, size)
            if verdict is not None:
                return verdict
            start += size
        return None


def compare(expected, checks, actual, variables=None, ended_by=""):
    """Compare the expanded expected entries and the `check` lines with the observations -> Verdict (ok, reason, index, ...)."""
    run = _Run(expected, actual, variables)
    verdict = run.entries()
    if verdict is not None:
        return verdict
    if len(expected) != len(actual):
        return Verdict(False, f"expected {len(expected)} entries, actual {len(actual)}", 0, len(expected), None, None, "count", ended_by)
    failed = run_checks(checks, run.bound)
    return Verdict(False, f"check: {failed}", 0, len(expected), None, None, "check", failed) if failed else Verdict(True, entries=len(expected))


def compare_transcript(transcript, actual, variables=None, ended_by=""):
    return compare(rt.expand(transcript), rt.checks_of(transcript), actual, variables, ended_by)


# ---- the V6 checks ---------------------------------------------------------------------------------------------------------------

def _expand_token(token, bound):
    return [n for n in bound if n.startswith(token[:-1])] if token.endswith("*") else [token]


def _names_of(check, bound):
    return [n for token in check.names for n in _expand_token(token, bound)]


def _values_equal(a, b):
    if a.json is not rf.MISSING and b.json is not rf.MISSING:
        return rs_json.values_equal(a.json, b.json)
    return a.raw == b.raw


def _first_equal(names, bound):
    return next((f"{a} and {b} are equal" for i, a in enumerate(names) for b in names[i + 1:] if _values_equal(bound[a], bound[b])), None)


def _first_unbound(names, bound):
    return next((n for n in names if n not in bound), None)


def _unmatched_glob(check, bound):
    return next((t for t in check.names if t.endswith("*") and not _expand_token(t, bound)), None)


def _distinct(check, bound):
    glob = _unmatched_glob(check, bound)
    if glob:
        return f"{glob} matches no bound name"
    names = _names_of(check, bound)
    absent = _first_unbound(names, bound)
    return f"{absent} is not bound" if absent else _first_equal(names, bound)


def _number(value):
    if value.json is not rf.MISSING:
        return value.json if isinstance(value.json, Decimal) else None
    return Decimal(value.raw.decode("ascii")) if DECIMAL_TEXT.match(value.raw) else None


def _operand(token, bound):
    if re.match(r"-?[0-9]", token):
        return Decimal(token), token
    return (_number(bound[token]), f"{token} ({_text(bound[token])})") if token in bound else (None, f"{token} (not bound)")


def _text(value):
    return rs_json.render(value.json) if value.json is not rf.MISSING else value.raw.decode("utf-8", "replace")


def _not_a_number(operand):
    return f"{operand[1]} is not a number" if operand[0] is None else None


def _compare_operands(left, right):
    return _not_a_number(left) or _not_a_number(right) or (None if left[0] == right[0] else f"{left[1]} is not equal to {right[1]}")


def _equal(check, bound):
    name = check.names[0]
    if name not in bound:
        return f"{name} is not bound"
    return _compare_operands(_operand(name, bound), _operand(check.other, bound))


_CHECKERS = {"distinct": _distinct, "equal": _equal}


def run_checks(checks, bound):
    """The first failed check as text, or None."""
    for check in checks:
        failed = _CHECKERS[check.op](check, bound)
        if failed:
            return failed
    return None
