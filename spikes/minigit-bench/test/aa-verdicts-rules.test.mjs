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

// ---- G-12 -------------------------------------------------------------------------------------------------------------------------

const withG12 = lines => [...GOOD_LINES.filter(l => !l.includes('| G-12 |')), ...lines];
const overCap = { cost: { stopRuleUsd: known(6.2), cliTotalUsd: known(6.2), brokerUsd: known(6.2) } };

test('G12_is_refuted_and_a_stop_when_a_session_reached_the_cap_and_was_not_killed_by_it', () => {
  const rep = report(makeSeries({ records: { aa0001: overCap } }));
  const g12 = rowOf(rep, 'G-12');
  assert.equal(g12.verdict, 'refuted', JSON.stringify(g12));
  assert.match(g12.observed, /aa0001.*\$6\.2.*cap \$6.*completed/);
  assert.equal(g12.stop, true);
  assert.equal(rep.verdict, 'STOP');
  const killed = report(makeSeries({ records: { aa0001: { ...overCap, class: 'killed: cap', cost: { ...overCap.cost, censoredAtUsd: known(6.2) } } } }));
  assert.equal(rowOf(killed, 'G-12').verdict, 'holds', 'a session that reached the cap and was killed by it, at settled spend of at least the cap');
  assert.equal(rowOf(report(makeSeries({ records: { aa0001: { cost: { stopRuleUsd: known(5.99) } } } })), 'G-12').verdict, 'not exercised', 'under the cap');
});

test('G12_applies_a_dated_refuted_line_and_picks_the_latest_line_by_date_not_by_file_order', () => {
  const refutedLine = '2026-10-30 | G-12 | refuted | aa0001 | scratch/g12-over.txt | operator';
  const rep = report(makeSeries(), withG12([refutedLine]));
  assert.equal(rowOf(rep, 'G-12').verdict, 'refuted');
  assert.equal(rep.verdict, 'STOP');
  const holdsLater = `2026-10-31 | G-12 | holds | aa0001 | test/broker.test.mjs P-6 green at ${'e'.repeat(12)} | operator`;
  assert.equal(rowOf(report(makeSeries(), withG12([holdsLater, refutedLine])), 'G-12').verdict, 'not exercised', 'the refuted line is the older: the file order does not decide');
  assert.equal(rowOf(report(makeSeries(), withG12([refutedLine, holdsLater])), 'G-12').verdict, 'not exercised');
  const refutedLater = '2026-11-01 | G-12 | refuted | aa0001 | scratch/g12-over.txt | operator';
  assert.equal(rowOf(report(makeSeries(), withG12([refutedLater, holdsLater])), 'G-12').verdict, 'refuted', 'the refuted line is the latest, though it comes first in the file');
});

test('G12_replacing_evidence_names_P6_and_the_harness_identity_the_run_is_at', () => {
  const at = identity => `2026-10-30 | G-12 | holds | aa0001 | test/broker.test.mjs P-6 and test/budget.test.mjs green at ${identity} | operator`;
  const row = lines => rowOf(report(makeSeries(), withG12(lines)), 'G-12');
  assert.match(row([at('e'.repeat(12))]).replacing, /P-6/, 'P-6 and the series identity: evidence');
  assert.equal(row([`2026-10-30 | G-12 | holds | aa0001 | test/budget.test.mjs green at ${'e'.repeat(12)} | operator`]).replacing, null, 'no P-6');
  assert.equal(row(['2026-10-30 | G-12 | holds | aa0001 | test/broker.test.mjs P-6 green at the run identity | operator']).replacing, null, 'no 12-hex identity');
  const other = row([at('a'.repeat(12))]);
  assert.equal(other.replacing, null, 'another identity');
  assert.match(other.observed, /harness identity eeeeeeeeeeee/, 'the row says which identity it wanted');
  const amended = (line) => rowOf(report(makeSeries(), [...withG12([line]), '2026-10-30 | instrument | harness:bbbbbbbbbbbb | instrument: a fix (G-8)']), 'G-12');
  assert.equal(amended(at('e'.repeat(12))).replacing, null, 'an instrument line moved the run to bbbbbbbbbbbb: the old identity is not the run\'s');
  assert.match(amended(at('b'.repeat(12))).replacing, /P-6/);
  assert.equal(report(makeSeries(), withG12([at('a'.repeat(12))])).verdict, 'NO GO');
  const dated = rowOf(report(makeSeries(), withG12([at('e'.repeat(12)).replace('2026-10-30', '2026-10-29'), at('a'.repeat(12))])), 'G-12');
  assert.equal(dated.replacing, null, 'the latest line by date names another identity, the older one is not used');
});

// ---- G-10, G-13: `explained` is not a hold --------------------------------------------------------------------------------------

test('An_explained_line_is_no_hold_for_G10_and_G13_the_contract_allows_it_only_for_G6_G5_and_G8_refused_hosts', () => {
  const asExplained = (g, rid) => GOOD_LINES.map(l => (l.includes(`| ${g} |`) && l.includes(`| ${rid} |`) ? l.replace('| holds |', '| explained |') : l));
  for (const [g, rid] of [['G-10', 'pi0001'], ['G-13', 'aa0002']]) {
    const rep = report(makeSeries(), asExplained(g, rid));
    assert.equal(rowOf(rep, g).verdict, 'not exercised', g);
    assert.match(rowOf(rep, g).observed, new RegExp(`${rid}.*explained`), 'the row says why');
    assert.equal(rep.verdict, 'NO GO', g);
  }
});

// ---- G-11: the sampler wrote every 5 s ------------------------------------------------------------------------------------------

test('G11_refutes_a_gap_over_the_registered_interval_plus_a_one_second_jitter_and_not_a_double_interval', () => {
  const g11 = every => rowOf(report(makeSeries({ sampler: { every } })), 'G-11');
  assert.equal(g11(5000).verdict, 'holds');
  assert.equal(g11(6000).verdict, 'holds', 'one second of jitter is allowed');
  assert.equal(g11(6100).verdict, 'refuted', '6.1 s is not every 5 s (the old rule allowed 10 s)');
  assert.match(g11(7000).observed, /gap of 7000 ms.*interval is 5000 ms/);
});

// ---- G-7: aliases ---------------------------------------------------------------------------------------------------------------

const usedAliases = aliases => ({ cost: { aliases: known({ aliases, internal: [], fallbacks: [], unresolved: [], check: { verdict: 'ok', reasons: [] } }) } });

test('G7_a_missing_alias_record_or_an_alias_the_baseline_lacks_is_NO_GO_and_only_drift_is_a_STOP', () => {
  const noRecord = report(makeSeries({ records: { aa0002: { cost: { aliases: { known: false, why: 'no init event' } } } } }));
  assert.equal(rowOf(noRecord, 'G-7').verdict, 'refuted');
  assert.match(rowOf(noRecord, 'G-7').observed, /aa0002: no alias record/);
  assert.equal(rowOf(noRecord, 'G-7').stop, false, 'no record is not drift');
  assert.equal(noRecord.verdict, 'NO GO');
  const unknownAlias = report(makeSeries({ records: { aa0001: usedAliases({ opus: 'claude-opus-5-5', haiku: 'claude-haiku-5-5' }) } }));
  assert.equal(rowOf(unknownAlias, 'G-7').verdict, 'refuted');
  assert.match(rowOf(unknownAlias, 'G-7').observed, /aa0001.*alias haiku.*not in the baseline/, 'the alias is named');
  assert.equal(unknownAlias.verdict, 'NO GO');
  const drift = report(makeSeries({ records: { aa0001: usedAliases({ opus: 'claude-opus-other' }) } }));
  assert.equal(drift.verdict, 'STOP', 'drift stays a stop');
  assert.equal(drift.verdict === 'STOP' && rowOf(drift, 'G-7').stop, true);
});
