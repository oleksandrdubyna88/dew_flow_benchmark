// K1 (fix batch 7): the stamp lock made sound for two probes. The lock holds `<pid> <start time>` and is created by `link()` from a complete temporary file, so no reader ever sees it empty; a holder is live only
// when its pid exists AND its start time is the one in the lock; a lock that cannot be parsed refuses; a stale lock is retired atomically (rename, read, compare, then take); release removes only the holder's own;
// every lock error is a record that says exit 2.
import assert from 'node:assert/strict';
import { spawn, spawnSync } from 'node:child_process';
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
  const again = acquireLock({ inputsDir: folder(t), probe: live(100), pid: 11 });
  again.release();
  assert.equal(fs.existsSync(path.join(path.dirname(lockOf(dir)), 'x')), false);
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
    const run = () => spawnSync(process.execPath, ['--input-type=module', '-e', `
      import { acquireLock } from ${lib};
      const got = acquireLock({ inputsDir: ${JSON.stringify(dir)} });
      console.log(JSON.stringify({ ok: got.ok }));
      if (got.ok) await new Promise(resolve => setTimeout(resolve, 600));
    `], { encoding: 'utf8' });
    const children = await Promise.all([0, 1].map(() => new Promise(resolve => { const c = spawn(process.execPath, ['--input-type=module', '-e', `
      import { acquireLock } from ${lib};
      const got = acquireLock({ inputsDir: ${JSON.stringify(dir)} });
      console.log(JSON.stringify({ ok: got.ok }));
      if (got.ok) await new Promise(resolve => setTimeout(resolve, 600));
    `], { stdio: ['ignore', 'pipe', 'ignore'] }); let out = ''; c.stdout.on('data', d => { out += d; }); c.on('close', () => resolve(JSON.parse(out.trim().split('\n').pop()))); })));
    void run;
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
