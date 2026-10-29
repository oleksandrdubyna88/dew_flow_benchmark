// Where each time span comes from (E1.S4 step 4). A span is { category, startMs, endMs, agentId, source, ... } in epoch ms.
//
// Two facts here are ASSUMED until E1.S3's step 0 produces a real capture; each is a named constant with a test that pins it,
// so the capture's answer is a one-line change:
//   OTEL_STAMP_MARKS  an OTel `api_request` record's time is the END of the request ('end'), so the span is [ts - duration, ts].
//                     'start' -> [ts, ts + duration]; 'unknown' -> model_api is "not captured" (E1.S3 plan A-O2). When S3's
//                     otel-decode.mjs lands, import its OTEL_STAMP_MARKS in place of this constant.
//   HOOK_TIME_SOURCE  hook_started/hook_response lines carry NO timestamp (verified on the two-children fixture: hook_id only),
//                     so the runner's ARRIVAL stamp of each line is the time ('arrival'); 'event' reads line.timestamp instead.
// Assumed shapes, not yet produced by their writers: the audit hook record { ts, event, tool_use_id, tool_name, agent_id, input }
// (E2.S1 extends hooks/audit.mjs), the OReq of E1.S3 { tsMs, durationMs, agentId }, the stamped line { t (epoch ms), line },
// and GitHub's intervals { ciIntervals, reviewIntervals } of [{ startMs, endMs }] (E3's github.mjs).
import { CI_WAIT, CODERABBIT_WAIT, HOOKS, MODEL_API, WAIT } from './categories.mjs';
import { classifyTool } from './classify.mjs';
import { intersectMerged, lengthOf, mergeIntervals } from './intervals.mjs';

export const OTEL_STAMP_MARKS = 'end';
export const HOOK_TIME_SOURCE = 'arrival';
export const AUDIT_TIME_FIELD = 'ts';

const MAIN = 'main';
const PRE = 'PreToolUse';
const POST_EVENTS = new Set(['PostToolUse', 'PostToolUseFailure']);
const part = (spans = [], notes = [], unpaired = []) => ({ spans, notes, unpaired });
const finite = n => (Number.isFinite(n) ? n : null);
const instantOf = v => (typeof v === 'number' ? finite(v) : finite(Date.parse(v ?? '')));

// ---- OTel api_request -> model_api -----------------------------------------------------------------------------------

const durationOf = r => [r.durationMs, r.duration_ms, r.attrs?.duration_ms].find(Number.isFinite) ?? null;

function modelApiSpan(r, marks) {
  const ts = finite(r.tsMs);
  const duration = durationOf(r);
  if (ts === null || duration === null || duration < 0) return null;
  const [startMs, endMs] = marks === 'start' ? [ts, ts + duration] : [ts - duration, ts];
  return { category: MODEL_API, startMs, endMs, agentId: r.agentId ?? MAIN, source: 'otel' };
}

function otelPart(otel, marks) {
  const requests = Array.isArray(otel) ? otel : otel?.requests;
  if (!Array.isArray(requests) || otel?.present === false) return part([], ['otel: none provided; model_api spans are not captured']);
  if (marks !== 'end' && marks !== 'start') return part([], [`otel: OTEL_STAMP_MARKS is '${marks}', so model_api spans are not captured`]);
  const built = requests.map(r => modelApiSpan(r, marks));
  const missing = built.filter(b => b === null).length;
  const notes = missing > 0 ? [`otel: ${missing} api_request record(s) lack a timestamp or duration_ms and are not model_api spans`] : [];
  return part(built.filter(Boolean), notes);
}

// ---- audit Pre/Post -> tool spans --------------------------------------------------------------------------------------

function toolSpan(id, pre, post) {
  const startMs = instantOf(pre[AUDIT_TIME_FIELD]);
  const endMs = instantOf(post[AUDIT_TIME_FIELD]);
  if (startMs === null || endMs === null || endMs < startMs) return { unpaired: { kind: 'tool', id, missing: 'readable, ordered timestamps' } };
  return {
    span: {
      category: classifyTool(pre.tool_name, pre.input), startMs, endMs, agentId: pre.agent_id ?? MAIN, source: 'audit',
      toolUseId: id, tool: pre.tool_name, failed: post.event === 'PostToolUseFailure',
    },
  };
}

function auditPart(audit) {
  if (!Array.isArray(audit)) return part([], ['audit: none provided; tool spans are not captured']);
  const pres = new Map();
  const posts = new Map();
  const unpaired = [];
  for (const r of audit) {
    if (r.event !== PRE && !POST_EVENTS.has(r.event)) continue;
    if (!r.tool_use_id) { unpaired.push({ kind: 'tool', id: null, missing: 'tool_use_id' }); continue; }
    (r.event === PRE ? pres : posts).set(r.tool_use_id, r);
  }
  const spans = [];
  for (const id of new Set([...pres.keys(), ...posts.keys()])) {
    if (!pres.has(id)) unpaired.push({ kind: 'tool', id, missing: PRE });
    else if (!posts.has(id)) unpaired.push({ kind: 'tool', id, missing: 'PostToolUse' });
    else {
      const built = toolSpan(id, pres.get(id), posts.get(id));
      if (built.span) spans.push(built.span); else unpaired.push(built.unpaired);
    }
  }
  return part(spans, [], unpaired);
}

// ---- stream hook_started/hook_response -> hooks -----------------------------------------------------------------------

function parseStamped(stamped) {
  let broken = 0;
  const events = [];
  for (const { t, line } of stamped) {
    let obj = line;
    if (typeof line === 'string') {
      try { obj = JSON.parse(line); } catch { broken += 1; continue; }
    }
    if (obj?.type === 'system' && (obj.subtype === 'hook_started' || obj.subtype === 'hook_response')) events.push({ t, line: obj });
  }
  return { events, broken };
}

const hookTime = (e, source) => (source === 'event' ? instantOf(e.line.timestamp) : finite(e.t));

function hookSpan(id, start, end, source) {
  const startMs = hookTime(start, source);
  const endMs = hookTime(end, source);
  if (startMs === null || endMs === null || endMs < startMs) return { unpaired: { kind: 'hook', id, missing: 'ordered times' } };
  return { span: { category: HOOKS, startMs, endMs, agentId: MAIN, source: 'stream.hooks', label: start.line.hook_name ?? id } };
}

function hooksPart(stamped, source) {
  if (!Array.isArray(stamped)) return part([], ['stamped: none provided; hooks spans are not captured']);
  const { events, broken } = parseStamped(stamped);
  const starts = new Map(events.filter(e => e.line.subtype === 'hook_started').map(e => [e.line.hook_id, e]));
  const ends = new Map(events.filter(e => e.line.subtype === 'hook_response').map(e => [e.line.hook_id, e]));
  const spans = [];
  const unpaired = [];
  for (const id of new Set([...starts.keys(), ...ends.keys()])) {
    if (!starts.has(id)) unpaired.push({ kind: 'hook', id, missing: 'hook_started' });
    else if (!ends.has(id)) unpaired.push({ kind: 'hook', id, missing: 'hook_response' });
    else {
      const built = hookSpan(id, starts.get(id), ends.get(id), source);
      if (built.span) spans.push(built.span); else unpaired.push(built.unpaired);
    }
  }
  const notes = broken > 0 ? [`stamped: ${broken} stamped stream line(s) were not JSON and were skipped`] : [];
  return part(spans, notes, unpaired);
}

// ---- coai rounds --------------------------------------------------------------------------------------------------------

function roundsPart(rounds) {
  if (!rounds) return part([], ['coai rounds: none provided; round spans are not captured']);
  if (!rounds.known) return part([], [`coai rounds unavailable: ${rounds.why}`]);
  return part(rounds.spans, (rounds.skipped ?? []).map(s => `coai rounds: skipped ${s}`));
}

// ---- generic waits -> ci_wait / coderabbit_wait --------------------------------------------------------------------

const asPairs = list => mergeIntervals((list ?? []).map(i => [i.startMs, i.endMs]));

function waitPieces(span, ci, review) {
  const own = mergeIntervals([[span.startMs, span.endMs]]);
  const inCi = intersectMerged(own, ci);
  const inReview = intersectMerged(own, review);
  const piece = category => ([startMs, endMs]) => ({ ...span, category, startMs, endMs });
  return { pieces: [...inCi.map(piece(CI_WAIT)), ...inReview.map(piece(CODERABBIT_WAIT))], covered: lengthOf(mergeIntervals([...inCi, ...inReview])) === lengthOf(own) };
}

/**
 * A generic `wait` span becomes ci_wait where it meets GitHub's in-progress CI intervals and coderabbit_wait where it meets a
 * push -> review interval; the rest of it, and every wait when GitHub's intervals are absent, stays unattributed (and the notes say so).
 */
export function refineWaits(spans, github) {
  const waits = spans.filter(s => s.category === WAIT);
  const rest = spans.filter(s => s.category !== WAIT);
  if (waits.length === 0) return part(rest);
  if (!github || (!Array.isArray(github.ciIntervals) && !Array.isArray(github.reviewIntervals))) {
    return part(rest, [`${waits.length} wait span(s) stay unattributed: no GitHub intervals were provided`]);
  }
  const ci = asPairs(github.ciIntervals);
  const review = asPairs(github.reviewIntervals);
  const refined = waits.map(w => waitPieces(w, ci, review));
  const partial = refined.filter(r => !r.covered).length;
  const notes = partial > 0 ? [`${partial} wait span(s) had time outside any GitHub interval; that time stays unattributed`] : [];
  return part([...rest, ...refined.flatMap(r => r.pieces)], notes);
}

/**
 * Every span source in one list, plus `notes` (what each missing or partial source means for the report) and `unpaired`
 * (tool and hook events whose other half never arrived). A source that is absent is a note, never an empty success.
 */
export function spansFrom({ otel, audit, stamped, rounds, github, otelStampMarks = OTEL_STAMP_MARKS, hookTimeSource = HOOK_TIME_SOURCE } = {}) {
  const parts = [otelPart(otel, otelStampMarks), auditPart(audit), hooksPart(stamped, hookTimeSource), roundsPart(rounds)];
  const refined = refineWaits(parts.flatMap(p => p.spans), github);
  return {
    spans: refined.spans.sort((a, b) => a.startMs - b.startMs || a.endMs - b.endMs),
    notes: [...parts.flatMap(p => p.notes), ...refined.notes],
    unpaired: parts.flatMap(p => p.unpaired),
  };
}
