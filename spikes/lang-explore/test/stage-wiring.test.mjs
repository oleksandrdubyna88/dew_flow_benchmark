// PR 4 wired into PR 3's seams (F3.S1 (a), PR 4): the default guard of `stage-run` is `stageGuard` for a paid series; before a unit is planned `stage-run` asks the agent box's environment, memory certification and the disk gate; a paid unit runs inside the clear
// held-out folder of its own task; `stage-init` checks the task repository at the record's W. Every refusal happens before anything is planned and leaves the run-map and the ledger byte for byte as they were (the disk halt alone adds its one event line).
// PR 3's fakes (`stage-run-fixture.mjs`: the fake loop, the registered lines and seed in a throw-away folder) with the seams of PR 4 replaced one at a time. Nothing is paid, nothing real is read, no held-out folder is opened.
import assert from 'node:assert/strict';
import { existsSync, readFileSync, readdirSync } from 'node:fs';
import { test } from 'node:test';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { readLedger } from '../../minigit-bench/lib/ledger.mjs';
import { loadExplore } from '../lib/inputs.mjs';
import { agentBoxEnv, envStep } from '../lib/stage-launch.mjs';
import { exploreRoot } from '../lib/paths.mjs';
import { ledgerFileOf, runMapFileOf } from '../lib/series.mjs';
import { stageInit } from '../lib/stage-init.mjs';
import { stageRun } from '../lib/stage-run.mjs';
import { FREEZE_TASKS } from '../lib/workspaces-sources.mjs';
import { LIVE, world } from './stage-guard-fixture.mjs';
import { COMMIT, EXPLORE, initIo, makeSeries } from './stage-series-fixture.mjs';
import { SLOTS, fakeLoop, runRig } from './stage-run-fixture.mjs';

const next = (rig, what = 'next') => stageRun({ values: { series: rig.dir, what }, io: rig.io });
const ledgerBytes = dir => (existsSync(ledgerFileOf(dir)) ? readFileSync(ledgerFileOf(dir), 'utf8') : '');
const bytes = rig => [ledgerBytes(rig.dir), readFileSync(runMapFileOf(rig.dir), 'utf8')];
const UNREADABLE = { launched: true, certified: false, reason: 'prog/memory.peak cannot be read after the exit of an empty program' };

/** A refused unit: its exit and message, and the proof that nothing changed (the run-map and the ledger as they were, the loop never called). */
async function refusedWith(rig, what = 'next') {
  const before = bytes(rig);
  const r = await next(rig, what);
  assert.deepEqual(bytes(rig), before, `${what}: the run-map and the ledger are byte for byte as they were`);
  assert.deepEqual(rig.log, [], `${what}: the loop was never called`);
  return r;
}

/** A disk halt: the run-map is as it was and the loop was never called; the ledger gained exactly one line, the halt's own event. */
async function haltedWith(rig) {
  const [ledgerBefore, mapBefore] = bytes(rig);
  const r = await next(rig);
  assert.equal(readFileSync(runMapFileOf(rig.dir), 'utf8'), mapBefore, 'the run-map is as it was');
  assert.deepEqual(rig.log, [], 'the loop was never called');
  const lines = ledgerBytes(rig.dir).slice(ledgerBefore.length).trim().split('\n');
  assert.equal(lines.length, 1, 'one new ledger line');
  assert.match(JSON.parse(lines[0]).event, /^disk-(reserve|rule)$/, 'and it is the halt\'s event');
  return r;
}

// ---- the default guard of stage-run ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

test('The_default_guard_of_stage_run_is_PR_4s_guard_for_a_paid_series_and_a_rehearsal_keeps_its_own_pass', async t => {
  const w = await world(t);
  const guardArgs = over => ({ repo: w.repo, taskRepo: w.c.dir, deps: { ...w.args.deps, ...over } });
  const rig = await runRig({ stage: { guardFn: undefined, guardArgs: guardArgs({}) } });
  assert.equal((await next(rig)).exit, 0, 'the control: a record, a task repository, identities and a pre-registration guard that all hold: the unit runs');
  const moved = await runRig({ stage: { guardFn: undefined, guardArgs: guardArgs({ live: async () => ({ ...LIVE, contract: { value: 'f'.repeat(64) } }) }) } });
  const refused = await refusedWith(moved);
  assert.equal(refused.exit, 4);
  assert.match(refused.message, /^1 problem\(s\) before a paid launch: contract differs: the manifest has /, 'stage-run says the guard\'s own named problem');
  const unwired = await runRig({ stage: { guardFn: undefined, guardArgs: guardArgs({ preregGuard: undefined }) } });
  const noPrereg = await refusedWith(unwired);
  assert.match(noPrereg.message, /the pre-registration guard \(PR 5: .* is not wired into this guard: a paid launch is refused without it/, 'until PR 5 hands in its guard a paid series is refused, by name');
  const asked = [];
  const seed = await runRig({ stage: { guardFn: undefined, guardArgs: guardArgs({ preregGuard: async a => { asked.push([a.requireSeed, a.series]); return []; } }) } });
  assert.equal((await next(seed)).exit, 0);
  assert.equal((await next(seed, 'aa')).exit, 0);
  assert.deepEqual(asked, [[true, seed.dir], [false, seed.dir]], 'PR 5\'s guard is asked about the seed for a data slot only');
  const { io, dir } = await makeSeries({ stage: { initGuardFn: undefined, guardArgs: guardArgs({ live: async () => { throw new Error('a rehearsal reads no identity'); } }) } }, { rehearsal: true, 'task-repo': '/fake/task-repo' });
  const log = [];
  io.runSeams = { prerequisitesFn: async () => ok({ calibration: {}, heldout: {}, world: { quick: false }, token: 'fake-token', prompts: {} }), loopFn: fakeLoop(log) };
  const rehearsal = await stageRun({ values: { series: dir, what: 'next' }, io });
  assert.equal(rehearsal.exit, 0, `a rehearsal is not under the guard: ${rehearsal.message}`);
  assert.equal(log.length, 1);
});

// ---- the agent box's environment ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

test('A_heap_key_in_the_agent_boxs_environment_refuses_a_unit_before_it_is_planned_and_leaves_the_run_map_and_the_ledger', async () => {
  const rig = await runRig();
  const planted = explore => ({ ...explore, envExtra: { ...explore.envExtra, NODE_OPTIONS: '--max-old-space-size=460' } });
  const refusedIo = { ...rig, io: { ...rig.io, exploreFn: () => ok(planted(EXPLORE)) } };
  const refused = await refusedWith(refusedIo);
  assert.equal(refused.exit, 3, 'a halt of the box\'s environment is exit 3');
  assert.match(refused.message, /the agent box's environment is not the registered base .*NODE_OPTIONS/, 'the key is named');
  const viaSeam = await refusedWith({ ...rig, io: { ...rig.io, stage: { ...rig.io.stage, agentEnvFn: explore => ({ ...explore.envExtra, DOTNET_GCHeapHardLimit: '0x1c000000' }) } } });
  assert.equal(viaSeam.exit, 3);
  assert.match(viaSeam.message, /DOTNET_GCHeapHardLimit/);
  assert.equal((await next(rig)).exit, 0, 'the control: the pins\' own environment is the registered base and the unit runs');
});

// ---- memory certification ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

test('A_host_that_cannot_certify_memory_refuses_every_paid_unit_before_it_is_planned_and_a_rehearsal_goes_on_with_its_flag', async () => {
  let probes = 0;
  const probeFn = async () => { probes += 1; return UNREADABLE; };
  const rig = await runRig({ stage: { probeFn } });
  for (const what of ['next', 'aa']) {
    const refused = await refusedWith(rig, what);
    assert.equal(refused.exit, 3, `${what}: the named halt is exit 3, resumable on a certifying host`);
    assert.match(refused.message, /^memory-not-certifiable: .*prog\/memory\.peak cannot be read/, what);
  }
  assert.equal(probes, 2, 'the probe ran once per launch');
  rig.io.stage.probeFn = async () => ({ launched: true, certified: true, peakBytes: 1_500_000 });
  assert.equal((await next(rig)).exit, 0, 'the control: on a host that certifies the same call runs');
  const { io, dir } = await makeSeries({ stage: { initGuardFn: undefined, probeFn: async () => UNREADABLE } }, { rehearsal: true, 'task-repo': '/fake/task-repo' });
  const log = [];
  const said = [];
  io.say = line => said.push(line);
  io.runSeams = { prerequisitesFn: async () => ok({ calibration: {}, heldout: {}, world: { quick: false }, token: 'fake-token', prompts: {} }), loopFn: fakeLoop(log) };
  const rehearsal = await stageRun({ values: { series: dir, what: 'next' }, io });
  assert.equal(rehearsal.exit, 0, rehearsal.message);
  assert.deepEqual(rehearsal.flags, ['memory bound not certified'], 'a rehearsal carries the flag in its result');
  assert.ok(said.includes('memory bound not certified'), 'and says it');
  assert.equal(log.length, 1, 'with the stub rig the round goes on');
});

// ---- the disk --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

test('The_disk_gate_halts_a_paid_unit_before_it_is_planned_with_its_one_event_line_and_a_rehearsal_holds_no_reserve', async () => {
  const rig = await runRig({ stage: { freeBytesFn: () => 1_000_000_000 } });
  const [ledgerBefore, mapBefore] = bytes(rig);
  const halted = await next(rig);
  assert.equal(halted.exit, 3, 'a disk halt is exit 3, resumable once the space is there');
  assert.match(halted.message, /^disk-reserve: 1000000000 bytes are free and the rest of the series needs \d+ bytes/, 'both numbers are in the sentence');
  assert.equal(readFileSync(runMapFileOf(rig.dir), 'utf8'), mapBefore, 'the run-map is as it was: nothing was planned');
  assert.deepEqual(rig.log, [], 'the loop was never called');
  const events = readLedger(ledgerFileOf(rig.dir));
  assert.equal(ledgerBefore, '', 'the ledger was empty');
  assert.deepEqual(events.map(e => [e.event, e.slot, e.freeBytes, 'usd' in e, 'rid' in e]), [['disk-reserve', 1, 1_000_000_000, false, false]], 'one event line, never a charge, naming the slot');
  const noSize = await runRig({ stage: { floorBytes: undefined } });
  const refused = await refusedWith(noSize);
  assert.equal(refused.exit, 4, 'a missing size is a refusal (exit 4), not a resumable halt');
  assert.match(refused.message, /^the disk check cannot be made: floorBytes .* must be a positive whole number/, 'and never a gate that passes');
  const unread = await runRig({ stage: { freeBytesFn: () => { throw new Error('statfs failed'); } } });
  const unreadRefused = await refusedWith(unread);
  assert.equal(unreadRefused.exit, 4);
  assert.match(unreadRefused.message, /^the disk check cannot be made: the free disk cannot be read/, 'free disk that cannot be read is a refusal too, with no ledger line');
  rig.io.stage.freeBytesFn = () => 10 ** 13;
  assert.equal((await next(rig)).exit, 0, 'the control: with the space the same call runs');
  const { io, dir } = await makeSeries({ stage: { initGuardFn: undefined, freeBytesFn: () => 1 } }, { rehearsal: true, 'task-repo': '/fake/task-repo' });
  const log = [];
  io.runSeams = { prerequisitesFn: async () => ok({ calibration: {}, heldout: {}, world: { quick: false }, token: 'fake-token', prompts: {} }), loopFn: fakeLoop(log) };
  assert.equal((await stageRun({ values: { series: dir, what: 'next' }, io })).exit, 0, 'a rehearsal is free: no reserve is held for it');
});

// ---- the clear held-out folder around the unit --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

test('The_prerequisites_of_a_paid_unit_are_handed_the_folder_its_slot_opened', async () => {
  const slot = { casesDir: '/invented/heldout-04/cases', opened: true };
  const rig = await runRig({ stage: { slotHeldoutFn: async ({ fn }) => ok(await fn(slot)) } });
  const given = [];
  const fake = rig.io.runSeams.prerequisitesFn;
  rig.io.runSeams.prerequisitesFn = async args => { given.push(args.heldout); return fake(args); };
  assert.equal((await next(rig)).exit, 0);
  assert.deepEqual(given.map(h => h?.slotFolder), [slot], 'the reader of the unit is told which folder is open, so it does not take the lock the slot holds');
});

test('A_paid_unit_runs_inside_the_clear_folder_of_its_own_task_and_a_unit_the_folder_refuses_changes_nothing', async () => {
  const seen = [];
  let open = false;
  const slotHeldoutFn = async ({ task, tasks, heldout, fn }) => {
    seen.push({ task: task.id, tasks: tasks.map(x => x.id), home: heldout.home });
    open = true;
    try { return ok(await fn()); } finally { open = false; }
  };
  const rig = await runRig({ stage: { slotHeldoutFn, pilotRand: (() => { let n = 0; return () => `r${(n += 1)}`; })() } });
  const inside = [];
  const loop = rig.io.runSeams.loopFn;
  rig.io.runSeams.loopFn = async a => { inside.push(open); return loop(a); };
  assert.equal((await next(rig)).exit, 0);
  assert.equal((await next(rig, 'aa')).exit, 0);
  assert.deepEqual(seen.map(s => s.task), [SLOTS[0].task, rig.log[1].task], 'each unit is wrapped for its own task: the data slot\'s, the A/A\'s');
  assert.deepEqual(seen[0].tasks, FREEZE_TASKS, 'with the six tasks of the series, so only the unit\'s own can stay in clear');
  assert.equal(seen[0].home, rig.io.home, 'under the operator\'s home');
  assert.deepEqual(inside, [true, true], 'the loop ran while the folder was open');
  assert.equal(open, false, 'and it was closed after');
  const refusing = await runRig({ stage: { slotHeldoutFn: async () => fail('the key\'s backup cannot be verified on the share\'s mount') } });
  const refused = await refusedWith(refusing);
  assert.equal(refused.exit, 4);
  assert.match(refused.message, /the key's backup cannot be verified/);
  const late = await runRig({ stage: { slotHeldoutFn: async ({ fn }) => { const result = await fn(); return fail('the clear folder of task 04 was left open after the slot', { result }); } } });
  const ended = await next(late);
  assert.equal(ended.exit, 3, 'a unit that ran and could not close its folder halts the series');
  assert.match(ended.message, /^done; the clear folder of task 04 was left open after the slot/, 'its own result stays in the message');
  assert.equal(late.log.length, 1, 'the unit did run');
  const { io, dir } = await makeSeries({ stage: { initGuardFn: undefined, slotHeldoutFn: async () => { throw new Error('a rehearsal is never wrapped'); } } }, { rehearsal: true, 'task-repo': '/fake/task-repo' });
  const log = [];
  io.runSeams = { prerequisitesFn: async () => ok({ calibration: {}, heldout: {}, world: { quick: false }, token: 'fake-token', prompts: {} }), loopFn: fakeLoop(log) };
  assert.equal((await stageRun({ values: { series: dir, what: 'next' }, io })).exit, 0, 'a rehearsal\'s held-out cases are fixtures: no clear folder is made');
});

// ---- the order of the checks ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

test('The_launch_checks_run_after_the_guard_and_before_the_plan_and_each_halts_before_the_next_is_asked', async () => {
  const asked = [];
  const stage = { probeFn: async () => { asked.push('memory'); return { launched: true, certified: true, peakBytes: 1 }; }, freeBytesFn: () => { asked.push('disk'); return 10 ** 13; }, slotHeldoutFn: async ({ fn }) => { asked.push('held-out'); return ok(await fn()); } };
  const noGo = await runRig({ lines: [], stage });
  assert.equal((await refusedWith(noGo)).exit, 4);
  const noGuard = await runRig({ stage: { ...stage, guardFn: async () => fail('the guard refuses') } });
  assert.equal((await refusedWith(noGuard)).message, 'the guard refuses');
  assert.deepEqual(asked, [], 'a unit refused by the go line or the guard asks none of the checks of PR 4');
  const both = await runRig({ stage: { ...stage, probeFn: async () => { asked.push('memory'); return UNREADABLE; }, freeBytesFn: () => { asked.push('disk'); return 1; } } });
  assert.match((await refusedWith(both)).message, /^memory-not-certifiable/, 'memory halts first');
  assert.deepEqual(asked, ['memory'], 'the disk is not asked, and no halt line is written');
  const full = await runRig({ stage: { ...stage, freeBytesFn: () => { asked.push('disk'); return 1; } } });
  assert.match((await next(full)).message, /^disk-reserve/);
  assert.deepEqual(asked, ['memory', 'memory', 'disk'], 'a disk halt came after the memory probe passed, and before the held-out folder was entered');
  const ok1 = await runRig({ stage });
  assert.equal((await next(ok1)).exit, 0);
  assert.deepEqual(asked.slice(3), ['memory', 'disk', 'held-out'], 'the order of a launch that goes ahead: memory, disk, the held-out folder');
});

// ---- stage-init at the record's W ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

const seriesCount = io => (existsSync(exploreRoot(io.home)) ? readdirSync(exploreRoot(io.home)).length : 0);

test('stage_init_checks_the_task_repository_at_the_records_W_for_a_paid_series_and_a_rehearsal_needs_no_record', async () => {
  const at = commit => ({ specStates: { freeze: { workspaceCommit: commit } } });
  const control = initIo({ stage: { recordFn: () => at(COMMIT) } });
  const made = await stageInit({ values: { 'task-repo': '/fake/task-repo' }, io: control });
  assert.equal(made.exit, 0, `the control: the checkout is at the record's W: ${made.message}`);
  const elsewhere = initIo({ stage: { recordFn: () => at('b'.repeat(40)) } });
  const refused = await stageInit({ values: { 'task-repo': '/fake/task-repo' }, io: elsewhere });
  assert.equal(refused.exit, 4);
  assert.match(refused.message, /^the workspaces the task repository publishes are not what a session is given \(the task repository checkout is at aaaaaaaaaaaa, not the record's workspace commit bbbbbbbbbbbb\): stage-init makes no series/, 'both commits are named');
  assert.equal(seriesCount(elsewhere), 0, 'no series was made');
  const noW = initIo({ stage: { recordFn: () => ({ specStates: {} }) } });
  const none = await stageInit({ values: { 'task-repo': '/fake/task-repo' }, io: noW });
  assert.equal(none.exit, 4);
  assert.match(none.message, /the frozen record .* names no workspace commit W/, 'a record without W is a refusal');
  assert.equal(seriesCount(noW), 0);
  const rehearsal = initIo({ stage: { initGuardFn: undefined, recordFn: () => { throw new Error('a rehearsal reads no record'); } } });
  assert.equal((await stageInit({ values: { rehearsal: true, 'task-repo': '/fake/task-repo' }, io: rehearsal })).exit, 0, 'a rehearsal holds no record to compare with');
  const changed = initIo({ stage: { recordFn: () => at(COMMIT), workspacesFn: async () => ok({ commit: COMMIT, findings: ['node/06-blog-api tree hash differs'] }) } });
  assert.match((await stageInit({ values: { 'task-repo': '/fake/task-repo' }, io: changed })).message, /node\/06-blog-api tree hash differs/, 'a changed workspace is still named');
});

// ---- Opus review (PR 4), O5: the checked environment is the one the agent box is built from ----------------------------------------------------------------------------------------------------------------------

test('The_environment_check_judges_the_quick_profile_the_agent_box_is_built_from_and_a_heap_key_that_only_that_profile_adds_is_refused', () => {
  const explore = loadExplore().value;
  const seen = [];
  const io = (agentEnvFn, home = '/invented/home') => ({ home, stage: { agentEnvFn } });
  const clean = envStep({ explore, io: io(e => { seen.push(e); return agentBoxEnv(e); }) });
  assert.equal(clean.ok, true, clean.reason);
  assert.equal(seen[0].quickProfile, true, 'the environment is built from the quick profile (`quickExplore`), as the agent box is');
  const real = envStep({ explore, io: { home: '/invented/home' } });
  assert.equal(real.ok, true, real.reason);
  assert.ok(real.value.boxKeys.agent.includes('DOTNET_ROOT'), `the default builder judges the keys the box really has (the quick profile's DOTNET_ROOT): ${real.value.boxKeys.agent.join(", ")}`);
  const refused = envStep({ explore, io: io(e => ({ ...agentBoxEnv(e), ...(e.quickProfile ? { DOTNET_GCHeapHardLimit: '0x1c000000' } : {}) })) });
  assert.equal(refused.ok, false, 'a heap key that only the quick profile adds is refused');
  assert.ok(refused.problems.some(p => p.includes('DOTNET_GCHeapHardLimit')), refused.problems?.join(' | '));
});
