// The `run` verb: executes a capture plan. I/O at the edge - it starts the file sink, spawns the CLI as exe + argv (no shell) with the
// prompt on stdin and the fixed environment, enforces the wall limit by killing the whole process group, then collects what the CLI left.
import { spawn } from 'node:child_process';
import { appendFileSync, closeSync, copyFileSync, createWriteStream, lstatSync, mkdirSync, openSync, readdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { performance } from 'node:perf_hooks';
import { fail, ok } from '../lib/result.mjs';
import { SUBAGENT_TOOL_NAMES, capturePlan, subagentToolOf } from './capture-plan.mjs';
import { startSink } from './otlp-sink.mjs';

const EXIT_CAPTURE_FAILED = 5;
const failed = reason => fail(reason, { exit: EXIT_CAPTURE_FAILED });

/**
 * Kill the child's whole process group. Called from timers and stream callbacks, so it NEVER throws: a group that is already gone
 * (ESRCH) is the goal, any other failure (EPERM, ...) is reported through `log` and the capture goes on to its own failure.
 */
export function killTree(child, { kill = process.kill.bind(process), log = () => {} } = {}) {
  if (!child.pid) return; // never started: nothing to kill
  try {
    kill(-child.pid, 'SIGKILL');
  } catch (e) {
    if (e.code !== 'ESRCH') {
      log(`could not kill the process group of pid ${child.pid}: ${e.code ?? e.name}: ${e.message}`);
    }
  }
}

/**
 * Every `*.jsonl` under `root` as a path relative to it, plus `refused`: entries that are neither a real directory nor a regular
 * file (a link, a device, a pipe). The layout is an assumption (A-T1), so it is walked, never built; nothing is followed, because
 * the config directory is written by the CLI under test and a link in it must not lead the copy outside the capture.
 */
function walkJsonl(root) {
  const files = [];
  const refused = [];
  const walk = dir => {
    for (const name of readdirSync(dir)) {
      const full = path.join(dir, name);
      const stat = lstatSync(full);
      if (stat.isDirectory()) walk(full);
      else if (!stat.isFile()) refused.push(path.relative(root, full));
      else if (name.endsWith('.jsonl')) files.push(path.relative(root, full));
    }
  };
  try {
    walk(root);
  } catch (e) {
    if (e.code !== 'ENOENT') throw e;
  }
  return { files, refused };
}

/** Copy the CLI's transcripts into `transcripts/`: ok(relative paths), or a failure naming every refused entry and copying nothing. */
export function copyTranscripts(out) {
  const from = path.join(out, 'config', 'projects');
  const { files, refused } = walkJsonl(from);
  if (refused.length > 0) return fail(`the transcript tree holds ${refused.length} entr${refused.length === 1 ? 'y' : 'ies'} that ${refused.length === 1 ? 'is' : 'are'} not a regular file or a real directory (${refused.join(', ')}): nothing was copied`);
  return ok(files.map(rel => {
    const target = path.join(out, 'transcripts', rel);
    mkdirSync(path.dirname(target), { recursive: true });
    copyFileSync(path.join(from, rel), target);
    return rel;
  }));
}

/** Watches the stream as it arrives: the init event's tool list must hold the subagent tool under either name (SUBAGENT_TOOL_NAMES). */
function streamWatcher(onProblem) {
  let buffer = '';
  const seen = { init: null, result: null };
  const line = text => {
    let ev;
    try { ev = JSON.parse(text); } catch { return; } // not JSON: kept in the file, nothing to inspect
    if (ev.type === 'system' && ev.subtype === 'init') {
      seen.init = ev;
      const names = (ev.tools ?? []).map(t => (typeof t === 'string' ? t : t?.name));
      if (!subagentToolOf(names)) onProblem(`the init event's tools list lacks ${SUBAGENT_TOOL_NAMES.join(' or ')} (${names.join(', ') || 'empty'}): the capture cannot launch its children`);
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

/** Run one capture into `out`. `exe` and the timings are injectable so the plumbing is testable without the real CLI. */
export async function runCapture({ out, spikeDir, exe = 'claude', token, env, timeoutMs, settleMs = 3000, spawnFn = spawn }) {
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
    const outcome = await launch(plan, exe, out, log, spawnFn);
    await new Promise(resolve => setTimeout(resolve, settleMs));
    log(`waited ${settleMs} ms for the last otel batch`);
    return finish({ out, plan, outcome, started, log });
  } finally {
    await sink.close();
    log('file sink closed');
  }
}

/**
 * Spawn the CLI and settle exactly once: on `close` (the normal end) or on `error` (a launch that failed, which need not be followed by
 * a close). Settling clears the timer and closes the descriptors; a second event finds the run already settled and does nothing.
 */
function launch(plan, exe, out, log, spawnFn) {
  return new Promise(resolve => {
    const stderrFd = openSync(path.join(out, 'stderr.txt'), 'w');
    const stream = createWriteStream(path.join(out, 'stream.ndjson'));
    let problem = null;
    let settled = false;
    const kill = () => killTree(child, { log });
    const watcher = streamWatcher(reason => { problem ??= reason; kill(); });
    const child = spawnFn(exe, plan.argv, { cwd: plan.cwd, env: plan.env, stdio: ['pipe', 'pipe', stderrFd], detached: true });
    const timer = setTimeout(() => { problem ??= `the capture exceeded its ${plan.timeoutMs} ms time limit and was killed with its process tree`; kill(); }, plan.timeoutMs);
    const settle = (code, signal) => {
      if (settled) return;
      settled = true;
      clearTimeout(timer);
      watcher.flush();
      closeSync(stderrFd);
      stream.end(() => resolve({ code, signal, problem, seen: watcher.seen }));
    };
    child.on('error', e => { problem ??= `could not launch claude: ${e.message}`; settle(null, null); });
    child.stdout.on('data', chunk => { stream.write(chunk); watcher.push(chunk.toString('utf8')); });
    child.stdin.on('error', () => { /* the child exited before reading its prompt: its exit code says why */ });
    child.stdin.end(plan.stdin);
    log(`spawned ${exe} (pid ${child.pid ?? 'none'})`);
    child.on('close', settle);
  });
}

function finish({ out, plan, outcome, started, log }) {
  const copied = copyTranscripts(out);
  const transcripts = copied.ok ? copied.value : [];
  log(copied.ok ? `copied ${transcripts.length} transcript file(s)` : copied.reason);
  const { seen } = outcome;
  const meta = {
    cliVersion: seen.init?.claude_code_version ?? null, date: new Date().toISOString(), sessionId: seen.init?.session_id ?? seen.result?.session_id ?? null,
    exitCode: outcome.code, signal: outcome.signal, wallMs: Math.round(performance.now() - started), costUsd: seen.result?.total_cost_usd ?? null,
  };
  writeFileSync(path.join(out, 'argv.json'), `${JSON.stringify({ exe: plan.exe, argv: plan.argv, envKeys: plan.envKeys }, null, 2)}\n`);
  writeFileSync(path.join(out, 'meta.json'), `${JSON.stringify(meta, null, 2)}\n`);
  log(`meta.json and argv.json written (exit ${outcome.code}, signal ${outcome.signal})`);
  if (outcome.problem) return failed(outcome.problem);
  if (!copied.ok) return failed(copied.reason);
  if (outcome.code !== 0) return failed(`claude exited with code ${outcome.code}${outcome.signal ? ` (signal ${outcome.signal})` : ''}: see stderr.txt`);
  if (!seen.result) return failed('the stream ended without a result event');
  return ok({ costUsd: meta.costUsd, meta, transcripts });
}
