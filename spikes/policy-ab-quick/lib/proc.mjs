import { spawn } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';

const POSIX = process.platform !== 'win32';
const SIGNAL_NUMBERS = Object.freeze({ SIGHUP: 1, SIGINT: 2, SIGTERM: 15 });
const live = new Set();
let handlersInstalled = false;

/** Kill every detached child still running; idempotent, never throws (it runs from signal handlers and the exit hook). */
function killLive() {
  for (const pid of [...live]) {
    live.delete(pid);
    killTree(pid, { log: line => process.stderr.write(`proc: ${line}\n`) });
  }
}

/**
 * The first live child installs the handlers: a detached child is in its OWN process group, so the terminal's Ctrl-C (SIGINT to
 * the parent's group), a SIGTERM or a closed terminal (SIGHUP) would otherwise leave it running - a `claude -p` would keep
 * spending with no wall or cost timer. The `exit` hook covers a caller whose own handler calls process.exit first.
 */
function installHandlers() {
  if (handlersInstalled) return;
  handlersInstalled = true;
  for (const [signal, number] of Object.entries(SIGNAL_NUMBERS)) {
    process.on(signal, () => { killLive(); process.exit(128 + number); });
  }
  process.on('exit', killLive);
}

/**
 * The ONE way a spike starts a child it will kill by pid: exe + argv, never a shell, detached on POSIX (its own process group, so
 * `killTree` can end the tree) and registered, so the parent's death takes it along. The registration is dropped on `close`.
 */
export function spawnTracked(exe, args, { cwd, env } = {}) {
  const child = spawn(exe, args, { cwd, env, shell: false, windowsHide: true, detached: POSIX });
  if (child.pid) {
    live.add(child.pid);
    installHandlers();
    child.once('close', () => live.delete(child.pid));
  }
  return child;
}

/** Run exe+args (never a shell); resolves {code, stdout: Buffer, stderr: Buffer}. */
export function run(exe, args, { cwd, env, input, timeoutMs } = {}) {
  return new Promise((resolve, reject) => {
    const child = spawnTracked(exe, args, { cwd, env });
    const out = [];
    const err = [];
    const timer = timeoutMs ? setTimeout(() => killTree(child.pid, { log: line => process.stderr.write(`run: ${line}\n`) }), timeoutMs) : undefined;
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

/** `/proc/<pid>/stat` -> { pid, ppid, pgid }: the fields after the command name, which may itself hold spaces and parentheses. */
function statRow(procDir, name) {
  try {
    const stat = fs.readFileSync(`${procDir}/${name}/stat`, 'utf8');
    const [, ppid, pgid] = stat.slice(stat.lastIndexOf(')') + 2).split(' ').map(Number);
    return Number.isFinite(ppid) && Number.isFinite(pgid) ? [{ pid: Number(name), ppid, pgid }] : [];
  } catch {
    return []; // gone while we looked, or not ours
  }
}

/** Every process as { pid, ppid, pgid }; empty where there is no /proc. */
export function readProcTable(procDir = '/proc') {
  try {
    return fs.readdirSync(procDir).filter(n => /^\d+$/.test(n)).flatMap(n => statRow(procDir, n));
  } catch {
    return [];
  }
}

/** The descendants of `pid` in `table` (breadth first), each with its own process group: a `setsid` child leads a group of its own. */
export function descendantsOf(pid, table) {
  const found = [];
  for (let frontier = [pid]; frontier.length > 0;) {
    const kids = table.filter(row => frontier.includes(row.ppid) && row.pid !== pid && !found.includes(row));
    found.push(...kids);
    frontier = kids.map(k => k.pid);
  }
  return found;
}

/** What to signal, in order: the pid's group, the pid, then each descendant's group and pid. Never the caller's own group. */
function killTargets(pid, table) {
  const ownGroup = table.find(row => row.pid === process.pid)?.pgid;
  const groupOf = d => (d.pgid > 1 && d.pgid !== ownGroup ? [-d.pgid] : []);
  return [...new Set([-pid, pid, ...descendantsOf(pid, table).flatMap(d => [...groupOf(d), d.pid])])];
}

/**
 * Kill ONE process tree by the PID this script started - never by image name. Windows: `taskkill /T`. POSIX: the descendants are
 * read from /proc BEFORE anything is signalled (a killed parent hands its children to init and the chain is lost), then the pid's
 * process group, the pid and each descendant with its own group - a child that called setsid (the CLI's Bash-tool shell does) is
 * in neither the pid's group nor reachable by one signal. A pid that is not a group leader has no group: ESRCH is expected.
 * Called from timers, stream callbacks and signal handlers, so it NEVER throws: the first failure other than ESRCH is reported
 * once through `log`. The spawn whose pid arrives here must be `detached` on POSIX (`spawnTracked`), or the group is the caller's own.
 */
export function killTree(pid, { kill = process.kill.bind(process), log = () => {}, procs = readProcTable } = {}) {
  if (!pid) return;
  if (!POSIX) {
    spawn('taskkill', ['/PID', String(pid), '/T', '/F'], { windowsHide: true })
      .on('error', e => log(`could not run taskkill for pid ${pid}: ${e.message}`));
    return;
  }
  const failure = killTargets(pid, procs()).map(target => killFailure(kill, target)).find(e => e !== null);
  if (failure) log(`could not kill pid ${pid} or its process tree: ${failure.code ?? failure.name}: ${failure.message}`);
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
