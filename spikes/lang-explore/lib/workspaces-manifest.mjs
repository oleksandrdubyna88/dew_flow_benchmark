// The manifest `workspaces` writes beside the twelve folders (`workspaces.json`), and the reading of it that `workspaces-copy` does: the commit the workspaces were made from, and the tree hash of each folder. The copy refuses a
// worktree whose HEAD is not that commit and a source folder whose bytes are no longer the ones that were made, so a hand edit between the two steps never reaches the task repository.
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { FREEZE_STACKS, FREEZE_TASKS } from './workspaces-sources.mjs';
import { MANIFEST } from './workspaces-write.mjs';

const FOLDER = /^(csharp|node)\/(\d\d)-[a-z0-9]+(?:-[a-z0-9]+)*$/;
const HEX64 = /^[0-9a-f]{64}$/;
const HEX40 = /^[0-9a-f]{40}$/;

const wanted = Object.freeze(FREEZE_STACKS.flatMap(stack => FREEZE_TASKS.map(id => `${stack}/${id}`)));
const keyOf = folder => { const m = FOLDER.exec(folder); return m === null ? null : `${m[1]}/${m[2]}`; };

const foldersProblem = trees => {
  const folders = Object.keys(trees ?? {});
  if (!folders.every(f => keyOf(f) !== null && HEX64.test(trees[f]))) return 'a folder of its list is not `<stack>/<NN-slug>` with a tree hash';
  return JSON.stringify(folders.map(keyOf).sort()) === JSON.stringify([...wanted].sort()) ? null : `its folders are not the twelve (${FREEZE_TASKS.join(', ')} in both stacks)`;
};

/** The sentence saying why a parsed manifest is not one, or null. */
export const manifestProblem = m => (HEX40.test(m?.commit ?? '') ? foldersProblem(m.trees) : 'its commit is not a 40-digit hex id');

/** `ok(manifest)` of the folder `from` | `fail` (absent, not JSON, or not a manifest of the twelve). */
export function readManifest(from) {
  const file = path.join(from, MANIFEST);
  let parsed;
  try {
    parsed = JSON.parse(readFileSync(file, 'utf8'));
  } catch (e) {
    return fail(`${file} cannot be read (${e.code ?? e.message}): it is made by \`explore.mjs workspaces\` beside the twelve folders`);
  }
  const problem = manifestProblem(parsed);
  return problem === null ? ok(parsed) : fail(`${file} is not the manifest of the twelve workspaces: ${problem}`);
}
