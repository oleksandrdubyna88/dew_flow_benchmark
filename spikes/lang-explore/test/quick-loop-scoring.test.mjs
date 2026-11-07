// P11 and P10 through the REAL pieces (review item 6): v1's loop drives sessions, its scoring step is `scoreSessionDep`, which runs the pass driver over fake seams. A session whose passes give `rejected` (or `intermittently
// incorrect`) is `scored` and final, with data, and is never voided; one whose passes are exhausted by the environment is the void the series re-runs. The launch is the loop's own fake (the bare-smoke fixture); no model, no box.
import '../../minigit-bench/test/fake-home.mjs';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { crosscheckPolicyFrom } from '../../minigit-bench/capture/policy-input.mjs';
import { aliasRecord } from '../../minigit-bench/lib/aliases.mjs';
import { ridOf } from '../../minigit-bench/lib/bare-plan.mjs';
import { EXIT, runLoop } from '../../minigit-bench/lib/bare-series.mjs';
import { readLedger } from '../../minigit-bench/lib/ledger.mjs';
import { openSeries, readRunMap } from '../../minigit-bench/lib/run-map.mjs';
import { acquireSeriesLock } from '../../minigit-bench/lib/series-lock.mjs';
import { SEED, context, sleep } from '../../minigit-bench/test/bare-ctx.mjs';
import { FIXTURE, lay } from '../../minigit-bench/test/bare-fixture.mjs';
import { spikeDir } from '../../minigit-bench/test/helpers.mjs';
import { scoreSessionDep } from '../lib/run-ctx.mjs';
import { VOID_UNSCORED } from '../lib/series.mjs';
import { harness } from './quick-passes-harness.mjs';

const DRIVER_ONLY = ['work', 'seriesDir', 'pinnedRunner', 'now', 'signal'];
const seamsOf = h => Object.fromEntries(Object.entries(h.a).filter(([k]) => !DRIVER_ONLY.includes(k)));

/** The loop's context and edges (the shape of `bare-series.test.mjs`'s harness) with the scoring step replaced by the pass driver over `plan`. */
function loopOver(plan) {
  const base = context();
  const parsed = crosscheckPolicyFrom(spikeDir, {});
  assert.equal(parsed.ok, true, parsed.reason);
  const fixtureEvents = readFileSync(path.join(FIXTURE, 'stream.ndjson'), 'utf8').split('\n').filter(Boolean).map(l => JSON.parse(l));
  const baseline = aliasRecord({ events: fixtureEvents }).value;
  const ctx = {
    ...base, kind: 'data', pair: 1, seed: SEED, runMapFile: path.join(base.seriesDir, 'run-map.json'), ledgerFile: path.join(base.seriesDir, 'ledger.ndjson'), ceilingUsd: 500, bareBudgetUsd: 40,
    caps: { capUsd: 6, wallMs: 3_600_000, stallMs: 2_700_000 }, table: parsed.value.table, policy: { t1: 'stop', t2: 'otel-incomplete', t3: 'investigate', t5: 'investigate', t3OutputSource: 'transcript', join: 'request_id' },
    handVerdictsFor: () => [], snapshotPackages: [], packageRules: [], harnessIdentity: 'a'.repeat(64), preregTag: 'prereg/minigit-bare-20261030', abortReason: () => 'SIGINT', abortExit: () => 130, spin: { medianMs: 20 },
    aliasBaseline: { aliases: baseline.aliases, internal: baseline.internal },
  };
  const passes = harness({ holdSeries: false, plan });
  const dep = scoreSessionDep({ world: { passSeams: () => seamsOf(passes) }, calibration: { limits: passes.a.limits }, heldout: { casesDir: '/x', traps: [] }, home: base.home });
  const deps = {
    guard: async () => ({ ok: true }), retireStale: async () => {}, preflight: async () => ({ ok: true }), quiet: async () => ({ ok: true, value: { quiet: true, flags: [] } }),
    startSampler: () => ({ stop: async () => ({ samples: 3, flags: [], error: null }) }), loadFlag: async () => false, now: Date.now, scoreSession: dep,
    phase: {
      maskCheckMs: 1, procApi: { pids: () => [], read: () => '' }, listProjectDirs: () => [path.join(ctx.home, '.claude', 'projects', 'p1')], spawn: () => ({}),
      launchSession: async (p, d, opts) => {
        d.spawnBox(['bwrap'], {});
        lay(p.layout);
        await sleep(5);
        const meta = JSON.parse(readFileSync(p.layout.files.meta, 'utf8'));
        return { ok: true, value: { meta: opts?.signal?.aborted ? { ...meta, stopped: 'cancelled' } : meta, exit: { code: 0, signal: null } } };
      },
    },
  };
  const map = openSeries({ seriesId: 's-x', seedSha256: 'b'.repeat(64), harnessIdentity: ctx.harnessIdentity, preregTag: ctx.preregTag, preregCommit: 'c'.repeat(40), pins: {}, stacksSha256: 'd', pricesSha256: 'e', snapshots: {}, caps: ctx.caps, bareBudgetUsd: 40, ceilingUsd: 500 });
  return { ctx, deps, map, passes };
}

async function run(plan) {
  const t = loopOver(plan);
  const lock = acquireSeriesLock({ seriesDir: t.ctx.seriesDir });
  assert.equal(lock.ok, true, lock.reason);
  try {
    const r = await runLoop({ ctx: t.ctx, deps: t.deps, map: t.map });
    return { ...t, r, map: readRunMap(t.ctx.runMapFile).value };
  } finally { lock.value.release(); }
}

const scoreOf = (t, rid) => JSON.parse(readFileSync(path.join(t.ctx.sessionsRoot, rid, 'score', 'score.json'), 'utf8'));
const FIRST = ridOf({ seed: SEED, pair: 1, stack: 'csharp', phase: 'v1', attempt: 1 });

test('A_rejected_or_intermittently_incorrect_session_is_scored_and_final_through_the_real_scoring_dep_and_the_loop_and_is_never_voided', async () => {
  for (const [label, plan, verdict] of [['rejected', () => ({ failing: { h001: 'wrong answer' } }), 'rejected'], ['intermittently incorrect', n => (n === 3 ? { failing: { h001: 'wrong answer' } } : 'clean'), 'intermittently incorrect']]) {
    const t = await run(plan);
    assert.equal(t.r.exit, EXIT.done, `${label}: ${t.r.message}`);
    const entry = t.map.sessions[FIRST];
    assert.deepEqual([entry.status, entry.data, entry.class], ['scored', true, 'completed'], label);
    assert.equal(entry.reclassifiedFrom, undefined, `${label}: never voided`);
    assert.equal(scoreOf(t, FIRST).explore.verdict, verdict, label);
    assert.equal(readLedger(t.ctx.ledgerFile).filter(l => l.event === 'unscored').length, 0, label);
    assert.ok(Object.values(t.map.sessions).every(s => s.status === 'scored' && s.data === true), `${label}: every session of the loop is final`);
    assert.ok(t.passes.calls.pass.length >= 40, `${label}: real passes ran for every session`);
  }
});

test('A_session_whose_passes_are_exhausted_by_the_environment_is_the_void_the_series_offers_one_re_run_for_through_the_same_pieces', async () => {
  const t = await run(n => (n === 4 ? 'env' : 'clean'));
  assert.equal(t.r.exit, EXIT.done, t.r.message);
  const entry = t.map.sessions[FIRST];
  assert.deepEqual([entry.status, entry.data, entry.class], ['ran', false, VOID_UNSCORED]);
  assert.equal(entry.reclassifiedFrom.length, 1);
  assert.ok(readLedger(t.ctx.ledgerFile).some(l => l.event === 'unscored' && l.rid === FIRST));
});
