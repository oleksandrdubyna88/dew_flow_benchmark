// The exclusive work-before-wait partition of a session's wall, and the views that sit beside it (parent §10, E1.S4).
//
// partition: every elementary interval of the wall takes the highest-priority category active in it, across ALL agents, so a
// wait counts only where no agent is working: one child's `sleep 300` cannot erase another child's build. The categories
// (plus `unattributed`) sum to the wall to the millisecond. `active` is the sum of the WORK categories, never wall minus waits.
// Nothing returned here is a sum of SPANS: the inclusive view is a union, agent volume is labelled work volume, and the
// partition's buckets are exclusive. Span sources and their assumptions live in spans.mjs.
import { sweepSpans } from '../../policy-ab-quick/lib/parallel.mjs';
import { PRIORITY, UNATTRIBUTED, WAITS, WORK } from './categories.mjs';
import { intersectMerged, lengthOf, mergeIntervals } from './intervals.mjs';

export { AUDIT_TIME_FIELD, HOOK_TIME_SOURCE, OTEL_STAMP_MARKS, refineWaits, spansFrom } from './spans.mjs';

export const CLOCK_SKEW_LIMIT_MS = 1000;
export const TOOL_TOLERANCE_MS = 1000;
export const TOOL_TOLERANCE_FRACTION = 0.02;
export const VOLUME_LABEL = 'work volume: each agent\'s own union, added across agents; not elapsed time';

const unknown = why => ({ known: false, why });
const sumMs = (cats, names) => names.reduce((n, c) => n + cats[c], 0);
const zeroed = names => Object.fromEntries(names.map(c => [c, 0]));
const isPriority = c => PRIORITY.includes(c);

/** Whole-millisecond wall, or null when it cannot be one. */
function wallOf(wall) {
  const startMs = Math.round(wall?.startMs);
  const endMs = Math.round(wall?.endMs);
  return Number.isFinite(startMs) && Number.isFinite(endMs) && endMs > startMs ? { startMs, endMs } : null;
}

/**
 * Pure: the spans of partition categories that can be placed, clipped to `bounds` and rounded to whole ms; `excluded` counts the
 * spans of other categories (containers, unrefined waits, unknown names) and `ignored` the ones with unusable times.
 */
function place(spans, bounds) {
  const kept = [];
  const excluded = {};
  let ignored = 0;
  for (const s of spans) {
    if (!isPriority(s?.category)) { excluded[s?.category] = (excluded[s?.category] ?? 0) + 1; continue; }
    const startMs = Math.round(s.startMs);
    const endMs = Math.round(s.endMs);
    if (!Number.isFinite(startMs) || !Number.isFinite(endMs) || endMs < startMs) { ignored += 1; continue; }
    const clipped = { category: s.category, agentId: s.agentId ?? 'main', startMs: Math.max(startMs, bounds.startMs), endMs: Math.min(endMs, bounds.endMs) };
    if (clipped.endMs > clipped.startMs) kept.push(clipped);
  }
  return { kept, excluded, ignored };
}

function sweepCategories(kept, w) {
  const counts = zeroed(PRIORITY);
  const totals = zeroed([...PRIORITY, UNATTRIBUTED]);
  const winner = () => PRIORITY.find(c => counts[c] > 0) ?? UNATTRIBUTED;
  const events = kept.flatMap(s => [{ at: s.startMs, d: 1, c: s.category }, { at: s.endMs, d: -1, c: s.category }]).sort((a, b) => a.at - b.at);
  let cursor = w.startMs;
  for (const ev of events) {
    if (ev.at > cursor) { totals[winner()] += ev.at - cursor; cursor = ev.at; }
    counts[ev.c] += ev.d;
  }
  totals[winner()] += w.endMs - cursor;
  return totals;
}

/**
 * Pure: { known: true, wallMs, categories, active, waits, unattributed, excluded, ignoredSpans } or { known: false, why }.
 * `wall` is { startMs, endMs } (the launcher's spawn and exit, mapped to epoch with mapClock); spans are spans.mjs's.
 */
export function partition(wall, spans) {
  const w = wallOf(wall);
  if (!w) return unknown('the wall has no positive, finite length');
  const { kept, excluded, ignored } = place(spans, w);
  const categories = sweepCategories(kept, w);
  return Object.freeze({
    known: true,
    wallMs: w.endMs - w.startMs,
    categories,
    active: sumMs(categories, WORK),
    waits: sumMs(categories, WAITS),
    unattributed: categories[UNATTRIBUTED],
    excluded,
    ignoredSpans: ignored,
  });
}

const seconds = ms => `${(ms / 1000).toFixed(1)} s`;
const listOf = (cats, names) => names.map(c => `${c} ${seconds(cats[c])}`).join(', ');

/** The three numbers the report leads with, and the categories behind them. */
export function renderPartition(p) {
  if (!p.known) return `partition unknown: ${p.why}`;
  return [
    `wall ${seconds(p.wallMs)}`,
    `active ${seconds(p.active)} (${listOf(p.categories, WORK)})`,
    `waits ${seconds(p.waits)} (${listOf(p.categories, WAITS)})`,
    `unattributed ${seconds(p.unattributed)}`,
  ].join('\n');
}

// ---- inclusive views ------------------------------------------------------------------------------------------------------

const boundsOf = wall => wallOf(wall) ?? { startMs: -Infinity, endMs: Infinity };
const pairsOf = list => list.map(s => [s.startMs, s.endMs]);

/** Pure: per category, the time at least one span of it was active (a union, from sweepSpans). */
export function inclusive(spans, wall) {
  const byCategory = Map.groupBy(place(spans, boundsOf(wall)).kept, s => s.category);
  return Object.fromEntries(PRIORITY.map(c => [c, { unionMs: sweepSpans(pairsOf(byCategory.get(c) ?? [])).unionMs }]));
}

/** Pure: per category, each agent's OWN union added across agents. A work volume (agent-ms), labelled, not elapsed time. */
export function agentSeconds(spans, wall) {
  const byCategory = Map.groupBy(place(spans, boundsOf(wall)).kept, s => s.category);
  const volume = c => [...Map.groupBy(byCategory.get(c) ?? [], s => s.agentId).values()]
    .reduce((n, own) => n + sweepSpans(pairsOf(own)).unionMs, 0);
  return { label: VOLUME_LABEL, agentMs: Object.fromEntries(PRIORITY.map(c => [c, volume(c)])) };
}

/** Pure: pairwise intersection of the category unions; the diagonal is the category's own union. */
export function overlapMatrix(spans, wall) {
  const byCategory = Map.groupBy(place(spans, boundsOf(wall)).kept, s => s.category);
  const unions = Object.fromEntries(PRIORITY.map(c => [c, mergeIntervals(pairsOf(byCategory.get(c) ?? []))]));
  return Object.fromEntries(PRIORITY.map(a => [a, Object.fromEntries(PRIORITY.map(b => [b, lengthOf(intersectMerged(unions[a], unions[b]))]))]));
}

// ---- tool spans against OTel -----------------------------------------------------------------------------------------------

const toolIdOf = r => r?.toolUseId ?? r?.tool_use_id ?? null;
const toolMsOf = r => [r?.durationMs, r?.duration_ms].find(Number.isFinite) ?? null;
const allowedMs = otelMs => Math.round(Math.max(TOOL_TOLERANCE_MS, TOOL_TOLERANCE_FRACTION * otelMs));

/**
 * By tool_use_id, the audit span's length against OTel's tool_result duration: |delta| <= max(1000 ms, 2 % of OTel's).
 * Mismatches and tools seen by only one side are listed, never averaged away.
 */
export function crossCheckTools(toolSpans, otelToolResults) {
  if (!Array.isArray(otelToolResults) || otelToolResults.length === 0) return unknown('no otel tool_result records');
  const otel = new Map(otelToolResults.filter(r => toolIdOf(r)).map(r => [toolIdOf(r), toolMsOf(r)]));
  if (otel.size === 0) return unknown('otel tool_result has no tool_use_id');
  const audit = new Map(toolSpans.filter(s => s.toolUseId).map(s => [s.toolUseId, s.endMs - s.startMs]));
  const timed = [...otel.keys()].filter(id => otel.get(id) !== null);
  const both = [...audit.keys()].filter(id => timed.includes(id));
  const mismatches = both.map(id => ({ toolUseId: id, auditMs: audit.get(id), otelMs: otel.get(id), deltaMs: audit.get(id) - otel.get(id), allowedMs: allowedMs(otel.get(id)) }))
    .filter(m => Math.abs(m.deltaMs) > m.allowedMs);
  return {
    known: true,
    compared: both.length,
    mismatches,
    auditOnly: [...audit.keys()].filter(id => !otel.has(id)),
    otelOnly: timed.filter(id => !audit.has(id)),
    otelNoDuration: [...otel.keys()].filter(id => otel.get(id) === null),
  };
}

// ---- the launcher's clock --------------------------------------------------------------------------------------------------

/**
 * Maps the launcher's monotonic clock (ms) to epoch ms by the mapping taken at spawn. When the exit mapping is given too, the
 * two offsets differ by `skew.ms`; above CLOCK_SKEW_LIMIT_MS it is flagged (the wall clock stepped during the session).
 */
export function mapClock({ hrAtSpawn, epochAtSpawn, hrAtExit, epochAtExit } = {}) {
  if (!Number.isFinite(hrAtSpawn) || !Number.isFinite(epochAtSpawn)) return unknown('the mapping at spawn needs hrAtSpawn and epochAtSpawn');
  const offset = epochAtSpawn - hrAtSpawn;
  const toEpoch = hrMs => hrMs + offset;
  if (!Number.isFinite(hrAtExit) || !Number.isFinite(epochAtExit)) return { known: true, toEpoch, skew: unknown('no mapping taken at exit') };
  const ms = Math.abs((epochAtExit - hrAtExit) - offset);
  return { known: true, toEpoch, skew: { known: true, ms, flagged: ms > CLOCK_SKEW_LIMIT_MS } };
}
