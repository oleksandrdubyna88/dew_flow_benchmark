// The spend gate (E3b.S1 T-G1): two limits, both through spendDecision; the stop-rule figure; what an interrupted session cost.
import assert from 'node:assert/strict';
import { test } from 'node:test';
import { reservationOf, sessionSpendUsd, spendGate, spendTotals, stopRuleFigure } from '../lib/spend-gate.mjs';

const near = (a, b, why) => assert.ok(Math.abs(a - b) < 1e-9, `${why ?? ''} ${a} is not ${b}`);
const ledger = [{ kind: 'prior', usd: 0.43 }, { kind: 'data', usd: 30 }];

test('The_spend_gate_applies_both_limits_and_prior_spend_only_to_the_ceiling', () => {
  const g = spendGate({ ledger, liveUsd: 0, caps: [6, 6], ceilingUsd: 500, bareBudgetUsd: 40 });
  assert.equal(g.stop, true);
  assert.deepEqual(g.stoppedBy, ['the bare budget']);
  assert.match(g.lines.join('\n'), /the bare budget/);
  near(g.ceiling.totalUsd, 0.43 + 30 + 13.2, 'the ceiling line counts the prior spend');
  near(g.bare.totalUsd, 30 + 13.2, 'the bare line does not');
});

test('The_spend_gate_goes_when_both_limits_hold_and_adds_the_live_sum', () => {
  const g = spendGate({ ledger: [{ kind: 'prior', usd: 0.43 }, { kind: 'aa', usd: 3 }], liveUsd: 2, caps: [6, 6], ceilingUsd: 500, bareBudgetUsd: 40 });
  assert.equal(g.stop, false);
  near(g.bare.totalUsd, 3 + 2 + 13.2);
  assert.deepEqual(g.stoppedBy, []);
});

test('The_spend_gate_names_the_ceiling_when_the_ceiling_is_what_stops', () => {
  const g = spendGate({ ledger: [{ kind: 'prior', usd: 495 }], liveUsd: 0, caps: [6], ceilingUsd: 500, bareBudgetUsd: 40 });
  assert.deepEqual(g.stoppedBy, ['the ceiling']);
});

test('The_spend_gate_refuses_a_ledger_line_of_an_unknown_kind_naming_the_legal_ones', () => {
  assert.throws(() => spendGate({ ledger: [{ kind: 'mystery', usd: 1 }], liveUsd: 0, caps: [6], ceilingUsd: 500, bareBudgetUsd: 40 }), /prior, rehearsal, aa, pilot, data, void/);
});

test('The_stop_rule_figure_is_the_largest_basis', () => {
  assert.equal(stopRuleFigure({ envelopeUsd: 1.0, recomputeUsd: 1.02, brokerUsd: 1.01 }), 1.02);
  assert.equal(stopRuleFigure({ envelopeUsd: null, recomputeUsd: null, brokerUsd: 2.1 }), 2.1, 'a session without an envelope is B');
  assert.equal(stopRuleFigure({ envelopeUsd: 1.0, recomputeUsd: null, brokerUsd: 1.01 }), 1.01, 'a recompute that could not be made is skipped, never zero');
});

const settled = (seq, usd) => ({ event: 'settled', seq, chargedUsd: usd, status: 200 });
const forwarding = seq => ({ event: 'forwarding', seq });

test('An_interrupted_session_is_charged_its_unsettled_reservations', () => {
  const lines = [forwarding(1), settled(1, 0.2), forwarding(2), settled(2, 0.3), forwarding(3), settled(3, 0.1), forwarding(4), forwarding(5)];
  assert.equal(reservationOf(lines), 0.3, 'the largest settled charge seen is above the $0.05 floor');
  near(sessionSpendUsd(lines), 0.6 + 2 * 0.3);
  assert.equal(reservationOf([forwarding(1)]), 0.05, 'nothing settled: the floor');
  near(sessionSpendUsd([forwarding(1), settled(1, 0.01), forwarding(2)]), 0.01 + 0.05);
});

test('An_event_line_of_the_ledger_carries_no_spend', () => {
  const lines = [{ kind: 'prior', usd: 0.43 }, { kind: 'aa', usd: 3 }];
  const withEvent = [...lines, { event: 'accept-harness', to: 'a'.repeat(64) }, { event: 'after-stop', rule: 'void: rate limit' }];
  assert.deepEqual(spendTotals(withEvent), spendTotals(lines), 'a harness acceptance or an --after-stop is recorded in the ledger and never summed');
  assert.throws(() => spendTotals([{ kind: 'bogus', usd: 1 }]), /legal kinds/, 'a spend line of an unknown kind still stops the series');
});
