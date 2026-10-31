// `aa-verdicts` (E3b.S4): the G table G-1..G-14 and the GO definition of contract section 10, computed from a series' records, run-map, ledger, rehearsal-v2.json and the dated by-hand lines.
// Synthetic series under the fake home; no real session, no paid call.
import './fake-home.mjs'; // first: nothing here may read the operator's files
import assert from 'node:assert/strict';
import { readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { main } from '../bare-runner.mjs';
import { aaVerdicts, renderTable, t3Rule } from '../lib/aa-verdicts.mjs';
import { GOOD_LINES, SMOKE_OK, T0, known, makeInputs, makeSeries } from './aa-fixture.mjs';

const report = (series, lines = GOOD_LINES, smoke = SMOKE_OK) => {
  const r = aaVerdicts({ seriesDir: series, inputsDir: makeInputs(lines), smoke, now: () => new Date(T0) });
  assert.equal(r.ok, true, r.reason);
  return r.value;
};
const rowOf = (rep, id) => rep.rows.find(r => r.id === id);
const refutedIds = rep => rep.rows.filter(r => r.verdict === 'refuted').map(r => r.id);
const ids = Array.from({ length: 14 }, (_, i) => `G-${i + 1}`);
const check = (name, status) => ({ cost: { checks: known(Object.fromEntries(['t1', 't2', 't3', 't4', 't5'].map(t => [t, t === name ? { status, verdict: status === 'breach' ? 'stop' : 'ok' } : { status: 'pass', verdict: 'ok' }]))) } });

test('Every_G_verdict_is_computed_and_none_is_blank', () => {
  const good = report(makeSeries());
  assert.deepEqual(good.rows.map(r => r.id), ids, '14 rows, in order');
  assert.ok(good.rows.every(r => typeof r.observed === 'string' && r.observed.length > 0 && ['holds', 'refuted', 'explained', 'not exercised', 'recorded'].includes(r.verdict)), 'no blank row');
  assert.deepEqual(refutedIds(good), [], 'the good series refutes nothing');
  assert.equal(good.verdict, 'GO', JSON.stringify(good.rows.filter(r => r.verdict !== 'holds')));
});

test('Each_broken_fixture_refutes_exactly_its_G', () => {
  const broken = {
    'G-1': { files: { aa0002: { 'readback.json': `${JSON.stringify({ ok: false, mismatches: ['cwd'] })}\n` } } },
    'G-2': { records: { aa0001: check('t1', 'breach') } },
    'G-3': { records: { pi0001: check('t2', 'breach') } },
    'G-4': { records: { aa0001: check('t3', 'breach') } },
    'G-5': { records: { aa0002: { cost: { brokerUsd: known(0.25) } } } },
    'G-6': { records: { aa0001: { time: { partition: known({ categories: { build_test: 6000, model_api: 40000, other_tool: 100, unattributed: 3200 }, unattributed: 3200, waits: 0 }) } } } },
    'G-7': { files: { aa0002: { 'box/out/audit.ndjson': null } } },
    'G-8': { records: { aa0001: { egress: known({ allowed: { 'example.org': 1 }, refused: {}, nonConnect: 0, tlsRefused: {}, bytes: { 'example.org': 5 }, bytesCaptured: true }) } } },
    'G-11': { records: { aa0001: { run: { maskObserved: known(false) } } } },
    'G-12': { records: { aa0001: { class: 'killed: cap', cost: { censoredAtUsd: known(5.2) } } } },
    'G-14': { rehearsal: { holds: false, stacks: { csharp: { holds: true, checks: [] }, node: { holds: false, checks: [] } }, checks: [] } },
  };
  for (const [g, over] of Object.entries(broken)) assert.deepEqual(refutedIds(report(makeSeries(over))), [g], `${g} broken once refutes ${g} and nothing else`);
  const byHand = (g, rid) => GOOD_LINES.map(l => (l.includes(`| ${g} |`) && l.includes(`| ${rid} |`) ? l.replace('| holds |', '| refuted |') : l));
  assert.deepEqual(refutedIds(report(makeSeries(), byHand('G-10', 'pi0001'))), ['G-10']);
  assert.deepEqual(refutedIds(report(makeSeries(), byHand('G-13', 'aa0002'))), ['G-13']);
});

test('GO_allows_only_the_listed_not_exercised_with_their_evidence', () => {
  const series = makeSeries();
  const go = report(series);
  assert.equal(rowOf(go, 'G-12').verdict, 'not exercised', 'no twin reached the cap');
  assert.match(rowOf(go, 'G-12').replacing, /P-6/, 'its replacing evidence is named');
  assert.equal(rowOf(go, 'G-4').verdict, 'not exercised', 'the twins launched no subagent');
  assert.match(rowOf(go, 'G-4').replacing, /claude-2\.1\.310-bare-smoke/, 'the smoke fixture is named');
  assert.equal(go.verdict, 'GO');
  const noEvidence = report(series, GOOD_LINES.filter(l => !l.includes('| G-12 |')));
  assert.equal(noEvidence.verdict, 'NO GO', 'G-12 not exercised with no replacing evidence');
  assert.equal(rowOf(noEvidence, 'G-12').replacing, null);
  assert.equal(report(series, GOOD_LINES.filter(l => !l.includes('| G-10 |'))).verdict, 'NO GO', 'a by-hand G with no dated line is not exercised and is not in the allowed list');
  assert.equal(report(series, GOOD_LINES, { ...SMOKE_OK, ok: false }).verdict, 'NO GO', 'a smoke whose B-T1 does not hold replaces nothing');
  assert.equal(report(series, GOOD_LINES, { ...SMOKE_OK, cli: '2.1.999' }).verdict, 'NO GO', 'the smoke must be on the same CLI');
  assert.equal(rowOf(report(series, GOOD_LINES.filter(l => !l.includes('| G-13 |'))), 'G-13').verdict, 'not exercised');
});

test('A_missing_pilot_or_a_missing_by_hand_line_is_never_a_hold', () => {
  const twinsOnly = makeSeries({ sessions: [{ rid: 'aa0001', stack: 'csharp', kind: 'aa', twin: 'a', mask: '0-5' }, { rid: 'aa0002', stack: 'csharp', kind: 'aa', twin: 'b', mask: '8-13' }] });
  const rep = report(twinsOnly);
  assert.equal(rep.verdict, 'NO GO');
  assert.ok(['G-1', 'G-2', 'G-3', 'G-5', 'G-6', 'G-8', 'G-11'].every(g => rowOf(rep, g).verdict === 'not exercised'), 'the double-dagger rows need the Node pilot too');
  assert.equal(rowOf(report(makeSeries({ rehearsal: null })), 'G-14').verdict, 'not exercised');
});

test('By_hand_verdicts_are_read_from_dated_lines', () => {
  const lines = [
    'G-10 | holds | aa0001 | scratch/a.txt | operator', // no date: not a line
    '2026-10-30 | G-10 | maybe | aa0001 | scratch/a.txt | operator', // not a verdict
    '2026-10-30 | G-13 | holds | aa0001 | scratch/a.txt', // five fields
    '2026-10-30 | G-10 | holds | pi0001 | scratch/p.txt | operator',
    '2026-10-30 | G-13 | holds | aa0002 | scratch/b.txt | operator',
  ];
  const rep = report(makeSeries(), lines);
  assert.equal(rowOf(rep, 'G-10').verdict, 'not exercised', 'only the pilot line is valid: no twin tree was read by hand');
  assert.equal(rowOf(rep, 'G-13').verdict, 'not exercised');
  assert.equal(rep.malformed.length, 3, 'the invalid lines are reported, never counted');
  assert.match(rowOf(rep, 'G-13').observed, /aa0001/, 'the row names the session with no line');
  assert.equal(rep.verdict, 'NO GO');
});

test('G6_explained_counts_as_holds', () => {
  const unattributed = { records: { aa0001: { time: { partition: known({ categories: { build_test: 6000, model_api: 40000, other_tool: 100, unattributed: 3200 }, unattributed: 3200, waits: 0 }) } } } };
  assert.equal(rowOf(report(makeSeries(unattributed)), 'G-6').verdict, 'refuted', '7 % unattributed with no line');
  const explained = report(makeSeries(unattributed), [...GOOD_LINES, '2026-10-30 | G-6 | explained | aa0001 | scratch/g6.txt | operator']);
  assert.equal(rowOf(explained, 'G-6').verdict, 'explained');
  assert.equal(explained.verdict, 'GO', 'an explained G-6 counts as holds');
  const sum = report(makeSeries({ records: { aa0001: { time: { partition: known({ categories: { build_test: 6000, model_api: 40000, other_tool: 100, unattributed: 90 }, unattributed: 90, waits: 0 }) } } } }));
  assert.equal(rowOf(sum, 'G-6').verdict, 'refuted', 'the categories do not sum to the registered wall');
});

test('Aliases_follow_D12_not_equal_model_sets', () => {
  const aliases = (set, ids2) => ({ cost: { aliases: known({ aliases: set, internal: [], fallbacks: [], unresolved: [], check: { verdict: 'ok', reasons: [] } }) }, ...ids2 });
  const both = { opus: 'claude-opus-5-5', sonnet: 'claude-sonnet-5-5' };
  const baseline = { aliases: both, internal: [] };
  const differentSets = report(makeSeries({ aliases: baseline, records: { aa0001: aliases(both), aa0002: aliases({ opus: 'claude-opus-5-5' }) } }));
  assert.notEqual(rowOf(differentSets, 'G-7').verdict, 'refuted', 'twin A used opus and sonnet, twin B opus only, the same ids: holds');
  const drift = report(makeSeries({ aliases: baseline, records: { aa0001: aliases(both), aa0002: aliases({ opus: 'claude-opus-other' }) } }));
  assert.equal(rowOf(drift, 'G-7').verdict, 'refuted');
  assert.equal(rowOf(drift, 'G-7').stop, true, 'drift is a stop');
  assert.equal(drift.verdict, 'STOP');
});

test('The_T3_rule_arms_only_on_established_coverage', () => {
  assert.equal(t3Rule({ main: true, subHere: true, subSmoke: false }), 'stop', 'main and sub here');
  assert.equal(t3Rule({ main: true, subHere: false, subSmoke: true }), 'stop', 'main here and sub in the smoke fixture');
  assert.equal(t3Rule({ main: true, subHere: false, subSmoke: false }), 'fallback-t2', 'main only');
  assert.equal(t3Rule({ main: false, subHere: true, subSmoke: true }), 'fallback-t2', 'no main coverage');
  assert.equal(report(makeSeries()).t3Rule, 'stop', 'the good twins (main) and the smoke (sub)');
  assert.equal(report(makeSeries(), GOOD_LINES, { ...SMOKE_OK, ok: false }).t3Rule, 'fallback-t2');
});

test('A_twin_difference_is_recorded_not_refuted', () => {
  const rep = report(makeSeries({ records: { aa0002: { class: 'killed: cap', cost: { censoredAtUsd: known(6.1), stopRuleUsd: known(6.1), cliTotalUsd: known(6.1), brokerUsd: known(6.1) }, time: { wallMs: known(90000) }, quality: { heldout: known({ percent: 80, defects: { count: 2, clauses: { c1: 2 } } }) } } } }));
  const g9 = rowOf(rep, 'G-9');
  assert.equal(g9.verdict, 'recorded', 'G-9 never gates');
  assert.ok(g9.recorded.costRatio > 20 && g9.recorded.heldoutPercentDiff === 20, JSON.stringify(g9.recorded));
  assert.equal(g9.recorded.heldoutDefectsDiff, 2, 'the defects of the real record are { count, clauses }: the difference is of the counts');
  assert.equal(rowOf(rep, 'G-12').verdict, 'holds', 'a twin that reached the cap was stopped at settled spend >= $6');
  assert.ok(!refutedIds(rep).includes('G-9'));
});

test('Stops_are_named_as_stops', () => {
  const stops = {
    'G-2': { records: { aa0001: check('t1', 'breach') } },
    'G-8': { records: { pi0001: { class: 'void: contaminated', detail: { high: 3, canary: 0, tokenHits: 0 } } } },
    'G-12': { records: { aa0001: { class: 'killed: cap', cost: { censoredAtUsd: known(5.2) } } } },
  };
  for (const [g, over] of Object.entries(stops)) {
    const rep = report(makeSeries(over));
    assert.equal(rep.verdict, 'STOP', g);
    assert.deepEqual(rep.stops, [g]);
  }
  assert.equal(report(makeSeries({ records: { aa0001: check('t3', 'breach') } })).verdict, 'NO GO', 'G-4 refuted is a NO GO, not a stop');
});

test('The_cap_row_and_the_sampler_row_read_the_sessions_own_evidence', () => {
  assert.equal(rowOf(report(makeSeries({ sampler: { every: 12000 } })), 'G-11').verdict, 'refuted', 'a sampler that wrote every 12 s did not write every 5 s');
  assert.equal(rowOf(report(makeSeries({ sampler: { spin: false } })), 'G-11').verdict, 'refuted', 'no spin probe sample');
  assert.equal(rowOf(report(makeSeries({ records: { aa0001: { run: { quiet: known({ quiet: false, flags: ['host-cpu'], numbers: {}, waitedMs: 0 }) } } } })), 'G-11').verdict, 'refuted', 'the quiet check did not pass');
  assert.equal(rowOf(report(makeSeries({ ledgerSkip: ['aa0002'] })), 'G-7').verdict, 'refuted', 'a twin with no ledger line');
});

test('The_command_writes_the_json_prints_the_table_and_exits_0_on_GO_2_on_NO_GO_and_4_on_a_refusal', async () => {
  const series = makeSeries();
  const run = async (dir, lines) => { const out = []; const code = await main(['aa-verdicts', '--series', dir, '--inputs-dir', makeInputs(lines)], l => out.push(l), { smoke: SMOKE_OK }); return { code, text: out.join('\n') }; };
  const first = await run(series, GOOD_LINES);
  assert.equal(first.code, 0);
  assert.match(first.text, /G-1\b.*holds/);
  assert.match(first.text, /GO/);
  const written = JSON.parse(readFileSync(path.join(series, 'aa-verdicts.json'), 'utf8'));
  assert.equal(written.verdict, 'GO');
  assert.equal(written.rows.length, 14);
  assert.equal((await run(series, [])).code, 2, 'no by-hand line: NO GO');
  assert.equal(JSON.parse(readFileSync(path.join(series, 'aa-verdicts.json'), 'utf8')).verdict, 'NO GO');
  assert.equal((await run(path.join(series, 'nowhere'), GOOD_LINES)).code, 4, 'no run-map: a refusal');
  const out = [];
  assert.equal(await main(['aa-verdicts'], l => out.push(l), {}), 4, 'no --series');
});

test('The_command_reads_the_series_and_changes_nothing_but_its_own_json', async () => {
  const series = makeSeries();
  const watched = ['run-map.json', 'ledger.ndjson', 'rehearsal-v2.json', 's/aa0001/record.json'].map(f => [f, readFileSync(path.join(series, f), 'utf8')]);
  await main(['aa-verdicts', '--series', series, '--inputs-dir', makeInputs(GOOD_LINES)], () => {}, { smoke: SMOKE_OK });
  for (const [f, text] of watched) assert.equal(readFileSync(path.join(series, f), 'utf8'), text, f);
  assert.ok(renderTable(report(series)).length > 14, 'the table has a line per G and a verdict line');
  writeFileSync(path.join(series, 'aa-verdicts.json'), '{}');
});

test('The_printed_table_never_clips_an_observation_or_a_replacing_evidence', () => {
  const long = `${'a long observation '.repeat(40)}END`;
  const rep = report(makeSeries());
  const lines = renderTable({ ...rep, rows: rep.rows.map(r => (r.id === 'G-1' ? { ...r, observed: long, replacing: long } : r)) });
  assert.ok(lines.filter(l => l.includes(long)).length === 2, 'both are printed whole');
  assert.ok(!lines.some(l => l.includes('…')), 'nothing is cut with an ellipsis');
});
