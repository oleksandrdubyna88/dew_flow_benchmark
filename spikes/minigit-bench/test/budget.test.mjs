import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { BARE_RESERVE_USD, OVER_CAP_FACTOR, admit, capStatus, openState, reservationFor, reserve, settle } from '../lib/budget.mjs';
import { loadPrices } from '../lib/prices.mjs';
import { spikeDir } from './helpers.mjs';

const table = loadPrices(JSON.parse(readFileSync(path.join(spikeDir, 'inputs', 'prices-2026-10-29.json'), 'utf8'))).value;
// the usage the broker logged on 2026-10-29 for the stage 2b run: 2 input, 2,297 cache-write-1h, 4 output tokens on claude-sonnet-5-5
const USAGE = { input_tokens: 2, cache_creation_input_tokens: 2297, cache_read_input_tokens: 0, cache_creation: { ephemeral_5m_input_tokens: 0, ephemeral_1h_input_tokens: 2297 }, output_tokens: 4 };

test('Admission_reserves_per_in_flight_request_and_refuses_at_the_cap', () => {
  assert.equal(BARE_RESERVE_USD, 0.05);
  const rows = [
    [{ spentUsd: 0, reservedUsd: 0, reserveUsd: 0.05, capUsd: 0.2 }, true],
    [{ spentUsd: 0.16, reservedUsd: 0, reserveUsd: 0.05, capUsd: 0.2 }, false],
    [{ spentUsd: 0.1, reservedUsd: 0.1, reserveUsd: 0.05, capUsd: 0.2 }, false],
    [{ spentUsd: 0, reservedUsd: 0.2, reserveUsd: 0.05, capUsd: 0.2 }, false],
    [{ spentUsd: 0.2, reservedUsd: 0, reserveUsd: 0.05, capUsd: 0.2 }, false],
  ];
  for (const [input, expected] of rows) {
    const r = admit(input);
    assert.equal(r.admit, expected, JSON.stringify(input));
    if (!expected) assert.match(r.why, /cap/);
  }
  assert.equal(admit({ spentUsd: 0.1, reservedUsd: 0.05, reserveUsd: 0.05, capUsd: 0.2 }).admit, true, 'landing exactly on the cap is admitted');
  assert.equal(admit({ spentUsd: 0, reservedUsd: 0, reserveUsd: 0.05, capUsd: 0 }).admit, false, 'a zero budget admits nothing (P-6)');
});

test('A_settlement_prices_the_parsed_usage_at_the_table_the_cli_bills', () => {
  const state = reserve(openState(), 0.05);
  assert.deepEqual([state.reservedUsd, state.inFlight], [0.05, 1]);
  const r = settle({ state, entry: { reserveUsd: 0.05 }, usage: USAGE, model: 'claude-sonnet-5-5', table, cliVersion: '2.1.310' });
  assert.equal(r.line.chargedUsd, 0.009232);
  assert.equal(r.line.priced.known, true);
  assert.equal(r.state.spentUsd, 0.009232);
  assert.deepEqual([r.state.reservedUsd, r.state.inFlight], [0, 0], 'the reservation is released');
  assert.deepEqual(state, { spentUsd: 0, reservedUsd: 0.05, inFlight: 1, largestChargeUsd: 0 }, 'the argument state is untouched');
});

test('Nothing_parsed_or_priced_is_charged_the_reservation_never_zero', () => {
  const state = reserve(openState(), 0.05);
  const cases = [
    { usage: null, model: null, why: /no usage/ },
    { usage: USAGE, model: 'claude-unknown-9', why: /no price/ },
    { usage: { input_tokens: 'x' }, model: 'claude-sonnet-5-5', why: /./ },
  ];
  for (const c of cases) {
    const r = settle({ state, entry: { reserveUsd: 0.05 }, usage: c.usage, model: c.model, table, cliVersion: '2.1.310' });
    assert.equal(r.line.chargedUsd, 0.05, 'the reservation stands as the charge');
    assert.equal(r.line.priced.known, false);
    assert.match(r.line.priced.why, c.why);
  }
});

test('A_waived_settlement_charges_nothing_releases_the_reservation_and_says_why', () => {
  const state = reserve(openState(), 0.05);
  const r = settle({ state, entry: { reserveUsd: 0.05 }, usage: null, model: null, table, cliVersion: '2.1.310', waive: 'upstream error' });
  assert.deepEqual(r.line.priced, { known: true, usd: 0, why: 'upstream error' });
  assert.equal(r.line.chargedUsd, 0);
  assert.deepEqual(r.state, { spentUsd: 0, reservedUsd: 0, inFlight: 0, largestChargeUsd: 0 });
  const unwaived = settle({ state, entry: { reserveUsd: 0.05 }, usage: null, model: null, table, cliVersion: '2.1.310' });
  assert.equal(unwaived.line.chargedUsd, 0.05, 'control: the same settlement without a waiver still charges the reservation');
});

test('A_reservation_is_the_floor_or_the_largest_charge_settled_so_far_in_the_cell_and_never_sized_from_max_tokens', () => {
  // the refuted approach: max_tokens x the output price. The CLI sends 32k-128k, so one Opus request would reserve $1-3, admission would refuse
  // under a $6 cap and the CLI would back off and retry: the stacks' wall time would change. The body is never read.
  assert.equal(reservationFor(openState(), 0.05), 0.05, 'a fresh cell reserves the floor');
  let state = openState();
  const spend = outputTokens => settle({ state, entry: { reserveUsd: 0.05 }, usage: { input_tokens: 0, output_tokens: outputTokens, cache_creation: { ephemeral_5m_input_tokens: 0, ephemeral_1h_input_tokens: 0 } }, model: 'claude-sonnet-5-5', table, cliVersion: '2.1.310' });
  state = reserve(state, 0.05);
  state = spend(500).state; // 0.005
  assert.equal(state.largestChargeUsd, 0.005);
  assert.equal(reservationFor(state, 0.05), 0.05, 'a charge under the floor leaves the floor');
  state = spend(7000).state; // 0.07 (a second settlement: the reservation arithmetic is not the point here, the largest charge is)
  assert.equal(state.largestChargeUsd, 0.07);
  assert.equal(reservationFor(state, 0.05), 0.07, 'the next request reserves what the dearest one cost');
  state = spend(1000).state;
  assert.equal(state.largestChargeUsd, 0.07, 'a cheaper settlement does not lower it');
});

test('An_unsplit_cache_write_is_charged_at_the_upper_bound', () => {
  const usage = { input_tokens: 2, cache_creation_input_tokens: 1000, output_tokens: 4 };
  const r = settle({ state: reserve(openState(), 0.05), entry: { reserveUsd: 0.05 }, usage, model: 'claude-sonnet-5-5', table, cliVersion: '2.1.310' });
  assert.equal(r.line.priced.known, true);
  assert.ok(r.line.priced.range.high >= r.line.priced.range.low);
  assert.equal(r.line.chargedUsd, r.line.priced.range.high, 'the high end: a cap is never undercounted');
});

test('The_cap_is_soft_and_a_session_over_1_1_times_it_is_flagged_with_the_exact_overshoot', () => {
  assert.equal(OVER_CAP_FACTOR, 1.1);
  // four concurrent requests: each is admitted (4 x 0.05 = 0.2 is not over the cap) and each settles above its reservation
  let state = openState();
  for (let i = 0; i < 4; i += 1) {
    assert.equal(admit({ ...state, reserveUsd: 0.05, capUsd: 0.2 }).admit, true, `request ${i + 1} is admitted`);
    state = reserve(state, 0.05);
  }
  assert.equal(admit({ ...state, reserveUsd: 0.05, capUsd: 0.2 }).admit, false, 'a fifth is refused');
  const spendMore = { input_tokens: 0, output_tokens: 7000, cache_creation: { ephemeral_5m_input_tokens: 0, ephemeral_1h_input_tokens: 0 } }; // 7000 x $10/M = $0.07
  for (let i = 0; i < 4; i += 1) state = settle({ state, entry: { reserveUsd: 0.05 }, usage: spendMore, model: 'claude-sonnet-5-5', table, cliVersion: '2.1.310' }).state;
  assert.deepEqual(capStatus({ spentUsd: state.spentUsd, capUsd: 0.2 }), { overCap: true, overshootUsd: 0.08, withinCap: false });
  assert.deepEqual(capStatus({ spentUsd: 0.21, capUsd: 0.2 }), { overCap: false, overshootUsd: 0.01, withinCap: false }, 'between 1.0 and 1.1 x the cap: over, but inside the series gate');
  assert.deepEqual(capStatus({ spentUsd: 0.2, capUsd: 0.2 }), { overCap: false, overshootUsd: 0, withinCap: true });
});
