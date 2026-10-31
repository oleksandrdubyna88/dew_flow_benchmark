// spendDecision, widened (E3b.S1): several caps, a live sum of running sessions and a safety factor; a number cap keeps today's behaviour.
import assert from 'node:assert/strict';
import { test } from 'node:test';
import { spendDecision } from '../lib/spend.mjs';

test('Spend_decision_takes_several_caps_a_live_sum_and_a_factor', () => {
  const go = spendDecision(10, 40, [6, 6], { liveUsd: 3, factor: 1.1 });
  assert.equal(go.stop, false);
  assert.equal(go.totalUsd, 26.2, '10 spent + 3 live + 1.1 x (6 + 6)');
  const stop = spendDecision(24, 40, [6, 6], { liveUsd: 3, factor: 1.1 });
  assert.equal(stop.stop, true);
  assert.equal(stop.totalUsd, 40.2);
  assert.match(stop.line, /STOP/);
});

test('Spend_decision_with_a_number_cap_keeps_the_existing_behaviour_and_wording', () => {
  assert.equal(spendDecision(70, 100, 30).stop, false);
  assert.equal(spendDecision(70.01, 100, 30).stop, true);
  assert.match(spendDecision(75, 100, 30).line, /spent \$75 \+ cap \$30 would pass --stop-after \$100/);
  assert.equal(spendDecision(100, 100).stop, false, 'no cap: stops only when the ceiling is passed');
});

test('Spend_decision_names_the_limit_it_was_asked_about', () => {
  const d = spendDecision(39, 40, [6], { label: 'the bare budget' });
  assert.equal(d.stop, true);
  assert.match(d.line, /the bare budget/);
});
