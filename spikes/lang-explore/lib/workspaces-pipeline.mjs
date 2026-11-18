// A command made of steps that each answer `ok(values)` or `fail`: the values of every step are added to the state the next one reads, and the first `fail` ends the run. Keeps each step small and the order readable.
import { ok } from '../../minigit-bench/lib/result.mjs';

/** Run `steps` (functions of the state, sync or async) in order. -> `ok(state)` with every step's values merged in | the first `fail`. */
export async function runSteps(steps, state) {
  let current = state;
  for (const step of steps) {
    const r = await step(current);
    if (!r.ok) return r;
    current = { ...current, ...r.value };
  }
  return ok(current);
}
