import assert from 'node:assert/strict';
import { chmodSync, existsSync, readFileSync, statSync, symlinkSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { generateKey, MAGIC, readKey, seal, unseal } from '../lib/heldout-crypto.mjs';
import { tmpDir } from './helpers.mjs';

const keyIn = (dir, bytes = Buffer.alloc(32, 7), mode = 0o600) => {
  const file = path.join(dir, 'k.key');
  writeFileSync(file, bytes, { mode });
  chmodSync(file, mode);
  return file;
};

test('A_sealed_blob_round_trips_and_carries_the_magic_iv_and_tag', () => {
  const key = Buffer.alloc(32, 1);
  const plain = Buffer.from('synthetic payload, nothing real');
  const sealed = seal(plain, key);
  assert.equal(sealed.subarray(0, 6).toString('latin1'), MAGIC);
  assert.equal(sealed.length, 6 + 12 + plain.length + 16);
  const opened = unseal(sealed, key);
  assert.equal(opened.ok, true);
  assert.deepEqual(opened.value, plain);
  assert.notDeepEqual(seal(plain, key), sealed, 'a random IV: two seals of one plaintext differ');
});

test('A_single_flipped_byte_or_another_key_fails_authentication_as_a_value', () => {
  const key = Buffer.alloc(32, 1);
  const sealed = seal(Buffer.from('synthetic payload'), key);
  for (const at of [3, 8, 20, sealed.length - 1]) {
    const bent = Buffer.from(sealed);
    bent[at] ^= 1;
    const r = unseal(bent, key);
    assert.equal(r.ok, false, `byte ${at}`);
    assert.match(r.reason, /authentication failed|not a sealed suite/);
  }
  assert.deepEqual(unseal(sealed, Buffer.alloc(32, 2)), { ok: false, reason: 'authentication failed' });
  assert.equal(unseal(Buffer.from('short'), key).ok, false);
  assert.match(unseal(Buffer.from('short'), key).reason, /not a sealed suite/);
});

test('A_key_file_readable_by_group_or_others_is_refused', () => {
  const dir = tmpDir('key');
  assert.equal(readKey(keyIn(dir)).ok, true);
  for (const mode of [0o644, 0o640, 0o604]) {
    const r = readKey(keyIn(dir, Buffer.alloc(32, 7), mode));
    assert.equal(r.ok, false);
    assert.match(r.reason, /mode/);
  }
  for (const size of [31, 33, 0]) {
    const r = readKey(keyIn(dir, Buffer.alloc(size, 7)));
    assert.equal(r.ok, false);
    assert.match(r.reason, /size|32 bytes/);
  }
  const real = keyIn(dir);
  const link = path.join(dir, 'link.key');
  symlinkSync(real, link);
  assert.match(readKey(link).reason, /not a regular file/);
  assert.match(readKey(path.join(dir, 'absent.key')).reason, /absent\.key.*does not exist|no key file/);
});

test('The_key_is_never_taken_from_the_environment', () => {
  const dir = tmpDir('key-env');
  const was = process.env.MINIGIT_HELDOUT_KEY;
  process.env.MINIGIT_HELDOUT_KEY = Buffer.alloc(32, 9).toString('hex');
  try {
    const r = readKey(path.join(dir, 'none.key'));
    assert.equal(r.ok, false);
    assert.match(r.reason, /none\.key/);
  } finally {
    if (was === undefined) delete process.env.MINIGIT_HELDOUT_KEY;
    else process.env.MINIGIT_HELDOUT_KEY = was;
  }
});

test('Keygen_writes_32_random_bytes_at_mode_600_and_refuses_to_overwrite', () => {
  const dir = tmpDir('keygen');
  const file = path.join(dir, 'sub', 'heldout.key');
  const first = generateKey(file);
  assert.equal(first.ok, true, first.reason);
  assert.equal(statSync(file).mode & 0o777, 0o600);
  assert.equal(statSync(path.dirname(file)).mode & 0o777, 0o700);
  assert.equal(readFileSync(file).length, 32);
  const before = readFileSync(file);
  const second = generateKey(file);
  assert.equal(second.ok, false);
  assert.match(second.reason, /already exists/);
  assert.deepEqual(readFileSync(file), before, 'the existing key is untouched');
  assert.equal(existsSync(file), true);
  assert.equal(readKey(file).ok, true);
});
