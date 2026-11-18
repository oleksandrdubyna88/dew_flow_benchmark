// A restart of a freeze writer (F2.S2 (d)): the owned staging folders in a parent folder whose owner is gone are removed (`sweepStaleStaging`, the owner rules of `common.test-leftovers`: only a `gone` owner deletes), and one whose
// owner still runs, or cannot be judged, is named and left. A staging folder is `testsToDelete-<owner>-bench-stage-<id>` with its claim beside it.
import { existsSync, lstatSync, readdirSync } from 'node:fs';
import path from 'node:path';
import { sweepStaleStaging } from '../../lang-bench/lib/toolchain-stale.mjs';
import { owned } from './workspaces-owned.mjs';

const isStage = name => name.startsWith(owned.ownedPrefix('stage')) && !name.endsWith('.owner');

/** The state of a staging folder's owner: `live`, `gone` or `unknown`. */
const stateOf = dir => owned.ownerState(owned.readOwner(dir, 'folder'), undefined, { nameOwner: owned.classifyName(path.basename(dir)).owner, kernelMine: owned.isMine(dir) });

/** The staging folders left in `parent` after a sweep: `[{ dir, state }]`. */
export function stagingLeft(parent) {
  if (!existsSync(parent)) return [];
  return readdirSync(parent).filter(isStage).map(name => path.join(parent, name)).filter(dir => lstatSync(dir).isDirectory()).map(dir => ({ dir, state: stateOf(dir) }));
}

/** Remove the staging folders of dead owners in `parent` and name the ones left, with what is known of their owner. -> `{ removed: [dir], left: [{ dir, state }] }`. */
export function sweepAndName({ parent, log }) {
  const removed = sweepStaleStaging({ storeRoot: parent, owned, log });
  const left = stagingLeft(parent);
  for (const l of left) log(`left the staging folder ${l.dir}: its owner is ${l.state === 'live' ? 'still running' : 'not known to be gone'}`);
  return { removed, left };
}
