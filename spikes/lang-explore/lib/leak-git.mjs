// The git reads of the two-repository leak check (build step 2b). Read-only and bounded, never through a shell, and with nothing printed that came from a file: a failure names the step and git's exit code, never git's own words.
// git is told not to follow `GIT_DIR`-style variables to another repository, not to use replace refs (they can show other history than the repository holds) and never to fetch a missing object (a missing object is a finding of
// its own: the history is not all present). Every question that can fail comes back as `ok(value)` | `fail(sentence)`.
import { realpathSync } from 'node:fs';
import path from 'node:path';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { runBounded } from '../../minigit-bench/lib/run.mjs';

const MAX_BYTES = 512 * 1024 * 1024;
const ALLOWED_ENV = Object.freeze(['PATH', 'HOME', 'LANG', 'LC_ALL', 'TZ']);

/**
 * The environment git runs with: an allowlist (`PATH`, `HOME`, `LANG`, `LC_ALL` and `TZ` when set), never the caller's whole environment, so no `GIT_CONFIG_*`, `GIT_DIR`, `GIT_WORK_TREE`, `GIT_INDEX_FILE`,
 * `GIT_OBJECT_DIRECTORY`, `GIT_ALTERNATE_OBJECT_DIRECTORIES` or `GIT_COMMON_DIR` of the caller reaches it. The system and global configuration are off, replace refs are off (they can show other history than the repository
 * holds), a missing object is never fetched, no prompt and no optional lock (a read leaves nothing behind).
 */
export const gitEnv = () => ({
  ...Object.fromEntries(ALLOWED_ENV.filter(name => process.env[name] !== undefined).map(name => [name, process.env[name]])),
  GIT_CONFIG_NOSYSTEM: '1', GIT_CONFIG_GLOBAL: '/dev/null', GIT_NO_REPLACE_OBJECTS: '1', GIT_NO_LAZY_FETCH: '1', GIT_TERMINAL_PROMPT: '0', GIT_PAGER: 'cat', GIT_OPTIONAL_LOCKS: '0',
});

/** One git command in `repo`, with no pager and quote-path off (a file name is read as it is). `extra` is `runBounded`'s options: `input`, `bytes`, `maxBytes`. */
export const git = (repo, args, extra = {}) => runBounded('git', ['--no-pager', '-c', 'core.quotePath=false', ...args], { cwd: repo, env: gitEnv(), timeoutMs: 120_000, maxBytes: MAX_BYTES, ...extra });

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
  const r = await git(repo, ['rev-parse', '--verify', '--quiet', '--end-of-options', `${ref}^{commit}`]);
  return finished(r) ? ok(r.value.stdout.trim()) : fail(`${ref} does not resolve to a commit`);
}

/** True when HEAD is a commit (an unborn branch has none). */
export const hasHead = async repo => finished(await git(repo, ['rev-parse', '--verify', '--quiet', '--end-of-options', 'HEAD^{commit}']));

const FULL_SHA = /^[0-9a-f]{40,64}$/;

const baseArgs = sha => (sha === null ? [] : [`^${sha}`]);

/** `ok([revision arguments])` of the history to read: every local branch and HEAD, minus what `baseSha` (null for none) already holds. A base that is not a full hex object id is a `fail`: nothing else reaches `rev-list`. */
export async function walkOf(repo, baseSha) {
  if (baseSha !== null && !FULL_SHA.test(baseSha)) return fail('the base is not a full object id: it is resolved first and never passed on as given');
  return ok(['--branches', ...((await hasHead(repo)) ? ['HEAD'] : []), ...baseArgs(baseSha)]);
}

/** The sentence refusing a history with an object missing (a partial or damaged repository), or null. `git rev-list --missing=print` marks each missing object with a `?`. */
export async function missingProblem(repo, walk) {
  const r = await gitText(repo, ['rev-list', '--objects', '--missing=print', ...walk, '--'], 'the walk over the history');
  if (!r.ok) return `${r.reason}: the history is not all present`;
  const gone = r.value.split('\n').filter(line => line.startsWith('?'));
  return gone.length === 0 ? null : `${gone.length} object(s) of the history are missing from the repository: the history is not all present, so it is not scanned and not called clean`;
}
