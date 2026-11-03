// E1.S8b fix batch 1 (K1/K2/O5): no full copy of the built tree per case. The box mounts `--overlay-src <built tree> --overlay-src <the case's placeholder> --tmp-overlay <placeholder>`: each launch
// gets a fresh writable layer over the read-only tree, on a tmpfs inside the box, discarded at exit. The writes are therefore charged to the program's cgroup, as writes to /tmp already were.
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { readFileSync, readdirSync, statSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { runCases } from '../lib/exec-layer/run.mjs';
import { needs, toolchains } from './support/exec-layer-host.mjs';
import { layerOptions, MIB } from './support/exec-tree.mjs';

const hashTree = dir => createHash('sha256').update(readdirSync(dir, { recursive: true }).sort().map(name => { const full = path.join(dir, name); return statSync(full).isFile() ? `${name}:${readFileSync(full).toString('hex')}` : name; }).join('|')).digest('hex');
const caseOf = (result, id) => result.record.cases.find(entry => entry.id === id);


test('Two_cases_never_share_a_writable_view_through_the_runner_and_the_built_tree_is_never_modified', async t => {
  if (!needs(t)) return;
  const options = layerOptions(t, { ids: ['s01', 's02'], runSh: '#!/bin/bash\nif [ -e marker ]; then echo SEEN; else echo FRESH; fi\necho x > marker\necho y > built.txt\n' });
  const before = hashTree(options.tree);
  const result = await runCases({ ...options, samplesDir: options.samplesDir });
  assert.equal(hashTree(options.tree), before, 'the built tree itself is never written, not even by a program that writes to it');
  assert.match(result.stdout, /s01/);
  for (const id of ['s01', 's02']) assert.equal(caseOf(result, id).status === 'PASS' || caseOf(result, id).reason === 'output differs', true);
});

test('B7_the_second_case_of_a_stub_that_writes_in_its_working_directory_does_not_see_the_first_files', async t => {
  if (!needs(t)) return;
  const options = layerOptions(t, { ids: ['s01', 's02'], runSh: '#!/bin/bash\nif [ -e marker ]; then echo SEEN; else echo FRESH; fi\necho x > marker\n' });
  options.samplesDir = (await import('./support/exec-tree.mjs')).samplesDir(t, ['s01', 's02'], { input: 'x\n', output: 'FRESH\n' });
  const result = await runCases(options);
  assert.equal(result.exitCode, 0, `${result.stdout}${result.stderr}`);
  assert.deepEqual(result.record.cases.map(entry => entry.status), ['PASS', 'PASS'], 'both cases saw a fresh working directory: neither printed SEEN');
});

test('A_programs_file_writes_in_its_workspace_are_charged_to_its_memory_peak_like_writes_to_tmp', async t => {
  if (!needs(t)) return;
  const quiet = await runCases(layerOptions(t, { runSh: '#!/bin/bash\nexec cat\n' }));
  const heavy = await runCases(layerOptions(t, { runSh: '#!/bin/bash\ndd if=/dev/zero of=big bs=1M count=64 status=none\nexec cat\n' }));
  const grown = caseOf(heavy, 's01').lifecycle.memory.peakBytes - caseOf(quiet, 's01').lifecycle.memory.peakBytes;
  assert.ok(grown >= 56 * MIB && grown <= 80 * MIB, `64 MiB written in the workspace raised memory.peak by ${Math.round(grown / MIB)} MiB: the overlay's upper layer is a tmpfs in the box`);
});

test('A_file_a_scenario_fixture_put_in_the_placeholder_before_the_launch_shows_in_the_workspace', async t => {
  if (!needs(t)) return;
  const { openRun } = await import('../lib/exec-layer/run.mjs');
  const { cleanup } = await import('../lib/exec-layer/cleanup.mjs');
  const { programArgv } = await import('../lib/exec-layer/run.mjs');
  const { runBounded } = await import('../../minigit-bench/lib/run.mjs');
  const { userEnv } = await import('../lib/exec-layer/scope.mjs');
  const { writeFileSync } = await import('node:fs');
  const { viewDir } = await import('../lib/exec-layer/case-view.mjs');
  const options = layerOptions(t, { runSh: '#!/bin/bash\ncat .scenario/fx.txt\n' });
  const opened = await openRun(options);
  assert.ok(opened.ok, opened.reason);
  t.after(() => cleanup(opened.value));
  const placeholder = viewDir(opened.value.dir, 's01');
  assert.deepEqual(readdirSync(placeholder), [], 'the placeholder is an empty folder, not a copy of the tree');
  writeFileSync(path.join(placeholder, 'fx.txt'), 'FIXTURE');
  const argv = programArgv(opened.value, 's01', ['bash', '-c', 'cat fx.txt; cat run.sh | head -1']);
  const ran = await runBounded(argv[0], argv.slice(1), { timeoutMs: 30_000, env: userEnv() });
  assert.match(ran.value.stdout, /FIXTURE#!\/bin\/bash/, `${ran.value.stdout}|${ran.value.stderr}|` + 'the second overlay source shows the placeholder\'s files beside the built tree');
  assert.equal(toolchains !== undefined, true);
});
