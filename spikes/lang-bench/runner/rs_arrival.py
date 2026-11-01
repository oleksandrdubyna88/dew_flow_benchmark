"""rs_arrival: the rule for bindings made inside an unordered group (FORMAT v2.1 5.4 and 5.5).

Which entry of an `unordered` group (or which element of a JSON `{"$unordered": [...]}` array) takes which observation depends on the
order they arrived in. A check that relates a binding of the group to anything outside it would pass or fail with the arrival order, so
the transcript parser refuses it, with file and line: `check equal` may not name such a binding, and `check distinct` may name all of
the group's bindings (the whole set is the same under every assignment) or none. The message names the binding and lists the group's.
"""
import itertools
import re
from typing import NamedTuple

import rs_lex as lex


class Binder(NamedTuple):
    head: str
    dynamic: bool
    text: str


class Group(NamedTuple):
    kind: str
    binders: tuple


def _template_text(template):
    return "".join(p.data.decode("ascii") if isinstance(p, lex.Lit) else "${" + p.name + "}" for p in template)


def binder_of(template):
    """The literal head of a bound name, whether the rest depends on a repeat variable, and the name as written."""
    literal = list(itertools.takewhile(lambda p: isinstance(p, lex.Lit), template))
    head = b"".join(p.data for p in literal).decode("ascii")
    return Binder(head, len(literal) < len(template), _template_text(template))


def _could_be(token, binder):
    """Could `token` (a name, or a prefix glob ending in `*`) stand for a name `binder` binds?"""
    if token.endswith("*"):
        prefix = token[:-1]
        return binder.head.startswith(prefix) or prefix.startswith(binder.head)
    return token.startswith(binder.head) if binder.dynamic else token == binder.head


def _covers(token, binder):
    """Does `token` stand for every name `binder` binds?"""
    return binder.head.startswith(token[:-1]) if token.endswith("*") else (not binder.dynamic and token == binder.head)


def _is_name(token):
    return token is not None and not re.match(r"-?[0-9]", token)


def _named(check):
    return [t for t in (*check.names, check.other) if _is_name(t)]


def _touching(check, group):
    """The names the check uses that stand for a binding of the group, in the order the check wrote them."""
    return [t for t in _named(check) if any(_could_be(t, b) for b in group.binders)]


def _left_out(check, group):
    """The bindings of the group that no name of the check stands for."""
    return [b for b in group.binders if not any(_covers(t, b) for t in check.names)]


def refuses(check, group):
    """True when the check relates a binding of the group to something else (unless it is a distinct over the whole group)."""
    whole = check.op == "distinct" and not _left_out(check, group)
    return bool(group.binders) and bool(_touching(check, group)) and not whole


def _ticked(items):
    return ", ".join(f"`{i}`" for i in items)


def _bindings(group):
    return ", ".join(b.text for b in group.binders)


REPAIR = "which entry binds it depends on the order the replies arrive in (name all of the group's bindings in one check distinct, or none)"


def message(check, group):
    """What is wrong with this check and this group, naming the binding and listing the group's."""
    names, listing = _ticked(_touching(check, group)), f"(the group binds {_bindings(group)})"
    if check.op == "equal":
        return f"check equal names {names}, a binding made inside an unordered {group.kind} {listing}: {REPAIR}"
    left = _ticked(b.text for b in _left_out(check, group))
    return f"check distinct names {names} of the bindings made inside an unordered {group.kind} {listing} but leaves out {left}: {REPAIR}"


def first_refusing(check, groups):
    """The first group the check may not name part of, or None."""
    return next((g for g in groups if refuses(check, g)), None)
