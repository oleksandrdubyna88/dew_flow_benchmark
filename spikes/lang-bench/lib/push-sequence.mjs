// Runs steps in order, each given the last value, stopping at the first fail: `ok(value)` | the first `fail`. A step may be async.
import { ok } from '../../minigit-bench/lib/result.mjs';

export async function sequence([step, ...rest], value = null) {
  if (step === undefined) return ok(value);
  const result = await step(value);
  return result.ok ? sequence(rest, result.value) : result;
}
