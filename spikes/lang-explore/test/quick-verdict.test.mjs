// The verdict rule of D71 (section 1 of the F1.S2 story): ten passes in, one of four states out. Pure; the passes are invented (`quick-passes-fixture.mjs`).
import assert from 'node:assert/strict';
import { test } from 'node:test';
import { ATTEMPTS_PER_PASS, CASE_VERDICT, VERDICTS, verdictOf } from '../lib/verdict.mjs';
import { BUILD_OK, caseOf, casesWith, cleanPass, completed, environment, exhaustedPass, failingPass, passOf, passesOf } from './quick-passes-fixture.mjs';

const answer = passes => verdictOf({ build: BUILD_OK, passes });
const NINE_FAILING = Object.fromEntries(Array.from({ length: 9 }, (_, i) => [i + 1, failingPass(i + 1)]));

test('Ten_completed_passes_with_every_case_passing_is_accepted', () => {
  const r = answer(passesOf());
  assert.equal(r.verdict, 'accepted');
  assert.equal(r.scoringState, 'decided');
  assert.equal(r.counts.clean, 10);
});

test('A_failed_build_is_rejected_with_no_pass_run_and_every_case_build_failed', () => {
  const r = verdictOf({ build: { ...BUILD_OK, ok: false, exit: 1 }, passes: [] });
  assert.equal(r.verdict, 'rejected');
  assert.equal(r.scoringState, 'decided');
  assert.deepEqual(r.counts, { clean: 0, failing: 0, open: 0, exhausted: 0 });
});

test('One_completed_pass_with_a_functional_failure_and_no_clean_pass_is_rejected', () => {
  const r = answer(passesOf(Object.fromEntries(Array.from({ length: 10 }, (_, i) => [i + 1, failingPass(i + 1, { h002: CASE_VERDICT.wrongAnswer })]))));
  assert.equal(r.verdict, 'rejected');
  assert.equal(r.scoringState, 'decided');
});

test('A_functional_failure_beside_clean_passes_is_intermittently_incorrect_and_never_a_majority_pass', () => {
  const nineClean = answer(passesOf({ 4: failingPass(4) }));
  const oneClean = answer(passesOf({ ...NINE_FAILING, 10: cleanPass(10) }));
  assert.equal(nineClean.verdict, 'intermittently incorrect');
  assert.notEqual(nineClean.verdict, 'accepted');
  assert.equal(oneClean.verdict, 'intermittently incorrect');
});

test('A_functional_failure_in_one_pass_beside_an_environment_failure_in_another_rejects', () => {
  const r = answer([failingPass(1), passOf(2, [environment(1)])]);
  assert.equal(r.verdict, 'rejected');
  assert.notEqual(r.verdict, 'pending');
  assert.notEqual(r.verdict, 'unscored: environment');
});

test('A_functional_failure_stands_when_another_pass_stays_environment_after_its_two_re_runs_the_session_is_rejected_never_unscored_and_never_re_run', () => {
  const some = answer(passesOf({ 3: failingPass(3, { h001: CASE_VERDICT.memoryLimit }), 7: exhaustedPass(7) }));
  assert.equal(some.verdict, 'intermittently incorrect');
  assert.equal(some.scoringState, 'decided');
  const none = answer(passesOf(Object.fromEntries([1, 2, 3, 4, 5, 6, 8, 9, 10].map(n => [n, failingPass(n)]).concat([[7, exhaustedPass(7)]]))));
  assert.equal(none.verdict, 'rejected');
  for (const r of [some, none]) assert.deepEqual(Object.keys(r).filter(k => /final|terminal|reRun/i.test(k)), []);
});

test('An_environment_attempt_re_run_whole_and_then_clean_leaves_the_pass_clean_and_the_session_accepted', () => {
  const r = answer(passesOf({ 4: passOf(4, [environment(1), completed(2)]) }));
  assert.equal(r.verdict, 'accepted');
  assert.equal(r.counts.clean, 10);
});

test('An_environment_failure_that_stays_after_two_re_runs_with_no_functional_failure_anywhere_is_unscored_environment', () => {
  const r = answer(passesOf({ 6: exhaustedPass(6) }));
  assert.equal(r.verdict, 'unscored: environment');
  assert.equal(r.scoringState, 'exhausted');
  assert.equal(r.counts.exhausted, 1);
});

test('The_verdict_is_pending_while_a_pass_without_a_functional_failure_still_has_an_attempt_left', () => {
  const nine = n => passesOf({ 10: passOf(10, Array.from({ length: n }, (_, i) => environment(i + 1))) });
  assert.equal(answer(nine(1)).verdict, 'pending');
  assert.equal(answer(nine(1)).scoringState, 'open');
  assert.equal(answer(nine(ATTEMPTS_PER_PASS - 1)).verdict, 'pending');
  assert.equal(answer(nine(ATTEMPTS_PER_PASS)).verdict, 'unscored: environment');
});

test('Every_non_pass_verdict_of_a_completed_pass_is_a_functional_failure_and_an_environment_attempt_holds_no_case_verdict', () => {
  const nonPass = Object.values(CASE_VERDICT).filter(v => v !== CASE_VERDICT.pass);
  assert.ok(nonPass.length >= 3, 'the list is the exported one and is not empty');
  for (const word of nonPass) assert.equal(answer([passOf(1, [completed(1, [caseOf('s01'), caseOf('h001', word)])])]).verdict, 'rejected', word);
  assert.throws(() => answer([passOf(1, [{ attempt: 1, kind: 'environment', reason: 'x', cases: casesWith() }])]), /environment attempt holds no case verdict/);
  assert.throws(() => answer([passOf(1, [{ attempt: 1, reason: 'x' }])]), /kind/);
  assert.ok(VERDICTS.includes('rejected'));
});

test('The_verdict_is_the_same_whatever_order_the_passes_and_attempts_are_read_in', () => {
  const passes = passesOf({ 3: failingPass(3, { h001: CASE_VERDICT.memoryLimit }), 7: exhaustedPass(7), 4: passOf(4, [environment(1), completed(2)]) });
  const reversed = [...passes].reverse().map(p => ({ ...p, attempts: [...p.attempts].reverse() }));
  assert.equal(JSON.stringify(answer(reversed)), JSON.stringify(answer(passes)));
});
