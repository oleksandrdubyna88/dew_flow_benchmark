// How a bare session ENDED (E3b.S1 step 7): `bareOutcome` over records, first match wins. Fixtures are the shapes the launcher and the broker write.
import assert from 'node:assert/strict';
import { test } from 'node:test';
import { rateLimitSignal } from '../../policy-ab-quick/lib/ratelimit.mjs';
import { bareOutcome } from '../lib/bare-outcome.mjs';

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
  const events = [envelope({ is_error: true, api_error_status: 404, result: 'not found' })];
  const r = bareOutcome(base({ events, brokerLines: [...ok2xx(1, 0), refused(2, 60, 'path')] }));
  assert.notEqual(r.class, 'killed: cap');
  assert.equal(r.class, 'crashed: success', 'an error envelope the other rows do not name');
  for (const reason of ['unknown', 'headers', undefined]) {
    assert.notEqual(bareOutcome(base({ events, brokerLines: [...ok2xx(1, 0), { ...refused(2, 60), reason }] })).class, 'killed: cap', `reason ${reason}`);
  }
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
