// Quick run W1: a daemon and a web case through the REAL prefix (scope, box, holder namespaces): PORT and the scenario's env reach the boxed program, SIGTERM
// reaches it (host pid found in the cgroup), the exit is read from bwrap, the memory from prog/. Toy programs only (python3 is in the box); the stacks' hello programs
// are proved by test/hello/prove.sh.
import assert from 'node:assert/strict';
import { writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { runCases } from '../lib/exec-layer/run.mjs';
import { needs } from './support/exec-layer-host.mjs';
import { layerOptions } from './support/exec-tree.mjs';
import { owned } from './support/owned.mjs';

const DAEMON_SH = `#!/bin/bash
exec python3 -I -u -c '
import os, signal, socket, sys
srv = socket.socket(); srv.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
srv.bind(("127.0.0.1", int(os.environ["PORT"]))); srv.listen(5)
signal.signal(signal.SIGTERM, lambda *a: os._exit(0))
tag = os.environ.get("TAG", "none").encode()
while True:
    c, _ = srv.accept()
    d = c.recv(100)
    c.sendall(b"echo:" + d + b":" + tag)
'
`;

const IN = `scenario 2
default within 2s
env TAG "boxed"
start ready port within 10s
connect c tcp
send c "hi"
recv c bytes 13
signal TERM
wait-exit within 5s
`;

function samples(t, out) {
  const dir = owned.ownedDir(t, 'samples');
  writeFileSync(path.join(dir, 's01.in'), IN);
  writeFileSync(path.join(dir, 's01.out'), out);
  return dir;
}

test('A_daemon_case_through_the_real_box_gets_PORT_and_env_and_SIGTERM_and_passes', async t => {
  if (!needs(t)) return;
  const result = await runCases(layerOptions(t, { kind: 'daemon', runSh: DAEMON_SH, samplesDir: samples(t, 'transcript 2\nready\nrecv c "echo:hi:boxed"\nexit 0\n') }));
  assert.equal(result.exitCode, 0, result.stderr);
  const entry = result.record.cases[0];
  assert.equal(entry.status, 'PASS', JSON.stringify(entry));
  assert.equal(entry.lifecycle.stop.signal, 'TERM');
  assert.equal(entry.lifecycle.memory.known, true);
  assert.equal(result.cleanup.clean, true, JSON.stringify(result.cleanup.left));
});

test('A_daemon_case_through_the_real_box_that_differs_fails_with_the_mismatch_not_a_crash', async t => {
  if (!needs(t)) return;
  const result = await runCases(layerOptions(t, { kind: 'daemon', runSh: DAEMON_SH, samplesDir: samples(t, 'transcript 2\nready\nrecv c "echo:hi:other"\nexit 0\n') }));
  assert.equal(result.exitCode, 1, result.stderr);
  assert.equal(result.record.cases[0].status, 'FAIL');
});
