// What the freeze tools take as given (F2.S2 (d)): the six quick tasks, the step-2a pin of FORMAT (the B entry of `inputs/specs.d/format.json`, never a hash taken from a freeze commit), and the six spec files a
// tree holds. A problem list is carried on a failed result as `problems`; `problemsOf` reads it back.
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { ENTRY_TASKS, readEntries } from './conformance-record.mjs';

export const SIX = ENTRY_TASKS.B;
export const INPUTS_DIR = fileURLToPath(new URL('../inputs', import.meta.url));
const SPEC_FILE = /^todo\/tasks\/(\d{2}-[a-z0-9]+(?:-[a-z0-9]+)*)\.md$/;
const SHA256 = /^[0-9a-f]{64}$/;

/** A failed result that carries every problem found (`problems`), its `reason` being the first. */
export const failWith = problems => fail(problems[0], { problems });

/** True for a failed result that is the environment's (git, the network, a file system), not a rule broken: `env: true` of this module's own, or `kind: 'env'` of the push check's git launcher. */
export const isEnv = result => !result.ok && (result.env === true || result.kind === 'env');

/** The sentences of a failed result: its `problems`, or its one `reason`. */
export const problemsOf = result => result.problems ?? [result.reason];

const sameList = (left, right) => left.length === right.length && left.every((item, index) => item === right[index]);

/** ok({ sha256, tasks }): the B entry of the record file with its sha256 (`tasks` as the record holds them); a failure says what the record lacks. */
export function loadPin(recordFile) {
  const record = readEntries(recordFile);
  if (!record.ok) return fail(record.reason);
  const entry = record.value.B;
  if (typeof entry?.sha256 !== 'string' || !SHA256.test(entry.sha256)) return fail(`${recordFile} has no entry B with a sha256 of 64 hexadecimal digits: the step-2a pin of FORMAT is missing`);
  return ok({ sha256: entry.sha256, tasks: Array.isArray(entry.tasks) ? entry.tasks : [], file: recordFile });
}

/** The sentence when the pin's task list is not exactly the six, else null. */
export const pinTaskProblem = pin => (sameList(pin.tasks, SIX) ? null : `the task list of entry B in ${pin.file} is [${pin.tasks.join(', ')}], not exactly the six (${SIX.join(', ')})`);

/** ok([{ task, slug, path }]): the one spec file of each of the six tasks in `tree` (a Map from `treeAt`), in the six's order; a task with no spec, or with two, is a problem naming it. */
export function specSet(tree) {
  const names = [...tree.keys()].flatMap(file => (SPEC_FILE.test(file) && tree.get(file).type === 'blob' ? [{ path: file, slug: SPEC_FILE.exec(file)[1] }] : []));
  const found = SIX.map(task => ({ task, hits: names.filter(entry => entry.slug.startsWith(`${task}-`)) }));
  const problems = found.filter(entry => entry.hits.length !== 1).map(entry => `task ${entry.task} has ${entry.hits.length === 0 ? 'no spec file' : `${entry.hits.length} spec files (${entry.hits.map(hit => hit.path).join(', ')})`} under todo/tasks/`);
  if (problems.length > 0) return failWith(problems);
  return ok(found.map(entry => ({ task: entry.task, slug: entry.hits[0].slug, path: entry.hits[0].path })));
}

/** The folder of a bench's inputs: `--inputs`, else the spike's own. */
export const inputsDirOf = given => path.resolve(given ?? INPUTS_DIR);
