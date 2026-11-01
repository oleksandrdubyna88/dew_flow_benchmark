// The limits a series is scored under (F7 and the coai review): the calibration is measured once (`explore.mjs calibrate`, `~/rsd/lang-v2/explore/calibration.json`), checked for shape and for the registered
// bounds, and COPIED into the series' `inputs.json` at `init`. A pair reads TL and ML from the series, never from the live calibration file, and refuses when that file has changed since: the limits of a
// series cannot move between pairs. Failures are values.
import { existsSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { loadExplore } from './inputs.mjs';
import { calibrationFile } from './paths.mjs';

const MAX_TIME_FACTOR = 10; // an effective TL above ten calibration ceilings is not a limit
const MAX_MEMORY_BYTES = 64 * 1024 ** 3;
const finitePositive = n => typeof n === 'number' && Number.isFinite(n) && n > 0;

const KEYS = ['timeMs', 'memoryBytes', 'buildTimeoutMs'];

/** Each bound as `[violated(limits, registered), sentence(limits, registered)]`. */
const BOUNDS = Object.freeze([
  [(l, r) => l.timeMs < r.timeFloorMs, (l, r) => `timeMs ${l.timeMs} is under the registered floor of ${r.timeFloorMs}`],
  [(l, r) => l.timeMs > MAX_TIME_FACTOR * r.calibrationCeilingMs, (l) => `timeMs ${l.timeMs} is over ${MAX_TIME_FACTOR} calibration ceilings`],
  [(l, r) => l.memoryBytes < r.memoryFloorBytes, (l, r) => `memoryBytes ${l.memoryBytes} is under the registered floor of ${r.memoryFloorBytes}`],
  [l => l.memoryBytes > MAX_MEMORY_BYTES, l => `memoryBytes ${l.memoryBytes} is over ${MAX_MEMORY_BYTES}`],
  [(l, r) => l.buildTimeoutMs !== r.buildTimeoutMs, (l, r) => `buildTimeoutMs ${l.buildTimeoutMs} is not the registered ${r.buildTimeoutMs}`],
]);

/** The sentences saying why `limits` cannot be the pilot's: a missing or non-numeric field, below the registered floor, above the sanity ceiling, or another build timeout. */
export function limitsProblems(limits, registered) {
  if (limits === null || typeof limits !== 'object') return ['limits is missing'];
  const bad = KEYS.filter(k => !finitePositive(limits[k]));
  if (bad.length > 0) return [`${bad.join(', ')} must be positive finite numbers`];
  return BOUNDS.filter(([violated]) => violated(limits, registered)).map(([, sentence]) => sentence(limits, registered));
}

/** The one validator of limits, for the live calibration and for a series' own: `ok(limits)`, or a `fail` that starts with `subject` and lists the problems. */
export function validLimits(limits, registered, subject) {
  const problems = limitsProblems(limits, registered);
  return problems.length === 0 ? ok(limits) : fail(`${subject}: ${problems.join('; ')}`);
}

const readJsonOr = file => { try { return ok(JSON.parse(readFileSync(file, 'utf8'))); } catch (e) { return fail(`${file} is unreadable: ${e.message}`); } };

/** The calibration (`explore.mjs calibrate`) for `home`, checked for its shape and bounds; or the sentence saying to run it: no paid session starts without limits. `exploreFn` loads the registered inputs (a test passes its own). */
export function loadCalibration(home, exploreFn = loadExplore) {
  const file = calibrationFile(home);
  if (!existsSync(file)) return fail(`${file} is not there: run \`explore.mjs calibrate\` first (the limits are measured before any paid session)`);
  const got = chainSync([() => exploreFn(), () => readJsonOr(file)]);
  if (!got.ok) return got;
  const valid = validLimits(got.value[1]?.limits, got.value[0].limits, `${file} is not a usable calibration`);
  return valid.ok ? ok(got.value[1]) : valid;
}

/** The synchronous `allOk`: the values of steps that each return `ok(value)`, or the first `fail`. */
function chainSync(steps) {
  const values = [];
  for (const step of steps) {
    const r = step();
    if (!r.ok) return r;
    values.push(r.value);
  }
  return ok(values);
}

/** The limits recorded in a series' `inputs.json` when it was created. */
export function seriesLimits(seriesDir) {
  try { return ok(JSON.parse(readFileSync(path.join(seriesDir, 'inputs.json'), 'utf8')).limits); } catch (e) { return fail(`${seriesDir}/inputs.json is unreadable: ${e.message}`); }
}

const same = (a, b) => KEYS.every(k => a?.[k] === b?.[k]);

const movedSentence = (live, own) => `calibration.json (TL ${live.timeMs} ms, ML ${live.memoryBytes} B) differs from the limits this series was created with (TL ${own.timeMs} ms, ML ${own.memoryBytes} B): the limits of a series do not move between pairs. Calibrate again only before \`init\`, and make a new series`;

/** The series' own limits, valid (they were checked when the series was made, and are checked again) against the registered ones `registered`. -> `ok(limits)` | `fail`. */
function ownLimits(seriesDir, registered) {
  const own = seriesLimits(seriesDir);
  return own.ok ? validLimits(own.value, registered, `the limits in ${seriesDir}/inputs.json are not usable`) : own;
}

/**
 * The calibration a pair of this series is scored under: the series' own limits (read from its inputs.json), valid, and equal to the live calibration file. A calibration file that changed since `init` is refused,
 * naming both. -> `ok(calibration)` with `limits` the series' | `fail`.
 */
export function calibrationFor({ home, seriesDir, exploreFn = loadExplore }) {
  const explore = exploreFn();
  if (!explore.ok) return explore;
  const got = chainSync([() => loadCalibration(home, () => explore), () => ownLimits(seriesDir, explore.value.limits)]);
  if (!got.ok) return got;
  const [live, own] = got.value;
  return same(own, live.limits) ? ok({ ...live, limits: own }) : fail(movedSentence(live.limits, own));
}
