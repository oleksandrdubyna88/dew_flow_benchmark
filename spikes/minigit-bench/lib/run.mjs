// One external process, exe + argv (never a shell), always with a timeout that kills the whole process group.
// Expected failures come back as values (result.mjs): ok(value) | fail(reason).
import { spawn } from 'node:child_process';
import { fail, ok } from './result.mjs';

const MAX_BYTES = 8 * 1024 * 1024;
const GRACE_MS = 2000;

/** Collects a stream up to `max` bytes; the rest is dropped and `truncated` says so. */
function collector(max) {
  const chunks = [];
  let size = 0;
  let truncated = false;
  return {
    push(chunk) {
      if (size >= max) { truncated = true; return; }
      const room = max - size;
      chunks.push(chunk.length > room ? chunk.subarray(0, room) : chunk);
      truncated ||= chunk.length > room;
      size += Math.min(chunk.length, room);
    },
    text: () => Buffer.concat(chunks).toString('utf8'),
    truncated: () => truncated,
  };
}

/** Signal the child's process group, then the child itself (the group may be gone already). */
function killGroup(child) {
  for (const target of [-child.pid, child.pid]) {
    try { process.kill(target, 'SIGKILL'); } catch (e) { if (e.code !== 'ESRCH') throw e; }
  }
}

/**
 * Run `exe args` and resolve with { code, signal, stdout, stderr, timedOut, truncated, ms }. A launch failure is
 * { ok: false, reason }. `timeoutMs` is required: nothing here runs unbounded.
 */
export function runBounded(exe, args, { cwd, env, timeoutMs, maxBytes = MAX_BYTES } = {}) {
  if (!Number.isFinite(timeoutMs) || timeoutMs <= 0) return Promise.resolve(fail('runBounded needs a positive timeoutMs'));
  return new Promise(resolve => {
    const started = Date.now();
    const out = collector(maxBytes);
    const err = collector(maxBytes);
    let timedOut = false;
    let settled = false;
    const child = spawn(exe, args, { cwd, env, shell: false, detached: true, stdio: ['ignore', 'pipe', 'pipe'] });
    const finish = (code, signal) => {
      if (settled) return;
      settled = true;
      clearTimeout(timer);
      child.stdout.destroy();
      child.stderr.destroy();
      resolve(ok({ code, signal, stdout: out.text(), stderr: err.text(), timedOut, truncated: out.truncated() || err.truncated(), ms: Date.now() - started }));
    };
    const timer = setTimeout(() => {
      timedOut = true;
      killGroup(child);
      setTimeout(() => finish(null, 'SIGKILL'), GRACE_MS).unref();
    }, timeoutMs);
    child.stdout.on('data', c => out.push(c));
    child.stderr.on('data', c => err.push(c));
    child.on('error', e => {
      if (settled) return;
      settled = true;
      clearTimeout(timer);
      resolve(fail(`could not start ${exe}: ${e.message}`));
    });
    child.on('close', (code, signal) => finish(code, signal));
  });
}
