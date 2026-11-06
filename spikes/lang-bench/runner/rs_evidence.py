"""rs_evidence: what a rejected daemon or web case keeps of the session, so a reject can be read later without running it again.

The record of a failed case holds, next to the position of the difference, the expected entry and the observations that explain it:
the differing observation of an ordered transcript, every observation of an unordered group that no assignment matched (with their
connection names), or the last observations of a session that ended with a different count. Every string is cut the way the
report's lines are (`abbreviate`) and every list has a bound. It is a program's output, like the report: the harness writes it
under the series folder only. Nothing here is read by a verdict: it is built after `rs_match` has judged."""
from rs_report import expected_text
from rs_transcript import abbreviate, render_actual

MAX_OBSERVATIONS = 64
MAX_HEADERS = 16
MAX_STEP_TEXT = 200
COUNT_TAIL = 8


def _headers(actual):
    return [[abbreviate(name), abbreviate(value)] for name, value in actual.headers[:MAX_HEADERS]]


def _reply_part(actual):
    if actual.kind != "reply":
        return {}
    return {"status": actual.status, "headers": _headers(actual), "body": abbreviate(actual.body)}


def _data_part(actual):
    return {"data": abbreviate(actual.data)} if actual.data else {}


def observation(actual):
    """One observation as a JSON-able dict: the line the report prints, its place in the script, and the data the line leaves out
    (a reply's headers and body, the bytes received)."""
    head = {"kind": actual.kind, "conn": actual.conn, "text": render_actual(actual), "stepLine": actual.step_line}
    head["stepText"] = actual.step_text[:MAX_STEP_TEXT]
    return {**head, **_reply_part(actual), **_data_part(actual)}


def _observed(verdict, entries):
    if verdict.part == "count":
        return list(entries[-COUNT_TAIL:])
    return [verdict.actual] if verdict.actual is not None else list(verdict.group)


def _expected(verdict):
    return None if verdict.expected is None else expected_text(verdict.expected)


def evidence(verdict, entries):
    """The evidence of a failed verdict, or None for a pass. `entries` are the session's observations in order."""
    if verdict.ok:
        return None
    seen = _observed(verdict, entries)
    kept = seen[:MAX_OBSERVATIONS]
    group = verdict.actual is None and verdict.part != "count"
    return {"expected": _expected(verdict), "group": group, "observed": [observation(a) for a in kept], "omitted": len(seen) - len(kept)}
