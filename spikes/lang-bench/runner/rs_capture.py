"""rs_capture: the `capture` of a daemon or web step (FORMAT v2 section 4.6): a value taken from what the program sent (a regular expression over the bytes,
or a JSON path over a reply's body), checked against its declared type and bound to a name for later steps.
A mixin of `rs_session.Session`: it uses `self.text_of`, `self.abort` and `self.variables`."""
import re

import rs_fields as rf
import rs_json

JSON_STEP = re.compile(r"\.([A-Za-z_][A-Za-z0-9_]*)|\[([0-9]+)\]")
CAPTURE_SCAN_BYTES = 16 * 1024 * 1024
TS_BASE_LENGTH = 20


def json_step(value, path):
    """The value at a JSON path (`$.a[0].b`), or raise KeyError."""
    for m in JSON_STEP.finditer(path):
        value = value[m.group(1)] if m.group(1) is not None else value[int(m.group(2))]
    return value


def _raw_of(value):
    return value.encode("utf-8") if isinstance(value, str) else rs_json.render(value).encode("utf-8")


def _fraction_ok(digits, raw):
    """A `ts.N` value has exactly N fraction digits (and the point); a bare `ts` any number."""
    return digits is None or len(raw) == TS_BASE_LENGTH + (digits + 1 if digits else 0)


def _ts_ok(kind, raw):
    digits = int(kind[3:]) if kind.startswith("ts.") else None
    return rf.parse_ts(raw) is not None and _fraction_ok(digits, raw)


def _base_of(kind):
    return "ts" if kind.startswith("ts") else kind


def of_type(kind, raw):
    """Is `raw` of the declared type (`kind` empty: any)?"""
    if not kind:
        return True
    base = _base_of(kind)
    return rf.FIELD_RX[base].fullmatch(raw) is not None and (base != "ts" or _ts_ok(kind, raw))


class CaptureMixin:
    def capture(self, step, data, json_source):
        for cap in step.args.get("capture", ()):
            bound = self.captured(cap, data, json_source)
            if bound is None:
                self.abort("capture-failed", name=cap.name, why=f"capture {cap.name} found nothing of its form")
            self.variables[cap.name] = bound

    def json_bound(self, cap, source):
        parsed = rs_json.parse(source or b"")
        if not parsed.ok:
            return None
        try:
            value = json_step(parsed.value, cap.source)
        except (KeyError, IndexError, TypeError):
            return None
        return rf.Bound(_raw_of(value), value)

    def regex_bound(self, cap, data):
        found = re.search(self.text_of(cap.source), data[:CAPTURE_SCAN_BYTES])
        if found is None:
            return None
        return rf.Bound(found.group(1) if found.re.groups else found.group(0))

    def captured(self, cap, data, json_source):
        bound = self.json_bound(cap, json_source) if cap.how == "json" else self.regex_bound(cap, data)
        return bound if bound is not None and of_type(cap.as_type, bound.raw) else None
