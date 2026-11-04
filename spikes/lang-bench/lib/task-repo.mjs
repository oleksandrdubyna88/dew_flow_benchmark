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
import { checkDestination } from './workspace-build.mjs';

/** The commit of the task repository's `main` that `inputs/hashes-lang.txt` describes (FORMAT v2, gated). */
export const PINNED_TASK_SHA = '40e678fe458dedb220afd4a99fd478f3d39157f9';
export const HASHES_PATH = fileURLToPath(new URL('../inputs/hashes-lang.txt', import.meta.url));
export const defaultCacheDir = () => path.join(os.homedir(), '.cache', 'lang-v2');

/** The folders whose every file must be on the hash list (a workspace is exactly its listed files); files elsewhere are checked only when listed. */
const WORKSPACE_ROOTS = Object.freeze(['csharp', 'node', 'runner', 'tasks']);
const STACKS = Object.freeze(['csharp', 'node']);
const TASK_NAME = /^[A-Za-z0-9][A-Za-z0-9_-]*$/;
const GIT_TIMEOUT_MS = 180_000;

// ---- the verification (T-L1) -----------------------------------------------------------------------------------------------------------

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

/** Every file under a workspace root that is not on the list (`not on the hash list`), and every link or other entry there (`not a regular file`). */
function rootProblems(root, listed) {
  return WORKSPACE_ROOTS.filter(name => existsSync(path.join(root, name))).flatMap(name => walk(path.join(root, name), root).flatMap(file => {
    if (!file.regular) return [{ path: file.rel, why: 'not a regular file' }];
    return listed.has(file.rel) ? [] : [{ path: file.rel, why: 'not on the hash list' }];
  }));
}

/** `ok({ root, sha, entries })` when the tree is exactly what the list says (both rules), else a `fail` naming each file. */
function verifyTree(root, sha, entries) {
  const problems = [...listProblems(root, entries), ...rootProblems(root, new Set(entries.map(e => e.path)))];
  if (problems.length === 0) return ok({ root, sha, entries });
  const seen = new Set();
  const unique = problems.filter(p => (seen.has(p.path) ? false : seen.add(p.path)));
  return fail(`the task repository files at ${root} do not match the hash list: ${unique.map(p => `${p.path} (${p.why})`).join(', ')}`, { mismatches: unique.map(p => p.path) });
}

// ---- the fetch ---------------------------------------------------------------------------------------------------------------------------

/** `git <args>` in `cwd` with a ceiling and a closed environment (no prompt, no system or user config). -> `ok(stdout)` | `fail`. */
async function git(args, { cwd, home }) {
  const env = { PATH: '/usr/bin:/bin', HOME: home, GIT_TERMINAL_PROMPT: '0', GIT_CONFIG_NOSYSTEM: '1', GIT_CONFIG_GLOBAL: '/dev/null' };
  const r = await runBounded('git', args, { cwd, env, timeoutMs: GIT_TIMEOUT_MS });
  if (!r.ok) return r;
  const { code, stdout, stderr, timedOut } = r.value;
  if (timedOut) return fail(`git ${args[0]} timed out after ${GIT_TIMEOUT_MS / 1000} s`);
  return code === 0 ? ok(stdout.trim()) : fail(`git ${args[0]} exited ${code}: ${stderr.trim().slice(-300)}`);
}

/** A clone of the local `repo` at the pinned `sha`, with its `.git` removed: the cached tree is the files at the sha and holds no repository. */
async function cloneAt({ repo, sha, scratch, home }) {
  const cloned = await git(['clone', '--quiet', '--no-hardlinks', '--no-checkout', '--no-tags', repo, scratch], { home });
  if (!cloned.ok) return cloned;
  const checkout = await git(['checkout', '--quiet', '--detach', sha], { cwd: scratch, home });
  if (!checkout.ok) return fail(`the pinned sha ${sha} cannot be checked out of ${repo}: ${checkout.reason}`);
  const head = await git(['rev-parse', 'HEAD'], { cwd: scratch, home });
  if (!head.ok) return head;
  if (head.value !== sha) return fail(`checked out ${head.value}, not the pinned ${sha}`);
  rmSync(path.join(scratch, '.git'), { recursive: true, force: true });
  return ok(scratch);
}

/** `repo` must be an absolute path to an existing local folder: a URL, a relative path and `file://` are refused naming the rule. */
function repoProblem(repo) {
  const rule = 'the task repository must be an absolute path to an existing local folder: no fetch here reaches the network';
  if (typeof repo !== 'string' || !path.isAbsolute(repo) || repo.includes('://')) return `${rule} (got ${JSON.stringify(repo)})`;
  try {
    return lstatSync(repo).isDirectory() ? null : `${rule} (${repo} is not a folder)`;
  } catch {
    return `${rule} (${repo} does not exist)`;
  }
}

/**
 * `fetchTaskRepo({ sha, cacheDir, repo, hashesText })` -> `ok({ root, sha, entries })` | `fail(reason, { mismatches })`. `root` is `<cacheDir>/<sha>/`. A cache hit is verified and used; a mismatch is a failure
 * naming the file, never a silent re-fetch. `hashesText` defaults to `inputs/hashes-lang.txt`.
 */
export async function fetchTaskRepo({ sha = PINNED_TASK_SHA, cacheDir = defaultCacheDir(), repo, hashesText } = {}) {
  if (!/^[0-9a-f]{40}$/.test(sha)) return fail(`not a 40 hex commit sha: ${sha}`);
  const problem = repoProblem(repo);
  if (problem !== null) return fail(problem);
  const parsed = parseHashes(hashesText ?? readFileSync(HASHES_PATH, 'utf8'));
  if (!parsed.ok) return parsed;
  const target = path.join(cacheDir, sha);
  if (existsSync(target)) return verifyTree(target, sha, parsed.value);
  mkdirSync(cacheDir, { recursive: true, mode: 0o700 });
  const scratch = path.join(cacheDir, `.fetch-${sha.slice(0, 8)}-${randomBytes(4).toString('hex')}`);
  try {
    const cloned = await cloneAt({ repo, sha, scratch, home: cacheDir });
    if (!cloned.ok) return cloned;
    const verified = verifyTree(scratch, sha, parsed.value);
    if (!verified.ok) return verified;
    try {
      renameSync(scratch, target);
    } catch (error) {
      if (error.code !== 'ENOTEMPTY' && error.code !== 'EEXIST') throw error; // another process finished first: its tree is checked below like any cache hit
    }
    return verifyTree(target, sha, parsed.value);
  } finally {
    rmSync(scratch, { recursive: true, force: true });
  }
}

// ---- the bare copy (T-L2) --------------------------------------------------------------------------------------------------------------

/** The task folder's files as relative paths, or the first link or other non-regular entry as a `fail`. */
function filesOfTask(src) {
  const all = walk(src);
  const odd = all.find(file => !file.regular);
  return odd === undefined ? ok(all.map(f => f.rel).sort()) : fail(`${path.join(src, odd.rel)} is a link or another kind of entry: a task folder holds regular files only`);
}

/**
 * `copyTaskFolder({ root, stack, task, out })` -> `ok({ files })` | `fail`: exactly `<root>/<stack>/<task>/` into `out`, nothing else (no root file, no other stack, no `todo/`). `task` is one path segment of
 * letters, digits, `_` and `-`; `out` passes the destination guard of the builder; a link inside the folder is refused before anything is written. Modes are kept.
 */
export function copyTaskFolder({ root, stack, task, out }) {
  if (!STACKS.includes(stack)) return fail(`the stack ${JSON.stringify(stack)} is not one of ${STACKS.join(', ')}`);
  if (typeof task !== 'string' || !TASK_NAME.test(task)) return fail(`the task ${JSON.stringify(task)} must match ${TASK_NAME}: one folder name, no path`);
  const src = path.join(root, stack, task);
  if (!existsSync(src) || !lstatSync(src).isDirectory()) return fail(`the task folder ${src} does not exist`);
  const dest = checkDestination(out);
  if (!dest.ok) return dest;
  const files = filesOfTask(src);
  if (!files.ok) return files;
  mkdirSync(dest.value, { recursive: true });
  cpSync(src, dest.value, { recursive: true });
  return ok({ files: files.value });
}
