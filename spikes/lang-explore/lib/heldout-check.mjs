// `heldout-check` (F2.S2 (d), bench PR 4a): the proof that a sealed task can be opened, read only. It takes no lock and removes nothing: the registry has an entry, the archive is the registered one (`archiveSha256`), `packages.json`
// agrees (when it is there), the key is the archive's (checked BEFORE any decryption), the archive opens and unseals to the registered tree, in an owned temporary folder in the temp root (never inside `heldout-NN/`: it holds no
// lock, so it must not touch the folder the lock guards), and a clear folder that is there is the archive's tree. It says what it found as counts only (D41).
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { tempRoot } from '../../../.agents/conventions/tools/lib/owned.mjs';
import { ctxOf } from './heldout-context.mjs';
import { backupProblem, loadKey, sha256Hex } from './heldout-key.mjs';
import { casesTreeHash, differsSentence, interruptedSentence } from './heldout-quick.mjs';
import { holdOf, readStamp } from './heldout-lifecycle.mjs';
import { registeredEntry, registryMismatchProblem } from './heldout-registry.mjs';
import { unsealToTemp } from './heldout-unseal.mjs';

/** The entry of the task, or a refusal: an archive with no entry is an interrupted seal, and no entry at all means the task is not sealed. */
function entryOrRefusal(ctx) {
  const entry = registeredEntry({ frozenFile: ctx.frozenFile, task: ctx.task });
  if (!entry.ok || entry.value !== undefined) return entry;
  return fail(ctx.fs.existsSync(ctx.archiveFile) ? interruptedSentence({ task: ctx.task, frozenFile: ctx.frozenFile }) : `no entry: task ${ctx.task.id} is not in ${ctx.frozenFile}`);
}

/** The archive's bytes when they are the registered ones: the file is there and its sha256 is the entry's `archiveSha256`. */
function archiveOf(ctx, entry) {
  const stat = ctx.fs.lstatSync(ctx.archiveFile, { throwIfNoEntry: false });
  if (stat === undefined) return fail(`the archive ${ctx.archiveFile} of task ${ctx.task.id} is not there`);
  const blob = ctx.fs.readFileSync(ctx.archiveFile);
  return sha256Hex(blob) === entry.archiveSha256 ? ok(blob) : fail(`the archive ${ctx.archiveFile} of task ${ctx.task.id} is not the registered one: its sha256 is not the entry's archiveSha256`);
}

const registryProblem = ctx => {
  const problem = registryMismatchProblem({ task: ctx.task, packagesFile: ctx.packagesFile, frozenFile: ctx.frozenFile });
  return problem === null ? null : fail(problem);
};

/** The registry's entry, the archive that is the registered one, and `packages.json`. -> `ok({ entry, blob })` | `fail`. */
function registeredBlob(ctx) {
  const entry = entryOrRefusal(ctx);
  const blob = entry.ok ? archiveOf(ctx, entry.value) : entry;
  if (!blob.ok) return blob;
  const problem = registryProblem(ctx);
  return problem === null ? ok({ entry: entry.value, blob: blob.value }) : problem;
}

/** The registry, the archive, `packages.json` and the key, in that order, before any decryption. -> `ok({ entry, blob, key })` | `fail`. */
function registered(ctx) {
  const found = registeredBlob(ctx);
  if (!found.ok) return found;
  const key = loadKey({ keyFile: ctx.keyFile, keySha256: found.value.entry.keySha256, task: ctx.task });
  return key.ok ? ok({ ...found.value, key: key.value }) : key;
}

/** The archive opens with the key and unseals to the registered tree: in the temp root, removed after. */
async function opens({ ctx, entry, blob, key }) {
  const unsealed = await unsealToTemp({ ctx, blob, key, parent: tempRoot() });
  if (!unsealed.ok) return unsealed;
  await unsealed.value.discard();
  return unsealed.value.treeSha256 === entry.treeSha256 ? ok(null) : fail(`the archive of task ${ctx.task.id} does not unseal to the registered cases (its treeSha256 differs)`);
}

const HOLDERS = Object.freeze({ operator: 'held by the operator', mine: 'held by this process', live: 'held by a running process', dead: 'stale (its opener is gone)', free: 'stale (no stamp)' });

/** A `fail` when the clear folder is not the archive's tree (or cannot be hashed), else null. */
function treeRefusal(ctx, entry) {
  const tree = casesTreeHash(ctx.casesDir);
  if (!tree.ok) return fail(tree.reason);
  return tree.value === entry.treeSha256 ? null : fail(differsSentence({ task: ctx.task, frozenFile: ctx.frozenFile }));
}

/** What the clear folder is, read only: closed, or present and equal to the archive (held or stale); a differing one or an unreadable stamp is a refusal. Nothing is removed. */
function clearState(ctx, entry) {
  if (ctx.fs.lstatSync(ctx.casesDir, { throwIfNoEntry: false }) === undefined) return ok('the clear folder is closed');
  const refusal = treeRefusal(ctx, entry);
  if (refusal !== null) return refusal;
  const stamp = readStamp(ctx);
  return stamp.kind === 'unreadable' ? fail(`${ctx.stampFile} is unreadable: it is not judged dead`) : ok(`the clear folder is present and equal to the archive, ${HOLDERS[holdOf(ctx, stamp).kind]}`);
}

/** The key backup line: verified when the backup and the share are named, else said not to be checked. */
async function backupLine(ctx, entry) {
  if (ctx.backupFile === undefined && ctx.shareMount === undefined) return ok('the key backup was not checked (no --key-backup and --share-mount)');
  const problem = await backupProblem({ task: ctx.task, backupFile: ctx.backupFile, shareMount: ctx.shareMount, keySha256: entry.keySha256, mountOf: ctx.mountOf, fs: ctx.fs });
  return problem === null ? ok('the key backup is verified') : fail(problem);
}

/**
 * Check one task. -> `ok({ line })` (counts and states only) | `fail(sentence)`. The sentences are the document's: no entry, an interrupted seal, an archive that is not the registered one, a `packages.json` that differs, a key that
 * is not the archive's, a clear folder that differs, a key backup that cannot be verified.
 */
export async function checkHeldout(args) {
  const ctx = ctxOf(args);
  const known = registered(ctx);
  if (!known.ok) return known;
  const proofs = await proven(ctx, known.value);
  const { caseCount, trapCount } = known.value.entry;
  return proofs.ok ? ok({ line: `task ${ctx.task.id}: ok; ${caseCount} cases, ${trapCount} traps; the archive opens with the key; ${proofs.value}` }) : proofs;
}

/** The archive opens, the clear folder is what it should be, the key backup is as asked. -> `ok(text)` | `fail`. */
async function proven(ctx, known) {
  const opened = await opens({ ctx, ...known });
  const state = opened.ok ? clearState(ctx, known.entry) : opened;
  if (!state.ok) return state;
  const backup = await backupLine(ctx, known.entry);
  return backup.ok ? ok(`${state.value}; ${backup.value}`) : backup;
}
