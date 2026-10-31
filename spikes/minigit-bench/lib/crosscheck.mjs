// T1-T5: the Claude counters checked against each other. Pure: a `stop` is a value the caller acts on; nothing here exits a process.
//   T1  E against itself          T2  O (all sources) against E          T3  T at the CLI's rates against O's cost
//   T5  the broker's ledger B against E (E2.S2 D-17): both price the same tokens from the same table
//   T4  T's input/cache tokens against the O records T names (E cannot be cut by source, so T is never compared with E).
// The join (A-J1) and T3's output source (A-T2) are read from the policy file, so the capture's result is a one-line change there.
import { sumSources } from './otel-decode.mjs';
import { asTheCliBills, priceFor, priceUsage } from './prices.mjs';
import { fail, ok } from './result.mjs';

const T1_ABS = 0.01;
const T2_REL = 0.005;
const T2_MIN_TOKENS = 100;
const T2_COST_REL = 0.01;
const T3_REL = 0.02; // ScoutCost.cs:187
const T4_REL = 0.001;
const T5_ABS = 0.000001;
const JOIN_SOURCES = Object.freeze(['main', 'subagent']);

const LEGAL = Object.freeze({
  t1: ['stop', 'investigate'], t5: ['stop', 'investigate'], t2: ['otel-incomplete', 'stop'], t3: ['investigate', 'stop', 'fallback-t2'],
  t3OutputSource: ['transcript', 'otel'], join: ['request_id', 'query_source'],
});

/** Validate the policy file -> `{ ok, value } | { ok: false, reason }`; an unknown value fails naming the legal ones. */
export function loadPolicy(json) {
  if (!json || typeof json !== 'object') return fail('the crosscheck policy is a JSON object');
  for (const [key, legal] of Object.entries(LEGAL)) {
    if (!legal.includes(json[key])) return fail(`policy.${key} is '${json[key]}'; legal values: ${legal.join(', ')}`);
  }
  if (!/^\d{4}-\d{2}-\d{2}$/.test(json.decidedOn ?? '')) return fail('policy.decidedOn is a date (YYYY-MM-DD)');
  if (typeof json.reason !== 'string' || json.reason.trim() === '') return fail('policy.reason says why the values are what they are');
  if (json.t3 === 'fallback-t2' && json.t2 !== 'stop') return fail("policy.t3 is 'fallback-t2' but policy.t2 is not 'stop': T3 would be informational and nothing would stop the series");
  return ok(Object.freeze({ ...json }));
}

// ---- Check shapes -----------------------------------------------------------------------------------------------------------

/** A check that could not run. `byEnding`: the session's own ending explains it (a request the cut-short session never settled), so under a `stop` policy it is not a stop (review N11). */
export const unchecked = (id, why, { byEnding = false } = {}) => ({ id, status: 'unchecked', verdict: 'unchecked', computed: null, reference: null, gap: null, threshold: null, detail: why, why, ...(byEnding ? { byEnding: true } : {}) });

function decided(id, { computed, reference, threshold, pass, onBreach, detail, why, table }) {
  const gap = Math.abs(computed - reference);
  const base = { id, computed, reference, gap, threshold, detail, ...(table ? { table } : {}), ...(why ? { why } : {}) };
  return pass ? { ...base, status: 'pass', verdict: 'ok' } : { ...base, status: 'breach', verdict: onBreach };
}

const fmt = n => (Number.isInteger(n) ? String(n) : n.toPrecision(7));

// ---- T1 ---------------------------------------------------------------------------------------------------------------------

function t1(E, policy) {
  if (!E?.known) return unchecked('T1', 'no envelope');
  const computed = Object.values(E.perModel).reduce((a, m) => a + m.costUsd, 0);
  const gap = Math.abs(computed - E.totalCostUsd);
  return decided('T1', {
    computed, reference: E.totalCostUsd, threshold: T1_ABS, pass: gap <= T1_ABS, onBreach: policy.t1,
    detail: `sum of modelUsage costUSD ${fmt(computed)} against total_cost_usd ${fmt(E.totalCostUsd)}: gap ${fmt(gap)} (limit ${T1_ABS})`,
  });
}

// ---- T2 ---------------------------------------------------------------------------------------------------------------------

const T2_KINDS = Object.freeze([['input', 'input'], ['output', 'output'], ['cacheRead', 'cacheRead'], ['cacheCreation', 'cacheWrite']]);

function t2Cells(E, O) {
  const models = [...new Set([...Object.keys(E.perModel), ...Object.keys(O.perModel)])];
  return Object.fromEntries(models.map(m => {
    const e = E.perModel[m];
    const o = sumSources(O.perModel[m]);
    const cells = Object.fromEntries(T2_KINDS.map(([ok_, ek]) => {
      const ev = e?.[ek] ?? 0;
      const threshold = Math.max(T2_REL * ev, T2_MIN_TOKENS);
      return [ok_, { o: o[ok_], e: ev, gap: Math.abs(o[ok_] - ev), threshold, ok: Math.abs(o[ok_] - ev) <= threshold }];
    }));
    const eCost = e?.costUsd ?? 0;
    cells.costUsd = { o: o.costUsd, e: eCost, gap: Math.abs(o.costUsd - eCost), threshold: T2_COST_REL * eCost, ok: Math.abs(o.costUsd - eCost) <= T2_COST_REL * eCost };
    return [m, { cells, costUnknown: o.requests - o.costKnownRequests }];
  }));
}

function t2(E, O, policy) {
  if (!E?.known) return unchecked('T2', 'no envelope');
  if (!O?.present) return unchecked('T2', 'no otel');
  const table = t2Cells(E, O);
  const bad = Object.entries(table).flatMap(([m, { cells }]) => Object.entries(cells).filter(([, c]) => !c.ok).map(([k, c]) => `${m} ${k}: otel ${fmt(c.o)} against envelope ${fmt(c.e)}`));
  const computed = Object.values(table).reduce((a, m) => a + m.cells.costUsd.o, 0);
  const reference = Object.values(table).reduce((a, m) => a + m.cells.costUsd.e, 0);
  const costUnknown = Object.values(table).reduce((a, m) => a + m.costUnknown, 0);
  if (bad.length === 0 && (O.incomplete > 0 || costUnknown > 0)) {
    return unchecked('T2', `${O.incomplete} otel records lack a token field and ${costUnknown} lack cost_usd: the sums are lower bounds, not a pass`);
  }
  const gaps = Object.entries(table).map(([m, { cells }]) => `${m}: ${Object.entries(cells).map(([k, c]) => `${k} gap ${fmt(c.gap)}`).join(', ')}`).join('; ');
  return decided('T2', {
    computed, reference, threshold: T2_COST_REL * reference, pass: bad.length === 0, onBreach: policy.t2 === 'stop' ? 'stop' : 'otel incomplete',
    detail: bad.length === 0 ? gaps : `otel does not match the envelope - ${bad.join('; ')}`, table: Object.fromEntries(Object.entries(table).map(([m, v]) => [m, v.cells])),
  });
}

// ---- the T <-> O join --------------------------------------------------------------------------------------------------------

const countBy = reqs => reqs.reduce((acc, r) => ({ ...acc, [r.role ?? 'unknown']: (acc[r.role ?? 'unknown'] ?? 0) + 1 }), {});

/** Which O records T names. `request_id`: exactly those whose id a transcript message carries. `query_source`: the main and subagent ROLES (roleOf maps the CLI's raw spellings). */
export function matchTO(T, O, join) {
  const reqs = O?.requests ?? [];
  const msgs = T?.messages ?? [];
  if (join === 'request_id') {
    const tIds = new Set(msgs.map(m => m.requestId).filter(Boolean));
    const oIds = new Set(reqs.map(r => r.requestId).filter(Boolean));
    const matched = reqs.filter(r => r.requestId && tIds.has(r.requestId));
    return { by: join, matched, unmatchedT: msgs.filter(m => !m.requestId || !oIds.has(m.requestId)), unmatchedO: countBy(reqs.filter(r => !matched.includes(r))) };
  }
  const matched = reqs.filter(r => JOIN_SOURCES.includes(r.role));
  return { by: join, matched, unmatchedT: [], unmatchedO: countBy(reqs.filter(r => !matched.includes(r))) };
}

function sharedRequestIds(T) {
  const counts = Map.groupBy(T.messages.filter(m => m.requestId), m => m.requestId);
  return [...counts.values()].filter(group => group.length > 1).length;
}

function transcriptTrouble(T) {
  const ids = new Set(T.disagreements.map(d => d.id));
  if (ids.size > 0) return `the transcripts hold ${ids.size} message id(s) whose duplicates disagree`;
  if (T.skippedLines > 0) return `${T.skippedLines} unreadable transcript line(s): T may be incomplete`;
  return null;
}

function gate34(T, O, M) {
  if (!T || T.messages.length === 0) return 'no transcripts';
  if (!O?.present) return 'no otel';
  if (M.by === 'query_source' && !T.coverage.subagent) return 'T covers only main';
  const n = M.unmatchedT.length;
  if (n > 0) return `otel incomplete: ${n} transcript message${n === 1 ? '' : 's'} ${n === 1 ? 'has' : 'have'} no api_request`;
  const stray = Object.entries(M.unmatchedO).filter(([role]) => role !== 'auxiliary').reduce((a, [, count]) => a + count, 0);
  if (stray > 0) return `T incomplete: ${stray} main/subagent api_request records have no transcript message`;
  const shared = sharedRequestIds(T);
  if (shared > 0) return `${shared} request id(s) shared by more than one transcript message: the join is ambiguous`;
  const trouble = transcriptTrouble(T);
  if (trouble) return trouble;
  if ((O.conflictingDuplicates ?? 0) > 0) return `otel has ${O.conflictingDuplicates} conflicting duplicate api_request record(s): the same request id with different counts`;
  const incomplete = M.matched.filter(r => r.missing.length > 0 || r.model === null);
  if (incomplete.length > 0) return `otel incomplete: ${incomplete.length} matched api_request records lack ${[...new Set(incomplete.flatMap(r => r.missing))].join(', ')}`;
  const unread = T.messages.find(m => [m.input, m.cacheRead, m.cacheWrite].includes(null));
  return unread ? `transcript message ${unread.id} has no input or cache count` : null;
}

const byModel = (items, key) => Map.groupBy(items, i => i[key]);
const sum = (xs, f) => xs.reduce((a, x) => a + f(x), 0);

// ---- T3 ---------------------------------------------------------------------------------------------------------------------

/** Output tokens of one model's T messages: the transcript's own, or the matched O record's (A-T2). Null when it cannot be read. */
function outputOf(msgs, M, source) {
  if (source === 'transcript') return msgs.some(m => m.output === null) ? null : sum(msgs, m => m.output);
  if (M.by === 'request_id') {
    const byReq = new Map(M.matched.map(r => [r.requestId, r]));
    return sum(msgs, m => byReq.get(m.requestId).output);
  }
  return sum(M.matched.filter(r => r.model === msgs[0].model), r => r.output);
}

function t3Line(model, msgs, M, cli, source) {
  const price = priceFor(cli, model);
  if (!price.known) return { why: price.why };
  if (msgs.some(m => !m.splitKnown)) return { why: `no 5m/1h split on the transcript messages of ${model}` };
  const output = outputOf(msgs, M, source);
  if (output === null) return { why: `a transcript message of ${model} has no output_tokens` };
  const usd = priceUsage(price.value, {
    input: sum(msgs, m => m.input), output, cacheRead: sum(msgs, m => m.cacheRead),
    cacheWrite5m: sum(msgs, m => m.cacheWrite5m), cacheWrite1h: sum(msgs, m => m.cacheWrite1h),
  });
  return { usd };
}

function t3(T, O, M, table, policy, cliVersion) {
  const gate = gate34(T, O, M);
  if (gate) return unchecked('T3', gate);
  if (M.matched.some(r => r.costUsd === null)) return unchecked('T3', 'otel incomplete: a matched api_request has no cost_usd');
  const lines = [...byModel(T.messages, 'model')].map(([model, msgs]) => [model, t3Line(model, msgs, M, asTheCliBills(table, cliVersion), policy.t3OutputSource)]);
  const refused = lines.find(([, l]) => l.why);
  if (refused) return unchecked('T3', refused[1].why);
  const computed = sum(lines, ([, l]) => l.usd);
  const reference = sum(M.matched, r => r.costUsd);
  const fallback = policy.t3 === 'fallback-t2';
  return decided('T3', {
    computed, reference, threshold: T3_REL * reference, pass: Math.abs(computed - reference) <= T3_REL * reference, onBreach: policy.t3 === 'stop' ? 'stop' : 'investigate',
    detail: `T at the CLI's rates (output from ${policy.t3OutputSource}) ${fmt(computed)} against the cost_usd of the ${M.matched.length} matched api_request records ${fmt(reference)}`,
    ...(fallback ? { why: 'policy.t3 is fallback-t2: T3 is informational and T2 is the stop' } : {}),
  });
}

// ---- T4 ---------------------------------------------------------------------------------------------------------------------

const T4_KINDS = Object.freeze([['input', 'input', 'input'], ['cacheRead', 'cacheRead', 'cacheRead'], ['cacheWrite', 'cacheCreation', 'cacheWrite']]);

function t4Table(T, M) {
  const tBy = byModel(T.messages, 'model');
  const oBy = byModel(M.matched, 'model');
  return Object.fromEntries([...new Set([...tBy.keys(), ...oBy.keys()])].map(model => [model, Object.fromEntries(T4_KINDS.map(([kind, ok_, tk]) => {
    const t = sum(tBy.get(model) ?? [], m => m[tk]);
    const o = sum(oBy.get(model) ?? [], r => r[ok_]);
    return [kind, { t, o, gap: Math.abs(t - o), threshold: T4_REL * o, ok: Math.abs(t - o) <= T4_REL * o }];
  }))]));
}

function t4(T, O, M, policy) {
  const gate = gate34(T, O, M);
  if (gate) return unchecked('T4', gate);
  const table = t4Table(T, M);
  const cells = Object.entries(table).flatMap(([m, ks]) => Object.entries(ks).map(([k, c]) => ({ m, k, ...c })));
  const bad = cells.filter(c => !c.ok);
  const computed = sum(cells, c => c.t);
  const reference = sum(cells, c => c.o);
  return decided('T4', {
    computed, reference, threshold: T4_REL * reference, pass: bad.length === 0, onBreach: 'investigate', table,
    detail: bad.length === 0 ? `T equals the matched otel records on every model and kind (${cells.length} cells)` : `T differs from the matched otel records - ${bad.map(c => `${c.m} ${c.k}: T ${c.t} against O ${c.o}`).join('; ')}`,
  });
}

// ---- T5 ---------------------------------------------------------------------------------------------------------------------

/** B (the broker's ledger, `brokerView`) against E. With an unsettled request B is the price of what a stopped session did spend, not a comparison. */
function t5(E, B, policy) {
  if (!E?.known) return unchecked('T5', 'no envelope');
  if (!B?.present) return unchecked('T5', 'no broker ledger');
  if (B.unsettled > 0) return unchecked('T5', `${B.unsettled} request(s) never settled: the session was stopped mid-request, so B (${fmt(B.spentUsd)}) is the price of what it did spend and E is not comparable`, { byEnding: true });
  return decided('T5', {
    computed: B.spentUsd, reference: E.totalCostUsd, threshold: T5_ABS, pass: Math.abs(B.spentUsd - E.totalCostUsd) <= T5_ABS, onBreach: policy.t5,
    detail: `the broker's ledger ${fmt(B.spentUsd)} against total_cost_usd ${fmt(E.totalCostUsd)} (limit ${T5_ABS}); ${B.forwarded} request(s) forwarded, ${B.refused} refused`,
  });
}

// ---- series stops -----------------------------------------------------------------------------------------------------------

/** The checks whose policy value can be `stop` (T4 has no policy: it only investigates). One rule for all of them (review N3): a breach under `stop` is a SERIES stop, and so is a check that could not run (N11). */
const STOPPING_CHECKS = Object.freeze(['t1', 't2', 't3', 't5']);
/** The series-stop rule names of the checks, as the run-map's `stopped.rule` and the flags carry them: `T<n> breach`, then `T<n> unchecked`. */
export const CHECK_STOP_RULES = Object.freeze([...STOPPING_CHECKS.map(k => `${k.toUpperCase()} breach`), ...STOPPING_CHECKS.map(k => `${k.toUpperCase()} unchecked`)]);

/** The class prefixes that NAME an ending (review MINOR-7): a session that ended as one of these and wrote no envelope has no evidence to be missing. `crashed: no result` names no ending and is not one. */
const NAMED_ENDINGS = /^(killed|void|stop):/;

/**
 * `[{ rule, check, detail }]` for every check of `c` (the `crosscheck` result) that stops the series under `policy` (`crosscheck-policy.json`): a breach under a `stop` policy (verdict `stop`), and
 * FAIL CLOSED (review N11) a check whose policy is `stop` that could not run for want of evidence (`unchecked`: no OTel, a transcript gap, no broker ledger). Not a stop: an unchecked check whose
 * session ended with no envelope (`enveloped` false) AND whose class `ended` is one of `killed:*`, `void:*` and `stop:*` (killed, never answered, cancelled: the class says how it ended and there is
 * no evidence to be missing; `crashed: no result` is not exempt), or whose gap the ending explains (`byEnding`).
 */
export const seriesStopsOf = (c, policy = {}, { enveloped = true, ended = '' } = {}) => STOPPING_CHECKS.flatMap(k => {
  const check = c[k];
  const id = k.toUpperCase();
  if (check?.verdict === 'stop' && check.status === 'breach') return [{ rule: `${id} breach`, check: id, detail: check.detail }];
  const explained = !enveloped && NAMED_ENDINGS.test(ended);
  const missing = check?.status === 'unchecked' && policy[k] === 'stop' && !explained && check.byEnding !== true;
  return missing ? [{ rule: `${id} unchecked`, check: id, detail: check.detail }] : [];
});

/** The five checks and the join that fed T3/T4. `cliVersion` selects the CLI-billed rates (none = published). `B` is the broker's ledger (optional). */
export function crosscheck({ E, O, T, B = null, table, policy, cliVersion }) {
  const M = matchTO(T, O, policy.join);
  const join = { by: M.by, matched: M.matched.length, unmatchedT: M.unmatchedT.length, unmatchedO: M.unmatchedO };
  return { t1: t1(E, policy), t2: t2(E, O, policy), t3: t3(T, O, M, table, policy, cliVersion), t4: t4(T, O, M, policy), t5: t5(E, B, policy), join };
}
