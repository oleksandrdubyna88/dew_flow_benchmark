import { createHash } from 'node:crypto';

/** Pure: seed text -> run order of two A cells and one B. B's position = first 8 bytes of sha256(seed) mod 3. */
export function drawOrder(seed) {
  const position = Number(createHash('sha256').update(seed).digest().readBigUInt64BE(0) % 3n);
  return [0, 1, 2].map(i => (i === position ? 'B' : 'A'));
}

/** Pure: which arm leads block `block` (1-based): the parity of the first 8 bytes of sha256("<seed>:<block>"); even -> A leads. */
export function blockLead(seed, block) {
  const draw = createHash('sha256').update(`${seed}:${block}`).digest().readBigUInt64BE(0);
  return draw % 2n === 0n ? 'A' : 'B';
}

/** Pure: `blocks` adjacent A/B pairs, each led by a seeded draw. Cells are named c1..c(2*blocks) in run order. */
export function drawBlocks(seed, blocks) {
  if (!Number.isInteger(blocks) || blocks < 1 || blocks > 4) throw new Error(`blocks must be an integer 1..4, got ${blocks}`);
  return Array.from({ length: blocks }, (_, i) => {
    const lead = blockLead(seed, i + 1);
    const arms = lead === 'A' ? ['A', 'B'] : ['B', 'A'];
    return arms.map((arm, position) => ({ cell: `c${2 * i + position + 1}`, arm, block: i + 1, position: position + 1 }));
  }).flat();
}
