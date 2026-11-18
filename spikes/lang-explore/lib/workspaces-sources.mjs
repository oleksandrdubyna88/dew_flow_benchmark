// What the freeze workspaces are made from (F2.S2 (d)): the six tasks of the freeze, each task's spec and FORMAT read from the task repository at ONE commit (the tag's commit, or HEAD of a checkout), and the refusal of a FORMAT
// that is not the frozen one. The spec is read as it is at that commit (its `Freeze tag` row differs from the gated text, so its pinned hash is not asked); FORMAT must hash to the task's pin, which is the step-2a pin of
// `lang-bench/inputs/specs.d/format.json`: a grammar of another FORMAT never reaches a workspace (T-L57's freeze half).
import { createHash } from 'node:crypto';
import path from 'node:path';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { finished, failed, git, resolveCommit } from './leak-git.mjs';
import { resolveTask } from './tasks.mjs';

/** The tasks of the freeze (B), in order, and the two stacks. */
export const FREEZE_TASKS = Object.freeze(['04', '05', '06', '07', '08', '10']);
export const FREEZE_STACKS = Object.freeze(['csharp', 'node']);

/** The folder name of a task's workspace: its spec's file name without `.md` (`04-rule-api`). */
export const slugOf = task => path.basename(task.spec.path, '.md');

/** The folder of one workspace under a root: `csharp/04-rule-api`. */
export const folderOf = (task, stack) => `${stack}/${slugOf(task)}`;

/** The six tasks of the freeze from the pins, each a quick task. -> `ok([task])` | `fail` naming the task that is not one. */
export function freezeTasks(explore) {
  const tasks = FREEZE_TASKS.map(id => resolveTask(explore, id));
  const bad = tasks.find(t => !t.ok || t.value.quick !== true);
  return bad === undefined ? ok(tasks.map(t => t.value)) : fail(bad.ok ? 'a task of the freeze is not a quick-run task' : bad.reason);
}

const sha256 = bytes => createHash('sha256').update(bytes).digest('hex');

/** The bytes of `file` at the commit `sha` of `repo`. -> `ok(Buffer)` | `fail`. */
export async function blobAt({ repo, sha, file }) {
  const r = await git(repo, ['cat-file', 'blob', `${sha}:${file}`], { bytes: true });
  return finished(r) ? ok(r.value.stdoutBytes) : failed(`${file} at ${sha.slice(0, 12)}`, r);
}

/** The FORMAT of a task at the commit, or a refusal when it is not the frozen one. */
async function formatAt({ repo, sha, task }) {
  const bytes = await blobAt({ repo, sha, file: task.format.path });
  if (!bytes.ok) return bytes;
  const hash = sha256(bytes.value);
  return hash === task.format.sha256 ? ok(bytes.value.toString('utf8')) : fail(`FORMAT.md at ${sha.slice(0, 12)} hashes to ${hash.slice(0, 12)}, the frozen FORMAT's pin of task ${task.id} is ${task.format.sha256.slice(0, 12)}: it is not the frozen FORMAT, so no workspace is made from it`);
}

/** The spec and FORMAT texts of one task at the commit `sha`. -> `ok({ spec, format })` | `fail`. */
export async function sourcesAt({ repo, sha, task }) {
  const spec = await blobAt({ repo, sha, file: task.spec.path });
  if (!spec.ok) return spec;
  const format = await formatAt({ repo, sha, task });
  return format.ok ? ok({ spec: spec.value.toString('utf8'), format: format.value }) : format;
}

/** The commit a ref (a tag, a branch, a sha) names in `repo`. -> `ok(sha)` | `fail`. */
export const commitOf = (repo, ref) => resolveCommit(repo, ref);
