import { spawn } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';

/** Run exe+args (never a shell); resolves {code, stdout: Buffer, stderr: Buffer}. */
export function run(exe, args, { cwd, env, input, timeoutMs } = {}) {
  return new Promise((resolve, reject) => {
    // detached: the child leads its own process group, so killTree can end its whole tree (a grandchild holds the pipes open).
    const child = spawn(exe, args, { cwd, env, shell: false, windowsHide: true, detached: process.platform !== 'win32' });
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

/** The first failure of `kill(target)` other than "no such process" (a target that is gone is the goal), or null. */
function killFailure(kill, target) {
  try {
    kill(target, 'SIGKILL');
    return null;
  } catch (e) {
    return e.code === 'ESRCH' ? null : e;
  }
}

/**
 * Kill ONE process tree by the PID this script started - never by image name. Windows: `taskkill /T`. POSIX: the pid's process
 * GROUP, then the pid itself (a pid that is not a group leader has no group, so the first signal is expected to find nothing).
 * Called from timers and stream callbacks, so it NEVER throws: a failure other than ESRCH is reported once through `log`.
 * The spawn whose pid arrives here must be `detached` on POSIX, or the group is the caller's own.
 */
export function killTree(pid, { kill = process.kill.bind(process), log = () => {} } = {}) {
  if (!pid) return;
  if (process.platform === 'win32') {
    spawn('taskkill', ['/PID', String(pid), '/T', '/F'], { windowsHide: true })
      .on('error', e => log(`could not run taskkill for pid ${pid}: ${e.message}`));
    return;
  }
  const failure = [-pid, pid].map(target => killFailure(kill, target)).find(e => e !== null);
  if (failure) log(`could not kill pid ${pid} or its process group: ${failure.code ?? failure.name}: ${failure.message}`);
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

/** Where a node distribution keeps npm's entry script: beside the exe (Windows) or under `<prefix>/lib` (POSIX). */
const npmCliCandidates = (execPath = process.execPath) => {
  const dir = path.dirname(execPath);
  return [path.join(dir, 'node_modules', 'npm', 'bin', 'npm-cli.js'), path.join(dir, '..', 'lib', 'node_modules', 'npm', 'bin', 'npm-cli.js')];
};

/**
 * npm without a shell: a `.cmd` cannot be spawned directly on current Node, so run npm's own entry script with the running node.
 * A node that ships no npm is an unexpected environment: it throws naming where it looked, and never falls back to a shell interpreter.
 */
export function npmInvocation(args, { exists = fs.existsSync } = {}) {
  const candidates = npmCliCandidates();
  const cli = candidates.find(c => exists(c));
  if (!cli) throw new Error(`npm-cli.js not found at ${candidates.join(' or ')}: run with a node that ships npm`);
  return { exe: process.execPath, args: [cli, ...args] };
}
