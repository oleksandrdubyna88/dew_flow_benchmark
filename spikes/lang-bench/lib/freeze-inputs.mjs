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

/** `[]` when `holds`, else the one sentence: a rule written as data, not as a branch. */
export const unless = (holds, sentence) => (holds ? [] : [sentence]);

/** Runs `steps` in order over a state: each is `async state -> ok(more) | fail`, `more` joins the state, and the first fail ends the run; ok(the whole state) at the end. */
export async function runSteps(steps, state) {
  let acc = state;
  for (const step of steps) {
    const result = await step(acc);
    if (!result.ok) return result;
    acc = { ...acc, ...result.value };
  }
  return ok(acc);
}

/** The sentences of a failed result: its `problems`, or its one `reason`. */
export const problemsOf = result => result.problems ?? [result.reason];

const sameList = (left, right) => left.length === right.length && left.every((item, index) => item === right[index]);

const hasSha = entry => typeof entry?.sha256 === 'string' && SHA256.test(entry.sha256);
const tasksOf = entry => (Array.isArray(entry.tasks) ? entry.tasks : []);

/** ok({ sha256, tasks }): the B entry of the record file with its sha256 (`tasks` as the record holds them); a failure says what the record lacks. */
export function loadPin(recordFile) {
  const record = readEntries(recordFile);
  if (!record.ok) return fail(record.reason);
  const entry = record.value.B;
  return hasSha(entry) ? ok({ sha256: entry.sha256, tasks: tasksOf(entry), file: recordFile }) : fail(`${recordFile} has no entry B with a sha256 of 64 hexadecimal digits: the step-2a pin of FORMAT is missing`);
}

/** The sentence when the pin's task list is not exactly the six, else null. */
export const pinTaskProblem = pin => (sameList(pin.tasks, SIX) ? null : `the task list of entry B in ${pin.file} is [${pin.tasks.join(', ')}], not exactly the six (${SIX.join(', ')})`);

/** The spec files of a tree: `{ path, slug }` for each blob that is `todo/tasks/NN-slug.md`. */
const specNames = tree => [...tree.keys()].filter(file => SPEC_FILE.test(file) && tree.get(file).type === 'blob').map(file => ({ path: file, slug: SPEC_FILE.exec(file)[1] }));

/** The sentence when a task has no spec file or more than one, else null. */
function countProblem({ task, hits }) {
  if (hits.length === 0) return `task ${task} has no spec file under todo/tasks/`;
  return hits.length === 1 ? null : `task ${task} has ${hits.length} spec files (${hits.map(hit => hit.path).join(', ')}) under todo/tasks/`;
}

/** ok([{ task, slug, path }]): the one spec file of each of the six tasks in `tree` (a Map from `treeAt`), in the six's order; a task with no spec, or with two, is a problem naming it. */
export function specSet(tree) {
  const names = specNames(tree);
  const found = SIX.map(task => ({ task, hits: names.filter(entry => entry.slug.startsWith(`${task}-`)) }));
  const problems = found.map(countProblem).filter(problem => problem !== null);
  return problems.length > 0 ? failWith(problems) : ok(found.map(entry => ({ task: entry.task, slug: entry.hits[0].slug, path: entry.hits[0].path })));
}

/** The folder of a bench's inputs: `--inputs`, else the spike's own. */
export const inputsDirOf = given => path.resolve(given ?? INPUTS_DIR);
