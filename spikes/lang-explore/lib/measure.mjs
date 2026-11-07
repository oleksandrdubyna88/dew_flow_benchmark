// The first measurement of what ten scoring passes cost (F1.S2, section 4): the real wall time and the real retained size of ten passes, and of the worst case of retries, on kept trees. It is a BOUNDED BOOTSTRAP step: no
// `growth.json` exists before it, so the disk rule has no number yet and the measurement runs under a conservative temporary guard of its own (`growth.mjs`: a free-disk floor before the first subject is copied, a write ceiling
// on the measurement folder checked after every attempt). It writes only inside its own folder, never deletes to fit, stops resumably (exit 3) at the ceiling, and writes `growth.json` LAST, through the validating writer, and reads
// it back. Counts, times and sizes are all that ever leave the private folder.
import { existsSync, lstatSync, mkdirSync, readdirSync, statfsSync, writeFileSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { systemClock } from './clock.mjs';
import { MOST_ATTEMPTS, loadGrowth, measureGuard, pairWorstCaseBytes, writeGrowth } from './growth.mjs';

/** The apparent size of everything under `dir` in bytes (what `du -sb` counts); 0 for a folder that is not there. */
export function dirBytes(dir) {
  if (!existsSync(dir)) return 0;
  const own = lstatSync(dir);
  if (!own.isDirectory()) return own.size;
  return own.size + readdirSync(dir).reduce((sum, name) => sum + dirBytes(path.join(dir, name)), 0);
}

/** The bytes a non-root user can still write on the file system of `dir`. */
export function freeBytesOf(dir, statfs = statfsSync) {
  const s = statfs(dir);
  return Number(s.bavail) * Number(s.bsize);
}

/** The ten passes' own time: the sum of the wall of every attempt of the scoring, build left out. */
const attemptsOf = verdict => verdict.passes.flatMap(p => p.attempts.map(a => ({ n: p.n, attempt: a.attempt, kind: a.kind, ms: a.ms })));
const wallOf = attempts => attempts.reduce((sum, a) => sum + a.ms, 0);

/** One subject's measured numbers from the driver's answer and the folder it wrote. */
function resultOf({ subject, answer, work, probe, free }) {
  const attempts = attemptsOf(answer.verdict);
  return {
    name: subject.name, stack: subject.stack, answer: answer.kind, verdict: answer.verdict.verdict, buildMs: answer.verdict.buildMs, attemptCount: attempts.length, attempts,
    tenPassesWallMs: wallOf(attempts), treeBytes: probe.treeBytes, retainedBytes: dirBytes(work), freeBefore: free.before, freeAfter: free.after,
  };
}

/** The guard after an attempt: the reason to stop, or null; and (once) the size of the built tree, which is gone when the scoring ends. */
function guardAfterAttempt({ work, probe, io }) {
  probe.treeBytes ??= dirBytes(path.join(work, 'tree'));
  const guard = measureGuard({ freeBytes: io.freeBytes(), folderBytes: io.folderBytes() });
  return guard.go ? null : guard.reason;
}

/**
 * One subject through the real driver, the guard checked after every attempt. `score(subject, { work, afterAttempt, severe })` is the driver call (`severe`: the first two attempts of every pass are made environment ones, through the same
 * code path). -> `ok(result)` | `fail(reason)` when the driver halted (the ceiling, or anything else resumable).
 */
export async function measureSubject({ folder, subject, score, io, severe = false }) {
  const work = path.join(folder, severe ? `${subject.name}-worst` : subject.name, 'score');
  const probe = { treeBytes: null };
  const before = io.freeBytes();
  const answer = await score(subject, { work, afterAttempt: () => guardAfterAttempt({ work, probe, io }), severe });
  if (!['scored', 'void'].includes(answer.kind)) return fail(`${subject.name}${severe ? ' (worst case)' : ''}: ${answer.reason}`);
  return ok(resultOf({ subject, answer, work, probe, free: { before, after: io.freeBytes() } }));
}

/** Keep one subject's result in the private folder, once (`wx`): the numbers a person reads. */
function keepResult({ folder, result, severe }) {
  const dir = path.join(folder, severe ? `${result.name}-worst` : result.name);
  mkdirSync(dir, { recursive: true });
  const file = path.join(dir, 'result.json');
  if (!existsSync(file)) writeFileSync(file, `${JSON.stringify(result, null, 2)}\n`, { flag: 'wx' });
  return result;
}

/** The growth record from the measured subjects and the worst case: the largest tree and retained size of any subject, the worst case never below the ordinary figure. */
export function growthFrom({ results, worst, host, measuredOn }) {
  const treeBytes = Math.max(...results.map(r => r.treeBytes));
  const retained = Math.max(...results.map(r => r.retainedBytes));
  const retainedWorst = Math.max(worst.retainedBytes, retained);
  return {
    measuredOn, host, sessionTreeBytes: treeBytes, retainedTenPassesBytes: retained, retainedTenPassesWorstBytes: retainedWorst,
    tenPassesWallMs: Object.fromEntries(results.map(r => [r.name, r.tenPassesWallMs])), mostAttempts: MOST_ATTEMPTS, pairWorstCaseBytes: pairWorstCaseBytes({ sessionTreeBytes: treeBytes, retainedTenPassesWorstBytes: retainedWorst }).value,
  };
}

/** The host in a few plain words (no machine name). */
export const hostWords = () => `${os.platform()} ${os.arch()}, ${os.cpus().length} CPUs`;

async function measureAll({ folder, subjects, score, io }) {
  const results = [];
  for (const subject of subjects) {
    const r = await measureSubject({ folder, subject, score, io });
    if (!r.ok) return r;
    results.push(keepResult({ folder, result: r.value, severe: false }));
    io.say(`${subject.name}: ${r.value.verdict}; ten passes ${r.value.tenPassesWallMs} ms in ${r.value.attemptCount} attempt(s); retained ${r.value.retainedBytes} bytes`);
  }
  return ok(results);
}

/** Every subject, then the worst case on the first. -> `ok({ results, worst })` | `fail`. */
async function measureEverything({ folder, subjects, score, io }) {
  const all = await measureAll({ folder, subjects, score, io });
  if (!all.ok) return all;
  const worst = await measureSubject({ folder, subject: subjects[0], score, io, severe: true });
  if (!worst.ok) return worst;
  keepResult({ folder, result: worst.value, severe: true });
  return ok({ results: all.value, worst: worst.value });
}

const sameRecord = (back, record) => back.ok && JSON.stringify(back.value) === JSON.stringify(record);

/** `growth.json`, written last through the validating writer and read back. -> the command's answer. */
function writeGrowthLast({ done, io }) {
  const record = growthFrom({ ...done, host: hostWords(), measuredOn: (io.now ?? systemClock)().toISOString().slice(0, 10) });
  const where = io.growthFile ? { file: io.growthFile } : {};
  const written = writeGrowth({ ...where, record });
  if (!written.ok) return { exit: 3, message: written.reason };
  const back = loadGrowth(where);
  return sameRecord(back, record) ? { exit: 0, message: 'growth.json was written and read back equal', results: done.results, growth: record } : { exit: 3, message: back.reason ?? 'growth.json did not read back equal' };
}

/**
 * The whole bootstrap: the free-disk floor first (nothing is copied below it), every subject, the worst case on the first subject, then `growth.json` (written last, read back, compared).
 * `io` = `{ freeBytes(), folderBytes(), say, now, growthFile }`. -> `{ exit, message, results?, growth? }`: 0 done, 3 a resumable halt (the ceiling, an environment halt), 4 refused before any copy.
 */
export async function runMeasurement({ folder, subjects, score, io }) {
  const start = measureGuard({ freeBytes: io.freeBytes(), folderBytes: 0 });
  if (!start.go) return { exit: 4, message: `the measurement does not start: ${start.reason}` };
  mkdirSync(folder, { recursive: true });
  const done = await measureEverything({ folder, subjects, score, io });
  return done.ok ? writeGrowthLast({ done: done.value, io }) : { exit: 3, message: `the measurement stopped, resumably, with every file kept: ${done.reason}` };
}
