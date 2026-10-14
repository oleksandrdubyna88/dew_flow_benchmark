import { createHash } from 'node:crypto';

/** Pure: seed text -> run order of two A cells and one B. B's position = first 8 bytes of sha256(seed) mod 3. */
export function drawOrder(seed) {
  const position = Number(createHash('sha256').update(seed).digest().readBigUInt64BE(0) % 3n);
  return [0, 1, 2].map(i => (i === position ? 'B' : 'A'));
}
