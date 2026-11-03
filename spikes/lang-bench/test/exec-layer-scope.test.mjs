// E1.S8b: the delegated scope (parent D43, the plan's "The shape, delegated scope"). `systemd-run --user --scope -p Delegate=yes -p OOMPolicy=continue -p MemorySwapMax=0 scope-entry.sh …`:
// the scope's first process makes the leaves `holder/` and `prog/`, parks a bounded sleep (the scope holder) in `holder/`, moves itself into `prog/` and sets `memory.max` there, then
// execs the launch chain. `prog/` outlasts the program while the scope holder lives, so `memory.peak`, `memory.events` and `cgroup.procs` are read after a 10 ms exit.
// The wrong mechanisms (a plain scope, a driver inside the scope, no `OOMPolicy=continue`) are built here from the right prefix with `tweak`, so each defect has a shape.
import assert from 'node:assert/strict';
import { existsSync, mkdirSync, readdirSync, readlinkSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { commOf, chain, MIB, needs, pidsIn, programPid, sh, start, text, waitFor, withoutView } from './support/exec-chain.mjs';
import { cleanup } from '../lib/exec-layer/cleanup.mjs';
import { scanProcesses } from '../lib/exec-layer/proc.mjs';
import { openRun, runnerArgv } from '../lib/exec-layer/run.mjs';
import { killCgroup, progCgroup, progTemplate, readProg, scopePrefix, unitState, userEnv } from '../lib/exec-layer/scope.mjs';
import { layerOptions } from './support/exec-tree.mjs';

const num = file => Number(text(file).trim());
const SLEEPER = { 'run.sh': '#!/bin/bash\nexec sleep 300\n' };
const QUICK = { 'run.sh': '#!/bin/bash\nexec true\n' };
const TOUCH = 'b = bytearray(%MIB% * 1024 * 1024)\nfor i in range(0, len(b), 4096):\n    b[i] = 1\n';
const allocator = mib => TOUCH.replace('%MIB%', String(mib));

/** The prefix of the OLD, plain scope: no `Delegate=yes`, no entry, no scope holder, no `OOMPolicy=continue`; the launch chain is the scope's own process. */
const plainScope = limitBytes => prefix => [prefix[0], '--user', '--scope', prefix.find(arg => arg.startsWith('--unit=')), '-p', `MemoryMax=${limitBytes}`, '-p', 'MemorySwapMax=0'];
/** The right prefix with one property removed together with its `-p`. */
const without = property => prefix => {
  const at = prefix.indexOf(property);
  return at < 0 ? prefix : [...prefix.slice(0, at - 1), ...prefix.slice(at + 1)];
};

test('A_delegated_scope_holds_only_the_launch_chain_in_prog', async t => {
  if (!needs(t)) return;
  const c = await chain(t, { files: SLEEPER });
  start(t, c.launch());
  assert.ok(await waitFor(() => programPid(c.prog, /^sleep$/)), 'the program (exec sleep) runs in prog/');
  const chainComms = new Set(['bwrap', 'sleep', 'nsenter']);
  for (const pid of pidsIn(c.prog)) assert.ok(chainComms.has(commOf(pid)), `pid ${pid} (${commOf(pid)}) is in prog/ and is not the launch chain`);
  assert.deepEqual(pidsIn(c.scope), [], 'the scope\'s own cgroup holds no process (cgroup2: a cgroup with controllers on its children holds none)');
  const holderPid = Number(text(path.join(c.out, 'holder.pid')).trim());
  assert.deepEqual(pidsIn(c.holderDir), [holderPid], 'holder/ holds only the scope holder');
  assert.equal(commOf(holderPid), 'sleep');
  assert.equal(text(path.join(c.out, 'scope-cgroup')).trim(), c.scope, 'the scope\'s cgroup path is written to <out-dir>');
  assert.equal(num(path.join(c.prog, 'memory.max')), 256 * MIB);
  assert.equal(num(path.join(c.prog, 'memory.swap.max')), 0);
  assert.match(text(path.join(c.scope, 'cgroup.subtree_control')), /memory/);
});

test('The_scope_holder_keeps_no_descriptor_of_the_launcher', async t => {
  if (!needs(t)) return;
  const c = await chain(t, { files: QUICK });
  // as the runner starts a launcher: pipes for its stdin and stdout, the status pipe at descriptor 7, and pipes at 12 and 20 (the holder closes EVERY inherited descriptor above 2, not 3-9 by number)
  start(t, c.launch(), { stdio: ['pipe', 'pipe', 'ignore', 'ignore', 'ignore', 'ignore', 'ignore', 'pipe', 'ignore', 'ignore', 'ignore', 'ignore', 'pipe', 'ignore', 'ignore', 'ignore', 'ignore', 'ignore', 'ignore', 'ignore', 'pipe'] });
  const holderPid = await waitFor(() => { try { return Number(text(path.join(c.out, 'holder.pid')).trim()); } catch { return null; } });
  const target = fd => { try { return [fd, readlinkSync(`/proc/${holderPid}/fd/${fd}`)]; } catch { return null; } }; // a descriptor closed since the listing is no descriptor
  // what keeps a runner's pipe open is a pipe or a socket of the launcher (the stdio pipes, the status pipe), or stdio that is not /dev/null; sleep's own start-up (libselinux reads
  // /proc/self/mounts through fd 3 for a moment) is neither
  const foreign = () => readdirSync(`/proc/${holderPid}/fd`).map(target).filter(entry => entry !== null && (/^(pipe|socket):/.test(entry[1]) || (Number(entry[0]) <= 2 && entry[1] !== '/dev/null')));
  await waitFor(() => (commOf(holderPid) === 'sleep' && foreign().length === 0) || null, 3000);
  const fds = foreign();
  assert.deepEqual(fds, [], 'a descriptor of the launcher in the scope holder would keep the runner\'s pipes open until the holder ends');
});

test('Memory_peak_is_read_after_a_program_that_exits_in_10_ms', async t => {
  if (!needs(t)) return;
  const c = await chain(t, { files: QUICK });
  assert.equal((await start(t, c.launch()).exited).code, 0);
  const read = readProg(c.prog);
  assert.ok(read.peakBytes > 0, `memory.peak is read after the exit: ${JSON.stringify(read)}`);
  assert.equal(read.events.oom_kill, 0);
  assert.ok(await waitFor(() => readProg(c.prog).procs?.length === 0 || null, 5000), 'nothing is left in prog/ (the box\'s own processes may still be going out when the launcher has exited)');
  assert.equal(pidsIn(c.holderDir).length, 1, 'the scope holder is what keeps the scope');
  const plain = await chain(t, { files: QUICK, caseId: 'c2', tweak: plainScope(256 * MIB), boxTweak: withoutView });
  assert.equal((await start(t, plain.launch()).exited).code, 0);
  assert.ok(await waitFor(() => !existsSync(plain.scope) || null, 3000), 'a program in the scope\'s own cgroup and no scope holder: the cgroup is gone at the read');
  assert.equal(readProg(plain.scope).peakBytes, null, 'memory.peak cannot be read: not certified');
});

test('A_scope_around_the_driver_would_charge_its_memory_to_the_program_and_that_is_the_point', async t => {
  if (!needs(t)) return;
  const opened = await openRun(layerOptions(t, { runSh: '#!/bin/bash\nexec sleep 4\n' }));
  assert.ok(opened.ok, opened.reason);
  const run = opened.value;
  t.after(() => cleanup(run));
  const json = { cgroupOf: progTemplate(run.unitPrefix), jsonOut: path.join(run.dir, 'right.json') };
  const right = start(t, runnerArgv(run, json), { stdio: 'ignore' });
  const rightDriver = await waitFor(() => scanProcesses(args => args.some(arg => arg.endsWith('run_samples.py')) && args.includes(json.jsonOut)).find(id => commOf(id.pid) === 'python3') ?? null, 15_000);
  assert.ok(rightDriver, 'the REAL driver (the runner under nsenter) runs');
  assert.ok(!text(`/proc/${rightDriver.pid}/cgroup`).includes(run.unitPrefix), 'outside every scope of the run: the program\'s limit is not charged with the driver');
  await right.exited;
  const wrongJson = { cgroupOf: json.cgroupOf, jsonOut: path.join(run.dir, 'wrong.json') };
  const wrongArgv = [...scopePrefix({ unit: `${run.unitPrefix}-drv`, outDir: path.join(run.dir, 'out', 'drv'), memoryMaxBytes: 256 * MIB, holdSeconds: 60 }), ...runnerArgv(run, wrongJson)];
  start(t, wrongArgv, { stdio: 'ignore' });
  const wrongDriver = await waitFor(() => scanProcesses(args => args.some(arg => arg.endsWith('run_samples.py')) && args.includes(wrongJson.jsonOut)).find(id => commOf(id.pid) === 'python3') ?? null, 15_000);
  assert.ok(wrongDriver, 'the real driver, launched inside a scope prefix, runs');
  const cgroup = text(`/proc/${wrongDriver.pid}/cgroup`).trim();
  assert.ok(cgroup.endsWith(`${run.unitPrefix}-drv.scope/prog`), `the refuted shape puts the real driver in a scope's prog/, under that scope's memory.max: ${cgroup}`);
  assert.equal(num(path.join(progCgroup(`${run.unitPrefix}-drv`), 'memory.max')), 256 * MIB, 'whatever the driver allocates is charged against that limit');
});

test('An_oom_killed_child_is_memory_limit_whatever_the_exit_status', async t => {
  if (!needs(t)) return;
  const files = { 'child.py': allocator(300), 'run.sh': '#!/bin/bash\npython3 -I child.py &\nwait\nexit 0\n' };
  const c = await chain(t, { files, memoryMax: 64 * MIB });
  const status = await start(t, c.launch()).exited;
  assert.equal(status.code, 0, 'the parent exits 0 whatever happened to its child');
  const read = readProg(c.prog);
  assert.ok(read.events.oom_kill >= 1, `oom_kill >= 1 is read back from prog/memory.events: ${JSON.stringify(read.events)}`);
  assert.ok(read.peakBytes > 0);
  const wrong = await chain(t, { files, memoryMax: 64 * MIB, caseId: 'c2', tweak: plainScope(64 * MIB), boxTweak: withoutView });
  await start(t, wrong.launch()).exited;
  assert.ok(await waitFor(() => !existsSync(wrong.scope) || null, 5000), 'a plain scope stops whole on the OOM kill (this host\'s DefaultOOMPolicy is stop): its cgroup is gone and memory.events cannot be read');
  assert.equal(await unitState(wrong.unit), 'failed', 'systemd says oom-kill: the case would be a launch defect, not memory limit');
});

test('OOMPolicy_continue_is_in_the_prefix_although_a_delegated_scope_defaults_to_it_on_this_host', async t => {
  if (!needs(t)) return;
  const c = await chain(t, { files: SLEEPER, tweak: without('OOMPolicy=continue') });
  start(t, c.launch());
  assert.ok(await waitFor(() => programPid(c.prog, /^sleep$/)));
  assert.equal((await sh('systemctl', ['--user', 'show', `${c.unit}.scope`, '-p', 'OOMPolicy'], { env: userEnv() })).stdout.trim(), 'OOMPolicy=continue', 'measured on systemd 259: Delegate=yes alone gives continue — the property in the prefix is a pin, not the cause');
  const plain = await chain(t, { files: SLEEPER, caseId: 'c2', tweak: plainScope(256 * MIB), boxTweak: withoutView });
  start(t, plain.launch());
  assert.ok(await waitFor(async () => (await unitState(plain.unit)) === 'active' || null));
  assert.equal((await sh('systemctl', ['--user', 'show', `${plain.unit}.scope`, '-p', 'OOMPolicy'], { env: userEnv() })).stdout.trim(), 'OOMPolicy=stop', 'a plain scope has the host default: stop');
});

test('A_fork_bomb_in_the_box_stops_at_the_pids_bound_and_nothing_survives', async t => {
  if (!needs(t)) return;
  const c = await chain(t, { files: { 'run.sh': '#!/bin/bash\nbomb() { bomb | bomb & }\nbomb\nsleep 30\n' } });
  start(t, c.launch());
  assert.ok(await waitFor(() => (existsSync(path.join(c.prog, 'pids.events')) && /max [1-9]/.test(text(path.join(c.prog, 'pids.events')))) || null, 30_000), 'forks were refused at the bound');
  assert.equal(num(path.join(c.prog, 'pids.max')), 512, 'the bound is written in prog/');
  assert.ok(num(path.join(c.prog, 'pids.peak')) <= 512, 'the number of processes never passed it');
  killCgroup(c.prog);
  assert.ok(await waitFor(() => (readProg(c.prog).procs ?? []).length === 0 || null, 15_000), 'nothing survives the kill of prog/');
  assert.equal(num(path.join(c.prog, 'pids.current')), 0);
  assert.equal(pidsIn(c.holderDir).length, 1, 'the scope holder outside prog/ is untouched');
});

// B2 (fix batch 4): a scope still starting after its run was cleaned up must not bring the run folder back (an unclaimed folder no sweep removes).
test('The_scope_entry_fails_when_the_run_folder_is_gone_and_never_creates_it_again', async t => {
  if (!needs(t)) return;
  const { spawnSync } = await import('node:child_process');
  const { rmSync } = await import('node:fs');
  const { owned } = await import('./support/owned.mjs');
  const runDir = owned.ownedDir(t, 'sentry');
  const unit = `${path.basename(runDir)}-c1`;
  let count = 0;
  const enter = out => spawnSync(...[scopePrefix({ unit: `${unit}-${count += 1}`, outDir: out, memoryMaxBytes: 256 * MIB, holdSeconds: 2 })].map(prefix => [prefix[0], [...prefix.slice(1), 'true']]).flat(), { env: userEnv(), encoding: 'utf8', timeout: 20_000 });
  const first = enter(path.join(runDir, 'out', 'c1'));
  assert.equal(first.status, 0, first.stderr);
  assert.ok(existsSync(path.join(runDir, 'out', 'c1', 'scope-cgroup')), 'with the run folder there, the entry makes out/<case> as before');
  rmSync(runDir, { recursive: true, force: true });
  const late = enter(path.join(runDir, 'out', 'c2'));
  assert.notEqual(late.status, 0, 'the run folder is gone: the entry fails');
  assert.match(late.stderr, /run folder/, 'and says why');
  assert.equal(existsSync(runDir), false, 'and the run folder is NOT made again: no unclaimed folder is left for a sweep that cannot remove it');
});

// L7: two scopes of one run start together and both make `<run>/out` and their own case folder: neither fails on the other's mkdir.
test('Two_scope_entries_of_one_run_started_together_both_succeed', async t => {
  if (!needs(t)) return;
  const { spawn, spawnSync } = await import('node:child_process');
  const { owned } = await import('./support/owned.mjs');
  const runDir = owned.ownedDir(t, 'l7');
  let count = 0;
  const enter = out => new Promise(resolve => {
    const prefix = scopePrefix({ unit: `${path.basename(runDir)}-r${count += 1}`, outDir: out, memoryMaxBytes: 256 * MIB, holdSeconds: 2 });
    const child = spawn(prefix[0], [...prefix.slice(1), 'true'], { env: userEnv(), stdio: ['ignore', 'ignore', 'pipe'] });
    let stderr = '';
    child.stderr.on('data', chunk => { stderr += chunk; });
    child.on('close', code => resolve({ code, stderr }));
  });
  const failures = [];
  for (let round = 0; round < 25; round += 1) {
    const base = path.join(runDir, `round${round}`);
    mkdirSync(base);
    const results = await Promise.all(Array.from({ length: 4 }, (_, n) => enter(path.join(base, 'out', `c${n}`)))); // `out/` does not exist yet: all four race to make it
    failures.push(...results.filter(result => result.code !== 0).map(result => `round ${round}: exit ${result.code} ${result.stderr.trim().slice(0, 80)}`));
  }
  const unitsLeft = () => spawnSync('systemctl', ['--user', 'list-units', '--all', '--plain', '--no-legend', '--full'], { encoding: 'utf8' }).stdout.split('\n').map(line => line.trim().split(/\s+/)[0]).filter(name => name.startsWith(`${path.basename(runDir)}-r`));
  const settle = Date.now() + 30_000; // LOW 8: the last round's scopes (a 2 s holder each) are gone before the test ends
  while (unitsLeft().length > 0 && Date.now() < settle) await new Promise(resolve => setTimeout(resolve, 200));
  assert.deepEqual(unitsLeft(), [], 'no scope of this test is left when it ends');
  assert.deepEqual(failures, [], 'no entry fails because another made out/ first');
});
