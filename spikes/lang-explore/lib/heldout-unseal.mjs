// The unsealing of an archive into the clear folder (F2.S1 item 3, bench PR 4a): into an OWNED temporary folder inside `heldout-NN/` (the helper's name, `testsToDelete-<owner>-bench-clear-<id>`, with its claim beside it), made
// mode 700 before any file is written, the files inside made mode 600, its tree hash checked against the registry, and then ONE rename of its `cases` folder to `heldout-NN/cases`. Same folder, so the same file system: the rename
// is atomic, and a reader sees no `cases/` or the whole one, never half of one. A temporary folder is made only by a holder of the task's lock, so the next holder removes any it finds, with no liveness test of its maker.
import { chmodSync, lstatSync, readdirSync, renameSync } from 'node:fs';
import path from 'node:path';
import { cleanupScope, removeClaim } from '../../../.agents/conventions/tools/lib/owned.mjs';
import { ownedFor } from '../../../.agents/conventions/tools/lib/owned.mjs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { removeUnder } from '../../policy-ab-quick/lib/safe-rm.mjs';
import { casesTreeHash } from './heldout-quick.mjs';
import { OWNED_TOKEN } from './score-quick.mjs';

const owned = ownedFor({ token: OWNED_TOKEN });

/** The files of an unpacked folder made mode 600 (the archive's own modes are 644 and 755). The tree hash does not read modes. */
function tightenFiles(dir) {
  for (const name of readdirSync(dir)) chmodSync(path.join(dir, name), 0o600);
}

const isTemp = (name, pattern) => pattern.test(name) && !name.endsWith('.owner');

/** Remove the owned temporary folders (and their claims) of this user in `heldoutDir`: what a killed open left. Only a holder of the lock calls this. -> `ok([names removed])` | `fail`. */
export function removeOwnedTemps({ heldoutDir }) {
  const found = readdirSync(heldoutDir).filter(name => isTemp(name, owned.ownedPattern()));
  try {
    for (const name of found) {
      removeUnder(path.join(heldoutDir, name), heldoutDir);
      removeClaim(path.join(heldoutDir, name));
    }
  } catch (e) {
    return fail(`a temporary folder in ${heldoutDir} cannot be removed: ${e.message}`);
  }
  return ok(found);
}

/** `fail` of a refusal after the temporary folder was made: the folder and its claim are removed first. */
async function refused(scope, reason) {
  await scope.close();
  return fail(reason);
}

/** The archive's plaintext unpacked into `<temp>/cases`, its files tightened, its tree hashed. -> `ok(treeSha256)` | `fail`; nothing is written when the blob does not open. */
function unpacked({ ctx, blob, key, casesDir }) {
  const plain = ctx.unseal(blob, key);
  if (!plain.ok) return fail(`the archive of task ${ctx.task.id} does not open: ${plain.reason}`);
  const made = ctx.unpackTo(plain.value, casesDir);
  if (!made.ok) return fail(`the archive of task ${ctx.task.id} does not unpack: ${made.reason}`);
  tightenFiles(casesDir);
  return casesTreeHash(casesDir);
}

/**
 * Unseal `blob` with `key` into a new owned temporary folder inside `heldout-NN/` (or in `parent`: `heldout-check` does not hold the lock, so it unseals in the temp root). -> `ok({ temp, casesDir, treeSha256, discard })` (`discard()` removes the folder and its claim; the caller renames `casesDir` or discards) | `fail`
 * (the folder and claim are already removed). The lock must be held.
 */
export async function unsealToTemp({ ctx, blob, key, parent = ctx.heldoutDir }) {
  const scope = cleanupScope();
  const temp = owned.ownedDir(scope, 'clear', { parent });
  chmodSync(temp, 0o700);
  ctx.hooks.afterMkdir?.(temp);
  const casesDir = path.join(temp, 'cases');
  const tree = unpacked({ ctx, blob, key, casesDir });
  return tree.ok ? ok({ temp, casesDir, treeSha256: tree.value, discard: () => scope.close() }) : refused(scope, tree.reason);
}

/** The temporary folder's `cases` renamed to `heldout-NN/cases`: refused when a `cases/` has appeared. -> `ok(casesDir)` | `fail` (the temporary folder is removed on a refusal). */
export async function renameIntoPlace({ ctx, unsealed }) {
  if (lstatSync(ctx.casesDir, { throwIfNoEntry: false }) !== undefined) {
    await unsealed.discard();
    return fail(`${ctx.casesDir} appeared while task ${ctx.task.id} was unsealed: it is left as it is`);
  }
  ctx.hooks.beforeRename?.(unsealed.temp);
  renameSync(unsealed.casesDir, ctx.casesDir);
  await unsealed.discard();
  return ok(ctx.casesDir);
}
