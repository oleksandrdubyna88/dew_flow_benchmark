import { spawn } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';

/** Run exe+args (never a shell); resolves {code, stdout: Buffer, stderr: Buffer}. */
export function run(exe, args, { cwd, env, input, timeoutMs } = {}) {
  return new Promise((resolve, reject) => {
    const child = spawn(exe, args, { cwd, env, shell: false, windowsHide: true });
    const out = [];
    const err = [];
    const timer = timeoutMs ? setTimeout(() => killTree(child.pid), timeoutMs) : undefined;
    child.stdout.on('data', c => out.push(c));
    child.stderr.on('data', c => err.push(c));
    child.on('error', reject);
    child.on('close', code => {
      clearTimeout(timer);
      resolve({ code, stdout: Buffer.concat(out), stderr: Buffer.concat(err) });
    });
    child.stdin.on('error', () => {});
    child.stdin.end(input ?? '');
  });
}

/** Run and throw on a non-zero exit, with the stderr tail in the message. */
export async function runOk(exe, args, opts) {
  const r = await run(exe, args, opts);
  if (r.code !== 0) throw new Error(`${exe} ${args.slice(0, 3).join(' ')} exited ${r.code}: ${r.stderr.toString('utf8').slice(-600)}`);
  return r;
}

/** Kill ONE process tree by the PID this script started — never by image name. */
export function killTree(pid) {
  if (!pid) return;
  if (process.platform === 'win32') spawn('taskkill', ['/PID', String(pid), '/T', '/F'], { windowsHide: true });
  else try { process.kill(pid, 'SIGKILL'); } catch { /* already gone */ }
}

/** Resolve an executable against a PATH string (Windows-aware). */
export function findOnPath(name, pathValue, exts = process.platform === 'win32' ? ['', '.exe', '.cmd'] : ['']) {
  for (const dir of (pathValue ?? '').split(path.delimiter).filter(Boolean)) {
    for (const ext of exts) {
      const candidate = path.join(dir, name + ext);
      if (fs.existsSync(candidate) && fs.statSync(candidate).isFile()) return candidate;
    }
  }
  return '';
}

/**
 * npm without a shell: a `.cmd` cannot be spawned directly on current Node, so run npm's own
 * entry script with the running node. Falls back to cmd.exe /c npm.cmd.
 */
export function npmInvocation(args) {
  const cli = path.join(path.dirname(process.execPath), 'node_modules', 'npm', 'bin', 'npm-cli.js');
  if (fs.existsSync(cli)) return { exe: process.execPath, args: [cli, ...args] };
  return { exe: 'cmd.exe', args: ['/d', '/s', '/c', 'npm.cmd', ...args] };
}
