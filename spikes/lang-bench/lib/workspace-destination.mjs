// E1.S3: where a workspace may be built (accepted finding a), shared by the builder and the bare copy. A destination is refused when it is a link, when it is inside a git worktree of any state, when any folder
// above it (or it itself) holds a `.git` entry or is a bare repository, when a path component is `.git`, and when git cannot answer. Outside every repository an existing destination must be an empty folder.
// The walk by `lstat` is what git alone cannot say: `git rev-parse` answers "not a worktree" for a linked worktree whose admin folder is gone, for a path inside `.git/` and for a bare repository.
import { lstatSync, readdirSync } from 'node:fs';
import path from 'node:path';
import { gitWorktreeOf } from '../../minigit-bench/lib/git-guard.mjs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';

const ABSENT = new Set(['ENOENT', 'ENOTDIR']);
const BARE_ENTRIES = Object.freeze(['HEAD', 'objects', 'refs']);

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
const isBare = dir => BARE_ENTRIES.every(name => hasEntry(dir, name));

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

/** The walk first, then git's own answer: a worktree of any state is refused, and a git that cannot answer is never a pass. */
function repositoryProblem(target, worktreeOf) {
  const walked = walkProblem(target);
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

/**
 * -> `ok(resolved out)` | `fail(reason, { refusal: true })`. `worktreeOf` is `gitWorktreeOf`, a parameter so a test can make git unable to answer.
 */
export function checkDestination(out, worktreeOf = gitWorktreeOf) {
  const target = path.resolve(out);
  const problem = linkProblem(target) ?? repositoryProblem(target, worktreeOf) ?? contentProblem(target);
  return problem === null ? ok(target) : fail(`the destination ${target} ${problem}`, { refusal: true });
}
