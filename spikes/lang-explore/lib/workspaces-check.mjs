// `explore.mjs workspaces --check --root <checkout>` (F2.S2 (d), amendment 7): the twelve workspaces are built in memory again from the spec and FORMAT of the checkout's HEAD and compared with the committed folders, per
// workspace by tree hash, and then byte for byte so the file that differs is named. F3.S1's `init` calls `checkWorkspaces`, so what an agent is launched with and restored from is what the task repository publishes.
//   success: the last line is `workspaces-check ok commit <HEAD> tasks 04,05,06,07,08,10 stacks csharp,node`, exit 0; a finding prints its lines and exits 1.
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { failed, finished, git, gitText, repoRootOf } from './leak-git.mjs';
import { buildWorkspaces } from './workspaces-build.mjs';
import { statusPaths } from './workspaces-copy.mjs';
import { FREEZE_STACKS, FREEZE_TASKS } from './workspaces-sources.mjs';
import { compareFiles, treeHashOfFiles } from './workspaces-tree.mjs';

/** The success line, exactly. */
export const checkLine = commit => `workspaces-check ok commit ${commit} tasks ${FREEZE_TASKS.join(',')} stacks ${FREEZE_STACKS.join(',')}`;

const GRAMMAR = 'runner/GRAMMAR.md';

const MODES = Object.freeze({ '100644': 0o644, '100755': 0o755 });

/** The entries of a folder in HEAD's tree: `ok([{ rel, mode, sha }])` | `fail(line)` for an entry that is no regular file (a link, a submodule). */
async function treeEntries({ root, folder, sha }) {
  const listed = await gitText(root, ['ls-tree', '-r', '-z', sha, '--', folder], `the tree of ${folder}`);
  if (!listed.ok) return listed;
  const entries = listed.value.split('\0').filter(Boolean).map(record => {
    const [meta, file] = [record.slice(0, record.indexOf('\t')), record.slice(record.indexOf('\t') + 1)];
    const [mode, , object] = meta.split(' ');
    return { rel: file.slice(folder.length + 1), mode, object };
  });
  const odd = entries.find(entry => MODES[entry.mode] === undefined);
  return odd === undefined ? ok(entries) : fail(`${folder}/${odd.rel} is not a regular file (mode ${odd.mode}: a link or another kind of entry)`);
}

/** One object of a `cat-file --batch` answer at `at`: `{ blob, next }`, or `{ problem }` when it is not a blob. */
function batchEntry({ out, at, object }) {
  const eol = out.indexOf(0x0a, at);
  const [, type, size] = out.toString('latin1', at, eol).split(' ');
  return type === 'blob' ? { blob: out.subarray(eol + 1, eol + 1 + Number(size)), next: eol + 2 + Number(size) } : { problem: `${object} is not a file of HEAD` };
}

/** The blobs of a `cat-file --batch` answer for `objects`, in order: `ok([Buffer])` | a fail naming the object that is not a blob. */
function splitBatch({ out, objects }) {
  const blobs = [];
  let at = 0;
  for (const object of objects) {
    const entry = batchEntry({ out, at, object });
    if (entry.problem !== undefined) return fail(entry.problem);
    blobs.push(entry.blob);
    at = entry.next;
  }
  return ok(blobs);
}

/** The blobs of `objects` through one `git cat-file --batch`, in order: `ok([Buffer])` | a fail. */
async function readBlobs({ root, objects }) {
  if (objects.length === 0) return ok([]);
  const ran = await git(root, ['cat-file', '--batch'], { input: `${objects.join('\n')}\n`, bytes: true });
  return finished(ran) ? splitBatch({ out: ran.value.stdoutBytes, objects }) : failed('the files of HEAD', ran);
}

/** The files of a committed workspace folder, read from HEAD's TREE (never the disk: an ignored or assume-unchanged file is not what the commit holds). -> `ok({ files })` | `fail(line)`. */
async function readCommitted({ root, folder, sha }) {
  const entries = await treeEntries({ root, folder, sha });
  if (!entries.ok) return entries;
  if (entries.value.length === 0) return fail(`${folder}: the workspace folder is missing from HEAD`);
  const blobs = await readBlobs({ root, objects: entries.value.map(entry => entry.object) });
  if (!blobs.ok) return fail(`${folder}: ${blobs.reason}`);
  return ok({ files: Object.fromEntries(entries.value.map((entry, index) => [entry.rel, { bytes: blobs.value[index], mode: MODES[entry.mode] }])) });
}

const grammarNote = line => (line.startsWith(`${GRAMMAR}: differs`) ? ' (the grammar is not the frozen FORMAT\'s)' : '');

/** The finding lines of one workspace folder: it is absent, unreadable, or differs (the tree hash first, then each file). */
async function findingsOfFolder({ root, folder, expected, sha }) {
  const read = await readCommitted({ root, folder, sha });
  if (!read.ok) return [read.reason.startsWith(folder) ? read.reason : `${folder}: ${read.reason}`];
  if (treeHashOfFiles(read.value.files) === treeHashOfFiles(expected)) return [];
  const files = compareFiles(expected, read.value.files).map(line => `${folder}/${line}${grammarNote(line)}`);
  return [`${folder}: the tree hash differs from the workspace made from the spec and FORMAT of HEAD`, ...files];
}

/** The paths of a `status --porcelain=v1 -z` that start with `marker` (`!!` for an ignored one), without the marker. */
const markedPaths = (text, marker) => text.split('\0').filter(entry => entry.startsWith(`${marker} `)).map(entry => entry.slice(3));

/** The commit of HEAD and whether the checkout has any change (ignored files apart: `ignoredUnder` judges those). -> `ok({ commit, ignored })` | `fail`. */
async function headOf(root) {
  const head = await gitText(root, ['rev-parse', '--verify', 'HEAD^{commit}'], 'HEAD of the checkout');
  if (!head.ok) return head;
  const status = await gitText(root, ['status', '--porcelain=v1', '-z', '--untracked-files=all', '--ignored=matching'], 'the status of the checkout');
  if (!status.ok) return status;
  const changed = statusPaths(status.value.split('\0').filter(entry => !entry.startsWith('!! ')).join('\0'));
  const ignored = markedPaths(status.value, '!!');
  return changed.length === 0 ? ok({ commit: head.value.trim(), ignored }) : fail(`the checkout ${root} is not clean (${changed.slice(0, 5).join(', ')}): the check judges the commit it names, not a working tree`);
}

/** The sentence when an ignored path lies under one of the workspace folders (an engine file kept on disk and out of the commit), else null. */
function ignoredUnder({ ignored, folders }) {
  const hits = ignored.filter(file => folders.some(folder => file === folder || file.startsWith(`${folder}/`)));
  return hits.length === 0 ? null : `ignored files under the workspace folders (${hits.slice(0, 5).join(', ')}): a .gitignore must not hide a file of a workspace; the check judges the commit`;
}

/** The checkout's top folder, its HEAD and the workspaces built from that HEAD: `ok({ top, head, built })` | the first fail. */
async function builtFrom({ root, ctx }) {
  const top = await repoRootOf(root);
  const head = top.ok ? await headOf(top.value) : top;
  const built = head.ok ? await buildWorkspaces({ ctx, repo: top.value, sha: head.value.commit }) : head;
  return built.ok ? ok({ top: top.value, head: head.value, built: built.value }) : built;
}

/**
 * Compare the committed workspaces of `root` (the top folder of a checkout of the task repository) with the ones built from its HEAD, read from HEAD's tree. `ctx` is `freezeContext`'s value. -> `ok({ commit, findings: [line] })`, `findings` empty when
 * every workspace is equal | `fail` when the check could not be made (not a checkout, a dirty one, an ignored file under a workspace folder, a FORMAT that is not the frozen one, a spec that cannot be read).
 */
export async function checkWorkspaces({ root, ctx }) {
  const got = await builtFrom({ root, ctx });
  if (!got.ok) return got;
  const { top, head, built } = got.value;
  const hidden = ignoredUnder({ ignored: head.ignored, folders: Object.keys(built) });
  if (hidden !== null) return fail(hidden);
  const lists = await Promise.all(Object.entries(built).map(([folder, expected]) => findingsOfFolder({ root: top, folder, expected, sha: head.commit })));
  return ok({ commit: head.commit, findings: lists.flat() });
}
