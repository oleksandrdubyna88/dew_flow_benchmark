// The frozen record's three files and the two inputs beside them, from the repository root (F3.S1 (a), PR 4): what `stageGuard` reads and what the identity readers need of the record. One list of paths.
import { readFileSync } from 'node:fs';
import path from 'node:path';

/** The three files `freezeProblems` reads (`lib/freeze-consumers.mjs`), from the repository's root. */
export const RECORD_PATHS = Object.freeze({
  specStates: 'spikes/lang-bench/inputs/specs.d/spec-states.json',
  heldoutFrozen: 'spikes/lang-explore/inputs/heldout-frozen.json',
  memoryLimit: 'spikes/lang-explore/inputs/memory-limit.json',
});
export const PACKAGES_PATH = 'spikes/lang-explore/inputs/packages.json';
export const HASHES_PATH = 'spikes/lang-bench/inputs/hashes-lang.txt';
export const PREREG_DIR = 'spikes/lang-explore/inputs/prereg';

/** One file of the record: `{ name, value }` when it is read, `{ problem }` (a sentence) when it cannot be. */
function readOne({ repo, name, rel }) {
  try {
    return { name, value: JSON.parse(readFileSync(path.join(repo, rel), 'utf8')) };
  } catch (e) {
    return { problem: `${rel} cannot be read (${e.code ?? e.message}): the frozen record is not there` };
  }
}

/** `{ record, problems }`: the three files parsed (`record[name]` is absent for a file that cannot be read) and a sentence for each that cannot. */
export function readRecord(repo) {
  const read = Object.entries(RECORD_PATHS).map(([name, rel]) => readOne({ repo, name, rel }));
  return { record: Object.fromEntries(read.filter(r => r.name !== undefined).map(r => [r.name, r.value])), problems: read.filter(r => r.problem !== undefined).map(r => r.problem) };
}
