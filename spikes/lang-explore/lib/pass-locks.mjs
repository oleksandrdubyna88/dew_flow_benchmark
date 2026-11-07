// The two locks around a session's scoring (F1.S2, section 2.8). The SERIES lock is the one `run` and `rescore` take (`minigit-bench/lib/series-lock.mjs`): the scoring driver proves it is held by this very process and never
// takes it. The SCORER lock `score/scorer.lock` is the driver's own: created `wx` with the same stamp (pid and start time, immune to pid reuse), removed by its owner, taken over only when its owner is dead, and left
// alone (with a refusal naming it) when nobody can read it, because it may be a scorer in the middle of writing it.
import { closeSync, openSync, readFileSync, rmSync, writeSync } from 'node:fs';
import path from 'node:path';
import { LOCK_FILE } from '../../minigit-bench/lib/series-lock.mjs';
import { systemClock } from './clock.mjs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { ownerAlive, processStartTime } from '../../minigit-bench/lib/session-state.mjs';

export const SCORER_LOCK = 'scorer.lock';

const readText = file => { try { return readFileSync(file, 'utf8'); } catch (e) { if (e.code === 'ENOENT') return null; throw e; } };

/** `{ pid, startTime }` of a stamp text; `'unreadable'` when it says nothing usable. */
function parseStamp(text) {
  try {
    const j = JSON.parse(text);
    return Number.isInteger(j.pid) && typeof j.startTime === 'string' ? { pid: j.pid, startTime: j.startTime } : 'unreadable';
  } catch { return 'unreadable'; }
}

/** `{ pid, startTime }` stamped in `file`; `null` when the file is absent; `'unreadable'` when it says nothing usable. */
const stampOf = file => { const text = readText(file); return text === null ? null : parseStamp(text); };

const holder = held => `held by pid ${held.pid} (started ${held.startTime})`;
const ownerProblems = Object.freeze([
  [held => held === null, file => `${file} is absent: scoring runs under the series lock that \`run\` and \`rescore\` take, and takes none itself`],
  [held => held === 'unreadable', file => `${file} is unreadable: scoring runs only under a series lock this process holds`],
]);

/** Why this process does not hold the series lock of `seriesDir` (a sentence naming `series.lock` and its holder), or null when it does. */
export function seriesLockProblem({ seriesDir, pid = process.pid, startTime = processStartTime(process.pid) }) {
  const file = path.join(seriesDir, LOCK_FILE);
  const held = stampOf(file);
  const known = ownerProblems.find(([is]) => is(held));
  if (known) return known[1](file);
  return held.pid === pid && held.startTime === startTime ? null : `${file} is ${holder(held)}, not by this process (pid ${pid}): scoring runs only under the series lock this process holds`;
}

/** Does this process hold `<series>/series.lock`? */
export const lockHeldByMe = a => seriesLockProblem(a) === null;

function createExclusive(file, text) {
  let fd;
  try { fd = openSync(file, 'wx', 0o600); } catch (e) { if (e.code === 'EEXIST') return false; throw e; }
  try { writeSync(fd, text); } finally { closeSync(fd); }
  return true;
}

const busy = (file, why) => fail(`scorer-busy: ${file} ${why}: one scorer per session`);

/** What the lock that is there says: a refusal, or `{ takeover }` (its dead owner, removed here) so the caller creates it again. */
function judged({ file, held, isAlive }) {
  if (held === 'unreadable') return busy(file, 'is unreadable (it may be a scorer that is writing it; if no scorer is active, remove it)');
  if (held !== null && isAlive(held.pid, held.startTime)) return busy(file, `is ${holder(held)}`);
  rmSync(file, { force: true });
  return ok({ takeover: held });
}

const releaseOf = ({ file, pid }) => () => { if (stampOf(file)?.pid === pid) rmSync(file, { force: true }); };

/** One try at the lock: created, or a refusal, or (the dead owner's lock removed) go round again while tries are left. */
function tryLock(a) {
  if (createExclusive(a.file, a.body)) return ok({ takeover: a.takeover, release: releaseOf(a) });
  const next = judged({ file: a.file, held: stampOf(a.file), isAlive: a.isAlive });
  if (!next.ok) return next;
  return a.tries <= 1 ? busy(a.file, 'keeps changing hands') : tryLock({ ...a, takeover: next.value.takeover ?? a.takeover, tries: a.tries - 1 });
}

/** `score/scorer.lock`, created `wx`. -> `ok({ release, takeover })` (`takeover`: the dead owner whose lock was removed, else null) | `fail('scorer-busy: ...')`. `isAlive(pid, startTime)` is the seam. */
export function takeScorerLock({ work, pid = process.pid, startTime = processStartTime(process.pid), isAlive = ownerAlive, now = systemClock }) {
  const file = path.join(work, SCORER_LOCK);
  const body = `${JSON.stringify({ pid, startTime, at: now().toISOString() })}\n`;
  return tryLock({ file, body, pid, isAlive, takeover: null, tries: 3 });
}
