// Text helpers of the pre-registration's extraction scripts: a block of a plan found by its first words, and the unwrapping that makes a quote comparable.

/** Whitespace runs (line wraps included) collapsed to one space: the form a quote is compared in. */
export const unwrap = text => text.replace(/\s+/g, ' ').trim();

const itemMarker = line => /^(- |\d+\. )/.exec(line)?.[1] ?? null;
const startsItem = (marker, line) => (marker === '- ' ? line.startsWith('- ') : marker !== null && /^\d+\. /.test(line));
const continues = (marker, line) => line.trim() !== '' && !startsItem(marker, line);

/**
 * The block of `text` that starts at the first line beginning with `start`: its consecutive non-blank lines, and for a list item (`- ` or `<n>. `) up to the next
 * item. -> `ok(lines)`-shaped `{ lines } | { missing }`; the caller names what it was looking for.
 */
export function blockAt(text, start) {
  const all = text.split('\n');
  const i = all.findIndex(l => l.startsWith(start));
  if (i < 0) return { missing: start };
  const marker = itemMarker(all[i]);
  const lines = [all[i]];
  for (let j = i + 1; j < all.length && continues(marker, all[j]); j += 1) lines.push(all[j]);
  return { lines };
}
