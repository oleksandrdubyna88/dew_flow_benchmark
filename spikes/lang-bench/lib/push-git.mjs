// One way to ask git (exe + argv, a ceiling on the wait, a bounded buffer): the push check's every question to a repository.
// A non-zero exit is a value, never a throw. `kind` says whose fault: `config` (a branch that is not there), `env` (git or the network).
import os from 'node:os';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { runBounded } from '../../minigit-bench/lib/run.mjs';

const ENV = Object.freeze({ PATH: process.env.PATH, HOME: os.homedir(), LANG: 'C.UTF-8', GIT_TERMINAL_PROMPT: '0' });
const DEFAULT_TIMEOUT_MS = 60_000;
export const BLOB_MAX_BYTES = 16 * 1024 * 1024;

const ENV_KIND = Object.freeze({ kind: 'env' });

/** null when the launcher ran the process to its end within its limits; otherwise the `env` fail saying which limit (or the start) failed. */
function launchFailure(launched, what, { timeoutMs, maxBytes }) {
  if (!launched.ok) return fail(launched.reason, ENV_KIND);
  if (launched.value.timedOut) return fail(`${what} did not finish within ${timeoutMs} ms`, ENV_KIND);
  return launched.value.truncated ? fail(`${what} produced more than ${maxBytes ?? 'the allowed number of'} bytes`, ENV_KIND) : null;
}

/** ok({ code, stdout, stderr }) for any exit code the caller listed in `accept` (default: 0); otherwise a fail with git's own words. */
export async function runGit(repo, args, { timeoutMs = DEFAULT_TIMEOUT_MS, maxBytes, accept = [0], config = [] } = {}) {
  const argv = ['-C', repo, ...config.flatMap(c => ['-c', c]), ...args];
  const r = await runBounded('git', argv, { env: ENV, timeoutMs, maxBytes });
  const bad = launchFailure(r, `git ${args[0]}`, { timeoutMs, maxBytes });
  if (bad) return bad;
  const { code, stdout, stderr } = r.value;
  return accept.includes(code) ? ok({ code, stdout, stderr }) : fail(`git ${args.join(' ')} exited ${code}: ${stderr.trim().slice(-300)}`, { kind: 'env', code });
}

/** ok(Buffer) of a git command's stdout, bytes untouched (a blob is not always text), through the shared launcher; a failure or a timeout is a value. */
export async function runGitBytes(repo, args, { timeoutMs = DEFAULT_TIMEOUT_MS, maxBytes = BLOB_MAX_BYTES } = {}) {
  const r = await runBounded('git', ['-C', repo, ...args], { env: ENV, timeoutMs, maxBytes, bytes: true });
  const bad = launchFailure(r, `git ${args[0]}`, { timeoutMs, maxBytes });
  if (bad) return bad;
  return r.value.code === 0 ? ok(r.value.stdoutBytes) : fail(`git ${args[0]} failed (exit ${r.value.code}): ${r.value.stderr.trim().slice(-300)}`, ENV_KIND);
}

export const BATCH_MAX_OBJECT_BYTES = BLOB_MAX_BYTES;
export const BATCH_MAX_TOTAL_BYTES = 256 * 1024 * 1024;
const HEADER_ALLOWANCE = 160;

const limitProblem = (size, name, total, { maxObjectBytes, maxTotalBytes }) => [
  [size > maxObjectBytes, `${name} is ${size} bytes, over the ${maxObjectBytes}-byte ceiling for one object: not read, so not clean`],
  [total + size > maxTotalBytes, `the objects of one check hold more than ${maxTotalBytes} bytes: refused, not read in full`],
].find(([over]) => over)?.[1];

/** One answer of `cat-file --batch` at `at`: `{ ok, missing, value?, size?, next }` or the fail naming the limit crossed or the line that makes no sense. */
function readEntry(out, at, name, limits, total) {
  const eol = out.indexOf(0x0a, at);
  if (eol === -1) return fail(`git cat-file --batch ended before ${name}`, ENV_KIND);
  const header = out.toString('latin1', at, eol);
  if (header.endsWith(' missing')) return { ok: true, missing: true, next: eol + 1 };
  const size = Number(header.split(' ')[2]);
  const problem = Number.isInteger(size) ? limitProblem(size, name, total, limits) : `git cat-file --batch answered ${JSON.stringify(header.slice(0, 80))} for ${name}`;
  return problem ? fail(problem, ENV_KIND) : { ok: true, missing: false, size, value: out.subarray(eol + 1, eol + 1 + size), next: eol + 2 + size };
}

/** The objects of a `cat-file --batch` answer, name -> Buffer, plus the names git said are missing; or a fail naming the first limit crossed. A loop, not recursion: a check can name tens of thousands of blobs. */
function parseBatch(out, names, limits) {
  const found = new Map();
  const missing = [];
  let at = 0;
  let total = 0;
  for (const name of names) {
    const entry = readEntry(out, at, name, limits, total);
    if (!entry.ok) return entry;
    at = entry.next;
    total += entry.size ?? 0;
    if (entry.missing) missing.push(name); else found.set(name, entry.value);
  }
  return { ...ok(found), missing };
}

/**
 * ok(Map(name -> Buffer)) with `.missing` (names git does not have), all read by ONE `git cat-file --batch` process: `names` are object
 * names (`<rev>:<path>`, or a blob id). A single object over `maxObjectBytes`, or the objects together over `maxTotalBytes`, is a fail.
 */
export async function runBatchCatFile(repo, names, { maxObjectBytes = BATCH_MAX_OBJECT_BYTES, maxTotalBytes = BATCH_MAX_TOTAL_BYTES, timeoutMs = 120_000 } = {}) {
  if (names.length === 0) return { ...ok(new Map()), missing: [] };
  const budget = maxTotalBytes + names.length * HEADER_ALLOWANCE + 1024;
  const r = await runBounded('git', ['-C', repo, 'cat-file', '--batch'], { env: ENV, timeoutMs, maxBytes: budget, input: `${names.join('\n')}\n`, bytes: true });
  const bad = launchFailure(r, 'git cat-file --batch', { timeoutMs, maxBytes: maxTotalBytes });
  if (bad) return bad;
  if (r.value.code !== 0) return fail(`git cat-file --batch failed (exit ${r.value.code}): ${r.value.stderr.trim().slice(-300)}`, ENV_KIND);
  return parseBatch(r.value.stdoutBytes, names, { maxObjectBytes, maxTotalBytes });
}
