// The only deletion in the spike: a scored cell's score/ copies and node_modules folders, by exact path, under the runs root.
// Every delete goes through safe-rm: no link or junction at the target, no link above it that leaves the runs root, no shallow root.
import { assertRunsRoot, removeUnder } from './safe-rm.mjs';
import { isShedPath, shedTargets } from './spend.mjs';

/** Removes each existing shed target of one cell; returns what was removed and what was refused (with the reason). */
export function shedCell(runsRoot, cell, opts, rm = removeUnder) {
  const root = assertRunsRoot(runsRoot);
  const removed = [];
  const refused = [];
  for (const target of shedTargets(root, cell, opts)) {
    if (!isShedPath(target, root)) { refused.push({ target, reason: 'not a shed path' }); continue; }
    try {
      if (rm(target, root)) removed.push(target);
    } catch (e) {
      refused.push({ target, reason: e.message });
    }
  }
  return { removed, refused };
}
