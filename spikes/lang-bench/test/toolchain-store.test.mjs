// E1.S5: the toolchain store on disk — a download is kept only when it is the published bytes, a directory is read-only with its tree hash
// recorded, the SDK directory holds only the pinned SDK, and the store never sits inside a git worktree. Fixtures are small trees.
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { chmodSync, mkdirSync, symlinkSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { checkDigest, checkStoreRoot, sdkVersionsIn, sealTree, treeRecord, unsealTree, verifyRecordedTrees, writablePaths } from '../lib/toolchain-store.mjs';
import { ownedFolder, writeTree } from './toolchain-fixtures.mjs';

const BYTES = Buffer.from('the published bytes\n');
const digest = (algorithm, encoding) => createHash(algorithm).update(BYTES).digest(encoding);

test('A_file_that_is_not_the_published_bytes_is_refused_naming_both_digests', async t => {
  const dir = ownedFolder(t, 'digest');
  const file = path.join(dir, 'artefact.bin');
  writeFileSync(file, BYTES);
  const good = await checkDigest({ file, algorithm: 'sha256', encoding: 'hex', expected: digest('sha256', 'hex'), label: 'the Node tarball' });
  assert.equal(good.ok, true, JSON.stringify(good));
  writeFileSync(file, Buffer.concat([BYTES, Buffer.from('x')]));
  const bad = await checkDigest({ file, algorithm: 'sha256', encoding: 'hex', expected: digest('sha256', 'hex'), label: 'the Node tarball' });
  assert.equal(bad.ok, false);
  assert.match(bad.reason, /the Node tarball/);
  assert.ok(bad.reason.includes(digest('sha256', 'hex')) && bad.reason.includes(createHash('sha256').update(Buffer.concat([BYTES, Buffer.from('x')])).digest('hex')), bad.reason);
});

test('A_digest_is_checked_in_the_encoding_its_publisher_uses_sha512_hex_and_npm_base64', async t => {
  const dir = ownedFolder(t, 'encoding');
  const file = path.join(dir, 'artefact.bin');
  writeFileSync(file, BYTES);
  assert.equal((await checkDigest({ file, algorithm: 'sha512', encoding: 'hex', expected: digest('sha512', 'hex'), label: 'sdk' })).ok, true);
  assert.equal((await checkDigest({ file, algorithm: 'sha512', encoding: 'base64', expected: digest('sha512', 'base64'), label: 'npm' })).ok, true);
  assert.equal((await checkDigest({ file, algorithm: 'sha512', encoding: 'hex', expected: digest('sha512', 'base64'), label: 'mixed up' })).ok, false);
});

test('A_file_that_is_missing_is_a_refusal_not_a_crash', async t => {
  const missing = await checkDigest({ file: path.join(ownedFolder(t, 'absent'), 'nothing'), algorithm: 'sha256', encoding: 'hex', expected: 'x', label: 'the archive' });
  assert.equal(missing.ok, false);
  assert.match(missing.reason, /the archive.*(missing|absent|cannot be read)/s);
});

test('A_sealed_directory_has_no_writable_entry_and_a_writable_one_is_named', t => {
  const root = writeTree(ownedFolder(t, 'seal'), { 'bin/tool': 'x', 'lib/a/b.txt': 'y' });
  symlinkSync('../lib/a/b.txt', path.join(root, 'bin', 'link'));
  assert.ok(writablePaths(root).length > 0, 'a fresh tree is writable');
  sealTree(root);
  try {
    assert.deepEqual(writablePaths(root), []);
    chmodSync(path.join(root, 'lib', 'a'), 0o755);
    assert.deepEqual(writablePaths(root), [path.join('lib', 'a')], 'one writable directory, named relative to the root');
  } finally {
    unsealTree(root);
  }
});

test('The_tree_hash_of_a_directory_is_the_same_sealed_or_not_and_changes_with_one_byte', t => {
  const root = writeTree(ownedFolder(t, 'tree'), { 'bin/tool': 'x', 'lib/b.txt': 'y' });
  const before = treeRecord(root);
  sealTree(root);
  try {
    assert.equal(treeRecord(root), before, 'write bits are not part of the tree');
  } finally {
    unsealTree(root);
  }
  writeFileSync(path.join(root, 'lib', 'b.txt'), 'z');
  assert.notEqual(treeRecord(root), before);
});

test('A_directory_whose_tree_hash_differs_from_the_record_is_named_and_a_missing_one_too', t => {
  const store = ownedFolder(t, 'record');
  writeTree(path.join(store, 'node'), { 'bin/node': 'n' });
  writeTree(path.join(store, 'ts'), { 'm/index.js': 't' });
  const recorded = { node: treeRecord(path.join(store, 'node')), ts: treeRecord(path.join(store, 'ts')), gone: 'f'.repeat(64) };
  const dirs = { node: path.join(store, 'node'), ts: path.join(store, 'ts'), gone: path.join(store, 'gone') };
  const first = verifyRecordedTrees({ dirs, recorded });
  assert.deepEqual(first.map(r => [r.name, r.state]), [['node', 'matches'], ['ts', 'matches'], ['gone', 'missing']]);
  writeFileSync(path.join(store, 'ts', 'm', 'index.js'), 'changed');
  assert.equal(verifyRecordedTrees({ dirs, recorded }).find(r => r.name === 'ts').state, 'differs');
});

test('The_SDK_directory_must_hold_only_the_pinned_SDK_and_a_second_one_is_named', t => {
  const dotnet = writeTree(ownedFolder(t, 'sdk'), { 'dotnet': 'host', 'sdk/10.0.403/dotnet.dll': 'x', 'sdk/10.0.114/dotnet.dll': 'y', 'sdk/NuGetFallbackFolder/readme': 'z' });
  assert.deepEqual(sdkVersionsIn(dotnet), ['10.0.114', '10.0.403'], 'versions only, sorted; a helper folder is not an SDK');
});

test('A_store_root_inside_a_git_worktree_is_refused', t => {
  const here = fileURLToPath(new URL('.', import.meta.url));
  const inside = checkStoreRoot(path.join(here, 'toolchains-would-be-here'));
  assert.equal(inside.ok, false);
  assert.match(inside.reason, /inside the git worktree/);
  const outside = checkStoreRoot(path.join(ownedFolder(t, 'outside'), 'toolchains'));
  assert.equal(outside.ok, true, JSON.stringify(outside));
});

test('The_root_of_a_directory_counts_as_writable_when_its_own_mode_is_writable', t => {
  const dir = ownedFolder(t, 'mode');
  mkdirSync(path.join(dir, 'a'), { mode: 0o755 });
  assert.deepEqual(writablePaths(path.join(dir, 'a')), ['.'], 'the root itself counts when it is writable');
});
