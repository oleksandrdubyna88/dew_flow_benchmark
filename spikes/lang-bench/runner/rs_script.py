"""rs_script: the scenario script (.in) of a daemon or web sample, parsed into frozen dataclasses and validated (FORMAT v2 section 4).

`parse_script(data, file)` returns `(scenario, problems)`: `scenario` is None whenever `problems` (a list of `Problem(file, line,
message)`) is not empty, and nothing is raised for a malformed sample. Version 1 files are a format error, never run (V1); `sleep`
has no deadline (V8); a `split` offset counts the joined bytes of the whole string expression (V9).
"""
import re
from dataclasses import dataclass
from typing import Any

import rs_json
import rs_lex as lex
from rs_check import check_scenario, expanded_steps, produced_entries, static_length  # noqa: F401  (re-exported)
from rs_lex import KEYWORDS, NAME, Cursor, Problem, take_single_string, take_str_expr
from rs_model import (BUILT_IN_CONNECTIONS, MAX_EXPANDED_STEPS, MAX_REPEAT, VERSION, Capture, Produced, Repeat, Scenario,  # noqa: F401
                      Setup, Start, Step, Together)

ENV_NAME = re.compile(r"[A-Z_][A-Z0-9_]*\Z")
METHOD = re.compile(r"[A-Z]+\Z")
CAPTURE_TYPES = lex.FIELD_KINDS
_DONE = object()


# ---- readers: each takes a Cursor and returns a value, or None after recording a problem ----------------------------------

def _always(value):
    return True


def _acceptable(tok, kind, accept):
    if tok is None or tok.kind != kind or tok.value is None:
        return False
    return accept(tok.value)


def _take(cur, kind, what, accept=_always):
    tok = cur.take()
    if not _acceptable(tok, kind, accept):
        return cur.fail(f"{what} is expected")
    return tok.value


def take_dur(cur):
    return _take(cur, "dur", "a duration of at most 600s")


def take_int(cur):
    return _take(cur, "int", "an integer")


def take_positive(cur):
    return _take(cur, "int", "a positive integer", lambda n: n >= 1)


def take_path(cur):
    return _take(cur, "path", "a JSON path ($.member[index])")


_OFFSETS = {"int": lambda value: (value,), "list": lambda value: value}


def take_offsets(cur):
    tok = cur.take()
    convert = None if tok is None else _OFFSETS.get(tok.kind)
    return convert(tok.value) if convert else cur.fail("byte offsets (1 or 1,9) are expected")


def take_name(cur):
    return _take(cur, "word", "a name [a-z][a-z0-9_]* that is not a keyword", lambda w: NAME.match(w) and w not in KEYWORDS)


def take_conn(cur):
    return _take(cur, "word", "a connection name", lambda w: NAME.match(w) and (w not in KEYWORDS or w in BUILT_IN_CONNECTIONS))


def take_env_name(cur):
    return _take(cur, "word", "an environment variable name [A-Z_][A-Z0-9_]*", ENV_NAME.match)


def take_method(cur):
    return _take(cur, "word", "an HTTP method in capital letters", METHOD.match)


def take_choice(choices):
    return lambda cur: _take(cur, "word", "one of " + ", ".join(choices), lambda w: w in choices)


def _refuse_within(cur):
    return cur.fail("sleep has no deadline: it takes no 'within'")


# ---- options ----------------------------------------------------------------------------------------------------------------

MULTI = frozenset({"capture"})


def _allowed(word, spec, out):
    return word in spec and (word not in out or word in MULTI)


def _next_option(cur, spec, out):
    tok = cur.take()
    if tok is None:
        return None
    if not _allowed(tok.text, spec, out):
        return cur.fail(f"unexpected or repeated '{tok.text}'")
    return tok.text


def _one_option(cur, spec, out):
    word = _next_option(cur, spec, out)
    value = None if word is None else spec[word](cur)
    if value is None:
        return False
    out[word] = out.get(word, ()) + (value,) if word in MULTI else value
    return True


def read_options(cur, spec):
    """`{option: value}` for the options after a statement's fixed words, in any order; None after a problem."""
    out = {}
    while not cur.at_end():
        if not _one_option(cur, spec, out):
            return None
    return out


def _read_positionals(cur, positionals):
    args = {}
    for key, reader in positionals:
        args[key] = reader(cur)
        if args[key] is None:
            return None
    return args


def _optional_count(cur):
    nxt = cur.peek()
    if nxt is None or nxt.text != "count":
        return 1
    cur.take()
    return take_positive(cur)


def _mode_until(cur):
    expr = take_str_expr(cur)
    count = None if expr is None else _optional_count(cur)
    return None if count is None else (expr, count)


def _mode_number(cur):
    n = take_positive(cur)
    return None if n is None else (n,)


_MODES = {"bytes": _mode_number, "lines": _mode_number, "until": _mode_until, "eof": lambda cur: ()}


def read_mode(cur):
    """The `recv` mode as ('bytes', n) | ('until', expr, count) | ('lines', k) | ('eof',), () when absent, None after a problem."""
    tok = cur.peek()
    if tok is None or tok.text not in _MODES:
        return ()
    cur.take()
    args = _MODES[tok.text](cur)
    return None if args is None else (tok.text, *args)


def _optional_as(cur):
    nxt = cur.peek()
    if nxt is None or nxt.text != "as":
        return ""
    cur.take()
    return take_choice(CAPTURE_TYPES)(cur)


def _capture_head(cur):
    name = take_name(cur)
    how = take_choice(("json", "regex"))(cur)
    return None if name is None or how is None else (name, how)


def _capture_tail(cur, name, how):
    source = take_path(cur) if how == "json" else take_str_expr(cur)
    as_type = None if source is None else _optional_as(cur)
    return None if as_type is None else Capture(name, how, source, as_type)


def read_capture(cur):
    head = _capture_head(cur)
    return None if head is None else _capture_tail(cur, *head)


# ---- statements ---------------------------------------------------------------------------------------------------------------

def _ok(cur, args):
    return True


def _both_or_neither(first, second, message):
    def check(cur, args):
        if (first in args) != (second in args):
            return bool(cur.fail(message))
        return True
    return check


def _simple(op, positionals, spec, post=_ok):
    """A handler for a statement of fixed words, then options: the Step carries `conn`, and every other value in `args`."""
    def handler(cur, parser):
        args = _read_positionals(cur, positionals)
        opts = None if args is None else read_options(cur, spec)
        if opts is None:
            return None
        args.update(opts)
        return Step(op, cur.lineno, args.pop("conn", ""), args) if post(cur, args) else None
    return handler


def _h_scenario(cur, parser):
    version = take_int(cur)
    if version is None:
        return None
    if version != VERSION:
        parser.stopped = True
        return cur.fail(f"format version {version}: this runner reads 'scenario {VERSION}' (a version 1 file is re-conformed, never run)")
    parser.stage = 1
    return _DONE


def _h_default(cur, parser):
    within = take_choice(("within",))(cur)
    ms = None if within is None else take_dur(cur)
    if ms is None:
        return None
    parser.default_within, parser.stage = ms, 2
    return _DONE


def _h_env(cur, parser):
    name = take_env_name(cur)
    if name == "PORT":
        return cur.fail("PORT is reserved: the runner chooses the port")
    return _setup(cur, parser, "env", name)


def _h_file(cur, parser):
    return _setup(cur, parser, "file", take_name(cur))


def _setup(cur, parser, kind, name):
    value = None if name is None else take_str_expr(cur)
    if value is None:
        return None
    parser.setup.append(Setup(kind, name, value, cur.lineno))
    return _DONE


def _ready_stdout(cur):
    expr = take_str_expr(cur)
    return None if expr is None else ("stdout", expr)


_READY = {"port": lambda cur: ("port", None), "stdout": _ready_stdout}


def _start_ready(cur):
    """('port', None) or ('stdout', expr), or None after a problem."""
    if take_choice(("ready",))(cur) is None:
        return None
    kind = take_choice(tuple(_READY))(cur)
    return None if kind is None else _READY[kind](cur)


def _h_start(cur, parser):
    ready = _start_ready(cur)
    opts = None if ready is None else read_options(cur, {"within": take_dur})
    if opts is None or "within" not in opts:
        return cur.fail("start needs 'within <duration>'")
    parser.start, parser.stage = Start(cur.lineno, ready[0], ready[1], opts["within"]), 3
    return _DONE


def _h_request(cur, parser):
    args = _read_positionals(cur, (("conn", take_conn), ("method", take_method), ("target", take_str_expr)))
    if args is None:
        return None
    parser.pending = {"line": cur.lineno, "args": {**args, "headers": [], "body": None}}
    return _DONE


def _h_header(cur, parser):
    if parser.pending is None:
        return cur.fail("a header line belongs to a request")
    name = take_single_string(cur)
    value = None if name is None else take_str_expr(cur)
    if value is None:
        return None
    parser.pending["args"]["headers"].append((name, value))
    return _DONE


def _read_body(cur):
    nxt = cur.peek()
    return _json_body(cur) if nxt is not None and nxt.text == "json" else _string_body(cur)


def _h_body(cur, parser):
    if parser.pending is None or parser.pending["args"]["body"] is not None:
        return cur.fail("a body line belongs to a request and a request has at most one")
    body = _read_body(cur)
    if body is None:
        return None
    parser.pending["args"]["body"] = body
    return _DONE


def _string_body(cur):
    expr = take_str_expr(cur)
    return None if expr is None else ("str", expr)


def _json_body(cur):
    cur.take()
    text = cur.rest()
    if "${" in text:
        return ("json", text)
    parsed = rs_json.parse(text)
    return ("json", text) if parsed.ok else cur.fail(f"the body is not JSON: {parsed.message}")


def _h_expect(cur, parser):
    conn = take_conn(cur)
    what = take_choice(("quiet", "closed"))(cur)
    if conn is None or what is None:
        return None
    return _EXPECT[what](cur, conn)


def _expect_quiet(cur, conn):
    if take_choice(("for",))(cur) is None:
        return None
    ms = take_dur(cur)
    return None if ms is None else Step("quiet", cur.lineno, conn, {"for": ms})


def _expect_closed(cur, conn):
    opts = read_options(cur, {"within": take_dur})
    return None if opts is None else Step("closed", cur.lineno, conn, opts)


_EXPECT = {"quiet": _expect_quiet, "closed": _expect_closed}


def _h_together(cur, parser):
    if parser.top().kind == "together":
        return cur.fail("a together group does not nest")
    parser.stack.append(_Frame("together", cur.lineno, None, []))
    return _DONE


def _h_repeat(cur, parser):
    count = _take(cur, "int", f"a repeat count of 1 to {MAX_REPEAT}", lambda n: 1 <= n <= MAX_REPEAT)
    name = None if count is None else take_name(cur)
    if name is None:
        return None
    if parser.top().kind == "together":
        return cur.fail("a together group holds no repeat")
    parser.stack.append(_Frame("repeat", cur.lineno, (count, name), []))
    return _DONE


def _h_end(cur, parser):
    if len(parser.stack) == 1:
        return cur.fail("'end' closes nothing")
    frame = parser.stack.pop()
    node = Together(frame.line, tuple(frame.items)) if frame.kind == "together" else Repeat(frame.line, *frame.extra, tuple(frame.items))
    return node if parser.valid_group(node, cur) else None


def _send_post(cur, args):
    return _both_or_neither("split", "pause", "'split' and 'pause' go together")(cur, args)


_POSITIONAL_SEND = (("conn", take_conn), ("data", take_str_expr))
_HANDLERS = {
    "connect": _simple("connect", (("conn", take_conn), ("kind", take_choice(("tcp", "http")))), {"rcvbuf": take_positive, "within": take_dur}),
    "send": _simple("send", _POSITIONAL_SEND, {"split": take_offsets, "pause": take_dur, "within": take_dur}, _send_post),
    "recv": _simple("recv", (("conn", take_conn), ("mode", read_mode)),
                    {"chunk": take_positive, "pause": take_dur, "capture": read_capture, "within": take_dur},
                    _both_or_neither("chunk", "pause", "'chunk' and 'pause' go together")),
    "close": _simple("close", (("conn", take_conn),), {}),
    "shutdown": _simple("shutdown", (("conn", take_conn),), {}),
    "sleep": _simple("sleep", (("for", take_dur),), {"within": _refuse_within}),
    "signal": _simple("signal", (("name", take_choice(("TERM",))),), {}),
    "wait-exit": _simple("wait_exit", (), {"within": take_dur}),
    "expect": _h_expect, "request": _h_request, "header": _h_header, "body": _h_body,
    "together": _h_together, "repeat": _h_repeat, "end": _h_end,
}
_STAGES = (
    {"scenario": _h_scenario},
    {"default": _h_default},
    {"env": _h_env, "file": _h_file, "start": _h_start},
    _HANDLERS,
)
_STAGE_HELP = ("the first statement is 'scenario 2'", "the second statement is 'default within <duration>'",
               "only env, file and start come before the start", "")


@dataclass
class _Frame:
    kind: str
    line: int
    extra: Any
    items: list


_CONTINUATION = ("header", "body")


def _not_allowed(word, stage):
    help_text = _STAGE_HELP[stage]
    return f"'{word}' is not allowed here" + (f" ({help_text})" if help_text else "")


class _Parser:
    def __init__(self, file, problems):
        self.file, self.problems = file, problems
        self.stage, self.stopped, self.last_line = 0, False, 0
        self.default_within, self.setup, self.start = None, [], None
        self.stack, self.pending = [_Frame("top", 0, None, [])], None

    def top(self):
        return self.stack[-1]

    def statement(self, lineno, text):
        cur = Cursor(text, self.file, lineno, self.problems)
        word = cur.take()
        if word is None or word.text not in _CONTINUATION:
            self.flush()
        handler = self._handler(word, cur)
        if handler is not None:
            self._run(handler, cur)

    def _handler(self, word, cur):
        if word is None or word.kind != "word":
            return cur.fail("a statement keyword is expected")
        return _STAGES[self.stage].get(word.text) or cur.fail(_not_allowed(word.text, self.stage))

    def _run(self, handler, cur):
        result = handler(cur, self)
        if result is None:
            return
        if not cur.at_end():
            cur.fail(f"unexpected '{cur.rest().split(' ')[0]}'")
        elif result is not _DONE:
            self.add(result, cur)

    def add(self, node, cur):
        frame = self.top()
        if frame.kind == "together" and not (isinstance(node, Step) and node.op in ("send", "request")):
            cur.fail("only send and request steps stand inside a together group")
            return
        frame.items.append(node)

    def flush(self):
        if self.pending is None:
            return
        pending, self.pending = self.pending, None
        args = {**pending["args"], "headers": tuple(pending["args"]["headers"])}
        self.top().items.append(Step("request", pending["line"], args.pop("conn"), args))

    def valid_group(self, node, cur):
        if isinstance(node, Together):
            return self._valid_together(node, cur)
        if expanded_steps(node) > MAX_EXPANDED_STEPS:
            return bool(cur.fail(f"the repeat expands to more than {MAX_EXPANDED_STEPS} steps"))
        return True

    def _valid_together(self, node, cur):
        conns = [s.conn for s in node.steps]
        if len(conns) < 2 or len(set(conns)) != len(conns):
            return bool(cur.fail("a together group holds two or more steps, one per connection"))
        return True

    def _unfinished(self):
        if self.problems:
            return None
        if len(self.stack) > 1:
            return Problem(self.file, self.top().line, f"the {self.top().kind} group is not closed by 'end'")
        return Problem(self.file, self.last_line, "the head is incomplete: scenario, default within, start") if self.stage < 3 else None

    def finish(self):
        self.flush()
        gap = self._unfinished()
        if gap is not None:
            self.problems.append(gap)
        return None if self.problems else Scenario(self.file, self.default_within, tuple(self.setup), self.start, tuple(self.top().items))


def _feed(parser, lines):
    for lineno, text in lines:
        if parser.stopped:
            return
        parser.last_line = lineno
        if not lex.is_blank_or_comment(text):
            parser.statement(lineno, text)


def parse_script(data, file):
    problems = []
    parser = _Parser(file, problems)
    _feed(parser, lex.split_lines(data, file, problems))
    scenario = parser.finish()
    problems.extend([] if scenario is None else check_scenario(scenario))
    return (None if problems else scenario), problems
