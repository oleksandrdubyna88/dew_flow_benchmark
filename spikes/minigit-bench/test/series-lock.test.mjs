// One runner per series at a time (E3b review N13): an O_EXCL lock file with its owner's pid and start time, taken over only when that exact process is gone (D-21a liveness).
import './fake-home.mjs'; // first: the runner reads os.homedir(); a fake HOME keeps every test off the operator's files (N18)
import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import { existsSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { LOCK_FILE, acquireSeriesLock } from '../lib/series-lock.mjs';
import { processStartTime } from '../lib/session-state.mjs';
import { tmpDir } from './helpers.mjs';

const lockOf = dir => path.join(dir, LOCK_FILE);
const owner = (pid, startTime) => `${JSON.stringify({ pid, startTime, at: '2026-11-02T10:00:00.000Z' })}\n`;

test('The_first_runner_takes_the_series_lock_and_a_second_one_is_refused_naming_the_owner', () => {
  const dir = tmpDir('lock');
  const first = acquireSeriesLock({ seriesDir: dir });
  assert.equal(first.ok, true, first.reason);
  const written = JSON.parse(readFileSync(lockOf(dir), 'utf8'));
  assert.deepEqual([written.pid, written.startTime], [process.pid, processStartTime(process.pid)], 'the owner is the (pid, start time) pair, immune to pid reuse');
  const second = acquireSeriesLock({ seriesDir: dir });
  assert.equal(second.ok, false);
  assert.match(second.reason, new RegExp(`pid ${process.pid}`));
  assert.match(second.reason, /another run, resume or settle/);
  first.value.release();
  assert.equal(existsSync(lockOf(dir)), false, 'released');
  const again = acquireSeriesLock({ seriesDir: dir });
  assert.equal(again.ok, true, 'a released lock can be taken again');
  again.value.release();
});

test('A_lock_whose_owner_is_gone_is_taken_over_and_one_whose_owner_lives_is_not', () => {
  const dir = tmpDir('lock-stale');
  writeFileSync(lockOf(dir), owner(2 ** 22 - 1, '123')); // no such process
  const taken = acquireSeriesLock({ seriesDir: dir });
  assert.equal(taken.ok, true, taken.reason);
  assert.equal(JSON.parse(readFileSync(lockOf(dir), 'utf8')).pid, process.pid, 'the stale lock was replaced');
  taken.value.release();
  const reused = tmpDir('lock-reuse');
  writeFileSync(lockOf(reused), owner(process.pid, 'not-this-process-start-time')); // the pid is live but it is another process now
  const afterReuse = acquireSeriesLock({ seriesDir: reused });
  assert.equal(afterReuse.ok, true, 'a reused pid with another start time is a gone owner');
  afterReuse.value.release();
  const live = tmpDir('lock-live');
  writeFileSync(lockOf(live), owner(process.pid, processStartTime(process.pid)));
  assert.equal(acquireSeriesLock({ seriesDir: live }).ok, false, 'control: the same pid with its own start time is alive');
});

test('A_lock_file_nobody_can_read_is_refused_not_taken_over_and_release_removes_only_its_own_lock', () => {
  const dir = tmpDir('lock-bad');
  writeFileSync(lockOf(dir), '{ not json');
  const r = acquireSeriesLock({ seriesDir: dir });
  assert.equal(r.ok, false);
  assert.match(r.reason, /unreadable/);
  assert.equal(readFileSync(lockOf(dir), 'utf8'), '{ not json', 'an unreadable lock may be a runner in the middle of writing it: it is left alone');
  const mine = tmpDir('lock-mine');
  const held = acquireSeriesLock({ seriesDir: mine }).value;
  writeFileSync(lockOf(mine), owner(2 ** 22 - 2, '9')); // something else took the lock over after we lost it
  held.release();
  assert.equal(existsSync(lockOf(mine)), true, 'a lock that is not ours is not removed');
});

test('Two_processes_cannot_both_hold_the_lock_and_a_killed_owner_leaves_a_lock_the_next_run_takes_over', async () => {
  const dir = tmpDir('lock-procs');
  const lib = path.join(path.dirname(new URL(import.meta.url).pathname), '..', 'lib', 'series-lock.mjs');
  const code = `import { acquireSeriesLock } from ${JSON.stringify(lib)}; const r = acquireSeriesLock({ seriesDir: process.argv[1] }); process.stdout.write(r.ok ? 'held\\n' : 'refused\\n'); if (r.ok) setInterval(() => {}, 1000);`;
  const child = spawn(process.execPath, ['--input-type=module', '-e', code, dir], { stdio: ['ignore', 'pipe', 'inherit'] });
  await new Promise((resolve, reject) => { child.stdout.once('data', d => (String(d).startsWith('held') ? resolve() : reject(new Error(`the child said ${d}`)))); child.once('error', reject); });
  const refused = acquireSeriesLock({ seriesDir: dir });
  assert.equal(refused.ok, false, 'the child holds it');
  assert.match(refused.reason, new RegExp(`pid ${child.pid}`));
  const gone = new Promise(resolve => child.once('exit', resolve));
  child.kill('SIGKILL'); // no release: the lock file stays behind
  await gone;
  assert.equal(existsSync(lockOf(dir)), true, 'a killed owner leaves its lock');
  const next = acquireSeriesLock({ seriesDir: dir });
  assert.equal(next.ok, true, next.reason);
  next.value.release();
});

// ---- the takeover race (E3b review coai-2) ---------------------------------------------------------------------------------------------------------------------------------
// Takeover and fresh creation are ONE critical section: every acquirer holds `series.lock.takeover` (O_EXCL, stamped with its owner) while it looks at the lock, removes a stale one and creates its own,
// and it drops the marker only if the marker is still its own. Two contenders are two fake owners in one process (`pid` and `isAlive` are seams); the hook runs while the first holds the marker.
const markerOf = dir => path.join(dir, `${LOCK_FILE}.takeover`);
const DEAD = 2 ** 22 - 1;
const alive = (...pids) => pid => pids.includes(pid);
const quick = { tries: 2, sleepMs: 1 };

test('A_second_contender_cannot_take_over_while_the_first_holds_the_takeover_marker_and_the_marker_is_kept_until_the_first_has_its_lock', () => {
  const dir = tmpDir('lock-race');
  writeFileSync(lockOf(dir), owner(DEAD, '123'));
  let hooked = 0;
  const first = acquireSeriesLock({
    seriesDir: dir, pid: 1001, startTime: 'a', isAlive: alive(1001, 1002), retry: quick,
    hooks: {
      afterStale: () => {
        hooked += 1;
        assert.equal(JSON.parse(readFileSync(markerOf(dir), 'utf8')).pid, 1001, 'the marker is the first contender\'s while it decides');
        const second = acquireSeriesLock({ seriesDir: dir, pid: 1002, startTime: 'b', isAlive: alive(1001, 1002), retry: quick });
        assert.equal(second.ok, false, 'the second contender does not take over, and does not take a fresh lock either');
        assert.match(second.reason, /another run is taking it over/);
        assert.equal(JSON.parse(readFileSync(markerOf(dir), 'utf8')).pid, 1001, 'the second contender left the first one\'s marker alone');
        assert.equal(JSON.parse(readFileSync(lockOf(dir), 'utf8')).pid, DEAD, 'and the stale lock is still there for the first to remove');
      },
    },
  });
  assert.equal(hooked, 1, 'the hook ran: the first contender found a stale lock');
  assert.equal(first.ok, true, first.reason);
  assert.equal(JSON.parse(readFileSync(lockOf(dir), 'utf8')).pid, 1001, 'the first contender holds the lock');
  assert.equal(existsSync(markerOf(dir)), false, 'its marker is dropped once it holds the lock');
  const late = acquireSeriesLock({ seriesDir: dir, pid: 1002, startTime: 'b', isAlive: alive(1001, 1002), retry: quick });
  assert.equal(late.ok, false);
  assert.match(late.reason, /in use by pid 1001/, 'afterwards the second contender is refused by the live owner, not by a marker');
  first.value.release();
});

test('A_stale_lock_that_is_replaced_by_a_live_one_during_the_takeover_is_not_removed_and_only_the_own_marker_is_dropped', () => {
  const dir = tmpDir('lock-replaced');
  writeFileSync(lockOf(dir), owner(DEAD, '123'));
  const r = acquireSeriesLock({
    seriesDir: dir, pid: 1001, startTime: 'a', isAlive: alive(1001, 1002), retry: quick,
    hooks: { afterStale: () => writeFileSync(lockOf(dir), owner(1002, 'b')) }, // another runner won the lock between the first contender's look and its removal
  });
  assert.equal(r.ok, false, 'the live replacement is a refusal naming it');
  assert.match(r.reason, /pid 1002/);
  assert.equal(JSON.parse(readFileSync(lockOf(dir), 'utf8')).pid, 1002, 'the replacement lock was not removed');
  assert.equal(existsSync(markerOf(dir)), false, 'the first contender dropped its own marker');
});

test('A_takeover_marker_another_owner_created_is_never_removed_not_when_it_is_gone_stale_and_not_by_a_release', () => {
  const stale = tmpDir('lock-marker-stale');
  writeFileSync(lockOf(stale), owner(DEAD, '123'));
  writeFileSync(markerOf(stale), owner(DEAD - 1, '9')); // a taker that was killed while it held the marker
  const refused = acquireSeriesLock({ seriesDir: stale, pid: 1001, startTime: 'a', isAlive: alive(1001), retry: quick });
  assert.equal(refused.ok, false);
  assert.match(refused.reason, new RegExp(`${LOCK_FILE}.takeover`));
  assert.match(refused.reason, /gone/);
  assert.equal(readFileSync(markerOf(stale), 'utf8'), owner(DEAD - 1, '9'), 'a marker whose owner is gone is named and left for the operator, never removed by a contender');
  const taken = tmpDir('lock-marker-taken');
  writeFileSync(lockOf(taken), owner(DEAD, '123'));
  const other = owner(1002, 'b');
  const r = acquireSeriesLock({ seriesDir: taken, pid: 1001, startTime: 'a', isAlive: alive(1001, 1002), retry: quick, hooks: { afterStale: () => writeFileSync(markerOf(taken), other) } });
  assert.equal(r.ok, true, r.reason);
  assert.equal(readFileSync(markerOf(taken), 'utf8'), other, 'the marker had become another owner\'s: dropping the own marker removes only a marker that is still its own');
  r.value.release();
});

test('Contenders_racing_for_a_stale_lock_or_for_a_free_series_end_with_exactly_one_holder', async () => {
  const lib = path.join(path.dirname(new URL(import.meta.url).pathname), '..', 'lib', 'series-lock.mjs');
  const code = `import { acquireSeriesLock } from ${JSON.stringify(lib)}; const r = acquireSeriesLock({ seriesDir: process.argv[1] }); process.stdout.write(r.ok ? 'held\\n' : 'refused\\n'); if (r.ok) setInterval(() => {}, 1000);`;
  for (const start of ['stale', 'free']) {
    const dir = tmpDir(`lock-procs-${start}`);
    if (start === 'stale') writeFileSync(lockOf(dir), owner(DEAD, '123'));
    const children = Array.from({ length: 8 }, () => spawn(process.execPath, ['--input-type=module', '-e', code, dir], { stdio: ['ignore', 'pipe', 'inherit'] }));
    const answers = await Promise.all(children.map(c => new Promise((resolve, reject) => { c.stdout.once('data', d => resolve(String(d).trim())); c.once('error', reject); })));
    for (const c of children) c.kill('SIGKILL');
    await Promise.all(children.map(c => new Promise(resolve => (c.exitCode !== null || c.signalCode ? resolve() : c.once('exit', resolve)))));
    assert.equal(answers.filter(a => a === 'held').length, 1, `${start}: exactly one of 8 contenders holds the lock (${answers.join(' ')})`);
  }
});
