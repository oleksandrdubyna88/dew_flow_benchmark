// How the ten passes are wired into the commands (sections 2.2 P12 and P13, and the quick world's seams): `rescore` scores through the same pass driver and keeps every refusal and write it had; `replay` stays a single-pass
// diagnostic on one case; the quick world's seams make the clean copy, the case folder and the build, and run a pass in this process when a test gives a `runFn`. Fakes and a temp home; no process starts.
import '../../minigit-bench/test/fake-home.mjs';
import assert from 'node:assert/strict';
import { existsSync, readFileSync, readdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { readLedger } from '../../minigit-bench/lib/ledger.mjs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { acquireSeriesLock } from '../../minigit-bench/lib/series-lock.mjs';
import { tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { passSeams } from '../lib/pass-seams.mjs';
import { scorePasses } from '../lib/passes.mjs';
import { replay } from '../lib/replay.mjs';
import { rescore } from '../lib/rescore.mjs';
import { scoreSessionDep } from '../lib/run-ctx.mjs';
import { ledgerFileOf, runMapFileOf } from '../lib/series.mjs';
import { harness, recordFor } from './quick-passes-harness.mjs';
import { LIMIT_BYTES, sortedIds } from './quick-passes-fixture.mjs';
import { built, frozenFiles, ran, rec, scoringArea } from './quick-score-fixture.mjs';
import { EXPLORE, TASK, world } from './quick-series-fixture.mjs';

const read = file => readFileSync(file, 'utf8');
const DRIVER_ONLY = ['work', 'seriesDir', 'pinnedRunner', 'now', 'signal'];
const seamsOf = h => Object.fromEntries(Object.entries(h.a).filter(([k]) => !DRIVER_ONLY.includes(k)));

/** The io of a rescore whose world scores through `seams` (the ten passes) and whose `scoreFn` must never be called. */
function rescoreIo(w, { seams, log }) {
  const ctx = { runMapFile: runMapFileOf(w.dir), ledgerFile: ledgerFileOf(w.dir), sessionsRoot: path.join(w.dir, 's') };
  return {
    home: w.home, say: () => {}, now: () => new Date('2026-11-05T18:00:00.000Z'),
    worldFn: async () => ok({ explore: EXPLORE, paths: { claudeBinary: '/x/claude' }, snapshots: {}, quick: true, filesFor: () => ({}), passSeams: () => seams, scoreFn: async () => { log.push('single-pass scoreFn'); return fail('must not be called'); } }),
    tokenFn: async () => ok('fake-token'), contextFn: async () => ok(ctx), collectFn: () => ok({ outcome: { class: 'completed', data: true, flags: [] } }),
    recordFn: ({ entry }) => { log.push('record'); writeFileSync(path.join(w.layout.dir, 'record.json'), JSON.stringify({ rid: entry.rid })); return ok({}); },
    finalizeFn: () => { log.push('finalize'); return { problems: [], pairFile: path.join(w.dir, 'pairs', 'p1.json') }; },
  };
}

test('Rescore_scores_through_the_same_ten_passes_and_its_refusals_and_writes_are_as_they_were', async () => {
  const w = world();
  const h = harness({ holdSeries: false });
  const log = [];
  const r = await rescore({ task: TASK, seriesDir: w.dir, rid: 'a1', io: rescoreIo(w, { seams: seamsOf(h), log }) });
  assert.equal(r.exit, 0, r.message);
  assert.equal(h.calls.pass.length, 10, 'the scoring seam was called through the pass driver, ten pass calls');
  assert.deepEqual(log, ['record', 'finalize']);
  assert.equal(JSON.parse(read(path.join(w.layout.dir, 'score', 'score.json'))).explore.verdict, 'accepted');
  assert.ok(readLedger(ledgerFileOf(w.dir)).some(l => l.event === 'rescore'));
});

test('A_rescore_whose_passes_give_unscored_environment_writes_nothing_and_halts_as_a_harness_failure_does_today', async () => {
  const w = world();
  const h = harness({ holdSeries: false, plan: () => 'env' });
  const before = [read(runMapFileOf(w.dir)), read(ledgerFileOf(w.dir))];
  const log = [];
  const r = await rescore({ task: TASK, seriesDir: w.dir, rid: 'a1', io: rescoreIo(w, { seams: seamsOf(h), log }) });
  assert.equal(r.exit, 3);
  assert.match(r.message, /nothing was changed/);
  assert.deepEqual([read(runMapFileOf(w.dir)), read(ledgerFileOf(w.dir))], before);
  assert.deepEqual(log, [], 'no record and no pair record');
});

test('Replay_stays_a_single_pass_diagnostic_on_one_case_writes_only_its_own_file_and_feeds_no_verdict', async () => {
  const w = world();
  const calls = [];
  const passSeamsCalled = [];
  const io = {
    home: w.home, say: () => {}, now: () => new Date('2026-11-05T19:30:15.123Z'),
    worldFn: async () => ok({ explore: EXPLORE, paths: {}, snapshots: {}, quick: true, passSeams: () => { passSeamsCalled.push(1); return {}; }, scoreFn: async args => { calls.push(args.only); return ok({ build: { ok: true }, explore: { build: { ok: true, status: 0, wallMs: 9, timedOut: false }, cases: [{ id: 'h003', set: 'heldout', verdict: 'pass', reason: '', wallMs: 5, peakBytes: 1 }], frozenChanges: [], runner: '0.x', memoryCertified: true } }); } }),
  };
  const r = await replay({ task: TASK, seriesDir: w.dir, rid: 'a1', caseId: 'h003', io, times: 2 });
  assert.equal(r.exit, 0, r.message);
  assert.deepEqual(calls, ['h003', 'h003'], 'scoreFn once per --times, with the case named');
  assert.deepEqual(passSeamsCalled, [], 'the ten passes were not asked for');
  assert.equal(existsSync(path.join(w.layout.dir, 'score')), false, 'nothing under score/ is written or read');
});

test('A_single_pass_scoring_keeps_scoreFn_even_in_a_world_that_has_pass_seams_as_the_rehearsal_does', async () => {
  const w = world();
  let single = 0;
  const dep = scoreSessionDep({
    world: { passSeams: () => { throw new Error('the passes must not be asked for'); }, scoreFn: async () => { single += 1; return ok({ build: { ok: true } }); } }, calibration: { limits: {} }, heldout: { casesDir: '/x', traps: [] }, home: w.home, singlePass: true,
  });
  const r = await dep({ session: w.layout.dir, source: w.layout.work, stack: 'node', nugetCache: '/n', npmCache: '/p' });
  assert.equal(r.kind, 'scored');
  assert.equal(single, 1);
});

test('The_quick_worlds_seams_make_the_clean_copy_the_case_folder_and_the_build_and_run_ten_passes_in_this_process', async () => {
  const a = scoringArea({});
  const seriesDir = tmpDir('quick-wiring-series');
  const lock = acquireSeriesLock({ seriesDir });
  const profile = { paths: { dotnetDir: '/d', nodeDir: '/n', tsDir: '/t' }, snapshots: {}, kind: 'daemon', caseWallMs: 660_000, runCeilingMs: 7_200_000 };
  const record = recordFor();
  const passed = [];
  const runFn = async ({ tree, caseIds }) => { passed.push([tree, caseIds]); return { exitCode: 0, stdout: '', stderr: '', record, memoryCertified: true }; };
  const seams = passSeams({
    a: { stack: 'node', source: a.source, files: frozenFiles(), sets: [{ name: 'heldout', dir: a.held }], limits: { memoryBytes: LIMIT_BYTES, buildTimeoutMs: 600_000, timeMs: 300_000 }, traps: [], driftPossible: false },
    profile, seams: { buildFn: built(), runFn, probeFn: async cpus => ok(cpus), growthFn: () => ok({}) }, seriesDir,
  });
  const r = await scorePasses({ ...seams, work: a.work, seriesDir });
  lock.value.release();
  assert.equal(r.kind, 'scored', r.reason);
  assert.equal(r.verdict.verdict, 'accepted');
  assert.equal(passed.length, 10);
  assert.ok(passed.every(([tree, ids]) => tree === path.join(a.work, 'tree') && ids.join() === 'h001,h002,s01'));
  assert.equal(r.score.build.ok, true);
  assert.equal(r.score.explore.cases.length, 3);
  assert.ok(sortedIds().length === 3 && rec('s01').id === 's01');
  assert.deepEqual(readdirSync(a.work).filter(f => !['events', 'passes'].includes(f)).sort(), ['build.json', 'score.json', 'verdict.json'], 'the tree and the case folder are gone');
});

test('The_launching_sessions_of_the_series_come_from_its_run_map', async () => {
  const w = world();
  const { writeRunMap, readRunMap } = await import('../../minigit-bench/lib/run-map.mjs');
  const map = readRunMap(runMapFileOf(w.dir)).value;
  writeRunMap(runMapFileOf(w.dir), { ...map, sessions: { ...map.sessions, b2: { ...map.sessions.a1, rid: 'b2', status: 'launching' } } });
  const profile = { paths: { dotnetDir: '/d', nodeDir: '/n', tsDir: '/t' }, snapshots: {}, kind: 'daemon', caseWallMs: 1, runCeilingMs: 1 };
  const seams = passSeams({ a: { stack: 'node', source: w.layout.work, files: frozenFiles(), sets: [], limits: { buildTimeoutMs: 1 } }, profile, seams: {}, seriesDir: w.dir });
  assert.deepEqual(seams.seriesState(), { launching: ['b2'] });
});

test('The_loops_scoring_dep_answers_scored_for_a_decided_scoring_void_for_an_exhausted_one_and_environment_for_a_halt', async () => {
  const answerWith = async (plan, extra = {}) => {
    const w = world(); // a session of its own each time: a scored session is read back, not scored again
    const h = harness({ holdSeries: false, plan });
    const lock = acquireSeriesLock({ seriesDir: w.dir });
    try {
      const dep = scoreSessionDep({ world: { passSeams: () => ({ ...seamsOf(h), ...extra }) }, calibration: { limits: h.a.limits }, heldout: { casesDir: '/x', traps: [] }, home: w.home });
      return await dep({ session: w.layout.dir, source: w.layout.work, stack: 'node', nugetCache: '/n', npmCache: '/p' });
    } finally { lock.value.release(); }
  };
  const decided = await answerWith(() => 'clean');
  assert.equal(decided.kind, 'scored');
  assert.equal(decided.score.explore.verdict, 'accepted');
  const exhausted = await answerWith(() => 'env');
  assert.equal(exhausted.kind, 'void');
  assert.equal(exhausted.class, 'void: unscored environment');
  assert.match(exhausted.reason, /pass\(es\) used every attempt/);
  const halted = await answerWith(() => 'clean', { seriesState: () => ({ launching: ['zz9999'] }) });
  assert.equal(halted.kind, 'environment');
  assert.match(halted.reason, /zz9999/);
});
