// E1.S8b: the scope's kill domain and the pids. The scope — not the program's pid, not the launcher — is what a case's end acts on: `prog/cgroup.kill` ends the program and every child it
// forked while the driver, outside the scope, survives and has read `memory.peak` and `memory.events` live; and the pid a signal goes to is the HOST pid listed in `prog/cgroup.procs`,
// never the pid seen inside the box (1 or 2) and never the `child-pid` of bwrap's status line (the box's init).
import assert from 'node:assert/strict';
import { existsSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { commOf, chain, needs, pidsIn, programPid, sh, start, text, waitFor } from './support/exec-chain.mjs';
import { sameProcess, identityOf, statOf } from '../lib/exec-layer/proc.mjs';
import { killCgroup, listScopes, readProg, stopUnit } from '../lib/exec-layer/scope.mjs';

const FORKER = { 'run.sh': '#!/bin/bash\nsleep 300 &\nexec sleep 301\n' };
const hostPidOf = (prog, arg) => (pidsIn(prog) ?? []).find(pid => { try { return text(`/proc/${pid}/cmdline`).split('\0')[0] === 'sleep' && text(`/proc/${pid}/cmdline`).split('\0')[1] === arg; } catch { return false; } }) ?? null;

test('Stopping_the_scope_ends_a_forked_child_and_the_driver_survives', async t => {
  if (!needs(t)) return;
  const wrong = await chain(t, { files: FORKER, caseId: 'c1' });
  start(t, wrong.launch());
  assert.ok(await waitFor(() => hostPidOf(wrong.prog, '300') && hostPidOf(wrong.prog, '301')), 'the program and its forked child both run in prog/');
  const program = identityOf(hostPidOf(wrong.prog, '301'));
  const child = identityOf(hostPidOf(wrong.prog, '300'));
  process.kill(program.pid, 'SIGKILL');
  await waitFor(() => !sameProcess(program) || null);
  assert.equal(sameProcess(child), true, 'the refuted shape — a kill of the program\'s pid only — leaves the child (its marker survives)');
  killCgroup(wrong.prog);

  const c = await chain(t, { files: FORKER, caseId: 'c2' });
  start(t, c.launch());
  assert.ok(await waitFor(() => hostPidOf(c.prog, '300') && hostPidOf(c.prog, '301')));
  const driver = start(t, [...c.driver, 'python3', '-I', '-c', 'import time; time.sleep(120)']);
  assert.ok(await waitFor(() => commOf(driver.pid) === 'python3'));
  const live = readProg(c.prog);
  assert.ok(live.peakBytes > 0 && live.events !== null && live.procs.length >= 3, 'the driver reads memory.peak, memory.events and cgroup.procs live, before any kill');
  const both = [identityOf(hostPidOf(c.prog, '300')), identityOf(hostPidOf(c.prog, '301'))];
  killCgroup(c.prog);
  killCgroup(c.holderDir);
  assert.ok(await waitFor(() => both.every(identity => !sameProcess(identity)) || null), 'both the program and its child end');
  assert.ok(await waitFor(async () => (existsSync(c.scope) ? null : true), 5000), 'the scope ends with the scope holder');
  assert.deepEqual(await listScopes(c.unit), [], 'no scope of the case is left');
  assert.equal(pidsIn(c.prog), null, 'no process of the case is left: prog/ is gone');
  assert.equal(commOf(driver.pid), 'python3', 'the driver, outside the scope, survived');
  await stopUnit(wrong.unit);
});

test('A_pid_seen_inside_the_box_would_signal_the_wrong_process_and_that_is_the_point', async t => {
  if (!needs(t)) return;
  const run = '#!/bin/bash\ntrap \'echo TERM > got-term; exit 0\' TERM\necho $$ > inbox.pid\nwhile :; do sleep 0.05; done\n';
  const c = await chain(t, { files: { 'run.sh': run }, extraBox: { statusFd: 7 } });
  const launcher = start(t, c.launch(), { stdio: ['ignore', 'ignore', 'ignore', 'ignore', 'ignore', 'ignore', 'ignore', 'pipe'] });
  const status = await new Promise(resolve => { launcher.stdio[7].once('data', chunk => resolve(JSON.parse(chunk.toString().split('\n')[0]))); });
  const childPid = status['child-pid'];
  assert.ok(await waitFor(() => existsSync(path.join(c.view, 'inbox.pid')) || null), 'the program wrote the pid it sees');
  const inBox = Number(readFileSync(path.join(c.view, 'inbox.pid'), 'utf8').trim());
  assert.ok(inBox === 1 || inBox === 2, `inside the box the program is pid ${inBox}`);
  assert.ok(!pidsIn(c.prog).includes(inBox), `on the host pid ${inBox} is ${commOf(inBox)}, not a process of the scope: signalling it would be signalling the wrong process`);
  assert.equal(commOf(childPid), 'bwrap', 'the child-pid of the status line is the box\'s init, not the program');
  process.kill(childPid, 'SIGTERM');
  await new Promise(resolve => setTimeout(resolve, 600));
  assert.equal(existsSync(path.join(c.view, 'got-term')), false, 'a TERM to the child-pid value does not reach the program');
  const hostPid = (pidsIn(c.prog) ?? []).find(pid => commOf(pid) === 'bash' && statOf(pid).ppid === childPid);
  assert.ok(hostPid, 'the host pid is in prog/cgroup.procs, a bash whose parent is the box\'s init');
  process.kill(hostPid, 'SIGTERM');
  assert.ok(await waitFor(() => existsSync(path.join(c.view, 'got-term')) || null, 5000), 'a TERM to the host pid from prog/cgroup.procs fires the program\'s trap');
});
