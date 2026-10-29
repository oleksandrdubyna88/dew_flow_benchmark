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
import { TWO_CHILDREN, asTranscripts, readEvents } from './synth.mjs';

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
  const O = syntheticO();
  const renamed = { ...O, perModel: { 'claude-nope-1': O.perModel['claude-opus-5-5'], 'claude-sonnet-5-5': O.perModel['claude-sonnet-5-5'] } };
  const cost = sessionCost({ E: null, O: renamed, T: null, table });
  assert.equal(cost.known, false);
  assert.equal(cost.perModel['claude-sonnet-5-5'].known, true);
  assert.ok(cost.flags.includes('no envelope'));
});
