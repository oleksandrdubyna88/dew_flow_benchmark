// The gated bytes of the six specs, as the engine pins them (F2.S2 (d), the coai code round 3): `spikes/lang-explore/inputs/explore.json` holds, per task, `tasks.NN.spec.sha256`, the sha256 of the GATED text (its
// Freeze tag row still the placeholder). The freeze may only freeze that text, so a spec edited after its gate is refused before the freeze commit, by `plan` (the checkout), `check-commit` and `check-tag` (the commit before
// the freeze commit) and `record` (through `check-tag`). A superseding freeze commit starts from specs whose row already holds an earlier tag, so the row is set back to the placeholder before the hash is taken.
// lang-bench imports nothing of lang-explore's CODE (a test of lang-explore scans for imports); this file reads lang-explore's DATA file by path, which is a read, not an import. `--pins <file>` names another file (tests).
import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { TAG_LABEL, withRowValue } from './freeze-rows.mjs';

export const DEFAULT_PINS = fileURLToPath(new URL('../../lang-explore/inputs/explore.json', import.meta.url));
const SHA256 = /^[0-9a-f]{64}$/;

/** ok({ file, specs: Map(task -> sha256) }) from the pins' data file: every task that has `spec.sha256`; a file that cannot be read or is not JSON is a fail. */
export function loadSpecPins(file = DEFAULT_PINS) {
  let tasks;
  try {
    tasks = JSON.parse(readFileSync(file, 'utf8')).tasks ?? {};
  } catch (error) {
    return fail(`the engine's pins ${file} cannot be read (${error.code ?? error.message}): the gated bytes of the six specs are not known, so none is frozen`);
  }
  const entries = Object.entries(tasks).flatMap(([task, entry]) => (SHA256.test(entry?.spec?.sha256 ?? '') ? [[task, entry.spec.sha256]] : []));
  return ok({ file, specs: new Map(entries) });
}

/** The sentences for one spec's bytes before the freeze commit: with its Freeze tag row set to the placeholder they must hash to the sha256 the pins name for the task. */
export function gatedProblems({ spec, bytes, facts, pins }) {
  const pinned = pins.specs.get(spec.task);
  if (pinned === undefined) return [`${spec.path}: ${pins.file} pins no spec sha256 for task ${spec.task}`];
  const gated = withRowValue(bytes, facts.header.placeholders[TAG_LABEL]);
  if (!gated.ok) return [`${spec.path}: ${gated.reason}`];
  const actual = createHash('sha256').update(gated.value).digest('hex');
  return actual === pinned ? [] : [`${spec.path} before the freeze commit has sha256 ${actual} (its Freeze tag row set to the placeholder), not the gated text's ${pinned} that ${pins.file} pins for task ${spec.task}: a spec edited after its gate cannot be frozen`];
}
