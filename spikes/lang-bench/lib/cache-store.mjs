// E1.S4: the store of the two cache snapshots. The manifest (every package with its integrity, in one stable order), the record of a snapshot, the sweep of a killed build's staging folder, supersede (a stack's record
// moves to `superseded[]`, never deleted) and prune (a superseded snapshot's folder goes only after the pre-registration tag, and only when it re-hashes to its record). Every refusal is a value (`fail`).
// The conventions' owner helpers decide who owns what: nothing here deletes on a name, an age or a pid in a name.
import { spawnSync } from 'node:child_process';
import { existsSync, lstatSync, readFileSync, readdirSync, renameSync, rmSync, writeFileSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { START_TOLERANCE_S, processProbe, readOwner, thisOwner } from '../../../.agents/conventions/tools/lib/owned.mjs';
import { makeWritable, snapshotRecord, treeHash } from '../../minigit-bench/lib/cache-snapshot.mjs';
import { readNpmPackages, readNugetPackages } from '../../minigit-bench/lib/packages.mjs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';

const idOf = p => `${p.ecosystem}:${p.name}@${p.version}`;

// ---- the manifest ---------------------------------------------------------------------------------------------------------------

const versionParts = v => v.split(/[.-]/).map(x => (/^\d+$/.test(x) ? Number(x) : x));
export const plainOrder = (a, b) => (a < b ? -1 : a > b ? 1 : 0);
/** A sortable key per part: a missing part first, then numbers, then text. */
const partKey = p => (p === undefined ? [-1, 0] : typeof p === 'number' ? [0, p] : [1, p]);
const partOrder = (p, q) => partKey(p)[0] - partKey(q)[0] || plainOrder(partKey(p)[1], partKey(q)[1]);
/** Versions by their numeric parts (`9.14.0` before `10.1.0`); a part that is not a number is compared as text. */
export function versionOrder(a, b) {
  const [x, y] = [versionParts(a), versionParts(b)];
  for (let i = 0; i < Math.max(x.length, y.length); i += 1) {
    const c = partOrder(x[i], y[i]);
    if (c !== 0) return c;
  }
  return 0;
}
/** Ecosystem, then name in plain string order, then version by its numeric parts. */
export const sortPackages = list => [...list].sort((a, b) => plainOrder(a.ecosystem, b.ecosystem) || plainOrder(a.name, b.name) || versionOrder(a.version, b.version));

/** The manifest of a snapshot folder: the packages of its `npm/` and `nuget/` parts with their integrity, sorted. A package without a hash fails naming it (absent is not zero). */
export function manifestOf(dir) {
  const list = sortPackages([...readNpmPackages(path.join(dir, 'npm'), { hashes: true }), ...readNugetPackages(path.join(dir, 'nuget'), { hashes: true })]);
  const missing = list.filter(p => !p.integrity);
  return missing.length === 0 ? ok(list) : fail(`${missing.map(idOf).join(', ')} has no integrity in the snapshot: a package without a hash cannot be in the manifest`);
}

/** The manifest's name@version set must equal the list the maker returned: a difference fails naming it. */
export function checkManifestMatches(manifest, listed) {
  const [a, b] = [new Set(manifest.map(idOf)), new Set(listed.map(idOf))];
  const onlyManifest = [...a].filter(id => !b.has(id));
  const onlyListed = [...b].filter(id => !a.has(id));
  return onlyManifest.length + onlyListed.length === 0 ? ok(null) : fail(`the manifest and the maker's package list differ: only in the manifest ${onlyManifest.join(', ') || '(none)'}; only in the maker's list ${onlyListed.join(', ') || '(none)'}`);
}

/** The record of one registered stack (`inputs/cache-snapshots-lang.json`), built from what the maker returned and the manifest; `registerSnapshot` adds `madeOn`. */
export function buildRecord({ snapshot, manifest, recipeSha256, projectSha256 }) {
  const base = snapshotRecord(snapshot);
  return { treeSha256: base.treeSha256, dirName: path.basename(snapshot.dir), parts: base.parts, sizeBytes: base.sizeBytes, packages: manifest, resolved: base.resolved, steps: base.steps, recipeSha256, projectSha256 };
}

// ---- the staging sweep ---------------------------------------------------------------------------------------------------------

const STAGING_NAME = /^\.building-(csharp|node)-[1-9][0-9]*$/;
const keep = why => ({ action: 'keep', why });
const remove = why => ({ action: 'remove', why });
const claimedByOther = (owner, me) => ['owner', 'host', 'ns'].some(key => owner[key] !== me[key]);
const startsApart = (owner, seen) => owner.start !== undefined && seen !== undefined && Math.abs(owner.start - seen) > START_TOLERANCE_S;
const notStaging = ({ stat, uid }) => !stat.isDirectory() || stat.isSymbolicLink() || stat.uid !== uid;
const noClaim = owner => owner === null || owner.unreadable === true;

/** Pid gone -> remove; alive with a start time that differs by more than the tolerance (the pid now belongs to another process) -> remove; otherwise `null`. */
function livenessRule({ owner, probe }) {
  const seen = probe(owner.pid);
  if (seen.state === 'dead') return remove(`owner pid ${owner.pid} is gone`);
  return seen.state === 'live' && startsApart(owner, seen.start) ? remove(`pid ${owner.pid} now belongs to another process (start ${seen.start} ≠ ${owner.start})`) : null;
}

/** The rules of `stagingVerdict`, in order; the first that answers decides. */
const STAGING_RULES = Object.freeze([
  c => (notStaging(c) ? keep('not this user\'s staging folder') : null),
  c => (noClaim(c.owner) ? keep('no readable owner claim') : null),
  c => (claimedByOther(c.owner, c.me) ? keep('claimed by another owner or host') : null),
  livenessRule,
]);

/**
 * Pure. `name`, `stat` (an lstat), the claim `owner` (`readOwner(dir, 'folder')`), `probe(pid)`, `me` (`thisOwner()`), `uid` -> `null` (not a staging folder name: not looked at) or `{ action: 'remove' | 'keep', why }`.
 * The rules in order: not a real folder of this user -> keep; no readable claim -> keep; another owner, host or namespace -> keep; pid gone -> remove; pid alive with a start time that differs by more than the
 * conventions' tolerance -> remove; anything else (alive, unknown) -> keep. The pid in the folder's name is never used.
 */
export function stagingVerdict(context) {
  if (!STAGING_NAME.test(context.name)) return null;
  for (const rule of STAGING_RULES) {
    const verdict = rule(context);
    if (verdict !== null) return verdict;
  }
  return keep('owner may be alive');
}

/** Remove a staging folder (a read-only tree included) and then its claim. -> `null`, or the sentence of the failure (one folder never stops the sweep). */
function removeStaging(dir) {
  try {
    makeWritable(dir);
    rmSync(dir, { recursive: true, force: true });
    rmSync(`${dir}.owner`, { force: true });
    return null;
  } catch (error) {
    return error.message;
  }
}

/** One folder looked at: judged, and named on the log. */
function visit({ caches, name, log, ...seams }) {
  const dir = path.join(caches, name);
  const verdict = stagingVerdict({ name, stat: lstatSync(dir), owner: readOwner(dir, 'folder'), ...seams });
  if (verdict !== null) log(`staging ${verdict.action} ${dir}: ${verdict.why}`);
  return { dir, verdict };
}

/** One log line per staging folder looked at; `removed` lists what went. `probe`, `uid` and `me` are seams. */
export function sweepStaging({ caches, log, probe = processProbe, uid = process.getuid(), me = thisOwner() }) {
  const removed = [];
  for (const name of readdirSync(caches).filter(n => STAGING_NAME.test(n)).sort()) { // a claim file is no folder to look at (and one goes while the loop runs)
    const { dir, verdict } = visit({ caches, name, log, probe, uid, me });
    if (verdict?.action !== 'remove') continue;
    const problem = removeStaging(dir);
    if (problem === null) removed.push({ dir, why: verdict.why });
    else log(`staging could not remove ${dir}: ${problem}`);
  }
  return { removed };
}

// ---- supersede and prune -------------------------------------------------------------------------------------------------------

export const PREREG_TAG_PATTERN = 'prereg/lang-v2-A-*';
const NO_TAG_RULE = 'a superseded snapshot is kept until the pre-registration tag (prereg/lang-v2-A-*) exists';

/** The real tag reader: `git -C <repo> tag -l 'prereg/lang-v2-A-*'` -> `ok([names])` | `fail` (exe + argv, a ceiling; a git that does not answer is a refusal, never "no tag"). */
export function tagsOf(repo) {
  const r = spawnSync('git', ['-C', repo, 'tag', '-l', PREREG_TAG_PATTERN], { encoding: 'utf8', timeout: 20_000, env: { PATH: '/usr/bin:/bin', HOME: os.homedir(), LANG: 'C.UTF-8' } });
  return r.status === 0 ? ok(r.stdout.split('\n').filter(Boolean)) : fail(`could not list the pre-registration tags of ${repo} (${r.error?.message ?? `git exited ${r.status}`}); nothing is changed`);
}

const readRecords = file => (existsSync(file) ? JSON.parse(readFileSync(file, 'utf8')) : {});

/** Beside-and-rename: a reader sees the old file or the new one, never half a file. */
function writeBeside(file, records) {
  const temp = `${file}.tmp-${process.pid}`;
  writeFileSync(temp, `${JSON.stringify(records, null, 2)}\n`, { flag: 'wx' });
  renameSync(temp, file);
}

/** `ok(true)` when a pre-registration tag exists, `ok(false)` when none, the reader's own `fail` when it could not tell (never read as "no tag"). */
const tagExists = readTags => { const read = readTags(); return read.ok ? ok(read.value.length > 0) : read; };
const refuseWhenTagged = (tagged, why) => (!tagged.ok ? tagged : tagged.value ? fail(why) : null);
const refuseUnlessTagged = (tagged, why) => (!tagged.ok ? tagged : tagged.value ? null : fail(why));

const missingStack = (records, stack) => stack === 'superseded' || records[stack] === undefined;
const noReason = reason => typeof reason !== 'string' || reason.trim() === '';
const supersedeRefusal = ({ records, stack, reason, file }) => (missingStack(records, stack) ? fail(`no record for stack '${stack}' in ${file}: nothing to supersede`) : noReason(reason) ? fail('--reason must say why the snapshot is superseded') : null);

/**
 * Move a stack's record, whole, into `superseded[]` as `{ stack, supersededOn, reason, record }` and drop the stack key, so the next build may register again. Append only; refused (nothing written) for no
 * record, an empty reason, a pre-registration tag that exists or cannot be read.
 */
export function supersede({ file, stack, reason, tagsOf: readTags, today = new Date().toISOString().slice(0, 10) }) {
  const records = readRecords(file);
  const refused = supersedeRefusal({ records, stack, reason, file }) ?? refuseWhenTagged(tagExists(readTags), `the pre-registration tag exists: a registered snapshot can no longer be superseded (${PREREG_TAG_PATTERN})`);
  if (refused !== null) return refused;
  const { [stack]: record, superseded = [], ...rest } = records;
  writeBeside(file, { ...rest, superseded: [...superseded, { stack, supersededOn: today, reason: reason.trim(), record }] });
  return ok({ stack, supersededOn: today });
}

const SNAPSHOT_NAME = /^(csharp|node)-([0-9a-f]{64})$/;
const isRegistered = (records, hash) => Object.entries(records).some(([key, r]) => key !== 'superseded' && r?.treeSha256 === hash);
const isSuperseded = (records, [, stack, hash]) => (records.superseded ?? []).some(s => s.stack === stack && s.record?.treeSha256 === hash);
const ownedRealFolder = (dir, uid) => { const stat = lstatSync(dir); return stat.isDirectory() && !stat.isSymbolicLink() && stat.uid === uid; };

/** The rules of prune, in order; the first that answers names why the folder is kept. The last one is the re-hash that proves a folder is that snapshot. */
const KEEP_RULES = Object.freeze([
  c => (c.match === null ? 'not a snapshot of this series' : null),
  c => (isRegistered(c.records, c.match[2]) ? 'registered' : null),
  c => (isSuperseded(c.records, c.match) ? null : 'not a snapshot of this series'),
  c => (ownedRealFolder(c.dir, c.uid) ? null : 'not this user\'s'),
  c => (treeHash(c.dir) === c.match[2] ? null : 'hash differs'),
]);

/** Why a folder of the caches root is kept, or `null` when it is a superseded snapshot of this user that re-hashes to its record. */
function keepReason(context) {
  for (const rule of KEEP_RULES) {
    const why = rule(context);
    if (why !== null) return why;
  }
  return null;
}

function pruneOne({ name, caches, records, uid }) {
  const [dir, match] = [path.join(caches, name), SNAPSHOT_NAME.exec(name)];
  const why = keepReason({ dir, match, records, uid });
  if (why !== null) return { kept: { name, why } };
  makeWritable(dir);
  rmSync(dir, { recursive: true, force: true });
  const entry = records.superseded.find(s => s.stack === match[1] && s.record.treeSha256 === match[2]);
  return { removed: { name, supersededOn: entry.supersededOn, reason: entry.reason } };
}

/** Prune: after the pre-registration tag, remove each folder directly under `caches` that is a superseded snapshot of the record, a real folder of this user, and re-hashes to its name. -> `ok({ removed, kept })` | `fail`. */
export function pruneSuperseded({ file, caches, tagsOf: readTags, uid = process.getuid() }) {
  const refused = refuseUnlessTagged(tagExists(readTags), `prune is refused: ${NO_TAG_RULE}`);
  if (refused !== null) return refused;
  const records = readRecords(file);
  const done = readdirSync(caches).sort().map(name => pruneOne({ name, caches, records, uid }));
  return ok({ removed: done.flatMap(d => d.removed ?? []), kept: done.flatMap(d => d.kept ?? []) });
}
