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
import { calibrationFile } from './paths.mjs';
import { allOk } from './chain.mjs';
import { pinsOk } from './pins.mjs';
import { loadSeries, planPair, runMapFileOf } from './series.mjs';

const readJson = file => JSON.parse(readFileSync(file, 'utf8'));

/** The calibration (`explore.mjs calibrate`), or the sentence saying to run it: no paid session starts without limits. */
export function loadCalibration(home) {
  const file = calibrationFile(home);
  return existsSync(file) ? ok(readJson(file)) : fail(`${file} is not there: run \`explore.mjs calibrate\` first (the limits are measured before any paid session)`);
}

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

/**
 * After the loop: each session of `pair` that has a record gains its `explore` section; the records are checked for completeness; the pair record is written. -> `{ problems, pairFile }`.
 */
export function finalizePair({ seriesDir, map, pair, kinds = ['pilot'] }) {
  const entries = Object.values(map.sessions).filter(s => s.pair === pair && kinds.includes(s.kind));
  const problems = entries.flatMap(e => finalizeOne(seriesDir, e));
  const recordOf = rid => (existsSync(recordFile(seriesDir, rid)) ? readJson(recordFile(seriesDir, rid)) : null);
  const pairFile = path.join(seriesDir, 'pairs', `p${pair}.json`);
  writeFileSync(pairFile, `${JSON.stringify(pairRecordOf({ pair, entries, recordOf }), null, 2)}\n`);
  return { problems, pairFile };
}

/** The real token, read ONCE from its mode-600 file; only a paid run calls this. */
export const readToken = home => readSecretFile({ home, parts: TOKEN_FILE_PARTS, label: 'token' });

/**
 * Run pair `pair` (or re-run a void's stack) of the series in `seriesDir`. `io` = `{ home, say, signal, abort, acceptNoisy, worldFn, tokenFn }`; the last two are for tests. -> `{ exit, message, pairFile, problems }`.
 * Exit: 0 done | 2 a stop | 3 a resumable halt | 4 configuration or a refusal | 5 failed.
 */
export async function runPair({ seriesDir, pair, rerun = null, io }) {
  const series = loadSeries(seriesDir);
  if (!series.ok) return { exit: 4, message: series.reason };
  if (series.value.map.stopped) return { exit: 2, message: `the series is stopped (${series.value.map.stopped.rule}): a stop is final for its series` };
  const lock = acquireSeriesLock({ seriesDir });
  if (!lock.ok) return { exit: 4, message: lock.reason };
  try {
    return await runLocked({ series: series.value, pair, rerun, io });
  } finally {
    lock.value.release();
  }
}

/** What a pair needs besides its plan: the calibration, the held-out side, the world and the token. -> `ok({ calibration, heldout, world, token })` | the first `fail`. */
async function prerequisites({ home, series, worldFn, tokenFn }) {
  const got = await allOk([() => loadCalibration(home), () => loadHeldout(home), () => worldFn({ home, work: path.join(series.dir, 'private') }), () => tokenFn(home)]);
  if (!got.ok) return got;
  const [calibration, heldout, world, token] = got.value;
  const pins = pinsOk({ explore: world.explore, home, snapshots: world.snapshots });
  return pins.ok ? ok({ calibration, heldout, world, token }) : pins;
}

async function runLocked({ series, pair, rerun, io }) {
  const planned = planPair({ map: series.map, pair, pairs: series.state.pairs, draw: series.state.draw, rerun });
  if (!planned.ok) return { exit: 4, message: planned.reason };
  const need = await prerequisites({ home: io.home, series, worldFn: io.worldFn, tokenFn: io.tokenFn });
  if (!need.ok) return { exit: 4, message: need.reason };
  writeRunMap(runMapFileOf(series.dir), planned.value.map);
  if (planned.value.resumed) io.say(`pair ${pair}: resuming ${planned.value.resumed.join(', ')} (left open by a halt or a failure; no new session is planned)`);
  else io.say(`pair ${pair}: planned ${planned.value.planned.map(p => `${p.rid} (${p.stack}, mask ${p.mask}, ${p.label})`).join(' and ')}`);
  return loopAndFinalize({ series, pair, planned: planned.value, ...need.value, io, abort: io.abort });
}

/** A resume first lets v1 settle what a crash left `launching` (charge from the broker ledger, class `void: harness`, or plan again a session that never paid); a fresh pair has nothing to settle. */
async function settleOpen({ ctx, deps, map, resumed, say }) {
  return resumed ? reconcile({ ctx, deps, map, say }) : ok(map);
}

/**
 * The loop over the planned pair, then the records. `io.seams` (`{ claudeBinary, planOver, machine, preflightHome, stub }`) is for tests only: a stub CLI behind a loopback stub upstream, a scripted machine. A real run
 * passes none, so it launches the pinned CLI copy against the real API behind the real machine.
 */
async function loopAndFinalize({ series, pair, planned, calibration, heldout, world, token, io, abort }) {
  const seams = io.seams ?? {};
  const ctx = await runContext({
    world, calibration, series: series.state, seriesDir: series.dir, kind: 'pilot', pair, token, claudeBinary: seams.claudeBinary ?? world.paths.claudeBinary, fill: seams.fill ?? frozenFill(world), planOver: seams.planOver ?? {}, abort, home: io.home,
  });
  if (!ctx.ok) return { exit: 4, message: ctx.reason };
  const machine = seams.machine ?? realMachine({ ctx: ctx.value, acceptNoisy: io.acceptNoisy });
  const deps = runDeps({ ctx: ctx.value, world, scoreDep: scoreSessionDep({ world, calibration, heldout, home: io.home }), machine, preflightHome: seams.preflightHome ?? io.home, stub: seams.stub === true });
  const settled = await settleOpen({ ctx: ctx.value, deps, map: planned.map, resumed: planned.resumed, say: io.say });
  if (!settled.ok) return { exit: settled.exit ?? 5, message: settled.reason };
  const result = await runLoop({ ctx: ctx.value, deps, map: settled.value, signal: io.signal, say: io.say });
  const fin = finalizePair({ seriesDir: series.dir, map: result.map, pair });
  return { exit: result.exit, message: result.message, pairFile: fin.pairFile, problems: fin.problems };
}
