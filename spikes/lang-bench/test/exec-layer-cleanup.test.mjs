// E1.S8b: ONE cleanup, called on every path (normal, timeout, exception), that never throws and leaves no process, scope, holder or view of the case — in the delegated order: the
// program's leaf is killed (`prog/cgroup.kill`), the scope holder's leaf is killed so the scope ends, the unit is stopped only as the last resort, the netns holder ends (SIGTERM, SIGKILL
// after 2 s), the views go, and a confirmation names whatever is still there. A resource it cannot remove stays named and reported.
// And the startup sweep: a run killed with SIGKILL mid-case leaves a scope, a holder and a view; the next run's sweep retires all three by owned name and run marker, and touches
// nothing it does not own.
import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import { chmodSync, existsSync, mkdirSync, readdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { cleanup, sweepStale } from '../lib/exec-layer/cleanup.mjs';
import { listScopes } from '../lib/exec-layer/scope.mjs';
import { unitPrefixOf } from '../lib/exec-layer/names.mjs';
import { openRun, runCases } from '../lib/exec-layer/run.mjs';
import { sameProcess, scanProcesses, statOf } from '../lib/exec-layer/proc.mjs';
import { needs } from './support/exec-layer-host.mjs';
import { waitFor } from './support/exec-chain.mjs';
import { layerOptions } from './support/exec-tree.mjs';
import { owned } from './support/owned.mjs';

const KILL_CHILD = fileURLToPath(new URL('./fixtures/exec-layer-killed-run.mjs', import.meta.url));
const LONG = '#!/bin/bash\nexec sleep 300\n';

const holdersOf = id => scanProcesses(argv => argv[0] === owned.ownedPrefix('lhold') + id);

test('Cleanup_leaves_no_scope_no_holder_no_view_and_no_run_folder_after_a_run_that_opened_everything', async t => {
  if (!needs(t)) return;
  const opened = await openRun(layerOptions(t));
  assert.ok(opened.ok, opened.reason);
  const run = opened.value;
  t.after(() => cleanup(run));
  assert.ok(existsSync(run.dir) && readdirSync(path.join(run.dir, 'views')).length === 3, 'the view of s01, the control\'s and the certification\'s');
  const report = await cleanup(run);
  assert.equal(report.clean, true, JSON.stringify(report.left));
  assert.equal(existsSync(run.dir), false, 'the run folder is gone');
  assert.equal(existsSync(`${run.dir}.owner`), false, 'and its claim');
  assert.deepEqual(holdersOf(run.id), [], 'no netns holder');
  assert.deepEqual(await listScopes(run.unitPrefix), [], 'no scope');
  assert.deepEqual((await cleanup(run)).left, [], 'a second call finds nothing and does not throw');
});

test('Cleanup_on_the_timeout_path_ends_a_program_that_is_still_running_and_the_run_is_reported_not_stuck', async t => {
  if (!needs(t)) return;
  const options = layerOptions(t, { runSh: LONG, runCeilingMs: 1_000 });
  const result = await runCases(options);
  assert.notEqual(result.exitCode, 0, 'the layer\'s ceiling ended the run');
  assert.match(result.stderr, /ceiling/);
  assert.equal(result.cleanup.clean, true, JSON.stringify(result.cleanup.left));
  assert.deepEqual(scanProcesses(argv => argv.some(arg => arg.endsWith('run_samples.py')) && argv.some(arg => arg.includes(options.parent))), [], 'no driver of the run is left');
});

test('Cleanup_on_the_exception_path_removes_what_a_run_that_could_not_open_had_made', async t => {
  if (!needs(t)) return;
  const options = layerOptions(t);
  const result = await runCases({ ...options, tree: path.join(options.tree, 'no-such-tree') });
  assert.equal(result.exitCode, 3);
  assert.match(result.stderr, /built tree .* is not a directory/);
  assert.deepEqual(readdirSync(options.parent), [], 'the run folder, its claim and the holder of a run that never started are gone');
});

test('Cleanup_names_a_resource_it_cannot_remove_and_never_throws', async t => {
  if (!needs(t)) return;
  let parent = null; // registered BEFORE the run folder's own removal: `after` hooks run first-registered-first, and an unwritable views folder cannot be removed
  t.after(() => { for (const name of parent === null ? [] : readdirSync(parent)) { try { chmodSync(path.join(parent, name, 'views'), 0o755); } catch { /* not a run folder, or gone */ } } });
  parent = owned.ownedDir(t, 'runs');
  const opened = await openRun(layerOptions(t, { parent }));
  const run = opened.value;
  t.after(() => cleanup(run));
  const views = path.join(run.dir, 'views');
  chmodSync(views, 0o555);
  const report = await cleanup(run);
  assert.equal(report.clean, false);
  assert.ok(report.left.some(entry => entry.kind === 'view' && entry.path.startsWith(views) && /EACCES|EPERM/.test(entry.reason)), JSON.stringify(report.left));
  assert.ok(existsSync(`${run.dir}.owner`), 'the run folder stays claimed, so the next sweep can judge it');
  assert.deepEqual(holdersOf(run.id), [], 'the rest was still cleaned: the holder is gone');
});

test('A_run_killed_with_SIGKILL_mid_case_leaves_a_scope_a_holder_and_a_view_and_the_next_runs_sweep_retires_all_three', async t => {
  if (!needs(t)) return;
  const options = layerOptions(t, { runSh: LONG });
  const child = spawn(process.execPath, [KILL_CHILD, JSON.stringify({ parent: options.parent, tree: options.tree, samplesDir: options.samplesDir })], { stdio: ['ignore', 'pipe', 'pipe'] });
  t.after(() => child.kill('SIGKILL'));
  const runFolder = await waitFor(() => readdirSync(options.parent).find(name => name.startsWith(owned.ownedPrefix('lrun')) && !name.endsWith('.owner')) ?? null, 30_000);
  assert.ok(runFolder, 'the child opened its run folder');
  const runDir = path.join(options.parent, runFolder);
  const id = runFolder.slice(owned.ownedPrefix('lrun').length);
  const prefix = unitPrefixOf(owned, id);
  assert.ok(await waitFor(async () => ((await listScopes(prefix)).some(unit => unit.endsWith('-s01')) ? true : null), 30_000), 'the child run is mid-case: the scope of s01 exists');
  child.kill('SIGKILL');
  await new Promise(resolve => child.once('exit', resolve));
  assert.ok(existsSync(path.join(runDir, 'views', 's01')), 'a view is left');
  assert.equal(holdersOf(id).length, 1, 'a holder is left');
  assert.ok((await listScopes(prefix)).length > 0, 'a scope is left');
  const next = await runCases(layerOptions(t, { parent: options.parent }));
  assert.equal(next.exitCode, 0, next.stderr);
  const kinds = new Set(next.sweep.retired.map(entry => entry.kind));
  for (const kind of ['scope', 'holder', 'view']) assert.ok(kinds.has(kind), `the sweep retired a ${kind}: ${JSON.stringify(next.sweep)}`);
  assert.deepEqual(await listScopes(prefix), [], 'no scope');
  assert.deepEqual(holdersOf(id), [], 'no holder');
  assert.equal(existsSync(runDir), false, 'no view, no run folder');
  assert.deepEqual(scanProcesses(argv => argv.some(arg => arg.endsWith('run_samples.py')) && argv.some(arg => arg.includes(runDir))), [], 'and the orphaned driver of the killed run is gone');
});

test('The_sweep_retires_nothing_it_does_not_own_a_live_run_a_young_run_another_owner_an_unmarked_folder', async t => {
  if (!needs(t)) return;
  const options = layerOptions(t);
  const live = await openRun(options);
  const other = path.join(options.parent, owned.ownedPrefix('lrun').replace(/-u\d+-/, '-u4242424-') + 'zzzzzz');
  mkdirSync(other);
  writeFileSync(`${other}.owner`, `u4242424:${'otherhost'}@linux:1:1:1\n`);
  const unmarked = path.join(options.parent, `${owned.ownedPrefix('lrun')}unmark`);
  mkdirSync(unmarked);
  const report = await sweepStale({ owned, parent: options.parent, minAgeMs: 0 });
  assert.deepEqual(report.retired, [], JSON.stringify(report));
  const reasons = Object.fromEntries(report.kept.map(entry => [entry.name, entry.reason]));
  assert.match(reasons[path.basename(live.value.dir)], /live/);
  assert.match(reasons[path.basename(unmarked)], /unmarked|unknown/);
  assert.ok(!(path.basename(other) in reasons), 'another owner\'s name is not even in this sweep\'s scope');
  assert.ok(existsSync(other) && existsSync(unmarked) && existsSync(live.value.dir));
  const young = await sweepStale({ owned, parent: options.parent, minAgeMs: 3_600_000 });
  assert.deepEqual(young.retired, []);
  await cleanup(live.value);
});

// ---- O9: the driver is ended as a whole process group, and the scopes are listed AGAIN after the holder step ----

/** A stand-in driver: a file named `run_samples.py` naming the run folder (what the cleanup looks for), which starts children of its own — in its process group — until it is ended. */
function fakeDriver(t, run, body) {
  const dir = owned.ownedDir(t, 'fakedriver');
  const script = path.join(dir, 'run_samples.py');
  writeFileSync(script, `import subprocess, sys, time\n${body}\n`);
  const child = spawn('python3', ['-I', script, run.dir], { detached: true, stdio: 'ignore', env: { PATH: '/usr/sbin:/usr/bin:/bin', XDG_RUNTIME_DIR: `/run/user/${process.getuid()}` } });
  t.after(() => { try { process.kill(-child.pid, 'SIGKILL'); } catch { /* gone */ } });
  return child;
}

test('Cleanup_ends_the_driver_as_a_whole_process_group_and_not_only_its_pid', async t => {
  if (!needs(t)) return;
  const opened = await openRun(layerOptions(t));
  const run = opened.value;
  const driver = fakeDriver(t, run, "subprocess.Popen(['sleep', '300'])\ntime.sleep(300)");
  assert.ok(await waitFor(() => scanProcesses(argv => argv[0] === 'sleep' && argv[1] === '300').some(id => statOf(id.pid)?.ppid === driver.pid) || null), 'the driver started its child');
  const kids = scanProcesses(argv => argv[0] === 'sleep' && argv[1] === '300').filter(id => statOf(id.pid)?.ppid === driver.pid);
  const report = await cleanup(run);
  assert.equal(report.clean, true, JSON.stringify(report.left));
  assert.ok(await waitFor(() => kids.every(id => !sameProcess(id)) || null, 5000), 'the driver\'s child, in its process group, is gone too');
});

test('A_driver_killed_while_a_systemd_run_child_is_registering_leaves_no_scope', async t => {
  if (!needs(t)) return;
  const opened = await openRun(layerOptions(t));
  const run = opened.value;
  // children that register their scope a moment AFTER the cleanup has looked: the pid-only kill of the old cleanup leaves them running
  const body = `for i in range(8):\n    subprocess.Popen(['bash', '-c', 'sleep 1.2; exec systemd-run --user --scope --quiet --unit=${run.unitPrefix}-race%d sleep 30' % i])\ntime.sleep(300)`;
  const driver = fakeDriver(t, run, body);
  assert.ok(await waitFor(() => scanProcesses(argv => argv[0] === 'bash' && argv.some(arg => arg.includes('-race0'))).length > 0 || null), 'the children are about to register');
  const report = await cleanup(run);
  assert.equal(report.clean, true, JSON.stringify(report.left));
  await new Promise(resolve => setTimeout(resolve, 3000));
  assert.deepEqual(await listScopes(run.unitPrefix), [], `no scope of the run is left (the driver ${driver.pid} was ended while its children were registering)`);
});
