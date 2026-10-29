// The A-* assumptions of E1.S3 as pure rules over a capture: each returns `{ verdict: 'holds' | 'refuted' | 'not exercised', observed }`.
// `observed` always carries the numbers or names the verdict rests on, so the table is read, not trusted.
import { envelopeView, share1h, streamView, transcriptView } from '../lib/claude-usage.mjs';
import { crosscheck, matchTO } from '../lib/crosscheck.mjs';
import { apiRequests, decodeOtlpLogs, otelView, toolResults } from '../lib/otel-decode.mjs';
import { asTheCliBills, priceFor, priceUsage } from '../lib/prices.mjs';
import { agentTimeline } from '../../policy-ab-quick/lib/parallel.mjs';
import { aliasRecord } from '../lib/aliases.mjs';

const holds = observed => ({ verdict: 'holds', observed });
const refuted = observed => ({ verdict: 'refuted', observed });
const notExercised = observed => ({ verdict: 'not exercised', observed });
const verdictOf = (ok, observed) => (ok ? holds(observed) : refuted(observed));
const sum = (xs, f) => xs.reduce((a, x) => a + f(x), 0);
const isJsonType = t => /^application\/json\s*(;|$)/i.test(t ?? '');
const near = (a, b, rel) => Math.abs(a - b) <= rel * Math.abs(b);

/** Everything the rules read, computed once from a loaded capture. */
export function viewsOf(capture, { table, policy }) {
  const logLines = capture.sink.filter(l => l.path === '/v1/logs');
  const decoded = logLines.map(l => ({ line: l, d: decodeOtlpLogs(l.body) }));
  const records = decoded.filter(x => x.d.ok).flatMap(x => x.d.value);
  const E = envelopeView(capture.events);
  const requests = apiRequests(records);
  const O = otelView(requests, { toolResults: toolResults(records) });
  return { ...capture, table, policy, E, S: streamView(capture.events), T: transcriptView(capture.transcripts), logLines, decoded, records, O, agents: agentTimeline(capture.events) };
}

export const RULES = Object.freeze({
  'A-E1': v => {
    if (!v.E.known) return refuted(`no usable envelope: ${v.E.why}`);
    const childModels = [...new Set([...v.agents.map(a => a.resolvedModel), ...v.S.messages.filter(m => m.source === 'subagent').map(m => m.model)].filter(Boolean))];
    if (childModels.length === 0) return notExercised('no subagent ran');
    const missing = childModels.filter(m => !(m in v.E.perModel));
    const total = sum(Object.values(v.E.perModel), m => m.output);
    const mainOut = v.E.main.known ? v.E.main.output : null;
    return verdictOf(missing.length === 0 && mainOut !== null && mainOut < total, `child models ${childModels.join(', ')}; absent from modelUsage: ${missing.join(', ') || 'none'}; usage.output_tokens ${mainOut} against sum of modelUsage output ${total}`);
  },
  'A-E2': v => {
    if (!v.E.known) return refuted(`no usable envelope: ${v.E.why}`);
    const total = sum(Object.values(v.E.perModel), m => m.costUsd);
    return verdictOf(Math.abs(total - v.E.totalCostUsd) <= 0.01, `sum of modelUsage costUSD ${total} against total_cost_usd ${v.E.totalCostUsd}: gap ${Math.abs(total - v.E.totalCostUsd)}`);
  },
  'A-S1': v => {
    const repeated = v.S.messages.filter(m => m.events > 1);
    if (repeated.length === 0) return notExercised('no message id appears more than once in the stream');
    const lastOut = new Map();
    for (const m of v.S.messages) lastOut.set(m.model, (lastOut.get(m.model) ?? 0) + (m.output ?? 0));
    const streamed = sum([...lastOut.values()], x => x);
    const billed = v.E.known ? sum(Object.values(v.E.perModel), m => m.output) : null;
    const ok = v.S.disagreements.length === 0 && billed !== null && streamed < billed;
    return verdictOf(ok, `${repeated.length} message id(s) repeated (max ${Math.max(...repeated.map(m => m.events))} events); ${v.S.disagreements.length} disagreement(s); streamed last-event output ${streamed} against modelUsage output ${billed}`);
  },
  'A-T1': v => {
    const t = v.T;
    const bySource = s => t.messages.filter(m => m.source === s);
    const stats = s => `${bySource(s).length} message(s), ${bySource(s).filter(m => m.splitKnown).length} with the 5m/1h split`;
    const complete = t.messages.length > 0 && t.messages.every(m => m.id && m.model && m.input !== null && m.splitKnown);
    return verdictOf(t.coverage.main && t.coverage.subagent && complete && t.skippedLines === 0,
      `${t.files} file(s); main: ${stats('main')}; subagent: ${stats('subagent')}; coverage ${JSON.stringify(t.coverage)}; unreadable lines ${t.skippedLines}`);
  },
  'A-T2': v => {
    if (!v.E.known || v.T.messages.length === 0) return refuted('needs both the envelope and the transcripts');
    const rows = Object.keys(v.E.perModel).map(m => ({ m, t: v.T.messages.filter(x => x.model === m), e: v.E.perModel[m].output })).filter(r => r.t.length > 0);
    const cells = rows.map(r => ({ ...r, tSum: r.t.some(x => x.output === null) ? null : sum(r.t, x => x.output) }));
    if (cells.length === 0) return notExercised('no model is in both the transcripts and the envelope');
    return verdictOf(cells.every(c => c.tSum !== null && near(c.tSum, c.e, 0.01)), cells.map(c => `${c.m}: transcript ${c.tSum} against modelUsage ${c.e}`).join('; '));
  },
  'A-J1': v => {
    if (!v.O.present) return refuted('no api_request records to join with');
    if (v.T.messages.length === 0) return refuted('no transcript messages to join');
    const m = matchTO(v.T, v.O, 'request_id');
    const strayO = Object.keys(m.unmatchedO).filter(s => s !== 'auxiliary');
    return verdictOf(m.unmatchedT.length === 0 && strayO.length === 0, `${v.T.messages.length} transcript message(s), ${m.unmatchedT.length} without an api_request; unmatched api_request by source ${JSON.stringify(m.unmatchedO)}`);
  },
  'A-O1': v => {
    const types = [...new Set(v.sink.map(l => l.contentType))];
    const undecodable = v.decoded.filter(x => !x.d.ok).map(x => x.d.reason);
    const missing = v.O.requests.flatMap(r => r.missing);
    const ok = v.logLines.length > 0 && v.sink.every(l => isJsonType(l.contentType)) && undecodable.length === 0 && v.O.present && missing.length === 0;
    return verdictOf(ok, `content types ${JSON.stringify(types)}; ${v.O.requests.length} api_request record(s); undecodable payloads ${undecodable.length}; missing attributes ${JSON.stringify([...new Set(missing)])}; attributes seen ${JSON.stringify(attributeKinds(v.logLines))}`);
  },
  'A-O2': v => stampRule(v),
  'A-O3': v => {
    const t2 = crosscheck({ E: v.E, O: v.O, T: null, table: v.table, policy: v.policy }).t2;
    return verdictOf(t2.status === 'pass', `T2 ${t2.status}: ${t2.detail}`);
  },
  'A-O4': v => {
    if (!v.E.known || !v.O.present) return refuted('needs the envelope and api_request records');
    const o = sum(v.O.requests, r => r.costUsd ?? 0);
    return verdictOf(near(o, v.E.totalCostUsd, 0.01), `sum of api_request cost_usd ${o} against total_cost_usd ${v.E.totalCostUsd}`);
  },
  'A-O5': v => {
    const subs = v.O.requests.filter(r => r.querySource === 'subagent');
    if (subs.length === 0) return notExercised('no api_request has query_source subagent');
    const ids = new Set(subs.map(r => r.agentId).filter(Boolean));
    const childIds = v.agents.map(a => a.agentId).filter(Boolean);
    const lacking = subs.filter(r => !r.agentId || !r.parentAgentId).length;
    return verdictOf(lacking === 0 && childIds.length > 0 && childIds.every(id => ids.has(id)), `${subs.length} subagent record(s), ${lacking} lacking agent_id/parent_agent_id; Agent result agentIds ${JSON.stringify(childIds)} against otel agent_ids ${JSON.stringify([...ids])}`);
  },
  'A-H1': v => {
    const hookEvents = v.events.filter(e => e.type === 'system' && (e.subtype === 'hook_started' || e.subtype === 'hook_response'));
    if (hookEvents.length === 0) return refuted('no hook_started/hook_response events in the stream');
    const stamped = hookEvents.filter(e => 'timestamp' in e).length;
    const toolIds = new Set(v.events.filter(e => e.type === 'assistant').flatMap(e => (e.message?.content ?? []).filter(b => b.type === 'tool_use').map(b => b.id)));
    const hookIds = [...new Set(v.hooks.map(h => h.tool_use_id).filter(Boolean))];
    const stray = hookIds.filter(id => !toolIds.has(id));
    const ok = hookEvents.every(e => e.hook_id) && stamped === 0 && stray.length === 0 && hookIds.length > 0;
    return verdictOf(ok, `${hookEvents.length} hook event(s), ${stamped} with a timestamp; ${hookIds.length} hook tool_use_id(s), ${stray.length} not in the stream`);
  },
  'A-H2': v => {
    const tr = v.O.toolResults;
    if (tr.length === 0) return refuted('no tool_result records with OTEL_LOG_TOOL_DETAILS=1');
    const lacking = tr.filter(r => r.missing.length > 0);
    return verdictOf(lacking.length === 0, `${tr.length} tool_result record(s), ${lacking.length} lacking ${JSON.stringify([...new Set(lacking.flatMap(r => r.missing))])}`);
  },
  'A-M1': v => aliasRule(v),
  'A-P1': v => billingRule(v),
});

/** attribute name -> the OTLP value kinds it arrived as, over every api_request record. */
function attributeKinds(logLines) {
  const kinds = {};
  for (const l of logLines) {
    let doc;
    try { doc = JSON.parse(l.body); } catch { continue; } // an undecodable body is reported by its own count
    const recs = (doc.resourceLogs ?? []).flatMap(rl => (rl.scopeLogs ?? []).flatMap(sl => sl.logRecords ?? []));
    for (const a of recs.flatMap(r => r.attributes ?? [])) kinds[a.key] = [...new Set([...(kinds[a.key] ?? []), Object.keys(a.value ?? {})[0] ?? 'none'])].sort();
  }
  return Object.fromEntries(Object.entries(kinds).sort(([a], [b]) => a.localeCompare(b)));
}

const SLACK_MS = 2000;

/** A-O2: for every api_request matched to a stream message by request id, is the stream's time near the record's time (end) or near
 * record + duration (start)? Only a request longer than 2 x slack can tell the two apart; the rest are listed, not counted as evidence.
 * `decision` is what `OTEL_STAMP_MARKS` must say: 'end', 'start', or 'unknown'. */
function stampRule(v) {
  const byReq = new Map(v.S.messages.filter(m => m.requestId).map(m => [m.requestId, m]));
  const marks = v.O.requests.filter(r => byReq.has(r.requestId) && r.tsMs !== null && r.durationMs !== null).map(r => markOf(r, byReq.get(r.requestId).lastTsMs));
  if (marks.length === 0) return { ...notExercised('no api_request matched a stream message by request id'), decision: 'unknown' };
  const count = m => marks.filter(x => x.mark === m).length;
  const decisive = marks.filter(x => x.decisive);
  const text = `${marks.length} matched: end ${count('end')}, start ${count('start')}, unknown ${count('unknown')}; ${decisive.length} longer than ${2 * SLACK_MS} ms (the only ones that separate end from start)`;
  if (decisive.length === 0) return { ...notExercised(`${text}: no request was long enough to tell`), decision: 'unknown' };
  const all = m => decisive.every(x => x.mark === m);
  const decision = all('end') ? 'end' : all('start') ? 'start' : 'unknown';
  return { ...verdictOf(decision === 'end', text), decision };
}

function markOf(r, streamAt) {
  const decisive = r.durationMs > 2 * SLACK_MS;
  if (streamAt === null) return { mark: 'unknown', decisive };
  const end = streamAt >= r.tsMs - r.durationMs - SLACK_MS && streamAt <= r.tsMs + SLACK_MS;
  const start = streamAt >= r.tsMs - SLACK_MS && streamAt <= r.tsMs + r.durationMs + SLACK_MS;
  return { mark: end ? 'end' : start ? 'start' : 'unknown', decisive };
}

function aliasRule(v) {
  const usage = v.events.findLast(e => e.type === 'result')?.modelUsage ?? {};
  const ids = [...new Set([v.events.find(e => e.type === 'system' && e.subtype === 'init')?.model, ...v.agents.map(a => a.resolvedModel), ...Object.keys(usage)].filter(Boolean))];
  const notPriced = ids.filter(id => !priceFor(v.table, id).known);
  const noCanonical = Object.entries(usage).filter(([, u]) => !u?.canonicalModel).map(([m]) => m);
  const rec = aliasRecord({ events: v.events });
  const fable = ids.some(i => /fable/.test(i));
  return verdictOf(ids.length > 0 && notPriced.length === 0 && noCanonical.length === 0 && rec.ok,
    `ids ${JSON.stringify(ids)}; not in the price table ${JSON.stringify(notPriced)}; modelUsage lines without canonicalModel ${JSON.stringify(noCanonical)}; aliases ${rec.ok ? JSON.stringify(rec.value.aliases) : rec.reason}; fable ${fable ? 'exercised' : 'not exercised'}`);
}

/** A-P1: each envelope model line reproduced at the CLI's rates (published for all but the observed ones), the split taken from the transcripts. */
function billingRule(v) {
  if (!v.E.known) return refuted(`no usable envelope: ${v.E.why}`);
  const cli = asTheCliBills(v.table);
  const rows = Object.entries(v.E.perModel).map(([id, u]) => billingRow(id, u, v, cli));
  const sonnetRead = rows.some(r => r.cliOnly && r.cacheRead > 0);
  const text = rows.map(r => `${r.id}: costUSD ${r.cost}, ${r.note}`).join('; ');
  if (rows.some(r => !r.fits)) return refuted(text);
  return sonnetRead ? holds(text) : notExercised(`${text}; no model with an observed CLI rate read from cache`);
}

function billingRow(id, u, v, cli) {
  const price = priceFor(v.table, id);
  if (!price.known) return { id, cost: u.costUsd, fits: false, note: price.why, cliOnly: false, cacheRead: u.cacheRead };
  const share = share1h(v.T, id);
  const base = { input: u.input, output: u.output, cacheRead: u.cacheRead };
  const tokens = u.cacheWrite === 0 || share.known
    ? { ...base, cacheWrite5m: u.cacheWrite - Math.round(u.cacheWrite * (share.value ?? 0)), cacheWrite1h: Math.round(u.cacheWrite * (share.value ?? 0)) }
    : { ...base, cacheWrite: u.cacheWrite, split: share };
  const at = p => { const r = priceUsage(p, tokens); return typeof r === 'number' ? { low: r, high: r } : r; };
  const pub = at(price.value);
  const bill = at(priceFor(cli, id).value);
  const within = ({ low, high }) => u.costUsd >= low - 1e-6 && u.costUsd <= high + 1e-6;
  const cliOnly = priceFor(cli, id).value.cacheRead !== price.value.cacheRead;
  return { id, cost: u.costUsd, cacheRead: u.cacheRead, cliOnly, fits: within(bill) && (!cliOnly || u.cacheRead === 0 || !within(pub)), note: `published ${fmtRange(pub)}, as the CLI bills ${fmtRange(bill)}` };
}

const fmtRange = ({ low, high }) => (low === high ? String(low) : `${low}..${high}`);
