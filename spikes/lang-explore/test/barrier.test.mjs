// The barrier and the pair plan (plan D-X1, T-X10): pair k+1 is refused until both sessions of pair k are collected, scored and recorded; a void is re-run once, alone, as `ran unpaired`; the masks follow the
// draw and swap every pair; a series directory is created private and loads back.
import assert from 'node:assert/strict';
import { readFileSync, statSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { openSeries, transition } from '../../minigit-bench/lib/run-map.mjs';
import { tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { loadExplore } from '../lib/inputs.mjs';
import { MASKS, barrierProblem, createSeries, drawMask, loadSeries, maskOf, pairComplete, planPair, seriesIdOf, stackState } from '../lib/series.mjs';

const EXPLORE = loadExplore().value;
const DRAW = { csharpHigh: true, method: 'test' };
let counter = 0;
const rand = () => (counter += 1).toString(16).padStart(8, '0');

/** A run-map with pair `pair` planned, and its sessions moved through `script` (`{ csharp: [...], node: [...] }`: each entry the patch of the last transition or a list of statuses). */
function mapWith(pair, ends) {
  let map = openSeries({ seriesId: 'x-test', caps: EXPLORE.caps });
  const planned = planPair({ map, pair, pairs: 3, draw: DRAW, rand });
  assert.equal(planned.ok, true, planned.reason);
  map = planned.value.map;
  for (const e of planned.value.planned) map = settle(map, e.rid, ends[e.stack]);
  return map;
}

/** Move a session through launching -> ran (-> scored) with the class and data of `end`. */
function settle(map, rid, end) {
  let m = transition(map, rid, 'launching', {}).value;
  m = transition(m, rid, 'ran', { class: end.class, data: end.data }).value;
  return end.scored ? transition(m, rid, 'scored', {}).value : m;
}

const GOOD = { class: 'completed', data: true, scored: true };
const VOID = { class: 'void: harness', data: false, scored: false };

test('The_masks_follow_the_draw_for_pair_1_and_swap_every_pair', () => {
  for (const draw of [{ csharpHigh: true }, { csharpHigh: false }]) {
    const at = (pair, stack) => maskOf({ draw, pair, stack });
    assert.equal(at(1, 'csharp'), draw.csharpHigh ? MASKS.high : MASKS.low);
    assert.notEqual(at(1, 'csharp'), at(1, 'node'), 'the two stacks never share a mask');
    assert.equal(at(2, 'csharp'), at(1, 'node'), 'swapped');
    assert.equal(at(3, 'csharp'), at(1, 'csharp'), 'and swapped back');
  }
  assert.deepEqual([...new Set([0, 1].map(() => drawMask(() => 1).csharpHigh))], [true]);
  assert.equal(drawMask(() => 0).csharpHigh, false);
});

test('Pair_1_plans_both_stacks_as_paired_attempt_1_and_each_rid_is_new', () => {
  const map = openSeries({ seriesId: 'x-test', caps: EXPLORE.caps });
  const r = planPair({ map, pair: 1, pairs: 3, draw: DRAW, rand });
  assert.equal(r.ok, true, r.reason);
  assert.deepEqual(r.value.planned.map(p => [p.stack, p.attempt, p.label, p.phase, p.kind]), [['csharp', 1, 'paired', 'v1', 'pilot'], ['node', 1, 'paired', 'v1', 'pilot']]);
  assert.deepEqual(r.value.planned.map(p => p.mask), [MASKS.high, MASKS.low]);
  assert.equal(new Set(r.value.planned.map(p => p.rid)).size, 2);
  assert.ok(r.value.planned.every(p => /^[0-9a-f]{6}$/.test(p.rid)), 'a random rid: nothing in a path the model sees names the arm or the stack');
  assert.deepEqual(Object.keys(map.sessions), [], 'the input map is not changed');
});

test('Pair_k_plus_1_is_refused_until_both_sessions_of_pair_k_are_scored', () => {
  assert.match(planPair({ map: openSeries({ seriesId: 'x', caps: {} }), pair: 2, pairs: 3, draw: DRAW }).reason, /pair 1 is not finished \(csharp: none, node: none\)/);
  const cases = [
    ['one session still only ran', { csharp: GOOD, node: { class: 'completed', data: true, scored: false } }, false],
    ['both scored', { csharp: GOOD, node: GOOD }, true],
  ];
  for (const [name, ends, allowed] of cases) {
    const map = mapWith(1, ends);
    assert.equal(pairComplete(map, 1), allowed, name);
    assert.equal(planPair({ map, pair: 2, pairs: 3, draw: DRAW, rand }).ok, allowed, name);
  }
});

test('A_void_is_re_run_once_alone_as_ran_unpaired_and_the_pair_then_counts_as_finished', () => {
  let map = mapWith(1, { csharp: VOID, node: GOOD });
  assert.equal(stackState(map, 1, 'csharp'), 'void-rerunnable');
  const blocked = planPair({ map, pair: 2, pairs: 3, draw: DRAW });
  assert.match(blocked.reason, /a void is re-run once, alone: run --pair 1 --rerun <stack>/);
  assert.match(planPair({ map, pair: 1, pairs: 3, draw: DRAW }).reason, /already been run/);
  assert.match(planPair({ map, pair: 1, pairs: 3, draw: DRAW, rerun: 'node' }).reason, /node is done/, 'only a void is re-run');
  assert.match(planPair({ map, pair: 1, pairs: 3, draw: DRAW, rerun: 'rust' }).reason, /the stacks are csharp, node/);
  const rerun = planPair({ map, pair: 1, pairs: 3, draw: DRAW, rerun: 'csharp', rand });
  assert.equal(rerun.ok, true, rerun.reason);
  assert.deepEqual(rerun.value.planned.map(p => [p.stack, p.attempt, p.label, p.mask]), [['csharp', 2, 'ran unpaired', MASKS.high]]);
  map = settle(rerun.value.map, rerun.value.planned[0].rid, VOID);
  assert.equal(stackState(map, 1, 'csharp'), 'void-final');
  assert.match(planPair({ map, pair: 1, pairs: 3, draw: DRAW, rerun: 'csharp' }).reason, /void-final|only a session that ended as a void, and only once/, 'one re-run per void, never two');
  assert.equal(pairComplete(map, 1), true, 'a void after its one re-run is the pair\'s end');
  assert.equal(planPair({ map, pair: 2, pairs: 3, draw: DRAW, rand }).ok, true);
});

test('A_pair_number_outside_the_pilot_and_a_stopped_session_are_refused', () => {
  const map = openSeries({ seriesId: 'x', caps: {} });
  for (const bad of [0, 4, 1.5, NaN]) assert.match(barrierProblem({ map, pair: bad, pairs: 3 }), /pairs 1 to 3/);
  const stopped = mapWith(1, { csharp: { class: 'stop: security', data: false, scored: false }, node: GOOD });
  assert.equal(stackState(stopped, 1, 'csharp'), 'stopped');
  assert.equal(pairComplete(stopped, 1), false);
});

test('A_series_directory_is_created_private_with_its_inputs_and_loads_back', () => {
  const home = tmpDir('home');
  const calibration = { limits: { timeMs: 2000, memoryBytes: 536870912, buildTimeoutMs: 600000 } };
  const made = createSeries({ home, explore: EXPLORE, calibration, draw: DRAW, date: new Date('2026-11-01T10:00:00Z'), rand: () => 'ab12', inputsExtra: { note: 'x' } });
  assert.equal(made.ok, true, made.reason);
  assert.equal(path.basename(made.value.dir), 'x-20261101-ab12');
  assert.equal(seriesIdOf({ date: new Date('2026-11-01T10:00:00Z'), rand: 'ab12' }), 'x-20261101-ab12');
  for (const d of ['', 's', 'pairs', 'private']) assert.equal(statSync(path.join(made.value.dir, d)).mode & 0o777, 0o700, d || 'the series dir');
  const inputs = JSON.parse(readFileSync(path.join(made.value.dir, 'inputs.json'), 'utf8'));
  assert.deepEqual([inputs.limits.timeMs, inputs.caps.capUsd, inputs.locale], [2000, 50, 'de_DE.UTF-8']);
  const loaded = loadSeries(made.value.dir);
  assert.equal(loaded.ok, true, loaded.reason);
  assert.deepEqual(loaded.value.state.draw, DRAW);
  assert.deepEqual(Object.keys(loaded.value.map.sessions), []);
  assert.equal(createSeries({ home, explore: EXPLORE, calibration, draw: DRAW, date: new Date('2026-11-01T10:00:00Z'), rand: () => 'ab12' }).ok, false, 'a series id is never reused');
  assert.equal(loadSeries(tmpDir('nothing')).ok, false);
});

// ---- F2: a halted or failed pair resumes -------------------------------------------------------------------------------------------

/** A pair planned, with its sessions moved to the given statuses (`planned`, `launching`, `ran-data`, `scored`, `void`). */
function pairIn(statuses) {
  let map = openSeries({ seriesId: 'x-test', caps: EXPLORE.caps });
  const planned = planPair({ map, pair: 1, pairs: 3, draw: DRAW, rand });
  map = planned.value.map;
  planned.value.planned.forEach((e, i) => {
    const to = statuses[i];
    if (to === 'planned') return;
    map = transition(map, e.rid, 'launching', {}).value;
    if (to === 'launching') return;
    map = transition(map, e.rid, 'ran', to === 'void' ? { class: 'void: harness', data: false } : { class: 'completed', data: true }).value;
    if (to === 'scored') map = transition(map, e.rid, 'scored', {}).value;
  });
  return map;
}

test('F2_a_pair_whose_sessions_are_planned_launching_or_ran_unscored_is_resumed_with_the_map_unchanged', () => {
  for (const statuses of [['planned', 'planned'], ['launching', 'launching'], ['ran-data', 'ran-data'], ['scored', 'ran-data'], ['scored', 'planned'], ['void', 'launching']]) {
    const map = pairIn(statuses);
    const r = planPair({ map, pair: 1, pairs: 3, draw: DRAW, rand });
    assert.equal(r.ok, true, `${statuses}: ${r.reason}`);
    assert.deepEqual(r.value.planned, [], `${statuses}: nothing new is planned (no new paid session)`);
    assert.equal(r.value.map, map, `${statuses}: the map is returned as it is`);
    assert.equal(r.value.resumed.length, statuses.filter(x => x !== 'scored' && x !== 'void').length, statuses.join());
  }
});

test('F2_a_pair_that_is_finished_is_still_refused_and_a_rerun_waits_for_the_open_sessions_of_the_pair', () => {
  const done = pairIn(['scored', 'scored']);
  assert.match(planPair({ map: done, pair: 1, pairs: 3, draw: DRAW }).reason, /already been run/);
  const open = pairIn(['void', 'planned']);
  const r = planPair({ map: open, pair: 1, pairs: 3, draw: DRAW, rerun: 'csharp', rand });
  assert.equal(r.ok, true, r.reason);
  assert.deepEqual(r.value.planned, [], 'the open session is resumed first; the re-run is asked for again afterwards');
  assert.equal(r.value.resumed.length, 1);
});
