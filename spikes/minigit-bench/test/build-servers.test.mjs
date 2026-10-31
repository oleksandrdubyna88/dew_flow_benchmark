// E3b seam 2: the bare box leaves the build servers at their defaults (banteg's cells ran with the .NET defaults), and whatever one leaves running ends with the box.
import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import { test } from 'node:test';
import { cellProcesses, realProcApi } from '../lib/janitor.mjs';
import { AGENT_ENV_KEYS, BUILD_SERVERS_OFF, agentBoxPrefix, agentEnv, dummyToken } from '../lib/sandbox.mjs';
import { boxOptions, newSession, nodeDir, sleep } from './box-helpers.mjs';
import { sandboxGate } from './helpers.mjs';

const THREE = ['DOTNET_CLI_USE_MSBUILD_SERVER', 'MSBUILDDISABLENODEREUSE', 'UseSharedCompilation'];
const bare = layout => agentEnv({ layout, dummyToken: dummyToken('x'), cellId: 'c', nodeDir: '/n' });

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
  const cell = `cell-${layout.rid}`;
  const env = agentEnv({ layout, dummyToken: dummyToken('x'), cellId: cell, nodeDir });
  const prefix = agentBoxPrefix(boxOptions(layout));
  // a background process outlives its parent shell, as a build server does; the box's command ends after 1.5 s
  const child = spawn(prefix[0], [...prefix.slice(1), 'sh', '-c', '(sleep 300 &) ; sleep 1.5; exit 0'], { env, stdio: 'ignore', detached: true });
  const closed = new Promise(resolve => child.on('close', resolve));
  await sleep(700);
  assert.ok(cellProcesses(cell, realProcApi).pids.length >= 1, 'control: the background process is listed while the box runs');
  await closed;
  await sleep(500);
  assert.equal(cellProcesses(cell, realProcApi).pids.length, 0, 'no cell process is left after the box closed');
});
