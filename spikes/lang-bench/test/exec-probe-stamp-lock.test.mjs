// K1 (fix batch 7): the stamp lock made sound for two probes. The lock holds `<pid> <start time>` and is created by `link()` from a complete temporary file, so no reader ever sees it empty; a holder is live only
// when its pid exists AND its start time is the one in the lock; a lock that cannot be parsed refuses; a stale lock is retired atomically (rename, read, compare, then take); release removes only the holder's own;
// every lock error is a record that says exit 2.
import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import * as fs from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { acquireLock } from '../lib/exec-probe/stamp.mjs';
import { owned } from './support/owned.mjs';

const live = start => () => ({ state: 'live', start });
const dead = () => ({ state: 'dead' });
const folder = t => { const dir = owned.ownedDir(t, 'lock'); fs.mkdirSync(path.join(dir, 'specs.d')); return dir; };
const lockOf = dir => path.join(dir, '.stamp.lock');
const leftovers = dir => fs.readdirSync(dir).filter(name => name.startsWith('.stamp.lock.'));

test('A_lock_is_never_seen_empty_by_a_reader_racing_a_thousand_takes', async t => {
  const dir = folder(t);
  const lib = JSON.stringify(new URL('../lib/exec-probe/stamp.mjs', import.meta.url).href);
  const taker = spawn(process.execPath, ['--input-type=module', '-e', `
    import { acquireLock } from ${lib};
    for (let i = 0; i < 1000; i += 1) { const got = acquireLock({ inputsDir: ${JSON.stringify(dir)} }); if (got.ok) got.release(); }
  `], { stdio: 'ignore' });
  const closed = new Promise(resolve => taker.once('close', resolve));
  let reads = 0;
  let empty = 0;
  let running = true;
  closed.then(() => { running = false; });
  while (running) {
    try { reads += 1; if (fs.readFileSync(lockOf(dir), 'utf8') === '') empty += 1; } catch { /* not there at this instant */ }
    await new Promise(resolve => setImmediate(resolve));
  }
  assert.ok(reads > 100, `the reader really raced the takes (${reads} reads)`);
  assert.equal(empty, 0, 'a reader never sees an empty lock');
});

test('A_pid_that_exists_with_another_start_time_is_pid_reuse_and_the_lock_is_stale_not_live', t => {
  const dir = folder(t);
  fs.writeFileSync(lockOf(dir), `${process.pid} 1`); // this very process is alive, but it did not start at second 1
  const got = acquireLock({ inputsDir: dir, probe: live(1_700_000_000), pid: process.pid + 1 });
  assert.equal(got.ok, true, `the holder's pid was reused: ${JSON.stringify(got)}`);
  assert.match(got.retired, /stale/);
  got.release();
});

test('A_live_holder_with_the_start_time_in_the_lock_refuses_and_names_its_pid_and_the_lock_path', t => {
  const dir = folder(t);
  fs.writeFileSync(lockOf(dir), '4242 1700000000');
  const got = acquireLock({ inputsDir: dir, probe: live(1_700_000_000), pid: 7 });
  assert.equal(got.ok, false);
  assert.equal(got.holder, 4242);
  assert.equal(got.path, lockOf(dir));
  assert.equal(fs.readFileSync(lockOf(dir), 'utf8'), '4242 1700000000', 'a live holder\'s lock is not touched');
});

test('A_lock_that_cannot_be_parsed_refuses_naming_its_path_and_is_never_retired', t => {
  for (const content of ['', 'garbage', '12', '12 abc', '\n']) {
    const dir = folder(t);
    fs.writeFileSync(lockOf(dir), content);
    const got = acquireLock({ inputsDir: dir, probe: dead, pid: 7 });
    assert.equal(got.ok, false, JSON.stringify(content));
    assert.match(got.error, new RegExp(lockOf(dir).replace(/[.*+?^${}()|[\]\\]/g, '\\$&')), 'it names the lock path');
    assert.match(got.error, /remove it by hand if no probe runs/);
    assert.equal(fs.readFileSync(lockOf(dir), 'utf8'), content, 'the unparsable lock stays');
  }
});

test('Two_probes_retiring_one_stale_lock_leave_exactly_one_holder_and_the_other_refuses', t => {
  const dir = folder(t);
  fs.writeFileSync(lockOf(dir), '999999 1');
  let second = null;
  let hooked = false;
  // probe B judges the lock stale, and before its rename probe A retires the same lock and takes it
  const ioB = { ...fs, renameSync: (from, to) => { if (!hooked) { hooked = true; second = acquireLock({ inputsDir: dir, probe: p => (p === 999999 ? { state: 'dead' } : { state: 'live', start: 100 }), pid: 200 }); } return fs.renameSync(from, to); } };
  const probe = p => (p === 999999 ? { state: 'dead' } : { state: 'live', start: 100 });
  const b = acquireLock({ inputsDir: dir, io: ioB, probe, pid: 300 });
  assert.equal(second.ok, true, 'A holds the lock');
  assert.equal(b.ok, false, 'B refuses: the lock it moved was A\'s new one, and it put it back');
  assert.match(fs.readFileSync(lockOf(dir), 'utf8'), /^200 /, 'A\'s lock is back in place, byte for byte');
  assert.deepEqual(leftovers(dir), [], 'no .retire or link file is left');
  second.release();
  assert.equal(fs.existsSync(lockOf(dir)), false);
});

test('A_release_removes_only_the_lock_that_is_still_its_own', t => {
  const dir = folder(t);
  const mine = acquireLock({ inputsDir: dir, probe: live(100), pid: 11 });
  assert.equal(mine.ok, true);
  assert.equal(fs.readFileSync(lockOf(dir), 'utf8'), '11 100');
  fs.writeFileSync(lockOf(dir), '22 200'); // another probe retired mine and took its own
  mine.release();
  assert.equal(fs.readFileSync(lockOf(dir), 'utf8'), '22 200', 'the other probe\'s lock is not removed');
});

test('EACCES_or_ENOSPC_on_creating_the_lock_is_a_refusal_with_the_path_and_the_error_never_a_throw', t => {
  for (const code of ['EACCES', 'ENOSPC']) {
    const dir = folder(t);
    const io = { ...fs, linkSync: () => { throw Object.assign(new Error(`${code}: no`), { code }); }, writeFileSync: (file, ...rest) => { if (String(file).includes('.stamp.lock')) throw Object.assign(new Error(`${code}: no`), { code }); return fs.writeFileSync(file, ...rest); } };
    let got;
    assert.doesNotThrow(() => { got = acquireLock({ inputsDir: dir, io, probe: live(1), pid: 5 }); });
    assert.equal(got.ok, false);
    assert.match(got.error, new RegExp(code));
    assert.match(got.error, /\.stamp\.lock/);
    assert.deepEqual(leftovers(dir), []);
  }
});

test('Two_real_probes_racing_one_stale_lock_over_many_rounds_never_both_hold_it', async t => {
  const lib = JSON.stringify(new URL('../lib/exec-probe/stamp.mjs', import.meta.url).href);
  for (let round = 0; round < 12; round += 1) {
    const dir = folder(t);
    fs.writeFileSync(lockOf(dir), '999999 1');
    const children = await Promise.all([0, 1].map(() => new Promise(resolve => { const c = spawn(process.execPath, ['--input-type=module', '-e', `
      import { acquireLock } from ${lib};
      const got = acquireLock({ inputsDir: ${JSON.stringify(dir)} });
      console.log(JSON.stringify({ ok: got.ok }));
      if (got.ok) await new Promise(resolve => setTimeout(resolve, 600));
    `], { stdio: ['ignore', 'pipe', 'ignore'] }); let out = ''; c.stdout.on('data', d => { out += d; }); c.on('close', () => resolve(JSON.parse(out.trim().split('\n').pop()))); })));
    assert.equal(children.filter(child => child.ok).length, 1, `round ${round}: exactly one probe holds the lock after a race over one stale lock`);
  }
});

// K3: strays are retired by their KNOWN names, never by listing `inputs/`.
test('A_planted_set_of_strays_of_a_dead_pid_is_retired_by_known_names_and_a_live_pids_are_not_touched', t => {
  const dir = folder(t);
  const deadPid = 999999;
  const known = ['specs.d/exec-layer.json.stamp-999999', 'specs.d/exec-layer.json.restore-999999', 'specs.json.stamp-999999', 'specs.json.restore-999999', '.stamp.lock.retire-999999', '.stamp.lock.link-999999', '.exec-probe-record.tmp-999999'];
  const others = ['specs.d/exec-layer.json.stamp-888888', 'specs.json.stamp-888888', '.exec-probe-record.tmp-888888', 'exec-probe-2026-11-03.json', 'specs.json', 'specs.d/exec-layer.json'];
  for (const name of [...known, ...others]) fs.writeFileSync(path.join(dir, name), 'x');
  fs.writeFileSync(lockOf(dir), `${deadPid} 1`);
  const listed = [];
  const io = { ...fs, readdirSync: (d, ...rest) => { listed.push(String(d)); return fs.readdirSync(d, ...rest); } };
  const got = acquireLock({ inputsDir: dir, io, probe: p => (p === deadPid ? { state: 'dead' } : { state: 'live', start: 5 }), pid: 7 });
  assert.equal(got.ok, true, JSON.stringify(got));
  assert.match(got.retired, /retired the stale stamp lock of pid 999999 and 7 stray file\(s\)/);
  for (const name of known) assert.equal(fs.existsSync(path.join(dir, name)), false, `${name} (the dead pid's) is retired`);
  for (const name of others) assert.equal(fs.existsSync(path.join(dir, name)), true, `${name} is not the dead pid's: untouched`);
  assert.deepEqual(listed, [], 'inputs/ was never listed: the strays are found by their known names');
  got.release();
});

// L1 (fix batch 8): three probes. A holder checks before its commit that the lock is still ITS content; a failed link-back in the retire keeps the moved file.
test('Three_probes_interleaving_leave_at_most_one_probe_that_may_stamp', t => {
  const dir = folder(t);
  const probe = () => ({ state: 'live', start: 100 });
  const a = acquireLock({ inputsDir: dir, probe, pid: 200 });
  assert.equal(a.ok, true, 'A holds the lock');
  fs.renameSync(lockOf(dir), `${lockOf(dir)}.retire-300`); // B (judging a stale lock it read BEFORE A took it) moves A's NEW lock away
  const c = acquireLock({ inputsDir: dir, probe, pid: 400 }); // between B's rename and its link-back C finds the lock free and takes it
  assert.equal(c.ok, true, 'C holds the lock');
  assert.deepEqual([a.holdsMine(), c.holdsMine()], [false, true], 'A, asked before its commit, finds the lock is not its own; only C may stamp');
});

test('A_holder_whose_lock_was_replaced_by_another_probe_finds_it_is_not_its_own_before_the_commit', t => {
  const dir = folder(t);
  const probe = () => ({ state: 'live', start: 100 });
  const a = acquireLock({ inputsDir: dir, probe, pid: 200 });
  assert.equal(a.ok, true);
  assert.equal(a.holdsMine(), true);
  fs.writeFileSync(lockOf(dir), '400 100'); // C took the lock after B moved A's away
  assert.equal(a.holdsMine(), false, 'A sees that the lock is no longer its own');
  fs.rmSync(lockOf(dir));
  assert.equal(a.holdsMine(), false, 'and that it is gone');
});

test('A_failed_link_back_in_the_retire_keeps_the_moved_file_refuses_and_names_the_file', t => {
  const dir = folder(t);
  fs.writeFileSync(lockOf(dir), '999999 1');
  const probe = p => (p === 999999 ? { state: 'dead' } : { state: 'live', start: 100 });
  let swapped = false;
  const io = { ...fs,
    renameSync: (from, to) => { if (!swapped && String(to).includes('.retire-')) { swapped = true; fs.writeFileSync(lockOf(dir), '200 100'); } return fs.renameSync(from, to); }, // the lock moved is another probe's NEW lock
    linkSync: (from, to) => { if (String(from).includes('.retire-')) throw Object.assign(new Error('EEXIST: a third probe holds it now'), { code: 'EEXIST' }); return fs.linkSync(from, to); } };
  const got = acquireLock({ inputsDir: dir, io, probe, pid: 300 });
  assert.equal(got.ok, false);
  const moved = `${lockOf(dir)}.retire-300`;
  assert.equal(fs.existsSync(moved), true, 'the moved file is KEPT, never removed');
  assert.equal(fs.readFileSync(moved, 'utf8'), '200 100');
  assert.match(got.error, /\.stamp\.lock\.retire-300/, 'the refusal names the file that was kept');
});

// L2: a `take()` whose cleanup of the temporary file throws after the link still holds the lock; the `committing` line.
test('A_take_whose_rm_of_the_temporary_file_throws_after_the_link_still_returns_the_lock_as_held', t => {
  const dir = folder(t);
  const io = { ...fs, rmSync: (file, ...rest) => { if (String(file).includes('.link-')) throw Object.assign(new Error('EBUSY: busy'), { code: 'EBUSY' }); return fs.rmSync(file, ...rest); } };
  let got;
  assert.doesNotThrow(() => { got = acquireLock({ inputsDir: dir, io, probe: live(100), pid: 31 }); });
  assert.equal(got.ok, true, 'the link succeeded: the lock IS held');
  assert.equal(fs.readFileSync(lockOf(dir), 'utf8'), '31 100');
  got.release();
  assert.equal(fs.existsSync(lockOf(dir)), false, 'and it is released later');
});

test('A_holder_marks_the_lock_committing_atomically_before_its_commit_and_a_retire_reports_it', t => {
  const dir = folder(t);
  const probe = p => (p === 31 ? { state: 'dead' } : { state: 'live', start: 100 });
  const first = acquireLock({ inputsDir: dir, probe: live(100), pid: 31 });
  assert.equal(first.ok, true);
  first.markCommitting();
  assert.equal(fs.readFileSync(lockOf(dir), 'utf8'), '31 100\ncommitting', 'the second line says the holder reached the commit');
  assert.equal(first.holdsMine(), true, 'still its own');
  const next = acquireLock({ inputsDir: dir, probe, pid: 32 });
  assert.equal(next.ok, true);
  assert.equal(next.retiredCommitting, true, 'the retired lock\'s holder had reached the commit');
  next.release();
  const quiet = acquireLock({ inputsDir: dir, probe: live(100), pid: 41 });
  assert.equal(quiet.ok, true);
  const after = acquireLock({ inputsDir: dir, probe: p => (p === 41 ? { state: 'dead' } : { state: 'live', start: 100 }), pid: 42 });
  assert.equal(after.retiredCommitting, false, 'a lock that never reached the commit is retired without that claim');
  after.release();
  const gone = acquireLock({ inputsDir: dir, probe: live(100), pid: 51 });
  gone.markCommitting();
  gone.release();
  assert.equal(fs.existsSync(lockOf(dir)), false, 'a committing lock is released by its own holder');
});
