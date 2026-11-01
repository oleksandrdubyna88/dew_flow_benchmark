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

/**
 * The words of `text` as v1's `normalise` reads them (lower-cased runs of a-z0-9, every other character a separator), each with the
 * line it starts on. No token is dropped: a leaked passage cannot hide behind markup, a slash or an underscore every few words.
 */
export function wordsWithLines(text) {
  const lineOf = lineIndex(text);
  return [...text.toLowerCase().matchAll(/[a-z0-9]+/g)].map(m => ({ word: m[0], line: lineOf(m.index) }));
}
