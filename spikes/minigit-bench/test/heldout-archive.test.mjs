import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { existsSync, lstatSync, mkdirSync, readFileSync, readdirSync, statSync, symlinkSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { gzipSync } from 'node:zlib';
import { packDir, unpackTo } from '../lib/heldout-archive.mjs';
import { seal, unseal } from '../lib/heldout-crypto.mjs';
import { tmpDir } from './helpers.mjs';

const sha = bytes => createHash('sha256').update(bytes).digest('hex');

function toyTree() {
  const dir = tmpDir('toy-tree');
  mkdirSync(path.join(dir, 'cases'));
  mkdirSync(path.join(dir, 'empty-dir-is-not-kept'));
  writeFileSync(path.join(dir, 'runner.py'), '# toy\n');
  writeFileSync(path.join(dir, 'cases', 'a.py'), 'x = 1\n');
  writeFileSync(path.join(dir, 'tool'), '#!/bin/sh\necho toy\n', { mode: 0o755 });
  writeFileSync(path.join(dir, 'bytes.bin'), Buffer.from([0, 255, 128, 10]));
  mkdirSync(path.join(dir, '__pycache__'));
  writeFileSync(path.join(dir, '__pycache__', 'a.pyc'), 'cache is never packed');
  return dir;
}

const snapshot = dir => readdirSync(dir, { recursive: true, withFileTypes: true })
  .filter(e => e.isFile()).map(e => path.join(e.parentPath, e.name)).sort()
  .map(f => [path.relative(dir, f), statSync(f).mode & 0o777, readFileSync(f).toString('base64')]);

test('A_sealed_suite_round_trips_byte_for_byte', () => {
  const src = toyTree();
  const key = Buffer.alloc(32, 5);
  const one = packDir(src);
  const two = packDir(src);
  assert.equal(one.ok, true, one.reason);
  assert.equal(sha(one.value), sha(two.value), 'plaintextSha256 is reproducible');
  const s1 = seal(one.value, key);
  const s2 = seal(two.value, key);
  assert.notDeepEqual(s1, s2, 'the ciphertext is not (random IV)');
  const out = path.join(tmpDir('toy-out'), 'suite');
  const opened = unseal(s1, key);
  assert.equal(opened.ok, true);
  const unpacked = unpackTo(opened.value, out);
  assert.equal(unpacked.ok, true, unpacked.reason);
  const expected = snapshot(src).filter(([rel]) => !rel.startsWith('__pycache__'));
  assert.deepEqual(snapshot(out), expected);
  assert.equal(statSync(path.join(out, 'tool')).mode & 0o777, 0o755);
  assert.equal(statSync(path.join(out, 'runner.py')).mode & 0o777, 0o644);
  assert.equal(statSync(out).mode & 0o777, 0o700);
});

test('Packing_refuses_a_link_inside_the_tree', () => {
  const src = toyTree();
  symlinkSync('/etc/hostname', path.join(src, 'sneaky'));
  const r = packDir(src);
  assert.equal(r.ok, false);
  assert.match(r.reason, /sneaky/);
});

const archiveOf = entries => gzipSync(Buffer.from(JSON.stringify({ format: 1, entries })));
const entry = (p, extra = {}) => ({ path: p, mode: 0o644, data: Buffer.from('x').toString('base64'), ...extra });

test('An_archive_entry_escaping_its_directory_is_refused', () => {
  const root = tmpDir('escape');
  const out = path.join(root, 'out');
  const cases = [
    ['../x', entry('../x')],
    ['/etc/x', entry('/etc/x')],
    ['nested ..', entry('a/../../x')],
    ['a symlink entry', entry('link', { link: '/etc/passwd' })],
    ['a typed symlink entry', entry('link2', { type: 'symlink' })],
    ['a bad mode', entry('m', { mode: 0o4755 })],
    ['a duplicate', null],
  ];
  for (const [name, bad] of cases) {
    const entries = bad === null ? [entry('d'), entry('d')] : [entry('fine-first'), bad];
    const r = unpackTo(archiveOf(entries), out);
    assert.equal(r.ok, false, name);
    assert.equal(existsSync(out), false, `${name}: nothing was written, not even the entry before the bad one`);
  }
  assert.equal(existsSync(path.join(root, 'x')), false);
  assert.equal(unpackTo(Buffer.from('not gzip'), out).ok, false);
  assert.equal(unpackTo(gzipSync(Buffer.from('{"format":2,"entries":[]}')), out).ok, false);
});

test('Unpacking_into_a_directory_that_already_exists_is_refused', () => {
  const dir = tmpDir('exists');
  const r = unpackTo(archiveOf([entry('ok')]), dir);
  assert.equal(r.ok, false);
  assert.match(r.reason, /already exists/);
  assert.equal(lstatSync(dir).isDirectory(), true);
});
