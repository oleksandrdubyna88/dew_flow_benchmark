// The registry after the freeze (F2.S2 (f), fix round): the row of a task whose spec is in an ACCEPTED freeze is the gated text's `specSha256`, which lang-bench's freeze checks read (`freeze-pins.mjs`: `check-tag` and a rerun of `record`).
// A package rerun after the freeze runs on the engine's pin, which is the FROZEN text, so its row would carry the frozen hash and overwrite the gated one. This guard refuses it: a frozen task's row is never replaced and never added;
// only an identical row (the same identity) is a no-op. The way to a different row is a superseding freeze (a later-date tag on a new freeze commit, lang-bench's module doc), never a registration. The record is lang-bench's DATA
// (`inputs/specs.d/spec-states.json`, read by path: lang-explore may read it, lang-bench never imports this code); a missing file is no freeze, one that cannot be read is a refusal.
import { existsSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { INPUTS } from './paths.mjs';

export const STATES_FILE = path.join(INPUTS, '..', '..', 'lang-bench', 'inputs', 'specs.d', 'spec-states.json');

/** `ok(record | null)`: the parsed record, `null` when there is no file (no freeze), a `fail` when it cannot be read. */
function readStates(file) {
  if (!existsSync(file)) return ok(null);
  try {
    return ok(JSON.parse(readFileSync(file, 'utf8')));
  } catch (error) {
    return fail(`${path.basename(file)} cannot be read (${error.code ?? error.message}): whether the spec of a task is in an accepted freeze is not known, so no row is written`);
  }
}

const isFrozen = (states, id) => states?.freeze?.state === 'accepted' && states?.specs?.[id] !== undefined;

/** The sentence refusing the write of `identity` as the row of `task`, or null: the task's spec is in an accepted freeze and its row is not already that identity. `row` is what the registry holds now. */
export function frozenRowProblem({ task, row, identity, statesFile = STATES_FILE }) {
  const read = readStates(statesFile);
  if (!read.ok) return read.reason;
  if (!isFrozen(read.value, task.id) || row?.identity === identity) return null;
  return `the spec of task ${task.id} is in the accepted freeze ${read.value.freeze.tag}: its row in packages.json is the gated text's, which the freeze's checks read, so a row of it is neither replaced nor added (a rerun would carry the frozen spec hash); a different row needs a superseding freeze (a later-date tag on a new freeze commit), not a registration`;
}
