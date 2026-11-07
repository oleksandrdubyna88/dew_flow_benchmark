// E1.S3: the task repository for the builder (parent D38, T-L1, T-L2). `fetchTaskRepo` puts the files of the task repository at ONE pinned commit into `<cache>/<sha>/` and checks them against the hash list
// of `inputs/hashes-lang.txt`; `copyTaskFolder` copies exactly one task folder, for the bare workspace of the process arm. v1's `fetchInputs` is the shape (a scratch folder beside the target, a rename, a cache hit that is
// verified and never fetched again); it is not imported, because it is bound to v1's branch and hash file.
// The repository is always a LOCAL folder: no fetch here reaches the network (the host has no egress box around git, and the task repository's checkout is local).
import { randomBytes } from 'node:crypto';
import { cpSync, existsSync, lstatSync, mkdirSync, readFileSync, readdirSync, renameSync, rmSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { parseHashes, verifyFiles } from '../../minigit-bench/lib/inputs.mjs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { runBounded } from '../../minigit-bench/lib/run.mjs';
import { checkDestination, lstatOrNull, writeAtomically } from './workspace-destination.mjs';

/** The commit of the task repository's `main` that `inputs/hashes-lang.txt` describes (FORMAT v2 with its first amendment, gated). */
export const PINNED_TASK_SHA = '4ddb8f80e84eef8b61db646fdf79560adb885a81';
export const HASHES_PATH = fileURLToPath(new URL('../inputs/hashes-lang.txt', import.meta.url));
export const defaultCacheDir = () => path.join(os.homedir(), '.cache', 'lang-v2');

/** The folders whose every file must be on the hash list (a workspace is exactly its listed files); files elsewhere are checked only when listed. */
const WORKSPACE_ROOTS = Object.freeze(['csharp', 'node', 'runner', 'tasks']);
const STACKS = Object.freeze(['csharp', 'node']);
const TASK_NAME = /^[A-Za-z0-9][A-Za-z0-9_-]*$/;
const GIT_TIMEOUT_MS = 180_000;

// ---- the verification (T-L1) -----------------------------------------------------------------------------------------------------------

const refuse = (reason, extra = {}) => fail(reason, { ...extra, refusal: true });

/** Every entry of the list that is missing or has other bytes: `{ path, why }`. A link is "unreadable" to the reader, so a listed link is `sha256 differs`. */
function listProblems(root, entries) {
  return verifyFiles(root, entries).filter(r => !r.ok).map(r => ({ path: r.path, why: r.actual === null ? 'missing' : 'sha256 differs' }));
}

/** The paths under `dir` by `lstat`: `{ rel, regular }` for every file, link or other entry (a folder is walked, never listed). */
function walk(dir, base = dir) {
  return readdirSync(dir, { withFileTypes: true }).flatMap(entry => {
    const abs = path.join(dir, entry.name);
    const rel = path.relative(base, abs).split(path.sep).join('/');
    const stat = lstatSync(abs);
    return stat.isDirectory() ? walk(abs, base) : [{ rel, regular: stat.isFile() }];
  });
}

const fileProblem = (file, listed) => {
  if (!file.regular) return [{ path: file.rel, why: 'not a regular file' }];
  return listed.has(file.rel) ? [] : [{ path: file.rel, why: 'not on the hash list' }];
};

/** One workspace root: absent is fine, a link or a regular file in its place is `not a regular file` (never a throw), a folder is walked. */
function oneRootProblems(root, name, listed) {
  const stat = lstatOrNull(path.join(root, name));
  if (stat === null) return [];
  if (!stat.isDirectory()) return [{ path: name, why: 'not a regular file' }];
  return walk(path.join(root, name), root).flatMap(file => fileProblem(file, listed));
}

/** Every file under a workspace root that is not on the list (`not on the hash list`), and every link or other entry there (`not a regular file`). */
const rootProblems = (root, listed) => WORKSPACE_ROOTS.flatMap(name => oneRootProblems(root, name, listed));

/** `ok({ root, sha, entries })` when the tree is exactly what the list says (both rules), else a refusal naming each file. */
function verifyTree(root, sha, entries) {
  const problems = [...listProblems(root, entries), ...rootProblems(root, new Set(entries.map(e => e.path)))];
  if (problems.length === 0) return ok({ root, sha, entries });
  const seen = new Set();
  const unique = problems.filter(p => (seen.has(p.path) ? false : seen.add(p.path)));
  return refuse(`the task repository files at ${root} do not match the hash list: ${unique.map(p => `${p.path} (${p.why})`).join(', ')}`, { mismatches: unique.map(p => p.path) });
}

// ---- the fetch ---------------------------------------------------------------------------------------------------------------------------

/** `git <args>` in `cwd` with a ceiling and a closed environment (no prompt, no system or user config). -> `ok(stdout)` | `fail` (a git failure or a timeout is not a refusal). */
async function git(args, { cwd, home }) {
  const env = { PATH: '/usr/bin:/bin', HOME: home, GIT_TERMINAL_PROMPT: '0', GIT_CONFIG_NOSYSTEM: '1', GIT_CONFIG_GLOBAL: '/dev/null' };
  const r = await runBounded('git', args, { cwd, env, timeoutMs: GIT_TIMEOUT_MS });
  if (!r.ok) return r;
  const { code, stdout, stderr, timedOut } = r.value;
  if (timedOut) return fail(`git ${args[0]} timed out after ${GIT_TIMEOUT_MS / 1000} s`);
  return code === 0 ? ok(stdout.trim()) : fail(`git ${args[0]} exited ${code}: ${stderr.trim().slice(-300)}`);
}

/** The clone of the local `repo` with `sha` checked out: `ok(HEAD)` | `fail`. */
async function checkoutAt({ repo, sha, scratch, home }) {
  const cloned = await git(['clone', '--quiet', '--no-hardlinks', '--no-checkout', '--no-tags', repo, scratch], { home });
  if (!cloned.ok) return cloned;
  const checkout = await git(['checkout', '--quiet', '--detach', sha], { cwd: scratch, home });
  if (!checkout.ok) return fail(`the pinned sha ${sha} cannot be checked out of ${repo}: ${checkout.reason}`);
  return git(['rev-parse', 'HEAD'], { cwd: scratch, home });
}

/** A clone of the local `repo` at the pinned `sha`, with its `.git` removed: the cached tree is the files at the sha and holds no repository. */
async function cloneAt(args) {
  const head = await checkoutAt(args);
  if (!head.ok) return head;
  if (head.value !== args.sha) return fail(`checked out ${head.value}, not the pinned ${args.sha}`);
  rmSync(path.join(args.scratch, '.git'), { recursive: true, force: true });
  return ok(args.scratch);
}

const isLocalPath = repo => typeof repo === 'string' && path.isAbsolute(repo) && !repo.includes('://');

function isFolder(target) {
  try {
    return lstatSync(target).isDirectory();
  } catch {
    return false;
  }
}

/** `repo` must be an absolute path to an existing local folder: a URL, a relative path and `file://` are refused naming the rule. */
function repoProblem(repo) {
  const rule = 'the task repository must be an absolute path to an existing local folder: no fetch here reaches the network';
  if (!isLocalPath(repo)) return `${rule} (got ${JSON.stringify(repo)})`;
  return isFolder(repo) ? null : `${rule} (${repo} is not an existing folder)`;
}

const requestProblem = (sha, repo) => (/^[0-9a-f]{40}$/.test(sha) ? repoProblem(repo) : `not a 40 hex commit sha: ${sha}`);

/** Rename the verified scratch onto the target (another process may have won: its tree is checked like a cache hit). */
function settle({ scratch, target, sha, entries }) {
  try {
    renameSync(scratch, target);
  } catch (error) {
    if (error.code !== 'ENOTEMPTY' && error.code !== 'EEXIST') throw error;
  }
  return verifyTree(target, sha, entries);
}

async function fetchInto({ sha, cacheDir, repo, target, entries }) {
  mkdirSync(cacheDir, { recursive: true, mode: 0o700 });
  const scratch = path.join(cacheDir, `.fetch-${sha.slice(0, 8)}-${randomBytes(4).toString('hex')}`);
  try {
    const cloned = await cloneAt({ repo, sha, scratch, home: cacheDir });
    if (!cloned.ok) return cloned;
    const verified = verifyTree(scratch, sha, entries);
    return verified.ok ? settle({ scratch, target, sha, entries }) : verified;
  } finally {
    rmSync(scratch, { recursive: true, force: true });
  }
}

/**
 * `fetchTaskRepo({ sha, cacheDir, repo, hashesText })` -> `ok({ root, sha, entries })` | `fail(reason, { mismatches, refusal })`. `root` is `<cacheDir>/<sha>/`. A cache hit is verified and used; a mismatch is a refusal
 * naming the file, never a silent re-fetch. A rule violation (a bad sha, a repository that is not a local folder) is a refusal; a git failure or a timeout is a plain failure. `hashesText` defaults to `inputs/hashes-lang.txt`.
 */
export async function fetchTaskRepo({ sha = PINNED_TASK_SHA, cacheDir = defaultCacheDir(), repo, hashesText } = {}) {
  const problem = requestProblem(sha, repo);
  if (problem !== null) return refuse(problem);
  const parsed = parseHashes(hashesText ?? readFileSync(HASHES_PATH, 'utf8'));
  if (!parsed.ok) return parsed;
  const target = path.join(cacheDir, sha);
  if (existsSync(target)) return verifyTree(target, sha, parsed.value);
  return fetchInto({ sha, cacheDir, repo, target, entries: parsed.value });
}

// ---- the bare copy (T-L2) --------------------------------------------------------------------------------------------------------------

/** The task folder's files as relative paths, or the first link or other non-regular entry as a `fail`. */
function filesOfTask(src) {
  const all = walk(src);
  const odd = all.find(file => !file.regular);
  return odd === undefined ? ok(all.map(f => f.rel).sort()) : fail(`${path.join(src, odd.rel)} is a link or another kind of entry: a task folder holds regular files only`);
}

function taskProblem({ stack, task }) {
  if (!STACKS.includes(stack)) return `the stack ${JSON.stringify(stack)} is not one of ${STACKS.join(', ')}`;
  return typeof task === 'string' && TASK_NAME.test(task) ? null : `the task ${JSON.stringify(task)} must match ${TASK_NAME}: one folder name, no path`;
}

/** `ok(<root>/<stack>/<task>)` for a legal stack and task whose folder exists, else a `fail`. */
function sourceFolder({ root, stack, task }) {
  const problem = taskProblem({ stack, task });
  if (problem !== null) return fail(problem);
  const src = path.join(root, stack, task);
  return isFolder(src) ? ok(src) : fail(`the task folder ${src} does not exist`);
}

/** Everything the copy needs, checked before anything is written. -> `ok({ src, dest, files })` | `fail`. */
function copyPlan({ root, stack, task, out }) {
  const src = sourceFolder({ root, stack, task });
  if (!src.ok) return src;
  const dest = checkDestination(out);
  if (!dest.ok) return dest;
  const files = filesOfTask(src.value);
  return files.ok ? ok({ src: src.value, dest: dest.value, files: files.value }) : files;
}

/**
 * `copyTaskFolder({ root, stack, task, out })` -> `ok({ files })` | `fail`: exactly `<root>/<stack>/<task>/` into `out`, nothing else (no root file, no other stack, no `todo/`). `task` is one path segment of
 * letters, digits, `_` and `-`; `out` passes the destination guard of the builder; a link inside the folder is refused before anything is written. Modes are kept. The copy is atomic: it is made in a staging
 * folder and renamed onto `out` when complete.
 */
export function copyTaskFolder(args) {
  const plan = copyPlan(args);
  if (!plan.ok) return plan;
  const written = writeAtomically(plan.value.dest, stage => cpSync(plan.value.src, stage, { recursive: true }));
  return written.ok ? ok({ files: plan.value.files }) : written;
}
