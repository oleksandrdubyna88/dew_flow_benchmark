// The read-back of the pair's CPU masks before a paid launch (F1.S2 open point 4; *The scoring CPUs*). The session masks (`0-5` and `8-13`) are applied by the launch, and the in-flight observation 15 s after it only RECORDS; a
// mask the kernel did not apply would be found after the money was spent. So the preflight probes each mask of the pair with `taskset` first and refuses the launch (exit 3, resumable, nothing planned or paid) when a probe reads
// back another mask. On success the read-back is kept in the series folder as `readback.json`, by pair.
import { existsSync, readFileSync, renameSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { affinityProblem } from './affinity.mjs';
import { systemClock } from './clock.mjs';
import { STACK_ORDER, maskOf } from './series.mjs';

const problemOf = ({ mask, got }) => (got.ok ? affinityProblem({ wanted: mask, observed: got.value }) : got.reason);

/** `readback.json` of the series with this pair's read-back added (the earlier pairs' kept), written to a temp name and renamed. */
function keepReadback({ seriesDir, pair, seen, now }) {
  const file = path.join(seriesDir, 'readback.json');
  const before = existsSync(file) ? JSON.parse(readFileSync(file, 'utf8')) : { pairs: {} };
  const entry = { at: now().toISOString(), masks: Object.fromEntries(seen.map(s => [s.mask, s.got.value])) };
  writeFileSync(`${file}.tmp`, `${JSON.stringify({ ...before, pairs: { ...before.pairs, [pair]: entry } }, null, 2)}\n`);
  renameSync(`${file}.tmp`, file);
}

/** Probe every mask of the pair (`probe(cpus)` -> `ok(observed)` | `fail`). -> `ok(null)` | `fail(sentence)`. */
export async function maskReadBack({ ctx, probe, now = systemClock }) {
  const masks = [...new Set(STACK_ORDER.map(stack => maskOf({ draw: ctx.series.draw, pair: ctx.pair, stack })))];
  const seen = await Promise.all(masks.map(async mask => ({ mask, got: await probe(mask) })));
  const bad = seen.map(problemOf).find(Boolean);
  if (bad) return fail(bad);
  keepReadback({ seriesDir: ctx.seriesDir, pair: ctx.pair, seen, now });
  return ok(null);
}
