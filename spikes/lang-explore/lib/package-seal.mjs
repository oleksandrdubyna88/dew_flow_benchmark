// The seal of a quick task's clear held-out folder (F2.S1 items 3 to 5 and F2.S2 (d), bench PR 4a). `sealPackage` packs `~/rsd/lang-v2/explore/heldout-NN/cases/`, seals it with the archive key (AES-256-GCM, v1's `sealTree`), publishes
// the ONE archive `heldout-NN/suite.enc` once (a temporary file, flushed, a hard link) and then registers it in `inputs/heldout-frozen.json` (`heldout-registry.mjs`): archive first, entry second, and a kill between the two is
// resumed by the next seal when the archive opens to today's cases. The seal copies the key to the private backup first and verifies the copy (`heldout-key.mjs`). It takes the task's lock (`heldout-lifecycle.mjs`).
//
// What it keeps. Six archives (one per quick task), each about the size of its suite (tens of KiB to a few MiB), kept as long as the series' results exist. A re-seal after a case change is a NEW archive and a new identity (F2's
// document): the archive and the entry are written once and nothing prunes them, and a seal over a registered archive of other bytes is refused. An interrupted write leaves only owned temporary files (the registry's
// `.heldout-frozen.json.<pid>.<hex>.tmp`, the unsealing's `testsToDelete-<owner>-bench-clear-<id>`), which the next seal or open removes once its owner is gone: a temporary folder is made only under the lock, so the next holder
// of the lock knows its maker is gone.
//
// This module is the facade of the held-out lifecycle: the seal is here; the open, the close, the stale-folder decision and the readers' wrapper are in `heldout-lifecycle.mjs` and are exported from here too.
import { readFileSync } from 'node:fs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { sealTree } from '../../minigit-bench/lib/heldout-seal.mjs';
import { publishOnce } from './publish-once.mjs';
import { ctxOf, withTaskLock } from './heldout-context.mjs';
import { entryOfSeal, registeredEntry, writeEntry } from './heldout-registry.mjs';
import { loadHeldoutQuick } from './heldout-quick.mjs';
import { backupProblem, makeBackup, readKeyFile, sha256Hex } from './heldout-key.mjs';
import { removeOwnedTemps, unsealToTemp } from './heldout-unseal.mjs';

export { registryMismatchProblem } from './heldout-registry.mjs';
export { checkHeldout } from './heldout-check.mjs';
export { closeClearFolder, openClearFolder, sweepClearFolder, withOpenedHeldout } from './heldout-lifecycle.mjs';
export { openedFolderProblem } from './heldout-quick.mjs';

const readOrNull = (fs, file) => { try { return fs.readFileSync(file); } catch (e) { if (e.code === 'ENOENT') return null; throw e; } };

const absentCases = ctx => (ctx.fs.lstatSync(ctx.casesDir, { throwIfNoEntry: false }) === undefined ? fail(`the clear folder of task ${ctx.task.id} is not there (${ctx.casesDir}): a seal needs the cases, and an archive with no entry is never trusted by its authentication alone`) : null);
const absentManifest = (ctx, manifest) => (manifest === null ? fail(`${ctx.manifestFile} is not there: the manifest of task ${ctx.task.id} is written before the seal, and its hash goes into the entry`) : null);

/** What a seal needs besides the cases: the manifest's hash (the trap coverage table, F2.S1's: a seal without it is refused), the cases' shape and the key. -> `ok({ held, manifestSha256, key })` | `fail`. */
function sealInputs(ctx) {
  const manifest = readOrNull(ctx.fs, ctx.manifestFile);
  const missing = absentCases(ctx) ?? absentManifest(ctx, manifest);
  if (missing !== null) return missing;
  const held = loadHeldoutQuick(ctx.home, ctx.task, ctx.frozenFile);
  if (!held.ok) return held;
  const key = readKeyFile(ctx.keyFile);
  return key.ok ? ok({ held: held.value, manifestSha256: sha256Hex(manifest), key: key.value }) : key;
}

/** The entry a seal registers: counts, hashes, the archive's relative path and the date, nothing else. */
const entryFor = ({ ctx, inputs, archiveSha256 }) => entryOfSeal({
  archive: ctx.archiveRel, archiveSha256, treeSha256: inputs.held.treeSha256, caseCount: inputs.held.caseIds.length, trapCount: inputs.held.traps.length, manifestSha256: inputs.manifestSha256, keySha256: sha256Hex(inputs.key),
  sealedOn: ctx.now().toISOString().slice(0, 10),
});

/** The copy of the key at the private backup, made when it is absent and verified (the first seal's step, and a resume's). */
async function backedUp(ctx, key) {
  return makeBackup({ task: ctx.task, key, backupFile: ctx.backupFile, shareMount: ctx.shareMount, mountOf: ctx.mountOf, fs: ctx.fs, pid: ctx.pid, startTime: ctx.startTime, isAlive: ctx.isAlive });
}

/** A seal of cases nobody has sealed: back the key up, seal, publish the archive once, then write the entry. */
async function sealFresh({ ctx, inputs }) {
  const backup = await backedUp(ctx, inputs.key);
  if (!backup.ok) return backup;
  const sealed = sealTree({ src: ctx.casesDir, key: inputs.key });
  if (!sealed.ok) return sealed;
  publishOnce({ file: ctx.archiveFile, text: sealed.value.sealed, fs: ctx.fs });
  ctx.hooks.afterArchive?.();
  const written = writeEntry({ frozenFile: ctx.frozenFile, task: ctx.task, entry: entryFor({ ctx, inputs, archiveSha256: sha256Hex(sealed.value.sealed) }), fs: ctx.fs, lock: ctx });
  return written.ok ? ok({ state: 'sealed' }) : written;
}

const interrupted = (ctx, why) => fail(`the archive of task ${ctx.task.id} (${ctx.archiveFile}) was published and its entry was not written, and ${why}; nothing is written`);

/** Open the archive a killed seal published into a temporary folder and compare it with today's cases. -> `ok(null)` (it is today's cases) | `fail`. The temporary folder is removed. */
async function sameAsToday({ ctx, inputs, archive }) {
  const unsealed = await unsealToTemp({ ctx, blob: archive, key: inputs.key });
  if (!unsealed.ok) return interrupted(ctx, `it does not open with this key (${unsealed.reason})`);
  await unsealed.value.discard();
  return unsealed.value.treeSha256 === inputs.held.treeSha256 ? ok(null) : interrupted(ctx, 'it does not hold today\'s cases: the clear folder changed since the seal');
}

/** The archive a killed seal published, opened and compared with today's cases: when it unseals to them the entry is written; else a refusal naming the task (nothing is written or removed). */
async function sealResume({ ctx, inputs, archive }) {
  const backup = await backedUp(ctx, inputs.key);
  const same = backup.ok ? await sameAsToday({ ctx, inputs, archive }) : backup;
  if (!same.ok) return same;
  const written = writeEntry({ frozenFile: ctx.frozenFile, task: ctx.task, entry: entryFor({ ctx, inputs, archiveSha256: sha256Hex(archive) }), fs: ctx.fs, lock: ctx });
  return written.ok ? ok({ state: 'resumed' }) : written;
}

/** A task with no entry: seal it, or resume the seal whose archive is already there. */
function sealNew({ ctx }) {
  const inputs = sealInputs(ctx);
  if (!inputs.ok) return inputs;
  const archive = readOrNull(ctx.fs, ctx.archiveFile);
  return archive === null ? sealFresh({ ctx, inputs: inputs.value }) : sealResume({ ctx, inputs: inputs.value, archive });
}

const archiveProblem = ({ ctx, archive, entry }) => (archive === null || sha256Hex(archive) !== entry.archiveSha256
  ? fail(`the archive ${ctx.archiveFile} of task ${ctx.task.id} is not the registered one (archiveSha256 differs, or the file is missing): it is not sealed over, and nothing is changed`) : null);

const movedProblem = ({ ctx, entry }) => {
  const held = loadHeldoutQuick(ctx.home, ctx.task, ctx.frozenFile);
  return held.ok && held.value.treeSha256 !== entry.treeSha256 ? fail(`the clear cases of task ${ctx.task.id} are not the ones sealed in ${ctx.archiveFile}: a re-seal after a case change is a new archive and a new identity, which is not made here`) : null;
};

/**
 * A task with an entry: nothing is sealed again. An archive that is not the registered one, or cases that are no longer the sealed ones, is a refusal; else it is a no-op, but a no-op that says "done" only over a key whose backup
 * is verified (on the share's mount, hashing to the entry's `keySha256`).
 */
async function sealRegistered({ ctx, entry }) {
  const problem = archiveProblem({ ctx, archive: readOrNull(ctx.fs, ctx.archiveFile), entry }) ?? movedProblem({ ctx, entry });
  if (problem !== null) return problem;
  const missing = await backupProblem({ task: ctx.task, backupFile: ctx.backupFile, shareMount: ctx.shareMount, keySha256: entry.keySha256, mountOf: ctx.mountOf, fs: ctx.fs });
  return missing === null ? ok({ state: 'unchanged' }) : fail(missing);
}

async function sealLocked({ ctx }) {
  const temps = removeOwnedTemps(ctx);
  if (!temps.ok) return temps;
  const entry = registeredEntry({ frozenFile: ctx.frozenFile, task: ctx.task });
  if (!entry.ok) return entry;
  return entry.value === undefined ? sealNew({ ctx }) : sealRegistered({ ctx, entry: entry.value });
}

/**
 * Seal the clear held-out folder of `task` and register the archive. `args` is the held-out context (`heldout-context.mjs`): `home`, `task`, `keyFile`, `backupFile` and `shareMount` (no defaults), `manifestFile`, `frozenFile`,
 * and the seams. -> `ok({ state })` (`sealed`, `resumed` after a killed seal, `unchanged` for an equal entry) | `fail(sentence)`. A kill is not caught: the lock is released and the archive and the entry are each whole or absent.
 */
export async function sealPackage(args) {
  const ctx = ctxOf(args);
  return withTaskLock(ctx, () => sealLocked({ ctx }));
}
