// F7 and the coai review: TL and ML of a series are the ones written into its inputs.json at `init`, never the live calibration.json, which is also checked for shape and the registered bounds; a calibration
// file that changed since `init` is refused naming both.
import '../../minigit-bench/test/fake-home.mjs';
import assert from 'node:assert/strict';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { loadExplore } from '../lib/inputs.mjs';
import { calibrationFor, limitsProblems, loadCalibration, seriesLimits, validLimits } from '../lib/limits.mjs';
import { exploreRoot } from '../lib/paths.mjs';
import { resolveTask, taskPaths } from '../lib/tasks.mjs';
import { createSeries } from '../lib/series.mjs';
import { runPair } from '../lib/run.mjs';

const EXPLORE = loadExplore().value;
const REG = EXPLORE.limits;
const TASK = resolveTask(EXPLORE, '01').value;
const calibrationFile = home => taskPaths(TASK, home).calibrationFile;
const GOOD = { timeMs: 2000, memoryBytes: 536870912, buildTimeoutMs: 600000 };

function homeWith(limits) {
  const home = tmpDir('lim-home');
  mkdirSync(exploreRoot(home), { recursive: true });
  writeFileSync(calibrationFile(home), JSON.stringify({ limits }));
  return home;
}

test('Limits_are_positive_finite_numbers_within_the_registered_floors_and_ceilings', () => {
  assert.deepEqual(limitsProblems(GOOD, REG), []);
  assert.deepEqual(limitsProblems({ ...GOOD, timeMs: 30000 }, REG), []);
  for (const [name, limits, pattern] of [
    ['missing', undefined, /limits is missing/],
    ['null', null, /limits is missing/],
    ['a string', { ...GOOD, timeMs: '2000' }, /timeMs must be positive finite/],
    ['NaN', { ...GOOD, memoryBytes: NaN }, /memoryBytes must be positive finite/],
    ['negative', { ...GOOD, timeMs: -1 }, /timeMs must be positive finite/],
    ['zero', { ...GOOD, buildTimeoutMs: 0 }, /buildTimeoutMs must be positive finite/],
    ['Infinity', { ...GOOD, timeMs: Infinity }, /timeMs must be positive finite/],
    ['under the floor', { ...GOOD, timeMs: 1999 }, /under the registered floor/],
    ['memory under the floor', { ...GOOD, memoryBytes: 1024 }, /memoryBytes 1024 is under the registered floor/],
    ['absurdly long', { ...GOOD, timeMs: 10 * REG.calibrationCeilingMs + 1 }, /calibration ceilings/],
    ['absurdly large', { ...GOOD, memoryBytes: 65 * 1024 ** 3 }, /memoryBytes .* is over/],
    ['another build timeout', { ...GOOD, buildTimeoutMs: 1 }, /not the registered 600000/],
  ]) assert.match(limitsProblems(limits, REG).join(' | '), pattern, name);
});

test('A_calibration_file_with_a_bad_shape_is_refused_by_the_loader', () => {
  assert.equal(loadCalibration(homeWith(GOOD), TASK).ok, true);
  const bad = loadCalibration(homeWith({ ...GOOD, timeMs: -5 }), TASK);
  assert.equal(bad.ok, false);
  assert.match(bad.reason, /not a usable calibration: timeMs must be positive finite/);
  assert.equal(loadCalibration(tmpDir('no-calibration'), TASK).ok, false);
  const home = tmpDir('garbled');
  mkdirSync(exploreRoot(home), { recursive: true });
  writeFileSync(calibrationFile(home), '{ not json');
  assert.match(loadCalibration(home, TASK).reason, /unreadable/);
});

test('F7_a_series_is_scored_under_the_limits_it_was_created_with_and_a_changed_calibration_file_is_refused', async () => {
  const home = homeWith(GOOD);
  const made = createSeries({ home, explore: EXPLORE, calibration: { limits: GOOD }, task: '01' });
  assert.deepEqual(seriesLimits(made.value.dir).value, GOOD);
  assert.deepEqual(calibrationFor({ home, seriesDir: made.value.dir, task: TASK }).value.limits, GOOD);
  writeFileSync(calibrationFile(home), JSON.stringify({ limits: { ...GOOD, timeMs: 5000 } })); // someone calibrated again
  const moved = calibrationFor({ home, seriesDir: made.value.dir, task: TASK });
  assert.equal(moved.ok, false);
  assert.match(moved.reason, /the calibration file \(TL 5000 ms, ML 536870912 B\) differs from the limits this series was created with \(TL 2000 ms, ML 536870912 B\)/);
  const never = () => { throw new Error('nothing after the limits is read for a refused pair'); };
  const refused = await runPair({ seriesDir: made.value.dir, pair: 1, explore: EXPLORE, task: TASK, io: { home, say: () => {}, abort: {}, worldFn: never, tokenFn: never } });
  assert.equal(refused.exit, 4);
  assert.match(refused.message, /differs from the limits this series was created with/);
  writeFileSync(path.join(made.value.dir, 'inputs.json'), JSON.stringify({ ...JSON.parse(readFileSync(path.join(made.value.dir, 'inputs.json'), 'utf8')), limits: { ...GOOD, timeMs: 1 } }));
  writeFileSync(calibrationFile(home), JSON.stringify({ limits: GOOD }));
  assert.match(calibrationFor({ home, seriesDir: made.value.dir, task: TASK }).reason, /inputs\.json are not usable/, 'a series whose own limits are not usable is refused too');
});

test('A_malformed_explore_json_is_a_failure_naming_it_and_never_a_TypeError', () => {
  const home = homeWith(GOOD);
  const made = createSeries({ home, explore: EXPLORE, calibration: { limits: GOOD }, task: '01' });
  const broken = tmpDir('bad-explore');
  const garbled = path.join(broken, 'explore.json');
  writeFileSync(garbled, '{ not json');
  const missingLimits = path.join(broken, 'no-limits.json');
  const { limits, ...rest } = JSON.parse(readFileSync(path.join(path.dirname(new URL(import.meta.url).pathname), '..', 'inputs', 'explore.json'), 'utf8'));
  writeFileSync(missingLimits, JSON.stringify(rest));
  for (const file of [garbled, missingLimits, path.join(broken, 'absent.json')]) {
    const exploreFn = () => loadExplore(file);
    assert.doesNotThrow(() => calibrationFor({ home, seriesDir: made.value.dir, task: TASK, exploreFn }), file);
    const got = calibrationFor({ home, seriesDir: made.value.dir, task: TASK, exploreFn });
    assert.equal(got.ok, false, file);
    assert.ok(got.reason.includes(file) || /explore\.json/.test(got.reason), got.reason);
    assert.doesNotThrow(() => loadCalibration(home, TASK, exploreFn));
    assert.equal(loadCalibration(home, TASK, exploreFn).ok, false);
  }
});

test('The_live_calibration_and_the_series_own_limits_go_through_the_one_validator_which_returns_an_outcome', () => {
  assert.deepEqual(validLimits(GOOD, REG, 'x'), { ok: true, value: GOOD });
  const bad = validLimits({ ...GOOD, timeMs: 1 }, REG, 'the thing is not usable');
  assert.equal(bad.ok, false);
  assert.match(bad.reason, /^the thing is not usable: timeMs 1 is under the registered floor of 2000$/);
  const home = homeWith({ ...GOOD, timeMs: 1 });
  assert.match(loadCalibration(home, TASK).reason, /is not a usable calibration: timeMs 1 is under the registered floor/);
  const made = createSeries({ home: homeWith(GOOD), explore: EXPLORE, calibration: { limits: GOOD }, task: '01' });
  writeFileSync(path.join(made.value.dir, 'inputs.json'), JSON.stringify({ ...JSON.parse(readFileSync(path.join(made.value.dir, 'inputs.json'), 'utf8')), limits: { ...GOOD, timeMs: 1 } }));
  assert.match(calibrationFor({ home: homeWith(GOOD), seriesDir: made.value.dir, task: TASK }).reason, /the limits in .*inputs\.json are not usable: timeMs 1 is under/);
});
