// O: Claude Code's OTel `api_request` records, decoded from OTLP/HTTP JSON. Pure.
// Every attribute NAME the plan reads (E1.S3 row A-O1) is listed once, here. An attribute a record lacks is `null` with its name in
// `missing` - absent is not zero. The three constants below are the capture's decisions (A-O1, A-O2, A-O5): each is a one-line change.
import { fail, ok } from './result.mjs';

/** A-O2: does an `api_request` record's timestamp mark the END of the request? 'end' | 'start' | 'unknown'. UNVERIFIED until the
 * step 0 capture (test/fixtures/real/claude-2.1.310-capture) decides; `verdicts.mjs` computes it and a test pins this constant to it. */
export const OTEL_STAMP_MARKS = 'end';

/** A-O1: where a record's name lives, tried in order: the log body or the `event.name` attribute. A `claude_code.` prefix is dropped. */
export const RECORD_NAME_SOURCES = Object.freeze(['body', 'event.name']);
const NAME_PREFIX = 'claude_code.';

/** A-O5: attributes only a subagent's request carries (CLI >= 2.1.310); a main request without them is not `missing` them. */
export const REQUIRED_FOR_SUBAGENT = Object.freeze(['agent_id', 'parent_agent_id']);

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

/** OTLP logs JSON text -> `{ ok, value: [{ name, tsMs, attrs, scope }] }`, or `{ ok: false, reason }` (one unreadable attribute fails the payload). */
export function decodeOtlpLogs(text) {
  let doc;
  try { doc = JSON.parse(text); } catch (e) { return fail(`not JSON: ${e.message}`); }
  if (!doc || !Array.isArray(doc.resourceLogs)) return fail('not an OTLP logs payload: no resourceLogs array');
  const out = [];
  for (const rl of doc.resourceLogs) {
    for (const sl of rl.scopeLogs ?? []) {
      for (const rec of sl.logRecords ?? []) {
        const d = decodeRecord(rec, sl.scope?.name ?? null);
        if (!d.ok) return d;
        out.push(d.value);
      }
    }
  }
  return ok(out);
}

/** Does the exporter send numbers as strings ("1749236")? Unknown until the capture; true = accept a plain decimal string for a numeric
 * attribute (A-O1's `types` column in CAPTURE.md says which spelling arrived). False = only OTLP int/double values are numbers. */
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
    const isSub = r.attrs.query_source === 'subagent';
    const { fields, missing } = readAttrs(API_REQUEST_ATTRS, r.attrs, n => isSub || !REQUIRED_FOR_SUBAGENT.includes(n));
    const optional = readAttrs(OPTIONAL_API_REQUEST_ATTRS, r.attrs, () => false).fields;
    return { name: 'api_request', tsMs: r.tsMs, ...fields, ...optional, missing };
  });
}

/** `tool_result` records the same way (A-H2: `tool_use_id` and `duration_ms` are what S4 pairs by). */
export function toolResults(records) {
  return records.filter(r => r.name === 'tool_result').map(r => {
    const { fields, missing } = readAttrs(TOOL_RESULT_ATTRS, r.attrs, n => n !== 'success' && n !== 'tool_name');
    return { name: 'tool_result', tsMs: r.tsMs, ...fields, missing };
  });
}

const NEEDED = Object.freeze(['model', 'input', 'output', 'cacheRead', 'cacheCreation', 'querySource']);
const SUMMED = Object.freeze(['input', 'output', 'cacheRead', 'cacheCreation']);

const emptyBucket = () => ({ input: 0, output: 0, cacheRead: 0, cacheCreation: 0, costUsd: 0, costKnownRequests: 0, requests: 0 });

function addTo(bucket, r) {
  return {
    ...Object.fromEntries(SUMMED.map(k => [k, bucket[k] + r[k]])),
    costUsd: bucket.costUsd + (r.costUsd ?? 0), costKnownRequests: bucket.costKnownRequests + (r.costUsd === null ? 0 : 1), requests: bucket.requests + 1,
  };
}

const mergeBuckets = (a, b) => ({
  ...Object.fromEntries(SUMMED.map(k => [k, a[k] + b[k]])), costUsd: a.costUsd + b.costUsd,
  costKnownRequests: a.costKnownRequests + b.costKnownRequests, requests: a.requests + b.requests,
});

/** Sums per model and `query_source` over records with no missing token field; the rest are counted in `incomplete` and left out. */
export function otelView(requests, { toolResults: tr = [], misconfigured = 0 } = {}) {
  const complete = requests.filter(r => NEEDED.every(k => r[k] !== null));
  const perModel = {};
  for (const r of complete) {
    const bySource = perModel[r.model]?.bySource ?? {};
    perModel[r.model] = { bySource: { ...bySource, [r.querySource]: addTo(bySource[r.querySource] ?? emptyBucket(), r) } };
  }
  return { present: requests.length > 0, requests, toolResults: tr, perModel, incomplete: requests.length - complete.length, misconfigured };
}

/** One model's buckets summed over the given sources (default: all of them). */
export function sumSources(modelEntry, sources = null) {
  const buckets = Object.entries(modelEntry?.bySource ?? {}).filter(([s]) => sources === null || sources.includes(s)).map(([, b]) => b);
  return buckets.reduce(mergeBuckets, emptyBucket());
}
