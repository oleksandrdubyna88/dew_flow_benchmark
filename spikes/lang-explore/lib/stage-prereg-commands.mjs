// The three verbs of the pre-registration (F3.S1 (b), PR 5), all FREE: `seed-commit` and `seed-reveal` (the seed as commit-reveal over v1's code, `stage-seed.mjs`) and `stage-sync` (the one way the series' own branch moves,
// `stage-branch.mjs`, and with `--accept-harness <12 hex>` the harness folders of a fix on main, `stage-accept.mjs`). Each takes the parsed flags and an `io` (`say`, `home`, and the seams `repo` and `random` of a test) and returns an exit code: 0 done | 4 usage, configuration or a refusal. None prints the seed.
import path from 'node:path';
import { EXIT } from './commands.mjs';
import { REPO } from './paths.mjs';
import { acceptHarness } from './stage-accept.mjs';
import { stageSync } from './stage-branch.mjs';
import { acquireSeriesLock } from '../../minigit-bench/lib/series-lock.mjs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { hasDataSession } from '../../minigit-bench/lib/run-map.mjs';
import { loadStageSeries, paidStageSeriesDirs, stageSeriesDirs, unreadableSeriesDirs } from './stage-series.mjs';
import { commitStageSeed, revealStageSeed, stageSeedFile } from './stage-seed.mjs';

const refused = (io, reason) => { io.say(reason); return EXIT.config; };

/** `seed-commit`: draws a seed the schedule accepts and writes it outside git and its hash into the tree. Last step before the tag. */
export function cmdSeedCommit(values, io) {
  const done = commitStageSeed({ root: io.repo ?? REPO, home: io.home, random: io.random });
  if (!done.ok) return refused(io, done.reason);
  io.say(`seed committed: sha256 ${done.value.hash} is in inputs/prereg/seed.sha256; the seed is in ${stageSeedFile(io.home)} (mode 600), outside git, until seed-reveal`);
  return EXIT.done;
}

/** `seed-reveal`: copies the seed to `inputs/prereg/seed.txt`, after the A/A; the file must hash to the committed value. */
export function cmdSeedReveal(values, io) {
  const done = revealStageSeed({ root: io.repo ?? REPO, home: io.home });
  if (!done.ok) return refused(io, done.reason);
  io.say('seed revealed: inputs/prereg/seed.txt is written; commit it with its own pull request, then stage-sync');
  return EXIT.done;
}

const HEX12 = /^[0-9a-f]{12}$/;
const needsValue = given => `--accept-harness needs a value: the 12 hex (0-9, a-f) of the harness identity that a dated line \`YYYY-MM-DD | instrument | harness:<12 hex> | commit:<40 hex> <reason>\` names in inputs/prereg/amendments.md on origin/main, as in \`stage-sync --accept-harness 0123456789ab\` (got ${given === '' ? 'no value' : `'${given}'`})`;

/** The sentence for an `--accept-harness` that has no value (bare, or `=`) or one that is not 12 lower-case hex, or null. */
const valueProblem = values => (values['accept-harness'] === undefined || HEX12.test(values['accept-harness']) ? null : needsValue(values['accept-harness']));

/** The Stage 1 series of the explore root whose lock `stage-sync` takes (with no `home` there is no explore root to look under). */
const lockedDirs = io => (io.home === undefined ? [] : stageSeriesDirs(io.home));

/** A `--series` that is not a Stage 1 series, or one that is not under the explore root of this home, or null: the verb names the series it works for, so a typo is a refusal, and the take moves the ONE branch every series of the root shares. */
function seriesProblem({ values, io }) {
  if (!values.series) return null;
  const dir = path.resolve(values.series);
  const loaded = loadStageSeries(dir);
  if (!loaded.ok) return loaded.reason;
  return lockedDirs(io).includes(dir) ? null : `${dir} is not a series under the explore root of this home: stage-sync works for every Stage 1 series of that root, since the branch it moves is shared`;
}

/** The sentence for a series of the explore root whose state cannot be read, or null: it is neither locked nor freeze-checked, so the verb cannot tell whether a unit runs or a data session has left planned. */
function unreadableProblem(io) {
  const [broken] = io.home === undefined ? [] : unreadableSeriesDirs(io.home);
  return broken === undefined ? null : `the series ${broken.dir} cannot be read (${broken.reason}), so stage-sync cannot be told whether a unit runs or a data session has left planned: fix or remove it`;
}

/** The recorded branch and tag commit of every paid Stage 1 series of the explore root, for the refusal of a branch that is none's (undefined without a `home`: there is no explore root to read). */
function seriesFacts(io) {
  if (io.home === undefined) return undefined;
  const loaded = paidStageSeriesDirs(io.home).map(dir => loadStageSeries(dir)).filter(s => s.ok);
  return loaded.map(s => ({ branch: s.value.state.branch, preregCommit: s.value.state.preregCommit }));
}

/** `acquireSeriesLock` of one series as a value: a lock that cannot be taken, even for an unexpected error, is a `fail`. */
function acquired(seriesDir) {
  try {
    return acquireSeriesLock({ seriesDir });
  } catch (e) {
    return fail(`the series lock of ${seriesDir} cannot be taken: ${e.message}`);
  }
}

/**
 * Takes the series lock of every directory, in order, with PR 3's `acquireSeriesLock`: `ok(release)` (one function that frees them all), or, when one cannot be taken, the `fail` that names its holder after the ones already taken are released.
 * With these held `stage-run` cannot start a unit during the sync, and the sync cannot start during a unit.
 */
function takeLocks(dirs, held = []) {
  if (dirs.length === 0) return ok(() => held.forEach(lock => lock.release()));
  const got = acquired(dirs[0]);
  if (got.ok) return takeLocks(dirs.slice(1), [...held, got.value]);
  held.forEach(lock => lock.release());
  return fail(`stage-sync does not run while a unit runs: ${got.reason}`);
}

/** The sentence for a call `stage-sync` refuses before it takes any lock, or null. */
const callProblem = ({ values, io }) => valueProblem(values) ?? seriesProblem({ values, io }) ?? unreadableProblem(io);

// ---- the freeze: no harness is accepted once the first data session is under way -----------------------------------------------------------------------------------------------------------------------

/** Why the series `{ dir, map }` accepts no harness any more, or null: a data session has left `planned` (the instrument is frozen with it), or the first data plan froze the accepted identity into the run-map (`harnessAtFirstData`). */
const FROZEN = Object.freeze([
  [({ map }) => hasDataSession(map), ({ dir }) => `a data session has left planned in the series ${dir}: the instrument is frozen with the first data session, so no harness is accepted now: start a new series`],
  [({ map }) => map.harnessAtFirstData !== undefined, ({ dir, map }) => `the first data plan of the series ${dir} froze the harness identity ${map.harnessAtFirstData}: no other harness is accepted now`],
]);
const frozenReason = series => FROZEN.find(([is]) => is(series))?.[1](series) ?? null;

/** The sentence for the first series of the explore root (every one: the take moves the branch they share, whatever `--series` names) that accepts no harness now, or null. A series that cannot be read is refused too: it may hide a data session. Read under the locks. */
function frozenProblem(io) {
  const reasons = lockedDirs(io).map(dir => ({ dir, loaded: loadStageSeries(dir) })).map(({ dir, loaded }) => (loaded.ok ? frozenReason({ dir, map: loaded.value.map }) : `the series ${dir} cannot be read (${loaded.reason}), so it cannot be told whether a data session has left planned`));
  return reasons.find(reason => reason !== null) ?? null;
}

// ---- the sync itself, under the locks -------------------------------------------------------------------------------------------------------------------------------------------------------------------

/** The result line of a plain sync. */
const syncedLine = ({ changed, commit }) => (commit === null ? 'stage-sync: nothing to take: the series\' branch holds origin/main\'s registered files' : `stage-sync: committed ${commit.slice(0, 12)}: ${changed.join(', ')}`);

/** The result line of an acceptance: the files taken and the commit of `main` they come from. */
const acceptedLine = ({ hex, value }) => (value.commit === null
  ? `stage-sync --accept-harness ${hex}: nothing to take: the series' branch holds the harness folders of ${value.from.slice(0, 12)}`
  : `stage-sync --accept-harness ${hex}: committed ${value.commit.slice(0, 12)}: ${value.changed.length} harness file${value.changed.length === 1 ? '' : 's'} taken from commit ${value.from.slice(0, 12)}`);

/** The plain sync under the locks: the result line. */
async function plainUnderLocks(io) {
  const synced = await (io.syncFn ?? stageSync)({ repo: io.repo ?? REPO, say: io.say, series: seriesFacts(io) });
  if (!synced.ok) return refused(io, synced.reason);
  io.say(syncedLine(synced.value));
  return EXIT.done;
}

/** The acceptance under the locks: refused when a series is frozen, else the taking of the harness of the commit the dated line names. */
async function acceptUnderLocks({ values, io }) {
  const frozen = frozenProblem(io);
  if (frozen !== null) return refused(io, frozen);
  const hex = values['accept-harness'];
  const taken = await (io.syncFn ?? acceptHarness)({ repo: io.repo ?? REPO, hex, say: io.say, series: seriesFacts(io) });
  if (!taken.ok) return refused(io, taken.reason);
  io.say(acceptedLine({ hex, value: taken.value }));
  return EXIT.done;
}

const underLocks = ({ values, io }) => (values['accept-harness'] === undefined ? plainUnderLocks(io) : acceptUnderLocks({ values, io }));

/**
 * `stage-sync [--series <dir>] [--accept-harness <12 hex>]`: takes the series lock of every Stage 1 series under the explore root for its whole run (and releases them on every way out), fetches, and commits on the series' branch,
 * never pushing. Plain: it takes the registered files that may change from `origin/main` and prints what changed. With `--accept-harness <12 hex>` (a bare flag or a value that is not 12 hex is refused naming the value): the harness
 * folders of the commit that the dated `instrument | harness:<12 hex> | commit:<40 hex> ...` line names, which must be on `origin/main`; refused once a data session has left `planned`. `io.syncFn` replaces `stageSync` (or `acceptHarness`) in a test.
 */
export async function cmdStageSync(values, io) {
  const problem = callProblem({ values, io });
  if (problem) return refused(io, problem);
  const locks = takeLocks(lockedDirs(io));
  if (!locks.ok) return refused(io, locks.reason);
  try {
    return await underLocks({ values, io });
  } finally {
    locks.value();
  }
}
