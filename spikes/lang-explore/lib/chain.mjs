// Run steps that each return `ok(value)` or a `fail`, in order: the values of all of them, or the first failure (the later steps are not started).
import { ok } from '../../minigit-bench/lib/result.mjs';

/** `steps` are functions (sync or async) -> `ok([value, ...])` | the first `fail`. */
export async function allOk(steps) {
  const values = [];
  for (const step of steps) {
    const r = await step();
    if (!r.ok) return r;
    values.push(r.value);
  }
  return ok(values);
}
