// The clear folder's lifecycle (F2.S2 (d), bench PR 4a): who may remove a clear folder and who may not. "Stale" is defined by ownership: a folder with no stamp, or whose stamp's process is dead (`isAlive` says false and nothing
// else says it), is removed under the task's lock once its key's backup is verified; an operator hold or a live process's stamp is used as it is; a folder whose tree hash differs from its archive is refused and never removed.
// An archive with no registry entry is an interrupted seal, never an absent task. Invented stamps and folders in a temp home.
import '../../minigit-bench/test/fake-home.mjs';
import assert from 'node:assert/strict';
import { existsSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { processStartTime } from '../../minigit-bench/lib/session-state.mjs';
import { checkHeldout } from '../lib/heldout-check.mjs';
import { loadHeldoutQuick, sealedAbsentProblem } from '../lib/heldout-quick.mjs';
import { closeClearFolder, openClearFolder, sweepClearFolder } from '../lib/package-seal.mjs';
import { sealedClosed, world } from './quick-heldout-fixture.mjs';
import { sealPackage } from '../lib/package-seal.mjs';

const stampOf = w => path.join(w.heldoutDir, '.open');
const plant = (w, body) => writeFileSync(stampOf(w), `${JSON.stringify({ at: '2026-11-09T08:00:00.000Z', ...body })}\n`);

/** A sealed world with its clear folder opened by the verb, whose stamp the test then replaces. */
async function opened(over = {}) {
  const w = await sealedClosed({ over });
  assert.equal((await openClearFolder(w.args)).ok, true);
  return w;
}

test('An_entry_point_after_heldout_open_uses_the_held_folder_and_removes_nothing', async () => {
  const w = await opened();
  const unsealed = w.spies.unseal;
  const swept = await sweepClearFolder(w.args);
  assert.equal(swept.ok, true, swept.reason);
  assert.equal(swept.value.cases, 'held');
  assert.deepEqual([existsSync(w.casesDir), existsSync(stampOf(w))], [true, true], 'cases/ and .open are still there');
  assert.equal(w.spies.unseal, unsealed, 'the folder was used as it is');
  const file = path.join(w.casesDir, 'h001.in');
  writeFileSync(file, 'changed after the open\n');
  const refused = await sweepClearFolder(w.args);
  assert.equal(refused.ok, false, 'its tree hash is still checked against the archive\'s');
  assert.match(refused.reason, /differs from its archive/);
});

test('A_clear_folder_whose_opener_is_dead_is_removed_as_stale', async () => {
  const w = await opened();
  plant(w, { pid: 4242, startTime: 'gone' });
  const asked = [];
  const swept = await sweepClearFolder({ ...w.args, isAlive: (pid, start) => { asked.push([pid, start]); return false; } });
  assert.equal(swept.ok, true, swept.reason);
  assert.equal(swept.value.cases, 'absent');
  assert.deepEqual([existsSync(w.casesDir), existsSync(stampOf(w))], [false, false], 'cases/ and .open are removed');
  assert.deepEqual(asked, [[4242, 'gone']], 'the pid and its start time were the one question asked');
  const live = await opened();
  plant(live, { pid: 4242, startTime: 'running' });
  const kept = await sweepClearFolder({ ...live.args, isAlive: () => true });
  assert.equal(kept.value.cases, 'held', 'control: a live opener keeps its folder');
  assert.equal(existsSync(live.casesDir), true);
});

test('An_archive_without_a_registry_entry_is_an_interrupted_seal_not_an_absent_task', async () => {
  const w = await sealedClosed();
  rmSync(w.frozenFile);
  const problem = sealedAbsentProblem({ home: w.home, task: w.task, frozenFile: w.frozenFile });
  assert.match(problem, /^interrupted seal: run `package --task 04 --seal` again to resume/);
  assert.ok(problem.includes('task 04'));
  const loaded = loadHeldoutQuick(w.home, w.task, w.frozenFile);
  assert.equal(loaded.ok, false);
  assert.equal(loaded.absent, undefined, 'never an absent flag: nothing may read it as an absent task');
  assert.equal(loaded.reason, problem);
  const checked = await checkHeldout(w.args);
  assert.equal(checked.ok, false);
  assert.match(checked.reason, /^interrupted seal: run `package --task 04 --seal` again to resume/);
  const plain = world({ taskId: '05' });
  rmSync(plain.casesDir, { recursive: true });
  assert.equal(sealedAbsentProblem({ home: plain.home, task: plain.task, frozenFile: plain.frozenFile }), null, 'control: no archive and no entry is today\'s behaviour');
  const absent = loadHeldoutQuick(plain.home, plain.task, plain.frozenFile);
  assert.equal(absent.absent, true);
});

test('Close_refuses_a_folder_held_by_another_live_process_and_names_its_pid', async () => {
  const w = await opened();
  plant(w, { pid: 4242, startTime: 'running' });
  const refused = await closeClearFolder({ ...w.args, isAlive: () => true });
  assert.equal(refused.ok, false);
  assert.match(refused.reason, /pid 4242/);
  assert.equal(existsSync(w.casesDir), true, 'cases/ stays');
  assert.equal(existsSync(stampOf(w)), true);
  const own = await opened();
  plant(own, { pid: process.pid, startTime: processStartTime(process.pid) });
  assert.equal((await closeClearFolder(own.args)).ok, true, 'control: this process\'s own stamp is removed with the folder');
  assert.equal(existsSync(own.casesDir), false);
  const operator = await opened();
  assert.equal((await closeClearFolder({ ...operator.args, isAlive: () => true })).ok, true, 'control: the operator\'s stamp is removed');
  const dead = await opened();
  plant(dead, { pid: 4242, startTime: 'gone' });
  assert.equal((await closeClearFolder({ ...dead.args, isAlive: () => false })).ok, true, 'control: a dead process\'s stamp');
});

test('A_stamp_whose_folder_is_absent_is_removed_under_the_lock_and_a_task_with_no_archive_is_left_alone', async () => {
  const w = await opened();
  rmSync(w.casesDir, { recursive: true });
  const swept = await sweepClearFolder(w.args);
  assert.equal(swept.ok, true, swept.reason);
  assert.equal(existsSync(stampOf(w)), false, 'a stamp guarding nothing is removed');
  const unsealed = world({ taskId: '06' });
  const locks = [];
  const untouched = await sweepClearFolder({ ...unsealed.args, hooks: { afterLock: () => locks.push(1) } });
  assert.equal(untouched.ok, true);
  assert.equal(untouched.value.cases, 'unsealed', 'an authoring folder of an unsealed task: today\'s behaviour');
  assert.deepEqual(locks, [], 'no lock is taken for a task that is not sealed');
  assert.equal(existsSync(unsealed.casesDir), true);
  const sealed = await sealPackage({ ...unsealed.args });
  assert.equal(sealed.ok, true);
  const stale = await sweepClearFolder({ ...unsealed.args, hooks: { afterLock: () => locks.push(1) } });
  assert.equal(stale.ok, true, stale.reason);
  assert.deepEqual(locks, [1], 'the lock was taken');
  assert.equal(existsSync(unsealed.casesDir), false, 'the equal authoring folder of a sealed task is stale: removed after the backup check');
  assert.equal(readFileSync(unsealed.manifestFile, 'utf8').length > 0, true);
});
