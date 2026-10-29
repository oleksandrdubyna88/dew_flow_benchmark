// O: Claude Code's OTel `api_request` records, decoded from OTLP/HTTP JSON. Pure.
// Every attribute NAME the plan reads (E1.S3 row A-O1) is listed once, here. An attribute a record lacks is `null` with its name in
// `missing` - absent is not zero. The constants below are the 2026-10-29 capture's decisions (A-O1, A-O2, A-O5), each a one-line change.
import { fail, ok } from './result.mjs';

/** A-O2: does an `api_request` record's timestamp mark the END of the request? 'end' | 'start' | 'unknown'. Decided by the 2026-10-29
 * capture (test/fixtures/real/claude-2.1.310-capture, CAPTURE.md row A-O2: 7 matched, all 'end'); a test pins this constant to
 * what `verdicts.mjs` computes from the capture. */
export const OTEL_STAMP_MARKS = 'end';

/** A-O1: where a record's name lives, tried in order: the log body (`claude_code.api_request`, as the 2.1.310 capture sends it) or the
 * `event.name` attribute (`api_request`, also present). A `claude_code.` prefix is dropped. */
export const RECORD_NAME_SOURCES = Object.freeze(['body', 'event.name']);
const NAME_PREFIX = 'claude_code.';

/** A-O5, as the 2.1.310 capture settled it: a depth-one child's request carries `agent_id` and NO `parent_agent_id` (that one is for nested
 * agents). So only `agent_id` is required of a subagent request; a main request without it is not `missing` it. */
export const REQUIRED_FOR_SUBAGENT = Object.freeze(['agent_id']);

/** Attributes only some requests carry: required (and reported `missing`) only where REQUIRED_FOR_SUBAGENT says so. */
const CONDITIONAL = Object.freeze(['agent_id', 'parent_agent_id']);
const MAIN_SOURCES = Object.freeze(['sdk', 'main']);
const SUBAGENT_SOURCE = /^agent:/;

/**
 * The role of a raw `query_source`: 'main' (`sdk`, `main`), 'subagent' (`agent:...`, `subagent`), else 'auxiliary' (kept raw on the request).
 * The documented names (main/subagent/auxiliary) are not what the CLI prints; the 2.1.310 capture showed `sdk` and `agent:builtin:general-purpose`.
 * An absent source is null - unknown, not auxiliary.
 */
export function roleOf(querySource) {
  if (typeof querySource !== 'string' || querySource === '') return null;
  if (MAIN_SOURCES.includes(querySource)) return 'main';
  return querySource === 'subagent' || SUBAGENT_SOURCE.test(querySource) ? 'subagent' : 'auxiliary';
}

/** The names `api_request` is read by -> the field they feed and their kind. */
export const API_REQUEST_ATTRS = Object.freeze({
  model: { field: 'model', kind: 'string' }, input_tokens: { field: 'input', kind: 'count' }, output_tokens: { field: 'output', kind: 'count' },
  cache_read_tokens: { field: 'cacheRead', kind: 'count' }, cache_creation_tokens: { field: 'cacheCreation', kind: 'count' },
  cost_usd: { field: 'costUsd', kind: 'number' }, duration_ms: { field: 'durationMs', kind: 'number' }, query_source: { field: 'querySource', kind: 'string' },
  request_id: { field: 'requestId', kind: 'string' }, agent_id: { field: 'agentId', kind: 'string' },
  parent_agent_id: { field: 'parentAgentId', kind: 'string' }, 'session.id': { field: 'sessionId', kind: 'string' },
});

/** Seen as strings in the 2.1.310 binary (grepped 2026-10-29), not named by the plan: read when present, never reported `missing`. */
export const OPTIONAL_API_REQUEST_ATTRS = Object.freeze({
  cache_creation_5m_tokens: { field: 'cacheCreation5m', kind: 'count' }, cache_creation_1h_tokens: { field: 'cacheCreation1h', kind: 'count' },
});

const TOOL_RESULT_ATTRS = Object.freeze({
  tool_use_id: { field: 'toolUseId', kind: 'string' }, duration_ms: { field: 'durationMs', kind: 'number' },
  tool_name: { field: 'toolName', kind: 'string' }, success: { field: 'success', kind: 'bool' },
});

const nonEmpty = s => (typeof s === 'string' && s !== '' ? s : null);

function decodeValue(key, v) {
  if (v === null || typeof v !== 'object') return fail(`attribute '${key}' has no value object`);
  if ('stringValue' in v) return ok(v.stringValue);
  if ('boolValue' in v) return ok(v.boolValue);
  if ('doubleValue' in v) return ok(Number(v.doubleValue));
  if ('intValue' in v) {
    const n = Number(v.intValue);
    if (!/^-?\d+$/.test(String(v.intValue)) || !Number.isSafeInteger(n)) return fail(`attribute '${key}' is not a safe integer: ${String(v.intValue).slice(0, 24)}`);
    return ok(n);
  }
  return ok(v);
}

function decodeAttrs(list) {
  const attrs = {};
  for (const a of list ?? []) {
    const d = decodeValue(a?.key, a?.value);
    if (!d.ok) return d;
    attrs[a.key] = d.value;
  }
  return ok(attrs);
}

function nameOf(rec, attrs) {
  const found = RECORD_NAME_SOURCES.map(s => (s === 'body' ? rec.body?.stringValue : attrs['event.name'])).find(n => nonEmpty(n));
  if (found === undefined) return null;
  return found.startsWith(NAME_PREFIX) ? found.slice(NAME_PREFIX.length) : found;
}

const nanoToMs = n => {
  if (!/^\d+$/.test(String(n ?? ''))) return null;
  return Number(BigInt(n) / 1_000_000n);
};

function decodeRecord(rec, scope) {
  const attrs = decodeAttrs(rec?.attributes);
  if (!attrs.ok) return attrs;
  return ok({ name: nameOf(rec, attrs.value), tsMs: nanoToMs(rec.timeUnixNano), attrs: attrs.value, scope });
}

/**
 * OTLP logs JSON text -> `{ ok: true, value: [{ name, tsMs, attrs, scope }], rejected: [{ index, reason }] }`, or `{ ok: false, reason }` when
 * the text is not an OTLP logs payload at all. A record with an unreadable attribute (an int64 past 2^53 - 1) is REJECTED BY ITSELF and
 * named in `rejected`; its neighbours survive.
 */
export function decodeOtlpLogs(text) {
  let doc;
  try { doc = JSON.parse(text); } catch (e) { return fail(`not JSON: ${e.message}`); }
  if (!doc || !Array.isArray(doc.resourceLogs)) return fail('not an OTLP logs payload: no resourceLogs array');
  const out = [];
  const rejected = [];
  let index = 0;
  for (const rl of doc.resourceLogs) {
    for (const sl of rl.scopeLogs ?? []) {
      for (const rec of sl.logRecords ?? []) {
        const d = decodeRecord(rec, sl.scope?.name ?? null);
        if (d.ok) out.push(d.value); else rejected.push({ index, reason: d.reason });
        index += 1;
      }
    }
  }
  return { ...ok(out), rejected };
}

/** Does the exporter send numbers as strings ("1749236")? Decided by the capture (CAPTURE.md A-O1, 'attributes seen'): `api_request` counts
 * arrive as intValue and cost_usd as doubleValue, but `duration_ms` is an intValue on some events and a stringValue on others (the
 * tool_result records A-H2 reads), so a plain decimal string is accepted for a numeric attribute. False would read only OTLP int/double values. */
export const ACCEPT_NUMERIC_STRINGS = true;
const DECIMAL = /^-?\d+(\.\d+)?$/;

const asNumber = v => (ACCEPT_NUMERIC_STRINGS && typeof v === 'string' && DECIMAL.test(v) ? Number(v) : v);

function coerce(kind, raw) {
  const v = kind === 'string' || kind === 'bool' ? raw : asNumber(raw);
  if (kind === 'string') return nonEmpty(v);
  if (kind === 'bool') return typeof v === 'boolean' ? v : null;
  if (kind === 'count') return Number.isSafeInteger(v) && v >= 0 ? v : null;
  return typeof v === 'number' && Number.isFinite(v) ? v : null;
}

function readAttrs(spec, attrs, required) {
  const fields = {};
  const missing = [];
  for (const [name, { field, kind }] of Object.entries(spec)) {
    fields[field] = coerce(kind, attrs[name]);
    if (fields[field] === null && required(name)) missing.push(name);
  }
  return { fields, missing };
}

/** `api_request` records as `OReq[]`; an attribute the plan names but the record lacks is `null` plus its name in `missing`. */
export function apiRequests(records) {
  return records.filter(r => r.name === 'api_request').map(r => {
    const isSub = roleOf(r.attrs.query_source) === 'subagent';
    const { fields, missing } = readAttrs(API_REQUEST_ATTRS, r.attrs, n => !CONDITIONAL.includes(n) || (isSub && REQUIRED_FOR_SUBAGENT.includes(n)));
    const optional = readAttrs(OPTIONAL_API_REQUEST_ATTRS, r.attrs, () => false).fields;
    return { name: 'api_request', tsMs: r.tsMs, ...fields, ...optional, role: roleOf(fields.querySource), missing };
  });
}

/** `tool_result` records the same way (A-H2: `tool_use_id` and `duration_ms` are what S4 pairs by). */
export function toolResults(records) {
  return records.filter(r => r.name === 'tool_result').map(r => {
    const { fields, missing } = readAttrs(TOOL_RESULT_ATTRS, r.attrs, n => n !== 'success' && n !== 'tool_name');
    return { name: 'tool_result', tsMs: r.tsMs, ...fields, missing };
  });
}

const NEEDED = Object.freeze(['model', 'input', 'output', 'cacheRead', 'cacheCreation']);
const SUMMED = Object.freeze(['input', 'output', 'cacheRead', 'cacheCreation', 'cacheCreation5m', 'cacheCreation1h', 'splitRequests']);

const emptyBucket = () => ({
  input: 0, output: 0, cacheRead: 0, cacheCreation: 0, cacheCreation5m: 0, cacheCreation1h: 0, splitRequests: 0, costUsd: 0, costKnownRequests: 0, requests: 0,
});

/** Does the request carry a 5m/1h split that adds up to its cache write? (Then it can be priced exactly from O alone.) */
const hasSplit = r => r.cacheCreation5m !== null && r.cacheCreation1h !== null && r.cacheCreation5m + r.cacheCreation1h === r.cacheCreation;

function addTo(bucket, r) {
  const split = hasSplit(r);
  return {
    input: bucket.input + r.input, output: bucket.output + r.output, cacheRead: bucket.cacheRead + r.cacheRead, cacheCreation: bucket.cacheCreation + r.cacheCreation,
    cacheCreation5m: bucket.cacheCreation5m + (split ? r.cacheCreation5m : 0), cacheCreation1h: bucket.cacheCreation1h + (split ? r.cacheCreation1h : 0),
    splitRequests: bucket.splitRequests + (split ? 1 : 0),
    costUsd: bucket.costUsd + (r.costUsd ?? 0), costKnownRequests: bucket.costKnownRequests + (r.costUsd === null ? 0 : 1), requests: bucket.requests + 1,
  };
}

const mergeBuckets = (a, b) => ({
  ...Object.fromEntries(SUMMED.map(k => [k, a[k] + b[k]])), costUsd: a.costUsd + b.costUsd,
  costKnownRequests: a.costKnownRequests + b.costKnownRequests, requests: a.requests + b.requests,
});

const sameRequest = (a, b) => ['model', 'input', 'output', 'cacheRead', 'cacheCreation', 'costUsd'].every(k => a[k] === b[k]);

/** One record per request id (the first), the rest counted: an exporter that re-sends a batch must not double the sums. */
function dedupe(requests) {
  const first = new Map();
  const kept = [];
  let duplicates = 0;
  let conflicting = 0;
  for (const r of requests) {
    if (r.requestId === null || !first.has(r.requestId)) {
      if (r.requestId !== null) first.set(r.requestId, r);
      kept.push(r);
    } else {
      duplicates += 1;
      if (!sameRequest(first.get(r.requestId), r)) conflicting += 1;
    }
  }
  return { kept, duplicates, conflicting };
}

/**
 * Sums per model and ROLE (main / subagent / auxiliary / unknown) over records with every token field; the rest are counted in `incomplete`
 * and left out. A record without a role is summed under 'unknown', never dropped. Duplicate request ids are counted in `duplicates`.
 */
export function otelView(requests, { toolResults: tr = [], misconfigured = 0 } = {}) {
  const { kept, duplicates, conflicting } = dedupe(requests);
  const complete = kept.filter(r => NEEDED.every(k => r[k] !== null));
  const perModel = {};
  for (const r of complete) {
    const bySource = perModel[r.model]?.bySource ?? {};
    const role = r.role ?? 'unknown';
    perModel[r.model] = { bySource: { ...bySource, [role]: addTo(bySource[role] ?? emptyBucket(), r) } };
  }
  return {
    present: kept.length > 0, requests: kept, toolResults: tr, perModel, incomplete: kept.length - complete.length, duplicates, conflictingDuplicates: conflicting, misconfigured,
  };
}

/** One model's buckets summed over the given sources (default: all of them). */
export function sumSources(modelEntry, sources = null) {
  const buckets = Object.entries(modelEntry?.bySource ?? {}).filter(([s]) => sources === null || sources.includes(s)).map(([, b]) => b);
  return buckets.reduce(mergeBuckets, emptyBucket());
}
