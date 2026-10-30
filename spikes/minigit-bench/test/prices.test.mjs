import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { asTheCliBills, loadPrices, priceFor, priceOpenAiShaped, priceUsage } from '../lib/prices.mjs';
import { renderUsd } from '../lib/render.mjs';
import { spikeDir } from './helpers.mjs';

const readJson = (...parts) => JSON.parse(readFileSync(path.join(spikeDir, ...parts), 'utf8'));
const banteg = readJson('test', 'fixtures', 'banteg-83644e0-csharp.json');
const dated = readJson('inputs', 'prices-2026-10-29.json');
const near = (a, b, eps = 5e-7) => assert.ok(Math.abs(a - b) < eps, `${a} is not within ${eps} of ${b}`);
const table = () => {
  const loaded = loadPrices(dated);
  assert.equal(loaded.ok, true, loaded.reason);
  return loaded.value;
};
const sum = xs => xs.reduce((a, b) => a + b, 0);

test('The_upstream_formula_bills_cached_input_twice_and_that_is_the_point', () => {
  const { input, cached, output } = banteg.source.rates;
  const billed = banteg.rows.map(r => (r.input_tokens * input + r.cache_read_tokens * cached + r.output_tokens * output) / 1e6);
  banteg.rows.forEach((r, i) => near(billed[i], r.cost_usd, 1e-6));
  near(sum(billed) / 3, 2.7453537);
  assert.equal(renderUsd(sum(billed) / 3), '$2.75');
});

test('Cached_input_is_billed_once', () => {
  const rates = { input: banteg.source.rates.input, cached: banteg.source.rates.cached, output: banteg.source.rates.output };
  const priced = banteg.rows.map(r => priceOpenAiShaped({ input: r.input_tokens, cached: r.cache_read_tokens, output: r.output_tokens }, rates));
  assert.ok(priced.every(p => p.ok), 'every row must price: a refused row would shrink the sum');
  const corrected = sum(priced.map(p => p.value)) / 3;
  near(corrected, 0.6037003);
  assert.equal(renderUsd(corrected), '$0.60');
  const ratio = 2.7453537 / corrected;
  assert.ok(Math.abs(ratio - 4.55) < 0.005, `ratio ${ratio}`);
});

test('Cached_above_input_is_refused_as_a_value', () => {
  const refused = priceOpenAiShaped({ input: 10, cached: 20, output: 1 }, { input: 5, cached: 0.5, output: 30 });
  assert.equal(refused.ok, false);
  assert.match(refused.reason, /cached > input/);
});

test('The_fixture_names_its_source_and_the_totals_match_the_scripts', () => {
  assert.equal(banteg.source.sha, '83644e0');
  assert.match(banteg.source.resultsSha256, /^[0-9a-f]{64}$/);
  assert.equal(banteg.rows.length, 6);
  assert.equal(banteg.totals.v1_total_count, 11);
  assert.equal(banteg.totals.v2_total_count, 30);
});

test('The_cli_billed_view_reproduces_the_pilots_sonnet_line_while_the_published_view_stays_at_ten_cents', () => {
  const tokens = { input: 104, output: 31113, cacheRead: 1749236, cacheWrite5m: 176496, cacheWrite1h: 0 };
  const t = table();
  const published = priceUsage(priceFor(t, 'claude-sonnet-5-5').value, tokens);
  const cli = priceUsage(priceFor(asTheCliBills(t, '2.1.289'), 'claude-sonnet-5-5').value, tokens);
  near(published, 0.9275016);
  near(cli, 1.1024252);
  near(cli - published, 0.1749236);
  near(priceFor(t, 'claude-sonnet-5-5').value.cacheRead, 0.10, 1e-12);
});

test('The_fixtures_sonnet_child_was_billed_at_twenty_cents_too', () => {
  const tokens = { input: 4, output: 118, cacheRead: 5951, cacheWrite5m: 8384, cacheWrite1h: 0 };
  const t = table();
  near(priceUsage(priceFor(t, 'claude-sonnet-5-5').value, tokens), 0.0227431);
  near(priceUsage(priceFor(asTheCliBills(t, '2.1.289'), 'claude-sonnet-5-5').value, tokens), 0.0233382);
});

test('A_cli_rate_for_an_unpriced_model_is_refused', () => {
  const bad = { ...dated, cliRates: [{ modelId: 'claude-x', cacheRead: 0.2, observed: 'a probe', cliVersion: '2.1.289' }] };
  const refused = loadPrices(bad);
  assert.equal(refused.ok, false);
  assert.match(refused.reason, /claude-x/);
  assert.equal(loadPrices({ ...dated, cliRates: [{ modelId: 'claude-sonnet-5-5', cacheRead: 0.2, observed: '  ', cliVersion: '2.1.289' }] }).ok, false);
  assert.equal(loadPrices({ ...dated, cliRates: [{ modelId: 'claude-sonnet-5-5', cacheRead: -1, observed: 'x', cliVersion: '2.1.289' }] }).ok, false);
});

test('An_unpriced_model_reads_unknown_never_zero', () => {
  const missing = priceFor(table(), 'claude-fable-5');
  assert.equal(missing.known, false);
  assert.match(missing.why, /claude-fable-5-1/, 'the reason lists the priced ids');
  assert.match(missing.why, /claude-opus-5-5/);
  assert.equal(renderUsd(missing), 'unknown');
  assert.equal(renderUsd({ known: false, why: 'x' }), 'unknown');
  assert.equal(renderUsd(Number.NaN), 'unknown');
});

test('No_prefix_or_default_row_stands_in_for_an_id', () => {
  const t = table();
  assert.equal(priceFor(t, 'claude-opus-5').known, false);
  assert.equal(priceFor(t, 'claude-opus-5-5-20251001').known, false);
  assert.equal(priceFor(t, '').known, false);
  assert.equal(priceFor(t, 'claude-opus-5-5').known, true);
});

test('The_opus_4_8_row_is_the_refusal_fallbacks_price_and_reproduces_the_re_smokes_own_figure', () => {
  // the CLI falls back to claude-opus-4-8 when a model refuses (stream subtype model_refusal_fallback). The re-smoke 20261030T002834Z reported
  // modelUsage claude-opus-4-8 costUSD 0.116565 for 2 input, 1009 output and 9133 one-hour cache-write tokens: 2 x 5 + 1009 x 25 + 9133 x 10 per MTok.
  const row = priceFor(table(), 'claude-opus-4-8');
  assert.equal(row.known, true);
  assert.deepEqual([row.value.input, row.value.output, row.value.cacheRead, row.value.cacheWrite5m, row.value.cacheWrite1h], [5, 25, 0.5, 6.25, 10]);
  near(priceUsage(row.value, { input: 2, output: 1009, cacheRead: 0, cacheWrite5m: 0, cacheWrite1h: 9133 }), 0.116565, 1e-9);
  assert.match(dated.notes.join(' '), /claude-opus-4-8.*2026-10-30/);
});

test('The_dated_table_loads_and_carries_the_fable_row', () => {
  const t = table();
  assert.equal(t.models.size, 5, 'opus 5.5, sonnet 5.5, haiku 4.5, fable 5.1 and the opus 4.8 refusal fallback');
  assert.equal(t.readOn, '2026-10-29');
  const fable = priceFor(t, 'claude-fable-5-1').value;
  assert.deepEqual(
    [fable.input, fable.output, fable.cacheRead, fable.cacheWrite5m, fable.cacheWrite1h],
    [10, 50, 0.25, 12.5, 20],
  );
  assert.equal(t.cliRates[0].modelId, 'claude-sonnet-5-5');
  assert.equal(t.cliRates[0].cacheRead, 0.20);
  assert.equal(t.cliRates[0].cliVersion, '2.1.289');
});

test('A_cli_rate_applies_only_to_the_cli_version_it_was_observed_on', () => {
  const t = table();
  const read = v => priceFor(asTheCliBills(t, v), 'claude-sonnet-5-5').value.cacheRead;
  assert.equal(read('2.1.289'), 0.20, 'the pilot observation still reproduces for 2.1.289');
  assert.equal(read('2.1.310'), 0.10, 'the capture observed published billing on 2.1.310');
  assert.equal(read('9.9.9'), 0.10, 'a version with no observation is billed as published, never at an older version\'s rate');
  assert.equal(read(undefined), 0.10);
  assert.equal(read(null), 0.10);
  const observed310 = t.cliRates.find(r => r.cliVersion === '2.1.310');
  assert.match(observed310.observed, /E1\.S3 capture/);
});

test('A_cli_rate_without_a_version_or_repeated_for_one_model_and_version_is_refused', () => {
  const rate = { modelId: 'claude-sonnet-5-5', cacheRead: 0.2, observed: 'a probe', cliVersion: '2.1.289' };
  const noVersion = loadPrices({ ...dated, cliRates: [{ ...rate, cliVersion: undefined }] });
  assert.equal(noVersion.ok, false);
  assert.match(noVersion.reason, /cliVersion/);
  const twice = loadPrices({ ...dated, cliRates: [rate, { ...rate, cacheRead: 0.3 }] });
  assert.equal(twice.ok, false);
  assert.match(twice.reason, /claude-sonnet-5-5.*2\.1\.289/);
  assert.equal(loadPrices({ ...dated, cliRates: [rate, { ...rate, cliVersion: '2.1.310' }] }).ok, true);
});

test('A_table_is_refused_for_a_non_https_source_no_models_duplicates_or_negative_rates', () => {
  assert.equal(loadPrices({ ...dated, source: 'http://x' }).ok, false);
  assert.equal(loadPrices({ ...dated, models: [] }).ok, false);
  assert.equal(loadPrices({ ...dated, models: [dated.models[0], dated.models[0]] }).ok, false);
  assert.equal(loadPrices({ ...dated, models: [{ ...dated.models[0], output: -1 }] }).ok, false);
  assert.equal(loadPrices({ ...dated, models: [{ ...dated.models[0], output: 'x' }] }).ok, false);
  assert.equal(loadPrices(null).ok, false);
});

test('An_unsplit_cache_write_prices_as_an_interval_and_never_as_one_number', () => {
  const opus = priceFor(table(), 'claude-opus-5-5').value;
  const interval = priceUsage(opus, { input: 0, output: 0, cacheRead: 0, cacheWrite: 1000, split: { known: false, why: 'x' } });
  near(interval.high - interval.low, 1000 * (8 - 5) / 1e6, 1e-12);
  near(interval.low, 1000 * 5 / 1e6, 1e-12);
});

test('The_table_is_not_changed_by_asTheCliBills', () => {
  const t = table();
  asTheCliBills(t);
  assert.equal(priceFor(t, 'claude-sonnet-5-5').value.cacheRead, 0.10);
  assert.ok(Object.isFrozen(t));
});
