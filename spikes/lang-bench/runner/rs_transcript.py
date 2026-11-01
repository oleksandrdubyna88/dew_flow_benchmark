"""rs_transcript: the expected transcript (.out) of a daemon or web sample, parsed into frozen dataclasses (FORMAT v2 section 5), and
the writer of strings in the transcript's canonical form.

`parse_transcript(data, file)` returns `(transcript, problems)` like `rs_script.parse_script`: None whenever there is a problem,
`transcript 1` is a format error (V1), a repeated member name or NaN in an expected JSON body is a problem, and the relational
`check` lines of V6 are kept apart from the entries (a check produces no entry).
"""
import itertools
import re
from dataclasses import dataclass, replace
from datetime import datetime
from typing import Any, Optional

import rs_json
import rs_lex as lex
from rs_lex import NAME, Cursor, Problem, StrExpr, take_single_string, take_str_expr
from rs_script import take_choice, take_int, take_token

VERSION = 2
MAX_UNORDERED = 64
MAX_REPEAT = 100_000
GLOB = re.compile(r"[a-z][a-z0-9_]*\*?\Z")
_DONE = object()


@dataclass(frozen=True)
class Entry:
    kind: str
    conn: str
    line: int
    source: str
    expr: Optional[StrExpr] = None
    status: int = 0
    headers: tuple = ()
    body: Optional[tuple] = None
    loop: tuple = ()
    group: Optional[int] = None


@dataclass(frozen=True)
class Unordered:
    line: int
    entries: tuple


@dataclass(frozen=True)
class TRepeat:
    line: int
    count: int
    name: str
    body: tuple


@dataclass(frozen=True)
class Check:
    line: int
    op: str
    names: tuple
    other: Optional[str] = None


@dataclass(frozen=True)
class Transcript:
    file: str
    nodes: tuple


@dataclass
class _Frame:
    kind: str
    line: int
    extra: Any
    items: list


# ---- canonical text of a string -----------------------------------------------------------------------------------------------

_ESCAPES = {0x22: '\\"', 0x5C: "\\\\", 0x0D: "\\r", 0x0A: "\\n", 0x09: "\\t", 0x00: "\\0"}
_CANONICAL = {**{b: chr(b) for b in range(0x20, 0x7F)}, **_ESCAPES}


def canonical(data):
    """A byte string as the runner writes it (5.1): printable ASCII as it is, `\\"` `\\\\` `\\r` `\\n` `\\t` `\\0`, `\\$` before a
    brace, every other byte `\\xhh` in lower case."""
    text = "".join(_CANONICAL.get(b) or f"\\x{b:02x}" for b in data)
    return '"' + text.replace("${", "\\${") + '"'


def abbreviate(data, limit=120):
    """`canonical(data)`, or for a string over `limit` bytes its first 60 and last 40 bytes with the count left out between (5.5)."""
    if len(data) <= limit:
        return canonical(data)
    return f"{canonical(data[:60])}...({len(data) - 100} bytes)...{canonical(data[-40:])}"


# ---- what the runner observed ---------------------------------------------------------------------------------------------------

@dataclass(frozen=True)
class Actual:
    """One observation: the kinds of an expected entry, and the actual-only kinds of 5.1 (`exit-signal` is the entry `exit signal NAME`).
    `headers` holds every occurrence as (name, value) bytes in arrival order; `at` is the UTC time the entry was recorded."""
    kind: str
    conn: str = ""
    data: bytes = b""
    status: int = 0
    headers: tuple = ()
    body: bytes = b""
    name: str = ""
    at: Optional[datetime] = None


_WITH_DATA = lambda a: f"{a.kind} {a.conn} {abbreviate(a.data)}"
_ACTUAL_TEXT = {
    "ready": lambda a: "ready", "recv": lambda a: f"recv {a.conn} {abbreviate(a.data)}", "reply": lambda a: f"reply {a.conn} {a.status}",
    "quiet": lambda a: f"quiet {a.conn}", "closed": lambda a: f"closed {a.conn}", "exit": lambda a: f"exit {a.status}",
    "not-ready": lambda a: "not-ready", "refused": lambda a: f"refused {a.conn}", "timeout": _WITH_DATA, "eof": _WITH_DATA,
    "noise": _WITH_DATA, "bad-reply": _WITH_DATA, "open": lambda a: f"open {a.conn}", "early-exit": lambda a: f"early-exit {a.status}",
    "exit-timeout": lambda a: "exit-timeout", "exit-signal": lambda a: f"exit signal {a.name}",
    "capture-failed": lambda a: f"capture-failed {a.name}", "overflow": lambda a: f"overflow {a.conn}",
}


def render_actual(actual):
    """The actual transcript's line for an observation (5.1)."""
    return _ACTUAL_TEXT[actual.kind](actual)


# ---- the expected JSON body ---------------------------------------------------------------------------------------------------

def _dollar_unordered_problem(node):
    if "$unordered" not in node:
        return None
    ok = list(node) == ["$unordered"] and isinstance(node["$unordered"], list)
    return None if ok else "the member name $unordered stands alone and holds an array"


def _node_problem(node):
    if isinstance(node, dict):
        return _dollar_unordered_problem(node)
    if isinstance(node, str) and lex.template_parts(node) is None:
        return f"a malformed ${{...}} in the JSON string {rs_json.render(node)}"
    return None


def json_problem(value):
    """The first problem in an expected JSON value (the reserved member name, a malformed template), or None."""
    stack = [value]
    while stack:
        node = stack.pop()
        found = _node_problem(node)
        if found:
            return found
        stack.extend(rs_json.children_of(node))
    return None


def _unusable(parsed):
    return json_problem(parsed.value) if parsed.ok else parsed.message


def _json_expected(cur):
    cur.take()
    text = cur.rest()
    parsed = rs_json.parse(text)
    problem = _unusable(parsed)
    if problem:
        return cur.fail(f"the expected body is not usable JSON: {problem}")
    return ("json", parsed.value, text)


def _empty_body(cur):
    cur.take()
    return ("empty",)


def _str_body(cur):
    expr = take_str_expr(cur)
    return None if expr is None else ("str", expr)


_BODY_FORMS = {"empty": _empty_body, "json": _json_expected}


def _read_body(cur):
    nxt = cur.peek()
    return _BODY_FORMS.get(None if nxt is None else nxt.text, _str_body)(cur)


# ---- statements ---------------------------------------------------------------------------------------------------------------

def _is_conn(tok):
    return tok is not None and (tok.text == "*" or (tok.kind == "word" and bool(NAME.match(tok.text))))


def take_entry_conn(cur):
    tok = cur.take()
    return tok.text if _is_conn(tok) else cur.fail("a connection name or * is expected")


def _entry(kind, conn, cur, **more):
    return Entry(kind, conn, cur.lineno, cur.text.strip(), **more)


def _t_ready(cur, parser):
    return _entry("ready", "", cur)


def _t_recv(cur, parser):
    conn = take_entry_conn(cur)
    expr = None if conn is None else take_str_expr(cur)
    return None if expr is None else _entry("recv", conn, cur, expr=expr)


def _t_conn_only(kind):
    def handler(cur, parser):
        conn = take_entry_conn(cur)
        return None if conn is None else _entry(kind, conn, cur)
    return handler


def _t_exit(cur, parser):
    status = take_token(cur, "int", "an exit status of 0 to 255", lambda n: n <= 255)
    return None if status is None else _entry("exit", "", cur, status=status)


def _t_reply(cur, parser):
    conn = take_entry_conn(cur)
    status = None if conn is None else take_token(cur, "int", "a status code of 100 to 599", lambda n: 100 <= n <= 599)
    if status is None:
        return None
    parser.pending = {"kind": "reply", "conn": conn, "status": status, "headers": [], "body": None, "line": cur.lineno, "source": cur.text.strip()}
    return _DONE


def _t_header(cur, parser):
    if parser.pending is None:
        return cur.fail("a header line belongs to a reply")
    name = take_single_string(cur)
    value = None if name is None else take_str_expr(cur)
    if value is None:
        return None
    parser.pending["headers"].append((name, value))
    return _DONE


def _t_body(cur, parser):
    if parser.pending is None or parser.pending["body"] is not None:
        return cur.fail("a body line belongs to a reply and a reply has exactly one")
    body = _read_body(cur)
    if body is None:
        return None
    parser.pending["body"] = body
    return _DONE


def _t_unordered(cur, parser):
    if parser.top().kind == "unordered":
        return cur.fail("unordered groups do not nest")
    parser.stack.append(_Frame("unordered", cur.lineno, None, []))
    return _DONE


def _t_repeat(cur, parser):
    count = take_token(cur, "int", f"a repeat count of 1 to {MAX_REPEAT}", lambda n: 1 <= n <= MAX_REPEAT)
    name = None if count is None else lex_name(cur)
    if name is None:
        return None
    if parser.top().kind == "unordered":
        return cur.fail("an unordered group holds no repeat")
    parser.stack.append(_Frame("repeat", cur.lineno, (count, name), []))
    return _DONE


def lex_name(cur):
    return take_token(cur, "word", "a name [a-z][a-z0-9_]* that is not a keyword", lambda w: NAME.match(w) and w not in lex.KEYWORDS)


def _t_end(cur, parser):
    if len(parser.stack) == 1:
        return cur.fail("'end' closes nothing")
    frame = parser.stack.pop()
    if frame.kind == "unordered":
        return _unordered_node(cur, frame)
    return TRepeat(frame.line, *frame.extra, tuple(frame.items))


def _unordered_node(cur, frame):
    if not 2 <= len(frame.items) <= MAX_UNORDERED:
        return cur.fail(f"an unordered group holds 2 to {MAX_UNORDERED} entries")
    return Unordered(frame.line, tuple(frame.items))


def _name_token(cur, pattern, what):
    tok = cur.take()
    if tok is None or not pattern.match(tok.text):
        return cur.fail(f"{what} is expected")
    return tok.text


def _check_distinct(cur):
    names = []
    while not cur.at_end():
        name = _name_token(cur, GLOB, "a bound name or a prefix glob such as r*")
        if name is None:
            return None
        names.append(name)
    return (tuple(names), None) if names else cur.fail("check distinct needs at least one name")


def _check_equal(cur):
    name = _name_token(cur, NAME, "a bound name")
    other = None if name is None else _name_token(cur, re.compile(r"-?[0-9]+(\.[0-9]+)?\Z|[a-z][a-z0-9_]*\Z"), "a decimal or a bound name")
    return None if other is None else ((name,), other)


_CHECKS = {"distinct": _check_distinct, "equal": _check_equal}


def _t_check(cur, parser):
    if parser.top().kind == "unordered":
        return cur.fail("an unordered group holds entries only")
    op = take_choice(tuple(_CHECKS))(cur)
    result = None if op is None else _CHECKS[op](cur)
    return None if result is None else Check(cur.lineno, op, *result)


def _t_version(cur, parser):
    version = take_int(cur)
    if version is None:
        return None
    if version != VERSION:
        parser.stopped = True
        return cur.fail(f"format version {version}: this runner reads 'transcript {VERSION}' (a version 1 file is re-conformed, never run)")
    parser.started, parser.table = True, _HANDLERS
    return _DONE


_HANDLERS = {
    "ready": _t_ready, "recv": _t_recv, "reply": _t_reply, "header": _t_header, "body": _t_body,
    "quiet": _t_conn_only("quiet"), "closed": _t_conn_only("closed"), "exit": _t_exit,
    "unordered": _t_unordered, "repeat": _t_repeat, "end": _t_end, "check": _t_check,
}
_CONTINUATION = ("header", "body")


def _not_allowed(word, started):
    return f"'{word}' is not allowed here" + ("" if started else " (the first statement is 'transcript 2')")


def _star_problem(node, kind):
    star = isinstance(node, Entry) and node.conn == "*" and kind != "unordered"
    return "the connection * is allowed only inside an unordered group" if star else None


def _group_problem(node, kind):
    return "an unordered group holds entries only" if kind == "unordered" and not isinstance(node, Entry) else None


class _Parser:
    def __init__(self, file, problems):
        self.file, self.problems = file, problems
        self.started, self.stopped, self.last_line = False, False, 0
        self.table = {"transcript": _t_version}
        self.stack, self.pending = [_Frame("top", 0, None, [])], None

    def top(self):
        return self.stack[-1]

    def statement(self, lineno, text):
        mark = len(self.problems)
        cur = Cursor(text, self.file, lineno, self.problems, allow_fields=True)
        word = cur.take()
        if word is None or word.text not in _CONTINUATION:
            self.flush()
        handler = self._handler(word, cur)
        if handler is not None:
            self._run(handler, cur)
        self.stopped = len(self.problems) > mark

    def _handler(self, word, cur):
        if word is None or word.kind != "word":
            return cur.fail("a statement keyword is expected")
        return self.table.get(word.text) or cur.fail(_not_allowed(word.text, self.started))

    def _run(self, handler, cur):
        result = handler(cur, self)
        if result is None:
            return
        if not cur.at_end():
            cur.fail(f"unexpected '{cur.rest().split(' ')[0]}'")
        elif result is not _DONE:
            self.add(result, cur)

    def add(self, node, cur):
        kind = self.top().kind
        problem = _star_problem(node, kind) or _group_problem(node, kind)
        if problem:
            cur.fail(problem)
            return
        self.top().items.append(node)

    def flush(self):
        if self.pending is None:
            return
        pending, self.pending = self.pending, None
        if pending["body"] is None:
            self.problems.extend([] if self.problems else [Problem(self.file, pending["line"], "a reply needs exactly one body line")])
            return
        entry = Entry("reply", pending["conn"], pending["line"], pending["source"], status=pending["status"],
                      headers=tuple(pending["headers"]), body=pending["body"])
        self.top().items.append(entry)

    def _unfinished(self):
        if self.problems:
            return None
        if len(self.stack) > 1:
            return Problem(self.file, self.top().line, f"the {self.top().kind} group is not closed by 'end'")
        return None if self.started else Problem(self.file, max(1, self.last_line), "the first statement is 'transcript 2'")

    def finish(self):
        self.flush()
        gap = self._unfinished()
        if gap is not None:
            self.problems.append(gap)
        return None if self.problems else Transcript(self.file, tuple(self.top().items))


def _feed(parser, lines):
    for lineno, text in lines:
        if parser.stopped:
            return
        parser.last_line = lineno
        if not lex.is_blank_or_comment(text):
            parser.statement(lineno, text)


def parse_transcript(data, file):
    problems = []
    parser = _Parser(file, problems)
    _feed(parser, lex.split_lines(data, file, problems))
    transcript = parser.finish()
    problems.extend([] if transcript is None else _binder_problems(transcript))
    return (None if problems else transcript), problems


# ---- walking a parsed transcript ------------------------------------------------------------------------------------------------

def _children(node):
    return getattr(node, "entries", None) or getattr(node, "body", None) or ()


def walk(nodes, scope=()):
    """Every Entry of the tree in file order as (entry, the repeat variable names in scope), repeats not multiplied."""
    for node in nodes:
        if isinstance(node, Entry):
            yield node, scope
        else:
            yield from walk(_children(node), scope + ((node.name,) if isinstance(node, TRepeat) else ()))


def checks_of(transcript):
    """The relational checks in file order."""
    return list(_checks(transcript.nodes))


def _checks(nodes):
    for node in nodes:
        if isinstance(node, Check):
            yield node
        elif isinstance(node, TRepeat):
            yield from _checks(node.body)


def entry_count(transcript):
    return sum(_count(n) for n in transcript.nodes)


def _count(node):
    if isinstance(node, Entry):
        return 1
    multiplier = node.count if isinstance(node, TRepeat) else 1
    return multiplier * sum(_count(n) for n in _children(node))


def slot_sizes(entries):
    """How many consecutive expanded entries each slot holds: one for an ordered entry, the whole run for an unordered group."""
    sizes = []
    for key, run in itertools.groupby(entries, key=lambda e: e.group):
        n = sum(1 for _ in run)
        sizes.extend([1] * n if key is None else [n])
    return sizes


def expand(transcript):
    """Every entry in order with repeats expanded; an entry carries its `loop` bindings and the `group` id of its unordered group."""
    return list(_expand(transcript.nodes, (), itertools.count()))


def _expand(nodes, loop, groups):
    for node in nodes:
        if isinstance(node, Entry):
            yield replace(node, loop=loop)
        else:
            yield from _EXPANDERS.get(type(node), lambda *_: ())(node, loop, groups)


def _expand_group(node, loop, groups):
    gid = next(groups)
    yield from (replace(e, loop=loop, group=gid) for e in node.entries)


def _expand_repeat(node, loop, groups):
    for i in range(node.count):
        yield from _expand(node.body, loop + ((node.name, i),), groups)


_EXPANDERS = {Unordered: _expand_group, TRepeat: _expand_repeat}


# ---- what an entry binds and uses -----------------------------------------------------------------------------------------------

def _json_templates(value):
    """The parts of every string of an expected JSON value (member names excluded)."""
    stack = [value]
    while stack:
        node = stack.pop()
        if isinstance(node, str):
            yield lex.template_parts(node) or []
        stack.extend(rs_json.children_of(node))


def _expr_parts(expr):
    return [p for item in expr.items for p in item.parts]


def _body_form(entry, form):
    return entry.body[1] if entry.body and entry.body[0] == form else None


def _exprs_of(entry):
    candidates = [entry.expr, _body_form(entry, "str")] + [value for _, value in entry.headers]
    return [e for e in candidates if e is not None]


def parts_of(entry):
    """Every list of parts (Lit, Var, Field) an entry's expected strings hold."""
    lists = [_expr_parts(e) for e in _exprs_of(entry)]
    value = _body_form(entry, "json")
    return lists + ([] if value is None else list(_json_templates(value)))


def _parts(transcript):
    for entry, scope in walk(transcript.nodes):
        for parts in parts_of(entry):
            for part in parts:
                yield entry, scope, part


def binders(transcript):
    """(name template parts, line, repeat depth) of every field that binds what it matched."""
    return [(p.bind, e.line, len(s)) for e, s, p in _parts(transcript) if isinstance(p, lex.Field) and p.bind]


def uses(transcript):
    """(variable name, line, repeat variables in scope) of every `${name}` in an expected string."""
    return [(p.name, e.line, s) for e, s, p in _parts(transcript) if isinstance(p, lex.Var)]


def static_name(template):
    """The name of a bind template without a variable, else None."""
    return None if any(isinstance(p, lex.Var) for p in template) else b"".join(p.data for p in template).decode("ascii")


def _rebinds(name, seen, depth):
    return name is not None and (name in seen or depth > 0)


def _binder_problems(transcript):
    problems, seen = [], set()
    for template, line, depth in binders(transcript):
        name = static_name(template)
        if _rebinds(name, seen, depth):
            problems.append(Problem(transcript.file, line, f"the name '{name}' is bound more than once (a name with ${{var}} binds once per round)"))
        seen.add(name)
    return problems
