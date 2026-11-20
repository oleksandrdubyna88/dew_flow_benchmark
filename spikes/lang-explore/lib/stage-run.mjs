// `stage-run --series <dir> --what aa | pilot:<NN> | next [--until round] [--dry-run] [--accept-noisy <why>]` (F3.S1 (a), PR 3; SPENDS MONEY unless a rehearsal series or `--dry-run`). `next` is the schedule's next slot and no flag names another data pair; a void's one
// re-run is the schedule's next step, so `next` runs it, alone, before any later slot. `--until round` repeats `next` until the round's last slot is finished, a halt, a stop, a file `<series>/pause` (checked between slots only: a running pair is never
// interrupted by it) or a read-back slot has just been scored (exit 3, naming the slot and the command that prints its read-back: nothing later runs until its dated lines are on main). One series lock is held from the first launch to the last scoring.
import { existsSync } from 'node:fs';
import path from 'node:path';
import { acquireSeriesLock } from '../../minigit-bench/lib/series-lock.mjs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { isRehearsal, pairComplete, seriesInputs } from './series.mjs';
import { stageDryRun } from './stage-dryrun.mjs';
import { amendmentsReport, readGates } from './stage-gates.mjs';
import { DATA_KIND } from './stage-schedule.mjs';
import { EXIT, exitOfRefusal, openStage, runStageUnit } from './stage-step.mjs';
import { unitOf, whatOf } from './stage-unit.mjs';

const refused = message => ({ exit: EXIT.config, message });

/** The arguments: `ok({ dir, what, until })` | `fail`. `--series` is required (a paid slot is never run on a series this command guessed), `--what` one of the three, `--until` only `round` and only with `next`. */
const untilProblem = ({ until, what }) => (until === undefined || (until === 'round' && what === 'next') ? null : '--until round goes with --what next only');

export function stageRunArgs(values) {
  if (!values.series) return fail('stage-run needs --series <dir>: a paid slot is never started on a series this command guessed (`stage-init` prints the directory)');
  const what = whatOf(values.what);
  return what.ok ? checkedArgs({ values, what: what.value }) : what;
}

function checkedArgs({ values, what }) {
  const problem = untilProblem({ until: values.until, what });
  return problem ? fail(problem) : ok({ dir: path.resolve(values.series), what, until: values.until ?? null });
}

/** The dry run: the five rounds, the masks and the cap, printed and claimed for nothing. */
function dryRun({ dir, io }) {
  const series = openStage({ seriesDir: dir, io });
  const read = series.ok ? readGates({ series: series.value, io }) : series;
  if (!read.ok) return refused(read.reason);
  const { caps } = seriesInputs(series.value.dir).value;
  const seed = read.value.gates.seedRevealed ? read.value.gates.seed : null;
  return { exit: EXIT.done, message: stageDryRun({ caps, seed }).join('\n') };
}

/** Is the slot that just ran a finished read-back slot of a PAID series? (`stage-run` ends its round loop there; a rehearsal is not stopped, no judge reads it: `stage-gates.mjs` gives its slots lines that hold.) */
const readbackDone = ({ dir, unit, io }) => {
  const series = openStage({ seriesDir: dir, io });
  return unit.readback && !isRehearsal(dir) && series.ok && pairComplete(series.value.map, unit.pair, DATA_KIND);
};

const readbackStop = ({ dir, unit }) => ({ exit: EXIT.halt, message: `slot ${unit.pair} (round ${unit.round}, task ${unit.task}) is a read-back slot: nothing later runs until its two dated read-back lines are on main; the print is \`node research/lang-v2/tools/readback.mjs --series ${dir} --pair ${unit.pair}\``, unit });

/** The schedule's next unit after one ran, or the reason there is none. */
function nextAfter({ dir, io }) {
  const series = openStage({ seriesDir: dir, io });
  const read = series.ok ? readGates({ series: series.value, io }) : series;
  return read.ok ? unitOf({ what: 'next', state: series.value.state, map: series.value.map, gates: read.value.gates }) : read;
}

const pauseEnd = ({ dir, result }) => (existsSync(path.join(dir, 'pause')) ? { exit: EXIT.done, message: `${result.message}; the pause file ${path.join(dir, 'pause')} exists: stopped between slots`, unit: result.unit } : null);

const sameRound = (next, result) => next.value !== null && next.value.round === result.unit.round;

/** The end of the round, or `null` to go on. The next slot in another round, or none (the series is complete), is "round finished"; a schedule that REFUSES (a stop, a gate or the amendments file that cannot be read) ends the loop with its reason and the exit of that refusal. */
const roundEnd = ({ result, ...args }) => {
  const next = nextAfter(args);
  if (!next.ok) return { exit: exitOfRefusal(next.reason), message: next.reason, unit: result.unit };
  return sameRound(next, result) ? null : { ...result, message: `${result.message}; the round's last slot is finished` };
};

const readbackEnd = ({ dir, io, result }) => (readbackDone({ dir, unit: result.unit, io }) ? readbackStop({ dir, unit: result.unit }) : null);

/** What ends a round loop after a unit that ran well: a read-back slot, the pause file, the end of the round or of the series; else `null` (go on). */
const afterUnit = args => [readbackEnd, pauseEnd, roundEnd].map(check => check(args)).find(Boolean) ?? null;

const MOST_STEPS = 12;
const endsLoop = result => result.exit !== EXIT.done || result.finished === true;

/** `--until round`: `next` over and over, from the round the first slot belongs to. A halt, a stop or a refusal ends it with its own exit. */
async function untilRound({ dir, io }) {
  for (let i = 0; i < MOST_STEPS; i += 1) {
    const result = await runStageUnit({ dir, what: 'next', io });
    const ended = endsLoop(result) ? result : afterUnit({ dir, io, result });
    if (ended) return ended;
  }
  return { exit: EXIT.failed, message: `a round holds six slots and a void re-run each: the loop did not end in ${MOST_STEPS} steps` };
}

const runWith = ({ dir, what, until, io }) => (until === null ? runStageUnit({ dir, what, io }) : untilRound({ dir, io }));

/** Run under the series lock, held from the first launch to the last scoring and released on every way out. */
async function underLock({ args, io }) {
  const lock = acquireSeriesLock({ seriesDir: args.dir });
  if (!lock.ok) return refused(lock.reason);
  try {
    return await runWith({ ...args, io });
  } finally {
    lock.value.release();
  }
}

/** The verb. `io` = `{ home, say, now, abort, signal, worldFn, tokenFn, heldoutArgs, acceptNoisy, stage: { guardFn, amendmentsFile, seedFile, pilotRand }, runSeams, seams }`. -> `{ exit, message, ... }`. */
export async function stageRun({ values, io }) {
  const args = stageRunArgs(values);
  const opened = args.ok ? openStage({ seriesDir: args.value.dir, io }) : args;
  if (!opened.ok) return refused(opened.reason);
  amendmentsReport(io).forEach(line => io.say(line));
  return values['dry-run'] === true ? dryRun({ dir: args.value.dir, io }) : underLock({ args: args.value, io });
}
