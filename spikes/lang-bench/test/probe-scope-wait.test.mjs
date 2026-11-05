// The wait of the marker test's check (§14 A1 step 0): a SIGKILL returns before its target has left `/proc`, so the check that the kill worked waits, with a bound, for the pids to go. The scan is the seam: no process is started here.
import assert from 'node:assert/strict';
import { test } from 'node:test';
import { awaitNoPids } from './support/probe-scope.mjs';

/** A scan that lists `pids` for its first `polls` calls and none after; `calls()` is how many times it was asked. */
const emptiesAfter = (polls, pids) => {
  let asked = 0;
  return { scan: () => (++asked <= polls ? pids : []), calls: () => asked };
};

test('A_pid_that_is_still_listed_for_the_first_polls_and_then_gone_passes_after_waiting_for_it', async () => {
  const { scan, calls } = emptiesAfter(3, ['4242']);
  await awaitNoPids(scan, { boundMs: 2000, stepMs: 5 });
  assert.equal(calls(), 4, 'it asked again after each listing, and stopped at the first empty one');
});

test('A_scan_that_is_empty_at_once_is_asked_once_and_passes', async () => {
  const { scan, calls } = emptiesAfter(0, []);
  await awaitNoPids(scan);
  assert.equal(calls(), 1);
});

test('A_scan_that_never_empties_fails_at_the_bound_in_bounded_time_and_names_the_pid_and_what_it_was', async () => {
  const started = Date.now();
  await assert.rejects(awaitNoPids(() => ['4242', '4243'], { boundMs: 150, stepMs: 10, what: 'the hanging child' }), /the hanging child is still there 150 ms after its kill: pid 4242, 4243/);
  const took = Date.now() - started;
  assert.ok(took >= 140 && took < 2000, `it waited for the bound and no longer: ${took} ms`);
});
