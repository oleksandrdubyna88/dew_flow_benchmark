"""rs_model: the frozen dataclasses of a parsed scenario script (FORMAT v2 section 4), shared by the parser and the checker."""
from dataclasses import dataclass, field
from typing import Any, NamedTuple, Optional

from rs_lex import StrExpr

VERSION = 2
MAX_REPEAT = 100_000
MAX_EXPANDED_STEPS = 1_000_000
BUILT_IN_CONNECTIONS = ("stdin", "stdout")


@dataclass(frozen=True)
class Step:
    op: str
    line: int
    conn: str = ""
    args: dict = field(default_factory=dict)


@dataclass(frozen=True)
class Together:
    line: int
    steps: tuple


@dataclass(frozen=True)
class Repeat:
    line: int
    count: int
    name: str
    body: tuple


@dataclass(frozen=True)
class Setup:
    kind: str
    name: str
    value: StrExpr
    line: int


@dataclass(frozen=True)
class Start:
    line: int
    ready: str
    text: Optional[StrExpr]
    within_ms: int


@dataclass(frozen=True)
class Capture:
    name: str
    how: str
    source: Any
    as_type: str


@dataclass(frozen=True)
class Scenario:
    file: str
    default_within_ms: int
    setup: tuple
    start: Start
    body: tuple


class Produced(NamedTuple):
    kind: str
    conn: str
