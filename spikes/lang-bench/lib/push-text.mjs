// Positions in a text. Every scanner of the push check reports WHERE (a 1-based line) and never WHAT, so the one thing they share
// is the map from an offset to its line.

/** `lineOf(offset)` -> the 1-based line of a UTF-16 offset in `text`. */
export function lineIndex(text) {
  const starts = [0];
  for (let i = text.indexOf('\n'); i !== -1; i = text.indexOf('\n', i + 1)) starts.push(i + 1);
  return offset => {
    let low = 0;
    let high = starts.length - 1;
    while (low < high) {
      const mid = Math.ceil((low + high) / 2);
      if (starts[mid] <= offset) low = mid; else high = mid - 1;
    }
    return low + 1;
  };
}

const INVISIBLE = /[\u00AD\u200B-\u200D\u2060\uFEFF]/g;
const NAMED_ENTITIES = Object.freeze({ nbsp: '\u00A0', amp: '&', lt: '<', gt: '>', quot: '"', apos: "'", shy: '\u00AD', zwnj: '\u200C', zwj: '\u200D', ensp: '\u2002', emsp: '\u2003', thinsp: '\u2009' });
const MAX_CODE_POINT = 0x10ffff;

const entity = (whole, named, decimal, hex) => {
  if (named !== undefined) return NAMED_ENTITIES[named] ?? whole;
  const code = decimal === undefined ? Number.parseInt(hex, 16) : Number(decimal);
  return Number.isInteger(code) && code > 0 && code <= MAX_CODE_POINT ? String.fromCodePoint(code) : whole;
};

/**
 * `text` as the tokeniser reads it: HTML entities decoded (`&nbsp;`, `&shy;`, `&#173;`, `&#x200B;`) and the invisible characters (soft hyphen
 * U+00AD, zero-width U+200B-U+200D, word joiner U+2060, byte order mark U+FEFF) either removed (`'strip'`: a word broken by one is whole again) or
 * made spaces (`'space'`: a word separated only by one is two again). A line break is never added, so line numbers stay the original ones.
 */
export const cleanText = (text, invisible = 'strip') => text
  .replace(/&(?:([a-zA-Z]+)|#(\d{1,8})|#[xX]([0-9a-fA-F]{1,6}));/g, entity)
  .replace(INVISIBLE, invisible === 'strip' ? '' : ' ');

/** True when `text` holds an entity or an invisible character, so that the two readings of it can differ. */
export const needsCleaning = text => /&(?:[a-zA-Z]+|#\d{1,8}|#[xX][0-9a-fA-F]{1,6});|[\u00AD\u200B-\u200D\u2060\uFEFF]/.test(text);

/**
 * The words of `text` as v1's `normalise` reads them (lower-cased runs of a-z0-9, every other character a separator), each with the line it
 * starts on and the number of the whitespace-delimited token it sits in (a finding reports how many tokens its window spans, never which
 * words). No token is dropped and no window is exempt for being inside one token: a passage joined with `/` or `-` is ONE token and still
 * a hit. A path or URL is not a hit only because the approved public texts hold its words (see aidNeedles and lib/push-public.mjs).
 * `invisible` is cleanText's: the same text is read both ways by the callers that must not be evaded.
 */
export function wordsWithLines(text, invisible = 'strip') {
  const clean = cleanText(text, invisible);
  const lineOf = lineIndex(clean);
  return [...clean.toLowerCase().matchAll(/\S+/g)].flatMap((token, n) => [...token[0].matchAll(/[a-z0-9]+/g)].map(m => ({ word: m[0], line: lineOf(token.index + m.index), token: n })));
}

/** The readings of `text` worth scanning: one, or two (invisible characters stripped, and turned into spaces) when the text has any. */
export const readings = text => (needsCleaning(text) ? ['strip', 'space'] : ['strip']);
