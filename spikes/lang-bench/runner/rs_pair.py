"""rs_pair: a scenario script and its expected transcript checked together (FORMAT v2 section 7, item 6): the transcript's entries
correspond one to one, after expansion, to the entries the script produces, and every variable a transcript string uses is bound."""
import re

import rs_assign
import rs_lex as lex
from rs_check import Produced  # noqa: F401
from rs_lex import Problem
from rs_script import produced_entries
from rs_transcript import binders, checks_of, expand, slot_sizes, static_name, uses


def _fits(produced, entry):
    return produced.kind == entry.kind and entry.conn in (produced.conn, "*")


def _slot_problem(file, produced, entries):
    owner = rs_assign.assign(len(entries), len(produced), lambda r, c: _fits(produced[c], entries[r]))
    lost = rs_assign.unmatched_rows(len(entries), owner)
    if not lost:
        return None
    e = entries[lost[0]]
    return Problem(file, e.line, f"the script produces nothing for '{e.source}' here (it produces {', '.join(p.kind + ' ' + p.conn for p in produced)})")


def _correspondence(scenario, transcript):
    produced, entries = list(produced_entries(scenario)), expand(transcript)
    if len(produced) != len(entries):
        return [Problem(transcript.file, 1, f"the transcript holds {len(entries)} entries but the script produces {len(produced)}")]
    i = 0
    for n in slot_sizes(entries):
        problem = _slot_problem(transcript.file, produced[i:i + n], entries[i:i + n])
        if problem is not None:
            return [problem]
        i += n
    return []


# ---- names ------------------------------------------------------------------------------------------------------------------

def _script_names(scenario):
    names = {s.name for s in scenario.setup if s.kind == "file"}
    return names | {c.name for c in _captures(scenario.body)}


def _captures(nodes):
    for node in nodes:
        if hasattr(node, "body"):
            yield from _captures(node.body)
        elif hasattr(node, "steps"):
            yield from _captures(node.steps)
        else:
            yield from node.args.get("capture", ())


class _Bound:
    """The names a transcript may use so far: the script's, the static binders met, and the literal heads of dynamic ones."""

    def __init__(self, script_names):
        self.names, self.heads = set(script_names), []

    def has(self, name):
        return name in self.names or any(name.startswith(h) for h in self.heads)

    def has_prefix(self, prefix):
        return any(n.startswith(prefix) for n in self.names) or any(_overlap(h, prefix) for h in self.heads)

    def add(self, template):
        name = static_name(template)
        if name is not None:
            self.names.add(name)
        else:
            self.heads.append(_head(template))


def _overlap(a, b):
    return a.startswith(b) or b.startswith(a)


def _head(template):
    return template[0].data.decode("ascii") if isinstance(template[0], lex.Lit) else ""


def _use_problems(file, transcript, bound):
    return [Problem(file, line, f"variable '{name}' is used before anything binds it") for name, line, scope in uses(transcript)
            if name not in scope and not bound.has(name)]


def _binder_problems(file, scenario_names, transcript):
    found = [(static_name(t), line) for t, line, _ in binders(transcript)]
    return [Problem(file, line, f"the name '{name}' is already a variable of the script") for name, line in found if name in scenario_names]


def _wanted(check):
    other = [] if check.other is None or re.match(r"-?[0-9]", check.other) else [check.other]
    return list(check.names) + other


def _known(name, bound):
    return bound.has_prefix(name[:-1]) if name.endswith("*") else bound.has(name)


def _check_problems(file, transcript, bound):
    return [Problem(file, c.line, f"check names '{n}', which nothing binds") for c in checks_of(transcript) for n in _wanted(c) if not _known(n, bound)]


def _name_problems(scenario, transcript):
    script_names = _script_names(scenario)
    bound = _Bound(script_names)
    for template, _, _ in binders(transcript):
        bound.add(template)
    problems = _binder_problems(transcript.file, script_names, transcript)
    problems += _use_problems(transcript.file, transcript, bound)
    return problems + _check_problems(transcript.file, transcript, bound)


def validate_pair(scenario, transcript):
    """Problems of a script and its transcript taken together, or []."""
    return _correspondence(scenario, transcript) + _name_problems(scenario, transcript)
