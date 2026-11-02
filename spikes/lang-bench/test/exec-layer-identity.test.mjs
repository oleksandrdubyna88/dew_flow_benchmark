// E1.S8b: the layer's ONE identity — the sha256 of its tree, every file of `lib/exec-layer/` (`scope-entry.sh` included) — written to `inputs/specs.d/exec-layer.json` and assembled into
// `inputs/specs.json` beside the runner's. Every record of E2 and E3 names it (D36, D43); a change to any file of the tree re-dates their evidence (parent §6.1).
import assert from 'node:assert/strict';
import { readFileSync, readdirSync, symlinkSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { computeIdentity, LAYER_DIR, SPIKES_ROOT, verifyIdentity } from '../lib/exec-layer/identity.mjs';
import { MEMORY_POLICY, POLICY_FILE } from '../lib/exec-layer/policy.mjs';
import { runCases } from '../lib/exec-layer/run.mjs';
import { needs } from './support/exec-layer-host.mjs';
import { layerOptions } from './support/exec-tree.mjs';
import { checkRecord } from '../lib/specs-record.mjs';
import { spikeDir } from './support/exec-layer-host.mjs';
import { owned } from './support/owned.mjs';
import { writeTree } from './toolchain-fixtures.mjs';

const recordFile = path.join(spikeDir, 'inputs', 'specs.d', 'exec-layer.json');

test('The_identity_of_the_real_tree_lists_every_file_scope_entry_included', () => {
  const identity = computeIdentity(LAYER_DIR);
  assert.ok(identity.ok, identity.reason);
  const names = identity.value.execLayer.files.map(file => file.path);
  for (const expected of ['scope-entry.sh', 'env.mjs', 'box.mjs', 'netns.mjs', 'scope.mjs', 'case-view.mjs', 'run.mjs', 'cleanup.mjs', 'identity.mjs']) assert.ok(names.includes(`lang-bench/lib/exec-layer/${expected}`), `${expected} is in the identity`);
  for (const outside of ['minigit-bench/lib/bwrap.mjs', 'minigit-bench/lib/run.mjs', 'minigit-bench/lib/find-on-path.mjs', 'minigit-bench/lib/result.mjs', 'lang-bench/lib/preflight-box.mjs']) assert.ok(names.includes(outside), `${outside}: what shapes the box's argv and every launch is in the identity`);
  assert.deepEqual(names, [...names].sort(), 'sorted, so the manifest is stable');
  assert.ok(names.every(name => !name.startsWith('/') && !name.includes('..')), 'paths are relative to the spikes folder');
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

const IMPORT = /(?:^|\n)\s*(?:import|export)\b[^'"\n]*?from\s+['"](\.[^'"]+)['"]/g;

/** Every relative import reachable from the layer's own files, found by a second, independent walk (the test finds an unlisted import). */
function reachableOutside(dir, root) {
  const seen = new Set();
  const stack = readdirSync(dir).filter(name => name.endsWith('.mjs')).map(name => path.join(dir, name));
  while (stack.length > 0) {
    const file = stack.pop();
    for (const match of readFileSync(file, 'utf8').matchAll(IMPORT)) {
      const target = path.resolve(path.dirname(file), match[1]);
      if (seen.has(target)) continue;
      seen.add(target);
      stack.push(target);
    }
  }
  return [...seen].filter(file => !file.startsWith(`${dir}${path.sep}`)).map(file => path.relative(root, file).split(path.sep).join('/'));
}

test('Every_module_outside_the_layer_that_the_layer_imports_is_in_the_identity', () => {
  const listed = new Set(computeIdentity(LAYER_DIR).value.execLayer.files.map(file => file.path));
  const outside = reachableOutside(LAYER_DIR, SPIKES_ROOT);
  assert.ok(outside.length >= 5, `the walk finds the helpers the layer imports (${outside.join(', ')})`);
  assert.deepEqual(outside.filter(file => !listed.has(file)), [], 'no import of the layer is left out of the identity');
});

function syntheticLayer(t) {
  const root = writeTree(owned.ownedDir(t, 'idspikes'), {
    'lang-bench/lib/exec-layer/a.mjs': "import { x } from '../../../other/lib/outside.mjs';\nimport fs from 'node:fs';\nexport const a = x;\n",
    'other/lib/outside.mjs': "export { deep as x } from './deep.mjs';\n",
    'other/lib/deep.mjs': 'export const deep = 1;\n',
    'other/lib/unrelated.mjs': 'export const u = 1;\n',
  });
  return root;
}

test('A_byte_changed_in_a_module_the_layer_imports_changes_the_identity_and_a_check_names_it', t => {
  const root = syntheticLayer(t);
  const dir = path.join(root, 'lang-bench', 'lib', 'exec-layer');
  const base = computeIdentity(dir, { root });
  assert.deepEqual(base.value.execLayer.files.map(file => file.path), ['lang-bench/lib/exec-layer/a.mjs', 'other/lib/deep.mjs', 'other/lib/outside.mjs'], 'the transitive imports, not the unrelated file');
  writeFileSync(path.join(root, 'other', 'lib', 'deep.mjs'), 'export const deep = 2;\n');
  const edited = computeIdentity(dir, { root });
  assert.notEqual(edited.value.execLayer.treeSha256, base.value.execLayer.treeSha256);
  const verdict = verifyIdentity(dir, base.value, { root });
  assert.equal(verdict.ok, false);
  assert.match(verdict.reason, /other\/lib\/deep\.mjs changed/);
});

test('A_data_run_refuses_a_layer_that_is_not_the_pinned_identity_and_every_result_names_the_live_hash', async t => {
  if (!needs(t)) return;
  const live = computeIdentity(LAYER_DIR).value.execLayer.treeSha256;
  const folder = owned.ownedDir(t, 'pinned');
  const pinned = (name, sha) => { const file = path.join(folder, name); writeFileSync(file, JSON.stringify({ execLayer: { treeSha256: sha, files: [] } })); return file; };
  const refused = await runCases({ ...layerOptions(t, { data: true }), pinnedIdentityFile: pinned('stale.json', '0'.repeat(64)) });
  assert.equal(refused.exitCode, 3);
  assert.equal(refused.halt, 'layer-identity-mismatch');
  assert.equal(refused.treeSha256, live, 'the result names the live hash');
  assert.equal(refused.record, null, 'no case was started');
  const accepted = await runCases({ ...layerOptions(t, { data: true }), pinnedIdentityFile: pinned('right.json', live) });
  assert.equal(accepted.exitCode, 0, accepted.stderr);
  assert.equal(accepted.treeSha256, live);
  const notData = await runCases({ ...layerOptions(t, { data: false }), pinnedIdentityFile: pinned('stale2.json', '1'.repeat(64)) });
  assert.equal(notData.exitCode, 0, 'a non-data run proceeds');
  assert.equal(notData.treeSha256, live);
  const missing = await runCases({ ...layerOptions(t, { data: true }), pinnedIdentityFile: path.join(folder, 'absent.json') });
  assert.equal(missing.halt, 'layer-identity-mismatch', 'no pinned identity is no identity to check against: a data run refuses');
});

const POLICY_PATH = 'lang-bench/inputs/specs.d/memory-policy.json';

test('The_memory_policy_file_is_part_of_the_layer_identity_and_a_byte_of_it_changes_the_hash', t => {
  const real = computeIdentity(LAYER_DIR).value.execLayer;
  assert.ok(real.files.some(file => file.path === POLICY_PATH), `${POLICY_PATH} is in the identity (parent §6.1: the policy is one identity of the series)`);
  assert.equal(path.relative(SPIKES_ROOT, POLICY_FILE).split(path.sep).join('/'), POLICY_PATH);
  const root = syntheticLayer(t);
  const policy = path.join(root, POLICY_PATH);
  writeTree(root, { [POLICY_PATH]: `${JSON.stringify({ policy: MEMORY_POLICY })}\n` });
  const dir = path.join(root, 'lang-bench', 'lib', 'exec-layer');
  const base = computeIdentity(dir, { root, policyFile: policy });
  assert.ok(base.value.execLayer.files.some(file => file.path === POLICY_PATH), 'the synthetic identity lists it too');
  writeFileSync(policy, `${JSON.stringify({ policy: MEMORY_POLICY })} \n`);
  const edited = computeIdentity(dir, { root, policyFile: policy });
  assert.notEqual(edited.value.execLayer.treeSha256, base.value.execLayer.treeSha256, 'one byte of the policy file changes the layer identity');
  assert.match(verifyIdentity(dir, base.value, { root, policyFile: policy }).reason, new RegExp(`${POLICY_PATH.replace(/\./g, '\\.')} changed`));
  assert.equal(computeIdentity(dir, { root }).value.execLayer.files.some(file => file.path === POLICY_PATH), false, 'a layer folder that is not the real one lists no policy unless it is given');
});

test('A_data_run_refuses_a_memory_policy_other_than_the_pinned_one_exit_3_memory_view_mismatch', async t => {
  const folder = owned.ownedDir(t, 'policies');
  const file = (name, content) => { const target = path.join(folder, name); writeFileSync(target, typeof content === 'string' ? content : JSON.stringify(content)); return target; };
  const decided = policy => ({ policy, decided: '2026-11-02', by: 'team lead', evidence: [] });
  const others = [file('old.json', decided('cgroup-view-own+gchl-limit')), file('ro.json', decided('cgroup-view-readonly')), file('undecided.json', { policy: null, decided: null, by: null }),
    file('bad.json', '{ not json'), path.join(folder, 'absent.json')];
  for (const policyFile of others) {
    const refused = await runCases({ ...layerOptions(t, { data: true }), policyFile });
    assert.equal(refused.exitCode, 3, policyFile);
    assert.equal(refused.halt, 'memory-view-mismatch', policyFile);
    assert.match(refused.stderr, /memory-view-mismatch.*cgroup-view-own\+gchl-90\+node-old-space-90/s, 'the message names the pinned policy');
    assert.equal(refused.record, null, 'no case was started');
  }
  const notData = await runCases({ ...layerOptions(t, { data: false }), policyFile: others[0] });
  assert.notEqual(notData.halt, 'memory-view-mismatch', 'a non-data run does not read the policy');
  if (!needs(t)) return;
  const accepted = await runCases({ ...layerOptions(t, { data: true }), policyFile: file('right.json', decided(MEMORY_POLICY)) });
  assert.equal(accepted.exitCode, 0, accepted.stderr);
  const committed = await runCases({ ...layerOptions(t, { data: true }) });
  assert.equal(committed.exitCode, 0, `the committed policy file is the pinned one: ${committed.stderr}`);
});

test('A_data_run_names_csharp_or_node_and_a_misspelled_or_missing_stack_is_refused_before_anything_starts', async t => {
  for (const stack of [undefined, 'dotnet', 'typescript', '', null]) {
    const refused = await runCases({ ...layerOptions(t, { data: true }), stack });
    assert.equal(refused.exitCode, 3, String(stack));
    assert.equal(refused.halt, 'stack-not-named', String(stack));
    assert.match(refused.stderr, /stack-not-named.*csharp.*node/s);
    assert.equal(refused.record, null, 'no case was started');
  }
  const notData = await runCases({ ...layerOptions(t, { data: false }), stack: 'dotnet' });
  assert.notEqual(notData.exitCode, 0, 'a non-data run with a misspelled stack does not run either: the box environment throws');
  assert.notEqual(notData.halt, 'stack-not-named');
  if (!needs(t)) return;
  for (const stack of ['csharp', 'node']) {
    const accepted = await runCases({ ...layerOptions(t, { data: true }), stack });
    assert.equal(accepted.exitCode, 0, `${stack}: ${accepted.stderr}`);
  }
});
