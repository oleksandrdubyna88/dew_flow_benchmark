// The stamp lock (K1, fix batch 7): two passing probes must not interleave their stamps. `inputs/.stamp.lock` holds `<pid> <start time>` — the start time is the one `owned.mjs` reads from `/proc/<pid>/stat` — and
//   create    is `link()` of a COMPLETE temporary file (`.stamp.lock.link-<pid>`): a reader never sees an empty lock, and EEXIST means held;
//   live      the holder's pid exists AND its start time is the one in the lock (a reused pid is stale); a holder that cannot be judged is live; a lock that cannot be parsed REFUSES, naming its path;
//   retire    of a stale lock is atomic: rename it to `.stamp.lock.retire-<my pid>`, read it, and only when its content is the stale content that was judged remove its strays and it and take the lock; when the
//             content differs (another probe's NEW lock was moved) it is linked back and the probe refuses as for a live holder; a vanished lock means someone else retired it: take once more, else refuse;
//   release   removes the lock only when its content is still this probe's own.
// Every failure is a value (`{ ok: false, error }`), never a throw: the caller turns it into a record that says exit 2.
import * as fs from 'node:fs';
import path from 'node:path';
import { processProbe } from '../../../../.agents/conventions/tools/lib/owned.mjs';

export const LOCK_NAME = '.stamp.lock';
const CONTENT = /^(\d+) (\d+|-)(\ncommitting)?$/;
const COMMITTING = '\ncommitting';

const parse = text => { const found = CONTENT.exec(String(text).trim()); return found === null ? null : { pid: Number(found[1]), start: found[2], committing: found[3] !== undefined }; };
const startOf = (probe, pid) => { const found = probe(pid).start; return found === undefined ? '-' : String(found); };
const fail = (lock, error, holder = null) => ({ ok: false, holder, path: lock, error });

/** `'live'` or `'stale'`: live when the pid exists and its start time is the one in the lock (or one of them cannot be read: it is then not judged stale); a pid that cannot be judged is live. */
function judge(held, probe) {
  const now = probe(held.pid);
  if (now.state === 'dead') return 'stale';
  if (now.state !== 'live') return 'live';
  return held.start === '-' || now.start === undefined || String(now.start) === held.start ? 'live' : 'stale';
}

const attempt = fn => { try { return { value: fn() }; } catch (error) { return { error }; } };
const describe = (lock, error) => `the stamp lock ${lock} could not be used (${error.code ?? ''} ${error.message})`.replace('( ', '(');

/**
 * Take the stamp lock: `{ ok: true, release(), retired, retiredHolder }` or `{ ok: false, holder, path, error }`. `retired` says (or is null) that a stale lock was retired. `io` and `probe` (`pid => { state, start }`)
 * are the test seams; `strays(holderPid)` removes the dead holder's stray files (K3) and returns the names.
 */
export function acquireLock({ inputsDir, io = fs, pid = process.pid, probe = processProbe, strays = () => [] }) {
  const lock = path.join(inputsDir, LOCK_NAME);
  const mine = `${pid} ${startOf(probe, pid)}`;
  const tmp = `${lock}.link-${pid}`;
  const release = () => {
    const read = attempt(() => io.readFileSync(lock, 'utf8'));
    if (read.error !== undefined) {
      if (read.error.code === 'ENOENT') return;
      throw read.error;
    }
    if (read.value === mine || read.value === `${mine}${COMMITTING}`) io.rmSync(lock, { force: true });
  };
  const take = () => {
    const wrote = attempt(() => io.writeFileSync(tmp, mine, { flag: 'wx' }));
    if (wrote.error !== undefined) return { error: wrote.error };
    const linked = attempt(() => io.linkSync(tmp, lock));
    attempt(() => io.rmSync(tmp, { force: true })); // L2: a temporary file that cannot be removed does not undo a link that succeeded: the lock IS held (the stray is retired later by its known name)
    if (linked.error === undefined) return { taken: true };
    return linked.error.code === 'EEXIST' ? { held: true } : { error: linked.error };
  };
  const holdsMine = () => {
    const read = attempt(() => io.readFileSync(lock, 'utf8'));
    return read.error === undefined && (read.value === mine || read.value === `${mine}${COMMITTING}`);
  };
  /** Mark the lock `committing` (a second line) through the same temporary file and a rename: a reader sees the old or the new lock, never a partial one. Throws when it cannot. */
  const markCommitting = () => {
    io.writeFileSync(tmp, `${mine}${COMMITTING}`, { flag: 'w' });
    try {
      io.renameSync(tmp, lock);
    } catch (error) {
      io.rmSync(tmp, { force: true });
      throw error;
    }
  };
  const taken = retired => ({ ok: true, release, retired, retiredHolder: null, retiredCommitting: false, holdsMine, markCommitting });
  const first = take();
  if (first.taken) return taken(null);
  if (first.error !== undefined) return fail(lock, describe(lock, first.error));
  return contend({ lock, io, probe, take, taken, strays, pid });
}

/** The lock is held by someone: judge it and either refuse or retire it. */
function contend({ lock, io, probe, take, taken, strays, pid }) {
  const read = attempt(() => io.readFileSync(lock, 'utf8'));
  if (read.error !== undefined) {
    if (read.error.code !== 'ENOENT') return fail(lock, describe(lock, read.error));
    const again = take(); // it vanished between our create and our read: someone released it
    return again.taken ? taken(null) : fail(lock, `the stamp lock ${lock} is held by another probe that just took it`);
  }
  const held = parse(read.value);
  if (held === null) return fail(lock, `the stamp lock ${lock} cannot be parsed (${JSON.stringify(read.value.slice(0, 40))}): remove it by hand if no probe runs`);
  if (judge(held, probe) === 'live') return fail(lock, `another probe holds the stamp lock (pid ${held.pid}) at ${lock}`, held.pid);
  return retire({ lock, io, take, taken, strays, pid, held, text: read.value });
}

/** Retire a stale lock atomically (see the header). */
function retire({ lock, io, take, taken, strays, pid, held, text }) {
  const moved = `${lock}.retire-${pid}`;
  const renamed = attempt(() => io.renameSync(lock, moved));
  if (renamed.error !== undefined) {
    if (renamed.error.code !== 'ENOENT') return fail(lock, describe(lock, renamed.error), held.pid);
    const again = take(); // someone else retired it first: take once more, else refuse
    return again.taken ? taken(`the stale stamp lock of pid ${held.pid} was retired by another probe`) : fail(lock, `another probe holds the stamp lock at ${lock}`);
  }
  const read = attempt(() => io.readFileSync(moved, 'utf8'));
  if (read.error !== undefined) return fail(lock, describe(lock, read.error), held.pid);
  if (read.value !== text) { // what was moved is another probe's NEW lock: put it back and refuse as for a live holder
    const back = attempt(() => io.linkSync(moved, lock));
    const other = parse(read.value);
    if (back.error !== undefined) return fail(lock, `another probe's stamp lock (pid ${other?.pid ?? 'unknown'}) was moved to ${moved} and could not be linked back (${back.error.code ?? back.error.message}): the file is KEPT, check it by hand`, other?.pid ?? null);
    io.rmSync(moved, { force: true });
    return fail(lock, `another probe holds the stamp lock (pid ${other?.pid ?? 'unknown'}) at ${lock}`, other?.pid ?? null);
  }
  const removed = strays(held.pid);
  io.rmSync(moved, { force: true });
  const got = take();
  if (got.error !== undefined) return fail(lock, describe(lock, got.error));
  if (!got.taken) return fail(lock, `another probe took the stamp lock at ${lock} while it was being retired`);
  return { ...taken(`retired the stale stamp lock of pid ${held.pid} and ${removed.length} stray file(s)`), retiredHolder: held.pid, retiredCommitting: held.committing };
}
