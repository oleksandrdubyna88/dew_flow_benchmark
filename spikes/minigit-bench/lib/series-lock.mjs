// One runner per series at a time (E3b review N13). `run`, `resume` and `resume --settle-only` all write the run-map, the ledger and the session directories of one series; two of them at once would
// plan the same rid twice, charge a session twice or remove the directory the other is running in. So each takes `<series>/series.lock`, created O_EXCL (`wx`: it either creates the file or fails,
// whatever else is running) and holding its owner's pid and start time (field 22 of /proc/<pid>/stat: the pair is immune to pid reuse, D-21a). A lock whose owner is gone (a crashed or killed
// runner) is taken over; one whose owner is alive refuses the new run naming it; one nobody can read is left alone (it may be a runner in the middle of writing it).
// The takeover of a stale lock goes through a second O_EXCL file (`series.lock.takeover`), so two runs that both find the same stale lock cannot both remove it and both create a fresh one.
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

/** `{ pid, startTime }` of the lock's owner, or `null` when the file is absent or says nothing usable. */
function ownerOf(file) {
  let text;
  try { text = readFileSync(file, 'utf8'); } catch (e) { if (e.code === 'ENOENT') return { gone: true }; throw e; }
  try {
    const j = JSON.parse(text);
    return Number.isInteger(j.pid) && typeof j.startTime === 'string' ? { pid: j.pid, startTime: j.startTime, text } : null;
  } catch {
    return null;
  }
}

const sameLock = (a, b) => a.pid === b.pid && a.startTime === b.startTime;

function release(file, mine) {
  const now = ownerOf(file);
  if (now && !now.gone && sameLock(now, mine)) rmSync(file, { force: true });
}

/**
 * Take the lock of a series. `pid`, `startTime` and `isAlive` are for tests. -> `ok({ release })` | `fail(reason)` naming the owner of the lock that refused.
 */
export function acquireSeriesLock({ seriesDir, pid = process.pid, startTime = processStartTime(process.pid), isAlive = ownerAlive }) {
  const file = path.join(seriesDir, LOCK_FILE);
  const takeover = path.join(seriesDir, TAKEOVER_FILE);
  const mine = { pid, startTime };
  const body = `${JSON.stringify({ ...mine, at: new Date().toISOString() })}\n`;
  const busy = o => fail(`the series ${path.basename(seriesDir)} is in use by pid ${o.pid} (started ${o.startTime}): another run, resume or settle holds ${file}. One runner per series at a time; wait for it to end`);
  for (let attempt = 0; attempt < 3; attempt += 1) {
    if (createExclusive(file, body)) return ok({ release: () => release(file, mine) });
    const held = ownerOf(file);
    if (held === null) return fail(`${file} exists and is unreadable: it may be a runner that is writing it, so it is left alone. If no run is active, remove it by hand`);
    if (held.gone) continue; // released between our create and our read: try again
    if (isAlive(held.pid, held.startTime)) return busy(held);
    // its owner is gone: take it over, once, through the second exclusive file
    if (!createExclusive(takeover, body)) return fail(`${file} is stale (pid ${held.pid} is gone) and another run is taking it over: try again in a moment`);
    try {
      const again = ownerOf(file);
      if (again && !again.gone && again.text === held.text) rmSync(file, { force: true }); // still the same stale lock: nobody took it over before us
    } finally {
      rmSync(takeover, { force: true });
    }
  }
  return fail(`${file} could not be taken after three tries: another run is racing for it`);
}
