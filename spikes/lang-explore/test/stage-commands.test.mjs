// The verbs of Stage 1 on the command line and their neighbours (F3.S1 (a), PR 3): `run`, `init` and `prune` refuse a Stage 1 series and name the new verbs, `stage-run` refuses an exploratory one, `stage-status` prints the rounds and the spend on five
// lines that add up to the ledger. The explore.mjs entry point lists the three verbs.
import assert from 'node:assert/strict';
import { appendFileSync, readFileSync } from 'node:fs';
import { test } from 'node:test';
import { main } from '../explore.mjs';
import { readLedger } from '../../minigit-bench/lib/ledger.mjs';
import { cmdInit, cmdPrune, cmdRun } from '../lib/commands.mjs';
import { cmdStageStatus } from '../lib/stage-commands.mjs';
import { stageRun } from '../lib/stage-run.mjs';
import { SPEND_LINES, spendLines, stageStatusLines } from '../lib/stage-status.mjs';
import { createSeries, ledgerFileOf, loadSeries } from '../lib/series.mjs';
import { quickCalibration } from '../lib/quick.mjs';
import { pinsOfTask } from '../lib/tasks.mjs';
import { EXPLORE, TASKS } from './stage-series-fixture.mjs';
import { SLOTS, mapOf, runRig } from './stage-run-fixture.mjs';

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
