// The test seam of the bench half (step 2b, code round): the library exports no entry point that scans the bench repository alone, because a check that scans one repository is a pass it did not earn. The tests of the bench half's own
// behaviour (the tiers over its files and its branch range, the ids, the baseline) call it through this file. Not a test file.
import { benchHalf, commandMaterial } from '../lib/leak.mjs';
import { noRecord } from './quick-leak-fixture.mjs';

/** The bench half alone: the material of the command, then the bench repository's scan. `{ exit, message? }`, as `leakcheckCommand` was. */
export async function scanBenchHalf({ values, io, explore, say }) {
  const seamed = { frozenFile: noRecord(), ...io };
  const material = await commandMaterial({ values, io: seamed, explore });
  if (!material.ok) return { exit: 4, message: material.reason };
  return benchHalf({ values, io: seamed, material: material.value, say });
}
