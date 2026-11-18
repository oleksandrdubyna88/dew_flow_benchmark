// `explore.mjs workspaces --check --root <checkout>` (F2.S2 (d), amendment 7): the twelve workspaces are built in memory again from the spec and FORMAT of the checkout's HEAD and compared with the committed folders, per
// workspace by tree hash, and then byte for byte so the file that differs is named. F3.S1's `init` calls `checkWorkspaces`, so what an agent is launched with and restored from is what the task repository publishes.
//   success: the last line is `workspaces-check ok commit <HEAD> tasks 04,05,06,07,08,10 stacks csharp,node`, exit 0; a finding prints its lines and exits 1.
import { existsSync } from 'node:fs';
import path from 'node:path';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { gitText, repoRootOf } from './leak-git.mjs';
import { buildWorkspaces } from './workspaces-build.mjs';
import { statusPaths } from './workspaces-copy.mjs';
import { FREEZE_STACKS, FREEZE_TASKS } from './workspaces-sources.mjs';
import { compareFiles, readFolderFiles, treeHashOfFiles } from './workspaces-tree.mjs';

/** The success line, exactly. */
export const checkLine = commit => `workspaces-check ok commit ${commit} tasks ${FREEZE_TASKS.join(',')} stacks ${FREEZE_STACKS.join(',')}`;

const GRAMMAR = 'runner/GRAMMAR.md';

/** The files of a committed workspace folder. -> `ok({ files })` | `fail(line)` when the folder is absent or holds an entry that is no regular file. */
function readCommitted({ root, folder }) {
  const dir = path.join(root, folder);
  if (!existsSync(dir)) return fail(`${folder}: the workspace folder is missing`);
  const read = readFolderFiles(dir);
  return read.ok ? read : fail(`${folder}: ${read.reason}`);
}

const grammarNote = line => (line.startsWith(`${GRAMMAR}: differs`) ? ' (the grammar is not the frozen FORMAT\'s)' : '');

/** The finding lines of one workspace folder: it is absent, unreadable, or differs (the tree hash first, then each file). */
function findingsOfFolder({ root, folder, expected }) {
  const read = readCommitted({ root, folder });
  if (!read.ok) return [read.reason];
  if (treeHashOfFiles(read.value.files) === treeHashOfFiles(expected)) return [];
  const files = compareFiles(expected, read.value.files).map(line => `${folder}/${line}${grammarNote(line)}`);
  return [`${folder}: the tree hash differs from the workspace made from the spec and FORMAT of HEAD`, ...files];
}

/** The commit of HEAD and whether the checkout has any change. -> `ok({ commit })` | `fail`. */
async function headOf(root) {
  const head = await gitText(root, ['rev-parse', '--verify', 'HEAD^{commit}'], 'HEAD of the checkout');
  if (!head.ok) return head;
  const status = await gitText(root, ['status', '--porcelain=v1', '-z', '--untracked-files=all'], 'the status of the checkout');
  if (!status.ok) return status;
  const changed = statusPaths(status.value);
  return changed.length === 0 ? ok({ commit: head.value.trim() }) : fail(`the checkout ${root} is not clean (${changed.slice(0, 5).join(', ')}): the check judges the commit it names, not a working tree`);
}

/**
 * Compare the committed workspaces of `root` (the top folder of a checkout of the task repository) with the ones built from its HEAD. `ctx` is `freezeContext`'s value. -> `ok({ commit, findings: [line] })`, `findings` empty when
 * every workspace is equal | `fail` when the check could not be made (not a checkout, a dirty one, a FORMAT that is not the frozen one, a spec that cannot be read).
 */
export async function checkWorkspaces({ root, ctx }) {
  const top = await repoRootOf(root);
  if (!top.ok) return top;
  const head = await headOf(top.value);
  if (!head.ok) return head;
  const built = await buildWorkspaces({ ctx, repo: top.value, sha: head.value.commit });
  if (!built.ok) return built;
  const findings = Object.entries(built.value).flatMap(([folder, expected]) => findingsOfFolder({ root: top.value, folder, expected }));
  return ok({ commit: head.value.commit, findings });
}
