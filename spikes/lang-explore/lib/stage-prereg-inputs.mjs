// Where Stage 1's engine finds its registered inputs (F3.S1 (b), PR 5; named apart from PR 3's `stage-inputs.mjs`, which holds the series' own inputs.json shape): the one dated price file of the pre-registration folder (`prices-<yyyy-mm-dd>.json`, the day the prices were read; closed point 11) and the policy beside it.
// `crosscheckPolicyFrom` takes both paths, so a Stage 1 session is priced from these files and never from v1's defaults.
import { readdirSync } from 'node:fs';
import path from 'node:path';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { PREREG_DIR } from './stage-lists.mjs';

const PRICE_FILE = /^prices-\d{4}-\d{2}-\d{2}\.json$/;

/** The names of the dated price files of `dir`, sorted: `ok(names)` | `fail` naming the folder when it cannot be read. */
function priceNames(dir) {
  try {
    return ok(readdirSync(dir).filter(n => PRICE_FILE.test(n)).sort());
  } catch (e) {
    return fail(`${dir} cannot be read: ${e.code ?? e.message}`);
  }
}

/** The registered price file and policy under `root` (the repository): `ok({ prices, policy })` as absolute paths, or a `fail` when the folder holds no price file or more than one (a day is chosen once). */
export function registeredInputs(root) {
  const dir = path.join(root, PREREG_DIR);
  const listed = priceNames(dir);
  if (!listed.ok) return listed;
  const names = listed.value;
  if (names.length !== 1) return fail(`${dir} must hold exactly one prices-<yyyy-mm-dd>.json (found ${names.length}${names.length === 0 ? '' : `: ${names.join(', ')}`})`);
  return ok({ prices: path.join(dir, names[0]), policy: path.join(dir, 'crosscheck-policy.json') });
}
