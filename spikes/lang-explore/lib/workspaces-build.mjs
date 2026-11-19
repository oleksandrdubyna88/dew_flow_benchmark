// The twelve freeze workspaces in memory (F2.S2 (d), D70): for the six tasks and both stacks, exactly the file map `quickWorkspaceFiles` gives a session, from the spec and FORMAT of one commit. Nothing is written here; the writer,
// the copy and the check all start from this one function, so what an agent is launched with, what the task repository publishes and what the check compares are one thing.
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { loadExplore } from './inputs.mjs';
import { quickExplore } from './quick.mjs';
import { packagesInputs, quickWorkspaceFiles } from './workspace-quick.mjs';
import { FREEZE_STACKS, folderOf, freezeTasks, sourcesAt } from './workspaces-sources.mjs';

/** What every freeze command needs, loaded once: the pins as the quick profile has them, the six tasks and the inputs `PACKAGES.md` is made from. `exploreFn` and `limit` are for tests. -> `ok({ explore, tasks, inputs, limit })` | `fail`. */
export function freezeContext({ home, exploreFn = loadExplore, limit = null }) {
  const base = exploreFn();
  if (!base.ok) return base;
  const tasks = freezeTasks(base.value);
  const inputs = packagesInputs();
  if (!tasks.ok) return tasks;
  return inputs.ok ? ok({ explore: quickExplore(base.value, home), tasks: tasks.value, inputs: inputs.value, limit }) : inputs;
}

/** One workspace's file map. -> `ok(files)` | `fail` naming the task and the stack. */
function filesOfOne({ ctx, task, stack, sources }) {
  const built = quickWorkspaceFiles({ explore: ctx.explore, task, stack, sources, inputs: ctx.inputs, limit: ctx.limit });
  return built.ok ? ok(built.value.files) : fail(`the ${stack} workspace of task ${task.id}: ${built.reason}`);
}

/** Both stacks' workspaces of one task from one commit. -> `ok([[folder, files]])` | `fail`. */
async function workspacesOfTask({ ctx, repo, sha, task }) {
  const sources = await sourcesAt({ repo, sha, task });
  if (!sources.ok) return fail(`task ${task.id}: ${sources.reason}`);
  const built = FREEZE_STACKS.map(stack => [folderOf(task, stack), filesOfOne({ ctx, task, stack, sources: sources.value })]);
  const bad = built.find(([, r]) => !r.ok);
  return bad === undefined ? ok(built.map(([folder, r]) => [folder, r.value])) : bad[1];
}

/**
 * The twelve workspaces from the spec and FORMAT of the commit `sha` of the task repository `repo`. -> `ok({ [folder]: files })` with the folders `csharp/<NN-slug>` and `node/<NN-slug>` in task order, stack by stack
 * inside a task | `fail` naming the first thing that cannot be made (an unreadable spec, a FORMAT that is not the frozen one, a spec whose samples cannot be read).
 */
export async function buildWorkspaces({ ctx, repo, sha }) {
  const all = [];
  for (const task of ctx.tasks) {
    const one = await workspacesOfTask({ ctx, repo, sha, task });
    if (!one.ok) return one;
    all.push(...one.value);
  }
  return ok(Object.fromEntries(all));
}
