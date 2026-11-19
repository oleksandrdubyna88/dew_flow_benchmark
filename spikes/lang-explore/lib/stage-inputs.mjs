// The inputs of a Stage 1 series (F3.S1 (a), PR 3): ONE `inputs.json` holds the series-wide parts once and, under `tasks.<NN>`, what a one-task `init` records for that task (its pins, the held-out identity, the sha256 of the generated files and
// of PACKAGES.md, the prompts, its limits). `inputsOfTask` hands back the one-task shape, so every check of the quick run (`frozenPinsProblem`, `heldoutMovedProblem`, `harnessMovedForStacks`, `seriesLimits`, `seriesPrompts`) takes a Stage 1
// series' inputs through it unchanged. Pure.
import { fail, ok } from '../../minigit-bench/lib/result.mjs';

/** The fields of a task's slice: what `init` records for ONE task (`init-quick.mjs` `quickInitExtra`, `commands.mjs` `initExtra`) plus its limits. */
export const TASK_SLICE_KEYS = Object.freeze(['taskPins', 'heldoutQuick', 'generatedSha256', 'packagesMdSha256', 'prompts', 'limits']);

/** Is this the inputs of a Stage 1 series? */
export const isStageInputs = inputs => inputs?.stage === 1 && inputs.tasks !== null && typeof inputs.tasks === 'object';

const wideOf = ({ tasks: _tasks, stage: _stage, arm: _arm, ...wide }) => wide;

/**
 * The inputs of ONE task of a series: for a Stage 1 series the series-wide parts and the task's slice, in the shape a one-task `init` writes (`task` is the task id); for any other series, the inputs as they are (a one-task series
 * has one task and `taskId` is not asked). -> `ok(inputs)` | `fail` naming the task and the tasks the series holds.
 */
export function inputsOfTask(inputs, taskId) {
  if (!isStageInputs(inputs)) return ok(inputs);
  if (!Object.hasOwn(inputs.tasks, taskId ?? '')) return fail(`task ${taskId}: not a task of this Stage 1 series (it holds ${Object.keys(inputs.tasks).join(', ')})`);
  return ok({ ...wideOf(inputs), task: taskId, ...inputs.tasks[taskId] });
}

/** The slice of one task out of the inputs a one-task `init` writes: the keys of `TASK_SLICE_KEYS` it holds. */
export const sliceOf = oneTask => Object.fromEntries(TASK_SLICE_KEYS.filter(k => oneTask[k] !== undefined).map(k => [k, oneTask[k]]));
