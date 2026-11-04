// E1.S4: the store of the two cache snapshots. The manifest (every package with its integrity, in one stable order), the record of a snapshot, the sweep of a killed build's staging folder, supersede (a stack's record
// moves to `superseded[]`, never deleted) and prune (a superseded snapshot's folder goes only after the pre-registration tag, and only when it re-hashes to its record). Every refusal is a value (`fail`).
// The conventions' owner helpers decide who owns what: nothing here deletes on a name, an age or a pid in a name.
import { createHash, randomBytes } from 'node:crypto';
import { spawnSync } from 'node:child_process';
import { existsSync, lstatSync, readFileSync, readdirSync, renameSync, rmSync, writeFileSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { ownerState, processProbe, readOwner, thisOwner } from '../../../.agents/conventions/tools/lib/owned.mjs';
import { makeWritable, registerSnapshot, snapshotRecord, treeHash } from '../../minigit-bench/lib/cache-snapshot.mjs';
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

/** sha256 of the sorted (ecosystem, name, version, integrity) rows: the identity a rebuild can match (the Node tree hash cannot be matched, every npm index entry holds a time). */
export const manifestSha256Of = manifest => createHash('sha256').update(JSON.stringify(sortPackages(manifest).map(p => [p.ecosystem, p.name, p.version, p.integrity]))).digest('hex');

/** The record of one registered stack (`inputs/cache-snapshots-lang.json`), built from what the maker returned and the manifest; `registerSnapshot` adds `madeOn`. */
export function buildRecord({ snapshot, manifest, recipeSha256, projectSha256 }) {
  const base = snapshotRecord(snapshot);
  return { treeSha256: base.treeSha256, dirName: path.basename(snapshot.dir), parts: base.parts, sizeBytes: base.sizeBytes, packages: manifest, manifestSha256: manifestSha256Of(manifest), resolved: base.resolved, steps: base.steps, recipeSha256, projectSha256 };
}

// ---- the staging sweep ---------------------------------------------------------------------------------------------------------

const STAGING_NAME = /^\.building-(csharp|node)-[1-9][0-9]*$/;
const keep = why => ({ action: 'keep', why });
const remove = why => ({ action: 'remove', why });
const claimedByOther = (owner, me) => ['owner', 'host', 'ns'].some(key => owner[key] !== me[key]);
const notStaging = ({ stat, uid }) => !stat.isDirectory() || stat.isSymbolicLink() || stat.uid !== uid;
const noClaim = owner => owner === null || owner.unreadable === true;

/**
 * The conventions' own judgement of the owner (`ownerState`): `gone` (no process has the pid, on this host and namespace, for this user) -> remove; `unknown` (a live pid with another start time, a pid that cannot be asked, ...)
 * -> KEEP and say so, because deleting on anything but a proven `gone` is how another run's live folder is lost; anything else (`live`) -> `null`, and the caller keeps it as "may be alive".
 */
function livenessRule({ owner, probe }) {
  const state = ownerState(owner, probe);
  if (state === 'gone') return remove(`owner pid ${owner.pid} is gone`);
  return state === 'unknown' ? keep(`owner state unknown (pid ${owner.pid}: alive with another start time, or it cannot be asked): kept; remove it by hand if no build is going`) : null;
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
 * The rules in order: not a real folder of this user -> keep; no readable claim -> keep; another owner, host or namespace -> keep; the conventions' `ownerState` says `gone` -> remove; `unknown` -> keep and say so;
 * anything else (alive) -> keep. The pid in the folder's name is never used.
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
function visit({ caches, name, log, lstat, ...seams }) {
  const dir = path.join(caches, name);
  const stat = statOrNull(dir, lstat);
  if (stat === null) return { dir, verdict: null }; // gone since the listing: nothing to look at
  const verdict = stagingVerdict({ name, stat, owner: readOwner(dir, 'folder'), ...seams });
  if (verdict !== null) log(`staging ${verdict.action} ${dir}: ${verdict.why}`);
  return { dir, verdict };
}

/** `lstat` of a path, `null` when it is gone (ENOENT); any other error is real. */
function statOrNull(file, lstat) {
  try { return lstat(file); } catch (error) { if (error.code === 'ENOENT') return null; throw error; }
}

const CLAIM_NAME = /^(\.building-(?:csharp|node)-[1-9][0-9]*)\.owner$/;
const ORPHAN_RULES = STAGING_RULES.slice(1); // the folder rules that read the claim: an orphan claim has no folder to look at

/** An orphan claim (`<caches>/.building-<stack>-<pid>.owner` whose folder is absent), judged by the same owner rules as a folder: `{ action, why }`. */
export function orphanVerdict({ owner, probe, me }) {
  for (const rule of ORPHAN_RULES) {
    const verdict = rule({ owner, probe, me });
    if (verdict !== null) return verdict;
  }
  return keep('owner may be alive');
}

/** One log line per staging folder looked at; `removed` lists what went. `probe`, `uid`, `me` and `lstat` are seams; `records` (the record as it is) lets the sweep judge stranded snapshot folders. */
function sweepFolders({ caches, log, ...seams }) {
  const removed = [];
  for (const name of readdirSync(caches).filter(n => STAGING_NAME.test(n)).sort()) { // a claim file is no folder to look at (and one goes while the loop runs)
    const { dir, verdict } = visit({ caches, name, log, ...seams });
    if (verdict?.action !== 'remove') continue;
    const problem = removeStaging(dir);
    if (problem === null) removed.push({ dir, why: verdict.why });
    else log(`staging could not remove ${dir}: ${problem}`);
  }
  return removed;
}

const orphanBases = caches => readdirSync(caches).sort().map(name => CLAIM_NAME.exec(name)?.[1]).filter(base => base !== undefined && !existsSync(path.join(caches, base)));

/**
 * The claims whose folder is gone (a build killed between the claim and the folder), judged by the same owner rules; every one is named on the log. -> `{ removed, gone, blocked }`: the stacks whose claim is
 * proven gone, and the stacks that also have a claim that is alive, unknown or not this user's (their snapshot folders are not judged).
 */
function sweepOrphans({ caches, log, probe, me }) {
  const result = { removed: [], gone: new Set(), blocked: new Set() };
  for (const base of orphanBases(caches)) {
    const dir = path.join(caches, base);
    const verdict = orphanVerdict({ owner: readOwner(dir, 'folder'), probe, me });
    log(`staging ${verdict.action} ${dir}.owner (orphan claim): ${verdict.why}`);
    const stack = base.split('-')[1];
    if (verdict.action !== 'remove') { result.blocked.add(stack); continue; }
    rmSync(`${dir}.owner`, { force: true });
    result.gone.add(stack);
    result.removed.push({ dir, why: verdict.why, orphan: true });
  }
  return result;
}

/** The `<stack>-<hash>` folders of one stack that the record does not hold and that are real folders of this user. */
const unrecordedFolders = ({ caches, stack, records, uid }) => readdirSync(caches).sort()
  .map(name => ({ name, m: new RegExp(`^${stack}-([0-9a-f]{64})$`).exec(name) }))
  .filter(({ name, m }) => m !== null && !isRecorded(records, stack, m[1]) && ownedRealFolder(path.join(caches, name), uid))
  .map(({ name }) => path.join(caches, name));

/**
 * A snapshot folder `<stack>-<hash>` that the record does not hold (registered or superseded) and whose build's claim is proven gone (and no other claim of that stack is alive): the maker was killed after it named the
 * folder and before the registration. Never judged without the record (the sweep cannot tell what is registered), never a folder that is not a real folder of this user.
 */
function sweepStranded({ caches, log, records, uid, orphans }) {
  if (records === undefined) return [];
  const stacks = [...orphans.gone].filter(s => !orphans.blocked.has(s));
  return stacks.flatMap(stack => unrecordedFolders({ caches, stack, records, uid })).flatMap(dir => removeStranded(dir, log));
}

/** One stranded snapshot folder removed and named; `[]` when the removal failed (said on the log). */
function removeStranded(dir, log) {
  const why = 'an unregistered snapshot of a build whose owner is gone';
  log(`staging remove ${dir}: ${why}`);
  const problem = removeStaging(dir);
  if (problem === null) return [{ dir, why }];
  log(`staging could not remove ${dir}: ${problem}`);
  return [];
}

export function sweepStaging({ caches, log, probe = processProbe, uid = process.getuid(), me = thisOwner(), lstat = lstatSync, records }) {
  const folders = sweepFolders({ caches, log, probe, uid, me, lstat });
  const orphans = sweepOrphans({ caches, log, probe, me });
  const removed = [...folders, ...orphans.removed, ...sweepStranded({ caches, log, records, uid, orphans })];
  return { removed };
}

// ---- supersede and prune -------------------------------------------------------------------------------------------------------

export const PREREG_TAG_PATTERN = 'prereg/lang-v2-A-*';
const NO_TAG_RULE = 'a superseded snapshot is kept until the pre-registration tag (prereg/lang-v2-A-*) exists';

const GIT_ENV = () => ({ PATH: process.env.PATH ?? '/usr/bin:/bin', HOME: os.homedir(), LANG: 'C.UTF-8', GIT_TERMINAL_PROMPT: '0' });
const git = (repo, args) => spawnSync('git', ['-C', repo, ...args], { encoding: 'utf8', timeout: 30_000, env: GIT_ENV() });
const gitFailure = (what, r) => fail(`could not ${what} (${r.error?.message ?? `git exited ${r.status}: ${r.stderr.trim().split('\n')[0]}`}); nothing is changed`);
const tagOfRef = line => line.split('\t')[1]?.replace(/^refs\/tags\//, '').replace(/\^\{\}$/, '');

const GITHUB_SSH = Object.freeze([/^git@github\.com:(.+?)(?:\.git)?$/, /^ssh:\/\/git@github\.com(?::\d+)?\/(.+?)(?:\.git)?$/]);

/** A GitHub SSH remote read over https (`git@github.com:<owner>/<repo>.git`, `ssh://git@github.com/...` -> `https://github.com/<owner>/<repo>.git`): the tag reader has no SSH key and must not need one. Any other url is used as it is. */
export function remoteUrlFor(url) {
  for (const form of GITHUB_SSH) {
    const m = form.exec(url);
    if (m !== null) return `https://github.com/${m[1]}.git`;
  }
  return url;
}

/** The remote to read: the flag's, else `remote.origin.url` of the repository; none is a refusal. -> `ok(url)` | `fail`. */
function remoteToRead(repo, remote) {
  if (remote !== undefined) return ok(remoteUrlFor(remote));
  const configured = git(repo, ['config', '--get', 'remote.origin.url']);
  return configured.status === 0 ? ok(remoteUrlFor(configured.stdout.trim())) : gitFailure(`find the remote of ${repo} (no remote.origin.url, and no --tags-remote)`, configured);
}

/**
 * The real tag reader: the pre-registration tags (`prereg/lang-v2-A-*`) of the LOCAL repository AND of its remote (`git ls-remote --tags <url>`: a tag pushed from another checkout counts; the url is `--tags-remote`, else
 * `origin`'s with a GitHub SSH form read over https). exe + argv, a ceiling; a git that cannot answer either question (no remote, no network) is a refusal, never "no tag". -> `ok([names])` | `fail`.
 */
export function tagsOf(repo, { remote } = {}) {
  const local = git(repo, ['tag', '-l', PREREG_TAG_PATTERN]);
  if (local.status !== 0) return gitFailure(`list the pre-registration tags of ${repo}`, local);
  const url = remoteToRead(repo, remote);
  if (!url.ok) return url;
  const read = git(repo, ['ls-remote', '--tags', url.value, `refs/tags/${PREREG_TAG_PATTERN}`]);
  if (read.status !== 0) return gitFailure(`read the pre-registration tags of ${url.value}`, read);
  return ok([...new Set([...local.stdout.split('\n'), ...read.stdout.split('\n').map(tagOfRef)].filter(Boolean))].sort());
}

const RECORD_IN_REPO = 'spikes/lang-bench/inputs/cache-snapshots-lang.json';

/** The registered record AT a tag (`git show <tag>:<the record>`): after the tag it is the one authority, whatever the working tree says. -> `ok(records)` | `fail` (a tag git cannot show is a refusal). */
export function recordAtTag(repo, tag) {
  const shown = git(repo, ['show', `${tag}:${RECORD_IN_REPO}`]);
  if (shown.status !== 0) return gitFailure(`read ${RECORD_IN_REPO} at ${tag}`, shown);
  try { return ok(JSON.parse(shown.stdout)); } catch (error) { return fail(`${RECORD_IN_REPO} at ${tag} is not JSON (${error.message})`); }
}

const readRecords = file => (existsSync(file) ? JSON.parse(readFileSync(file, 'utf8')) : {});

/** Beside-and-rename: a reader sees the old file or the new one, never half a file. */
function writeBeside(file, records) {
  const temp = `${file}.tmp-${process.pid}-${randomBytes(6).toString('hex')}`; // never a name an earlier killed run may have left
  writeFileSync(temp, `${JSON.stringify(records, null, 2)}\n`, { flag: 'wx' });
  renameSync(temp, file);
}

/** `fail` when the reader could not tell (never read as "no tag"), else `null`; `refuseWhenTagged` refuses supersede after the tag, `tagsOrRefusal` is the list prune needs. */
const refuseWhenTagged = (read, why) => (!read.ok ? read : read.value.length > 0 ? fail(why) : null);

const missingStack = (records, stack) => stack === 'superseded' || records[stack] === undefined;
const noReason = reason => typeof reason !== 'string' || reason.trim() === '';
const supersedeRefusal = ({ records, stack, reason, file }) => (missingStack(records, stack) ? fail(`no record for stack '${stack}' in ${file}: nothing to supersede`) : noReason(reason) ? fail('--reason must say why the snapshot is superseded') : null);

/**
 * Move a stack's record, whole, into `superseded[]` as `{ stack, supersededOn, reason, record }` and drop the stack key, so the next build may register again. Append only; refused (nothing written) for no
 * record, an empty reason, a pre-registration tag that exists or cannot be read.
 */
export function supersede({ file, stack, reason, tagsOf: readTags, today = new Date().toISOString().slice(0, 10) }) {
  const records = readRecords(file);
  const refused = supersedeRefusal({ records, stack, reason, file }) ?? refuseWhenTagged(readTags(), `the pre-registration tag exists: a registered snapshot can no longer be superseded (${PREREG_TAG_PATTERN})`);
  if (refused !== null) return refused;
  const { [stack]: record, superseded = [], ...rest } = records;
  writeBeside(file, { ...rest, superseded: [...superseded, { stack, supersededOn: today, reason: reason.trim(), record }] });
  return ok({ stack, supersededOn: today });
}

const SNAPSHOT_NAME = /^(csharp|node)-([0-9a-f]{64})$/;
/** The pair (stack, hash) is the registered one: a hash registered under ANOTHER stack says nothing about this folder. */
const isRegistered = (records, [, stack, hash]) => stack !== 'superseded' && records[stack]?.treeSha256 === hash;
const isSuperseded = (records, [, stack, hash]) => (records.superseded ?? []).some(s => s.stack === stack && s.record?.treeSha256 === hash);
const ownedRealFolder = (dir, uid) => { const stat = lstatSync(dir); return stat.isDirectory() && !stat.isSymbolicLink() && stat.uid === uid; };

/** The rules of prune, in order; the first that answers names why the folder is kept. The last one is the re-hash that proves a folder is that snapshot. */
const KEEP_RULES = Object.freeze([
  c => (c.match === null ? 'not a snapshot of this series' : null),
  c => (isRegistered(c.records, c.match) ? 'registered' : null),
  c => (isSuperseded(c.records, c.match) ? null : 'not a snapshot of this series'),
  c => (ownedRealFolder(c.dir, c.uid) ? null : 'not this user\'s'),
  c => { c.log(`checking snapshot ${c.name} (re-hashing it, this can take a while)`); return treeHash(c.dir) === c.match[2] ? null : 'hash differs'; },
]);

/** Why a folder of the caches root is kept, or `null` when it is a superseded snapshot of this user that re-hashes to its record. */
function keepReason(context) {
  for (const rule of KEEP_RULES) {
    const why = rule(context);
    if (why !== null) return why;
  }
  return null;
}

/** One folder of the caches root classified: `{ name, dir, entry }` when it is a superseded snapshot that re-hashes to its record (a candidate), else `{ name, why }`. Says which snapshot it is re-hashing first. */
function classify({ name, caches, records, uid, log }) {
  const [dir, match] = [path.join(caches, name), SNAPSHOT_NAME.exec(name)];
  const why = keepReason({ dir, match, records, uid, log, name });
  if (why !== null) return { name, why };
  return { name, dir, entry: records.superseded.find(s => s.stack === match[1] && s.record.treeSha256 === match[2]) };
}

const removeTree = dir => { makeWritable(dir); rmSync(dir, { recursive: true, force: true }); };

/** The removal of one candidate: `{ removed }` or `{ failed }` (a failure is a value that names the snapshot and why, never a throw that hides the rest). */
function removeCandidate({ name, dir, entry }, remove) {
  try {
    remove(dir);
    return { removed: { name, supersededOn: entry.supersededOn, reason: entry.reason } };
  } catch (error) {
    return { failed: { name, why: error.message } };
  }
}

/** The tags, and the record AT the first of them: `ok({ tag, records })` | `fail` (no tag: the rule; git cannot answer or show: a refusal). */
function recordAfterTag({ readTags, readRecordAtTag }) {
  const tags = readTags();
  if (!tags.ok) return tags;
  if (tags.value.length === 0) return fail(`prune is refused: ${NO_TAG_RULE}`);
  const at = readRecordAtTag(tags.value[0]);
  return at.ok ? ok(at.value) : at;
}

/**
 * Prune: after the pre-registration tag, remove each folder directly under `caches` that is a superseded snapshot of the record AT THE TAG (not the working tree's), whose own (stack, hash) is not registered, a real folder
 * of this user, and re-hashes to its name. EVERY folder is classified and re-hashed first (each re-hash is announced on `log`), then the candidates are removed, and a removal that fails is reported, not hidden.
 * -> `ok({ removed, kept, failed })` | `fail`. `remove` is a seam.
 */
export function pruneSuperseded({ caches, tagsOf: readTags, recordAtTag: readRecordAtTag, uid = process.getuid(), log = () => {}, remove = removeTree }) {
  const at = recordAfterTag({ readTags, readRecordAtTag });
  if (!at.ok) return at;
  const classified = readdirSync(caches).sort().map(name => classify({ name, caches, records: at.value, uid, log }));
  const done = classified.filter(c => c.dir !== undefined).map(c => removeCandidate(c, remove));
  return ok({ removed: done.flatMap(d => d.removed ?? []), kept: classified.filter(c => c.dir === undefined).map(({ name, why }) => ({ name, why })), failed: done.flatMap(d => d.failed ?? []) });
}

const isRecorded = (records, stack, hash) => records[stack]?.treeSha256 === hash || (records.superseded ?? []).some(s => s.stack === stack && s.record?.treeSha256 === hash);

/** A made snapshot folder goes (read-only tree included) unless its own (stack, hash) is in the record, registered or superseded. */
export function discardUnlessRecorded({ dir, stack, treeSha256, records }) {
  if (isRecorded(records, stack, treeSha256) || !existsSync(dir)) return { discarded: false };
  makeWritable(dir);
  rmSync(dir, { recursive: true, force: true });
  return { discarded: true };
}

function recordAndRegister({ made, stack, recipeSha256, projectSha256, file, madeOn }) {
  const manifest = manifestOf(made.dir);
  if (!manifest.ok) return manifest;
  const matched = checkManifestMatches(manifest.value, made.packages);
  if (!matched.ok) return matched;
  const record = buildRecord({ snapshot: made, manifest: manifest.value, recipeSha256, projectSha256 });
  const registered = registerSnapshot({ file, stack, record, madeOn });
  return registered.ok ? ok(record) : registered;
}

/** The record as it can be read now; one that cannot be read is `{}`, so it authorises keeping nothing. */
function readableRecords(file) {
  try { return readRecords(file); } catch { return {}; }
}

/**
 * The manifest, the record and the registration of a made snapshot. On ANY failure after the maker succeeded (a refusal, or an exception inside the steps) the made folder is removed unless its (stack, hash) is in the record
 * (E1.S4 review M2: a failed build must leave no read-only folder in the caches root); a record that cannot be read keeps nothing. -> `ok(record)` | `fail`.
 */
export function registerBuilt(args) {
  let done = null;
  try {
    done = recordAndRegister(args);
    return done;
  } finally {
    if (done === null || !done.ok) discardUnlessRecorded({ dir: args.made.dir, stack: args.stack, treeSha256: args.made.treeSha256, records: readableRecords(args.file) });
  }
}
