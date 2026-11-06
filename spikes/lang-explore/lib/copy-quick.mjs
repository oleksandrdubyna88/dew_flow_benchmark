// The clean copy of a quick-run session's tree for scoring: the agent's tree without the build output of the stack, every frozen file put back, and nothing that cannot be source.
//
// A daemon the agent ran from its tree can leave its own socket (or a named pipe) in it; `cpSync` throws on such an entry. A file that is neither a regular file, a directory nor a link cannot be source, so it is left out of the
// copy and named, and the score carries the names in a flag (`explore.nonRegularLeftOut`). Links are copied as links (verbatim), as in the olympiad's copy.
import { cpSync, lstatSync, statSync } from 'node:fs';
import path from 'node:path';
import { excludedDirs } from '../../minigit-bench/lib/score-deps.mjs';
import { frozenChanges, restoreFrozen } from './workspace.mjs';

const isKnownKind = st => st.isFile() || st.isDirectory() || st.isSymbolicLink();

const isSkippedDir = (p, skip) => skip.has(path.basename(p)) && statSync(p, { throwIfNoEntry: false })?.isDirectory() === true;

/** Keep an entry that is source (a file, a directory or a link); a non-regular one is listed in `left`; one that vanished is not kept. */
function keepEntry(p, source, left) {
  const st = lstatSync(p, { throwIfNoEntry: false });
  if (st === undefined) return false;
  if (!isKnownKind(st)) left.push(path.relative(source, p));
  return isKnownKind(st);
}

/** The `cpSync` filter: build-output directories out, non-regular entries out and listed in `left`, everything else in. */
const filterFor = ({ source, stack, left }) => {
  const skip = new Set(excludedDirs(stack));
  return p => p === source || (!isSkippedDir(p, skip) && keepEntry(p, source, left));
};

/** Copy `source` to `dest`, put every frozen file back. -> `{ changed: [{ path, state }], left: [relative path] }` (the frozen files the tree changed, and the non-regular entries left out). */
export function quickPristineCopy({ source, dest, stack, files }) {
  const left = [];
  cpSync(source, dest, { recursive: true, filter: filterFor({ source, stack, left }), verbatimSymlinks: true, preserveTimestamps: true });
  const changed = frozenChanges({ files, work: dest });
  for (const rel of changed.map(c => c.path)) restoreFrozen({ root: dest, rel, file: files[rel] });
  return { changed, left: [...left].sort() };
}

/**
 * The changes to frozen files, told apart. Under `runner/` the harness itself may have moved since the session began (the runner is re-pinned): when `driftPossible`, a change there is `harness`, not the agent's;
 * every other change is the agent's. Without drift every change is the agent's, as it always was. -> `{ agent, harness }`.
 */
export function splitFrozenChanges(changes, driftPossible) {
  const harness = driftPossible ? changes.filter(c => c.path.startsWith('runner/')) : [];
  return { agent: changes.filter(c => !harness.includes(c)), harness };
}
