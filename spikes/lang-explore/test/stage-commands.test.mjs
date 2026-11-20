// The verbs of Stage 1 on the command line and their neighbours (F3.S1 (a), PR 3): `run`, `init` and `prune` refuse a Stage 1 series and name the new verbs, `stage-run` refuses an exploratory one, `stage-status` prints the rounds and the spend on five
// lines that add up to the ledger. The explore.mjs entry point lists the three verbs.
import assert from 'node:assert/strict';
import { appendFileSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { main } from '../explore.mjs';
import { readLedger } from '../../minigit-bench/lib/ledger.mjs';
import { cmdInit, cmdPrune, cmdRun } from '../lib/commands.mjs';
import { exploreRoot } from '../lib/paths.mjs';
import { cmdStageStatus } from '../lib/stage-commands.mjs';
import { stageInit } from '../lib/stage-init.mjs';
import { stageRun } from '../lib/stage-run.mjs';
import { SPEND_LINES, spendLines, stageStatusLines } from '../lib/stage-status.mjs';
import { createSeries, ledgerFileOf, loadSeries, runMapFileOf } from '../lib/series.mjs';
import { quickCalibration } from '../lib/quick.mjs';
import { pinsOfTask } from '../lib/tasks.mjs';
import { EXPLORE, TASKS, makeSeries } from './stage-series-fixture.mjs';
import { GO_ALL, GO_PILOT, PILOT_GO_LINE, SLOTS, mapOf, readbackHolds, runRig } from './stage-run-fixture.mjs';

const say = lines => line => lines.push(line);
const ioOf = (rig, lines) => ({ ...rig.io, say: say(lines), exploreFn: undefined });

test('run_and_init_refuse_a_stage_series_and_stage_run_refuses_an_exploratory_one', async () => {
  const rig = await runRig();
  const lines = [];
  const run = await cmdRun({ task: '04', series: rig.dir, pair: '1' }, ioOf(rig, lines));
  assert.equal(run, 4);
  assert.match(lines.join('\n'), /is a Stage 1 series over several tasks: the verbs of Stage 1 are stage-init, stage-run and stage-status/);
  const initLines = [];
  assert.equal(await cmdInit({ task: '04' }, ioOf(rig, initLines)), 4);
  assert.match(initLines.join('\n'), /a Stage 1 series exists .*made by `stage-init` and run by `stage-run`/);
  const one = createSeries({ home: rig.io.home, explore: EXPLORE, calibration: quickCalibration(EXPLORE), task: '04', inputsExtra: { taskPins: pinsOfTask(TASKS[0]) } });
  const r = await stageRun({ values: { series: one.value.dir, what: 'next' }, io: rig.io });
  assert.equal(r.exit, 4);
  assert.match(r.message, /is not a Stage 1 series: stage-run works on a series made by stage-init, a one-task series is run with `run`/);
  assert.deepEqual(rig.log, []);
});

const STAGE_REFUSAL = /a Stage 1 series exists .*made by `stage-init` and run by `stage-run`/;
const initWords = async rig => { const lines = []; const exit = await cmdInit({ task: '04' }, ioOf(rig, lines)); return { exit, text: lines.join('\n') }; };
const nextSlot = rig => stageRun({ values: { series: rig.dir, what: 'next' }, io: rig.io });
const finishedRig = async slots => {
  const rig = await runRig({ lines: [GO_PILOT, GO_ALL, PILOT_GO_LINE, ...readbackHolds()] });
  for (let i = 0; i < slots; i += 1) assert.equal((await nextSlot(rig)).exit, 0, `slot ${i + 1}`);
  return rig;
};

test('init_is_not_refused_by_a_rehearsal_series', async () => {
  const { io, dir } = await makeSeries({ stage: { initGuardFn: undefined } }, { rehearsal: true, 'task-repo': '/fake/task-repo' });
  assert.match(path.basename(dir), /^r-/, 'the control: the series is a rehearsal');
  const got = await initWords({ io });
  assert.doesNotMatch(got.text, /a Stage 1 series exists/);
});

test('init_is_still_refused_by_an_open_paid_series_beside_a_rehearsal_one', async () => {
  const rig = await runRig();
  const rehearsal = await stageInit({ values: { rehearsal: true, 'task-repo': '/fake/task-repo' }, io: { ...rig.io, stage: { ...rig.io.stage, initGuardFn: undefined, rand: () => 'cd34' } } });
  assert.match(path.basename(rehearsal.dir), /^r-/, 'the control: a rehearsal exists beside the paid series');
  const got = await initWords(rig);
  assert.equal(got.exit, 4);
  assert.match(got.text, STAGE_REFUSAL);
  assert.ok(got.text.includes(path.basename(rig.dir)) && !got.text.includes('r-2026'), 'the sentence names the open paid series, not the rehearsal');
});

test('init_is_not_refused_by_a_finished_or_stopped_stage_series', async () => {
  const almost = await finishedRig(SLOTS.length - 1);
  assert.match((await initWords(almost)).text, STAGE_REFUSAL, 'the control: with one slot left the series is open');
  const done = await finishedRig(SLOTS.length);
  assert.match((await nextSlot(done)).message, /finished/, 'the control: the schedule offers nothing more');
  assert.doesNotMatch((await initWords(done)).text, /a Stage 1 series exists/, 'every slot done');
  const stopped = await runRig();
  writeFileSync(runMapFileOf(stopped.dir), `${JSON.stringify({ ...mapOf(stopped.dir), stopped: { rule: 'stop: auth', reason: '{}', at: '2026-11-20T10:00:00Z' } }, null, 2)}\n`);
  assert.doesNotMatch((await initWords(stopped)).text, /a Stage 1 series exists/, 'a stopped series');
});

test('init_is_refused_when_a_stage_series_cannot_be_read_and_the_sentence_says_why', async () => {
  const rig = await runRig();
  writeFileSync(runMapFileOf(rig.dir), 'not json');
  const got = await initWords(rig);
  assert.equal(got.exit, 4);
  assert.match(got.text, STAGE_REFUSAL);
  assert.match(got.text, /cannot be read .*counts as open/);
});

test('init_refuses_when_a_series_directory_under_the_explore_root_has_a_state_file_that_cannot_be_read_and_names_it', async () => {
  const { io } = await makeSeries({ stage: { initGuardFn: undefined } }, { rehearsal: true, 'task-repo': '/fake/task-repo' });
  assert.doesNotMatch((await initWords({ io })).text, /cannot be read/, 'the control: only a rehearsal beside it, nothing refuses');
  const broken = path.join(exploreRoot(io.home), 'x-20261121-cd34');
  mkdirSync(broken, { recursive: true });
  writeFileSync(path.join(broken, 'explore-series.json'), '{ not json');
  const got = await initWords({ io });
  assert.equal(got.exit, 4);
  assert.ok(got.text.includes(broken), 'the sentence names the directory');
  assert.match(got.text, /a state that cannot be read \(.*is unreadable: .*JSON/, 'and the read error');
  assert.doesNotMatch(got.text, /a Stage 1 series exists/, 'its own sentence: nobody knows whether it is a Stage 1 series');
});

test('prune_refuses_a_stage_series', async () => {
  const rig = await runRig();
  const lines = [];
  assert.equal(cmdPrune({ task: '04', series: rig.dir }, ioOf(rig, lines)), 4);
  assert.match(lines.join('\n'), /^prune refuses a Stage 1 series: nothing of Stage 1 is pruned/);
  const other = [];
  assert.equal(cmdPrune({ series: rig.dir }, ioOf(rig, other)), 4, 'with or without a task');
});

test('The_spend_lines_are_pilot_A_A_data_void_and_re_run_and_they_sum_to_the_ledger', () => {
  const session = (rid, kind, attempt) => [rid, { rid, kind, attempt, pair: 1, stack: 'node', status: 'scored' }];
  const map = { sessions: Object.fromEntries([session('p1', 'pilot', 1), session('a1', 'aa', 1), session('d1', 'data', 1), session('v1', 'data', 1), session('r1', 'data', 2), session('r2', 'pilot', 2)]) };
  const charge = (rid, kind, usd) => ({ kind, usd, what: 'x', rid, t: '2026-11-20T10:00:00Z' });
  const ledger = [charge('p1', 'pilot', 1.25), charge('a1', 'aa', 2.5), charge('d1', 'data', 3), charge('v1', 'void', 0.5), charge('r1', 'data', 4), charge('r2', 'pilot', 0.75), { event: 'owner-go', pair: 1 }];
  const got = spendLines({ map, ledger });
  assert.deepEqual(got.lines, { pilot: 1.25, 'A/A': 2.5, data: 3, void: 0.5, 're-run': 4.75 });
  assert.deepEqual(SPEND_LINES, ['pilot', 'A/A', 'data', 'void', 're-run'], 'five lines');
  assert.equal(got.other, 0);
  assert.equal(Object.values(got.lines).reduce((a, b) => a + b, 0), got.total, 'the five lines add up to the ledger');
  assert.equal(got.total, 12);
  const prior = spendLines({ map, ledger: [...ledger, { kind: 'prior', usd: 7, what: 'quick run', rid: undefined }] });
  assert.deepEqual([prior.other, prior.total], [7, 19], 'a charge no line holds is shown apart, so the lines plus it still add up');
});

test('An_attempt_1_session_voided_after_its_launch_is_a_void_charge_whatever_kind_its_ledger_line_has_and_the_lines_still_sum', () => {
  const session = (rid, over) => [rid, { rid, kind: 'data', attempt: 1, pair: 1, stack: 'node', status: 'scored', ...over }];
  const map = { sessions: Object.fromEntries([session('d1', { data: true, class: 'completed' }), session('v1', { data: false, class: 'void: harness' }), session('s1', { data: false, class: 'stop: auth' }), session('p1', { kind: 'pilot', data: false, class: 'void: harness' })]) };
  const charge = (rid, kind, usd) => ({ kind, usd, what: 'x', rid, t: '2026-11-20T10:00:00Z' });
  const ledger = [charge('d1', 'data', 3), charge('v1', 'data', 0.5), charge('s1', 'data', 0.25), charge('p1', 'pilot', 2)];
  const got = spendLines({ map, ledger });
  assert.deepEqual(got.lines, { pilot: 2, 'A/A': 0, data: 3.25, void: 0.5, 're-run': 0 }, 'a void by the scorer or a dated line (status scored, data false) is on the void line; a stop and a pilot session are not');
  assert.equal(Object.values(got.lines).reduce((a, b) => a + b, 0) + got.other, got.total, 'the five lines add up to the ledger');
});

test('stage_status_prints_the_rounds_the_slots_the_pilot_and_the_five_spend_lines', async () => {
  const rig = await runRig({ loop: { voids: s => s.stack === 'csharp' && s.attempt === 1 && s.pair === 1 } });
  const run = async what => stageRun({ values: { series: rig.dir, what }, io: rig.io });
  assert.equal((await run('next')).exit, 0);
  assert.equal((await run('next')).exit, 0);
  assert.equal((await run('next')).exit, 0);
  const map = mapOf(rig.dir);
  for (const s of Object.values(map.sessions)) appendFileSync(ledgerFileOf(rig.dir), `${JSON.stringify({ kind: s.data === false ? 'void' : 'data', usd: 1, what: s.class, rid: s.rid, t: '2026-11-20T10:00:00Z' })}\n`);
  const lines = [];
  assert.equal(cmdStageStatus({ series: rig.dir }, ioOf(rig, lines)), 0);
  const text = lines.join('\n');
  assert.match(text, /^series x-20261120-ab12: Stage 1, arm bare, tasks 04 05 06 07 08 10/);
  assert.match(text, /pilot and A\/A: not started/);
  assert.match(text, /round 1: 2 of 6 slots finished/);
  assert.match(text, new RegExp(`slot 1 \\(task ${SLOTS[0].task}\\): finished`));
  assert.equal((text.match(/^round \d: not started$/gm) ?? []).length, 4, 'rounds 2 to 5 have not started');
  assert.match(text, /spend \(\$5\.0000 in all\): pilot \$0\.0000, A\/A \$0\.0000, data \$3\.0000, void \$1\.0000, re-run \$1\.0000/);
  const ledger = readLedger(ledgerFileOf(rig.dir));
  assert.equal(stageStatusLines({ state: loadSeries(rig.dir).value.state, map, ledger }).join('\n'), text, 'the command is the pure function over the files');
  const none = [];
  assert.equal(cmdStageStatus({}, ioOf(rig, none)), 4);
  assert.equal(readFileSync(ledgerFileOf(rig.dir), 'utf8').length > 0, true);
});

test('explore_main_has_the_three_verbs_and_refuses_stage_run_without_its_arguments', async () => {
  const written = [];
  const seams = { home: (await runRig()).io.home, say: l => written.push(l) };
  assert.equal(await main(['stage-run'], seams), 4);
  assert.equal(await main(['stage-run', '--series', '/nowhere', '--what', 'everything'], seams), 4);
  assert.equal(await main(['stage-status'], seams), 4);
  assert.match(written.join('\n'), /stage-run needs --series|--what is aa, pilot:<NN> or next/);
  assert.equal(await main(['stage-run', '--until', 'round', '--series', '/x', '--what', 'aa'], seams), 4);
});
