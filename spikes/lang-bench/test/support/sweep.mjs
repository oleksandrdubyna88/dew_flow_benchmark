// The suite's start-of-run sweep (common.test-leftovers §4, fix batch 4). The conventions' own `sweepLeftovers` sweeps the token of the mount's `owned-token.mjs`; this repository's token is bound with
// `ownedFor`, so the same two keys are applied here with the helper's own functions (`classifyName`, `readOwner`, `ownerState`, `isMine`, `removeQuietly`):
//   scope        the name matches `testsToDelete-<owner>-bench-…` for THIS caller's owner and the kernel owner of the folder is the caller;
//   abandonment  the folder's claim says `gone`: no process has that pid, in a process list that hides none.
// Everything else is kept: a live run, a young folder (younger than `minAgeMs`: the age keeps the sweep off a run between creation and claim, it never authorises), an unmarked folder, another token's.
// Never throws.
import { lstatSync, readdirSync } from 'node:fs';
import path from 'node:path';
import { tmpdir } from 'node:os';
import { thisOwnerToken } from '../../../../.agents/conventions/tools/lib/owned.mjs';

export const TEN_MINUTES_MS = 10 * 60_000;

function verdict(owned, dir, minAgeMs, now) {
  const stat = lstatSync(dir);
  if (!stat.isDirectory()) return 'not a folder';
  if (now - stat.mtimeMs < minAgeMs) return 'young';
  const state = owned.ownerState(owned.readOwner(dir, 'folder'), undefined, { nameOwner: owned.classifyName(path.basename(dir)).owner, kernelMine: owned.isMine(dir) });
  return state === 'gone' ? null : `kept: ${state}`;
}

/** `{ removed: [path], kept: [{ path, why }], errors: [text] }` for the folders of `owned`'s token in `tmp` that a killed run left. */
export function sweepOwnedLeftovers({ owned, tmp = tmpdir(), minAgeMs = TEN_MINUTES_MS, now = Date.now() }) {
  const report = { removed: [], kept: [], errors: [] };
  let names = [];
  try {
    names = readdirSync(tmp).filter(name => owned.ownedPattern(thisOwnerToken()).test(name));
  } catch (error) {
    report.errors.push(`${tmp}: ${error.code ?? error.message}`);
  }
  for (const name of names) {
    const dir = path.join(tmp, name);
    try {
      const why = verdict(owned, dir, minAgeMs, now);
      if (why === null) {
        owned.removeQuietly(dir);
        report.removed.push(dir);
      } else {
        report.kept.push({ path: dir, why });
      }
    } catch (error) {
      report.errors.push(`${dir}: ${error.code ?? error.message}`);
    }
  }
  return report;
}
