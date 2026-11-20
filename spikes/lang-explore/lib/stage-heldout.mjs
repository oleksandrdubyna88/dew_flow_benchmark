// The clear held-out folder of ONE slot of Stage 1 (F3.S1 (a), PR 4; stories document section 5, brief item 6). A slot runs inside `withSlotHeldout` for its own task: the folder is made from the archive and checked against the registered tree
// hash, and it is removed when the slot ends, whether the slot returns or throws (`withOpenedHeldout`, `heldout-lifecycle.mjs`), and a folder that is still in clear after the slot is said on every way out. Around that, three things the lifecycle does not decide for a series:
//   - a slot is refused BEFORE its launch when the key's backup copy cannot be verified on the share's mount (the removal after the slot needs it, so a slot that could not be closed is never opened);
//   - a stale clear folder of the slot's own task, left by an interrupted run, is removed first (`sweepClearFolder`, the stale decision every entry point takes), and one that is held by a live process or the operator is a refusal;
//   - only the slot's own task is ever in clear: every other task of the six is swept first, and one that is still in clear and held refuses the slot.
// Never prints, returns or logs a case, a trap or an id: the only text is the lifecycle's own sentences about folders, hashes and holders.
import { existsSync } from 'node:fs';
import path from 'node:path';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { backupProblem } from './heldout-key.mjs';
import { sweepClearFolder, withOpenedHeldout } from './heldout-lifecycle.mjs';
import { registeredEntry } from './heldout-registry.mjs';
import { taskPaths } from './tasks.mjs';

const casesDirOf = ({ task, home }) => path.join(taskPaths(task, home).heldoutDir, 'cases');

/** The sentence for another task's folder after the sweep, or null: it must be gone (a held one, or one with no registry entry, is still in clear). */
function strayProblem({ other, swept }) {
  if (!swept.ok) return `the clear folder of task ${other.id} cannot be judged (${swept.reason}): only the slot's own task is ever in clear`;
  return ['absent', 'unsealed'].includes(swept.value.cases) ? null : `the clear folder of task ${other.id} is still in clear (${swept.value.cases}${swept.value.hold ? `, held by ${swept.value.hold.kind}` : ''}): only the slot's own task is ever in clear; close it with explore.mjs heldout-close --task ${other.id}`;
}

/** The refusal of the first other task that is still in clear after its stale folder was swept, or null. Each is swept under its own task lock. */
async function strayTask({ task, tasks, heldout }) {
  for (const other of tasks.filter(t => t.id !== task.id)) {
    const problem = strayProblem({ other, swept: await sweepClearFolder({ ...heldout, task: other }) });
    if (problem !== null) return problem;
  }
  return null;
}

/** The refusal before the launch: the task has a registry entry and its key's backup copy is verified on the share's mount, or null. */
async function backupRefusal({ task, heldout }) {
  const entry = registeredEntry({ frozenFile: heldout.frozenFile, task });
  if (!entry.ok) return entry.reason;
  if (entry.value === undefined) return `task ${task.id} has no sealed archive registered: a slot has no held-out folder to open`;
  return backupProblem({ task, backupFile: heldout.backupFile, shareMount: heldout.shareMount, keySha256: entry.value.keySha256, mountOf: heldout.mountOf, fs: heldout.fs });
}

/** The own task's stale folder, swept first: a folder that is still there afterwards is held by a live reader or the operator, and a slot cannot be sure to close it. -> a sentence or null. */
async function staleProblem({ task, heldout }) {
  const swept = await sweepClearFolder({ ...heldout, task });
  if (!swept.ok) return swept.reason;
  return swept.value.cases === 'held' ? `the clear folder of task ${task.id} is held by ${swept.value.hold.kind === 'operator' ? 'the operator' : `pid ${swept.value.hold.pid}`}: a slot opens its own folder and removes it after; close it first with explore.mjs heldout-close --task ${task.id}` : null;
}

/** The refusals before the launch, in order: another task in clear, the backup, the own task's stale folder. -> a sentence or null. */
async function launchRefusal({ task, tasks, heldout }) {
  return (await strayTask({ task, tasks, heldout })) ?? (await backupRefusal({ task, heldout })) ?? (await staleProblem({ task, heldout }));
}

const isOpen = ({ task, heldout }) => existsSync(casesDirOf({ task, home: heldout.home }));

const leftOpenSentence = task => `the clear folder of task ${task.id} was left open after the slot (its key's backup could not be verified, or the removal failed): run explore.mjs heldout-close --task ${task.id} before the next slot`;

/** The end of a slot that returned (`ran` is what `withOpenedHeldout` gave), whatever it returned: a folder still in clear after it is the halt sentence, joined to the reason of an open that failed; what the slot returned is kept as `result`. */
function afterSlot({ task, heldout, ran }) {
  if (!isOpen({ task, heldout })) return ran;
  return ran.ok ? fail(leftOpenSentence(task), { result: ran.value }) : fail(`${ran.reason}; ${leftOpenSentence(task)}`);
}

/** The error of a slot that threw: the slot's own, and when its folder is still in clear the halt sentence joined to it (the throw still propagates: the series does not go on). */
const afterThrow = ({ task, heldout, error }) => (isOpen({ task, heldout }) ? new Error(`${error.message}; ${leftOpenSentence(task)}`, { cause: error }) : error);

/**
 * Run `fn({ casesDir, opened })` over the clear held-out folder of `task`, made for this slot and removed after it. `tasks` are the tasks of the series; `heldout` is the held-out context (`heldout-context.mjs`: `home`, `frozenFile`, `keyFile`,
 * `backupFile`, `shareMount` and the seams). -> `ok(what fn returns)`, or `fail(sentence)` when the slot was refused before it began (`fn` did not run) or, with `result` (what `fn` returned), when the folder could not be removed after it.
 * A folder that is still in clear after the slot is always said, on every way out: after a return (`fail` with `result`), after an open that failed once the folder was placed (`fail`, the open's reason and the sentence) and after a throw (the
 * error is rethrown with the sentence joined to its message and the original as `cause`, so the series does not go on and the operator still reads `heldout-close`).
 */
export async function withSlotHeldout({ task, tasks, heldout, fn }) {
  const refused = await launchRefusal({ task, tasks, heldout });
  if (refused !== null) return fail(refused);
  try {
    return afterSlot({ task, heldout, ran: await withOpenedHeldout({ ...heldout, task, fn: async arg => ok(await fn(arg)) }) });
  } catch (error) {
    throw afterThrow({ task, heldout, error });
  }
}
