import assert from 'node:assert/strict';
import { existsSync, readFileSync } from 'node:fs';
import net from 'node:net';
import path from 'node:path';
import { test } from 'node:test';
import {
  API_REQUEST_ATTRS, OTEL_STAMP_MARKS, REQUIRED_FOR_SUBAGENT, apiRequests, decodeOtlpLogs, otelView, roleOf, toolResults,
} from '../lib/otel-decode.mjs';
import { liveSumOf, otelEnv, startReceiver } from '../lib/otel-receiver.mjs';
import { spikeDir, tmpDir } from './helpers.mjs';
import { apiAttrs, otlpPayload, parseLines } from './synth.mjs';

const synthetic = readFileSync(path.join(spikeDir, 'test', 'fixtures', 'otel-synthetic-api-requests.json'), 'utf8');
const decoded = text => {
  const r = decodeOtlpLogs(text);
  assert.equal(r.ok, true, r.reason);
  return r.value;
};
const T0 = 1760000000000;
const payloadText = recs => JSON.stringify(otlpPayload(recs));
const post = (base, p, body, contentType = 'application/json') => fetch(`${base}${p}`, { method: 'POST', headers: { 'content-type': contentType }, body });

test('The_synthetic_fixture_is_labelled_and_decodes_to_named_records', () => {
  assert.match(JSON.parse(synthetic)._label, /^SYNTHETIC/);
  const records = decoded(synthetic);
  assert.deepEqual(records.map(r => r.name), ['api_request', 'api_request', 'api_request', 'tool_result']);
  assert.equal(records[0].tsMs, 1760000002000);
  assert.equal(records[0].scope, 'com.anthropic.claude_code.events');
  assert.equal(records[0].attrs.input_tokens, 2);
  assert.equal(records[0].attrs.cost_usd, 0.0204);
  assert.equal(records[3].attrs.success, true);
});

test('Int_values_arrive_as_strings_and_decode_to_safe_integers', () => {
  const ok = decoded(payloadText([{ name: 'api_request', tsMs: T0, attrs: apiAttrs({ cache_read_tokens: 1749236 }) }]));
  assert.equal(ok[0].attrs.cache_read_tokens, 1749236);
  assert.ok(payloadText([{ name: 'api_request', tsMs: T0, attrs: apiAttrs({ cache_read_tokens: 1749236 }) }]).includes('"intValue":"1749236"'), 'the wire form is a string');

  const raw = JSON.parse(payloadText([{ name: 'api_request', tsMs: T0, attrs: apiAttrs() }]));
  raw.resourceLogs[0].scopeLogs[0].logRecords[0].attributes.push({ key: 'cache_read_tokens', value: { intValue: '9007199254740993' } });
  const refused = decodeOtlpLogs(JSON.stringify(raw));
  assert.equal(refused.ok, true, 'one unreadable record does not void the payload');
  assert.equal(refused.value.length, 0);
  assert.equal(refused.rejected.length, 1);
  assert.match(refused.rejected[0].reason, /cache_read_tokens/);
});

test('One_unreadable_record_is_rejected_by_itself_and_its_neighbours_survive', () => {
  const raw = JSON.parse(payloadText([
    { name: 'api_request', tsMs: T0, attrs: apiAttrs({ request_id: 'good_1' }) },
    { name: 'api_request', tsMs: T0, attrs: apiAttrs({ request_id: 'bad' }) },
    { name: 'api_request', tsMs: T0, attrs: apiAttrs({ request_id: 'good_2' }) },
  ]));
  raw.resourceLogs[0].scopeLogs[0].logRecords[1].attributes.push({ key: 'input_tokens', value: { intValue: '9007199254740993' } });
  const r = decodeOtlpLogs(JSON.stringify(raw));
  assert.equal(r.ok, true);
  assert.deepEqual(apiRequests(r.value).map(q => q.requestId), ['good_1', 'good_2']);
  assert.deepEqual(r.rejected.map(x => x.index), [1]);
  assert.match(r.rejected[0].reason, /input_tokens/);
});

test('A_record_without_query_source_is_summed_under_unknown_never_dropped', () => {
  const attrs = apiAttrs({ query_source: undefined, request_id: 'r_nosrc' });
  const view = otelView(apiRequests(decoded(payloadText([{ name: 'api_request', tsMs: T0, attrs }]))));
  assert.equal(view.incomplete, 0);
  assert.deepEqual(Object.keys(view.perModel['claude-opus-5-5'].bySource), ['unknown']);
  assert.equal(view.perModel['claude-opus-5-5'].bySource.unknown.cacheRead, 300);
});

test('The_same_request_id_sent_twice_is_counted_once_and_reported', () => {
  const rec = { name: 'api_request', tsMs: T0, attrs: apiAttrs({ request_id: 'dup' }) };
  const other = { name: 'api_request', tsMs: T0, attrs: apiAttrs({ request_id: 'other', cache_read_tokens: 7 }) };
  const view = otelView(apiRequests(decoded(payloadText([rec, rec, other, rec]))));
  assert.equal(view.requests.length, 2);
  assert.equal(view.duplicates, 2);
  assert.equal(view.conflictingDuplicates, 0);
  assert.equal(view.perModel['claude-opus-5-5'].bySource.main.requests, 2);
  const conflict = otelView(apiRequests(decoded(payloadText([rec, { ...rec, attrs: { ...rec.attrs, output_tokens: 999 } }]))));
  assert.deepEqual([conflict.duplicates, conflict.conflictingDuplicates], [1, 1]);
  const noId = { name: 'api_request', tsMs: T0, attrs: apiAttrs({ request_id: undefined }) };
  assert.equal(otelView(apiRequests(decoded(payloadText([noId, noId])))).requests.length, 2, 'records without an id cannot be told apart, so none is dropped');
});

test('The_receiver_counts_a_resent_request_once_in_the_live_sum', async t => {
  const rx = await startReceiver({ dir: tmpDir('recv-dup') });
  t.after(() => rx.close());
  for (let i = 0; i < 3; i += 1) assert.equal((await post(`http://127.0.0.1:${rx.port}`, '/v1/logs', synthetic)).status, 200);
  const live = rx.liveSum();
  assert.equal(live.requests, 3);
  assert.ok(Math.abs(live.usd - (0.0204 + 0.0233382 + 0.001493)) < 1e-9);
  assert.equal(live.duplicates, 6);
});

test('A_payload_with_one_unreadable_record_is_accepted_and_the_rest_is_kept', async t => {
  const dir = tmpDir('recv-reject');
  const rx = await startReceiver({ dir });
  t.after(() => rx.close());
  const raw = JSON.parse(payloadText([{ name: 'api_request', tsMs: T0, attrs: apiAttrs({ request_id: 'ok' }) }, { name: 'api_request', tsMs: T0, attrs: apiAttrs({ request_id: 'bad' }) }]));
  raw.resourceLogs[0].scopeLogs[0].logRecords[1].attributes.push({ key: 'input_tokens', value: { intValue: '9007199254740993' } });
  assert.equal((await post(`http://127.0.0.1:${rx.port}`, '/v1/logs', JSON.stringify(raw))).status, 200);
  assert.equal(parseLines(readFileSync(path.join(dir, 'otel.ndjson'), 'utf8')).length, 1);
  assert.equal(rx.rejectedRecords, 1);
  assert.match(readFileSync(path.join(dir, 'otel-rejected.ndjson'), 'utf8'), /input_tokens/);
});

test('Text_that_is_not_an_otlp_logs_payload_is_a_value_not_a_throw', () => {
  assert.equal(decodeOtlpLogs('not json').ok, false);
  assert.equal(decodeOtlpLogs('{"resourceMetrics":[]}').ok, false);
  assert.equal(decodeOtlpLogs('{"resourceLogs":[]}').ok, true);
});

test('A_missing_token_attribute_is_unknown_not_zero', () => {
  const attrs = apiAttrs();
  delete attrs.cache_read_tokens;
  const reqs = apiRequests(decoded(payloadText([{ name: 'api_request', tsMs: T0, attrs }])));
  assert.equal(reqs[0].cacheRead, null);
  assert.deepEqual(reqs[0].missing, ['cache_read_tokens']);
  const view = otelView(reqs);
  assert.equal(view.incomplete, 1);
  assert.deepEqual(view.perModel, {}, 'an incomplete record is excluded from every sum');
});

test('The_record_name_comes_from_the_body_or_the_event_name_attribute', () => {
  const byBody = { timeUnixNano: '1000000', body: { stringValue: 'claude_code.api_request' }, attributes: [] };
  const byAttr = { timeUnixNano: '1000000', attributes: [{ key: 'event.name', value: { stringValue: 'api_request' } }] };
  const other = { timeUnixNano: '1000000', body: { stringValue: 'claude_code.user_prompt' }, attributes: [] };
  const wrap = recs => JSON.stringify({ resourceLogs: [{ scopeLogs: [{ logRecords: recs }] }] });
  assert.deepEqual(decoded(wrap([byBody, byAttr, other])).map(r => r.name), ['api_request', 'api_request', 'user_prompt']);
  assert.equal(apiRequests(decoded(wrap([byBody, byAttr, other]))).length, 2);
});

test('A_subagent_request_needs_an_agent_id_and_a_main_request_does_not', () => {
  assert.deepEqual([...REQUIRED_FOR_SUBAGENT], ['agent_id'], 'the 2.1.310 capture: a depth-one child carries agent_id and no parent_agent_id');
  const main = apiRequests(decoded(payloadText([{ name: 'api_request', tsMs: T0, attrs: apiAttrs() }])));
  assert.deepEqual(main[0].missing, []);
  assert.equal(main[0].agentId, null);
  for (const qs of ['subagent', 'agent:builtin:general-purpose']) {
    const sub = apiRequests(decoded(payloadText([{ name: 'api_request', tsMs: T0, attrs: apiAttrs({ query_source: qs }) }])));
    assert.deepEqual(sub[0].missing, ['agent_id'], qs);
    assert.equal(sub[0].role, 'subagent');
  }
  const withId = apiRequests(decoded(payloadText([{ name: 'api_request', tsMs: T0, attrs: apiAttrs({ query_source: 'agent:builtin:general-purpose', agent_id: 'a1' }) }])));
  assert.deepEqual(withId[0].missing, []);
  assert.equal(withId[0].parentAgentId, null);
});

test('The_raw_query_source_maps_to_one_of_three_roles', () => {
  assert.deepEqual(['sdk', 'main'].map(roleOf), ['main', 'main']);
  assert.deepEqual(['subagent', 'agent:builtin:general-purpose', 'agent:custom:x'].map(roleOf), ['subagent', 'subagent', 'subagent']);
  assert.deepEqual(['auxiliary', 'compact', 'agent', 'sdk2', 'Agent:x', ''].map(roleOf), ['auxiliary', 'auxiliary', 'auxiliary', 'auxiliary', 'auxiliary', null]);
  assert.equal(roleOf(null), null, 'an absent source is unknown, not auxiliary');
  assert.equal(roleOf(undefined), null);
});

test('The_view_buckets_by_role_and_keeps_the_raw_source_on_the_request', () => {
  const attrs = (qs, id) => ({ name: 'api_request', tsMs: T0, attrs: apiAttrs({ query_source: qs, request_id: id, agent_id: qs.startsWith('agent:') ? 'a' : undefined }) });
  const reqs = apiRequests(decoded(payloadText([attrs('sdk', 'r1'), attrs('agent:builtin:general-purpose', 'r2'), attrs('compact', 'r3')])));
  assert.deepEqual(reqs.map(r => [r.querySource, r.role]), [['sdk', 'main'], ['agent:builtin:general-purpose', 'subagent'], ['compact', 'auxiliary']]);
  assert.deepEqual(Object.keys(otelView(reqs).perModel['claude-opus-5-5'].bySource).sort(), ['auxiliary', 'main', 'subagent']);
});

test('The_plans_attribute_names_are_the_ones_the_decoder_reads', () => {
  assert.deepEqual(Object.keys(API_REQUEST_ATTRS).sort(), [
    'agent_id', 'cache_creation_tokens', 'cache_read_tokens', 'cost_usd', 'duration_ms', 'input_tokens', 'model', 'output_tokens',
    'parent_agent_id', 'query_source', 'request_id', 'session.id',
  ]);
});

test('The_cache_write_split_the_binary_names_is_read_when_present_and_never_reported_missing', () => {
  const without = apiRequests(decoded(payloadText([{ name: 'api_request', tsMs: T0, attrs: apiAttrs() }])))[0];
  assert.equal(without.cacheCreation5m, null);
  assert.deepEqual(without.missing, []);
  const withSplit = apiRequests(decoded(payloadText([{ name: 'api_request', tsMs: T0, attrs: apiAttrs({ cache_creation_5m_tokens: 15, cache_creation_1h_tokens: 25 }) }])))[0];
  assert.deepEqual([withSplit.cacheCreation5m, withSplit.cacheCreation1h], [15, 25]);
});

test('Numeric_attributes_sent_as_plain_decimal_strings_are_read_and_other_strings_are_missing', () => {
  const [a] = apiRequests(decoded(payloadText([{ name: 'api_request', tsMs: T0, attrs: apiAttrs({ input_tokens: '17', cost_usd: '0.25', output_tokens: 'many' }) }])));
  assert.equal(a.input, 17);
  assert.equal(a.costUsd, 0.25);
  assert.equal(a.output, null);
  assert.deepEqual(a.missing, ['output_tokens']);
});

test('The_view_sums_per_model_and_source_and_keeps_incomplete_records_out', () => {
  const view = otelView(apiRequests(decoded(synthetic)));
  assert.equal(view.present, true);
  assert.equal(view.incomplete, 0);
  assert.deepEqual(Object.keys(view.perModel).sort(), ['claude-haiku-4-5-20251001', 'claude-opus-5-5', 'claude-sonnet-5-5']);
  const sonnet = view.perModel['claude-sonnet-5-5'].bySource.subagent;
  assert.deepEqual([sonnet.input, sonnet.output, sonnet.cacheRead, sonnet.cacheCreation, sonnet.requests], [4, 118, 5951, 8384, 1]);
  assert.equal(Object.keys(view.perModel['claude-haiku-4-5-20251001'].bySource)[0], 'auxiliary');
  assert.equal(otelView([]).present, false);
});

test('Tool_results_carry_their_ids_and_a_missing_one_is_named', () => {
  const tr = toolResults(decoded(synthetic));
  assert.equal(tr.length, 1);
  assert.equal(tr[0].toolUseId, 'toolu_1');
  assert.equal(tr[0].durationMs, 120);
  const bare = toolResults(decoded(payloadText([{ name: 'tool_result', tsMs: T0, attrs: { tool_name: 'Bash' } }])));
  assert.deepEqual(bare[0].missing, ['tool_use_id', 'duration_ms']);
  assert.equal(bare[0].toolUseId, null);
});

test('The_stamp_verdict_is_a_decided_direction', () => {
  assert.ok(['end', 'start', 'unknown'].includes(OTEL_STAMP_MARKS), `OTEL_STAMP_MARKS is ${OTEL_STAMP_MARKS}`);
});

test('The_receiver_binds_loopback_only_and_replays_a_payload_into_its_files', async t => {
  const dir = tmpDir('recv');
  const rx = await startReceiver({ dir });
  t.after(() => rx.close());
  assert.equal(rx.server.address().address, '127.0.0.1');
  const base = `http://127.0.0.1:${rx.port}`;
  const res = await post(base, '/v1/logs', synthetic);
  assert.equal(res.status, 200);
  assert.deepEqual(await res.json(), {});

  const raw = parseLines(readFileSync(path.join(dir, 'otel-raw.ndjson'), 'utf8'));
  assert.equal(raw.length, 1);
  assert.deepEqual(Object.keys(raw[0]).sort(), ['body', 'contentType', 'path', 'receivedAt']);
  assert.equal(raw[0].body, synthetic, 'the body is stored raw');
  const records = parseLines(readFileSync(path.join(dir, 'otel.ndjson'), 'utf8'));
  assert.equal(records.length, 4);
  const live = rx.liveSum();
  assert.ok(Math.abs(live.usd - (0.0204 + 0.0233382 + 0.001493)) < 1e-9);
  assert.equal(live.requests, 3);
  assert.equal(live.costUnknown, 0);
  assert.equal(live.usd, liveSumOf(records).usd, 'the live sum equals the sum of the decoded file');
  assert.ok(live.lastAt > 0);
});

test('A_metrics_post_is_accepted_and_kept_raw_without_a_decode', async t => {
  const dir = tmpDir('recv-metrics');
  const rx = await startReceiver({ dir });
  t.after(() => rx.close());
  const res = await post(`http://127.0.0.1:${rx.port}`, '/v1/metrics', '{"resourceMetrics":[]}');
  assert.equal(res.status, 200);
  assert.equal(parseLines(readFileSync(path.join(dir, 'otel-raw.ndjson'), 'utf8'))[0].path, '/v1/metrics');
  assert.equal(existsSync(path.join(dir, 'otel.ndjson')), false);
});

test('A_protobuf_post_is_refused_415_and_recorded_as_misconfigured', async t => {
  const dir = tmpDir('recv-proto');
  const rx = await startReceiver({ dir });
  t.after(() => rx.close());
  const res = await post(`http://127.0.0.1:${rx.port}`, '/v1/logs', new Uint8Array([8, 1]), 'application/x-protobuf');
  assert.equal(res.status, 415);
  assert.equal(rx.misconfigured, 1);
  assert.equal(existsSync(path.join(dir, 'otel.ndjson')), false);
  assert.match(readFileSync(path.join(dir, 'otel-misconfigured.ndjson'), 'utf8'), /application\/x-protobuf/);
});

test('A_body_over_eight_megabytes_is_refused_413', async t => {
  const dir = tmpDir('recv-big');
  const rx = await startReceiver({ dir });
  t.after(() => rx.close());
  const base = `http://127.0.0.1:${rx.port}`;
  const res = await post(base, '/v1/logs', 'x'.repeat(9 * 1024 * 1024));
  assert.equal(res.status, 413);
  assert.equal(existsSync(path.join(dir, 'otel-raw.ndjson')), false, 'the oversized body is not stored');
  assert.equal((await post(base, '/v1/logs', synthetic)).status, 200, 'the server stays up');
});

test('An_undecodable_json_body_is_400_and_counted_but_kept_raw', async t => {
  const dir = tmpDir('recv-bad');
  const rx = await startReceiver({ dir });
  t.after(() => rx.close());
  const res = await post(`http://127.0.0.1:${rx.port}`, '/v1/logs', '{"resourceMetrics":[]}');
  assert.equal(res.status, 400);
  assert.equal(rx.undecodable, 1);
  assert.equal(parseLines(readFileSync(path.join(dir, 'otel-raw.ndjson'), 'utf8')).length, 1);
});

test('Other_paths_and_methods_are_refused', async t => {
  const rx = await startReceiver({ dir: tmpDir('recv-paths') });
  t.after(() => rx.close());
  const base = `http://127.0.0.1:${rx.port}`;
  assert.equal((await post(base, '/v1/traces', '{}')).status, 404);
  assert.equal((await fetch(`${base}/v1/logs`)).status, 405);
});

test('Close_resolves_only_after_the_listening_socket_is_closed', async () => {
  const rx = await startReceiver({ dir: tmpDir('recv-close') });
  const { port } = rx;
  await rx.close();
  const refused = await new Promise(resolve => {
    const s = net.connect({ port, host: '127.0.0.1' });
    s.on('connect', () => { s.destroy(); resolve(false); });
    s.on('error', () => resolve(true));
  });
  assert.equal(refused, true);
});

test('The_receiver_env_is_the_one_the_capture_prescribes', () => {
  const env = otelEnv(4318);
  assert.deepEqual(env, {
    CLAUDE_CODE_ENABLE_TELEMETRY: '1', OTEL_LOGS_EXPORTER: 'otlp', OTEL_METRICS_EXPORTER: 'otlp', OTEL_EXPORTER_OTLP_PROTOCOL: 'http/json',
    OTEL_EXPORTER_OTLP_ENDPOINT: 'http://127.0.0.1:4318', OTEL_LOGS_EXPORT_INTERVAL: '1000', OTEL_METRIC_EXPORT_INTERVAL: '1000',
    OTEL_LOG_TOOL_DETAILS: '1', OTEL_LOG_USER_PROMPTS: '0',
  });
  assert.ok(Object.isFrozen(env));
});
