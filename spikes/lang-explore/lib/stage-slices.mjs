// The per-task slices of a Stage 1 series (`stage-inputs.mjs`), made at `stage-init`: for each task of the freeze exactly what a one-task `init` records (`quickInitExtra`: the prompts, the held-out identity, the sha256 of the generated files and of
// PACKAGES.md) beside its pins and its limits. A REAL series reads the registered held-out side; a REHEARSAL's slice holds fixture cases of its own (never the real ones, D41).
import path from 'node:path';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { casesTreeHash } from './heldout-quick.mjs';
import { bytesOf, quickInitExtra, quickPrompts } from './init-quick.mjs';
import { loadCalibration } from './limits.mjs';
import { exploreRoot } from './paths.mjs';
import { quickExplore } from './quick.mjs';
import { rehearsalHeldout } from './rehearsal-quick.mjs';
import { pinsOfTask } from './tasks.mjs';
import { taskSources } from './workspace.mjs';
import { freezeTasks } from './workspaces-sources.mjs';

/** One task's slice of a REAL series: what a one-task `init` records beside the task's limits; the series-wide parts (the harness pins) are left out. */
export async function realSlice({ base, task, home, heldout, calibration }) {
  const extra = await quickInitExtra({ explore: base, task, home, heldout });
  if (!extra.ok) return extra;
  const { runnerPin: _pin, templatesIdentity: _identity, templateHashes: _hashes, ...mine } = extra.value;
  return ok({ taskPins: pinsOfTask(task), ...mine, limits: calibration.limits });
}

/** The held-out identity of a rehearsal's fixture cases, kept in a folder of the explore root shared by the rehearsals (the content is fixed per kind). */
function fixtureIdentity({ task, home }) {
  const held = rehearsalHeldout({ dir: path.join(exploreRoot(home), 'rehearsal-fixtures', task.id), task });
  return { treeSha256: casesTreeHash(held.casesDir).value, caseCount: held.caseIds.length, trapCount: 0, parsed: 'rehearsal fixture' };
}

/** One task's slice of a REHEARSAL: the fixture held-out cases, and the same prompts and generated hashes. */
export async function rehearsalSlice({ base, task, home, calibration }) {
  const bytes = await bytesOf({ explore: base, task, home, sourcesFn: taskSources });
  if (!bytes.ok) return bytes;
  return ok({ taskPins: pinsOfTask(task), prompts: quickPrompts(quickExplore(base, home)), ...bytes.value, heldoutQuick: fixtureIdentity({ task, home }), limits: calibration.limits });
}

/** One task's slice, or the sentence naming the task: its limits first, then `make` (the real slice, a rehearsal's, or a test's). */
async function oneSlice({ make, base, task, io }) {
  const calibration = loadCalibration(io.home, task, () => ok(base));
  if (!calibration.ok) return fail(`task ${task.id}: ${calibration.reason}`);
  const made = await make({ base, task, home: io.home, heldout: io.heldoutArgs, calibration: calibration.value });
  return made.ok ? made : fail(`task ${task.id}: ${made.reason}`);
}

/** The `[id, slice]` entries so far plus the next task's, or the first refusal (a refusal is carried on: the tasks after it are not made). */
const withNext = ({ make, base, io }) => async (soFar, task) => {
  const done = await soFar;
  if (!done.ok) return done;
  const one = await oneSlice({ make, base, task, io });
  return one.ok ? ok([...done.value, [task.id, one.value]]) : one;
};

/** The `[id, slice]` entries of the tasks in order, up to the first task that refuses: `ok(entries)` | `fail`. */
const entriesOf = ({ tasks, make, base, io }) => tasks.reduce(withNext({ make, base, io }), Promise.resolve(ok([])));

/** The slice of every task of the freeze, in order; the first task whose held-out side or sources are not the registered ones refuses the series, named. -> `ok({ slices })` | `fail`. */
export async function slicesOf({ io, base, make }) {
  const tasks = freezeTasks(base);
  if (!tasks.ok) return tasks;
  const entries = await entriesOf({ tasks: tasks.value, make, base, io });
  return entries.ok ? ok({ slices: Object.fromEntries(entries.value) }) : entries;
}
