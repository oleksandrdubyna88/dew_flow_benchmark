// The substring tier of the leak scan (v1's semantics: a private line occurs anywhere inside the haystack's text), made linear: every needle is at least 8 characters, so one pass over the haystack rolls a 32-bit hash of its
// 8-character windows, looks each up in the needles' prefix table, and verifies a candidate with `startsWith`. The cost is the haystack's size, not needles x size (v1's `includes` per needle). Pure.

const PREFIX = 8;
const BASE = 31;

/** `31 ** n` modulo 2^32. */
function power(n) {
  let p = 1;
  for (let i = 0; i < n; i += 1) p = Math.imul(p, BASE);
  return p;
}

const OUT = power(PREFIX - 1);

const prefixHash = s => { let h = 0; for (let i = 0; i < PREFIX; i += 1) h = (Math.imul(h, BASE) + s.charCodeAt(i)) | 0; return h; };

/** The prefix table of `needles` (`[{ text, ... }]`, each text at least 8 characters): a bitmap for a quick no, and a map from the prefix hash to the needles that start so. */
export function buildSubstringIndex(needles) {
  const byHash = new Map();
  const bitmap = new Uint8Array(65536);
  for (const needle of needles) {
    const h = prefixHash(needle.text);
    bitmap[h & 0xffff] = 1;
    byHash.set(h, [...(byHash.get(h) ?? []), needle]);
  }
  return { byHash, bitmap, size: needles.length };
}

/** The needles that start at position `pos` of `text` (verified), given the prefix hash there. */
const startingAt = (index, text, pos, h) => (index.bitmap[h & 0xffff] === 1 ? (index.byHash.get(h) ?? []).filter(n => text.startsWith(n.text, pos)) : []);

/** Every needle that occurs in `text`, once each, with the position of its first occurrence: `[{ needle, pos }]`. */
export function findSubstrings(text, index) {
  if (!searchable(text, index)) return [];
  const found = new Map();
  let h = 0;
  for (let i = 0; i < text.length; i += 1) {
    h = roll(h, text, i);
    if (i >= PREFIX - 1) record(found, startingAt(index, text, i - PREFIX + 1, h), i - PREFIX + 1);
  }
  return [...found].map(([needle, pos]) => ({ needle, pos }));
}

const searchable = (text, index) => index.size > 0 && text.length >= PREFIX;

/** The hash of the 8-character window ending at `i`, from the one ending at `i - 1`. */
const roll = (h, text, i) => (Math.imul(i >= PREFIX ? (h - Math.imul(text.charCodeAt(i - PREFIX), OUT)) | 0 : h, BASE) + text.charCodeAt(i)) | 0;

const record = (found, needles, pos) => { for (const needle of needles) if (!found.has(needle)) found.set(needle, pos); };
