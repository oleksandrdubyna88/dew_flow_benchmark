// The ONE road into a task's clear held-out folder for every reader that does not open it itself: `init`, `run`, `rescore` and `replay` (F2.S2 (d), bench PR 4a). In this order: the stale decision under the task's lock
// (`sweepClearFolder`: a folder with no live owner is removed after its key's backup is verified, a held one is used, one that differs from its archive is refused), the load (`loadHeldout`, which refuses a sealed task whose folder
// is absent through `sealedAbsentProblem` and never says `absent`), and the tree hash checked against the registry again (`openedFolderProblem`), so a pair is never scored over a folder that is not its archive's.
// `leakcheck` is the one reader that opens a sealed task for itself (`withOpenedHeldout`).
import path from 'node:path';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { loadHeldout } from './heldout.mjs';
import { openedFolderProblem } from './heldout-quick.mjs';
import { sweepClearFolder } from './heldout-lifecycle.mjs';
import { isQuick } from './quick.mjs';
import { taskPaths } from './tasks.mjs';

/** The load and the registry check: `ok(heldout)` | `fail`. */
function loadChecked({ home, task, heldout }) {
  const loaded = loadHeldout(home, task, heldout.frozenFile);
  if (!loaded.ok) return loaded;
  const problem = openedFolderProblem({ task, heldout: loaded.value, frozenFile: heldout.frozenFile });
  return problem === null ? loaded : fail(problem);
}

/** True when `heldout.slotFolder` (what `withSlotHeldout` handed its unit) is this task's own clear folder: the slot opened it, checked it against the registry and holds its lock for the whole unit, in this process. */
const isSlotsOwn = ({ home, task, heldout }) => heldout.slotFolder?.casesDir === path.join(taskPaths(task, home).heldoutDir, 'cases');

/**
 * `ok(heldout)` | `fail(sentence)`. `heldout` is the held-out context of the command (`frozenFile`, `backupFile`, `shareMount`, `keyFile` and the seams: `heldout-context.mjs`); none given reads today's defaults. A task of the olympiad
 * is loaded as it always was. A unit of a paid slot is handed the folder its slot opened (`heldout.slotFolder`): that slot holds the task's lock until the unit ends (the lock is not re-entrant), so the reader does not sweep, and the
 * registry check of `loadChecked` still runs. Any other reader takes the lock as before, so a second process is refused.
 */
export async function readerHeldout({ home, task, heldout = {} }) {
  if (!isQuick(task)) return loadHeldout(home, task);
  const swept = isSlotsOwn({ home, task, heldout }) ? ok(null) : await sweepClearFolder({ ...heldout, home, task });
  return swept.ok ? loadChecked({ home, task, heldout }) : fail(swept.reason);
}
