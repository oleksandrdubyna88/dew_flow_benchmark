"""rs_end: the end of a scenario script (FORMAT v2.1 4.4): the wait-exit, and what may stand after it.

After the exit only the steps that read what the program wrote, or wait, may stand: `recv`, `expect`, `sleep` and `close`. Nothing more is
sent to the program. (Part ii runs them in order against the bytes the program wrote before it exited.)
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


def _wait_index(body):
    return next((i for i, n in enumerate(body) if isinstance(n, Step) and n.op == "wait_exit"), None)


def _refused_after(file, node):
    if isinstance(node, Step) and node.op in AFTER_OPS:
        return None
    what = node.op if isinstance(node, Step) else type(node).__name__.lower()
    return Problem(file, node.line, f"{what} may not stand after wait-exit: only recv, expect, sleep and close read or wait (nothing more is sent)")


def _after_problems(file, nodes):
    leaves = [n for n in _flat(nodes) if not isinstance(n, Repeat)]
    return list(filter(None, (_refused_after(file, n) for n in leaves)))


def check_end(scenario):
    """Problems of how the scenario ends: a top-level wait-exit, and only reads and waits after it."""
    index = _wait_index(scenario.body)
    if index is None:
        return [Problem(scenario.file, scenario.start.line, "a scenario ends with wait-exit within <duration> at its top level")]
    return _after_problems(scenario.file, scenario.body[index + 1:])
