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
