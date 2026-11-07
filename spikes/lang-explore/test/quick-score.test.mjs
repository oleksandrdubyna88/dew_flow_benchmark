// The quick-run scoring where a harness failure must not become a verdict, and where a rejected case must keep what it saw. The build and the run are fakes (`buildFn`, `runFn`, `inBox`); nothing starts.
import '../../minigit-bench/test/fake-home.mjs';
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { mkdirSync, readFileSync, readdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import net from 'node:net';
import { ok } from '../../minigit-bench/lib/result.mjs';
import { HOLD_MARGIN_S } from '../../lang-bench/lib/exec-layer/prefix.mjs';
import { BUILD_MARKER, boxBuild } from '../lib/build-quick.mjs';
import { acceptedOf, scoreShape } from '../lib/score.mjs';
import { layerRun, scoreQuick } from '../lib/score-quick.mjs';
import { MIB, built, frozenFiles, ran, rec, score, scoreWith, scoringArea } from './quick-score-fixture.mjs';


test('A_case_whose_peak_memory_was_not_read_is_a_harness_failure_and_not_a_not_accepted_verdict', async t => {
  const r = await score(t, { runFn: ran([rec('s01'), rec('h001', 'PASS', '', null), rec('h002')]) });
  assert.equal(r.ok, false);
  assert.equal(r.kind, 'environment');
  assert.match(r.reason, /read no peak memory for case\(s\) h001/);
});

test('A_case_the_memory_limit_killed_keeps_its_reading_and_stays_a_memory_limit_verdict', async t => {
  const r = await score(t, { runFn: ran([rec('s01'), rec('h001', 'FAIL', 'memory limit: oom_kill 1', 512 * MIB), rec('h002')]) });
  assert.equal(r.ok, true, r.reason);
  assert.equal(r.value.explore.cases.find(c => c.id === 'h001').verdict, 'memory limit');
});

test('A_failing_case_keeps_the_evidence_the_runner_recorded_in_the_score', async t => {
  const evidence = { expected: 'recv c1 "b"', group: false, omitted: 0, observed: [{ kind: 'recv', conn: 'c1', text: 'recv c1 "x"', data: '"x"', stepLine: 4, stepText: 'invented step' }] };
  const failing = { ...rec('h002', 'FAIL', 'entry 3 of 4 differs'), firstMismatch: { entry: 3, of: 4, part: '', detail: 'first difference at byte 0', evidence } };
  const r = await score(t, { runFn: ran([rec('s01'), rec('h001'), failing]) });
  assert.equal(r.ok, true, r.reason);
  assert.deepEqual(r.value.explore.cases.find(c => c.id === 'h002').firstMismatch.evidence, evidence);
  assert.equal(r.value.explore.cases.find(c => c.id === 'h001').firstMismatch, null);
});

test('Evidence_changes_no_verdict_the_same_failing_case_without_it_scores_the_same', async t => {
  const plain = { ...rec('h002', 'FAIL', 'entry 3 of 4 differs'), firstMismatch: { entry: 3, of: 4, part: '', detail: 'd' } };
  const withEvidence = { ...plain, firstMismatch: { ...plain.firstMismatch, evidence: { expected: 'x', group: true, omitted: 0, observed: [] } } };
  const a = await score(t, { runFn: ran([rec('s01'), rec('h001'), plain]) });
  const b = await score(t, { runFn: ran([rec('s01'), rec('h001'), withEvidence]) });
  const verdicts = r => [acceptedOf(r.value), r.value.heldout, r.value.explore.verdicts, r.value.explore.passRate];
  assert.deepEqual(verdicts(b), verdicts(a));
});

test('A_held_out_folder_that_is_gone_or_empty_at_scoring_time_is_a_void_never_a_visible_only_score', async t => {
  const area = scoringArea(t);
  const profile = { paths: { dotnetDir: '/d', nodeDir: '/n', tsDir: '/t' }, snapshots: {}, caseWallMs: 1, runCeilingMs: 1 };
  const empty = path.join(area.work, 'empty-held');
  mkdirSync(empty);
  let runs = 0;
  for (const sets of [[], [{ name: 'heldout', dir: path.join(area.work, 'nowhere') }], [{ name: 'heldout', dir: empty }]]) {
    const r = await scoreQuick({
      stack: 'node', source: area.source, files: frozenFiles(), sets, limits: { memoryBytes: 512 * MIB, buildTimeoutMs: 1, timeMs: 1 }, work: area.work, profile, buildFn: built(), runFn: async () => { runs += 1; return {}; },
    });
    assert.equal(r.ok, false);
    assert.equal(r.kind, 'environment');
    assert.match(r.reason, /held-out/);
  }
  assert.equal(runs, 0);
});

test('The_score_shape_never_calls_a_score_with_no_held_out_case_primary_100_whatever_feeds_it', () => {
  const visibleOnly = scoreShape({ result: { build: { ok: true }, limits: {}, cases: [{ id: 's01', set: 'visible', verdict: 'pass', peakBytes: 1, wallMs: 1 }] } });
  assert.equal(visibleOnly.heldout.primary100, false);
  assert.equal(acceptedOf(visibleOnly), false);
});

test('A_socket_the_agents_daemon_left_in_its_tree_is_left_out_of_the_copy_and_named_and_the_session_is_still_scored', async t => {
  const a = scoringArea(t);
  const server = net.createServer();
  await new Promise(resolve => { server.listen(path.join(a.source, 'daemon.sock'), resolve); });
  try {
    const r = await scoreWith(a, { runFn: ran(['s01', 'h001', 'h002'].map(id => rec(id))) });
    assert.equal(r.ok, true, r.reason);
    assert.deepEqual(r.value.explore.nonRegularLeftOut, ['daemon.sock']);
    assert.equal(acceptedOf(r.value), true, 'a socket is no reason to refuse the tree');
  } finally {
    server.close();
  }
});

test('A_score_of_a_tree_with_only_regular_files_and_links_has_no_left_out_key', async t => {
  const r = await score(t, { runFn: ran(['s01', 'h001', 'h002'].map(id => rec(id))) });
  assert.equal('nonRegularLeftOut' in r.value.explore, false);
  assert.equal('harnessDrift' in r.value.explore, false);
});

test('Anything_thrown_while_scoring_is_a_void_and_never_an_uncaught_crash', async t => {
  for (const [name, seams] of [['the build', { buildFn: async () => { throw new Error('boom in the build'); } }], ['the run', { runFn: async () => { throw new TypeError('boom in the run'); } }]]) {
    const r = await score(t, seams);
    assert.equal(r.ok, false, name);
    assert.equal(r.kind, 'environment', name);
    assert.match(r.reason, /unexpected .*boom in the/, name);
  }
});

test('Under_a_moved_runner_a_changed_runner_file_is_harness_drift_and_not_an_agent_change_and_other_files_stay_the_agents', async t => {
  const a = scoringArea(t);
  mkdirSync(path.join(a.source, 'runner'), { recursive: true });
  writeFileSync(path.join(a.source, 'runner', 'rs_old.py'), 'the 0.3.1 bytes\n');
  writeFileSync(path.join(a.source, 'build.sh'), '#!/bin/bash\nexit 0\n', { mode: 0o755 });
  const files = { ...frozenFiles(), 'runner/rs_old.py': { bytes: Buffer.from('the 0.3.2 bytes\n'), mode: 0o644 } };
  const common = { runFn: ran(['s01', 'h001', 'h002'].map(id => rec(id))), files };
  // the blanket rule (every change under runner/ is the harness's when the runner moved) is gone: the file is judged by its bytes against the runner the series was made with (item 5 of the final seam review of F1)
  const history = ({ rel }) => (rel === 'runner/rs_old.py' ? { known: true, sha256: createHash('sha256').update('the 0.3.1 bytes\n').digest('hex') } : { known: true, sha256: 'f'.repeat(64) });
  const drifted = await scoreWith(a, { ...common, driftPossible: true, history });
  assert.deepEqual(drifted.value.explore.harnessDrift, [{ path: 'runner/rs_old.py', state: 'changed' }]);
  assert.deepEqual(drifted.value.explore.frozenChanges, [{ path: 'build.sh', state: 'changed' }], 'the agent really changed build.sh');
  const plain = await scoreWith(a, common);
  assert.deepEqual(plain.value.explore.frozenChanges.map(c => c.path).sort(), ['build.sh', 'runner/rs_old.py']);
  assert.equal('harnessDrift' in plain.value.explore, false);
});

test('A_build_that_never_began_in_the_box_is_a_void_through_the_whole_scoring_and_nothing_is_run', async t => {
  let runs = 0;
  const snapshots = { csharp: { dir: '/c/cs', record: {} }, node: { dir: '/c/node', record: {} } };
  const inBox = answer => async () => ok({ code: 1, signal: null, timedOut: false, stdout: '', stderr: answer, ms: 5 });
  const r = await score(t, { buildFn: boxBuild({ owned: {}, dirs: {}, snapshots, stack: 'node', inBox: inBox('bwrap: Creating new namespace failed\n') }), runFn: async () => { runs += 1; return {}; } });
  assert.equal(r.ok, false);
  assert.equal(r.kind, 'environment');
  assert.equal(runs, 0);
  const real = await score(t, { buildFn: boxBuild({ owned: {}, dirs: {}, snapshots, stack: 'node', inBox: inBox(`${BUILD_MARKER}\nerror CS1002\n`) }), runFn: async () => { runs += 1; return {}; } });
  assert.equal(real.ok, true, real.reason);
  assert.equal(real.value.build.ok, false, 'a marked build that failed is the submission');
  assert.equal(runs, 0);
});

test('Every_exit_of_the_scoring_that_is_the_harness_failing_is_a_void_and_the_run_is_not_started_after_a_void_build', async t => {
  const environment = (r, pattern) => { assert.equal(r.ok, false); assert.equal(r.kind, 'environment', r.reason); assert.match(r.reason, pattern); };
  let runs = 0;
  const counting = async () => { runs += 1; return ran([rec('s01'), rec('h001'), rec('h002')])(); };
  environment(await score(t, { runFn: async () => ({ exitCode: 2, stdout: '', stderr: 'bad usage', record: { cases: [] } }) }), /failed \(exit 2\)/);
  environment(await score(t, { runFn: async () => ({ exitCode: 0, stdout: '', stderr: 'it wrote nothing', record: null }) }), /wrote no record/);
  environment(await score(t, { runFn: async () => ({ exitCode: 1, halt: 'layer-identity-mismatch', stderr: 'not the pinned layer', record: null }) }), /layer halted: layer-identity-mismatch/);
  environment(await score(t, { runFn: async () => ({ exitCode: 0, stdout: '', stderr: '', record: { cases: 'not a list' } }) }), /wrote no case list/);
  environment(await score(t, { runFn: ran([rec('s01'), rec('h001'), rec('h001')]) }), /does not hold exactly the expected cases/);
  const snapshots = { csharp: { dir: '/c/cs', record: {} }, node: { dir: '/c/node', record: {} } };
  environment(await score(t, { buildFn: boxBuild({ owned: {}, dirs: {}, snapshots, stack: 'node', inBox: async () => ok({ code: null, signal: 'SIGKILL', timedOut: false, stdout: '', stderr: '', ms: 1 }) }), runFn: counting }), /ended from outside/);
  assert.equal(runs, 0, 'a void build starts no run');
});

test('A_held_out_case_with_an_id_the_runner_would_refuse_is_a_void_before_anything_is_built', async t => {
  const area = scoringArea(t);
  writeFileSync(path.join(area.held, 'x9.in'), 'i');
  writeFileSync(path.join(area.held, 'x9.out'), 'o');
  let builds = 0;
  const profile = { paths: { dotnetDir: '/d', nodeDir: '/n', tsDir: '/t' }, snapshots: {}, caseWallMs: 1, runCeilingMs: 1 };
  const r = await scoreQuick({
    stack: 'node', source: area.source, files: frozenFiles(), sets: [{ name: 'heldout', dir: area.held }], limits: { memoryBytes: 512 * MIB, buildTimeoutMs: 1, timeMs: 1 }, work: area.work, profile,
    buildFn: async () => { builds += 1; return built()(); }, runFn: ran([]),
  });
  assert.equal(r.ok, false);
  assert.equal(r.kind, 'environment');
  assert.equal(builds, 0);
});

test('A_quick_case_may_run_to_the_test_bound_so_the_scope_holder_is_held_for_the_case_wall_and_not_for_T_plus_G', async () => {
  let seen = null;
  const limits = { memoryBytes: 512 * MIB, buildTimeoutMs: 600_000, timeMs: 300_000 };
  const run = layerRun({ owned: {}, dirs: {}, stack: 'node', kind: 'daemon', limits, caseWallMs: 660_000, runCeilingMs: 7_200_000, parent: '/p', runCasesFn: async options => { seen = options; return {}; } });
  await run({ tree: '/t', samplesDir: '/s', caseIds: ['s01'] });
  assert.equal(seen.holdSeconds, 660 + HOLD_MARGIN_S);
  assert.equal(seen.caseWallMs, 660_000);
  assert.equal(seen.kind, 'daemon');
});

test('The_score_states_the_time_bound_that_applies_and_that_the_recorded_time_limit_is_not_enforced', async t => {
  const r = await score(t, { runFn: ran(['s01', 'h001', 'h002'].map(id => rec(id))) });
  assert.deepEqual(r.value.explore.enforced, { memoryBytes: 512 * MIB, buildTimeoutMs: 600_000, caseTestBoundMs: 600_000, timeMsEnforced: false });
  assert.equal(r.value.explore.limits.timeMs, 300_000, 'the recorded limit is not changed');
});

test('A_replay_names_one_case_and_the_case_folder_holds_that_pair_alone_with_its_own_manifest_line', async t => {
  const a = scoringArea(t);
  const files = frozenFiles();
  const only = await import('../lib/score-quick.mjs');
  const dir = path.join(a.work, 'one');
  const held = only.combinedCases({ files, heldoutDir: a.held, dir, only: 'h002' });
  assert.deepEqual([held.value.visible, held.value.heldout], [[], ['h002']]);
  assert.deepEqual(readdirSync(dir).sort(), ['cases.txt', 'h002.in', 'h002.out']);
  assert.equal(readFileSync(path.join(dir, 'cases.txt'), 'utf8'), 'h002 bytes R2\n', 'the manifest keeps the comparison of that case only');
  const vis = only.combinedCases({ files, heldoutDir: a.held, dir: path.join(a.work, 'two'), only: 's01' });
  assert.deepEqual([vis.value.visible, vis.value.heldout], [['s01'], []]);
  assert.deepEqual(readdirSync(path.join(a.work, 'two')).sort(), ['s01.in', 's01.out']);
});

test('A_scoring_of_one_case_needs_no_other_case_and_a_case_that_is_not_there_is_a_void', async t => {
  const one = await scoreWith(scoringArea(t), { runFn: ran([rec('s01')]), only: 's01' });
  assert.equal(one.ok, true, one.reason);
  assert.deepEqual(one.value.explore.cases.map(c => c.id), ['s01']);
  const none = await scoreWith(scoringArea(t), { runFn: ran([]), only: 'h777' });
  assert.equal(none.ok, false);
  assert.equal(none.kind, 'environment');
  assert.match(none.reason, /not among the visible samples or the held-out cases/);
});

test('A_scratch_folder_that_cannot_be_made_is_a_void_and_never_a_throw', async t => {
  const a = scoringArea(t);
  const blocked = path.join(a.work, 'a-file-where-a-folder-should-be');
  writeFileSync(blocked, 'x');
  const r = await scoreWith({ ...a, work: blocked }, { runFn: ran(['s01', 'h001', 'h002'].map(id => rec(id))) });
  assert.equal(r.ok, false);
  assert.equal(r.kind, 'environment');
  assert.match(r.reason, /unexpected/);
});

test('A_cleanup_that_fails_after_a_good_score_keeps_the_score_and_flags_the_cleanup', async t => {
  const calls = [];
  const cleanFn = dir => { calls.push(dir); if (calls.length > 1) throw new Error('cannot remove the scratch folder'); };
  const r = await scoreWith(scoringArea(t), { runFn: ran(['s01', 'h001', 'h002'].map(id => rec(id))), cleanFn });
  assert.equal(r.ok, true, r.reason);
  assert.equal(acceptedOf(r.value), true, 'the score is whole');
  assert.match(r.value.explore.cleanupFailed, /cannot remove the scratch folder/);
  assert.equal(calls.length, 2);
});

test('A_cleanup_that_fails_after_a_void_keeps_the_void_and_says_so', async t => {
  const calls = [];
  const cleanFn = () => { calls.push(1); if (calls.length > 1) throw new Error('cannot remove the scratch folder'); };
  const r = await scoreWith(scoringArea(t), { runFn: async () => { throw new TypeError('boom in the run'); }, cleanFn });
  assert.equal(r.ok, false);
  assert.equal(r.kind, 'environment');
  assert.match(r.reason, /boom in the run.*cannot remove the scratch folder/);
});

test('A_score_with_a_cleanup_that_works_has_no_cleanup_flag', async t => {
  const r = await score(t, { runFn: ran(['s01', 'h001', 'h002'].map(id => rec(id))) });
  assert.equal('cleanupFailed' in r.value.explore, false);
});
