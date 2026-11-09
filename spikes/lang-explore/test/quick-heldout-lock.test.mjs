// The lock of a task's clear held-out folder (bench PR 4a): `heldout-NN/.clear.lock` with the takeover guard `.clear.lock.takeover`, made by widening v1's `acquireSeriesLock` (`acquireGuardedLock` takes the lock's file name),
// never by a third lock shape. The stamps hold `{ pid, startTime, at }` exactly as `takeScorerLock` writes them; "dead" is `isAlive(pid, startTime)` saying false and nothing else; an unreadable stamp is a refusal naming the file.
// Invented stamps in owned temporary folders; no real key, folder or share is read.
import '../../minigit-bench/test/fake-home.mjs';
import assert from 'node:assert/strict';
import { existsSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { acquireGuardedLock } from '../../minigit-bench/lib/series-lock.mjs';
import { processStartTime } from '../../minigit-bench/lib/session-state.mjs';
import { tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { seriesLockProblem } from '../lib/pass-locks.mjs';

const NAME = '.clear.lock';
const DEAD = 2 ** 22 - 1;
const alive = (...pids) => pid => pids.includes(pid);
const quick = { tries: 2, sleepMs: 1 };
const stamp = (pid, startTime, at = '2026-11-09T10:00:00.000Z') => `${JSON.stringify({ pid, startTime, at })}\n`;
const lockOf = dir => path.join(dir, NAME);
const guardOf = dir => path.join(dir, `${NAME}.takeover`);
const take = (dir, over = {}) => acquireGuardedLock({ dir, name: NAME, pid: 1001, startTime: 'a', isAlive: alive(1001, 1002), retry: quick, ...over });

test('Two_takers_on_one_dead_lock_leave_exactly_one_holder', () => {
  const dir = tmpDir('q-lock-two');
  writeFileSync(lockOf(dir), stamp(DEAD, '123'));
  let second;
  const first = take(dir, { hooks: { afterStale: () => { second = take(dir, { pid: 1002, startTime: 'b' }); } } });
  assert.equal(first.ok, true, first.reason);
  assert.equal(second.ok, false, 'the second taker read the same dead stamp and is refused: no ok for both');
  assert.match(second.reason, /another run is taking it over/);
  assert.equal(JSON.parse(readFileSync(lockOf(dir), 'utf8')).pid, 1001, 'the first holds the lock');
  assert.equal(existsSync(guardOf(dir)), false, 'the guard is dropped once the first holds the lock');
  first.value.release();
  assert.equal(existsSync(lockOf(dir)), false, 'released: the lock file is gone');
  assert.equal(existsSync(guardOf(dir)), false, 'and no guard is left');
  const control = take(dir, { pid: 1002, startTime: 'b' });
  assert.equal(control.ok, true, `control: after the release the other taker holds it (${control.reason})`);
  control.value.release();
});

test('A_live_lock_found_under_the_guard_is_never_removed', () => {
  const dir = tmpDir('q-lock-live');
  writeFileSync(lockOf(dir), stamp(DEAD, '123'));
  const replacement = stamp(1002, 'b');
  let reads = 0;
  const isAlive = pid => { reads += 1; return pid === 1002 || pid === 1001; };
  const r = take(dir, { isAlive, hooks: { afterStale: () => writeFileSync(lockOf(dir), replacement) } });
  assert.equal(r.ok, false, 'the stamp was replaced by a live owner between the two reads: a refusal naming the live holder');
  assert.match(r.reason, /pid 1002/);
  assert.equal(readFileSync(lockOf(dir), 'utf8'), replacement, 'the live lock\'s bytes are unchanged');
  assert.equal(existsSync(guardOf(dir)), false, 'the guard was removed');
  assert.ok(reads >= 1);
});

test('A_dead_guard_is_refused_with_its_pid_start_time_and_the_remedy_and_nothing_is_removed', () => {
  const dir = tmpDir('q-lock-guard');
  const [lock, guard] = [stamp(DEAD, '123'), stamp(DEAD - 1, 'guard-start', '2026-11-08T08:30:00.000Z')];
  writeFileSync(lockOf(dir), lock);
  writeFileSync(guardOf(dir), guard);
  const r = take(dir);
  assert.equal(r.ok, false);
  for (const part of [guardOf(dir), `pid ${DEAD - 1}`, 'guard-start', '2026-11-08T08:30:00.000Z', 'not running with that start time', `no takeover is in progress; remove ${guardOf(dir)} and run again`]) {
    assert.ok(r.reason.includes(part), `the sentence holds ${JSON.stringify(part)}: ${r.reason}`);
  }
  assert.equal(readFileSync(guardOf(dir), 'utf8'), guard, 'the guard is left for the operator');
  assert.equal(readFileSync(lockOf(dir), 'utf8'), lock, 'the lock is untouched');
  const live = tmpDir('q-lock-guard-live');
  writeFileSync(guardOf(live), stamp(1002, 'b'));
  const waits = take(live);
  assert.equal(waits.ok, false);
  assert.match(waits.reason, /another run is taking it over/, 'control: a LIVE guard is waited for and then refused as a taker at work, not as a dead one');
});

test('The_lock_and_the_guard_hold_pid_and_startTime_as_takeScorerLock_writes_them', () => {
  const dir = tmpDir('q-lock-shape');
  const seen = [];
  const r = acquireGuardedLock({ dir, name: NAME, now: () => new Date('2026-11-09T12:00:00.000Z'), hooks: { afterLock: () => { for (const f of [lockOf(dir), guardOf(dir)]) seen.push(JSON.parse(readFileSync(f, 'utf8'))); } } });
  assert.equal(r.ok, true, r.reason);
  assert.equal(seen.length, 2, 'the hook ran with the lock and the guard both in place');
  for (const j of seen) {
    assert.deepEqual(Object.keys(j).sort(), ['at', 'pid', 'startTime']);
    assert.ok(Number.isInteger(j.pid) && j.pid === process.pid);
    assert.equal(j.startTime, processStartTime(process.pid), 'startTime is field 22 of /proc/<pid>/stat, as takeScorerLock reads it');
    assert.equal(j.at, '2026-11-09T12:00:00.000Z', 'the clock is a seam');
  }
  writeFileSync(path.join(dir, 'series.lock'), readFileSync(lockOf(dir)));
  assert.equal(seriesLockProblem({ seriesDir: dir }), null, 'pass-locks.mjs\'s stamp parser accepts these bytes as a lock held by this process: the same shape');
  r.value.release();
});

test('An_unreadable_lock_or_guard_is_a_refusal_naming_the_file_and_never_judged_dead', () => {
  for (const [label, body] of [['not json', '{ not json'], ['no pid', '{}']]) {
    const dir = tmpDir('q-lock-unreadable');
    writeFileSync(lockOf(dir), body);
    let asked = 0;
    const r = take(dir, { isAlive: () => { asked += 1; return false; } });
    assert.equal(r.ok, false, label);
    assert.ok(r.reason.includes(lockOf(dir)) && /unreadable/.test(r.reason), `${label}: names the file: ${r.reason}`);
    assert.equal(readFileSync(lockOf(dir), 'utf8'), body, `${label}: nothing removed`);
    assert.equal(asked, 0, `${label}: isAlive is never asked about it`);
  }
  const dir = tmpDir('q-lock-guard-unreadable');
  writeFileSync(guardOf(dir), '{ half');
  let asked = 0;
  const r = take(dir, { isAlive: () => { asked += 1; return false; } });
  assert.equal(r.ok, false);
  assert.ok(r.reason.includes(guardOf(dir)), r.reason);
  assert.equal(asked, 0, 'a guard nobody can read may be a taker in the middle of writing it: never judged dead');
  assert.equal(readFileSync(guardOf(dir), 'utf8'), '{ half');
});

test('A_dead_owners_lock_is_taken_over_and_the_taker_is_told_whose_it_was', () => {
  const dir = tmpDir('q-lock-takeover');
  writeFileSync(lockOf(dir), stamp(DEAD, 'gone-start'));
  const r = take(dir);
  assert.equal(r.ok, true, r.reason);
  assert.deepEqual([r.value.takeover.pid, r.value.takeover.startTime], [DEAD, 'gone-start'], 'the dead owner is named');
  r.value.release();
  const free = take(tmpDir('q-lock-free'));
  assert.equal(free.value.takeover, null, 'control: a free lock has no takeover');
  free.value.release();
});
