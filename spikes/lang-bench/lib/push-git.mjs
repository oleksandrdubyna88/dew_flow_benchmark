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
