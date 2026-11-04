// E1.S4: the D40 gate lock (parent plan section 3). One file, `~/.cache/lang-v2/gate.lock` by default (the path is always an argument here), that holds the owner's record in the conventions' grammar
// (`<owner>:<host>@<os>:<pid>[:<start>[:<ns>]]`). While it exists, no other command that needs the box (the snapshot build, later the gates) may start. No module had this before: E1.S2a is not built.
//
// The atomic operation it rests on: `open(file, 'wx')` (O_CREAT | O_EXCL). Exactly one taker creates the file; every other one gets EEXIST and refuses at once. It never waits.
//   live or unknown owner   the lock is refused and left alone (an owner of another user, host or pid namespace, a pid that cannot be asked, or a live pid with another start time is `unknown`, never `gone`).
//   gone owner              only when the record is this user's, on this host and namespace, and its pid has no process (the conventions' `ownerState` says `gone`): the next taker removes the lock only if its text is
//                           still the text it judged (a re-read), says so, and tries once more. A refusal after that is final.
//   unreadable lock         an empty file, a text that is not one record, a file over 512 bytes, a link, a file of another user: refused and left in place, naming the file, for a person to look at.
//   release                 removes the file only while it still holds the text this taker wrote.
// Residual, stated here because a caller reads it here: (1) a taker that dies between the create and the write leaves an EMPTY lock, which is unreadable and so refused until a person removes it (the safe
// direction); (2) between the re-read and the removal of a stale lock another taker could replace the file, and that takes a process starting in the same few microseconds as the first one; (3) the owner's
// liveness is a pid probe, not a heartbeat: a wedged but live owner holds the lock until a person ends it. Every failure is a value (`fail`), never a throw.
import { closeSync, mkdirSync, openSync, rmSync, writeSync } from 'node:fs';
import path from 'node:path';
import { formatOwner, ownerState, parseOwner, processProbe, readClaimText, runRecord } from '../../../.agents/conventions/tools/lib/owned.mjs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';

const sayOnStderr = line => process.stderr.write(`${line}\n`);

/** `{ created: true }`, `{ exists: true }` or `{ error }`: the one atomic step. A half-written lock is removed again. */
function createExclusive(file, text) {
  let fd;
  try {
    fd = openSync(file, 'wx', 0o600);
  } catch (error) {
    return error.code === 'EEXIST' ? { exists: true } : { error };
  }
  try {
    writeSync(fd, text);
    return { created: true };
  } catch (error) {
    rmSync(file, { force: true });
    return { error };
  } finally {
    closeSync(fd);
  }
}

const unreadable = file => `gate lock ${file} is unreadable (not one owner record of this user); it is left in place: look at it, and remove it by hand only when no gate run is going`;

const ownerOf = read => (typeof read === 'string' ? parseOwner(read) : { unreadable: true });

/** What the file in the way says: `{ vanished }` (it went since the create), `{ refuse }` (held or unreadable) or `{ gone, text }` (its owner is proven gone). */
function judge(file, probe) {
  const read = readClaimText(file);
  if (read === null) return { vanished: true };
  const owner = ownerOf(read);
  if (owner.unreadable === true) return { refuse: unreadable(file) };
  return ownerState(owner, probe) === 'gone' ? { gone: true, text: read } : { refuse: `gate lock held by ${read.trim()}` };
}

/** Remove the lock only while its text is still `judged`; `true` when it is gone afterwards. */
function removeWhileStill(file, judged) {
  const now = readClaimText(file);
  if (now === null) return true;
  if (now !== judged) return false;
  rmSync(file, { force: true });
  return true;
}

function releaseWhileMine(file, text) {
  try {
    if (readClaimText(file) === text) rmSync(file, { force: true });
  } catch (error) {
    sayOnStderr(`could not remove the gate lock ${file} (${error.message}): remove it by hand when no gate run is going`);
  }
}

/** A stale lock of a gone owner: removed (when its text is still the one judged) and said; `null` when the way is clear, a `fail` otherwise. */
function reclaim({ file, verdict, say }) {
  if (!removeWhileStill(file, verdict.text)) return fail(`gate lock ${file} changed while its owner was judged; it is left in place`, { held: true });
  say(`removed a gate lock whose owner is gone: ${verdict.text.trim()}`);
  return null;
}

/** The file is in the way: `null` when it went or was a stale lock that is now removed (try again), else the refusal. A second look at an existing file is final. */
function clearTheWay({ file, probe, say, retried }) {
  const verdict = judge(file, probe);
  if (verdict.refuse) return fail(verdict.refuse, { held: true });
  if (retried) return fail(`gate lock ${file} could not be taken after one retry`, { held: true });
  return verdict.gone ? reclaim({ file, verdict, say }) : null;
}

const outcomeOf = (made, { file, text }) => (made.created ? ok({ text, release: () => releaseWhileMine(file, text) }) : fail(`gate lock ${file} cannot be created: ${made.error.message}`));

function attempt(args) {
  const made = createExclusive(args.file, args.text);
  if (made.exists !== true) return outcomeOf(made, args);
  return clearTheWay(args) ?? attempt({ ...args, retried: true });
}

/** Take the lock at `file`. -> `ok({ release, text })` | `fail('gate lock held by <owner text>', { held: true })` (or the unreadable / changed / cannot-create sentence). `probe` and `say` are seams. */
export function takeGateLock({ file, probe = processProbe, say = sayOnStderr }) {
  mkdirSync(path.dirname(file), { recursive: true, mode: 0o700 });
  return attempt({ file, text: `${formatOwner(runRecord())}\n`, probe, say, retried: false });
}
