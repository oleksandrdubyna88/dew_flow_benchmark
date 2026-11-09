// The v1 suite seals to the same bytes as before (bench PR 4a, S7). The sealed blob holds a random IV, so "the same bytes" is pinned in four parts: the pack of a tree is deterministic (its JSON is pinned by a committed
// hash); a blob sealed by the code BEFORE the widening still unseals and unpacks to the same tree; `sealSuite` writes exactly its four outputs; and the container's constants are the ones `heldout-crypto.test.mjs` pins.
// The widening itself: `sealTree` is the one function `sealSuite` and the lang-explore seal share. The key of the fixture blob is invented, committed, and seals fixture content only.
import './fake-home.mjs';
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { chmodSync, mkdirSync, readFileSync, readdirSync, statSync, symlinkSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { gunzipSync } from 'node:zlib';
import { packDir, unpackTo } from '../lib/heldout-archive.mjs';
import { MAGIC, unseal } from '../lib/heldout-crypto.mjs';
import { sealSuite, sealTree } from '../lib/heldout-seal.mjs';
import { tmpDir } from './helpers.mjs';
import { writeToySuite } from './toy-suite.mjs';

const FIXTURES = fileURLToPath(new URL('./fixtures/heldout-seal/', import.meta.url));
const sha = bytes => createHash('sha256').update(bytes).digest('hex');
const PACK_JSON_SHA256 = '3d5075f4968c3e4eb272e20dee287670aee8ea99203627d507b9ef49278f9ea7';

function fixtureTree() {
  const dir = path.join(tmpDir('seal-tree'), 'tree');
  mkdirSync(path.join(dir, 'sub'), { recursive: true });
  writeFileSync(path.join(dir, 'alpha.txt'), 'invented fixture alpha\n');
  writeFileSync(path.join(dir, 'sub', 'beta.txt'), 'invented fixture beta\n');
  writeFileSync(path.join(dir, 'sub', 'run.sh'), '#!/bin/sh\necho invented\n');
  chmodSync(path.join(dir, 'sub', 'run.sh'), 0o755);
  return dir;
}

const fixtureKey = () => Buffer.from(readFileSync(path.join(FIXTURES, 'fixture-key.hex'), 'utf8').trim(), 'hex');

function keyFileIn(dir) {
  const file = path.join(dir, 'k', 'heldout.key');
  mkdirSync(path.dirname(file), { mode: 0o700 });
  writeFileSync(file, fixtureKey(), { mode: 0o600 });
  return file;
}

test('The_pack_of_a_fixture_tree_is_deterministic_and_its_json_is_the_committed_one', () => {
  const tree = fixtureTree();
  const [a, b] = [packDir(tree), packDir(tree)];
  assert.equal(a.ok, true, a.reason);
  assert.deepEqual(a.value, b.value, 'identical trees give identical bytes');
  assert.equal(sha(gunzipSync(a.value)), PACK_JSON_SHA256, 'sorted paths, modes 755/644, base64 data, no timestamps: the committed hash of the pack\'s json (the gzip bytes may differ between zlib builds, the json may not)');
});

test('A_blob_sealed_before_the_widening_still_unseals_and_unpacks_to_the_same_tree', () => {
  const opened = unseal(readFileSync(path.join(FIXTURES, 'suite.enc')), fixtureKey());
  assert.equal(opened.ok, true, opened.reason);
  assert.equal(sha(gunzipSync(opened.value)), PACK_JSON_SHA256, 'the plaintext of the committed blob is the pack of the fixture tree');
  const out = path.join(tmpDir('seal-open'), 'out');
  assert.equal(unpackTo(opened.value, out).ok, true);
  const tree = fixtureTree();
  const listing = dir => readdirSync(dir, { recursive: true }).filter(f => statSync(path.join(dir, f)).isFile()).sort();
  assert.deepEqual(listing(out), listing(tree));
  for (const f of listing(tree)) assert.deepEqual(readFileSync(path.join(out, f)), readFileSync(path.join(tree, f)), f);
  assert.equal(statSync(path.join(out, 'sub', 'run.sh')).mode & 0o111, 0o111, 'the executable bit survives');
});

test('The_shared_seal_function_packs_seals_checks_the_round_trip_and_reports_the_plaintext_hash', () => {
  const tree = fixtureTree();
  const r = sealTree({ src: tree, key: fixtureKey() });
  assert.equal(r.ok, true, r.reason);
  assert.equal(sha(unseal(r.value.sealed, fixtureKey()).value), r.value.plaintextSha256);
  assert.deepEqual(r.value.sealed.subarray(0, MAGIC.length).toString('latin1'), MAGIC);
  symlinkSync('/etc/hostname', path.join(tree, 'link.txt'));
  const refused = sealTree({ src: tree, key: fixtureKey() });
  assert.equal(refused.ok, false, 'a link is refused and nothing is sealed');
  assert.match(refused.reason, /link\.txt.*link or special file/);
});

test('The_v1_seal_writes_exactly_its_four_outputs_and_the_blob_is_the_shared_functions', () => {
  const root = tmpDir('seal-suite');
  const src = path.join(root, 'src');
  writeToySuite(src);
  const outDir = path.join(root, 'heldout');
  const hashes = path.join(root, 'hashes.txt');
  writeFileSync(hashes, `${'a'.repeat(64)}  upstream/one.txt\n`);
  const r = sealSuite({ src, keyFile: keyFileIn(root), outDir, hashesFile: hashes, today: '2026-11-09' });
  assert.equal(r.ok, true, r.reason);
  assert.deepEqual(readdirSync(outDir).sort(), ['manifest.json', 'suite.enc', 'suite.enc.sha256']);
  const manifest = JSON.parse(readFileSync(path.join(outDir, 'manifest.json'), 'utf8'));
  assert.deepEqual(Object.keys(manifest).sort(), ['format', 'm', 'n1', 'n2', 'plaintextSha256', 'sealedOn']);
  assert.equal(manifest.format, 1);
  const enc = readFileSync(path.join(outDir, 'suite.enc'));
  assert.equal(sha(unseal(enc, fixtureKey()).value), manifest.plaintextSha256, 'the blob unseals to the pack whose hash the manifest holds');
  assert.equal(readFileSync(path.join(outDir, 'suite.enc.sha256'), 'utf8'), `${sha(enc)}  suite.enc\n`);
  assert.equal(readFileSync(hashes, 'utf8').split('\n').filter(l => l.endsWith('heldout/suite.enc')).length, 1, 'one hashes.txt line');
  assert.equal(statSync(path.join(outDir, 'suite.enc')).mode & 0o777, 0o644);
});

test('The_container_layout_is_the_magic_a_twelve_byte_iv_the_ciphertext_and_a_sixteen_byte_tag', () => {
  const sealed = sealTree({ src: fixtureTree(), key: fixtureKey() }).value.sealed;
  const packLength = packDir(fixtureTree()).value.length;
  assert.equal(MAGIC, 'MGHO1\n');
  assert.equal(sealed.subarray(0, 6).toString('latin1'), MAGIC);
  assert.equal(sealed.length, 6 + 12 + packLength + 16, 'magic, iv, ciphertext (as long as the pack: GCM adds nothing), tag');
  const bent = Buffer.from(sealed);
  bent[0] ^= 1;
  assert.match(unseal(bent, fixtureKey()).reason, /not a sealed suite/, 'the magic is also the AAD: another magic is refused');
});
