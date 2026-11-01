"""rs_checks: the relational checks of FORMAT v2.1 5.5, evaluated after every entry matched, in file order.

One namespace: the script's `capture` variables and the transcript's binding fields alike (the caller passes both, in binding order).
`distinct` compares values pairwise, as bytes or as exact JSON values; `equal` compares a name with a decimal (exact, a JSON string is never
a number), with another name (the same rule as `distinct`) or with a string (bytes, or a JSON string with those characters, never a number).
"""
import itertools
import re
from decimal import Decimal

import rs_fields as rf
import rs_json
from rs_transcript import canonical

DECIMAL_TEXT = re.compile(rb"-?[0-9]+(\.[0-9]+)?\Z")
NUMBER_TOKEN = re.compile(r"-?[0-9]")


def _expand_token(token, variables):
    return [n for n in variables if n.startswith(token[:-1])] if token.endswith("*") else [token]


def _names_of(check, variables):
    return [n for token in check.names for n in _expand_token(token, variables)]


def _typed(value):
    return value.json is not rf.MISSING


def _values_equal(a, b):
    """Exact JSON values when both came from JSON, else bytes."""
    return rs_json.values_equal(a.json, b.json) if _typed(a) and _typed(b) else a.raw == b.raw


def _text(value):
    return rs_json.render(value.json) if _typed(value) else value.raw.decode("utf-8", "replace")


def _first_equal(names, variables):
    pairs = itertools.combinations(names, 2)
    return next((f"{a} and {b} are equal" for a, b in pairs if _values_equal(variables[a], variables[b])), None)


def _first_unbound(names, variables):
    return next((n for n in names if n not in variables), None)


def _unmatched_glob(check, variables):
    return next((t for t in check.names if t.endswith("*") and not _expand_token(t, variables)), None)


def _distinct(check, variables):
    glob = _unmatched_glob(check, variables)
    if glob:
        return f"{glob} matches no bound name"
    names = _names_of(check, variables)
    absent = _first_unbound(names, variables)
    return f"{absent} is not bound" if absent else _first_equal(names, variables)


def _number(value):
    """The exact Decimal of a value that is a JSON number, or of bytes that are a decimal; None for anything else (a JSON string too)."""
    if _typed(value):
        return value.json if isinstance(value.json, Decimal) else None
    return Decimal(value.raw.decode("ascii")) if DECIMAL_TEXT.match(value.raw) else None


def _equal_decimal(name, value, token):
    return None if _number(value) == Decimal(token) else f"{name} is {_text(value)}, expected {token}"


def _string_equal(value, literal):
    if _typed(value):
        return isinstance(value.json, str) and value.json.encode("utf-8", "surrogatepass") == literal
    return value.raw == literal


def _shown(value):
    """A value as the message shows it: JSON as JSON (a string with quotes, a number without), bytes as a quoted string."""
    return rs_json.render(value.json) if _typed(value) else canonical(value.raw)


def _equal_string(name, value, literal):
    return None if _string_equal(value, literal) else f"{name} is {_shown(value)}, expected {canonical(literal)}"


def _equal_name(name, value, other, variables):
    if other not in variables:
        return f"{other} is not bound"
    peer = variables[other]
    return None if _values_equal(value, peer) else f"{name} ({_text(value)}) is not equal to {other} ({_text(peer)})"


def _equal(check, variables):
    name = check.names[0]
    if name not in variables:
        return f"{name} is not bound"
    value = variables[name]
    if check.literal is not None:
        return _equal_string(name, value, check.literal)
    if NUMBER_TOKEN.match(check.other):
        return _equal_decimal(name, value, check.other)
    return _equal_name(name, value, check.other, variables)


_CHECKERS = {"distinct": _distinct, "equal": _equal}


def run_checks(checks, variables):
    """The first failed check as text, or None."""
    for check in checks:
        failed = _CHECKERS[check.op](check, variables)
        if failed:
            return failed
    return None
