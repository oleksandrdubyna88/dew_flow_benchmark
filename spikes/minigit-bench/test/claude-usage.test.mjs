import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { envelopeView, headlineUsd, share1h, streamMeterUsd, streamView, transcriptView } from '../lib/claude-usage.mjs';
import { loadPrices } from '../lib/prices.mjs';
import { renderUsd } from '../lib/render.mjs';
import { spikeDir } from './helpers.mjs';
import { TWO_CHILDREN, asTranscripts, readEvents, readFixtureEvents } from './synth.mjs';

const dated = JSON.parse(readFileSync(path.join(spikeDir, 'inputs', 'prices-2026-10-29.json'), 'utf8'));
const table = loadPrices(dated).value;
const near = (a, b, eps = 5e-7) => assert.ok(Math.abs(a - b) < eps, `${a} is not within ${eps} of ${b}`);
const real = readEvents(TWO_CHILDREN);

const usageOf = (m, over = {}) => ({ inputTokens: 0, outputTokens: 0, cacheReadInputTokens: 0, cacheCreationInputTokens: 0, costUSD: 0, ...over, ...m });
const resultOf = modelUsage => ({ type: 'result', subtype: 'success', total_cost_usd: 1, duration_ms: 1, duration_api_ms: 1, num_turns: 1, modelUsage });
const assistantOf = (model, cw5, cw1) => ({
  type: 'assistant',
  request_id: 'req_x',
  message: { id: `msg_${model}`, model, usage: { input_tokens: 1, output_tokens: 1, cache_read_input_tokens: 0, cache_creation_input_tokens: cw5 + cw1, cache_creation: { ephemeral_5m_input_tokens: cw5, ephemeral_1h_input_tokens: cw1 } } },
});

test('A_stream_repeating_one_message_three_times_is_one_message_with_placeholder_output', () => {
  const view = streamView(readFixtureEvents('stream-synthetic-triple.ndjson'));
  assert.equal(view.messages.length, 1);
  assert.equal(view.messages[0].events, 3);
  assert.equal(view.messages[0].outputPlaceholder, true);
  assert.equal(view.messages[0].output, 16);
  assert.equal(view.messages[0].requestId, 'req_synthetic_1');
  assert.deepEqual(view.disagreements, []);
});

test('Duplicates_that_disagree_on_input_or_cache_are_a_finding_not_a_pick', () => {
  const events = readFixtureEvents('stream-synthetic-triple.ndjson');
  const second = events.filter(e => e.type === 'assistant')[1];
  second.message.usage.cache_read_input_tokens = 100;
  const view = streamView(events);
  assert.equal(view.messages.length, 1);
  assert.deepEqual(view.disagreements, [{ id: 'msg_synthetic_1', field: 'cacheRead', values: [0, 100] }]);
});

test('The_headline_refuses_a_stream_without_an_envelope', () => {
  const events = readFixtureEvents('stream-synthetic-triple.ndjson');
  assert.equal(events.some(e => e.type === 'result'), false);
  assert.deepEqual(headlineUsd(envelopeView(events), null, table), { known: false, why: 'no envelope' });
  const meter = streamMeterUsd(streamView(events), table, { outputFloor: 1000 });
  assert.equal(meter.estimate, true);
  assert.match(meter.basis, /floored/);
});

test('The_stream_meter_prices_at_published_rates_with_the_output_floored_per_message', () => {
  const view = streamView(readFixtureEvents('stream-synthetic-triple.ndjson'));
  const floored = streamMeterUsd(view, table, { outputFloor: 1000 });
  // input 2 * 4 + output max(16, 1000) * 20 + 5943 cache writes (5m) * 5, per million
  near(floored.usd, (2 * 4 + 1000 * 20 + 5943 * 5) / 1e6, 1e-12);
  const unfloored = streamMeterUsd(view, table, { outputFloor: 0 });
  near(unfloored.usd, (2 * 4 + 16 * 20 + 5943 * 5) / 1e6, 1e-12);
  assert.equal(streamMeterUsd(view, table, {}).known, false, 'a meter without a floor is refused, not defaulted');
});

test('The_meter_does_not_price_a_model_the_table_lacks_and_says_so', () => {
  const events = readFixtureEvents('stream-synthetic-triple.ndjson');
  for (const e of events.filter(x => x.type === 'assistant')) e.message.model = 'claude-nope-1';
  const meter = streamMeterUsd(streamView(events), table, { outputFloor: 1000 });
  assert.equal(meter.usd, 0, 'nothing was priced');
  assert.deepEqual(meter.unpriced, ['msg_synthetic_1']);
  assert.equal(meter.lowerBound, true);
});

test('A_message_without_an_id_stands_alone', () => {
  const a = { type: 'assistant', message: { model: 'claude-opus-5-5', usage: { input_tokens: 1, output_tokens: 1, cache_read_input_tokens: 0, cache_creation_input_tokens: 0 } } };
  const view = streamView([a, structuredClone(a)]);
  assert.equal(view.messages.length, 2);
  assert.equal(view.messages[0].id, null);
});

test('The_real_stream_is_one_message_per_id_with_the_children_marked_as_subagents', () => {
  const view = streamView(real);
  const ids = new Set(real.filter(e => e.type === 'assistant').map(e => e.message.id));
  assert.equal(view.messages.length, ids.size);
  assert.deepEqual(view.disagreements, [], 'the real duplicates agree on input and cache');
  assert.ok(view.messages.some(m => m.source === 'subagent'));
  assert.ok(view.messages.every(m => m.requestId?.startsWith('req_')));
});

test('Totals_come_only_from_modelUsage_and_a_child_only_model_is_not_lost', () => {
  const e = envelopeView(real);
  assert.equal(e.known, true);
  assert.deepEqual(Object.keys(e.perModel).sort(), ['claude-haiku-4-5-20251001', 'claude-opus-5-5', 'claude-sonnet-5-5']);
  const sonnet = e.perModel['claude-sonnet-5-5'];
  assert.deepEqual([sonnet.output, sonnet.cacheRead, sonnet.cacheWrite], [118, 5951, 8384]);
  assert.equal(e.main.output, 356);
  assert.ok(Object.values(e.perModel).every(m => m.output !== 356), 'the main-only usage block is in no per-model total');
  assert.equal(e.main.cacheWrite5m, 0);
  assert.equal(e.main.cacheWrite1h, 45937);
  assert.equal(e.totalCostUsd, 0.594582);
  assert.equal(e.durationMs, 24556);
});

test('An_envelope_that_cannot_be_read_is_unknown_with_its_reason_never_a_zero_total', () => {
  assert.deepEqual(envelopeView(readFixtureEvents('stream-synthetic-triple.ndjson')), { known: false, why: 'no result envelope' });
  assert.equal(envelopeView([resultOf({})]).known, false, 'an empty modelUsage is not a zero-dollar session');
  const noCost = { ...resultOf({ m: usageOf({}) }), total_cost_usd: null };
  assert.match(envelopeView([noCost]).why, /total_cost_usd/);
  const bad = resultOf({ 'claude-opus-5-5': usageOf({ inputTokens: 'x' }) });
  assert.match(envelopeView([bad]).why, /claude-opus-5-5.*inputTokens/);
});

test('The_real_opus_line_is_reproduced_only_with_the_true_1h_share', () => {
  const e = envelopeView(real);
  const t = transcriptView(asTranscripts(real));
  const s = share1h(t, 'claude-opus-5-5');
  assert.equal(s.known, true);
  near(s.value, 45937 / 74214, 1e-12);
  const h = headlineUsd(e, t, table);
  near(h.perModel['claude-opus-5-5'].usd, 0.5697508);
  assert.equal(h.perModel['claude-opus-5-5'].exact, true);

  const unknownSplit = headlineUsd(e, transcriptView([]), table).perModel['claude-opus-5-5'];
  near(unknownSplit.low, 0.4319398);
  near(unknownSplit.high, 0.6545818);
  near(unknownSplit.high - unknownSplit.low, (74214 * (8 - 5)) / 1e6, 1e-9);
});

test('The_headline_on_the_real_envelope_is_exact_at_published_prices_and_states_its_basis', () => {
  const h = headlineUsd(envelopeView(real), transcriptView(asTranscripts(real)), table);
  assert.equal(h.known, true);
  assert.equal(h.exact, true);
  near(h.usd, 0.5697508 + 0.0227431 + 0.001493);
  assert.match(h.basis, /published list prices \(2026-10-29\); tokens from the result envelope; 5m\/1h split from transcripts/);
  assert.equal(h.perModel['claude-haiku-4-5-20251001'].exact, true, 'no cache writes: nothing to split');
});

test('An_unsplit_model_is_an_interval_never_a_guess', () => {
  const e = envelopeView([resultOf({ 'claude-opus-5-5': usageOf({ cacheCreationInputTokens: 1000 }) })]);
  const t = transcriptView([{ relPath: 's.jsonl', text: JSON.stringify(assistantOf('claude-sonnet-5-5', 5, 0)) }]);
  const h = headlineUsd(e, t, table);
  const m = h.perModel['claude-opus-5-5'];
  near(m.high - m.low, (1000 * (8 - 5)) / 1e6, 1e-12);
  assert.equal(h.exact, false);
  assert.ok(h.low <= h.usd && h.usd <= h.high);
  assert.equal(m.share1h.known, false);
});

test('Zero_1h_tokens_on_split_messages_is_a_measurement', () => {
  const e = envelopeView([resultOf({ 'claude-opus-5-5': usageOf({ cacheCreationInputTokens: 1000 }) })]);
  const t = transcriptView([{ relPath: 's.jsonl', text: JSON.stringify(assistantOf('claude-opus-5-5', 1000, 0)) }]);
  assert.deepEqual(share1h(t, 'claude-opus-5-5'), { known: true, value: 0 });
  const m = headlineUsd(e, t, table).perModel['claude-opus-5-5'];
  assert.equal(m.exact, true);
  near(m.usd, (1000 * 5) / 1e6, 1e-12);
  assert.equal(share1h(t, 'claude-sonnet-5-5').known, false);
  assert.match(share1h(t, 'claude-sonnet-5-5').why, /no split message for claude-sonnet-5-5/);
});

test('An_unpriced_model_in_the_envelope_makes_the_headline_unknown_and_the_others_still_priced', () => {
  const e = envelopeView([resultOf({
    'claude-fable-5': usageOf({ inputTokens: 100 }),
    'claude-opus-5-5': usageOf({ inputTokens: 1000 }),
  })]);
  const h = headlineUsd(e, null, table);
  assert.equal(h.known, false);
  assert.match(h.why, /claude-fable-5/);
  assert.equal(h.perModel['claude-fable-5'].known, false);
  near(h.perModel['claude-opus-5-5'].usd, 4000 / 1e6, 1e-12);
  assert.equal(renderUsd(h), 'unknown');
  assert.equal(renderUsd(h.perModel['claude-fable-5']), 'unknown');
});

test('The_transcript_view_reads_main_and_subagent_files_and_reports_its_coverage', () => {
  const t = transcriptView(asTranscripts(real));
  assert.equal(t.files, 3);
  assert.deepEqual(t.coverage, { main: true, subagent: true });
  assert.deepEqual(t.disagreements, []);
  const child = t.messages.find(m => m.source === 'subagent');
  assert.match(child.agentId, /^[A-Za-z0-9_]+$/);
  assert.equal(t.messages.filter(m => m.source === 'main').length, 2);
  assert.ok(t.messages.every(m => m.splitKnown === true), 'the real children carry the split');
});

test('A_transcript_set_without_subagent_files_covers_only_main', () => {
  const main = asTranscripts(real).filter(f => !f.relPath.includes('/subagents/'));
  assert.deepEqual(transcriptView(main).coverage, { main: true, subagent: false });
  const emptySub = [...main, { relPath: 'sess-1/subagents/agent-x.jsonl', text: '{"type":"user"}\n' }];
  assert.equal(transcriptView(emptySub).coverage.subagent, false, 'a subagent file without an assistant line is not coverage');
});

test('A_line_that_is_not_json_is_counted_not_dropped_silently', () => {
  const t = transcriptView([{ relPath: 's.jsonl', text: `not json\n${JSON.stringify(assistantOf('claude-opus-5-5', 1, 0))}\n` }]);
  assert.equal(t.skippedLines, 1);
  assert.equal(t.messages.length, 1);
  // the count has an effect: the headline built from it says so, and T3/T4 refuse to vouch for it (see crosscheck.test.mjs)
  assert.deepEqual(headlineUsd(opusE(1), t, table).flags, ['1 unreadable transcript line(s)']);
});

test('A_message_without_the_5m_1h_split_is_marked_unsplit_not_zero', () => {
  const a = assistantOf('claude-opus-5-5', 7, 0);
  delete a.message.usage.cache_creation;
  const t = transcriptView([{ relPath: 's.jsonl', text: JSON.stringify(a) }]);
  const m = t.messages[0];
  assert.equal(m.splitKnown, false);
  assert.equal(m.cacheWrite, 7);
  assert.equal(m.cacheWrite5m, null);
  assert.equal(m.cacheWrite1h, null);
  assert.equal(share1h(t, 'claude-opus-5-5').known, false);
});

const opusE = cacheWrite => envelopeView([resultOf({ 'claude-opus-5-5': usageOf({ cacheCreationInputTokens: cacheWrite }) })]);
const lineOf = (E, T) => headlineUsd(E, T, table).perModel['claude-opus-5-5'];
const jsonl = (...msgs) => [{ relPath: 's.jsonl', text: msgs.map(m => JSON.stringify(m)).join('\n') }];

test('A_cache_write_the_transcripts_do_not_account_for_is_an_interval_not_an_extrapolation', () => {
  // The envelope wrote 1500 tokens; the transcripts that survived hold 1000 of them (all 1h). The 500 a lost child file held could be either.
  const m = lineOf(opusE(1500), transcriptView(jsonl(assistantOf('claude-opus-5-5', 0, 1000))));
  assert.equal(m.exact, false);
  near(m.low, (1000 * 8 + 500 * 5) / 1e6, 1e-12);
  near(m.high, (1000 * 8 + 500 * 8) / 1e6, 1e-12);
  assert.ok(m.basis.includes('1000 of 1500'), m.basis);
});

test('A_mixed_split_and_unsplit_model_is_an_interval_over_what_the_split_does_not_cover', () => {
  const unsplit = assistantOf('claude-opus-5-5', 10000, 0);
  delete unsplit.message.usage.cache_creation;
  unsplit.message.id = 'msg_unsplit';
  const m = lineOf(opusE(10100), transcriptView(jsonl(assistantOf('claude-opus-5-5', 100, 0), unsplit)));
  assert.equal(m.exact, false);
  near(m.low, (100 * 5 + 10000 * 5) / 1e6, 1e-12);
  near(m.high, (100 * 5 + 10000 * 8) / 1e6, 1e-12);
});

test('Transcripts_that_hold_more_cache_writes_than_the_envelope_do_not_narrow_anything', () => {
  const m = lineOf(opusE(500), transcriptView(jsonl(assistantOf('claude-opus-5-5', 0, 900))));
  assert.equal(m.exact, false);
  near(m.low, (500 * 5) / 1e6, 1e-12);
  near(m.high, (500 * 8) / 1e6, 1e-12);
});

test('Transcripts_that_account_for_every_cache_write_are_exact', () => {
  const m = lineOf(opusE(1500), transcriptView(jsonl(assistantOf('claude-opus-5-5', 500, 0), { ...assistantOf('claude-opus-5-5', 0, 1000), message: { ...assistantOf('claude-opus-5-5', 0, 1000).message, id: 'msg_b' } })));
  assert.equal(m.exact, true);
  near(m.usd, (500 * 5 + 1000 * 8) / 1e6, 1e-12);
});

test('Disagreeing_duplicates_and_unreadable_lines_are_flagged_on_the_headline', () => {
  const dup = assistantOf('claude-opus-5-5', 1000, 0);
  const second = structuredClone(dup);
  second.message.usage.cache_read_input_tokens = 5;
  const clean = headlineUsd(opusE(1000), transcriptView(jsonl(dup)), table);
  assert.deepEqual(clean.flags, []);
  const t = transcriptView([{ relPath: 's.jsonl', text: `${JSON.stringify(dup)}\n${JSON.stringify(second)}\nnot json\n` }]);
  const flagged = headlineUsd(opusE(1000), t, table);
  assert.ok(flagged.flags.some(f => /1 transcript message id\(s\) whose duplicates disagree/.test(f)), JSON.stringify(flagged.flags));
  assert.ok(flagged.flags.some(f => /1 unreadable transcript line/.test(f)));
});
