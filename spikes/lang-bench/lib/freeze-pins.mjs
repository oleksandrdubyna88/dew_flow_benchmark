// The gated bytes of the six specs (F2.S2 (d), the coai code round 3; the source moved in F2.S2 (f), 2026-11-19, consult c53c6f56). The freeze may only freeze the text each spec had when it was VALIDATED and GATED, so a spec edited after its
// gate is refused before the freeze commit, by `plan` (the checkout), `check-commit` and `check-tag` (the commit before the freeze commit) and `record` (through `check-tag`). That text's sha256 is the `specSha256` of the task's
// registered package in `spikes/lang-explore/inputs/packages.json` (one package per task, each validated on the gated text, its Freeze tag row still the placeholder). It is NOT `explore.json`'s `tasks.NN.spec.sha256`: that
// field is the engine's pin, which names the gated text before the freeze and the FROZEN text (the row set) after it, so the two files hold two meanings and this reader takes the registry's. Strict: a task without a registered
// package, or a package without a well-formed `specSha256`, is a refusal; there is no fallback to the engine's pin. A superseding freeze commit starts from specs whose row already holds an earlier tag, so the row is set back
// to the placeholder before the hash is taken. lang-bench imports nothing of lang-explore's CODE (a test of lang-explore scans for imports); this file reads lang-explore's DATA file by path, which is a read, not an import.
// `--pins <file>` names another file (tests), in the shape of `packages.json`: `{ "NN": { "specSha256": "<64 hex>" } }`.
import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { TAG_LABEL, withRowValue } from './freeze-rows.mjs';

export const DEFAULT_PINS = fileURLToPath(new URL('../../lang-explore/inputs/packages.json', import.meta.url));
const SHA256 = /^[0-9a-f]{64}$/;

const isRecord = value => value !== null && typeof value === 'object' && !Array.isArray(value);

/** The registry's text as an object: `ok(packages)`, or a `fail` for a file that cannot be read, is not JSON or is not an object of packages. */
function readPackages(file) {
  let packages;
  try {
    packages = JSON.parse(readFileSync(file, 'utf8'));
  } catch (error) {
    return fail(`the registered packages ${file} cannot be read (${error.code ?? error.message}): the gated bytes of the six specs are not known, so none is frozen`);
  }
  return isRecord(packages) ? ok(packages) : fail(`the registered packages ${file} are not an object of packages: the gated bytes of the six specs are not known, so none is frozen`);
}

/** ok({ file, specs: Map(task -> specSha256) }) from the registry of packages: each package's `specSha256` is the gated text it was validated on. A package whose `specSha256` is not 64 lower-case hex refuses the whole read. */
export function loadSpecPins(file = DEFAULT_PINS) {
  const read = readPackages(file);
  if (!read.ok) return read;
  const entries = Object.entries(read.value);
  const malformed = entries.filter(([, entry]) => !SHA256.test(entry?.specSha256 ?? '')).map(([task]) => task);
  if (malformed.length > 0) return fail(`the registered packages ${file} hold no well-formed specSha256 for task ${malformed.join(', ')}: the gated bytes of the specs are not known, so none is frozen`);
  return ok({ file, specs: new Map(entries.map(([task, entry]) => [task, entry.specSha256])) });
}

/** The sentences for one spec's bytes before the freeze commit: with its Freeze tag row set to the placeholder they must hash to the `specSha256` the task's registered package names. */
export function gatedProblems({ spec, bytes, facts, pins }) {
  const pinned = pins.specs.get(spec.task);
  if (pinned === undefined) return [`${spec.path}: ${pins.file} registers no package for task ${spec.task}, so its gated bytes are not known`];
  const gated = withRowValue(bytes, facts.header.placeholders[TAG_LABEL]);
  if (!gated.ok) return [`${spec.path}: ${gated.reason}`];
  const actual = createHash('sha256').update(gated.value).digest('hex');
  return actual === pinned ? [] : [`${spec.path} before the freeze commit has sha256 ${actual} (its Freeze tag row set to the placeholder), not the gated text's ${pinned} that the package of task ${spec.task} in ${pins.file} records as its specSha256: a spec edited after its gate cannot be frozen`];
}
