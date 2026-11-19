// E3b seam 2: the bare box leaves the build servers at their defaults (banteg's cells ran with the .NET defaults), and whatever one leaves running ends with the box.
import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import { randomBytes } from 'node:crypto';
import { test } from 'node:test';
import { cellProcesses, realProcApi } from '../lib/janitor.mjs';
import { AGENT_ENV_KEYS, BUILD_SERVERS_OFF, agentBoxPrefix, agentEnv, dummyToken } from '../lib/sandbox.mjs';
import { boxOptions, newSession, nodeDir, sleep, waitNone } from './box-helpers.mjs';
import { sandboxGate } from './helpers.mjs';

const THREE = ['DOTNET_CLI_USE_MSBUILD_SERVER', 'MSBUILDDISABLENODEREUSE', 'UseSharedCompilation'];
const bare = layout => agentEnv({ layout, dummyToken: dummyToken('x'), cellId: 'c', nodeDir: '/n' });
const GONE_WITHIN_MS = 10_000;
/**
 * A cell id of THIS test's own. The test counts the processes that carry it, and the count must not see another test's box: `cell-${layout.rid}` is `cell-s2` here and in every other test file
 * whose second session it is (the rid is a per-file counter). That is what failed in the whole suite on 2026-11-19, 5 processes "left": they were the box of `agent-box-dotnet.test.mjs`, whose
 * second session builds a project for many seconds under the same `cell-s2`. Alone the test passed, even with its pause at 0.
 */
const ownCell = () => `cell-bs-${process.pid}-${randomBytes(4).toString('hex')}`;

test('The_bare_env_leaves_build_servers_at_their_defaults', () => {
  const layout = newSession();
  const env = bare(layout);
  for (const k of THREE) assert.equal(k in env, false, `${k} is absent from the bare environment`);
  assert.deepEqual(Object.keys(BUILD_SERVERS_OFF).sort(), [...THREE].sort(), 'the three keys are exported as the arm\'s choice');
  const off = agentEnv({ layout, dummyToken: dummyToken('x'), cellId: 'c', nodeDir: '/n', extra: BUILD_SERVERS_OFF });
  assert.deepEqual(THREE.map(k => off[k]), ['0', '1', 'false'], 'passed through extra, an arm that wants them off gets them');
  for (const k of THREE) assert.equal(AGENT_ENV_KEYS.includes(k), false, `${k} is not in the one fixed key list`);
  assert.equal(Object.isFrozen(BUILD_SERVERS_OFF), true);
});

test('A_build_server_left_running_ends_with_the_box', async t => {
  if (!sandboxGate(t)) return;
  const layout = newSession();
  const cell = ownCell();
  const env = agentEnv({ layout, dummyToken: dummyToken('x'), cellId: cell, nodeDir });
  const prefix = agentBoxPrefix(boxOptions(layout));
  // a background process outlives its parent shell, as a build server does; the box's command ends after 1.5 s
  const child = spawn(prefix[0], [...prefix.slice(1), 'sh', '-c', '(sleep 300 &) ; sleep 1.5; exit 0'], { env, stdio: 'ignore', detached: true });
  const closed = new Promise(resolve => child.on('close', resolve));
  await sleep(700);
  assert.ok(cellProcesses(cell, realProcApi).pids.length >= 1, 'control: the background process is listed while the box runs');
  await closed;
  // no fixed pause: asked every 100 ms and done at the first empty answer; the bound is how long a loaded host may take to reap the namespace before this is a defect
  const gone = await waitNone(cell, GONE_WITHIN_MS, 100);
  assert.deepEqual(cellProcesses(cell, realProcApi).pids, [], `no process of this box's own cell is left ${gone ? '' : `${GONE_WITHIN_MS / 1000} s `}after the box closed`);
});

test('A_process_that_really_stays_under_the_cell_id_is_reported_the_wait_does_not_pass_by_waiting', async () => {
  // no box: a process of this user OUTSIDE any pid namespace, carrying a cell id of this test's own, is what a box that leaked would leave
  const cell = ownCell();
  const stays = spawn('sleep', ['30'], { env: { PATH: process.env.PATH, BENCH_CELL: cell }, stdio: 'ignore' });
  const exited = new Promise(resolve => stays.on('exit', resolve));
  try {
    await sleep(100);
    assert.equal(await waitNone(cell, 600, 100), false, 'the wait says a process is left');
    assert.deepEqual(cellProcesses(cell, realProcApi).pids, [String(stays.pid)], 'and it is that process, by its pid');
  } finally {
    stays.kill('SIGKILL');
    await exited;
  }
  assert.equal(await waitNone(cell, GONE_WITHIN_MS, 100), true, 'control: once it is gone the same wait ends at once');
});
