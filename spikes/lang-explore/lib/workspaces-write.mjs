// The writer of the twelve workspaces (F2.S2 (d), `explore.mjs workspaces --out`): the whole output folder is made in an owned staging folder beside the destination and published with ONE rename (`writeAtomically`), through
// `checkDestination`, so a destination inside any git worktree is refused and a restart finds either no output folder or the whole one. The staging folder carries the owner rules of `common.test-leftovers` (its claim is
// written first); a restart removes the staging folders whose owner is gone (`sweepStaleStaging`) and names one whose owner still runs. Beside the folders it writes `workspaces.json`, the manifest `workspaces-copy` reads.
import { mkdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { checkDestination, writeAtomically } from '../../lang-bench/lib/workspace-destination.mjs';
import { ok } from '../../minigit-bench/lib/result.mjs';
import { owned } from './workspaces-owned.mjs';
import { fillWorkspace } from './workspace.mjs';
import { sweepAndName } from './workspaces-stale.mjs';
import { treeHashOfFiles } from './workspaces-tree.mjs';

export const MANIFEST = 'workspaces.json';

/** The manifest of a build: the ref as given, the commit it named, the tasks and stacks, and each folder's tree hash. A pure value; its text is `JSON.stringify` of it, keys in this order. */
export const manifestOf = ({ ref, sha, built, tasks, stacks }) => ({ ref, commit: sha, tasks, stacks, trees: Object.fromEntries(Object.entries(built).map(([folder, files]) => [folder, treeHashOfFiles(files)])) });

/** The staging folder's name, claimed first: the claim sits beside it, the folder is made by `writeAtomically`. */
function claimedStage(out, record) {
  const stage = path.join(path.dirname(out), owned.ownedName('stage'));
  owned.writeOwner(stage);
  record.stage = stage;
  return stage;
}

/** Fill a staging folder: every workspace, then the manifest. `hooks.beforeRename` is the seam a test plants a kill at. */
function fillStage({ stage, built, manifest, hooks }) {
  for (const [folder, files] of Object.entries(built)) fillWorkspace({ files, work: path.join(stage, folder) });
  writeFileSync(path.join(stage, MANIFEST), `${JSON.stringify(manifest, null, 2)}\n`);
  hooks.beforeRename?.();
}

/** The staging folders of dead runs beside the destination, removed and named. */
function sweepBeside(out, log) {
  const parent = path.dirname(out);
  mkdirSync(parent, { recursive: true });
  return sweepAndName({ parent, log });
}

/**
 * Write the workspaces into `out` (absent or empty, outside every git worktree). -> `ok(out)` | `fail`. A failure removes this run's staging folder and its claim and leaves `out` as it was; a kill leaves them, and the next
 * run removes them.
 */
export function writeWorkspaces({ out, built, manifest, hooks = {}, log = () => {} }) {
  const checked = checkDestination(out);
  if (!checked.ok) return checked;
  sweepBeside(checked.value, log);
  const record = { stage: null };
  const written = writeAtomically(checked.value, stage => fillStage({ stage, built, manifest, hooks }), { stageOf: o => claimedStage(o, record) });
  if (record.stage !== null) owned.removeClaim(record.stage);
  return written.ok ? ok(checked.value) : written;
}
