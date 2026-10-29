import assert from 'node:assert/strict';
import { randomBytes } from 'node:crypto';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import test from 'node:test';
import { spawn } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { cliInvocation, descendantsOf, killTree, npmInvocation, run } from '../lib/proc.mjs';

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
  assert.doesNotThrow(() => killTree(4242, { kill: errno('ESRCH'), log, procs: () => [] }));
  assert.deepEqual(logged, [], 'a group that is already gone is the goal, not a problem');
  assert.doesNotThrow(() => killTree(4242, { kill: errno('EPERM'), log, procs: () => [] }));
  assert.equal(logged.length, 1);
  assert.match(logged[0], /EPERM/);
});

test('A_posix_kill_signals_the_group_then_the_pid_and_no_pid_means_nothing', { skip: !posix && 'POSIX signals' }, () => {
  const calls = [];
  const kill = (pid, sig) => calls.push([pid, sig]);
  killTree(4242, { kill, procs: () => [] });
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

const ctrlcParent = fileURLToPath(new URL('./fixtures/ctrlc-parent.mjs', import.meta.url));
const setsid = ['/usr/bin/setsid', '/bin/setsid'].find(p => fs.existsSync(p));

test('A_timeout_ends_a_grandchild_in_its_own_session_too', { skip: !posix && 'POSIX sessions' }, async t => {
  if (!setsid) return t.skip('setsid is not installed');
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'testsToDelete-pab-proc-'));
  const mark = `BENCH_MARK_${randomBytes(6).toString('hex')}`;
  try {
    // the CLI's Bash-tool shell runs in its own session (pid = pgid = sid): a group kill of the launch cannot reach it
    const script = `sleep 300 & echo $! > '${dir}/plain'; ${setsid} sleep 300 & echo $! > '${dir}/own-session'; wait`;
    const outcome = await raceDeadline(run('sh', ['-c', script], { timeoutMs: 500, env: { ...process.env, BENCH_MARK: mark } }), 5000);
    const own = Number(fs.readFileSync(path.join(dir, 'own-session'), 'utf8').trim());
    const survivors = pidsCarrying(mark);
    assert.notEqual(outcome, 'deadline', 'run() resolves');
    await sleep(100);
    assert.equal(alive(own), false, `the setsid'd grandchild ${own} survived the timeout`);
    assert.deepEqual(survivors, [], 'no process still carries the mark');
  } finally {
    for (const pid of pidsCarrying(mark)) { try { process.kill(pid, 'SIGKILL'); } catch { /* gone */ } }
    fs.rmSync(dir, { recursive: true, force: true });
  }
});

test('descendantsOf_walks_the_ppid_chain_and_reports_each_pgid', () => {
  const table = [{ pid: 10, ppid: 1, pgid: 10 }, { pid: 11, ppid: 10, pgid: 10 }, { pid: 12, ppid: 11, pgid: 12 }, { pid: 13, ppid: 99, pgid: 13 }, { pid: 14, ppid: 12, pgid: 14 }];
  assert.deepEqual(descendantsOf(10, table).map(d => [d.pid, d.pgid]), [[11, 10], [12, 12], [14, 14]]);
  assert.deepEqual(descendantsOf(13, table), []);
  assert.deepEqual(descendantsOf(7, table), []);
});

test('The_kill_signals_each_descendant_and_its_group_but_never_the_callers_own_group', { skip: !posix && 'POSIX signals' }, () => {
  const calls = [];
  const own = { pid: process.pid, ppid: 1, pgid: 555 };
  const table = [own, { pid: 20, ppid: 1, pgid: 20 }, { pid: 21, ppid: 20, pgid: 21 }, { pid: 22, ppid: 21, pgid: 555 }];
  killTree(20, { kill: (pid, sig) => calls.push([pid, sig]), procs: () => table });
  const targets = calls.map(c => c[0]);
  assert.deepEqual(targets, [-20, 20, -21, 21, 22]);
  assert.equal(targets.includes(-555), false, 'the harness\'s own group is never signalled');
});

test('Ctrl_C_on_the_parent_leaves_no_detached_child_behind', { skip: !posix && 'POSIX signals' }, async () => {
  const mark = `CTRLC_${randomBytes(6).toString('hex')}`;
  const parent = spawn(process.execPath, [ctrlcParent, mark], { stdio: 'ignore', detached: true });
  const closed = new Promise(resolve => parent.on('close', (code, signal) => resolve({ code, signal })));
  try {
    for (let i = 0; i < 100 && pidsCarrying(mark).length === 0; i += 1) await sleep(50);
    assert.equal(pidsCarrying(mark).length, 1, 'the parent has started its detached child');
    process.kill(parent.pid, 'SIGINT'); // a terminal's Ctrl-C reaches the parent's group, not the child's own
    const ended = await raceDeadline(closed, 5000);
    assert.notEqual(ended, 'deadline', 'the parent exits on SIGINT');
    assert.equal(ended.code, 130, 'exit code 128 + SIGINT');
    await sleep(100);
    assert.deepEqual(pidsCarrying(mark), [], 'the detached child died with its parent');
  } finally {
    for (const pid of pidsCarrying(mark)) { try { process.kill(pid, 'SIGKILL'); } catch { /* gone */ } }
    try { process.kill(-parent.pid, 'SIGKILL'); } catch { /* gone */ }
  }
});

test('A_cmd_shim_is_run_through_its_js_entry_by_the_running_node_and_anything_else_by_its_own_path', () => {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'testsToDelete-pab-cli-'));
  try {
    const entry = '@openai/codex/bin/codex.js';
    fs.mkdirSync(path.join(dir, 'win', 'node_modules', '@openai', 'codex', 'bin'), { recursive: true });
    fs.writeFileSync(path.join(dir, 'win', 'codex.cmd'), '@echo off\r\n');
    fs.writeFileSync(path.join(dir, 'win', 'node_modules', entry), '');
    fs.mkdirSync(path.join(dir, 'posix'));
    fs.writeFileSync(path.join(dir, 'posix', 'codex'), '');
    const exts = ['', '.exe', '.cmd'];
    assert.deepEqual(cliInvocation('codex', ['exec', '-'], { pathValue: path.join(dir, 'win'), entry, exts }),
      { exe: process.execPath, args: [path.join(dir, 'win', 'node_modules', entry), 'exec', '-'] });
    assert.deepEqual(cliInvocation('codex', ['exec'], { pathValue: path.join(dir, 'posix'), entry, exts }), { exe: path.join(dir, 'posix', 'codex'), args: ['exec'] });
    assert.throws(() => cliInvocation('codex', [], { pathValue: path.join(dir, 'none'), entry, exts }), /codex not found on PATH/);
    fs.rmSync(path.join(dir, 'win', 'node_modules'), { recursive: true });
    assert.throws(() => cliInvocation('codex', [], { pathValue: path.join(dir, 'win'), entry, exts }), /codex\.js/);
  } finally {
    fs.rmSync(dir, { recursive: true, force: true });
  }
});
