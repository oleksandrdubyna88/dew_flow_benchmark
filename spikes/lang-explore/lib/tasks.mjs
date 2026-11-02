// The tasks of the pilot (plan research/PLAN_lang_explore_olympiad.md D-O2): ONE road from `--task NN` to everything that differs per task: the spec and FORMAT pins, the private notes, the held-out folder, the
// calibration file and the note's label. Nothing else in the pilot names a task's folder or file, so a command that forgot its task cannot fall back to task 1's (a test scans for it). Pure: values in, values out.
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { loadExplore } from './inputs.mjs';
import { underHome } from './paths.mjs';

/** A series made before tasks existed (the task-1 series) records no task: it is task 1's. */
export const LEGACY_TASK = '01';

/** The legal task ids of the pins, in order. */
export const taskIds = explore => Object.keys(explore.tasks);

const legalSentence = explore => `the tasks are ${taskIds(explore).join(', ')}`;

/** `--task` as given -> `ok(task)` (the pins entry plus its `id`) | `fail` naming the legal ids. An id is written exactly as the map has it (`02`, not `2`). */
export function resolveTask(explore, id) {
  if (typeof id !== 'string' || id === '') return fail(`--task is required: ${legalSentence(explore)}`);
  if (!Object.hasOwn(explore.tasks, id)) return fail(`--task ${id} is not a task of the pilot: ${legalSentence(explore)}`);
  return ok(Object.freeze({ id, ...explore.tasks[id] }));
}

/** The task a series was made for. */
export const taskOfSeries = state => state?.task ?? LEGACY_TASK;

/** Where a task's private material is under `home`: the held-out folder, the calibration file and the private test notes. */
export const taskPaths = (task, home) => ({
  heldoutDir: underHome(task.heldoutDirParts, home), calibrationFile: underHome(task.calibrationParts, home), notesFile: underHome(task.privateNotes.relativePath.split('/'), home),
});

/** The sentence refusing a command whose task is not its series' task, or null. */
export const seriesTaskProblem = ({ state, task }) => (taskOfSeries(state) === task.id ? null : `series ${state.seriesId} is a series of task ${taskOfSeries(state)}, not of task ${task.id}: a command works on one task's series`);

/** The pins and the task a command was given: `values.task` (the parsed `--task`) resolved against `inputs/explore.json`. -> `ok({ explore, task })` | `fail`. */
export function taskFromValues(values, exploreFn = loadExplore) {
  const explore = exploreFn();
  if (!explore.ok) return explore;
  const task = resolveTask(explore.value, values.task);
  return task.ok ? ok({ explore: explore.value, task: task.value }) : task;
}

/** The pins a series freezes at `init` (`inputs.json` `taskPins`): the task's spec, FORMAT and private notes. */
export const pinsOfTask = task => ({ spec: task.spec, format: task.format, privateNotes: task.privateNotes });

/**
 * The sentence refusing a series whose frozen pins are not the task's pins now (an amendment after `init`: a new series is needed), or null. A draft task's series that froze none is refused; task 1's closed series,
 * made before the freeze, is not.
 */
export function frozenPinsProblem({ inputs, task, seriesId }) {
  const frozen = inputs?.taskPins;
  if (frozen === undefined) return task.draft ? `series ${seriesId} froze no pins of task ${task.id}: make a new series with \`explore.mjs init --task ${task.id}\`` : null;
  const now = pinsOfTask(task);
  const moved = Object.keys(now).filter(k => JSON.stringify(frozen[k]) !== JSON.stringify(now[k]));
  return moved.length === 0 ? null : `the pins of task ${task.id} changed since series ${seriesId} was made (${moved.join(', ')}): a new series is needed after an amendment`;
}
