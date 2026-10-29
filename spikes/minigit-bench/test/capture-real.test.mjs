// The scenarios that need the step 0 capture. Each SKIPS with the reason 'awaits the E1.S3 step 0 capture' until
// test/fixtures/real/claude-2.1.310-capture/ exists, and none of them is satisfied by anything invented: they read that directory only.
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { claudeArgv } from '../capture/capture-plan.mjs';
import { loadCapture } from '../capture/capture-files.mjs';
import { crosscheckPolicyFrom } from '../capture/policy-input.mjs';
import { computeVerdicts, decisionsOf, renderVerdicts, verdictBlockOf } from '../capture/verdicts.mjs';
import { viewsOf } from '../capture/verdict-rules.mjs';
import { aliasRecord } from '../lib/aliases.mjs';
import { envelopeView, headlineUsd, transcriptView } from '../lib/claude-usage.mjs';
import { crosscheck } from '../lib/crosscheck.mjs';
import { sessionCost } from '../lib/session-cost.mjs';
import { OTEL_STAMP_MARKS, apiRequests, decodeOtlpLogs, otelView, toolResults } from '../lib/otel-decode.mjs';
import { HOOK_TIME_SOURCE, crossCheckTools, partition, spansFrom } from '../lib/timeline.mjs';
import { startReceiver } from '../lib/otel-receiver.mjs';
import { asTheCliBills, priceFor } from '../lib/prices.mjs';
import { spikeDir, tmpDir } from './helpers.mjs';
import { CAPTURE_DIR, captureGate, parseLines } from './synth.mjs';

const loaded = () => {
  const capture = loadCapture(CAPTURE_DIR);
  assert.equal(capture.ok, true, capture.reason);
  const inputs = crosscheckPolicyFrom(spikeDir);
  assert.equal(inputs.ok, true, inputs.reason);
  return { capture: capture.value, ...inputs.value, views: viewsOf(capture.value, inputs.value) };
};
const captureMd = () => readFileSync(path.join(CAPTURE_DIR, 'CAPTURE.md'), 'utf8');

test('The_real_capture_decodes_and_names_every_attribute_the_plan_reads', t => {
  if (!captureGate(t)) return;
  const { capture, views } = loaded();
  assert.ok(capture.sink.length > 0);
  assert.ok(capture.sink.every(l => /^application\/json/i.test(l.contentType)), 'every sink line is JSON');
  assert.ok(views.decoded.every(x => x.d.ok), 'every logs payload decodes');
  assert.ok(views.O.requests.length >= 3, `api_request records: ${views.O.requests.length} (main turns plus two children)`);
  assert.deepEqual(views.O.requests.flatMap(r => r.missing), [], 'no named attribute is missing; per the capture\'s amendment this list is the one it recorded');
});

test('The_stamp_verdict_is_a_named_constant_and_matches_the_capture', t => {
  if (!captureGate(t)) return;
  const { capture, table, policy } = loaded();
  assert.equal(decisionsOf(computeVerdicts(capture, { table, policy })).stampMarks, OTEL_STAMP_MARKS);
});

test('The_hook_time_source_is_a_named_constant_and_matches_the_capture', t => {
  if (!captureGate(t)) return;
  const { capture, table, policy } = loaded();
  assert.equal(decisionsOf(computeVerdicts(capture, { table, policy })).hookTimeSource, HOOK_TIME_SOURCE);
});

test('The_real_capture_replays_through_the_span_pipeline_with_the_decoders_own_field_names', t => {
  if (!captureGate(t)) return;
  const { capture } = loaded();
  const records = capture.sink.filter(l => l.path === '/v1/logs').flatMap(l => {
    const decoded = decodeOtlpLogs(l.body);
    assert.equal(decoded.ok, true, decoded.reason);
    return decoded.value;
  });
  const otel = otelView(apiRequests(records), { toolResults: toolResults(records) });
  // the hook lines the capture's stamp hook wrote ({ t, hook_event_name, ... }) in the audit shape the span source reads ({ ts, event, ... })
  const audit = capture.hooks.map(h => ({ ...h, ts: h.t, event: h.hook_event_name }));
  const built = spansFrom({ otel, audit });
  assert.deepEqual(built.unpaired, []);
  const modelApi = built.spans.filter(s => s.category === 'model_api');
  assert.equal(modelApi.length, 7, 'one model_api span per api_request record');
  assert.equal(modelApi.length, otel.requests.length);
  const tools = built.spans.filter(s => s.source === 'audit' && s.toolUseId);
  const crossed = crossCheckTools(tools, otel.toolResults);
  assert.equal(crossed.known, true, crossed.why);
  assert.equal(crossed.compared, 5, '5 tools are on both sides');
  assert.deepEqual(crossed.mismatches, []);
  const wall = { startMs: Math.min(...built.spans.map(s => s.startMs)), endMs: Math.max(...built.spans.map(s => s.endMs)) };
  const p = partition(wall, built.spans);
  assert.equal(p.known, true, p.why);
  assert.equal(Object.values(p.categories).reduce((a, b) => a + b, 0), p.wallMs, 'the categories sum to the wall to the millisecond');
  assert.equal(p.wallMs, wall.endMs - wall.startMs);
});

test('The_policy_file_carries_what_the_capture_decided', t => {
  if (!captureGate(t)) return;
  const { capture, table, policy } = loaded();
  const d = decisionsOf(computeVerdicts(capture, { table, policy }));
  assert.equal(policy.join, d.join);
  assert.equal(policy.t3OutputSource, d.t3OutputSource, 'an undecided A-T2 (null) must be settled by hand before the series');
});

test('The_receiver_replays_the_capture_and_sums_cost_live', async t => {
  if (!captureGate(t)) return;
  const { capture, views } = loaded();
  const dir = tmpDir('replay');
  const rx = await startReceiver({ dir });
  t.after(() => rx.close());
  for (const line of capture.sink.filter(l => l.path === '/v1/logs')) {
    const res = await fetch(`http://127.0.0.1:${rx.port}${line.path}`, { method: 'POST', headers: { 'content-type': line.contentType }, body: line.body });
    assert.equal(res.status, 200);
  }
  assert.equal(rx.server.address().address, '127.0.0.1');
  const expected = views.O.requests.reduce((a, r) => a + (r.costUsd ?? 0), 0);
  const live = rx.liveSum();
  assert.ok(Math.abs(live.usd - expected) < 1e-9);
  assert.equal(live.requests, views.O.requests.length);
  assert.equal(parseLines(readFileSync(path.join(dir, 'otel.ndjson'), 'utf8')).length, views.records.length);
});

test('T4_holds_on_the_real_capture', t => {
  if (!captureGate(t)) return;
  const { table, policy, views } = loaded();
  const c = crosscheck({ E: views.E, O: views.O, T: views.T, table, policy, cliVersion: views.cliVersion });
  assert.equal(c.t4.status, 'pass', c.t4.detail);
  assert.equal(c.t4.gap, 0, 'token counts agree exactly');
  assert.equal(c.t3.status, 'pass', `${c.t3.detail} ${c.t3.why ?? ''}`);
  assert.equal(c.t1.status, 'pass');
});

test('The_real_capture_resolves_every_alias_it_used_to_an_exact_id', t => {
  if (!captureGate(t)) return;
  const { capture, table } = loaded();
  const rec = aliasRecord({ events: capture.events });
  assert.equal(rec.ok, true, rec.reason);
  const init = capture.events.find(e => e.type === 'system' && e.subtype === 'init');
  assert.equal(rec.value.aliases.opus, init.model);
  assert.ok(rec.value.aliases.sonnet, 'the sonnet child resolved an id');
  if (!rec.value.aliases.fable) assert.match(captureMd(), /A-M1[^\n]*fable not exercised/, 'a missing fable alias must be stated as not exercised in CAPTURE.md');
  for (const id of Object.values(rec.value.aliases)) assert.equal(priceFor(table, id).known, true, `${id} is in the price table`);
});

test('Every_assumption_has_a_verdict_and_none_is_blank_and_CAPTURE_md_is_the_computed_table', t => {
  if (!captureGate(t)) return;
  const { capture, table, policy } = loaded();
  const rows = computeVerdicts(capture, { table, policy });
  assert.equal(rows.length, 15);
  assert.ok(rows.every(r => ['holds', 'refuted', 'not exercised'].includes(r.verdict) && r.observed.length > 0));
  assert.equal(verdictBlockOf(captureMd()), renderVerdicts(rows), 'CAPTURE.md equals renderVerdicts byte for byte');
});

test('The_capture_ran_the_argv_the_plan_prescribes_and_stayed_inside_its_budget', t => {
  if (!captureGate(t)) return;
  const { capture } = loaded();
  assert.deepEqual(capture.argv.argv, claudeArgv('/cap'));
  assert.ok(capture.meta.costUsd !== null && capture.meta.costUsd <= 1.5, `cost ${capture.meta.costUsd}`);
  assert.match(capture.meta.cliVersion, /^2\.1\.310$/);
});

test('A_O5_is_evaluated_on_the_real_capture_and_never_skipped', t => {
  if (!captureGate(t)) return;
  const { capture, table, policy } = loaded();
  const row = computeVerdicts(capture, { table, policy }).find(r => r.id === 'A-O5');
  assert.notEqual(row.verdict, 'not exercised', row.observed);
  assert.match(row.observed, /subagent record/);
});

test('For_2_1_310_the_cli_billed_view_equals_published_on_the_real_capture', t => {
  if (!captureGate(t)) return;
  const { table, views, capture } = loaded();
  assert.equal(views.cliVersion, '2.1.310');
  assert.equal(views.cliVersion, capture.meta.cliVersion);
  const sonnet = (tb, v) => priceFor(asTheCliBills(tb, v), 'claude-sonnet-5-5').value;
  assert.deepEqual(sonnet(table, '2.1.310'), priceFor(table, 'claude-sonnet-5-5').value);
  assert.notDeepEqual(sonnet(table, '2.1.289'), priceFor(table, 'claude-sonnet-5-5').value);
  const row = computeVerdicts(capture, { table, policy: loaded().policy }).find(r => r.id === 'A-P1');
  assert.equal(row.verdict, 'holds', row.observed);
  assert.ok(views.E.perModel['claude-sonnet-5-5'].cacheRead > 0, 'the sonnet line reads from cache, so its rate matters in this capture');
});

const CORNERSTONE_USD = 0.1809576;
const near = (a, b, eps = 5e-7) => assert.ok(Math.abs(a - b) < eps, `${a} is not within ${eps} of ${b}`);

test('The_cornerstone_headline_on_the_real_capture_is_exact_and_every_cross_check_passes', t => {
  if (!captureGate(t)) return;
  const { table, policy, views } = loaded();
  const headline = headlineUsd(views.E, views.T, table);
  assert.equal(headline.known, true);
  assert.equal(headline.exact, true);
  near(headline.usd, CORNERSTONE_USD);
  const cost = sessionCost({ E: views.E, O: views.O, T: views.T, table });
  assert.equal(cost.exact, true);
  near(cost.usd, CORNERSTONE_USD);
  near(cost.cliTotalUsd, CORNERSTONE_USD);
  const c = crosscheck({ E: views.E, O: views.O, T: views.T, table, policy, cliVersion: views.cliVersion });
  assert.deepEqual(['t1', 't2', 't3', 't4'].map(k => c[k].status), ['pass', 'pass', 'pass', 'pass']);
});

test('A_killed_session_on_the_real_otel_is_priced_exactly_from_its_own_cache_write_split', t => {
  if (!captureGate(t)) return;
  const { table, views } = loaded();
  const cost = sessionCost({ E: { known: false, why: 'killed' }, O: views.O, T: null, table });
  assert.equal(cost.known, true);
  assert.equal(cost.exact, true, 'api_request carries cache_creation_5m/1h_tokens, so no interval is needed');
  near(cost.usd, CORNERSTONE_USD);
});

test('A_real_capture_that_lost_its_subagent_transcripts_is_not_cross_checked_and_not_exact', t => {
  if (!captureGate(t)) return;
  const { capture, table, policy, views } = loaded();
  const lost = transcriptView(capture.transcripts.filter(f => !f.relPath.includes('/subagents/')));
  assert.equal(lost.coverage.subagent, false);
  for (const t3 of ['investigate', 'stop']) {
    const c = crosscheck({ E: views.E, O: views.O, T: lost, table, policy: { ...policy, t3 }, cliVersion: views.cliVersion });
    for (const id of ['t3', 't4']) {
      assert.equal(c[id].status, 'unchecked', `${id} with t3=${t3}`);
      assert.match(c[id].why, /T incomplete: 4 main\/subagent api_request records have no transcript message/);
    }
  }
  const headline = headlineUsd(views.E, lost, table);
  assert.equal(headline.perModel['claude-fable-5-1'].exact, false, 'a model whose every transcript is lost is an interval');
  assert.equal(headline.perModel['claude-sonnet-5-5'].exact, false);
  assert.equal(headline.exact, false);
  assert.ok(headline.low <= CORNERSTONE_USD && CORNERSTONE_USD <= headline.high, 'the interval still contains the true cost');
});

test('A_child_rewritten_onto_the_main_model_whose_file_is_lost_is_an_interval_that_holds_the_truth', t => {
  if (!captureGate(t)) return;
  const { capture, table } = loaded();
  const swap = s => s.replaceAll('claude-fable-5-1', 'claude-opus-5-5');
  const events = capture.events.map(e => JSON.parse(swap(JSON.stringify(e))));
  const mu = capture.events.findLast(e => e.type === 'result').modelUsage;
  const a = mu['claude-opus-5-5'];
  const b = mu['claude-fable-5-1'];
  const merged = Object.fromEntries(Object.keys(a).map(k => [k, typeof a[k] === 'number' && typeof b[k] === 'number' && !['contextWindow', 'maxOutputTokens'].includes(k) ? a[k] + b[k] : a[k]]));
  events.findLast(e => e.type === 'result').modelUsage = { ...events.findLast(e => e.type === 'result').modelUsage, 'claude-opus-5-5': merged };
  const E = envelopeView(events);
  const all = capture.transcripts.map(f => ({ ...f, text: swap(f.text) }));
  const whole = headlineUsd(E, transcriptView(all), table).perModel['claude-opus-5-5'];
  const lost = headlineUsd(E, transcriptView(all.filter(f => !f.relPath.includes('agent-a92c'))), table).perModel['claude-opus-5-5'];
  assert.equal(whole.exact, true);
  assert.equal(lost.exact, false, 'the lost child file must not let its tokens inherit the main session\'s share');
  assert.ok(lost.low <= whole.usd + 1e-9 && whole.usd <= lost.high + 1e-9);
});
