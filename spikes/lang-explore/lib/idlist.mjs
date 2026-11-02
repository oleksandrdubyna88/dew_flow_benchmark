// A long list in a message is cut: the first ten, then "… and N more", with the total (coai finding 13), so a calibration, a leak check or a pause point that names many things stays one readable sentence.
const SHOWN = 10;

/** `items` joined with `separator`; more than ten become the first ten, then `… and N more (T in all)`. */
export function listOf(items, separator = ', ') {
  if (items.length <= SHOWN) return items.join(separator);
  return `${items.slice(0, SHOWN).join(separator)}${separator}… and ${items.length - SHOWN} more (${items.length} in all)`;
}
