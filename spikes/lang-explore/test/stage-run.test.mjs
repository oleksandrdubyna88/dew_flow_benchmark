// `stage-run` (F3.S1 (a), PR 3): the order of the checks, what the schedule offers, the owner's go, the guard, the series lock, the pause point that is not asked, the read-back stop. A FAKE loop (`io.runSeams.loopFn`) commits the plan and ends the
// sessions, so no box, no CLI and no model is involved; the real loop is driven in `stage-loop.test.mjs`.
import assert from 'node:assert/strict';
import { appendFileSync, existsSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { readLedger } from '../../minigit-bench/lib/ledger.mjs';
import { REPO } from '../lib/paths.mjs';
import { ledgerFileOf, runMapFileOf, seriesInputs } from '../lib/series.mjs';
import { cmdStageStatus } from '../lib/stage-commands.mjs';
import { stageRun } from '../lib/stage-run.mjs';
import { GO_ALL, GO_PILOT, PILOT_GO_LINE, SEED, SLOTS, fakeLoop, mapOf, readbackHolds, runRig, sha256 } from './stage-run-fixture.mjs';

const next = (rig, extra = {}) => stageRun({ values: { series: rig.dir, what: 'next', ...extra }, io: rig.io });
const snapshot = dir => readFileSync(runMapFileOf(dir), 'utf8');

test('next_runs_the_schedules_slot_and_no_flag_names_another_data_pair', async () => {
  const rig = await runRig();
  const first = await next(rig);
  assert.equal(first.exit, 0, first.message);
  assert.deepEqual([rig.log[0].pair, rig.log[0].kind, rig.log[0].task, rig.log[0].round], [1, 'data', SLOTS[0].task, 1], 'the schedule\'s slot 1, under the seed');
  assert.deepEqual(rig.log[0].planned, [SLOTS[0].first, SLOTS[0].first === 'csharp' ? 'node' : 'csharp'].map(stack => SLOTS[0].rids[stack][0]), 'the rids of the seed, the leading stack first');
  assert.equal((await next(rig)).exit, 0);
  assert.equal(rig.log[1].pair, 2, 'the second call runs slot 2');
  for (const what of ['pair:3', '3', 'slot:3', 'pilot', 'pilot:5', 'NEXT']) {
    const r = await stageRun({ values: { series: rig.dir, what }, io: rig.io });
    assert.equal(r.exit, 4, what);
    assert.match(r.message, /--what is aa, pilot:<NN> or next .*no flag names another data pair/, what);
  }
  assert.equal((await stageRun({ values: { series: rig.dir, what: 'next', pair: '9' }, io: rig.io })).exit, 0, 'a --pair flag is no flag of stage-run: it names nothing');
  assert.equal(rig.log[2].pair, 3);
  assert.equal(rig.log.length, 3, 'the refusals launched nothing');
  assert.match((await stageRun({ values: { what: 'next' }, io: rig.io })).message, /needs --series/);
});

test('A_void_is_re_run_alone_by_next_before_any_later_slot', async () => {
  const rig = await runRig({ loop: { voids: s => s.stack === 'csharp' && s.attempt === 1 } });
  assert.equal((await next(rig)).exit, 0);
  assert.equal((await next(rig)).exit, 0);
  assert.equal(rig.log[1].pair, 1, 'the second call is the re-run of slot 1, not slot 2');
  assert.deepEqual([rig.log[1].stacks, rig.log[1].attempts, rig.log[1].planned.length], [['csharp'], [2], 1], 'that stack alone, attempt 2');
  assert.equal(rig.log[1].planned[0], SLOTS[0].rids.csharp[1], 'under its own rid');
  assert.equal((await next(rig)).exit, 0);
  assert.equal(rig.log[2].pair, 2, 'then the next slot');
});

test('A_paid_launch_without_its_owner_go_line_is_refused_before_anything_is_planned', async () => {
  const rig = await runRig({ lines: [PILOT_GO_LINE] });
  const before = snapshot(rig.dir);
  const r = await next(rig);
  assert.equal(r.exit, 4);
  assert.match(r.message, /^round 1: no owner-go line covers round 1/);
  assert.equal(snapshot(rig.dir), before, 'the run-map is as it was: nothing was planned');
  assert.deepEqual(rig.log, [], 'the loop is never called');
  assert.deepEqual(readLedger(ledgerFileOf(rig.dir)), [], 'and no ledger line');
  writeFileSync(rig.files.amendmentsFile, `${PILOT_GO_LINE}\n${GO_ALL}\n`);
  assert.equal((await next(rig)).exit, 0, 'the control: with the line the same call runs');
  const events = readLedger(ledgerFileOf(rig.dir)).filter(l => l.event === 'owner-go');
  assert.deepEqual(events.map(e => [e.date, e.scope, e.pair, e.what]), [['2026-11-21', { from: 1, to: 5 }, 1, 'next']], 'the line it used is an event of the ledger, never a charge');
  assert.equal(events[0].words, 'the owner, 2026-11-21: all five rounds');
});

const ledgerBytes = dir => (existsSync(ledgerFileOf(dir)) ? readFileSync(ledgerFileOf(dir), 'utf8') : '');
const goEvents = dir => readLedger(ledgerFileOf(dir)).filter(l => l.event === 'owner-go');

test('A_run_refused_after_the_go_check_leaves_the_ledger_byte_for_byte_and_a_run_that_launches_has_exactly_one_go_event', async () => {
  const rig = await runRig();
  const good = rig.io.runSeams.prerequisitesFn;
  rig.io.runSeams.prerequisitesFn = async () => fail('a prerequisite is missing: the token');
  const before = [ledgerBytes(rig.dir), snapshot(rig.dir)];
  const refused = await next(rig);
  assert.equal(refused.exit, 4);
  assert.match(refused.message, /a prerequisite is missing/);
  assert.deepEqual([ledgerBytes(rig.dir), snapshot(rig.dir)], before, 'a refusal after the go check leaves the ledger and the run-map as they were');
  assert.equal((await next(rig)).exit, 4, 'the retry is refused the same way');
  assert.deepEqual(goEvents(rig.dir), [], 'no go event for a unit that did not go ahead');
  rig.io.runSeams.prerequisitesFn = good;
  assert.equal((await next(rig)).exit, 0);
  assert.equal(goEvents(rig.dir).length, 1, 'the unit that launched has its one go event');
});

test('A_Stage_1_unit_on_a_moved_harness_is_refused_and_the_ledger_and_the_run_map_stay_byte_for_byte', async () => {
  const rig = await runRig();
  rig.io.runSeams.prerequisitesFn = async () => ok({ calibration: {}, heldout: {}, world: { quick: true, filesFor: () => ({}) }, token: 'fake-token', prompts: {} });
  const before = [ledgerBytes(rig.dir), snapshot(rig.dir)];
  const refused = await next(rig);
  assert.deepEqual([refused.exit, /^harness-moved: /.test(refused.message)], [3, true], refused.message);
  assert.deepEqual([ledgerBytes(rig.dir), snapshot(rig.dir)], before, 'a refusal of a Stage 1 unit leaves the ledger and the run-map as they were: no harness-moved event');
  assert.equal((await next(rig)).exit, 3, 'the retry is refused the same way and still writes nothing');
  assert.equal(ledgerBytes(rig.dir), before[0]);
});

test('A_run_the_context_refuses_after_the_prerequisites_adds_no_go_event_either', async () => {
  const rig = await runRig();
  const inputs = JSON.parse(readFileSync(path.join(rig.dir, 'inputs.json'), 'utf8'));
  const notAPolicy = inputs.policy.prices;
  writeFileSync(path.join(rig.dir, 'inputs.json'), `${JSON.stringify({ ...inputs, policy: { ...inputs.policy, policy: notAPolicy, policySha256: sha256(readFileSync(path.join(REPO, notAPolicy), 'utf8')) } }, null, 2)}\n`);
  assert.equal(seriesInputs(rig.dir).ok, true);
  delete rig.io.runSeams.loopFn;
  rig.io.seams = { claudeBinary: '/c', fill: () => {} };
  const before = ledgerBytes(rig.dir);
  const refused = await next(rig);
  assert.equal(refused.exit, 4, refused.message);
  assert.match(refused.message, /policy file is refused/);
  assert.equal(ledgerBytes(rig.dir), before, 'the context of the loop refused: no go event');
});

test('stage_run_and_stage_status_report_every_malformed_Stage_1_line_and_every_home_path_and_neither_is_a_refusal', async () => {
  const rig = await runRig();
  const bad = [
    '2026-11-20 | owner-go | rounds:1-9 | the owner: too many rounds',
    '2026-11-22 | caps | wallMs:soon | stallMs:3000000 | the wall, G-18',
    '2026-11-22 | pilot | GO | verdicts:nothex | the team lead',
    `2026-11-24 | G-1 | holds | zzz999 | ${rig.io.home}/rsd/e.md | the judge`,
  ];
  writeFileSync(rig.files.amendmentsFile, `${[GO_PILOT, GO_ALL, PILOT_GO_LINE, ...bad].join('\n')}\n`);
  const said = [];
  rig.io.say = line => said.push(line);
  const run = await next(rig);
  assert.equal(run.exit, 0, 'a report line is no refusal');
  const report = said.filter(l => l.startsWith('amendments: '));
  assert.equal(report.length, 4, report.join('\n'));
  for (const line of bad.slice(0, 3)) assert.ok(report.some(r => /malformed line, not counted/.test(r) && r.endsWith(line)), `the malformed line is reported: ${line}`);
  assert.ok(report.some(r => /holds the home path/.test(r) && r.endsWith(bad[3])), 'the line with the home path written out is reported');
  const shown = [];
  assert.equal(cmdStageStatus({ series: rig.dir }, { ...rig.io, say: l => shown.push(l) }), 0);
  assert.deepEqual(shown.filter(l => l.startsWith('amendments: ')), report, 'stage-status prints the same report');
  const clean = await runRig();
  const quiet = [];
  clean.io.say = l => quiet.push(l);
  await next(clean);
  assert.deepEqual(quiet.filter(l => l.startsWith('amendments: ')), [], 'the control: a clean file reports nothing');
});

test('A_pinned_price_or_policy_file_that_is_missing_at_stage_run_is_refused_with_its_path_and_nothing_changes', async () => {
  const rig = await runRig();
  const inputs = JSON.parse(readFileSync(path.join(rig.dir, 'inputs.json'), 'utf8'));
  writeFileSync(path.join(rig.dir, 'inputs.json'), `${JSON.stringify({ ...inputs, policy: { ...inputs.policy, prices: 'spikes/nonexistent/prices.json' } }, null, 2)}\n`);
  const before = [ledgerBytes(rig.dir), snapshot(rig.dir)];
  const r = await next(rig);
  assert.equal(r.exit, 4, r.message);
  assert.match(r.message, /the price file .*spikes\/nonexistent\/prices\.json cannot be read \(.*ENOENT/);
  assert.deepEqual([ledgerBytes(rig.dir), snapshot(rig.dir)], before, 'the run-map and the ledger are as they were');
  assert.deepEqual(rig.log, []);
});

test('An_owner_go_for_rounds_1_to_2_does_not_cover_round_3_and_one_for_the_pilot_covers_no_round_at_stage_run', async () => {
  const rig = await runRig({ lines: [GO_PILOT, '2026-11-21 | owner-go | rounds:1-2 | the owner: rounds 1 and 2', PILOT_GO_LINE, ...readbackHolds()] });
  for (let i = 0; i < 12; i += 1) assert.equal((await next(rig)).exit, 0, `slot ${i + 1}`);
  const refused = await next(rig);
  assert.equal(refused.exit, 4);
  assert.match(refused.message, /^round 3: no owner-go line covers round 3/);
  assert.equal(rig.log.length, 12);
  const pilotOnly = await runRig({ lines: [GO_PILOT, PILOT_GO_LINE] });
  assert.match((await next(pilotOnly)).message, /round 1: no owner-go line covers round 1/, 'a go for the pilot covers no round');
  const roundsOnly = await runRig({ lines: ['2026-11-21 | owner-go | rounds:1-5 | the owner', PILOT_GO_LINE] });
  const pilot = await stageRun({ values: { series: roundsOnly.dir, what: 'aa' }, io: roundsOnly.io });
  assert.match(pilot.message, /^the pilot: no owner-go line covers the pilot/, 'a go for rounds covers no pilot step');
});

test('A_malformed_go_line_is_reported_in_the_refusal_and_never_counts', async () => {
  const rig = await runRig({ lines: ['2026-11-21 | owner-go | rounds:1-9 | the owner', PILOT_GO_LINE] });
  const r = await next(rig);
  assert.equal(r.exit, 4);
  assert.match(r.message, /1 malformed line\(s\) of the dated kinds were reported and count for nothing/);
  assert.deepEqual(rig.log, []);
});

test('No_data_slot_before_the_pilot_is_GO_and_the_seed_is_revealed_and_a_seed_that_is_not_the_committed_one_is_refused', async () => {
  const noGo = await runRig({ lines: [GO_ALL] });
  assert.match((await next(noGo)).message, /no data slot before the pilot is GO/);
  const noSeed = await runRig({ seedRevealed: false });
  assert.match((await next(noSeed)).message, /no data slot before the seed is revealed/);
  const wrong = await runRig();
  writeFileSync(wrong.files.seedFile, 'lv2-ffffffffffffffff\n');
  const r = await next(wrong);
  assert.equal(r.exit, 4);
  assert.match(r.message, /does not hash to the series' seedSha256/);
  assert.deepEqual([noGo.log, noSeed.log, wrong.log], [[], [], []]);
});

test('A_stage_series_never_runs_with_the_guard_that_always_passes', async () => {
  const rig = await runRig({ stage: { guardFn: undefined, guardArgs: { deps: { preregGuard: undefined } } } });
  const before = snapshot(rig.dir);
  const r = await next(rig);
  assert.equal(r.exit, 4);
  assert.match(r.message, /^\d+ problem\(s\) before a paid launch: .*the pre-registration guard \(PR 5: .* is not wired into this guard: a paid launch is refused without it/, 'the default is PR 4\'s guard, and with the pre-registration guard removed from its deps it refuses a paid launch by name (PR 5 plugs its own in by default: stage-wiring.test.mjs)');
  assert.equal(snapshot(rig.dir), before, 'refused before anything is planned');
  assert.deepEqual(rig.log, []);
  const pilot = await stageRun({ values: { series: rig.dir, what: 'aa' }, io: rig.io });
  assert.equal(pilot.exit, 4, 'the pilot is paid too');
});

test('The_guard_a_paid_series_is_given_asks_for_a_paid_launch_and_the_seed_only_for_a_data_slot', async () => {
  const asked = [];
  const rig = await runRig({ stage: { guardFn: async a => { asked.push(a); return { ok: true, value: null }; } } });
  assert.equal((await next(rig)).exit, 0);
  assert.equal((await stageRun({ values: { series: rig.dir, what: 'aa' }, io: rig.io })).exit, 0);
  assert.deepEqual(asked, [{ paid: true, requireSeed: true }, { paid: true, requireSeed: false }]);
  assert.equal(typeof rig.log[0].guard, 'function');
});

test('The_dry_run_prints_the_five_rounds_with_their_masks_and_the_cap_and_claims_nothing', async () => {
  const revealed = await runRig();
  const [ledgerBefore, mapBefore] = [readFileSync(ledgerFileOf(revealed.dir), 'utf8'), snapshot(revealed.dir)];
  const r = await stageRun({ values: { series: revealed.dir, what: 'next', 'dry-run': true }, io: revealed.io });
  assert.equal(r.exit, 0, r.message);
  assert.match(r.message, /a cap of \$50, a wall of 75 min, a stall of 45 min; no total ceiling and no pause point/);
  assert.equal((r.message.match(/^round \d$/gm) ?? []).length, 5);
  assert.equal((r.message.match(/^ {2}task \d\d: C# [AB] \(\S+\), TypeScript [AB] \(\S+\)$/gm) ?? []).length, 30, 'every slot of the five rounds with both masks');
  assert.match(r.message, /the seed is revealed: the order of each round is the order printed/);
  const firstRound = r.message.split('\n').filter(l => l.startsWith('  task ')).slice(0, 6).map(l => l.slice(7, 9));
  assert.deepEqual(firstRound, SLOTS.slice(0, 6).map(s => s.task), 'the order of round 1 is the seed\'s');
  assert.deepEqual([readFileSync(ledgerFileOf(revealed.dir), 'utf8'), snapshot(revealed.dir)], [ledgerBefore, mapBefore], 'nothing was written');
  assert.deepEqual(revealed.log, []);
  const hidden = await runRig({ seedRevealed: false });
  const h = await stageRun({ values: { series: hidden.dir, what: 'next', 'dry-run': true }, io: hidden.io });
  assert.match(h.message, /the seed is not revealed yet: the order of the tasks inside a round comes from it, so it is not printed/);
  assert.equal((h.message.match(/the order of its tasks is not known yet/g) ?? []).length, 5);
  assert.equal((h.message.match(/^ {2}task \d\d: /gm) ?? []).length, 30);
  assert.deepEqual(hidden.log, []);
});

test('A_second_stage_run_is_refused_while_the_first_holds_the_series_lock_and_names_the_holder', async () => {
  let release;
  const held = new Promise(resolve => { release = resolve; });
  let inside;
  const entered = new Promise(resolve => { inside = resolve; });
  const rig = await runRig({ loop: { hold: () => { inside(); return held; } } });
  const first = next(rig);
  await entered;
  const second = await next(rig);
  assert.equal(second.exit, 4);
  assert.match(second.message, new RegExp(`${process.pid}`), 'the holder is named');
  assert.equal(rig.log.length, 1, 'the second launched nothing');
  release();
  assert.equal((await first).exit, 0);
  assert.equal((await next(rig)).exit, 0, 'released at the end: the next call runs');
});

test('A_stage_series_is_not_under_the_quick_runs_pause_point_and_that_check_neither_counts_it_nor_stops_on_it', async () => {
  const { pauseVerdict, planSeriesDirs } = await import('../lib/pause.mjs');
  const { EXPLORE } = await import('./stage-series-fixture.mjs');
  const { quickPlan } = await import('../lib/quick.mjs');
  const rig = await runRig();
  appendFileSync(ledgerFileOf(rig.dir), `${JSON.stringify({ kind: 'data', usd: 9999, what: 'completed', rid: 'aaaaaa', t: '2026-11-20T10:00:00Z' })}\n`);
  const plan = quickPlan(EXPLORE);
  assert.deepEqual(planSeriesDirs(rig.io.home, EXPLORE, undefined, plan), [], 'not a series of the plan: not summed');
  const verdict = pauseVerdict({ home: rig.io.home, explore: EXPLORE, plan });
  assert.deepEqual(verdict.stop, false, 'and the check does not fail closed for it, whatever its ledger says');
  assert.equal((await next(rig)).exit, 0, 'stage-run never asks the check: a slot runs far past $150 in all');
});

test('stage_run_refuses_a_series_that_is_no_stage_series_a_copy_a_rename_and_a_directory_outside_the_explore_root', async () => {
  const rig = await runRig();
  const other = await stageRun({ values: { series: path.join(rig.io.home, 'nowhere'), what: 'next' }, io: rig.io });
  assert.equal(other.exit, 4);
  const renamed = await stageRun({ values: { series: rig.dir, what: 'next' }, io: { ...rig.io, seriesNameOk: () => false } });
  assert.match(renamed.message, /is not a canonical series name \(x-yyyymmdd-4 hex\)/);
  assert.deepEqual(rig.log, []);
});

test('A_stopped_series_is_refused_for_every_unit_with_the_stop_exit_and_nothing_is_launched', async () => {
  const rig = await runRig();
  const map = mapOf(rig.dir);
  writeFileSync(runMapFileOf(rig.dir), `${JSON.stringify({ ...map, stopped: { rule: 'stop: auth', reason: '{}', at: '2026-11-20T10:00:00Z' } }, null, 2)}\n`);
  for (const what of ['next', 'aa']) {
    const r = await stageRun({ values: { series: rig.dir, what }, io: rig.io });
    assert.equal(r.exit, 2, what);
    assert.match(r.message, /the series is stopped \(stop: auth\): a stopped series is never run again/, what);
  }
  assert.deepEqual(rig.log, []);
});

void fakeLoop; void mapOf;

test('A_rehearsal_series_has_its_own_seed_no_go_lines_to_wait_for_and_read_back_lines_that_hold', async () => {
  const { readGates } = await import('../lib/stage-gates.mjs');
  const { openStage } = await import('../lib/stage-step.mjs');
  const { stageInit } = await import('../lib/stage-init.mjs');
  const { initIo } = await import('./stage-series-fixture.mjs');
  const io = initIo();
  const made = await stageInit({ values: { rehearsal: true, 'task-repo': '/t' }, io });
  const series = openStage({ seriesDir: made.dir, io });
  const read = readGates({ series: series.value, io: { ...io, stage: { ...io.stage, amendmentsFile: '/nonexistent/amendments.md' } } });
  assert.equal(read.ok, true, read.reason);
  assert.deepEqual([read.value.gates.seedRevealed, read.value.gates.pilotGo, read.value.gates.seed === series.value.state.rehearsalSeed], [true, true, true]);
  assert.equal(read.value.gates.readbackLines.length, 8, 'two read-back slots, two stacks, two attempts');
  assert.ok(read.value.gates.readbackLines.every(l => l.g === 'G-1' && l.verdict === 'holds'));
});
