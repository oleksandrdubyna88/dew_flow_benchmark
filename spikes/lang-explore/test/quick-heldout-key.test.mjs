// The archive key and its backup (bench PR 4a): the key is read by `readKey` and held in memory only; only its sha256 is ever written; before a clear case may be removed, the backup copy of the key is on the share's mount
// (`findmnt` through a seam, as a program with an argument list), is a regular file and hashes to the entry's `keySha256`. Invented keys in owned folders; the real key, share and `findmnt` answers of the operator are never read.
import '../../minigit-bench/test/fake-home.mjs';
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { chmodSync, existsSync, readFileSync, readdirSync, realpathSync, rmSync, symlinkSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { generateKey, readKey } from '../../minigit-bench/lib/heldout-crypto.mjs';
import { sealTree } from '../../minigit-bench/lib/heldout-seal.mjs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { backupProblem, keyProblem, makeMountOf, mountOf } from '../lib/heldout-key.mjs';
import { checkHeldout } from '../lib/heldout-check.mjs';
import { closeClearFolder, openClearFolder, sweepClearFolder } from '../lib/package-seal.mjs';
import { mountFor, sealedClosed, world } from './quick-heldout-fixture.mjs';

const sha = bytes => createHash('sha256').update(bytes).digest('hex');

test('The_real_findmnt_seam_runs_as_a_program_with_an_argument_list_and_names_the_root_mount_for_the_root_folder', async () => {
  const real = await mountOf('/');
  assert.deepEqual(real, ok('/'), 'the one real call: the root folder is on the root mount');
  const calls = [];
  const seam = makeMountOf(async (exe, args, options) => { calls.push({ exe, args, options }); return ok({ code: 0, stdout: '/mnt/invented-share\n', stderr: '' }); });
  const dir = realpathSync(tmpDir('q-mount'));
  assert.deepEqual(await seam(dir), ok('/mnt/invented-share'));
  assert.equal(calls.length, 1);
  assert.deepEqual([calls[0].exe, calls[0].args], ['findmnt', ['--noheadings', '--output', 'TARGET', '--target', dir]], 'an executable and an argument list, no shell string');
  assert.ok(Number.isFinite(calls[0].options.timeoutMs) && calls[0].options.timeoutMs > 0, 'a timeout is set');
  assert.equal(calls[0].options.shell, undefined);
  const failing = makeMountOf(async () => ok({ code: 1, stdout: '', stderr: 'findmnt: no such target' }));
  assert.equal((await failing(dir)).ok, false, 'a non-zero exit is a fail value');
  const missing = makeMountOf(async () => fail('spawn findmnt ENOENT'));
  assert.equal((await missing(dir)).ok, false, 'a program that is not there is a fail value');
  assert.equal((await makeMountOf(async () => ok({ code: 0, stdout: '\n', stderr: '' }))(dir)).ok, false, 'an empty answer is not a mount');
});

test('The_backup_check_refuses_a_missing_a_differing_and_a_misplaced_copy_and_passes_an_equal_one_on_the_mount', async () => {
  const w = world();
  const key = readKey(w.keyFile).value;
  const ask = (over = {}) => backupProblem({ task: w.task, backupFile: w.backupFile, shareMount: w.shareMount, keySha256: sha(key), mountOf: w.args.mountOf, ...over });
  assert.match(await ask(), /the key backup cannot be verified: .*heldout\.key\.bak.*mount or restore it, then run again.*task 04/, 'the file is missing');
  writeFileSync(w.backupFile, key, { mode: 0o600 });
  assert.equal(await ask(), null, 'control: an equal copy on the mount');
  const bent = Buffer.from(key);
  bent[0] ^= 1;
  writeFileSync(w.backupFile, bent);
  assert.match(await ask(), /cannot be verified/, 'a copy that differs');
  writeFileSync(w.backupFile, key);
  const elsewhere = await ask({ mountOf: mountFor('/mnt/another-share') });
  assert.match(elsewhere, /the key backup folder is not on the mounted share: .* resolves to the mount \/, expected /);
  assert.match(await ask({ backupFile: undefined }), /cannot be verified.*no backup file is named/, 'no default path in the code');
  assert.match(await ask({ shareMount: undefined }), /cannot be verified.*no share mount is named/, 'no default mount in the code');
  const link = path.join(w.shareMount, 'link.bak');
  symlinkSync(w.backupFile, link);
  assert.match(await ask({ backupFile: link }), /cannot be verified/, 'a link is not a regular file');
  const out = tmpDir('q-outside');
  symlinkSync(out, path.join(w.shareMount, 'sneaky'));
  const seen = [];
  await ask({ backupFile: path.join(w.shareMount, 'sneaky', 'k.bak'), mountOf: async d => { seen.push(d); return ok('/'); } });
  assert.deepEqual(seen, [realpathSync(out)], 'the mount is asked about the REAL path of the backup folder, so a link into the share proves nothing');
});

test('A_key_whose_sha256_differs_from_the_entry_is_refused_and_the_remedy_names_the_key_file_option', () => {
  const w = world();
  const key = readKey(w.keyFile).value;
  assert.equal(keyProblem({ keyFile: w.keyFile, keySha256: sha(key) }), null);
  assert.match(keyProblem({ keyFile: w.keyFile, keySha256: 'c'.repeat(64) }), /the key is not the one the archive was sealed with/);
  const gone = path.join(tmpDir('q-nokey'), 'heldout.key');
  assert.match(keyProblem({ keyFile: gone, keySha256: sha(key) }), /--key-file/, 'the remedy tells the operator the option, not v1\'s tool');
  const loose = path.join(tmpDir('q-loosekey'), 'heldout.key');
  writeFileSync(loose, key, { mode: 0o600 });
  chmodSync(loose, 0o640);
  assert.match(keyProblem({ keyFile: loose, keySha256: sha(key) }), /mode/);
  assert.equal(JSON.stringify(keyProblem({ keyFile: w.keyFile, keySha256: 'c'.repeat(64) })).includes(key.toString('hex')), false, 'the key is never in a sentence');
});

// ---- the key through open, check, close and the sweep ---------------------------------------------------------------------------------------------------------------------
const entryFile = w => JSON.parse(readFileSync(w.frozenFile, 'utf8')).tasks['04'];
const tempsIn = dir => readdirSync(dir).filter(n => /^testsToDelete-|\.owner$/.test(n));
const newKey = () => { const file = path.join(tmpDir('q-key-b'), 'heldout.key'); generateKey(file); return file; };

test('The_registry_entry_holds_the_keys_sha256_and_never_its_bytes', async () => {
  const w = world();
  const key = readKey(w.keyFile).value;
  const heldLock = [];
  const hooks = { afterLock: () => heldLock.push(readFileSync(path.join(w.heldoutDir, '.clear.lock'), 'latin1')) };
  const sealed = await sealPackageWith(w, hooks);
  assert.equal(sealed.ok, true, sealed.reason);
  rmSync(w.casesDir, { recursive: true });
  assert.equal((await openClearFolder({ ...w.args, hooks })).ok, true);
  assert.equal((await checkHeldout({ ...w.args, hooks })).ok, true);
  const stamp = readFileSync(path.join(w.heldoutDir, '.open'), 'latin1');
  assert.equal((await closeClearFolder({ ...w.args, hooks })).ok, true);
  const written = [readFileSync(w.frozenFile, 'latin1'), readFileSync(path.join(w.heldoutDir, 'suite.enc'), 'latin1'), stamp, ...heldLock].join('\n');
  for (const [form, text] of [['raw', key.toString('latin1')], ['hex', key.toString('hex')], ['base64', key.toString('base64')]]) assert.equal(written.includes(text), false, `the key's ${form} bytes are in nothing the module wrote`);
  assert.equal(heldLock.length >= 3, true, 'the lock was read while held, in the seal, the open and the close');
  assert.equal(entryFile(w).keySha256, sha(key));
  assert.deepEqual(tempsIn(w.heldoutDir), []);
});

async function sealPackageWith(w, hooks) {
  const { sealPackage } = await import('../lib/package-seal.mjs');
  return sealPackage({ ...w.args, hooks });
}

test('heldout_check_and_heldout_open_refuse_a_key_whose_sha256_differs_from_the_entry_before_any_decryption', async () => {
  const w = await sealedClosed();
  const b = newKey();
  const unsealed = w.spies.unseal;
  for (const [name, run] of [['open', () => openClearFolder({ ...w.args, keyFile: b })], ['check', () => checkHeldout({ ...w.args, keyFile: b })]]) {
    const r = await run();
    assert.equal(r.ok, false, name);
    assert.match(r.reason, /the key is not the one the archive was sealed with/, name);
    assert.match(r.reason, /task 04/, `${name} names the task`);
  }
  assert.equal(w.spies.unseal, unsealed, 'the decryption seam was not called');
  assert.equal(existsSync(w.casesDir), false, 'no cases/');
  assert.deepEqual(tempsIn(w.heldoutDir), [], 'no temporary folder');
  assert.equal((await openClearFolder(w.args)).ok, true, 'control: key A opens');
  assert.equal((await checkHeldout(w.args)).ok, true, 'control: key A checks');
  assert.equal(w.spies.unseal > unsealed, true);
});

test('A_key_file_that_is_missing_or_not_mode_600_is_refused', async () => {
  const w = await sealedClosed();
  const key = readKey(w.keyFile).value;
  const dir = path.dirname(w.keyFile);
  const loose = path.join(dir, 'loose.key');
  writeFileSync(loose, key, { mode: 0o600 });
  chmodSync(loose, 0o640);
  const link = path.join(dir, 'link.key');
  symlinkSync(w.keyFile, link);
  const unsealed = w.spies.unseal;
  for (const [label, keyFile, why] of [['missing', path.join(dir, 'absent.key'), /no key file/], ['mode 640', loose, /mode 640/], ['a link', link, /not a regular file/]]) {
    for (const [name, run] of [['open', () => openClearFolder({ ...w.args, keyFile })], ['check', () => checkHeldout({ ...w.args, keyFile })]]) {
      const r = await run();
      assert.equal(r.ok, false, `${label} ${name}`);
      assert.match(r.reason, why, `${label} ${name}`);
    }
  }
  assert.equal(w.spies.unseal, unsealed, 'the decryption seam was never reached');
  assert.equal((await openClearFolder(w.args)).ok, true, 'control: the 600 regular file in a 700 folder');
});

test('No_clear_case_is_removed_while_the_keys_backup_copy_is_missing_or_differs', async () => {
  const unverified = /the key backup cannot be verified: .*heldout\.key\.bak.*task 04/;
  const states = [
    ['missing', w => rmSync(w.backupFile), unverified, {}],
    ['differs', w => writeFileSync(w.backupFile, 'one byte of the key changed, and more'), unverified, {}],
    ['off the mount', () => {}, /the key backup folder is not on the mounted share: .*resolves to the mount \/, expected /, { mountOf: async () => ok('/') }],
  ];
  for (const [label, damage, sentence, seam] of states) {
    const stale = await sealedWorldWithClearFolder();
    damage(stale);
    const swept = await sweepClearFolder({ ...stale.args, ...seam });
    assert.equal(swept.ok, false, `sweep, ${label}`);
    assert.match(swept.reason, sentence, `sweep, ${label}`);
    assert.equal(existsSync(stale.casesDir), true, `sweep, ${label}: nothing removed`);
    const held = await sealedClosed();
    assert.equal((await openClearFolder(held.args)).ok, true);
    damage(held);
    const closed = await closeClearFolder({ ...held.args, ...seam });
    assert.equal(closed.ok, false, `close, ${label}`);
    assert.match(closed.reason, sentence, `close, ${label}`);
    assert.equal(existsSync(held.casesDir), true, `close, ${label}: nothing removed`);
  }
  const fine = await sealedWorldWithClearFolder();
  const swept = await sweepClearFolder(fine.args);
  assert.equal(swept.ok, true, swept.reason);
  assert.equal(existsSync(fine.casesDir), false, 'control: an equal backup on the expected mount: the stale folder is removed');
});

/** A sealed world whose authoring `cases/` is still there: a stale folder equal to its archive, with no stamp. */
async function sealedWorldWithClearFolder() {
  const w = world();
  const { sealPackage } = await import('../lib/package-seal.mjs');
  const r = await sealPackage(w.args);
  assert.equal(r.ok, true, r.reason);
  return w;
}

test('A_key_restored_from_the_backup_is_used_only_after_its_sha256_matches_the_entry', async () => {
  const w = await sealedClosed();
  const key = readKey(w.keyFile).value;
  rmSync(w.keyFile);
  const bent = Buffer.from(key);
  bent[7] ^= 1;
  writeFileSync(w.keyFile, bent, { mode: 0o600 });
  const unsealed = w.spies.unseal;
  const wrong = await openClearFolder(w.args);
  assert.equal(wrong.ok, false);
  assert.match(wrong.reason, /the key is not the one the archive was sealed with/);
  assert.equal(w.spies.unseal, unsealed, 'a restored copy with one byte changed is refused before decryption');
  rmSync(w.keyFile);
  writeFileSync(w.keyFile, readFileSync(w.backupFile), { mode: 0o600 });
  const exact = await openClearFolder(w.args);
  assert.equal(exact.ok, true, exact.reason);
  assert.equal(w.spies.unseal, unsealed + 1, 'the exact copy is used');
});

test('A_backup_folder_that_is_not_on_the_shares_mount_is_refused_naming_both_paths', async () => {
  const w = await sealedClosed();
  assert.equal((await openClearFolder(w.args)).ok, true);
  const elsewhere = { mountOf: async () => ok('/') };
  const closed = await closeClearFolder({ ...w.args, ...elsewhere });
  assert.equal(closed.ok, false);
  assert.ok(closed.reason.includes(path.dirname(w.backupFile)) && closed.reason.includes('resolves to the mount /') && closed.reason.includes(`expected ${w.shareMount}`), closed.reason);
  assert.equal(existsSync(w.casesDir), true, 'nothing removed');
  const fresh = world();
  const { sealPackage } = await import('../lib/package-seal.mjs');
  const seal = await sealPackage({ ...fresh.args, ...elsewhere });
  assert.equal(seal.ok, false);
  assert.match(seal.reason, /not on the mounted share: .* resolves to the mount \/, expected /);
  assert.deepEqual([existsSync(fresh.backupFile), existsSync(path.join(fresh.heldoutDir, 'suite.enc'))], [false, false], 'nothing copied, no archive');
  const closedOk = await closeClearFolder(w.args);
  assert.equal(closedOk.ok, true, `control: on the expected mount the close removes (${closedOk.reason})`);
  assert.equal(existsSync(w.casesDir), false);
});

test('A_tampered_archive_or_one_sealed_with_another_key_is_refused_before_any_file_is_written', async () => {
  const setEntry = (w, archive) => {
    const record = JSON.parse(readFileSync(w.frozenFile, 'utf8'));
    record.tasks['04'].archiveSha256 = sha(archive);
    writeFileSync(w.frozenFile, JSON.stringify(record));
  };
  const a = await sealedClosed();
  const bent = Buffer.from(readFileSync(path.join(a.heldoutDir, 'suite.enc')));
  bent[bent.length - 40] ^= 1;
  writeFileSync(path.join(a.heldoutDir, 'suite.enc'), bent);
  setEntry(a, bent);
  const b = await sealedClosed();
  const src = path.join(tmpDir('q-other-cases'), 'cases');
  (await import('node:fs')).mkdirSync(src);
  writeFileSync(path.join(src, 'h001.in'), 'another invented case\n');
  const otherKey = readKey(newKey()).value;
  const foreign = sealTree({ src, key: otherKey }).value.sealed;
  writeFileSync(path.join(b.heldoutDir, 'suite.enc'), foreign);
  setEntry(b, foreign);
  for (const [label, w] of [['a flipped byte', a], ['another key', b]]) {
    const r = await openClearFolder(w.args);
    assert.equal(r.ok, false, label);
    assert.match(r.reason, /authentication failed/, label);
    assert.equal(existsSync(w.casesDir), false, `${label}: no cases/`);
    assert.equal(w.spies.unpackTo, 0, `${label}: no file was written (the unpacking seam was not called)`);
    assert.deepEqual(tempsIn(w.heldoutDir), [], `${label}: the temporary folder and its claim are removed`);
  }
});

test('The_archive_hash_is_checked_against_the_registry_before_the_key_is_used', async () => {
  const w = await sealedClosed();
  const registered = readFileSync(path.join(w.heldoutDir, 'suite.enc'));
  const src = path.join(tmpDir('q-other-cases-2'), 'cases');
  (await import('node:fs')).mkdirSync(src);
  writeFileSync(path.join(src, 'h001.in'), 'a different invented case\n');
  writeFileSync(path.join(w.heldoutDir, 'suite.enc'), sealTree({ src, key: readKey(w.keyFile).value }).value.sealed);
  const unsealed = w.spies.unseal;
  for (const [name, run] of [['open', () => openClearFolder(w.args)], ['check', () => checkHeldout(w.args)]]) {
    const r = await run();
    assert.equal(r.ok, false, name);
    assert.ok(r.reason.includes('archiveSha256') && r.reason.includes(path.join(w.heldoutDir, 'suite.enc')), `${name}: ${r.reason}`);
  }
  assert.equal(w.spies.unseal, unsealed, 'the decryption seam was not called');
  writeFileSync(path.join(w.heldoutDir, 'suite.enc'), registered);
  assert.equal((await openClearFolder(w.args)).ok, true, 'control: the registered blob');
});

test('A_findmnt_that_fails_or_is_missing_is_a_refusal_and_nothing_is_removed', async () => {
  for (const answer of [fail('findmnt: not found'), fail('findmnt exited 1 for the folder: no mount is named')]) {
    const held = await sealedClosed();
    assert.equal((await openClearFolder(held.args)).ok, true);
    const closed = await closeClearFolder({ ...held.args, mountOf: async () => answer });
    assert.equal(closed.ok, false);
    assert.ok(/the key backup cannot be verified/.test(closed.reason) && closed.reason.includes(held.backupFile) && /task 04/.test(closed.reason), closed.reason);
    assert.equal(existsSync(held.casesDir), true, 'close removed nothing');
    const stale = await sealedWorldWithClearFolder();
    const swept = await sweepClearFolder({ ...stale.args, mountOf: async () => answer });
    assert.equal(swept.ok, false);
    assert.match(swept.reason, /the key backup cannot be verified/);
    assert.equal(existsSync(stale.casesDir), true, 'the sweep removed nothing');
  }
});
