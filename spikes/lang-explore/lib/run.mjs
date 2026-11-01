// `explore.mjs run --pair k [--rerun <stack>]`: ONE pair, the operator reads it before paying for the next (plan D-X1). The pair's sessions are planned in the run-map (kind `pilot`) by `lib/series.mjs`,
// then v1's `runLoop` does what it does for the bare runner (the gates, the same-tick start on the masks, the barrier, the scoring in v1's order, the classes, the charge), with the pilot's workspace fill,
// plan, machine and judge. Afterwards each session's record gains its `explore` section and the pair's record is written.
import { existsSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { reconcile, runLoop } from '../../minigit-bench/lib/bare-series.mjs';
import { loadSessionDir } from '../../minigit-bench/lib/smoke-load.mjs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { writeRunMap } from '../../minigit-bench/lib/run-map.mjs';
import { acquireSeriesLock } from '../../minigit-bench/lib/series-lock.mjs';
import { TOKEN_FILE_PARTS } from '../../minigit-bench/capture/capture-env.mjs';
import { readSecretFile } from '../../minigit-bench/lib/secrets.mjs';
import { layoutPaths } from '../../minigit-bench/lib/sandbox.mjs';
import { loadHeldout } from './heldout.mjs';
import { finalizeSessionRecord, missingPaths, pairRecordOf } from './records.mjs';
import { frozenFill, realMachine, runContext, runDeps, scoreSessionDep } from './run-ctx.mjs';
import { allOk } from './chain.mjs';
import { calibrationFor, loadCalibration } from './limits.mjs';
import { pinsOk } from './pins.mjs';
import { canonicalNameProblem, isRehearsal, loadSeries, planPair, runMapFileOf, seriesInputs, seriesPrompts, seriesRootProblem } from './series.mjs';
import { pauseVerdict } from './pause.mjs';
import { acquirePlanLock } from './plan-lock.mjs';
import { frozenPinsProblem, seriesTaskProblem } from './tasks.mjs';

const readJson = file => JSON.parse(readFileSync(file, 'utf8'));

export { loadCalibration };

const sessionDirOf = (seriesDir, rid) => path.join(seriesDir, 's', rid);
const scoreOf = dir => { try { return readJson(path.join(dir, 'score', 'score.json')); } catch { return null; } };
const auditOf = dir => { const l = loadSessionDir(dir); return l.ok ? l.value.audit : null; };

const recordFile = (seriesDir, rid) => path.join(sessionDirOf(seriesDir, rid), 'record.json');

/** One session's record gains its `explore` section; -> the problems found (an absent record, a missing metric). */
function finalizeOne(seriesDir, e) {
  const dir = sessionDirOf(seriesDir, e.rid);
  if (!existsSync(recordFile(seriesDir, e.rid))) return [`${e.rid}: no record.json (the session ended as ${e.status})`];
  const record = finalizeSessionRecord({ sessionDir: dir, score: scoreOf(dir), audit: auditOf(dir) });
  return missingPaths(record, e.status === 'scored').map(p => `${e.rid}: the record lacks ${p}`);
}

/** What the pair's record already holds of the owner's `--allow-over` texts (a resume rewrites the record and must not erase them). */
const earlierOverrides = file => { try { return readJson(file).pauseOverrides ?? []; } catch { return []; } };

/**
 * After the loop: each session of `pair` that has a record gains its `explore` section; the records are checked for completeness; the pair record is written, with the owner's `--allow-over` text when the pause
 * point was passed by it (`pause`, see lib/pause.mjs; earlier ones are kept). -> `{ problems, pairFile }`.
 */
export function finalizePair({ seriesDir, map, pair, kinds = ['pilot'], pause = null }) {
  const entries = Object.values(map.sessions).filter(s => s.pair === pair && kinds.includes(s.kind));
  const problems = entries.flatMap(e => finalizeOne(seriesDir, e));
  const recordOf = rid => (existsSync(recordFile(seriesDir, rid)) ? readJson(recordFile(seriesDir, rid)) : null);
  const pairFile = path.join(seriesDir, 'pairs', `p${pair}.json`);
  writeFileSync(pairFile, `${JSON.stringify(withOverrides(pairRecordOf({ pair, entries, recordOf }), [...earlierOverrides(pairFile), ...(pause ? [pause] : [])]), null, 2)}\n`);
  return { problems, pairFile };
}

/** The pair record with the owner's `--allow-over` texts, when there are any. */
const withOverrides = (record, overrides) => (overrides.length > 0 ? { ...record, pauseOverrides: overrides } : record);

/** The real token, read ONCE from its mode-600 file; only a paid run calls this. */
export const readToken = home => readSecretFile({ home, parts: TOKEN_FILE_PARTS, label: 'token' });

/**
 * Run pair `pair` (or re-run a void's stack) of the series in `seriesDir`, which must be a series of `task` (its pins entry; `explore` the pins). `io` = `{ home, say, signal, abort, acceptNoisy, allowOver, worldFn, tokenFn, seriesNameOk }`;
 * the last two are for tests. -> `{ exit, message, pairFile, problems }`. Exit: 0 done | 2 a stop (the series is stopped, or the plan's $60 pause point) | 3 a resumable halt | 4 configuration or a refusal | 5 failed.
 */
export async function runPair({ seriesDir, pair, rerun = null, io, explore, task }) {
  const outside = seriesRootProblem(io.home, seriesDir);
  if (outside) return { exit: 4, message: outside };
  const series = loadSeries(seriesDir);
  const refusal = seriesRefusal(seriesDir, series, task, io.seriesNameOk);
  if (refusal) return refusal;
  const lock = acquireSeriesLock({ seriesDir });
  if (!lock.ok) return { exit: 4, message: lock.reason };
  try {
    return await underPlanLock({ series: series.value, pair, rerun, io, explore, task });
  } finally {
    lock.value.release();
  }
}

/** The plan's tasks run under ONE lock (lib/plan-lock.mjs) held from the pause check to the end of the pair; task 1's own series does not. */
async function underPlanLock(a) {
  if (!a.explore.olympiad.planTasks.includes(a.task.id)) return runLocked(a);
  const lock = acquirePlanLock({ home: a.io.home });
  if (!lock.ok) return { exit: 4, message: lock.reason };
  try {
    return await runLocked(a);
  } finally {
    lock.value.release();
  }
}

/** Why a series cannot run a paid pair, as a result, or null: it is not a series, it is of another task, it is a rehearsal's, or it is stopped. */
function seriesRefusal(seriesDir, series, task, nameOk) {
  if (!series.ok) return { exit: 4, message: series.reason };
  const refused = [
    () => seriesTaskProblem({ state: series.value.state, task }),
    () => (isRehearsal(seriesDir) ? `${seriesDir} is a rehearsal series (stub CLI): a paid pair runs on a series made by \`explore.mjs init\`` : null),
    () => canonicalNameProblem(seriesDir, nameOk),
  ].map(check => check()).find(Boolean);
  return refused ? { exit: 4, message: refused } : stoppedRefusal(series.value.map);
}

const stoppedRefusal = map => (map.stopped ? { exit: 2, message: `the series is stopped (${map.stopped.rule}): a stop is final for its series` } : null);

/** What a pair needs besides its plan: the task's calibration, its held-out side, the world, the token and the series' frozen prompts. -> `ok({ calibration, heldout, world, token, prompts })` | the first `fail`. */
async function prerequisites({ home, series, task, worldFn, tokenFn }) {
  const got = await allOk([() => calibrationFor({ home, seriesDir: series.dir, task }), () => loadHeldout(home, task), () => worldFn({ home, work: path.join(series.dir, 'private'), task }), () => tokenFn(home), () => seriesPrompts(series.dir)]);
  if (!got.ok) return got;
  const [calibration, heldout, world, token, prompts] = got.value;
  const pins = pinsOk({ explore: world.explore, task, home, snapshots: world.snapshots });
  return pins.ok ? ok({ calibration, heldout, world, token, prompts }) : pins;
}

/** The plan reaches the run map only now, when everything the pair needs is in hand: a pair that cannot start leaves run-map.json as it was. */
function commitPlan({ series, pair, planned, say }) {
  writeRunMap(runMapFileOf(series.dir), planned.map);
  if (planned.resumed) say(`pair ${pair}: resuming ${planned.resumed.join(', ')} (left open by a halt or a failure; no new session is planned)`);
  else say(`pair ${pair}: planned ${planned.planned.map(p => `${p.rid} (${p.stack}, mask ${p.mask}, ${p.label})`).join(' and ')}`);
}

/** The pair's plan (the barrier), refused first when the series' frozen pins are not the task's pins now. -> `ok(plan)` | `fail`. */
function planOnPins({ series, pair, rerun, task }) {
  const moved = pinsMoved({ series, task });
  return moved ? fail(moved) : planPair({ map: series.map, pair, pairs: series.state.pairs, draw: series.state.draw, rerun });
}

/** Why the series cannot start a pair on today's pins of the task (an amendment since `init`), or null. */
function pinsMoved({ series, task }) {
  const inputs = seriesInputs(series.dir);
  return inputs.ok ? frozenPinsProblem({ inputs: inputs.value, task, seriesId: series.state.seriesId }) : inputs.reason;
}

/** The pause point (lib/pause.mjs) applies to the plan's tasks only; task 1's own series, made before it existed, is not under it. */
const pauseOf = ({ io, explore, task }) => (explore.olympiad.planTasks.includes(task.id) ? pauseVerdict({ home: io.home, explore, allowOver: io.allowOver ?? null, ...(io.now ? { now: io.now } : {}) }) : { stop: false, record: null });

async function runLocked({ series, pair, rerun, io, explore, task }) {
  const planned = planOnPins({ series, pair, rerun, task });
  if (!planned.ok) return { exit: 4, message: planned.reason };
  const pause = pauseOf({ io, explore, task });
  if (pause.stop) return { exit: 2, message: pause.message };
  await io.afterPauseCheck?.(); // a seam for tests: a run that has passed the check and holds the plan lock
  const need = await prerequisites({ home: io.home, series, task, worldFn: io.worldFn, tokenFn: io.tokenFn });
  if (!need.ok) return { exit: 4, message: need.reason };
  return loopAndFinalize({ series, pair, planned: planned.value, ...need.value, io, abort: io.abort, pause: pause.record });
}

/** A resume first lets v1 settle what a crash left `launching` (charge from the broker ledger, class `void: harness`, or plan again a session that never paid); a fresh pair has nothing to settle. */
async function settleOpen({ ctx, deps, map, resumed, say }) {
  return resumed ? reconcile({ ctx, deps, map, say }) : ok(map);
}

/**
 * The loop over the planned pair, then the records. `io.seams` (`{ claudeBinary, planOver, machine, preflightHome, stub }`) is for tests only: a stub CLI behind a loopback stub upstream, a scripted machine. A real run
 * passes none, so it launches the pinned CLI copy against the real API behind the real machine.
 */
async function loopAndFinalize({ series, pair, planned, calibration, heldout, world, token, prompts, io, abort, pause }) {
  const seams = io.seams ?? {};
  const ctx = await runContext({
    world, calibration, series: series.state, seriesDir: series.dir, kind: 'pilot', pair, token, claudeBinary: seams.claudeBinary ?? world.paths.claudeBinary, fill: seams.fill ?? frozenFill(world), planOver: seams.planOver ?? {}, abort, home: io.home, prompts,
  });
  if (!ctx.ok) return { exit: 4, message: ctx.reason };
  const machine = seams.machine ?? realMachine({ ctx: ctx.value, acceptNoisy: io.acceptNoisy });
  const deps = runDeps({ ctx: ctx.value, world, scoreDep: scoreSessionDep({ world, calibration, heldout, home: io.home, say: io.say }), machine, preflightHome: seams.preflightHome ?? io.home, stub: seams.stub === true });
  commitPlan({ series, pair, planned, say: io.say });
  const settled = await settleOpen({ ctx: ctx.value, deps, map: planned.map, resumed: planned.resumed, say: io.say });
  if (!settled.ok) return { exit: settled.exit ?? 5, message: settled.reason };
  const result = await runLoop({ ctx: ctx.value, deps, map: settled.value, signal: io.signal, say: io.say });
  const fin = finalizePair({ seriesDir: series.dir, map: result.map, pair, pause });
  return { exit: result.exit, message: result.message, pairFile: fin.pairFile, problems: fin.problems };
}
