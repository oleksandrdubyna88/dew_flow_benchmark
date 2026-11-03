// E1.S8b, the stop path of the layer's cleanup (fix batches 4 to 8; split from exec-layer-cleanup.test.mjs to stay under 400 lines — the tests moved here unchanged): an `openRun` that fails leaves nothing, a late scope
// never brings the run folder back, a stopping process opens nothing, a cleanup that leaves something keeps the run marker, the stop message of a hung cleanup names the run, and a cleanup in flight is shared.
import assert from 'node:assert/strict';
import { existsSync, mkdirSync, readdirSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { cleanup, sweepStale } from '../lib/exec-layer/cleanup.mjs';
import { openRun } from '../lib/exec-layer/run.mjs';
import { scanProcesses } from '../lib/exec-layer/proc.mjs';
import { needs } from './support/exec-layer-host.mjs';
import { layerOptions, retireCrashedRuns } from './support/exec-tree.mjs';
import { unitsStartingWith } from './support/probe-scope.mjs';
import { owned } from './support/owned.mjs';

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
  const options = layerOptions(t, { retireCrashed: true }); // the retire hook is registered BEFORE the folder (M1)
  const { owned: _owned, retireCrashed: _retire, ...plain } = options;
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
  const options = layerOptions(t, { retireCrashed: true }); // L5, M1: a crash or a timeout of the child never leaves its run: the hook is registered BEFORE the folder
  const { owned: _owned, retireCrashed: _retire, ...plain } = options;
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

// K5 (Opus L5): a test whose child can crash or time out registers a PARENT hook that retires the child's runs, so the test never leaves a holder or a folder.
test('The_parent_hook_retires_the_run_of_a_child_that_crashed_with_its_holder_still_running', async t => {
  if (!needs(t)) return;
  const { spawnSync } = await import('node:child_process');
  const options = layerOptions(t, { retireCrashed: true });
  const { owned: _owned, retireCrashed: _retire, ...plain } = options;
  const script = `
    import { ownedFor } from ${JSON.stringify(new URL('../../../.agents/conventions/tools/lib/owned.mjs', import.meta.url).href)};
    import { openRun } from ${JSON.stringify(new URL('../lib/exec-layer/run.mjs', import.meta.url).href)};
    const opened = await openRun({ ...${JSON.stringify(plain)}, owned: ownedFor({ token: 'bench' }) });
    console.log(JSON.stringify({ ok: opened.ok, holder: opened.value?.holderName, dir: opened.value?.dir }));
    process.kill(process.pid, 'SIGKILL'); // the child crashes with its run open
  `;
  const ran = spawnSync(process.execPath, ['--input-type=module', '-e', script], { encoding: 'utf8', timeout: 60_000 });
  const info = JSON.parse(ran.stdout.trim().split('\n').pop());
  assert.equal(info.ok, true);
  const holders = () => scanProcesses(argv => argv[0] === info.holder);
  assert.equal(holders().length, 1, 'the crashed child left its holder running');
  await retireCrashedRuns(options); // what the hook does
  assert.deepEqual(holders(), [], 'the hook retired the holder by the run\'s id');
  assert.equal(existsSync(info.dir), false, 'and the run folder');
});

// M1 (fix batch 8): `node:test` runs `after` hooks FIRST-IN, FIRST-OUT, so a retire hook registered after `layerOptions` (which registers the removal of the runs parent) runs on an empty folder. `retireCrashed: true`
// registers it BEFORE the folder. ONLY the hook (not the body) retires what the crashed child left: its holder and its scope.
test('Only_the_hook_retires_the_holder_and_the_scope_of_a_child_that_crashed_and_it_runs_before_its_folder_is_removed', async t => {
  if (!needs(t)) return;
  const { spawnSync } = await import('node:child_process');
  const options = layerOptions(t, { retireCrashed: true });
  const { owned: _owned, retireCrashed: _retire, ...plain } = options;
  const seen = { holder: null, unitPrefix: null };
  t.after(() => { // registered AFTER layerOptions: it runs after the retire hook and after the folders are gone
    assert.deepEqual(scanProcesses(argv => argv[0] === seen.holder), [], 'the holder of the crashed child is gone after the hooks');
    assert.deepEqual(unitsStartingWith(seen.unitPrefix), [], 'and so is its scope');
  });
  const script = `
    import { spawn, spawnSync } from 'node:child_process';
    import { ownedFor } from ${JSON.stringify(new URL('../../../.agents/conventions/tools/lib/owned.mjs', import.meta.url).href)};
    import { openRun } from ${JSON.stringify(new URL('../lib/exec-layer/run.mjs', import.meta.url).href)};
    import { userEnv } from ${JSON.stringify(new URL('../lib/exec-layer/scope.mjs', import.meta.url).href)};
    const opened = await openRun({ ...${JSON.stringify(plain)}, owned: ownedFor({ token: 'bench' }) });
    const run = opened.value;
    spawn('systemd-run', ['--user', '--scope', '--quiet', '--unit=' + run.unitPrefix + '-x1', 'sleep', '120'], { env: userEnv(), stdio: 'ignore', detached: true }).unref();
    // D5 (Opus LOW 6): wait until the scope is ACTIVE (a time limit, not a fixed pause) before the child reports and crashes
    const unit = run.unitPrefix + '-x1.scope';
    const until = Date.now() + 20000;
    let active = false;
    while (!active && Date.now() < until) { active = spawnSync('systemctl', ['--user', 'is-active', '--quiet', unit]).status === 0; if (!active) await new Promise(resolve => setTimeout(resolve, 50)); }
    console.log(JSON.stringify({ holder: run.holderName, unitPrefix: run.unitPrefix, active }));
    process.kill(process.pid, 'SIGKILL'); // the child crashes with its run open
  `;
  const ran = spawnSync(process.execPath, ['--input-type=module', '-e', script], { encoding: 'utf8', timeout: 60_000 });
  Object.assign(seen, JSON.parse(ran.stdout.trim().split('\n').pop()));
  assert.equal(seen.active, true, 'the child saw its scope active before it crashed');
  assert.equal(scanProcesses(argv => argv[0] === seen.holder).length, 1, 'the crashed child left its holder running');
  assert.equal(unitsStartingWith(seen.unitPrefix).length, 1, 'and its scope');
});
