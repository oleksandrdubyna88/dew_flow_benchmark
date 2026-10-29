import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import { existsSync, mkdirSync, writeFileSync } from 'node:fs';
import http from 'node:http';
import path from 'node:path';
import { test } from 'node:test';
import { killTree } from '../../policy-ab-quick/lib/proc.mjs';
import { cellProcesses, cpuTicksOf, endCell, endSurvivors, realProcApi } from '../lib/janitor.mjs';
import { startReceiver } from '../lib/otel-receiver.mjs';
import { agentBoxArgv } from '../lib/sandbox.mjs';
import { claudeArgv, mcpConfigFor, settingsFor } from '../lib/launch.mjs';
import { TS_DIR, boxOptions, envFor, newSession, nodeDir, sleep } from './box-helpers.mjs';
import { sandboxGate, tmpDir } from './helpers.mjs';
import { findOnPath } from '../lib/find-on-path.mjs';

const rand = () => Math.random().toString(36).slice(2, 10);

// ---- pure: the process table is a fake ------------------------------------------------------------------------------------------

/** A fake /proc: `{ [pid]: { environ: ['A=1'], argv: ['bwrap', ...], stat: '...' } }`; `denied` pids throw EACCES. */
function fakeProc(table, denied = []) {
  return {
    pids: () => Object.keys(table),
    read: (pid, file) => {
      if (denied.includes(pid)) throw Object.assign(new Error('denied'), { code: 'EACCES' });
      const p = table[pid];
      if (!p) throw Object.assign(new Error('gone'), { code: 'ENOENT' });
      if (file === 'environ') return `${p.environ.join('\0')}\0`;
      if (file === 'cmdline') return `${p.argv.join('\0')}\0`;
      return p.stat;
    },
    signal: () => {},
  };
}

test('The_marker_is_matched_exactly_and_inner_drops_the_outer_bwrap_and_unreadable_entries_are_counted', () => {
  const proc = fakeProc({
    10: { environ: ['BENCH_CELL=abc'], argv: ['bwrap', '--unshare-net', '--', 'node', 'x'] },
    11: { environ: ['BENCH_CELL=abc'], argv: ['/opt/tools/bwrap', '--', 'x'] },
    12: { environ: ['PATH=/x', 'BENCH_CELL=abc'], argv: ['node', 'box-init.mjs'] },
    13: { environ: ['BENCH_CELL=abcd'], argv: ['node', 'other'] },
    14: { environ: ['XBENCH_CELL=abc'], argv: ['node', 'other'] },
    15: { environ: ['BENCH_CELL=abc'], argv: ['claude', '-p'] },
    16: { environ: [], argv: ['x'] },
  }, ['16']);
  const all = cellProcesses('abc', proc);
  assert.deepEqual(all.pids.sort(), ['10', '11', '12', '15']);
  assert.equal(all.unreadable, 1);
  assert.deepEqual(cellProcesses('abc', proc, { inner: true }).pids.sort(), ['12', '15'], 'both bwrap spellings are the outer processes');
  assert.deepEqual(cellProcesses('nope', proc).pids, []);
});

test('Cpu_ticks_are_utime_plus_stime_of_the_listed_pids_read_after_the_last_paren_of_the_name', () => {
  const stat = (name, utime, stime) => `1 (${name}) S 0 0 0 0 0 0 0 0 0 0 ${utime} ${stime} 0 0 20 0 1 0 5 0 0`;
  const proc = fakeProc({ 1: { environ: [], argv: [], stat: stat('a) b', 7, 3) }, 2: { environ: [], argv: [], stat: stat('c', 100, 1) } });
  assert.equal(cpuTicksOf(['1', '2'], proc), 111);
  assert.equal(cpuTicksOf(['1', '99'], proc), 10, 'a pid that vanished adds nothing');
  assert.equal(cpuTicksOf([], proc), 0);
});

// ---- effect: the real box ----------------------------------------------------------------------------------------------------------

/** Start a box (box-init + `script`) detached, resolve once its stdout shows "ready". */
async function startBox(layout, cell, script) {
  const argv = agentBoxArgv({ ...boxOptions(layout), command: ['sh', '-c', script] });
  const child = spawn(argv[0], argv.slice(1), { env: { ...envFor(layout), BENCH_CELL: cell }, stdio: ['ignore', 'pipe', 'pipe'], detached: true });
  const box = { child, stderr: '', stdout: '', exit: null, closed: new Promise(r => child.on('close', (code, signal) => { box.exit = { code, signal }; r(box.exit); })) };
  child.stderr.on('data', c => { box.stderr += c; });
  child.stdout.on('data', c => { box.stdout += c; });
  for (let i = 0; i < 200 && !box.stdout.includes('ready'); i += 1) await sleep(50);
  assert.ok(box.stdout.includes('ready'), `the box did not get ready: ${box.stderr}`);
  return box;
}
const TRAP = 'trap "echo got-TERM >&2; exit 3" TERM; echo ready; sleep 60 & wait $!';
const waitNone = async (cell, ms = 3000) => {
  for (const end = Date.now() + ms; Date.now() < end; await sleep(50)) if (cellProcesses(cell, realProcApi).pids.length === 0) return true;
  return cellProcesses(cell, realProcApi).pids.length === 0;
};

test('A_group_SIGTERM_never_reaches_inside_the_box', async t => {
  if (!sandboxGate(t)) return;
  const cell = `cell-${rand()}`;
  const box = await startBox(newSession(), cell, TRAP);
  const t0 = Date.now();
  process.kill(-box.child.pid, 'SIGTERM');
  const exit = await Promise.race([box.closed, sleep(5000).then(() => 'timeout')]);
  assert.notEqual(exit, 'timeout');
  assert.equal(exit.signal, 'SIGTERM', 'bwrap itself died by the signal');
  assert.ok(Date.now() - t0 < 1000, `closed within a second (${Date.now() - t0} ms)`);
  assert.equal(box.stderr.includes('got-TERM'), false, 'the inner shell never saw the TERM: the box runs under --new-session');
  assert.equal(await waitNone(cell), true, '--die-with-parent ended the inside');
});

test('A_SIGTERM_to_the_inner_pid_is_delivered_and_its_code_comes_out', async t => {
  if (!sandboxGate(t)) return;
  const cell = `cell-${rand()}`;
  const box = await startBox(newSession(), cell, TRAP);
  const everyone = cellProcesses(cell, realProcApi);
  const inner = cellProcesses(cell, realProcApi, { inner: true });
  assert.ok(everyone.pids.length > inner.pids.length, 'control: the outer bwrap and its pid-namespace init carry the marker too');
  const shell = inner.pids.find(pid => realProcApi.read(pid, 'cmdline').split('\0').slice(0, 2).join(' ').endsWith('sh -c'));
  assert.ok(shell, 'the inner shell is found by marker with argv[0] not bwrap');
  process.kill(Number(shell), 'SIGTERM');
  const exit = await Promise.race([box.closed, sleep(5000).then(() => 'timeout')]);
  assert.deepEqual(exit, { code: 3, signal: null }, 'the trap ran and its code came out through box-init and bwrap');
  assert.match(box.stderr, /got-TERM/);
});

test('endCell_is_graceful_then_final', async t => {
  if (!sandboxGate(t)) return;
  // (a) the shell exits itself on TERM: the grace is enough and killTree is never called
  const cellA = `cell-${rand()}`;
  const a = await startBox(newSession(), cellA, TRAP);
  const spyA = [];
  const ra = await endCell({ cellId: cellA, bwrapPid: a.child.pid, graceMs: 2000, killTree: (...x) => { spyA.push(x); killTree(...x); }, procApi: realProcApi });
  assert.deepEqual(await a.closed, { code: 3, signal: null });
  assert.equal(spyA.length, 0, 'killTree was not called');
  assert.equal(ra.closedWithinGrace, true);
  assert.equal(ra.killed, false);
  assert.ok(ra.signalled.length >= 2, 'every inner process was signalled');
  // (b) a shell that ignores TERM: the grace runs out, then killTree, then nothing is left
  const cellB = `cell-${rand()}`;
  const b = await startBox(newSession(), cellB, 'trap "" TERM; echo ready; sleep 300 & wait $!');
  const spyB = [];
  const t0 = Date.now();
  const rb = await endCell({ cellId: cellB, bwrapPid: b.child.pid, graceMs: 500, killTree: (...x) => { spyB.push([Date.now() - t0, x[0]]); killTree(...x); }, procApi: realProcApi });
  assert.equal(spyB.length, 1, 'killTree called once');
  assert.ok(spyB[0][0] >= 500, `after the grace (${spyB[0][0]} ms)`);
  assert.equal(spyB[0][1], b.child.pid);
  assert.equal(rb.closedWithinGrace, false);
  assert.equal(rb.killed, true);
  assert.equal(await waitNone(cellB), true, 'within 3 s no cell process');
  assert.equal(rb.survivorsEnded, 0, 'the janitor ended none: killTree was enough');
});

test('Killing_bwrap_leaves_no_cell_process', async t => {
  if (!sandboxGate(t)) return;
  const cell = `cell-${rand()}`;
  const box = await startBox(newSession(), cell, 'echo ready; sleep 300 & sleep 300');
  assert.ok(cellProcesses(cell, realProcApi).pids.length >= 2, 'control: before the kill the cell has processes');
  killTree(box.child.pid);
  await box.closed;
  assert.equal(await waitNone(cell), true);
});

test('The_janitor_ends_a_survivor_by_its_marker_never_by_name', async () => {
  const cell = `cell-${rand()}`;
  const marked = spawn('sleep', ['300'], { env: { ...process.env, BENCH_CELL: cell }, stdio: 'ignore' });
  // a process NAMED claude without the marker: a copy of sleep called claude
  const dir = tmpDir('jan');
  const named = path.join(dir, 'claude');
  writeFileSync(named, '#!/bin/sh\nexec sleep 300\n', { mode: 0o755 });
  const bystander = spawn(named, [], { stdio: 'ignore', env: { PATH: process.env.PATH } });
  try {
    await sleep(200);
    assert.deepEqual(cellProcesses(cell, realProcApi).pids, [String(marked.pid)], 'control: only the marked process is the cell\'s');
    const ended = await endSurvivors(cell, { killTree, procApi: realProcApi });
    assert.equal(ended, 1);
    await sleep(300);
    assert.equal(marked.exitCode !== null || marked.signalCode !== null, true, 'the marked process is gone');
    assert.equal(bystander.exitCode === null && bystander.signalCode === null, true, 'the one named claude, without the marker, is untouched');
  } finally {
    marked.kill('SIGKILL');
    bystander.kill('SIGKILL');
  }
});

test('The_CLI_stopped_mid_request_exits_itself_within_the_grace', async t => {
  if (!sandboxGate(t)) return;
  const claude = findOnPath('claude');
  if (!claude) { t.skip('the claude executable is not on PATH on this host'); return; }
  const layout = newSession();
  const cell = `cell-${rand()}`;
  // the hanging recorder: records the POST and never answers; free (no model is reached)
  const posts = [];
  const hang = http.createServer((req) => { posts.push(req.url); req.resume(); });
  await new Promise(r => hang.listen(layout.sockets.api, r));
  const rx = await startReceiver({ dir: layout.otel, socketPath: layout.sockets.otel });
  writeFileSync(layout.files.settings, `${JSON.stringify(settingsFor({ arm: 'bare', layout }))}\n`);
  writeFileSync(layout.files.mcp, `${JSON.stringify(mcpConfigFor('bare').value)}\n`);
  writeFileSync(layout.files.gitconfig, '');
  const real = (await import('node:fs')).realpathSync(claude);
  const argv = agentBoxArgv({ ...boxOptions(layout, { claudeBinary: real, toolchains: [], operatorHome: (await import('node:os')).homedir() }), command: [real, ...claudeArgv({ layout, cap: 0.05 })] });
  const child = spawn(argv[0], argv.slice(1), { env: { ...envFor(layout), BENCH_CELL: cell }, stdio: ['pipe', 'pipe', 'pipe'], detached: true });
  let stdout = '';
  child.stdout.on('data', c => { stdout += c; });
  child.stdin.end('Reply OK');
  const exited = new Promise(r => child.on('close', (code, signal) => r({ code, signal })));
  try {
    for (let i = 0; i < 160 && posts.length === 0; i += 1) await sleep(250);
    assert.ok(posts.length > 0, `the CLI reached the recorder: ${stdout.slice(0, 300)}`);
    await sleep(2500);
    const before = rx.liveSum().requests + (await import('node:fs')).readFileSync(path.join(layout.otel, 'otel.ndjson'), 'utf8').split('\n').filter(Boolean).length;
    const spy = [];
    const sentAt = Date.now();
    const r = await endCell({ cellId: cell, bwrapPid: child.pid, graceMs: 10_000, killTree: (...x) => { spy.push(x); killTree(...x); }, procApi: realProcApi });
    const exit = await exited;
    assert.equal(spy.length, 0, 'killTree was not called: the CLI left by itself');
    assert.equal(r.closedWithinGrace, true);
    assert.equal(exit.code, 143, `exit code 143 after ${Date.now() - sentAt} ms`);
    assert.ok(existsSync(layout.config), 'the config dir exists');
    const transcripts = (await import('node:fs')).readdirSync(path.join(layout.config, 'projects'), { recursive: true }).filter(f => String(f).endsWith('.jsonl'));
    t.diagnostic(`transcripts after the stop: ${transcripts.length}; otel records before the SIGTERM: ${before}; after: ${(await import('node:fs')).readFileSync(path.join(layout.otel, 'otel.ndjson'), 'utf8').split('\n').filter(Boolean).length - before} (B-K2's shape; 2026-10-29: 0)`);
    assert.ok(transcripts.length >= 1, 'a transcript is on disk');
  } finally {
    killTree(child.pid);
    hang.close();
    hang.closeAllConnections();
    await rx.close();
  }
}, { timeout: 120_000 });
