// The commits a push would add - `git rev-list <tip> --not <published base>`, the base being the destination's own remote ref or else origin/main - and what each one adds. Every scan of the push
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

/**
 * ok(refname | null): the PUBLISHED BASE, what the destination already holds - the remote ref of the branch being updated (`dest`, default the
 * branch) if there is one, otherwise `origin/main`, otherwise nothing (an empty remote: everything is new). Never "any remote ref": a blob that
 * only another branch holds is not in the destination.
 */
export async function publishedBase(repo, dest) {
  for (const ref of [`refs/remotes/origin/${dest}`, 'refs/remotes/origin/main']) {
    const found = await runGit(repo, ['rev-parse', '--verify', '--quiet', `${ref}^{commit}`], { accept: [0, 1] });
    if (!found.ok || found.value.code === 0) return found.ok ? ok(ref) : found;
  }
  return ok(null);
}

const notBase = base => (base === null ? [] : ['--not', base]);

/** ok([sha]) oldest first: every commit of the tip that the published base does not hold, or a `config` fail when the branch does not exist. */
export async function newCommits(repo, branch, rev, dest = branch) {
  const tip = await branchTip(repo, branch, rev);
  const base = tip.ok ? await publishedBase(repo, dest) : tip;
  if (!base.ok) return base;
  const list = await runGit(repo, ['rev-list', '--reverse', tip.value, ...notBase(base.value)]);
  return list.ok ? ok(list.value.stdout.split('\n').filter(Boolean)) : list;
}

/**
 * ok(Set(object id)): the objects NEW to the destination, `git rev-list --objects <tip> --not <published base>`. This is the one rule for what is
 * scanned and counted: it covers every parent of every merge and skips only what the destination already holds (a merge of origin/main brings
 * main's blobs in; they are not charged to the branch).
 */
export async function newObjects(repo, tip, dest) {
  const base = await publishedBase(repo, dest);
  if (!base.ok) return base;
  const listing = await runGit(repo, ['rev-list', '--objects', tip, ...notBase(base.value)], { maxBytes: 256 * 1024 * 1024, timeoutMs: 300_000 });
  return listing.ok ? ok(new Set(listing.value.stdout.split('\n').map(line => line.split(' ')[0]).filter(Boolean))) : listing;
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

/** ok(Map(commit -> { parents: [sha], text })) for `commits`, from ONE `git log --no-walk --stdin` (not one process per commit). */
async function logOf(repo, commits) {
  if (commits.length === 0) return ok(new Map());
  const log = await runGit(repo, ['log', '--no-walk=unsorted', '--stdin', '--format=%x01%H%x00%P%x00%B'], { input: `${commits.join('\n')}\n`, maxBytes: 256 * 1024 * 1024 });
  if (!log.ok) return log;
  const entries = log.value.stdout.split('\x01').filter(Boolean).map(chunk => { const [commit, parents, ...text] = chunk.split('\0'); return [commit, { parents: parents.split(' ').filter(Boolean), text: text.join('\0') }]; });
  return ok(new Map(entries));
}

async function filesOfCommit(repo, commit, parents) {
  const base = parents.length === 0 ? ['--root'] : [parents[0]];
  const diff = await runGit(repo, ['diff-tree', '-r', '-z', '--no-renames', '--no-commit-id', ...base, commit]);
  return diff.ok ? ok(parseRaw(diff.value.stdout, commit)) : diff;
}

/** ok({ messages: [{ commit, text }], files: [{ commit, path, oid, mode }] }): messages from one `git log`, files from each commit's diff against its first parent (a merge's other parents are covered by their own commits of the range). */
export async function changesOf(repo, commits) {
  const log = await logOf(repo, commits);
  if (!log.ok) return log;
  const messages = commits.map(commit => ({ commit, text: log.value.get(commit)?.text ?? '' }));
  const files = [];
  for (const commit of commits) {
    const one = await filesOfCommit(repo, commit, log.value.get(commit)?.parents ?? []);
    if (!one.ok) return one;
    files.push(...one.value);
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
 * ok(Set("path\0blob id")): the (path, blob) pairs the destination already holds, from ONE `git ls-tree -r <published base>`; empty when there is no
 * base. A published blob at a path the base does not hold under that blob is new PLACEMENT (a README copied into a task folder, a note moved into
 * an agent-visible one), whatever its bytes: this set is what tells the two apart.
 */
export async function basePairs(repo, dest) {
  const base = await publishedBase(repo, dest);
  if (!base.ok || base.value === null) return base.ok ? ok(new Set()) : base;
  const listing = await runGit(repo, ['ls-tree', '-r', '-z', base.value], { maxBytes: 64 * 1024 * 1024 });
  if (!listing.ok) return listing;
  return ok(new Set(listing.value.stdout.split('\0').filter(Boolean).map(record => {
    const tab = record.indexOf('\t');
    return `${record.slice(tab + 1)}\0${record.slice(0, tab).split(' ')[2]}`;
  })));
}
