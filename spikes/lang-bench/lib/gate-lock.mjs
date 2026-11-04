// E1.S4: the D40 gate lock (parent plan section 3). One file, `~/.cache/lang-v2/gate.lock` by default (the path is always an argument here), that holds the owner's record in the conventions' grammar
// (`<owner>:<host>@<os>:<pid>[:<start>[:<ns>]]`). While it exists, no other command that needs the box (the snapshot build, later the gates) may start. No module had this before: E1.S2a is not built.
//
// The atomic operation it rests on: `open(file, 'wx')` (O_CREAT | O_EXCL). Exactly one taker creates the file; every other one gets EEXIST and refuses at once. It never waits.
//   live or unknown owner   the lock is refused and left alone (an owner of another user, host or pid namespace, a pid that cannot be asked, or a live pid with another start time is `unknown`, never `gone`).
//   unknown owner           `ownerState` says `unknown` (another host or namespace, a pid that cannot be asked, a live pid with another start time): refused, saying the state is unknown and which file to remove by hand.
//   gone owner              only when the record is this user's, on this host and namespace, and its pid has no process (the conventions' `ownerState` says `gone`): the next taker renames the stale file to a unique sibling,
//                           compares the sibling's text with the text it judged, removes it only if they are the same (otherwise it puts the file back), says so, and tries once more. A refusal after that is final.
//   unreadable lock         an empty file, a text that is not one record, a file over 512 bytes, a link, a file of another user: refused and left in place, naming the file, for a person to look at.
//   folder                  the lock's folder must be a real directory of this user, not a link: otherwise refused, naming it.
//   release                 removes the file only while it still holds the text this taker wrote; a lock that still exists but cannot be read is said on stderr with the manual step.
// A failed write removes the lock only while the path still names the file this taker created (device and inode). Residual, stated here because a caller reads it here: (1) a taker that dies between the create and the write leaves an EMPTY lock, which is unreadable and so refused until a person removes it (the safe
// direction); (2) between the rename of a stale lock and its put-back a taker could find no lock and take it (the put-back then refuses, and the stale text is dropped: the lock that was judged is gone anyway); (3) the owner's
// liveness is a pid probe, not a heartbeat: a wedged but live owner holds the lock until a person ends it. Every failure is a value (`fail`), never a throw.
import { closeSync, fstatSync, linkSync, lstatSync, mkdirSync, openSync, renameSync, rmSync, writeSync } from 'node:fs';
import { randomBytes } from 'node:crypto';
import path from 'node:path';
import { formatOwner, ownerState, parseOwner, processProbe, readClaimText, runRecord } from '../../../.agents/conventions/tools/lib/owned.mjs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';

const sayOnStderr = line => process.stderr.write(`${line}\n`);

const lstatOrNull = file => {
  try { return lstatSync(file); } catch (error) { if (error.code === 'ENOENT') return null; throw error; }
};
const sameFile = (a, b) => a.dev === b.dev && a.ino === b.ino;

/** Remove the file at `file` only while it is the file `fd` created (same device and inode): a path another taker took since is not ours. */
function removeWhileCreatedBy(fd, file) {
  const now = lstatOrNull(file);
  if (now !== null && sameFile(fstatSync(fd), now)) rmSync(file, { force: true });
}

/** `{ created: true }`, `{ exists: true }` or `{ error }`: the one atomic step. A half-written lock is removed again, if it is still ours. `write` is a seam. */
function createExclusive(file, text, write) {
  let fd;
  try {
    fd = openSync(file, 'wx', 0o600);
  } catch (error) {
    return error.code === 'EEXIST' ? { exists: true } : { error };
  }
  try {
    write(fd, text);
    return { created: true };
  } catch (error) {
    removeWhileCreatedBy(fd, file);
    return { error };
  } finally {
    closeSync(fd);
  }
}

const unreadable = file => `gate lock ${file} is unreadable (not one owner record of this user); it is left in place: look at it, and remove it by hand only when no gate run is going`;
const unknownOwner = (file, text) => `gate lock ${file}: owner state unknown (${text.trim()}) — remove ${file} by hand if no gate run is going`;
const ownerOf = read => (typeof read === 'string' ? parseOwner(read) : { unreadable: true });

/** The verdict on a lock that was read: `{ refuse }` (held, unknown or unreadable) or `{ gone, text }`. */
function verdictFor(file, read, probe) {
  const owner = ownerOf(read);
  if (owner.unreadable === true) return { refuse: unreadable(file) };
  const state = ownerState(owner, probe);
  if (state === 'gone') return { gone: true, text: read };
  return { refuse: state === 'live' ? `gate lock held by ${read.trim()}` : unknownOwner(file, read) };
}

/** What the file in the way says: `{ vanished }` (it went since the create), `{ refuse }` or `{ gone, text }` (its owner is proven gone). */
function judge(file, probe) {
  const read = readClaimText(file);
  return read === null ? { vanished: true } : verdictFor(file, read, probe);
}

/**
 * Remove a stale lock through a unique sibling: the file is RENAMED away (one atomic step), its text compared with the one that was judged, and only then removed. A different text is a successor's lock that took the
 * path since: it is put back (a link, which never replaces a file) and `false` is answered. `true` when the way is clear.
 */
function removeWhileStill(file, judged, afterRename) {
  const sibling = `${file}.gone-${process.pid}-${randomBytes(4).toString('hex')}`;
  try {
    renameSync(file, sibling);
  } catch (error) {
    return error.code === 'ENOENT';
  }
  afterRename?.(file, sibling); // a seam: what happens between the rename and the compare
  const same = readClaimText(sibling) === judged;
  if (!same) putBack(sibling, file);
  rmSync(sibling, { force: true });
  return same;
}

/** Put a successor's lock back where it was (a link never replaces a file): a path a third taker has taken since stays theirs, and that is a plain `false` answer, not a throw. */
function putBack(sibling, file) {
  try {
    linkSync(sibling, file);
  } catch (error) {
    if (error.code !== 'EEXIST') throw error;
  }
}

const stillExistsUnreadable = now => now !== null && ownerOf(now).unreadable === true;

/** A lock that was already gone when the unlink came (ENOENT) is not news; any other failure is said, with the manual step. */
const sayRemoveFailure = (file, error, say) => (error.code === 'ENOENT' ? undefined : say(`could not remove the gate lock ${file} (${error.message}): remove it by hand when no gate run is going`));

function releaseWhileMine(file, text, say, rm) {
  try {
    const now = readClaimText(file);
    if (now === text) rm(file, { force: true });
    else if (stillExistsUnreadable(now)) say(`gate lock ${file} still exists but cannot be read, and it is not known to be ours: remove it by hand if no gate run is going`);
  } catch (error) {
    sayRemoveFailure(file, error, say);
  }
}

/** A stale lock of a gone owner: removed (when its text is still the one judged) and said; `null` when the way is clear, a `fail` otherwise. */
function reclaim({ file, verdict, say, afterRename }) {
  if (!removeWhileStill(file, verdict.text, afterRename)) return fail(`gate lock ${file} changed while its owner was judged; it is left in place`, { held: true });
  say(`removed a gate lock whose owner is gone: ${verdict.text.trim()}`);
  return null;
}

/** The file is in the way: `null` when it went or was a stale lock that is now removed (try again), else the refusal. A second look at an existing file is final. */
function clearTheWay({ file, probe, say, retried, afterRename }) {
  const verdict = judge(file, probe);
  if (verdict.refuse) return fail(verdict.refuse, { held: true });
  if (retried) return fail(`gate lock ${file} could not be taken after one retry`, { held: true });
  return verdict.gone ? reclaim({ file, verdict, say, afterRename }) : null;
}

const outcomeOf = (made, { file, text, say, rm }) => (made.created ? ok({ text, release: () => releaseWhileMine(file, text, say, rm) }) : fail(`gate lock ${file} cannot be created: ${made.error.message}`));

function attempt(args) {
  const made = createExclusive(args.file, args.text, args.write);
  if (made.exists !== true) return outcomeOf(made, args);
  return clearTheWay(args) ?? attempt({ ...args, retried: true });
}

/** The lock's folder is made (owner only) and must be a real directory of this user: a link or another user's folder would put the lock where this taker does not own the path. */
function folderRefusal(dir, uid) {
  mkdirSync(dir, { recursive: true, mode: 0o700 });
  const stat = lstatSync(dir);
  return stat.isDirectory() && !stat.isSymbolicLink() && stat.uid === uid ? null : fail(`gate lock folder ${dir} is not a real folder of this user (a link, a file, or another user's): refused`);
}

/** Take the lock at `file`. -> `ok({ release, text })` | `fail('gate lock held by <owner text>', { held: true })` (or the unknown / unreadable / changed / folder / cannot-create sentence). `probe`, `say`, `uid` and `io` (`writeSync`, `afterRename`, `rm`) are seams. */
export function takeGateLock({ file, probe = processProbe, say = sayOnStderr, uid = process.getuid(), io = {} }) {
  const refused = folderRefusal(path.dirname(file), uid);
  if (refused !== null) return refused;
  return attempt({ file, text: `${formatOwner(runRecord())}\n`, probe, say, retried: false, write: io.writeSync ?? writeSync, afterRename: io.afterRename, rm: io.rm ?? rmSync });
}
