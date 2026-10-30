// The G rows (contract section 10) that every session reads, the double-dagger rows: G-1, G-2, G-3, G-5, G-6, G-8, G-11. Each `(ctx) -> { verdict, observed, ... }`; `ctx.all` is the A/A twins and the Node
// pilot (the facts of each), `ctx.amendments` the parsed dated lines. A rule reads a session's record and the evidence file a record does not carry, and never types a number.
import { EGRESS_HOSTS, KNOWN_REFUSED } from './egress-hosts.mjs';
import { combine, fromProblems, fromStatus, holds, leaf, needPilot, perSession, refuted, tri, unknown, withExplain } from './aa-check.mjs';

const T5_TOLERANCE_USD = 0.000001;
const UNATTRIBUTED_MAX_SHARE = 0.05;
const keys = o => Object.keys(o ?? {});

const g1 = f => tri(f.loaded?.readback?.ok, { yes: 'the read-back is ok on every init field', no: `the read-back mismatched: ${JSON.stringify(f.loaded?.readback?.mismatches ?? 'no detail')}`, missing: 'no readback.json' });
const tCheck = t => f => fromStatus(leaf(f.record, 'cost', 'checks')?.[t]?.status, t.toUpperCase());

/** G-5: T5 passes and the broker's total is the envelope's within $0.000001. */
function g5(f) {
  const t5 = tCheck('t5')(f);
  const [broker, envelope] = [leaf(f.record, 'cost', 'brokerUsd'), leaf(f.record, 'cost', 'cliTotalUsd')];
  const apart = typeof broker === 'number' && typeof envelope === 'number' && Math.abs(broker - envelope) > T5_TOLERANCE_USD;
  return apart ? refuted(`B ${broker} is not E ${envelope} within $${T5_TOLERANCE_USD}; ${t5.note}`) : t5;
}

/** The stream's own wall in whole ms: the first to the last stamped line (what the partition is cut from), or null. */
function stampedWall(f) {
  const t = (f.loaded?.stamped ?? []).map(s => s.t);
  return t.length > 1 ? Math.round(Math.max(...t)) - Math.round(Math.min(...t)) : null;
}

const sumOf = categories => Object.values(categories).reduce((a, b) => a + b, 0);
const percent = share => `${(share * 100).toFixed(1)} %`;

const g6Problems = ({ sum, wall, share }) => [
  ...(sum === wall ? [] : [`the categories sum to ${sum} ms, the stamped wall is ${wall} ms`]),
  ...(share < UNATTRIBUTED_MAX_SHARE ? [] : [`unattributed is ${percent(share)} of the wall (limit 5 %)`]),
];

/** G-6: the categories sum to the stamped wall to the ms, and `unattributed` is under 5 % of it. */
function g6(f) {
  const part = leaf(f.record, 'time', 'partition');
  const wall = stampedWall(f);
  if (!part || wall === null) return unknown('no partition in the record or no stamped stream');
  const figures = { sum: sumOf(part.categories), wall, share: part.unattributed / wall };
  return fromProblems(g6Problems(figures), `sum ${figures.sum} ms = wall ${wall} ms; unattributed ${percent(figures.share)}`);
}

// ---- G-8: isolation ---------------------------------------------------------------------------------------------------------

const outsideList = (hosts, list) => keys(hosts).filter(h => !list.includes(h));
const nonZero = n => typeof n === 'number' && n > 0;

const hostProblems = egress => [
  ...outsideList(egress?.allowed, EGRESS_HOSTS.bare).map(host => ({ problem: `a CONNECT to ${host} outside the bare host list` })),
  ...[...outsideList(egress?.refused, KNOWN_REFUSED), ...outsideList(egress?.tlsRefused, KNOWN_REFUSED)].map(host => ({ problem: `a refused host ${host} outside the known-refused list`, host })),
  ...(nonZero(egress?.nonConnect) ? [{ problem: `${egress.nonConnect} non-CONNECT line(s) at the proxy` }] : []),
];

const realTokenSeen = f => nonZero(f.loaded?.meta?.samplers?.realInCmdline) || nonZero(f.loaded?.meta?.samplers?.realInEnviron) || f.record?.class === 'stop: security';

const leakProblems = f => [
  ...(realTokenSeen(f) ? [{ problem: 'the real token was counted' }] : []),
  ...['high', 'canary', 'tokenHits'].filter(k => nonZero(f.record?.detail?.[k])).map(k => ({ problem: `${f.record.detail[k]} ${k} contamination hit(s) (class ${f.record.class})` })),
];

/** The refusals of G-8 a session's own evidence shows, as `{ problem, host? }` (a refused host can be named by a dated line). */
const isolationProblems = f => [...hostProblems(leaf(f.record, 'egress')), ...leakProblems(f)];

/** G-8 for one session: the problems, each named; a refused host a dated line names does not count. */
function g8(f, ctx) {
  if (!leaf(f.record, 'egress')) return unknown('no egress summary in the record');
  const found = isolationProblems(f);
  const checks = found.map(p => withExplain({ rid: f.rid, ...refuted(p.problem) }, { ctx, g: 'G-8', needle: p.host ?? '\0' }));
  const open = checks.filter(c => c.status === 'refuted');
  if (open.length > 0) return refuted(open.map(c => c.note).join(', '));
  return checks.length > 0 ? { status: 'explained', note: checks.map(c => c.note).join(', ') } : holds('allowed CONNECTs within the bare list, refused within the known list, the real token 0, no contamination, canary or token hit');
}

// ---- G-11: the contention control ---------------------------------------------------------------------------------------------

/** The sampler's gaps: the samples of the session's window, at most twice the registered interval apart, each with a spin probe figure. */
function samplerProblems(f, intervalMs) {
  const t = f.samples.map(s => s.t);
  const gaps = t.slice(1).map((x, i) => x - t[i]);
  return [
    ...(f.samples.length < 2 ? [`${f.samples.length} sampler sample(s) in the session's window`] : []),
    ...(Math.max(0, ...gaps) > 2 * intervalMs ? [`a gap of ${Math.max(...gaps)} ms between samples (the interval is ${intervalMs} ms)`] : []),
    ...(f.samples.some(s => typeof s.spinMs !== 'number') ? ['a sample without the spin probe'] : []),
  ];
}

function g11(f, ctx) {
  const quiet = leaf(f.record, 'run', 'quiet');
  const problems = [
    ...(leaf(f.record, 'run', 'maskObserved') === true ? [] : ['the mask was not observed']),
    ...(quiet?.quiet === true ? [] : ['the quiet check did not pass']),
    ...(leaf(f.record, 'run', 'spin') ? [] : ['no spin record']),
    ...samplerProblems(f, ctx.quiet.intervalMs ?? 5000),
  ];
  return fromProblems(problems, 'the quiet check passed, the sampler and the spin probe wrote every interval, the mask was observed');
}

const T = Object.freeze({
  'G-1': 'the read-back is ok on every init field', 'G-2': 'T1', 'G-3': 'T2 per model and kind', 'G-5': 'T5: B = E within $0.000001',
  'G-6': 'partition sum = wall and unattributed < 5 % (or explained)', 'G-8': 'isolation: hosts, the real token 0, no contamination', 'G-11': 'quiet check, sampler, spin probe, mask observed',
});

const rowOf = (id, checks, extra = {}) => ({ id, title: T[id], ...combine(checks), ...extra });
const over = (ctx, fn) => [...perSession(ctx.all, f => fn(f, ctx)), ...needPilot(ctx)];

/** The per-session rows, keyed by G-id. */
export const SESSION_ROWS = Object.freeze({
  'G-1': ctx => rowOf('G-1', over(ctx, g1)),
  'G-2': ctx => rowOf('G-2', over(ctx, tCheck('t1')), { stopIfRefuted: true }),
  'G-3': ctx => rowOf('G-3', over(ctx, tCheck('t2'))),
  'G-5': ctx => rowOf('G-5', over(ctx, f => withExplain({ rid: f.rid, ...g5(f) }, { ctx, g: 'G-5' }))),
  'G-6': ctx => rowOf('G-6', over(ctx, f => withExplain({ rid: f.rid, ...g6(f) }, { ctx, g: 'G-6' }))),
  'G-8': ctx => rowOf('G-8', over(ctx, g8), { stopIfRefuted: true }),
  'G-11': ctx => rowOf('G-11', over(ctx, g11)),
});
