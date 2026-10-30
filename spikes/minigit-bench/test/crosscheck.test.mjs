import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { envelopeView, transcriptView } from '../lib/claude-usage.mjs';
import { CHECK_STOP_RULES, crosscheck, loadPolicy, matchTO, seriesStopsOf } from '../lib/crosscheck.mjs';
import { apiRequests, decodeOtlpLogs, otelView } from '../lib/otel-decode.mjs';
import { asTheCliBills, loadPrices, priceFor, priceUsage } from '../lib/prices.mjs';
import { spikeDir } from './helpers.mjs';
import { TWO_CHILDREN, apiAttrs, asTranscripts, otlpPayload, readEvents } from './synth.mjs';

const readJson = (...p) => JSON.parse(readFileSync(path.join(spikeDir, ...p), 'utf8'));
const table = loadPrices(readJson('inputs', 'prices-2026-10-29.json')).value;
const policyFile = readJson('inputs', 'crosscheck-policy.json');
const policyWith = (over = {}) => {
  const p = loadPolicy({ ...policyFile, ...over });
  assert.equal(p.ok, true, p.reason);
  return p.value;
};
const real = readEvents(TWO_CHILDREN);
const E = envelopeView(real);
const T = transcriptView(asTranscripts(real));
const OPUS = 'claude-opus-5-5';
const SONNET = 'claude-sonnet-5-5';
const HAIKU = 'claude-haiku-4-5-20251001';

/** Specs -> the O view, through the real OTLP encoding, decoder and view (never a hand-built O). */
function buildO(specs) {
  const records = specs.map((s, i) => ({ name: 'api_request', tsMs: 1760000000000 + i * 1000, attrs: apiAttrs(s) }));
  const decoded = decodeOtlpLogs(JSON.stringify(otlpPayload(records)));
  assert.equal(decoded.ok, true, decoded.reason);
  return otelView(apiRequests(decoded.value));
}

const CLI_VERSION = '2.1.289'; // the version the two-children fixture was captured on
const cliUsd = (model, t) => priceUsage(priceFor(asTheCliBills(table, CLI_VERSION), model).value, t);

/** One O record per T message at the CLI's rates; `over(m)` varies a record; `extra` adds records T never named. */
function oFromT(t, { over = () => ({}), extra = [], outputOf = m => m.output, inflate = 1 } = {}) {
  const specs = t.messages.map(m => {
    const output = outputOf(m);
    const cost = cliUsd(m.model, { input: m.input, output, cacheRead: m.cacheRead, cacheWrite5m: m.cacheWrite5m, cacheWrite1h: m.cacheWrite1h }) * inflate;
    return {
      model: m.model, query_source: m.source, request_id: m.requestId, input_tokens: m.input, output_tokens: output, cache_read_tokens: m.cacheRead,
      cache_creation_tokens: m.cacheWrite, cost_usd: cost, ...(m.source === 'subagent' ? { agent_id: m.agentId ?? 'a', parent_agent_id: 'root' } : {}), ...over(m),
    };
  });
  return buildO([...specs, ...extra]);
}

const AUX = { model: HAIKU, query_source: 'auxiliary', request_id: 'req_aux', input_tokens: 1403, output_tokens: 18, cache_read_tokens: 0, cache_creation_tokens: 0, cost_usd: 0.001493 };

/** O built from the envelope's modelUsage: Opus main = the envelope's main usage, the rest of Opus a subagent; Sonnet a subagent; Haiku auxiliary. */
function oFromEnvelope(e, { without = [] } = {}) {
  const opus = e.perModel[OPUS];
  const mainCost = cliUsd(OPUS, { input: e.main.input, output: e.main.output, cacheRead: e.main.cacheRead, cacheWrite5m: e.main.cacheWrite5m, cacheWrite1h: e.main.cacheWrite1h });
  const specs = [
    { model: OPUS, query_source: 'main', request_id: 'r1', input_tokens: e.main.input, output_tokens: e.main.output, cache_read_tokens: e.main.cacheRead, cache_creation_tokens: e.main.cacheWrite5m + e.main.cacheWrite1h, cost_usd: mainCost },
    {
      model: OPUS, query_source: 'subagent', request_id: 'r2', agent_id: 'a1', parent_agent_id: 'root', input_tokens: opus.input - e.main.input, output_tokens: opus.output - e.main.output,
      cache_read_tokens: opus.cacheRead - e.main.cacheRead, cache_creation_tokens: opus.cacheWrite - e.main.cacheWrite5m - e.main.cacheWrite1h, cost_usd: opus.costUsd - mainCost,
    },
    { model: SONNET, query_source: 'subagent', request_id: 'r3', agent_id: 'a2', parent_agent_id: 'root', input_tokens: 4, output_tokens: 118, cache_read_tokens: 5951, cache_creation_tokens: 8384, cost_usd: e.perModel[SONNET].costUsd },
    { model: HAIKU, query_source: 'auxiliary', request_id: 'r4', input_tokens: 1403, output_tokens: 18, cache_read_tokens: 0, cache_creation_tokens: 0, cost_usd: e.perModel[HAIKU].costUsd },
  ];
  return buildO(specs.filter(s => !without.includes(s.model)));
}

const run = (over = {}) => crosscheck({ E, O: null, T: null, table, policy: policyWith(), cliVersion: CLI_VERSION, ...over });

test('The_committed_policy_loads_and_an_illegal_value_names_the_legal_ones', () => {
  assert.deepEqual([policyFile.t1, policyFile.t2, policyFile.t3, policyFile.t3OutputSource, policyFile.join], ['stop', 'otel-incomplete', 'investigate', 'transcript', 'request_id']);
  const bad = loadPolicy({ ...policyFile, join: 'guess' });
  assert.equal(bad.ok, false);
  assert.match(bad.reason, /policy\.join.*request_id, query_source/);
  assert.equal(loadPolicy({ ...policyFile, decidedOn: 'today' }).ok, false);
  assert.equal(loadPolicy({ ...policyFile, reason: ' ' }).ok, false);
  assert.equal(loadPolicy(null).ok, false);
});

test('T1_holds_on_the_real_envelope', () => {
  const c = run();
  assert.equal(c.t1.status, 'pass');
  assert.ok(c.t1.gap < 1e-6);
  for (const id of ['t2', 't3', 't4']) {
    assert.equal(c[id].status, 'unchecked', id);
    assert.ok(c[id].why.length > 0, `${id} says why`);
  }
});

test('T1_breaches_by_the_policy_when_the_envelope_disagrees_with_itself', () => {
  const skewed = { ...E, totalCostUsd: E.totalCostUsd + 0.05 };
  const stop = run({ E: skewed });
  assert.deepEqual([stop.t1.status, stop.t1.verdict], ['breach', 'stop']);
  assert.ok(stop.t1.gap > 0.04 && stop.t1.threshold === 0.01);
  assert.equal(run({ E: skewed, policy: policyWith({ t1: 'investigate' }) }).t1.verdict, 'investigate');
  assert.equal(run({ E: { known: false, why: 'x' } }).t1.status, 'unchecked');
});

test('T2_passes_when_otel_matches_the_envelope_within_its_thresholds', () => {
  const c = run({ E, O: oFromEnvelope(E) });
  assert.equal(c.t2.status, 'pass', c.t2.detail);
  assert.deepEqual(Object.keys(c.t2.table).sort(), [HAIKU, OPUS, SONNET]);
  assert.deepEqual(Object.keys(c.t2.table[OPUS]).sort(), ['cacheCreation', 'cacheRead', 'costUsd', 'input', 'output']);
  assert.match(c.t2.detail, /claude-sonnet-5-5.*gap/);
  assert.ok(Object.values(c.t2.table).every(m => Object.values(m).every(cell => cell.ok)));
});

test('T2_breach_marks_the_cell_otel_incomplete_and_keeps_it', () => {
  const c = run({ E, O: oFromEnvelope(E, { without: [SONNET] }) });
  assert.deepEqual([c.t2.status, c.t2.verdict], ['breach', 'otel incomplete']);
  assert.match(c.t2.detail, /claude-sonnet-5-5/);
  assert.equal(c.t1.status, 'pass');
});

test('T2_is_the_stop_under_the_pre_registered_fallback', () => {
  const c = run({ E, O: oFromEnvelope(E, { without: [SONNET] }), policy: policyWith({ t2: 'stop', t3: 'fallback-t2' }) });
  assert.equal(c.t2.verdict, 'stop');
});

test('T2_tolerates_a_hundred_tokens_and_not_a_thousand', () => {
  const near = (delta) => {
    const o = oFromEnvelope(E);
    const specs = o.requests.map(r => ({
      model: r.model, query_source: r.querySource, request_id: r.requestId, input_tokens: r.input, output_tokens: r.output + (r.model === SONNET ? delta : 0),
      cache_read_tokens: r.cacheRead, cache_creation_tokens: r.cacheCreation, cost_usd: r.costUsd, ...(r.agentId ? { agent_id: r.agentId, parent_agent_id: r.parentAgentId } : {}),
    }));
    return run({ E, O: buildO(specs) }).t2.status;
  };
  assert.equal(near(100), 'pass');
  assert.equal(near(101), 'breach', 'output 118 + 101 differs by more than max(0.5 %, 100)');
});

test('T2_is_unchecked_not_a_pass_when_an_otel_record_is_incomplete', () => {
  const full = oFromEnvelope(E);
  const lacking = buildO([{ model: SONNET, query_source: 'subagent', request_id: 'rx', agent_id: 'a', parent_agent_id: 'p', input_tokens: 1, output_tokens: 1, cache_read_tokens: undefined, cache_creation_tokens: 1, cost_usd: 0 }]);
  assert.equal(lacking.incomplete, 1);
  const c = run({ E, O: { ...full, incomplete: lacking.incomplete } });
  assert.equal(c.t2.status, 'unchecked');
  assert.match(c.t2.why, /lower bounds/);
});

test('T3_matches_T_against_the_O_records_it_names_and_lists_the_rest_by_source', () => {
  const c = run({ E, T, O: oFromT(T, { extra: [AUX] }) });
  assert.equal(c.join.by, 'request_id');
  assert.equal(c.join.unmatchedT, 0);
  assert.deepEqual(c.join.unmatchedO, { auxiliary: 1 });
  assert.equal(c.t3.status, 'pass', c.t3.detail);
  assert.ok(c.t3.gap < 0.02);
  assert.ok(Math.abs(c.t3.reference - T.messages.reduce((a, m) => a + cliUsd(m.model, { input: m.input, output: m.output, cacheRead: m.cacheRead, cacheWrite5m: m.cacheWrite5m, cacheWrite1h: m.cacheWrite1h }), 0)) < 1e-9, 'the auxiliary cost is not in the reference');
});

test('T3_is_investigate_until_armed_then_stop', () => {
  const O = oFromT(T, { inflate: 1.05 });
  const at = t3 => run({ E, T, O, policy: policyWith({ t3, ...(t3 === 'fallback-t2' ? { t2: 'stop' } : {}) }) }).t3;
  assert.equal(at('investigate').verdict, 'investigate');
  assert.equal(at('investigate').status, 'breach');
  assert.equal(at('stop').verdict, 'stop');
  const fallback = at('fallback-t2');
  assert.equal(fallback.verdict, 'investigate');
  assert.match(fallback.why, /fallback-t2/);
});

test('T3_takes_output_from_otel_when_the_policy_says_so', () => {
  const billed = m => m.output + 500;
  const O = oFromT(T, { outputOf: billed });
  const fromOtel = run({ E, T, O, policy: policyWith({ t3OutputSource: 'otel' }) });
  const fromTranscript = run({ E, T, O, policy: policyWith({ t3OutputSource: 'transcript' }) });
  const expected = T.messages.reduce((a, m) => a + cliUsd(m.model, { input: m.input, output: billed(m), cacheRead: m.cacheRead, cacheWrite5m: m.cacheWrite5m, cacheWrite1h: m.cacheWrite1h }), 0);
  assert.ok(Math.abs(fromOtel.t3.computed - expected) < 1e-9);
  assert.equal(fromOtel.t3.status, 'pass');
  assert.notEqual(fromTranscript.t3.computed, fromOtel.t3.computed);
});

test('T3_and_T4_are_unchecked_when_a_transcript_message_has_no_otel_record', () => {
  const first = T.messages[0].requestId;
  const O = oFromT(T, { over: m => (m.requestId === first ? { request_id: 'req_elsewhere' } : {}) });
  const c = run({ E, T, O });
  for (const id of ['t3', 't4']) {
    assert.equal(c[id].status, 'unchecked');
    assert.match(c[id].why, /otel incomplete: 1 transcript message/);
  }
  assert.equal(c.join.unmatchedT, 1);
});

test('T3_and_T4_are_unchecked_without_transcripts_or_without_otel', () => {
  assert.match(run({ E, T: transcriptView([]), O: oFromT(T) }).t3.why, /no transcripts/);
  assert.match(run({ E, T, O: buildO([]) }).t4.why, /no otel/);
});

test('T4_holds_on_a_fixture_derived_pair_and_breaches_with_its_table_when_a_count_differs', () => {
  const pass = run({ E, T, O: oFromT(T) });
  assert.equal(pass.t4.status, 'pass', pass.t4.detail);
  assert.equal(pass.t4.gap, 0);
  const firstSonnet = T.messages.find(m => m.model === SONNET);
  const O = oFromT(T, { over: m => (m.id === firstSonnet.id ? { cache_read_tokens: m.cacheRead + 50 } : {}) });
  const breach = run({ E, T, O });
  assert.deepEqual([breach.t4.status, breach.t4.verdict], ['breach', 'investigate']);
  assert.equal(breach.t4.table[SONNET].cacheRead.ok, false);
  assert.equal(breach.t4.table[SONNET].input.ok, true);
  assert.match(breach.t4.detail, /claude-sonnet-5-5 cacheRead/);
});

test('T3_is_unchecked_when_a_model_is_unpriced_or_its_split_is_unknown', () => {
  const unsplit = transcriptView(asTranscripts(real).map(f => ({ ...f, text: f.text.replaceAll('"cache_creation":{', '"cache_creation_gone":{') })));
  assert.equal(unsplit.messages.some(m => m.splitKnown), false, 'the fixture really lost its split');
  const c = run({ E, T: unsplit, O: oFromT(T) });
  assert.equal(c.t3.status, 'unchecked');
  assert.match(c.t3.why, /5m\/1h split/);
  const noPrice = loadPrices({ ...readJson('inputs', 'prices-2026-10-29.json'), models: readJson('inputs', 'prices-2026-10-29.json').models.filter(m => m.modelId !== SONNET), cliRates: [] }).value;
  assert.match(run({ E, T, O: oFromT(T), table: noPrice }).t3.why, /claude-sonnet-5-5/);
});

test('The_query_source_join_requires_subagent_coverage', () => {
  const main = transcriptView(asTranscripts(real).filter(f => !f.relPath.includes('/subagents/')));
  assert.equal(main.coverage.subagent, false);
  const c = run({ E, T: main, O: oFromT(T, { extra: [AUX] }), policy: policyWith({ join: 'query_source' }) });
  for (const id of ['t3', 't4']) {
    assert.equal(c[id].status, 'unchecked');
    assert.match(c[id].why, /T covers only main/);
  }
});

test('The_query_source_join_compares_T_with_main_plus_subagent_and_lists_the_rest', () => {
  const policy = policyWith({ join: 'query_source' });
  const c = run({ E, T, O: oFromT(T, { extra: [AUX] }), policy });
  assert.equal(c.join.by, 'query_source');
  assert.deepEqual(c.join.unmatchedO, { auxiliary: 1 });
  assert.equal(c.t3.status, 'pass', c.t3.detail);
  assert.equal(c.t4.status, 'pass', c.t4.detail);
});

test('Matching_names_exactly_the_records_whose_request_id_a_transcript_carries', () => {
  const O = oFromT(T, { extra: [AUX] });
  const m = matchTO(T, O, 'request_id');
  assert.equal(m.matched.length, T.messages.length);
  assert.equal(m.matched.some(r => r.querySource === 'auxiliary'), false);
  assert.equal(matchTO(T, O, 'query_source').matched.length, T.messages.length);
});

const REAL_SPELLING = { main: 'sdk', subagent: 'agent:builtin:general-purpose' };

test('The_query_source_join_understands_the_spellings_the_cli_really_uses', () => {
  const O = oFromT(T, { over: m => ({ query_source: REAL_SPELLING[m.source] }), extra: [{ ...AUX, query_source: 'compact' }] });
  const c = run({ E, T, O, policy: policyWith({ join: 'query_source' }) });
  assert.deepEqual(c.join.unmatchedO, { auxiliary: 1 });
  assert.equal(c.join.matched, T.messages.length);
  assert.equal(c.t3.status, 'pass', c.t3.detail);
  assert.equal(c.t4.status, 'pass', c.t4.detail);
});

test('T3_prices_the_cli_billed_view_of_the_sessions_own_cli_version', () => {
  const O = oFromT(T);
  const at289 = run({ E, T, O, cliVersion: '2.1.289' }).t3;
  const at310 = run({ E, T, O, cliVersion: '2.1.310' }).t3;
  assert.equal(at289.status, 'pass');
  assert.ok(Math.abs(at289.computed - at310.computed - (5951 * 0.1) / 1e6) < 1e-9, 'the sonnet cache read is $0.10 cheaper per million on 2.1.310');
  assert.equal(run({ E, T, O, cliVersion: undefined }).t3.computed, at310.computed, 'no version means published, never an older version\'s rate');
});

const mainOnly = transcriptView(asTranscripts(real).filter(f => !f.relPath.includes('/subagents/')));

test('T3_and_T4_are_unchecked_when_T_lost_the_subagent_transcripts_even_when_T3_is_armed', () => {
  const O = oFromT(T, { extra: [AUX] });
  assert.equal(mainOnly.coverage.subagent, false);
  for (const t3 of ['investigate', 'stop']) {
    const c = run({ E, T: mainOnly, O, policy: policyWith({ t3 }) });
    for (const id of ['t3', 't4']) {
      assert.equal(c[id].status, 'unchecked', `${id} under t3=${t3}`);
      assert.match(c[id].why, /T incomplete: \d+ main\/subagent api_request records have no transcript message/);
    }
    assert.ok(c.join.unmatchedO.subagent > 0 && c.join.unmatchedO.auxiliary === 1);
  }
});

test('An_api_request_with_no_role_that_T_does_not_name_also_makes_T_incomplete', () => {
  const c = run({ E, T, O: oFromT(T, { extra: [{ ...AUX, query_source: undefined, request_id: 'req_roleless' }] }) });
  assert.equal(c.join.unmatchedO.unknown, 1);
  assert.match(c.t3.why, /T incomplete: 1 main\/subagent api_request record/);
});

test('Two_transcript_messages_with_one_request_id_make_T3_and_T4_unchecked', () => {
  const dup = { ...T, messages: T.messages.map((m, i) => (i === 1 ? { ...m, requestId: T.messages[0].requestId } : m)) };
  const c = run({ E, T: dup, O: otelView(oFromT(dup).requests) });
  for (const id of ['t3', 't4']) assert.match(c[id].why, /1 request id\(s\) shared by more than one transcript message/, id);
});

test('Transcripts_with_disagreeing_duplicates_or_unreadable_lines_are_not_trusted_by_T3_or_T4', () => {
  const O = oFromT(T);
  const bad = { ...T, disagreements: [{ id: 'msg_x', field: 'cacheRead', values: [0, 1] }] };
  assert.match(run({ E, T: bad, O }).t3.why, /disagree/);
  const skipped = { ...T, skippedLines: 2 };
  assert.match(run({ E, T: skipped, O }).t4.why, /2 unreadable transcript line/);
  assert.equal(run({ E, T, O }).t4.status, 'pass', 'the same pair without the defects is checked');
});

test('Conflicting_duplicate_api_requests_make_T3_and_T4_unchecked', () => {
  const O = oFromT(T);
  const first = O.requests[0];
  const conflicting = otelView([...O.requests, { ...first, output: first.output + 7 }]);
  assert.equal(conflicting.conflictingDuplicates, 1);
  assert.match(run({ E, T, O: conflicting }).t3.why, /conflicting duplicate/);
});

test('A_policy_that_leaves_no_stop_at_all_is_refused', () => {
  const refused = loadPolicy({ ...policyFile, t3: 'fallback-t2', t2: 'otel-incomplete' });
  assert.equal(refused.ok, false);
  assert.match(refused.reason, /fallback-t2.*t2.*stop/);
  assert.equal(loadPolicy({ ...policyFile, t3: 'fallback-t2', t2: 'stop' }).ok, true);
  assert.equal(loadPolicy({ ...policyFile, t3: 'investigate', t2: 'otel-incomplete' }).ok, true);
});

// ---- T5: the broker's ledger against the envelope (E2.S2 D-17) -------------------------------------------------------------------

const ledger = (spentUsd, over = {}) => ({ present: true, forwarded: 3, refused: 0, unsettled: 0, spentUsd, settled: [], ...over });

test('T5_passes_when_the_brokers_ledger_equals_the_envelope_within_a_millionth_of_a_dollar', () => {
  assert.equal(policyFile.t5, 'investigate', 'the committed policy carries the T5 row');
  const same = run({ B: ledger(E.totalCostUsd + 0.0000005) });
  assert.equal(same.t5.status, 'pass', same.t5.detail);
  assert.equal(same.t5.threshold, 0.000001);
  const off = run({ B: ledger(E.totalCostUsd + 0.00001) });
  assert.deepEqual([off.t5.status, off.t5.verdict], ['breach', 'investigate']);
  assert.match(off.t5.detail, /broker/);
  assert.equal(loadPolicy({ ...policyFile, t5: 'guess' }).ok, false);
  assert.match(loadPolicy({ ...policyFile, t5: 'guess' }).reason, /t5.*stop, investigate/);
});

test('T5_is_unchecked_without_a_ledger_without_an_envelope_or_with_an_unsettled_request', () => {
  assert.match(run({ B: null }).t5.why, /no broker ledger/);
  assert.match(run({ B: { ...ledger(0), present: false } }).t5.why, /no broker ledger/);
  assert.match(run({ E: { known: false, why: 'x' }, B: ledger(1) }).t5.why, /no envelope/);
  const killed = run({ B: ledger(0.31, { unsettled: 1 }) });
  assert.equal(killed.t5.status, 'unchecked');
  assert.match(killed.t5.why, /1 request.*never settled.*0\.31/, 'B is then the price of what the session did spend, and says so');
});

const STOP_ALL = { t1: 'stop', t2: 'stop', t3: 'stop', t5: 'stop' };

// ---- series stops: one generic rule over every check whose policy is `stop` (review N3) --------------------------------------------

test('Every_check_whose_policy_is_stop_and_that_breaches_is_a_series_stop_and_no_other_verdict_is', () => {
  const check = (id, verdict, status = 'breach') => ({ id, status, verdict, detail: `${id} says ${verdict}` });
  const all = { t1: check('T1', 'stop'), t2: check('T2', 'stop'), t3: check('T3', 'stop'), t4: check('T4', 'investigate'), t5: check('T5', 'stop') };
  assert.deepEqual(seriesStopsOf(all, STOP_ALL, { enveloped: true }).map(x => x.rule), ['T1 breach', 'T2 breach', 'T3 breach', 'T5 breach'], 'T1, T2, T3 and T5 alike; T4 has no policy and only investigates');
  assert.deepEqual(seriesStopsOf(all, STOP_ALL, { enveloped: true }).map(x => x.detail), ['T1 says stop', 'T2 says stop', 'T3 says stop', 'T5 says stop']);
  const lenient = { t1: check('T1', 'investigate'), t2: check('T2', 'otel incomplete'), t3: check('T3', 'investigate'), t4: check('T4', 'investigate'), t5: check('T5', 'investigate') };
  assert.deepEqual(seriesStopsOf(lenient, STOP_ALL, { enveloped: true }), [], 'a breach under any other policy is investigated or flagged, never a stop');
  const passing = Object.fromEntries(Object.entries(all).map(([k, v]) => [k, { ...v, status: 'pass', verdict: 'ok' }]));
  assert.deepEqual(seriesStopsOf(passing, STOP_ALL, { enveloped: true }), []);
  assert.deepEqual([...CHECK_STOP_RULES], ['T1 breach', 'T2 breach', 'T3 breach', 'T5 breach', 'T1 unchecked', 'T2 unchecked', 'T3 unchecked', 'T5 unchecked'], 'the rules the runner refuses to pass with --after-stop are derived from the same list');
});

test('A_check_that_could_not_run_for_want_of_evidence_is_a_stop_under_a_stop_policy_and_only_then', () => {
  const unchecked = (id, why, extra = {}) => ({ id, status: 'unchecked', verdict: 'unchecked', detail: why, why, ...extra });
  const c = { t1: unchecked('T1', 'no envelope'), t2: unchecked('T2', 'no otel'), t3: unchecked('T3', 'T covers only main'), t4: unchecked('T4', 'no otel'), t5: unchecked('T5', 'no broker ledger') };
  const rules = (policy, opts) => seriesStopsOf(c, policy, opts).map(x => x.rule);
  assert.deepEqual(rules(STOP_ALL, { enveloped: true }), ['T1 unchecked', 'T2 unchecked', 'T3 unchecked', 'T5 unchecked'], 'fail closed: evidence that is missing is not a pass (T4 has no policy)');
  assert.deepEqual(rules({ ...STOP_ALL, t2: 'otel-incomplete', t3: 'investigate', t5: 'investigate' }, { enveloped: true }), ['T1 unchecked'], 'only the checks whose policy is stop');
  assert.deepEqual(rules(STOP_ALL, { enveloped: false }), [], 'a session that ended with no envelope (killed, never answered) has no evidence to be missing: its class says how it ended');
  const midRequest = { ...c, t5: unchecked('T5', '1 request(s) never settled', { byEnding: true }) };
  assert.deepEqual(seriesStopsOf(midRequest, { t5: 'stop' }, { enveloped: true }).map(x => x.rule), [], 'a request the cut-short session never settled is how it ended');
  assert.equal(seriesStopsOf({ t2: unchecked('T2', 'no otel') }, STOP_ALL, { enveloped: true })[0].detail, 'no otel', 'the detail says what was missing');
});

test('The_real_checks_mark_only_what_the_ending_explains_as_by_ending', () => {
  const policy = policyWith({ t5: 'stop' });
  const noEnvelope = crosscheck({ E: { known: false, why: 'no result event' }, O: { present: false }, T, B: { present: true, unsettled: 0, spentUsd: 1, forwarded: 1, refused: 0 }, table, policy, cliVersion: '2.1.310' });
  assert.equal(noEnvelope.t5.status, 'unchecked');
  const midRequest = crosscheck({ E, O: { present: false }, T, B: { present: true, unsettled: 2, spentUsd: 1, forwarded: 3, refused: 0 }, table, policy, cliVersion: '2.1.310' });
  assert.equal(midRequest.t5.byEnding, true, 'unsettled requests: the session was stopped mid-request');
  const noLedger = crosscheck({ E, O: { present: false }, T, B: { present: false }, table, policy, cliVersion: '2.1.310' });
  assert.equal(noLedger.t5.byEnding, undefined, 'a missing ledger is missing evidence, not an ending');
});
