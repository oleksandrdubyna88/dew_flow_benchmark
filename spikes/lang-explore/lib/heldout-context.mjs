// The arguments every held-out operation takes, with their defaults in ONE place (bench PR 4a): `{ home, task, frozenFile, keyFile, backupFile, shareMount, manifestFile, now, isAlive, mountOf, unseal, unpackTo, fs, hooks, pid,
// startTime, retry }`. Every one is an argument with a default and never a global, so a test replaces the clock, the liveness question, `findmnt`, either decryption seam and the file system, and plants a fault where `hooks` names
// it (`afterLock`, `afterStale`, `afterMkdir`, `beforeRename`, `afterArchive`). `backupFile` and `shareMount` have no default (the operator names them); `keyFile` defaults to the one place the document allows.
import * as nodeFs from 'node:fs';
import path from 'node:path';
import { unpackTo } from '../../minigit-bench/lib/heldout-archive.mjs';
import { unseal } from '../../minigit-bench/lib/heldout-crypto.mjs';
import { acquireGuardedLock } from '../../minigit-bench/lib/series-lock.mjs';
import { ownerAlive, processStartTime } from '../../minigit-bench/lib/session-state.mjs';
import { systemClock } from './clock.mjs';
import { FROZEN_FILE } from './heldout-quick.mjs';
import { defaultKeyFile, mountOf } from './heldout-key.mjs';
import { INPUTS } from './paths.mjs';
import { taskPaths } from './tasks.mjs';

export const LOCK_NAME = '.clear.lock';
export const OPEN_STAMP = '.open';

/** `a` with every default filled and the task's folders worked out. The folders, the archive's name and the lock's name are not arguments: no caller can name another archive. */
export function ctxOf(given) {
  const a = Object.fromEntries(Object.entries(given).filter(([, v]) => v !== undefined)); // an option left out is its default, never `undefined`
  const heldoutDir = taskPaths(a.task, a.home).heldoutDir;
  return {
    pid: process.pid, startTime: processStartTime(process.pid), isAlive: ownerAlive, mountOf, unseal, unpackTo, fs: nodeFs, hooks: {}, now: systemClock, frozenFile: FROZEN_FILE, packagesFile: path.join(INPUTS, 'packages.json'), keyFile: defaultKeyFile(a.home), warn: line => process.stderr.write(`${line}\n`), ...a,
    heldoutDir, casesDir: path.join(heldoutDir, 'cases'), archiveFile: path.join(heldoutDir, 'suite.enc'), stampFile: path.join(heldoutDir, OPEN_STAMP), archiveRel: `${path.basename(heldoutDir)}/suite.enc`,
  };
}

const busyOf = ctx => o => ({ ok: false, reason: `the clear held-out folder of task ${ctx.task.id} is in use by pid ${o.pid} (started ${o.startTime}): another reader holds ${path.join(ctx.heldoutDir, LOCK_NAME)}; wait for it to end` });

/**
 * Hold the task's lock (`heldout-NN/.clear.lock`, with its takeover guard) while `fn(lock)` runs, and release it on every exit path. `lock.takeover` names the dead owner whose lock was taken. -> what `fn` returns | `fail(reason)`
 * naming the holder that refused.
 */
export async function withTaskLock(ctx, fn) {
  const lock = acquireGuardedLock({ dir: ctx.heldoutDir, name: LOCK_NAME, pid: ctx.pid, startTime: ctx.startTime, isAlive: ctx.isAlive, retry: ctx.retry, hooks: ctx.hooks, now: ctx.now, busy: busyOf(ctx) });
  if (!lock.ok) return lock;
  try {
    return await fn(lock.value);
  } finally {
    lock.value.release();
  }
}
