// F2.S2 (d), built by step 2b: every reader of a task's clear held-out folder fails closed. With the folder absent and a sealed archive registered for the task in `heldout-frozen.json`, `leakcheck`'s task loading and its link
// check refuse with `sealed: open it first` and the task, through ONE function (`sealedAbsentProblem`), and never report clean or empty. A task with no archive registered keeps today's behaviour; a task whose folder is absent is
// never skipped while another task's folder is present. The private side is INVENTED content in a temp home; the record is a temp file; nothing real is read.
import '../../minigit-bench/test/fake-home.mjs';
import assert from 'node:assert/strict';
import { rmSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { loadHeldout } from '../lib/heldout.mjs';
import { sealedAbsentProblem } from '../lib/heldout-quick.mjs';
import { leakMaterial } from '../lib/leak.mjs';
import { scanBenchHalf } from './bench-half-seam.mjs';
import { buildMaterial } from '../lib/leak-material.mjs';
import { taskPaths } from '../lib/tasks.mjs';
import { tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { repoWith } from './leak-scan-fixture.mjs';
import { exploreOf, quickHome, quickTask, writeFrozen, writeQuickHeldout } from './quick-leak-fixture.mjs';

const CASES = [{ id: 'h001', input: 'scenario 2\ndefault within 2s\n', output: 'transcript 2\nrecv c "invented-sealed-answer-XYZZY-31415"\nexit 0\n' }];
const homeWith = ids => { const home = quickHome(); for (const id of ids) writeQuickHeldout({ home, task: quickTask(id), cases: CASES, traps: ['h001'] }); return home; };
const cases = (home, id) => path.join(taskPaths(quickTask(id), home).heldoutDir, 'cases');

test('A_sealed_task_with_no_clear_folder_is_a_problem_naming_the_task_and_the_opened_control_is_not', () => {
  const home = homeWith(['04']);
  const frozenFile = writeFrozen(['04', '05']);
  assert.match(sealedAbsentProblem({ home, task: quickTask('05'), frozenFile }), /^sealed: open it first \(task 05\)/);
  assert.equal(sealedAbsentProblem({ home, task: quickTask('04'), frozenFile }), null, 'the control: task 04 is registered and its folder is there');
});

test('A_task_with_no_archive_registered_or_no_record_at_all_keeps_today_s_behaviour', () => {
  const home = homeWith([]);
  assert.equal(sealedAbsentProblem({ home, task: quickTask('06'), frozenFile: writeFrozen(['04']) }), null, 'a record that does not list the task');
  assert.equal(sealedAbsentProblem({ home, task: quickTask('06'), frozenFile: path.join(tmpDir('q-frozen-none'), 'heldout-frozen.json') }), null, 'no record file at all');
});

test('A_record_that_cannot_be_read_is_a_refusal_when_the_folder_is_absent_and_nothing_when_it_is_there', () => {
  const home = homeWith(['04']);
  const garbled = writeFrozen([]);
  writeFileSync(garbled, '{ not json');
  assert.match(sealedAbsentProblem({ home, task: quickTask('05'), frozenFile: garbled }), /held-out record .* cannot be read/);
  const wrongShape = writeFrozen([]);
  writeFileSync(wrongShape, '["04"]');
  assert.match(sealedAbsentProblem({ home, task: quickTask('05'), frozenFile: wrongShape }), /held-out record .* cannot be read/);
  assert.equal(sealedAbsentProblem({ home, task: quickTask('04'), frozenFile: garbled }), null, 'the folder is there: the record is not needed');
});

test('The_leak_material_refuses_a_sealed_task_with_no_clear_folder_even_when_another_tasks_folder_is_present', () => {
  const home = homeWith(['04']);
  const frozenFile = writeFrozen(['05']);
  const r = leakMaterial({ home, explore: exploreOf(['04', '05']), publicText: '', frozenFile });
  assert.equal(r.ok, false, 'never a pass');
  assert.match(r.reason, /sealed: open it first \(task 05\)/);
  assert.doesNotMatch(r.reason, /XYZZY|31415/);
  const opened = homeWith(['04', '05']);
  const control = leakMaterial({ home: opened, explore: exploreOf(['04', '05']), publicText: '', frozenFile });
  assert.equal(control.ok, true, control.reason);
  assert.deepEqual([control.value.scanned, control.value.absent], [['04', '05'], []], 'opened, the same task is read');
});

test('The_leak_material_refuses_an_absent_task_that_is_not_sealed_while_another_tasks_folder_is_present', () => {
  const home = homeWith(['04']);
  const r = leakMaterial({ home, explore: exploreOf(['04', '06']), publicText: '', frozenFile: writeFrozen([]) });
  assert.equal(r.ok, false, 'a task that is skipped is a scan that proved less than it says');
  assert.match(r.reason, /task 06.*not present/);
  assert.equal(leakMaterial({ home, explore: exploreOf(['04', '06']), only: '04', publicText: '', frozenFile: writeFrozen([]) }).ok, true, 'a named task is the only one asked for');
});

test('The_link_check_calls_the_same_function_so_a_sealed_task_whose_folder_is_gone_is_never_scanned_for_links', () => {
  const home = homeWith(['04']);
  const held = loadHeldout(home, quickTask('04'));
  rmSync(cases(home, '04'), { recursive: true });
  const frozenFile = writeFrozen(['04']);
  const task = { id: '04', pins: quickTask('04'), heldout: held.value, shinglesText: '', quick: true };
  const built = buildMaterial({ tasks: [task], home, frozenFile });
  assert.equal(built.ok, false);
  assert.match(built.reason, /sealed: open it first \(task 04\)/);
});

test('The_leakcheck_command_exits_4_for_a_sealed_task_with_no_clear_folder_and_never_0', async () => {
  const home = homeWith(['04']);
  const said = [];
  const io = { home, repoRoot: repoWith(), progress: () => {}, frozenFile: writeFrozen(['05']) };
  const r = await scanBenchHalf({ values: {}, io, explore: exploreOf(['04', '05']), say: l => said.push(l) });
  assert.equal(r.exit, 4, said.join('\n'));
  assert.match(r.message, /sealed: open it first \(task 05\)/);
  assert.doesNotMatch(`${r.message}${said.join('\n')}`, /clean|XYZZY|31415/);
});
