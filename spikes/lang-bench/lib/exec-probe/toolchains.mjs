// The pinned toolchain directories as the spike's scripts need them: `{ dotnetSdk, node, typescript }` under the home, from inputs/stacks-lang.json.
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { loadStacksLang } from '../toolchain-pins.mjs';

/** `storeDirs` is `lib/preflight-box.mjs`'s; the spike folder holds `inputs/stacks-lang.json`. Throws when the pins cannot be read. */
export function toolchainsOf(spike, storeDirs, home) {
  const stacks = loadStacksLang(JSON.parse(readFileSync(path.join(spike, 'inputs', 'stacks-lang.json'), 'utf8')));
  if (!stacks.ok) throw new Error(stacks.reason);
  return storeDirs(stacks.value, path.join(home, ...stacks.value.storeRoot));
}
