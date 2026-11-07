// A frozen file rewritten DURING the real build (the final seam review of F1, item 1; parent D65), in the real build box with the registered snapshots. The C# twin of T-L62: the agent's `App.csproj` is its own, and an MSBuild
// target in it rewrites `run.sh` inside the writable box; `commitBuild` puts the launcher back, the passes would run the restored file, and the change is an agent deviation `changed during the build`. The Node control: with
// the template's `.npmrc` (`ignore-scripts=true`) a `preinstall` script cannot do the same, so nothing is recorded; the same script without the key does rewrite the file and is caught, which is why the control can fail.
// The build's output folder is not frozen and is not touched. Behind `needs` (bwrap, the user bus, the pinned toolchains, the registered snapshots): skipped with the reason, a failure under BENCH_REQUIRE_SANDBOX=1.
// No fake home: the pinned toolchains are found under the real one.
import assert from 'node:assert/strict';
import { existsSync, readFileSync, statSync, writeFileSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { test } from 'node:test';
import { ownedFor } from '../../../.agents/conventions/tools/lib/owned.mjs';
import { SCRIPTS, PIN_FILES } from '../../lang-bench/lib/frozen-set.mjs';
import { NPMRC_OFFLINE } from '../../lang-bench/lib/cache-offline.mjs';
import { ensureTestRoot } from '../../lang-bench/test/support/cache-box.mjs';
import { needs, toolchains } from '../../lang-bench/test/support/exec-layer-host.mjs';
import { ownedFolder } from '../../lang-bench/test/toolchain-fixtures.mjs';
import { commitBuild } from '../lib/build-commit.mjs';
import { boxBuild } from '../lib/build-quick.mjs';
import { DURING_BUILD } from '../lib/copy-quick.mjs';
import { loadExplore } from '../lib/inputs.mjs';
import { quickSnapshots } from '../lib/quick.mjs';
import { prepareTree } from '../lib/score-quick.mjs';
import { layDaemonTree } from './quick-real-programs.mjs';

const TIMEOUT = { timeout: 900_000 };
const REWRITE_CS = '<Target Name="RewriteTheLauncher" AfterTargets="Build"><WriteLinesToFile File="$(MSBuildProjectDirectory)/run.sh" Lines="#!/usr/bin/env bash;echo a launcher the build wrote" Overwrite="true" /></Target>';
const REWRITE_NODE = 'echo a launcher the build wrote > run.sh';

/** The agent's tree: the daemon template of `stack` with the invented program, `edit(dir)` applied, and the frozen files of it (the scripts and the pin files except `leave`) as a file map. */
function agentTree(t, { stack, edit, leave = [] }) {
  const root = ownedFolder(t, 'bfz', ensureTestRoot());
  const source = path.join(root, 'tree');
  layDaemonTree({ stack, dest: source });
  const names = [...SCRIPTS, ...PIN_FILES[stack]].filter(rel => !leave.includes(rel));
  const frozen = Object.fromEntries(names.map(rel => [rel, { bytes: readFileSync(path.join(source, rel)), mode: statSync(path.join(source, rel)).mode & 0o777 }]));
  edit(source);
  return { root, source, frozen };
}

/** `commitBuild` over the real box. `frozen` is the file map as the template made it, read before `edit`. */
async function realCommit({ root, source, stack, frozen }) {
  const snapshots = quickSnapshots({ base: loadExplore().value, home: os.homedir() });
  assert.equal(snapshots.ok, true, snapshots.reason);
  const box = boxBuild({ owned: ownedFor({ token: 'bench' }), dirs: toolchains, snapshots: snapshots.value, stack });
  const a = { source, stack, files: frozen, profile: { kind: 'daemon' }, contractIdentityFn: () => 'a'.repeat(64) };
  const work = path.join(root, 'score');
  const made = await commitBuild({ work, prepare: async dest => prepareTree(a, dest), build: ({ tree }) => box({ tree, timeoutMs: 600_000 }) });
  return { made, work, tree: path.join(work, 'tree') };
}

const csproj = dir => path.join(dir, 'App.csproj');

test('A_csharp_MSBuild_target_that_rewrites_run_sh_in_the_real_box_is_put_back_and_recorded_as_changed_during_the_build', TIMEOUT, async t => {
  if (!needs(t)) return;
  const tree = agentTree(t, { stack: 'csharp', edit: dir => writeFileSync(csproj(dir), readFileSync(csproj(dir), 'utf8').replace('</Project>', `${REWRITE_CS}</Project>`)) });
  const launcher = tree.frozen['run.sh'].bytes;
  const { made, work, tree: built } = await realCommit({ ...tree, stack: 'csharp' });
  assert.equal(made.ok, true, made.reason);
  assert.equal(made.value.ok, true, `the build passed: ${made.value.tail}`);
  assert.equal(readFileSync(path.join(built, 'run.sh')).equals(launcher), true, 'the launcher the passes would run is the frozen one');
  assert.deepEqual(made.value.frozenChanges, [{ path: 'run.sh', state: 'changed', reason: DURING_BUILD }], 'one agent deviation, with the new reason');
  assert.deepEqual(JSON.parse(readFileSync(path.join(work, 'build.json'), 'utf8')).frozenChanges, made.value.frozenChanges, 'and it is in build.json');
  assert.equal(existsSync(path.join(built, 'bin', 'Release', 'net10.0', 'App.dll')), true, 'the output folder is not frozen and is left as the build made it');
});

test('The_csharp_control_with_no_such_target_records_no_deviation', TIMEOUT, async t => {
  if (!needs(t)) return;
  const tree = agentTree(t, { stack: 'csharp', edit: () => {} });
  const { made } = await realCommit({ ...tree, stack: 'csharp' });
  assert.equal(made.ok, true, made.reason);
  assert.deepEqual(made.value.frozenChanges, [], 'the control: an ordinary build changes no frozen file, so the record above can fail');
});

/** Adds a `preinstall` script that rewrites `run.sh` to the tree's `package.json`. */
const withPreinstall = dir => {
  const file = path.join(dir, 'package.json');
  const pkg = JSON.parse(readFileSync(file, 'utf8'));
  writeFileSync(file, `${JSON.stringify({ ...pkg, scripts: { preinstall: REWRITE_NODE } }, null, 2)}\n`);
};

test('The_Node_control_with_ignore_scripts_cannot_rewrite_run_sh_so_nothing_is_recorded', TIMEOUT, async t => {
  if (!needs(t)) return;
  const tree = agentTree(t, { stack: 'node', edit: withPreinstall });
  const { made, tree: built } = await realCommit({ ...tree, stack: 'node' });
  assert.equal(made.ok, true, made.reason);
  assert.equal(made.value.ok, true, `the build passed: ${made.value.tail}`);
  assert.equal(readFileSync(path.join(built, 'run.sh')).equals(tree.frozen['run.sh'].bytes), true);
  assert.deepEqual(made.value.frozenChanges, [], 'ignore-scripts closes the Node way, so there is nothing to put back');
});

test('The_same_Node_script_without_ignore_scripts_does_rewrite_run_sh_and_is_caught_and_put_back', TIMEOUT, async t => {
  if (!needs(t)) return;
  const tree = agentTree(t, { stack: 'node', leave: ['.npmrc'], edit: dir => { withPreinstall(dir); writeFileSync(path.join(dir, '.npmrc'), `${NPMRC_OFFLINE}update-notifier=false\n`); } });
  const { made, tree: built } = await realCommit({ ...tree, stack: 'node' });
  assert.equal(made.ok, true, made.reason);
  assert.equal(readFileSync(path.join(built, 'run.sh')).equals(tree.frozen['run.sh'].bytes), true, 'put back');
  assert.deepEqual(made.value.frozenChanges, [{ path: 'run.sh', state: 'changed', reason: DURING_BUILD }], 'the control: the key is what closes the Node way, and the new check catches the way when it is open');
});
