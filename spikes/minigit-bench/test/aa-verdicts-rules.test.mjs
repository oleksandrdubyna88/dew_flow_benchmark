// The rules of `aa-verdicts` that the review of the instrument amendment tightened (G-6, G-12, G-9, G-10, G-13, G-11, G-7): each one broken once on a synthetic series under the fake home.
import './fake-home.mjs'; // first: nothing here may read the operator's files
import assert from 'node:assert/strict';
import { test } from 'node:test';
import { aaVerdicts } from '../lib/aa-verdicts.mjs';
import { GOOD_LINES, SMOKE_OK, T0, known, makeInputs, makeSeries } from './aa-fixture.mjs';

const report = (series, lines = GOOD_LINES) => {
  const r = aaVerdicts({ seriesDir: series, inputsDir: makeInputs(lines), smoke: SMOKE_OK, now: () => new Date(T0) });
  assert.equal(r.ok, true, r.reason);
  return r.value;
};
const rowOf = (rep, id) => rep.rows.find(r => r.id === id);
const partition = (categories, unattributed, wallMs) => ({ time: { ...(wallMs ? { wallMs: known(wallMs) } : {}), partition: known({ categories, unattributed, waits: 0 }) } });

test('G6_compares_the_partition_with_the_registered_wall_of_the_record_not_with_the_stamped_streams_own_span', () => {
  // the live series: the stamped stream spans exactly what the partition sums to (it is what the partition is cut from), while the registered wall (spawn to exit) is 3.67 s longer
  const live = partition({ build_test: 1888, model_api: 48534, other_tool: 899, hooks: 232, unattributed: 142 }, 142, 55365);
  const rep = report(makeSeries({ records: { aa0001: live } }));
  const g6 = rowOf(rep, 'G-6');
  assert.equal(g6.verdict, 'refuted', 'the old rule held by construction');
  assert.match(g6.observed, /aa0001/);
  assert.match(g6.observed, /registered wall is 55365 ms/, 'names the registered wall');
  assert.match(g6.observed, /3670 ms/, 'names the gap');
  assert.match(g6.observed, /6\.9 %/, 'the gap is counted as unattributed: (142 + 3670) / 55365');
});

test('G6_holds_within_one_ms_of_the_wall_and_a_dated_explained_line_covers_a_gap', () => {
  const sum = 46200;
  assert.equal(rowOf(report(makeSeries({ records: { aa0001: partition({ build_test: 6000, model_api: 40000, other_tool: 100, unattributed: 100 }, 100, sum + 1) } })), 'G-6').verdict, 'holds', 'to the ms: within the rounding of two stamps');
  const gap = { records: { aa0001: partition({ build_test: 6000, model_api: 40000, other_tool: 100, unattributed: 100 }, 100, sum + 2) } };
  assert.equal(rowOf(report(makeSeries(gap)), 'G-6').verdict, 'refuted', '2 ms apart');
  const explained = report(makeSeries(gap), [...GOOD_LINES, '2026-10-30 | G-6 | explained | aa0001 | scratch/g6.txt | operator']);
  assert.equal(rowOf(explained, 'G-6').verdict, 'explained');
  assert.equal(explained.verdict, 'GO');
  const over = report(makeSeries({ records: { aa0001: partition({ build_test: 6000, model_api: 41000, other_tool: 100, unattributed: 100 }, 100, sum) } }));
  assert.equal(rowOf(over, 'G-6').verdict, 'refuted', 'a partition longer than the wall is not the wall either');
});
