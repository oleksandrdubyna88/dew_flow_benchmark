// The exclusive work-before-wait time partition and its span sources (E1.S4, T-T1, T-T3 S4 half).
// Every fixture is SYNTHETIC: no real OTel, audit or stream capture exists yet (E1.S3 step 0 produces one). The tests that pin
// an assumption the capture will settle say so in their names and read the named constants, so the capture's answer is a
// one-line change (OTEL_STAMP_MARKS, HOOK_TIME_SOURCE) plus the flipped pin.
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { PRIORITY, WAITS, WORK } from '../lib/categories.mjs';
import {
  agentSeconds, CLOCK_SKEW_LIMIT_MS, crossCheckTools, HOOK_TIME_SOURCE, inclusive, mapClock, OTEL_STAMP_MARKS, overlapMatrix,
  partition, renderPartition, spansFrom,
} from '../lib/timeline.mjs';
import { spikeDir } from './helpers.mjs';

const S = n => n * 1000;
const span = (category, from, to, agentId = 'main') => ({ category, startMs: S(from), endMs: S(to), agentId });
const wall = (from, to) => ({ startMs: S(from), endMs: S(to) });
const sumOf = cats => Object.values(cats).reduce((a, b) => a + b, 0);
const fixture = () => JSON.parse(readFileSync(path.join(spikeDir, 'test', 'fixtures', 'timeline-session.json'), 'utf8'));
const must = p => {
  assert.equal(p.known, true, p.why);
  return p;
};
/** No output key may name a sum of spans. */
function keysOf(value, found = []) {
  if (value !== null && typeof value === 'object') for (const [k, v] of Object.entries(value)) { found.push(k); keysOf(v, found); }
  return found;
}
const assertNoSpanSumKey = value => assert.deepEqual(keysOf(value).filter(k => /sum/i.test(k)), []);

// ---- the partition ---------------------------------------------------------------------------------------------------

test('Active_is_the_sum_of_work_never_wall_minus_waits', () => {
  const p = must(partition(wall(0, 100), [span('build_test', 0, 40), span('coai_gate', 40, 90)]));
  assert.equal(p.wallMs, S(100));
  assert.equal(p.active, S(40));
  assert.equal(p.unattributed, S(10));
  assert.equal(p.waits, S(50));
  assert.notEqual(p.active, p.wallMs - p.waits);
  const text = renderPartition(p);
  assert.match(text, /active 40\.0 s/);
  assert.match(text, /waits 50\.0 s/);
  assert.match(text, /unattributed 10\.0 s/);
  assert.match(text, /wall 100\.0 s/);
});

test('Active_is_exactly_the_four_work_categories', () => {
  const p = must(partition(wall(0, 100), [span('build_test', 0, 10), span('model_api', 10, 25), span('other_tool', 25, 27), span('hooks', 27, 28), span('sleep300', 28, 90)]));
  assert.equal(p.active, S(28));
  assert.equal(p.categories.unattributed, S(10));
  assert.deepEqual([...WORK], ['build_test', 'model_api', 'other_tool', 'hooks']);
  assert.deepEqual([...WAITS], ['coai_gate', 'consultant', 'ci_wait', 'coderabbit_wait', 'sleep300']);
});

test('Agent_containers_are_excluded', () => {
  const p = must(partition(wall(0, 100), [span('container', 0, 100, 'main'), span('build_test', 10, 40, 'child')]));
  assert.equal('container' in p.categories, false);
  assert.equal(p.categories.build_test, S(30));
  assert.equal(p.categories.unattributed, S(70));
  assert.equal(p.excluded.container, 1);
});

test('One_childs_sleep_cannot_erase_another_childs_build', () => {
  const p = must(partition(wall(0, 300), [
    span('sleep300', 0, 300, 'A'), span('build_test', 100, 160, 'B'), span('model_api', 200, 230, 'B'),
  ]));
  assert.equal(p.categories.build_test, S(60));
  assert.equal(p.categories.model_api, S(30));
  assert.equal(p.categories.sleep300, S(210));
  assert.equal(p.categories.unattributed, 0);
});

test('Every_category_beats_the_ones_below_it_in_priority_order', () => {
  const stacked = PRIORITY.map(c => span(c, 0, 10));
  for (let i = 0; i < PRIORITY.length; i += 1) {
    const p = must(partition(wall(0, 10), stacked.slice(i)));
    assert.equal(p.categories[PRIORITY[i]], S(10), PRIORITY[i]);
    assert.equal(sumOf(p.categories), S(10));
  }
});

test('An_interval_nobody_claims_is_unattributed_not_work', () => {
  const p = must(partition(wall(0, 10), []));
  assert.equal(p.categories.unattributed, S(10));
  assert.equal(p.active, 0);
});

test('Spans_outside_the_wall_are_clipped', () => {
  const p = must(partition(wall(10, 20), [span('build_test', 0, 15), span('model_api', 18, 99), span('hooks', 0, 5), span('other_tool', 30, 40)]));
  assert.equal(p.categories.build_test, S(5));
  assert.equal(p.categories.model_api, S(2));
  assert.equal(p.categories.hooks, 0);
  assert.equal(p.categories.other_tool, 0);
  assert.equal(p.categories.unattributed, S(3));
});

test('Spans_that_cannot_be_placed_are_counted_never_guessed', () => {
  const bad = [{ category: 'build_test', startMs: Number.NaN, endMs: 5 }, { category: 'build_test', startMs: 9, endMs: 3 }, { category: 'mystery', startMs: 0, endMs: 5 }];
  const p = must(partition(wall(0, 10), bad));
  assert.equal(p.ignoredSpans, 2);
  assert.equal(p.excluded.mystery, 1);
  assert.equal(p.categories.unattributed, S(10));
});

test('A_wall_that_cannot_be_measured_is_unknown_not_zero', () => {
  for (const w of [{ startMs: 5, endMs: 5 }, { startMs: 9, endMs: 3 }, { startMs: Number.NaN, endMs: 3 }, undefined]) {
    assert.equal(partition(w, []).known, false);
  }
});

test('Boundaries_are_whole_milliseconds_and_still_sum_exactly', () => {
  const p = must(partition({ startMs: 0.4, endMs: 100.6 }, [{ category: 'model_api', startMs: 10.49, endMs: 20.51 }]));
  assert.equal(Number.isInteger(p.wallMs), true);
  assert.equal(sumOf(p.categories), p.wallMs);
});

/** mulberry32: a seeded generator so the property test replays the same cases every run. */
function rng(seed) {
  let a = seed;
  return () => {
    a = (a + 0x6D2B79F5) | 0;
    let t = Math.imul(a ^ (a >>> 15), 1 | a);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

/** The oracle: for every whole millisecond of the wall, the first category in PRIORITY covering it. */
function bruteForce(w, spans) {
  const totals = Object.fromEntries([...PRIORITY, 'unattributed'].map(c => [c, 0]));
  for (let ms = w.startMs; ms < w.endMs; ms += 1) {
    const hit = PRIORITY.find(c => spans.some(s => s.category === c && s.startMs <= ms && ms < s.endMs));
    totals[hit ?? 'unattributed'] += 1;
  }
  return totals;
}

test('Categories_sum_to_the_wall_exactly', () => {
  const categories = [...PRIORITY, 'container', 'wait'];
  const w = { startMs: 1000, endMs: 4000 };
  for (let seed = 1; seed <= 25; seed += 1) {
    const next = rng(seed);
    const spans = Array.from({ length: 200 }, () => {
      const startMs = Math.floor(next() * 4600) - 300;
      return { category: categories[Math.floor(next() * categories.length)], startMs, endMs: startMs + Math.floor(next() * 900), agentId: `a${Math.floor(next() * 4)}` };
    });
    const p = must(partition(w, spans));
    assert.equal(sumOf(p.categories), w.endMs - w.startMs, `seed ${seed}`);
    assert.deepEqual(p.categories, bruteForce(w, spans), `seed ${seed} against the per-millisecond oracle`);
    assert.equal(p.active + p.waits + p.unattributed, p.wallMs);
  }
});

test('Partitioning_does_not_change_its_inputs', () => {
  const spans = [span('build_test', -5, 15), span('hooks', 2, 3)];
  const before = JSON.stringify(spans);
  partition(wall(0, 10), spans);
  assert.equal(JSON.stringify(spans), before);
});

// ---- inclusive unions, agent-seconds, overlap --------------------------------------------------------------------------

test('Inclusive_unions_and_the_overlap_matrix_are_not_sums', () => {
  const spans = [span('model_api', 0, 10, 'A'), span('model_api', 5, 15, 'B'), span('build_test', 12, 20, 'A')];
  assert.equal(inclusive(spans).model_api.unionMs, S(15));
  const volume = agentSeconds(spans);
  assert.equal(volume.agentMs.model_api, S(20));
  assert.match(volume.label, /work volume/);
  const m = overlapMatrix(spans);
  assert.equal(m.model_api.build_test, S(3));
  assert.equal(m.build_test.model_api, S(3));
  assert.equal(m.model_api.model_api, S(15));
  assert.equal(m.hooks.model_api, 0);
  for (const out of [inclusive(spans), volume, m]) assertNoSpanSumKey(out);
});

test('One_agents_own_overlap_is_not_counted_twice_in_agent_volume', () => {
  const v = agentSeconds([span('model_api', 0, 10, 'A'), span('model_api', 5, 15, 'A')]);
  assert.equal(v.agentMs.model_api, S(15));
});

test('Inclusive_views_exclude_containers_and_clip_to_the_wall', () => {
  const spans = [span('container', 0, 100), span('model_api', -10, 5)];
  assert.equal('container' in inclusive(spans, wall(0, 20)), false);
  assert.equal(inclusive(spans, wall(0, 20)).model_api.unionMs, S(5));
});

test('The_partition_output_names_no_span_sum', () => {
  const p = must(partition(wall(0, 10), [span('build_test', 0, 5)]));
  assertNoSpanSumKey(p);
});

// ---- span sources ----------------------------------------------------------------------------------------------------------

test('Decoded_otel_requests_become_model_api_spans', () => {
  const r = spansFrom({ otel: { present: true, requests: [{ name: 'api_request', tsMs: 10_000, durationMs: 2500, agentId: null }] } });
  assert.deepEqual(r.spans, [{ category: 'model_api', startMs: 7500, endMs: 10_000, agentId: 'main', source: 'otel' }]);
});

test('Assumption_pin_an_otel_api_request_time_is_the_END_of_the_request', () => {
  // A-O2 of the E1.S3 plan: the capture decides. If it says 'start', change OTEL_STAMP_MARKS (one line) and flip this pin.
  assert.equal(OTEL_STAMP_MARKS, 'end');
});

test('The_other_stamp_marks_build_the_other_spans', () => {
  const otel = { present: true, requests: [{ tsMs: 10_000, durationMs: 2500 }] };
  assert.deepEqual(spansFrom({ otel, otelStampMarks: 'start' }).spans.map(s => [s.startMs, s.endMs]), [[10_000, 12_500]]);
  const unknown = spansFrom({ otel, otelStampMarks: 'unknown' });
  assert.deepEqual(unknown.spans, []);
  assert.match(unknown.notes.join('\n'), /model_api.*not captured/);
});

test('A_request_without_a_duration_is_listed_not_given_a_zero_length', () => {
  const r = spansFrom({ otel: { present: true, requests: [{ tsMs: 10_000, durationMs: null, missing: ['duration_ms'] }, { tsMs: 20_000, durationMs: 1000 }] } });
  assert.equal(r.spans.length, 1);
  assert.match(r.notes.join('\n'), /1 api_request.*duration/);
});

test('Snake_case_attributes_from_a_raw_decoded_record_are_read_too', () => {
  const r = spansFrom({ otel: [{ name: 'api_request', tsMs: 5000, attrs: { duration_ms: 1000 } }] });
  assert.deepEqual(r.spans.map(s => [s.startMs, s.endMs]), [[4000, 5000]]);
});

test('Hook_spans_use_arrival_stamps', () => {
  const hook = (t, subtype, extra = {}) => ({ t, line: { type: 'system', subtype, hook_id: 'h1', hook_name: 'PreToolUse:Bash', ...extra } });
  const r = spansFrom({ stamped: [hook(1000, 'hook_started'), { t: 1050, line: { type: 'assistant' } }, hook(1120, 'hook_response')] });
  assert.deepEqual(r.spans, [{ category: 'hooks', startMs: 1000, endMs: 1120, agentId: 'main', source: 'stream.hooks', label: 'PreToolUse:Bash' }]);
});

test('Assumption_pin_hook_events_carry_no_timestamp_so_the_runners_arrival_stamp_is_the_time', () => {
  // Verified on the two-children fixture: hook_started/hook_response have hook_id and no timestamp. A capture that shows
  // timestamps on them changes HOOK_TIME_SOURCE to 'event' (one line) and flips this pin.
  assert.equal(HOOK_TIME_SOURCE, 'arrival');
  const line = (t, subtype) => ({ t, line: { type: 'system', subtype, hook_id: 'h', timestamp: new Date(t + 99_000).toISOString() } });
  const stamped = [line(1000, 'hook_started'), line(1200, 'hook_response')];
  assert.deepEqual(spansFrom({ stamped }).spans.map(s => [s.startMs, s.endMs]), [[1000, 1200]]);
  assert.deepEqual(spansFrom({ stamped, hookTimeSource: 'event' }).spans.map(s => [s.startMs, s.endMs]), [[1000 + 99_000, 1200 + 99_000]]);
});

test('Stamped_stream_lines_may_be_JSON_text_and_a_broken_one_is_counted', () => {
  const t = (ms, o) => ({ t: ms, line: typeof o === 'string' ? o : JSON.stringify(o) });
  const r = spansFrom({ stamped: [t(1, { type: 'system', subtype: 'hook_started', hook_id: 'x' }), t(2, 'not json'), t(9, { type: 'system', subtype: 'hook_response', hook_id: 'x' })] });
  assert.equal(r.spans.length, 1);
  assert.match(r.notes.join('\n'), /1 stamped stream line.*not JSON/);
});

test('A_hook_that_never_responded_is_listed_unpaired_with_no_span', () => {
  const r = spansFrom({ stamped: [{ t: 5, line: { type: 'system', subtype: 'hook_started', hook_id: 'lost' } }] });
  assert.deepEqual(r.spans, []);
  assert.deepEqual(r.unpaired.map(u => [u.kind, u.id, u.missing]), [['hook', 'lost', 'hook_response']]);
});

const rec = (event, ts, id, tool, input = {}, agent = null) => ({ ts, event, tool_use_id: id, tool_name: tool, agent_id: agent, input });

test('Tool_spans_pair_pre_and_post_by_tool_use_id_and_take_their_category_from_the_tool', () => {
  const r = spansFrom({
    audit: [
      rec('PreToolUse', '2026-10-29T12:00:00.000Z', 'a', 'Bash', { command: 'dotnet build' }, 'child1'),
      rec('PreToolUse', '2026-10-29T12:00:01.000Z', 'b', 'Read'),
      rec('PostToolUse', '2026-10-29T12:00:02.000Z', 'b', 'Read'),
      rec('PostToolUseFailure', '2026-10-29T12:00:09.000Z', 'a', 'Bash', {}, 'child1'),
      rec('PreToolUse', '2026-10-29T12:00:10.000Z', 'c', 'Agent'),
      rec('PostToolUse', '2026-10-29T12:00:20.000Z', 'c', 'Agent'),
      rec('PreToolUse', '2026-10-29T12:00:20.000Z', 'd', 'Bash', { command: 'sleep 300' }),
      rec('PostToolUse', '2026-10-29T12:05:20.000Z', 'd', 'Bash'),
    ],
  });
  const by = Object.fromEntries(r.spans.map(s => [s.toolUseId, s]));
  assert.equal(by.a.category, 'build_test');
  assert.equal(by.a.agentId, 'child1');
  assert.equal(by.a.endMs - by.a.startMs, 9000);
  assert.equal(by.a.failed, true);
  assert.equal(by.b.category, 'other_tool');
  assert.equal(by.b.agentId, 'main');
  assert.equal(by.c.category, 'container');
  assert.equal(by.d.category, 'sleep300');
  assert.equal(by.d.endMs - by.d.startMs, 300_000);
});

test('A_tool_that_never_returned_is_listed_unpaired', () => {
  const r = spansFrom({ audit: [rec('PreToolUse', '2026-10-29T12:00:00Z', 'x', 'Bash', { command: 'ls' }), rec('PostToolUse', '2026-10-29T12:00:01Z', 'orphan', 'Read')] });
  assert.deepEqual(r.spans, []);
  assert.deepEqual(r.unpaired.map(u => [u.id, u.missing]).sort(), [['orphan', 'PreToolUse'], ['x', 'PostToolUse']]);
});

test('A_record_with_an_unreadable_timestamp_is_unpaired_not_dated_zero', () => {
  const r = spansFrom({ audit: [rec('PreToolUse', 'yesterday', 'x', 'Read'), rec('PostToolUse', '2026-10-29T12:00:01Z', 'x', 'Read')] });
  assert.deepEqual(r.spans, []);
  assert.equal(r.unpaired[0].id, 'x');
});

test('Coai_rounds_spans_are_carried_and_an_unknown_gate_is_a_note', () => {
  const gate = { category: 'coai_gate', startMs: 1, endMs: 2 };
  assert.deepEqual(spansFrom({ rounds: { known: true, spans: [gate] } }).spans, [gate]);
  const lost = spansFrom({ rounds: { known: false, why: 'no such table: rounds' } });
  assert.deepEqual(lost.spans, []);
  assert.match(lost.notes.join('\n'), /coai rounds.*no such table: rounds/);
});

test('A_generic_wait_becomes_ci_wait_and_coderabbit_wait_where_it_meets_GitHub_intervals', () => {
  const audit = [
    rec('PreToolUse', '1970-01-01T00:00:30.000Z', 'w', 'Bash', { command: 'gh pr checks 3 --watch' }),
    rec('PostToolUse', '1970-01-01T00:00:50.000Z', 'w', 'Bash'),
  ];
  const github = { ciIntervals: [{ startMs: S(35), endMs: S(45) }], reviewIntervals: [{ startMs: S(44), endMs: S(60) }] };
  const r = spansFrom({ audit, github });
  const got = r.spans.map(s => [s.category, s.startMs / 1000, s.endMs / 1000]).sort((a, b) => a[1] - b[1]);
  assert.deepEqual(got, [['ci_wait', 35, 45], ['coderabbit_wait', 44, 50]]);
  assert.match(r.notes.join('\n'), /1 wait span.*outside any GitHub interval/);
});

test('Without_GitHub_intervals_a_generic_wait_stays_unattributed_and_the_note_says_why', () => {
  const audit = [rec('PreToolUse', '1970-01-01T00:00:30Z', 'w', 'Bash', { command: 'gh run watch 1' }), rec('PostToolUse', '1970-01-01T00:00:50Z', 'w', 'Bash')];
  const r = spansFrom({ audit });
  assert.deepEqual(r.spans.map(s => s.category), []);
  assert.match(r.notes.join('\n'), /1 wait span.*stay unattributed.*no GitHub intervals/);
});

test('A_literal_sleep_300_never_waits_on_GitHub_intervals', () => {
  const audit = [rec('PreToolUse', '1970-01-01T00:00:00Z', 's', 'Bash', { command: 'sleep 300' }), rec('PostToolUse', '1970-01-01T00:05:00Z', 's', 'Bash')];
  const r = spansFrom({ audit, github: { ciIntervals: [], reviewIntervals: [] } });
  assert.deepEqual(r.spans.map(s => s.category), ['sleep300']);
});

test('Missing_sources_are_notes_so_the_report_says_what_it_could_not_see', () => {
  const r = spansFrom({});
  assert.deepEqual(r.spans, []);
  const text = r.notes.join('\n');
  for (const word of ['otel', 'audit', 'stamped', 'rounds']) assert.match(text, new RegExp(word));
});

test('A_synthetic_session_partitions_into_the_hand_computed_buckets', () => {
  const f = fixture();
  assert.match(f._label, /^SYNTHETIC/);
  const built = spansFrom({ otel: f.otel, audit: f.audit, stamped: f.stamped, rounds: f.rounds, github: f.github });
  assert.deepEqual(built.unpaired, []);
  const p = must(partition(f.wall, built.spans));
  assert.deepEqual(p.categories, f.expectedMs);
  assert.equal(sumOf(p.categories), 60_000);
  assert.equal(p.active, 27_500);
  assert.equal(p.waits, 20_000);
  assert.equal(p.unattributed, 12_500);
  assert.equal(p.excluded.container, 1);
  assert.equal(inclusive(built.spans, f.wall).coai_gate.unionMs, S(6));
});

// ---- tool cross-check and the clock ------------------------------------------------------------------------------------

const toolSpan = (id, seconds) => ({ category: 'other_tool', startMs: 0, endMs: S(seconds), toolUseId: id });

test('A_tool_span_disagreeing_with_otel_is_listed', () => {
  const r = crossCheckTools([toolSpan('a', 4.0), toolSpan('b', 4.0)], [{ toolUseId: 'a', durationMs: 6000 }, { toolUseId: 'b', durationMs: 4050 }]);
  assert.equal(r.known, true);
  assert.equal(r.compared, 2);
  assert.equal(r.mismatches.length, 1);
  assert.deepEqual(r.mismatches[0], { toolUseId: 'a', auditMs: 4000, otelMs: 6000, deltaMs: -2000, allowedMs: 1000 });
});

test('The_tolerance_is_two_percent_when_that_is_larger_than_one_second', () => {
  const r = crossCheckTools([toolSpan('a', 100)], [{ toolUseId: 'a', durationMs: 101_900 }]);
  assert.deepEqual(r.mismatches, []);
  const worse = crossCheckTools([toolSpan('a', 100)], [{ toolUseId: 'a', durationMs: 102_100 }]);
  assert.equal(worse.mismatches[0].allowedMs, 2042);
});

test('Tools_seen_by_only_one_side_are_listed_not_averaged_away', () => {
  const r = crossCheckTools([toolSpan('a', 1), toolSpan('only-audit', 1)], [{ toolUseId: 'a', durationMs: 1000 }, { toolUseId: 'only-otel', durationMs: 5 }]);
  assert.deepEqual(r.auditOnly, ['only-audit']);
  assert.deepEqual(r.otelOnly, ['only-otel']);
  assert.equal(r.compared, 1);
});

test('Otel_tool_results_without_tool_use_id_make_the_check_unknown_naming_why', () => {
  const r = crossCheckTools([toolSpan('a', 1)], [{ durationMs: 1000 }]);
  assert.deepEqual(r, { known: false, why: 'otel tool_result has no tool_use_id' });
  assert.equal(crossCheckTools([toolSpan('a', 1)], []).known, false);
  assert.equal(crossCheckTools([toolSpan('a', 1)], undefined).known, false);
});

test('The_clock_maps_the_launchers_monotonic_time_to_epoch', () => {
  const c = mapClock({ hrAtSpawn: 1000, epochAtSpawn: 1_800_000_000_000, hrAtExit: 61_000, epochAtExit: 1_800_000_060_000 });
  assert.equal(c.known, true);
  assert.equal(c.toEpoch(31_000), 1_800_000_030_000);
  assert.equal(c.skew.flagged, false);
  assert.equal(c.skew.ms, 0);
});

test('A_clock_skew_above_one_second_is_flagged', () => {
  const c = mapClock({ hrAtSpawn: 0, epochAtSpawn: 1_800_000_000_000, hrAtExit: 60_000, epochAtExit: 1_800_000_061_200 });
  assert.equal(c.skew.ms, 1200);
  assert.equal(c.skew.flagged, true);
  assert.equal(CLOCK_SKEW_LIMIT_MS, 1000);
});

test('A_skew_of_exactly_one_second_is_not_above_it', () => {
  assert.equal(mapClock({ hrAtSpawn: 0, epochAtSpawn: 5, hrAtExit: 10, epochAtExit: 1015 }).skew.flagged, false);
});

test('A_clock_without_an_exit_mapping_has_unknown_skew_not_zero', () => {
  const c = mapClock({ hrAtSpawn: 0, epochAtSpawn: 5 });
  assert.equal(c.known, true);
  assert.equal(c.skew.known, false);
  assert.equal(mapClock({ epochAtSpawn: 5 }).known, false);
});
