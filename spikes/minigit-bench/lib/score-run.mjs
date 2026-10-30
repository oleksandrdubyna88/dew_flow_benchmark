// Scoring one finished session as a child process (E3b.S1 step 6), after the barrier and never while a cell still runs: `taskset -c 0-13` (the cells are
// done, the whole machine is free), lowered, bounded as a WHOLE by `outerTimeoutMs` (the scorer's own steps are bounded one by one, the process is not).
//   exit 0  scored            exit 3  environment: retried 3 x 60 s apart, then a RESUMABLE halt            exit 4 / 5  failed (usage / the scorer itself)
//   outer timeout  `scorer-timeout`: the group is killed (runBounded), never retried (a hang repeats), the session stays `ran`
// A session is NEVER voided for a scorer fault: every kind here leaves the session's class alone.
import { existsSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { fail, ok } from './result.mjs';
import { runBounded } from './run.mjs';

export const SCORER = fileURLToPath(new URL('../score-session.mjs', import.meta.url));
/** The scorer's own steps sum to about an hour at worst (build 600 s, two visible and two upstream scripts, held-out cases, latency runs). */
export const SCORER_OUTER_MS = 90 * 60 * 1000;
const ENV_RETRIES = 3;
const ENV_WAIT_MS = 60_000;

/** The last line the scorer printed that says why (its `environment:` / `scorer failed:` / `usage error:` line), else the tail of its output. */
const why = stdout => stdout.trim().split('\n').filter(Boolean).at(-1) ?? '(no output)';

function argvFor({ session, source, stack, phase, toolchains, nugetCache, npmCache, scorer, cpus }) {
  return ['-c', cpus, 'nice', '-n', '19', process.execPath, scorer, '--session', session, '--source', source, '--arm', 'bare', '--stack', stack, '--phase', phase,
    ...toolchains.flatMap(t => ['--toolchain', t]), ...(nugetCache ? ['--nuget-cache', nugetCache] : []), ...(npmCache ? ['--npm-cache', npmCache] : [])];
}

function readScore(session) {
  const file = path.join(session, 'score', 'score.json');
  if (!existsSync(file)) return fail(`the scorer exited 0 but wrote no ${file}`, { kind: 'failed' });
  try { return ok(JSON.parse(readFileSync(file, 'utf8'))); } catch (e) { return fail(`${file} is unreadable: ${e.message}`, { kind: 'failed' }); }
}

/**
 * -> `{ kind: 'scored', score, trace }` | `{ kind: 'environment' | 'failed' | 'scorer-timeout', reason }`. `sleep` is injected so a test does not wait a minute.
 * `scorer` and `cpus` are parameters for the same reason (a stub scorer; the real default is the whole machine).
 */
export async function scoreSession({ session, source, stack, phase, toolchains, nugetCache, npmCache, outerTimeoutMs = SCORER_OUTER_MS, sleep, scorer = SCORER, cpus = '0-13' }) {
  const argv = argvFor({ session, source, stack, phase, toolchains, nugetCache, npmCache, scorer, cpus });
  let last = null;
  for (let attempt = 0; attempt <= ENV_RETRIES; attempt += 1) {
    if (attempt > 0) await sleep(ENV_WAIT_MS);
    const r = await runBounded('taskset', argv, { timeoutMs: outerTimeoutMs });
    if (!r.ok) return { kind: 'failed', reason: r.reason };
    if (r.value.timedOut) return { kind: 'scorer-timeout', reason: `the scorer did not finish within ${Math.round(outerTimeoutMs / 1000)} s and its process group was killed` };
    if (r.value.code === 0) {
      const score = readScore(session);
      return score.ok ? { kind: 'scored', score: score.value, trace: r.value.stdout } : { kind: 'failed', reason: score.reason };
    }
    last = why(r.value.stdout);
    if (r.value.code !== 3) return { kind: 'failed', reason: `the scorer exited ${r.value.code}: ${last}` };
  }
  return { kind: 'environment', reason: `${last} (after ${ENV_RETRIES} retries, ${ENV_WAIT_MS / 1000} s apart)` };
}
