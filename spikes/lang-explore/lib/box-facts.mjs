// What a scoring box had for its environment, as a stored fact of every completed pass (F3.S1 (a), PR 3, item 11; T-L14, T-L32): the keys, the two per-case heap keys with their values (D55: .NET `DOTNET_GCHeapHardLimit` and Node
// `NODE_OPTIONS=--max-old-space-size`, derived from the registered memory value) and the culture (`LANG`, `LC_ALL`). The layer builds the box's environment with `boxEnv` from the stack, the toolchain folders and the case's memory limit, and nothing
// else; this module calls the same function with the same inputs, so the fact names what the layer built. A field that cannot be taken is `{ known: false, why }`, never a missing key.
import { boxEnv, heapKeys } from '../../lang-bench/lib/exec-layer/env.mjs';

const SOURCE = 'the layer\'s own boxEnv, from the stack, the toolchain folders and the registered memory limit of the pass';

/** `{ known: true, source, envKeys, heap, LANG, LC_ALL }` for the box of `stack` at the memory limit `memoryBytes`, or `{ known: false, why }`. */
export function scoringBoxFacts({ stack, dirs, memoryBytes }) {
  try {
    const env = boxEnv({ dirs, stack, memoryMaxBytes: memoryBytes });
    const heap = Object.fromEntries(Object.keys(heapKeys(stack, memoryBytes)).map(key => [key, env[key]]));
    return { known: true, source: SOURCE, envKeys: Object.keys(env).sort(), heap, LANG: env.LANG ?? null, LC_ALL: env.LC_ALL ?? null };
  } catch (e) {
    return { known: false, why: `the scoring box's environment cannot be built: ${e.message}` };
  }
}
