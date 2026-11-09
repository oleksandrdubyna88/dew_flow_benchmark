// The lock of a task's clear held-out folder (bench PR 4a): `heldout-NN/.clear.lock` with the takeover guard `.clear.lock.takeover`, made by widening v1's `acquireSeriesLock` (`acquireGuardedLock` takes the lock's file name),
// never by a third lock shape. The stamps hold `{ pid, startTime, at }` exactly as `takeScorerLock` writes them; "dead" is `isAlive(pid, startTime)` saying false and nothing else; an unreadable stamp is a refusal naming the file.
// Invented stamps in owned temporary folders; no real key, folder or share is read.
import '../../minigit-bench/test/fake-home.mjs';
import assert from 'node:assert/strict';
import { existsSync, readFileSync, readdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { acquireGuardedLock } from '../../minigit-bench/lib/series-lock.mjs';
import { ownerAlive, processStartTime } from '../../minigit-bench/lib/session-state.mjs';
import { tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { casesTreeHash } from '../lib/heldout-quick.mjs';
import { closeClearFolder, openClearFolder, sweepClearFolder } from '../lib/package-seal.mjs';
import { seriesLockProblem } from '../lib/pass-locks.mjs';
import { sealedClosed } from './quick-heldout-fixture.mjs';
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

// ---- the lock as the open and the close take it ---------------------------------------------------------------------------------------------------------------------------
const ghost = (pid, startTime) => !(startTime === 'not-mine') && ownerAlive(pid, startTime);

test('Open_and_close_wait_for_the_lock_and_a_dead_owners_lock_is_taken_over_and_named', async () => {
  const opened = await sealedClosed();
  const lock = lockOf(opened.heldoutDir);
  writeFileSync(lock, stamp(process.pid, 'not-mine'));
  const taken = await openClearFolder({ ...opened.args, isAlive: ghost });
  assert.equal(taken.ok, true, taken.reason);
  assert.deepEqual([taken.value.takeover.pid, taken.value.takeover.startTime], [process.pid, 'not-mine'], 'the dead owner is named');
  assert.equal(existsSync(lock), false, 'the lock this process took is released');
  writeFileSync(lock, stamp(process.pid, 'not-mine'));
  const closed = await closeClearFolder({ ...opened.args, isAlive: ghost });
  assert.equal(closed.ok, true, closed.reason);
  assert.equal(closed.value.takeover.startTime, 'not-mine', 'close names the dead owner too');
  const live = await sealedClosed();
  writeFileSync(lockOf(live.heldoutDir), stamp(1234, 'live-start'));
  const refused = await openClearFolder({ ...live.args, isAlive: (pid, start) => pid === 1234 || ghost(pid, start) });
  assert.equal(refused.ok, false);
  assert.ok(/pid 1234/.test(refused.reason) && /live-start/.test(refused.reason), `a live holder is named: ${refused.reason}`);
  assert.equal(readFileSync(lockOf(live.heldoutDir), 'utf8'), stamp(1234, 'live-start'), 'no file changed');
  assert.equal(existsSync(live.casesDir), false, 'no cases/ made');
  const held = await sealedClosed();
  assert.equal((await openClearFolder(held.args)).ok, true);
  writeFileSync(lockOf(held.heldoutDir), stamp(1234, 'live-start'));
  const closeRefused = await closeClearFolder({ ...held.args, isAlive: (pid, start) => pid === 1234 || ghost(pid, start) });
  assert.equal(closeRefused.ok, false);
  assert.match(closeRefused.reason, /pid 1234/);
  assert.equal(existsSync(held.casesDir), true, 'close removed nothing');
});

test('Two_opens_at_once_make_one_cases_folder', async () => {
  const w = await sealedClosed();
  let second;
  const first = await openClearFolder({ ...w.args, hooks: { beforeRename: () => { second = openClearFolder({ ...w.args, hooks: {}, retry: { tries: 3, sleepMs: 1 } }); } } });
  assert.equal(first.ok, true, first.reason);
  const late = await second;
  assert.equal(late.ok, false, 'the second ran while the first held the lock: it is refused with the holder named, never a second folder');
  assert.match(late.reason, new RegExp(`pid ${process.pid}`));
  assert.equal(casesTreeHash(w.casesDir).ok, true);
  assert.deepEqual(readdirSync(w.heldoutDir).filter(n => /^testsToDelete-/.test(n)), [], 'no temporary folder is left');
  const unsealed = w.spies.unseal;
  const third = await openClearFolder(w.args);
  assert.equal(third.ok, true, third.reason);
  assert.equal(third.value.state, 'held', 'afterwards an open finds the folder equal and uses it');
  assert.equal(w.spies.unseal, unsealed, 'the archive is not opened a second time');
});

test('An_unreadable_open_stamp_or_lock_is_a_refusal_naming_the_file_and_never_judged_dead', async () => {
  const w = await sealedClosed();
  assert.equal((await openClearFolder(w.args)).ok, true);
  const stampFile = path.join(w.heldoutDir, '.open');
  writeFileSync(stampFile, '{ not json');
  let asked = 0;
  const isAlive = () => { asked += 1; return false; };
  for (const [name, run] of [['sweep', () => sweepClearFolder({ ...w.args, isAlive })], ['close', () => closeClearFolder({ ...w.args, isAlive })], ['open', () => openClearFolder({ ...w.args, isAlive })]]) {
    const r = await run();
    assert.equal(r.ok, false, name);
    assert.ok(r.reason.includes(stampFile), `${name} names the file: ${r.reason}`);
    assert.equal(existsSync(w.casesDir), true, `${name} removed nothing`);
    assert.equal(readFileSync(stampFile, 'utf8'), '{ not json');
  }
  assert.equal(asked, 0, 'isAlive was never asked about it');
  writeFileSync(lockOf(w.heldoutDir), '{}');
  const locked = await openClearFolder({ ...w.args, isAlive });
  assert.ok(locked.reason.includes(lockOf(w.heldoutDir)) && /unreadable/.test(locked.reason), locked.reason);
  assert.equal(asked, 0);
});

test('The_open_stamp_holds_the_operator_or_the_pid_and_startTime_as_takeScorerLock_writes_them', async () => {
  const w = await sealedClosed({ over: { now: () => new Date('2026-11-09T12:00:00.000Z') } });
  assert.equal((await openClearFolder(w.args)).ok, true);
  const operator = JSON.parse(readFileSync(path.join(w.heldoutDir, '.open'), 'utf8'));
  assert.deepEqual(operator, { operator: true, at: '2026-11-09T12:00:00.000Z' });
  const { withOpenedHeldout } = await import('../lib/package-seal.mjs');
  const other = await sealedClosed({ over: { now: () => new Date('2026-11-09T12:00:00.000Z') } });
  const seen = [];
  const r = await withOpenedHeldout({ ...other.args, fn: () => { seen.push(JSON.parse(readFileSync(path.join(other.heldoutDir, '.open'), 'utf8'))); return 'value'; } });
  assert.equal(r, 'value');
  const [stamped] = seen;
  assert.deepEqual(Object.keys(stamped).sort(), ['at', 'pid', 'startTime']);
  assert.equal(stamped.pid, process.pid);
  assert.equal(stamped.startTime, processStartTime(process.pid));
  assert.equal(stamped.at, '2026-11-09T12:00:00.000Z');
  writeFileSync(path.join(other.heldoutDir, 'series.lock'), `${JSON.stringify(stamped)}\n`);
  assert.equal(seriesLockProblem({ seriesDir: other.heldoutDir }), null, 'pass-locks.mjs\'s stamp parser accepts the pid stamp: the same fields');
});
