// Tier 3 of the leak scan, the window tier: three consecutive case lines pasted anywhere (a code fence, a Markdown table, a commit message, a diff hunk) are a leak even when no single line is long enough to be distinctive, which is
// the shape of task 09's millions of short numeric lines. For every case file, each window of three consecutive QUALIFYING lines (normalised, at least 8 characters, not one repeated character) gets a 53-bit key in a sorted
// typed array; the haystack's non-blank lines slide through the same ring (`git log -p` lines with their diff prefix removed). A key that hits is verified against the case lines (lib/leak-verify.mjs) before it is reported.
import { caseFilesOf, caseLinesOf } from './leak-case-files.mjs';
import { KeyIndex, normLine, pack, polyHash, stripDiffPrefix, usable } from './leak-text.mjs';

export const WINDOW_LINE_MIN = 8;

const lo = x => x % 4294967296;
const hi = x => Math.floor(x / 4294967296);
const mix = (h, v) => (Math.imul(h ^ v, 0x9e3779b1) ^ (h >>> 15)) | 0;

/** The key of three line keys, in order (a different order is a different key). */
export function windowKey(a, b, c) {
  const h1 = mix(mix(mix(mix(mix(mix(1, lo(a) | 0), hi(a)), lo(b) | 0), hi(b)), lo(c) | 0), hi(c));
  const h2 = mix(mix(mix(mix(mix(mix(7, hi(c)), lo(c) | 0), hi(b)), lo(b) | 0), hi(a)), lo(a) | 0);
  return pack(h1, h2);
}

/** The last three qualifying lines of a text: their keys, physical line numbers, normalised texts and raw lines, in three reused slots (no object per line). */
export class LineRing {
  #hash = [0, 0, 0];
  #line = [0, 0, 0];
  #text = ['', '', ''];
  #raw = ['', '', ''];
  #count = 0;

  /** Takes in a line; true when the ring now holds three. */
  push(hash, line, text, raw) {
    const slot = this.#count % 3;
    this.#hash[slot] = hash;
    this.#line[slot] = line;
    this.#text[slot] = text;
    this.#raw[slot] = raw;
    this.#count += 1;
    return this.#count >= 3;
  }

  /** The key of the three lines in the ring (oldest first). */
  key() {
    const o = this.#count % 3;
    return windowKey(this.#hash[o], this.#hash[(o + 1) % 3], this.#hash[(o + 2) % 3]);
  }

  /** The three lines in the ring, oldest first: `{ from, to, texts, raws }` (physical line numbers, 1-based). */
  window() {
    const slots = [0, 1, 2].map(k => (this.#count + k) % 3);
    return { from: this.#line[slots[0]], to: this.#line[slots[2]], texts: slots.map(s => this.#text[s]), raws: slots.map(s => this.#raw[s]) };
  }
}

/** The keys of every window of every case file of the folders `[{ casesDir }]`: a frozen sorted index. */
export function buildWindowKeys(sources, hash = polyHash) {
  const keys = new KeyIndex();
  for (const { casesDir } of sources) for (const name of caseFilesOf(casesDir)) addFile(keys, caseLinesOf(casesDir, name), hash);
  return keys.freeze();
}

function addFile(keys, lines, hash) {
  const ring = new LineRing();
  lines.forEach((raw, i) => {
    if (takeLine(ring, normLine(raw), raw, i + 1, hash)) keys.add(ring.key());
  });
}

/** Tier 3 over one text: the candidates (`{ tier: 'window', file, line, key, texts, raws }`) whose window key is in the index. `diff` text is read with its prefixes removed. */
export function windowTier({ name, lines, norms, diff, material }) {
  const pending = [];
  const seen = new Set();
  const ring = new LineRing();
  lines.forEach((raw, i) => {
    if (takeLine(ring, lineForm(raw, norms[i], diff), raw, i + 1, material.hash) && material.windowKeys.has(ring.key()) && !seen.has(ring.key())) { seen.add(ring.key()); pending.push(candidate(name, ring)); }
  });
  return pending;
}

/** The form of a haystack line the window tier slides: its normal form, with the diff prefix removed first in `git log -p` text. */
const lineForm = (raw, n, diff) => (diff ? normLine(stripDiffPrefix(raw)) : n);

/** Puts a line in the ring if it qualifies (at least 8 characters, not noise); true when the ring then holds a full window. */
export const takeLine = (ring, n, raw, line, hash = polyHash) => usable(n, WINDOW_LINE_MIN) && ring.push(hash(n), line, n, raw);

const candidate = (name, ring) => { const w = ring.window(); return { tier: 'window', file: name, line: w.from, key: ring.key(), texts: w.texts, raws: w.raws }; };
