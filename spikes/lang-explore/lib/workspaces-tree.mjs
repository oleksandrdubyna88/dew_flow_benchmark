// The tree of a workspace folder, as bytes and as one hash (F2.S2 (d), the freeze tools): a folder read into the file map `{ relPath: { bytes, mode } }` that `quickWorkspaceFiles` gives a session, the sha256 over such a map,
// and the byte-for-byte comparison that names the file when two maps differ. A map read from a folder and the map built in memory hash alike, because the hash sees only what git keeps: the path, the execute bit and the bytes.
import { createHash } from 'node:crypto';
import { closeSync, lstatSync, openSync, readFileSync, readSync, readdirSync, readlinkSync } from 'node:fs';
import path from 'node:path';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';

const sha256 = bytes => createHash('sha256').update(bytes).digest('hex');

/** The mode git would keep: 755 with any execute bit, else 644. */
export const keptMode = mode => ((mode & 0o111) === 0 ? 0o644 : 0o755);

/** The sha256 over a file map: for each path in name order, the path, the kept mode and the sha256 of the bytes. Empty folders are not part of it (git holds none). */
export function treeHashOfFiles(files) {
  const h = createHash('sha256');
  for (const rel of Object.keys(files).sort()) h.update(`${rel}\0${keptMode(files[rel].mode).toString(8)}\0${sha256(files[rel].bytes)}\n`);
  return h.digest('hex');
}

/** One entry of a folder: `[rel, file]`, or a sentence when it is neither a regular file nor a folder (a link is never followed). */
function entryOf({ root, rel, entry }) {
  const full = path.join(root, rel, entry.name);
  const st = lstatSync(full);
  if (st.isDirectory()) return { dir: path.join(rel, entry.name) };
  if (!st.isFile()) return { problem: `${path.join(rel, entry.name)} is not a regular file (a link or another kind of entry)` };
  return { file: [path.join(rel, entry.name), { bytes: readFileSync(full), mode: st.mode & 0o777 }] };
}

/** The regular files under `root/rel`, the folders met and the problems. */
function walkOne({ root, rel }) {
  const found = readdirSync(path.join(root, rel), { withFileTypes: true }).map(entry => entryOf({ root, rel, entry }));
  return { files: found.filter(f => f.file).map(f => f.file), dirs: found.filter(f => f.dir).map(f => f.dir), problems: found.filter(f => f.problem).map(f => f.problem) };
}

/** `ok({ files })` of a folder, every regular file by its path with `/` separators | `fail` naming an entry that is no regular file. */
export function readFolderFiles(root) {
  const files = [];
  const problems = [];
  const queue = [''];
  while (queue.length > 0) {
    const one = walkOne({ root, rel: queue.shift() });
    files.push(...one.files);
    problems.push(...one.problems);
    queue.push(...one.dirs);
  }
  if (problems.length > 0) return fail(`${root}: ${problems.join('; ')}`);
  return ok({ files: Object.fromEntries(files.map(([rel, file]) => [rel.split(path.sep).join('/'), file])) });
}

/** The first byte at which two buffers differ (the shorter one's length when one is a prefix of the other). */
function firstDifference(a, b) {
  const n = Math.min(a.length, b.length);
  let i = 0;
  while (i < n && a[i] === b[i]) i += 1;
  return i;
}

const BYTES_DIFFER = (rel, want, have) => (want.bytes.equals(have.bytes) ? null : `${rel}: differs at byte ${firstDifference(want.bytes, have.bytes)} (expected ${want.bytes.length} bytes, found ${have.bytes.length})`);
const MODE_DIFFERS = (rel, want, have) => (keptMode(want.mode) === keptMode(have.mode) ? null : `${rel}: the execute bit differs (expected mode ${keptMode(want.mode).toString(8)}, found ${keptMode(have.mode).toString(8)})`);

/** What differs about one path, or null: a file only one side has, then its bytes, then its execute bit. */
function differenceOf(rel, want, have) {
  if (want === undefined) return `${rel}: is not part of the workspace`;
  if (have === undefined) return `${rel}: is missing`;
  return BYTES_DIFFER(rel, want, have) ?? MODE_DIFFERS(rel, want, have);
}

/** The files two maps disagree on, one sentence each, in name order: `expected` is the map built from the sources, `found` the one read from a folder. Empty when they are equal. */
export function compareFiles(expected, found) {
  const paths = [...new Set([...Object.keys(expected), ...Object.keys(found)])].sort();
  return paths.map(rel => differenceOf(rel, expected[rel], found[rel])).filter(line => line !== null);
}

// ---- a streaming walk for trees that are too large to read into memory (F3.S1 PR 4: the series archive, the free verb, the disk rule) ------------------------------------------------------------------------------------------

const CHUNK_BYTES = 1024 * 1024;
const kindOf = st => (st.isFile() ? 'file' : st.isSymbolicLink() ? 'link' : 'other');

/** One entry by `lstat` (a link is never followed and no file is read): `{ rel, kind, size, mode, target }`; `target` is the link's text, else null. */
function entryByLstat({ root, rel }) {
  const full = path.join(root, rel);
  const st = lstatSync(full);
  return { rel: rel.split(path.sep).join('/'), kind: kindOf(st), directory: st.isDirectory(), size: st.size, mode: st.mode & 0o777, target: st.isSymbolicLink() ? readlinkSync(full) : null };
}

/** The entries directly under `rel`: `{ files, dirs }` (a folder is queued, not listed). */
function listOne({ root, rel, skip }) {
  const names = readdirSync(path.join(root, rel)).sort().map(name => path.join(rel, name)).filter(child => !skip(child.split(path.sep).join('/')));
  const entries = names.map(child => entryByLstat({ root, rel: child }));
  return { files: entries.filter(e => !e.directory).map(({ directory: _d, ...entry }) => entry), dirs: names.filter((_, i) => entries[i].directory) };
}

/**
 * Every entry under `root`, by `lstat`, sorted by path with `/` separators: `[{ rel, kind: 'file' | 'link' | 'other', size, mode, target }]` (a folder is walked and not listed; a link is listed and never followed). Nothing is read
 * into memory. `skip(rel)` leaves an entry out, a folder with everything under it.
 */
export const walkEntries = (root, { skip = () => false } = {}) => [...streamEntries(root, { skip })].sort(byRel);

/** The order of `walkEntries`: by path, code point order. */
export const byRel = (a, b) => (a.rel < b.rel ? -1 : Number(a.rel > b.rel));

/** The entries of `walkEntries` as the walk finds them (not sorted), one folder at a time: a generator, so a caller can count them while the walk goes on. */
export const streamEntries = (root, { skip = () => false } = {}) => entriesUnder({ root, rel: '', skip });

/** The entries under `root/rel` one folder at a time (a folder's own entries, then each sub folder): a generator, so no tree is held but the entries already yielded; the recursion is as deep as the tree. */
function* entriesUnder({ root, rel, skip }) {
  const { files, dirs } = listOne({ root, rel, skip });
  yield* files;
  for (const dir of dirs) yield* entriesUnder({ root, rel: dir, skip });
}

/** The sha256 of a file, streamed in 1 MiB chunks (a cache file may be large). */
export function sha256OfFile(file) {
  const h = createHash('sha256');
  const fd = openSync(file, 'r');
  try {
    const buffer = Buffer.alloc(CHUNK_BYTES);
    let n = readSync(fd, buffer, 0, CHUNK_BYTES, null);
    while (n > 0) {
      h.update(buffer.subarray(0, n));
      n = readSync(fd, buffer, 0, CHUNK_BYTES, null);
    }
  } finally {
    closeSync(fd);
  }
  return h.digest('hex');
}
