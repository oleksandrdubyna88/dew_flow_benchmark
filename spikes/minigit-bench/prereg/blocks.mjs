// Text helpers of the pre-registration's extraction scripts: a block of a plan found by its first words, and the unwrapping that makes a quote comparable.

/** Whitespace runs (line wraps included) collapsed to one space: the form a quote is compared in. */
export const unwrap = text => text.replace(/\s+/g, ' ').trim();

/**
 * The block of `text` that starts at the first line beginning with `start`: its consecutive non-blank lines, and for a list item (`- ` or `<n>. `) up to the next
 * item. -> `ok(lines)`-shaped `{ lines } | { missing }`; the caller names what it was looking for.
 */
export function blockAt(text, start) {
  const all = text.split('\n');
  const i = all.findIndex(l => l.startsWith(start));
  if (i < 0) return { missing: start };
  const marker = /^(- |\d+\. )/.exec(all[i])?.[1] ?? null;
  const nextItem = line => (marker === '- ' ? line.startsWith('- ') : marker !== null && /^\d+\. /.test(line));
  const lines = [all[i]];
  for (let j = i + 1; j < all.length && all[j].trim() !== '' && !nextItem(all[j]); j += 1) lines.push(all[j]);
  return { lines };
}
