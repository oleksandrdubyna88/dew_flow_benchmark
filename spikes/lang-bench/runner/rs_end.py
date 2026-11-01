"""rs_end: the end of a scenario script (FORMAT v2.1 4.4).

A scenario has exactly one `wait-exit`, always with its `within` (the grace period G), and before it the end trigger: `signal TERM`, or
`close stdin`, whichever comes first. Between the trigger and the `wait-exit`, and after the `wait-exit`, only `recv`, `expect`, `sleep` and
`close` may stand: the steps that read what the program wrote or wait. Nothing more is sent. The trigger and the wait-exit stand at the top
level of the script (not inside a `repeat` or a `together`). Part ii runs these steps in order against the bytes the program wrote.
"""
from rs_lex import Problem
from rs_model import Repeat, Step

AFTER_OPS = ("recv", "quiet", "closed", "sleep", "close")


def _flat(nodes):
    """Every node of a list with the bodies of repeats and the steps of groups inlined, depth first."""
    for node in nodes:
        yield node
        if isinstance(node, Repeat):
            yield from _flat(node.body)
        elif hasattr(node, "steps"):
            yield from _flat(node.steps)


def _is_wait(node):
    return isinstance(node, Step) and node.op == "wait_exit"


def is_trigger(node):
    """`signal TERM` and `close stdin` are the two end triggers."""
    return isinstance(node, Step) and (node.op == "signal" or (node.op == "close" and node.conn == "stdin"))


def _name(node):
    return node.op if isinstance(node, Step) else type(node).__name__.lower()


def _reads_or_waits(node):
    return isinstance(node, Step) and node.op in AFTER_OPS


def _refused(file, node, where):
    allowed = "only recv, expect, sleep and close may stand there: they read what the program wrote or wait, and nothing more is sent"
    return Problem(file, node.line, f"{_name(node)} may not stand {where}: {allowed}")


def _leaves(nodes):
    return [n for n in _flat(nodes) if not isinstance(n, Repeat)]


def _outside(file, nodes, where):
    return [_refused(file, n, where) for n in _leaves(nodes) if not _reads_or_waits(n)]


def _misplaced(file, node):
    return Problem(file, node.line, f"{_name(node)} stands at the top level of the script, not inside a repeat or a together group")


def _marks_end(node):
    return _is_wait(node) or is_trigger(node)


def _at_top(body, node):
    return any(n is node for n in body)


def _nested(file, body):
    """The end trigger and the wait-exit anywhere but the top level of the body."""
    return [_misplaced(file, n) for n in _flat(body) if _marks_end(n) and not _at_top(body, n)]


def _wait_problems(file, waits):
    many = [Problem(file, w.line, "a scenario has exactly one wait-exit") for w in waits[1:]]
    return many + [Problem(file, w.line, "wait-exit takes its within: the task's grace period") for w in waits if "within" not in w.args]


def _position_problems(file, body, wait):
    """The trigger comes before the wait-exit; only reads and waits stand between them and after the wait-exit."""
    index = body.index(wait)
    triggers = [i for i, n in enumerate(body[:index]) if is_trigger(n)]
    if not triggers:
        return [Problem(file, wait.line, "wait-exit needs the end trigger (signal TERM or close stdin) before it")]
    between = _outside(file, body[triggers[0] + 1:index], "between the end trigger and wait-exit")
    return between + _outside(file, body[index + 1:], "after wait-exit")


def check_end(scenario):
    """Problems of how the scenario ends (see the module doc)."""
    file, body = scenario.file, scenario.body
    waits = [n for n in _flat(body) if _is_wait(n)]
    if not waits:
        return [Problem(file, scenario.start.line, "a scenario has exactly one wait-exit, with its within, at the top level")]
    return _wait_problems(file, waits) + _nested(file, body) + _positions(file, body, waits)


def _positions(file, body, waits):
    """The order rules, judged only for the one well-placed wait-exit (the others are problems of their own)."""
    return _position_problems(file, body, waits[0]) if len(waits) == 1 and _at_top(body, waits[0]) else []
