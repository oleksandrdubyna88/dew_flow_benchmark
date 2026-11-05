// E1.S3: where a workspace may be built (accepted finding a), shared by the builder and the bare copy. A destination is refused when it is a link, when it is inside a git worktree of any state, when any folder
// above it (or it itself) holds a `.git` entry or is a bare repository, when a path component is `.git`, and when git cannot answer. Outside every repository an existing destination must be an empty folder.
// The walk by `lstat` is what git alone cannot say: `git rev-parse` answers "not a worktree" for a linked worktree whose admin folder is gone, for a path inside `.git/` and for a bare repository.
import { randomBytes } from 'node:crypto';
import { lstatSync, mkdirSync, readFileSync, readdirSync, realpathSync, renameSync, rmSync } from 'node:fs';
import path from 'node:path';
import { gitWorktreeOf } from '../../minigit-bench/lib/git-guard.mjs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';

const ABSENT = new Set(['ENOENT', 'ENOTDIR']);

/** `lstat` of `target`, or null when nothing is there (or a folder above it is a file). Any other error is thrown: only "absent" is an expected answer. */
export function lstatOrNull(target) {
  try {
    return lstatSync(target);
  } catch (error) {
    if (ABSENT.has(error.code)) return null;
    throw error;
  }
}

/** The folders from `target` up to the root, nearest first. */
function ancestorsOf(target) {
  const list = [];
  for (let dir = target; ; dir = path.dirname(dir)) {
    list.push(dir);
    if (path.dirname(dir) === dir) return list;
  }
}

const hasEntry = (dir, name) => lstatOrNull(path.join(dir, name)) !== null;
const isFolderEntry = (dir, name) => lstatOrNull(path.join(dir, name))?.isDirectory() === true;
const HEAD_LINE = /^(ref:\s*|[0-9a-f]{40})/;

/** `HEAD` is a link (the old form of a symbolic ref), or a regular file that reads as a git HEAD (a `ref:` line or a commit id): three plain folders, or a text file called HEAD, are not a repository. */
function isHeadFile(dir) {
  const head = lstatOrNull(path.join(dir, 'HEAD'));
  if (head?.isSymbolicLink() === true) return true;
  return head?.isFile() === true && HEAD_LINE.test(readFileSync(path.join(dir, 'HEAD'), 'utf8').slice(0, 64));
}

const isBare = dir => isFolderEntry(dir, 'objects') && isFolderEntry(dir, 'refs') && isHeadFile(dir);

/** What a folder is as a repository: it holds a `.git` entry (a folder, or the file of a linked worktree), or it is itself a bare repository; else null. */
function repositoryKind(dir) {
  if (hasEntry(dir, '.git')) return 'a .git entry';
  return isBare(dir) ? 'a bare git repository' : null;
}

/** A sentence when the destination is, or is under, a repository by the walk of `lstat`; else null. */
function walkProblem(target) {
  if (target.split(path.sep).includes('.git')) return 'is inside a .git folder';
  const found = ancestorsOf(target).map(dir => ({ dir, kind: repositoryKind(dir) })).find(entry => entry.kind !== null);
  return found === undefined ? null : `is under ${found.dir}, which holds ${found.kind}`;
}

const linkProblem = target => (lstatOrNull(target)?.isSymbolicLink() ? 'is a link' : null);

/** The nearest folder of `target` (or `target` itself) that exists. */
function existingAncestor(target) {
  for (let dir = target; ; dir = path.dirname(dir)) {
    if (lstatOrNull(dir) !== null || path.dirname(dir) === dir) return dir;
  }
}

/** The walk of `.git` entries over the path as written and over the real path of its deepest existing part (a link into a repository is a way in). */
function repositoryWalk(target) {
  const direct = walkProblem(target);
  if (direct !== null) return direct;
  const real = realpathSync(existingAncestor(target));
  const viaLink = real === target ? null : walkProblem(real);
  return viaLink === null ? null : `${viaLink} (through a link, real path ${real})`;
}

/** The walk first, then git's own answer: a worktree of any state is refused, and a git that cannot answer is never a pass. */
function repositoryProblem(target, worktreeOf) {
  const walked = repositoryWalk(target);
  if (walked !== null) return walked;
  const inside = worktreeOf(target);
  if (!inside.ok) return `cannot be judged: ${inside.reason}`;
  return inside.value === null ? null : `is inside the git worktree ${inside.value}: a workspace is never built where a stray \`git add\` reaches it`;
}

/** Outside every repository: nothing there is fine, a folder must be empty, anything else is refused. */
function contentProblem(target) {
  const found = lstatOrNull(target);
  if (found === null) return null;
  if (!found.isDirectory()) return 'exists and is not a folder';
  return readdirSync(target).length === 0 ? null : 'exists and is not empty';
}

const JUDGE_ERRORS = new Set(['EACCES', 'ELOOP', 'ENAMETOOLONG', 'ENOENT']);

/** The first problem of the destination, or null. A path the filesystem cannot answer for (permission, a link loop, a name too long) is a problem that says so, never a throw. */
function destinationProblem(target, worktreeOf) {
  try {
    return linkProblem(target) ?? repositoryProblem(target, worktreeOf) ?? contentProblem(target);
  } catch (error) {
    if (JUDGE_ERRORS.has(error.code)) return `cannot be judged (${error.code})`;
    throw error;
  }
}

/**
 * -> `ok(resolved out)` | `fail(reason, { refusal: true })`. `worktreeOf` is `gitWorktreeOf`, a parameter so a test can make git unable to answer.
 */
export function checkDestination(out, worktreeOf = gitWorktreeOf) {
  const target = path.resolve(out);
  const problem = destinationProblem(target, worktreeOf);
  return problem === null ? ok(target) : fail(`the destination ${target} ${problem}`, { refusal: true });
}

// ---- the atomic write (shared by the builder and the bare copy) -----------------------------------------------------------------------------------

const stagingOf = out => path.join(path.dirname(out), `.${path.basename(out)}.staging-${randomBytes(4).toString('hex')}`);

/**
 * Writes a folder atomically: `fill(stage)` fills a unique sibling staging folder, which is renamed onto `out` only when complete (an existing empty `out` is replaced by the rename). The staging folder is made with an
 * exclusive `mkdir` after the parent exists, and only a folder this call made is ever removed. A failure removes the staging folder and leaves `out` as it was, so a retry is not refused; parent folders that were
 * created for `out` stay. `stageOf` exists for the tests. -> `ok(out)` | `fail`.
 */
export function writeAtomically(out, fill, { stageOf = stagingOf } = {}) {
  const stage = stageOf(out);
  let made = false;
  try {
    mkdirSync(path.dirname(out), { recursive: true });
    mkdirSync(stage);
    made = true;
    fill(stage);
    renameSync(stage, out);
    return ok(out);
  } catch (error) {
    if (made) rmSync(stage, { recursive: true, force: true });
    return fail(`the workspace could not be written to ${out} (${error.code ?? error.message}); a staging folder of this call was removed, and parent folders made for the destination stay`);
  }
}
