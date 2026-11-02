// E1.S8b: a fresh writable view of the built tree per case (consult B7). The program starts in its case's view and writes there; no case ever sees another's files and the built
// tree is never written. The layer makes the views OUTSIDE the timed window and names them to the runner with `--case-view <run>/views/{case}`; they are removed with the run (cleanup
// step 5) or, for a run that was killed, by the next run's startup sweep together with its run folder.
//
// DISK BUDGET: the views are all made before the runner starts, because the runner takes ONE template for the whole run and needs a case's view when that case starts. A run
// therefore holds `cases x treeBytes(built tree)` of views at its peak, and nothing for the `empty-program` control, which starts in an empty folder (its launch is timed; nothing is
// read from it). A 20 MiB built tree and 300 samples hold 6 GiB: put the run folder (`parent`) on a disk that has it, not on a RAM-backed `/tmp`. bwrap 0.11's
// `--overlay-src <built> --tmp-overlay` would make a view free of copying; it is an optimisation CP2 may take once T-L34 passes with it.
import { cpSync, lstatSync, mkdirSync, readdirSync, rmSync } from 'node:fs';
import path from 'node:path';
import { makeWritable } from '../../../minigit-bench/lib/cache-snapshot.mjs';
import { fail, ok } from '../../../minigit-bench/lib/result.mjs';

/** The case id of the empty-program control (the runner reserves it: no sample may take it). */
export const EMPTY_CASE = 'empty-program';
const CASE_ID = /^[A-Za-z0-9_-]+$/;

const viewsRoot = runDir => path.join(runDir, 'views');
/** The view of one case inside the run folder. */
export const viewDir = (runDir, caseId) => path.join(viewsRoot(runDir), caseId);
/** `--case-view`: `{case}` stands where the runner puts the case id. */
export const viewTemplate = runDir => viewDir(runDir, '{case}');

/** The bytes of the regular files of a tree (a link is not followed and counts nothing): one view's disk cost. */
export function treeBytes(dir) {
  const sizeOf = entry => {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) return treeBytes(full);
    return entry.isFile() ? lstatSync(full).size : 0;
  };
  return readdirSync(dir, { withFileTypes: true }).reduce((sum, entry) => sum + sizeOf(entry), 0);
}

function copyView(tree, dest) {
  cpSync(tree, dest, { recursive: true, verbatimSymlinks: true, errorOnExist: true, force: false });
  makeWritable(dest);
}

/**
 * `ok({ dirs })` after one copy of `tree` per case id plus the empty control view, or `fail(reason)`. A case id must be letters, digits, `_` and `-` (it reaches a path) and not the
 * control's. A copy that fails leaves what was made for `removeViews`; it is never half-trusted.
 */
export function makeViews({ tree, runDir, caseIds }) {
  const bad = caseIds.filter(id => !CASE_ID.test(id) || id === EMPTY_CASE);
  if (bad.length > 0) return fail(`case id ${bad.map(id => JSON.stringify(id)).join(', ')} is not letters, digits, '_' and '-' (or is the reserved ${EMPTY_CASE})`);
  try {
    mkdirSync(viewsRoot(runDir), { recursive: true });
    for (const id of caseIds) copyView(tree, viewDir(runDir, id));
    mkdirSync(viewDir(runDir, EMPTY_CASE));
  } catch (error) {
    return fail(`a case view could not be made: ${error.code ?? error.message}`);
  }
  return ok({ dirs: [...caseIds, EMPTY_CASE].map(id => viewDir(runDir, id)) });
}

function removeOne(runDir, id) {
  const dir = viewDir(runDir, id);
  try {
    makeWritable(dir);
    rmSync(dir, { recursive: true, force: true, maxRetries: 3, retryDelay: 50 });
    return null;
  } catch (error) {
    return { path: dir, reason: error.code ?? error.message };
  }
}

/** Remove every view of the run: `{ removed: [case ids], left: [{ path, reason }] }`. A view that cannot be removed stays and is named. Never throws. */
export function removeViews(runDir) {
  let ids = [];
  try {
    ids = readdirSync(viewsRoot(runDir));
  } catch {
    return { removed: [], left: [] };
  }
  const left = ids.map(id => removeOne(runDir, id)).filter(Boolean);
  const removed = ids.filter(id => !left.some(entry => entry.path === viewDir(runDir, id)));
  if (left.length === 0) rmSync(viewsRoot(runDir), { recursive: true, force: true });
  return { removed, left };
}
