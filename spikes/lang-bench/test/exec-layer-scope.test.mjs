// E1.S8b: the delegated scope (parent D43, the plan's "The shape, delegated scope"). `systemd-run --user --scope -p Delegate=yes -p OOMPolicy=continue -p MemorySwapMax=0 scope-entry.sh …`:
// the scope's first process makes the leaves `holder/` and `prog/`, parks a bounded sleep (the scope holder) in `holder/`, moves itself into `prog/` and sets `memory.max` there, then
// execs the launch chain. `prog/` outlasts the program while the scope holder lives, so `memory.peak`, `memory.events` and `cgroup.procs` are read after a 10 ms exit.
// The wrong mechanisms (a plain scope, a driver inside the scope, no `OOMPolicy=continue`) are built here from the right prefix with `tweak`, so each defect has a shape.
import assert from 'node:assert/strict';
import { existsSync, readdirSync, readlinkSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { commOf, chain, MIB, needs, pidsIn, programPid, sh, start, text, waitFor, withoutView } from './support/exec-chain.mjs';
import { readProg, unitState, userEnv } from '../lib/exec-layer/scope.mjs';

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
  // as the runner starts a launcher: pipes for its stdin and stdout, and the status pipe at descriptor 7
  start(t, c.launch(), { stdio: ['pipe', 'pipe', 'ignore', 'ignore', 'ignore', 'ignore', 'ignore', 'pipe'] });
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
  const right = await chain(t, { files: SLEEPER });
  start(t, right.launch());
  assert.ok(await waitFor(() => programPid(right.prog, /^sleep$/)));
  const driver = start(t, [...right.driver, 'python3', '-I', '-c', `${allocator(80)}print("done", flush=True)\nimport time\ntime.sleep(30)`], { stdio: ['ignore', 'pipe', 'ignore'] });
  let said = '';
  driver.stdout.on('data', chunk => { said += chunk; });
  assert.ok(await waitFor(() => said.includes('done') || null), 'the driver holds its 80 MiB');
  assert.ok(num(path.join(right.prog, 'memory.peak')) < 40 * MIB, 'a driver outside the scope is not charged to the program');
  assert.ok(!text(`/proc/${driver.pid}/cgroup`).includes(right.scope), 'the driver\'s cgroup is not the program\'s scope');
  const wrong = await chain(t, { files: { 'alloc.py': `${allocator(80)}import time\ntime.sleep(30)\n`, 'run.sh': '#!/bin/bash\nexec python3 alloc.py\n' }, caseId: 'c2' });
  start(t, wrong.launch());
  assert.ok(await waitFor(() => (readProg(wrong.prog).peakBytes >= 80 * MIB) || null), 'a driver launched INSIDE the scope is charged to the program: prog/memory.peak holds its 80 MiB');
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
