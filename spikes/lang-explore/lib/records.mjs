// The records of the pilot (plan D-X9, D-X10, T-X7, T-X8). v1 writes the session record (`record.json`: every v1 metric, each leaf known or saying why not); this module adds the pilot's `explore` section
// to it, builds a pair's record from v1's `pairRecord` with the single-phase cell (seam W4: `sessionMeasure`), and computes cost per correct solution (CPC). Pure over values except the two edges that read
// and write a session's files. An unknown is `{ known: false, why }`, never 0.
import { readFileSync, renameSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { MEASURES, pairRecord, sessionMeasure } from '../../minigit-bench/lib/bare-record.mjs';
import { acceptedOf } from './score.mjs';

export const known = value => ({ known: true, value });
export const unknown = why => ({ known: false, why });
const MIB = 1024 * 1024;
const BUILD_TEST = /(?:\b(?:bash|sh|source)\s+|\.\/|\s\.\s+)(?:\S*\/)?(?:build|run)\.sh\b|\brun_samples\.py\b|\btsc\b|\bdotnet\s+build\b/;
const round = (n, k = 1e6) => Math.round(n * k) / k;

/** Bash calls whose command RUNS build.sh or run.sh (bash, sh, source, ./), the visible-samples runner, tsc or dotnet build; `cat build.sh` is not one. The class v1 does not give (its classifier knows `test-v*.sh`, not these). */
export const isBuildTestCommand = command => typeof command === 'string' && BUILD_TEST.test(command);

const isBuildCall = a => a.event === 'PreToolUse' && a.tool_name === 'Bash' && isBuildTestCommand(a.input?.command);
const isPost = a => ['PostToolUse', 'PostToolUseFailure'].includes(a.event);
const spanOf = (start, end) => (Number.isFinite(start) && end >= start ? [end - start] : []);

/**
 * The summed spans of the session's Bash calls that build or run the program (plan D-X9), from the audit log's Pre/Post pairs. A call with no readable end is not summed and is counted in `unpaired`.
 * -> `{ ms, calls, unpaired }`.
 */
export function buildTestTime(audit) {
  const pres = new Map(audit.filter(isBuildCall).map(a => [a.tool_use_id, Date.parse(a.ts)]));
  const posts = new Map(audit.filter(a => isPost(a) && pres.has(a.tool_use_id)).map(a => [a.tool_use_id, Date.parse(a.ts)]));
  const spans = [...pres].flatMap(([id, start]) => (posts.has(id) ? spanOf(start, posts.get(id)) : []));
  return { ms: spans.reduce((a, b) => a + b, 0), calls: spans.length, unpaired: pres.size - spans.length };
}

/** Cases that pass but would have failed the spec's own floors (1 s, 256 MB): the note says how many. */
export function floorBreaches(cases, floors = { timeMs: 1000, memoryBytes: 256 * MIB }) {
  const passing = cases.filter(c => c.verdict === 'pass');
  return { time: passing.filter(c => c.wallMs > floors.timeMs).length, memory: passing.filter(c => typeof c.peakBytes === 'number' && c.peakBytes > floors.memoryBytes).length, passing: passing.length };
}

const stats = xs => (xs.length === 0 ? null : { min: Math.min(...xs), median: [...xs].sort((a, b) => a - b)[Math.floor(xs.length / 2)], max: Math.max(...xs) });

/**
 * The `explore` section of a session record: visible and held-out pass rates, verdict counts by class, the wall and certified peak of every case (private: the case ids are in the record, never in the note),
 * the build time, the limits used, the trap hits (a count), the frozen files the agent changed, the build/test time of the agent's own calls, whether the session is accepted. A session that was not scored
 * says why for every leaf.
 */
export function exploreSection({ score, audit }) {
  const buildTest = audit ? known(buildTestTime(audit)) : unknown('no audit log was read');
  if (!score) return { scored: unknown('the session was not scored'), buildTest };
  const e = score.explore;
  const held = e.cases.filter(c => c.set === 'heldout');
  const visible = e.cases.filter(c => c.set === 'visible');
  return {
    scored: known(true), accepted: known(acceptedOf(score)), buildTest,
    passRate: known({ visible: e.passRate.visible, heldout: e.passRate.heldout, visibleCases: visible.length, heldoutCases: held.length }),
    verdicts: known(e.verdicts), limits: known(e.limits), buildMs: known(e.build.wallMs ?? null),
    trapHits: known({ hits: e.trapHits, trapCases: e.trapCases }), frozenChanges: known(e.frozenChanges),
    cases: known(e.cases.map(c => ({ set: c.set, id: c.id, verdict: c.verdict, wallMs: c.wallMs, peakBytes: c.peakBytes, oomKills: c.events?.oom_kill ?? 0, exit: c.exit }))),
    wall: known({ visible: stats(visible.map(c => c.wallMs)), heldout: stats(held.map(c => c.wallMs)) }),
    peak: known({ visible: stats(visible.map(c => c.peakBytes).filter(Number.isFinite)), heldout: stats(held.map(c => c.peakBytes).filter(Number.isFinite)) }),
    floorBreaches: known(floorBreaches(e.cases)),
  };
}

/** The paths (dotted) every pilot session record must hold: each v1 metric of D34 beside the pilot's `explore` fields. */
export const REQUIRED_PATHS = Object.freeze([
  'class', 'flags', 'ids.label', 'run.mask', 'run.quiet', 'run.spin', 'run.harnessIdentity',
  'cost.cliTotalUsd', 'cost.brokerUsd', 'cost.headlineUsd', 'cost.stopRuleUsd', 'cost.perModel', 'cost.tokens', 'cost.checks.value.t1', 'cost.checks.value.t2', 'cost.checks.value.t3', 'cost.checks.value.t4', 'cost.checks.value.t5',
  'time.wallMs', 'time.modelApiMs', 'time.activeMs', 'time.activeMinusModelApiMs', 'time.partition', 'time.descriptive', 'time.turns', 'time.children',
  'quality.buildOk', 'quality.visible', 'quality.heldout', 'egress', 'packages', 'loc', 'workspace', 'process',
  'explore.scored', 'explore.buildTest',
]);
/** Paths a SCORED session also holds (the explore fields that need a score). */
export const SCORED_PATHS = Object.freeze(['explore.accepted', 'explore.passRate', 'explore.verdicts', 'explore.cases', 'explore.limits', 'explore.buildMs', 'explore.trapHits', 'explore.floorBreaches', 'quality.heldout.value.percent', 'quality.heldout.value.defects', 'quality.heldout.value.primary100']);

const at = (o, dotted) => dotted.split('.').reduce((x, k) => (x === undefined || x === null ? undefined : x[k]), o);

/** The required paths a record lacks (a scored session's list is longer); `[]` means every metric is there. */
export const missingPaths = (record, scored = true) => [...REQUIRED_PATHS, ...(scored ? SCORED_PATHS : [])].filter(p => at(record, p) === undefined);

const readJson = file => JSON.parse(readFileSync(file, 'utf8'));
const writeJsonAtomic = (file, value) => { const tmp = `${file}.${process.pid}.tmp`; writeFileSync(tmp, `${JSON.stringify(value, null, 2)}\n`); renameSync(tmp, file); };

/** Add the `explore` section to a session's `record.json` (v1 wrote it), from its `score/score.json` and audit log. Idempotent. -> the record. */
export function finalizeSessionRecord({ sessionDir, score, audit }) {
  const file = path.join(sessionDir, 'record.json');
  const record = readJson(file);
  const next = { ...record, explore: exploreSection({ score, audit }) };
  writeJsonAtomic(file, next);
  return next;
}

// ---- cells, pairs, CPC ---------------------------------------------------------------------------------------------------------------

/** One measure of a session as a cell leaf: a value (a lower bound when the session was cut short) or the reason it is unknown. */
const cellLeaf = (record, m) => {
  const x = sessionMeasure(record, m);
  return x === null ? unknown(`${m} is not known for the session`) : { known: true, value: round(x.value), bound: x.censored ? '>=' : null };
};

/** The cell of a stack in a pilot pair: the pilot has ONE phase, so the cell is the session (shape of v1's `cellOf`: each measure `{ known, value, bound }`). */
export function cellOfSession(record) {
  if (!record) return { missing: true, unpaired: false, ...Object.fromEntries(MEASURES.map(m => [m, unknown('the session has no record')])) };
  return { missing: false, unpaired: record.ids.label === 'ran unpaired', ...Object.fromEntries(MEASURES.map(m => [m, { ...cellLeaf(record, m) }])) };
}

/** The session that counts for a stack of a pair: the latest attempt with data, else the latest attempt. */
export function finalOf(sessions) {
  const all = [...sessions].sort((a, b) => a.attempt - b.attempt);
  return all.findLast(s => s.data === true) ?? all.at(-1) ?? null;
}

/**
 * A pair's record: `entries` are the run-map entries of the pair, `recordOf(rid)` reads a session's record. v1's `pairRecord` with the pilot's cells; the ratios are C# over TypeScript with and without the
 * cells that hold a `ran unpaired` session.
 */
export function pairRecordOf({ pair, entries, recordOf }) {
  const final = Object.fromEntries(['csharp', 'node'].map(stack => [stack, finalOf(entries.filter(e => e.stack === stack))]));
  const records = Object.fromEntries(Object.entries(final).map(([stack, s]) => [stack, s ? recordOf(s.rid) : null]));
  const cells = Object.fromEntries(Object.entries(records).map(([stack, r]) => [stack, cellOfSession(r)]));
  return pairRecord({
    pair, cells, masks: Object.fromEntries(entries.map(e => [e.rid, e.mask])), spawnStamps: Object.fromEntries(entries.map(e => [e.rid, e.run?.spawnStampMs ?? null])),
    skewsMs: Object.fromEntries(entries.filter(e => e.run?.startSkewMs != null).map(e => [e.rid, e.run.startSkewMs])), barrierAtMs: null,
    labels: Object.fromEntries(entries.map(e => [e.rid, { stack: e.stack, attempt: e.attempt, label: e.label, class: e.class ?? null }])),
    quality: Object.fromEntries(Object.entries(records).map(([stack, r]) => [stack, r?.quality ?? null])),
    flags: [...new Set(entries.flatMap(e => e.run?.flags ?? []))],
  });
}

const NO_SOLUTION = 'no correct solution';

/** The summed cost of sessions, `{ value, bound }` (a lower bound when one was cut short), or null when one is not known. */
function spendOf(sessions) {
  const costs = sessions.map(s => sessionMeasure(s.record, 'costUsd'));
  return costs.includes(null) ? null : { value: round(costs.reduce((a, c) => a + c.value, 0)), bound: costs.some(c => c.censored) ? '>=' : null };
}

/**
 * Cost per correct solution of a stack (D24): the summed cost of the stack's sessions, voids and killed ones included (a `killed: cap` session counts in the numerator at its censored figure), over the
 * number accepted. No accepted session is `no correct solution` WITH its spend, never a division by zero. A session whose cost is unknown makes the sum unknown.
 * `sessions` is `[{ record, accepted }]`.
 */
export function cpcOf(sessions) {
  const accepted = sessions.filter(s => s.accepted === true).length;
  const spend = spendOf(sessions);
  const base = { sessions: sessions.length, accepted };
  if (spend === null) return { ...base, spendUsd: unknown('a session\'s cost is not known'), cpc: unknown('the spend is not known') };
  const cpc = accepted === 0 ? { known: true, value: null, text: NO_SOLUTION, spendUsd: spend.value } : { known: true, value: round(spend.value / accepted), bound: spend.bound };
  return { ...base, spendUsd: { known: true, ...spend }, cpc };
}
