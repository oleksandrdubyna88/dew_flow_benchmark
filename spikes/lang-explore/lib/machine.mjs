// The machine-quiet check, the phase sampler and the spin probe of the pilot: v1's pieces (`lib/quiet.mjs`, `lib/cpu-sampler.mjs`, `inputs/quiet.json`) wired to the pilot's two roots.
// One difference from v1's wiring: the sampler's `liveSessions` lists the live sessions under BOTH `~/rsd/minigit` and `~/rsd/lang-v2/explore`, so a v1 session still running is foreign load for a pilot pair.
import { existsSync, readFileSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { createSampler, liveSessionDirs, spinProbe, startPhaseSampler, calibrateSpin } from '../../minigit-bench/lib/cpu-sampler.mjs';
import { realProcApi } from '../../minigit-bench/lib/janitor.mjs';
import { calibrateIfQuiet, loadQuiet, noisyOverride, quietWindow, waitForQuiet } from '../../minigit-bench/lib/quiet.mjs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { V1, exploreRoot } from './paths.mjs';

const readText = p => (existsSync(p) ? readFileSync(p, 'utf8') : '');
const abortableSleep = (ms, signal) => new Promise(resolve => {
  if (signal?.aborted) { resolve(); return; }
  const timer = setTimeout(done, ms);
  function done() { clearTimeout(timer); signal?.removeEventListener('abort', done); resolve(); }
  signal?.addEventListener('abort', done, { once: true });
});

/** v1's registered quiet thresholds (`inputs/quiet.json`), checked for shape. -> `ok(thresholds)` | `fail`. */
export function pilotQuiet() {
  return loadQuiet(JSON.parse(readFileSync(path.join(V1, 'inputs', 'quiet.json'), 'utf8')));
}

/** The live session directories of both roots. */
export const liveSessionsOf = home => () => [...liveSessionDirs(path.join(home, 'rsd', 'minigit')), ...liveSessionDirs(exploreRoot(home))];

/** The spin probe: each call runs one probe on the next of the spin CPUs (the harness core). */
export function spinOf(thresholds) {
  const cpus = thresholds.spinCpus ?? [6, 7];
  let n = 0;
  return async () => {
    const r = await spinProbe({ iterations: thresholds.spinIterations, cpu: cpus[n++ % cpus.length] });
    if (!r.ok) throw new Error(r.reason);
    return r.value;
  };
}

/** One sampler of this machine; `cellIds` are the cells that count as ours. */
export const samplerFor = ({ thresholds, cellIds = [], home = os.homedir(), procApi = realProcApi, now = Date.now, spin = spinOf(thresholds) }) => createSampler({
  procApi, readText, cellIds, harnessPids: [String(process.pid)], liveSessions: liveSessionsOf(home), spin, now, patterns: thresholds.agentPatterns,
});

/** The spin probe's calibration on a quiet machine (30 runs on CPUs 6-7). -> `ok({ calibration, window })` | `fail(reason)` when the machine is not quiet. */
export async function calibrateSpinIfQuiet({ thresholds, home = os.homedir() }) {
  const probe = spinOf(thresholds);
  const sampleOnce = await samplerFor({ thresholds, home, spin: probe });
  return calibrateIfQuiet({ thresholds, sampleOnce, sleep: ms => new Promise(r => setTimeout(r, ms)), probe: () => probe() });
}

export { calibrateSpin };

const secs = ms => Math.round(ms / 1000);
const failureList = verdict => verdict.failures.map(f => `${f.condition} (${f.detail})`).join('; ');

/** The quiet check ended without a quiet machine: refused, or started anyway under `--accept-noisy <reason>` with the flag `started not quiet`. */
function notQuiet({ waited, acceptNoisy }) {
  if (!acceptNoisy) return fail(`the machine was not quiet for ${Math.round(waited.waitedMs / 60000)} min: ${failureList(waited.verdict)}`, { verdict: waited.verdict });
  const o = noisyOverride({ phase: 'v1', v1Quiet: null, reason: acceptNoisy });
  return o.ok ? ok({ quiet: false, flags: o.value.flags, reason: o.value.reason, failures: waited.verdict.failures }) : o;
}

/**
 * The machine of a run: `quiet({ cellIds, say, signal })` waits for a quiet 60 s window (re-check every 60 s for 30 min, saying each verdict), `startSampler({ pair, cellIds })` writes the phase's CPU
 * file and flags (never kills), `loadFlag({ cellIds })` says whether the machine is loaded now. `calibratedMs` is the idle spin median `prepare` wrote.
 */
export function machineFor({ thresholds, calibratedMs, seriesDir, acceptNoisy = null, home = os.homedir(), procApi = realProcApi, now = Date.now }) {
  const t = { ...thresholds, calibratedMs };
  const build = cellIds => samplerFor({ thresholds, cellIds, home, procApi, now });
  async function quiet({ cellIds = [], say = () => {}, signal }) {
    const sampler = await build(cellIds);
    const sleep = ms => abortableSleep(ms, signal);
    const onRetry = ({ verdict, waitedMs, nextCheckInMs }) => say(`the machine is not quiet (${failureList(verdict)}); waited ${secs(waitedMs)} s of ${secs(thresholds.maxWaitMs)} s; next check in ${secs(nextCheckInMs)} s`);
    const waited = await waitForQuiet({ check: () => quietWindow({ thresholds: t, sampleOnce: sampler, sleep }), maxWaitMs: thresholds.maxWaitMs, recheckMs: thresholds.recheckMs, now, sleep, onRetry, signal });
    if (waited.aborted) return fail('cancelled while waiting for a quiet machine');
    return waited.quiet ? ok({ quiet: true, flags: [], numbers: waited.verdict.numbers, waitedMs: waited.waitedMs }) : notQuiet({ waited, acceptNoisy });
  }
  const startSampler = ({ pair, cellIds = [] }) => startPhaseSampler({
    sample: async () => (await build(cellIds))(), file: path.join(seriesDir, 'pairs', `p${pair}-cpu.ndjson`), intervalMs: thresholds.intervalMs, thresholds: t, sleep: ms => new Promise(r => setTimeout(r, ms)),
  });
  const loadFlag = async ({ cellIds = [] }) => !(await quietWindow({ thresholds: t, sampleOnce: await build(cellIds), sleep: ms => new Promise(r => setTimeout(r, ms)), windowMs: thresholds.intervalMs * 2 })).quiet;
  return { quiet, startSampler, loadFlag };
}
