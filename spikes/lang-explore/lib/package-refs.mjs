// The two references of a package (F2.S1, P3): each is scored by F1.S2's ten-pass driver at the one memory value, and green means `accepted` (every visible and held-out case passed in all ten passes, five per CPU profile). The call
// is the one `measure-growth.mjs` makes: `world.passSeams({ stack, source, limits, sets, traps })` into `scorePasses`; nothing of the driver changes. The record keeps, per reference and per pass, the largest peak, the slowest case
// wall, the ten passes' wall and the retained size (the numbers *Growth* reads). `loadRule` is F-LOAD's measured duration: the slowest whole-scenario wall of each load case over every pass of both references under both profiles
// against 600 s minus 30 s and 600 s minus twice the non-load twin's wall. Sentences name stacks and counts, never a case id.
import { copyFileSync, existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { dirBytes } from './measure.mjs';
import { maxOf } from './package-max.mjs';
import { wrapPass } from './package-pass.mjs';
import { scorePasses } from './passes.mjs';
import { readAttempts } from './score-files.mjs';
import { runnerRecordPinNow } from './harness-pin.mjs';


const numbers = (cases, key) => cases.map(c => c[key]).filter(Number.isFinite);

/** The rows of one scoring from its files: per pass the attempt that completed (`wallMs`, the largest `peakBytes`, the slowest case wall), and the sums the record keeps. `ids` are the cases the passes judged. */
export function referenceRows({ work, ids }) {
  const passes = readAttempts({ work, expectedIds: ids, pinnedRunner: runnerRecordPinNow() });
  const rows = passes.map(p => {
    const done = p.attempts.find(a => a.kind === 'completed');
    return { n: p.n, profile: p.profile, attempts: p.attempts.length, completed: done !== undefined, wallMs: done?.ms ?? null, peakBytes: done ? maxOf(numbers(done.cases, 'peakBytes')) : null, slowestCaseWallMs: done ? maxOf(numbers(done.cases, 'wallMs')) : null, cases: done?.cases.map(c => ({ id: c.id, wallMs: c.wallMs })) ?? [] };
  });
  return {
    passes: rows, maxPeakBytes: maxOf(rows.map(r => r.peakBytes).filter(Number.isFinite)), slowestCaseWallMs: maxOf(rows.map(r => r.slowestCaseWallMs).filter(Number.isFinite)),
    tenPassWallMs: rows.reduce((sum, r) => sum + (r.wallMs ?? 0), 0), retainedBytes: dirBytes(work), mostAttempts: maxOf(rows.map(r => r.attempts)),
  };
}

const countsLine = counts => `${counts.clean} clean, ${counts.failing} failing, ${counts.open} open, ${counts.exhausted} exhausted pass(es)`;

/** What the driver's answer means for a reference: `{ ok, halted, verdict, sentence }`. Green only on `accepted`; an environment answer is a halt (resumable), never a verdict. */
function judgedAnswer({ answer, stack }) {
  if (answer.kind === 'scored') {
    const v = answer.verdict;
    return { ok: v.verdict === 'accepted', halted: false, verdict: v.verdict, sentence: v.verdict === 'accepted' ? `the ${stack} reference is accepted: ${countsLine(v.counts)}` : `the ${stack} reference reads ${v.verdict} (${countsLine(v.counts)}): green is accepted, ten of ten` };
  }
  const reason = answer.reason ?? 'no reason given';
  return { ok: false, halted: true, verdict: answer.kind === 'void' ? 'unscored: environment' : null, sentence: `the ${stack} reference was not scored (${answer.kind}): ${reason}` };
}

/**
 * Score one reference: `a` = `{ world, pkg, stack, source, work, seriesDir, limits, memoryBytes, say, signal, now?, scoreFn?, heartbeatMs? }`. `source` is the assembled tree (`assembleReference`), `work` the scoring's own folder (its attempts
 * and events stay there, whole, written once: a run that halts resumes from them). -> `{ ok, halted, verdict, sentence, rows }` (`rows` absent when nothing was scored).
 */
export async function validateReference(a) {
  const { world, pkg, stack } = a;
  const seams = world.passSeams({ stack, source: a.source, limits: a.limits, sets: [{ name: 'heldout', dir: pkg.casesDir }], traps: pkg.held.traps, say: a.say, seriesDir: a.seriesDir });
  const runPass = wrapPass({ runPass: seams.runPass, label: `reference ${stack}`, say: a.say ?? (() => {}), memoryBytes: a.memoryBytes, ...(a.heartbeatMs === undefined ? {} : { heartbeatMs: a.heartbeatMs }) });
  mkdirSync(a.work, { recursive: true });
  const answer = await (a.scoreFn ?? scorePasses)({ ...seams, runPass, work: a.work, seriesDir: a.seriesDir, signal: a.signal, ...(a.now === undefined ? {} : { now: a.now }) });
  const judged = judgedAnswer({ answer, stack });
  const ids = answer.score?.explore.cases.map(c => c.id);
  return ids === undefined ? judged : { ...judged, rows: referenceRows({ work: a.work, ids: [...ids].sort() }) };
}

const LOAD_BOUND_MS = 570_000;
const TEST_BOUND_MS = 600_000;

/** The slowest wall of a load case over every pass given (`[{ stack, profile, n, cases: [{ id, wallMs }] }]`). */
const slowestOf = (passes, loadCases) => maxOf(passes.flatMap(p => p.cases.filter(c => loadCases.includes(c.id)).map(c => c.wallMs)).filter(Number.isFinite));

function boundMissed({ slowest, twin }) {
  const bounds = [[LOAD_BOUND_MS, '600 s minus 30 s'], [TEST_BOUND_MS - 2 * twin, '600 s minus twice the non-load part']];
  const [limit, name] = bounds.reduce((worst, b) => (b[0] < worst[0] ? b : worst));
  return slowest > limit ? { limit, name } : null;
}

const NO_TWIN = 'no twin measurement of the load scenarios: the non-load part is unknown, so F-LOAD\'s duration is not measured and is never green by the spec\'s number';
const NO_LOAD_PASS = 'no load case has a completed pass: the slowest load wall is unknown';

/** The sentence of a load measurement that cannot be judged, or null. */
const unmeasured = ({ slowest, twinWallMs }) => (Number.isFinite(twinWallMs) ? (slowest === null ? NO_LOAD_PASS : null) : NO_TWIN);

/** The rule over a measured slowest wall and twin: green, or red with the duration that fits. */
function measuredRule({ slowest, twin, durationS, numbersOut }) {
  const missed = boundMissed({ slowest, twin });
  if (missed === null) return { ok: true, ...numbersOut, durationS, problems: [] };
  const fits = durationS - Math.ceil((slowest - missed.limit) / 1000);
  return { ok: false, ...numbersOut, durationS: fits, problems: [`the slowest load scenario took ${slowest} ms, over ${missed.name} (${missed.limit} ms); the load duration must be at most ${fits} s in the spec (it is ${durationS} s)`] };
}

/**
 * F-LOAD's measured rule. `passes` are the completed passes of both references under both profiles, `loadCases` the load cases, `twinWallMs` the wall of the load scenario with its load steps removed (the slowest over the two
 * references), `durationS` the load duration the spec states. -> `{ ok, slowestWallMs, twinWallMs, durationS, problems }`; `durationS` is the duration to write into the spec (shortened by the excess when a bound is missed).
 * A package with no load case has nothing to measure; a load case with no twin measurement is red, never green by the spec's number.
 */
export function loadRule({ passes, loadCases, twinWallMs, durationS }) {
  if (loadCases.length === 0) return { ok: true, slowestWallMs: null, twinWallMs: null, durationS: null, problems: [] };
  const slowest = slowestOf(passes, loadCases);
  const numbersOut = { slowestWallMs: slowest, twinWallMs: twinWallMs ?? null };
  const refused = unmeasured({ slowest, twinWallMs });
  return refused === null ? measuredRule({ slowest, twin: twinWallMs, durationS, numbersOut }) : { ok: false, ...numbersOut, durationS, problems: [refused] };
}

/** The twins of the load cases staged as a case folder `dir` for the seams: each twin pair, and the `cases.txt` lines of those cases (the runner reads the comparison and the R-ids there). -> the ids staged. */
export function stageTwins({ pkg, dir }) {
  mkdirSync(dir, { recursive: true });
  const ids = pkg.manifest.load?.cases ?? [];
  for (const id of ids) for (const ext of ['in', 'out']) copyFileSync(path.join(pkg.twinsDir, `${id}.${ext}`), path.join(dir, `${id}.${ext}`));
  const lines = existsSync(path.join(pkg.casesDir, 'cases.txt')) ? readFileSync(path.join(pkg.casesDir, 'cases.txt'), 'utf8').split('\n').filter(l => ids.includes(l.trim().split(/\s+/)[0])) : [];
  writeFileSync(path.join(dir, 'cases.txt'), `${lines.join('\n')}\n`);
  return ids;
}
