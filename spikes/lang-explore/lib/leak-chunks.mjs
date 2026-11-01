// Tier 4 of the leak scan, the chunk tier: a partial paste of a LONG case line (a 1000-character grid row, a 2 MB expression) that no whole-line lookup can see. Every case line of 256 characters or more (normalised) gives its aligned
// 64-character chunks (offsets 0, 64, 128, ...; a chunk of one repeated character is dropped) as 53-bit keys; a haystack line of 64 characters or more is slid by a rolling 64-character hash (lib/leak-text.mjs `forEachWindowKey`).
// This is a superset of "haystack tokens": a window of a token is a window of its line, and a long case line with spaces is found too. A key that hits is verified against the chunk's text (lib/leak-verify.mjs). A paste of 127
// characters or more of a long line always contains an aligned chunk; a shorter one may not.
import { caseFilesOf, caseLinesOf } from './leak-case-files.mjs';
import { KeyIndex, forEachWindowKey, hasOneChar, normLine, polyHash, stripDiffPrefix } from './leak-text.mjs';

export const CHUNK = 64;
export const LONG_LINE_MIN = 256;

/** The aligned chunks of a normalised long line as `[offset, ...]`: those of `CHUNK` characters that are not one repeated character. */
export function chunkOffsets(n) {
  const offsets = [];
  for (let at = 0; at + CHUNK <= n.length; at += CHUNK) if (!hasOneChar(n, at, at + CHUNK)) offsets.push(at);
  return offsets;
}

/** The keys of the aligned chunks of every long case line of the folders `[{ casesDir }]`. */
export function buildChunkKeys(sources) {
  const keys = new KeyIndex();
  for (const { casesDir } of sources) for (const name of caseFilesOf(casesDir)) for (const raw of caseLinesOf(casesDir, name)) addChunks(keys, normLine(raw));
  return keys.freeze();
}

function addChunks(keys, n) {
  if (n.length < LONG_LINE_MIN) return;
  for (const at of chunkOffsets(n)) keys.add(polyHash(n, at, at + CHUNK));
}

/** The forms of a haystack line the rolling pass reads: its normal form, and for `git log -p` text also the form without its diff prefix. */
const formsOf = (raw, n, diff) => (diff ? [...new Set([n, normLine(stripDiffPrefix(raw))])] : [n]);

/** Tier 4 over one text: a candidate `{ tier: 'chunk', file, line, key, text, raws }` for each rolling window whose key is in the index (the text kept is the 64 characters, only for the verify step). */
export function chunkTier({ name, lines, norms, diff, material }) {
  const out = { pending: [], seen: new Set() };
  lines.forEach((raw, i) => {
    for (const form of formsOf(raw, norms[i], diff)) if (form.length >= CHUNK) scanForm({ form, name, line: i + 1, raw, material, out });
  });
  return out.pending;
}

/** One candidate per DISTINCT chunk key in the text (the first place it stands) and its 64 characters sliced only then: a file that repeats a matching row thousands of times holds a handful of candidates, not thousands. */
function scanForm({ form, name, line, raw, material, out }) {
  forEachWindowKey(form, CHUNK, (key, at) => {
    if (!material.chunkKeys.has(key) || out.seen.has(key)) return;
    out.seen.add(key);
    out.pending.push({ tier: 'chunk', file: name, line, key, text: form.slice(at, at + CHUNK), raws: [raw] });
  });
}
