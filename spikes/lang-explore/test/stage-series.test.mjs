// The Stage 1 series (F3.S1 (a), PR 3): `stage-init` makes ONE series over the six tasks, each task with its frozen slice; the slice passes every check a one-task series passes; the default guard makes a rehearsal series only. Fakes for the
// guard, the published workspaces and the slices; a throw-away home; the held-out side and the task repository are never read.
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { existsSync, readFileSync, readdirSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { ok, fail } from '../../minigit-bench/lib/result.mjs';
import { readRunMap } from '../../minigit-bench/lib/run-map.mjs';
import { tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { templatesIdentity } from '../lib/contract-identity.mjs';
import { harnessMovedForStacks } from '../lib/harness-moved.mjs';
import { runnerPinNow } from '../lib/harness-pin.mjs';
import { heldoutMovedProblem } from '../lib/heldout-quick.mjs';
import { seriesLimits } from '../lib/limits.mjs';
import { CANONICAL_SERIES, createSeries, isRehearsal, loadSeries, runMapFileOf, seriesInputs, seriesPrompts, seriesRootProblem } from '../lib/series.mjs';
import { stageInit } from '../lib/stage-init.mjs';
import { inputsOfTask, loadStageSeries, stageSeriesDirs } from '../lib/stage-series.mjs';
import { seedProblem, STAGE_ROUNDS } from '../lib/stage-schedule.mjs';
import { frozenPinsProblem, pinsOfTask, seriesTaskProblem, taskIds } from '../lib/tasks.mjs';
import { COMMIT, EXPLORE, PRE, TASKS, fakeSlice, initIo, makeSeries, sliceOfTask } from './stage-series-fixture.mjs';
import { FREEZE_TASKS } from '../lib/workspaces-sources.mjs';

const readJson = file => JSON.parse(readFileSync(file, 'utf8'));
const sha = text => createHash('sha256').update(text).digest('hex');

test('stage_init_makes_one_series_over_the_six_tasks_with_each_tasks_frozen_slice', async () => {
  const { io, dir, made } = await makeSeries();
  assert.equal(made.exit, 0, made.message);
  assert.match(path.basename(dir), CANONICAL_SERIES, 'the canonical name, so a paid run and `seriesRootProblem` hold');
  assert.equal(path.basename(dir), 'x-20261120-ab12');
  assert.equal(seriesRootProblem(io.home, dir), null);
  const state = readJson(path.join(dir, 'explore-series.json'));
  assert.deepEqual(Object.keys(state).sort(), ['arm', 'createdAt', 'pilot', 'preregCommit', 'preregTag', 'rounds', 'seedSha256', 'seriesId', 'stage', 'tasks'].sort());
  assert.deepEqual([state.stage, state.arm, state.tasks, state.rounds, state.preregTag, state.preregCommit, state.seedSha256], [1, 'bare', [...FREEZE_TASKS], STAGE_ROUNDS, PRE.preregTag, COMMIT, PRE.seedSha256]);
  const inputs = readJson(path.join(dir, 'inputs.json'));
  assert.deepEqual(Object.keys(inputs.tasks).sort(), [...FREEZE_TASKS], 'one slice per task of the freeze (a JSON object lists the key "10" first: the order of the tasks is the state\'s array)');
  for (const task of TASKS) assert.deepEqual(inputs.tasks[task.id], sliceOfTask(task), `task ${task.id}: exactly its own slice`);
  for (const key of ['subject', 'cli', 'python3', 'caps', 'locale', 'envExtra', 'runnerPin', 'templatesIdentity', 'templateHashes', 'preregManifestSha256', 'policy', 'taskRepo', 'harnessIdentity']) assert.ok(inputs[key] !== undefined, `series-wide: ${key}`);
  assert.equal(inputs.preregManifestSha256, PRE.manifestSha256);
  assert.deepEqual(inputs.taskRepo, { root: '/fake/task-repo', commit: COMMIT }, 'the commit the workspaces were checked at is recorded');
  assert.deepEqual(inputs.caps, EXPLORE.caps, 'the cap, the wall and the stall of the registered pins');
  assert.equal(inputs.policy.pricesSha256.length, 64);
  assert.equal(readRunMap(runMapFileOf(dir)).value.stage, 1);
  assert.deepEqual(readdirSync(path.join(dir, 's')), [], 'no session yet');
  assert.equal(loadStageSeries(dir).ok, true);
  assert.deepEqual(stageSeriesDirs(io.home), [dir]);
  assert.equal(isRehearsal(dir), false);
});

/** A one-task series made the way `init` makes it, from the pieces of one slice. */
function oneTaskSeries({ home, task, wide }) {
  const { limits, ...extra } = sliceOfTask(task);
  const made = createSeries({ home, explore: EXPLORE, calibration: { limits }, task: task.id, inputsExtra: { subject: wide.subject, python3: wide.python3, cli: wide.cli, specs: wide.specs, runnerPin: wide.runnerPin, templatesIdentity: wide.templatesIdentity, templateHashes: wide.templateHashes, ...extra } });
  return made.value.dir;
}

const file = text => ({ bytes: Buffer.from(text), mode: 0o644 });

test('A_task_slice_passes_every_check_a_one_task_series_passes', async () => {
  const { io, dir } = await makeSeries();
  const stage = readJson(path.join(dir, 'inputs.json'));
  for (const task of TASKS) {
    const one = oneTaskSeries({ home: io.home, task, wide: stage });
    const control = seriesInputs(one).value;
    const sliced = seriesInputs(dir, task.id);
    assert.equal(sliced.ok, true, sliced.reason);
    for (const key of Object.keys(control).filter(k => k !== 'seriesId')) assert.deepEqual(sliced.value[key], control[key], `task ${task.id}: ${key} is what a one-task init writes`);
    assert.deepEqual(inputsOfTask(stage, task.id).value, sliced.value);
    const held = { task: task.id, treeSha256: stage.tasks[task.id].heldoutQuick.treeSha256, caseIds: Array(7).fill('x') };
    assert.deepEqual([heldoutMovedProblem({ inputs: sliced.value, heldout: held }), heldoutMovedProblem({ inputs: control, heldout: held })], [null, null]);
    assert.equal(heldoutMovedProblem({ inputs: sliced.value, heldout: { ...held, treeSha256: sha('another') } }) === null, false);
    assert.deepEqual([frozenPinsProblem({ inputs: sliced.value, task, seriesId: 's' }), frozenPinsProblem({ inputs: control, task, seriesId: 's' })], [null, null]);
    assert.deepEqual(seriesLimits(dir, task.id), seriesLimits(one));
    assert.deepEqual(seriesPrompts(dir, task.id), seriesPrompts(one));
    const files = { 'STATEMENT.md': file(`${task.id}:sc`), 'samples/load.json': file(`${task.id}:lc`), 'PACKAGES.md': file('x') };
    const world = { filesFor: () => files };
    const now = { runnerPin: runnerPinNow(), templatesIdentity: templatesIdentity() };
    const [a, b] = [sliced.value, control].map(inputs => harnessMovedForStacks({ inputs, world, stacks: ['csharp'], now }));
    assert.equal(a, b, 'the harness check says the same of the slice and of the one-task inputs');
  }
  assert.equal(inputsOfTask(stage, '99').ok, false);
  assert.match(inputsOfTask(stage, '99').reason, /task 99: not a task of this Stage 1 series \(it holds 04, 05, 06, 07, 08, 10\)/);
});

test('A_changed_pin_of_one_task_refuses_that_tasks_slot_and_names_the_task', async () => {
  const { dir } = await makeSeries();
  const stage = readJson(path.join(dir, 'inputs.json'));
  const moved = { ...TASKS.find(t => t.id === '07'), spec: { ...TASKS.find(t => t.id === '07').spec, sha256: sha('a spec amended since the series was made') } };
  const problem = frozenPinsProblem({ inputs: inputsOfTask(stage, '07').value, task: moved, seriesId: 'x-1' });
  assert.match(problem, /^the pins of task 07 changed since series x-1 was made \(spec\)/);
  for (const task of TASKS.filter(t => t.id !== '07')) assert.equal(frozenPinsProblem({ inputs: inputsOfTask(stage, task.id).value, task, seriesId: 'x-1' }), null, `task ${task.id} is not refused for task 07's change`);
  assert.deepEqual(Object.keys(pinsOfTask(moved)), ['spec', 'format', 'privateNotes']);
  assert.equal(taskIds(EXPLORE).includes('07'), true);
});

test('stage_init_with_the_default_guard_makes_a_rehearsal_series_only', async () => {
  const io = initIo({ stage: { initGuardFn: undefined } });
  const paid = await stageInit({ values: { 'task-repo': '/t' }, io });
  assert.equal(paid.exit, 4);
  assert.match(paid.message, /^stage-init: the pre-registration guard .* is not merged yet: until it is, stage-init makes a rehearsal series only \(--rehearsal\)/);
  assert.deepEqual(readdirSyncOrEmpty(io.home), [], 'nothing was written');
  const rehearsal = await stageInit({ values: { rehearsal: true, 'task-repo': '/t' }, io });
  assert.equal(rehearsal.exit, 0, rehearsal.message);
  assert.match(path.basename(rehearsal.dir), /^r-\d{8}-[0-9a-f]{4}$/, 'an r- series: no paid run can pick it by its name');
  assert.equal(isRehearsal(rehearsal.dir), true);
  const state = readJson(path.join(rehearsal.dir, 'explore-series.json'));
  assert.equal(state.preregTag, null);
  assert.equal(seedProblem(state.rehearsalSeed), null, 'its own seed has 120 distinct rids');
  assert.equal(seriesTaskProblem({ state: loadSeries(rehearsal.dir).value.state, task: TASKS[0] }) !== null, true);
});

const readdirSyncOrEmpty = dir => (existsSync(path.join(dir, 'rsd')) ? readdirSync(path.join(dir, 'rsd', 'lang-v2', 'explore')) : []);

test('stage_init_refuses_and_writes_nothing_for_a_task_whose_held_out_side_is_not_the_registered_one_a_workspace_that_differs_and_a_missing_task_repository', async () => {
  const cases = [
    ['a held-out side that is not the registered one', { stage: { sliceFn: async a => (a.task.id === '07' ? fail('the clear folder differs from its archive') : ok(sliceOfTask(a.task))) } }, { 'task-repo': '/t' }, /^task 07: the clear folder differs from its archive/],
    ['a published workspace that is not what a session is given', { stage: { workspacesFn: async () => ok({ commit: COMMIT, findings: ['csharp/04-x differs at STATEMENT.md'] }) } }, { 'task-repo': '/t' }, /not what a session is given \(csharp\/04-x differs/],
    ['a checkout that cannot be checked', { stage: { workspacesFn: async () => fail('dirty checkout') } }, { 'task-repo': '/t' }, /cannot be checked: dirty checkout/],
    ['no task repository', {}, {}, /needs the task repository: --task-repo <checkout> or LANG_V2_TASK_REPO/],
  ];
  for (const [what, over, values, message] of cases) {
    const io = initIo(over);
    const r = await stageInit({ values, io });
    assert.equal(r.exit, 4, what);
    assert.match(r.message, message, what);
    assert.deepEqual(readdirSyncOrEmpty(io.home), [], `${what}: no series directory`);
  }
});

test('A_harness_identity_that_is_not_the_manifests_refuses_stage_init_unless_a_dated_line_accepts_it', async () => {
  const wrong = { ...PRE, harnessIdentity: 'f'.repeat(64) };
  const refused = await stageInit({ values: { 'task-repo': '/t' }, io: initIo({ stage: { initGuardFn: async () => ok(wrong) } }) });
  assert.equal(refused.exit, 4);
  assert.match(refused.message, /^the harness identity of this checkout \([0-9a-f]{12}\) is not the pre-registration manifest's \(ffffffffffff\) and no dated `instrument \| harness:[0-9a-f]{12}` line accepts it/);
  const live = /\(([0-9a-f]{12})\)/.exec(refused.message)[1];
  const dir = tmpDir('stage-amend');
  const amendments = path.join(dir, 'amendments.md');
  const { writeFileSync } = await import('node:fs');
  writeFileSync(amendments, `2026-11-20 | instrument | harness:${live} | the accepted identity, G-3\n`);
  const accepted = await stageInit({ values: { 'task-repo': '/t' }, io: initIo({ stage: { initGuardFn: async () => ok(wrong), amendmentsFile: amendments } }) });
  assert.equal(accepted.exit, 0, accepted.message);
});

test('The_guards_answer_is_the_only_source_of_the_registered_identities_and_a_slice_missing_for_a_task_is_refused', async () => {
  const { createStageSeries } = await import('../lib/stage-series.mjs');
  const made = createStageSeries({ home: tmpDir('stage-missing'), explore: EXPLORE, pre: PRE, wide: {}, slices: { '04': {} }, date: new Date('2026-11-20T10:00:00Z') });
  assert.equal(made.ok, false);
  assert.match(made.reason, /a slice of every task of the freeze: task 05, task 06, task 07, task 08, task 10 has none/);
  assert.equal((await fakeSlice({ task: TASKS[0] })).ok, true);
});
