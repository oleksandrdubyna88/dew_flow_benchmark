// E1.S5: an install that was killed leaves its staging folder in the store root, possibly already sealed read-only (which no plain delete removes).
// The next install collects them: this user's `testsToDelete-u<uid>-bench-stage-*` folders whose owner record (the claim beside the folder) says the owning process is gone.
// A live owner, a missing or unreadable claim, another user's folder and anything else are left exactly as they are: only "gone" deletes (`common.test-leftovers` § 3).
import { existsSync, lstatSync, readdirSync, rmSync } from 'node:fs';
import path from 'node:path';
import { unsealTree } from './toolchain-store.mjs';

/** A real directory (lstat: a symlink is refused, never followed) directly in `storeRoot`: the only thing an unseal and a recursive remove may touch. */
function isRealDirectoryIn(dir, storeRoot) {
  try {
    return path.dirname(dir) === path.resolve(storeRoot) && lstatSync(dir).isDirectory();
  } catch {
    return false;
  }
}

/**
 * `sweepStaleStaging({ storeRoot, owned, log })` -> the folders removed (absolute paths). `owned` is the conventions mount's owned-name helper bound to the `bench` token
 * (`ownedFor({ token: 'bench' })`); `log(line)` names each removal.
 */
export function sweepStaleStaging({ storeRoot, owned, log = () => {} }) {
  if (!existsSync(storeRoot)) return [];
  const prefix = owned.ownedPrefix('stage');
  const candidates = readdirSync(storeRoot).filter(name => name.startsWith(prefix) && !name.endsWith('.owner')).map(name => path.join(storeRoot, name));
  const removed = [];
  for (const dir of candidates) {
    if (!isRealDirectoryIn(dir, storeRoot)) continue;
    const state = owned.ownerState(owned.readOwner(dir, 'folder'), undefined, { nameOwner: owned.classifyName(path.basename(dir)).owner, kernelMine: owned.isMine(dir) });
    if (state !== 'gone') continue;
    unsealTree(dir);
    rmSync(dir, { recursive: true, force: true });
    rmSync(`${dir}.owner`, { force: true });
    log(`removed the staging folder ${dir}: an earlier install was interrupted and its process is gone`);
    removed.push(dir);
  }
  return removed;
}
