// How a number reaches a reader: an unknown renders `unknown`, never `$0.00`.
const money = n => `$${n.toFixed(2)}`;
const isNumber = n => typeof n === 'number' && Number.isFinite(n);

/** A USD figure: a number, `{ known: true, usd }`, `{ low, high }`, or anything not measured (`unknown`). */
export function renderUsd(x) {
  if (isNumber(x)) return money(x);
  if (x && x.known === false) return 'unknown';
  if (x && isNumber(x.low) && isNumber(x.high) && x.low !== x.high) return `${money(x.low)}-${money(x.high)}`;
  if (x && isNumber(x.usd)) return money(x.usd);
  return 'unknown';
}
