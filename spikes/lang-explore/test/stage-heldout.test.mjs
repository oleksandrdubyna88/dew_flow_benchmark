// The clear held-out folder of a Stage 1 slot (F3.S1 (a), PR 4, scenarios 55 to 58 of the stories document): made from the archive for the slot's own task, checked against the registered tree hash, removed when the slot ends (whether it
// returns or throws), a stale one removed first after an interruption, a slot refused before its launch when the key's backup cannot be verified, and only the slot's own task ever in clear. Invented keys, cases, folders and shares only
// (`quick-heldout-fixture.mjs`): no real held-out folder is opened, no real key is read, and no case text, trap name or case id is in this file.
import '../../minigit-bench/test/fake-home.mjs';
import assert from 'node:assert/strict';
import { existsSync, readdirSync, rmSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { casesTreeHash } from '../lib/heldout-quick.mjs';
import { registeredEntry } from '../lib/heldout-registry.mjs';
import { readerHeldout } from '../lib/heldout-reader.mjs';
import { sweepClearFolder } from '../lib/heldout-lifecycle.mjs';
import { closeClearFolder, openClearFolder, sealPackage } from '../lib/package-seal.mjs';
import { withSlotHeldout } from '../lib/stage-heldout.mjs';
import { taskPaths } from '../lib/tasks.mjs';
import { quickTask, writeQuickHeldout } from './quick-leak-fixture.mjs';
import { CASES, TOKEN, TRAPS, sealedClosed } from './quick-heldout-fixture.mjs';

const DEAD = () => false;
const DEAD_PID = 2 ** 22 - 1;
/** A slot killed in the middle: the clear folder made from the archive and a stamp of a process that is gone. */
async function interrupted(args) {
  const opened = await openClearFolder({ ...args, owner: 'operator' });
  assert.equal(opened.ok, true, opened.reason);
  writeFileSync(path.join(taskPaths(args.task, args.home).heldoutDir, '.open'), `${JSON.stringify({ pid: DEAD_PID, startTime: '1', at: '2026-11-25T09:00:00.000Z' })}\n`);
}
const casesOf = (w, task = w.task) => path.join(taskPaths(task, w.home).heldoutDir, 'cases');
const heldoutOf = w => { const { task: _t, ...rest } = w.args; return rest; };

/** A second sealed task (05) in the same home, with the same key, whose clear folder is removed: the world of "only the slot's own task is ever in clear". */
async function withSecondTask(w) {
  const task = quickTask('05');
  const paths = writeQuickHeldout({ home: w.home, task, cases: CASES.map(c => ({ ...c, input: `${c.input}five\n` })), traps: TRAPS });
  const manifestFile = path.join(taskPaths(task, w.home).heldoutDir, 'manifest.json');
  writeFileSync(manifestFile, '{"coverage":"invented"}\n');
  const sealed = await sealPackage({ ...w.args, task, manifestFile, hooks: {} });
  assert.equal(sealed.ok, true, sealed.reason);
  rmSync(paths.casesDir, { recursive: true });
  return { task, args: { ...w.args, task, manifestFile } };
}

test('The_clear_folder_of_a_slots_task_is_made_for_the_slot_and_removed_after_it_whether_the_slot_ends_or_throws', async () => {
  const w = await sealedClosed();
  const tasks = [w.task];
  assert.equal(existsSync(casesOf(w)), false, 'before the slot there is no clear folder');
  let seen = null;
  const ended = await withSlotHeldout({ task: w.task, tasks, heldout: heldoutOf(w), fn: async ({ casesDir }) => {
    seen = { exists: existsSync(casesDir), tree: casesTreeHash(casesDir).value, files: readdirSync(casesDir).length, stamp: existsSync(path.join(taskPaths(w.task, w.home).heldoutDir, '.open')) };
    return 'the slot ran';
  } });
  assert.equal(ended.ok, true, ended.reason);
  assert.equal(ended.value, 'the slot ran', 'what the slot returns comes back');
  assert.equal(seen.exists && seen.files > 0 && seen.stamp, true, 'the folder is there for the slot, stamped');
  assert.equal(seen.tree, registeredEntry({ frozenFile: w.frozenFile, task: w.task }).value.treeSha256, 'and it is the registered tree');
  assert.equal(existsSync(casesOf(w)), false, 'it is removed when the slot ends');
  assert.equal(existsSync(path.join(taskPaths(w.task, w.home).heldoutDir, '.open')), false, 'with its stamp');
  await assert.rejects(withSlotHeldout({ task: w.task, tasks, heldout: heldoutOf(w), fn: async () => { throw new Error('the slot broke'); } }), /the slot broke/, 'a slot that throws is not hidden');
  assert.equal(existsSync(casesOf(w)), false, 'and the folder is removed all the same');
  assert.equal(w.spies.unseal, 2, 'the archive was opened once per slot');
  const warnings = [];
  const unclosable = await withSlotHeldout({ task: w.task, tasks, heldout: { ...heldoutOf(w), warn: line => warnings.push(line) }, fn: async () => { rmSync(w.backupFile); return 'ran'; } });
  assert.equal(unclosable.ok, false, 'a slot whose folder could not be removed after it (the backup vanished in the slot) is not a clean end');
  assert.match(unclosable.reason, /left open after the slot.*heldout-close/s, `it says what to do: ${unclosable.reason}`);
  assert.equal(unclosable.result, 'ran', 'and what the slot returned is not lost: the slot did run and may have spent');
  assert.equal(warnings.length, 1, 'the lifecycle said so too');
});

test('A_run_after_an_interruption_removes_the_stale_clear_folder_first', async () => {
  const w = await sealedClosed();
  await interrupted(w.args);
  assert.equal(existsSync(casesOf(w)), true, 'a killed slot left its clear folder, stamped by a process that is gone');
  const before = w.spies.unseal;
  let opened = null;
  const ended = await withSlotHeldout({ task: w.task, tasks: [w.task], heldout: { ...heldoutOf(w), isAlive: DEAD }, fn: async arg => { opened = arg.opened; return 'ran'; } });
  assert.equal(ended.ok, true, ended.reason);
  assert.equal(opened, true, 'the stale folder was removed and a fresh one made for the slot');
  assert.equal(w.spies.unseal, before + 1, 'from the archive, once');
  assert.equal(existsSync(casesOf(w)), false, 'and the slot removed it');
  const held = await openClearFolder({ ...w.args, owner: 'operator' });
  assert.equal(held.ok, true, held.reason);
  let ran = false;
  const refused = await withSlotHeldout({ task: w.task, tasks: [w.task], heldout: heldoutOf(w), fn: async () => { ran = true; } });
  assert.equal(refused.ok, false, 'a folder of its own task that the operator holds is not used as it is: the slot could not be sure to remove it');
  assert.match(refused.reason, /held by the operator.*heldout-close/s);
  assert.equal(ran, false);
  assert.equal((await closeClearFolder({ ...w.args })).ok, true);
});

test('A_slot_is_refused_before_its_launch_when_the_keys_backup_cannot_be_verified', async () => {
  const w = await sealedClosed();
  rmSync(w.backupFile);
  let ran = false;
  const refused = await withSlotHeldout({ task: w.task, tasks: [w.task], heldout: heldoutOf(w), fn: async () => { ran = true; } });
  assert.equal(refused.ok, false, 'the slot is refused');
  assert.match(refused.reason, /backup/i, `it names the backup: ${refused.reason}`);
  assert.equal(ran, false, 'nothing of the slot ran');
  assert.equal(w.spies.unseal, 0, 'and the archive was never opened: the check comes before the folder is made');
  assert.equal(existsSync(casesOf(w)), false);
  const noMount = await withSlotHeldout({ task: w.task, tasks: [w.task], heldout: { ...heldoutOf(w), shareMount: undefined }, fn: async () => { ran = true; } });
  assert.equal(noMount.ok, false, 'a share that is not named is the same refusal');
  assert.equal(ran, false);
});

test('Only_the_slots_own_task_is_ever_in_clear', async () => {
  const w = await sealedClosed();
  const second = await withSecondTask(w);
  const tasks = [w.task, second.task];
  const during = [];
  const control = await withSlotHeldout({ task: w.task, tasks, heldout: heldoutOf(w), fn: async () => { during.push(existsSync(casesOf(w)), existsSync(casesOf(w, second.task))); } });
  assert.equal(control.ok, true, control.reason);
  assert.deepEqual(during, [true, false], 'the slot\'s task is in clear and the other task is not');
  await interrupted(second.args);
  const swept = [];
  const cleaned = await withSlotHeldout({ task: w.task, tasks, heldout: { ...heldoutOf(w), isAlive: DEAD }, fn: async () => { swept.push(existsSync(casesOf(w, second.task))); } });
  assert.equal(cleaned.ok, true, cleaned.reason);
  assert.deepEqual(swept, [false], 'another task\'s stale clear folder is removed before the slot');
  const held = await openClearFolder({ ...second.args });
  assert.equal(held.ok, true, held.reason);
  let ran = false;
  const refused = await withSlotHeldout({ task: w.task, tasks, heldout: heldoutOf(w), fn: async () => { ran = true; } });
  assert.equal(refused.ok, false, 'another task held open by the operator refuses the slot');
  assert.ok(refused.reason.includes(second.task.id) && /only the slot/.test(refused.reason), `it names the task and the rule: ${refused.reason}`);
  assert.equal(ran, false);
  assert.equal(JSON.stringify(refused).includes(TOKEN), false, 'no case text is in the refusal (D41)');
  assert.equal((await closeClearFolder({ ...second.args })).ok, true, 'the operator closes it');
});

// ---- Opus review (PR 4), O1: the unit's own prerequisites read the folder its slot opened ------------------------------------------------------------------------------------------------------------------------

test('The_units_prerequisites_read_the_folder_the_slot_opened_without_taking_its_lock_again_and_any_other_reader_is_still_refused', async () => {
  const w = await sealedClosed();
  const heldout = heldoutOf(w);
  const inside = {};
  const ended = await withSlotHeldout({ task: w.task, tasks: [w.task], heldout, fn: async slot => {
    inside.today = await readerHeldout({ home: w.home, task: w.task, heldout });
    inside.handed = await readerHeldout({ home: w.home, task: w.task, heldout: { ...heldout, slotFolder: slot } });
    inside.foreign = await readerHeldout({ home: w.home, task: w.task, heldout: { ...heldout, slotFolder: { ...slot, casesDir: `${slot.casesDir}-of-another-task` } } });
    inside.other = await sweepClearFolder({ ...heldout, home: w.home, task: w.task, pid: DEAD_PID - 1, startTime: '1' });
    inside.exists = existsSync(slot.casesDir);
    return 'ran';
  } });
  assert.equal(ended.ok, true, ended.reason);
  assert.equal(inside.handed.ok, true, `the reader given the slot's folder reads it: ${inside.handed.reason}`);
  assert.equal(inside.handed.value.casesDir, casesOf(w), 'it is the slot\'s own folder');
  assert.equal(inside.exists, true, 'the folder was open while the unit read it');
  assert.equal(inside.today.ok, false, 'the control: a reader that is not handed the folder asks for the lock the slot holds');
  assert.match(inside.today.reason, /in use by pid/, inside.today.reason);
  assert.equal(inside.foreign.ok, false, 'a folder that is not this task\'s own does not skip the lock');
  assert.equal(inside.other.ok, false, 'another process is refused while the slot holds the lock');
  assert.match(inside.other.reason, new RegExp(`in use by pid ${process.pid}`), inside.other.reason);
  assert.equal(existsSync(casesOf(w)), false, 'and the folder is removed after the slot');
});

// ---- Opus review (PR 4), O8: a folder still in clear after the slot is always said, whether the slot returned, failed or threw -------------------------------------------------------------------------------------

test('A_slot_that_throws_with_its_folder_still_open_says_so_in_the_error_it_throws', async () => {
  const w = await sealedClosed();
  const warnings = [];
  await assert.rejects(
    withSlotHeldout({ task: w.task, tasks: [w.task], heldout: { ...heldoutOf(w), warn: line => warnings.push(line) }, fn: async () => { rmSync(w.backupFile); throw new Error('the slot broke'); } }),
    error => { assert.match(error.message, /the slot broke/, 'the slot\'s own error is not hidden'); assert.match(error.message, /left open after the slot.*heldout-close --task 04/s, `and the folder is named: ${error.message}`); return true; },
  );
  assert.equal(existsSync(casesOf(w)), true, 'the folder really is still there (the backup could not be verified for its removal)');
});

test('An_open_that_fails_after_the_folder_was_placed_says_the_folder_is_left_open_and_the_slot_did_not_run', async () => {
  const w = await sealedClosed();
  let ran = false;
  const stamp = path.join(taskPaths(w.task, w.home).heldoutDir, '.open');
  const hooks = { beforeRename: () => writeFileSync(stamp, '{"operator":true}\n') };
  const refused = await withSlotHeldout({ task: w.task, tasks: [w.task], heldout: { ...heldoutOf(w), hooks }, fn: async () => { ran = true; } });
  assert.equal(refused.ok, false, 'a stamp that appeared after the check refuses the open');
  assert.equal(ran, false, 'the slot did not run');
  assert.equal(existsSync(casesOf(w)), true, 'but the folder was placed and is in clear');
  assert.match(refused.reason, /already exists/, 'the reason of the open is kept');
  assert.match(refused.reason, /left open after the slot.*heldout-close --task 04/s, `and the folder is named: ${refused.reason}`);
  assert.equal(refused.result, undefined, 'it is no result of a slot');
});
