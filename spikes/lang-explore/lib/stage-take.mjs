// How the series' branch takes a set of harness paths from a commit and survives a kill at any step (F3.S1 (b), PR 5, coai code round 2). `stage-sync --accept-harness` never edits the working tree or the shared index before the branch moves:
//   1. it builds the new tree in an index FILE of its own (`GIT_INDEX_FILE` in the git folder: `read-tree HEAD`, then ONE `update-index --index-info` for the paths, then `write-tree`) and the commit with `commit-tree`;
//   2. it writes a small journal `{ ref, old, new }` into the git folder (a temporary file and a rename, so it is whole or absent);
//   3. it moves the branch with `update-ref <ref> <new> <old>`, the compare-and-swap form: a branch that moved meanwhile is refused and nothing changed;
//   4. only then it brings the working tree and the shared index to the new commit, step by step, each step safe to run twice (`checkout <new> -- files`, unlink + index entry for a removed file, the gitlink entry and the submodule's own checkout);
//   5. it drops the journal.
// A kill before step 3 leaves the old branch whole (the tree is untouched). A kill after it leaves the journal and a branch that is ahead of its tree: `recoverPending` (run first by `stage-sync` in both forms, through `refusal`) reads the journal
// and, where the branch is at `new`, runs step 4 again, where it is still at `old` drops the journal, and where it is at neither refuses by name (it never guesses). `pendingProblem` is the same reading for the paid launch: a tree behind HEAD is
// refused there. Nothing here resets, cleans or forces anything, and no path is removed from a lookup that failed: step 4 reads both trees first and stops, naming the failure, when either cannot be read.
// Before step 4 touches a file the take looks at it (Opus review): a link on the way, a folder where a file is to be removed and a working file that is neither the old nor the new blob (a person edited it) each refuse, naming the path, and
// leave the journal for the next run. A removal that fails is a refusal value naming the path; nothing here throws for it. A recovery refuses while the branch's lock file exists (a git child outlives a killed node: `detached`) and reads the
// harness identity the commit's message names before it writes anything. Every function answers a value; nothing throws for an expected failure.
import { existsSync, readFileSync, realpathSync, renameSync, rmSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { git, harnessIdentity } from '../../minigit-bench/lib/prereg-guard.mjs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { linkOnWay, lstatOf } from './safe-place.mjs';
import { STAGE_HARNESS_DIRS, STAGE_HARNESS_LISTS, STAGE_HARNESS_TOP_DIRS } from './stage-lists.mjs';

const LINK = '160000';
const NIL = '0'.repeat(40);
const HEX40 = /^[0-9a-f]{40}$/;
const JOURNAL = 'stage-take.journal';
const INDEX = 'stage-take.index';

// ---- the harness entries of a commit and the change between two --------------------------------------------------------------------------------------------------------------------------------

const ENTRY = /^(\d+) (\w+) ([0-9a-f]+)\t(.*)$/s;

/** The entries of a `git ls-tree -z` text: `[{ mode, type, sha, file }]`. */
const parsedEntries = text => text.split('\0').map(line => ENTRY.exec(line)).filter(m => m !== null).map(([, mode, type, sha, file]) => ({ mode, type, sha, file }));

/** The harness entries at `rev`, as `ok(Map file -> entry)`: every blob of the harness folders (a submodule is its one gitlink entry) and the top-level `*.mjs` of the three spikes. A lookup that fails is the `fail`, never an empty map. */
export async function entriesAt(repo, rev) {
  const [deep, tops] = await Promise.all([git(repo, ['ls-tree', '-r', '-z', rev, '--', ...STAGE_HARNESS_DIRS]), git(repo, ['ls-tree', '-z', rev, '--', ...STAGE_HARNESS_TOP_DIRS])]);
  const unread = [deep, tops].find(r => !r.ok);
  if (unread) return unread;
  const topFiles = parsedEntries(tops.value).filter(e => e.type === 'blob' && e.file.endsWith('.mjs'));
  return ok(new Map([...parsedEntries(deep.value), ...topFiles].map(e => [e.file, e])));
}

const sameEntry = (a, b) => a !== undefined && a.mode === b.mode && a.sha === b.sha;
export const isLink = entry => entry?.mode === LINK;
export const linksOf = put => put.filter(e => isLink(e));

/** The change from `head` to `want`: the files `head` holds and `want` does not (`removed`) and the entries of `want` that `head` lacks or holds otherwise (`put`). */
export const changesOf = ({ head, want }) => ({
  removed: [...head.keys()].filter(file => !want.has(file)),
  put: [...want.values()].filter(entry => !sameEntry(head.get(entry.file), entry)),
});

/** The paths of `changes`, sorted. */
export const changedFiles = ({ removed, put }) => [...removed, ...put.map(e => e.file)].sort();

// ---- the new tree, built in an index file of its own ---------------------------------------------------------------------------------------------------------------------------------------------

const infoLine = e => `${e.mode} ${e.sha}\t${e.file}\0`;
const goneLine = file => `0 ${NIL}\t${file}\0`;
/** The text `update-index -z --index-info` reads: an entry per put path, mode 0 and a zero id per removed path. */
const infoOf = ({ removed, put }) => [...removed.map(goneLine), ...put.map(infoLine)].join('');

/** The tree `old`'s tree comes to with `changes`, built in the index file `index` (the shared index and the working tree are never touched): `ok(tree sha)` | `fail`. */
export async function builtTree({ repo, old, index, changes }) {
  const env = { GIT_INDEX_FILE: index };
  const read = await git(repo, ['read-tree', old], { env });
  if (!read.ok) return read;
  const put = await git(repo, ['update-index', '-z', '--index-info'], { env, input: infoOf(changes) });
  if (!put.ok) return put;
  const tree = await git(repo, ['write-tree'], { env });
  return tree.ok ? ok(tree.value.trim()) : tree;
}

/** The commit of `tree` on `parent` (nothing moves): `ok(sha)` | `fail`. */
export async function commitTree({ repo, tree, parent, subject, body }) {
  const made = await git(repo, ['commit-tree', tree, '-p', parent, '-m', subject, '-m', body]);
  return made.ok ? ok(made.value.trim()) : made;
}

// ---- the journal and the index file of the take, in the git folder ---------------------------------------------------------------------------------------------------------------------------

async function gitPath(repo, name) {
  const r = await git(repo, ['rev-parse', '--git-path', name]);
  return r.ok ? ok(path.resolve(repo, r.value.trim())) : r;
}

/** Where the take keeps its journal and its index file (the git folder of this worktree): `ok({ journal, index })` | `fail`. */
export async function takeFiles(repo) {
  const [journal, index] = await Promise.all([gitPath(repo, JOURNAL), gitPath(repo, INDEX)]);
  const unread = [journal, index].find(r => !r.ok);
  return unread ?? ok({ journal: journal.value, index: index.value });
}

const isJournal = j => typeof j?.ref === 'string' && HEX40.test(j.old ?? '') && HEX40.test(j.new ?? '');

/** The journal in `file`: `ok(null)` when there is none, `ok({ ref, old, new })`, or the `fail` for a file that is not a journal (it is never guessed at). */
function readJournal(file) {
  if (!existsSync(file)) return ok(null);
  try {
    const journal = JSON.parse(readFileSync(file, 'utf8'));
    return isJournal(journal) ? ok(journal) : fail(`${file} is not the journal of a harness take ({ ref, old, new } with two 40-hex commits): look at it, then delete it`);
  } catch (e) {
    return fail(`${file} cannot be read: ${e.message}: look at it, then delete it`);
  }
}

/** The journal written whole or not at all (a temporary file, then a rename): `ok(true)` | `fail`. */
function writeJournal(file, entry) {
  try {
    writeFileSync(`${file}.tmp`, `${JSON.stringify(entry)}\n`);
    renameSync(`${file}.tmp`, file);
    return ok(true);
  } catch (e) {
    return fail(`cannot write ${file}: ${e.message}`);
  }
}

/** One file removed, as a value: `ok(true)` (a file that is not there is gone) | `fail` naming the path and the error. */
export function gone(file) {
  try {
    rmSync(file, { force: true });
    return ok(true);
  } catch (e) {
    return fail(`cannot remove ${file}: ${e.message}`);
  }
}

/** The journal and the index file of the take, gone: `ok(true)` | the first `fail`. */
const dropped = ({ journal, index }) => [journal, `${journal}.tmp`, index].map(gone).find(r => !r.ok) ?? ok(true);

/** The index file of a take that is about to be built, made ready: a stale one is removed; a stale `.lock` of it (a kill during `read-tree` leaves one) is removed only when no journal exists, since then no take runs under the series locks; with a journal it is refused, naming it. `ok(true)` | `fail`. */
export function readiedIndex(files) {
  const lock = `${files.index}.lock`;
  if (existsSync(lock) && existsSync(files.journal)) return fail(`${lock} exists while ${files.journal} records a take: look at git status and git log, then delete what is stale`);
  const cleared = gone(lock);
  return cleared.ok ? gone(files.index) : cleared;
}

// ---- the working tree and the shared index, brought to the new commit -----------------------------------------------------------------------------------------------------------------------

/** `[make(list)]` when `list` holds anything, else no step. */
const when = (list, make) => (list.length === 0 ? [] : [make(list)]);

/** The sentence for a submodule folder that is not its own checkout (git there would find the parent repository and `checkout --detach` would move the PARENT's HEAD), or null: `top` is `git rev-parse --show-toplevel` run in `dir`. */
export const notCheckedOut = ({ dir, top, name }) => (!top.ok || realpathSync(top.value.trim()) !== realpathSync(dir) ? `the submodule ${name} is not checked out: run \`git submodule update --init\`` : null);

/** The sentence for the folder of the submodule `file` of `repo` when it is not a checkout of its own, or null. */
export async function ownCheckoutProblem({ repo, file }) {
  const dir = path.join(repo, file);
  return notCheckedOut({ dir, top: await git(dir, ['rev-parse', '--show-toplevel']), name: file });
}

/** Each submodule of `links` checked out at its entry's commit, only where its folder is a checkout of its own: `ok(true)` | `fail` naming the submodule. */
async function checkedOut({ repo, links }) {
  if (links.length === 0) return ok(true);
  const [link, ...rest] = links;
  const own = await ownCheckoutProblem({ repo, file: link.file });
  if (own !== null) return fail(own);
  const r = await git(path.join(repo, link.file), ['checkout', '-q', '--detach', link.sha]);
  return r.ok ? checkedOut({ repo, links: rest }) : fail(`the submodule ${link.file} cannot be moved to ${link.sha.slice(0, 12)}: ${r.reason}`);
}

const indexed = (repo, changes) => git(repo, ['update-index', '-z', '--index-info'], { input: infoOf(changes) });

/** A removed file: unlinked (a failing unlink is a `fail` naming the path), then its entry out of the shared index (both are safe to repeat). */
async function unlinked(repo, files) {
  const failed = files.map(file => gone(path.join(repo, file))).find(r => !r.ok);
  return failed ?? indexed(repo, { removed: files, put: [] });
}

/** The steps, in order, that bring the tree and the shared index from the old commit to `to`: the blobs by `checkout`, the removals, the gitlinks' entries and then each submodule's own checkout. Each is a function that answers `ok` | `fail` and may run twice. */
const stepsOf = ({ repo, to, changes }) => [
  ...when(changes.put.filter(e => !isLink(e)).map(e => e.file), files => () => git(repo, ['checkout', to, '--', ...files])),
  ...when(changes.removed, files => () => unlinked(repo, files)),
  ...when(linksOf(changes.put), links => () => indexed(repo, { removed: [], put: links })),
  ...when(linksOf(changes.put), links => () => checkedOut({ repo, links })),
];

/** The steps one after the other, the first failure stops; `seam('worktree:<n>')` is awaited before step n (a test's kill point). */
async function inOrder(steps, seam, at = 0) {
  const [step, ...rest] = steps;
  if (step === undefined) return ok(true);
  await seam(`worktree:${at}`);
  const r = await step();
  return r.ok ? inOrder(rest, seam, at + 1) : r;
}

// ---- a path is looked at before it is written or removed ---------------------------------------------------------------------------------------------------------------------------------------

const wayOf = ({ repo, file }) => linkOnWay({ root: repo, folders: [path.posix.dirname(file)] });

/** The sentence for the first path of `files` that has a link on its way or, among the removed ones, is a folder, or null. Nothing is followed or removed. */
function pathProblem({ repo, files, removed }) {
  const linked = files.map(file => ({ file, link: wayOf({ repo, file }) })).find(x => x.link !== null);
  if (linked) return `${linked.link} is a link on the way to ${linked.file}, a path of the take: nothing is written or removed through a link`;
  const folder = removed.find(file => lstatOf(path.join(repo, file))?.isDirectory());
  return folder === undefined ? null : `${folder} is a folder, not a file: it is not removed`;
}

/** The blob ids of the working files `files` (the regular files among them), as a map file -> id: `ok(Map)` | `fail`. A file that is not there, or is not a regular file, has no entry. */
async function workingBlobs({ repo, files }) {
  const present = files.filter(file => lstatOf(path.join(repo, file))?.isFile());
  const ids = present.length === 0 ? ok('') : await git(repo, ['hash-object', '--', ...present]);
  return ids.ok ? ok(new Map(present.map((file, i) => [file, ids.value.split('\n')[i]]))) : ids;
}

/** The sentence for the first changed path whose working file is neither the old nor the new blob (a person edited it), or null; a failing read is a refusal too. */
async function editedProblem({ repo, entries, files }) {
  const blobs = await workingBlobs({ repo, files });
  if (!blobs.ok) return `${blobs.reason}: the working files of the interrupted take cannot be read, so nothing was written`;
  const edited = files.find(file => blobs.value.has(file) && ![entries.from.get(file)?.sha, entries.to.get(file)?.sha].includes(blobs.value.get(file)));
  return edited === undefined ? null : `the working file ${edited} is neither the old nor the new blob of the interrupted take: a person edited it: nothing was written, look at it, then run stage-sync again`;
}

/** The sentence for a change the take must not write yet: a link, a folder or an edited file, or null. */
async function unsafeProblem({ repo, entries, changes }) {
  const files = [...changes.removed, ...changes.put.filter(e => !isLink(e)).map(e => e.file)];
  return pathProblem({ repo, files: [...files, ...linksOf(changes.put).map(e => e.file)], removed: changes.removed }) ?? await editedProblem({ repo, entries, files });
}

/** The change between the two commits of `entry`, read from both trees: `ok({ changes, from, to })`, or the `fail` that says no file was removed or written because a tree could not be read. */
async function changesBetween({ repo, entry }) {
  const [from, to] = await Promise.all([entriesAt(repo, entry.old), entriesAt(repo, entry.new)]);
  const unread = [from, to].find(r => !r.ok);
  return unread ? fail(`${unread.reason}: the harness entries of the interrupted take cannot be read, so no file was removed or written`) : ok({ changes: changesOf({ head: from.value, want: to.value }), entries: { from: from.value, to: to.value } });
}

/** The working tree and the shared index brought to `entry.new`, then the journal and the index file dropped: `ok(true)` | `fail` (the journal stays, so the next stage-sync runs it again). */
async function finishedTake({ repo, files, entry, seam }) {
  const read = await changesBetween({ repo, entry });
  if (!read.ok) return read;
  const problem = await unsafeProblem({ repo, ...read.value });
  if (problem !== null) return fail(problem);
  const written = await inOrder(stepsOf({ repo, to: entry.new, changes: read.value.changes }), seam);
  if (!written.ok) return written;
  await seam('worktreeDone');
  return dropped(files);
}

// ---- landing the new commit ---------------------------------------------------------------------------------------------------------------------------------------------------------------------

const noSeam = async () => {};
const short = sha => sha.slice(0, 12);

/** `fail` that says the branch is ahead of its tree and that the next stage-sync finishes it. */
const unfinished = ({ entry, failure }) => fail(`the branch ${entry.ref} holds ${short(entry.new)} but its working tree is not finished (${failure.reason}): run stage-sync again, which finishes it before anything else`);

/** The journal of a take from `old` to `next` on the branch HEAD is on, written: `ok(entry)` | `fail`. */
async function journaled({ repo, files, old, next }) {
  const ref = await git(repo, ['symbolic-ref', '-q', 'HEAD']);
  if (!ref.ok) return ref;
  const entry = { ref: ref.value.trim(), old, new: next };
  const written = writeJournal(files.journal, entry);
  return written.ok ? ok(entry) : written;
}

/** The sentence for a branch that is no longer at `old` when it was to move: the journal is dropped, nothing else changed. */
function refused({ files, entry, moved }) {
  const cleared = dropped(files);
  return fail(`the branch ${entry.ref} is no longer at ${short(entry.old)}: nothing was taken (${moved.reason})${cleared.ok ? '' : `; ${cleared.reason}`}`);
}

/** The journal, the compare-and-swap of the branch from `old` to `next`, then the working tree: `ok(true)` | `fail` (a branch that moved is refused and the journal dropped; a tree that is not finished is left to the next stage-sync). */
export async function landedTake({ repo, files, old, next, seam = noSeam }) {
  const entry = await journaled({ repo, files, old, next });
  if (!entry.ok) return entry;
  await seam('journalWritten');
  const moved = await git(repo, ['update-ref', '-m', 'stage-sync --accept-harness', entry.value.ref, next, old]);
  if (!moved.ok) return refused({ files, entry: entry.value, moved });
  await seam('refMoved');
  const finished = await finishedTake({ repo, files, entry: entry.value, seam });
  return finished.ok ? finished : unfinished({ entry: entry.value, failure: finished });
}

// ---- what a kill leaves, found and finished by the next stage-sync ---------------------------------------------------------------------------------------------------------------------

/** Where this worktree's HEAD is, against the journal's branch: `ok(sha of the branch)` | `fail`. */
async function tipOf(repo, entry) {
  const [head, tip] = await Promise.all([git(repo, ['symbolic-ref', '-q', 'HEAD']), git(repo, ['rev-parse', '--verify', '-q', entry.ref])]);
  if (!head.ok || head.value.trim() !== entry.ref) return fail(`an interrupted harness take of ${entry.ref} is recorded, and this worktree is not on that branch: look at git status, then delete the journal`);
  return tip.ok ? ok(tip.value.trim()) : tip;
}

/** The journal of this worktree: `ok(null)` | `ok({ files, entry })` | `fail`. */
async function journalOf(repo) {
  const files = await takeFiles(repo);
  if (!files.ok) return files;
  const entry = readJournal(files.value.journal);
  return entry.ok && entry.value !== null ? ok({ files: files.value, entry: entry.value }) : entry;
}

/** The interrupted take of this worktree, if any: `ok(null)` | `ok({ files, entry, tip })` | `fail`. Read only. */
async function pendingOf(repo) {
  const found = await journalOf(repo);
  if (!found.ok || found.value === null) return found;
  const tip = await tipOf(repo, found.value.entry);
  return tip.ok ? ok({ ...found.value, tip: tip.value }) : tip;
}

/** The lock file of the branch or of the packed refs that exists, or null: a git child outlives a killed node (it is spawned detached), so an `update-ref` of the killed take may still be running. */
async function refLockOf({ repo, entry }) {
  const paths = await Promise.all([`${entry.ref}.lock`, 'packed-refs.lock'].map(name => git(repo, ['rev-parse', '--git-path', name])));
  return paths.filter(r => r.ok).map(r => path.resolve(repo, r.value.trim())).find(file => existsSync(file)) ?? null;
}

/** The 12 hex the message of `entry.new` names as its harness identity (`harness:<12 hex>`), or undefined. */
async function messageHex({ repo, entry }) {
  const message = await git(repo, ['log', '-1', '--format=%B', entry.new]);
  return /^harness:([0-9a-f]{12})$/m.exec(message.ok ? message.value : '')?.[1];
}

/** The sentence for a commit whose harness identity is not the one its own message names, or null: read before the working tree is written. */
async function messageProblem({ repo, entry }) {
  const hex = await messageHex({ repo, entry });
  if (hex === undefined) return `the commit ${short(entry.new)} of the interrupted take names no harness identity in its message: nothing was written`;
  const identity = await harnessIdentity(repo, STAGE_HARNESS_LISTS, entry.new);
  if (!identity.ok) return `${identity.reason}: the harness identity of the interrupted take cannot be read, so nothing was written`;
  return identity.value.startsWith(hex) ? null : `the harness folders of the interrupted take's commit ${short(entry.new)} have the identity ${short(identity.value)}, not the ${hex} its message names: nothing was written`;
}

/** The branch is at `new`: the commit's identity is checked, then the working tree is finished. */
async function finishedPending({ repo, files, entry }) {
  const problem = await messageProblem({ repo, entry });
  return problem === null ? finishedTake({ repo, files, entry, seam: noSeam }) : fail(problem);
}

/** The branch is at `old`: the take never landed, so the journal is dropped, unless a lock file says a git process may still move the branch. */
async function neverLanded({ repo, files, entry }) {
  const lock = await refLockOf({ repo, entry });
  if (lock !== null) return fail(`${lock} exists: a git process may still be moving the branch: nothing was dropped (wait for it, or delete the lock when no git runs, then run stage-sync again)`);
  const cleared = dropped(files);
  return cleared.ok ? ok(null) : cleared;
}

/** Finishes or drops one interrupted take, by where the branch is: at `new` the working tree is finished, at `old` the take never reached the branch, anywhere else it is refused. */
async function resolved({ repo, files, entry, tip }) {
  if (tip === entry.new) return finishedPending({ repo, files, entry });
  if (tip !== entry.old) return fail(`the branch ${entry.ref} is at ${short(tip)}, which is neither ${short(entry.old)} nor ${short(entry.new)} of the interrupted harness take in ${files.journal}: look at git log and git status, then delete the journal`);
  return neverLanded({ repo, files, entry });
}

/** Finishes what a killed take left (run first by every stage-sync): `ok(...)` when there is nothing pending or it is done, `fail(sentence)` when it cannot be finished (a journal that is not one, a branch that is elsewhere, a tree that cannot be read). */
export async function recoverPending(repo) {
  const found = await pendingOf(repo);
  return !found.ok || found.value === null ? found : resolved({ repo, ...found.value });
}

/** The paid launch's sentence for a worktree with an interrupted take (its tree may be behind HEAD, and the code a launch runs is the tree's), or null. Read only. */
export async function pendingProblem(repo) {
  const found = await pendingOf(repo);
  if (!found.ok) return found.reason;
  return found.value === null ? null : `an interrupted harness take is recorded in ${found.value.files.journal}: the working tree may be behind HEAD, and a paid launch runs the working tree's code: run stage-sync, which finishes it, before any paid launch`;
}
