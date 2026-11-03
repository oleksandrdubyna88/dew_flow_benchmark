// The launch chain of the execution layer built from its parts, for the tests of one part at a time (E1.S8b): a holder, a view, the delegated scope's prefix, `nsenter`, the box.
// `chain(t)` starts the holder and returns what a test needs to start a program the way the layer does and to look at its scope; everything it made ends with the test, whatever
// state the test leaves (the program's `prog/` is killed, the scope holder's `holder/` is killed, the unit is stopped, the netns holder is ended).
import { spawn } from 'node:child_process';
import { existsSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { boxArgv } from '../../lib/exec-layer/box.mjs';
import { boxEnv } from '../../lib/exec-layer/env.mjs';
import { nsenterArgv, startHolder, stopHolder } from '../../lib/exec-layer/netns.mjs';
import { killCgroup, progCgroup, scopeCgroup, scopePrefix, stopUnit } from '../../lib/exec-layer/scope.mjs';
import { needs, sh, toolchains } from './exec-layer-host.mjs';
import { owned } from './owned.mjs';

export const MIB = 1024 * 1024;
export { SPAWN_ENV };
const SPAWN_ENV = { PATH: '/usr/sbin:/usr/bin:/bin', XDG_RUNTIME_DIR: `/run/user/${process.getuid()}` };

export const waitFor = async (condition, ms = 15_000) => {
  for (let waited = 0; waited < ms; waited += 25) {
    const found = await condition();
    if (found) return found;
    await new Promise(resolve => setTimeout(resolve, 25));
  }
  return null;
};

export const text = file => readFileSync(file, 'utf8');

/** The pids a scope's leaf lists, or null when the leaf is not there. */
export function pidsIn(dir) {
  try {
    return text(path.join(dir, 'cgroup.procs')).split('\n').filter(Boolean).map(Number);
  } catch {
    return null;
  }
}
export const commOf = pid => { try { return text(`/proc/${pid}/comm`).trim(); } catch { return null; } };

/**
 * One case's launch chain. `files` is `{ name: text }` written into the view; `holdSeconds` bounds the scope holder; `memoryMax` is `memory.max` of `prog/`; `tweak(prefix)` lets a test
 * build the WRONG mechanism from the right one (a prefix without `OOMPolicy=continue`, without the delegated entry).
 */
export async function chain(t, { files = {}, view = null, memoryMax = 256 * MIB, holdSeconds = 60, caseId = 'c1', tweak = prefix => prefix, boxTweak = argv => argv, extraBox = {}, stack } = {}) {
  const holder = await startHolder({ name: path.basename(owned.ownedDir(t, 'lhold')), boundSeconds: 300 });
  if (!holder.ok) throw new Error(holder.reason);
  t.after(() => stopHolder(holder.value));
  const viewDir = view ?? owned.ownedDir(t, 'view');
  for (const [name, text] of Object.entries(files)) writeFileSync(path.join(viewDir, name), text);
  const unitPrefix = path.basename(owned.ownedDir(t, 'lsc'));
  const out = owned.ownedDir(t, 'out');
  const unit = `${unitPrefix}-${caseId}`;
  const parts = { unit, scope: scopeCgroup(unit), prog: progCgroup(unit), holderDir: path.join(scopeCgroup(unit), 'holder'), out, view: viewDir, holder: holder.value, driver: nsenterArgv(holder.value) };
  t.after(async () => {
    try { killCgroup(parts.prog); killCgroup(parts.holderDir); } catch { /* already gone */ }
    await stopUnit(unit);
  });
  const launch = (program = ['bash', 'run.sh']) => [
    ...tweak(scopePrefix({ unit, outDir: out, memoryMaxBytes: memoryMax, holdSeconds })),
    ...nsenterArgv(holder.value),
    ...boxTweak(boxArgv({ view: viewDir, dirs: toolchains, env: boxEnv({ dirs: toolchains, stack, memoryMaxBytes: memoryMax }), prog: parts.prog, ...extraBox })), ...program,
  ];
  return { ...parts, launch, start: (program, options = {}) => start(t, launch(program), options) };
}

/** Start the launch argv as the runner does (its own session, no stdin), and wait for the scope's `prog/` to hold a process. The child ends with the test. */
export function start(t, argv, { stdio = 'ignore', env = SPAWN_ENV } = {}) {
  const child = spawn(argv[0], argv.slice(1), { stdio, detached: true, env });
  child.exited = new Promise(resolve => child.once('exit', (code, signal) => resolve({ code, signal })));
  t.after(() => { try { process.kill(-child.pid, 'SIGKILL'); } catch { /* gone */ } });
  return child;
}

/** The host pid of the process in `prog/` whose comm matches, or null. */
export const programPid = (prog, comm) => (pidsIn(prog) ?? []).find(pid => comm.test(commOf(pid) ?? '')) ?? null;

export { needs, sh, existsSync };

/** The refuted shape: the box without the own-cgroup view (what it was before OP-K was decided), and the box of a plain scope, which has no `prog/` to bind. */
export function withoutView(argv) {
  const ro = argv.findIndex((arg, i) => arg === '--ro-bind' && argv[i + 2] === '/sys/fs/cgroup');
  return argv.filter((arg, i) => arg !== '--unshare-cgroup' && !(i >= ro && i < ro + 3));
}

/** The first complete line of `stream` parsed as JSON: collected until its newline whatever the chunking; rejects when the stream ends first (a fragment is never parsed). */
export function readJsonLine(stream) {
  return new Promise((resolve, reject) => {
    let said = '';
    const onData = chunk => {
      said += chunk;
      if (said.includes('\n')) {
        stream.off('data', onData);
        resolve(JSON.parse(said.slice(0, said.indexOf('\n'))));
      }
    };
    stream.on('data', onData);
    stream.once('end', () => reject(new Error(`the stream ended with no complete line: ${JSON.stringify(said)}`)));
  });
}
