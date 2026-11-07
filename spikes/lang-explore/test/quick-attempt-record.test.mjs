// The complete validator for the runner's record of one attempt (section 2.9): a record that is malformed, truncated or partial is an ENVIRONMENT attempt, never a functional failure, even when the results it does hold
// carry a non-pass verdict. Pure; the records are invented in the runner's real shape.
import assert from 'node:assert/strict';
import { test } from 'node:test';
import { COMPLETION_KEYS, REQUIRED_CASE_FIELDS, attemptRecordProblem } from '../lib/attempt-record.mjs';
import { runnerRecordPinNow } from '../lib/harness-pin.mjs';
import { verdictOf } from '../lib/verdict.mjs';
import { rec } from './quick-score-fixture.mjs';
import { BUILD_OK, cleanPass, passOf, passesOf } from './quick-passes-fixture.mjs';

const pinned = runnerRecordPinNow();
const EXPECTED = ['h001', 'h002', 's01'];
const recordOf = (cases = EXPECTED.map(id => rec(id)), over = {}) => ({ format: pinned.format, runner: pinned.version, grammar: 2, kind: 'daemon', build: { ok: true }, launch: { failed: false, stderr: '' }, cases, ...over });
const problem = (record, expected = EXPECTED) => attemptRecordProblem({ text: typeof record === 'string' ? record : JSON.stringify(record), expected, pinnedRunner: pinned });

test('A_truncated_or_unparseable_record_is_an_environment_attempt_never_a_functional_failure', () => {
  const whole = JSON.stringify(recordOf());
  for (const text of [whole.slice(0, Math.floor(whole.length / 2)), '{}', '', 'not json at all', 'null']) assert.match(problem(text), /^record-invalid: /, text.slice(0, 20));
});

test('A_record_without_the_runners_completion_marker_is_an_environment_attempt_even_when_every_case_it_holds_is_present', () => {
  for (const key of COMPLETION_KEYS.filter(k => k !== 'cases')) {
    const { [key]: _gone, ...without } = recordOf();
    assert.match(problem(without), new RegExp(`record-invalid: .*${key}`), key);
  }
  assert.match(problem(recordOf(undefined, { runner: `${pinned.version}-other` })), /runner/);
  assert.match(problem(recordOf(undefined, { format: pinned.format + 1 })), /format/);
  assert.equal(problem(recordOf()), null, 'a record with the marker and every case is complete');
});

test('A_partial_record_holding_a_non_pass_verdict_is_an_environment_attempt_and_the_verdict_in_it_is_not_counted', () => {
  const partial = recordOf([rec('s01', 'FAIL', 'invented difference'), rec('h002')]);
  assert.match(problem(partial), /record-invalid: .*h001/);
  const pending = verdictOf({ build: BUILD_OK, passes: passesOf({ 10: passOf(10, [{ attempt: 1, kind: 'environment', reason: problem(partial) }]) }) });
  assert.equal(pending.verdict, 'pending');
  assert.notEqual(pending.verdict, 'rejected');
  assert.equal(cleanPass(1).attempts[0].kind, 'completed');
});

test('A_duplicate_id_or_an_unknown_id_is_an_environment_attempt', () => {
  assert.match(problem(recordOf([...EXPECTED.map(id => rec(id)), rec('h001')])), /h001.*duplicate/);
  assert.match(problem(recordOf([...EXPECTED.map(id => rec(id)), rec('h777')])), /h777.*unexpected/);
});

test('A_result_missing_a_required_field_is_an_environment_attempt_and_the_required_list_is_one_exported_constant', () => {
  assert.equal(problem(recordOf()), null, 'the positive case: a result with every field passes');
  for (const field of REQUIRED_CASE_FIELDS) {
    const cases = EXPECTED.map(id => structuredClone(rec(id)));
    const keys = field.split('.');
    const last = keys.pop();
    const name = field === 'id' ? 'cases\\[0\\]' : cases[0].id; // a result without its id can only be named by its place
    delete keys.reduce((o, k) => o[k], cases[0])[last];
    assert.match(problem(recordOf(cases)), new RegExp(`record-invalid: .*${name}.*${field.replaceAll('.', '\\.')}`), field);
  }
});
