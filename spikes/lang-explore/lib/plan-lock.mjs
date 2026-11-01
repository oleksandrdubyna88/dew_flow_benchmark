// One lock for the whole olympiad plan (review F4, coai finding 3): a lock file in the explore root, taken around the pause check AND the pair launch, so two runs on different series of the plan cannot both read the same
// spend and both pass the $60 check. It is v1's series lock (exclusive create, owner pid and start time, stale owners taken over) over the explore root. Task 1's own series is not under it.
import { fail } from '../../minigit-bench/lib/result.mjs';
import { acquireSeriesLock, LOCK_FILE } from '../../minigit-bench/lib/series-lock.mjs';
import { mkdirSync } from 'node:fs';
import path from 'node:path';
import { exploreRoot } from './paths.mjs';

/** `ok({ release })` | `fail` naming the lock and who holds it. */
export function acquirePlanLock({ home }) {
  const root = exploreRoot(home);
  mkdirSync(root, { recursive: true, mode: 0o700 });
  const got = acquireSeriesLock({ seriesDir: root });
  return got.ok ? got : fail(`the plan lock ${path.join(root, LOCK_FILE)} is held (${got.reason}): it covers the pause check and the pair launch, so two runs of the plan never both pass the check; wait for the other run to end`);
}
