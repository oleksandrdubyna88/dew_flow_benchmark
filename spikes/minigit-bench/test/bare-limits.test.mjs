// The registered limits and the one permitted raise of the $40 (analysis contract section 9, item 3; E3b review m2 and N14): n is the re-runs still left for the pair, and the line
// is dated before those re-runs start.
import './fake-home.mjs'; // first: the runner reads os.homedir(); a fake HOME keeps every test off the operator's files (N18)
import assert from 'node:assert/strict';
import { test } from 'node:test';
import { effectiveBudget } from '../lib/bare-limits.mjs';
import { BARE_CAPS } from '../lib/bare-plan.mjs';

const entry = (rid, stack, phase, attempt, over = {}) => ({ rid, pair: 1, stack, phase, attempt, kind: 'data', status: 'ran', data: true, startedAt: '2026-11-04T09:00:00.000Z', ...over });
const sessionsOf = (...list) => Object.fromEntries(list.map(s => [s.rid, s]));
const map = (...list) => ({ caps: BARE_CAPS, bareBudgetUsd: 40, ceilingUsd: 500, sessions: sessionsOf(...list) });
const raise = (n, date = '2026-11-05', pair = 1) => `# header\n${date} | budget-raise | pair:${pair} | n:${n} | usd:${(n * 1.1 * 6).toFixed(1)} | the re-runs of pair ${pair}\n`;
const check = (m, text, pair = 1) => effectiveBudget({ map: m, amendmentsText: text, pair });

const TWO_VOIDS = [entry('a1', 'csharp', 'v1', 1, { data: false }), entry('b1', 'node', 'v1', 1, { data: false })];

test('The_raise_names_exactly_the_re_runs_still_left_for_its_pair', () => {
  const both = check(map(...TWO_VOIDS), raise(2));
  assert.equal(both.ok, true, both.reason);
  assert.equal(both.value.bareBudgetUsd, 53.2);
  const tooFew = check(map(...TWO_VOIDS), raise(1));
  assert.equal(tooFew.ok, false);
  assert.match(tooFew.reason, /n:1/);
  assert.match(tooFew.reason, /2 re-run\(s\) are left/);
  const oneVoid = map(TWO_VOIDS[0], entry('b1', 'node', 'v1', 1));
  assert.equal(check(oneVoid, raise(1)).ok, true, 'one void, one re-run');
  const tooMany = check(oneVoid, raise(2));
  assert.equal(tooMany.ok, false, 'a raise for a re-run that does not exist');
  assert.match(tooMany.reason, /1 re-run\(s\) are left/);
  const none = check(map(entry('a1', 'csharp', 'v1', 1), entry('b1', 'node', 'v1', 1)), raise(1));
  assert.equal(none.ok, false);
  assert.match(none.reason, /no re-run is left/);
});

test('A_re_run_that_ran_after_the_line_still_counts_so_a_resume_in_the_middle_is_not_refused_and_one_that_ended_before_it_is_not_left', () => {
  const rerunDone = entry('a2', 'csharp', 'v1', 2, { startedAt: '2026-11-05T14:00:00.000Z' });
  const midway = map(...TWO_VOIDS, rerunDone);
  assert.equal(check(midway, raise(2)).ok, true, 'n was 2 when the line was written: one re-run has run since, one is left');
  assert.equal(check(midway, raise(1)).ok, false);
  const everyRerunDone = map(...TWO_VOIDS, rerunDone, entry('b2', 'node', 'v1', 2, { startedAt: '2026-11-05T15:00:00.000Z' }));
  assert.equal(check(everyRerunDone, raise(2)).ok, true, 'all re-runs ran under the line: the line is still the one that applied');
  const second = map(...TWO_VOIDS, entry('a2', 'csharp', 'v1', 2, { data: false, startedAt: '2026-11-05T14:00:00.000Z' }));
  assert.equal(check(second, raise(2)).ok, true, 'a re-run that was itself a void is done: a second void leaves the cell missing, it is not re-run again');
});

test('The_line_must_be_dated_before_the_re_runs_start', () => {
  const early = entry('a2', 'csharp', 'v1', 2, { startedAt: '2026-11-04T18:00:00.000Z' });
  const late = check(map(...TWO_VOIDS, early), raise(2, '2026-11-05'));
  assert.equal(late.ok, false, 'the re-run started on 2026-11-04 and the line is dated 2026-11-05');
  assert.match(late.reason, /dated 2026-11-05.*before.*re-run.*a2.*2026-11-04/s);
  const sameDay = check(map(...TWO_VOIDS, entry('a2', 'csharp', 'v1', 2, { startedAt: '2026-11-05T08:00:00.000Z' })), raise(2, '2026-11-05'));
  assert.equal(sameDay.ok, true, `a day is the finest date the line has: the same day is not after: ${sameDay.reason}`);
  const planned = check(map(...TWO_VOIDS, entry('a2', 'csharp', 'v1', 2, { status: 'planned', data: undefined, startedAt: undefined })), raise(2, '2026-11-05'));
  assert.equal(planned.ok, true, 'a re-run that is planned and not started is exactly what the line is for');
});

test('The_re_run_count_and_the_date_are_checked_for_the_run_of_the_raised_pair_only', () => {
  const other = { ...map(...TWO_VOIDS), sessions: { ...sessionsOf(...TWO_VOIDS), p2: entry('p2', 'csharp', 'v1', 1, { pair: 2 }) } };
  const forPair2 = check(other, raise(2), 2);
  assert.equal(forPair2.ok, true, forPair2.reason);
  assert.equal(forPair2.value.bareBudgetUsd, 40, 'the raise is not applied to another pair');
  const afterWards = check(map(entry('a1', 'csharp', 'v1', 1), entry('b1', 'node', 'v1', 1)), raise(2), 2);
  assert.equal(afterWards.ok, true, 'a later pair is not refused because the raised pair has nothing left');
  assert.equal(check(map(...TWO_VOIDS), raise(2), null).ok, true, 'the A/A and the pilot have no pair');
});

test('A_pair_whose_sessions_are_all_only_planned_has_not_started_so_a_raise_for_it_is_refused', () => {
  const planned = [entry('a1', 'csharp', 'v1', 1, { status: 'planned', data: undefined }), entry('b1', 'node', 'v1', 1, { status: 'planned', data: undefined })];
  const refused = check(map(...planned), raise(1));
  assert.equal(refused.ok, false);
  assert.match(refused.reason, /has not already started/, 'the loop plans a pair before its gates: a halt there leaves it planned and paid nothing');
  const begun = check(map(...planned, entry('c1', 'csharp', 'v1', 2, { status: 'launching', data: undefined }), entry('d1', 'node', 'v1', 1, { data: false, status: 'ran' })), raise(1));
  assert.equal(begun.ok === true || !/has not already started/.test(begun.reason), true, 'control: a session that left planned is a started pair');
});
