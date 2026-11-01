// The commits a push would add - `git rev-list <branch> --not --remotes=origin` - and what each one adds. Every scan of the push
// check runs over exactly this: the messages and, per added or changed path, the blob as that commit left it. A file added by one
// commit and removed by the next is in the history a push publishes, so it is scanned; a blob that several paths or commits share
// is read once (the first commit that introduced the path owns the finding).
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { BATCH_MAX_OBJECT_BYTES, BATCH_MAX_TOTAL_BYTES, BLOB_MAX_BYTES, describeObject, runBatchCatFile, runBatchCheck, runGit } from './push-git.mjs';

export const SUBMODULE_MODE = '160000';
export const SYMLINK_MODE = '120000';

/** What names the thing checked: the given sha (`rev`, as git is about to publish it) or else the branch's ref. */
export const revOf = (branch, rev) => rev ?? `refs/heads/${branch}`;

/** ok(sha) of the tip commit of `branch` (or of `rev`), or a `config` fail when it does not exist. */
export async function branchTip(repo, branch, rev) {
  const tip = await runGit(repo, ['rev-parse', '--verify', '--quiet', `${revOf(branch, rev)}^{commit}`], { accept: [0, 1] });
  if (!tip.ok) return tip;
  return tip.value.code === 1 ? fail(`${rev === undefined ? `the branch ${branch}` : `the commit ${rev}`} does not exist in ${repo}`, { kind: 'config' }) : ok(tip.value.stdout.trim());
}

/** ok(null) when the branch does not exist or points at `tip`; otherwise a `config` fail: the branch moved after the sha was taken. */
export async function verifyNotMoved(repo, branch, tip) {
  const now = await runGit(repo, ['rev-parse', '--verify', '--quiet', `refs/heads/${branch}^{commit}`], { accept: [0, 1] });
  if (!now.ok) return now;
  const sha = now.value.stdout.trim();
  return now.value.code === 1 || sha === tip ? ok(null) : fail(`the branch ${branch} moved: it is at ${sha} now, the check was asked for ${tip}`, { kind: 'config' });
}

/** ok([sha]) oldest first, or a `config` fail when the branch does not exist. */
export async function newCommits(repo, branch, rev) {
  const tip = await branchTip(repo, branch, rev);
  if (!tip.ok) return tip;
  const list = await runGit(repo, ['rev-list', '--reverse', tip.value, '--not', '--remotes=origin']);
  return list.ok ? ok(list.value.stdout.split('\n').filter(Boolean)) : list;
}

/** Records of `git diff-tree --raw -z`: added or changed entries only (no deletion). A submodule pointer has mode 160000 and no blob to read. */
function parseRaw(raw, commit) {
  const parts = raw.split('\0').filter(Boolean);
  const files = [];
  for (let i = 0; i + 1 < parts.length; i += 2) {
    const [, newMode, , oid, status] = parts[i].slice(1).split(' ');
    if (status !== 'D') files.push({ commit, path: parts[i + 1], oid, mode: newMode });
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

/** ok({ messages: [{ commit, text }], files: [{ commit, path, oid, mode }] }). A merge is compared with its first parent only. */
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

const LARGEST_NAMED = 3;

/** The fail for an object over `maxObjectBytes`, or for sizes together over `maxTotalBytes` (naming the largest); null when all is within. Sizes only: no content has been read. */
function sizeRefusal(sizes, { maxObjectBytes, maxTotalBytes, where }) {
  const bySize = [...sizes].sort(([, a], [, b]) => b.size - a.size);
  const [name, biggest] = bySize[0] ?? [];
  const total = bySize.reduce((sum, [, { size }]) => sum + size, 0);
  if (biggest?.size > maxObjectBytes) return fail(`${describeObject(name, where)} is ${biggest.size} bytes, over the ${maxObjectBytes}-byte ceiling for one object: refused before any content was read, so not clean`, { kind: 'env' });
  const largest = bySize.slice(0, LARGEST_NAMED).map(([n, { size }]) => `${describeObject(n, where)} ${size} bytes`).join('; ');
  return total > maxTotalBytes ? fail(`the blobs of one check hold more than ${maxTotalBytes} bytes (${total}): refused before any content was read; largest: ${largest}`, { kind: 'env' }) : null;
}

/**
 * ok(Map(oid -> Buffer)) for the distinct blob ids. The sizes come FIRST, from one `git cat-file --batch-check` (no content): one blob over
 * `maxObjectBytes` (16 MiB), or the blobs together over `maxTotalBytes` (256 MiB), is refused naming the offender (sha, size, path and commit
 * where `limits.where` knows them) before anything is read. Only then does one `git cat-file --batch` read the content (`limits.readBatch`
 * replaces that reader, for a test that must see it was not called). A blob git does not have is a fail.
 */
export async function readBlobs(repo, oids, limits = {}) {
  const { readBatch = runBatchCatFile, maxObjectBytes = BATCH_MAX_OBJECT_BYTES, maxTotalBytes = BATCH_MAX_TOTAL_BYTES, where } = limits;
  const unique = [...new Set(oids)];
  const sizes = await runBatchCheck(repo, unique);
  if (!sizes.ok) return sizes;
  if (sizes.missing.length > 0) return fail(`git has no blob ${sizes.missing.slice(0, 3).join(', ')}: the history is not read in full, so it is not clean`, { kind: 'env' });
  const refusal = sizeRefusal(sizes.value, { maxObjectBytes, maxTotalBytes, where });
  if (refusal) return refusal;
  const read = await readBatch(repo, unique, { ...limits, maxObjectBytes, maxTotalBytes });
  return read.ok ? ok(read.value) : read;
}

/** ok(text) of `path` at the tip of `branch`, ok(null) when the path is not there. */
export async function readFileAt(repo, branch, path, rev) {
  const at = `${revOf(branch, rev)}:${path}`;
  const exists = await runGit(repo, ['cat-file', '-e', at], { accept: [0, 128] });
  if (!exists.ok) return exists;
  if (exists.value.code !== 0) return ok(null);
  const blob = await runGit(repo, ['cat-file', 'blob', at], { maxBytes: BLOB_MAX_BYTES });
  return blob.ok ? ok(blob.value.stdout) : blob;
}

/** ok(Map(path -> { oid, mode })) of every entry in the tree at the tip of `branch` (submodule pointers and symlinks included): what a run base would carry. */
export async function tipTree(repo, branch, rev) {
  const listing = await runGit(repo, ['ls-tree', '-r', '-z', revOf(branch, rev)], { maxBytes: 64 * 1024 * 1024 });
  if (!listing.ok) return listing;
  const tree = new Map();
  for (const record of listing.value.stdout.split('\0').filter(Boolean)) {
    const tab = record.indexOf('\t');
    const [mode, , oid] = record.slice(0, tab).split(' ');
    tree.set(record.slice(tab + 1), { oid, mode });
  }
  return ok(tree);
}

/**
 * ok(Set(object id)) of every object reachable from the remotes (`git rev-list --objects --remotes=origin`): what is already published. A blob in
 * this set is public and was checked when it was pushed, so a branch that merely brings it in (a merge of origin/main, a rebase onto it) is not
 * charged for it: it is neither scanned nor counted toward a size ceiling.
 */
export async function publishedObjects(repo) {
  const listing = await runGit(repo, ['rev-list', '--objects', '--remotes=origin'], { maxBytes: 256 * 1024 * 1024, timeoutMs: 300_000 });
  if (!listing.ok) return listing;
  return ok(new Set(listing.value.stdout.split('\n').map(line => line.split(' ')[0]).filter(Boolean)));
}
