// The G rows read from the twins, the rehearsal and the dated by-hand lines (contract section 10): G-4, G-7, G-9, G-10, G-12, G-13, G-14, and the T3 rule (section 11). `ctx.aa` are the twins' facts,
// `ctx.pilot` the Node pilot's, `ctx.smoke` the committed smoke fixture's B-T1 (`{ ok, cli, fixture, observed }`, or null).
import { combine, fromStatus, holds, latestOf, leaf, lineFor, needPilot, perSession, refuted, unknown } from './aa-check.mjs';

const mean = list => (list.length === 0 ? null : list.reduce((a, b) => a + b, 0) / list.length);

/** The T3 rule (section 11): `stop` only on established coverage: the main half on the A/A, and the subagent half on the A/A or in the committed smoke fixture on the same CLI. */
export const t3Rule = ({ main, subHere, subSmoke }) => (main && (subHere || subSmoke) ? 'stop' : 'fallback-t2');

// ---- G-4 --------------------------------------------------------------------------------------------------------------------

const launched = f => leaf(f.record, 'time', 'children')?.launched ?? 0;
const t34 = f => [fromStatus(leaf(f.record, 'cost', 'checks')?.t3?.status, 'T3'), fromStatus(leaf(f.record, 'cost', 'checks')?.t4?.status, 'T4')];
const firstOf = (checks, status) => checks.find(c => c.status === status);
/** The T3 / T4 pair as one check: refuted if either is, else unknown if either is, else holds. */
const t34Check = f => { const pair = t34(f); return firstOf(pair, 'refuted') ?? firstOf(pair, 'unknown') ?? holds('T3 and T4 pass'); };

/** The coverage the twins show: `{ checks, main, subHere }`. `main` holds when every twin's T3 and T4 pass; `subHere` when a twin that launched subagents passed them too. */
export function coverage(aa) {
  const checks = perSession(aa, t34Check);
  const main = checks.length > 0 && checks.every(c => c.status === 'holds');
  return { checks, main, subHere: main && aa.some(f => launched(f) > 0) };
}

const smokeOn = (ctx) => (ctx.smoke?.ok === true && ctx.smoke.cli === ctx.map.pins?.claude ? ctx.smoke : null);

/** G-4: the main half from the twins; the subagent half from the twins, or (`not exercised`) from the smoke fixture's B-T1 on the same CLI, named. */
export function g4(ctx) {
  const cov = coverage(ctx.aa);
  const base = { id: 'G-4', title: 'T3 and T4 over matched sources: main and subagent', ...combine(cov.checks), t3Rule: t3Rule({ main: cov.main, subHere: cov.subHere, subSmoke: smokeOn(ctx) !== null }) };
  if (base.verdict !== 'holds' || cov.subHere) return base;
  const smoke = smokeOn(ctx);
  return { ...base, verdict: 'not exercised', replacing: smoke ? `the subagent half: the E2.S2 smoke fixture's B-T1 on CLI ${smoke.cli} (${smoke.fixture}): ${smoke.observed}` : null, observed: `${base.observed}; no twin launched a subagent` };
}

// ---- G-7: record kinds and aliases ------------------------------------------------------------------------------------------

const known = (f, ...keys) => leaf(f.record, ...keys) !== undefined;

/** The record kinds a twin carries, `{ name: boolean }` (the list of G-7). */
function kindsOf(f, ctx) {
  const l = f.loaded;
  const quality = Object.values(f.record?.quality ?? {});
  return {
    stream: (l?.events ?? []).length > 0, envelope: (l?.events ?? []).some(e => e.type === 'result'), O: (l?.sink ?? []).length > 0, T: (l?.transcripts ?? []).length > 0, audit: (l?.audit ?? []).length > 0,
    broker: (l?.brokerLines ?? []).length > 0, egress: leaf(f.record, 'egress')?.bytesCaptured === true, cpuSamples: f.samples.length > 0, spin: known(f, 'run', 'spin'), packages: known(f, 'packages'),
    score: f.hasScore && quality.length > 0 && quality.every(q => q.known === true), ledger: ctx.ledger.some(line => line.rid === f.rid),
  };
}

/** D12: every alias a twin used resolves to the baseline's id (the twins may use different aliases). -> the problems. */
function aliasProblems(f, ctx) {
  const used = leaf(f.record, 'cost', 'aliases');
  const base = ctx.baseline?.aliases ?? {};
  if (!used) return [`${f.rid}: no alias record`];
  const drifted = Object.entries(used.aliases ?? {}).filter(([a, id]) => base[a] !== undefined && base[a] !== id).map(([a, id]) => `${a} -> ${id} (baseline ${base[a]})`);
  return [...drifted.map(d => `${f.rid}: alias drift: ${d}`), ...(used.check?.verdict === 'stop' ? [`${f.rid}: the alias check says stop`] : [])];
}

const missingKinds = (f, ctx) => Object.entries(kindsOf(f, ctx)).filter(([, there]) => !there).map(([k]) => k);

export function g7(ctx) {
  const kinds = ctx.aa.map(f => ({ rid: f.rid, missing: missingKinds(f, ctx) })).filter(k => k.missing.length > 0);
  const aliases = ctx.aa.flatMap(f => aliasProblems(f, ctx));
  const row = { id: 'G-7', title: 'the same record kinds for each twin; aliases per D12' };
  if (ctx.aa.length < 2) return { ...row, verdict: 'not exercised', observed: `the A/A needs two twins, found ${ctx.aa.length}` };
  const problems = [...kinds.map(k => `${k.rid} lacks ${k.missing.join(', ')}`), ...aliases];
  return { ...row, verdict: problems.length === 0 ? 'holds' : 'refuted', observed: problems.join('; ') || 'both twins carry stream, envelope, O, T, audit, broker, egress with bytes, CPU and spin samples, packages, a score with every step known and a ledger line; every alias resolves to the baseline', stopIfRefuted: aliases.length > 0 };
}

// ---- G-9: recorded, never gating ----------------------------------------------------------------------------------------------

const spread = (a, b) => (a > 0 && b > 0 ? Math.max(a, b) / Math.min(a, b) : null);
const maskCpus = mask => { const [from, to] = String(mask).split('-').map(Number); return Array.from({ length: to - from + 1 }, (_, i) => from + i); };

/** The mean CPU of the session's own mask over its window, or null. */
const cpuOfMask = f => mean(f.samples.map(s => mean(maskCpus(f.entry.mask).map(c => s.perCpu?.[c] ?? 0))).filter(x => x !== null));
const pick = (f, ...keys) => leaf(f.record, ...keys);
const diff = (a, b) => (typeof a === 'number' && typeof b === 'number' ? Math.abs(a - b) : null);

export function g9(ctx) {
  const row = { id: 'G-9', title: 'the twin spread, recorded, never gating' };
  if (ctx.aa.length < 2) return { ...row, verdict: 'not exercised', observed: `the A/A needs two twins, found ${ctx.aa.length}` };
  const [a, b] = ctx.aa;
  const recorded = {
    costRatio: spread(pick(a, 'cost', 'stopRuleUsd'), pick(b, 'cost', 'stopRuleUsd')), wallRatio: spread(pick(a, 'time', 'wallMs'), pick(b, 'time', 'wallMs')),
    modelApiRatio: spread(pick(a, 'time', 'modelApiMs'), pick(b, 'time', 'modelApiMs')), activeMinusModelApiRatio: spread(pick(a, 'time', 'activeMinusModelApiMs'), pick(b, 'time', 'activeMinusModelApiMs')),
    heldoutPercentDiff: diff(pick(a, 'quality', 'heldout')?.percent, pick(b, 'quality', 'heldout')?.percent), heldoutDefectsDiff: diff(pick(a, 'quality', 'heldout')?.defects?.count, pick(b, 'quality', 'heldout')?.defects?.count),
    cpuPerMask: Object.fromEntries(ctx.aa.map(f => [f.entry.mask, cpuOfMask(f)])),
  };
  return { ...row, verdict: 'recorded', recorded, observed: `${a.rid} against ${b.rid}: ${JSON.stringify(recorded)}` };
}

// ---- G-10, G-13: by hand ------------------------------------------------------------------------------------------------------

const lineCheck = (ctx, g, f) => {
  const line = lineFor(ctx, g, f.rid);
  if (!line) return unknown(`no dated ${g} line for ${f.rid}`);
  return line.verdict === 'refuted' ? refuted(`refuted by hand: ${line.evidence}`) : holds(`${line.verdict} by hand: ${line.evidence}`);
};

/** G-10: the scorer by hand on one twin's tree and on the Node tree: a dated line for the first twin that has one, and for the pilot. */
export function g10(ctx) {
  const twin = ctx.aa.find(f => lineFor(ctx, 'G-10', f.rid)) ?? ctx.aa[0];
  const checks = perSession([...(twin ? [twin] : []), ...ctx.pilot], f => lineCheck(ctx, 'G-10', f));
  return { id: 'G-10', title: 'the scorer by hand on a twin tree and the Node tree', ...combine([...checks, ...needPilot(ctx)]) };
}

/** G-13: each session's class read by hand: a dated line for every twin and the pilot. */
export function g13(ctx) {
  const checks = perSession([...ctx.aa, ...ctx.pilot], f => lineCheck(ctx, 'G-13', f));
  return { id: 'G-13', title: "each session's class read by hand", ...combine([...checks, ...needPilot(ctx)]) };
}

// ---- G-12 ---------------------------------------------------------------------------------------------------------------------

const capCheck = cap => f => {
  const at = leaf(f.record, 'cost', 'censoredAtUsd');
  return typeof at === 'number' && at >= cap ? holds(`stopped at $${at} (cap $${cap})`) : refuted(`stopped at ${at ?? 'an unknown figure'} (cap $${cap})`);
};

/** A dated by-hand `refuted` line for the session (the latest by date): the cap failed, as read by hand. */
const handRefusal = (f, ctx) => {
  const line = lineFor(ctx, 'G-12', f.rid);
  return line?.verdict === 'refuted' ? refuted(`refuted by hand: ${line.evidence}`) : null;
};

/** A session whose stop-rule figure reached the cap and whose class is not `killed: cap`: the cap did not stop it. */
const overCap = (f, cap) => {
  const figure = leaf(f.record, 'cost', 'stopRuleUsd');
  return typeof figure === 'number' && figure >= cap ? refuted(`the stop-rule figure $${figure} reached the cap $${cap} but the class is ${f.record?.class}, not killed: cap`) : null;
};

/** The check of a session that reached the cap (or that a dated line refutes), or null for a session that did not reach it. */
const capSession = (f, ctx, cap) => handRefusal(f, ctx) ?? (f.record?.class === 'killed: cap' ? capCheck(cap)(f) : overCap(f, cap));

const HEX12 = /(?<![0-9a-f])[0-9a-f]{12}(?![0-9a-f])/g;

/** The harness identity (12 hex) the run is at: the latest accepted instrument line, else the series' own. */
const runIdentity = ctx => latestOf(ctx.amendments.instrument)?.identity ?? ctx.map.harnessIdentity?.slice(0, 12);

/**
 * The replacing evidence of a G-12 nobody exercised (contract section 10: E2.S2's broker proof P-6 and the budget tests green at the run's harness identity): the latest dated G-12 `holds` line by DATE, whose evidence names
 * P-6 and the 12-hex harness identity the run is at. `{ replacing }` (the sentence) or `{ replacing: null, why }`.
 */
function capEvidence(ctx) {
  const line = latestOf(ctx.amendments.verdicts.filter(v => v.g === 'G-12' && v.verdict === 'holds'));
  if (!line) return { replacing: null, why: 'no dated G-12 holds line' };
  const identity = runIdentity(ctx);
  const problems = [...(/\bP-6\b/.test(line.evidence) ? [] : ['it does not name P-6']), ...((line.evidence.match(HEX12) ?? []).includes(identity) ? [] : [`it does not name the run's harness identity ${identity}`])];
  return problems.length === 0 ? { replacing: `dated G-12 line for ${line.rid}: ${line.evidence}` } : { replacing: null, why: `the latest dated G-12 line (${line.date}, ${line.rid}) is no evidence: ${problems.join(' and ')}` };
}

/**
 * G-12: a session whose stop-rule figure reached the cap must have been `killed: cap`, at settled spend of at least the cap; one that did not is refuted (a STOP), as is one a dated `refuted` line names. With no session at the cap
 * the row is `not exercised`, and its replacing evidence is the latest dated G-12 line (`capEvidence`).
 */
export function g12(ctx) {
  const cap = ctx.map.caps?.capUsd;
  const row = { id: 'G-12', title: 'the cap stops a session that reaches it at settled spend >= the cap', stopIfRefuted: true };
  const checks = [...ctx.aa, ...ctx.pilot].flatMap(f => { const c = capSession(f, ctx, cap); return c ? [{ rid: f.rid, ...c }] : []; });
  if (checks.length > 0) return { ...row, ...combine(checks) };
  const { replacing, why } = capEvidence(ctx);
  return { ...row, verdict: 'not exercised', observed: `no session reached the cap${why ? `; ${why}` : ''}`, replacing };
}

// ---- G-14 ---------------------------------------------------------------------------------------------------------------------

const stackLine = (r, s) => `${s} ${r.stacks?.[s]?.holds === true ? 'holds' : 'does not hold'}`;

/** G-14 from `rehearsal-v2.json` (the free v2 rehearsal wrote it, computed): both stacks must hold. */
export function g14(ctx) {
  const r = ctx.rehearsal;
  const row = { id: 'G-14', title: 'the v2 rehearsal for both stacks' };
  if (!r) return { ...row, verdict: 'not exercised', observed: 'no rehearsal-v2.json: run --what rehearse-v2' };
  const stacks = ['csharp', 'node'].map(s => stackLine(r, s));
  return { ...row, verdict: r.holds === true && stacks.every(s => s.endsWith(' holds')) ? 'holds' : 'refuted', observed: stacks.join('; ') };
}
