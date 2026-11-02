// E1.S8b: the case's workspace. There is NO copy of the built tree per case (fix batch 1, K1): the box mounts a copy-on-write overlay over the read-only built tree (`box.mjs`), so each launch gets
// a fresh writable layer on a tmpfs inside the box, discarded at exit, and no case ever sees another's files; the built tree is never written. What stays on the host is one EMPTY placeholder folder
// per case under `<run>/views/{case}`: the runner starts the launcher in it (`--case-view`), the overlay is mounted at that very path, and it is the overlay's second source, so a scenario's `.scenario/`
// fixtures the runner writes into it before the launch show in the workspace. The control `empty-program` and the `certify` launch have placeholders too.
// DISK: a run holds no copy, so the budget is a few empty folders. The program's file writes live in the box's tmpfs and are CHARGED TO ITS CGROUP (`memory.peak` rises by what it writes), exactly like
// writes to /tmp: a program's file writes count against its memory limit.
import { chmodSync, lstatSync, mkdirSync, readdirSync, rmSync } from 'node:fs';
import path from 'node:path';
import { fail, ok } from '../../../minigit-bench/lib/result.mjs';

/** The case id of the empty-program control (the runner reserves it: no sample may take it). */
export const EMPTY_CASE = 'empty-program';
/** The case id of the launch that certifies the memory read before a run (the layer's own: no sample may take it either). */
export const CERTIFY_CASE = 'certify';
const RESERVED = Object.freeze([EMPTY_CASE, CERTIFY_CASE]);
const CASE_ID = /^[A-Za-z0-9_-]+$/;

const viewsRoot = runDir => path.join(runDir, 'views');
/** The placeholder of one case inside the run folder. */
export const viewDir = (runDir, caseId) => path.join(viewsRoot(runDir), caseId);
/** `--case-view`: `{case}` stands where the runner puts the case id. */
export const viewTemplate = runDir => viewDir(runDir, '{case}');

/** `chmod u+rwx` top-down, then remove: a folder or file whose mode a program made unusable (`chmod 000`) never makes a removal fail. Never follows a link. */
export function removeTree(target) {
  const stat = lstatSync(target, { throwIfNoEntry: false });
  if (stat === undefined) return;
  if (stat.isDirectory()) {
    chmodSync(target, 0o700);
    for (const name of readdirSync(target)) removeTree(path.join(target, name));
  }
  rmSync(target, { recursive: true, force: true, maxRetries: 3, retryDelay: 50 });
}

/**
 * `ok({ dirs })` after one empty placeholder per case id plus the reserved ones, or `fail(reason)`. A case id must be letters, digits, `_` and `-` (it reaches a path) and not a reserved one.
 * Whatever was made before a failure is left for `removeViews`.
 */
export function makeViews({ runDir, caseIds }) {
  const bad = caseIds.filter(id => !CASE_ID.test(id) || RESERVED.includes(id));
  if (bad.length > 0) return fail(`case id ${bad.map(id => JSON.stringify(id)).join(', ')} is not letters, digits, '_' and '-' (or is one of the reserved ${RESERVED.join(', ')})`);
  try {
    mkdirSync(viewsRoot(runDir), { recursive: true });
    for (const id of [...caseIds, ...RESERVED]) mkdirSync(viewDir(runDir, id));
  } catch (error) {
    return fail(`a case placeholder could not be made: ${error.code ?? error.message}`);
  }
  return ok({ dirs: [...caseIds, ...RESERVED].map(id => viewDir(runDir, id)) });
}

function removeOne(runDir, id) {
  const dir = viewDir(runDir, id);
  try {
    removeTree(dir);
    return null;
  } catch (error) {
    return { path: dir, reason: error.code ?? error.message };
  }
}

/** Remove every placeholder of the run: `{ removed: [case ids], left: [{ path, reason }] }`. One that cannot be removed stays and is named. Never throws. */
export function removeViews(runDir) {
  let ids = [];
  try {
    ids = readdirSync(viewsRoot(runDir));
  } catch {
    return { removed: [], left: [] };
  }
  const left = ids.map(id => removeOne(runDir, id)).filter(Boolean);
  const removed = ids.filter(id => !left.some(entry => entry.path === viewDir(runDir, id)));
  if (left.length === 0) removeOne(runDir, '');
  return { removed, left };
}
