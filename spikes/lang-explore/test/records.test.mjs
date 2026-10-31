// The records (plan D-X9, D-X10, T-X7, T-X8): every v1 metric is in the session record beside the `explore` section (removing one fails the test), the build/test time is read from the audit log, the pair record
// carries the single-phase cells and their ratios, and CPC is the summed spend over the number accepted. The session record is v1's own, built over the committed real smoke fixture.
import assert from 'node:assert/strict';
import { readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { crosscheckPolicyFrom } from '../../minigit-bench/capture/policy-input.mjs';
import { aliasRecord } from '../../minigit-bench/lib/aliases.mjs';
import { bareOutcome } from '../../minigit-bench/lib/bare-outcome.mjs';
import { leafProblems, sessionRecord } from '../../minigit-bench/lib/bare-record.mjs';
import { sessionCost } from '../../minigit-bench/lib/session-cost.mjs';
import { loadSmoke } from '../../minigit-bench/lib/smoke-load.mjs';
import { smokeViews } from '../../minigit-bench/lib/smoke-verdicts.mjs';
import { spikeDir, tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { REQUIRED_PATHS, SCORED_PATHS, buildTestTime, cellOfSession, cpcOf, exploreSection, finalOf, finalizeSessionRecord, floorBreaches, isBuildTestCommand, missingPaths, pairRecordOf } from '../lib/records.mjs';
import { acceptedOf, scoreShape } from '../lib/score.mjs';

const FIXTURE = path.join(spikeDir, 'test', 'fixtures', 'real', 'claude-2.1.310-bare-smoke');
const MIB = 1024 * 1024;

const c = (set, id, verdict, over = {}) => ({ set, id, verdict, wallMs: 100, peakBytes: 50 * MIB, events: { oom_kill: 0 }, exit: 0, ...over });
/** A pilot score: 3 visible and 5 held-out cases, h003 and h005 the traps. */
const resultOf = (failing = []) => ({
  build: { ok: true, wallMs: 4321 }, limits: { timeMs: 2000, memoryBytes: 512 * MIB },
  cases: [...['s01', 's02', 's03'].map(id => c('visible', id, 'pass')), ...['h001', 'h002', 'h003', 'h004', 'h005'].map(id => c('heldout', id, failing.includes(id) ? 'wrong answer' : 'pass'))],
});
const scoreOf = (failing = []) => scoreShape({ result: resultOf(failing), traps: ['h003', 'h005'] });

/** A v1 session record over the smoke fixture with the pilot's score (the same call `writeSessionRecord` makes). */
function recordOf({ score = scoreOf(), label = 'paired', over = {} } = {}) {
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

const at = (o, dotted) => dotted.split('.').reduce((x, k) => (x === undefined || x === null ? undefined : x[k]), o);
const without = (record, dotted) => {
  const copy = structuredClone(record);
  const keys = dotted.split('.');
  delete keys.slice(0, -1).reduce((o, k) => o[k], copy)[keys.at(-1)];
  return copy;
};

test('The_session_record_has_every_v1_metric_beside_the_explore_section_and_every_leaf_is_known_or_says_why', () => {
  const rec = { ...recordOf(), explore: exploreSection({ score: scoreOf(), audit: [] }) };
  assert.deepEqual(missingPaths(rec), []);
  for (const section of ['run', 'cost', 'time', 'quality', 'explore']) assert.deepEqual(leafProblems(rec[section], section), [], section);
  assert.deepEqual(Object.keys(rec.cost.tokens.value[Object.keys(rec.cost.tokens.value)[0]]).sort(), ['cacheRead', 'cacheWrite', 'input', 'output'], 'the token record keeps the cache write split');
  const all = [...REQUIRED_PATHS, ...SCORED_PATHS];
  for (const named of ['cost.cliTotalUsd', 'cost.brokerUsd', 'cost.tokens', 'cost.checks.value.t1', 'cost.checks.value.t5', 'time.wallMs', 'time.modelApiMs', 'time.activeMinusModelApiMs', 'time.partition', 'quality.heldout.value.percent', 'quality.heldout.value.defects', 'class', 'flags']) {
    assert.ok(all.includes(named), `${named} is one of the D34 metrics the list must hold`);
  }
  for (const p of [...REQUIRED_PATHS, ...SCORED_PATHS]) {
    assert.notEqual(at(rec, p), undefined, `${p} is in the record`);
    assert.ok(missingPaths(without(rec, p)).includes(p), `removing ${p} is detected`);
  }
  assert.deepEqual(missingPaths({ ...recordOf(), explore: exploreSection({ score: null, audit: [] }) }, false), [], 'a session that was not scored needs the shorter list');
  assert.ok(missingPaths({ ...recordOf(), explore: exploreSection({ score: null, audit: [] }) }, true).includes('explore.passRate'));
});

test('The_explore_section_counts_what_the_judge_found_and_says_why_for_a_session_that_was_not_scored', () => {
  const e = exploreSection({ score: scoreOf(['h003']), audit: [] });
  assert.deepEqual(e.passRate.value, { visible: 100, heldout: 80, visibleCases: 3, heldoutCases: 5 });
  assert.deepEqual(e.verdicts.value, { visible: { pass: 3 }, heldout: { pass: 4, 'wrong answer': 1 } });
  assert.deepEqual(e.trapHits.value, { hits: 1, trapCases: 2 });
  assert.equal(e.accepted.value, false);
  assert.equal(e.buildMs.value, 4321);
  assert.equal(e.cases.value.length, 8);
  assert.equal(exploreSection({ score: scoreOf(), audit: [] }).accepted.value, true);
  const none = exploreSection({ score: null, audit: [] });
  assert.equal(none.scored.known, false);
  assert.match(none.scored.why, /not scored/);
  assert.equal(none.passRate, undefined);
  assert.equal(exploreSection({ score: scoreOf(), audit: null }).buildTest.known, false, 'an unread audit log is unknown, not zero build time');
});

test('Build_and_test_time_is_the_summed_span_of_the_Bash_calls_that_build_or_run_the_program', () => {
  const ev = (event, id, ts, extra = {}) => ({ event, tool_use_id: id, ts: new Date(Date.parse('2026-11-01T10:00:00Z') + ts).toISOString(), tool_name: 'Bash', input: { command: 'ls' }, ...extra });
  const audit = [
    ev('PreToolUse', 'a', 0, { input: { command: 'bash build.sh' } }), ev('PostToolUse', 'a', 4000),
    ev('PreToolUse', 'b', 5000, { input: { command: 'cd x && python3 -I runner/run_samples.py samples/' } }), ev('PostToolUseFailure', 'b', 5500),
    ev('PreToolUse', 'c', 6000, { input: { command: 'cat src/Program.cs' } }), ev('PostToolUse', 'c', 6100),
    ev('PreToolUse', 'd', 7000, { input: { command: 'npx tsc --noEmit' } }),
    ev('PreToolUse', 'e', 8000, { tool_name: 'Read', input: { file_path: 'build.sh' } }), ev('PostToolUse', 'e', 8100, { tool_name: 'Read' }),
  ];
  assert.deepEqual(buildTestTime(audit), { ms: 4500, calls: 2, unpaired: 1 }, 'a call with no end is counted, not summed; a file read is not a build');
  assert.deepEqual(['bash build.sh', 'cd w && bash run.sh < x', './run.sh', 'dotnet build src/App.csproj', 'tsc --noEmit', 'python3 -I runner/run_samples.py samples/'].map(isBuildTestCommand), [true, true, true, true, true, true]);
  assert.deepEqual(['cat build.sh', 'grep tscript x', 'bash rebuild.shx', 'ls', 'echo run.sh is frozen'].map(isBuildTestCommand), [false, false, false, false, false]);
  assert.deepEqual(buildTestTime([]), { ms: 0, calls: 0, unpaired: 0 });
});

test('Passing_runs_that_would_have_failed_the_specs_own_floors_are_counted', () => {
  const cases = [c('heldout', 'a', 'pass', { wallMs: 1500 }), c('heldout', 'b', 'pass', { peakBytes: 300 * MIB }), c('heldout', 'c', 'wrong answer', { wallMs: 5000 }), c('heldout', 'd', 'pass')];
  assert.deepEqual(floorBreaches(cases), { time: 1, memory: 1, passing: 3 });
});

test('The_record_on_disk_gains_the_explore_section_once_and_keeps_every_v1_leaf', () => {
  const dir = tmpDir('rec');
  const v1 = recordOf();
  writeFileSync(path.join(dir, 'record.json'), JSON.stringify(v1));
  const first = finalizeSessionRecord({ sessionDir: dir, score: scoreOf(), audit: [] });
  const again = finalizeSessionRecord({ sessionDir: dir, score: scoreOf(), audit: [] });
  assert.deepEqual(first, again, 'idempotent');
  const disk = JSON.parse(readFileSync(path.join(dir, 'record.json'), 'utf8'));
  const { explore, ...rest } = disk;
  assert.deepEqual(rest, JSON.parse(JSON.stringify(v1)), 'nothing of v1 is changed');
  assert.ok(explore);
  assert.equal(disk.explore.passRate.known, true);
});

const entry = (stack, attempt, over = {}) => ({ rid: `${stack}${attempt}`, stack, attempt, mask: stack === 'csharp' ? '0-5' : '8-13', label: 'paired', class: 'completed', data: true, run: { spawnStampMs: 1000, startSkewMs: 12, flags: ['x'] }, ...over });

test('A_pair_record_has_a_cell_per_stack_the_ratios_with_and_without_unpaired_cells_and_the_masks_and_skews', () => {
  const records = { csharp1: recordOf(), node1: recordOf() };
  const r = pairRecordOf({ pair: 1, entries: [entry('csharp', 1), entry('node', 1)], recordOf: rid => records[rid] });
  assert.equal(r.cells.csharp.missing, false);
  assert.equal(r.cells.csharp.costUsd.known, true);
  assert.deepEqual(Object.keys(r.ratios), ['costUsd', 'wallMs', 'modelApiMs', 'activeMinusModelApiMs']);
  assert.equal(r.ratios.costUsd.with.value, 1, 'two equal sessions: C# over TypeScript is 1');
  assert.deepEqual(r.masks, { csharp1: '0-5', node1: '8-13' });
  assert.deepEqual(r.skewsMs, { csharp1: 12, node1: 12 });
  assert.deepEqual(r.flags, ['x']);
  const unpaired = pairRecordOf({ pair: 1, entries: [entry('csharp', 1, { class: 'void: harness', data: false }), entry('csharp', 2, { label: 'ran unpaired' }), entry('node', 1)], recordOf: rid => (rid === 'csharp2' ? recordOf({ label: 'ran unpaired' }) : records[rid] ?? recordOf()) });
  assert.equal(unpaired.cells.csharp.unpaired, true);
  assert.equal(unpaired.ratios.costUsd.without.known, false, 'a ratio without the unpaired cell is unknown when a cell is unpaired');
  assert.equal(unpaired.ratios.costUsd.with.known, true);
  assert.equal(cellOfSession(null).missing, true);
  const killed = { ...recordOf(), class: 'killed: cap' };
  assert.equal(cellOfSession(killed).costUsd.bound, '>=', 'a session cut short is a lower bound');
  assert.equal(cellOfSession(recordOf()).costUsd.bound, null);
  assert.equal(finalOf([entry('csharp', 1, { data: false }), entry('csharp', 2)]).attempt, 2);
  assert.equal(finalOf([entry('csharp', 1), entry('csharp', 2, { data: false })]).attempt, 1, 'the latest attempt WITH data counts');
});

test('CPC_is_the_summed_spend_over_the_accepted_count_with_voids_in_the_numerator_and_a_cap_kill_a_lower_bound', () => {
  const rec = (usd, cls = 'completed') => ({ ...recordOf(), class: cls, cost: { ...recordOf().cost, headlineUsd: { known: true, value: { usd } }, stopRuleUsd: { known: true, value: usd } } });
  const r = cpcOf([{ record: rec(0.2), accepted: true }, { record: rec(0.4), accepted: true }, { record: rec(0.3, 'void: upstream'), accepted: false }]);
  assert.deepEqual([r.sessions, r.accepted, r.spendUsd.value, r.cpc.value], [3, 2, 0.9, 0.45]);
  const none = cpcOf([{ record: rec(0.2), accepted: false }, { record: rec(0.3), accepted: false }]);
  assert.deepEqual([none.cpc.value, none.cpc.text, none.cpc.spendUsd], [null, 'no correct solution', 0.5]);
  const capped = cpcOf([{ record: rec(50, 'killed: cap'), accepted: false }, { record: rec(0.2), accepted: true }]);
  assert.equal(capped.spendUsd.value, 50.2, 'a killed: cap session counts in the numerator');
  assert.equal(capped.spendUsd.bound, '>=');
  assert.equal(capped.cpc.bound, '>=');
  const unknownCost = { ...recordOf(), cost: { ...recordOf().cost, headlineUsd: { known: false, why: 'x' }, stopRuleUsd: { known: false, why: 'x' } } };
  const u = cpcOf([{ record: unknownCost, accepted: true }]);
  assert.equal(u.cpc.known, false, 'an unknown cost is not 0');
  assert.equal(acceptedOf(scoreOf()), true);
});
