// Fetches the task inputs from the public fork (parent D18) into a cache outside every repository, and refuses anything that
// does not match inputs/hashes.txt. The files live ONLY in the fork: this repository holds their hashes and the pinned sha.
import { randomBytes } from 'node:crypto';
import { existsSync, mkdirSync, readFileSync, renameSync, rmSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { parseHashes, readFileOrNull, verifyFiles } from './inputs.mjs';
import { fail, ok } from './result.mjs';
import { runBounded } from './run.mjs';

export const FORK_URL = 'https://github.com/oleksandrdubyna88/minigit-bench.git';
export const INPUTS_BRANCH = 'inputs';
/** The commit of the fork's `inputs` branch this repository's hashes.txt describes. Also written in inputs/UPSTREAM.md. */
export const PINNED_INPUTS_SHA = 'df00cac8dae3f7bdc64a8cd5a5b7f777fec055c3';
export const HASHES_PATH = fileURLToPath(new URL('../inputs/hashes.txt', import.meta.url));
/** Entries under `heldout/` are this repository's own (the sealed suite), not the fork's: they are checked against the spike folder. */
const LOCAL_PREFIX = 'heldout/';
export const SPIKE_DIR = fileURLToPath(new URL('..', import.meta.url));
export const defaultCacheDir = () => path.join(os.homedir(), '.cache', 'minigit-bench', 'inputs');

const GIT_TIMEOUT_MS = 180_000;
const GIT_ENV = Object.freeze({ ...process.env, GIT_TERMINAL_PROMPT: '0', GIT_CONFIG_NOSYSTEM: '1' });

async function git(args, cwd) {
  const r = await runBounded('git', args, { cwd, env: GIT_ENV, timeoutMs: GIT_TIMEOUT_MS });
  if (!r.ok) return r;
  const { code, stdout, stderr, timedOut } = r.value;
  if (timedOut) return fail(`git ${args[0]} timed out after ${GIT_TIMEOUT_MS / 1000} s`);
  if (code !== 0) return fail(`git ${args[0]} exited ${code}: ${stderr.trim().slice(-300)}`);
  return ok(stdout.trim());
}

/** Verify a fetched tree against the entries: ok with { root, sha, entries }, or a fail naming every bad file. */
function verifyTree(root, sha, entries) {
  const bad = verifyFiles(root, entries, readFileOrNull).filter(r => !r.ok);
  if (bad.length === 0) return ok({ root, sha, entries });
  const named = bad.map(r => `${r.path} (${r.actual === null ? 'missing' : 'sha256 differs'})`).join(', ');
  return fail(`inputs at ${root} do not match hashes.txt: ${named}`, { mismatches: bad.map(r => r.path) });
}

/** A single-branch clone at the pinned sha, in a scratch directory beside the final one. */
async function cloneAt(repoUrl, sha, scratch) {
  const clone = await git(['clone', '--quiet', '--single-branch', '--branch', INPUTS_BRANCH, '--no-tags', repoUrl, scratch], undefined);
  if (!clone.ok) return clone;
  const checkout = await git(['checkout', '--quiet', '--detach', sha], scratch);
  if (!checkout.ok) return fail(`the pinned sha ${sha} is not on the ${INPUTS_BRANCH} branch of ${repoUrl}: ${checkout.reason}`);
  const head = await git(['rev-parse', 'HEAD'], scratch);
  return head.ok && head.value !== sha ? fail(`checked out ${head.value}, not the pinned ${sha}`) : head;
}

/**
 * `fetchInputs({ sha, cacheDir, repoUrl, hashesText, localRoot })` -> ok({ root, sha, entries, local }) | fail(reason). `entries` are the
 * fork's files (under `root`); `local` are this repository's own (`heldout/suite.enc`), checked under `localRoot`. A cache hit is
 * verified and used; a mismatch is a failure naming the file, never a silent re-fetch.
 */
export async function fetchInputs({ sha = PINNED_INPUTS_SHA, cacheDir = defaultCacheDir(), repoUrl = FORK_URL, hashesText, localRoot = SPIKE_DIR } = {}) {
  if (!/^[0-9a-f]{40}$/.test(sha)) return fail(`not a 40-hex commit sha: ${sha}`);
  const parsed = parseHashes(hashesText ?? readFileSync(HASHES_PATH, 'utf8'));
  if (!parsed.ok) return parsed;
  const local = parsed.value.filter(e => e.path.startsWith(LOCAL_PREFIX));
  const fetched = await fetchForkFiles({ sha, cacheDir, repoUrl, entries: parsed.value.filter(e => !e.path.startsWith(LOCAL_PREFIX)) });
  if (!fetched.ok) return fetched;
  const bad = verifyFiles(localRoot, local, readFileOrNull).filter(r => !r.ok);
  if (bad.length > 0) {
    return fail(`files of this repository do not match hashes.txt: ${bad.map(r => `${r.path} (${r.actual === null ? 'missing' : 'sha256 differs'})`).join(', ')}`, { mismatches: bad.map(r => r.path) });
  }
  return ok({ ...fetched.value, local });
}

async function fetchForkFiles({ sha, cacheDir, repoUrl, entries }) {
  const target = path.join(cacheDir, sha);
  if (existsSync(target)) return verifyTree(target, sha, entries);

  mkdirSync(cacheDir, { recursive: true, mode: 0o700 });
  const scratch = path.join(cacheDir, `.fetch-${sha.slice(0, 8)}-${randomBytes(4).toString('hex')}`);
  try {
    const cloned = await cloneAt(repoUrl, sha, scratch);
    if (!cloned.ok) return cloned;
    const verified = verifyTree(scratch, sha, entries);
    if (!verified.ok) return verified;
    try {
      renameSync(scratch, target);
    } catch (e) {
      if (e.code !== 'ENOTEMPTY' && e.code !== 'EEXIST') throw e;
      // another process finished first: its tree is checked below like any cache hit
    }
    return verifyTree(target, sha, entries);
  } finally {
    rmSync(scratch, { recursive: true, force: true });
  }
}
