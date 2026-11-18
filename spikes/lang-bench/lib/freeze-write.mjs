// The atomic outputs of the freeze tools (F2.S2 (d), "Atomic outputs, and what a restart does"). `hashes --write` and `record` write their file to an OWNED temporary name beside the destination's folder
// (`ownedFor`: `testsToDelete-<owner>-bench-<what>-<id>.tmp`, its claim `<name>.owner` beside it), fsync it, validate it, and only then publish it with one `rename` onto the name. A reader therefore never sees half
// a file. A restart first removes every owned temporary file whose owner is gone (the owner rules of `common.test-leftovers`: the name selects, the record and the kernel authorise, `gone` is "no process has that pid"),
// names one whose owner still runs or cannot be judged and leaves it, and then runs the step again from its inputs; the outputs are deterministic, so a re-run gives the same bytes.
import { closeSync, existsSync, fsyncSync, openSync, readdirSync, readFileSync, renameSync, rmSync, writeSync } from 'node:fs';
import path from 'node:path';
import { ownedFor } from '../../../.agents/conventions/tools/lib/owned.mjs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';

const owned = ownedFor({ token: 'bench' });
const SUFFIX = '.tmp';
const MAX_DRAWS = 3;

/** The sentence for one temporary file of the owner form: removed when its owner is gone, otherwise left and named with the reason. */
function judge({ dir, name, remove }) {
  const file = path.join(dir, name);
  const owner = owned.readOwner(file, 'folder');
  const state = owned.ownerState(owner, undefined, { nameOwner: owned.classifyName(name).owner, kernelMine: owned.isMine(file) });
  if (state !== 'gone') return `left ${name}: its owner is ${state === 'live' ? `still running (pid ${owner.pid})` : 'not known to be gone'}, so it is not removed`;
  remove(file);
  return `removed ${name}: its owner (pid ${owner.pid}) is gone`;
}

/** Removes a temporary file and its claim; one that cannot be removed is left with its claim and a warning in the sweep's notes. */
function removeOwned(file) {
  rmSync(file, { force: true });
  owned.removeClaim(file);
}

/** The notes of the restart's sweep of `dir`: every owned temporary file of this repository's token and this user judged on its owner's record. A claim whose file is gone is removed when its owner is gone. */
export function sweepStale(dir) {
  const pattern = owned.ownedPattern();
  const names = readdirSync(dir).filter(name => pattern.test(name));
  const files = names.filter(name => name.endsWith(SUFFIX)).map(name => judge({ dir, name, remove: removeOwned }));
  const claims = names.filter(name => name.endsWith(`${SUFFIX}.owner`) && !existsSync(path.join(dir, name.slice(0, -'.owner'.length))));
  const orphans = claims.map(name => judge({ dir, name: name.slice(0, -'.owner'.length), remove: () => owned.removeClaim(path.join(dir, name.slice(0, -'.owner'.length))) }));
  return [...files, ...orphans];
}

/** `bytes` written whole to the open descriptor: `writeSync` may write fewer bytes than asked, so it is called again from where it stopped until all are written (a call that writes nothing is an error, never a loop that does not end); then fsynced and closed. */
function writeAndSync(fd, bytes, ops) {
  try {
    let at = 0;
    while (at < bytes.length) {
      const written = ops.writeSync(fd, bytes, at, bytes.length - at);
      if (!(written > 0)) throw new Error('a write made no progress');
      at += written;
    }
    ops.fsyncSync(fd);
  } finally {
    ops.closeSync(fd);
  }
}

/** ok(the text read back) when the temporary file, READ BACK, holds exactly `bytes` (a write the system cut short without saying so is caught here, before the rename); a fail naming both sizes otherwise. */
function readBack({ tmp, bytes, ops }) {
  const held = ops.readFileSync(tmp);
  return held.equals(bytes) ? ok(held.toString('utf8')) : fail(`the temporary file read back holds ${held.length} bytes, not the ${bytes.length} written: nothing is published`);
}

/** The folder fsynced after a rename in it, so the new name (not only the file's bytes) survives a power loss. */
function syncFolder(dir, ops) {
  const fd = ops.openSync(dir, 'r');
  try {
    ops.fsyncSync(fd);
  } finally {
    ops.closeSync(fd);
  }
}

/** The claim and the name of a new temporary file in `dir`: the claim first (an exclusive create), a taken one draws a new id. */
function claimTemp({ dir, what }) {
  for (let draw = 0; draw < MAX_DRAWS; draw += 1) {
    const file = path.join(dir, `${owned.ownedName(what)}${SUFFIX}`);
    try {
      owned.writeOwner(file);
      return file;
    } catch (error) {
      if (error?.code !== 'EEXIST') throw error;
    }
  }
  throw new Error(`no free temporary name for ${what} in ${dir}`);
}

/** ok(true) once the open temporary file holds `bytes` (written whole, read back, validated), has been renamed onto `target` and its folder synced; a fail says why nothing was published. */
async function publish({ fd, tmp, bytes, ops, validate, target, dir }) {
  writeAndSync(fd, bytes, ops);
  const held = readBack({ tmp, bytes, ops });
  const valid = held.ok ? await validate(held.value) : held;
  if (!valid.ok) return fail(`the new ${path.basename(target)} was not published: ${valid.reason}`);
  ops.renameSync(tmp, target);
  syncFolder(dir, ops);
  owned.removeClaim(tmp);
  return ok(true);
}

/** What a run leaves behind: a file this run did not create (a squatter's) is never removed, only the claim this run wrote; a file it made is removed with its claim. */
function cleanup({ made, tmp }) {
  if (!made) return owned.removeClaim(tmp);
  return existsSync(tmp) ? removeOwned(tmp) : undefined;
}

/**
 * Publishes `text` as `target` through an owned temporary file in `dir` (the same file system as the target). `validate(text)` -> ok | fail(reason) runs on the text READ BACK from the finished temporary file before the rename; a refusal
 * removes the file. `io` replaces `openSync`, `writeSync`, `fsyncSync`, `closeSync`, `readFileSync`, `renameSync`, `rmSync` (tests plant a kill at the rename, or a short write). -> ok({ notes }) | fail(reason).
 */
export async function writeOutput({ dir, target, text, what, validate = () => ok(true), io = {} }) {
  const ops = { openSync, writeSync, fsyncSync, closeSync, readFileSync, renameSync, rmSync, ...io };
  const bytes = Buffer.from(text, 'utf8');
  const notes = sweepStale(dir);
  const tmp = claimTemp({ dir, what });
  const state = { made: false };
  try {
    const fd = ops.openSync(tmp, 'wx', 0o644);
    state.made = true;
    const published = await publish({ fd, tmp, bytes, ops, validate, target, dir });
    return published.ok ? ok({ notes }) : fail(published.reason, { notes });
  } catch (error) {
    return fail(`${target} could not be written (${error.code ?? error.message})`, { notes, env: true });
  } finally {
    cleanup({ made: state.made, tmp });
  }
}
