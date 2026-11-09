// The stale-folder decision at EVERY entry point (F2.S2 (d), bench PR 4a): `init`, `run`, `rescore`, `replay`, `leakcheck`, and the three verbs run the same decision under the task's lock before they read: a clear folder equal to its
// archive with no stamp, or whose opener is dead, is removed (after the key's backup is verified) and the entry point goes on (fails closed, or opens for itself); one that differs is refused and nothing is removed; a held one is read
// as it is. Each entry point is called through the function it reads with: `quickInitExtra`, run's `prerequisites`, rescore's `seriesHeldout`, replay's `heldoutOf`, `commandMaterial` (leakcheck). Invented folders, keys and shares.
import '../../minigit-bench/test/fake-home.mjs';
import assert from 'node:assert/strict';
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { ok } from '../../minigit-bench/lib/result.mjs';
import { main } from '../explore.mjs';
import { backupConfigFile } from '../lib/heldout-backup-config.mjs';
import { heldoutAware } from '../lib/heldout-commands.mjs';
import { quickInitExtra } from '../lib/init-quick.mjs';
import { commandMaterial } from '../lib/leak.mjs';
import { quickCalibration } from '../lib/quick.mjs';
import { heldoutOf, seriesHeldout } from '../lib/rescore.mjs';
import { prerequisites } from '../lib/run.mjs';
import { createSeries } from '../lib/series.mjs';
import { pinsOfTask } from '../lib/tasks.mjs';
import { repoWith } from './leak-scan-fixture.mjs';
import { sealPackage } from '../lib/package-seal.mjs';
import { EXPLORE } from './quick-leak-fixture.mjs';
import { sealedClosed, world } from './quick-heldout-fixture.mjs';

/** A sealed world whose authoring `cases/` is still there: equal to its archive, no stamp, a verified backup. Hooks count the lock. */
async function stale() {
  const locks = [];
  const w = world({ over: { hooks: { afterLock: () => locks.push(1) } } });
  const r = await sealPackage({ ...w.args, hooks: {} });
  assert.equal(r.ok, true, r.reason);
  return { ...w, locks };
}

const lockOf = w => path.join(w.heldoutDir, '.clear.lock');
const seriesOf = w => createSeries({ home: w.home, explore: EXPLORE, calibration: quickCalibration(EXPLORE), task: w.taskId, inputsExtra: { taskPins: pinsOfTask(w.task) } }).value.dir;
const fakeWorld = async () => ok({ explore: EXPLORE });
const runPrerequisites = (w, dir) => prerequisites({ home: w.home, series: { dir }, task: w.task, worldFn: fakeWorld, tokenFn: async () => ok('token'), heldout: w.args });
const leak = w => commandMaterial({ values: { task: w.taskId }, io: { home: w.home, repoRoot: repoWith(), progress: () => {}, frozenFile: w.frozenFile, heldoutArgs: w.args }, explore: EXPLORE });

/** Each entry point: its name, and what it does with the world (the result is the entry point's own answer). */
const ENTRY_POINTS = Object.freeze([
  ['init', w => quickInitExtra({ explore: EXPLORE, task: w.task, home: w.home, heldout: w.args, sourcesFn: async () => ok({}) })],
  ['run', w => runPrerequisites(w, seriesOf(w))],
  ['rescore', w => seriesHeldout({ home: w.home, seriesDir: seriesOf(w), task: w.task, heldout: w.args })],
  ['replay', w => heldoutOf({ home: w.home, seriesDir: seriesOf(w), task: w.task, heldout: w.args })],
  ['leakcheck', w => leak(w)],
]);

for (const [name, run] of ENTRY_POINTS) {
  test(`An_entry_point_removes_a_stale_clear_folder_equal_to_its_archive_under_the_lock_${name}`, async () => {
    const w = await stale();
    const answer = await run({ ...w, args: { ...w.args, hooks: { afterLock: () => w.locks.push(1) } } });
    assert.equal(existsSync(w.casesDir), false, `${name}: the stale folder is gone`);
    assert.equal(w.locks.length >= 1, true, `${name}: the lock was taken`);
    assert.equal(existsSync(lockOf(w)), false, `${name}: and released`);
    if (name === 'leakcheck') assert.equal(answer.ok, true, answer.reason);
    else assert.match(answer.reason, /^sealed: open it first \(task 04\)/, `${name} fails closed after the removal`);
  });

  test(`An_entry_point_refuses_a_clear_folder_that_differs_from_its_archive_and_removes_nothing_${name}`, async () => {
    const w = await stale();
    const file = path.join(w.casesDir, 'h003.out');
    writeFileSync(file, 'changed after sealing\n');
    const answer = await run(w);
    assert.equal(answer.ok, false, name);
    assert.match(answer.reason, /^the clear folder differs from its archive: changed after sealing/, name);
    assert.equal(readFileSync(file, 'utf8'), 'changed after sealing\n', `${name}: the folder and the byte are unchanged`);
    assert.equal(existsSync(path.join(w.heldoutDir, '.open')), false);
    assert.equal(existsSync(lockOf(w)), false, `${name}: the lock is released`);
  });
}

test('Init_and_leakcheck_read_a_folder_held_by_the_operator_and_remove_nothing', async () => {
  const w = await sealedClosed();
  const opened = await main(['heldout-open', '--task', '04', '--key-file', w.keyFile], { home: w.home, frozenFile: w.frozenFile, say: () => {}, heldout: { mountOf: w.args.mountOf } });
  assert.equal(opened, 0);
  const held = await quickInitExtra({ explore: EXPLORE, task: w.task, home: w.home, heldout: w.args, sourcesFn: async () => ok({}) });
  assert.doesNotMatch(String(held.reason), /sealed: open it first|differs/, 'init reached the runner parse over the held folder (which the fixture\'s invented pairs do not satisfy)');
  const material = await leak(w);
  assert.equal(material.ok, true, material.reason);
  assert.equal(existsSync(w.casesDir), true, 'cases/ is still there');
  assert.equal(existsSync(path.join(w.heldoutDir, '.open')), true, 'and so is the stamp');
});

test('A_clear_folder_whose_opener_is_dead_is_removed_at_an_entry_point_and_a_live_one_is_used', async () => {
  const w = await sealedClosed();
  assert.equal((await main(['heldout-open', '--task', '04', '--key-file', w.keyFile], { home: w.home, frozenFile: w.frozenFile, say: () => {}, heldout: {} })), 0);
  writeFileSync(path.join(w.heldoutDir, '.open'), `${JSON.stringify({ pid: 4242, startTime: 'gone', at: '2026-11-09T08:00:00.000Z' })}\n`);
  const live = await seriesHeldout({ home: w.home, seriesDir: seriesOf(w), task: w.task, heldout: { ...w.args, isAlive: () => true } });
  assert.doesNotMatch(String(live.reason), /sealed: open it first/, 'control: a live opener keeps its folder, and the entry point reads it');
  assert.equal(existsSync(w.casesDir), true);
  const dead = await seriesHeldout({ home: w.home, seriesDir: seriesOf(w), task: w.task, heldout: { ...w.args, isAlive: () => false } });
  assert.match(dead.reason, /^sealed: open it first \(task 04\)/);
  assert.deepEqual([existsSync(w.casesDir), existsSync(path.join(w.heldoutDir, '.open'))], [false, false]);
});

test('The_verbs_run_the_same_decision_heldout_open_and_close_remove_a_stale_folder_and_check_reports_it_and_removes_nothing', async () => {
  const say = [];
  const seams = w => ({ home: w.home, frozenFile: w.frozenFile, say: l => say.push(l), heldout: { mountOf: w.args.mountOf, hooks: { afterLock: () => w.locks.push(1) } } });
  const checked = await stale();
  const check = await main(['heldout-check', '--tasks', '04', '--key-file', checked.keyFile], seams(checked));
  assert.equal(check, 0);
  assert.match(say.join('\n'), /present and equal to the archive, stale \(no stamp\)/);
  assert.equal(existsSync(checked.casesDir), true, 'check removes nothing');
  assert.equal(checked.locks.length, 0, 'and takes no lock');
  const closed = await stale();
  const flags = ['--key-backup', closed.backupFile, '--share-mount', closed.shareMount];
  assert.equal(await main(['heldout-close', '--task', '04', ...flags], seams(closed)), 0);
  assert.equal(existsSync(closed.casesDir), false);
  assert.equal(closed.locks.length >= 1, true);
  const opened = await stale();
  assert.equal(await main(['heldout-open', '--task', '04', '--key-file', opened.keyFile, '--key-backup', opened.backupFile, '--share-mount', opened.shareMount], seams(opened)), 0);
  assert.equal(existsSync(opened.casesDir), true, 'removed as stale, then opened again from the archive');
  assert.equal(existsSync(path.join(opened.heldoutDir, '.open')), true);
});

test('The_five_readers_are_given_the_held_out_context_of_their_flags_by_one_wrapper_and_a_bad_private_file_refuses_them', async () => {
  const w = await sealedClosed();
  const seen = [];
  const said = [];
  const wrapped = heldoutAware(async (values, io) => { seen.push(io.heldoutArgs); return 0; });
  const io = { home: w.home, say: l => said.push(l), frozenFile: w.frozenFile, heldout: { retry: { tries: 1, sleepMs: 1 } } };
  assert.equal(await wrapped({ 'key-file': w.keyFile, 'key-backup': w.backupFile, 'share-mount': w.shareMount }, io), 0);
  assert.deepEqual([seen[0].keyFile, seen[0].backupFile, seen[0].shareMount, seen[0].frozenFile, seen[0].retry.tries], [w.keyFile, w.backupFile, w.shareMount, w.frozenFile, 1]);
  const file = backupConfigFile(w.home);
  mkdirSync(path.dirname(file), { recursive: true });
  writeFileSync(file, '{ not json', { mode: 0o600 });
  assert.equal(await wrapped({}, io), 4, 'a private file that does not parse refuses the command before it starts');
  assert.match(said.join('\n'), /not valid JSON/);
  assert.equal(seen.length, 1, 'the command did not run');
  const source = readFileSync(new URL('../lib/commands.mjs', import.meta.url), 'utf8');
  for (const name of ['cmdInit', 'cmdRun', 'cmdRescore', 'cmdReplay', 'cmdLeakcheck']) assert.match(source, new RegExp(`export const ${name} = heldoutAware\\(`), `${name} is wrapped`);
});
