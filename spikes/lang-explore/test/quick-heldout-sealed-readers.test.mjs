// F2.S2 (d): every reader of a task's clear held-out folder fails closed (bench PR 4a). With the folder absent and a sealed archive registered, a reader refuses with `sealed: open it first (task NN)` and the verb that opens it, and
// never passes, reports clean, empty or scores visible-only; the control is the same task opened by `heldout-open`, which reads. The six tests of `quick-leak-sealed.test.mjs` (build step 2b) stay green and are not written again.
import '../../minigit-bench/test/fake-home.mjs';
import assert from 'node:assert/strict';
import { existsSync, readFileSync, readdirSync, rmSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { ok } from '../../minigit-bench/lib/result.mjs';
import { loadHeldout } from '../lib/heldout.mjs';
import { loadHeldoutQuick, openedFolderProblem } from '../lib/heldout-quick.mjs';
import { readerHeldout } from '../lib/heldout-reader.mjs';
import { quickInitExtra } from '../lib/init-quick.mjs';
import { commandMaterial } from '../lib/leak.mjs';
import { quickCalibration } from '../lib/quick.mjs';
import { heldoutOf, seriesHeldout } from '../lib/rescore.mjs';
import { prerequisites } from '../lib/run.mjs';
import { caseFolder } from '../lib/score-quick.mjs';
import { createSeries } from '../lib/series.mjs';
import { openClearFolder, withOpenedHeldout } from '../lib/package-seal.mjs';
import { pinsOfTask, resolveTask } from '../lib/tasks.mjs';
import { tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { repoWith } from './leak-scan-fixture.mjs';
import { EXPLORE } from './quick-leak-fixture.mjs';
import { sealedClosed } from './quick-heldout-fixture.mjs';

const lockOf = w => path.join(w.heldoutDir, '.clear.lock');
const stampOf = w => path.join(w.heldoutDir, '.open');

test('A_reader_that_opens_the_archive_closes_the_folder_after_it_also_when_it_fails', async () => {
  const failing = await sealedClosed();
  const seenInside = [];
  await assert.rejects(withOpenedHeldout({ ...failing.args, fn: () => { seenInside.push(existsSync(failing.casesDir)); throw new Error('the reader failed'); } }), /the reader failed/);
  assert.deepEqual(seenInside, [true], 'the folder was there for the reader');
  assert.deepEqual([existsSync(failing.casesDir), existsSync(stampOf(failing)), existsSync(lockOf(failing))], [false, false, false], 'no cases/, no .open, the lock released');
  const fine = await sealedClosed();
  const value = await withOpenedHeldout({ ...fine.args, fn: () => 'the reader\'s value' });
  assert.equal(value, 'the reader\'s value', 'control: the value is returned');
  assert.deepEqual([existsSync(fine.casesDir), existsSync(stampOf(fine)), existsSync(lockOf(fine))], [false, false, false], 'and the same cleanup');
  const refused = await withOpenedHeldout({ ...fine.args, keyFile: path.join(path.dirname(fine.keyFile), 'absent.key'), fn: () => { throw new Error('the reader must not run'); } });
  assert.equal(refused.ok, false, 'a folder that cannot be opened is a refusal value, and the reader never runs');
});

const SEALED = /^sealed: open it first \(task 04\)/;
const seriesOf = w => createSeries({ home: w.home, explore: EXPLORE, calibration: quickCalibration(EXPLORE), task: w.taskId, inputsExtra: { taskPins: pinsOfTask(w.task) } }).value.dir;

test('loadHeldoutQuick_refuses_a_sealed_task_with_no_clear_folder', async () => {
  const w = await sealedClosed();
  const r = loadHeldoutQuick(w.home, w.task, w.frozenFile);
  assert.equal(r.ok, false);
  assert.match(r.reason, SEALED);
  assert.ok(r.reason.includes('heldout-open --task 04'), 'the sentence names the verb that opens it');
  assert.equal(r.absent, undefined, 'never an absent flag');
  assert.equal((await openClearFolder(w.args)).ok, true);
  const control = loadHeldoutQuick(w.home, w.task, w.frozenFile);
  assert.equal(control.ok, true, control.reason);
  assert.equal(control.value.treeSha256, JSON.parse(readFileSync(w.frozenFile, 'utf8')).tasks['04'].treeSha256);
  const unsealed = await sealedClosed({ taskId: '05' });
  const other = loadHeldoutQuick(unsealed.home, resolveTask(EXPLORE, '06').value, unsealed.frozenFile);
  assert.equal(other.absent, true, 'an unsealed task with no folder keeps today\'s absent');
});

test('loadHeldout_refuses_a_sealed_quick_task_with_no_clear_folder', async () => {
  const w = await sealedClosed();
  const through = loadHeldout(w.home, w.task, w.frozenFile);
  assert.match(through.reason, SEALED, 'through heldout.mjs');
  const series = seriesOf(w);
  const prereq = await prerequisites({ home: w.home, series: { dir: series }, task: w.task, worldFn: async () => ok({}), tokenFn: async () => ok('token'), heldout: w.args });
  assert.match(prereq.reason, SEALED, 'run\'s prerequisites');
  const rescore = await seriesHeldout({ home: w.home, seriesDir: series, task: w.task, heldout: w.args });
  assert.match(rescore.reason, SEALED, 'rescore\'s seriesHeldout');
  const replay = await heldoutOf({ home: w.home, seriesDir: series, task: w.task, heldout: w.args });
  assert.match(replay.reason, SEALED, 'replay\'s heldoutOf');
  for (const r of [through, prereq, rescore, replay]) assert.equal(r.absent, undefined);
});

test('The_leak_check_opens_a_sealed_task_scans_it_and_closes_it_leaving_no_clear_folder', async () => {
  const w = await sealedClosed();
  const io = { home: w.home, repoRoot: repoWith(), progress: () => {}, frozenFile: w.frozenFile, heldoutArgs: { ...w.args, hooks: {} } };
  const r = await commandMaterial({ values: { task: '04' }, io, explore: EXPLORE });
  assert.equal(r.ok, true, r.reason);
  assert.deepEqual(r.value.scanned, ['04'], 'the scan\'s counts name the task');
  assert.deepEqual([existsSync(w.casesDir), existsSync(stampOf(w)), existsSync(lockOf(w))], [false, false, false], 'no cases/, no .open, no lock after the scan');
  assert.ok(w.spies.unseal >= 1, 'it opened the archive itself');
});

test('Init_refuses_a_sealed_task_with_no_clear_folder_and_names_heldout_open', async () => {
  const w = await sealedClosed();
  const r = await quickInitExtra({ explore: EXPLORE, task: w.task, home: w.home, heldout: w.args, sourcesFn: async () => ok({}) });
  assert.equal(r.ok, false);
  assert.match(r.reason, SEALED);
  assert.ok(r.reason.includes('heldout-open --task 04'));
  assert.deepEqual(readdirSync(path.join(w.home, 'rsd', 'lang-v2', 'explore')).filter(n => /^[xr]-/.test(n)), [], 'no series folder is written');
  assert.equal((await openClearFolder(w.args)).ok, true);
  const control = await readerHeldout({ home: w.home, task: w.task, heldout: w.args });
  assert.equal(control.ok, true, control.reason);
  assert.deepEqual([control.value.caseIds.length, control.value.traps.length], [3, 1], 'opened, the held-out step gives the tree hash and the counts');
});

test('Scoring_refuses_a_sealed_task_with_no_clear_folder_and_copies_no_pair', async () => {
  const w = await sealedClosed();
  const area = tmpDir('q-area');
  const sets = [{ name: 'heldout', dir: w.casesDir }];
  const r = caseFolder({ files: {}, sets, only: undefined }, area);
  assert.equal(r.ok, false);
  assert.equal(r.kind, 'environment', 'the environment failure, never a visible-only score');
  assert.deepEqual(readdirSync(area).filter(n => n === 'cases').flatMap(() => readdirSync(path.join(area, 'cases'))).filter(n => /^h\d+\.(in|out)$/.test(n)), [], 'no pair is copied');
  assert.match((await readerHeldout({ home: w.home, task: w.task, heldout: w.args })).reason, SEALED, 'and the loader before it refuses with the sealed sentence');
  assert.equal((await openClearFolder(w.args)).ok, true);
  const copied = caseFolder({ files: {}, sets, only: undefined }, tmpDir('q-area-2'));
  assert.equal(copied.ok, true, copied.reason);
  assert.equal(copied.value.ids.length, 3, 'control: opened, the pairs are copied');
});

test('The_olympiad_readers_are_not_reached_for_a_quick_task', async () => {
  const w = await sealedClosed();
  assert.equal((await openClearFolder(w.args)).ok, true);
  writeFileSync(path.join(w.heldoutDir, 'manifest.json'), '{"coverage":"invented"}\n');
  const quick = loadHeldout(w.home, w.task, w.frozenFile);
  assert.equal(quick.ok, true, 'the olympiad loader would read manifest.files of this file and throw: a quick task is routed to the quick loader only');
  const locks = [];
  const olympiad = await readerHeldout({ home: w.home, task: resolveTask(EXPLORE, '02').value, heldout: { ...w.args, hooks: { afterLock: () => locks.push(1) } } });
  assert.equal(olympiad.absent, true, 'a task of the olympiad is loaded as it always was');
  assert.deepEqual(locks, [], 'with no lock');
});

test('A_scoring_pair_checks_the_clear_folders_hash_against_the_registry_before_it_copies_a_pair', async () => {
  const w = await sealedClosed();
  assert.equal((await openClearFolder(w.args)).ok, true);
  const loaded = loadHeldoutQuick(w.home, w.task, w.frozenFile).value;
  assert.equal(openedFolderProblem({ home: w.home, task: w.task, heldout: loaded, frozenFile: w.frozenFile }), null, 'control: unchanged');
  const file = path.join(w.casesDir, 'h001.out');
  writeFileSync(file, `${readFileSync(file, 'utf8')}x`);
  const moved = loadHeldoutQuick(w.home, w.task, w.frozenFile).value;
  assert.match(openedFolderProblem({ home: w.home, task: w.task, heldout: moved, frozenFile: w.frozenFile }), /^the clear folder differs from its archive: changed after sealing/);
  rmSync(file);
  writeFileSync(file, 'x');
  const raced = await sealedClosed();
  assert.equal((await openClearFolder(raced.args)).ok, true);
  writeFileSync(stampOf(raced), `${JSON.stringify({ pid: 4242, startTime: 'running', at: '2026-11-09T08:00:00.000Z' })}\n`);
  const edit = path.join(raced.casesDir, 'h002.in');
  const changesAfterTheSweepLooked = () => { writeFileSync(edit, 'changed after the sweep looked\n'); return true; };
  const late = await readerHeldout({ home: raced.home, task: raced.task, heldout: { ...raced.args, isAlive: changesAfterTheSweepLooked } });
  assert.match(late.reason, /^the clear folder differs from its archive/, 'a folder changed after the stale decision is still refused by the load that follows it: the registry\'s hash is checked again before a pair is scored');
  const area = tmpDir('q-area-3');
  const refused = await readerHeldout({ home: w.home, task: w.task, heldout: w.args });
  assert.match(refused.reason, /^the clear folder differs from its archive/);
  assert.equal(existsSync(path.join(area, 'cases')), false, 'the area\'s cases/ holds nothing: no pair is copied after a refusal');
});
