// The `run` verb: executes a capture plan. I/O at the edge - it starts the file sink, spawns the CLI as exe + argv (no shell) with the
// prompt on stdin and the fixed environment, enforces the wall limit by killing the whole process group, then collects what the CLI left.
import { spawn } from 'node:child_process';
import { appendFileSync, closeSync, copyFileSync, createWriteStream, mkdirSync, openSync, readdirSync, statSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { performance } from 'node:perf_hooks';
import { fail, ok } from '../lib/result.mjs';
import { capturePlan } from './capture-plan.mjs';
import { startSink } from './otlp-sink.mjs';

const EXIT_CAPTURE_FAILED = 5;
const failed = reason => fail(reason, { exit: EXIT_CAPTURE_FAILED });

function killTree(child) {
  if (!child.pid) return; // never started: nothing to kill
  try {
    process.kill(-child.pid, 'SIGKILL');
  } catch (e) {
    if (e.code !== 'ESRCH') throw e; // the group is already gone: that is the goal
  }
}

/** Every `*.jsonl` under `root`, as paths relative to it. The layout is an assumption (A-T1), so it is walked, never built. */
function walkJsonl(root) {
  const walk = dir => readdirSync(dir).flatMap(name => {
    const full = path.join(dir, name);
    if (statSync(full).isDirectory()) return walk(full);
    return name.endsWith('.jsonl') ? [path.relative(root, full)] : [];
  });
  try {
    return walk(root);
  } catch (e) {
    if (e.code === 'ENOENT') return [];
    throw e;
  }
}

function copyTranscripts(out) {
  const from = path.join(out, 'config', 'projects');
  return walkJsonl(from).map(rel => {
    const target = path.join(out, 'transcripts', rel);
    mkdirSync(path.dirname(target), { recursive: true });
    copyFileSync(path.join(from, rel), target);
    return rel;
  });
}

/** Watches the stream as it arrives: the init event's tool list must hold `Agent` (the parent plan's launch line says `Task`). */
function streamWatcher(onProblem) {
  let buffer = '';
  const seen = { init: null, result: null };
  const line = text => {
    let ev;
    try { ev = JSON.parse(text); } catch { return; } // not JSON: kept in the file, nothing to inspect
    if (ev.type === 'system' && ev.subtype === 'init') {
      seen.init = ev;
      const names = (ev.tools ?? []).map(t => (typeof t === 'string' ? t : t?.name));
      if (!names.includes('Agent')) onProblem(`the init event's tools list lacks Agent (${names.join(', ') || 'empty'}): the capture cannot launch its children`);
    }
    if (ev.type === 'result') seen.result = ev;
  };
  return {
    seen,
    push(chunk) {
      buffer += chunk;
      const parts = buffer.split('\n');
      buffer = parts.pop();
      parts.filter(p => p.trim() !== '').forEach(line);
    },
    flush() { if (buffer.trim() !== '') line(buffer); buffer = ''; },
  };
}

function spawnClaude(plan, exe, stderrFd) {
  return spawn(exe, plan.argv, { cwd: plan.cwd, env: plan.env, stdio: ['pipe', 'pipe', stderrFd], detached: true });
}

/** Run one capture into `out`. `exe` and the timings are injectable so the plumbing is testable without the real CLI. */
export async function runCapture({ out, spikeDir, exe = 'claude', token, env, timeoutMs, settleMs = 3000 }) {
  const started = performance.now();
  const log = step => appendFileSync(path.join(out, 'capture.log'), `${new Date().toISOString()} ${step}\n`);
  mkdirSync(path.join(out, 'tree'), { recursive: true });
  mkdirSync(path.join(out, 'config'), { recursive: true });
  log('out dir created');
  const sink = await startSink({ dir: out });
  try {
    log(`file sink listening on 127.0.0.1:${sink.port}`);
    const planned = capturePlan({ out, spikeDir, port: sink.port, token, env });
    if (!planned.ok) return failed(planned.reason);
    const plan = timeoutMs ? { ...planned.value, timeoutMs } : planned.value;
    for (const [rel, text] of Object.entries(plan.files)) {
      mkdirSync(path.dirname(path.join(out, rel)), { recursive: true });
      writeFileSync(path.join(out, rel), text);
    }
    log('tree, settings.json and mcp.json written');
    const outcome = await launch(plan, exe, out, log);
    await new Promise(resolve => setTimeout(resolve, settleMs));
    log(`waited ${settleMs} ms for the last otel batch`);
    return finish({ out, plan, outcome, started, log });
  } finally {
    await sink.close();
    log('file sink closed');
  }
}

function launch(plan, exe, out, log) {
  return new Promise(resolve => {
    const stderrFd = openSync(path.join(out, 'stderr.txt'), 'w');
    const stream = createWriteStream(path.join(out, 'stream.ndjson'));
    let problem = null;
    const watcher = streamWatcher(reason => { problem ??= reason; killTree(child); });
    const child = spawnClaude(plan, exe, stderrFd);
    const timer = setTimeout(() => { problem ??= `the capture exceeded its ${plan.timeoutMs} ms time limit and was killed with its process tree`; killTree(child); }, plan.timeoutMs);
    child.on('error', e => { problem ??= `could not launch claude: ${e.message}`; });
    child.stdout.on('data', chunk => { stream.write(chunk); watcher.push(chunk.toString('utf8')); });
    child.stdin.on('error', () => { /* the child exited before reading its prompt: its exit code says why */ });
    child.stdin.end(plan.stdin);
    log(`spawned ${exe} (pid ${child.pid ?? 'none'})`);
    child.on('close', (code, signal) => {
      clearTimeout(timer);
      watcher.flush();
      closeSync(stderrFd);
      stream.end(() => resolve({ code, signal, problem, seen: watcher.seen }));
    });
  });
}

function finish({ out, plan, outcome, started, log }) {
  const transcripts = copyTranscripts(out);
  log(`copied ${transcripts.length} transcript file(s)`);
  const { seen } = outcome;
  const meta = {
    cliVersion: seen.init?.claude_code_version ?? null, date: new Date().toISOString(), sessionId: seen.init?.session_id ?? seen.result?.session_id ?? null,
    exitCode: outcome.code, signal: outcome.signal, wallMs: Math.round(performance.now() - started), costUsd: seen.result?.total_cost_usd ?? null,
  };
  writeFileSync(path.join(out, 'argv.json'), `${JSON.stringify({ exe: plan.exe, argv: plan.argv, envKeys: plan.envKeys }, null, 2)}\n`);
  writeFileSync(path.join(out, 'meta.json'), `${JSON.stringify(meta, null, 2)}\n`);
  log(`meta.json and argv.json written (exit ${outcome.code}, signal ${outcome.signal})`);
  if (outcome.problem) return failed(outcome.problem);
  if (outcome.code !== 0) return failed(`claude exited with code ${outcome.code}${outcome.signal ? ` (signal ${outcome.signal})` : ''}: see stderr.txt`);
  if (!seen.result) return failed('the stream ended without a result event');
  return ok({ costUsd: meta.costUsd, meta, transcripts });
}
