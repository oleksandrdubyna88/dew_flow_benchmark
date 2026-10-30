// The Claude counters' pure core: E (the result envelope), S (the stream), T (the transcripts) and the headline USD.
// Rules this file keeps: tokens come from E.modelUsage, never from `usage` (main loop only); the stream prices nothing but a
// labelled estimate (its output_tokens are placeholders); a count that is absent is null, never 0; a price the table lacks is unknown.
// It reads the envelope through `resultEnvelope` and calls nothing from the fitted-rate module (T-C1 scans for it).
import { resultEnvelope } from '../../policy-ab-quick/lib/summary.mjs';
import { priceFor, priceUsage } from './prices.mjs';

const MODEL_USAGE_FIELDS = Object.freeze({ input: 'inputTokens', output: 'outputTokens', cacheRead: 'cacheReadInputTokens', cacheWrite: 'cacheCreationInputTokens', costUsd: 'costUSD' });
const uint = n => (Number.isSafeInteger(n) && n >= 0 ? n : null);
const finiteOrNull = n => (typeof n === 'number' && Number.isFinite(n) ? n : null);
const tsMs = iso => (typeof iso === 'string' && Number.isFinite(Date.parse(iso)) ? Date.parse(iso) : null);

// ---- E: the result envelope -------------------------------------------------------------------------------------------------

function modelLine(id, usage) {
  const line = {};
  for (const [kind, field] of Object.entries(MODEL_USAGE_FIELDS)) {
    const v = kind === 'costUsd' ? finiteOrNull(usage?.[field]) : uint(usage?.[field]);
    if (v === null) return { bad: `modelUsage['${id}'].${field} is missing or not a count` };
    line[kind] = v;
  }
  return { line };
}

/** The main loop's own tokens, from the envelope's `usage` (never summed into a total). */
function mainTokens(usage) {
  if (!usage || typeof usage !== 'object') return { known: false, why: 'the result envelope has no usage block' };
  const base = { input: uint(usage.input_tokens), output: uint(usage.output_tokens), cacheRead: uint(usage.cache_read_input_tokens) };
  const cc = usage.cache_creation;
  const w5 = uint(cc?.ephemeral_5m_input_tokens);
  const w1 = uint(cc?.ephemeral_1h_input_tokens);
  if (w5 !== null && w1 !== null) return { known: true, ...base, cacheWrite5m: w5, cacheWrite1h: w1 };
  return { known: true, ...base, cacheWrite: uint(usage.cache_creation_input_tokens), split: { known: false, why: 'usage.cache_creation is absent' } };
}

/** E from stream events -> `{ known: true, totalCostUsd, durationMs, durationApiMs, numTurns, perModel, main } | { known: false, why }`. */
export function envelopeView(events) {
  const env = resultEnvelope(events);
  if (!env.present) return { known: false, why: 'no result envelope' };
  if (finiteOrNull(env.total_cost_usd) === null) return { known: false, why: 'the result envelope has no total_cost_usd' };
  const entries = Object.entries(env.modelUsage ?? {});
  if (entries.length === 0) return { known: false, why: 'the result envelope has no modelUsage: a session with no model line is unknown, not free' };
  const lines = entries.map(([id, u]) => [id, modelLine(id, u)]);
  const bad = lines.find(([, l]) => l.bad);
  if (bad) return { known: false, why: bad[1].bad };
  const usage = events.findLast(e => e.type === 'result')?.usage;
  return {
    known: true, totalCostUsd: env.total_cost_usd, durationMs: finiteOrNull(env.duration_ms), durationApiMs: finiteOrNull(env.duration_api_ms),
    numTurns: finiteOrNull(env.num_turns), perModel: Object.fromEntries(lines.map(([id, l]) => [id, l.line])), main: mainTokens(usage),
  };
}

// ---- S and T: messages grouped by id ----------------------------------------------------------------------------------------

function cacheSplit(usage) {
  const w5 = uint(usage?.cache_creation?.ephemeral_5m_input_tokens);
  const w1 = uint(usage?.cache_creation?.ephemeral_1h_input_tokens);
  return w5 !== null && w1 !== null ? { w5, w1 } : null;
}

function eventFields(item) {
  const u = item.ev.message?.usage ?? {};
  const split = cacheSplit(u);
  return {
    input: uint(u.input_tokens), cacheRead: uint(u.cache_read_input_tokens), output: uint(u.output_tokens),
    cacheWrite: uint(u.cache_creation_input_tokens) ?? (split ? split.w5 + split.w1 : null),
    cacheWrite5m: split ? split.w5 : null, cacheWrite1h: split ? split.w1 : null, splitKnown: split !== null,
    model: item.ev.message?.model ?? null, source: item.source, tsMs: tsMs(item.ev.timestamp),
  };
}

const AGREE_FIELDS = Object.freeze(['input', 'cacheRead', 'cacheWrite', 'model', 'source']);

function disagreementsOf(id, fields) {
  return AGREE_FIELDS.flatMap(field => {
    const values = [...new Set(fields.map(f => f[field]))];
    return values.length > 1 ? [{ id, field, values }] : [];
  });
}

/** One message from its events: the LAST event stands for it; a duplicate that disagrees is reported, never silently picked. */
function messageOf(id, items, placeholder) {
  const fields = items.map(eventFields);
  const last = fields.at(-1);
  const lastEv = items.at(-1).ev;
  const msg = {
    id, model: last.model, source: last.source, agentId: items.at(-1).agentId, requestId: lastEv.request_id ?? lastEv.requestId ?? null,
    input: last.input, output: last.output, cacheRead: last.cacheRead, cacheWrite: last.cacheWrite, cacheWrite5m: last.cacheWrite5m,
    cacheWrite1h: last.cacheWrite1h, splitKnown: last.splitKnown, outputPlaceholder: placeholder, events: items.length,
    firstTsMs: fields[0].tsMs, lastTsMs: last.tsMs,
  };
  return { msg, disagreements: disagreementsOf(id, fields) };
}

function groupMessages(items, placeholder) {
  const groups = new Map();
  items.forEach((item, i) => {
    const id = item.ev.message?.id ?? null;
    const key = id ?? `\0anonymous-${i}`;
    groups.set(key, [...(groups.get(key) ?? []), item]);
  });
  const built = [...groups.entries()].map(([key, group]) => messageOf(key.startsWith('\0') ? null : key, group, placeholder));
  return { messages: built.map(b => b.msg), disagreements: built.flatMap(b => b.disagreements) };
}

const isAssistant = ev => ev?.type === 'assistant' && ev.message && typeof ev.message === 'object';

/** S: the stream's assistant events grouped by `message.id`; output is always a placeholder. */
export function streamView(events) {
  const items = events.filter(isAssistant).map(ev => ({ ev, source: ev.parent_tool_use_id ? 'subagent' : 'main', agentId: null }));
  return groupMessages(items, true);
}

/** The estimate that watches a live stream: published rates, output floored per message, labelled. Never a headline. */
export function streamMeterUsd(view, table, { outputFloor } = {}) {
  if (!Number.isSafeInteger(outputFloor) || outputFloor < 0) return { known: false, why: 'the stream meter needs an outputFloor (a non-negative integer per message)' };
  const priced = view.messages.map(m => meterLine(m, table, outputFloor));
  return {
    usd: priced.reduce((a, p) => a + (p.usd ?? 0), 0), estimate: true, basis: `published prices; stream output floored to ${outputFloor} per message`,
    unpriced: priced.filter(p => p.usd === undefined).map(p => p.id), lowerBound: priced.some(p => p.usd === undefined),
  };
}

function meterLine(m, table, outputFloor) {
  const price = priceFor(table, m.model);
  if (!price.known || [m.input, m.cacheRead, m.cacheWrite].includes(null)) return { id: m.id };
  const tokens = { input: m.input, output: Math.max(m.output ?? 0, outputFloor), cacheRead: m.cacheRead };
  const usd = m.splitKnown
    ? priceUsage(price.value, { ...tokens, cacheWrite5m: m.cacheWrite5m, cacheWrite1h: m.cacheWrite1h })
    : priceUsage(price.value, { ...tokens, cacheWrite: m.cacheWrite, split: { known: false, why: 'no 5m/1h split' } }).high;
  return { id: m.id, usd };
}

const normal = relPath => relPath.replaceAll('\\', '/');
const AGENT_FILE = /(?:^|\/)agent-([^/]+)\.jsonl$/;

function parseJsonl(text) {
  const out = { lines: [], skipped: 0 };
  for (const raw of text.split('\n')) {
    if (raw.trim() === '') continue;
    try { out.lines.push(JSON.parse(raw)); } catch { out.skipped += 1; }
  }
  return out;
}

/** T from `files = [{ relPath, text }]` (what the collector's walk found): main + `subagents/*.jsonl`. */
export function transcriptView(files) {
  const parsed = files.map(f => {
    const rel = normal(f.relPath);
    const sub = rel.includes('/subagents/');
    return { rel, source: sub ? 'subagent' : 'main', agentId: sub ? AGENT_FILE.exec(rel)?.[1] ?? null : null, ...parseJsonl(f.text) };
  });
  const items = parsed.flatMap(p => p.lines.filter(isAssistant).map(ev => ({ ev, source: p.source, agentId: p.agentId })));
  const grouped = groupMessages(items, false);
  const has = source => items.some(i => i.source === source);
  return {
    ...grouped, coverage: { main: has('main'), subagent: has('subagent') }, files: files.length,
    skippedLines: parsed.reduce((a, p) => a + p.skipped, 0),
  };
}

/** Σ1h / Σ(5m + 1h) over a model's SPLIT messages. Unknown when there is none; a measured zero is `{ known: true, value: 0 }`. */
export function share1h(t, model) {
  const split = (t?.messages ?? []).filter(m => m.model === model && m.splitKnown);
  if (split.length === 0) return { known: false, why: `no split message for ${model}` };
  const w1 = split.reduce((a, m) => a + m.cacheWrite1h, 0);
  const all = split.reduce((a, m) => a + m.cacheWrite5m + m.cacheWrite1h, 0);
  if (all === 0) return { known: false, why: `the split messages for ${model} wrote no cache` };
  return { known: true, value: w1 / all };
}

// ---- the headline -----------------------------------------------------------------------------------------------------------

/** The 5m/1h cache writes the transcripts' SPLIT messages account for, per model. */
function splitWritesOf(T, model) {
  const msgs = (T?.messages ?? []).filter(m => m.model === model && m.splitKnown);
  return { w5: msgs.reduce((a, m) => a + m.cacheWrite5m, 0), w1: msgs.reduce((a, m) => a + m.cacheWrite1h, 0) };
}

const exact = (usd, share, basis) => ({ known: true, exact: true, usd, low: usd, high: usd, share1h: share, basis });

/**
 * A cache write is priced exactly only where its 5m/1h split is KNOWN FOR EVERY TOKEN of it: the exporter's own split (`u.otelSplit`), or
 * transcripts that account for all of the envelope's writes. What the transcripts do not cover is an interval - a lost child file never
 * lets its tokens inherit the main session's share.
 */
function line(id, u, T, table) {
  const price = priceFor(table, id);
  if (!price.known) return { known: false, why: price.why };
  const share = share1h(T, id);
  const base = { input: u.input, output: u.output, cacheRead: u.cacheRead };
  if (u.cacheWrite === 0) return exact(priceUsage(price.value, { ...base, cacheWrite5m: 0, cacheWrite1h: 0 }), share, 'no cache writes: nothing to split');
  if (u.otelSplit) return exact(priceUsage(price.value, { ...base, cacheWrite5m: u.otelSplit.w5, cacheWrite1h: u.otelSplit.w1 }), share, 'exact 5m/1h split from the otel cache_creation_5m/1h_tokens');
  const known = splitWritesOf(T, id);
  const rest = u.cacheWrite - known.w5 - known.w1;
  const covered = rest < 0 ? { w5: 0, w1: 0 } : known;
  const left = rest < 0 ? u.cacheWrite : rest;
  const at = (w5, w1) => priceUsage(price.value, { ...base, cacheWrite5m: covered.w5 + w5, cacheWrite1h: covered.w1 + w1 });
  if (left === 0) return exact(at(0, 0), share, `5m/1h split from transcripts (1h share ${share.value}); they account for every cache-write token`);
  const low = at(left, 0);
  const high = at(0, left);
  return {
    known: true, exact: false, usd: low, low, high, share1h: share,
    basis: `transcripts account for ${rest < 0 ? 0 : u.cacheWrite - left} of ${u.cacheWrite} cache-write tokens${rest < 0 ? ' (they hold more than the envelope: ignored)' : ''}: the other ${left} are an interval, every one at 5m to every one at 1h`,
  };
}

/** Defects of T the reader must see beside any figure priced from it. */
function transcriptFlags(T) {
  const ids = new Set((T?.disagreements ?? []).map(d => d.id));
  return [
    ...(ids.size > 0 ? [`${ids.size} transcript message id(s) whose duplicates disagree`] : []),
    ...((T?.skippedLines ?? 0) > 0 ? [`${T.skippedLines} unreadable transcript line(s)`] : []),
  ];
}

/** The session's USD at published list prices -> `{ known: true, usd, low, high, exact, basis, perModel, flags } | { known: false, why, perModel? }`. */
export function headlineUsd(E, T, table) {
  if (!E?.known) return { known: false, why: 'no envelope' };
  const perModel = Object.fromEntries(Object.entries(E.perModel).map(([id, u]) => [id, line(id, u, T, table)]));
  const lines = Object.values(perModel);
  const unknown = Object.entries(perModel).filter(([, l]) => !l.known);
  if (unknown.length > 0) return { known: false, why: `unpriced model(s) in the envelope: ${unknown.map(([id]) => id).join(', ')} (${unknown[0][1].why})`, perModel };
  const low = lines.reduce((a, l) => a + l.low, 0);
  const high = lines.reduce((a, l) => a + l.high, 0);
  const basis = `published list prices (${table.readOn}); tokens from the result envelope; 5m/1h split from transcripts`;
  return { known: true, usd: lines.reduce((a, l) => a + l.usd, 0), low, high, exact: lines.every(l => l.exact), basis, perModel, flags: transcriptFlags(T) };
}
