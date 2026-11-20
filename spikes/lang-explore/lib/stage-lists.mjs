// Stage 1's lists (F3.S1 (b), PR 5; stories document section 5, "one pre-registration"): the registered files, the harness folders, the paths the series' own branch may take from `origin/main` and the policy's free fields. Pure data, no
// code: the guard (`stage-prereg.mjs`), the series' branch (`stage-branch.mjs`) and the generators (`prereg/`) read them from here, so each list has one source. Every path is from the repository's root.
import { V1_PREREG_CONFIG } from '../../minigit-bench/lib/prereg-guard.mjs';

/** The folder of the pre-registration's own files. */
export const PREREG_DIR = 'spikes/lang-explore/inputs/prereg';
const EXPLORE_INPUTS = 'spikes/lang-explore/inputs';
const BENCH_INPUTS = 'spikes/lang-bench/inputs';

const inPrereg = name => `${PREREG_DIR}/${name}`;

/**
 * The registered files. A text is one path; a folder entry (`{ dir, pattern, what, exactlyOne }`) is every file of that folder whose name matches: the one dated price file of the pre-registration folder (`prices-<yyyy-mm-dd>.json`,
 * named by the day it was read) and every spec file of `specs.d`. The files of the table of F3.S1 (b) come first, then the files outside that folder the pre-registration pins by path.
 */
export const STAGE_PREREG_FILES = Object.freeze([
  ...['statement.md', 'analysis-contract.md', 'prediction.md', 'prompts-lang.json', 'schedule.json', 'manifest.json', 'crosscheck-policy.json', 'pilot-rules.json', 'adjudication-rules.json', 'amendments.md', 'seed.sha256'].map(inPrereg),
  Object.freeze({ dir: PREREG_DIR, pattern: /^prices-\d{4}-\d{2}-\d{2}\.json$/, what: 'the price file', exactlyOne: true }),
  ...['explore.json', 'memory-limit.json', 'packages.json', 'heldout-frozen.json', 'growth.json', 'package-flags.json'].map(name => `${EXPLORE_INPUTS}/${name}`),
  ...['specs.json', 'hashes-lang.txt', 'cache-snapshots-lang.json', 'cache-recipe-lang.json', 'package-matrix.json'].map(name => `${BENCH_INPUTS}/${name}`),
  Object.freeze({ dir: `${BENCH_INPUTS}/specs.d`, pattern: /\.json$/, what: 'the spec files of specs.d' }),
  'spikes/minigit-bench/inputs/quiet.json',
]);

/** The files that carry a dated line or a revealed value after the tag: the one list of what a series' branch may take from `origin/main` (stories document section 3). `explore.json` only in its two cap fields, `quiet.json` and `package-flags.json` through their dated lines. */
export const AMENDABLE_PATHS = Object.freeze([inPrereg('amendments.md'), inPrereg('seed.txt'), inPrereg('crosscheck-policy.json'), `${EXPLORE_INPUTS}/explore.json`, 'spikes/minigit-bench/inputs/quiet.json', `${EXPLORE_INPUTS}/package-flags.json`]);

/**
 * The folders whose git tree ids are the harness identity: v1's six, then the folders of the language comparison's engine. The plan's list holds `spikes/lang-bench/runner` and `spikes/lang-bench/templates` (the quick run's runner and
 * templates, which PR 4's `runnerManifest` and `templates` identities read as a pin and as live bytes). `spikes/lang-explore/runner` (the olympiad judge) and `spikes/lang-explore/templates` are read by NO identity of PR 4's list, so
 * they are here as well (the team lead's rule of 2026-11-20: a folder no reader reads is added).
 */
export const STAGE_HARNESS_DIRS = Object.freeze([
  ...V1_PREREG_CONFIG.harnessDirs, 'spikes/lang-explore/lib', 'spikes/lang-explore/prereg', 'spikes/lang-explore/rehearsal', 'spikes/lang-explore/runner', 'spikes/lang-explore/templates',
  'spikes/lang-bench/lib', 'spikes/lang-bench/runner', 'spikes/lang-bench/templates',
  // the code the harness imports from outside its own folders: the freeze tools (`workspace-quick.mjs`, `freeze-plan.mjs`) and the shared rules' submodule (the scoring code: `owned.mjs`, `pass-seams.mjs`, `package-quick.mjs`). A submodule is one tree id
  // here: the commit the superproject records, which the guard asks the checkout to be at and clean (`submoduleProblems`).
  'spikes/lang-bench/tools', '.agents/conventions',
]);
/** The folders whose top-level `*.mjs` files are in the harness identity too. */
export const STAGE_HARNESS_TOP_DIRS = Object.freeze(['spikes/minigit-bench/', 'spikes/lang-explore/', 'spikes/lang-bench/']);
/** The part of a configuration `harnessIdentity` reads: the one place both the guard and the series' own reader take the Stage 1 list from. */
export const STAGE_HARNESS_LISTS = Object.freeze({ harnessDirs: STAGE_HARNESS_DIRS, harnessTopDirs: STAGE_HARNESS_TOP_DIRS });

/** The policy's four free fields after the tag: T3, T5 and their record fields `decidedOn` and `reason` (the parent's section 8 and F3's closed point 9; v1's own list frees t2 and t3 and is v1's). The ONE list: PR 4's identity of the policy (`stage-identity-readers.mjs`) and the guard's free fields both import it. */
export const POLICY_FREE_FIELDS = Object.freeze(['t3', 't5', 'decidedOn', 'reason']);
/** The guard's policy entry: the free fields are the one list of T3, T5 and their record fields (`decidedOn`, `reason`) that the policy's identity leaves out too; a policy file is required, and a change of it needs its dated `input:crosscheck-policy.json:<12 hex>` line (v1's line kind for an amended input). */
export const STAGE_POLICY = Object.freeze({ file: inPrereg('crosscheck-policy.json'), free: POLICY_FREE_FIELDS, required: true, dated: 'crosscheck-policy.json', rule: 't3, t5 and their dated reason', why: 'F3\'s closed point 9' });

/** The pilot's trees, registered by the owner's open point 2 (answered 2026-11-19, consult a4be62c7): the A/A is task 05 on Node (both twins), the daemon pair is task 10 in both stacks, the web pair is task 04. Not drawn by the seed. */
export const STAGE_PILOT = Object.freeze({ aa: Object.freeze({ task: '05', stack: 'node' }), daemon: '10', web: '04' });
