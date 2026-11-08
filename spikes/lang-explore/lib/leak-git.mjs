// The git reads of the two-repository leak check (build step 2b). Read-only and bounded, never through a shell, and with nothing printed that came from a file: a failure names the step and git's exit code, never git's own words.
// git is told not to follow `GIT_DIR`-style variables to another repository, not to use replace refs (they can show other history than the repository holds) and never to fetch a missing object (a missing object is a finding of
// its own: the history is not all present). Every question that can fail comes back as `ok(value)` | `fail(sentence)`.
import { realpathSync } from 'node:fs';
import path from 'node:path';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { runBounded } from '../../minigit-bench/lib/run.mjs';

const MAX_BYTES = 512 * 1024 * 1024;
const ROUTING = /^GIT_(?:DIR|WORK_TREE|INDEX_FILE|COMMON_DIR|OBJECT_DIRECTORY|ALTERNATE_OBJECT_DIRECTORIES|NAMESPACE|CEILING_DIRECTORIES|GRAFT_FILE|REPLACE_REF_BASE|SHALLOW_FILE)$/;

/** The caller's environment without the variables that point git at another repository, with replace refs and lazy fetches off. */
const gitEnv = () => ({ ...Object.fromEntries(Object.entries(process.env).filter(([name]) => !ROUTING.test(name))), GIT_NO_REPLACE_OBJECTS: '1', GIT_NO_LAZY_FETCH: '1', GIT_TERMINAL_PROMPT: '0' });

/** One git command in `repo` (`extra` is `runBounded`'s options: `input`, `bytes`). */
export const git = (repo, args, extra = {}) => runBounded('git', args, { cwd: repo, env: gitEnv(), timeoutMs: 120_000, maxBytes: MAX_BYTES, ...extra });

/** True when the command ran, exited 0 and its output was not cut. */
export const finished = r => r.ok && r.value.code === 0 && !r.value.timedOut && !r.value.truncated;

/** The failure of a step: what it was and git's exit (never git's text). */
export const failed = (what, r) => fail(`${what} could not be read (git ${r.ok ? `exit ${r.value.code}${r.value.truncated ? ', output too large' : ''}` : r.reason})`);

/** The text a command printed, or the failure of its step. */
export async function gitText(repo, args, what) {
  const r = await git(repo, args);
  return finished(r) ? ok(r.value.stdout) : failed(what, r);
}

const realOrNull = file => { try { return realpathSync(file); } catch { return null; } };

/** `ok(real path)` of a repository's top folder; a path that is missing, not a work tree or only a folder inside one is a `fail` (a scan from a sub folder would list that folder alone). */
export async function repoRootOf(dir) {
  const real = realOrNull(path.resolve(dir));
  if (real === null) return fail(`${dir} is not a folder that exists`);
  const top = await gitText(real, ['rev-parse', '--show-toplevel'], 'the top folder');
  if (!top.ok) return fail(`${dir} is not a git work tree`);
  const root = realOrNull(top.value.trim());
  return root === real ? ok(real) : fail(`${dir} is not the top folder of its repository (that is ${root}): name the top folder`);
}

/** The real path of a repository's common git folder (a worktree of a repository shares it), or null. */
async function commonDirOf(repo) {
  const r = await gitText(repo, ['rev-parse', '--git-common-dir'], 'the git folder');
  return r.ok ? realOrNull(path.resolve(repo, r.value.trim())) : null;
}

/** True when two folders are the same repository, or two worktrees of it (they hold one history). */
export async function sameRepository(a, b) {
  const [first, second] = await Promise.all([commonDirOf(a), commonDirOf(b)]);
  return first !== null && first === second;
}

/** The sentence refusing a shallow repository (or one that cannot say), or null: its history is not all there. */
export async function shallowProblem(repo) {
  const r = await gitText(repo, ['rev-parse', '--is-shallow-repository'], 'the shallow check');
  if (!r.ok) return r.reason;
  return r.value.trim() === 'false' ? null : 'the repository is shallow (or git cannot say it is not): its history is not all present, so a scan of it would be clean over history it could not read';
}

/** `ok(sha)` of the commit a ref names, else a `fail` (a ref that names a tree, a blob or nothing is no commit). */
export async function resolveCommit(repo, ref) {
  const r = await git(repo, ['rev-parse', '--verify', '--quiet', `${ref}^{commit}`]);
  return finished(r) ? ok(r.value.stdout.trim()) : fail(`${ref} does not resolve to a commit`);
}

/** True when HEAD is a commit (an unborn branch has none). */
export const hasHead = async repo => finished(await git(repo, ['rev-parse', '--verify', '--quiet', 'HEAD^{commit}']));

/** The revision arguments of the history to read: every local branch and HEAD, minus what `baseSha` (or nothing: null) already holds. */
export async function walkOf(repo, baseSha) {
  const head = await hasHead(repo);
  return ['--branches', ...(head ? ['HEAD'] : []), ...(baseSha === null ? [] : [`^${baseSha}`])];
}

/** The sentence refusing a history with an object missing (a partial or damaged repository), or null. `git rev-list --missing=print` marks each missing object with a `?`. */
export async function missingProblem(repo, walk) {
  const r = await gitText(repo, ['rev-list', '--objects', '--missing=print', ...walk], 'the walk over the history');
  if (!r.ok) return `${r.reason}: the history is not all present`;
  const gone = r.value.split('\n').filter(line => line.startsWith('?'));
  return gone.length === 0 ? null : `${gone.length} object(s) of the history are missing from the repository: the history is not all present, so it is not scanned and not called clean`;
}

/** The commit messages and every patch of the history `walk` names, as texts to scan. `--cc` adds the lines a merge changed that no parent had. */
export async function historyTexts(repo, walk) {
  const messages = await gitText(repo, ['log', '--format=%B', ...walk], 'the commit messages of the history');
  if (!messages.ok) return messages;
  const patches = await gitText(repo, ['log', '-p', '--cc', '--no-color', '--no-ext-diff', '--no-textconv', '--format=%H', ...walk], 'the patches of the history');
  return patches.ok ? ok([{ name: 'commit messages of the local branches', raw: messages.value, diff: false }, { name: 'patches of the local branches', raw: patches.value, diff: true }]) : patches;
}

/** The staged changes as one diff text (an unborn branch compares with the empty tree). */
export async function stagedText(repo) {
  const r = await gitText(repo, ['diff', '--cached', '--no-color', '--no-ext-diff', '--no-textconv'], 'the staged changes');
  return r.ok ? ok({ name: 'staged changes', raw: r.value, diff: true }) : r;
}

const parseEntry = entry => {
  const tab = entry.indexOf('\t');
  const [, type, oid] = entry.slice(0, tab).split(' ');
  return { type, oid, file: entry.slice(tab + 1) };
};

/** `git ls-tree -r -z` output as `{ blobs: [{ oid, file }], modules: [file] }` (a `commit` entry is a submodule's root: another repository, scanned by its own checks). */
export function parseTree(listing) {
  const entries = listing.split('\0').filter(Boolean).map(parseEntry);
  return { blobs: entries.filter(e => e.type === 'blob'), modules: entries.filter(e => e.type === 'commit').map(e => e.file) };
}

/** `{ next, body }` for the object that starts at `start` with a known size, or null when the answer is cut short. */
const sliceOf = (bytes, start, size) => (Number.isInteger(size) && start + size <= bytes.length ? { next: start + size + 1, body: bytes.subarray(start, start + size) } : null);

/** One answer of `git cat-file --batch` at `at`: `{ next, body }` (`body` null for a missing object), or null when the answer is cut short. */
function readOne(bytes, at) {
  const end = bytes.indexOf(0x0a, at);
  if (end === -1) return null;
  const header = bytes.toString('latin1', at, end).split(' ');
  return header[1] === 'missing' ? { next: end + 1, body: null } : sliceOf(bytes, end + 1, Number(header[2]));
}

/** The answers of `git cat-file --batch` for `oids`, in order: `Map(oid -> Buffer | null)`, or null when git's answer does not hold one for each. */
export function parseBatch(bytes, oids) {
  const found = new Map();
  let at = 0;
  for (const oid of oids) {
    const one = readOne(bytes, at);
    if (one === null) return null;
    found.set(oid, one.body);
    at = one.next;
  }
  return found;
}

/** `ok({ blobs, modules })` of a commit's tree. */
async function listTree(repo, commit) {
  const listed = await gitText(repo, ['ls-tree', '-r', '-z', '--full-tree', commit], 'the file list of the tree');
  return listed.ok ? ok(parseTree(listed.value)) : listed;
}

/** `ok(Map(oid -> Buffer | null))`: the blobs by one `git cat-file --batch` (each read once, a missing one null). */
async function readBlobs(repo, oids) {
  const r = await git(repo, ['cat-file', '--batch'], { input: oids.map(oid => `${oid}\n`).join(''), bytes: true });
  if (!finished(r)) return failed('the files of the tree', r);
  const bodies = parseBatch(r.value.stdoutBytes, oids);
  return bodies === null ? fail('the files of the tree could not be read (git\'s answer was cut short)') : ok(bodies);
}

/** The files of a commit's tree as texts: `ok({ texts, unreadable, modules })`. A name is `<label>:<path>`. A blob git cannot give is `unreadable` (an incomplete scan, never a clean one). */
export async function treeTexts({ repo, commit, label }) {
  const listed = await listTree(repo, commit);
  if (!listed.ok) return listed;
  const bodies = await readBlobs(repo, [...new Set(listed.value.blobs.map(b => b.oid))]);
  if (!bodies.ok) return bodies;
  const named = listed.value.blobs.map(b => ({ name: `${label}:${b.file}`, body: bodies.value.get(b.oid) }));
  return ok({ texts: named.filter(n => n.body !== null).map(n => ({ name: n.name, raw: n.body.toString('latin1'), diff: false })), unreadable: named.filter(n => n.body === null).map(n => n.name), modules: listed.value.modules });
}
