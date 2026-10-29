// The pinned inputs' bookkeeping: the sha256sum-format list, the check of files against it, the expected test count.
import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import path from 'node:path';
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

/** Per entry { path, expected, actual, ok }. A missing file is `actual: null`, never a mismatch with the empty string. */
export function verifyFiles(root, entries, readFile = readFileOrNull) {
  return entries.map(({ sha256, path: rel }) => {
    const bytes = readFile(path.join(root, rel));
    const actual = bytes === null ? null : sha256Of(bytes);
    return { path: rel, expected: sha256, actual, ok: actual === sha256 };
  });
}

/** How many tests a visible script contains: the count of its `# Test <n>:` headers. */
export const expectedTotal = scriptText => scriptText.split('\n').filter(l => /^# Test \d+:/.test(l)).length;
