// The records of a bare session and of a pair (E3b.S1 step 9). Pure: the runner computes the views (E1's readers over the session's files) and the score, and
// hands them here; nothing is typed. Every leaf is `{ known: true, value }`, `{ known: false, why }` (not captured, with the reason) or the string
// 'n/a (bare)' for a process-only field: an unknown is never rendered as a zero. A pair record gives each ratio WITH and WITHOUT the cells that hold a
// `ran unpaired` session, and a ratio with a censored side is a BOUND.
import { hundredPercent } from './score-pipeline.mjs';
import { stopRuleFigure } from './spend-gate.mjs';

export const NOT_BARE = 'n/a (bare)';
export const known = value => ({ known: true, value });
export const notCaptured = why => ({ known: false, why });
const finite = n => typeof n === 'number' && Number.isFinite(n);
const maybe = (v, why) => (v === null || v === undefined || (typeof v === 'number' && !Number.isFinite(v)) ? notCaptured(why) : known(v));

const costPart = ({ outcome, views, cost, meta }) => {
  const E = views.E;
  const B = views.B;
  return {
    headlineUsd: cost.known ? known({ usd: cost.usd, basis: cost.basis ?? null, exact: cost.exact ?? null, flags: cost.flags ?? [] }) : notCaptured(cost.why),
    cliTotalUsd: E.known ? known(E.totalCostUsd) : notCaptured(E.why),
    brokerUsd: B?.present ? known(B.spentUsd) : notCaptured('no broker ledger'),
    stopRuleUsd: maybe(stopRuleFigure({ envelopeUsd: E.known ? E.totalCostUsd : null, recomputeUsd: cost.known ? cost.usd : null, brokerUsd: B?.present ? B.spentUsd : null }), 'no cost figure of any basis'),
    censoredAtUsd: known(outcome.censoredAtUsd ?? null),
    perModel: cost.known && cost.perModel ? known(cost.perModel) : notCaptured(cost.known ? 'the cost view has no per-model split' : cost.why),
    tokens: E.known ? known(Object.fromEntries(Object.entries(E.perModel).map(([m, l]) => [m, { input: l.input, output: l.output, cacheRead: l.cacheRead, cacheWrite: l.cacheWrite ?? null }]))) : notCaptured(E.why),
    firstTurnCacheRead: maybe(views.S.messages.find(m => m.source === 'main')?.cacheRead, 'no main assistant message in the stream'),
    checks: known(Object.fromEntries(['t1', 't2', 't3', 't4', 't5'].map(k => [k, { status: views.c[k].status, verdict: views.c[k].verdict }]))),
    metaBrokerOvershootUsd: maybe(meta.broker?.overshootUsd, 'the launch recorded no overshoot'),
  };
};

const timePart = ({ views, meta }) => {
  const E = views.E;
  const part = views.part;
  const modelApi = E.known ? E.durationApiMs : null;
  const active = part.known ? part.active : null;
  const assistant = new Set(views.S.messages.filter(m => m.source === 'main').map(m => m.id)).size;
  return {
    wallMs: maybe(meta.wallMs, 'the launch recorded no wall'),
    modelApiMs: maybe(modelApi, 'no envelope: no model_api'),
    activeMs: maybe(active, part.known ? '' : part.why),
    activeMinusModelApiMs: finite(active) && finite(modelApi) ? known(active - modelApi) : notCaptured('needs the partition and the envelope'),
    partition: part.known ? known({ categories: part.categories, unattributed: part.unattributed, waits: part.waits }) : notCaptured(part.why),
    descriptive: part.known ? known({ build_test: part.categories.build_test, other_tool: part.categories.other_tool, note: 'descriptive only: a chained Bash call counts as its highest category (V16)' }) : notCaptured(part.why),
    turns: E.known ? known({ envelope: E.numTurns, assistantMessages: assistant, agree: E.numTurns === assistant }) : notCaptured(E.why),
    children: known({ launched: views.agents.length, perModel: Object.fromEntries(Object.entries(Object.groupBy(views.agents, a => a.resolvedModel ?? a.requestedModel ?? 'unresolved')).map(([m, l]) => [m, l.length])) }),
  };
};

/** The held-out measures; a failed build never ran the suite (`{ known: true, primary100: false, reason }`, no totals), so its percent is null, never 0. */
const heldoutOf = h => (h.totals
  ? { percent: h.totals.percent, defects: h.defects, blocked: h.totals.blocked, failed: h.totals.failed, total: h.totals.total, primary100: h.primary100 }
  : { percent: null, defects: null, blocked: null, failed: null, total: null, primary100: h.primary100, reason: h.reason ?? 'the suite reported no totals' });

const qualityPart = score => {
  if (!score) return { buildOk: notCaptured('the session was not scored'), visible: notCaptured('the session was not scored'), heldout: notCaptured('the session was not scored'), upstreamOriginal: notCaptured('the session was not scored'), latency: notCaptured('the session was not scored') };
  const h = score.heldout;
  return {
    buildOk: known(score.build.ok),
    visible: known(score.visible.map(v => ({ script: v.script, passed: v.passed, failed: v.failed, expectedTotal: v.expectedTotal, hundredPercent: hundredPercent(v) }))),
    heldout: h.known ? known(heldoutOf(h)) : notCaptured(h.why),
    upstreamOriginal: known(score.upstream.map(v => ({ script: v.script, passed: v.passed, expectedTotal: v.expectedTotal }))),
    latency: h.known && h.latency ? (h.latency.known === false ? notCaptured(h.latency.why) : known(h.latency)) : notCaptured(h.known ? `the held-out suite did not run: ${h.reason ?? 'no latency'}` : 'no held-out latency'),
  };
};

/** One session's record. `run` carries what the runner observed (mask, spawn stamp, skew, quiet, spin, harness identity, prereg tag, CLI version). */
export function sessionRecord({ ids, outcome, run, views, cost, aliasRecord, aliasCheck, score, loc, packages, meta, cacheGrowthBytes = null }) {
  return {
    ids, class: outcome.class, data: outcome.data, flags: outcome.flags, detail: outcome.detail,
    run: {
      mask: known(run.mask), maskObserved: run.maskObserved === undefined ? notCaptured('the sampler did not observe the mask') : known(run.maskObserved), spawnStampMs: known(run.spawnStampMs),
      startSkewMs: maybe(run.startSkewMs, 'a session run alone has no partner to skew against'), quiet: maybe(run.quiet, 'no quiet record'), spin: maybe(run.spin, 'no spin record'),
      harnessIdentity: known(run.harnessIdentity), preregTag: known(run.preregTag), cliVersion: known(run.cliVersion),
    },
    cost: { ...costPart({ outcome, views, cost, meta }), aliases: aliasRecord?.ok ? known({ ...aliasRecord.value, check: aliasCheck }) : notCaptured(aliasRecord?.reason ?? 'no alias record') },
    time: timePart({ views, meta }),
    quality: qualityPart(score),
    loc: loc ? known(loc) : notCaptured('the tree was not counted'),
    egress: known({ ...views.egress, bytesCaptured: Object.keys(views.egress.allowed).every(h => h in views.egress.bytes), cacheGrowthBytes }),
    packages: packages ? known({ packages: packages.packages, added: packages.added, flagged: packages.flagged }) : notCaptured('the caches were not read'),
    process: { compliance: NOT_BARE, coaiRounds: NOT_BARE, codeRabbit: NOT_BARE, ciRuns: NOT_BARE, deliveredBy: NOT_BARE },
  };
}

// ---- cells and pairs ------------------------------------------------------------------------------------------------------------

const measureOf = (record, path) => path.reduce((o, k) => o?.[k], record);

/** One measure of a session as `{ value, censored }`, or null when it is not known. A killed session's figure is a LOWER bound (`censored`). */
function sessionMeasure(record, measure) {
  const leaf = measure === 'costUsd' ? record.cost.stopRuleUsd : measureOf(record, ['time', measure]);
  const headline = measure === 'costUsd' && record.cost.headlineUsd.known ? record.cost.headlineUsd.value.usd : null;
  const value = headline !== null && !record.class.startsWith('killed') ? headline : leaf?.known ? leaf.value : null;
  return value === null ? null : { value, censored: record.class.startsWith('killed') };
}

export const MEASURES = Object.freeze(['costUsd', 'wallMs', 'modelApiMs', 'activeMinusModelApiMs']);

/**
 * The cell of one stack: v1 + v2. -> `{ [measure]: { value, bound: null | '>=' }, unpaired, missing }`. A measure with a side that is not known is `{ known: false }`;
 * a censored side makes the sum a lower bound. `v1` / `v2` are session records (null when the phase is missing).
 */
export function cellOf(v1, v2) {
  if (!v1 || !v2) return { missing: true, unpaired: false, ...Object.fromEntries(MEASURES.map(m => [m, { known: false, why: 'a phase of the cell is missing' }])) };
  const unpaired = [v1, v2].some(r => r.ids.label === 'ran unpaired');
  const cells = MEASURES.map(m => {
    const parts = [sessionMeasure(v1, m), sessionMeasure(v2, m)];
    if (parts.includes(null)) return [m, { known: false, why: `${m} is not known for a session of the cell` }];
    return [m, { known: true, value: Math.round((parts[0].value + parts[1].value) * 1e6) / 1e6, bound: parts.some(p => p.censored) ? '>=' : null }];
  });
  return { missing: false, unpaired, ...Object.fromEntries(cells) };
}

/** C# over TS for one measure: a value, or a bound (`>=` when only the numerator is censored, `<=` when only the denominator is); both censored claims nothing. */
export function ratioOf(c, t) {
  if (!c.known || !t.known) return { known: false, why: 'a side is not known' };
  if (c.bound && t.bound) return { known: false, why: 'both sides are censored: no bound can be claimed' };
  if (!(t.value > 0)) return { known: false, why: 'the denominator is not positive' };
  const bound = c.bound ? '>=' : t.bound ? '<=' : null;
  return { known: true, value: Math.round((c.value / t.value) * 1e6) / 1e6, bound };
}

/**
 * One pair's record: `cells` are `{ csharp, node }` from `cellOf`. Each ratio is given WITH every cell and WITHOUT the cells that hold a `ran unpaired` session
 * (then it is unknown when either cell does), beside the unpaired labels, masks, spawn stamps, skews and the quality of each phase.
 */
export function pairRecord({ pair, cells, masks, spawnStamps, skewsMs, barrierAtMs, labels, quality, flags = [] }) {
  const ratios = Object.fromEntries(MEASURES.map(m => [m, {
    with: ratioOf(cells.csharp[m], cells.node[m]),
    without: cells.csharp.unpaired || cells.node.unpaired ? { known: false, why: 'a cell holds a ran unpaired session' } : ratioOf(cells.csharp[m], cells.node[m]),
  }]));
  return { pair, masks, spawnStamps, skewsMs, barrierAtMs, labels, cells, ratios, quality, flags };
}
