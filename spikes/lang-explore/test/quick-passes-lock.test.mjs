// One scorer per session (section 2.8): the driver proves the series lock is held (it never takes it) and takes its own `score/scorer.lock`, `wx`, stamped with pid and start time, taken over only when its owner is dead.
import '../../minigit-bench/test/fake-home.mjs';
import assert from 'node:assert/strict';
import { existsSync, mkdirSync, readFileSync, readdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { acquireSeriesLock } from '../../minigit-bench/lib/series-lock.mjs';
import { processStartTime } from '../../minigit-bench/lib/session-state.mjs';
import { tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { lockHeldByMe, seriesLockProblem, takeScorerLock } from '../lib/pass-locks.mjs';
import { scorePasses } from '../lib/passes.mjs';
import { harness } from './quick-passes-harness.mjs';

const stamp = (pid, startTime = '111') => `${JSON.stringify({ pid, startTime, at: '2026-11-07T10:00:00.000Z' })}\n`;
const scoreDir = () => { const work = path.join(tmpDir('quick-locks'), 'score'); mkdirSync(work, { recursive: true }); return work; };

test('The_driver_proves_the_series_lock_is_held_by_this_process_and_a_missing_or_foreign_lock_is_named', () => {
  const dir = tmpDir('quick-series-lock');
  const absent = seriesLockProblem({ seriesDir: dir });
  assert.match(absent, /series\.lock/);
  assert.equal(lockHeldByMe({ seriesDir: dir }), false);
  writeFileSync(path.join(dir, 'series.lock'), stamp(999_999_999, '5'));
  const foreign = seriesLockProblem({ seriesDir: dir });
  assert.match(foreign, /series\.lock/);
  assert.match(foreign, /999999999/, 'the holder is named');
  writeFileSync(path.join(dir, 'series.lock'), stamp(process.pid, 'not-my-start-time'));
  assert.match(seriesLockProblem({ seriesDir: dir }), /not-my-start-time/, 'the same pid with another start time is a reused pid, not this process');
  const mine = tmpDir('quick-series-lock-mine');
  const lock = acquireSeriesLock({ seriesDir: mine });
  assert.equal(lock.ok, true, lock.reason);
  assert.equal(lockHeldByMe({ seriesDir: mine }), true);
  assert.equal(seriesLockProblem({ seriesDir: mine }), null);
  lock.value.release();
});

test('A_scorer_lock_whose_owner_is_dead_is_taken_over_and_one_whose_owner_lives_is_not', () => {
  const work = scoreDir();
  const first = takeScorerLock({ work, pid: process.pid, startTime: processStartTime(process.pid) });
  assert.equal(first.ok, true, first.reason);
  assert.equal(first.value.takeover, null);
  const live = takeScorerLock({ work, pid: process.pid + 1, startTime: 'x', isAlive: () => true });
  assert.equal(live.ok, false);
  assert.match(live.reason, /^scorer-busy: /);
  assert.ok(live.reason.includes(String(process.pid)) && live.reason.includes('scorer.lock'), 'the owner and the file are named');
  first.value.release();
  assert.equal(existsSync(path.join(work, 'scorer.lock')), false, 'the owner removes its own lock');
  writeFileSync(path.join(work, 'scorer.lock'), stamp(424_242, '9'), { flag: 'wx' });
  const dead = takeScorerLock({ work, pid: process.pid, startTime: '77', isAlive: () => false });
  assert.equal(dead.ok, true, dead.reason);
  assert.equal(dead.value.takeover.pid, 424_242);
  assert.equal(JSON.parse(readFileSync(path.join(work, 'scorer.lock'), 'utf8')).pid, process.pid, 'the lock now names the new owner');
  dead.value.release();
});

test('A_scorer_lock_nobody_can_read_is_left_alone_and_named', () => {
  const work = scoreDir();
  writeFileSync(path.join(work, 'scorer.lock'), 'half a stamp');
  const r = takeScorerLock({ work, pid: 1, startTime: '1', isAlive: () => false });
  assert.equal(r.ok, false);
  assert.match(r.reason, /scorer-busy: .*unreadable/);
});

test('scorePasses_refuses_to_start_unless_this_process_holds_the_series_lock_that_run_and_rescore_take', async () => {
  const none = harness({ holdSeries: false });
  const absent = await scorePasses(none.a);
  assert.equal(absent.kind, 'environment');
  assert.match(absent.reason, /series\.lock/);
  assert.deepEqual(readdirSync(none.work), [], 'nothing is written under score/');
  assert.equal(none.calls.build.length, 0);
  writeFileSync(path.join(none.seriesDir, 'series.lock'), stamp(999_999_999, '5'));
  const foreign = await scorePasses(none.a);
  assert.equal(foreign.kind, 'environment');
  assert.match(foreign.reason, /series\.lock.*999999999/);
  assert.deepEqual(readdirSync(none.work), []);
  const held = harness();
  const r = await scorePasses(held.a);
  held.release();
  assert.equal(r.kind, 'scored', r.reason);
});

test('Two_concurrent_starts_on_the_same_session_cannot_run_the_same_pass', async () => {
  let release;
  const gate = new Promise(resolve => { release = resolve; });
  const h = harness();
  const inner = h.a.runPass;
  h.a.runPass = async spec => { if (spec.n === 1) await gate; return inner(spec); };
  const first = scorePasses(h.a);
  await new Promise(resolve => setTimeout(resolve, 50));
  const second = await scorePasses(h.a);
  assert.equal(second.kind, 'environment');
  assert.match(second.reason, /^scorer-busy: /);
  assert.ok(second.reason.includes('scorer.lock') && second.reason.includes(String(process.pid)));
  release();
  const done = await first;
  h.release();
  assert.equal(done.kind, 'scored', done.reason);
  const seen = h.calls.pass.map(c => `${c.n}.${c.attempt}`);
  assert.equal(new Set(seen).size, seen.length, 'each (pass, attempt) was seen once');
  assert.equal(readdirSync(path.join(h.work, 'passes')).length, 10);
});

test('A_dead_scorers_lock_is_taken_over_by_the_driver_and_the_takeover_is_an_event', async () => {
  const h = harness();
  writeFileSync(path.join(h.work, 'scorer.lock'), stamp(424_242, '9'));
  const r = await scorePasses({ ...h.a, isAlive: () => false });
  h.release();
  assert.equal(r.kind, 'scored', r.reason);
  assert.ok(readdirSync(path.join(h.work, 'events')).some(f => f.endsWith('-takeover.json')));
  assert.equal(existsSync(path.join(h.work, 'scorer.lock')), false, 'released');
});
