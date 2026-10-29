import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { envelopeView, transcriptView } from '../lib/claude-usage.mjs';
import { apiRequests, decodeOtlpLogs, otelView } from '../lib/otel-decode.mjs';
import { loadPrices } from '../lib/prices.mjs';
import { renderUsd } from '../lib/render.mjs';
import { sessionCost } from '../lib/session-cost.mjs';
import { spikeDir } from './helpers.mjs';
import { TWO_CHILDREN, apiAttrs, asTranscripts, otlpPayload, readEvents } from './synth.mjs';

const readText = (...p) => readFileSync(path.join(spikeDir, ...p), 'utf8');
const table = loadPrices(JSON.parse(readText('inputs', 'prices-2026-10-29.json'))).value;
const real = readEvents(TWO_CHILDREN);
const lastUser = real.map(e => e.type).lastIndexOf('user');
const killed = real.slice(0, lastUser + 1);
const syntheticO = () => otelView(apiRequests(decodeOtlpLogs(readText('test', 'fixtures', 'otel-synthetic-api-requests.json')).value));
const near = (a, b, eps = 5e-7) => assert.ok(Math.abs(a - b) < eps, `${a} is not within ${eps} of ${b}`);

test('An_envelope_less_session_gets_no_number_from_fitted_rates', () => {
  assert.equal(killed.some(e => e.type === 'result'), false, 'the fixture really was cut before its result');
  assert.ok(killed.length < real.length && killed.length > 10);
  const cost = sessionCost({ E: envelopeView(killed), O: otelView([]), T: null, table });
  assert.deepEqual(cost, { known: false, why: 'no envelope and no otel records' });
  assert.equal(renderUsd(cost), 'unknown');
  assert.deepEqual(sessionCost({ E: envelopeView(killed), O: null, T: null, table }), cost);
});

test('A_killed_session_with_otel_is_priced_from_otel_and_flagged', () => {
  const cost = sessionCost({ E: envelopeView(killed), O: syntheticO(), T: null, table });
  assert.equal(cost.known, true);
  assert.ok(cost.flags.includes('no envelope'));
  assert.match(cost.basis, /otel/);
  assert.deepEqual(cost.cliTotalUsd, { known: false, why: 'no envelope' });
  assert.equal(cost.perModel['claude-opus-5-5'].exact, false, 'no transcript: the opus cache write is an interval');
  assert.equal(cost.perModel['claude-haiku-4-5-20251001'].exact, true, 'no cache writes: nothing to split');
  assert.ok(cost.low <= cost.usd && cost.usd <= cost.high);
  assert.equal(cost.exact, false);
});

test('A_killed_session_with_otel_and_a_transcript_split_is_exact_where_the_split_is_known', () => {
  const T = transcriptView(asTranscripts(real));
  const cost = sessionCost({ E: envelopeView(killed), O: syntheticO(), T, table });
  assert.equal(cost.perModel['claude-sonnet-5-5'].exact, true, 'the sonnet child carries the split');
  // 4 in, 118 out, 5951 read, 8384 written (all 5m in the transcripts), published sonnet rates
  near(cost.perModel['claude-sonnet-5-5'].usd, (4 * 2 + 118 * 10 + 5951 * 0.1 + 8384 * 2.5) / 1e6, 1e-12);
});

test('A_session_with_an_envelope_reports_the_clis_total_beside_the_headline', () => {
  const cost = sessionCost({ E: envelopeView(real), O: null, T: transcriptView(asTranscripts(real)), table });
  assert.equal(cost.known, true);
  assert.equal(cost.cliTotalUsd, 0.594582);
  assert.deepEqual(cost.flags, []);
  near(cost.usd, 0.5697508 + 0.0227431 + 0.001493);
  assert.ok(cost.usd < cost.cliTotalUsd, 'published Sonnet cache reads are cheaper than the CLI bills them');
});

test('Incomplete_otel_records_make_the_killed_session_figure_a_lower_bound_and_say_so', () => {
  const O = { ...syntheticO(), incomplete: 2 };
  const cost = sessionCost({ E: { known: false, why: 'x' }, O, T: null, table });
  assert.equal(cost.lowerBound, true);
  assert.ok(cost.flags.some(f => /2 otel records lack a token field/.test(f)));
});

test('An_unpriced_model_in_otel_makes_the_killed_session_unknown_with_the_other_lines_priced', () => {
  const cost = killedFromSpecs([{ model: 'claude-nope-1' }, { model: 'claude-sonnet-5-5' }]);
  assert.equal(cost.known, false);
  assert.equal(cost.perModel['claude-sonnet-5-5'].known, true);
  assert.equal(cost.perModel['claude-nope-1'].known, false);
  assert.ok(cost.flags.includes('no envelope'));
});

const killedFromSpecs = specs => {
  const records = specs.map((attrs, i) => ({ name: 'api_request', tsMs: 1760000000000 + i, attrs: apiAttrs({ request_id: `r${i}`, ...attrs }) }));
  const O = otelView(apiRequests(decodeOtlpLogs(JSON.stringify(otlpPayload(records))).value));
  return sessionCost({ E: { known: false, why: 'killed' }, O, T: null, table });
};

test('A_killed_session_whose_records_lack_query_source_is_priced_the_same_as_with_it', () => {
  const withSource = killedFromSpecs([{ cache_creation_tokens: 0 }, { model: 'claude-sonnet-5-5', cache_creation_tokens: 0 }]);
  const without = killedFromSpecs([{ query_source: undefined, cache_creation_tokens: 0 }, { query_source: undefined, model: 'claude-sonnet-5-5', cache_creation_tokens: 0 }]);
  assert.equal(without.known, true);
  assert.ok(without.usd > 0, 'absent is not zero');
  near(without.usd, withSource.usd, 1e-12);
});

test('A_killed_session_with_no_complete_record_is_unknown_never_zero_dollars', () => {
  const allMissing = killedFromSpecs([{ cache_read_tokens: undefined }, { cache_read_tokens: undefined, request_id: 'r9' }]);
  assert.deepEqual(allMissing, { known: false, why: 'no complete api_request record', flags: ['no envelope', '2 otel records lack a token field and are left out: a lower bound'], cliTotalUsd: { known: false, why: 'no envelope' } });
  assert.equal(renderUsd(allMissing), 'unknown');
});

test('A_model_whose_every_record_is_incomplete_makes_the_killed_session_unknown_not_cheaper', () => {
  const cost = killedFromSpecs([{}, { model: 'claude-sonnet-5-5', input_tokens: undefined }]);
  assert.equal(cost.known, false);
  assert.match(cost.why, /claude-sonnet-5-5/);
});

test('A_lower_bound_is_never_exact', () => {
  const cost = killedFromSpecs([{ cache_creation_tokens: 0 }, { cache_creation_tokens: 0, output_tokens: undefined, request_id: 'r_incomplete' }]);
  assert.equal(cost.known, true);
  assert.equal(cost.lowerBound, true);
  assert.equal(cost.exact, false);
  assert.equal(cost.perModel['claude-opus-5-5'].exact, true, 'the model line itself is exact; the session is a lower bound');
});

test('A_killed_session_is_priced_exactly_from_the_otel_cache_write_split_when_it_is_present', () => {
  const cost = killedFromSpecs([{ cache_creation_tokens: 1000, cache_creation_5m_tokens: 400, cache_creation_1h_tokens: 600 }]);
  assert.equal(cost.exact, true);
  near(cost.usd, (10 * 4 + 20 * 20 + 300 * 0.2 + 400 * 5 + 600 * 8) / 1e6, 1e-12);
  assert.match(cost.perModel['claude-opus-5-5'].basis, /otel/);
  const partial = killedFromSpecs([{ cache_creation_tokens: 1000, cache_creation_5m_tokens: 400, cache_creation_1h_tokens: 600 }, { cache_creation_tokens: 50 }]);
  assert.equal(partial.exact, false, 'one request without a split makes the model an interval again');
});
