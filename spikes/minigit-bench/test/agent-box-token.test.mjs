import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import { readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { scanSessionDir } from '../lib/session-scan.mjs';
import { dummyToken } from '../lib/sandbox.mjs';
import { brokerServer } from '../lib/broker.mjs';
import { fakeToken, prices } from './broker-stub.mjs';
import { envFor, newSession, probe, probeInit, sleep } from './box-helpers.mjs';
import { sandboxGate, tmpDir } from './helpers.mjs';

/** A broker holding the fake real token R on the layout's api socket: the host side of the story, while the box holds only D. */
async function brokerHolding(layout, real) {
  return brokerServer({
    socketPath: layout.sockets.api, token: real, upstream: { host: '127.0.0.1', port: 9, tls: false }, budget: { capUsd: 0.2, reserveUsd: 0.05 },
    prices: prices(), cliVersion: '2.1.310', log: () => {},
  });
}

const D = dummyToken('tokentest01');

test('The_box_holds_only_the_dummy_in_printenv', async t => {
  if (!sandboxGate(t)) return;
  const R = fakeToken();
  const layout = newSession();
  const broker = await brokerHolding(layout, R);
  try {
    const env = envFor(layout);
    const box = await probe(layout, 'printenv CLAUDE_CODE_OAUTH_TOKEN; env', { env: { ...env, CLAUDE_CODE_OAUTH_TOKEN: D } });
    assert.ok(box.out.includes(D), 'the dummy is what the box holds');
    assert.equal(box.out.includes(R), false, 'the real token is nowhere in printenv or env');
    const control = await probe(layout, 'printenv CLAUDE_CODE_OAUTH_TOKEN; env', { env: { ...env, CLAUDE_CODE_OAUTH_TOKEN: R } });
    assert.ok(control.out.includes(R), 'positive control: with R placed in the env by hand the same probe prints R');
  } finally {
    await broker.close();
  }
});

test('The_box_holds_only_the_dummy_in_proc_environ', async t => {
  if (!sandboxGate(t)) return;
  const R = fakeToken();
  const layout = newSession();
  const broker = await brokerHolding(layout, R);
  try {
    const count = value => `cat /proc/[0-9]*/environ 2>/dev/null | tr '\\0' '\\n' | grep -c -F -e ${value} ; true`;
    const env = { ...envFor(layout), CLAUDE_CODE_OAUTH_TOKEN: D };
    // through box-init: the box has several processes (the init, sh, the probe's own pipeline) whose environ the probe reads
    const box = await probeInit(layout, `echo "real=$(${count(R)})"; echo "dummy=$(${count(D)})"`, { env });
    assert.match(box.out, /real=0\b/);
    assert.match(box.out, /dummy=([1-9]\d*)\b/, 'the probe can read environs: the dummy IS in them');
    const control = await probeInit(layout, `echo "real=$(${count(R)})"`, { env: { ...env, CLAUDE_CODE_OAUTH_TOKEN: R } });
    assert.match(control.out, /real=([1-9]\d*)\b/, 'positive control: with R in the env the same probe finds it');
  } finally {
    await broker.close();
  }
});

test('The_box_holds_only_the_dummy_in_what_it_persists', async t => {
  if (!sandboxGate(t)) return;
  const R = fakeToken();
  const layout = newSession();
  const broker = await brokerHolding(layout, R);
  try {
    const persist = l => `printenv CLAUDE_CODE_OAUTH_TOKEN > ${l.out}/t.txt; cp /proc/self/environ ${l.work}/e.bin`;
    const env = { ...envFor(layout), CLAUDE_CODE_OAUTH_TOKEN: D };
    await probe(layout, persist(layout), { env });
    assert.equal(readFileSync(path.join(layout.out, 't.txt'), 'utf8').trim(), D, 't.txt holds the dummy and nothing else');
    const scan = scanSessionDir(layout.dir, { real: R, dummy: D });
    assert.deepEqual(scan.hits, [], 'tokenHits over the whole session dir finds nothing for R');
    assert.ok(scan.scanned >= 2);
    const control = newSession();
    await probe(control, persist(control), { env: { ...envFor(control), CLAUDE_CODE_OAUTH_TOKEN: R } });
    const alive = scanSessionDir(control.dir, { real: R, dummy: D });
    assert.deepEqual(alive.hits.map(h => h.source).sort(), [path.join('box', 'out', 't.txt'), path.join('box', 'work', 'e.bin')].sort(), 'control: with R in the env the scan names both files');
    assert.equal(JSON.stringify(alive.hits).includes(R), false, 'a hit says where and what kind, never the value');
  } finally {
    await broker.close();
  }
});

test('Setting_a_secret_with_setenv_would_list_it_and_that_is_the_point', async () => {
  // the refuted approach, shaped: a value on bwrap's argv is in /proc/<pid>/cmdline, readable by every user
  const secret = dummyToken('setenv-demo');
  const child = spawn('bwrap', ['--ro-bind', '/', '/', '--dev', '/dev', '--proc', '/proc', '--unshare-pid', '--die-with-parent', '--setenv', 'CLAUDE_CODE_OAUTH_TOKEN', secret, 'sleep', '5'], { stdio: 'ignore' });
  try {
    await sleep(300);
    assert.ok(readFileSync(`/proc/${child.pid}/cmdline`, 'latin1').includes(secret), 'found in the cmdline: --setenv is for non-secret values only');
    const ours = readFileSync(`/proc/${child.pid}/environ`, 'latin1');
    assert.equal(ours.includes(secret), false, 'while the spawn env path keeps it out of every cmdline');
  } finally {
    child.kill('SIGKILL');
  }
});

test('The_scan_masks_the_dummy_reports_where_and_never_the_value_and_lists_what_it_could_not_read', () => {
  const dir = tmpDir('scan');
  const R = fakeToken();
  writeFileSync(path.join(dir, 'a.txt'), `dummy ${D} and nothing else`);
  writeFileSync(path.join(dir, 'b.txt'), `leak ${R} here`);
  const scan = scanSessionDir(dir, { real: R, dummy: D });
  assert.deepEqual(scan.hits.map(h => h.source), ['b.txt']);
  assert.equal(scan.hits[0].offset, 5);
  assert.deepEqual(scan.tooLarge, []);
  assert.equal(JSON.stringify(scan).includes(R), false);
});
