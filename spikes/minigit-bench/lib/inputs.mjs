// The pinned inputs' bookkeeping: the sha256sum-format list, the check of files against it, the expected test count.
import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { readAgentFile } from './agent-files.mjs';
import { fail, ok } from './result.mjs';

const LINE = /^([0-9a-f]{64}) {2}(\S.*)$/;

/** sha256sum format, one `<64 hex>  <path>` per line; blank lines ignored. Names the first malformed line or the duplicate path. */
export function parseHashes(text) {
  const entries = [];
  const seen = new Set();
  const lines = text.split('\n');
  for (let i = 0; i < lines.length; i += 1) {
    if (lines[i].trim() === '') continue;
    const m = LINE.exec(lines[i]);
    if (!m) return fail(`hashes.txt line ${i + 1} is malformed: ${JSON.stringify(lines[i].slice(0, 80))}`);
    if (seen.has(m[2])) return fail(`hashes.txt has a duplicate path: ${m[2]} (line ${i + 1})`);
    seen.add(m[2]);
    entries.push(Object.freeze({ sha256: m[1], path: m[2] }));
  }
  return ok(Object.freeze(entries));
}

/** The bytes of a file, or null when it does not exist. Any other error is thrown: only "absent" is an expected answer. */
export function readFileOrNull(file) {
  try {
    return readFileSync(file);
  } catch (e) {
    if (e.code === 'ENOENT' || e.code === 'ENOTDIR') return null;
    throw e;
  }
}

export const sha256Of = bytes => createHash('sha256').update(bytes).digest('hex');

/** The largest input file worth hashing; the pinned ones are a few KB. Anything bigger is not a pinned input. */
export const MAX_INPUT_BYTES = 4 * 1024 * 1024;

/**
 * For a path inside an agent-written tree: the bytes of a REGULAR file of at most `maxBytes`, `null` when absent, or
 * `{ unreadable: why }` for a symlink, a device, a pipe, a directory or an oversize file. The one reader is lib/agent-files.mjs: it opens
 * with O_NOFOLLOW and checks the opened descriptor, so a link swapped in after a check is not followed either.
 */
export const readRegularFile = (file, maxBytes = MAX_INPUT_BYTES) => readAgentFile(file, maxBytes);

/** Per entry { path, expected, actual, ok }. A missing file is `actual: null`; an unreadable one is `actual: 'unreadable: <why>'`. Never ok. */
export function verifyFiles(root, entries, readFile = readRegularFile) {
  return entries.map(({ sha256, path: rel }) => {
    const got = readFile(path.join(root, rel));
    const actual = got === null ? null : got.unreadable ? `unreadable: ${got.unreadable}` : sha256Of(got);
    return { path: rel, expected: sha256, actual, ok: actual === sha256 };
  });
}

/** How many tests a visible script contains: the count of its `# Test <n>:` headers. */
export const expectedTotal = scriptText => scriptText.split('\n').filter(l => /^# Test \d+:/.test(l)).length;
