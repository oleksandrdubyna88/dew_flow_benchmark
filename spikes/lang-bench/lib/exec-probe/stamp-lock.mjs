// The stamp lock (K1, fix batch 7; D1, fix batch 9): two passing probes must not interleave their stamps. `inputs/.stamp.lock` holds `<pid> <start time>` — the start time is the one `owned.mjs` reads from
// `/proc/<pid>/stat` — and a second line `committing` once the holder is about to commit.
//   create    is `link()` of a COMPLETE temporary file (`.exec-probe-tmp/.stamp.lock.link-<pid>-<start>`): a reader never sees an empty lock, and EEXIST means held (exactly one probe wins);
//   live      the holder's pid exists AND its start time is the one in the lock: the probe refuses, naming the holder's pid and the lock path; a holder that cannot be judged is live;
//   stale     a lock whose pid is gone, or alive with another start time (pid reuse), and a lock that cannot be parsed, is NEVER removed or moved by a probe: the probe refuses, and the refusal says the exact manual step
//             (`rm <lock>`, when no probe is running). A stale lock that says `committing` also has `committing: true`, for the warning that the pins MAY be stamped without a record;
//   change    only the HOLDER ever changes the lock: `markCommitting` (a temporary file renamed over its OWN lock) and `release` (removes it only while its content is still its own). A check followed by such a
//             rename has no other writer to race. Contents are compared through `parse` (pid, start time), never as strings.
// Every failure is a value (`{ ok: false, error }`), never a throw: the caller turns it into a record that says exit 2.
import * as fs from 'node:fs';
import path from 'node:path';
import { processProbe } from '../../../../.agents/conventions/tools/lib/owned.mjs';
import { dropTmpFolder, ownId, tmpPath, writeTmp } from './tmp-files.mjs';

export const LOCK_NAME = '.stamp.lock';
const CONTENT = /^(\d+) (\d+|-)(\ncommitting)?$/;
const COMMITTING = 'committing';

/** `{ pid, start, committing }` of a lock's text, or null when it cannot be parsed. */
export const parse = text => { const found = CONTENT.exec(String(text).trim()); return found === null ? null : { pid: Number(found[1]), start: found[2], committing: found[3] !== undefined }; };
const startOf = (probe, pid) => { const found = probe(pid).start; return found === undefined ? '-' : String(found); };
const sameHolder = (a, b) => a !== null && b !== null && a.pid === b.pid && a.start === b.start;
const fail = (lock, error, extra = {}) => ({ ok: false, holder: null, path: lock, error, ...extra });

/** `'live'` or `'stale'`: live when the pid exists and its start time is the one in the lock (or one of them cannot be read: it is then not judged stale); a pid that cannot be judged is live. */
function judge(held, probe) {
  const now = probe(held.pid);
  if (now.state === 'dead') return 'stale';
  if (now.state !== 'live') return 'live';
  return held.start === '-' || now.start === undefined || String(now.start) === held.start ? 'live' : 'stale';
}

const attempt = fn => { try { return { value: fn() }; } catch (error) { return { error }; } };
const describe = (lock, error) => `the stamp lock ${lock} could not be used (${error.code ?? ''} ${error.message})`.replace('( ', '(');
const manualStep = lock => `If no probe is running, remove it: rm ${lock}`;

/**
 * Take the stamp lock: `{ ok: true, release(), holdsMine(), markCommitting() }` or `{ ok: false, holder, path, error }`; a stale or unparsable lock is a refusal that also has `stale: true` and `committing` (the
 * stale lock said it). `io` and `probe` (`pid => { state, start }`) are the test seams. Nothing here ever removes or moves a lock that is not this probe's own.
 */
export function acquireLock({ inputsDir, io = fs, pid = process.pid, probe = processProbe }) {
  const lock = path.join(inputsDir, LOCK_NAME);
  const own = { pid, start: startOf(probe, pid) };
  const mine = `${own.pid} ${own.start}`;
  const tmp = tmpPath(lock, 'link', ownId(probe, pid)); // D2: in `.exec-probe-tmp/` beside the lock
  const markTmp = tmpPath(lock, 'mark', ownId(probe, pid)); // N5: the committing mark has its own temporary kind, never the link temp
  const holdsMine = () => {
    const read = attempt(() => io.readFileSync(lock, 'utf8'));
    return read.error === undefined && sameHolder(parse(read.value), own);
  };
  const release = () => {
    try {
      const read = attempt(() => io.readFileSync(lock, 'utf8'));
      if (read.error !== undefined) {
        if (read.error.code === 'ENOENT') return;
        throw read.error;
      }
      if (sameHolder(parse(read.value), own)) io.rmSync(lock, { force: true });
    } finally {
      attempt(() => { io.rmSync(tmp, { force: true }); io.rmSync(markTmp, { force: true }); }); // N5: a link temp that could not be removed after the take goes now (the start sweep would retire it otherwise)
      dropTmpFolder(inputsDir, io);
    }
  };
  const take = () => {
    const wrote = attempt(() => writeTmp(tmp, mine, { flag: 'wx' }, io));
    if (wrote.error !== undefined) { dropTmpFolder(inputsDir, io); return { error: wrote.error }; }
    const linked = attempt(() => io.linkSync(tmp, lock));
    attempt(() => { io.rmSync(tmp, { force: true }); }); // L2: a temporary file that cannot be removed does not undo a link that succeeded: the lock IS held (the start sweep retires the stray by its name)
    dropTmpFolder(inputsDir, io);
    if (linked.error === undefined) return { taken: true };
    return linked.error.code === 'EEXIST' ? { held: true } : { error: linked.error };
  };
  /** Mark the lock `committing` (a second line) through its own temporary file, created with `wx` (a file left by an earlier failed mark is removed first), and a rename over the holder's OWN lock: a reader sees the old or the new lock, never a partial one, and nothing is written through a hard link to the lock. Throws when it cannot. */
  const markCommitting = () => {
    io.rmSync(markTmp, { force: true });
    writeTmp(markTmp, `${mine}\n${COMMITTING}`, { flag: 'wx' }, io);
    try {
      io.renameSync(markTmp, lock);
    } catch (error) {
      io.rmSync(markTmp, { force: true });
      throw error;
    } finally {
      dropTmpFolder(inputsDir, io);
    }
  };
  const held = { ok: true, release, holdsMine, markCommitting };
  const first = take();
  if (first.taken) return held;
  if (first.error !== undefined) return fail(lock, describe(lock, first.error));
  return refuse({ lock, io, probe, take, held });
}

/** The lock is held by someone: refuse (live, stale or unparsable), never retire. A lock that vanished between our create and our read was released: take once more. */
function refuse({ lock, io, probe, take, held }) {
  const read = attempt(() => io.readFileSync(lock, 'utf8'));
  if (read.error !== undefined) {
    if (read.error.code !== 'ENOENT') return fail(lock, describe(lock, read.error));
    const again = take();
    if (again.taken) return held;
    return fail(lock, again.error === undefined ? `the stamp lock ${lock} is held by another probe that just took it` : describe(lock, again.error)); // N4: an error on the retry is said as it is
  }
  const found = parse(read.value);
  if (found === null) return fail(lock, `the stamp lock ${lock} cannot be parsed (${JSON.stringify(read.value.slice(0, 40))}). ${manualStep(lock)}`, { stale: true, committing: false });
  if (judge(found, probe) === 'live') return fail(lock, `another probe holds the stamp lock (pid ${found.pid}) at ${lock}`, { holder: found.pid });
  return fail(lock, `a probe that was killed left the stamp lock ${lock} (pid ${found.pid}, start time ${found.start}, now gone). ${manualStep(lock)}`, { holder: found.pid, stale: true, committing: found.committing });
}
