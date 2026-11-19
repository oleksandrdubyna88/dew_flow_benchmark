// The Stage 1 series (F3.S1 (a), PR 3): ONE directory `x-<yyyymmdd>-<4 hex>` holds the A/A, the pilot and the 30 slots, so there is one ledger, one run-map and one alias baseline (D47). It is made through `createSeries`'s own steps,
// widened by its `stage` argument. `explore-series.json` holds `{ seriesId, createdAt, stage: 1, arm: 'bare', tasks, rounds, preregTag, preregCommit, seedSha256, pilot }`; `inputs.json` the series-wide parts once and, under
// `tasks.<NN>`, each task's slice (`stage-inputs.mjs`). Pure but for the directory it makes.
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { STAGE_ROUNDS } from './stage-schedule.mjs';
import { createSeries, exploreChildren, isStageState, loadSeries } from './series.mjs';
import { FREEZE_TASKS } from './workspaces-sources.mjs';

export { inputsOfTask, isStageInputs } from './stage-inputs.mjs';

export const STAGE = 1;
/** The arm of every Stage 1 session: bare (Stage 2 would add the process arm, and is not in F3). */
export const ARM = 'bare';

/**
 * What `createSeries` takes as its `stage` argument. `pre` is the pre-registration's answer (`{ preregTag, preregCommit, seedSha256, pilot }`: null tag, commit and seed for a rehearsal), `wide` the series-wide inputs and `slices` the
 * per-task slices (`{ '04': {...}, ... }`, one for each task of the freeze).
 */
export function stageParts({ pre, wide, slices }) {
  const { preregTag, preregCommit, seedSha256, pilot, rehearsalSeed } = pre;
  return {
    state: { stage: STAGE, arm: ARM, tasks: [...FREEZE_TASKS], rounds: STAGE_ROUNDS, preregTag, preregCommit, seedSha256, pilot, ...(rehearsalSeed === undefined ? {} : { rehearsalSeed }) },
    inputs: { stage: STAGE, arm: ARM, ...wide, tasks: slices },
    map: { stage: STAGE, preregTag, preregCommit, seedSha256 },
  };
}

/** The sentence for a missing slice: every task of the freeze has one. */
const sliceProblem = slices => FREEZE_TASKS.filter(task => slices?.[task] === undefined).map(task => `task ${task}`).join(', ');

/** Make the series directory (mode 0700): `ok({ dir, state })` | `fail`. `prefix` is `x` for a paid series and `r` for a rehearsal's. */
export function createStageSeries({ home, explore, pre, wide, slices, date, prefix = 'x', rand }) {
  const missing = sliceProblem(slices);
  if (missing) return fail(`a Stage ${STAGE} series holds a slice of every task of the freeze: ${missing} has none`);
  return createSeries({ home, explore, stage: stageParts({ pre, wide, slices }), date, prefix, ...(rand ? { rand } : {}) });
}

/** The Stage 1 series directories under the explore root (a rehearsal's included), whatever their names. */
export const stageSeriesDirs = home => exploreChildren(home).filter(c => c.state.ok && isStageState(c.state.value)).map(c => c.dir);

/** The series in `dir`, which must be a Stage 1 series: `ok({ dir, state, map })` | `fail` naming the verbs of an ordinary series. */
export function loadStageSeries(dir) {
  const series = loadSeries(dir);
  if (!series.ok) return series;
  return isStageState(series.value.state) ? series : fail(`${dir} is not a Stage ${STAGE} series: stage-run works on a series made by stage-init, a one-task series is run with \`run\``);
}
