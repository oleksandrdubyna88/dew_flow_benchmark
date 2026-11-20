// The recovery path of Stage 1 that keeps the data (F3.S1 (a), PR 4; stories document section 5, brief item 5). Three FREE verbs; none launches or scores, each takes the series lock, and each is safe to run again after a kill (it copies, removes or
// restores only what its list names, and verifies after):
//   archiveSeries  copies the whole series folder to the private backup, writes the sha256 list of the source, verifies the copy file by file against it and appends the archive's identity to `<series>/archive.json`. It deletes nothing.
//   freeSeries     the ONLY thing that frees local space: it frees ONE closed list (`FREEABLE`, the three cache copies inside a session's box) and only of sessions that are final in a finished slot; it first verifies the newest archive is at its place
//                  and, per file, that the file is in the archive's list with the same sha256 locally and in the copy. A file that is not proven is kept and named. Each session keeps a `freed.json`: the archive's identity and what was removed.
//                  Never freed: `work/`, `score/`, `record.json`, `meta.json`, `plan.json`, `argv.json`, the streams, the transcripts, the logs, the read-back files and everything at the series' root. An unfinished slot's sessions are never touched.
//   restoreSeries  copies a freed session's files back from the archive, verifies them against `freed.json`'s list and removes `freed.json`.
// Who needs a freed folder: nothing between the halt and the report reads `FREEABLE` (the scoring builds from `work/` over the registered snapshot, never the session's cache). The final hash list of the record (PR 13) and the last archive
// after the series ended do need the whole tree, and each refuses a series that still holds a `freed.json` (`freedSessionProblem`), naming `stage-restore`.
import { chmodSync, copyFileSync, existsSync, lstatSync, mkdirSync, readFileSync, readdirSync, realpathSync, renameSync, rmSync, rmdirSync, symlinkSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { acquireSeriesLock } from '../../minigit-bench/lib/series-lock.mjs';
import { makeWritable } from '../../minigit-bench/lib/cache-snapshot.mjs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { readRunMap } from '../../minigit-bench/lib/run-map.mjs';
import { systemClock } from './clock.mjs';
import { isVoid, pairComplete, runMapFileOf } from './series.mjs';
import { DATA_SLOTS } from './stage-schedule.mjs';
import { mapSaying } from './stage-progress.mjs';
import { copyProblems, hashAt, listSeries, listText, parseList, sha256Text } from './stage-list.mjs';
import { walkEntries } from './workspaces-tree.mjs';

/** The closed list a recovery may free: the cache copies inside a session's box. Nothing else is ever removed. */
export const FREEABLE = Object.freeze(['box/nuget', 'box/npm', 'box/home/.dotnet']);
export const ARCHIVE_FILE = 'archive.json';
export const FREED_FILE = 'freed.json';
const sessionsDir = series => path.join(series, 's');
const sessionDir = (series, rid) => path.join(sessionsDir(series), rid);

const writeJson = (file, value) => {
  const tmp = `${file}.${process.pid}.tmp`;
  writeFileSync(tmp, `${JSON.stringify(value, null, 2)}\n`);
  renameSync(tmp, file);
};

/** `fn()` under the series lock `run` takes (a held lock is a refusal naming the holder), released on every path. */
function withSeriesLock(series, fn) {
  const lock = acquireSeriesLock({ seriesDir: series });
  if (!lock.ok) return lock;
  try {
    return fn();
  } finally {
    lock.value.release();
  }
}

// ---- archive.json ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

/** `ok([entry])` of `<series>/archive.json` (no file is no archive yet), or a `fail` when it cannot be read. */
function readArchives(series) {
  const file = path.join(series, ARCHIVE_FILE);
  if (!existsSync(file)) return ok([]);
  try {
    return ok(JSON.parse(readFileSync(file, 'utf8')).archives);
  } catch (e) {
    return fail(`${file} cannot be read (${e.message}): no archive is trusted over it`);
  }
}

/** The slots of the series that are finished (both stacks scored, or void after the one re-run), from its run-map: `ok([n])` | `fail`. */
function finishedSlots(series) {
  const map = readRunMap(runMapFileOf(series));
  return map.ok ? ok(Array.from({ length: DATA_SLOTS }, (_, i) => i + 1).filter(n => pairComplete(map.value, n, 'data'))) : map;
}

// ---- archive ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

/** Copy one listed entry under `dest` (a link as a link): a file already there with the listed hash is left alone, so a killed archive finishes where it stopped. */
function copyEntry({ series, dest, entry }) {
  const target = path.join(dest, entry.rel);
  if (hashAt(target) === entry.hash) return;
  mkdirSync(path.dirname(target), { recursive: true, mode: 0o700 });
  rmSync(target, { force: true });
  if (entry.kind === 'link') { symlinkSync(entry.target, target); return; }
  const tmp = `${target}.${process.pid}.tmp`;
  copyFileSync(path.join(series, entry.rel), tmp);
  chmodSync(tmp, entry.mode);
  renameSync(tmp, target);
}

/** The real path of `p` as far as it exists (every link resolved); the part that is not there yet is appended as written. A link cannot hide where a folder really is. */
function realIn(p) {
  const abs = path.resolve(p);
  const parent = path.dirname(abs);
  if (existsSync(abs)) return realpathSync(abs);
  return parent === abs ? abs : path.join(realIn(parent), path.basename(abs));
}

const within = (child, parent) => child === parent || child.startsWith(`${parent}${path.sep}`);

/** True when two real paths are the same folder or one is inside the other. */
export const pathsOverlap = (a, b) => within(a, b) || within(b, a);

/** The folder the copy of a series goes to: the series' own name under the backup folder. */
const destOf = ({ series, to }) => path.join(to, path.basename(path.resolve(series)));

/** The sentence for a backup folder that is missing, or whose copy would be the series folder, inside it or around it (links followed), or null. */
function locationProblem({ series, to }) {
  if (!existsSync(to)) return `the backup folder ${to} is not there: mount the share or create the folder, and run again`;
  const dest = destOf({ series, to });
  return pathsOverlap(realIn(dest), realIn(series)) ? `the archive's place ${dest} is the series folder, inside it or around it (links followed): an archive is a copy elsewhere` : null;
}

/** The sentence refusing an archive before anything is read, or null. */
const archiveRefusal = ({ series, to, final }) => locationProblem({ series, to }) ?? (final ? freedSessionProblem(series) : null);

function recordArchive({ series, to, dest, entries, text, now }) {
  const listFile = `${dest}.${sha256Text(text).slice(0, 12)}.sha256list`;
  writeFileSync(listFile, text);
  const slots = finishedSlots(series);
  if (!slots.ok) return slots;
  const entry = { listSha256: sha256Text(text), listFile, files: entries.length, bytes: entries.reduce((n, e) => (e.kind === 'file' ? n + e.size : n), 0), to, copy: dest, date: now().toISOString(), slotsFinished: slots.value };
  const known = readArchives(series);
  if (!known.ok) return known;
  writeJson(path.join(series, ARCHIVE_FILE), { archives: [...known.value, entry] });
  return ok(entry);
}

function archiveLocked({ series, to, now, hooks, report }) {
  const listed = listSeries(series, report);
  if (!listed.ok) return listed;
  const dest = destOf({ series, to });
  mapSaying({ items: listed.value, phase: 'copying', report, fn: entry => copyEntry({ series, dest, entry }) });
  hooks.afterCopy?.(dest);
  const problems = copyProblems({ entries: listed.value, root: dest, report });
  if (problems.length > 0) return fail(`the copy differs from the source (${problems.length} file(s)): ${problems.slice(0, 5).join('; ')}; nothing is recorded`);
  return recordArchive({ series, to, dest, entries: listed.value, text: listText(listed.value), now });
}

/**
 * `stage-archive`: copy the whole series to `to`, verify the copy file by file against the sha256 list, record it in `archive.json` (the list's sha256, the bytes, the place, the date and the slots finished at that time). It deletes nothing.
 * `progress(sentence)` (optional) hears one line per phase step: listing, copying and verifying, every `progressEvery` files (500 by default).
 * `final: true` is the last archive after the series ended and refuses a series that still holds a freed session. -> `ok(entry)` | `fail(sentence)`.
 */
export function archiveSeries({ series, to, now = systemClock, final = false, hooks = {}, progress, progressEvery }) {
  const refused = archiveRefusal({ series, to, final });
  return refused ? fail(refused) : withSeriesLock(series, () => archiveLocked({ series, to, now, hooks, report: { say: progress, every: progressEvery } }));
}

// ---- who is freed ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

/** The sessions that are freed (they hold a `freed.json`), by rid; a rid given narrows it to that one. */
const freedRids = (series, rid) => (existsSync(sessionsDir(series)) ? readdirSync(sessionsDir(series)) : []).filter(name => (rid === undefined || name === rid) && existsSync(path.join(sessionDir(series, name), FREED_FILE))).sort();

/** The sentence refusing a series (or one session of it) that still holds a freed session, naming `stage-restore`, or null. The final hash list of the record, the last archive and any command that rebuilds a record from a folder ask it. */
export function freedSessionProblem(series, rid) {
  const freed = freedRids(series, rid);
  return freed.length === 0 ? null : `${freed.length} session(s) of this series are freed (${freed.join(', ')}): their cache folders are in the archive, not on disk; run stage-restore --series ${series} first`;
}

const isFinalSession = s => s.kind === 'data' && (s.status === 'scored' || isVoid(s));

/** The rids a free may touch: final sessions (scored, or a settled void) of data slots that are finished and are not awaiting their read-back lines. */
function eligibleRids({ map, unreadSlots }) {
  const open = new Set(unreadSlots);
  return Object.values(map.sessions).filter(s => isFinalSession(s) && pairComplete(map, s.pair, 'data') && !open.has(s.pair)).map(s => s.rid).sort();
}

// ---- free -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

/** `ok({ entry, list })` for an archive entry whose list and copy are there and whose list is the recorded one, else a `fail` that frees nothing. */
function archiveAtPlace(entry) {
  const missing = [entry.listFile, entry.copy].find(file => !existsSync(file));
  if (missing !== undefined) return fail(`the newest archive is not at its place (${missing} is missing): nothing is freed`);
  const text = readFileSync(entry.listFile, 'utf8');
  return sha256Text(text) === entry.listSha256 ? ok({ entry, list: parseList(text) }) : fail(`the archive's list ${entry.listFile} is no longer the recorded one (its sha256 is not ${entry.listSha256}): nothing is freed`);
}

/** `ok({ entry, list })` when the newest archive is at its place, else a `fail`. */
function verifiedArchive(series) {
  const known = readArchives(series);
  if (!known.ok) return known;
  const entry = known.value.at(-1);
  return entry === undefined ? fail(`${series} has no archive recorded: run stage-archive --series ${series} --to <backup folder> first`) : archiveAtPlace(entry);
}

/** Why a file may not be removed (a sentence) or null when the archive proves it: it is listed with the hash it has now, and its archived copy has that hash too. */
function unproven({ series, rel, archive }) {
  const listed = archive.list.get(rel);
  if (listed === undefined) return 'it is not in the archive\'s list';
  if (hashAt(path.join(series, rel)) !== listed) return 'its hash is not the listed one';
  return hashAt(path.join(archive.entry.copy, rel)) === listed ? null : 'the archived copy no longer has the listed hash';
}

const lstatOf = file => { try { return lstatSync(file); } catch { return undefined; } };

/** The first link on the way from the series to `rel` (every folder on it is `lstat`ed, none is followed): its path from the series, or null. */
const linkOnWay = ({ series, rel }) => rel.split('/').map((_, i, parts) => parts.slice(0, i + 1).join('/')).find(part => lstatOf(path.join(series, part))?.isSymbolicLink()) ?? null;

/** A session's FREEABLE roots that are there: `{ rel, link }`, `link` the first link on the way to the root (the root itself included), else null. A root behind a link is outside what a free may touch. */
const rootsOf = ({ series, rid }) => FREEABLE.map(root => `s/${rid}/${root}`).map(rel => ({ rel, link: linkOnWay({ series, rel }) })).filter(r => r.link !== null || lstatOf(path.join(series, r.rel)) !== undefined);

const plainRoots = ({ series, rid }) => rootsOf({ series, rid }).filter(r => r.link === null).map(r => r.rel);

/** The entries under a session's FREEABLE folders that no link leads to, paths from the series root: `[{ rel, kind, size, mode, target }]`. */
const cacheEntriesOf = ({ series, rid }) => plainRoots({ series, rid }).flatMap(root => walkEntries(path.join(series, root)).map(e => ({ ...e, rel: `${root}/${e.rel}` })));

/** The roots a link leads to or hides, named as kept: they are never walked, chmodded or removed. */
const linkedRoots = ({ series, rid }) => rootsOf({ series, rid }).filter(r => r.link !== null).map(r => ({ path: r.rel, why: `${r.link} is a link and ${r.rel} is, or lies under, it: a link is never followed, so nothing behind it is walked, changed or removed` }));

const freedItem = (rid, e, hash) => ({ path: e.rel.slice(`s/${rid}/`.length), kind: e.kind, bytes: e.size, mode: e.mode, sha256: hash, ...(e.kind === 'link' ? { target: e.target } : {}) });

/** What the archive proves among a session's cache files: `{ removable: [{ entry, hash }], kept: [{ path, why }] }`. */
function judgeSession({ series, rid, archive }) {
  const judged = cacheEntriesOf({ series, rid }).map(e => ({ e, why: e.kind === 'other' ? 'it is neither a file nor a link' : unproven({ series, rel: e.rel, archive }) }));
  return { removable: judged.filter(j => j.why === null).map(j => ({ entry: j.e, hash: archive.list.get(j.e.rel) })), kept: [...judged.filter(j => j.why !== null).map(j => ({ path: j.e.rel, why: j.why })), ...linkedRoots({ series, rid })] };
}

const oldFreed = dir => (existsSync(path.join(dir, FREED_FILE)) ? JSON.parse(readFileSync(path.join(dir, FREED_FILE), 'utf8')) : { removed: [] });

/** `freed.json`, written BEFORE anything is removed (a kill in between leaves a list that covers more than was removed, never less). It names ONE archive and the whole list that archive holds: a session that already has one is never freed again (`freeLocked`). */
function writeFreed({ series, rid, archive, removable, now }) {
  const removed = removable.map(r => freedItem(rid, r.entry, r.hash)).sort((a, b) => (a.path < b.path ? -1 : 1));
  writeJson(path.join(sessionDir(series, rid), FREED_FILE), { archive: { listSha256: archive.entry.listSha256, to: archive.entry.to, copy: archive.entry.copy, listFile: archive.entry.listFile, date: archive.entry.date }, freedOn: now().toISOString(), removed });
}

const subFolders = dir => readdirSync(dir, { withFileTypes: true }).filter(e => e.isDirectory()).map(e => path.join(dir, e.name));

/** Remove the empty folders left under `dir` (bottom-up), the root included: a folder that still holds a kept file stays. */
function removeEmptyDirs(dir) {
  subFolders(dir).forEach(removeEmptyDirs);
  if (readdirSync(dir).length === 0) rmdirSync(dir);
}

function removeFiles({ series, rid, removable }) {
  const roots = plainRoots({ series, rid }).map(root => path.join(series, root));
  roots.forEach(makeWritable);
  for (const { entry } of removable) rmSync(path.join(series, entry.rel), { force: true });
  roots.filter(existsSync).forEach(removeEmptyDirs);
}

function freeSession({ series, rid, archive, now }) {
  const { removable, kept } = judgeSession({ series, rid, archive });
  if (removable.length > 0) {
    writeFreed({ series, rid, archive, removable, now });
    removeFiles({ series, rid, removable });
  }
  return { rid, removed: removable.map(r => r.entry.rel), bytes: removable.reduce((n, r) => n + r.entry.size, 0), kept };
}

function freeLocked({ series, unreadSlots, now }) {
  const archive = verifiedArchive(series);
  const map = archive.ok ? readRunMap(runMapFileOf(series)) : archive;
  if (!map.ok) return map;
  const eligible = eligibleRids({ map: map.value, unreadSlots });
  const held = new Set(freedRids(series));
  const done = eligible.filter(rid => !held.has(rid)).map(rid => freeSession({ series, rid, archive: archive.value, now }));
  return ok({ sessions: done.filter(d => d.removed.length > 0).map(d => d.rid), removed: done.flatMap(d => d.removed), bytes: done.reduce((n, d) => n + d.bytes, 0), kept: done.flatMap(d => d.kept), alreadyFreed: eligible.filter(rid => held.has(rid)) });
}

/**
 * `stage-free`: remove the three cache folders (`FREEABLE`) of sessions that are final in a finished slot, each file only when the newest verified archive lists it with the same sha256 and its archived copy has it too. `unreadSlots` (REQUIRED, an array: a call without it is refused) are the
 * read-back slots whose dated lines do not exist yet (`unreadReadbackSlots`): such a slot is no finished slot for this verb. A session that already holds a `freed.json` is not freed again (its list names the one archive that holds its files): it is named in
 * `alreadyFreed`. A final session's caches do not change after it ends, so nothing is lost; `stage-restore` it first to free it again. -> `ok({ sessions, removed, bytes, kept, alreadyFreed })` (`kept` names every file left, with the reason) | `fail`.
 */
export function freeSeries({ series, unreadSlots, now = systemClock }) {
  if (!Array.isArray(unreadSlots)) return fail('stage-free needs unreadSlots, the read-back slots whose dated lines do not exist yet (an empty list says none wait): without it a slot that still awaits its judge could be freed');
  return withSeriesLock(series, () => freeLocked({ series, unreadSlots, now }));
}

// ---- restore -----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

const copyOf = ({ freed, rid, item }) => path.join(freed.archive.copy, 's', rid, item.path);

/** The sentences for every listed file whose archived copy does not have the listed hash (nothing is placed before every copy is verified). */
const restoreProblems = ({ freed, rid, report }) => mapSaying({ items: freed.removed, phase: `verifying ${rid} (archive copy)`, report, fn: item => (hashAt(copyOf({ freed, rid, item })) === item.sha256 ? null : `${item.path}: the archived copy is missing or differs from the listed sha256`) }).filter(line => line !== null);

function placeItem({ series, freed, rid, item }) {
  const target = path.join(sessionDir(series, rid), item.path);
  if (hashAt(target) === item.sha256) return;
  mkdirSync(path.dirname(target), { recursive: true });
  rmSync(target, { force: true });
  if (item.kind === 'link') { symlinkSync(item.target, target); return; }
  const tmp = `${target}.${process.pid}.tmp`;
  copyFileSync(copyOf({ freed, rid, item }), tmp);
  chmodSync(tmp, item.mode);
  renameSync(tmp, target);
}

function restoreSession({ series, rid, report }) {
  const freed = oldFreed(sessionDir(series, rid));
  const problems = restoreProblems({ freed, rid, report });
  if (problems.length > 0) return fail(`session ${rid} is not restored: ${problems.join('; ')}`);
  mapSaying({ items: freed.removed, phase: `restoring ${rid}`, report, fn: item => placeItem({ series, freed, rid, item }) });
  const wrong = mapSaying({ items: freed.removed, phase: `verifying ${rid} (restored)`, report, fn: item => (hashAt(path.join(sessionDir(series, rid), item.path)) === item.sha256 ? null : item) }).filter(item => item !== null);
  if (wrong.length > 0) return fail(`session ${rid}: ${wrong.map(w => w.path).join(', ')} do not match the listed bytes after the copy`);
  rmSync(path.join(sessionDir(series, rid), FREED_FILE));
  return ok(freed.removed.length);
}

/** The sessions restored one after the other; the first refusal ends it (what was restored stays restored). */
function restoreAll({ series, rids, report }) {
  const done = [];
  for (const rid of rids) {
    const restored = restoreSession({ series, rid, report });
    if (!restored.ok) return restored;
    done.push(rid);
  }
  return ok({ restored: done });
}

function restoreLocked({ series, rid, report }) {
  const rids = freedRids(series, rid);
  return rid !== undefined && rids.length === 0 ? fail(`session ${rid} is not freed: there is no ${FREED_FILE} in it`) : restoreAll({ series, rids, report });
}

/** `stage-restore`: the freed files of one session (`rid`) or of every freed session copied back from the archive, each verified against `freed.json`'s list, and `freed.json` removed. A copy that differs is refused and nothing of that session is placed. `progress(sentence)` (optional) hears the verifying and restoring of each session. -> `ok({ restored })` | `fail`. */
export const restoreSeries = ({ series, rid, progress, progressEvery }) => withSeriesLock(series, () => restoreLocked({ series, rid, report: { say: progress, every: progressEvery } }));
