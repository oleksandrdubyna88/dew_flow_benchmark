"""rs_json: JSON read and compared exactly (FORMAT v2 5.2, decision V2).

Numbers are `Decimal` (an integer and a decimal alike), so `2` equals `2.0` and `-0` equals `0` by value, and two integers that
differ in the last digit of 9007199254740993 stay different. A repeated member name, NaN, Infinity and -Infinity are refused.
Nothing here is a float, and nothing compares a string form or `as_tuple()` of a number.
"""
import json
from decimal import Decimal
from typing import Any, NamedTuple

MAX_DEPTH = 200


class Parsed(NamedTuple):
    ok: bool
    value: Any = None
    kind: str = ""
    message: str = ""


class _Refused(Exception):
    pass


def _refuse_constant(token):
    raise _Refused(f"{token} is not a JSON value")


def _no_duplicates(pairs):
    seen = {}
    for name, member in pairs:
        if name in seen:
            raise _Refused(f"member name {json.dumps(name, ensure_ascii=False)} is repeated in one object")
        seen[name] = member
    return seen


def parse(data):
    """`Parsed(ok, value)`, or `Parsed(False, kind='bad-utf8' | 'bad-json', message=...)`. `data` is bytes (strict UTF-8) or str."""
    if isinstance(data, bytes):
        try:
            data = data.decode("utf-8")
        except UnicodeDecodeError as e:
            return Parsed(False, None, "bad-utf8", f"invalid UTF-8 at byte {e.start}")
    return _loads(data)


def _loads(text):
    try:
        value = json.loads(text, parse_float=Decimal, parse_int=Decimal, parse_constant=_refuse_constant, object_pairs_hook=_no_duplicates)
    except _Refused as e:
        return Parsed(False, None, "bad-json", str(e))
    except json.JSONDecodeError as e:
        return Parsed(False, None, "bad-json", f"{e.msg} at character {e.pos}")
    except RecursionError:
        return Parsed(False, None, "bad-json", f"nested deeper than {MAX_DEPTH}")
    return _depth_checked(value)


def _depth_checked(value):
    if depth_of(value) > MAX_DEPTH:
        return Parsed(False, None, "bad-json", f"nested deeper than {MAX_DEPTH}")
    return Parsed(True, value)


def depth_of(value):
    """The nesting depth of a parsed value, counted without recursion."""
    deepest, stack = 0, [(value, 1)]
    while stack:
        node, depth = stack.pop()
        deepest = max(deepest, depth)
        stack.extend((child, depth + 1) for child in children_of(node))
    return deepest


def children_of(node):
    if isinstance(node, dict):
        return list(node.values())
    return node if isinstance(node, list) else []


def kind_of(value):
    """'null', 'bool', 'number', 'string', 'array' or 'object'. A bool is not a number although Python says True == 1."""
    if value is None:
        return "null"
    return _KINDS[type(value)]


_KINDS = {bool: "bool", Decimal: "number", str: "string", list: "array", dict: "object"}


def values_equal(left, right):
    """Exact JSON equality: same kind, numbers by value, objects without regard to member order, arrays in order."""
    stack = [(left, right)]
    while stack:
        a, b = stack.pop()
        pair = _pair_children(a, b)
        if pair is None:
            return False
        stack.extend(pair)
    return True


def _pair_children(a, b):
    """The child pairs still to compare, [] when `a` and `b` are equal leaves, None when they differ."""
    if kind_of(a) != kind_of(b):
        return None
    return _COMPARE[kind_of(a)](a, b)


def _leaf(a, b):
    return [] if a == b else None


def _array(a, b):
    return list(zip(a, b)) if len(a) == len(b) else None


def _object(a, b):
    return [(a[k], b[k]) for k in a] if a.keys() == b.keys() else None


_COMPARE = {"null": _leaf, "bool": _leaf, "number": _leaf, "string": _leaf, "array": _array, "object": _object}


def render(value):
    """Compact JSON text of a parsed value with every number as its exact decimal digits."""
    return _RENDER[kind_of(value)](value)


_RENDER = {
    "null": lambda v: "null",
    "bool": lambda v: "true" if v else "false",
    "number": str,
    "string": lambda v: json.dumps(v, ensure_ascii=False),
    "array": lambda v: "[" + ",".join(render(x) for x in v) + "]",
    "object": lambda v: "{" + ",".join(json.dumps(k, ensure_ascii=False) + ":" + render(x) for k, x in v.items()) + "}",
}
