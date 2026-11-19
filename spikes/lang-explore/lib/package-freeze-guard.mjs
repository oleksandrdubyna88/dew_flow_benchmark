// The registry after the freeze (F2.S2 (f), fix round): the row of a task whose spec is in an ACCEPTED freeze is the gated text's `specSha256`, which lang-bench's freeze checks read (`freeze-pins.mjs`: `check-tag` and a rerun of `record`).
// A package rerun after the freeze runs on the engine's pin, which is the FROZEN text, so its row would carry the frozen hash and overwrite the gated one. This guard refuses it: a frozen task's row is never replaced and never added;
// only an identical row (the same identity) is a no-op. The way to a different row is a superseding freeze (a later-date tag on a new freeze commit, lang-bench's module doc), never a registration. The record is lang-bench's DATA
// (`inputs/specs.d/spec-states.json`, read by path: lang-explore may read it, lang-bench never imports this code). ONLY an absent file is no freeze; a record that cannot be read, or is valid JSON but not the shape `freeze.mjs record` writes
// (the fields this guard reads: `freeze.state` "accepted", `freeze.tag`, and `specs.<NN>` rows with `state` "frozen" and a `sha256`), refuses every registration and says what is malformed, so a damaged record never silently turns the guard off. The same sentence
// is given at once by `explore.mjs package --register` before the run starts (`frozenTaskProblem`). Superseding an ACCEPTED freeze is not a path of this plan: it would need its own design (the gated hashes would have to be preserved).
import { existsSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { INPUTS } from './paths.mjs';

export const STATES_FILE = path.join(INPUTS, '..', '..', 'lang-bench', 'inputs', 'specs.d', 'spec-states.json');

const HEX64 = /^[0-9a-f]{64}$/;
const isObject = value => value !== null && typeof value === 'object' && !Array.isArray(value);
const isText = value => typeof value === 'string' && value !== '';

const freezeShape = freeze => {
  if (!isObject(freeze)) return ['no `freeze` object'];
  return [...(freeze.state === 'accepted' ? [] : [`freeze.state is ${JSON.stringify(freeze.state)}, not "accepted"`]), ...(isText(freeze.tag) ? [] : ['freeze.tag is not text'])];
};
const rowShape = ([id, row]) => (isObject(row) && row.state === 'frozen' && HEX64.test(row.sha256 ?? '') ? [] : [`specs.${id} is not a frozen spec row (state "frozen" and a sha256)`]);
const specsShape = specs => (isObject(specs) && Object.keys(specs).length > 0 ? Object.entries(specs).flatMap(rowShape) : ['no `specs` object with rows']);

/** The sentences saying what is malformed in a parsed record; `[]` for the shape the guard reads. */
export const shapeProblems = states => (isObject(states) ? [...freezeShape(states.freeze), ...specsShape(states.specs)] : ['the record is not an object']);

/** `ok(record | null)`: the checked record, `null` when there is no file (no freeze), a `fail` when it cannot be read or is malformed. */
function readStates(file) {
  if (!existsSync(file)) return ok(null);
  const name = path.basename(file);
  let states;
  try {
    states = JSON.parse(readFileSync(file, 'utf8'));
  } catch (error) {
    return fail(`${name} cannot be read (${error.code ?? error.message}): whether the spec of a task is in an accepted freeze is not known, so no row is written`);
  }
  const problems = shapeProblems(states);
  return problems.length === 0 ? ok(states) : fail(`${name} is malformed (${problems.join('; ')}): whether the spec of a task is in an accepted freeze is not known, so no row is written`);
}

const frozenSentence = (id, tag) => `the spec of task ${id} is in the accepted freeze ${tag}: its row in packages.json is the gated text's, which the freeze's checks read, so a row of it is neither replaced nor added (a rerun would carry the frozen spec hash); a different row needs a superseding freeze (a later-date tag on a new freeze commit), not a registration`;

/** The sentence refusing any registration of `task`, or null: the preflight of `package --register`, before the run starts (the row is not known yet, so no row is "identical"). */
export function frozenTaskProblem({ task, statesFile = STATES_FILE }) {
  const read = readStates(statesFile);
  if (!read.ok) return read.reason;
  return read.value?.specs?.[task.id] === undefined ? null : frozenSentence(task.id, read.value.freeze.tag);
}

/** The sentence refusing the write of `identity` as the row of `task`, or null: the task's spec is in the accepted freeze and its row is not already that identity. `row` is what the registry holds now. */
export function frozenRowProblem({ task, row, identity, statesFile = STATES_FILE }) {
  return row?.identity === identity && readStates(statesFile).ok ? null : frozenTaskProblem({ task, statesFile });
}
