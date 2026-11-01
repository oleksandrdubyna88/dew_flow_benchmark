"""rs_samples: what the command line reads from disk besides the samples' contents: the kind of a task, the pairs of a samples folder,
the `cases.txt` manifest, the limits file. Every function returns its value and a list of Problems; none raises for a bad input."""
import os
import re
from decimal import Decimal
from typing import NamedTuple, Optional

import rs_json
from rs_lex import Problem

KIND_PHRASES = {
    "a program that reads standard input once and writes standard output once": "olympiad",
    "a long-running program that serves until it is stopped by SIGTERM": "daemon",
    "a long-running HTTP server that serves until it is stopped by SIGTERM": "web",
}
SAMPLE_ID = re.compile(r"[A-Za-z0-9][A-Za-z0-9_-]*\Z")
R_ID = re.compile(r"R[0-9]+\Z")
COMPARISONS = ("bytes", "normalised", "json", "relational")
COMBINABLE = COMPARISONS[1:]
LIMIT_KEYS = ("timeS", "readyS", "graceS")
FLOOR_S = 1
MAX_S = 600


class Pair(NamedTuple):
    id: str
    in_path: str
    out_path: str
    in_name: str
    out_name: str
    r_ids: tuple = ()
    comparison: Optional[str] = None


class Limits(NamedTuple):
    time_ms: Optional[int] = None
    ready_ms: Optional[int] = None
    grace_ms: Optional[int] = None


def shown(name):
    """A file name as text that survives any stream: bytes that are not UTF-8 appear as \\xHH."""
    return os.fsencode(name).decode("utf-8", "backslashreplace")


def read_bytes(path):
    with open(path, "rb") as f:
        return f.read()


# ---- the kind ---------------------------------------------------------------------------------------------------------------------

def _statement_text(workspace):
    try:
        return read_bytes(os.path.join(workspace, "STATEMENT.md")).decode("utf-8"), []
    except (OSError, UnicodeDecodeError) as e:
        return None, [Problem("STATEMENT.md", 0, f"cannot read the Kind row: {type(e).__name__}")]


def _kind_phrase(text):
    return next((cells[1] for cells in map(_cells, text.splitlines()) if len(cells) >= 2 and cells[0] == "Kind"), None)


def kind_from_statement(workspace):
    """(kind, problems) from the `Kind` row of the workspace's STATEMENT.md."""
    text, problems = _statement_text(workspace)
    if text is None:
        return None, problems
    phrase = _kind_phrase(text)
    kind = KIND_PHRASES.get(phrase)
    return kind, ([] if kind else [Problem("STATEMENT.md", 0, f"the Kind row is {phrase!r}, not one of the three kind phrases")])


def _cells(line):
    return [c.strip() for c in line.strip().strip("|").split("|")]


# ---- pairs ------------------------------------------------------------------------------------------------------------------------

def _ids_with_suffix(names, suffix):
    return {n[: -len(suffix)]: n for n in names if n.endswith(suffix)}


def _files_in(folder):
    return [n for n in os.listdir(folder) if os.path.isfile(os.path.join(folder, n))]


def _missing(source, other, message):
    return [Problem(shown(n), 0, message) for i, n in source.items() if i not in other]


def _bad_ids(ins, outs):
    return [Problem(shown(ins[i]), 0, "the id before .in is not a sample id") for i in ins if i in outs and not SAMPLE_ID.match(i)]


def discover(samples_dir):
    """(pairs sorted by id, problems): every `<id>.in` has its `<id>.out` and the other way round."""
    names = _files_in(samples_dir)
    ins, outs = _ids_with_suffix(names, ".in"), _ids_with_suffix(names, ".out")
    problems = _missing(ins, outs, "has no matching .out file") + _missing(outs, ins, "has no matching .in file") + _bad_ids(ins, outs)
    return [_pair(samples_dir, i, ins[i], outs[i]) for i in sorted(ins) if i in outs and SAMPLE_ID.match(i)], problems


def _pair(samples_dir, ident, in_name, out_name):
    return Pair(ident, os.path.join(samples_dir, in_name), os.path.join(samples_dir, out_name), shown(in_name), shown(out_name))


def cases_manifest(samples_dir, ids):
    """({id: (comparison, r_ids)}, problems) from `cases.txt` beside the pairs (FORMAT 3.1: `<id> <comparison> <R-id>...`); no file is none."""
    path = os.path.join(samples_dir, "cases.txt")
    if not os.path.isfile(path):
        return {}, []
    try:
        lines = read_bytes(path).decode("utf-8").splitlines()
    except UnicodeDecodeError:
        return {}, [Problem("cases.txt", 0, "not valid UTF-8")]
    manifest, problems = {}, []
    for number, line in enumerate(lines, 1):
        problems += _manifest_line(number, line, ids, manifest)
    return manifest, problems


def _manifest_line(number, line, ids, manifest):
    words = line.split()
    if not words or line.lstrip().startswith("#"):
        return []
    ident, comparison, rids = words[0], "".join(words[1:2]), tuple(words[2:])
    bad = _manifest_problem(ident, comparison, rids, ids, manifest)
    manifest.setdefault(ident, (comparison, rids))
    return [Problem("cases.txt", number, bad)] if bad else []


def _joined_ok(parts):
    return len(set(parts)) == len(parts) and set(parts) <= set(COMBINABLE)


def comparison_ok(text):
    """`bytes`, `normalised`, `json`, `relational`, or several of the last three joined by `+`, each at most once."""
    parts = text.split("+")
    return text in COMPARISONS if len(parts) == 1 else _joined_ok(parts)


def _manifest_problem(ident, comparison, rids, ids, manifest):
    checks = (
        (not comparison_ok(comparison), f"{ident}: the comparison {comparison!r} is none of bytes, normalised, json, relational or a + join of the last three"),
        (not rids, f"{ident} names no requirement id"),
        (not all(R_ID.match(r) for r in rids), f"{ident} lists something that is not an R-id (R<number>)"),
        (ident in manifest, f"{ident} appears twice"),
        (ident not in ids, f"{ident} has no sample pair"),
    )
    return next((message for failed, message in checks if failed), None)


# ---- limits -----------------------------------------------------------------------------------------------------------------------

def _why(parsed):
    return f" ({parsed.message})" if not parsed.ok else ""


def _read_limits(path, name):
    try:
        parsed = rs_json.parse(read_bytes(path))
    except OSError as e:
        return None, [Problem(name, 0, f"cannot read the limits file: {type(e).__name__}")]
    if parsed.ok and isinstance(parsed.value, dict):
        return parsed.value, []
    return None, [Problem(name, 0, "the limits file must be one JSON object" + _why(parsed))]


def load_limits(path):
    """(Limits, problems) from a JSON object with some of timeS, readyS, graceS (whole or decimal seconds, exact)."""
    name = os.path.basename(path)
    value, problems = _read_limits(path, name)
    if problems:
        return Limits(), problems
    problems = _unknown_keys(name, value) + _bad_values(name, value)
    return (Limits() if problems else _limits_of(value)), problems


def _unknown_keys(name, value):
    return [Problem(name, 0, f"unknown key {k!r} (allowed: {', '.join(LIMIT_KEYS)})") for k in value if k not in LIMIT_KEYS]


def _bad_values(name, value):
    return [Problem(name, 0, _limit_message(k, value[k])) for k in LIMIT_KEYS if k in value and _limit_message(k, value[k])]


def _ms_of(seconds):
    return seconds * 1000 if isinstance(seconds, Decimal) and seconds * 1000 == (seconds * 1000).to_integral_value() else None


def _whole_ok(key, ms):
    return key == "timeS" or ms % 1000 == 0


def _limit_message(key, seconds):
    ms = _ms_of(seconds)
    if ms is None or not FLOOR_S * 1000 <= ms <= MAX_S * 1000:
        return f"{key} must be a number of seconds from {FLOOR_S} to {MAX_S}"
    return None if _whole_ok(key, ms) else f"{key} must be whole seconds"


def _limits_of(value):
    ms = {k: int(_ms_of(value[k])) if k in value else None for k in LIMIT_KEYS}
    return Limits(ms["timeS"], ms["readyS"], ms["graceS"])
