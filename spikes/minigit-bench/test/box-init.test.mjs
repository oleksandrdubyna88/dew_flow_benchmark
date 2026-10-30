import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import net from 'node:net';
import path from 'node:path';
import { test } from 'node:test';
import { runBounded } from '../lib/run.mjs';
import { spikeDir, tmpDir } from './helpers.mjs';

const BOX_INIT = path.join(spikeDir, 'harness', 'box-init.mjs');
const boxInit = (args, timeoutMs = 20_000) => runBounded(process.execPath, [BOX_INIT, ...args], { timeoutMs });

async function freePort() {
  const s = net.createServer();
  await new Promise(r => s.listen(0, '127.0.0.1', r));
  const { port } = s.address();
  await new Promise(r => s.close(r));
  return port;
}

test('Box_init_runs_the_command_and_returns_its_code', async () => {
  const exit7 = await boxInit(['--', 'sh', '-c', 'exit 7']);
  assert.equal(exit7.value.code, 7);
  const killed = await boxInit(['--', 'sh', '-c', 'kill -TERM $$']);
  assert.equal(killed.value.code, 143, 'a child ended by SIGTERM is 128 + 15');
  const killed9 = await boxInit(['--', 'sh', '-c', 'kill -KILL $$']);
  assert.equal(killed9.value.code, 137);
  const hup = await boxInit(['--', 'sh', '-c', 'kill -HUP $$']);
  assert.equal(hup.value.code, 129, 'every signal maps to its own number, not 137');
  const missing = await boxInit(['--', '/no/such/command']);
  assert.equal(missing.value.code, 127);
  assert.match(missing.value.stderr, /could not run/);
});

test('A_forwarder_whose_port_is_taken_fails_before_the_command_runs', async () => {
  const taken = net.createServer();
  await new Promise(r => taken.listen(0, '127.0.0.1', r));
  try {
    const marker = path.join(tmpDir('bi'), 'ran');
    const r = await boxInit([`${taken.address().port}:/nonexistent.sock`, '--', 'sh', '-c', `touch ${marker}`]);
    assert.notEqual(r.value.code, 0);
    assert.match(r.value.stderr, /could not listen/);
    assert.equal((await import('node:fs')).existsSync(marker), false, 'the command never ran');
  } finally {
    taken.close();
  }
});

test('A_forwarder_pipes_a_loopback_port_to_a_unix_socket_and_dies_with_box_init', async () => {
  const dir = tmpDir('bi-fwd');
  const sock = path.join(dir, 's.sock');
  const server = net.createServer(c => c.on('data', d => c.write(`got:${d}`)));
  await new Promise(r => server.listen(sock, r));
  try {
    const port = await freePort();
    const script = `exec 3<>/dev/tcp/127.0.0.1/${port}; printf hello >&3; head -c 9 <&3`;
    const r = await boxInit([`${port}:${sock}`, '--', 'bash', '-c', script]);
    assert.equal(r.value.code, 0, r.value.stderr);
    assert.equal(r.value.stdout, 'got:hello');
    await assert.rejects(new Promise((resolve, reject) => net.connect(port, '127.0.0.1').once('connect', resolve).once('error', reject)), /ECONNREFUSED/, 'the forwarder died with box-init');
  } finally {
    server.close();
  }
});

test('Box_init_is_one_file_of_node_builtins_only', () => {
  const src = readFileSync(BOX_INIT, 'utf8');
  const imports = [...src.matchAll(/from '([^']+)'/g)].map(m => m[1]);
  assert.ok(imports.length > 0);
  assert.ok(imports.every(i => i.startsWith('node:')), `only builtins: ${imports.join(', ')}`);
});
