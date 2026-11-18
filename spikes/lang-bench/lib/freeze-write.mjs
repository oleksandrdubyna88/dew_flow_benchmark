// The atomic outputs of the freeze tools (F2.S2 (d), "Atomic outputs, and what a restart does"). `hashes --write` and `record` write their file to an OWNED temporary name beside the destination's folder
// (`ownedFor`: `testsToDelete-<owner>-bench-<what>-<id>.tmp`, its claim `<name>.owner` beside it), fsync it, validate it, and only then publish it with one `rename` onto the name. A reader therefore never sees half
// a file. A restart first removes every owned temporary file whose owner is gone (the owner rules of `common.test-leftovers`: the name selects, the record and the kernel authorise, `gone` is "no process has that pid"),
// names one whose owner still runs or cannot be judged and leaves it, and then runs the step again from its inputs; the outputs are deterministic, so a re-run gives the same bytes.
import { closeSync, existsSync, fsyncSync, openSync, readdirSync, renameSync, rmSync, writeSync } from 'node:fs';
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

/** `text` written whole to the open descriptor, fsynced, and the descriptor closed. */
function writeAndSync(fd, text, ops) {
  try {
    ops.writeSync(fd, text);
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

/**
 * Publishes `text` as `target` through an owned temporary file in `dir` (the same file system as the target). `validate(tmpFile)` -> ok | fail(reason) runs on the finished temporary file before the rename; a refusal
 * removes the file. `io` replaces `openSync`, `writeSync`, `fsyncSync`, `closeSync`, `renameSync`, `rmSync` (tests plant a kill at the rename). -> ok({ notes }) | fail(reason).
 */
export async function writeOutput({ dir, target, text, what, validate = () => ok(true), io = {} }) {
  const ops = { openSync, writeSync, fsyncSync, closeSync, renameSync, rmSync, ...io };
  const notes = sweepStale(dir);
  const tmp = claimTemp({ dir, what });
  let made = false;
  try {
    const fd = ops.openSync(tmp, 'wx', 0o644);
    made = true;
    writeAndSync(fd, text, ops);
    const valid = await validate(tmp);
    if (!valid.ok) return fail(`the new ${path.basename(target)} was not published: ${valid.reason}`, { notes });
    ops.renameSync(tmp, target);
    owned.removeClaim(tmp);
    return ok({ notes });
  } catch (error) {
    return fail(`${target} could not be written (${error.code ?? error.message})`, { notes, env: true });
  } finally {
    // a file this run did not create (a squatter's) is never removed; only the claim this run wrote goes
    if (made && existsSync(tmp)) removeOwned(tmp);
    else if (!made) owned.removeClaim(tmp);
  }
}
