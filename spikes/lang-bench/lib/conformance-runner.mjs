// The runner's parse of a spec's sample pairs (E1.S8): `python3 -I -B run_samples.py --kind <daemon|web> --parse-only <samples>`, never a second parser. The runner is the bench's own
// (spikes/lang-bench/runner/), found relative to this file and never through `--repo`. It runs through v1's `runBounded` (exe + argv, a ceiling, the whole process tree ended at the
// ceiling). A run that cannot be made or finished is a fail, never a pass: the checker exits 4.
import { existsSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { runBounded } from '../../minigit-bench/lib/run.mjs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';

export const RUNNER = fileURLToPath(new URL('../runner/run_samples.py', import.meta.url));
export const RUNNER_TIMEOUT_MS = 60_000;
const EXIT_CONFIG = 4;
const MAX_LINES = 40;

const linesOf = text => text.split('\n').map(line => line.trimEnd()).filter(line => line !== '');

const endedWith = (code, lines) => fail(`the runner ended with exit ${code}${lines.length > 0 ? `: ${lines[0]}` : ''}`, { kind: 'env' });

/** What a finished run means: no finding at exit 0, the runner's own lines at exit 4, a fail for a time-out or any other end. */
function outcomeOf(run, timeoutMs) {
  const { code, stderr, timedOut } = run.value;
  if (timedOut) return fail(`the runner did not finish within ${timeoutMs / 1000} s and was stopped`, { kind: 'env' });
  if (code === 0) return ok([]);
  const lines = linesOf(stderr).slice(0, MAX_LINES);
  return code === EXIT_CONFIG ? ok(lines.map(line => `the runner refuses the pairs — ${line}`)) : endedWith(code, lines);
}

/**
 * ok([finding]) once the runner has parsed the pairs: no finding when it exits 0, its own `(file, line, message)` lines when it exits 4 (a malformed sample); a fail when the runner or
 * python is missing, the run times out or the runner ends in any other way. Seams: `python`, `runner`, `timeoutMs`, `tmp` (the folder given as TMPDIR).
 */
export async function parseSamples({ dir, kind, tmp, python = 'python3', runner = RUNNER, timeoutMs = RUNNER_TIMEOUT_MS }) {
  if (!existsSync(runner)) return fail(`the runner is missing at ${runner}`, { kind: 'env' });
  const env = { PATH: process.env.PATH ?? '', LANG: 'C.UTF-8', TMPDIR: tmp, TEMP: tmp, TMP: tmp };
  const run = await runBounded(python, ['-I', '-B', runner, '--kind', kind, '--parse-only', dir], { env, timeoutMs, cwd: tmp });
  return run.ok ? outcomeOf(run, timeoutMs) : fail(`python3 could not be started (${run.reason})`, { kind: 'env' });
}
