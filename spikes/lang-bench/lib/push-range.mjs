// The commits a push would add - `git rev-list <branch> --not --remotes=origin` - and what each one adds. Every scan of the push
// check runs over exactly this: the messages and, per added or changed path, the blob as that commit left it. A file added by one
// commit and removed by the next is in the history a push publishes, so it is scanned; a blob that several paths or commits share
// is read once (the first commit that introduced the path owns the finding).
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { BLOB_MAX_BYTES, runGit } from './push-git.mjs';

const SUBMODULE_MODE = '160000';

/** ok([sha]) oldest first, or a `config` fail when the branch does not exist. */
export async function newCommits(repo, branch) {
  const tip = await runGit(repo, ['rev-parse', '--verify', '--quiet', `refs/heads/${branch}^{commit}`], { accept: [0, 1] });
  if (!tip.ok) return tip;
  if (tip.value.code === 1) return fail(`the branch ${branch} does not exist in ${repo}`, { kind: 'config' });
  const list = await runGit(repo, ['rev-list', '--reverse', tip.value.stdout.trim(), '--not', '--remotes=origin']);
  return list.ok ? ok(list.value.stdout.split('\n').filter(Boolean)) : list;
}

/** Records of `git diff-tree --raw -z`: added or changed files only (no deletion, no submodule pointer). */
function parseRaw(raw, commit) {
  const parts = raw.split('\0').filter(Boolean);
  const files = [];
  for (let i = 0; i + 1 < parts.length; i += 2) {
    const [, newMode, , oid, status] = parts[i].slice(1).split(' ');
    if (status !== 'D' && newMode !== SUBMODULE_MODE) files.push({ commit, path: parts[i + 1], oid });
  }
  return files;
}

async function changesOfCommit(repo, commit) {
  const head = await runGit(repo, ['log', '-1', '--format=%P%x00%B', commit]);
  if (!head.ok) return head;
  const [parents, message] = head.value.stdout.split('\0');
  const firstParent = parents.trim().split(' ')[0];
  const base = firstParent === '' ? ['--root'] : [firstParent];
  const diff = await runGit(repo, ['diff-tree', '-r', '-z', '--no-renames', '--no-commit-id', ...base, commit]);
  return diff.ok ? ok({ message: { commit, text: message }, files: parseRaw(diff.value.stdout, commit) }) : diff;
}

/** ok({ messages: [{ commit, text }], files: [{ commit, path, oid }] }). A merge is compared with its first parent only. */
export async function changesOf(repo, commits) {
  const messages = [];
  const files = [];
  for (const commit of commits) {
    const one = await changesOfCommit(repo, commit);
    if (!one.ok) return one;
    messages.push(one.value.message);
    files.push(...one.value.files);
  }
  return ok({ messages, files });
}

/** ok(Map(oid -> text)) for the distinct blob ids, read eight at a time; a blob over the ceiling is a fail (not scanned, so not clean). */
export async function readBlobs(repo, oids) {
  const texts = new Map();
  const unique = [...new Set(oids)];
  for (let i = 0; i < unique.length; i += 8) {
    const batch = await Promise.all(unique.slice(i, i + 8).map(oid => runGit(repo, ['cat-file', 'blob', oid], { maxBytes: BLOB_MAX_BYTES })));
    const bad = batch.find(r => !r.ok);
    if (bad) return bad;
    batch.forEach((r, n) => texts.set(unique[i + n], r.value.stdout));
  }
  return ok(texts);
}

/** ok(text) of `path` at the tip of `branch`, ok(null) when the path is not there. */
export async function readFileAt(repo, branch, path) {
  const exists = await runGit(repo, ['cat-file', '-e', `refs/heads/${branch}:${path}`], { accept: [0, 128] });
  if (!exists.ok) return exists;
  if (exists.value.code !== 0) return ok(null);
  const blob = await runGit(repo, ['cat-file', 'blob', `refs/heads/${branch}:${path}`], { maxBytes: BLOB_MAX_BYTES });
  return blob.ok ? ok(blob.value.stdout) : blob;
}
