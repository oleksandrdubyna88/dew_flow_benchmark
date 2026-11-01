"""rs_jmatch: an actual JSON body compared with an expected `body json` value (FORMAT v2 5.2 to 5.4).

Objects match when they have the same member names and each member matches, in any order; arrays element by element; numbers by exact
decimal value; a string of the expected value may be a template (a variable, a normalised field, a mix) and `{"$unordered": [...]}`
stands for an array whose elements match in any order, under a one-to-one assignment. The first difference is returned with its path.
"""
import functools
import re
from datetime import datetime
from typing import Any, NamedTuple, Optional

import rs_assign
import rs_fields as rf
import rs_json
import rs_lex as lex
from rs_json import kind_of, render
from rs_lex import Field, Lit, Var


class Diff(NamedTuple):
    path: str
    expected: str
    actual: str

    def text(self):
        return f"body {self.path}: expected {self.expected}, actual {self.actual}"


class JsonResult(NamedTuple):
    ok: bool
    kind: str = ""
    message: str = ""
    diff: Optional[Diff] = None
    binds: tuple = ()


class Ctx:
    """Variables, the clock of the entry being compared, and what fields bound so far."""

    def __init__(self, variables, clock):
        self.variables, self.clock, self.binds = variables, clock, []

    def trial(self):
        return Ctx(self.variables, self.clock)


@functools.lru_cache(maxsize=4096)
def _parts_of(text):
    return tuple(lex.template_parts(text) or ())


IDENTIFIER = re.compile(r"[A-Za-z_][A-Za-z0-9_]*\Z")


def member_path(path, name):
    return f"{path}.{name}" if IDENTIFIER.match(name) else f"{path}[{render(name)}]"


# ---- leaves ---------------------------------------------------------------------------------------------------------------------

def _diff_leaf(exp, act, ctx, path):
    same = kind_of(exp) == kind_of(act) and exp == act
    return None if same else Diff(path, render(exp), render(act))


def _diff_array(exp, act, ctx, path):
    if kind_of(act) != "array" or len(act) != len(exp):
        return Diff(path, f"an array of {len(exp)}", _describe_actual(act))
    return _first_diff(((e, a, f"{path}[{i}]") for i, (e, a) in enumerate(zip(exp, act))), ctx)


def _describe_actual(act):
    shown = {"array": lambda a: f"an array of {len(a)}", "object": lambda a: f"an object with {len(a)} members"}
    return shown.get(kind_of(act), render)(act)


def _first_diff(triples, ctx):
    for exp, act, path in triples:
        found = json_diff(exp, act, ctx, path)
        if found is not None:
            return found
    return None


def _member_triples(exp, act, path):
    return ((value, act[name], member_path(path, name)) for name, value in exp.items())


def _diff_object(exp, act, ctx, path):
    if "$unordered" in exp:
        return _diff_unordered(exp["$unordered"], act, ctx, path)
    return _object_shape_diff(exp, act, path) or _first_diff(_member_triples(exp, act, path), ctx)


def _object_shape_diff(exp, act, path):
    if kind_of(act) != "object":
        return Diff(path, f"an object with {len(exp)} members", _describe_actual(act))
    return _member_set_diff(exp, act, path)


def _absent_from(source, other):
    return [n for n in source if n not in other]


def _member_set_diff(exp, act, path):
    missing, extra = _absent_from(exp, act), _absent_from(act, exp)
    if missing:
        return Diff(member_path(path, missing[0]), render(exp[missing[0]]), "no such member")
    return Diff(member_path(path, extra[0]), "no such member", render(act[extra[0]])) if extra else None


def _same_size_array(act, items):
    return kind_of(act) == "array" and len(act) == len(items)


def _fits_in(items, act, ctx, path):
    return lambda r, c: json_diff(items[r], act[c], ctx.trial(), f"{path}[{c}]") is None


def _diff_unordered(items, act, ctx, path):
    if not _same_size_array(act, items):
        return Diff(path, f"an array of {len(items)} in any order", _describe_actual(act))
    owner = rs_assign.assign(len(items), len(act), _fits_in(items, act, ctx, path))
    lost = rs_assign.unmatched_rows(len(items), owner)
    if lost:
        return Diff(path, f"an element matching {render(items[lost[0]])}", f"none of the {len(act)} elements")
    return _first_diff(((items[r], act[c], f"{path}[{c}]") for c, r in enumerate(owner)), ctx)


# ---- strings: plain, a variable, a field, a mix --------------------------------------------------------------------------------

def _bytes_of(text):
    return text.encode("utf-8", "surrogatepass")


def _bound_same(bound, act):
    if bound.json is not rf.MISSING:
        return rs_json.values_equal(bound.json, act)
    return isinstance(act, str) and _bytes_of(act) == bound.raw


def _bound_shown(bound):
    return render(bound.json) if bound.json is not rf.MISSING else render(bound.raw.decode("utf-8", "replace"))


def _diff_variable(name, act, ctx, path):
    bound = ctx.variables.get(name)
    if bound is None:
        return Diff(path, f'"${{{name}}}" (not bound)', render(act))
    return None if _bound_same(bound, act) else Diff(path, f'"${{{name}}}" = {_bound_shown(bound)}', render(act))


_NUMBER_FIELDS = ("int", "uint", "num")


def _number_ok(field, act):
    integral = act == act.to_integral_value()
    return {
        "int": lambda: integral,
        "uint": lambda: integral and act >= 0,
        "num": lambda: rf.within_tolerance(act, field.args[0], field.args[1]),
    }[field.kind]()


def _field_value_ok(field, act, ctx):
    if field.kind in _NUMBER_FIELDS:
        return kind_of(act) == "number" and _number_ok(field, act)
    return isinstance(act, str) and rf.field_accepts(field, _bytes_of(act), ctx.clock)


def _diff_field(field, act, ctx, path):
    if not _field_value_ok(field, act, ctx):
        return Diff(path, rf.field_text(field), render(act))
    if field.bind:
        raw = _bytes_of(act) if isinstance(act, str) else str(act).encode("ascii")
        ctx.binds.append((rf.bind_name(field.bind, ctx.variables), rf.Bound(raw, act)))
    return None


def _diff_mixed(parts, exp, act, ctx, path):
    resolved, error = rf.resolve_template(list(parts), ctx.variables)
    if error:
        return Diff(path, render(exp), error)
    if not isinstance(act, str):
        return Diff(path, render(exp), render(act))
    found = rf.match_template(resolved, _bytes_of(act), ctx.variables, ctx.clock)
    ctx.binds.extend(found.binds)
    return None if found.ok else Diff(path, render(exp), render(act))


def _plain_same(exp, act):
    return isinstance(act, str) and act == exp


def _diff_literal_or_mixed(parts, exp, act, ctx, path):
    if all(isinstance(p, Lit) for p in parts):
        return None if _plain_same(exp, act) else Diff(path, render(exp), render(act))
    return _diff_mixed(parts, exp, act, ctx, path)


_SINGLE = {Var: lambda part, act, ctx, path: _diff_variable(part.name, act, ctx, path), Field: lambda part, act, ctx, path: _diff_field(part, act, ctx, path)}


def _diff_string(exp, act, ctx, path):
    parts = _parts_of(exp)
    handler = _SINGLE.get(type(parts[0])) if len(parts) == 1 else None
    if handler is not None:
        return handler(parts[0], act, ctx, path)
    return _diff_literal_or_mixed(parts, exp, act, ctx, path)


_DIFFERS = {
    "null": _diff_leaf, "bool": _diff_leaf, "number": _diff_leaf, "string": _diff_string, "array": _diff_array, "object": _diff_object,
}


def json_diff(exp, act, ctx, path="$"):
    """The first difference between an expected JSON value and an actual one as a Diff, or None when they match."""
    return _DIFFERS[kind_of(exp)](exp, act, ctx, path)


def match_json_body(expected, body, variables, clock):
    """Parse the actual body (`bad-utf8` / `bad-json` when it cannot be read) and compare it with the expected value."""
    parsed = rs_json.parse(body)
    if not parsed.ok:
        return JsonResult(False, parsed.kind, parsed.message)
    ctx = Ctx(variables, clock or datetime.now(rf.UTC))
    diff = json_diff(expected, parsed.value, ctx)
    return JsonResult(diff is None, "", "", diff, tuple(ctx.binds))
