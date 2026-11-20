// The bare loop's test rig, shared by `bare-series.test.mjs` and `explore-seams.test.mjs` (extracted, never copied): a run context over fakes, a fake launch that lays the bare-smoke fixture into each session's
// directory (and variants of it), a fake scorer, and the run-map of a new series. Not a test file.
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { crosscheckPolicyFrom } from '../capture/policy-input.mjs';
import { aliasRecord } from '../lib/aliases.mjs';
import { openSeries } from '../lib/run-map.mjs';
import { FIXTURE, SCORE, lay } from './bare-fixture.mjs';
import { SEED, context, sleep } from './bare-ctx.mjs';
import { spikeDir } from './helpers.mjs';

/** The policy BEHAVIOUR tests run under an explicit policy (t3 investigate), never under the registered file, which the T3 commit set to t3 = stop on purpose (`crosscheck.test.mjs` pins it). */
export const POLICY = Object.freeze({ t1: 'stop', t2: 'otel-incomplete', t3: 'investigate', t5: 'investigate', t3OutputSource: 'transcript', join: 'request_id' });

export function harness(over = {}) {
  const base = context();
  const parsed = crosscheckPolicyFrom(spikeDir, {});
  assert.equal(parsed.ok, true, parsed.reason);
  const events = [];
  const variants = over.variants ?? {};
  const launched = [];
  const scoredWith = [];
  const ctx = {
    ...base, kind: 'data', pair: 1, seed: SEED, runMapFile: path.join(base.seriesDir, 'run-map.json'), ledgerFile: path.join(base.seriesDir, 'ledger.ndjson'), ceilingUsd: 500, bareBudgetUsd: 40,
    caps: { capUsd: 6, wallMs: 3_600_000, stallMs: 2_700_000 }, table: parsed.value.table, policy: parsed.value.policy, handVerdictsFor: () => [], snapshotPackages: [], packageRules: [],
    harnessIdentity: 'a'.repeat(64), preregTag: 'prereg/minigit-bare-20261030', abortReason: () => 'SIGINT', abortExit: () => 130, spin: { medianMs: 20 }, ...over.ctx,
  };
  ctx.aliasBaseline = (() => { const lay0 = { files: {} }; void lay0; const rec = aliasRecord({ events: readFileSync(path.join(FIXTURE, 'stream.ndjson'), 'utf8').split('\n').filter(Boolean).map(l => JSON.parse(l)) }); return { aliases: rec.value.aliases, internal: rec.value.internal }; })();
  const deps = {
    guard: async arg => { events.push(`guard paid=${arg.paid}`); return over.guard ? over.guard(arg) : { ok: true }; },
    retireStale: async () => { events.push('retire'); },
    preflight: async () => ({ ok: true }),
    quiet: async arg => { events.push(`quiet ${arg.phase}`); return over.quiet ? over.quiet(arg) : { ok: true, value: { quiet: true, flags: [] } }; },
    startSampler: () => ({ stop: async () => ({ samples: 3, flags: over.samplerFlags ?? [], error: null }) }),
    loadFlag: async () => false, now: Date.now,
    scoreSession: async arg => { events.push(`score ${path.basename(arg.session)}`); scoredWith.push(arg.dotnetPin); return over.score ? over.score(arg) : { kind: 'scored', score: SCORE }; },
    phase: {
      maskCheckMs: 1, procApi: { pids: () => [], read: () => '' }, listProjectDirs: () => [path.join(ctx.home, '.claude', 'projects', 'p1')], spawn: () => ({}),
      launchSession: async (plan, d, opts) => {
        d.spawnBox(['bwrap'], {});
        const rid = plan.layout.rid;
        events.push(`launch ${plan.layout.rid}`);
        launched.push(rid);
        lay(plan.layout, variants[rid]);
        await sleep(5);
        const meta = JSON.parse(readFileSync(plan.layout.files.meta, 'utf8'));
        return { ok: true, value: { meta: opts?.signal?.aborted ? { ...meta, stopped: 'cancelled' } : meta, exit: { code: 0, signal: null } } };
      },
    },
  };
  const map = over.map ?? openSeries({ seriesId: 's-x', seedSha256: 'b'.repeat(64), harnessIdentity: ctx.harnessIdentity, preregTag: ctx.preregTag, preregCommit: 'c'.repeat(40), pins: {}, stacksSha256: 'd', pricesSha256: 'e', snapshots: {}, caps: ctx.caps, bareBudgetUsd: 40, ceilingUsd: 500 });
  return { ctx, deps, map, events, launched, scoredWith };
}
