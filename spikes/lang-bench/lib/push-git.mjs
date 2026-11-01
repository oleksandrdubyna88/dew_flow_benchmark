// One way to ask git (exe + argv, a ceiling on the wait, a bounded buffer): the push check's every question to a repository.
// A non-zero exit is a value, never a throw. `kind` says whose fault: `config` (a branch that is not there), `env` (git or the network).
import os from 'node:os';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { runBounded } from '../../minigit-bench/lib/run.mjs';

const ENV = Object.freeze({ PATH: process.env.PATH, HOME: os.homedir(), LANG: 'C.UTF-8', GIT_TERMINAL_PROMPT: '0' });
const DEFAULT_TIMEOUT_MS = 60_000;
export const BLOB_MAX_BYTES = 16 * 1024 * 1024;

/** ok({ code, stdout, stderr }) for any exit code the caller listed in `accept` (default: 0); otherwise a fail with git's own words. */
export async function runGit(repo, args, { timeoutMs = DEFAULT_TIMEOUT_MS, maxBytes, accept = [0], config = [] } = {}) {
  const argv = ['-C', repo, ...config.flatMap(c => ['-c', c]), ...args];
  const r = await runBounded('git', argv, { env: ENV, timeoutMs, maxBytes });
  if (!r.ok) return fail(r.reason, { kind: 'env' });
  const { code, stdout, stderr, timedOut, truncated } = r.value;
  if (timedOut) return fail(`git ${args[0]} did not finish within ${timeoutMs} ms`, { kind: 'env' });
  if (truncated) return fail(`git ${args[0]} produced more than ${maxBytes ?? 'the allowed number of'} bytes`, { kind: 'env' });
  if (!accept.includes(code)) return fail(`git ${args.join(' ')} exited ${code}: ${stderr.trim().slice(-300)}`, { kind: 'env', code });
  return ok({ code, stdout, stderr });
}

/** ok(Buffer) of a git command's stdout, bytes untouched (a blob is not always text), through the shared launcher; a failure or a timeout is a value. */
export async function runGitBytes(repo, args, { timeoutMs = DEFAULT_TIMEOUT_MS, maxBytes = BLOB_MAX_BYTES } = {}) {
  const r = await runBounded('git', ['-C', repo, ...args], { env: ENV, timeoutMs, maxBytes, bytes: true });
  if (!r.ok) return fail(r.reason, { kind: 'env' });
  const { code, stderr, timedOut, truncated, stdoutBytes } = r.value;
  if (timedOut) return fail(`git ${args[0]} did not finish within ${timeoutMs} ms`, { kind: 'env' });
  if (truncated) return fail(`git ${args[0]} produced more than ${maxBytes} bytes`, { kind: 'env' });
  return code === 0 ? ok(stdoutBytes) : fail(`git ${args[0]} failed (exit ${code}): ${stderr.trim().slice(-300)}`, { kind: 'env' });
}

export const BATCH_MAX_OBJECT_BYTES = BLOB_MAX_BYTES;
export const BATCH_MAX_TOTAL_BYTES = 256 * 1024 * 1024;
const HEADER_ALLOWANCE = 160;

/** The objects of a `cat-file --batch` answer, name -> Buffer, plus the names git said are missing; or a fail naming the first limit crossed. */
function parseBatch(out, names, { maxObjectBytes, maxTotalBytes }) {
  const found = new Map();
  const missing = [];
  let at = 0;
  let total = 0;
  for (const name of names) {
    const eol = out.indexOf(0x0a, at);
    if (eol === -1) return fail(`git cat-file --batch ended before ${name}`, { kind: 'env' });
    const header = out.toString('latin1', at, eol);
    at = eol + 1;
    if (header.endsWith(' missing')) { missing.push(name); continue; }
    const size = Number(header.split(' ')[2]);
    total += size;
    if (!Number.isInteger(size)) return fail(`git cat-file --batch answered ${JSON.stringify(header.slice(0, 80))} for ${name}`, { kind: 'env' });
    if (size > maxObjectBytes) return fail(`${name} is ${size} bytes, over the ${maxObjectBytes}-byte ceiling for one object: not read, so not clean`, { kind: 'env' });
    if (total > maxTotalBytes) return fail(`the objects of one check hold more than ${maxTotalBytes} bytes: refused, not read in full`, { kind: 'env' });
    found.set(name, out.subarray(at, at + size));
    at += size + 1;
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
  if (!r.ok) return fail(r.reason, { kind: 'env' });
  if (r.value.timedOut) return fail(`git cat-file --batch did not finish within ${timeoutMs} ms`, { kind: 'env' });
  if (r.value.truncated) return fail(`the objects of one check hold more than ${maxTotalBytes} bytes: refused, not read in full`, { kind: 'env' });
  if (r.value.code !== 0) return fail(`git cat-file --batch failed (exit ${r.value.code}): ${r.value.stderr.trim().slice(-300)}`, { kind: 'env' });
  return parseBatch(r.value.stdoutBytes, names, { maxObjectBytes, maxTotalBytes });
}
