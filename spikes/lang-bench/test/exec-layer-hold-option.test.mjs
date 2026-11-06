// The scope holder's bound (lib/exec-layer/prefix.mjs) as a pure function of the run: the default is T + G + the margin, as `exec-layer-hold.test.mjs` pins it with a real run; a caller whose cases may last longer (a daemon
// or web case lasts up to the runner's test bound) gives `holdSeconds`, and T and G stay what they are. No run is opened and nothing starts.
import assert from 'node:assert/strict';
import { test } from 'node:test';
import { HOLD_MARGIN_S, holdForCaseWall, holdSeconds } from '../lib/exec-layer/prefix.mjs';

const run = (limits, options = {}) => ({ limits, options });

test('Without_the_option_the_hold_is_T_plus_G_plus_the_margin_as_it_always_was', () => {
  assert.equal(holdSeconds(run({})), 10 + 5 + HOLD_MARGIN_S);
  assert.equal(holdSeconds(run({ timeS: 30, graceS: 5 })), 35 + HOLD_MARGIN_S);
  assert.equal(holdSeconds(run({ timeS: 1.2, graceS: 5 })), 7 + HOLD_MARGIN_S, 'rounded up');
});

test('A_caller_that_gives_the_hold_gets_exactly_that_and_the_runners_limits_are_not_read_for_it', () => {
  assert.equal(holdSeconds(run({ timeS: 30, graceS: 5 }, { holdSeconds: 720 })), 720);
});

test('The_hold_for_a_case_wall_is_the_wall_plus_the_margin', () => {
  assert.equal(holdForCaseWall(660_000), 660 + HOLD_MARGIN_S);
  assert.equal(holdForCaseWall(600_001), 601 + HOLD_MARGIN_S);
});
