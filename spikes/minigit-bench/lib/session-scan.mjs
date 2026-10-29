// Scan a session directory for the REAL token (E2.S2 B-S3): every file under it is read and handed to S1's `tokenHits` with the real token
// as an own secret. The box holds a DUMMY of the same shape on purpose, so the dummy is masked first; what is left is the real token or a
// credential of another shape. A file too large to read is listed, never skipped silently (absent is not zero). Values are never returned.
import { lstatSync, readFileSync, readdirSync } from 'node:fs';
import path from 'node:path';
import { tokenHits } from './token-scan.mjs';

export const MAX_SCAN_BYTES = 64 * 1024 * 1024;
const MASK = '[dummy]';

/** Every regular file under `root` as a path relative to it; links and devices are not followed. */
export function filesUnder(root) {
  const out = [];
  const walk = dir => {
    for (const name of readdirSync(dir)) {
      const full = path.join(dir, name);
      const stat = lstatSync(full);
      if (stat.isDirectory()) walk(full);
      else if (stat.isFile()) out.push({ rel: path.relative(root, full), size: stat.size });
    }
  };
  walk(root);
  return out;
}

/** Pure: `texts` with the dummy masked, scanned for `real` and every credential shape. `[{ source, kind, offset }]`. */
export function realTokenHits(texts, { real, dummy = null }) {
  const masked = texts.map(t => ({ source: t.source, text: dummy ? String(t.text).replaceAll(dummy, MASK) : String(t.text) }));
  return tokenHits(masked, [real]);
}

/** Scan a directory -> `{ hits, scanned, tooLarge: [rel] }`. Files are read as latin1, so a binary file (an environ dump) is searchable. */
export function scanSessionDir(root, { real, dummy = null }) {
  const files = filesUnder(root);
  const readable = files.filter(f => f.size <= MAX_SCAN_BYTES);
  const texts = readable.map(f => ({ source: f.rel, text: readFileSync(path.join(root, f.rel), 'latin1') }));
  return { hits: realTokenHits(texts, { real, dummy }), scanned: readable.length, tooLarge: files.filter(f => f.size > MAX_SCAN_BYTES).map(f => f.rel) };
}
