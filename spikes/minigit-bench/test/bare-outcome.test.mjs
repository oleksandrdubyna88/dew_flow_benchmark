// How a bare session ENDED (E3b.S1 step 7): `bareOutcome` over records, first match wins. Fixtures are the shapes the launcher and the broker write.
import assert from 'node:assert/strict';
import { test } from 'node:test';
import { rateLimitSignal } from '../../policy-ab-quick/lib/ratelimit.mjs';
import { bareOutcome } from '../lib/bare-outcome.mjs';
import { REFUSAL_REASONS } from '../lib/broker.mjs';

const T0 = Date.parse('2026-10-30T10:00:00Z');
const at = s => new Date(T0 + s * 1000).toISOString();

// ---- fixtures: the broker's lines and the stream's events -----------------------------------------------------------------------

const forwarding = (seq, s) => ({ t: at(s), seq, event: 'forwarding' });
const settled = (seq, s, over = {}) => ({ t: at(s), seq, event: 'settled', status: 200, model: 'claude-opus-4-7', usage: { input_tokens: 10, output_tokens: 5 }, chargedUsd: 0.4, ...over });
const refused = (seq, s, reason = 'cap') => ({ t: at(s), seq, event: 'refused', reason, why: 'the budget' });
const ok2xx = (seq, s) => [forwarding(seq, s), settled(seq, s + 1)];

const envelope = (over = {}) => ({ type: 'result', subtype: 'success', is_error: false, total_cost_usd: 3.1, result: 'done', ...over });
const meta = (over = {}) => ({ stopped: null, outcome: { ok: true, label: 'ok' }, envelopeCostUsd: 3.1, broker: { spentUsd: 3.1, overCap: false }, samplers: { realInCmdline: 0, realInEnviron: 0 }, ...over });
const base = (over = {}) => ({
  meta: meta(), events: [envelope()], brokerLines: ok2xx(1, 0), aliasCheck: { verdict: 'ok', reasons: [] }, scan: { hits: 0 },
  contamination: { high: [], canary: [], tokenHits: [] }, handVerdicts: [], retired: false, abortReason: null, recomputeUsd: 3.12, ...over,
});
const classOf = over => bareOutcome(base(over)).class;

test('A_completed_session_with_a_transient_refusal_is_completed', () => {
  const r = bareOutcome(base({ brokerLines: [refused(1, 0), ...ok2xx(2, 4)] }));
  assert.equal(r.class, 'completed');
  assert.equal(r.data, true);
  assert.ok(r.flags.includes('broker refused 1'), r.flags.join());
  assert.equal(r.detail.brokerRefused.count, 1);
  assert.equal(r.detail.brokerRefused.backoffMs, 4000, 'from the refusal to the next admitted request');
});

test('A_final_cap_refusal_is_a_cap_kill_not_a_rate_limit_void', () => {
  // The broker answers its cap refusal with HTTP 429 `rate_limit_error`: the CLI ends with an is_error envelope, status 429. rateLimitSignal ALONE calls that a void.
  const events = [envelope({ subtype: 'success', is_error: true, api_error_status: 429, result: 'API Error: 429 rate_limit_error: the cell\'s budget is spent' })];
  assert.equal(rateLimitSignal(events).void, true, 'the refuted approach: the bare rateLimitSignal reading would void and pay for the session twice');
  const r = bareOutcome(base({ events, brokerLines: [...ok2xx(1, 0), refused(2, 60, 'cap')], meta: meta({ broker: { spentUsd: 5.97, overCap: false }, envelopeCostUsd: 5.97 }), recomputeUsd: 6.02 }));
  assert.equal(r.class, 'killed: cap');
  assert.equal(r.data, true);
  assert.equal(r.censoredAtUsd, 6.02, 'censored at the observed stop-rule figure: the largest of the three');
});

test('A_path_refusal_is_not_a_cap', () => {
  // The broker's REAL answers to the CLI (lib/broker.mjs): a path or body refusal is HTTP 403, a busy refusal 503, a cap refusal 429. The CLI's envelope carries that status.
  const answered = { path: 403, body: 403, busy: 503 };
  for (const reason of REFUSAL_REASONS.filter(r => r !== 'cap')) {
    const events = [envelope({ is_error: true, api_error_status: answered[reason], result: `API Error: ${answered[reason]}` })];
    const r = bareOutcome(base({ events, brokerLines: [...ok2xx(1, 0), refused(2, 60, reason)] }));
    assert.notEqual(r.class, 'killed: cap', `reason ${reason}`);
    assert.notEqual(r.class, 'stop: auth', `the broker's own ${answered[reason]} is not the credential failing (reason ${reason})`);
    assert.notEqual(r.class, 'void: upstream', `the broker's own ${answered[reason]} is not the provider (reason ${reason})`);
    assert.equal(r.class, 'void: harness', `reason ${reason}`);
    assert.equal(r.data, false);
    assert.match(r.detail.reason, new RegExp(`the broker itself refused .*${reason}`), 'the void says which refusal ended the session');
  }
  for (const reason of ['unknown', undefined]) {
    const events = [envelope({ is_error: true, api_error_status: 403, result: 'API Error: 403' })];
    const r = bareOutcome(base({ events, brokerLines: [...ok2xx(1, 0), { ...refused(2, 60), reason }] }));
    assert.equal(r.class, 'void: harness', `a refusal without a known reason is never a cap and never auth (${reason})`);
  }
  // a refusal the session SURVIVED is a flag, not a class
  assert.equal(bareOutcome(base({ brokerLines: [refused(1, 0, 'path'), ...ok2xx(2, 4)] })).class, 'completed');
});

test('The_credential_failing_is_an_upstream_401_or_403_and_only_that', () => {
  const upstream = status => bareOutcome(base({ events: [envelope({ is_error: true, api_error_status: status, result: `API Error ${status}` })], brokerLines: [...ok2xx(1, 0), forwarding(2, 9), settled(2, 10, { status, usage: null, chargedUsd: 0 })] }));
  assert.equal(upstream(401).class, 'stop: auth');
  assert.equal(upstream(403).class, 'stop: auth');
  // control: the same envelope status with the broker's own refusal as the last request is not auth
  const own = bareOutcome(base({ events: [envelope({ is_error: true, api_error_status: 403 })], brokerLines: [...ok2xx(1, 0), refused(2, 9, 'path')] }));
  assert.notEqual(own.class, 'stop: auth');
  // an envelope that says 403 while the broker's log shows no failed request at all is not read as the credential failing
  assert.notEqual(bareOutcome(base({ events: [envelope({ is_error: true, api_error_status: 403 })] })).class, 'stop: auth');
});

test('The_final_request_is_the_last_to_settle_not_the_last_to_open', () => {
  const T = { is_error: true, subtype: 'error_during_execution' };
  // the main request (seq 5) opens first and is answered 529 at 40 s; a concurrent haiku request (seq 6) opens later and settles 200 at 12 s
  const lines = [...ok2xx(1, 0), forwarding(5, 10), forwarding(6, 11), settled(6, 12), settled(5, 40, { status: 529, usage: null, chargedUsd: 0 })];
  assert.equal(bareOutcome(base({ events: [envelope({ ...T, api_error_status: 529 })], brokerLines: lines })).class, 'void: upstream', 'by settle time the 529 is last; by seq the 200 would have been');
  // the reverse: the later-opened request fails first, the earlier-opened one is answered 200 last: the session ended on a success
  const reverse = [...ok2xx(1, 0), forwarding(5, 10), forwarding(6, 11), settled(6, 12, { status: 529, usage: null, chargedUsd: 0 }), settled(5, 40)];
  assert.equal(bareOutcome(base({ brokerLines: reverse })).class, 'completed');
});

test('An_envelope_that_says_5xx_is_an_upstream_void_even_when_the_broker_log_is_not_conclusive', () => {
  const r = bareOutcome(base({ events: [envelope({ is_error: true, subtype: 'error_during_execution', api_error_status: 529 })], brokerLines: ok2xx(1, 0) }));
  assert.equal(r.class, 'void: upstream');
  assert.equal(r.detail.status, 529);
  assert.equal(r.data, false);
});

test('The_CLIs_own_budget_stop_is_a_cap_kill', () => {
  const events = [envelope({ subtype: 'error_max_budget_usd', is_error: true, total_cost_usd: 6.05 })];
  const r = bareOutcome(base({ events, meta: meta({ envelopeCostUsd: 6.05, broker: { spentUsd: 6.05, overCap: false } }), recomputeUsd: 6.06 }));
  assert.equal(r.class, 'killed: cap');
  assert.equal(r.censoredAtUsd, 6.06);
});

test('A_settled_cost_kill_is_a_cap_kill', () => {
  const r = bareOutcome(base({ events: [], meta: meta({ stopped: 'cost', envelopeCostUsd: null, broker: { spentUsd: 6.12, overCap: true } }), recomputeUsd: null }));
  assert.equal(r.class, 'killed: cap');
  assert.equal(r.censoredAtUsd, 6.12, 'no envelope: B is the figure');
  assert.ok(r.flags.includes('over cap'));
});

test('Only_a_session_that_ended_on_the_limit_is_a_rate_limit_void', () => {
  // (a) two upstream 429s, an assistant rate_limit marker mid-stream, then success
  const midway = [{ type: 'assistant', error: 'rate_limit', message: { content: [] } }, envelope()];
  const a = bareOutcome(base({ events: midway, brokerLines: [forwarding(1, 0), settled(1, 1, { status: 429, usage: null, chargedUsd: 0 }), forwarding(2, 5), settled(2, 6, { status: 429, usage: null, chargedUsd: 0 }), ...ok2xx(3, 12)] }));
  assert.equal(a.class, 'completed');
  assert.ok(a.flags.includes('upstream 429 2'), a.flags.join());
  // (b) the session ended on the account's limit
  const end = [envelope({ is_error: true, api_error_status: 429, result: 'You have hit your weekly limit' })];
  const b = bareOutcome(base({ events: end, brokerLines: [...ok2xx(1, 0), forwarding(2, 9), settled(2, 10, { status: 429, usage: null, chargedUsd: 0 })] }));
  assert.equal(b.class, 'void: rate limit');
  assert.equal(b.data, false);
  assert.ok(!b.flags.some(f => f.startsWith('upstream 429')), 'the 429 that ENDED the session is its class, not a flag');
});

test('A_final_5xx_is_an_upstream_void_and_401_403_stop', () => {
  const final = status => bareOutcome(base({ events: [envelope({ is_error: true, api_error_status: status, result: `API Error ${status}` })], brokerLines: [...ok2xx(1, 0), forwarding(2, 9), settled(2, 10, { status, usage: null, chargedUsd: 0 })] }));
  assert.equal(final(529).class, 'void: upstream');
  assert.equal(final(503).class, 'void: upstream');
  assert.equal(final(529).data, false);
  assert.equal(final(401).class, 'stop: auth');
  assert.equal(final(403).class, 'stop: auth');
  assert.equal(final(401).data, false);
  assert.equal(final(401).stop, true, 'a stop halts the series');
});

test('No_2xx_settled_line_means_the_model_never_answered', () => {
  const only4xx = bareOutcome(base({ events: [], brokerLines: [forwarding(1, 0), settled(1, 1, { status: 400, usage: null, chargedUsd: 0 })] }));
  assert.equal(only4xx.class, 'void: harness');
  const noUsage = bareOutcome(base({ events: [], brokerLines: [forwarding(1, 0), settled(1, 1, { status: 200, usage: null, chargedUsd: 0.05 })] }));
  assert.equal(noUsage.class, 'void: harness');
  assert.equal(noUsage.data, false);
});

test('Cancelled_and_disposed_are_interrupted_harness_voids', () => {
  const cancelled = bareOutcome(base({ events: [], meta: meta({ stopped: 'cancelled' }), abortReason: 'SIGINT' }));
  assert.equal(cancelled.class, 'void: harness (interrupted)');
  assert.match(cancelled.detail.reason, /SIGINT/);
  const disposed = bareOutcome(base({ events: [], meta: meta({ stopped: 'disposed' }) }));
  assert.equal(disposed.class, 'void: harness (disposed)');
  assert.equal(bareOutcome(base({ events: [], meta: meta({ stopped: null }), retired: true })).class, 'void: harness (disposed)', 'a session retired by retireStale: nobody recorded why it ended');
});

test('A_readback_stop_is_a_harness_void', () => {
  assert.equal(classOf({ meta: meta({ stopped: 'readback' }) }), 'void: harness');
});

test('A_watchdog_kill_is_a_data_point_whatever_sessionOutcome_says', () => {
  const wall = bareOutcome(base({ events: [], meta: meta({ stopped: 'wall', outcome: { ok: false, label: 'void: stopped' }, broker: { spentUsd: 2.5, overCap: false }, envelopeCostUsd: null }), recomputeUsd: 2.6 }));
  assert.equal(wall.class, 'killed: wall');
  assert.equal(wall.data, true);
  assert.equal(wall.censoredAtUsd, 2.5, 'no envelope: B');
  assert.equal(classOf({ events: [], meta: meta({ stopped: 'stall', envelopeCostUsd: null }) }), 'killed: stall');
});

test('Precedence_holds_on_overlaps', () => {
  const capped = { meta: meta({ stopped: 'cost' }), events: [] };
  assert.equal(classOf({ ...capped, contamination: { high: [{ rule: 'web' }], canary: [], tokenHits: [] } }), 'void: contaminated', 'contaminated beats capped');
  assert.equal(classOf({ meta: meta({ stopped: 'readback' }), abortReason: 'SIGINT' }), 'void: harness', 'readback beats cancelled');
  const both = bareOutcome(base({ events: [envelope({ is_error: true, api_error_status: 529 })], brokerLines: [...ok2xx(1, 0), forwarding(2, 9), settled(2, 10, { status: 401, usage: null, chargedUsd: 0 })] }));
  assert.equal(both.class, 'stop: auth', 'auth beats upstream');
  assert.equal(classOf({ aliasCheck: { verdict: 'stop', reasons: ['alias drift'] }, scan: { hits: 1 } }), 'stop: alias drift', 'alias drift is row 1');
  assert.equal(classOf({ scan: { hits: 1 } }), 'stop: security');
  assert.equal(classOf({ meta: meta({ samplers: { realInCmdline: 0, realInEnviron: 2 } }) }), 'stop: security', 'a sampler hit counts as well');
});

test('Contamination_has_three_sources_and_a_hand_verdict_reclassifies_to_a_harness_void_only', () => {
  assert.equal(classOf({ contamination: { high: [], canary: [{ file: 'x' }], tokenHits: [] } }), 'void: contaminated');
  assert.equal(classOf({ contamination: { high: [], canary: [], tokenHits: [{ kind: 'github' }] } }), 'void: contaminated');
  const reclass = bareOutcome(base({ handVerdicts: [{ g: 'G-1', verdict: 'refuted', evidence: 'x' }] }));
  assert.equal(reclass.class, 'void: harness');
  assert.match(reclass.detail.reason, /G-1/);
  assert.equal(classOf({ handVerdicts: [{ g: 'G-1', verdict: 'holds' }] }), 'completed', 'only a refuted verdict reclassifies');
});

test('A_session_the_model_answered_that_ends_in_an_error_envelope_is_crashed_with_its_subtype', () => {
  const r = bareOutcome(base({ events: [envelope({ subtype: 'error_during_execution', is_error: true })] }));
  assert.equal(r.class, 'crashed: error_during_execution');
  assert.equal(r.data, true);
});

test('The_fallback_flag_is_a_flag_never_a_class', () => {
  const r = bareOutcome(base({ aliasCheck: { verdict: 'flag', reasons: ['model_refusal_fallback: a -> b (x)'] } }));
  assert.equal(r.class, 'completed');
  assert.ok(r.flags.includes('model_refusal_fallback'));
});

test('A_final_request_answered_2xx_that_then_broke_off_with_a_transport_error_is_an_upstream_void', () => {
  // the broker's line for a mid-stream reset: the upstream answered 200, then the connection failed (`error` set); the CLI ends in an error envelope with no status of its own
  const broke = (error, over = {}) => bareOutcome(base({
    events: [envelope({ subtype: 'error_during_execution', is_error: true, result: 'the stream ended early' })],
    brokerLines: [...ok2xx(1, 0), forwarding(2, 9), settled(2, 40, { status: 200, error, usage: null, chargedUsd: 0, ...over })],
  }));
  for (const error of ['ECONNRESET', 'aborted', 'ETIMEDOUT']) {
    const r = broke(error);
    assert.equal(r.class, 'void: upstream', `status 200 and error ${error}: the provider, not the stack, ended the session`);
    assert.equal(r.data, false);
    assert.equal(r.detail.error, error);
  }
  assert.equal(bareOutcome(base({ events: [envelope({ subtype: 'error_during_execution', is_error: true })], brokerLines: [...ok2xx(1, 0), forwarding(2, 9), settled(2, 40, { status: 200, usage: null, chargedUsd: 0 })] })).class, 'crashed: error_during_execution',
    'control: the same ending with no transport error stays a crash (data), as before');
  assert.equal(bareOutcome(base({ brokerLines: [...ok2xx(1, 0), forwarding(2, 9), settled(2, 40, { status: 200, error: 'ECONNRESET' })] })).class, 'completed', 'control: a session that ended in success is never voided by a reset on its last request');
});

test('The_broker_refused_flag_counts_cap_refusals_only_as_the_contract_says', () => {
  const flags = lines => bareOutcome(base({ brokerLines: lines })).flags.filter(f => f.startsWith('broker refused'));
  assert.deepEqual(flags([refused(1, 0, 'cap'), ...ok2xx(2, 4)]), ['broker refused 1'], 'control: a cap refusal the session survived');
  for (const reason of ['path', 'body', 'busy']) assert.deepEqual(flags([refused(1, 0, reason), ...ok2xx(2, 4)]), [], `a survived ${reason} refusal is the harness's door, not a budget refusal: no flag`);
  const noReason = { t: at(0), seq: 1, event: 'refused' };
  assert.deepEqual(flags([noReason, ...ok2xx(2, 4)]), [], 'a refusal with no reason is unknown and is never a cap');
  const mixed = bareOutcome(base({ brokerLines: [refused(1, 0, 'cap'), refused(2, 5, 'body'), refused(3, 8, 'cap'), ...ok2xx(4, 12)] }));
  assert.ok(mixed.flags.includes('broker refused 2'), mixed.flags.join());
  assert.equal(mixed.detail.brokerRefused.count, 2);
  assert.equal(mixed.detail.brokerRefused.backoffMs, 16000, 'each cap refusal to the next ADMITTED request: 12 s + 4 s');
});

test('A_contaminated_void_keeps_the_high_hits_themselves_not_only_their_count', () => {
  const high = [{ severity: 'high', rule: 'outside-path', source: 'call', where: 'main:Bash', excerpt: '/srv/partner/box/work' }];
  const r = bareOutcome(base({ contamination: { high, canary: [], tokenHits: [] } }));
  assert.equal(r.class, 'void: contaminated');
  assert.equal(r.detail.high, 1);
  assert.deepEqual(r.detail.contamination, { high });
});
