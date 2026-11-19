// The pinned execution-layer identity of the language comparison (`spikes/lang-bench/inputs/specs.d/exec-layer.json`, part of the frozen spec `spec-frozen-B-20261118`) names files of THIS spike:
// `lib/bwrap.mjs`, `lib/run.mjs`, `lib/result.mjs`, `lib/find-on-path.mjs`, and through them every module they import. A change to one of them, or a new import in one, moves the layer's tree hash, and
// every run through the layer halts with `layer-identity-mismatch`; re-pinning is a re-freeze, which is the owner's. On 2026-11-19 a change to `lib/bwrap.mjs` (the scoring box's dotnet list, and an
// import of `dotnet-hide.mjs`) did exactly that, and nothing in this spike's own suite said so: 13 tests of `spikes/lang-explore` went red in a whole run. This file says so here, by path.
// It reads the pin and hashes; it changes nothing and never re-pins.
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { existsSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { LAYER_DIR, SPIKES_ROOT, computeIdentity } from '../../lang-bench/lib/exec-layer/identity.mjs';

const PIN_FILE = path.join(SPIKES_ROOT, 'lang-bench', 'inputs', 'specs.d', 'exec-layer.json');
const pinned = JSON.parse(readFileSync(PIN_FILE, 'utf8')).execLayer;
const sha256Of = file => (existsSync(file) ? createHash('sha256').update(readFileSync(file)).digest('hex') : 'absent');

test('Every_file_of_the_pinned_execution_layer_identity_still_hashes_to_its_pinned_sha256', () => {
  assert.ok(pinned.files.length >= 20, `${pinned.files.length} files are pinned`);
  const moved = pinned.files.filter(f => sha256Of(path.join(SPIKES_ROOT, f.path)) !== f.sha256).map(f => `${f.path} is ${sha256Of(path.join(SPIKES_ROOT, f.path)).slice(0, 12)}, the pin is ${f.sha256.slice(0, 12)}`);
  assert.deepEqual(moved, [], 'a pinned file changed: the layer halts every run with layer-identity-mismatch. Put the change in a module outside the identity; re-pinning is a re-freeze and the owner\'s');
});

test('The_layers_own_computation_over_what_it_imports_gives_the_pinned_tree_and_the_pinned_list', () => {
  const now = computeIdentity(LAYER_DIR);
  assert.equal(now.ok, true, now.reason);
  assert.deepEqual(now.value.execLayer.files.map(f => f.path), pinned.files.map(f => f.path), 'the files the layer reaches by its imports are the pinned ones: a new import in a pinned file adds a path here');
  assert.equal(now.value.execLayer.treeSha256, pinned.treeSha256);
});

test('The_pin_names_files_of_this_spike_so_this_check_is_about_something', () => {
  const ours = pinned.files.map(f => f.path).filter(p => p.startsWith('minigit-bench/'));
  assert.deepEqual(ours, ['minigit-bench/lib/bwrap.mjs', 'minigit-bench/lib/find-on-path.mjs', 'minigit-bench/lib/result.mjs', 'minigit-bench/lib/run.mjs'], 'the pinned files of this spike (a list that grows or shrinks is a re-freeze, and this line is then edited with it)');
});
