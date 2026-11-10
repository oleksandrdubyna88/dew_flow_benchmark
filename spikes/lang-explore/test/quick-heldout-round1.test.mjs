// Bench PR 4a, code round 1: the twelve accepted findings, each with its control. The registry is read and written under ONE registry lock held inside the per-task lock; a task with an entry is sealed even when its folder is
// gone, and an unreadable registry is a refusal everywhere; a stamp is removed only after its folder and judged when the folder is already absent; the key's backup copy is published whole or not at all; a malformed private backup
// file blocks only the quick tasks; the config folder's owner and mode are checked; `findmnt`'s first line is the mount; the warning of a folder left open names its remedy; `--tasks` rejects empty entries; and the two known gaps
// of the first report (the no-op seal re-verifies the backup; the olympiad guard of the reader) are proved. Invented keys, folders and shares only.
import '../../minigit-bench/test/fake-home.mjs';
import assert from 'node:assert/strict';
import * as nodeFs from 'node:fs';
import { chmodSync, existsSync, mkdirSync, readFileSync, readdirSync, rmSync, statSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { readKey } from '../../minigit-bench/lib/heldout-crypto.mjs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { main } from '../explore.mjs';
import { backupConfigFile, readBackupConfig } from '../lib/heldout-backup-config.mjs';
import { makeBackup, makeMountOf } from '../lib/heldout-key.mjs';
import { readerHeldout } from '../lib/heldout-reader.mjs';
import { entryOfSeal, writeEntry } from '../lib/heldout-registry.mjs';
import { closeClearFolder, openClearFolder, sealPackage, sweepClearFolder, withOpenedHeldout } from '../lib/package-seal.mjs';
import { resolveTask } from '../lib/tasks.mjs';
import { tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { EXPLORE, writeFrozen } from './quick-leak-fixture.mjs';
import { sealedClosed, world } from './quick-heldout-fixture.mjs';

const stampOf = w => path.join(w.heldoutDir, '.open');
const plant = (w, body) => writeFileSync(stampOf(w), `${JSON.stringify({ at: '2026-11-09T08:00:00.000Z', ...body })}\n`);
const fsWith = over => ({ ...Object.fromEntries(Object.entries(nodeFs).filter(([, v]) => typeof v === 'function')), ...over });
const entryFor = n => entryOfSeal({ archive: `heldout-0${n}/suite.enc`, archiveSha256: 'a'.repeat(64), treeSha256: 'b'.repeat(64), caseCount: 3, trapCount: 1, manifestSha256: 'c'.repeat(64), keySha256: 'd'.repeat(64), sealedOn: '2026-11-09' });

// ---- 1: the registry lock -------------------------------------------------------------------------------------------------------------------------------------------------

test('Two_seals_of_different_tasks_interleaved_at_the_registry_both_keep_their_entry', async () => {
  const a = world();
  const b = world({ taskId: '05', over: { frozenFile: a.frozenFile } });
  let competing;
  const seen = [];
  const hooks = { afterRegistryRead: () => {
    seen.push([existsSync(path.join(a.heldoutDir, '.clear.lock')), existsSync(`${a.frozenFile}.lock`)]);
    competing ??= writeEntry({ frozenFile: a.frozenFile, task: b.task, entry: entryFor(5), lock: { retry: { tries: 2, sleepMs: 1 } } });
  } };
  const r = await sealPackage({ ...a.args, hooks });
  assert.equal(r.ok, true, r.reason);
  assert.equal(competing.ok, false, 'the other seal\'s write ran while this one had read the record and was refused by the registry lock');
  assert.ok(/heldout-frozen\.json\.lock/.test(competing.reason), competing.reason);
  assert.deepEqual(seen, [[true, true]], 'the per-task lock is held first, and the registry lock inside it');
  assert.equal(writeEntry({ frozenFile: a.frozenFile, task: b.task, entry: entryFor(5) }).ok, true, 'afterwards the other seal writes its entry');
  assert.deepEqual(Object.keys(JSON.parse(readFileSync(a.frozenFile, 'utf8')).tasks), ['04', '05'], 'both entries survive');
  assert.deepEqual(readdirSync(path.dirname(a.frozenFile)), ['heldout-frozen.json'], 'no lock, guard or temporary file is left');
});

// ---- 2: sealState fails closed ----------------------------------------------------------------------------------------------------------------------------------------------

test('A_task_with_an_entry_whose_folder_is_absent_is_a_refusal_naming_the_task_and_the_archive', async () => {
  const w = await sealedClosed();
  const archive = path.join(w.heldoutDir, 'suite.enc');
  rmSync(w.heldoutDir, { recursive: true });
  let ran = 0;
  const answers = [await sweepClearFolder(w.args), await openClearFolder(w.args), await closeClearFolder(w.args), await withOpenedHeldout({ ...w.args, fn: () => { ran += 1; } }), await readerHeldout({ home: w.home, task: w.task, heldout: w.args })];
  for (const r of answers) {
    assert.equal(r?.ok, false, 'a task with an entry is sealed even when heldout-NN/ is gone: never "unsealed", never "nothing to do"');
    assert.ok(r.reason.includes('task 04') && r.reason.includes(archive), r.reason);
  }
  assert.equal(ran, 0, 'the reader never ran');
  const control = await sealedClosed();
  assert.equal((await sweepClearFolder(control.args)).ok, true, 'control: the folder is there');
});

test('An_unreadable_registry_is_a_refusal_everywhere_and_a_missing_one_is_an_unsealed_task', async () => {
  const w = world({ taskId: '05' });
  writeFileSync(w.frozenFile, '{ not json');
  let ran = 0;
  const answers = [await sweepClearFolder(w.args), await openClearFolder(w.args), await closeClearFolder(w.args), await withOpenedHeldout({ ...w.args, fn: () => { ran += 1; } })];
  for (const r of answers) {
    assert.equal(r?.ok, false);
    assert.ok(r.reason.includes('heldout-frozen.json') && /cannot be read/.test(r.reason), r.reason);
  }
  assert.equal(ran, 0, 'fn never ran');
  rmSync(w.frozenFile);
  assert.equal((await sweepClearFolder(w.args)).value.cases, 'unsealed', 'control: no record file is an unsealed task');
  assert.equal(await withOpenedHeldout({ ...w.args, fn: () => 'ran' }), 'ran');
});

// ---- 3: the stamp and the folder ---------------------------------------------------------------------------------------------------------------------------------------------

test('A_stamp_with_no_folder_is_judged_a_dead_process_is_dropped_a_live_one_and_the_operator_are_not', async () => {
  const dead = await sealedClosed();
  assert.equal((await openClearFolder(dead.args)).ok, true);
  rmSync(dead.casesDir, { recursive: true });
  plant(dead, { pid: 4242, startTime: 'gone' });
  assert.equal((await sweepClearFolder({ ...dead.args, isAlive: () => false })).ok, true);
  assert.equal(existsSync(stampOf(dead)), false, 'a dead process\'s stamp is dropped');
  const live = await sealedClosed();
  assert.equal((await openClearFolder(live.args)).ok, true);
  rmSync(live.casesDir, { recursive: true });
  plant(live, { pid: 4242, startTime: 'running' });
  const kept = await sweepClearFolder({ ...live.args, isAlive: () => true });
  assert.equal(kept.ok, false);
  assert.ok(kept.reason.includes(stampOf(live)) && /pid 4242/.test(kept.reason), kept.reason);
  assert.equal(existsSync(stampOf(live)), true, 'a live process\'s stamp is never dropped');
  const operator = await sealedClosed();
  assert.equal((await openClearFolder(operator.args)).ok, true);
  rmSync(operator.casesDir, { recursive: true });
  const held = await sweepClearFolder(operator.args);
  assert.equal(held.ok, false, 'an operator hold is never dropped by an entry point');
  assert.ok(held.reason.includes(stampOf(operator)));
  assert.equal((await closeClearFolder(operator.args)).ok, true, 'the operator\'s own close drops it');
  assert.equal(existsSync(stampOf(operator)), false);
});

test('The_stamp_is_removed_only_after_the_folder_is_gone_and_a_crash_between_the_two_leaves_a_stamp_the_next_run_judges', async () => {
  const w = await sealedClosed();
  assert.equal((await openClearFolder(w.args)).ok, true);
  const atStampRemoval = [];
  const crashing = fsWith({ rmSync: (file, ...rest) => {
    if (file === stampOf(w)) { atStampRemoval.push(existsSync(w.casesDir)); throw new Error('killed between the two removals'); }
    return nodeFs.rmSync(file, ...rest);
  } });
  await assert.rejects(closeClearFolder({ ...w.args, fs: crashing }), /killed between/);
  assert.deepEqual(atStampRemoval, [false], 'cases/ was gone when the stamp was about to be removed');
  assert.deepEqual([existsSync(w.casesDir), existsSync(stampOf(w))], [false, true]);
  assert.equal(existsSync(path.join(w.heldoutDir, '.clear.lock')), false, 'the lock was released');
  plant(w, { pid: 4242, startTime: 'gone' });
  assert.equal((await sweepClearFolder({ ...w.args, isAlive: () => false })).ok, true, 'the next run judges the stamp: its process is dead');
  assert.equal(existsSync(stampOf(w)), false);
});

// ---- 4: the backup copy is atomic -------------------------------------------------------------------------------------------------------------------------------------------

const backupArgs = w => ({ task: w.task, key: readKey(w.keyFile).value, backupFile: w.backupFile, shareMount: w.shareMount, mountOf: w.args.mountOf });
const tempsOf = w => readdirSync(path.dirname(w.backupFile)).filter(n => n.endsWith('.tmp'));

test('The_backup_copy_is_written_to_a_temporary_name_and_published_whole_or_not_at_all', async () => {
  const w = world();
  const crashing = fsWith({ linkSync: () => { throw new Error('killed before the link'); } });
  await assert.rejects(makeBackup({ ...backupArgs(w), fs: crashing }), /killed before the link/);
  assert.equal(existsSync(w.backupFile), false, 'no half-written copy is ever at the backup\'s name');
  const [temp] = tempsOf(w);
  assert.match(temp, /^\.heldout\.key\.bak\.\d+\.\d+\.[0-9a-f]{8}\.tmp$/, 'an owned temporary name beside the copy');
  const next = await makeBackup({ ...backupArgs(w), isAlive: () => false });
  assert.equal(next.ok, true, next.reason);
  assert.deepEqual(tempsOf(w), [], 'the next run removes a temporary file whose owner is dead');
  assert.deepEqual(readFileSync(w.backupFile), readKey(w.keyFile).value);
  assert.equal(statSync(w.backupFile).mode & 0o777, 0o600);
});

test('A_temporary_copy_of_a_live_owner_is_kept_and_an_existing_equal_copy_is_accepted_and_a_different_one_is_refused', async () => {
  const w = world();
  const liveTemp = path.join(path.dirname(w.backupFile), '.heldout.key.bak.4242.123456.deadbeef.tmp');
  writeFileSync(liveTemp, 'partial');
  assert.equal((await makeBackup({ ...backupArgs(w), isAlive: () => true })).ok, true);
  assert.equal(existsSync(liveTemp), true, 'a running owner\'s temporary file is not removed');
  const mtime = statSync(w.backupFile).mtimeMs;
  assert.equal((await makeBackup(backupArgs(w))).ok, true, 'resume: a copy that hashes to the key is accepted');
  assert.equal(statSync(w.backupFile).mtimeMs, mtime, 'and not touched');
  writeFileSync(w.backupFile, 'a different key copy');
  const refused = await makeBackup(backupArgs(w));
  assert.equal(refused.ok, false);
  assert.ok(refused.reason.includes(w.backupFile) && /sha256/.test(refused.reason), refused.reason);
  assert.equal(readFileSync(w.backupFile, 'utf8'), 'a different key copy', 'never overwritten');
});

// ---- 5: a malformed private file blocks only the quick tasks ----------------------------------------------------------------------------------------------------------------------

test('A_malformed_backup_file_blocks_a_quick_task_and_an_olympiad_task_runs_as_before', async () => {
  const home = tmpDir('q-home-badfile');
  const file = backupConfigFile(home);
  mkdirSync(path.dirname(file), { recursive: true, mode: 0o700 });
  writeFileSync(file, '{ not json', { mode: 0o600 });
  const say = task => { const said = []; return main(['init', '--task', task], { home, say: l => said.push(l) }).then(code => ({ code, said: said.join('\n') })); };
  const quick = await say('04');
  assert.equal(quick.code, 4);
  assert.match(quick.said, /heldout-backup\.json is not valid JSON/);
  const olympiad = await say('02');
  assert.doesNotMatch(olympiad.said, /heldout-backup\.json/, 'the olympiad task is refused, if at all, for its own reasons');
  assert.ok(resolveTask(EXPLORE, '02').ok);
});

// ---- 6: the config folder ---------------------------------------------------------------------------------------------------------------------------------------------------

test('The_config_folder_must_be_the_operators_and_not_writable_by_group_or_others', () => {
  const home = tmpDir('q-home-config');
  const file = backupConfigFile(home);
  const dir = path.dirname(file);
  mkdirSync(dir, { recursive: true, mode: 0o700 });
  writeFileSync(file, JSON.stringify({ keyBackup: '/x/k', shareMount: '/x' }), { mode: 0o600 });
  assert.equal(readBackupConfig(home).ok, true, 'control: owned by the operator, mode 700');
  chmodSync(dir, 0o770);
  const open = readBackupConfig(home);
  assert.equal(open.ok, false);
  assert.ok(open.reason.includes(dir), open.reason);
  chmodSync(dir, 0o700);
  const other = readBackupConfig(home, { uid: process.getuid() + 1 });
  assert.equal(other.ok, false, 'a folder of another uid is refused');
  assert.ok(other.reason.includes(dir));
});

// ---- 7: findmnt's first line ------------------------------------------------------------------------------------------------------------------------------------------------

test('Findmnt_gives_the_first_line_as_the_mount_and_an_empty_first_line_is_refused', async () => {
  const run = stdout => makeMountOf(async () => ok({ code: 0, stdout, stderr: '' }));
  assert.deepEqual(await run('/mnt/share\n/mnt/other\n')('/x'), ok('/mnt/share'), 'two lines: the first is the mount');
  assert.equal((await run('\n/mnt/other\n')('/x')).ok, false, 'an empty first line is not a mount');
  assert.equal((await run('  \n')('/x')).ok, false);
  const seen = [];
  await makeMountOf(async (exe, args) => { seen.push(args); return ok({ code: 0, stdout: '/', stderr: '' }); })('/x');
  assert.deepEqual(seen[0].slice(0, 3), ['--noheadings', '--output', 'TARGET']);
});

// ---- 8: the warning of a folder left open -----------------------------------------------------------------------------------------------------------------------------------

test('A_reader_that_cannot_close_its_folder_says_the_remedy', async () => {
  const w = await sealedClosed();
  const said = [];
  const r = await withOpenedHeldout({ ...w.args, mountOf: async () => fail('findmnt: not found'), warn: line => said.push(line), fn: () => 'value' });
  assert.equal(r, 'value');
  assert.equal(existsSync(w.casesDir), true, 'the folder was left');
  assert.equal(said.length, 1);
  assert.ok(said[0].includes('run `explore.mjs heldout-close --task 04` with the key backup named (flag or the private file)'), said[0]);
});

// ---- 9: --tasks syntax ------------------------------------------------------------------------------------------------------------------------------------------------------

test('Heldout_check_rejects_an_empty_entry_of_tasks_before_it_resolves_any', async () => {
  const w = await sealedClosed();
  let resolved = 0;
  for (const tasks of ['04,,05', '04,', ',04', '04, ,05']) {
    const said = [];
    const code = await main(['heldout-check', '--tasks', tasks], { home: w.home, frozenFile: w.frozenFile, say: l => said.push(l), exploreFn: () => { resolved += 1; return ok(EXPLORE); } });
    assert.equal(code, 4, JSON.stringify(tasks));
    assert.match(said.join('\n'), /--tasks is NN,NN: .* an empty entry/, JSON.stringify(tasks));
  }
  assert.equal(resolved, 0, 'the pins were never read');
  const none = [];
  assert.equal(await main(['heldout-check', '--tasks', ' '], { home: w.home, say: l => none.push(l) }), 4);
  assert.match(none.join('\n'), /needs --tasks NN,NN/);
  const said = [];
  assert.equal(await main(['heldout-check', '--tasks', '04, 05', '--key-file', w.keyFile], { home: w.home, frozenFile: w.frozenFile, say: l => said.push(l), heldout: { mountOf: w.args.mountOf } }), 4, 'control: a well-formed list is read (05 has no entry here)');
  assert.match(said.join('\n'), /task 04: ok/);
});

// ---- 11: the no-op seal re-verifies the backup ---------------------------------------------------------------------------------------------------------------------------------

test('A_second_seal_with_an_equal_entry_still_verifies_the_key_backup_before_it_returns', async () => {
  const w = world();
  assert.equal((await sealPackage(w.args)).ok, true);
  rmSync(w.backupFile);
  const gone = await sealPackage(w.args);
  assert.equal(gone.ok, false, 'the no-op may not say "done" over a key whose backup is gone');
  assert.match(gone.reason, /the key backup cannot be verified/);
  writeFileSync(w.backupFile, readKey(w.keyFile).value, { mode: 0o600 });
  const again = await sealPackage(w.args);
  assert.equal(again.ok, true, again.reason);
  assert.equal(again.value.state, 'unchanged');
});

// ---- 12: the olympiad guard of the reader --------------------------------------------------------------------------------------------------------------------------------------

test('An_olympiad_task_takes_no_lock_in_the_reader_and_reads_as_before', async () => {
  const home = world().home;
  const task = resolveTask(EXPLORE, '02').value;
  const dir = path.join(home, 'rsd', 'lang-v2', 'explore', 'heldout-02');
  mkdirSync(dir, { recursive: true });
  writeFileSync(path.join(dir, 'suite.enc'), 'looks like an archive');
  const locks = [];
  const heldout = { frozenFile: writeFrozen(['02']), hooks: { afterLock: () => locks.push(1) } };
  const r = await readerHeldout({ home, task, heldout });
  assert.equal(r.absent, true, 'the olympiad loader answers as it always did (no manifest.json: absent)');
  assert.deepEqual(locks, [], 'and no lock was taken');
  assert.deepEqual(readdirSync(dir), ['suite.enc']);
});
