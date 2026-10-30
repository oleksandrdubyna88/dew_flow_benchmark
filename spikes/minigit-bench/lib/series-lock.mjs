// One runner per series at a time (E3b review N13). `run`, `resume` and `resume --settle-only` all write the run-map, the ledger and the session directories of one series; two of them at once would
// plan the same rid twice, charge a session twice or remove the directory the other is running in. So each takes `<series>/series.lock`, created O_EXCL (`wx`: it either creates the file or fails,
// whatever else is running) and holding its owner's pid and start time (field 22 of /proc/<pid>/stat: the pair is immune to pid reuse, D-21a). A lock whose owner is gone (a crashed or killed
// runner) is taken over; one whose owner is alive refuses the new run naming it; one nobody can read is left alone (it may be a runner in the middle of writing it).
// Taking the lock is ONE critical section (review coai-2): every acquirer holds a second O_EXCL file, `series.lock.takeover`, stamped with its own (pid, start time), while it looks at the lock,
// removes a stale one and creates its own, and drops the marker only if the marker still names it. So two runs that find the same stale lock cannot both remove it, a fresh creator cannot slip in
// between the removal and the creation of the one that took over, and a marker another owner created is never removed by anybody else: a contender waits a little for a live holder of the marker,
// and refuses naming a marker whose owner is gone (a taker killed in the middle: the operator removes that one file).
import { closeSync, openSync, readFileSync, rmSync, writeSync } from 'node:fs';
import path from 'node:path';
import { fail, ok } from './result.mjs';
import { ownerAlive, processStartTime } from './session-state.mjs';

export const LOCK_FILE = 'series.lock';
const TAKEOVER_FILE = `${LOCK_FILE}.takeover`;

/** Create `file` exclusively with `text`. -> `true`, or `false` when it already exists. */
function createExclusive(file, text) {
  let fd;
  try {
    fd = openSync(file, 'wx', 0o600);
  } catch (e) {
    if (e.code === 'EEXIST') return false;
    throw e;
  }
  try { writeSync(fd, text); } finally { closeSync(fd); }
  return true;
}

/** The text of `file`, or `null` when it is absent. */
function readText(file) {
  try {
    return readFileSync(file, 'utf8');
  } catch (e) {
    if (e.code === 'ENOENT') return null;
    throw e;
  }
}

/** `{ pid, startTime, text }` of an owner stamp, or `null` when the text says nothing usable. */
function parseOwner(text) {
  try {
    const j = JSON.parse(text);
    return Number.isInteger(j.pid) && typeof j.startTime === 'string' ? { pid: j.pid, startTime: j.startTime, text } : null;
  } catch {
    return null;
  }
}

/** `{ pid, startTime, text }` of the owner stamped in `file`, `{ gone: true }` when the file is absent, `null` when it says nothing usable. */
function ownerOf(file) {
  const text = readText(file);
  return text === null ? { gone: true } : parseOwner(text);
}

const sameLock = (a, b) => a.pid === b.pid && a.startTime === b.startTime;

/** Remove `file` only when it still names `mine` (a release, and the drop of the takeover marker). */
function removeIfMine(file, mine) {
  const now = ownerOf(file);
  if (now && !now.gone && sameLock(now, mine)) rmSync(file, { force: true });
}

/** Remove the stale lock `held` only when the file still holds exactly that text: a lock that was replaced meanwhile is not ours to remove. */
function removeIfSame(file, held) {
  const again = ownerOf(file);
  if (again && !again.gone && again.text === held.text) rmSync(file, { force: true });
}

const sleepMs = ms => Atomics.wait(new Int32Array(new SharedArrayBuffer(4)), 0, 0, ms);
const DEFAULT_RETRY = Object.freeze({ tries: 100, sleepMs: 10 });

/** The owner stamped in `marker` when that process is gone, else `null`. */
function goneOwner(marker, isAlive) {
  const held = ownerOf(marker);
  return held && !held.gone && !isAlive(held.pid, held.startTime) ? held : null;
}

/** Hold the takeover marker, waiting a little for a live holder. -> `ok(null)` | `fail(reason)`. */
function enterMarker({ file, marker, body, isAlive, retry }) {
  for (let n = 0; n < retry.tries; n += 1) {
    if (createExclusive(marker, body)) return ok(null);
    const held = goneOwner(marker, isAlive);
    if (held) return fail(`${marker} was made by pid ${held.pid}, which is gone (a run killed while it took ${file}): it is not removed by another run. If no run is active, remove that file by hand`);
    sleepMs(retry.sleepMs);
  }
  return fail(`${file} is stale or free and another run is taking it over (it holds ${marker}): try again in a moment`);
}

/** What a lock that exists says: a `fail` (a live owner, an unreadable file), or `null` (it was released, or it was stale and is removed): the caller tries to create it again. */
function judgeLock({ file, isAlive, hooks, busy }) {
  const held = ownerOf(file);
  if (held === null) return fail(`${file} exists and is unreadable: it may be a runner that is writing it, so it is left alone. If no run is active, remove it by hand`);
  if (held.gone) return null; // released between our create and our read
  if (isAlive(held.pid, held.startTime)) return busy(held);
  hooks.afterStale?.(held);
  removeIfSame(file, held);
  return null;
}

/** The lock, under the marker: create it, or judge the one that is there and go round again (it is gone, or stale and removed). */
function takeLock({ file, body, mine, isAlive, hooks, busy }) {
  for (let attempt = 0; attempt < 4; attempt += 1) {
    if (createExclusive(file, body)) return ok({ release: () => removeIfMine(file, mine) });
    const judged = judgeLock({ file, isAlive, hooks, busy });
    if (judged) return judged;
  }
  return fail(`${file} could not be taken after four tries: the lock keeps changing hands`);
}

/**
 * Take the lock of a series. `pid`, `startTime`, `isAlive`, `retry` (`{ tries, sleepMs }`: the wait for the takeover marker) and `hooks` (`afterStale`: called, with the marker held, once a stale lock is
 * found) are for tests. -> `ok({ release })` | `fail(reason)` naming the owner of the lock that refused.
 */
export function acquireSeriesLock({ seriesDir, pid = process.pid, startTime = processStartTime(process.pid), isAlive = ownerAlive, retry = DEFAULT_RETRY, hooks = {} }) {
  const file = path.join(seriesDir, LOCK_FILE);
  const marker = path.join(seriesDir, TAKEOVER_FILE);
  const mine = { pid, startTime };
  const body = `${JSON.stringify({ ...mine, at: new Date().toISOString() })}\n`;
  const busy = o => fail(`the series ${path.basename(seriesDir)} is in use by pid ${o.pid} (started ${o.startTime}): another run, resume or settle holds ${file}. One runner per series at a time; wait for it to end`);
  const entered = enterMarker({ file, marker, body, isAlive, retry });
  if (!entered.ok) return entered;
  try {
    return takeLock({ file, body, mine, isAlive, hooks, busy });
  } finally {
    removeIfMine(marker, mine);
  }
}
