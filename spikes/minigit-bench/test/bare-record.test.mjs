// The records (E3b.S1 step 9): every field is known, not captured with a reason, or n/a (bare); cells and pairs carry values or bounds, with and without unpaired cells.
import assert from 'node:assert/strict';
import path from 'node:path';
import { test } from 'node:test';
import { crosscheckPolicyFrom } from '../capture/policy-input.mjs';
import { aliasRecord } from '../lib/aliases.mjs';
import { bareOutcome } from '../lib/bare-outcome.mjs';
import { MEASURES, NOT_BARE, cellOf, pairRecord, ratioOf, sessionRecord } from '../lib/bare-record.mjs';
import { countLoc, isTestPath } from '../lib/loc.mjs';
import { sessionCost } from '../lib/session-cost.mjs';
import { loadSmoke } from '../lib/smoke-load.mjs';
import { smokeViews } from '../lib/smoke-verdicts.mjs';
import { spikeDir } from './helpers.mjs';

const FIXTURE = path.join(spikeDir, 'test', 'fixtures', 'real', 'claude-2.1.310-bare-smoke');
const SCORE = {
  build: { ok: true }, visible: [{ script: 'test-v1.sh', passed: 10, failed: 0, expectedTotal: 10, summaryCaptured: true, buildFailed: false, exitCode: 0, timedOut: false, signal: null }],
  heldout: { known: true, primary100: false, totals: { total: 29, passed: 28, failed: 1, blocked: 0, percent: 96.6 }, defects: 1, latency: { known: false, why: 'no latency runs' } },
  upstream: [{ script: 'upstream/test-v1.sh', passed: 9, expectedTotal: 10 }],
};

function smokeRecord(over = {}) {
  const loaded = loadSmoke(FIXTURE);
  assert.equal(loaded.ok, true, loaded.reason);
  const ctx = crosscheckPolicyFrom(spikeDir, {});
  assert.equal(ctx.ok, true, ctx.reason);
  const views = smokeViews(loaded.value, ctx.value);
  const cost = sessionCost({ E: views.E, O: views.O, T: views.T, table: ctx.value.table });
  const alias = aliasRecord({ events: views.events });
  const outcome = bareOutcome({
    meta: loaded.value.meta, events: views.events, brokerLines: loaded.value.brokerLines, aliasCheck: { verdict: 'ok', reasons: [] }, scan: { hits: 0 },
    contamination: { high: [], canary: [], tokenHits: [] }, handVerdicts: [], retired: false, abortReason: null, recomputeUsd: cost.known ? cost.usd : null,
  });
  return sessionRecord({
    ids: { pair: 1, stack: 'csharp', phase: 'v1', attempt: 1, rid: 'abc123', kind: 'data', label: 'paired' }, outcome,
    run: { mask: '0-5', maskObserved: true, spawnStampMs: 1, startSkewMs: 30, quiet: { quiet: true }, spin: { medianMs: 20 }, harnessIdentity: 'a'.repeat(64), preregTag: 'prereg/minigit-bare-20261030', cliVersion: '2.1.310' },
    views, cost, aliasRecord: alias, aliasCheck: { verdict: 'ok' }, score: SCORE, loc: countLoc([{ path: 'Program.cs', text: 'a\nb\n' }]), packages: { packages: [], added: [], flagged: [] }, meta: loaded.value.meta, ...over,
  });
}

/** Every leaf under the record's measured sections: known / not captured (with a reason) / n/a (bare). Returns the paths that are none of those. */
function badLeaves(node, at = '') {
  if (typeof node === 'string') return node === NOT_BARE ? [] : [`${at}: a bare string '${node}'`];
  if (node === null || typeof node !== 'object' || Array.isArray(node)) return [`${at}: a bare value`];
  if (node.known === true) return 'value' in node ? [] : [`${at}: known without a value`];
  if (node.known === false) return typeof node.why === 'string' && node.why !== '' ? [] : [`${at}: unknown without a reason`];
  return Object.entries(node).flatMap(([k, v]) => badLeaves(v, `${at}.${k}`));
}

test('The_session_record_has_every_field_or_says_why', () => {
  const r = smokeRecord();
  for (const section of ['run', 'cost', 'time', 'quality', 'process']) assert.deepEqual(badLeaves(r[section], section), []);
  for (const leaf of ['loc', 'egress', 'packages']) assert.deepEqual(badLeaves(r[leaf], leaf), []);
  assert.equal(r.cost.cliTotalUsd.known, true);
  assert.equal(r.cost.brokerUsd.known, true);
  assert.equal(r.cost.stopRuleUsd.known, true);
  assert.equal(r.time.wallMs.known, true);
  assert.equal(r.quality.heldout.value.percent, 96.6);
  assert.equal(r.quality.visible.value[0].hundredPercent, true);
  assert.equal(r.process.compliance, NOT_BARE);
  assert.deepEqual(Object.keys(r.cost.checks.value), ['t1', 't2', 't3', 't4', 't5'], 'every cross-check is in the record');
  assert.equal(r.class, 'completed');
});

test('A_session_that_was_not_scored_says_so_with_a_reason_and_a_missing_view_is_not_zero', () => {
  const r = smokeRecord({ score: null, packages: null, loc: null, aliasRecord: { ok: false, reason: 'two ids' } });
  assert.deepEqual(badLeaves(r.quality, 'quality'), []);
  assert.equal(r.quality.buildOk.known, false);
  assert.match(r.quality.buildOk.why, /not scored/);
  assert.equal(r.packages.known, false);
  assert.equal(r.cost.aliases.known, false);
});

test('The_turns_are_checked_against_assistant_messages', () => {
  const r = smokeRecord();
  assert.equal(typeof r.time.turns.value.agree, 'boolean');
  assert.ok(r.time.turns.value.assistantMessages > 0);
});

// ---- cells and pairs ------------------------------------------------------------------------------------------------------------

const rec = (phase, costUsd, { cls = 'completed', label = 'paired', wall = 1000, api = 400, active = 700 } = {}) => ({
  ids: { phase, label }, class: cls,
  cost: { stopRuleUsd: { known: true, value: costUsd }, headlineUsd: { known: true, value: { usd: costUsd } } },
  time: { wallMs: { known: true, value: wall }, modelApiMs: { known: true, value: api }, activeMinusModelApiMs: { known: true, value: active - api } },
});

test('A_censored_side_makes_the_pair_ratio_a_bound', () => {
  assert.deepEqual(ratioOf({ known: true, value: 6.3, bound: '>=' }, { known: true, value: 3, bound: null }), { known: true, value: 2.1, bound: '>=' });
  assert.deepEqual(ratioOf({ known: true, value: 6, bound: null }, { known: true, value: 3, bound: '>=' }), { known: true, value: 2, bound: '<=' });
  assert.equal(ratioOf({ known: true, value: 6, bound: '>=' }, { known: true, value: 3, bound: '>=' }).known, false, 'two lower bounds say nothing about their ratio');
  const cs = cellOf(rec('v1', 3.15, { cls: 'killed: cap' }), rec('v2', 3.15));
  assert.deepEqual([cs.costUsd.value, cs.costUsd.bound], [6.3, '>=']);
  const ts = cellOf(rec('v1', 1.5), rec('v2', 1.5));
  assert.deepEqual([ts.costUsd.value, ts.costUsd.bound], [3, null]);
});

test('A_cell_with_a_missing_phase_is_missing_and_every_measure_says_why', () => {
  const c = cellOf(rec('v1', 1), null);
  assert.equal(c.missing, true);
  for (const m of MEASURES) assert.equal(c[m].known, false);
});

test('Ratios_are_given_with_and_without_unpaired_cells', () => {
  const cs = cellOf(rec('v1', 3, { label: 'ran unpaired' }), rec('v2', 3, { label: 'ran unpaired' }));
  const ts = cellOf(rec('v1', 1), rec('v2', 1));
  assert.equal(cs.unpaired, true);
  const p = pairRecord({ pair: 1, cells: { csharp: cs, node: ts }, masks: { csharp: '0-5', node: '8-13' }, spawnStamps: {}, skewsMs: {}, barrierAtMs: 5, labels: ['ran unpaired'], quality: {} });
  assert.deepEqual(p.ratios.costUsd.with, { known: true, value: 3, bound: null });
  assert.equal(p.ratios.costUsd.without.known, false);
  assert.match(p.ratios.costUsd.without.why, /ran unpaired/);
  const paired = pairRecord({ pair: 2, cells: { csharp: cellOf(rec('v1', 3), rec('v2', 3)), node: ts }, masks: {}, spawnStamps: {}, skewsMs: {}, barrierAtMs: 5, labels: [], quality: {} });
  assert.deepEqual(paired.ratios.wallMs.without, paired.ratios.wallMs.with, 'with no unpaired cell the two sets are equal');
});

test('The_loc_count_separates_product_from_test_by_path_and_says_it_is_a_heuristic', () => {
  assert.equal(isTestPath('src/Foo.Tests/FooTests.cs'), true);
  assert.equal(isTestPath('tests/a.ts'), true);
  assert.equal(isTestPath('src/a.test.ts'), true);
  assert.equal(isTestPath('src/protest.ts'), false);
  const c = countLoc([{ path: 'src/a.ts', text: '1\n2\n3\n' }, { path: 'src/a.test.ts', text: '1\n2\n' }, { path: 'test-v1.sh', text: 'x\n' }, { path: 'SPEC-v1.txt', text: 'long\nlong\n' }, { path: 'obj/x.cs', text: '1\n' }]);
  assert.deepEqual([c.product, c.test], [3, 2], 'the pinned test script and the spec are not source');
});
