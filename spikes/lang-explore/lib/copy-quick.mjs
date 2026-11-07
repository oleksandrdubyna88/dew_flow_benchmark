// The clean copy of a quick-run session's tree for scoring: the agent's tree without the build output of the stack, every frozen file put back, and nothing that cannot be source.
//
// A daemon the agent ran from its tree can leave its own socket (or a named pipe) in it; `cpSync` throws on such an entry. A file that is neither a regular file, a directory nor a link cannot be source, so it is left out of the
// copy and named, and the score carries the names in a flag (`explore.nonRegularLeftOut`). Links are copied as links (verbatim), as in the olympiad's copy.
import { createHash } from 'node:crypto';
import { cpSync, lstatSync, readFileSync, statSync } from 'node:fs';
import path from 'node:path';
import { excludedDirs } from '../../lang-bench/lib/frozen-set.mjs';
import { frozenChanges, pathKind, restoreFrozen } from './workspace.mjs';

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

const sha256Of = bytes => createHash('sha256').update(bytes).digest('hex');

/** The sha256 of each changed frozen file as the tree has it, before it is put back; null for a file that is gone or is not a regular file (a link is never read). */
function treeHashes({ changed, dest }) {
  return Object.fromEntries(changed.map(c => [c.path, pathKind(dest, c.path) === 'plain' ? sha256Of(readFileSync(path.join(dest, c.path))) : null]));
}

/** Copy `source` to `dest`, put every frozen file back. -> `{ changed: [{ path, state }], left: [relative path], treeSha: { path: sha256 | null } }` (the frozen files the tree changed with the bytes the tree had, and the non-regular entries left out). */
export function quickPristineCopy({ source, dest, stack, files }) {
  const left = [];
  cpSync(source, dest, { recursive: true, filter: filterFor({ source, stack, left }), verbatimSymlinks: true, preserveTimestamps: true });
  const changed = frozenChanges({ files, work: dest });
  const treeSha = treeHashes({ changed, dest });
  for (const rel of changed.map(c => c.path)) restoreFrozen({ root: dest, rel, file: files[rel] });
  return { changed, left: [...left].sort(), treeSha };
}

/**
 * The changes to frozen files, told apart. Under `runner/` the harness itself may have moved since the session began (the runner is re-pinned): when `runnerDrift`, a change there is `harness`, not the agent's. The template files and
 * the derived packages list are judged by their bytes (`decide`, see `template-drift.mjs`). Every other change is the agent's; without drift every change is the agent's, as it always was.
 * -> `{ agent, harness, undecided: [{ path, state, why }] }`.
 */
export function splitFrozenChanges(changes, { runnerDrift = false, decide = () => 'agent' } = {}) {
  const verdicts = changes.map(c => [c, runnerDrift && c.path.startsWith('runner/') ? 'harness' : decide(c)]);
  const of = name => verdicts.filter(([, v]) => v === name).map(([c]) => c);
  return { agent: of('agent'), harness: of('harness'), undecided: verdicts.filter(([, v]) => typeof v === 'object').map(([c, v]) => ({ ...c, why: v.undecided })) };
}
