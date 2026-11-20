// Two more ways to cut a quoted block out of the parent plan, beside v1's `blockAt` (`spikes/minigit-bench/prereg/blocks.mjs`, a paragraph or a list item): one table row, and a number of paragraphs from a line (a paragraph, its formula and the
// paragraph after; a paragraph and the table under it). Pure. Each answers `{ lines }` or `{ missing }` and never throws.
import { blockAt } from '../../minigit-bench/prereg/blocks.mjs';

/** The one line of `text` that starts with `start` (a table row of a plan). */
export function rowAt(text, start) {
  const line = text.split('\n').find(l => l.startsWith(start));
  return line === undefined ? { missing: start } : { lines: [line] };
}

/** The ends (exclusive indexes) of the paragraphs of `lines`: a paragraph ends at the last non-blank line before a blank line or the end. */
const paragraphEnds = lines => lines.flatMap((line, k) => (line.trim() !== '' && (k + 1 === lines.length || lines[k + 1].trim() === '') ? [k + 1] : []));

/** `count` paragraphs of `text` from the first line that starts with `start`, the blank lines between them kept. */
export function paragraphsAt(text, start, count) {
  const all = text.split('\n');
  const from = all.findIndex(l => l.startsWith(start));
  if (from < 0) return { missing: start };
  const rest = all.slice(from);
  const ends = paragraphEnds(rest);
  return { lines: rest.slice(0, ends[Math.min(count, ends.length) - 1]) };
}

const CUTS = Object.freeze({
  block: (text, q) => blockAt(text, q.start),
  row: (text, q) => rowAt(text, q.start),
  paragraphs: (text, q) => paragraphsAt(text, q.start, q.count),
});

/** The block a quote entry (`{ start, mode, count }`) names in `text`: `{ lines }` or `{ missing }`. */
export const cutQuote = (text, q) => CUTS[q.mode](text, q);
