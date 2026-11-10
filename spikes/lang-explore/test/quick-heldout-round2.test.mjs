// Bench PR 4a, code round 2: five accepted findings, each with its control. A clear folder is removed crash-safely (renamed to an owned removing-folder first, so a crash never leaves a half-deleted `cases/` that reads as "changed
// after sealing"); the backup is read through ONE handle (O_NOFOLLOW, fstat, the bytes of that descriptor); a backup folder that vanishes is a refusal, never an uncaught ENOENT; a stamp that already exists is a refusal naming it and
// its owner; the open verb's hint prints the resolved backup paths. Invented keys, folders and shares only.
import '../../minigit-bench/test/fake-home.mjs';
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import * as nodeFs from 'node:fs';
import { existsSync, readFileSync, readdirSync, rmSync, symlinkSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { readKey } from '../../minigit-bench/lib/heldout-crypto.mjs';
import { main } from '../explore.mjs';
import { backupProblem, makeBackup } from '../lib/heldout-key.mjs';
import { closeClearFolder, openClearFolder, sweepClearFolder } from '../lib/package-seal.mjs';
import { sealedClosed, world } from './quick-heldout-fixture.mjs';

const fsWith = over => ({ ...Object.fromEntries(Object.entries(nodeFs).filter(([, v]) => typeof v === 'function')), ...over });
const sha = bytes => createHash('sha256').update(bytes).digest('hex');
const stampOf = w => path.join(w.heldoutDir, '.open');
const removingOf = w => readdirSync(w.heldoutDir).filter(n => /^testsToDelete-.*-bench-removing-/.test(n) && !n.endsWith('.owner'));
const claimsOf = w => readdirSync(w.heldoutDir).filter(n => /^testsToDelete-.*-bench-removing-.*\.owner$/.test(n));

// ---- 1: crash-safe removal ---------------------------------------------------------------------------------------------------------------------------------------------------

test('A_crash_after_the_rename_or_midway_through_the_removal_never_leaves_a_cases_folder_and_the_next_sweep_finishes_it', async () => {
  for (const [label, crash] of [
    ['after the rename', () => fsWith({ rmSync: () => { throw new Error('killed after the rename'); } })],
    ['midway through the removal', () => fsWith({ rmSync: target => { rmSyncOne(target); throw new Error('killed midway'); } })],
  ]) {
    const w = await sealedClosed();
    assert.equal((await openClearFolder(w.args)).ok, true);
    const [archive, record] = [readFileSync(path.join(w.heldoutDir, 'suite.enc')), readFileSync(w.frozenFile)];
    const crashed = await closeClearFolder({ ...w.args, fs: crash() });
    assert.equal(crashed.ok, false, `${label}: the removal did not finish`);
    assert.equal(existsSync(w.casesDir), false, `${label}: no cases/ is left, whole or half: it was renamed away before anything was deleted`);
    assert.equal(removingOf(w).length, 1, `${label}: the owned removing-folder is there for the next lock holder`);
    const next = await sweepClearFolder({ ...w.args, isAlive: () => false });
    assert.equal(next.ok, true, `${label}: the next sweep never hashes it as cases/ (${next.reason})`);
    assert.doesNotMatch(String(next.reason), /changed after sealing/);
    assert.deepEqual([removingOf(w), claimsOf(w)], [[], []], `${label}: the removal is finished, claim included`);
    assert.equal(existsSync(stampOf(w)), false, `${label}: finishing the killed removal ends with its stamp`);
    assert.deepEqual([readFileSync(path.join(w.heldoutDir, 'suite.enc')), readFileSync(w.frozenFile)], [archive, record], `${label}: the archive and the registry are untouched`);
  }
});

/** Remove one file of a tree (a removal killed after it began). */
function rmSyncOne(target) {
  const [first] = readdirSync(target);
  if (first !== undefined) rmSync(path.join(target, first), { force: true });
}

test('A_removal_that_finishes_leaves_no_removing_folder_and_no_claim_and_the_stamp_goes_last', async () => {
  const w = await sealedClosed();
  assert.equal((await openClearFolder(w.args)).ok, true);
  const at = [];
  const watching = fsWith({ rmSync: (file, ...rest) => { if (file === stampOf(w)) at.push([existsSync(w.casesDir), removingOf(w).length]); return nodeFs.rmSync(file, ...rest); } });
  assert.equal((await closeClearFolder({ ...w.args, fs: watching })).ok, true);
  assert.deepEqual(at, [[false, 0]], 'when the stamp is removed, cases/ and the removing-folder are gone');
  assert.deepEqual([removingOf(w), claimsOf(w), existsSync(stampOf(w))], [[], [], false]);
});

// ---- 2: one handle -----------------------------------------------------------------------------------------------------------------------------------------------------------

test('The_backup_is_read_through_one_handle_opened_without_following_a_link_and_a_link_in_its_place_is_refused', async () => {
  const w = await sealedClosed();
  const key = readKey(w.keyFile).value;
  const calls = [];
  const spying = fsWith({
    openSync: (file, flags, ...rest) => { calls.push(['open', file, flags]); return nodeFs.openSync(file, flags, ...rest); },
    lstatSync: (...a) => { calls.push(['lstat', a[0]]); return nodeFs.lstatSync(...a); },
    readFileSync: (...a) => { calls.push(['readFile', a[0]]); return nodeFs.readFileSync(...a); },
    fstatSync: (...a) => { calls.push(['fstat']); return nodeFs.fstatSync(...a); },
  });
  const ask = over => backupProblem({ task: w.task, backupFile: w.backupFile, shareMount: w.shareMount, keySha256: sha(key), mountOf: w.args.mountOf, fs: spying, ...over });
  assert.equal(await ask(), null, 'control: the equal copy');
  const mine = calls.filter(c => c[1] === w.backupFile || c[0] === 'fstat');
  assert.deepEqual(mine.map(c => c[0]), ['open', 'fstat'], 'the copy is opened once and that descriptor is examined: no lstat and no second open or read by path');
  assert.equal((mine[0][2] & nodeFs.constants.O_NOFOLLOW) !== 0, true, 'opened with O_NOFOLLOW');
  const real = `${w.backupFile}.real`;
  nodeFs.renameSync(w.backupFile, real);
  symlinkSync(real, w.backupFile);
  const linked = await ask({ fs: nodeFs });
  assert.match(linked, /the key backup cannot be verified/, 'a link put in place of the copy is refused, though what it points at hashes right');
});

// ---- 3: a vanished backup folder ---------------------------------------------------------------------------------------------------------------------------------------------

test('A_backup_folder_that_is_gone_when_the_stale_copies_are_looked_for_is_a_refusal_naming_it', async () => {
  const w = world();
  const key = readKey(w.keyFile).value;
  const vanishing = fsWith({ readdirSync: () => { throw Object.assign(new Error('ENOENT: no such file or directory'), { code: 'ENOENT' }); } });
  const r = await makeBackup({ task: w.task, key, backupFile: w.backupFile, shareMount: w.shareMount, mountOf: w.args.mountOf, fs: vanishing });
  assert.equal(r.ok, false);
  assert.ok(r.reason.includes(path.dirname(w.backupFile)), r.reason);
  assert.equal(existsSync(w.backupFile), false, 'nothing was written');
  const control = await makeBackup({ task: w.task, key, backupFile: w.backupFile, shareMount: w.shareMount, mountOf: w.args.mountOf });
  assert.equal(control.ok, true, control.reason);
});

// ---- 4: a stamp that exists --------------------------------------------------------------------------------------------------------------------------------------------------

test('A_stamp_that_appears_before_the_open_writes_its_own_is_a_refusal_naming_the_stamp_and_its_owner_never_an_overwrite', async () => {
  const w = await sealedClosed();
  const other = `${JSON.stringify({ pid: 4242, startTime: 'running', at: '2026-11-09T08:00:00.000Z' })}\n`;
  const r = await openClearFolder({ ...w.args, hooks: { beforeRename: () => writeFileSync(stampOf(w), other) }, isAlive: () => true });
  assert.equal(r.ok, false, 'a refusal value, not a crash');
  assert.ok(r.reason.includes(stampOf(w)) && /pid 4242/.test(r.reason), r.reason);
  assert.equal(readFileSync(stampOf(w), 'utf8'), other, 'the stamp that was there is never overwritten');
  const control = await sealedClosed();
  assert.equal((await openClearFolder(control.args)).ok, true, 'control: no stamp in the way');
});

// ---- 5: the open verb's hint ------------------------------------------------------------------------------------------------------------------------------------------------

test('The_open_hint_prints_the_resolved_backup_paths_when_they_are_known_and_the_placeholders_when_they_are_not', async () => {
  const w = await sealedClosed();
  const say = [];
  const seams = { home: w.home, frozenFile: w.frozenFile, say: l => say.push(l), heldout: { mountOf: w.args.mountOf } };
  assert.equal(await main(['heldout-open', '--task', '04', '--key-file', w.keyFile, '--key-backup', w.backupFile, '--share-mount', w.shareMount], seams), 0);
  assert.ok(say.join('\n').includes(`heldout-close --task 04 --key-backup ${w.backupFile} --share-mount ${w.shareMount}`), say.join('\n'));
  assert.equal(await main(['heldout-close', '--task', '04', '--key-backup', w.backupFile, '--share-mount', w.shareMount], seams), 0);
  say.length = 0;
  assert.equal(await main(['heldout-open', '--task', '04', '--key-file', w.keyFile], seams), 0);
  assert.ok(say.join('\n').includes('heldout-close --task 04 --key-backup <file> --share-mount <dir>'), 'none named: the placeholders');
});
