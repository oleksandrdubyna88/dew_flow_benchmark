"""rs_samples: what the command line reads from disk besides the samples' contents: the kind of a task, the pairs of a samples folder,
the `cases.txt` manifest, the limits file. Every function returns its value and a list of Problems; none raises for a bad input."""
import os
import re
import stat
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
    stdin: bytes = b""
    expected: bytes = b""


MAX_SAMPLE_BYTES = 1 << 30


READ_CHUNK = 1 << 20
TOO_LARGE = object()


class _Budget:
    """What the samples may still hold in memory, in bytes."""

    def __init__(self, ceiling):
        self.left = ceiling


def _unreadable(name, e):
    return Problem("samples", 0, f"cannot read {shown(name)}: {type(e).__name__}")


def _slurp(fd, budget):
    """The bytes of an open file, at most the budget left plus one (so that 'more than the budget' is seen), or None when there are more."""
    data = bytearray()
    while len(data) <= budget.left:
        chunk = os.read(fd, min(READ_CHUNK, budget.left + 1 - len(data)))
        if not chunk:
            break
        data += chunk
    return None if len(data) > budget.left else bytes(data)


def _read_open(fd, name, budget):
    """(bytes, None), (None, Problem) or (None, TOO_LARGE): a regular file by fstat, read within the budget."""
    if not stat.S_ISREG(os.fstat(fd).st_mode):
        return None, Problem("samples", 0, f"{shown(name)} is not a regular file")
    data = _slurp(fd, budget)
    if data is None:
        return None, TOO_LARGE
    budget.left -= len(data)
    return data, None


def read_sample(path, name, budget):
    """Open the file once and judge what was opened (O_NONBLOCK, so a FIFO swapped in after the listing cannot block the open)."""
    try:
        fd = os.open(path, os.O_RDONLY | os.O_NONBLOCK)
    except OSError as e:
        return None, _unreadable(name, e)
    try:
        return _read_open(fd, name, budget)
    except OSError as e:
        return None, _unreadable(name, e)
    finally:
        os.close(fd)


UNITS = ("B", "KiB", "MiB", "GiB", "TiB")
LARGEST_SHOWN = 3


def human(size):
    """A size in binary units with one decimal, by integer arithmetic: 1.5 MiB, 110.0 MiB, 1.0 GiB (bytes below 1 KiB)."""
    unit = 0
    while unit < len(UNITS) - 1 and size >> (10 * (unit + 1)):
        unit += 1
    if unit == 0:
        return f"{size} B"
    return f"{size >> (10 * unit)}.{(size * 10 >> (10 * unit)) % 10} {UNITS[unit]}"


def _file_sizes(pairs):
    """[(size, case id, file name)] of every sample file that can be measured, largest first."""
    sizes = []
    for pair in pairs:
        for path, name in ((pair.in_path, pair.in_name), (pair.out_path, pair.out_name)):
            try:
                sizes.append((os.path.getsize(path), pair.id, name))
            except OSError:
                continue
    return sorted(sizes, key=lambda item: (-item[0], item[2]))


def too_large_message(pairs, ceiling):
    """Why the samples cannot be held, in human units, with the largest files and the way out: run fewer cases at a time."""
    head = f"the samples are too large to hold in memory: more than {human(ceiling)} of .in and .out files (the ceiling is {human(ceiling)})"
    largest = _file_sizes(pairs)[:LARGEST_SHOWN]
    if not largest:
        return head
    names = ", ".join(f"{name} {human(size)}" for size, _, name in largest)
    return f"{head}; largest: {names}; run fewer cases at a time with --only <case>, for example --only {largest[0][1]}"


def _too_large(pairs):
    return Problem("samples", 0, too_large_message(pairs, MAX_SAMPLE_BYTES))


def _bytes_or_empty(data):
    return b"" if data is None else data


def _then_out(pair, first, budget):
    """The `.out` file is read unless the `.in` file already used up the budget."""
    return (None, None) if first is TOO_LARGE else read_sample(pair.out_path, pair.out_name, budget)


def _problems_of(*results):
    return [r for r in results if isinstance(r, Problem)]


def _load_pair(pair, budget):
    """`(pair with its bytes, problems)`, or `(None, TOO_LARGE)` when the budget ran out."""
    stdin, first = read_sample(pair.in_path, pair.in_name, budget)
    expected, second = _then_out(pair, first, budget)
    if TOO_LARGE in (first, second):
        return None, TOO_LARGE
    return pair._replace(stdin=_bytes_or_empty(stdin), expected=_bytes_or_empty(expected)), _problems_of(first, second)


def load_all(pairs):
    """`(pairs with their input and expected bytes, problems)`. Every sample is read into memory before anything is built or run, so a
    program that rewrites a file of the samples directory cannot change what its output is compared with. Each file is opened once,
    checked as a regular file with fstat, and read for at most what the budget (MAX_SAMPLE_BYTES for all of them) has left, plus one byte:
    the size limit rests on what was actually read, not on a size looked at earlier."""
    budget, loaded, problems = _Budget(MAX_SAMPLE_BYTES), [], []
    for pair in pairs:
        got, found = _load_pair(pair, budget)
        if found is TOO_LARGE:
            return list(pairs), [_too_large(pairs)]
        loaded.append(got)
        problems += found
    return loaded, problems


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


def _sample_names(folder):
    """Every name that looks like a sample file, whatever it is: a FIFO or a directory is named for what it is, never opened."""
    return [n for n in os.listdir(folder) if n.endswith((".in", ".out"))]


def _irregular_names(folder, names):
    return {n for n in names if not os.path.isfile(os.path.join(folder, n))}


def _irregular_problems(names):
    return [Problem("samples", 0, f"{shown(n)} is not a regular file (a sample is a file: not a FIFO, a directory, a device or a link to one)")
            for n in sorted(names)]


def _missing(source, other, message):
    return [Problem(shown(n), 0, message) for i, n in source.items() if i not in other]


def _bad_ids(ins, outs):
    return [Problem(shown(ins[i]), 0, "the id before .in is not a sample id") for i in ins if i in outs and not SAMPLE_ID.match(i)]


def _is_regular_pair(pair, irregular):
    return not {pair.in_name, pair.out_name} & irregular


def _valid_ids(ins, outs):
    return [i for i in sorted(ins) if i in outs and SAMPLE_ID.match(i)]


def _regular_pairs(samples_dir, ins, outs, irregular):
    pairs = [_pair(samples_dir, i, ins[i], outs[i]) for i in _valid_ids(ins, outs)]
    return [p for p in pairs if _is_regular_pair(p, irregular)]


def discover(samples_dir):
    """(pairs sorted by id, problems): every `<id>.in` has its `<id>.out` and the other way round, and every one of them is a regular file."""
    names = _sample_names(samples_dir)
    ins, outs = _ids_with_suffix(names, ".in"), _ids_with_suffix(names, ".out")
    irregular = _irregular_names(samples_dir, names)
    problems = _missing(ins, outs, "has no matching .out file") + _missing(outs, ins, "has no matching .in file") + _bad_ids(ins, outs)
    return _regular_pairs(samples_dir, ins, outs, irregular), problems + _irregular_problems(irregular)


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
