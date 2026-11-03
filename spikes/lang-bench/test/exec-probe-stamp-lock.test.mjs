// K1 (fix batch 7), D1 (fix batch 9): the stamp lock for two probes. The lock holds `<pid> <start time>` and is created by `link()` from a complete temporary file, so no reader ever sees it empty; a holder is live only
// when its pid exists AND its start time is the one in the lock; a stale lock and a lock that cannot be parsed are NEVER removed or moved by a probe: the probe refuses with the exact manual step; only the holder
// changes its own lock (`committing`, release); every lock error is a record that says exit 2. Rewritten for D1 (old -> new):
//   Two_probes_retiring_one_stale_lock_leave_exactly_one_holder_and_the_other_refuses -> Two_probes_meeting_one_stale_lock_both_refuse_and_the_lock_is_byte_equal
//   Two_real_probes_racing_one_stale_lock_over_many_rounds_never_both_hold_it -> Two_real_probes_racing_one_stale_lock_over_many_rounds_both_refuse_and_the_lock_stays
//   A_failed_link_back_in_the_retire_keeps_the_moved_file_refuses_and_names_the_file -> A_stale_lock_is_never_renamed_removed_or_linked_by_a_probe_whatever_the_io_allows
//   A_holder_marks_the_lock_committing_atomically_before_its_commit_and_a_retire_reports_it -> A_holder_marks_the_lock_committing_atomically_before_its_commit_and_a_refusal_by_a_stale_committing_lock_says_so
//   Three_probes_interleaving_leave_at_most_one_probe_that_may_stamp -> exec-probe-stamp-many.test.mjs: N_probes_started_together_against_one_stale_lock_none_stamps_every_record_says_2_and_the_lock_is_unchanged
// (A_pid_that_exists_with_another_start_time... and A_lock_that_cannot_be_parsed... keep their names, their assertions are the refusal.)
import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import * as fs from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { acquireLock } from '../lib/exec-probe/stamp.mjs';
import { parse } from '../lib/exec-probe/stamp-lock.mjs';
import { owned } from './support/owned.mjs';

const live = start => () => ({ state: 'live', start });
const dead = () => ({ state: 'dead' });
const folder = t => { const dir = owned.ownedDir(t, 'lock'); fs.mkdirSync(path.join(dir, 'specs.d')); return dir; };
const lockOf = dir => path.join(dir, '.stamp.lock');
const leftovers = dir => fs.readdirSync(dir).filter(name => name.startsWith('.stamp.lock.') || name === '.exec-probe-tmp');

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
  assert.equal(got.ok, false, 'a stale lock refuses: a probe never takes it over');
  assert.equal(got.stale, true, `the holder's pid was reused, so the lock is stale: ${JSON.stringify(got)}`);
  assert.equal(got.holder, process.pid);
  assert.match(got.error, new RegExp(`a probe that was killed left the stamp lock ${lockOf(dir).replace(/[.*+?^${}()|[\]\\]/g, '\\$&')} \\(pid ${process.pid}, start time 1, now gone\\)\\. If no probe is running, remove it: rm ${lockOf(dir).replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}`));
  assert.equal(fs.readFileSync(lockOf(dir), 'utf8'), `${process.pid} 1`, 'and leaves the lock byte-equal');
  assert.deepEqual(leftovers(dir), []);
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
    assert.match(got.error, new RegExp(`If no probe is running, remove it: rm ${lockOf(dir).replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}`), 'and the exact manual step');
    assert.equal(fs.readFileSync(lockOf(dir), 'utf8'), content, 'the unparsable lock stays');
    assert.deepEqual(leftovers(dir), []);
  }
});

test('Two_probes_meeting_one_stale_lock_both_refuse_and_the_lock_is_byte_equal', t => {
  const dir = folder(t);
  fs.writeFileSync(lockOf(dir), '999999 1');
  const probe = p => (p === 999999 ? { state: 'dead' } : { state: 'live', start: 100 });
  const results = [200, 300].map(pid => acquireLock({ inputsDir: dir, probe, pid }));
  assert.deepEqual(results.map(got => got.ok), [false, false], 'neither takes the stale lock');
  assert.equal(fs.readFileSync(lockOf(dir), 'utf8'), '999999 1', 'it is byte-equal');
  assert.deepEqual(leftovers(dir), [], 'no .retire or link file, no tmp folder is left');
});

test('A_release_removes_only_the_lock_that_is_still_its_own', t => {
  const dir = folder(t);
  const mine = acquireLock({ inputsDir: dir, probe: live(100), pid: 11 });
  assert.equal(mine.ok, true);
  assert.equal(fs.readFileSync(lockOf(dir), 'utf8'), '11 100');
  fs.writeFileSync(lockOf(dir), '22 200'); // someone removed mine by hand and another probe took its own
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

test('Two_real_probes_racing_one_stale_lock_over_many_rounds_both_refuse_and_the_lock_stays', async t => {
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
    assert.equal(children.filter(child => child.ok).length, 0, `round ${round}: no probe takes a stale lock`);
    assert.equal(fs.readFileSync(lockOf(dir), 'utf8'), '999999 1', `round ${round}: the lock is byte-equal`);
  }
});

// D2 (fix batch 9): the stamp lock never looks for strays: it lists nothing, so `inputs/` is never listed and another probe's temporary files are left to the START sweep (`exec-probe-strays.test.mjs`).
// Rewritten from A_planted_set_of_strays_of_a_dead_pid_is_retired_by_known_names_and_a_live_pids_are_not_touched (the retire of strays by known names is gone with the design).
test('Taking_a_lock_never_lists_inputs_and_never_touches_the_temporary_files_of_other_probes', t => {
  const dir = folder(t);
  const others = ['.exec-probe-tmp/stamp.lock.link-999999-1', '.exec-probe-tmp/specs.json.stamp-999999-1', 'exec-probe-2026-11-03.json', 'specs.json'];
  fs.mkdirSync(path.join(dir, '.exec-probe-tmp'));
  for (const name of others) fs.writeFileSync(path.join(dir, name), 'x');
  const listed = [];
  const io = { ...fs, readdirSync: (d, ...rest) => { listed.push(String(d)); return fs.readdirSync(d, ...rest); } };
  const got = acquireLock({ inputsDir: dir, io, probe: () => ({ state: 'live', start: 5 }), pid: 7 });
  assert.equal(got.ok, true, JSON.stringify(got));
  for (const name of others) assert.equal(fs.existsSync(path.join(dir, name)), true, `${name} is another probe's: untouched`);
  assert.equal(listed.filter(d => path.basename(d) !== '.exec-probe-tmp').length, 0, `inputs/ itself was never listed: ${listed.join(', ')}`);
  got.release();
});

// L1 (fix batch 8): a holder checks before its commit that the lock is still ITS content.
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

test('A_stale_lock_is_never_renamed_removed_or_linked_by_a_probe_whatever_the_io_allows', t => {
  const dir = folder(t);
  fs.writeFileSync(lockOf(dir), '999999 1\ncommitting');
  const probe = p => (p === 999999 ? { state: 'dead' } : { state: 'live', start: 100 });
  const touched = [];
  const watch = name => (from, ...rest) => { touched.push(`${name} ${path.basename(String(from))}`); return fs[`${name}Sync`](from, ...rest); };
  const io = { ...fs, renameSync: watch('rename'), rmSync: (file, ...rest) => { touched.push(`rm ${path.basename(String(file))}`); return fs.rmSync(file, ...rest); }, linkSync: (from, to) => { touched.push(`link ${path.basename(String(to))}`); return fs.linkSync(from, to); } };
  const got = acquireLock({ inputsDir: dir, io, probe, pid: 300 });
  assert.equal(got.ok, false);
  assert.deepEqual([got.stale, got.committing, got.holder], [true, true, 999999]);
  assert.equal(touched.some(entry => /\.stamp\.lock$/.test(entry) && !entry.startsWith('link')), false, `the stale lock itself is never renamed or removed: ${touched.join(', ')}`);
  assert.equal(touched.some(entry => entry.includes('.retire-')), false, 'no .retire- file');
  assert.equal(fs.readFileSync(lockOf(dir), 'utf8'), '999999 1\ncommitting');
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

test('A_holder_marks_the_lock_committing_atomically_before_its_commit_and_a_refusal_by_a_stale_committing_lock_says_so', t => {
  const dir = folder(t);
  const probe = p => (p === 31 ? { state: 'dead' } : { state: 'live', start: 100 });
  const first = acquireLock({ inputsDir: dir, probe: live(100), pid: 31 });
  assert.equal(first.ok, true);
  first.markCommitting();
  assert.equal(fs.readFileSync(lockOf(dir), 'utf8'), '31 100\ncommitting', 'the second line says the holder reached the commit');
  assert.equal(first.holdsMine(), true, 'still its own, structurally: the flag does not change whose it is');
  const next = acquireLock({ inputsDir: dir, probe, pid: 32 });
  assert.equal(next.ok, false, 'the holder is gone: the lock is stale and refuses');
  assert.deepEqual([next.stale, next.committing], [true, true], 'the refusal says that the dead holder had reached the commit');
  assert.equal(fs.readFileSync(lockOf(dir), 'utf8'), '31 100\ncommitting', 'byte-equal');
  fs.rmSync(lockOf(dir)); // by hand, the manual step
  const quiet = acquireLock({ inputsDir: dir, probe: live(100), pid: 41 });
  assert.equal(quiet.ok, true);
  const after = acquireLock({ inputsDir: dir, probe: p => (p === 41 ? { state: 'dead' } : { state: 'live', start: 100 }), pid: 42 });
  assert.deepEqual([after.ok, after.stale, after.committing], [false, true, false], 'a lock that never reached the commit says so');
  fs.rmSync(lockOf(dir));
  const gone = acquireLock({ inputsDir: dir, probe: live(100), pid: 51 });
  gone.markCommitting();
  gone.release();
  assert.equal(fs.existsSync(lockOf(dir)), false, 'a committing lock is released by its own holder');
  assert.deepEqual(leftovers(dir), []);
});

test('The_lock_text_is_compared_through_parse_not_as_a_string', () => {
  assert.deepEqual(parse('12 345'), { pid: 12, start: '345', committing: false });
  assert.deepEqual(parse('12 345\ncommitting\n'), { pid: 12, start: '345', committing: true });
  assert.deepEqual(parse('12 -'), { pid: 12, start: '-', committing: false });
  assert.equal(parse('12 345 committing'), null);
  assert.equal(parse('12 345\nCommitting'), null);
});

test('A_holder_whose_lock_text_has_a_trailing_newline_still_holds_it_and_releases_it', t => {
  const dir = folder(t);
  const mine = acquireLock({ inputsDir: dir, probe: live(100), pid: 61 });
  assert.equal(mine.ok, true);
  fs.writeFileSync(lockOf(dir), '61 100\n'); // the same holder and start time, written another way: structurally the same
  assert.equal(mine.holdsMine(), true);
  mine.release();
  assert.equal(fs.existsSync(lockOf(dir)), false, 'released: the comparison is structural (coai 9)');
});

// N4 (coai 5, Opus LOW 5): when the retry after a vanished lock fails with an error, the refusal says that error, not "held by another probe".
test('A_lock_that_vanished_and_whose_retry_fails_with_an_error_is_refused_with_that_error_not_as_held_by_another_probe', t => {
  const dir = folder(t);
  let links = 0;
  let writes = 0;
  const io = { ...fs,
    linkSync: (from, to) => { links += 1; if (links === 1) throw Object.assign(new Error('EEXIST: held'), { code: 'EEXIST' }); return fs.linkSync(from, to); }, // the first take finds it held, and by the read it is gone
    writeFileSync: (file, ...rest) => { writes += 1; if (writes === 2) throw Object.assign(new Error('EACCES: no'), { code: 'EACCES' }); return fs.writeFileSync(file, ...rest); } };
  const got = acquireLock({ inputsDir: dir, io, probe: live(1), pid: 5 });
  assert.equal(got.ok, false);
  assert.match(got.error, /could not be used \(EACCES/);
  assert.doesNotMatch(got.error, /just took it/);
  assert.deepEqual(leftovers(dir), []);
});

// N5 (Opus LOW 4): the committing mark has its own temporary kind, created with `wx`, so it can never write through a hard link to the lock (the case where the link temp could not be removed after the link, L2).
test('A_committing_mark_never_writes_through_a_hard_link_left_by_a_take_and_release_leaves_no_temporary_file', t => {
  const dir = folder(t);
  let failed = false;
  const io = { ...fs, rmSync: (file, ...rest) => { if (!failed && String(file).includes('.link-')) { failed = true; throw Object.assign(new Error('EBUSY: busy'), { code: 'EBUSY' }); } return fs.rmSync(file, ...rest); } };
  const got = acquireLock({ inputsDir: dir, io, probe: live(100), pid: 31 });
  assert.equal(got.ok, true);
  const linkTemp = path.join(dir, '.exec-probe-tmp', fs.readdirSync(path.join(dir, '.exec-probe-tmp'))[0]);
  assert.equal(fs.statSync(lockOf(dir)).nlink, 2, 'the link temp could not be removed: the lock has two names');
  got.markCommitting();
  assert.equal(fs.readFileSync(lockOf(dir), 'utf8'), '31 100\ncommitting');
  assert.equal(fs.statSync(lockOf(dir)).nlink, 1, 'the mark was renamed over the lock: a new file, not the one the link temp names');
  assert.equal(fs.readFileSync(linkTemp, 'utf8'), '31 100', 'the link temp was not written through');
  assert.equal(got.holdsMine(), true);
  got.release();
  assert.equal(fs.existsSync(lockOf(dir)), false);
  assert.deepEqual(leftovers(dir), [], 'no temporary file and no tmp folder is left after release');
});
