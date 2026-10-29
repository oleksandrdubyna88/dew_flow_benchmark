import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { loadPolicy } from '../lib/crosscheck.mjs';
import { loadPrices } from '../lib/prices.mjs';
import { ASSUMPTIONS, computeVerdicts, decisionsOf, renderCapture, renderVerdicts, verdictBlockOf } from '../capture/verdicts.mjs';
import { derivedCapture } from './derived-capture.mjs';
import { spikeDir } from './helpers.mjs';

const readJson = (...p) => JSON.parse(readFileSync(path.join(spikeDir, ...p), 'utf8'));
const ctx = { table: loadPrices(readJson('inputs', 'prices-2026-10-29.json')).value, policy: loadPolicy(readJson('inputs', 'crosscheck-policy.json')).value };
const rowsOf = capture => computeVerdicts(capture, ctx);
const verdictsOf = capture => Object.fromEntries(rowsOf(capture).map(r => [r.id, r.verdict]));
const IDS = ['A-E1', 'A-E2', 'A-S1', 'A-T1', 'A-T2', 'A-J1', 'A-O1', 'A-O2', 'A-O3', 'A-O4', 'A-O5', 'A-H1', 'A-H2', 'A-M1', 'A-P1'];

test('Every_assumption_of_the_plan_has_a_row_and_none_is_blank', () => {
  assert.deepEqual(ASSUMPTIONS.map(([id]) => id), IDS);
  const rows = rowsOf(derivedCapture());
  assert.deepEqual(rows.map(r => r.id), IDS);
  for (const r of rows) {
    assert.ok(['holds', 'refuted', 'not exercised'].includes(r.verdict), `${r.id}: ${r.verdict}`);
    assert.ok(typeof r.observed === 'string' && r.observed.length > 10, `${r.id} observed`);
    assert.ok(r.assumption.length > 10);
  }
});

test('On_data_derived_from_the_real_stream_the_rules_say_what_that_data_supports', () => {
  const v = verdictsOf(derivedCapture());
  assert.deepEqual(v, {
    'A-E1': 'holds', 'A-E2': 'holds', 'A-S1': 'holds', 'A-T1': 'holds',
    'A-T2': 'refuted', // the transcripts here are the STREAM's lines, whose output_tokens are placeholders
    'A-J1': 'holds', 'A-O1': 'holds', 'A-O2': 'holds',
    'A-O3': 'refuted', 'A-O4': 'refuted', // the synthetic api_request records carry the placeholder outputs and a flat cost
    'A-O5': 'holds', 'A-H1': 'holds', 'A-H2': 'holds', 'A-M1': 'holds', 'A-P1': 'holds',
  });
});

test('The_real_envelope_rules_report_the_numbers_they_rest_on', () => {
  const rows = Object.fromEntries(rowsOf(derivedCapture()).map(r => [r.id, r]));
  assert.match(rows['A-E1'].observed, /claude-sonnet-5-5.*usage\.output_tokens 356/);
  assert.match(rows['A-E2'].observed, /total_cost_usd 0\.594582/);
  assert.match(rows['A-P1'].observed, /claude-sonnet-5-5: costUSD 0\.0233/);
  assert.match(rows['A-M1'].observed, /fable not exercised/);
  assert.match(rows['A-M1'].observed, /subagent tool reported by init: Task/, 'the 2.1.289 init names the tool Task');
  assert.match(rows['A-O1'].observed, /"input_tokens":\["intValue"\]/, 'the observed value kinds are recorded: they decide the numeric-string switch');
});

test('A_stream_time_near_the_record_time_means_the_record_marks_the_end', () => {
  const row = rowsOf(derivedCapture({ stamp: 'end' })).find(r => r.id === 'A-O2');
  assert.deepEqual([row.verdict, row.decision], ['holds', 'end']);
});

test('A_stream_time_a_duration_after_the_record_means_the_record_marks_the_start', () => {
  const rows = rowsOf(derivedCapture({ stamp: 'start' }));
  const row = rows.find(r => r.id === 'A-O2');
  assert.deepEqual([row.verdict, row.decision], ['refuted', 'start']);
  assert.equal(decisionsOf(rows).stampMarks, 'start');
});

test('Requests_too_short_to_tell_start_from_end_are_not_evidence', () => {
  const rows = rowsOf(derivedCapture({ stamp: 'end', durationMs: 1500 }));
  const row = rows.find(r => r.id === 'A-O2');
  assert.deepEqual([row.verdict, row.decision], ['not exercised', 'unknown']);
  assert.match(row.observed, /no request was long enough/);
});

test('A_protobuf_transport_refutes_the_json_assumption_and_says_what_arrived', () => {
  const v = rowsOf(derivedCapture({ contentType: 'application/x-protobuf' })).find(r => r.id === 'A-O1');
  assert.equal(v.verdict, 'refuted');
  assert.match(v.observed, /application\/x-protobuf/);
});

test('A_capture_without_transcripts_refutes_the_transcript_rows_and_the_join', () => {
  const v = verdictsOf(derivedCapture({ withTranscripts: false }));
  assert.equal(v['A-T1'], 'refuted');
  assert.equal(v['A-J1'], 'refuted');
  assert.equal(v['A-T2'], 'refuted');
  assert.equal(decisionsOf(rowsOf(derivedCapture({ withTranscripts: false }))).join, 'query_source');
});

test('A_hook_event_that_now_carries_a_timestamp_refutes_the_hook_assumption', () => {
  const c = derivedCapture();
  const events = c.events.map(e => (e.subtype === 'hook_started' ? { ...e, timestamp: '2026-10-29T00:00:00Z' } : e));
  const row = rowsOf({ ...c, events }).find(r => r.id === 'A-H1');
  assert.equal(row.verdict, 'refuted');
  assert.match(row.observed, /1 with a timestamp/);
});

test('A_model_id_the_price_table_lacks_refutes_the_exact_id_assumption', () => {
  const c = derivedCapture();
  const renamed = ({ 'claude-opus-5-5': opus, ...rest }) => ({ ...rest, 'claude-opus-5-5-20260101': opus });
  const events = c.events.map(e => (e.type === 'result' ? { ...e, modelUsage: renamed(e.modelUsage) } : e));
  const row = rowsOf({ ...c, events }).find(r => r.id === 'A-M1');
  assert.equal(row.verdict, 'refuted');
  assert.match(row.observed, /claude-opus-5-5-20260101/);
});

test('A_sonnet_line_billed_at_the_published_rate_refutes_the_cli_rate_assumption', () => {
  const c = derivedCapture();
  const events = c.events.map(e => (e.type === 'result' ? { ...e, modelUsage: { ...e.modelUsage, 'claude-sonnet-5-5': { ...e.modelUsage['claude-sonnet-5-5'], costUSD: 0.0227431 } } } : e));
  assert.equal(rowsOf({ ...c, events }).find(r => r.id === 'A-P1').verdict, 'refuted');
});

test('A_subagent_with_the_real_query_source_spelling_is_exercised_not_skipped', () => {
  const row = rowsOf(derivedCapture({ spelling: 'real' })).find(r => r.id === 'A-O5');
  assert.equal(row.verdict, 'holds', row.observed);
  assert.match(row.observed, /7 subagent record/);
});

test('A_child_without_a_parent_agent_id_refutes_A_O5_as_worded_and_says_what_did_hold', () => {
  const row = rowsOf(derivedCapture({ spelling: 'real', parentAgent: false })).find(r => r.id === 'A-O5');
  assert.equal(row.verdict, 'refuted');
  assert.match(row.observed, /agent_id present on 7 of 7/);
  assert.match(row.observed, /parent_agent_id present on 0 of 7/);
  assert.match(row.observed, /match the Agent results' agentIds/);
});

const withInitVersion = (c, version) => ({ ...c, events: c.events.map(e => (e.type === 'system' && e.subtype === 'init' ? { ...e, claude_code_version: version } : e)) });
const sonnetCost = (c, costUSD) => ({ ...c, events: c.events.map(e => (e.type === 'result' ? { ...e, modelUsage: { ...e.modelUsage, 'claude-sonnet-5-5': { ...e.modelUsage['claude-sonnet-5-5'], costUSD } } } : e)) });

test('A_P1_compares_with_the_cli_billed_view_of_the_captures_own_cli_version', () => {
  const published = 0.0227431;
  const billedAt20 = 0.0233382;
  const verdict = (version, cost) => rowsOf(sonnetCost(withInitVersion(derivedCapture(), version), cost)).find(r => r.id === 'A-P1').verdict;
  assert.equal(verdict('2.1.289', billedAt20), 'holds', 'the pilot observation');
  assert.equal(verdict('2.1.289', published), 'refuted');
  assert.equal(verdict('2.1.310', published), 'holds', 'on 2.1.310 the CLI bills Sonnet cache reads at the published rate');
  assert.equal(verdict('2.1.310', billedAt20), 'refuted', 'the 2.1.289 rate does not leak into 2.1.310');
  assert.equal(verdict('9.9.9', published), 'holds', 'an unobserved version is billed as published');
});

test('The_decisions_follow_the_rows', () => {
  const rows = rowsOf(derivedCapture());
  assert.deepEqual(decisionsOf(rows), { join: 'request_id', t3OutputSource: 'otel', stampMarks: 'end' });
  const flip = (id, verdict) => rows.map(r => (r.id === id ? { ...r, verdict } : r));
  assert.equal(decisionsOf(flip('A-J1', 'refuted')).join, 'query_source');
  assert.equal(decisionsOf(flip('A-T2', 'holds')).t3OutputSource, 'transcript');
  assert.equal(decisionsOf(flip('A-T2', 'not exercised')).t3OutputSource, null);
});

test('The_rendered_table_has_a_row_per_assumption_escapes_pipes_and_round_trips_through_the_markers', () => {
  const rows = [{ id: 'A-X1', assumption: 'a | b', verdict: 'holds', observed: 'line one\nline two | three' }];
  const md = renderVerdicts(rows);
  assert.match(md, /^<!-- verdicts:begin -->\n\| Id \| Assumption \| Verdict \| Observed \|\n\|---\|---\|---\|---\|\n/);
  assert.ok(md.includes('| A-X1 | a \\| b | holds | line one line two \\| three |'));
  assert.equal(verdictBlockOf(`preamble\n${md}\npostamble`), md);
  assert.equal(verdictBlockOf('no block'), null);
  assert.equal(renderVerdicts(rowsOf(derivedCapture())).split('\n').length, 15 + 4);
});

test('CAPTURE_md_carries_the_date_version_command_env_keys_cost_and_sanitiser_hash', () => {
  const c = derivedCapture();
  const md = renderCapture({ meta: c.meta, argv: c.argv, command: 'node capture-session.mjs run --out X', sanitiserSha256: 'ab'.repeat(32), rows: rowsOf(c) });
  for (const part of ['0.0.0-derived', '2026-10-29', 'node capture-session.mjs run --out X', 'HOME, PATH', '$0.594582', 'pilot spend', 'ab'.repeat(32), 'claude -p']) assert.ok(md.includes(part), part);
  assert.ok(md.includes(renderVerdicts(rowsOf(c))), 'the table inside is exactly renderVerdicts');
});
