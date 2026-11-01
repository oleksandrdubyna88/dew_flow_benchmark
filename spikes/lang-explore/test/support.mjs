// Shared builders of the pilot's tests: a v1 session record over the committed real smoke fixture (the same call `writeSessionRecord` makes) and a pilot score.
import assert from 'node:assert/strict';
import path from 'node:path';
import { crosscheckPolicyFrom } from '../../minigit-bench/capture/policy-input.mjs';
import { aliasRecord } from '../../minigit-bench/lib/aliases.mjs';
import { bareOutcome } from '../../minigit-bench/lib/bare-outcome.mjs';
import { sessionRecord } from '../../minigit-bench/lib/bare-record.mjs';
import { sessionCost } from '../../minigit-bench/lib/session-cost.mjs';
import { loadSmoke } from '../../minigit-bench/lib/smoke-load.mjs';
import { smokeViews } from '../../minigit-bench/lib/smoke-verdicts.mjs';
import { spikeDir } from '../../minigit-bench/test/helpers.mjs';
import { scoreShape } from '../lib/score.mjs';

export const FIXTURE = path.join(spikeDir, 'test', 'fixtures', 'real', 'claude-2.1.310-bare-smoke');
export const MIB = 1024 * 1024;

export const c = (set, id, verdict, over = {}) => ({ set, id, verdict, wallMs: 100, peakBytes: 50 * MIB, events: { oom_kill: 0 }, exit: 0, ...over });
/** A pilot score: 3 visible and 5 held-out cases, k003 and k005 the traps. */
export const resultOf = (failing = []) => ({
  build: { ok: true, wallMs: 4321 }, limits: { timeMs: 2000, memoryBytes: 512 * MIB },
  cases: [...['s01', 's02', 's03'].map(id => c('visible', id, failing.includes(id) ? 'wrong answer' : 'pass')), ...['k001', 'k002', 'k003', 'k004', 'k005'].map(id => c('heldout', id, failing.includes(id) ? 'wrong answer' : 'pass'))],
});
export const scoreOf = (failing = []) => scoreShape({ result: resultOf(failing), traps: ['k003', 'k005'] });

/** A v1 session record over the smoke fixture with the pilot's score (the same call `writeSessionRecord` makes). */
export function recordOf({ score = scoreOf(), label = 'paired', over = {} } = {}) {
  const loaded = loadSmoke(FIXTURE);
  assert.equal(loaded.ok, true, loaded.reason);
  const ctx = crosscheckPolicyFrom(spikeDir, {});
  const views = smokeViews(loaded.value, ctx.value);
  const cost = sessionCost({ E: views.E, O: views.O, T: views.T, table: ctx.value.table });
  const outcome = bareOutcome({
    meta: loaded.value.meta, events: views.events, brokerLines: loaded.value.brokerLines, aliasCheck: { verdict: 'ok', reasons: [] }, scan: { hits: 0 },
    contamination: { high: [], canary: [], tokenHits: [] }, handVerdicts: [], retired: false, abortReason: null, recomputeUsd: cost.known ? cost.usd : null,
  });
  return sessionRecord({
    ids: { pair: 1, stack: 'csharp', phase: 'v1', attempt: 1, rid: 'abc123', kind: 'pilot', label }, outcome,
    run: { mask: '0-5', maskObserved: true, spawnStampMs: 1, startSkewMs: 30, quiet: { quiet: true }, spin: { medianMs: 20 }, harnessIdentity: 'a'.repeat(64), preregTag: null, cliVersion: '2.1.310' },
    views, cost, aliasRecord: aliasRecord({ events: views.events }), aliasCheck: { verdict: 'ok' }, score, loc: null, packages: { packages: [], added: [], flagged: [] }, meta: loaded.value.meta, ...over,
  });
}

