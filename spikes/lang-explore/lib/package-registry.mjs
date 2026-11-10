// The registry of the packages and the registration of the memory value's inputs (F2.S1, P8): `inputs/packages.json`, `{ "NN": entry }`, counts, hashes and durations only (never a case id, a trap id, a path or a sentence). An entry is the
// task's identity: the spec's sha256 and FORMAT's, the held-out tree hash, the manifest's, the sealed archive's hash (once sealed), the two references' tree hashes, the witnesses' tree hash, the counts, the memory value both references
// passed at, the F-LOAD numbers, the gate verdict and date, THE RUNNER AND EXECUTION-LAYER PINS the validation ran under, and one identity (a sha256 over these, `packageIdentity`). `registerPackage` refuses while an item is red, the record is
// older than the material, the pins moved since the run, or the archive's hash differs from `heldout-frozen.json`'s; for a later task (04, 06, 07, 08, 10) it also refuses until all five have their S recorded for their current suites.
import { createHash } from 'node:crypto';
import { existsSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { systemClock } from './clock.mjs';
import { registeredEntry, registryMismatchProblem } from './heldout-registry.mjs';
import { FROZEN_FILE, casesTreeHash } from './heldout-quick.mjs';
import { INPUTS } from './paths.mjs';
import { materialOf, packageIdentity } from './package-quick.mjs';
import { sOfSuite } from './package-record.mjs';
import { taskPaths } from './tasks.mjs';
import { replaceJson } from './package-write.mjs';

export const PACKAGES_FILE = path.join(INPUTS, 'packages.json');
const SPECS_D = path.join(INPUTS, '..', '..', 'lang-bench', 'inputs', 'specs.d');
/** The five tasks whose S is counted before any of them registers (the plan round's finding 2, F2's team lead note 2); task 05 runs first and decides alone. */
export const LATER_TASKS = Object.freeze(['04', '06', '07', '08', '10']);

const sha256File = file => createHash('sha256').update(readFileSync(file)).digest('hex');

/** The runner and execution-layer pins as committed now: the sha256 of `specs.d/runner.json` and `specs.d/exec-layer.json`. */
export const pinsNow = (dir = SPECS_D) => ({ runnerSha256: sha256File(path.join(dir, 'runner.json')), execLayerSha256: sha256File(path.join(dir, 'exec-layer.json')) });

/** The items a package needs, in the order of the run. */
export const REQUIRED_ITEMS = Object.freeze(['gate', 'suite', 'refs', 'load', 'witnesses']);

/** The names of the required items of a record that are not green (an item that was not run is not green). */
const redItems = record => REQUIRED_ITEMS.filter(name => record.items?.[name]?.ok !== true);

const MATERIAL_NAMES = Object.freeze({ specSha256: 'the spec', formatSha256: 'FORMAT', heldoutTreeSha256: 'the held-out cases', manifestSha256: 'the manifest', refsTreeSha256: 'a reference', witnessesTreeSha256: 'the witnesses', twinsTreeSha256: 'the twins' });

/** What of the material moved since the record was made: names, never values. */
const movedMaterial = (recorded, now) => Object.entries(MATERIAL_NAMES).filter(([k]) => JSON.stringify(recorded[k]) !== JSON.stringify(now[k])).map(([, name]) => name);

const pinsProblem = (recorded, now) => (recorded?.runnerSha256 === now.runnerSha256 && recorded?.execLayerSha256 === now.execLayerSha256 ? null : 'the runner or the execution-layer pin moved since the run: the references and witnesses must run again under the committed pins');

/** The suite tree hash a task has now: its clear folder's, else its sealed entry's; `null` when neither. */
function currentSuiteHash({ home, task, frozenFile }) {
  const cases = path.join(taskPaths(task, home).heldoutDir, 'cases');
  if (existsSync(cases)) { const h = casesTreeHash(cases); return h.ok ? h.value : null; }
  const entry = registeredEntry({ frozenFile, task });
  return entry.ok ? entry.value?.treeSha256 ?? null : null;
}

/** The sentence refusing the registration of a later task until all five S are recorded for their current suites, or null. `tasks` are the five pins entries (`{ id, ... }`). */
export function sGateProblem({ home, task, tasks, frozenFile = FROZEN_FILE }) {
  if (!LATER_TASKS.includes(task.id)) return null;
  const missing = tasks.filter(t => LATER_TASKS.includes(t.id)).filter(t => { const h = currentSuiteHash({ home, task: t, frozenFile }); return h === null || sOfSuite({ home, task: t, treeSha256: h }) === null; }).map(t => t.id);
  return missing.length === 0 ? null : `S, the largest expected reply body, is not recorded for the current suite of task ${missing.join(', ')}: run \`package --task NN --only suite\` for each; the five later tasks register together, so the chunk decision is taken once, for the largest of the five`;
}

const entryOf = ({ record, material, archiveSha256, identity, now }) => ({
  specSha256: material.specSha256, formatSha256: material.formatSha256, heldoutTreeSha256: material.heldoutTreeSha256, manifestSha256: material.manifestSha256, ...(archiveSha256 === undefined ? {} : { archiveSha256 }),
  refsTreeSha256: material.refsTreeSha256, witnessesTreeSha256: material.witnessesTreeSha256, twinsTreeSha256: material.twinsTreeSha256, counts: material.counts, memoryBytes: record.memoryBytes,
  referenceVerdicts: record.referenceVerdicts, load: record.load, loadDurationS: record.load?.durationS ?? null, pins: record.pins, gate: record.gate, identity, validatedOn: now().toISOString().slice(0, 10),
});

/** The sorted-keys text of the whole registry with `entry` set for `id`. */
const withEntry = (packages, id, entry) => Object.fromEntries(Object.entries({ ...packages, [id]: entry }).sort(([a], [b]) => (a < b ? -1 : 1)));

const readPackagesFile = file => { try { return existsSync(file) ? JSON.parse(readFileSync(file, 'utf8')) : {}; } catch { return null; } };

/** The sentence refusing a registration, in the order of the rules, or null. Every item of `REQUIRED_ITEMS` must be there and green; the seal reads the same rules, so a seal is never made over a package that could not register. */
export function registrationProblem({ record, pkg, task, home, tasks, frozenFile = FROZEN_FILE, packagesFile = PACKAGES_FILE, pinsFn = pinsNow }) {
  const red = redItems(record);
  if (red.length > 0) return `the package of task ${task.id} is not green: ${red.join(', ')} ${red.length === 1 ? 'is' : 'are'} red or not run`;
  const moved = movedMaterial(record.material, materialOf(pkg));
  if (moved.length > 0) return `the record is older than the material: ${moved.join(', ')} changed since the run, so it is validated again before it is registered`;
  return pinsProblem(record.pins, pinsFn()) ?? sGateProblem({ home, task, tasks, frozenFile }) ?? registryMismatchProblem({ task, packagesFile, frozenFile });
}

/**
 * Register the package of `task`: `record` is a complete run's `record.json`, `pkg` `loadPackage`'s value now. -> `ok({ state: 'registered' | 'unchanged', identity })` | `fail(sentence)`. The entry is written whole, the file rewritten with sorted
 * keys; an entry with the same identity is a no-op. The archive's hash joins the entry once the task is sealed (`heldout-frozen.json` is the registry of the seal): the entry then has a new identity and replaces the old one (a re-seal is a
 * new identity, never an edit of one).
 */
export function registerPackage({ home, task, tasks = [], record, pkg, packagesFile = PACKAGES_FILE, frozenFile = FROZEN_FILE, pinsFn = pinsNow, now = systemClock }) {
  const problem = registrationProblem({ record, pkg, task, home, tasks, frozenFile, packagesFile, pinsFn });
  if (problem) return fail(problem);
  const sealed = registeredEntry({ frozenFile, task });
  if (!sealed.ok) return sealed;
  const material = { ...materialOf(pkg), archiveSha256: sealed.value?.archiveSha256 };
  const identity = packageIdentity({ ...material, gate: record.gate, memoryBytes: record.memoryBytes, loadDurationS: record.load?.durationS ?? null, pins: record.pins });
  return writeEntryOf({ task, packagesFile, identity, entry: entryOf({ record, material, archiveSha256: material.archiveSha256, identity, now }) });
}

/** Write the entry of `task` unless the registry already holds one with this identity. -> `ok({ state, identity })` | `fail`. */
function writeEntryOf({ task, packagesFile, identity, entry }) {
  const packages = readPackagesFile(packagesFile);
  if (packages === null) return fail(`${path.basename(packagesFile)} cannot be read: nothing is written over it`);
  if (packages[task.id]?.identity === identity) return ok({ state: 'unchanged', identity });
  replaceJson({ file: packagesFile, value: withEntry(packages, task.id, entry) });
  return ok({ state: 'registered', identity });
}
