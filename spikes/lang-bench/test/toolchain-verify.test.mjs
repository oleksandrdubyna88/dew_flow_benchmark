// E1.S5: reading the store back — every directory present, sealed, the SDK directory holding only the pinned SDK, each tree hash equal to the
// record in inputs/stacks-lang.json — and writing that record without ever moving a hash that is already there.
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { chmodSync, existsSync, readFileSync, writeFileSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { loadStacksLang } from '../lib/toolchain-pins.mjs';
import { sealTree, treeRecord, unsealTree } from '../lib/toolchain-store.mjs';
import { recordTrees, verifyStore } from '../lib/toolchain-verify.mjs';
import { ownedFolder, writeTree } from './toolchain-fixtures.mjs';

const realPath = fileURLToPath(new URL('../inputs/stacks-lang.json', import.meta.url));
const real = JSON.parse(readFileSync(realPath, 'utf8'));
const cli = fileURLToPath(new URL('../install-toolchains.mjs', import.meta.url));

/** A store holding the three directories of the real pins as small trees, sealed, and the stacks with their tree hashes recorded. */
function sealedStore(t) {
  const storeRoot = ownedFolder(t, 'vstore');
  const d = real.directories;
  writeTree(path.join(storeRoot, d.dotnetSdk), { dotnet: 'host', [`sdk/${real.pins.dotnetSdk}/a.dll`]: 'x' });
  writeTree(path.join(storeRoot, d.node), { 'bin/node': 'n' });
  writeTree(path.join(storeRoot, d.typescript), { 'node_modules/typescript/bin/tsc': 't' });
  const stacks = structuredClone(real);
  stacks.treeSha256 = Object.fromEntries(Object.entries(d).map(([name, dir]) => [name, treeRecord(path.join(storeRoot, dir))]));
  for (const dir of Object.values(d)) sealTree(path.join(storeRoot, dir));
  t.after(() => { for (const dir of Object.values(d)) unsealTree(path.join(storeRoot, dir)); });
  return { storeRoot, stacks };
}

test('A_sealed_store_whose_trees_match_the_record_verifies', t => {
  const { storeRoot, stacks } = sealedStore(t);
  const result = verifyStore({ stacks, storeRoot, deep: true });
  assert.deepEqual(result.problems, []);
  assert.equal(result.ok, true);
});

test('A_missing_toolchain_directory_is_named', t => {
  const { stacks } = sealedStore(t);
  const empty = ownedFolder(t, 'vempty');
  const result = verifyStore({ stacks, storeRoot: empty, deep: false });
  assert.equal(result.ok, false);
  assert.match(result.problems.join('\n'), /dotnet-sdk-10\.0\.403.*not in the store/s);
  assert.equal(result.problems.length, 3, 'one problem per missing directory');
});

test('A_directory_that_a_user_can_write_to_is_named', t => {
  const { storeRoot, stacks } = sealedStore(t);
  const dir = path.join(storeRoot, stacks.directories.node);
  chmodSync(path.join(dir, 'bin'), 0o755);
  const result = verifyStore({ stacks, storeRoot, deep: false });
  assert.equal(result.ok, false);
  assert.match(result.problems.join('\n'), /node-v26\.13\.0-linux-x64.*writable.*bin/s);
});

test('A_second_SDK_in_the_SDK_directory_is_named_with_its_version', t => {
  const { storeRoot, stacks } = sealedStore(t);
  const dir = path.join(storeRoot, stacks.directories.dotnetSdk);
  unsealTree(dir);
  writeTree(dir, { 'sdk/10.0.114/a.dll': 'y' });
  sealTree(dir);
  const result = verifyStore({ stacks, storeRoot, deep: false });
  assert.equal(result.ok, false);
  assert.match(result.problems.join('\n'), /10\.0\.114/);
});

test('A_changed_byte_in_a_toolchain_is_found_by_the_recorded_tree_hash_and_only_in_deep_mode', t => {
  const { storeRoot, stacks } = sealedStore(t);
  const dir = path.join(storeRoot, stacks.directories.typescript);
  unsealTree(dir);
  writeFileSync(path.join(dir, 'node_modules', 'typescript', 'bin', 'tsc'), 'changed');
  sealTree(dir);
  assert.equal(verifyStore({ stacks, storeRoot, deep: false }).ok, true, 'the shallow check reads modes and versions, not bytes');
  const deep = verifyStore({ stacks, storeRoot, deep: true });
  assert.equal(deep.ok, false);
  assert.match(deep.problems.join('\n'), /typescript-6\.0\.3.*tree hash/s);
});

test('A_deep_check_without_a_recorded_hash_is_a_refusal_not_a_pass', t => {
  const { storeRoot, stacks } = sealedStore(t);
  const { treeSha256, ...unrecorded } = stacks;
  const result = verifyStore({ stacks: unrecorded, storeRoot, deep: true });
  assert.equal(result.ok, false);
  assert.match(result.problems.join('\n'), /no tree hash recorded/);
});

test('Recording_tree_hashes_adds_them_keeps_the_rest_and_never_moves_one_already_recorded', () => {
  const { treeSha256: _recorded, ...unrecorded } = real; // the committed file already carries the real hashes
  const text = `${JSON.stringify(unrecorded, null, 2)}\n`;
  const trees = { dotnetSdk: 'a'.repeat(64), node: 'b'.repeat(64), typescript: 'c'.repeat(64) };
  const recorded = recordTrees(text, trees);
  assert.equal(recorded.ok, true, JSON.stringify(recorded));
  const parsed = JSON.parse(recorded.value);
  assert.deepEqual(parsed.treeSha256, trees);
  const shuffled = JSON.parse(recordTrees(text, { typescript: trees.typescript, node: trees.node, dotnetSdk: trees.dotnetSdk }).value);
  assert.deepEqual(Object.keys(shuffled.treeSha256), ['dotnetSdk', 'node', 'typescript'], 'sorted keys: the same record is the same bytes whatever order it was found in');
  assert.deepEqual({ ...parsed, treeSha256: undefined }, { ...unrecorded, treeSha256: undefined }, 'nothing else changed');
  assert.equal(loadStacksLang(parsed).ok, true);
  assert.equal(recordTrees(recorded.value, trees).value, recorded.value, 'recording the same hashes again is a no-op');
  const moved = recordTrees(recorded.value, { ...trees, node: 'd'.repeat(64) });
  assert.equal(moved.ok, false);
  assert.match(moved.reason, /node.*b{64}.*d{64}/s);
});

test('The_command_verifies_a_store_and_exits_nonzero_naming_what_is_wrong', t => {
  const { storeRoot, stacks } = sealedStore(t);
  const stacksFile = path.join(ownedFolder(t, 'cliin'), 'stacks-lang.json');
  writeFileSync(stacksFile, JSON.stringify(stacks));
  const good = spawnSync(process.execPath, [cli, '--verify', '--deep', '--store', storeRoot, '--stacks', stacksFile], { encoding: 'utf8' });
  assert.equal(good.status, 0, good.stderr + good.stdout);
  chmodSync(path.join(storeRoot, stacks.directories.node), 0o755);
  const bad = spawnSync(process.execPath, [cli, '--verify', '--store', storeRoot, '--stacks', stacksFile], { encoding: 'utf8' });
  assert.equal(bad.status, 1, bad.stderr + bad.stdout);
  assert.match(bad.stderr, /node-v26\.13\.0-linux-x64.*writable/s);
});

test('The_command_refuses_a_store_inside_a_git_worktree_and_an_unknown_flag', () => {
  const inside = spawnSync(process.execPath, [cli, '--verify', '--store', path.join(path.dirname(cli), 'toolchains')], { encoding: 'utf8' });
  assert.equal(inside.status, 2, inside.stderr);
  assert.match(inside.stderr, /inside the git worktree/);
  const unknown = spawnSync(process.execPath, [cli, '--frobnicate'], { encoding: 'utf8' });
  assert.equal(unknown.status, 2);
  assert.match(unknown.stderr, /--frobnicate/);
});

test('The_committed_record_is_the_installed_store_every_tree_hash_equal_and_every_directory_sealed', t => {
  const storeRoot = path.join(os.homedir(), ...real.storeRoot);
  const stacks = loadStacksLang(real).value;
  if (!Object.values(stacks.directories).every(name => existsSync(path.join(storeRoot, name)))) {
    if (process.env.BENCH_REQUIRE_SANDBOX === '1') throw new Error(`BENCH_REQUIRE_SANDBOX=1 and the pinned toolchains are not installed under ${storeRoot}`);
    return t.skip(`the pinned toolchains are not installed under ${storeRoot} (node spikes/lang-bench/install-toolchains.mjs)`);
  }
  const result = verifyStore({ stacks, storeRoot, deep: true });
  assert.deepEqual(result.problems, []);
});
