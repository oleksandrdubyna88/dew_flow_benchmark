// `explore.mjs replay --task NN --series DIR --rid R --case ID [--times N]`: read a rejected case again on the CURRENT runner, keeping what it saw. It copies the session's kept tree, scores that one case through the same path as a
// scoring (`lib/score-quick.mjs`: the build in the stack's box, the layer, the runner) and writes ONLY a new file, `s/<rid>/replay/<utc-time>.json`, with per run the verdict and the kept evidence; the same lines go to
// standard output. It changes no record, no run-map entry, no verdict and no spend, and it writes no `score.json`: its result is a diagnostic reading, and the file says so.
//   - It takes the series lock itself and refuses at once while a run holds it.
//   - It refuses a series whose frozen pins are not the task's pins now.
//   - The frozen files of the copy are today's frozen set. The series recorded no frozen set of its own, so a change under `runner/` is reported as harness drift when the runner pin moved since the series was made
//     (or the series recorded none), as in a rescore; the file says so.
//   - A run that is the harness failing (kind `environment`) is recorded as a void run, never as a verdict.
import { existsSync, mkdirSync, rmSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { ok } from '../../minigit-bench/lib/result.mjs';
import { layoutPaths } from '../../minigit-bench/lib/sandbox.mjs';
import { acquireSeriesLock } from '../../minigit-bench/lib/series-lock.mjs';
import { allOk } from './chain.mjs';
import { systemClock } from './clock.mjs';
import { runnerDriftPossible, runnerPinNow, templateDriftPossible } from './harness-pin.mjs';
import { calibrationFor } from './limits.mjs';
import { pinsOk } from './pins.mjs';
import { heldoutOf } from './rescore.mjs';
import { isRehearsal, loadSeries, seriesInputs, seriesRootProblem } from './series.mjs';
import { frozenPinsProblem, seriesTaskProblem } from './tasks.mjs';

const EXIT = Object.freeze({ done: 0, halt: 3, config: 4 });
const MAX_TIMES = 20;
const CASE_ID = /^(?:s\d\d|h\d{3})$/;
export const DIAGNOSTIC_NOTE = 'A diagnostic reading of a kept tree on the current runner. It is no verdict: no record, no run-map entry, no score and no spend of the series was changed by it, and it must not be used in place of the session\'s own score.';

const refuse = (message, exit = EXIT.config) => ({ exit, message });

const caseWrong = caseId => (CASE_ID.test(caseId ?? '') ? null : `--case names a visible sample (s01) or a held-out case (h001), got '${caseId}'`);
const timesWrong = times => (Number.isInteger(times) && times >= 1 && times <= MAX_TIMES ? null : `--times is a whole number from 1 to ${MAX_TIMES} (got '${times}')`);

/** The sentence refusing the arguments, or null. */
const argsProblem = ({ caseId, times }) => caseWrong(caseId) ?? timesWrong(times);

/** Why the session cannot be read again, or null: it must exist and have kept its workspace. */
function sessionWrong({ entry, layout, rid }) {
  if (entry === undefined) return `the run-map has no session ${rid}`;
  return existsSync(layout.work) ? null : `session ${rid} has no workspace (${layout.work}): there is no tree to read`;
}

/** Why the series' frozen pins are not the task's pins now, or null. */
function pinsWrong({ series, task, seriesDir }) {
  const inputs = seriesInputs(seriesDir);
  return inputs.ok ? frozenPinsProblem({ inputs: inputs.value, task, seriesId: series.state.seriesId }) : inputs.reason;
}

const heldWrong = ({ caseId, heldout, task }) => (caseId.startsWith('h') && heldout.caseIds !== undefined && !heldout.caseIds.includes(caseId) ? `the held-out set of task ${task.id} has no case ${caseId}` : null);

/** The sentence refusing the series, the session or the case, or null. Read after the lock is held. */
const targetProblem = a => seriesTaskProblem({ state: a.series.state, task: a.task }) ?? sessionWrong(a) ?? pinsWrong(a) ?? heldWrong(a);

/** One run's line for the file and for standard output: the case's verdict and what was kept, or the void. */
export function runLine({ run, scored, id }) {
  if (!scored.ok) return { run, void: true, kind: scored.kind ?? 'failed', reason: scored.reason };
  const c = scored.value.explore.cases.find(x => x.id === id);
  const explore = scored.value.explore;
  return {
    run, void: false, build: { ok: explore.build.ok, status: explore.build.status ?? null, wallMs: explore.build.wallMs ?? null, timedOut: explore.build.timedOut ?? false },
    verdict: c?.verdict ?? null, reason: c?.reason ?? '', wallMs: c?.wallMs ?? null, peakBytes: c?.peakBytes ?? null, firstMismatch: c?.firstMismatch ?? null, runner: explore.runner ?? null,
    memoryCertified: explore.memoryCertified ?? null, harnessDrift: explore.harnessDrift ?? [], nonRegularLeftOut: explore.nonRegularLeftOut ?? [], frozenChanges: explore.frozenChanges,
  };
}

const sayRun = (say, l) => say(l.void ? `run ${l.run}: VOID (${l.kind}): ${l.reason}` : `run ${l.run}: ${l.verdict}${l.reason ? ` (${l.reason})` : ''}; wall ${l.wallMs} ms${l.firstMismatch?.evidence ? '; evidence kept' : ''}`);

/** What a replay needs: the limits, the held-out set, the world, and (a real series) the hash pins. -> `ok({ calibration, heldout, world })` | `fail`. */
async function needs({ io, seriesDir, task }) {
  const home = io.home;
  const got = await allOk([() => calibrationFor({ home, seriesDir, task }), () => heldoutOf({ home, seriesDir, task }), () => io.worldFn({ home, work: path.join(seriesDir, 'private'), task })]);
  if (!got.ok) return got;
  const [calibration, heldout, world] = got.value;
  const pins = isRehearsal(seriesDir) ? ok(null) : pinsOk({ explore: world.explore, task, home, snapshots: world.snapshots });
  return pins.ok ? ok({ calibration, heldout, world }) : pins;
}

/** The runs, one after the other (each its own build and run), in a scratch folder that is removed; a stop signal ends them between two runs. -> `[line]`. */
async function runAll({ need, entry, layout, caseId, times, driftPossible, templateDrift, work, say, signal }) {
  const lines = [];
  for (let run = 1; run <= times && signal?.aborted !== true; run += 1) {
    const scored = await need.world.scoreFn({
      stack: entry.stack, source: layout.work, limits: need.calibration.limits, sets: [{ name: 'heldout', dir: need.heldout.casesDir }], traps: need.heldout.traps, caches: { nuget: layout.nuget, npm: layout.npm },
      work, operatorHome: need.home, say, only: caseId, driftPossible, templateDriftPossible: templateDrift,
    });
    lines.push(runLine({ run, scored, id: caseId }));
    sayRun(say, lines.at(-1));
  }
  return lines;
}

const stampOf = now => now.toISOString().replace(/[:.]/g, '-');

/** The one file a replay writes. -> its path. */
function writeResult({ layout, stamp, body }) {
  const dir = path.join(layout.dir, 'replay');
  mkdirSync(dir, { recursive: true });
  const file = path.join(dir, `${stamp}.json`);
  writeFileSync(file, `${JSON.stringify(body, null, 2)}\n`, { flag: 'wx' });
  return file;
}

/** The answer of a replay that a stop signal ended before its last run: nothing is written, the scratch folder is removed by the caller. */
const stopped = ({ io, done, times }) => ({ exit: io.abort?.exit ?? 130, message: `replay stopped by ${io.abort?.reason ?? 'a stop signal'} after ${done} of ${times} run(s): nothing was written` });

/** The runs and the one file, inside the scratch folder `work`. -> the result. */
async function replayIn({ io, need, seriesDir, entry, layout, rid, caseId, times, work, now, stamp }) {
  const inputs = seriesInputs(seriesDir).value;
  const [driftPossible, templateDrift] = [runnerDriftPossible({ inputs }), templateDriftPossible({ inputs })];
  const runs = await runAll({ need: { ...need, home: io.home }, entry, layout, caseId, times, driftPossible, templateDrift, work, say: io.say, signal: io.signal });
  if (runs.length < times) return stopped({ io, done: runs.length, times });
  const body = { diagnostic: true, note: DIAGNOSTIC_NOTE, rid, caseId, stack: entry.stack, times, at: now.toISOString(), runner: runnerPinNow(), driftPossible, templateDriftPossible: templateDrift, sessionClass: entry.class, runs };
  const file = writeResult({ layout, stamp, body });
  io.say(`replay file: ${file}`);
  return { exit: runs.some(r => r.void) ? EXIT.halt : EXIT.done, message: `replayed ${caseId} of ${rid} ${times} time(s); ${runs.filter(r => r.void).length} void; a diagnostic only: nothing of the series was changed`, file };
}

async function replayLocked({ io, task, series, seriesDir, rid, caseId, times, entry, layout }) {
  const need = await needs({ io, seriesDir, task });
  if (!need.ok) return refuse(need.reason);
  const problem = targetProblem({ series, task, entry, layout, rid, heldout: need.value.heldout, caseId, seriesDir });
  if (problem) return refuse(problem);
  const now = (io.now ?? systemClock)();
  const stamp = stampOf(now);
  const work = path.join(layout.dir, 'replay', `work-${stamp}`);
  mkdirSync(work, { recursive: true });
  try {
    return await replayIn({ io, need: need.value, seriesDir, entry, layout, rid, caseId, times, work, now, stamp });
  } finally {
    rmSync(work, { recursive: true, force: true });
  }
}

/**
 * Replay case `caseId` of session `rid` of the series in `seriesDir` (a series of `task`), `times` times. `io` = `{ home, say, worldFn, now?, signal?, abort? }`. -> `{ exit, message, file? }`: 0 done, 3 at least one run was a void (the file is
 * written all the same), 4 a refusal (nothing is written).
 */
export async function replay({ task, seriesDir, rid, caseId, times = 1, io }) {
  const bad = argsProblem({ caseId, times }) ?? seriesRootProblem(io.home, seriesDir);
  if (bad) return refuse(bad);
  const lock = acquireSeriesLock({ seriesDir });
  if (!lock.ok) return refuse(lock.reason);
  try {
    const series = loadSeries(seriesDir);
    if (!series.ok) return refuse(series.reason);
    return await replayLocked({ io, task, series: series.value, seriesDir, rid, caseId, times, entry: series.value.map.sessions[rid], layout: layoutPaths(path.join(seriesDir, 's'), rid) });
  } finally {
    lock.value.release();
  }
}
