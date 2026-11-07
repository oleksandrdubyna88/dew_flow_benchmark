// The pointer to the pass child that is running (F1.S2 review item 2). The pass child is started detached, in a process group of its own, so a driver that is killed (SIGKILL, the host restarting under it) leaves it running with
// its scope, its holder and its program. The driver writes `score/child.json` (pid, start time, the token of its answer file) when it spawns a child and clears it when the child is gone. A scorer that starts reads it first: a
// child that still runs (the same pid AND start time, so a reused pid is another process) is killed with its group and waited for BEFORE the first attempt, and an event says so. The pointer is the one rewritable file of the scoring
// (written whole to a temporary name and renamed): it is not a record, it says where a live process is.
import { existsSync, readFileSync, renameSync, rmSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { killTree } from '../../policy-ab-quick/lib/proc.mjs';
import { ownerAlive, processStartTime } from '../../minigit-bench/lib/session-state.mjs';
import { systemClock } from './clock.mjs';

export const CHILD_RECORD = 'child.json';
const fileOf = work => path.join(work, CHILD_RECORD);

/** The start time of a pid for the pointer, or null when it cannot be read (a pointer with no start time never names a live process). */
export const startTimeOf = pid => { try { return processStartTime(pid); } catch { return null; } };

/** Point at the child now running. Written whole to a temporary name and renamed. */
export function recordChild({ work, pid, startTime, token, now = systemClock }) {
  const tmp = `${fileOf(work)}.${process.pid}.tmp`;
  writeFileSync(tmp, `${JSON.stringify({ pid, startTime, token, at: now().toISOString() })}\n`);
  renameSync(tmp, fileOf(work));
}

/** The child is gone: drop the pointer. */
export const clearChild = work => rmSync(fileOf(work), { force: true });

function readChild(work) {
  if (!existsSync(fileOf(work))) return null;
  try {
    const j = JSON.parse(readFileSync(fileOf(work), 'utf8'));
    return Number.isInteger(j.pid) ? j : null;
  } catch { return null; }
}

const liveIn = (rec, isAlive) => rec.startTime !== null && rec.startTime !== undefined && isAlive(rec.pid, rec.startTime);
const sleep = ms => new Promise(resolve => { setTimeout(resolve, ms); });

async function goneWithin({ rec, isAlive, waitMs, pollMs }) {
  for (let waited = 0; waited < waitMs && liveIn(rec, isAlive); waited += pollMs) await sleep(pollMs);
  return !liveIn(rec, isAlive);
}

/**
 * Deal with the child a dead scorer may have left. -> `{ found: null }` (no pointer), `{ found: { pid, token, killed } }` (killed: it was running and is gone now), or `{ found, problem }` when it is still running after `waitMs`
 * (the pointer stays and nothing starts beside it). `isAlive(pid, startTime)` and `killGroup(pid)` are the seams.
 */
export async function reapOrphanChild({ work, isAlive = ownerAlive, killGroup = killTree, waitMs = 10_000, pollMs = 50 }) {
  const rec = readChild(work);
  if (rec === null) { clearChild(work); return { found: null }; }
  if (!liveIn(rec, isAlive)) { clearChild(work); return { found: { ...rec, killed: false } }; }
  killGroup(rec.pid);
  if (!(await goneWithin({ rec, isAlive, waitMs, pollMs }))) return { found: { ...rec, killed: false }, problem: `the pass child ${rec.pid} of a dead scorer is still running after ${waitMs} ms and was not started beside` };
  clearChild(work);
  return { found: { ...rec, killed: true } };
}
