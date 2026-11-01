"""rs_fields: the normalised fields of FORMAT v2 5.3 and the matcher of one expected byte string against an actual one.

A string is a list of parts (Lit, Var, Field). Variables are substituted first; a field then matches the SHORTEST run of bytes that lets
the rest of the string match (backtracking, with a memo of failed (part, position) pairs so it cannot go exponential), and a field
may bind what it matched (V6). Numbers inside a `num` field are compared as exact fractions, never as floats.
"""
import re
from datetime import datetime, timedelta, timezone
from fractions import Fraction
from typing import Any, NamedTuple

import rs_lex as lex
from rs_lex import Field, Lit, Var

MISSING = object()
MAX_PARTS = 200
MAX_STEPS = 500_000
MAX_EXPONENT = 1_000
UTC = timezone.utc

FIELD_PATTERNS = {
    "int": rb"-?(0|[1-9][0-9]*)",
    "uint": rb"0|[1-9][0-9]*",
    "hex": rb"[0-9a-f]+",
    "uuid": rb"[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12}",
    "token": rb"[A-Za-z0-9._~-]+",
    "ts": rb"[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}(\.[0-9]{1,9})?Z",
    "num": rb"-?[0-9]+(\.[0-9]+)?",
}
FIELD_PATTERNS["ts~"] = FIELD_PATTERNS["ts"]
FIELD_RX = {kind: re.compile(pattern) for kind, pattern in FIELD_PATTERNS.items()}
FIXED_LENGTH = {"uuid": (36, 36), "ts": (20, 30), "ts~": (20, 30)}
TS_PARTS = re.compile(rb"([0-9]{4})-([0-9]{2})-([0-9]{2})T([0-9]{2}):([0-9]{2}):([0-9]{2})(?:\.([0-9]{1,9}))?Z\Z")


class Bound(NamedTuple):
    """What a variable holds: its bytes, and for a value that came from JSON also the typed JSON value."""
    raw: bytes
    json: Any = MISSING


class TemplateMatch(NamedTuple):
    ok: bool
    binds: tuple = ()
    furthest: int = 0
    note: str = ""


def parse_ts(run):
    """A UTC datetime from `YYYY-MM-DDThh:mm:ss[.fraction]Z`, or None when the text is not a real time."""
    m = TS_PARTS.match(run)
    if m is None:
        return None
    try:
        return datetime(*_ts_numbers(m), tzinfo=UTC)
    except ValueError:
        return None


def _ts_numbers(m):
    numbers = [int(g) for g in m.groups()[:6]]
    micro = int((m.group(7) or b"0").ljust(6, b"0")[:6])
    return (*numbers, micro)


def _within_window(run, window_ms, clock):
    ts = parse_ts(run)
    return ts is not None and abs(ts - clock) <= timedelta(milliseconds=window_ms)


def fraction_of(text):
    """An exact Fraction of a decimal text or Decimal, or None when its exponent is too large to do exact arithmetic with."""
    exponent = text.as_tuple().exponent if hasattr(text, "as_tuple") else 0
    return None if abs(exponent) > MAX_EXPONENT else Fraction(text)


def within_tolerance(value, centre, tolerance):
    """True when |value - centre| <= tolerance, all three exact (Decimal, or a decimal text)."""
    parts = [fraction_of(x) for x in (value, centre, tolerance)]
    return None not in parts and abs(parts[0] - parts[1]) <= parts[2]


def _numeric_ok(field, run, clock):
    return within_tolerance(run.decode("ascii"), field.args[0], field.args[1])


_EXTRA_CHECKS = {
    "ts": lambda field, run, clock: parse_ts(run) is not None,
    "ts~": lambda field, run, clock: _within_window(run, field.args[0], clock),
    "num": _numeric_ok,
}


def field_accepts(field, run, clock):
    """Does `run` (bytes) match the field: its pattern in full, and for `ts`, `ts~` and `num` a real time, the window, the tolerance."""
    if FIELD_RX[field.kind].fullmatch(run) is None:
        return False
    return _EXTRA_CHECKS.get(field.kind, lambda *_: True)(field, run, clock)


def field_text(field):
    """A field as it is written in a transcript: `${:uint}`, `${:ts~10s}`, `${:num 1.5 0.25=x}`."""
    args = {"ts~": lambda a: f"~{a[0]}ms", "num": lambda a: f" {a[0]} {a[1]}"}.get(field.kind, lambda a: "")(field.args)
    bind = "=" + "".join(p.data.decode("ascii") if isinstance(p, Lit) else "${" + p.name + "}" for p in field.bind) if field.bind else ""
    return "${:" + field.kind.rstrip("~") + args + bind + "}"


# ---- variables ------------------------------------------------------------------------------------------------------------------

def _item_parts(item, variables):
    out = []
    for part in item.parts:
        out.append(Lit(variables[part.name].raw) if isinstance(part, Var) else part)
    return out


def _joinable(merged, part):
    return bool(merged) and isinstance(part, Lit) and isinstance(merged[-1], Lit)


def _merge(parts):
    merged = []
    for part in parts:
        if _joinable(merged, part):
            merged[-1] = Lit(merged[-1].data + part.data)
        else:
            merged.append(part)
    return merged


def _repeated(parts, count):
    single = _merge(parts)
    if len(single) == 1 and isinstance(single[0], Lit):
        return [Lit(single[0].data * count)]
    return single * count


def _first_missing(names, variables):
    return next((n for n in names if n not in variables), None)


def _size_estimate(expr):
    return sum(1 if all(isinstance(p, Lit) for p in i.parts) else len(i.parts) * i.count for i in expr.items)


def _problem(expr, variables):
    absent = _first_missing(lex.vars_of(expr), variables)
    if absent is not None:
        return f"variable '{absent}' is not bound"
    return "the string expands to too many parts" if _size_estimate(expr) > MAX_PARTS else None


def _expanded(expr, variables):
    return [p for item in expr.items for p in _repeated(_item_parts(item, variables), item.count)]


def resolve(expr, variables):
    """`(parts, None)` with every variable replaced by its bytes and every repeat written out, or `(None, message)`."""
    problem = _problem(expr, variables)
    return (None, problem) if problem else (_merge(_expanded(expr, variables)), None)


def _var_names(parts):
    return [p.name for p in parts if isinstance(p, Var)]


def _substituted(part, variables):
    return Lit(variables[part.name].raw) if isinstance(part, Var) else part


def resolve_template(parts, variables):
    """The same for a list of parts that is already flat (a JSON string's template)."""
    absent = _first_missing(_var_names(parts), variables)
    if absent is not None:
        return None, f"variable '{absent}' is not bound"
    return _merge([_substituted(p, variables) for p in parts]), None


def bind_name(template, variables):
    """The name a field binds, with a repeat variable filled in: `r${i}` -> `r2`."""
    return "".join(p.data.decode("ascii") if isinstance(p, Lit) else variables[p.name].raw.decode("ascii") for p in template)


# ---- the matcher ------------------------------------------------------------------------------------------------------------------

def _common_prefix(a, b):
    n = 0
    while n < min(len(a), len(b)) and a[n] == b[n]:
        n += 1
    return n


class _Search:
    def __init__(self, parts, data, variables, clock):
        self.parts, self.data, self.variables, self.clock = parts, data, variables, clock
        self.failed, self.furthest, self.steps = set(), 0, 0

    def run(self, i, pos):
        """The binds of a match of parts[i:] against data[pos:] to its end, or None."""
        self.steps += 1
        if self._exhausted(i, pos):
            return None
        found = self._step(i, pos)
        return found if found is not None else self._fail(i, pos)

    def _exhausted(self, i, pos):
        return (i, pos) in self.failed or self.steps > MAX_STEPS

    def _step(self, i, pos):
        if i == len(self.parts):
            return [] if pos == len(self.data) else None
        part = self.parts[i]
        return self._literal(i, pos, part) if isinstance(part, Lit) else self._field(i, pos, part)

    def _fail(self, i, pos):
        self.failed.add((i, pos))
        self.furthest = max(self.furthest, pos)
        return None

    def _literal(self, i, pos, lit):
        if self.data.startswith(lit.data, pos):
            return self.run(i + 1, pos + len(lit.data))
        self.furthest = max(self.furthest, pos + _common_prefix(lit.data, self.data[pos:pos + len(lit.data)]))
        return None

    def _field(self, i, pos, field):
        for end in self._ends(i, pos, field):
            self.steps += 1
            if not field_accepts(field, self.data[pos:end], self.clock):
                continue
            rest = self.run(i + 1, end)
            if rest is not None:
                return self._bind(field, self.data[pos:end]) + rest
        self.furthest = max(self.furthest, pos + self._reach(field, pos))
        return None

    def _bind(self, field, run):
        return [(bind_name(field.bind, self.variables), Bound(run))] if field.bind else []

    def _reach(self, field, pos):
        m = FIELD_RX[field.kind].match(self.data, pos)
        return 0 if m is None else m.end() - pos

    def _ends(self, i, pos, field):
        """The candidate end positions of a field starting at `pos`, shortest first."""
        low, high = FIXED_LENGTH.get(field.kind, (1, len(self.data) - pos))
        return self._ends_before(self.parts[i + 1] if i + 1 < len(self.parts) else None, pos, low, high)

    def _ends_before(self, nxt, pos, low, high):
        if nxt is None:
            return [len(self.data)] if low <= len(self.data) - pos <= high else []
        if isinstance(nxt, Lit):
            return self._literal_ends(pos, nxt.data, low, high)
        return range(pos + low, min(len(self.data), pos + high) + 1)

    def _literal_ends(self, pos, literal, low, high):
        ends, at = [], self.data.find(literal, pos + low)
        while at != -1 and at - pos <= high:
            ends.append(at)
            at = self.data.find(literal, at + 1)
        return ends


def match_template(parts, data, variables, clock):
    """Does the byte string `data` match `parts` (Lit and Field, variables already substituted)? -> TemplateMatch."""
    search = _Search(parts, data, variables, clock)
    binds = search.run(0, 0)
    if search.steps > MAX_STEPS:
        return TemplateMatch(False, (), search.furthest, "the match was abandoned as too complex")
    return TemplateMatch(True, tuple(binds)) if binds is not None else TemplateMatch(False, (), search.furthest)
