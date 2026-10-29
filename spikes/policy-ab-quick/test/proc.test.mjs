import assert from 'node:assert/strict';
import { randomBytes } from 'node:crypto';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import test from 'node:test';
import { killTree, npmInvocation, run } from '../lib/proc.mjs';

const posix = process.platform !== 'win32';
const sleep = ms => new Promise(r => setTimeout(r, ms));
const alive = pid => fs.existsSync(`/proc/${pid}`);

/** A `/proc/<pid>/environ` that carries `mark`: the only way to find a process a test did not keep a pid for. */
function pidsCarrying(mark) {
  const found = [];
  for (const pid of fs.readdirSync('/proc').filter(n => /^\d+$/.test(n))) {
    try { if (fs.readFileSync(`/proc/${pid}/environ`, 'utf8').includes(mark)) found.push(Number(pid)); } catch { /* gone or not ours */ }
  }
  return found;
}

/** Resolves with the settled value, or with the sentinel after `ms`: a hang becomes an assertion message, not a CI timeout. */
const raceDeadline = (promise, ms) => Promise.race([promise, sleep(ms).then(() => 'deadline')]);

test('A_timeout_ends_a_grandchild_and_run_resolves', { skip: !posix && 'POSIX process groups' }, async () => {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'testsToDelete-pab-proc-'));
  const mark = `BENCH_MARK_${randomBytes(6).toString('hex')}`;
  const gcFile = path.join(dir, 'gc');
  try {
    const started = Date.now();
    const outcome = await raceDeadline(
      run('sh', ['-c', `sleep 300 & echo $! > '${gcFile}'; wait`], { timeoutMs: 500, env: { ...process.env, BENCH_MARK: mark } }),
      5000);
    const gc = Number(fs.readFileSync(gcFile, 'utf8').trim());
    const survivors = pidsCarrying(mark);
    assert.notEqual(outcome, 'deadline', `run() did not resolve 5 s after its 0.5 s timeout; grandchild ${gc} alive: ${alive(gc)}`);
    assert.ok(Date.now() - started < 3000, 'run() resolves soon after the timeout');
    await sleep(100);
    assert.equal(alive(gc), false, `grandchild ${gc} survived the timeout`);
    assert.deepEqual(survivors, [], 'no process still carries the mark');
  } finally {
    for (const pid of pidsCarrying(mark)) { try { process.kill(pid, 'SIGKILL'); } catch { /* gone */ } }
    fs.rmSync(dir, { recursive: true, force: true });
  }
});

const errno = code => () => { throw Object.assign(new Error(code), { code }); };

test('A_kill_of_a_group_that_is_gone_is_quiet', { skip: !posix && 'POSIX signals' }, () => {
  const logged = [];
  const log = line => logged.push(line);
  assert.doesNotThrow(() => killTree(4242, { kill: errno('ESRCH'), log }));
  assert.deepEqual(logged, [], 'a group that is already gone is the goal, not a problem');
  assert.doesNotThrow(() => killTree(4242, { kill: errno('EPERM'), log }));
  assert.equal(logged.length, 1);
  assert.match(logged[0], /EPERM/);
});

test('A_posix_kill_signals_the_group_then_the_pid_and_no_pid_means_nothing', { skip: !posix && 'POSIX signals' }, () => {
  const calls = [];
  const kill = (pid, sig) => calls.push([pid, sig]);
  killTree(4242, { kill });
  assert.deepEqual(calls, [[-4242, 'SIGKILL'], [4242, 'SIGKILL']]);
  killTree(undefined, { kill });
  killTree(0, { kill });
  assert.equal(calls.length, 2);
});

test('npm_without_its_cli_script_is_an_error_not_a_shell', () => {
  const beside = path.join(path.dirname(process.execPath), 'node_modules', 'npm', 'bin', 'npm-cli.js');
  const underLib = path.join(path.dirname(process.execPath), '..', 'lib', 'node_modules', 'npm', 'bin', 'npm-cli.js');
  assert.throws(() => npmInvocation(['ci'], { exists: () => false }), err => {
    assert.match(err.message, /npm-cli\.js not found/);
    assert.ok(err.message.includes(beside) && err.message.includes(underLib), err.message);
    assert.equal(err.message.includes('cmd.exe'), false);
    return true;
  });
});

test('npm_is_found_beside_the_exe_or_under_lib_and_run_by_the_running_node', () => {
  const beside = path.join(path.dirname(process.execPath), 'node_modules', 'npm', 'bin', 'npm-cli.js');
  const underLib = path.join(path.dirname(process.execPath), '..', 'lib', 'node_modules', 'npm', 'bin', 'npm-cli.js');
  assert.deepEqual(npmInvocation(['ci'], { exists: p => p === beside }), { exe: process.execPath, args: [beside, 'ci'] });
  assert.deepEqual(npmInvocation(['ci'], { exists: p => p === underLib }), { exe: process.execPath, args: [underLib, 'ci'] });
});
