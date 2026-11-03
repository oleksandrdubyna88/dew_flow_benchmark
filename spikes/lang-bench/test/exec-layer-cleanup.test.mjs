// E1.S8b: ONE cleanup, called on every path (normal, timeout, exception), that never throws and leaves no process, scope, holder or view of the case — in the delegated order: the
// program's leaf is killed (`prog/cgroup.kill`), the scope holder's leaf is killed so the scope ends, the unit is stopped only as the last resort, the netns holder ends (SIGTERM, SIGKILL
// after 2 s), the views go, and a confirmation names whatever is still there. A resource it cannot remove stays named and reported.
// And the startup sweep: a run killed with SIGKILL mid-case leaves a scope, a holder and a view; the next run's sweep retires all three by owned name and run marker, and touches
// nothing it does not own.
import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import { chmodSync, existsSync, mkdirSync, readdirSync, readFileSync, writeFileSync } from 'node:fs';
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

// W4 (fix batch 4): an `openRun` that fails unregisters its run and ends what it opened, at every open step.
test('An_openRun_that_fails_at_any_open_step_leaves_nothing_registered_nothing_on_disk_and_nothing_in_systemd', async t => {
  if (!needs(t)) return;
  const { liveRunCount } = await import('../lib/exec-layer/cleanup.mjs');
  const { startHolder } = await import('../lib/exec-layer/netns.mjs');
  const base = layerOptions(t);
  const holderNames = [];
  const recording = async args => { holderNames.push(args.name); return startHolder(args); }; // this test's own holders are known by name: the other test files' holders (same token `bench`) are not counted
  const steps = {
    'the tree is not a directory': { tree: path.join(base.tree, 'no-such-tree') },
    'the limits file is malformed': { limitsFile: path.join(base.tree, 'run.sh') },
    'the case wall is shorter than the time limit': { caseWallMs: 1 },
    'a unit name would be too long': { caseIds: ['s'.repeat(80)] },
    'the box environment is refused (an unknown stack), after the folder would have been made': { stack: 'dotnet' },
    'the holder does not start': { startHolder: async args => { holderNames.push(args.name); return { ok: false, reason: 'forced: no holder' }; } },
    'the views cannot be made (after the holder started)': { caseIds: ['../escape'], startHolder: recording },
  };
  for (const [step, override] of Object.entries(steps)) {
    const options = { ...layerOptions(t), ...override };
    const before = liveRunCount();
    const opened = await openRun(options);
    assert.equal(opened.ok, false, step);
    assert.equal(liveRunCount(), before, `${step}: the failed run is not left in the live registry`);
    assert.deepEqual(readdirSync(options.parent), [], `${step}: no run folder, claim or view on disk`);
    const running = readdirSync('/proc').filter(entry => /^\d+$/.test(entry)).flatMap(pid => { try { return [readFileSync(`/proc/${pid}/cmdline`, 'utf8').split('\0')[0]]; } catch { return []; } }).filter(name => holderNames.includes(name));
    assert.deepEqual(running, [], `${step}: no holder process of this test's runs`);
  }
});

// B2: a late scope can bring the run folder back after a cleanup pass; every pass removes it directly.
test('Every_cleanup_pass_removes_the_run_folder_itself_even_when_a_late_scope_made_it_again', async t => {
  if (!needs(t)) return;
  const opened = await openRun(layerOptions(t));
  assert.ok(opened.ok, opened.reason);
  const run = opened.value;
  t.after(() => cleanup(run));
  assert.equal((await cleanup(run)).clean, true);
  assert.equal(existsSync(run.dir), false);
  mkdirSync(path.join(run.dir, 'out', 'late-case'), { recursive: true }); // what a scope still in flight did with `mkdir -p`
  const second = await cleanup(run);
  assert.equal(existsSync(run.dir), false, 'the next pass removes the run folder directly, not only through scope.close()');
  assert.equal(second.clean, true, JSON.stringify(second.left));
});

// B2: once the process is stopping, no new launch path starts a scope, a holder or a run (a check in the launch path itself).
test('Once_stopping_is_set_no_run_is_opened_and_no_scope_prefix_is_built', async t => {
  const { spawnSync } = await import('node:child_process');
  const script = `
    import { markStopped, isStopped } from ${JSON.stringify(new URL('../lib/exec-layer/stopping.mjs', import.meta.url).href)};
    import { launchPrefix } from ${JSON.stringify(new URL('../lib/exec-layer/prefix.mjs', import.meta.url).href)};
    import { registerRun } from ${JSON.stringify(new URL('../lib/exec-layer/cleanup.mjs', import.meta.url).href)};
    const fake = { unitPrefix: 'u', dir: '/nonexistent', options: { memoryMaxBytes: 1, dirs: {} }, limits: {}, tools: {}, env: {}, holder: { pid: 1 } };
    const results = { before: isStopped() };
    try { launchPrefix(fake); results.beforePrefix = 'built'; } catch (error) { results.beforePrefix = error.message.slice(0, 60); }
    markStopped();
    results.after = isStopped();
    for (const [name, fn] of [['prefix', () => launchPrefix(fake)], ['register', () => registerRun({})]]) {
      try { fn(); results[name] = 'allowed'; } catch (error) { results[name] = /stopping/.test(error.message) ? 'refused' : error.message; }
    }
    console.log(JSON.stringify(results));
  `;
  const ran = spawnSync(process.execPath, ['--input-type=module', '-e', script], { encoding: 'utf8', timeout: 20_000 });
  const results = JSON.parse(ran.stdout.trim().split('\n').pop());
  assert.equal(results.before, false);
  assert.equal(results.after, true);
  assert.deepEqual([results.prefix, results.register], ['refused', 'refused'], ran.stderr);
});

// M2 (fix batch 5): a cleanup that leaves something keeps the run folder and its claim, so the next start's sweep — which finds a run only through its folder — can still retire what is left.
test('A_cleanup_that_leaves_a_unit_keeps_the_run_folder_and_its_claim_and_a_later_sweep_retires_them', async t => {
  if (!needs(t)) return;
  const { spawnSync } = await import('node:child_process');
  const options = layerOptions(t);
  const { owned: _owned, ...plain } = options;
  const script = `
    import { ownedFor } from ${JSON.stringify(new URL('../../../.agents/conventions/tools/lib/owned.mjs', import.meta.url).href)};
    import { cleanup } from ${JSON.stringify(new URL('../lib/exec-layer/cleanup.mjs', import.meta.url).href)};
    import { openRun } from ${JSON.stringify(new URL('../lib/exec-layer/run.mjs', import.meta.url).href)};
    const opened = await openRun({ ...${JSON.stringify(plain)}, owned: ownedFor({ token: 'bench' }) });
    if (!opened.ok) { console.log(JSON.stringify({ problem: opened.reason })); process.exit(1); }
    const run = opened.value;
    const stub = async () => ({ removed: [], left: [{ kind: 'scope', name: 'testsToDelete-stub-scope', reason: 'stub: it will not end' }] });
    const report = await cleanup(run, { scopes: stub });
    console.log(JSON.stringify({ dir: run.dir, clean: report.clean, left: report.left.map(item => item.kind + ':' + item.name) }));
    process.kill(process.pid, 'SIGKILL'); // the run's process is gone: its claim now says so
  `;
  const ran = spawnSync(process.execPath, ['--input-type=module', '-e', script], { encoding: 'utf8', timeout: 60_000 });
  const info = JSON.parse(ran.stdout.trim().split('\n').pop());
  assert.equal(info.problem, undefined, info.problem);
  assert.equal(info.clean, false);
  assert.ok(info.left.some(entry => entry.startsWith('scope:')), 'the unit is named');
  assert.ok(info.left.some(entry => entry.startsWith('run:')), 'and the run folder is reported as kept');
  assert.ok(existsSync(info.dir), 'the run folder stays');
  assert.ok(existsSync(`${info.dir}.owner`), 'and its claim');
  const swept = await sweepStale({ owned, parent: options.parent, minAgeMs: 0 });
  assert.ok(swept.retired.length >= 1, JSON.stringify(swept));
  assert.equal(existsSync(info.dir), false, 'the next start\'s sweep retires the run folder');
  assert.equal(existsSync(`${info.dir}.owner`), false, 'and its claim');
});

// L6: once the process is stopping, nothing creates a folder or starts a build.
test('A_stop_before_the_run_or_the_build_begins_creates_no_folder_and_starts_no_build', async t => {
  const { spawnSync } = await import('node:child_process');
  const root = owned.ownedDir(t, 'l6');
  const parent = path.join(root, 'runs');
  mkdirSync(parent);
  mkdirSync(path.join(root, 'tmp'));
  const options = layerOptions(t);
  const { owned: _owned, ...plain } = options;
  const script = `
    import { ownedFor } from ${JSON.stringify(new URL('../../../.agents/conventions/tools/lib/owned.mjs', import.meta.url).href)};
    import { markStopped } from ${JSON.stringify(new URL('../lib/exec-layer/stopping.mjs', import.meta.url).href)};
    import { openRun } from ${JSON.stringify(new URL('../lib/exec-layer/run.mjs', import.meta.url).href)};
    import { buildCsharp } from ${JSON.stringify(new URL('../lib/exec-probe/programs.mjs', import.meta.url).href)};
    const owned = ownedFor({ token: 'bench' });
    const scope = owned.cleanupScope();
    markStopped();
    const opened = await openRun({ ...${JSON.stringify(plain)}, parent: ${JSON.stringify(parent)}, owned });
    const built = await buildCsharp({ owned, scope, dirs: ${JSON.stringify(plain.dirs)} });
    console.log(JSON.stringify({ opened: opened.ok, built: built.ok, why: built.reason ?? '' }));
  `;
  const ran = spawnSync(process.execPath, ['--input-type=module', '-e', script], { encoding: 'utf8', timeout: 30_000, env: { ...process.env, TMPDIR: path.join(root, 'tmp') } });
  const result = JSON.parse(ran.stdout.trim().split('\n').pop());
  assert.deepEqual([result.opened, result.built], [false, false], ran.stderr);
  assert.match(result.why, /stopping/);
  assert.deepEqual(readdirSync(parent), [], 'no run folder');
  assert.deepEqual(readdirSync(path.join(root, 'tmp')), [], 'no probebuild folder, no claim');
});

// S2 (fix batch 6): a run stays in the registry until its cleanup has FINISHED CLEAN, so a stop whose cleanup hangs can still name it.
test('The_stop_message_after_a_hung_cleanup_names_the_run_its_scope_units_and_its_folder', async t => {
  if (!needs(t)) return;
  const { spawnSync } = await import('node:child_process');
  const options = layerOptions(t);
  const { owned: _owned, ...plain } = options;
  const script = `
    import { ownedFor } from ${JSON.stringify(new URL('../../../.agents/conventions/tools/lib/owned.mjs', import.meta.url).href)};
    import { cleanup, cleanupLiveRuns, describeLiveRuns } from ${JSON.stringify(new URL('../lib/exec-layer/cleanup.mjs', import.meta.url).href)};
    import { openRun } from ${JSON.stringify(new URL('../lib/exec-layer/run.mjs', import.meta.url).href)};
    import { stopCleanly } from ${JSON.stringify(new URL('../lib/exec-probe/stop.mjs', import.meta.url).href)};
    const opened = await openRun({ ...${JSON.stringify(plain)}, owned: ownedFor({ token: 'bench' }) });
    if (!opened.ok) { console.log(JSON.stringify({ problem: opened.reason })); process.exit(1); }
    const run = opened.value;
    let release;
    let calls = 0;
    const hungOnce = () => (calls++ === 0 ? new Promise(resolve => { release = resolve; }) : Promise.resolve({ removed: [], left: [] }));
    const said = [];
    const exits = [];
    await stopCleanly({ code: 143, bound: 400, say: text => said.push(text), exit: code => exits.push(code), release() {}, describeLive: describeLiveRuns, cleanLive: () => cleanupLiveRuns({ scopes: hungOnce }), closeScope: async () => {} });
    release({ removed: [], left: [] }); // the hung step is let go, and the cleanup that was in flight finishes: the run is really cleaned, so the test leaves nothing
    const after = await cleanup(run);
    console.log(JSON.stringify({ said, exits, id: run.id, unitPrefix: run.unitPrefix, dir: run.dir, clean: after.clean }));
    process.exit(0);
  `;
  const ran = spawnSync(process.execPath, ['--input-type=module', '-e', script], { encoding: 'utf8', timeout: 60_000 });
  const info = JSON.parse(ran.stdout.trim().split('\n').pop());
  assert.equal(info.problem, undefined, info.problem);
  assert.deepEqual(info.exits, [75]);
  const text = info.said.join('\n');
  assert.match(text, /stopping — cleaning up 1 open run\(s\)/, 'the first line counts the run');
  const incomplete = text.split('cleanup incomplete')[1] ?? '';
  assert.ok(incomplete.includes(`run ${info.id}`), 'the 75 message names the run\'s id');
  assert.ok(incomplete.includes(`${info.unitPrefix}-*`), 'its scope units');
  assert.ok(incomplete.includes(info.dir), 'and its folder');
  assert.equal(info.clean, true, 'the real cleanup afterwards left nothing');
});

// LOW 7: "kept" is reported only when the folder is still there.
test('The_run_folder_is_reported_as_kept_only_when_it_still_exists', async t => {
  if (!needs(t)) return;
  const { rmSync } = await import('node:fs');
  const opened = await openRun(layerOptions(t));
  assert.ok(opened.ok, opened.reason);
  const run = opened.value;
  t.after(() => cleanup(run));
  const stub = async () => ({ removed: [], left: [{ kind: 'scope', name: 'stub-scope', reason: 'stub' }] });
  const kept = await cleanup(run, { scopes: stub });
  assert.ok(kept.left.some(item => item.kind === 'run' && existsSync(run.dir)), 'folder there: reported as kept');
  rmSync(run.dir, { recursive: true, force: true });
  rmSync(`${run.dir}.owner`, { force: true });
  const gone = await cleanup(run, { scopes: stub });
  assert.deepEqual(gone.left.map(item => item.kind), ['scope', 'scope'], 'folder gone: nothing is "kept"; only the unit is named (once per scope step)');
});

// S4 (Opus LOW 4): EVERY owned folder of the probe is made through the stop guard, not only the build's and the run's.
test('A_stop_before_any_probe_step_makes_no_folder_for_the_run_mode_the_probe_run_or_the_empty_tree_and_no_raw_ownedDir_is_left', async t => {
  const { spawnSync } = await import('node:child_process');
  const { readFileSync } = await import('node:fs');
  const root = owned.ownedDir(t, 'low4');
  mkdirSync(path.join(root, 'tmp'));
  const mod = file => JSON.stringify(new URL(file, import.meta.url).href);
  const script = `
    import { ownedFor } from ${mod('../../../.agents/conventions/tools/lib/owned.mjs')};
    import { markStopped } from ${mod('../lib/exec-layer/stopping.mjs')};
    import { runMode } from ${mod('../lib/exec-probe/observe.mjs')};
    import { openProbeRun } from ${mod('../lib/exec-probe/lines.mjs')};
    import { emptyProgramTree } from ${mod('../lib/exec-probe/empty-tree.mjs')};
    const owned = ownedFor({ token: 'bench' });
    const scope = owned.cleanupScope();
    markStopped();
    const results = {};
    const attempt = async (name, fn) => { try { await fn(); results[name] = 'ran'; } catch (error) { results[name] = /stopping/.test(error.message) ? 'refused' : error.message; } };
    await attempt('runMode', () => runMode({ owned, scope, dirs: {}, csharpBuild: '/none', stack: 'node', mode: 'churn', memoryMaxBytes: 268435456 }));
    await attempt('openProbeRun', () => openProbeRun({ owned, scope, dirs: {}, sentinelHome: ${JSON.stringify(path.join(root, 'tmp'))}, csharpBuild: '/none' }));
    await attempt('emptyProgramTree', () => emptyProgramTree(owned, scope));
    console.log(JSON.stringify(results));
  `;
  const ran = spawnSync(process.execPath, ['--input-type=module', '-e', script], { encoding: 'utf8', timeout: 30_000, env: { ...process.env, TMPDIR: path.join(root, 'tmp') } });
  assert.deepEqual(JSON.parse(ran.stdout.trim().split('\n').pop()), { runMode: 'refused', openProbeRun: 'refused', emptyProgramTree: 'refused' }, ran.stderr);
  assert.deepEqual(readdirSync(path.join(root, 'tmp')), [], 'no folder, no claim');
  const lib = fileURLToPath(new URL('../lib/exec-probe', import.meta.url));
  const files = [...readdirSync(lib).map(name => path.join(lib, name)), fileURLToPath(new URL('../exec-probe.mjs', import.meta.url))].filter(file => file.endsWith('.mjs'));
  const raw = files.filter(file => /\bowned\.ownedDir\(/.test(readFileSync(file, 'utf8'))).map(file => path.basename(file));
  assert.deepEqual(raw, [], 'a raw owned.ownedDir( in the probe is a folder made without the stop guard');
});

// K4 (Opus L4): a cleanup of a run already in flight is not started twice.
test('Two_concurrent_cleanups_of_one_run_run_the_steps_once_and_share_the_report', async t => {
  if (!needs(t)) return;
  const opened = await openRun(layerOptions(t));
  assert.ok(opened.ok, opened.reason);
  const run = opened.value;
  t.after(() => cleanup(run));
  let calls = 0;
  const slow = async () => { calls += 1; await new Promise(resolve => setTimeout(resolve, 150)); return { removed: [], left: [] }; };
  const [one, two] = await Promise.all([cleanup(run, { scopes: slow }), cleanup(run, { scopes: slow })]);
  assert.equal(calls, 2, 'the scope step runs twice per cleanup (the confirmation pass), and the cleanup ran ONCE: four calls would be two cleanups');
  assert.deepEqual(one, two, 'both callers get the one report');
  assert.equal(one.clean, true);
});
