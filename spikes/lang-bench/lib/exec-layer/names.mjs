// E1.S8b: the owned names of one layer run (common.test-leftovers: `testsToDelete-<owner>-bench-<what>-<id>`, at most 63 characters). One run has ONE id, drawn by the owned helper for the
// run folder; every other thing the run makes carries it: the scopes `…-lsc-<id>-<case>` and the netns holder `…-lhold-<id>` (its first argument, set with `exec -a`). The run folder's claim
// (`<run>.owner`, the pid of the layer process) is the run marker: a sweep judges every resource of the run by it, and a resource whose name has no run folder is unmarked and left alone.
import path from 'node:path';

export const RUN_WHAT = 'lrun';
export const SCOPE_WHAT = 'lsc';
export const HOLDER_WHAT = 'lhold';
export const MAX_NAME = 63;

/** The id of a run folder: what follows `testsToDelete-<owner>-<token>-lrun-`. */
export const runIdOf = (owned, runDir) => path.basename(runDir).slice(owned.ownedPrefix(RUN_WHAT).length);
/** The unit name without its case: `…-lsc-<id>`; the unit of a case is `<this>-<case id>`. */
export const unitPrefixOf = (owned, id) => `${owned.ownedPrefix(SCOPE_WHAT)}${id}`;
export const holderNameOf = (owned, id) => `${owned.ownedPrefix(HOLDER_WHAT)}${id}`;

/** The case ids whose unit name would pass 63 characters (rule §1: a name is never truncated, it is refused before anything exists). */
export const tooLongForUnit = (unitPrefix, caseIds) => caseIds.filter(id => `${unitPrefix}-${id}`.length > MAX_NAME);
