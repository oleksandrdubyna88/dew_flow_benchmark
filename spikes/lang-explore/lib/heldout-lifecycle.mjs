// The life of a task's clear held-out folder (F2.S2 (d), bench PR 4a): `heldout-open` makes it from the archive, `heldout-close` removes it, an entry point removes a STALE one, and a reader that opens for itself closes it after.
// Every one of them runs under the task's lock (`heldout-NN/.clear.lock`, `heldout-context.mjs`) and decides by the same table:
//
//   cases/ and its entry                 decision
//   absent, no entry, no archive         today's behaviour (an unsealed task: no lock is taken at all)
//   present, no entry                    today's behaviour (an authoring folder)
//   present, entry, tree hash differs    refused, never removed ("the clear folder differs from its archive: changed after sealing")
//   present, equal, stamp unreadable     refused, naming `.open` (an unreadable stamp is never judged dead)
//   present, equal, operator or live pid used as it is; nothing is removed
//   present, equal, no stamp or dead pid removed after the key's backup is verified (`removeUnder`, boundary `heldout-NN/`), then as "absent"
//
// The stamp `heldout-NN/.open` is `{ operator: true, at }` (the verb, held until `heldout-close`) or `{ pid, startTime, at }` (a reader that opened for itself), written `wx` after the rename. "Dead" is `isAlive(pid, startTime)`
// saying false (`ownerAlive`, which compares the start time read from /proc, so a reused pid is not alive) and nothing else. There is no override of the backup check.
import path from 'node:path';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { removeUnder } from '../../policy-ab-quick/lib/safe-rm.mjs';
import { ctxOf, withTaskLock } from './heldout-context.mjs';
import { backupProblem, loadKey, sha256Hex } from './heldout-key.mjs';
import { casesTreeHash, differsSentence, entryInRecord, interruptedSentence, readRecord } from './heldout-quick.mjs';
import { registeredEntry } from './heldout-registry.mjs';
import { removeOwnedTemps, renameIntoPlace, unsealToTemp } from './heldout-unseal.mjs';

// ---- the open stamp -------------------------------------------------------------------------------------------------------------------------------------------------------

/** What a parsed stamp is: the operator's, a process's, or unreadable. */
function stampFrom(j) {
  if (j?.operator === true) return { kind: 'operator' };
  return Number.isInteger(j?.pid) && typeof j.startTime === 'string' ? { kind: 'process', pid: j.pid, startTime: j.startTime } : { kind: 'unreadable' };
}

/** The stamp of the clear folder: `{ kind: 'none' | 'operator' | 'process' | 'unreadable' }` (a process's carries `pid` and `startTime`). Anything that does not parse into one of the two shapes is unreadable. */
export function readStamp(ctx) {
  const stat = ctx.fs.lstatSync(ctx.stampFile, { throwIfNoEntry: false });
  if (stat === undefined) return { kind: 'none' };
  if (!stat.isFile()) return { kind: 'unreadable' };
  try {
    return stampFrom(JSON.parse(ctx.fs.readFileSync(ctx.stampFile, 'utf8')));
  } catch {
    return { kind: 'unreadable' };
  }
}

/** Write the stamp `wx` (mode 600): the operator's, or this process's `{ pid, startTime }` exactly as `takeScorerLock` writes them. */
function writeStamp(ctx, owner) {
  const who = owner === 'operator' ? { operator: true } : { pid: ctx.pid, startTime: ctx.startTime };
  const fd = ctx.fs.openSync(ctx.stampFile, 'wx', 0o600);
  try { ctx.fs.writeSync(fd, `${JSON.stringify({ ...who, at: ctx.now().toISOString() })}\n`); } finally { ctx.fs.closeSync(fd); }
}

const isThisProcess = (ctx, stamp) => stamp.pid === ctx.pid && stamp.startTime === ctx.startTime;

/** Who holds the folder, from its stamp: `operator`, `mine` (this process), `live` (another process that is running), `dead` (the stamped process is gone) or `free` (no stamp). */
export function holdOf(ctx, stamp) {
  if (stamp.kind === 'process') return processHold(ctx, stamp);
  return { kind: stamp.kind === 'operator' ? 'operator' : 'free' };
}

/** A process's stamp: this process's own, a running one's, or a dead one's (the pid with that start time is not running). */
function processHold(ctx, stamp) {
  if (isThisProcess(ctx, stamp)) return { kind: 'mine' };
  return { ...stamp, kind: ctx.isAlive(stamp.pid, stamp.startTime) ? 'live' : 'dead' };
}

const isHeld = hold => !['free', 'dead'].includes(hold.kind);

const unreadableStamp = ctx => fail(`${ctx.stampFile} is unreadable: it may be a reader that is writing it, so it is not judged dead and nothing of task ${ctx.task.id} is removed. If no reader is active, remove it: rm ${ctx.stampFile}`);
const stampProblem = (ctx, stamp) => (stamp.kind === 'unreadable' ? unreadableStamp(ctx) : null);

// ---- the facts under the lock ---------------------------------------------------------------------------------------------------------------------------------------------

/** The owned temporary folders a killed open left are removed (the holder of the lock knows their maker is gone), then `ok({ entry, present })`: the registry's entry (or undefined) and whether `cases/` exists. */
function factsOf(ctx) {
  const temps = removeOwnedTemps(ctx);
  const entry = temps.ok ? registeredEntry({ frozenFile: ctx.frozenFile, task: ctx.task }) : temps;
  return entry.ok ? ok({ entry: entry.value, present: ctx.fs.lstatSync(ctx.casesDir, { throwIfNoEntry: false }) !== undefined }) : entry;
}

/** A `fail` when `cases/` is not the tree its archive was sealed from (or cannot be hashed), else null. */
function treeProblem(ctx, entry) {
  const tree = casesTreeHash(ctx.casesDir);
  if (!tree.ok) return fail(`the clear folder of task ${ctx.task.id} cannot be hashed: ${tree.reason}`);
  return tree.value === entry.treeSha256 ? null : fail(differsSentence({ task: ctx.task, frozenFile: ctx.frozenFile }));
}

/** Remove `cases/` and its stamp, after the key's backup is verified. -> `ok(null)` | `fail`. There is no override of the backup check. */
async function removeClear(ctx, entry) {
  const missing = await backupProblem({ task: ctx.task, backupFile: ctx.backupFile, shareMount: ctx.shareMount, keySha256: entry.keySha256, mountOf: ctx.mountOf, fs: ctx.fs });
  if (missing !== null) return fail(missing);
  try {
    removeUnder(ctx.casesDir, ctx.heldoutDir);
  } catch (e) {
    return fail(`the clear folder ${ctx.casesDir} of task ${ctx.task.id} cannot be removed: ${e.message}`);
  }
  ctx.fs.rmSync(ctx.stampFile, { force: true });
  return ok(null);
}

/** The refusal of a stamp that holds a folder that is not there: a live process's or (for an entry point) the operator's hold is never dropped. */
const isDroppable = (hold, operator) => hold.kind === 'free' || hold.kind === 'dead' || (hold.kind === 'operator' && operator);
const holderOf = (ctx, hold) => (hold.kind === 'operator' ? 'the operator' : `pid ${hold.pid ?? ctx.pid}`);
const remedyOf = (ctx, hold) => (hold.kind === 'operator' ? `Close it: explore.mjs heldout-close --task ${ctx.task.id}` : 'Wait for that process to end');

function heldStamp(ctx, hold, { operator }) {
  if (isDroppable(hold, operator)) return null;
  return fail(`${ctx.stampFile} is a hold of ${holderOf(ctx, hold)} on the clear folder of task ${ctx.task.id}, whose folder is not there: it is not dropped. ${remedyOf(ctx, hold)}`);
}

/**
 * A stamp whose folder is absent is JUDGED, not removed blind: an unreadable one is a refusal, a dead process's (or none) is dropped, a running process's is kept and refused, the operator's is kept and refused unless `operator`
 * (the operator's own `heldout-close`). The folder is removed before its stamp, so a kill between the two leaves exactly this case. -> `ok(null)` | `fail`.
 */
function dropStamp(ctx, { operator = false } = {}) {
  const stamp = readStamp(ctx);
  const problem = stampProblem(ctx, stamp) ?? heldStamp(ctx, holdOf(ctx, stamp), { operator });
  if (problem !== null) return problem;
  ctx.fs.rmSync(ctx.stampFile, { force: true });
  return ok(null);
}

// ---- the stale decision (every entry point) -------------------------------------------------------------------------------------------------------------------------------

/** A folder that is there, with an entry: the table's rows. -> `ok({ cases: 'held' | 'absent', entry })` | `fail`. */
async function sweepPresent(ctx, entry) {
  const stamp = readStamp(ctx);
  const problem = treeProblem(ctx, entry) ?? stampProblem(ctx, stamp);
  if (problem !== null) return problem;
  const hold = holdOf(ctx, stamp);
  if (isHeld(hold)) return ok({ cases: 'held', hold, entry });
  const removed = await removeClear(ctx, entry);
  return removed.ok ? ok({ cases: 'absent', removed: true, entry }) : removed;
}

/** The decision of the table, the lock held: `ok({ cases })` with `held` (used as it is), `absent` (none, or removed as stale), `authoring` (no entry: today's behaviour); or a `fail`. */
export async function sweepLocked(ctx) {
  const facts = factsOf(ctx);
  if (!facts.ok) return facts;
  const { entry, present } = facts.value;
  if (!present) return afterDrop(dropStamp(ctx), ok({ cases: 'absent', entry }));
  return entry === undefined ? ok({ cases: 'authoring', entry }) : sweepPresent(ctx, entry);
}

/** `then` when the stamp could be dropped, else the refusal that dropping gave. */
const afterDrop = (dropped, then) => (dropped.ok ? then : dropped);

/**
 * What the registry says of the task, read FIRST: `{ kind: 'sealed' }` (an entry, or an archive with no entry: an interrupted seal), `{ kind: 'none' }` (no record file, or neither an entry nor an archive: today's behaviour, no lock),
 * or `{ kind: 'refused', reason }`: a record that cannot be read (the task may be sealed in it) or an entry whose `heldout-NN/` is gone (the archive is missing). Nothing is opened, removed or read as unsealed on a refusal.
 */
function sealState(ctx) {
  const record = readRecord(ctx.frozenFile);
  if (!record.ok) return { kind: 'refused', reason: `the held-out record ${path.basename(ctx.frozenFile)} cannot be read (${record.reason}): task ${ctx.task.id} may be sealed in it, so nothing is opened, removed or read as unsealed` };
  return ctx.fs.existsSync(ctx.heldoutDir) ? { kind: isRegistered(ctx, record) ? 'sealed' : 'none' } : noFolderState(ctx, record);
}

/** A task whose `heldout-NN/` is not there: unsealed when the registry has no entry, a refusal when it has one (the archive is gone). */
const noFolderState = (ctx, record) => (entryInRecord(record, ctx.task.id) === undefined ? { kind: 'none' } : { kind: 'refused', reason: archiveGone(ctx) });

const archiveGone = ctx => `sealed: open it first (task ${ctx.task.id}) cannot be done: the registry holds an entry for it but its folder ${ctx.heldoutDir} is not there, so the archive ${ctx.archiveFile} is missing`;

const isRegistered = (ctx, record) => entryInRecord(record, ctx.task.id) !== undefined || ctx.fs.existsSync(ctx.archiveFile);

const withTakeover = (result, lock) => (result.ok ? ok({ ...result.value, takeover: lock.takeover }) : result);

/**
 * The stale decision at an entry point (init, run, rescore, replay): under the task's lock, a clear folder equal to its archive with no live owner is removed after the key's backup is verified; a held one is used as it is; one that
 * differs from its archive is refused. A task that is not sealed is left alone and no lock is taken. -> `ok({ cases })` | `fail(sentence)`.
 */
export async function sweepClearFolder(args) {
  const ctx = ctxOf(args);
  const state = sealState(ctx);
  if (state.kind !== 'sealed') return state.kind === 'none' ? ok({ cases: 'unsealed' }) : fail(state.reason);
  return withTaskLock(ctx, async lock => withTakeover(await sweepLocked(ctx), lock));
}

// ---- open -----------------------------------------------------------------------------------------------------------------------------------------------------------------

const readArchive = ctx => { try { return ctx.fs.readFileSync(ctx.archiveFile); } catch (e) { if (e.code === 'ENOENT') return null; throw e; } };

/** The archive's bytes when they are the registered ones, else a `fail` naming `archiveSha256` and the path. */
function registeredArchive(ctx, entry) {
  const blob = readArchive(ctx);
  if (blob === null) return fail(`the archive ${ctx.archiveFile} of task ${ctx.task.id} is not there (the registry holds an entry for it)`);
  return sha256Hex(blob) === entry.archiveSha256 ? ok(blob) : fail(`the archive ${ctx.archiveFile} of task ${ctx.task.id} is not the registered one: its sha256 is not the entry's archiveSha256`);
}

/** The unsealed tree is the registered one, or the temporary folder is removed and a `fail` names `treeSha256`. */
async function treeChecked(ctx, entry, unsealed) {
  if (unsealed.treeSha256 === entry.treeSha256) return ok(unsealed);
  await unsealed.discard();
  return fail(`the archive of task ${ctx.task.id} does not unseal to the registered cases (its treeSha256 differs): nothing is made`);
}

/**
 * The order of an open (the lock held): the key is valid and is the archive's (its sha256 is the entry's `keySha256`), the archive is the registered one (its sha256 is `archiveSha256`), it unseals into an owned temporary
 * folder, the unsealed tree is the registered one (`treeSha256`), `cases/` is still absent, one rename. Any refusal before the rename removes the temporary folder and its claim. -> `ok(casesDir)` | `fail`.
 */
async function unsealIntoPlace(ctx, entry) {
  const key = loadKey({ keyFile: ctx.keyFile, keySha256: entry.keySha256, task: ctx.task });
  const archive = key.ok ? registeredArchive(ctx, entry) : key;
  if (!archive.ok) return archive;
  const checked = await unsealedTree(ctx, entry, { blob: archive.value, key: key.value });
  return checked.ok ? renameIntoPlace({ ctx, unsealed: checked.value }) : checked;
}

/** The archive unsealed into a temporary folder whose tree is the registered one. -> `ok(unsealed)` | `fail` (the temporary folder is removed). */
async function unsealedTree(ctx, entry, { blob, key }) {
  const unsealed = await unsealToTemp({ ctx, blob, key });
  return unsealed.ok ? treeChecked(ctx, entry, unsealed.value) : unsealed;
}

/** The refusal of an open of a task that has no entry: an interrupted seal when the archive is there. */
const noEntrySentence = ctx => (ctx.fs.existsSync(ctx.archiveFile) ? interruptedSentence({ task: ctx.task, frozenFile: ctx.frozenFile }) : `task ${ctx.task.id} has no sealed archive registered in ${ctx.frozenFile}: there is nothing to open`);

/** Make the clear folder from the archive and stamp it. -> `ok({ state: 'opened' })` | `fail`. */
async function openPlaced(ctx, entry, owner) {
  if (entry === undefined) return fail(noEntrySentence(ctx));
  const placed = await unsealIntoPlace(ctx, entry);
  if (!placed.ok) return placed;
  writeStamp(ctx, owner);
  return ok({ state: 'opened' });
}

async function openLocked(ctx, owner) {
  const swept = await sweepLocked(ctx);
  if (!swept.ok) return swept;
  return swept.value.cases === 'held' ? ok({ state: 'held' }) : openPlaced(ctx, swept.value.entry, owner);
}

/**
 * `heldout-open`: the clear folder of a sealed task made from its archive under the lock, stamped for the operator (held until `heldout-close`). A folder that is there, equal and held is returned as it is (the archive is not opened
 * again); one that differs is refused; a stale one is removed first (which needs the backup named). -> `ok({ state: 'opened' | 'held', takeover })` | `fail`.
 */
export async function openClearFolder(args) {
  const ctx = ctxOf(args);
  const state = sealState(ctx);
  if (state.kind !== 'sealed') return fail(state.kind === 'none' ? noEntrySentence(ctx) : state.reason);
  return withTaskLock(ctx, async lock => withTakeover(await openLocked(ctx, args.owner ?? 'operator'), lock));
}

// ---- close ----------------------------------------------------------------------------------------------------------------------------------------------------------------

const foreignProblem = (ctx, hold) => (hold.kind === 'live' ? fail(`the clear folder of task ${ctx.task.id} is held by pid ${hold.pid} (started ${hold.startTime}), which is running: it is not closed under its user`) : null);

async function closePresent(ctx, entry) {
  const stamp = readStamp(ctx);
  const problem = treeProblem(ctx, entry) ?? stampProblem(ctx, stamp) ?? foreignProblem(ctx, holdOf(ctx, stamp));
  if (problem !== null) return problem;
  const removed = await removeClear(ctx, entry);
  return removed.ok ? ok({ state: 'closed' }) : removed;
}

/** The close, the lock held: nothing there is a no-op; an authoring folder (no entry) is never removed; a folder held by another live process is refused; the rest as the table. */
export async function closeLocked(ctx) {
  const facts = factsOf(ctx);
  if (!facts.ok) return facts;
  const { entry, present } = facts.value;
  if (!present) return afterDrop(dropStamp(ctx, { operator: true }), ok({ state: 'absent' }));
  return entry === undefined ? fail(`task ${ctx.task.id} has no sealed archive registered: its clear folder is an authoring folder and is not removed`) : closePresent(ctx, entry);
}

/** `heldout-close`: the clear folder removed, with its stamp, after the key's backup is verified. A second close is a no-op. -> `ok({ state: 'closed' | 'absent', takeover })` | `fail`. */
export async function closeClearFolder(args) {
  const ctx = ctxOf(args);
  const state = sealState(ctx);
  if (state.kind !== 'sealed') return state.kind === 'none' ? ok({ state: 'absent', takeover: null }) : fail(state.reason);
  return withTaskLock(ctx, async lock => withTakeover(await closeLocked(ctx), lock));
}

// ---- a reader that opens for itself ---------------------------------------------------------------------------------------------------------------------------------------

/** After the reader: the folder this reader made is closed. A refusal (the backup cannot be verified) leaves the folder, with a stamp of a process that is then gone, for the next entry point's sweep; it is said, never thrown. */
async function closeAfter(ctx) {
  const closed = await closeLocked(ctx);
  if (!closed.ok) ctx.warn(`the clear folder of task ${ctx.task.id} was left open: ${closed.reason}; run \`explore.mjs heldout-close --task ${ctx.task.id}\` with the key backup named (flag or the private file)`);
}

const placeIfAbsent = (ctx, swept) => (swept.value.cases === 'absent' ? openPlaced(ctx, swept.value.entry, 'process') : ok(null));

async function openedLocked(ctx, fn) {
  const swept = await sweepLocked(ctx);
  if (!swept.ok) return swept;
  const opens = swept.value.cases === 'absent';
  const placed = await placeIfAbsent(ctx, swept);
  if (!placed.ok) return placed;
  try {
    return await fn({ casesDir: ctx.casesDir, opened: opens });
  } finally {
    if (opens) await closeAfter(ctx);
  }
}

/**
 * A reader that opens the folder for itself (`leakcheck`): under the task's lock, held for the whole read, `fn({ casesDir, opened })` runs over the clear folder, made from the archive with this process's stamp when it was absent,
 * and closed after in a `finally`, whether `fn` returns or throws (the error is rethrown). A folder that is held (the operator's) is used as it is and kept. A task that is not sealed runs `fn` with no lock.
 * -> what `fn` returns | `fail(sentence)` when the folder cannot be opened (`fn` does not run).
 */
export async function withOpenedHeldout({ fn, ...args }) {
  const ctx = ctxOf(args);
  const state = sealState(ctx);
  if (state.kind !== 'sealed') return state.kind === 'none' ? fn({ casesDir: ctx.casesDir, opened: false }) : fail(state.reason);
  return withTaskLock(ctx, () => openedLocked(ctx, fn));
}
