// The barrier and the void / re-run table of a bare pair (E3b.S1 step 8; parent 11.1: a void is re-run ONCE, unpaired unless the partner voided too).
import assert from 'node:assert/strict';
import { test } from 'node:test';
import { nextStep, pairSummary } from '../lib/bare-schedule.mjs';

const CLASS = { ok: { class: 'completed', data: true }, void: { class: 'void: rate limit', data: false }, stop: { class: 'stop: auth', data: false } };
const key = (stack, phase, attempt) => `${stack}:${phase}:a${attempt}`;
const name = s => `${s.stack}:${s.phase}:a${s.attempt}`;

/** Drive the schedule to its end: every launched session ends as `outcomes[key]` (default 'ok'); returns the steps taken as readable lines. */
function drive(outcomes = {}, start = []) {
  let state = { pair: 1, sessions: start };
  const trace = [];
  for (let guard = 0; guard < 40; guard += 1) {
    const step = nextStep(state);
    if (step.action === 'done' || step.action === 'halt') return { trace, step, state };
    trace.push(`${step.action} ${step.sessions.map(name).join('+')}${step.label ? ` [${step.label}]` : ''}`);
    if (step.action === 'launch') {
      state = { ...state, sessions: [...state.sessions, ...step.sessions.map(s => ({ ...s, label: step.label, status: 'ran', ...CLASS[outcomes[key(s.stack, s.phase, s.attempt)] ?? 'ok'] }))] };
    } else {
      const names = new Set(step.sessions.map(name));
      state = { ...state, sessions: state.sessions.map(s => (names.has(name(s)) ? { ...s, status: 'scored' } : s)) };
    }
  }
  throw new Error(`the schedule did not end: ${trace.join(' | ')}`);
}

test('The_schedule_follows_the_void_table', () => {
  assert.deepEqual(drive().trace, [
    'launch csharp:v1:a1+node:v1:a1 [paired]', 'score csharp:v1:a1+node:v1:a1', 'launch csharp:v2:a1+node:v2:a1 [paired]', 'score csharp:v2:a1+node:v2:a1',
  ], 'both data: the v2 pair');

  assert.deepEqual(drive({ 'csharp:v1:a1': 'void', 'node:v1:a1': 'void' }).trace, [
    'launch csharp:v1:a1+node:v1:a1 [paired]', 'launch csharp:v1:a2+node:v1:a2 [paired]', 'score csharp:v1:a2+node:v1:a2',
    'launch csharp:v2:a1+node:v2:a1 [paired]', 'score csharp:v2:a1+node:v2:a1',
  ], 'both v1 void: the v1 re-run pair, paired, then the v2 pair');

  assert.deepEqual(drive({ 'csharp:v1:a1': 'void' }).trace, [
    'launch csharp:v1:a1+node:v1:a1 [paired]', 'score node:v1:a1',
    'launch node:v2:a1 [ran unpaired]', 'score node:v2:a1',
    'launch csharp:v1:a2 [ran unpaired]', 'score csharp:v1:a2',
    'launch csharp:v2:a1 [ran unpaired]', 'score csharp:v2:a1',
  ], 'one v1 void: the partner v2 alone, then the re-run alone, then its v2 alone');

  const missing = drive({ 'csharp:v1:a1': 'void', 'csharp:v1:a2': 'void' });
  assert.equal(missing.trace.some(t => t.startsWith('launch csharp:v2')), false, 'a void a2: that cell is missing and its v2 never runs');
  assert.deepEqual(pairSummary(missing.state).missing, [{ stack: 'csharp', phase: 'v1', why: 'a second void' }, { stack: 'csharp', phase: 'v2', why: 'its v1 is missing' }]);

  assert.deepEqual(drive({ 'node:v2:a1': 'void' }).trace.slice(4), ['launch node:v2:a2 [ran unpaired]', 'score node:v2:a2'], 'one v2 void: that v2 a2 alone, from the recorded v1 tree');
  assert.deepEqual(drive({ 'csharp:v2:a1': 'void', 'node:v2:a1': 'void' }).trace.slice(3), ['launch csharp:v2:a2+node:v2:a2 [paired]', 'score csharp:v2:a2+node:v2:a2'], 'both v2 void: the v2 re-run pair');
});

test('A_second_void_leaves_the_cell_missing_and_is_never_re_run_again', () => {
  const r = drive({ 'node:v2:a1': 'void', 'node:v2:a2': 'void' });
  assert.equal(r.step.action, 'done');
  assert.ok(!r.trace.some(t => t.includes(':a3')));
  assert.deepEqual(pairSummary(r.state).missing, [{ stack: 'node', phase: 'v2', why: 'a second void' }]);
});

test('The_barrier_holds_until_both_are_scored', () => {
  const v1 = status => [
    { stack: 'csharp', phase: 'v1', attempt: 1, status: 'scored', ...CLASS.ok },
    { stack: 'node', phase: 'v1', attempt: 1, status, ...CLASS.ok },
  ];
  const held = nextStep({ pair: 1, sessions: v1('ran') });
  assert.equal(held.action, 'score');
  assert.deepEqual(held.sessions.map(name), ['node:v1:a1'], 'only the unscored one');
  assert.equal(nextStep({ pair: 1, sessions: v1('ran') }).sessions.some(s => s.phase === 'v2'), false);
  assert.deepEqual(nextStep({ pair: 1, sessions: v1('scored') }).sessions.map(name), ['csharp:v2:a1', 'node:v2:a1']);
});

test('A_void_session_is_not_scored_and_a_session_still_launching_is_waited_for', () => {
  const sessions = [{ stack: 'csharp', phase: 'v1', attempt: 1, status: 'launching' }, { stack: 'node', phase: 'v1', attempt: 1, status: 'ran', ...CLASS.ok }];
  const s = nextStep({ pair: 1, sessions });
  assert.equal(s.action, 'launch', 'the v1 phase is not complete while one session is launching: resume reconciles it first');
  const voided = drive({ 'node:v1:a1': 'void' });
  assert.ok(!voided.trace.some(t => t === 'score node:v1:a1'), 'a void has no data to score');
});

test('A_stop_class_halts_the_pair_and_the_session_is_kept', () => {
  const sessions = [{ stack: 'csharp', phase: 'v1', attempt: 1, status: 'ran', ...CLASS.stop }, { stack: 'node', phase: 'v1', attempt: 1, status: 'ran', ...CLASS.ok }];
  const s = nextStep({ pair: 1, sessions });
  assert.equal(s.action, 'halt');
  assert.match(s.reason, /stop: auth/);
});

test('The_summary_counts_unpaired_cells_for_the_with_and_without_ratios', () => {
  const r = drive({ 'csharp:v1:a1': 'void' });
  const sum = pairSummary(r.state);
  assert.deepEqual(sum.unpairedStacks.sort(), ['csharp', 'node'], 'both cells hold a ran-unpaired session');
  assert.deepEqual(sum.missing, []);
  assert.deepEqual(pairSummary(drive().state).unpairedStacks, []);
});
