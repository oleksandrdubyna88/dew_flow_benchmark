// The clean copy of a quick-run session's tree for scoring: the agent's tree without the build output of the stack, every frozen file put back, and nothing that cannot be source.
//
// A daemon the agent ran from its tree can leave its own socket (or a named pipe) in it; `cpSync` throws on such an entry. A file that is neither a regular file, a directory nor a link cannot be source, so it is left out of the
// copy and named, and the score carries the names in a flag (`explore.nonRegularLeftOut`). Links are copied as links (verbatim), as in the olympiad's copy.
import { createHash } from 'node:crypto';
import { cpSync, lstatSync, readFileSync, statSync } from 'node:fs';
import path from 'node:path';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { excludedDirs } from '../../lang-bench/lib/frozen-set.mjs';
import { frozenChanges, pathKind, restoreFrozen } from './workspace.mjs';

/** The reason of a frozen file the BUILD changed (an MSBuild target of the agent's project can rewrite `run.sh` in the writable build box). */
export const DURING_BUILD = 'changed during the build';

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

/** After the build: every frozen file the build changed is put back, and named with the reason `DURING_BUILD`. The comparison is with the frozen bytes, file by file; the build's output folder is not frozen and is not looked at. -> `[{ path, state, reason }]`. */
function restoreAfterBuild({ files, root }) {
  const changed = frozenChanges({ files, work: root });
  for (const c of changed) restoreFrozen({ root, rel: c.path, file: files[c.path] });
  return changed.map(c => ({ ...c, reason: DURING_BUILD }));
}

/**
 * THE build step, the only way a quick tree is built (F1 final seam review): it runs `build({ tree, ...rest })`, then compares every frozen file with its frozen bytes and puts each changed one back, and returns the build's own answer
 * with the deviations found during the build in `duringBuild` (never absent). The build is the agent's own (an MSBuild target in `App.csproj`), so no caller gets a result without the restore having run. A build that did not run (a `fail`)
 * is passed on as it is; a tree that is not a directory after the build is an environment failure. -> `ok({ ...built, duringBuild: [{ path, state, reason }] })` | the build's `fail`.
 */
export async function buildAndRestore({ build, tree, files, ...rest }) {
  const built = await build({ tree, ...rest });
  if (!built.ok) return built;
  if (statSync(tree, { throwIfNoEntry: false })?.isDirectory() !== true) return fail(`the built tree ${tree} is not a directory`, { kind: 'environment' });
  return ok({ ...built.value, duringBuild: restoreAfterBuild({ files, root: tree }) });
}

/**
 * The changes to frozen files, told apart. A change to a template file, a runner file, the derived packages list or a generated file is judged by its bytes (`decide`, see `template-drift.mjs`): the harness's when the tree has the bytes the
 * file had when the series began, the agent's when it has any other, undecided where the old bytes cannot be read. Every other change is the agent's; without a history every change is the agent's, as it always was.
 * -> `{ agent, harness, undecided: [{ path, state, why }] }`.
 */
export function splitFrozenChanges(changes, { decide = () => 'agent' } = {}) {
  const verdicts = changes.map(c => [c, decide(c)]);
  const of = name => verdicts.filter(([, v]) => v === name).map(([c]) => c);
  return { agent: of('agent'), harness: of('harness'), undecided: verdicts.filter(([, v]) => typeof v === 'object').map(([c, v]) => ({ ...c, why: v.undecided })) };
}
