// What the score and the record say about the run that made them: which runner judged it and whether its memory was certified (the quick path), and an unknown wall that stays unknown. Pure functions.
import '../../minigit-bench/test/fake-home.mjs';
import assert from 'node:assert/strict';
import { test } from 'node:test';
import { exploreSection } from '../lib/records.mjs';
import { scoreShape } from '../lib/score.mjs';
import { score, ran, rec } from './quick-score-fixture.mjs';

const build = { ok: true };
const kase = (id, wallMs, peakBytes = 1000) => ({ id, set: id.startsWith('s') ? 'visible' : 'heldout', verdict: 'pass', wallMs, peakBytes });

test('The_score_says_which_runner_judged_it_and_whether_its_memory_was_certified', async t => {
  const r = await score(t, { runFn: ran(['s01', 'h001', 'h002'].map(id => rec(id))) });
  assert.equal(r.value.explore.runner, '0.x');
  assert.equal(r.value.explore.memoryCertified, true);
});

test('A_result_that_names_neither_gives_a_score_with_neither_key_so_the_olympiads_bytes_are_what_they_were', () => {
  const s = scoreShape({ result: { build, limits: {}, cases: [kase('s01', 5), kase('h001', 7)] } });
  assert.equal('runner' in s.explore, false);
  assert.equal('memoryCertified' in s.explore, false);
  assert.deepEqual(Object.keys(s.explore), ['limits', 'build', 'frozenChanges', 'cases', 'verdicts', 'passRate', 'trapHits', 'trapCases']);
});

test('A_memory_claim_that_was_not_certified_is_recorded_as_false_and_not_dropped', () => {
  const s = scoreShape({ result: { build, limits: {}, cases: [kase('s01', 5)], runner: '0.3.2', memoryCertified: false } });
  assert.equal(s.explore.memoryCertified, false);
});

test('A_wall_that_was_not_measured_is_unknown_and_never_a_minimum_and_maximum_of_zero', () => {
  const s = scoreShape({ result: { build, limits: {}, cases: [kase('s01', null), kase('s02', null), kase('h001', 40), kase('h002', null)] } });
  const section = exploreSection({ score: s, audit: null });
  assert.equal(section.wall.value.visible, null, 'no wall at all is unknown');
  assert.deepEqual(section.wall.value.heldout, { min: 40, median: 40, max: 40 }, 'an unknown wall is not counted among the known ones');
});

test('Known_walls_keep_their_statistics', () => {
  const s = scoreShape({ result: { build, limits: {}, cases: [kase('s01', 10), kase('s02', 30), kase('s03', 20), kase('h001', 5)] } });
  assert.deepEqual(exploreSection({ score: s, audit: null }).wall.value.visible, { min: 10, median: 20, max: 30 });
});
