// E1.S8b: the layer's ONE identity — the sha256 of its tree, every file of `lib/exec-layer/` (`scope-entry.sh` included) — written to `inputs/specs.d/exec-layer.json` and assembled into
// `inputs/specs.json` beside the runner's. Every record of E2 and E3 names it (D36, D43); a change to any file of the tree re-dates their evidence (parent §6.1).
import assert from 'node:assert/strict';
import { readFileSync, symlinkSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { computeIdentity, LAYER_DIR, verifyIdentity } from '../lib/exec-layer/identity.mjs';
import { checkRecord } from '../lib/specs-record.mjs';
import { spikeDir } from './support/exec-layer-host.mjs';
import { owned } from './support/owned.mjs';
import { writeTree } from './toolchain-fixtures.mjs';

const recordFile = path.join(spikeDir, 'inputs', 'specs.d', 'exec-layer.json');

test('The_identity_of_the_real_tree_lists_every_file_scope_entry_included', () => {
  const identity = computeIdentity(LAYER_DIR);
  assert.ok(identity.ok, identity.reason);
  const names = identity.value.execLayer.files.map(file => file.path);
  for (const expected of ['scope-entry.sh', 'env.mjs', 'box.mjs', 'netns.mjs', 'scope.mjs', 'case-view.mjs', 'run.mjs', 'cleanup.mjs', 'identity.mjs']) assert.ok(names.includes(expected), `${expected} is in the identity`);
  assert.deepEqual(names, [...names].sort(), 'sorted, so the manifest is stable');
  assert.match(identity.value.execLayer.treeSha256, /^[0-9a-f]{64}$/);
});

test('The_identity_changes_with_any_byte_of_any_file_and_with_a_file_added_or_removed', t => {
  const tree = writeTree(owned.ownedDir(t, 'idtree'), { 'a.mjs': 'one', 'scope-entry.sh': '#!/bin/sh\n', 'sub/b.mjs': 'two' });
  const base = computeIdentity(tree).value.execLayer.treeSha256;
  writeFileSync(path.join(tree, 'scope-entry.sh'), '#!/bin/sh\n ');
  const edited = computeIdentity(tree).value.execLayer.treeSha256;
  assert.notEqual(edited, base, 'one byte of scope-entry.sh changes the identity');
  writeFileSync(path.join(tree, 'c.mjs'), 'three');
  assert.notEqual(computeIdentity(tree).value.execLayer.treeSha256, edited, 'a file added changes it');
  assert.equal(computeIdentity(tree).value.execLayer.files.length, 4);
});

test('A_link_in_the_tree_is_refused_because_the_hash_would_not_cover_what_runs', t => {
  const tree = writeTree(owned.ownedDir(t, 'idlink'), { 'a.mjs': 'one' });
  symlinkSync('a.mjs', path.join(tree, 'b.mjs'));
  const identity = computeIdentity(tree);
  assert.equal(identity.ok, false);
  assert.match(identity.reason, /b\.mjs.*not a regular file/);
});

test('The_committed_identity_is_the_tree_as_it_stands_and_specs_json_is_its_assembly', () => {
  const committed = JSON.parse(readFileSync(recordFile, 'utf8'));
  const verdict = verifyIdentity(LAYER_DIR, committed);
  assert.ok(verdict.ok, `${verdict.reason}: run node spikes/lang-bench/pin-exec-layer.mjs`);
  assert.deepEqual(checkRecord(path.join(spikeDir, 'inputs')), { ok: true });
});

test('A_stale_identity_is_named_by_the_file_that_differs', t => {
  const tree = writeTree(owned.ownedDir(t, 'idstale'), { 'a.mjs': 'one', 'b.mjs': 'two' });
  const record = computeIdentity(tree).value;
  writeFileSync(path.join(tree, 'b.mjs'), 'TWO');
  const verdict = verifyIdentity(tree, record);
  assert.equal(verdict.ok, false);
  assert.match(verdict.reason, /b\.mjs changed/);
});
