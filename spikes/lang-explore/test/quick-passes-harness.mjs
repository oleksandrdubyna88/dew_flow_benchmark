// The fakes the pass driver's tests share (sections 2.2, 2.7, 2.8, 2.11, 2.12): a series folder with a real series lock, a session's `score/` folder, a fake build, a fake case folder and a fake pass child that answers by a plan. No box,
// no layer, no process: `scorePasses` takes `prepare`, `cases`, `build`, `runPass`, `affinity`, `seriesState` and `growth` as seams. The invented ids are `s01`, `h001`, `h002`.
import '../../minigit-bench/test/fake-home.mjs';
import { mkdirSync, readFileSync, readdirSync, statSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { acquireSeriesLock } from '../../minigit-bench/lib/series-lock.mjs';
import { ok } from '../../minigit-bench/lib/result.mjs';
import { tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { LIMIT_BYTES, MIB, PINNED, runnerRecord, sortedIds } from './quick-passes-fixture.mjs';
import { rec } from './quick-score-fixture.mjs';

export const LIMITS = Object.freeze({ memoryBytes: LIMIT_BYTES, buildTimeoutMs: 600_000, timeMs: 300_000 });
export const SPEC = Object.freeze({ caseWallMs: 660_000, holdSeconds: 720, runCeilingMs: 7_200_000 });
const REASON = Object.freeze({ 'wrong answer': 'entry 3 of 4 differs', 'memory limit': 'memory limit: oom_kill 1' });

/** The runner's record for a plan entry `{ failing: { id: verdict word }, peak: { id: bytes } }` (an empty entry: every case passes). */
export function recordFor(entry = {}) {
  const cases = sortedIds().map(id => (entry.failing?.[id] ? { ...rec(id, 'FAIL', REASON[entry.failing[id]], entry.peak?.[id]), ...(entry.evidence ? { firstMismatch: entry.evidence } : {}) } : rec(id, 'PASS', '', entry.peak?.[id])));
  return runnerRecord(sortedIds(), { cases });
}

/** The body a fake pass child answers for a plan entry: 'clean', 'env', or `{ failing, peak, evidence }`. */
export function bodyFor(entry, spec) {
  if (entry === 'env') return { kind: 'environment', reason: 'the box did not start', observedCpus: null };
  return { kind: 'completed', record: recordFor(entry === 'clean' ? {} : entry), runner: PINNED.version, memoryCertified: true, observedCpus: spec.cpus };
}

/** Every file under `dir` with its bytes: `{ 'passes/p01-2cpu-a1.json': '...' }`. */
export function snapshotOfDir(dir, rel = '') {
  return Object.fromEntries(readdirSync(path.join(dir, rel)).sort().flatMap(name => {
    const r = rel === '' ? name : `${rel}/${name}`;
    return statSync(path.join(dir, r)).isDirectory() ? Object.entries(snapshotOfDir(dir, r)) : [[r, readFileSync(path.join(dir, r), 'utf8')]];
  }));
}

/** A fake clock that stands still unless `advance(ms)` moves it (the fake pass child does, so an attempt's `ms` is exact). */
export function fakeClock(start = '2026-11-07T10:00:00.000Z') {
  let t = Date.parse(start);
  return { now: () => new Date(t), advance: ms => { t += ms; } };
}

/**
 * The whole fake environment of one session's scoring. `over.plan(n, attempt)` -> 'clean' | 'env' | `{ failing, peak, evidence }`; `over.a` replaces any seam; `over.holdSeries` leaves the series lock untaken.
 * -> `{ a, calls, work, seriesDir, clock, release }`; `a` is `scorePasses`'s argument.
 */
export function harness(over = {}) {
  const seriesDir = tmpDir('quick-passes-series');
  const work = path.join(seriesDir, 's', 'abc123', 'score');
  mkdirSync(work, { recursive: true });
  const lock = over.holdSeries === false ? null : acquireSeriesLock({ seriesDir });
  const clock = fakeClock();
  const calls = { prepare: [], cases: [], build: [], pass: [] };
  const plan = over.plan ?? (() => 'clean');
  const a = {
    work, seriesDir, limits: LIMITS, spec: SPEC, traps: [], pinnedRunner: PINNED, now: clock.now, say: () => {},
    prepare: async dest => { calls.prepare.push(dest); mkdirSync(path.join(dest, 'src'), { recursive: true }); writeFileSync(path.join(dest, 'src', 'main.txt'), 'invented program'); return ok({ frozen: [], drift: [], left: [] }); },
    cases: ({ dir }) => { calls.cases.push(dir); mkdirSync(dir, { recursive: true }); return ok({ dir, ids: sortedIds() }); },
    build: async ({ tree }) => { calls.build.push(tree); writeFileSync(path.join(tree, 'built.bin'), 'out'); return ok({ ok: true, exit: 0, timedOut: false, wallMs: 1234, tail: '' }); },
    runPass: async spec => { calls.pass.push(spec); clock.advance(1500); return bodyFor(plan(spec.n, spec.attempt), spec); },
    affinity: { probe: async cpus => ok(cpus) },
    seriesState: () => ({ launching: [] }),
    growth: { loaded: () => ok({}) },
    ...over.a,
  };
  return { a, calls, work, seriesDir, clock, release: () => lock?.value.release(), MIB };
}
