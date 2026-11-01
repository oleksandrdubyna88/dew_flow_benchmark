import { readFileSync } from 'node:fs';

// Text and key tools of the leak scan (lib/leak-scan.mjs): ONE normalisation applied to the private material and to the haystack alike, 53-bit string keys, a sorted key index over typed arrays, and the small
// predicates the tiers share. Pure. Nothing here ever returns or logs a line: the findings built from it name WHERE, never the text.

// A line needs work only if it has an upper-case letter, a pipe, a tab, a CR, a leading space or list marker, a trailing space or a run of spaces; every other line is already normal (the fast path).
const NEEDS_WORK = /[A-ZÀ-Þ|\t\r]|^[\s>*-]|\s$|\s\s/;
const LIST_MARKER = /^(?:[-*]\s+|>+\s*)+/;
const SEPARATOR_ONLY = /^[\s:=+_*~-]*$/;

/**
 * The normal form of a line, for BOTH sides: lower case; CR removed; pipes and tabs become spaces; trimmed; whitespace runs collapsed to one space; a leading list marker (`-`, `*`, `>`) stripped. Blank and
 * separator-only lines (`---`, `|---|`) come out as a string for which `isNoise` is true. Tokens are never joined.
 */
export function normLine(line) {
  if (!NEEDS_WORK.test(line)) return line;
  return line.toLowerCase().replace(/[|\t\r]/g, ' ').trim().replace(LIST_MARKER, '').replace(/\s+/g, ' ').trim();
}

/** True when the characters of `s[from, to)` other than spaces are all one and the same character (or there are none): a ruler of dots, a row of zeros, a line of `#`. */
export function hasOneChar(s, from = 0, to = s.length) {
  const first = firstNonSpace(s, from, to);
  if (first === -1) return true;
  for (let i = first + 1; i < to; i += 1) if (differs(s[i], s[first])) return false;
  return true;
}

const firstNonSpace = (s, from, to) => { for (let i = from; i < to; i += 1) if (s[i] !== ' ') return i; return -1; };
const differs = (c, first) => c !== first && c !== ' ';

/** Blank, separator-only or made of one distinct character: a line that carries no content to match. */
export const isNoise = n => n === '' || SEPARATOR_ONLY.test(n) || hasOneChar(n);

/** A normalised line the tier with minimum length `min` takes: long enough and not noise. */
export const usable = (n, min) => n.length >= min && !isNoise(n);

const B1 = 0x01000193;
const B2 = 0x9e3779b1;

/** `base ** n` modulo 2^32, with 32-bit integer arithmetic (the factor that a rolling hash removes). */
function power(base, n) {
  let p = 1;
  for (let i = 0; i < n; i += 1) p = Math.imul(p, base);
  return p;
}

export const pack = (h1, h2) => (h1 >>> 0) * 2097152 + (h2 >>> 11); // 32 + 21 bits: an integer a double holds exactly

/** The 53-bit polynomial key of `s[from, to)`: two lanes of a hash modulo 2^32, so the same function serves whole lines and the windows a rolling pass slides. Equal keys are verified against the text before anything is reported. */
export function polyHash(s, from = 0, to = s.length) {
  let h1 = 0;
  let h2 = 0;
  for (let i = from; i < to; i += 1) {
    const c = s.charCodeAt(i);
    h1 = (Math.imul(h1, B1) + c) | 0;
    h2 = (Math.imul(h2, B2) + c) | 0;
  }
  return pack(h1, h2);
}

/** Calls `fn(key, position)` for the key of every window of `size` characters of `s`, by rolling: the key equals `polyHash(s, position, position + size)`. */
export function forEachWindowKey(s, size, fn) {
  if (s.length < size) return;
  const st = { h1: 0, h2: 0, out1: power(B1, size), out2: power(B2, size) };
  for (let i = 0; i < s.length; i += 1) {
    advance(st, s, i, size);
    if (i >= size - 1) fn(pack(st.h1, st.h2), i - size + 1);
  }
}

/** One step of the rolling hash: take in the character at `i`, and drop the one that left the window of `size`. */
function advance(st, s, i, size) {
  const c = s.charCodeAt(i);
  st.h1 = (Math.imul(st.h1, B1) + c) | 0;
  st.h2 = (Math.imul(st.h2, B2) + c) | 0;
  if (i < size) return;
  const gone = s.charCodeAt(i - size);
  st.h1 = (st.h1 - Math.imul(gone, st.out1)) | 0;
  st.h2 = (st.h2 - Math.imul(gone, st.out2)) | 0;
}

/** A set of 53-bit keys over one growing Float64Array: `add` while building, `freeze` once (a native numeric sort), then `has` by binary search. No JS object per key. */
export class KeyIndex {
  #keys = new Float64Array(1024);
  #n = 0;
  #frozen = false;

  add(key) {
    if (this.#n === this.#keys.length) {
      const bigger = new Float64Array(this.#keys.length * 2);
      bigger.set(this.#keys);
      this.#keys = bigger;
    }
    this.#keys[this.#n] = key;
    this.#n += 1;
  }

  /** Sorts the keys, compacts the duplicates (a task's chunk keys and window keys repeat) and makes the index searchable. */
  freeze() {
    const sorted = this.#keys.subarray(0, this.#n).sort();
    let kept = 0;
    for (let i = 0; i < sorted.length; i += 1) if (i === 0 || sorted[i] !== sorted[i - 1]) sorted[kept++] = sorted[i];
    this.#keys = sorted.subarray(0, kept);
    this.#n = kept;
    this.#frozen = true;
    return this;
  }

  get size() { return this.#n; }

  has(key) {
    if (!this.#frozen) throw new Error('KeyIndex: freeze() before has()');
    let lo = 0;
    let hi = this.#n - 1;
    while (lo <= hi) {
      const mid = (lo + hi) >>> 1;
      const v = this.#keys[mid];
      if (v === key) return true;
      if (v < key) lo = mid + 1; else hi = mid - 1;
    }
    return false;
  }
}

/** A file is binary for the text tiers when a NUL byte is among its first 8 KB. */
export const isBinary = raw => raw.slice(0, 8192).includes('\0');

/** A line of `git log -p` text without its one-character diff prefix (`+`, `-` or a space), or the line itself when it has none. */
export const stripDiffPrefix = line => (/^[+\- ]/.test(line) ? line.slice(1) : line);

/** A text split into lines at `\n` (the 1-based physical line is the index plus one): the one split every reader of the pilot's leak scan uses. */
export const splitLines = text => text.split('\n');

/** The lines of a file read in `encoding`: `latin1` for the private case files and the haystack (a byte is a character), `utf8` where a line is compared with what git prints. */
export const readLines = (file, encoding = 'utf8') => splitLines(readFileSync(file, encoding));
