// A directory <-> one deterministic byte string. `packDir`: sorted POSIX paths, { path, mode: 0o755|0o644, data: base64 }, JSON with no
// timestamps, gzip -- identical trees give identical bytes. `unpackTo` validates EVERY entry before it writes ANY, so a bad archive
// leaves nothing behind; it refuses an absolute path, `..`, a link, a duplicate, an unknown field and a mode outside the two.
import { chmodSync, lstatSync, mkdirSync, readFileSync, readdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { gunzipSync, gzipSync } from 'node:zlib';
import { fail, ok } from './result.mjs';

const FORMAT = 1;
const EXECUTABLE = 0o755;
const PLAIN = 0o644;
const KEYS = ['data', 'mode', 'path'];
const SKIPPED_DIRS = new Set(['__pycache__']);

/** Every regular file below `root`, as sorted POSIX relative paths; a link (or anything else) is a fail naming it. */
function walk(root, rel = '') {
  const found = [];
  for (const name of readdirSync(path.join(root, rel)).sort()) {
    const relPath = rel === '' ? name : `${rel}/${name}`;
    const st = lstatSync(path.join(root, relPath));
    if (st.isDirectory()) {
      if (SKIPPED_DIRS.has(name)) continue;
      const inner = walk(root, relPath);
      if (!inner.ok) return inner;
      found.push(...inner.value);
    } else if (st.isFile()) {
      found.push(relPath);
    } else {
      return fail(`${relPath} is a link or special file: refusing to pack it`);
    }
  }
  return ok(found);
}

export function packDir(dir) {
  const files = walk(dir);
  if (!files.ok) return files;
  const entries = files.value.sort().map(rel => {
    const full = path.join(dir, rel);
    return { path: rel, mode: (lstatSync(full).mode & 0o111) !== 0 ? EXECUTABLE : PLAIN, data: readFileSync(full).toString('base64') };
  });
  return ok(gzipSync(Buffer.from(JSON.stringify({ format: FORMAT, entries })), { level: 9 }));
}

function checkEntry(e, seen) {
  if (e === null || typeof e !== 'object' || JSON.stringify(Object.keys(e).sort()) !== JSON.stringify(KEYS)) return 'an entry has fields other than path, mode, data (a link entry is refused)';
  if (typeof e.path !== 'string' || e.path === '' || e.path.includes('\\') || e.path.includes('\0')) return `bad entry path ${JSON.stringify(e.path)}`;
  if (path.posix.isAbsolute(e.path) || e.path.split('/').some(s => s === '' || s === '.' || s === '..')) return `entry path ${e.path} escapes its directory`;
  if (e.mode !== EXECUTABLE && e.mode !== PLAIN) return `entry ${e.path} has mode ${e.mode}: only 755 and 644 are allowed`;
  if (typeof e.data !== 'string') return `entry ${e.path} has no data`;
  if (seen.has(e.path)) return `entry ${e.path} appears twice`;
  return null;
}

/** Parse and validate; ok(entries) | fail. Nothing is written. */
function parseArchive(buf) {
  let doc;
  try {
    doc = JSON.parse(gunzipSync(buf).toString('utf8'));
  } catch (e) {
    if (e.code?.startsWith('Z_') || e instanceof SyntaxError) return fail(`not an archive: ${e.message}`);
    throw e;
  }
  if (doc?.format !== FORMAT || !Array.isArray(doc.entries)) return fail(`not an archive of format ${FORMAT}`);
  const seen = new Set();
  for (const e of doc.entries) {
    const bad = checkEntry(e, seen);
    if (bad) return fail(bad);
    seen.add(e.path);
  }
  return ok(doc.entries);
}

function writeEntries(root, entries) {
  for (const e of entries) {
    const target = path.join(root, ...e.path.split('/'));
    mkdirSync(path.dirname(target), { recursive: true, mode: 0o700 });
    writeFileSync(target, Buffer.from(e.data, 'base64'), { mode: e.mode, flag: 'wx' });
    chmodSync(target, e.mode); // the umask must not decide the mode
  }
  return ok(root);
}

/** Write the archive under `dir`: created 0700 here, and it must not exist yet. Validation comes first, so a bad archive writes nothing. */
export function unpackTo(buf, dir) {
  const parsed = parseArchive(buf);
  if (!parsed.ok) return parsed;
  const root = path.resolve(dir);
  mkdirSync(path.dirname(root), { recursive: true, mode: 0o700 });
  try {
    mkdirSync(root, { mode: 0o700 });
  } catch (e) {
    if (e.code === 'EEXIST') return fail(`${root} already exists: the clear suite goes into a directory of its own`);
    throw e;
  }
  return writeEntries(root, parsed.value);
}
