// The archive key and its backup (bench PR 4a): the key is read by `readKey` and held in memory only; only its sha256 is ever written; before a clear case may be removed, the backup copy of the key is on the share's mount
// (`findmnt` through a seam, as a program with an argument list), is a regular file and hashes to the entry's `keySha256`. Invented keys in owned folders; the real key, share and `findmnt` answers of the operator are never read.
import '../../minigit-bench/test/fake-home.mjs';
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { chmodSync, realpathSync, symlinkSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { readKey } from '../../minigit-bench/lib/heldout-crypto.mjs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { backupProblem, keyProblem, makeMountOf, mountOf } from '../lib/heldout-key.mjs';
import { mountFor, world } from './quick-heldout-fixture.mjs';

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
