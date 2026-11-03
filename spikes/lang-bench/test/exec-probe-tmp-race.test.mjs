// D2 (fix batch 9), found by the full suite: probes share one `.exec-probe-tmp/` folder, and each removes it when it is empty. Another probe's removal can land between a probe's `mkdir` of the folder and its
// `open` of a file in it (ENOENT). Every writer of a temporary file therefore makes the folder and writes the file again when the folder vanished. Each test removes the folder right after the Nth `mkdir`
// (what the other probe's housekeeping does) and asks the writer to succeed.
import assert from 'node:assert/strict';
import * as fs from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { writeReservedRecord } from '../lib/exec-probe/probe.mjs';
import { makeReservation, reserveRecord } from '../lib/exec-probe/record-path.mjs';
import { acquireLock, commitStamp, prepareStamp, restorePins } from '../lib/exec-probe/stamp.mjs';
import { TMP_DIR, writeTmp } from '../lib/exec-probe/tmp-files.mjs';
import { owned } from './support/owned.mjs';

const REAL_INPUTS = fileURLToPath(new URL('../inputs', import.meta.url));
const RECORD = { probedOn: '2026-11-03T00:00:00.000Z', host: { hostname: 'h', kernel: 'k' }, lines: [], exit: { code: 0, halt: null } };

/** An io that removes the tmp folder it just made, on its `nth` mkdir of a tmp folder (another probe's `dropTmpFolder` between the mkdir and the open). */
const racy = (nth = 1) => {
  let seen = 0;
  return { ...fs, mkdirSync: (dir, ...rest) => { const made = fs.mkdirSync(dir, ...rest); if (path.basename(String(dir)) === TMP_DIR && (seen += 1) === nth) { try { fs.rmdirSync(dir); } catch { /* not empty: the other probe's rmdir fails too */ } } return made; } };
};

function pins(t) {
  const dir = path.join(owned.ownedDir(t, 'race'), 'inputs');
  fs.mkdirSync(path.join(dir, 'specs.d'), { recursive: true });
  for (const name of fs.readdirSync(path.join(REAL_INPUTS, 'specs.d'))) fs.copyFileSync(path.join(REAL_INPUTS, 'specs.d', name), path.join(dir, 'specs.d', name));
  fs.copyFileSync(path.join(REAL_INPUTS, 'specs.json'), path.join(dir, 'specs.json'));
  return dir;
}

test('The_tmp_file_writer_makes_the_folder_again_when_another_probe_removed_it_and_gives_up_after_a_bounded_number_of_tries', t => {
  const dir = owned.ownedDir(t, 'racewriter');
  const file = path.join(dir, TMP_DIR, 'x.json.stamp-1-1');
  writeTmp(file, 'one', { flag: 'wx' }, racy());
  assert.equal(fs.readFileSync(file, 'utf8'), 'one');
  let tries = 0;
  const stubborn = { ...fs, mkdirSync: (d, ...rest) => { tries += 1; if (tries > 50) throw new Error('runaway: the writer never gives up'); fs.mkdirSync(d, ...rest); fs.rmdirSync(d); } };
  const other = owned.ownedDir(t, 'racestubborn');
  assert.throws(() => writeTmp(path.join(other, TMP_DIR, 'x.json.stamp-2-2'), 'two', {}, stubborn), /ENOENT/);
  assert.ok(tries > 1 && tries <= 10, `it tried again, but not for ever (${tries} tries)`);
});

test('A_lock_take_and_a_committing_mark_survive_the_tmp_folder_being_removed_between_the_mkdir_and_the_write', t => {
  const dir = pins(t);
  const got = acquireLock({ inputsDir: dir, io: racy(1), probe: () => ({ state: 'live', start: 5 }), pid: 7 });
  assert.equal(got.ok, true, JSON.stringify(got));
  const second = pins(t);
  const held = acquireLock({ inputsDir: second, io: racy(2), probe: () => ({ state: 'live', start: 5 }), pid: 8 });
  assert.equal(held.ok, true);
  assert.doesNotThrow(() => held.markCommitting());
  assert.equal(fs.readFileSync(path.join(second, '.stamp.lock'), 'utf8'), '8 5\ncommitting');
});

test('A_prepared_stamp_and_a_put_back_survive_the_tmp_folder_being_removed_between_the_mkdir_and_the_write', t => {
  for (const nth of [1, 2]) {
    const dir = pins(t);
    const stage = path.join(owned.ownedDir(t, 'racestage'), 's');
    fs.mkdirSync(stage);
    const before = fs.readFileSync(path.join(dir, 'specs.d', 'exec-layer.json'), 'utf8');
    const prepared = prepareStamp({ inputsDir: dir, record: RECORD, stageDir: stage, io: racy(nth), tag: '7-5' });
    assert.equal(commitStamp(prepared).ok, true, `prepare, mkdir ${nth}`);
    assert.equal(restorePins(prepared, racy(nth)).stillStamped.length, 0, `put back, mkdir ${nth}`);
    assert.equal(fs.readFileSync(path.join(dir, 'specs.d', 'exec-layer.json'), 'utf8'), before, 'the pin is back as it was');
  }
});

test('The_record_write_survives_the_tmp_folder_being_removed_between_the_mkdir_and_the_write', t => {
  const dir = owned.ownedDir(t, 'racerecord');
  const reservation = makeReservation(reserveRecord(path.join(dir, 'exec-probe-x.json')));
  assert.doesNotThrow(() => writeReservedRecord(reservation.file, RECORD, reservation.held(), racy(1)));
  assert.equal(JSON.parse(fs.readFileSync(reservation.file, 'utf8')).exit.code, 0);
  assert.deepEqual(fs.readdirSync(dir), ['exec-probe-x.json'], 'and no tmp folder is left');
});
