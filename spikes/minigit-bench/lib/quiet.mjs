// The quiet check (E3b.S2): REFUSE to start a phase on a loaded machine, and give up resumably. Pure over an injected sampler, clock and sleep.
//   quietWindow   one 60 s window of 5 s samples -> a `quietVerdict` (cpu-sampler.mjs)
//   waitForQuiet  re-check every 60 s up to 30 min; not quiet by then -> the caller exits 3 (resumable) with the last verdict
//   noisyOverride `--accept-noisy <reason>` starts anyway with the flag `started not quiet`, never for a v2 phase whose v1 was quiet
import { calibrateSpin, quietVerdict } from './cpu-sampler.mjs';
import { fail, ok } from './result.mjs';

const NUMERIC = Object.freeze(['windowMs', 'intervalMs', 'meanCpuMax', 'sampleCpuMax', 'foreignCpuMax', 'memAvailMinPct', 'spinMaxRatio', 'spinIterations', 'spinCalibrationRuns', 'recheckMs', 'maxWaitMs']);

const positive = v => typeof v === 'number' && Number.isFinite(v) && v > 0;
const hasPatterns = patterns => Array.isArray(patterns) && patterns.length > 0;

/** `inputs/quiet.json` -> `ok(thresholds)` | `fail` naming the first field that is not what it must be. */
export function loadQuiet(json) {
  const bad = NUMERIC.find(k => !positive(json?.[k]));
  if (bad) return fail(`quiet.json: '${bad}' must be a positive number (the numeric fields are ${NUMERIC.join(', ')})`);
  if (!hasPatterns(json.agentPatterns)) return fail('quiet.json: agentPatterns must list at least one agent pattern');
  return ok(Object.freeze({ ...json }));
}

/** Sample every `intervalMs` for `windowMs` (the interval is slept BETWEEN samples) -> the window's `quietVerdict`. `thresholds.calibratedMs` is the idle spin median. */
export async function quietWindow({ thresholds, sampleOnce, sleep, windowMs = thresholds.windowMs, intervalMs = thresholds.intervalMs }) {
  const count = Math.floor(windowMs / intervalMs);
  const samples = [];
  for (let i = 0; i < count; i += 1) {
    if (i > 0) await sleep(intervalMs);
    samples.push(await sampleOnce());
  }
  return quietVerdict(samples, thresholds);
}

/** How the wait ends after a check, or null to go on: cancelled, quiet, or out of time. */
function waitEnd({ verdict, waitedMs, signal, maxWaitMs }) {
  if (signal?.aborted) return { quiet: false, aborted: true, waitedMs, verdict };
  if (verdict.quiet) return { quiet: true, waitedMs, verdict };
  return waitedMs >= maxWaitMs ? { quiet: false, gaveUp: true, waitedMs, verdict } : null;
}

/**
 * Check, and re-check after `recheckMs`, until quiet or `maxWaitMs` is spent. `check()` runs one window and resolves to its verdict.
 * `onRetry({ attempt, verdict, waitedMs, nextCheckInMs })` is called before each sleep, so a long wait is never silent (review G4).
 * A `signal` that aborts ends the wait at once: `{ quiet: false, aborted: true, ... }` (cancelled; never read as quiet or as gave-up; review M7).
 * -> `{ quiet: true, waitedMs, verdict }` or `{ quiet: false, gaveUp: true, waitedMs, verdict }` (the last verdict, naming what failed).
 */
export async function waitForQuiet({ check, maxWaitMs, recheckMs, now, sleep, onRetry = () => {}, signal }) {
  const start = now();
  let attempt = 0;
  for (;;) {
    const verdict = await check();
    const waitedMs = now() - start;
    const end = waitEnd({ verdict, waitedMs, signal, maxWaitMs });
    if (end) return end;
    attempt += 1;
    onRetry({ attempt, verdict, waitedMs, nextCheckInMs: recheckMs });
    await sleep(recheckMs);
  }
}

const blank = reason => typeof reason !== 'string' || reason.trim() === '';
const secondHalfOfQuietCell = (phase, v1Quiet) => phase === 'v2' && v1Quiet === true;

/** `--accept-noisy <reason>` -> `ok({ flags: ['started not quiet'], reason })`; refused without a reason and for a v2 phase whose v1 started quiet. */
export function noisyOverride({ phase, v1Quiet, reason }) {
  if (blank(reason)) return fail('--accept-noisy needs a reason');
  if (secondHalfOfQuietCell(phase, v1Quiet)) return fail('--accept-noisy is refused for a v2 phase whose v1 was quiet: the cell would hold one quiet and one loaded phase');
  return ok({ flags: ['started not quiet'], reason: reason.trim() });
}

/**
 * `init`'s spin calibration (E3b.S2 step 4): ONE window checked on conditions (a) to (d) only (the spin condition is what is being calibrated), and the probes run only when it is quiet:
 * a baseline taken on a loaded machine would make every later "quiet" verdict wrong. -> `ok({ calibration, window })` | `fail(reason, { verdict })`.
 */
export async function calibrateIfQuiet({ thresholds, sampleOnce, sleep, probe }) {
  const window = await quietWindow({ thresholds: { ...thresholds, spinMaxRatio: Infinity, calibratedMs: 1 }, sampleOnce, sleep });
  if (!window.quiet) return fail(`the machine is not quiet, so the spin probe is not calibrated: ${window.failures.map(f => `${f.condition} (${f.detail})`).join('; ')}`, { verdict: window });
  const calibration = await calibrateSpin({ runs: thresholds.spinCalibrationRuns, probe });
  return ok({ calibration, window });
}
