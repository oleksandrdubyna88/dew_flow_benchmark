// The pass driver (section 2.2, with the driver halves of 2.1, 2.7 and 2.11): a session is built once and run ten times, five on each CPU profile; an environment attempt is re-run whole at most twice; nothing is ever overwritten;
// a resume runs only what is missing; `verdict.json` is the derived snapshot; a stop ends the scoring between two passes. The build, the case folder, the pass child, the affinity probe and the series state are fakes.
import assert from 'node:assert/strict';
import { existsSync, readFileSync, readdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { MIB } from './quick-passes-fixture.mjs';
import { ok } from '../../minigit-bench/lib/result.mjs';
import { scorePasses } from '../lib/passes.mjs';
import { VOID_UNSCORED } from '../lib/series.mjs';
import { harness, snapshotOfDir } from './quick-passes-harness.mjs';

const files = (work, dir) => readdirSync(path.join(work, dir)).sort();
const verdictFile = work => JSON.parse(readFileSync(path.join(work, 'verdict.json'), 'utf8'));
const ODD = [1, 3, 5, 7, 9];
const EVEN = [2, 4, 6, 8, 10];

test('A_session_is_built_once_and_then_run_ten_times_five_on_the_2_CPU_profile_and_five_on_the_8_CPU_profile', async () => {
  const h = harness();
  const r = await scorePasses(h.a);
  h.release();
  assert.equal(r.kind, 'scored', r.reason);
  assert.equal(h.calls.build.length, 1);
  assert.equal(h.calls.pass.length, 10);
  assert.deepEqual(h.calls.pass.map(c => c.cpus), [1, 2, 3, 4, 5, 6, 7, 8, 9, 10].map(n => (n % 2 === 1 ? '6-7' : '0-7')));
  assert.deepEqual(files(h.work, 'passes'), [...ODD.map(n => `p0${n}-2cpu-a1.json`), ...EVEN.map(n => (n === 10 ? 'p10-8cpu-a1.json' : `p0${n}-8cpu-a1.json`))].sort());
  assert.equal(r.verdict.verdict, 'accepted');
});

test('A_failed_build_is_rejected_with_no_pass_run_and_every_case_build_failed_through_the_driver', async () => {
  const h = harness({ a: { build: async () => ok({ ok: false, exit: 1, timedOut: false, wallMs: 5, tail: 'error CS1002' }) } });
  const r = await scorePasses(h.a);
  h.release();
  assert.equal(r.kind, 'scored', r.reason);
  assert.equal(r.verdict.verdict, 'rejected');
  assert.equal(h.calls.pass.length, 0, 'the pass seam counts 0 calls');
  assert.ok(r.score.explore.cases.length === 3 && r.score.explore.cases.every(c => c.verdict === 'build failed'));
  assert.equal(r.score.build.ok, false);
  assert.equal(existsSync(path.join(h.work, 'tree')), false, 'the built copy is removed once the state leaves open');
});

test('Every_pass_runs_every_case_on_the_same_built_copy_with_the_same_ids_and_deadlines', async () => {
  const h = harness();
  await scorePasses(h.a);
  h.release();
  const shape = c => JSON.stringify([c.tree, c.caseIds, c.caseWallMs, c.holdSeconds, c.runCeilingMs, c.limits, c.samplesDir]);
  assert.equal(new Set(h.calls.pass.map(shape)).size, 1);
  assert.equal(h.calls.pass[0].tree, path.join(h.work, 'tree'));
  assert.deepEqual(h.calls.pass[0].caseIds, ['h001', 'h002', 's01']);
  assert.equal(h.calls.pass[0].caseWallMs, 660_000);
});

test('An_environment_attempt_is_re_run_whole_at_most_twice_and_each_attempt_is_kept_as_its_own_file', async () => {
  const h = harness({ plan: (n, attempt) => (n === 5 ? 'env' : (n === 2 && attempt < 3 ? 'env' : 'clean')) });
  const r = await scorePasses(h.a);
  h.release();
  const names = files(h.work, 'passes');
  assert.deepEqual(names.filter(f => f.startsWith('p02')), ['p02-8cpu-a1.json', 'p02-8cpu-a2.json', 'p02-8cpu-a3.json']);
  assert.deepEqual(names.filter(f => f.startsWith('p05')), ['p05-2cpu-a1.json', 'p05-2cpu-a2.json', 'p05-2cpu-a3.json']);
  assert.equal(h.calls.pass.filter(c => c.n === 5).length, 3, 'no fourth call');
  assert.equal(r.kind, 'void');
  assert.equal(r.class, VOID_UNSCORED);
  assert.equal(r.verdict.verdict, 'unscored: environment');
  assert.equal(r.verdict.scoringState, 'exhausted');
});

test('No_attempt_file_is_ever_overwritten_or_removed_and_a_second_scoring_of_the_same_session_appends_nothing', async () => {
  const h = harness();
  const first = await scorePasses(h.a);
  const before = snapshotOfDir(h.work);
  const calls = h.calls.pass.length;
  const second = await scorePasses(h.a);
  h.release();
  assert.equal(first.kind, 'scored');
  assert.equal(second.kind, 'scored', second.reason);
  assert.equal(h.calls.pass.length, calls, 'the pass child was called zero times the second time');
  assert.equal(h.calls.build.length, 1);
  assert.deepEqual(snapshotOfDir(h.work), before, 'the directory listing and every file are the same');
  assert.deepEqual(second.score, first.score);
});

test('A_resume_after_an_interruption_reuses_the_completed_passes_and_runs_only_what_is_missing', async () => {
  const ctl = new AbortController();
  const h = harness();
  const inner = h.a.runPass;
  h.a.runPass = async spec => { const body = await inner(spec); if (spec.n === 6) ctl.abort(); return body; };
  const first = await scorePasses({ ...h.a, signal: ctl.signal });
  assert.equal(first.kind, 'stopped');
  assert.equal(files(h.work, 'passes').length, 6);
  h.calls.pass.length = 0;
  h.a.runPass = inner;
  const second = await scorePasses(h.a);
  h.release();
  assert.deepEqual(h.calls.pass.map(c => c.n), [7, 8, 9, 10]);
  assert.equal(second.kind, 'scored', second.reason);
  assert.equal(second.verdict.counts.clean, 10);
  assert.equal(h.calls.build.length, 1, 'the committed build is reused');
});

test('The_verdict_file_names_every_pass_its_profile_its_attempts_the_scoring_time_and_keeps_it_apart_from_the_agents_wall', async () => {
  const h = harness({ plan: (n, attempt) => (n === 3 && attempt === 1 ? 'env' : 'clean') });
  await scorePasses(h.a);
  h.release();
  const v = verdictFile(h.work);
  assert.deepEqual(Object.keys(v).sort(), ['build', 'buildMs', 'counts', 'events', 'passes', 'reason', 'scoringMs', 'scoringState', 'verdict']);
  assert.deepEqual(Object.keys(v).filter(k => /terminal|final|reRun/i.test(k)), []);
  assert.equal(v.passes.length, 10);
  assert.deepEqual(v.passes[2], { n: 3, profile: '2cpu', cpus: '6-7', attempts: [
    { attempt: 1, kind: 'environment', file: 'p03-2cpu-a1.json', startedAt: v.passes[2].attempts[0].startedAt, ms: 1500 },
    { attempt: 2, kind: 'completed', file: 'p03-2cpu-a2.json', startedAt: v.passes[2].attempts[1].startedAt, ms: 1500 }] });
  assert.equal(v.build.exit, 0);
  assert.equal(v.buildMs, 1234);
  assert.equal(v.scoringMs, 1234 + 11 * 1500, 'the build and every attempt');
  assert.deepEqual(v.events, []);
});

test('A_pass_refuses_to_start_while_a_session_of_the_series_is_launching_and_records_the_halt_without_an_attempt', async () => {
  const h = harness({ a: { seriesState: () => ({ launching: ['zz9999'] }) } });
  const r = await scorePasses(h.a);
  h.release();
  assert.equal(r.kind, 'environment');
  assert.match(r.reason, /zz9999/);
  assert.equal(h.calls.pass.length, 0);
  assert.equal(existsSync(path.join(h.work, 'passes')), false, 'no pass file is written');
  const events = files(h.work, 'events');
  assert.equal(events.length, 1);
  assert.match(events[0], /-halt\.json$/);
  assert.deepEqual(verdictFile(h.work).events.map(e => e.file), events, 'the rebuilt snapshot lists it');
});

test('The_rollup_score_keeps_the_records_meaning_each_case_carries_its_worst_verdict_and_largest_peak_over_the_completed_passes_and_accepted_equals_the_verdict', async () => {
  const evidence = { entry: 3, of: 4, part: '', detail: 'd', evidence: { expected: 'recv c1 "b"', group: false, omitted: 0, observed: [] } };
  const h = harness({ plan: n => (n === 4 ? { failing: { h001: 'wrong answer' }, evidence } : (n === 7 ? { peak: { h001: 400 * MIB } } : 'clean')) });
  const r = await scorePasses(h.a);
  h.release();
  assert.equal(r.kind, 'scored', r.reason);
  const kase = r.score.explore.cases.find(c => c.id === 'h001');
  assert.equal(kase.verdict, 'wrong answer');
  assert.equal(kase.reason, 'entry 3 of 4 differs');
  assert.deepEqual(kase.firstMismatch, evidence);
  assert.equal(kase.peakBytes, 400 * MIB, 'the largest of the ten');
  assert.equal(kase.memoryMaxBytes, 512 * MIB);
  assert.equal(r.score.explore.verdict, 'intermittently incorrect');
  assert.equal(r.score.explore.scoringState, 'decided');
  assert.equal(r.score.explore.passes, 10);
  assert.equal(r.score.explore.buildTreeSha256, verdictFile(h.work).build.treeSha256);
  assert.equal(r.score.heldout.primary100, false);
  assert.equal(r.score.heldout.defects.count, 1);
  assert.equal(r.score.explore.passRate.heldout, 50);
  assert.deepEqual(JSON.parse(readFileSync(path.join(h.work, 'score.json'), 'utf8')), JSON.parse(JSON.stringify(r.score)), 'score.json is what the record reads');
});

test('A_stop_signal_ends_the_scoring_between_two_passes_keeps_what_was_written_and_the_next_run_resumes_it', async () => {
  const ctl = new AbortController();
  const h = harness();
  const inner = h.a.runPass;
  h.a.runPass = async spec => { const body = await inner(spec); if (spec.n === 3) ctl.abort(); return body; };
  const stopped = await scorePasses({ ...h.a, signal: ctl.signal });
  assert.equal(stopped.kind, 'stopped');
  assert.deepEqual(files(h.work, 'passes'), ['p01-2cpu-a1.json', 'p02-8cpu-a1.json', 'p03-2cpu-a1.json'], 'no partial fourth');
  h.a.runPass = inner;
  h.calls.pass.length = 0;
  const resumed = await scorePasses(h.a);
  h.release();
  assert.equal(resumed.kind, 'scored');
  assert.equal(h.calls.pass[0].n, 4);
});

test('A_pass_child_that_throws_is_an_environment_attempt_and_the_driver_goes_on', async () => {
  let thrown = 0;
  const h = harness();
  const inner = h.a.runPass;
  h.a.runPass = async spec => { if (spec.n === 2 && thrown === 0) { thrown += 1; throw new Error('invented crash'); } return inner(spec); };
  const r = await scorePasses(h.a);
  h.release();
  assert.equal(r.kind, 'scored', r.reason);
  const body = JSON.parse(readFileSync(path.join(h.work, 'passes', 'p02-8cpu-a1.json'), 'utf8'));
  assert.equal(body.kind, 'environment');
  assert.match(body.reason, /invented crash/);
});

test('A_build_record_that_does_not_match_its_tree_is_a_halt_with_an_event_and_no_pass_runs_and_the_attempt_files_are_untouched', async () => {
  const ctl = new AbortController();
  const h = harness();
  const inner = h.a.runPass;
  h.a.runPass = async spec => { const body = await inner(spec); if (spec.n === 2) ctl.abort(); return body; };
  await scorePasses({ ...h.a, signal: ctl.signal });
  const attemptsBefore = snapshotOfDir(path.join(h.work, 'passes'));
  writeFileSync(path.join(h.work, 'tree', 'built.bin'), 'oUt');
  h.calls.pass.length = 0;
  h.a.runPass = inner;
  const r = await scorePasses(h.a);
  h.release();
  assert.equal(r.kind, 'environment');
  assert.match(r.reason, /^build-record-mismatch: /);
  assert.equal(h.calls.pass.length, 0);
  assert.deepEqual(snapshotOfDir(path.join(h.work, 'passes')), attemptsBefore);
  assert.ok(files(h.work, 'events').some(f => f.endsWith('-build-record-mismatch.json')));
});

test('A_build_that_never_begins_is_tried_three_times_commits_nothing_and_leaves_the_session_unscored_environment', async () => {
  const h = harness({ a: { build: async () => ({ ok: false, reason: 'the build did not begin (exit 1)', kind: 'environment' }) } });
  const r = await scorePasses(h.a);
  h.release();
  assert.equal(r.kind, 'void');
  assert.equal(r.verdict.verdict, 'unscored: environment');
  assert.equal(h.calls.pass.length, 0);
  assert.equal(existsSync(path.join(h.work, 'build.json')), false);
  assert.equal(existsSync(path.join(h.work, 'tree')), false);
  assert.equal(files(h.work, 'events').filter(f => /-build-environment(-\d+)?\.json$/.test(f)).length, 3);
});

test('A_halt_answered_by_a_pass_child_that_could_not_apply_its_mask_is_an_event_and_no_attempt', async () => {
  const h = harness({ a: { runPass: async () => ({ halt: 'affinity', kind: 'environment', reason: 'affinity-not-applied: wanted 6-7, observed 6' }) } });
  const r = await scorePasses(h.a);
  h.release();
  assert.equal(r.kind, 'environment');
  assert.match(r.reason, /^affinity-not-applied: /);
  assert.equal(existsSync(path.join(h.work, 'passes')), false);
  assert.ok(files(h.work, 'events').some(f => f.endsWith('-affinity.json')));
  assert.ok(verdictFile(h.work).events.some(e => e.kind === 'affinity'));
});

test('A_pre_pass_probe_that_reads_another_mask_is_the_same_halt_before_the_child_starts', async () => {
  const h = harness({ a: { affinity: { probe: async () => ok('0-4') } } });
  const r = await scorePasses(h.a);
  h.release();
  assert.equal(r.kind, 'environment');
  assert.match(r.reason, /^affinity-not-applied: wanted 6-7, observed 0-4/);
  assert.equal(h.calls.pass.length, 0);
});

// ---- the runner pin of the build (review item 1) ---------------------------------------------------------------------------------------------------

const MOVED = Object.freeze({ format: 1, version: '9.9.9' });
const stopAfter = (h, n, ctl) => { const inner = h.a.runPass; h.a.runPass = async spec => { const body = await inner(spec); if (spec.n === n) ctl.abort(); return body; }; return inner; };

test('A_runner_re_pinned_after_a_functional_failure_halts_the_resume_as_runner_moved_and_the_stored_failure_is_not_re_judged', async () => {
  const ctl = new AbortController();
  const h = harness({ plan: n => (n === 1 ? { failing: { h001: 'wrong answer' } } : 'clean') });
  const inner = stopAfter(h, 2, ctl);
  assert.equal((await scorePasses({ ...h.a, signal: ctl.signal })).kind, 'stopped');
  h.a.runPass = inner;
  h.calls.pass.length = 0;
  const before = snapshotOfDir(path.join(h.work, 'passes'));
  const moved = await scorePasses({ ...h.a, pinnedRunner: MOVED });
  assert.equal(moved.kind, 'environment');
  assert.match(moved.reason, /^runner-moved: .*version 9\.9\.9/);
  assert.equal(h.calls.pass.length, 0, 'no pass runs under another runner');
  assert.deepEqual(snapshotOfDir(path.join(h.work, 'passes')), before);
  assert.ok(files(h.work, 'events').some(f => f.endsWith('-runner-moved.json')));
  const snap = verdictFile(h.work);
  assert.equal(snap.passes[0].attempts[0].kind, 'completed', 'the stored failing attempt is still a completed attempt');
  assert.equal(snap.counts.failing, 1);
  const resumed = await scorePasses(h.a);
  h.release();
  assert.equal(resumed.kind, 'scored', resumed.reason);
  assert.equal(resumed.verdict.verdict, 'intermittently incorrect', 'under the pin of the commit the failure stands');
  assert.equal(JSON.parse(readFileSync(path.join(h.work, 'build.json'), 'utf8')).runnerPin.version, h.a.pinnedRunner.version);
});

test('A_decided_session_entered_again_after_a_re_pin_finishes_from_its_records_and_never_reopens_a_pass_or_halts_on_the_removed_tree', async () => {
  const h = harness({ plan: n => (n === 3 ? { failing: { h002: 'wrong answer' } } : 'clean') });
  const first = await scorePasses(h.a);
  assert.equal(first.verdict.verdict, 'intermittently incorrect');
  assert.equal(existsSync(path.join(h.work, 'tree')), false);
  h.calls.pass.length = 0;
  const again = await scorePasses({ ...h.a, pinnedRunner: MOVED });
  h.release();
  assert.equal(again.kind, 'scored', again.reason);
  assert.equal(again.verdict.verdict, 'intermittently incorrect');
  assert.equal(h.calls.pass.length, 0);
  assert.deepEqual(again.score, first.score);
});
