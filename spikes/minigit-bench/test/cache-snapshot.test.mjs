// The cache snapshots (E3b.S1 step 2, parent 7.1): a tree hash that is stable, a seed copy that re-hashes to the registered value, and the recipe run in a real box.
import assert from 'node:assert/strict';
import { chmodSync, existsSync, mkdirSync, readFileSync, readdirSync, statSync, utimesSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { loadRecipe, makeSnapshot, makeWritable, pickHighest, seedCaches, treeHash } from '../lib/cache-snapshot.mjs';
import { layoutPaths } from '../lib/sandbox.mjs';
import { homeDir, sandboxGate, spikeDir, tmpDir } from './helpers.mjs';

const put = (p, text, mode) => { mkdirSync(path.dirname(p), { recursive: true }); writeFileSync(p, text, mode ? { mode } : undefined); };
function fixture() {
  const dir = tmpDir('snap');
  put(path.join(dir, 'b', 'two.txt'), 'two');
  put(path.join(dir, 'a', 'one.txt'), 'one');
  put(path.join(dir, 'top.txt'), 'top');
  return dir;
}

test('The_snapshot_tree_hash_is_stable_and_the_seed_copy_matches', () => {
  const dir = fixture();
  const h = treeHash(dir);
  assert.match(h, /^[0-9a-f]{64}$/);
  const old = new Date('2001-01-01T00:00:00Z');
  utimesSync(path.join(dir, 'a', 'one.txt'), old, old);
  assert.equal(treeHash(dir), h, 'touching mtimes changes nothing');
  const reordered = tmpDir('snap2');
  put(path.join(reordered, 'top.txt'), 'top');
  put(path.join(reordered, 'a', 'one.txt'), 'one');
  put(path.join(reordered, 'b', 'two.txt'), 'two');
  assert.equal(treeHash(reordered), h, 'the order the files were written in is not in the hash');
  writeFileSync(path.join(dir, 'a', 'one.txt'), 'ONE');
  assert.notEqual(treeHash(dir), h, 'one changed byte');
  writeFileSync(path.join(dir, 'a', 'one.txt'), 'one');
  assert.equal(treeHash(dir), h);
  chmodSync(path.join(dir, 'top.txt'), 0o755);
  assert.notEqual(treeHash(dir), h, 'a changed mode');
});

test('An_empty_directory_and_a_missing_file_are_not_the_same_tree', () => {
  const a = tmpDir('e1');
  const b = tmpDir('e2');
  mkdirSync(path.join(b, 'empty'));
  assert.notEqual(treeHash(a), treeHash(b));
});

test('Seeding_copies_both_snapshots_into_the_session_and_the_copy_re_hashes_to_the_registered_value', () => {
  const csharp = tmpDir('cs');
  put(path.join(csharp, 'nuget', 'pkg', '1.0.0', 'a.nupkg'), 'n');
  put(path.join(csharp, 'dotnet', 'sentinel'), 's');
  const node = tmpDir('nd');
  put(path.join(node, 'npm', '_cacache', 'index-v5', 'x'), 'i');
  const snapshots = {
    csharp: { dir: csharp, parts: { nuget: treeHash(path.join(csharp, 'nuget')), dotnet: treeHash(path.join(csharp, 'dotnet')) } },
    node: { dir: node, parts: { npm: treeHash(path.join(node, 'npm')) } },
  };
  const layout = layoutPaths(tmpDir('sess'), 'abc123');
  for (const d of [layout.nuget, layout.npm, layout.home]) mkdirSync(d, { recursive: true });
  const r = seedCaches({ layout, snapshots });
  assert.equal(r.ok, true, r.reason);
  assert.equal(readFileSync(path.join(layout.nuget, 'pkg', '1.0.0', 'a.nupkg'), 'utf8'), 'n');
  assert.equal(readFileSync(path.join(layout.home, '.dotnet', 'sentinel'), 'utf8'), 's');
  assert.equal(readFileSync(path.join(layout.npm, '_cacache', 'index-v5', 'x'), 'utf8'), 'i');
  assert.deepEqual(r.value.hashes, { nuget: snapshots.csharp.parts.nuget, dotnet: snapshots.csharp.parts.dotnet, npm: snapshots.node.parts.npm });
  assert.ok((statSync(path.join(layout.nuget, 'pkg')).mode & 0o200) !== 0, 'the copy is writable: the session needs to add to its cache');
  // a snapshot that no longer matches its registered hash is refused, naming the part
  const bad = seedCaches({ layout: layoutPaths(tmpDir('sess'), 'def456'), snapshots: { ...snapshots, node: { dir: node, parts: { npm: '0'.repeat(64) } } } });
  assert.equal(bad.ok, false);
  assert.match(bad.reason, /npm/);
});

test('The_recipe_is_registered_with_both_stacks_and_a_pick_rule_for_the_types_version', () => {
  const r = loadRecipe(JSON.parse(readFileSync(path.join(spikeDir, 'inputs', 'cache-recipe.json'), 'utf8')));
  assert.equal(r.ok, true, r.reason);
  assert.deepEqual(Object.keys(r.value.stacks).sort(), ['csharp', 'node']);
  assert.equal(r.value.hosts, 'bare');
  assert.equal(loadRecipe({ ...r.value, hosts: 'mystery' }).ok, false);
  assert.equal(loadRecipe({ stacks: { csharp: { steps: [] } }, hosts: 'bare' }).ok, false, 'a recipe with no step, or no capture, is refused');
});

test('The_highest_published_version_is_picked_numerically_not_alphabetically', () => {
  assert.equal(pickHighest(JSON.stringify(['26.0.9', '26.10.2', '26.2.0'])), '26.10.2');
  assert.equal(pickHighest(JSON.stringify('26.1.0')), '26.1.0');
  assert.equal(pickHighest('not json'), null);
  assert.equal(pickHighest(JSON.stringify(['26.0.0-beta.1', '26.0.0'])), '26.0.0', 'a pre-release is never the pin');
});

// ---- the recipe, run for real in a box (no network needed: the stub recipe only writes files) -------------------------------------

test('A_recipe_runs_in_a_box_and_its_caches_are_captured_hashed_and_made_read_only', async t => {
  if (!sandboxGate(t)) return;
  const root = homeDir('snap-root');
  const out = homeDir('snap-out');
  const recipe = {
    hosts: 'bare', stepTimeoutMs: 60_000,
    stacks: {
      csharp: {
        steps: [{ argv: ['sh', '-c', 'mkdir -p "$NUGET_PACKAGES/demo/1.0.0" "$DOTNET_CLI_HOME/.dotnet" && echo x > "$NUGET_PACKAGES/demo/1.0.0/a.txt" && echo s > "$DOTNET_CLI_HOME/.dotnet/sentinel" && echo "$HTTPS_PROXY" > proxy.txt']  }],
        capture: { nuget: 'box/nuget', dotnet: 'box/home/.dotnet' },
      },
    },
  };
  const r = await makeSnapshot({ stack: 'csharp', recipe, root, out });
  t.after(() => makeWritable(out));
  assert.equal(r.ok, true, r.reason);
  const snap = r.value;
  assert.match(path.basename(snap.dir), /^cache-csharp-[0-9a-f]{12}$/);
  assert.equal(path.dirname(snap.dir), out);
  assert.equal(readFileSync(path.join(snap.dir, 'nuget', 'demo', '1.0.0', 'a.txt'), 'utf8'), 'x\n');
  assert.equal(snap.treeSha256.slice(0, 12), path.basename(snap.dir).slice(-12), 'the name carries the hash');
  assert.equal(treeHash(snap.dir), snap.treeSha256, 'the hash is of the final directory');
  assert.deepEqual(snap.parts, { nuget: treeHash(path.join(snap.dir, 'nuget')), dotnet: treeHash(path.join(snap.dir, 'dotnet')) });
  assert.equal(statSync(snap.dir).mode & 0o222, 0, 'read-only');
  assert.equal(statSync(path.join(snap.dir, 'nuget', 'demo', '1.0.0', 'a.txt')).mode & 0o222, 0);
  assert.ok(snap.sizeBytes > 0);
  assert.deepEqual(snap.packages.map(p => `${p.ecosystem}:${p.name}@${p.version}`), ['nuget:demo@1.0.0']);
  assert.deepEqual(readdirSync(out), [path.basename(snap.dir)], 'no building directory is left beside it');
  assert.equal(existsSync(path.join(root, 'snapshot-csharp-1')), true, 'the work dir (log, egress) stays under the work root, outside the snapshot');
});

test('A_step_that_fails_makes_no_snapshot', async t => {
  if (!sandboxGate(t)) return;
  const out = homeDir('snap-out');
  const recipe = { hosts: 'bare', stepTimeoutMs: 60_000, stacks: { csharp: { steps: [{ argv: ['sh', '-c', 'exit 7'] }], capture: { nuget: 'box/nuget' } } } };
  const r = await makeSnapshot({ stack: 'csharp', recipe, root: homeDir('snap-root'), out });
  assert.equal(r.ok, false);
  assert.match(r.reason, /exit 7|exited 7/);
  assert.deepEqual(readdirSync(out), [], 'nothing half-made is left in the output directory');
});
