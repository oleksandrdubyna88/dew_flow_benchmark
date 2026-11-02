// E1.S8b: the network namespace the driver and the program share — a holder's, loopback only (D43). One holder owns the user and network namespace; the driver joins it with
// `nsenter -t <holder> -U -n --preserve-credentials` and so does the box's bwrap, WITHOUT `--unshare-net`.
import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import { readlinkSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { boxArgv } from '../lib/exec-layer/box.mjs';
import { boxEnv } from '../lib/exec-layer/env.mjs';
import { nsenterArgv, startHolder, stopHolder } from '../lib/exec-layer/netns.mjs';
import { identityOf, sameProcess, statOf } from '../lib/exec-layer/proc.mjs';
import { isolation } from '../../minigit-bench/lib/bwrap.mjs';
import { owned } from './support/owned.mjs';
import { needs, sh, toolchains } from './support/exec-layer-host.mjs';
import { chain, commOf, programPid, start, text, waitFor } from './support/exec-chain.mjs';
import { PROGRAM_COMM, ping, readyPort, STACKS, stubView } from './support/exec-stubs.mjs';

const NS = '/proc/self/ns/net';

/** A started holder with an owned name (the name of a claimed owned folder, so what names it is claimed and removed by the test); stopped when the test ends. */
async function holderFor(t, boundSeconds = 120) {
  const name = path.basename(owned.ownedDir(t, 'lhold'));
  const made = await startHolder({ name, boundSeconds });
  assert.ok(made.ok, made.reason);
  t.after(() => stopHolder(made.value));
  return made.value;
}

const nsOf = pid => readlinkSync(`/proc/${pid}/ns/net`);

/**
 * Start a program in the box inside the holder's namespaces, as the layer does; it ends with the test. This test has no scope to kill, so it adds `--die-with-parent` to its OWN copy of
 * the argv (the layer's box never has it: the scope is the kill domain) and a killed bwrap then takes the box with it.
 */
function startInBox(t, holder, argv, program) {
  const withDie = [...argv.slice(0, argv.indexOf('--unshare-pid')), '--die-with-parent', ...argv.slice(argv.indexOf('--unshare-pid'))];
  const [exe, ...args] = [...nsenterArgv(holder), ...withDie, ...program];
  const child = spawn(exe, args, { stdio: 'ignore', detached: true, env: { PATH: '/usr/sbin:/usr/bin:/bin' } });
  t.after(() => { try { process.kill(-child.pid, 'SIGKILL'); } catch { /* already gone */ } });
  return child;
}

test('A_holder_has_a_network_namespace_of_its_own_with_lo_up_and_nothing_else', async t => {
  if (!needs(t)) return;
  const holder = await holderFor(t);
  assert.notEqual(nsOf(holder.pid), readlinkSync(NS), 'the holder\'s namespace is not the host\'s');
  const links = await sh(nsenterArgv(holder)[0], [...nsenterArgv(holder).slice(1), '/usr/sbin/ip', '-br', 'link']);
  const lines = links.stdout.trim().split('\n');
  assert.equal(lines.length, 1, `lo is the only interface: ${links.stdout}`);
  assert.match(lines[0], /^lo\s+UNKNOWN|^lo\s+UP/);
});

test('A_holder_ends_by_itself_at_its_bound_and_is_stopped_at_once_by_stopHolder', async t => {
  if (!needs(t)) return;
  const short = await holderFor(t, 1);
  for (let i = 0; i < 60 && sameProcess(short); i += 1) await new Promise(resolve => setTimeout(resolve, 100));
  assert.equal(sameProcess(short), false, 'a holder whose layer crashed still ends: its sleep is bounded');
  const long = await holderFor(t, 120);
  assert.equal(sameProcess(long), true);
  assert.equal(await stopHolder(long), 'terminated');
  assert.equal(sameProcess(long), false);
});

test('stopHolder_never_signals_a_process_that_is_not_the_holder', async t => {
  if (!needs(t)) return;
  const bystander = spawn('sleep', ['60'], { stdio: 'ignore' });
  t.after(() => bystander.kill('SIGKILL'));
  const real = identityOf(bystander.pid);
  const wrongStart = { ...real, startTicks: real.startTicks + 1 };
  assert.equal(await stopHolder(wrongStart), 'gone', 'the pid is another process now: nothing is signalled');
  assert.equal(sameProcess(real), true, 'the bystander is alive');
  const wrongName = { ...real, argv0: 'testsToDelete-u0-bench-lhold-zzzzzz' };
  assert.equal(await stopHolder(wrongName), 'gone');
  assert.equal(sameProcess(real), true);
});

test('A_box_with_its_own_network_namespace_would_leave_the_driver_unable_to_connect_and_that_is_the_point', async t => {
  if (!needs(t)) return;
  const holder = await holderFor(t);
  const driver = nsenterArgv(holder);
  const wrongView = await stubView(t, 'python');
  const wrong = boxArgv({ view: wrongView, dirs: toolchains, env: boxEnv({ dirs: toolchains }), prog: null });
  wrong.splice(wrong.indexOf('--unshare-pid'), 0, ...isolation().filter(flag => flag === '--unshare-net'));
  startInBox(t, holder, wrong, ['bash', 'run.sh']);
  const wrongPort = await readyPort(wrongView);
  assert.ok(wrongPort, 'the program listens — in a namespace the driver is not in');
  assert.match(await ping(driver, wrongPort), /^ERR ConnectionRefusedError$/, 'the driver cannot connect: that is what --unshare-net does to the driver');
  const rightView = await stubView(t, 'python');
  startInBox(t, holder, boxArgv({ view: rightView, dirs: toolchains, env: boxEnv({ dirs: toolchains }), prog: null }), ['bash', 'run.sh']);
  const port = await readyPort(rightView);
  assert.ok(port, 'with the holder\'s namespace the program listens');
  assert.equal(await ping(driver, port), 'PING', 'the driver reaches the program\'s port on 127.0.0.1');
});

test('The_program_holds_no_capabilities_and_cannot_take_lo_down_and_that_is_the_point', async t => {
  if (!needs(t)) return;
  const view = await stubView(t, 'python');
  const probe = ['bash', '-c', 'grep CapEff /proc/self/status; /usr/sbin/ip link set lo down; echo "lo-down-exit=$?"'];
  const right = await holderFor(t);
  const dropped = await sh(nsenterArgv(right)[0], [...nsenterArgv(right).slice(1), ...boxArgv({ view, dirs: toolchains, env: boxEnv({ dirs: toolchains }), prog: null }), ...probe]);
  assert.match(dropped.stdout, /CapEff:\s+0000000000000000/);
  assert.match(dropped.stdout, /lo-down-exit=[1-9]/, 'the program cannot take the shared lo down');
  const wrongHolder = await holderFor(t);
  const kept = boxArgv({ view, dirs: toolchains, env: boxEnv({ dirs: toolchains }), prog: null });
  kept.splice(kept.indexOf('--cap-drop'), 2);
  const loud = await sh(nsenterArgv(wrongHolder)[0], [...nsenterArgv(wrongHolder).slice(1), ...kept, ...probe]);
  assert.doesNotMatch(loud.stdout, /CapEff:\s+0000000000000000/, 'without the drop the program holds every capability of the holder\'s user namespace');
  assert.match(loud.stdout, /lo-down-exit=0/, 'and it takes lo down for the driver as well');
});

// ---- T-L33 through the real chain (holder, delegated scope, box): the same test body for a Python, a Node and a C# stub, the same facts ----

for (const stack of STACKS) {
  test(`The_driver_and_a_${stack}_stub_share_one_loopback_only_namespace_and_the_driver_reaches_the_programs_port`, async t => {
    if (!needs(t)) return;
    const view = await stubView(t, stack);
    const c = await chain(t, { view });
    start(t, c.launch());
    const port = await readyPort(view);
    assert.ok(port, `the ${stack} stub listens`);
    const driver = start(t, [...c.driver, 'python3', '-I', '-c', 'import time; time.sleep(60)']);
    assert.ok(await waitFor(() => commOf(driver.pid) === 'python3'), 'the driver has joined the namespaces and runs python');
    const program = await waitFor(() => programPid(c.prog, PROGRAM_COMM[stack]));
    assert.ok(program, `the program is ${PROGRAM_COMM[stack]} in prog/`);
    assert.equal(nsOf(driver.pid), nsOf(program), 'their /proc/<pid>/ns/net are equal');
    assert.notEqual(nsOf(program), readlinkSync(NS), 'and differ from the host\'s');
    assert.ok(text(`/proc/${program}/cgroup`).trim().endsWith(`${c.unit}.scope/prog`), '/proc/<pid>/cgroup puts the program in its scope');
    assert.ok(!text(`/proc/${driver.pid}/cgroup`).includes(c.unit), 'and the driver not');
    const links = await sh(c.driver[0], [...c.driver.slice(1), '/usr/sbin/ip', '-br', 'link']);
    assert.deepEqual(links.stdout.trim().split('\n').map(line => line.split(/\s+/)[0]), ['lo'], 'lo is the only interface');
    assert.equal(await ping(c.driver, port), 'PING', 'the driver reaches the program\'s port on 127.0.0.1');
    assert.equal(commOf(statOf(program).ppid), 'bwrap', 'run.sh ended with exec: no bash above the program in the scope');
  });
}

test('A_run_sh_without_exec_leaves_a_bash_above_the_program_and_that_is_the_point', async t => {
  if (!needs(t)) return;
  const view = await stubView(t, 'python');
  writeFileSync(path.join(view, 'run.sh'), '#!/bin/bash\npython3 server.py\n');
  const c = await chain(t, { view });
  start(t, c.launch());
  assert.ok(await readyPort(view));
  const program = await waitFor(() => programPid(c.prog, PROGRAM_COMM.python));
  assert.equal(commOf(statOf(program).ppid), 'bash', 'without exec the program\'s parent is bash, and a signal to the launcher would never reach the program (T-L31)');
});
