// The workspaces of a bare session (E3b.S1 step 4): v1 holds exactly the pinned v1 inputs; v2 is the v1 tree minus build output plus the v2 inputs.
import assert from 'node:assert/strict';
import { mkdirSync, readFileSync, readdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { sha256Of } from '../lib/inputs.mjs';
import { fillV1, fillV2, pinnedInputsChanged } from '../lib/workspace.mjs';
import { tmpDir } from './helpers.mjs';

/** A fake fetched-inputs cache and its hashes entries, shaped like `fetchInputs` + `hashes.txt`: the upstream SPECs and the FIXED visible scripts. */
function fakeCache() {
  const root = tmpDir('cache');
  mkdirSync(path.join(root, 'upstream'));
  const files = { 'upstream/SPEC-v1.txt': 'spec one\n', 'upstream/SPEC-v2.txt': 'spec two\n', 'test-v1.sh': '#!/bin/bash\necho fixed one\n', 'test-v2.sh': '#!/bin/bash\necho fixed two\n', 'upstream/test-v1.sh': 'ORIGINAL ONE\n' };
  for (const [p, text] of Object.entries(files)) writeFileSync(path.join(root, p), text);
  return { root, entries: Object.entries(files).map(([p, text]) => ({ path: p, sha256: sha256Of(Buffer.from(text)) })) };
}
const list = dir => readdirSync(dir).sort();
const put = (p, text) => { mkdirSync(path.dirname(p), { recursive: true }); writeFileSync(p, text); };

test('A_v1_workspace_holds_exactly_the_pinned_v1_inputs', () => {
  const cache = fakeCache();
  const work = tmpDir('work');
  const r = fillV1({ inputsRoot: cache.root, hashes: cache.entries, work });
  assert.equal(r.ok, true, r.reason);
  assert.deepEqual(list(work), ['SPEC-v1.txt', 'test-v1.sh'], 'nothing else: no SPEC-v2, no upstream tests');
  assert.equal(readFileSync(path.join(work, 'SPEC-v1.txt'), 'utf8'), 'spec one\n');
  assert.equal(readFileSync(path.join(work, 'test-v1.sh'), 'utf8'), '#!/bin/bash\necho fixed one\n', 'the FIXED script, not the upstream original');
  assert.deepEqual(pinnedInputsChanged({ work, hashes: cache.entries, phase: 'v1' }), []);
});

test('A_v1_workspace_is_refused_when_a_cache_file_does_not_match_its_hash', () => {
  const cache = fakeCache();
  writeFileSync(path.join(cache.root, 'upstream', 'SPEC-v1.txt'), 'tampered\n');
  const r = fillV1({ inputsRoot: cache.root, hashes: cache.entries, work: tmpDir('work') });
  assert.equal(r.ok, false);
  assert.match(r.reason, /SPEC-v1\.txt/);
});

test('A_v2_workspace_is_the_v1_tree_minus_build_output_plus_v2_inputs', () => {
  const cache = fakeCache();
  const v1 = tmpDir('v1');
  fillV1({ inputsRoot: cache.root, hashes: cache.entries, work: v1 });
  put(path.join(v1, 'Program.cs'), 'class P {}');
  put(path.join(v1, 'src', 'obj.ts'), 'export const obj = 1;'); // a FILE whose name starts like a build dir: kept
  put(path.join(v1, 'bin', 'Debug', 'minigit.dll'), 'dll');
  put(path.join(v1, 'obj', 'project.assets.json'), '{}');
  put(path.join(v1, 'node_modules', 'x', 'index.js'), '');
  put(path.join(v1, 'sub', 'obj', 'nested.txt'), 'nested build output');
  writeFileSync(path.join(v1, 'test-v1.sh'), 'the agent edited this\n'); // the agent changed a pinned input
  const work = tmpDir('v2');
  const r = fillV2({ inputsRoot: cache.root, hashes: cache.entries, v1Work: v1, work, stack: 'csharp' });
  assert.equal(r.ok, true, r.reason);
  assert.deepEqual(list(work), ['Program.cs', 'SPEC-v1.txt', 'SPEC-v2.txt', 'src', 'sub', 'test-v1.sh', 'test-v2.sh']);
  assert.deepEqual(list(path.join(work, 'src')), ['obj.ts']);
  assert.deepEqual(list(path.join(work, 'sub')), [], 'a nested obj/ is build output at any depth');
  assert.equal(readFileSync(path.join(work, 'test-v1.sh'), 'utf8'), 'the agent edited this\n', 'the edit is kept, not repaired');
  assert.equal(readFileSync(path.join(work, 'test-v2.sh'), 'utf8'), '#!/bin/bash\necho fixed two\n');
  assert.deepEqual(r.value.flags, ['v1 inputs changed']);
  assert.deepEqual(r.value.changed.map(c => c.path), ['test-v1.sh']);
  assert.deepEqual(pinnedInputsChanged({ work, hashes: cache.entries, phase: 'v2' }).map(c => c.path), ['test-v1.sh']);
  writeFileSync(path.join(work, 'test-v2.sh'), 'edited in v2\n');
  assert.deepEqual(pinnedInputsChanged({ work, hashes: cache.entries, phase: 'v2' }).map(c => c.path), ['test-v1.sh', 'test-v2.sh'], 'a v2 session is checked on the v2 inputs too');
  assert.deepEqual(pinnedInputsChanged({ work, hashes: cache.entries, phase: 'v1' }).map(c => c.path), ['test-v1.sh'], 'and a v1 check is only about the v1 inputs');
});

test('A_v2_workspace_of_the_node_stack_keeps_a_real_bin_directory_of_source', () => {
  const cache = fakeCache();
  const v1 = tmpDir('v1');
  fillV1({ inputsRoot: cache.root, hashes: cache.entries, work: v1 });
  put(path.join(v1, 'bin', 'minigit.ts'), 'source');
  put(path.join(v1, 'node_modules', 'x', 'index.js'), '');
  const work = tmpDir('v2');
  const r = fillV2({ inputsRoot: cache.root, hashes: cache.entries, v1Work: v1, work, stack: 'node' });
  assert.equal(r.ok, true, r.reason);
  assert.deepEqual(list(work), ['SPEC-v1.txt', 'SPEC-v2.txt', 'bin', 'test-v1.sh', 'test-v2.sh']);
  assert.deepEqual(r.value.flags, [], 'untouched inputs raise no flag');
});

test('A_changed_or_absent_pinned_input_is_reported_with_absent_as_null', () => {
  const cache = fakeCache();
  const v1 = tmpDir('v1');
  fillV1({ inputsRoot: cache.root, hashes: cache.entries, work: v1 });
  writeFileSync(path.join(v1, 'SPEC-v1.txt'), 'x');
  const changed = pinnedInputsChanged({ work: v1, hashes: cache.entries, phase: 'v1' });
  assert.equal(changed.length, 1);
  assert.equal(changed[0].actual === null, false);
  writeFileSync(path.join(v1, 'SPEC-v1.txt'), '');
  const gone = tmpDir('gone');
  assert.equal(pinnedInputsChanged({ work: gone, hashes: cache.entries, phase: 'v1' }).every(c => c.actual === null), true, 'absent is recorded as null, not as unchanged');
});
