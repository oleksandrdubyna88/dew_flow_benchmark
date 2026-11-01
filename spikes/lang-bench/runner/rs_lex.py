"""rs_lex: the lexical rules of FORMAT v2 section 4.1, shared by the scenario (.in) and the transcript (.out) parsers.

Nothing here raises for a malformed sample: a problem is a `Problem(file, line, message)` appended to a list the caller owns.
A line is read through a `Cursor`, token by token, because JSON text occupies the rest of its line and must not be lexed.
"""
import re
from dataclasses import dataclass
from decimal import Decimal
from typing import NamedTuple


class Problem(NamedTuple):
    file: str
    line: int
    message: str

    def text(self):
        return f"{self.file}:{self.line}: {self.message}"


@dataclass(frozen=True)
class Lit:
    data: bytes


@dataclass(frozen=True)
class Var:
    name: str


@dataclass(frozen=True)
class Field:
    """A normalised field of a transcript string: `${:kind args=bind}`; `bind` is a name template (Lit and Var parts) or ()."""
    kind: str
    args: tuple = ()
    bind: tuple = ()


@dataclass(frozen=True)
class StrItem:
    parts: tuple
    count: int = 1


@dataclass(frozen=True)
class StrExpr:
    items: tuple


@dataclass(frozen=True)
class Tok:
    kind: str
    text: str
    value: object = None
    col: int = 0


MAX_DURATION_MS = 600_000
UNIT_MS = {"ms": 1, "s": 1000}
MAX_REPEAT_COUNT = 10_000_000
NAME = re.compile(r"[a-z][a-z0-9_]*\Z")
DURATION = re.compile(r"([0-9]+)(ms|s)\Z")
INTEGER = re.compile(r"[0-9]+\Z")
INT_LIST = re.compile(r"[0-9]+(,[0-9]+)+\Z")
WORD = re.compile(r"[A-Za-z_][A-Za-z0-9_-]*\Z")
JSON_PATH = re.compile(r"\$(\.[A-Za-z_][A-Za-z0-9_]*|\[[0-9]+\])*\Z")
DECIMAL = re.compile(r"-?[0-9]+(\.[0-9]+)?\Z")
COUNT = re.compile(r"\*([0-9]+)(?= |\Z)")
CONTROL = re.compile(r"[\x00-\x09\x0b-\x1f\x7f]")
VARIABLE_IN_NAME = re.compile(r"(\$\{[a-z][a-z0-9_]*\})")
FIELD_KINDS = ("int", "uint", "hex", "uuid", "token", "ts")
SIMPLE_ESCAPES = {"\\": b"\\", '"': b'"', "$": b"$", "r": b"\r", "n": b"\n", "t": b"\t", "0": b"\0"}
HEX_DIGITS = frozenset("0123456789abcdefABCDEF")
KEYWORDS = frozenset(
    "scenario default within env file start ready port stdout stdin connect tcp http rcvbuf send split pause request header body "
    "json recv bytes until count lines eof chunk capture regex as expect quiet for closed close shutdown sleep together repeat end "
    "signal TERM wait-exit transcript reply exit check distinct equal unordered empty".split()
)


def duration_ms(text):
    """The milliseconds of `250ms` / `2s`, or None when `text` is not a duration of at most 600 s."""
    m = DURATION.match(text)
    if m is None:
        return None
    value = int(m.group(1)) * UNIT_MS[m.group(2)]
    return value if value <= MAX_DURATION_MS else None


def split_lines(data, file, problems):
    """`[(line number, text)]` of a file's bytes: UTF-8 without a BOM, every line ended by a line feed, no control character but LF."""
    text = _decode(data, file, problems)
    if text is None:
        return []
    lines = text.split("\n")
    problems.extend(_text_problems(text, lines, file))
    if text.endswith("\n"):
        lines.pop()
    return list(enumerate(lines, 1))


def _decode(data, file, problems):
    try:
        return data.decode("utf-8")
    except UnicodeDecodeError as e:
        problems.append(Problem(file, data.count(b"\n", 0, e.start) + 1, f"not valid UTF-8 at byte {e.start}"))
        return None


def _text_problems(text, lines, file):
    found = [Problem(file, n, "a tab, carriage return or other control character") for n, line in enumerate(lines, 1) if CONTROL.search(line)]
    found += [Problem(file, 1, "a byte-order mark is not allowed")] * text.startswith("\ufeff")
    found += [Problem(file, len(lines), "the last line is not ended by a line feed")] * bool(text and not text.endswith("\n"))
    return found


def is_blank_or_comment(text):
    stripped = text.lstrip(" ")
    return stripped == "" or stripped.startswith("#")


class Cursor:
    """Reads one line token by token. Problems go to `problems`; `ok` turns False at the first one on this line."""

    def __init__(self, text, file, lineno, problems, allow_fields=False):
        self.text, self.file, self.lineno, self.problems = text, file, lineno, problems
        self.pos, self.ok, self.allow_fields = 0, True, allow_fields

    def fail(self, message):
        if self.ok:
            self.problems.append(Problem(self.file, self.lineno, message))
        self.ok = False
        return None

    def skip_spaces(self):
        while self.pos < len(self.text) and self.text[self.pos] == " ":
            self.pos += 1

    def at_end(self):
        self.skip_spaces()
        return self.pos >= len(self.text)

    def rest(self):
        """The raw remainder of the line (JSON text), consumed."""
        self.skip_spaces()
        out, self.pos = self.text[self.pos:], len(self.text)
        return out

    def peek_kind(self):
        """The kind of the next token without consuming it, or None at the end of the line."""
        pos, ok, mark = self.pos, self.ok, len(self.problems)
        tok = self.take()
        self.pos, self.ok = pos, ok
        del self.problems[mark:]
        return None if tok is None else tok.kind

    def take(self):
        self.skip_spaces()
        if self.pos >= len(self.text):
            return None
        return self._string_token() if self.text[self.pos] == '"' else self._bare_token()

    def _bare_token(self):
        start = self.pos
        end = self.text.find(" ", start)
        self.pos = len(self.text) if end < 0 else end
        return Tok(*_classify(self.text[start:self.pos]), col=start)

    def _string_token(self):
        start = self.pos
        end = _closing_quote(self.text, start)
        if end is None:
            self.pos = len(self.text)
            return self.fail("a string is not closed")
        parts = decode_content(self.text[start + 1:end], self.allow_fields, self.fail)
        self.pos = end + 1
        count = self._repeat_count()
        ok = parts is not None and count is not None
        return Tok("str", self.text[start:self.pos], StrItem(tuple(parts), count), start) if ok else None

    def _repeat_count(self):
        if self.text[self.pos:self.pos + 1] != "*":
            return 1
        m = COUNT.match(self.text, self.pos)
        n = int(m.group(1)) if m else 0
        if not 1 <= n <= MAX_REPEAT_COUNT:
            return self.fail(f"a repeat count after a string must be 1 to {MAX_REPEAT_COUNT}")
        self.pos = m.end()
        return n


_TOKEN_KINDS = (
    ("dur", DURATION, duration_ms),
    ("int", INTEGER, int),
    ("list", INT_LIST, lambda raw: tuple(int(x) for x in raw.split(","))),
    ("path", JSON_PATH, str),
    ("word", WORD, str),
)


def _classify(raw):
    for kind, pattern, convert in _TOKEN_KINDS:
        if pattern.match(raw):
            return kind, raw, convert(raw)
    return "bare", raw, None


def _closing_quote(text, start):
    i = start + 1
    while i < len(text):
        if text[i] == '"':
            return i
        i += 2 if text[i] == "\\" else 1
    return None


def decode_content(content, allow_fields, fail):
    """The parts (Lit, Var, Field) of a string's content, or None after calling `fail(message)`."""
    parts, buf, i = [], bytearray(), 0
    while i < len(content):
        i = _decode_one(content, i, allow_fields, buf, parts)
        if isinstance(i, str):
            fail(i)
            return None
    _flush(buf, parts)
    return parts


def _flush(buf, parts):
    if buf:
        parts.append(Lit(bytes(buf)))
        buf.clear()


def _decode_one(content, i, allow_fields, buf, parts):
    """The index after one character, escape or variable, or a message (str) when it is malformed."""
    ch = content[i]
    if ch == "\\":
        return _escape(content, i, buf)
    if ch == "$" and content.startswith("{", i + 1):
        return _variable(content, i, allow_fields, buf, parts)
    buf.extend(ch.encode("utf-8"))
    return i + 1


def _escape(content, i, buf):
    nxt = content[i + 1:i + 2]
    if nxt in SIMPLE_ESCAPES:
        buf.extend(SIMPLE_ESCAPES[nxt])
        return i + 2
    digits = content[i + 2:i + 4]
    if nxt == "x" and _is_hex_pair(digits):
        buf.append(int(digits, 16))
        return i + 4
    return f"unknown escape \\{nxt}"


def _is_hex_pair(digits):
    return len(digits) == 2 and all(c in HEX_DIGITS for c in digits)


def _variable(content, i, allow_fields, buf, parts):
    close = _matching_brace(content, i + 1)
    if close is None:
        return "a variable is not closed"
    _flush(buf, parts)
    part = _variable_part(content[i + 2:close], allow_fields)
    if part is None:
        return f"a malformed or misplaced variable ${{{content[i + 2:close]}}}"
    parts.append(part)
    return close + 1


def _variable_part(body, allow_fields):
    if body.startswith(":"):
        return parse_field(body[1:]) if allow_fields else None
    return Var(body) if NAME.match(body) else None


def _matching_brace(content, open_index):
    depth = 0
    for j in range(open_index, len(content)):
        depth += {"{": 1, "}": -1}.get(content[j], 0)
        if depth == 0:
            return j
    return None


def parse_name_template(text):
    """A bound name that may carry `${var}`: a tuple of Lit and Var parts, or None."""
    if not NAME.match(VARIABLE_IN_NAME.sub("x", text)):
        return None
    return tuple(_name_part(p) for p in VARIABLE_IN_NAME.split(text) if p)


def _name_part(piece):
    return Var(piece[2:-1]) if piece.startswith("${") else Lit(piece.encode("ascii"))


def parse_field(body):
    """`type[ args][=bind]` -> Field, or None. The bound name may carry `${var}`, so only the last `=` binds."""
    spec, bind = body, ()
    if "=" in body:
        spec, name = body.rsplit("=", 1)
        bind = parse_name_template(name)
    return None if bind is None else _field_of(spec.split(" "), bind)


def _field_of(words, bind):
    build = _FIELD_BUILDERS.get("ts~" if words[0].startswith("ts~") else words[0])
    return build(words, bind) if build else None


def _plain_field(words, bind):
    return Field(words[0], (), bind) if len(words) == 1 else None


def _window_field(words, bind):
    window = duration_ms(words[0][3:])
    return Field("ts~", (window,), bind) if len(words) == 1 and window is not None else None


def _num_field(words, bind):
    if len(words) != 3 or not all(DECIMAL.match(w) for w in words[1:]):
        return None
    return Field("num", (Decimal(words[1]), Decimal(words[2])), bind)


_FIELD_BUILDERS = {**{k: _plain_field for k in FIELD_KINDS}, "ts~": _window_field, "num": _num_field}


def take_str_expr(cursor):
    """One or more adjacent strings joined into a StrExpr (4.1), or None after a problem."""
    items = []
    while cursor.peek_kind() == "str":
        items.append(cursor.take().value)
    return StrExpr(tuple(items)) if items else cursor.fail("a string is expected")


def take_single_string(cursor):
    """Exactly one string token (a header name), as a StrExpr, or None after a problem."""
    tok = cursor.take()
    if tok is None or tok.kind != "str" or tok.value.count != 1:
        return cursor.fail("a plain string is expected")
    return StrExpr((tok.value,))


def vars_of(expr):
    """Every variable name a StrExpr uses, in order."""
    return [p.name for item in expr.items for p in item.parts if isinstance(p, Var)]
