"""rs_check: the validation of a parsed scenario script (names bound once and before use, connections, split offsets, the last
statement) and the entries a script produces (FORMAT v2 5.1), for the one-to-one correspondence with a transcript."""
import re

import rs_lex as lex
from rs_lex import Problem
from rs_model import BUILT_IN_CONNECTIONS, MAX_EXPANDED_STEPS, Produced, Repeat, Step, Together

JSON_VARIABLE = re.compile(r"\$\{([a-z][a-z0-9_]*)\}")


def expanded_steps(node):
    """How many steps a node runs once its repeats are expanded."""
    if isinstance(node, Repeat):
        return node.count * sum(expanded_steps(n) for n in node.body)
    return len(node.steps) if isinstance(node, Together) else 1


def _flat_parts(expr):
    for item in expr.items:
        for part in item.parts:
            yield part, item.count


def static_length(expr):
    """The joined length in bytes of a string expression when it holds no variable, else None (V9)."""
    parts = list(_flat_parts(expr))
    if not all(isinstance(p, lex.Lit) for p, _ in parts):
        return None
    return sum(len(p.data) * count for p, count in parts)


# ---- validation of a parsed scenario: names, connections, offsets, the last statement -------------------------------------------

def check_scenario(scenario):
    return _Checker(scenario).run()


class _Checker:
    def __init__(self, scenario):
        self.s, self.problems = scenario, []
        self.conns = {name: name for name in BUILT_IN_CONNECTIONS}
        self.bound, self.in_repeat = {}, 0

    def problem(self, line, message):
        self.problems.append(Problem(self.s.file, line, message))

    def run(self):
        for setup in self.s.setup:
            self.use(setup.value, setup.line)
            if setup.kind == "file":
                self.bind(setup.name, "file", setup.line)
        self.nodes(self.s.body)
        self.ending()
        return self.problems

    def ending(self):
        last = self.s.body[-1] if self.s.body else None
        if not (isinstance(last, Step) and last.op == "wait_exit"):
            self.problem(self.s.start.line, "the last statement of a scenario is wait-exit")

    def use(self, expr, line):
        for name in lex.vars_of(expr):
            self.use_name(name, line)

    def use_name(self, name, line):
        if name not in self.bound:
            self.problem(line, f"variable '{name}' is used before it is bound")

    def bind(self, name, kind, line):
        if name in self.bound:
            self.problem(line, f"'{name}' is bound twice")
        self.bound[name] = kind

    def nodes(self, nodes):
        for node in nodes:
            if isinstance(node, Repeat):
                self.repeat(node)
            elif isinstance(node, Together):
                self.nodes(node.steps)
            else:
                _STEP_CHECKS[node.op](self, node)

    def repeat(self, node):
        self.bind(node.name, "repeat", node.line)
        self.in_repeat += 1
        self.nodes(node.body)
        self.in_repeat -= 1
        self.bound.pop(node.name, None)

    def kind_of(self, step, allowed, what):
        """The connection kind of `step.conn` when it is one of `allowed`, else None after a problem."""
        kind = self.conns.get(step.conn)
        if kind is None:
            self.problem(step.line, f"connection '{step.conn}' is not connected")
        elif kind not in allowed:
            self.problem(step.line, f"{what} does not apply to the {kind} connection '{step.conn}'")
        return kind if kind in allowed else None


def _c_connect(c, step):
    if step.conn in c.conns:
        c.problem(step.line, f"connection '{step.conn}' is already named")
    c.conns[step.conn] = step.args["kind"]


def _c_send(c, step):
    if c.kind_of(step, ("tcp", "stdin"), "send") is None:
        return
    c.use(step.args["data"], step.line)
    _check_offsets(c, step)


def _inside(offset, length):
    return 0 < offset and (length is None or offset < length)


def _offsets_valid(offsets, length):
    return list(offsets) == sorted(set(offsets)) and all(_inside(o, length) for o in offsets)


def _check_offsets(c, step):
    length = static_length(step.args["data"])
    if not _offsets_valid(step.args.get("split", ()), length):
        c.problem(step.line, f"split offsets must be strictly increasing and inside the joined bytes (0 < offset < {length})")


def _c_request(c, step):
    if c.kind_of(step, ("http",), "request") is None:
        return
    c.use(step.args["target"], step.line)
    for name, value in step.args["headers"]:
        c.use(name, step.line)
        c.use(value, step.line)
    _check_body(c, step)


def _check_body(c, step):
    body = step.args["body"]
    if body is None:
        return
    if body[0] == "str":
        c.use(body[1], step.line)
        return
    for name in JSON_VARIABLE.findall(body[1]):
        c.use_name(name, step.line)


def _c_recv(c, step):
    kind = c.kind_of(step, ("tcp", "http", "stdout"), "recv")
    if kind is None:
        return
    if (kind == "http") != (step.args["mode"] == ()):
        c.problem(step.line, "recv on an http connection takes no mode; on tcp or stdout it needs one")
    for item in step.args.get("capture", ()):
        _check_capture(c, step, kind, item)


def _check_capture(c, step, kind, item):
    if item.how == "regex":
        c.use(item.source, step.line)
    elif kind != "http":
        c.problem(step.line, "a json capture needs an http reply")
    if c.in_repeat:
        c.problem(step.line, f"capture '{item.name}' would be bound every round of a repeat")
    c.bind(item.name, "capture", step.line)


def _c_quiet(c, step):
    c.kind_of(step, ("tcp", "http", "stdout"), "expect quiet")


def _c_closed(c, step):
    c.kind_of(step, ("tcp", "http"), "expect closed")


def _c_close(c, step):
    c.kind_of(step, ("tcp", "http", "stdin"), "close")


def _c_shutdown(c, step):
    c.kind_of(step, ("tcp",), "shutdown")


def _c_none(c, step):
    return None


_STEP_CHECKS = {
    "connect": _c_connect, "send": _c_send, "request": _c_request, "recv": _c_recv, "quiet": _c_quiet, "closed": _c_closed,
    "close": _c_close, "shutdown": _c_shutdown, "sleep": _c_none, "signal": _c_none, "wait_exit": _c_none,
}


# ---- the entries a script produces (FORMAT v2 5.1), for the one-to-one correspondence with a transcript ---------------------------

def produced_entries(scenario):
    """Every entry the script produces, in order, repeats expanded: `Produced(kind, conn)`."""
    yield Produced("ready", "")
    yield from _produced_nodes(scenario.body, {})


def _produced_nodes(nodes, kinds):
    for node in nodes:
        yield from _produced_node(node, kinds)


def _produced_node(node, kinds):
    if isinstance(node, Repeat):
        for _ in range(node.count):
            yield from _produced_nodes(node.body, kinds)
    elif isinstance(node, Step):
        yield from _produced_step(node, kinds)


def _p_connect(step, kinds):
    kinds[step.conn] = step.args["kind"]
    return ()


def _p_recv(step, kinds):
    if kinds.get(step.conn) == "http":
        return [Produced("reply", step.conn)]
    mode = step.args["mode"]
    return [Produced("recv", step.conn)] * (mode[1] if mode[0] == "lines" else 1)


_PRODUCERS = {
    "connect": _p_connect, "recv": _p_recv,
    "quiet": lambda step, kinds: [Produced("quiet", step.conn)],
    "closed": lambda step, kinds: [Produced("closed", step.conn)],
    "wait_exit": lambda step, kinds: [Produced("exit", "")],
}


def _produced_step(step, kinds):
    producer = _PRODUCERS.get(step.op)
    if producer is not None:
        yield from producer(step, kinds)
