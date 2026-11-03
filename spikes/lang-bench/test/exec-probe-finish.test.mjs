// The end of the probe command (W3, L3, S1; fix batches 4 to 6): THE RECORD IS THE LAST WRITE. The stamp is locked, prepared in temporary files beside the pins and committed (two renames) first; a failure is a
// record that says exit 2 with `stampError` (the FILE is read, not only the returned code). Then the record is written once, to a temporary file renamed over the placeholder; if that fails both pins are put back
// and the placeholder released. Only then the reservation is taken. The tests run the command's own end path (`endOfProbe`) on a real copy of the pins.
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import * as fs from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { endOfProbe } from '../lib/exec-probe/command-end.mjs';
import { shouldStampProbedOn } from '../lib/exec-probe/probe.mjs';
import { makeReservation, reserveRecord } from '../lib/exec-probe/record-path.mjs';
import { stopCleanly } from '../lib/exec-probe/stop.mjs';
import { checkRecord } from '../lib/specs-record.mjs';
import { owned } from './support/owned.mjs';

const REAL_INPUTS = fileURLToPath(new URL('../inputs', import.meta.url));
const record = (code = 0) => ({ probedOn: '2026-11-03T00:00:00.000Z', host: { hostname: 'h', kernel: 'k' }, lines: [{ id: 'a', ok: true }], exit: { code, halt: code === 0 ? null : 'memory-view-mismatch' } });
const noop = () => {};

/** A real copy of the pins plus one big probe record that must never be staged, a staging folder and a reservation. */
function setup(t) {
  const root = owned.ownedDir(t, 'finish');
  const inputsDir = path.join(root, 'inputs');
  fs.mkdirSync(path.join(inputsDir, 'specs.d'), { recursive: true });
  for (const name of fs.readdirSync(path.join(REAL_INPUTS, 'specs.d'))) fs.copyFileSync(path.join(REAL_INPUTS, 'specs.d', name), path.join(inputsDir, 'specs.d', name));
  fs.copyFileSync(path.join(REAL_INPUTS, 'specs.json'), path.join(inputsDir, 'specs.json'));
  fs.writeFileSync(path.join(inputsDir, 'exec-probe-big-old-record.json'), 'x'.repeat(10_000));
  const stageDir = path.join(root, 'stage');
  fs.mkdirSync(stageDir);
  const recordsDir = path.join(root, 'records');
  const reservation = makeReservation(reserveRecord(path.join(recordsDir, 'exec-probe-x.json')));
  const pins = () => ({ layer: fs.readFileSync(path.join(inputsDir, 'specs.d', 'exec-layer.json'), 'utf8'), specs: fs.readFileSync(path.join(inputsDir, 'specs.json'), 'utf8') });
  const strays = () => [inputsDir, path.join(inputsDir, 'specs.d'), recordsDir].flatMap(dir => fs.readdirSync(dir)).filter(name => /\.(stamp|restore|tmp)-\d+$|^\.stamp\.lock$/.test(name));
  const said = [];
  const run = (over = {}) => endOfProbe({ record: record(), reservation, noSpecs: false, inputsDir, stageDir, say: text => said.push(text), report: noop, ...over });
  return { inputsDir, stageDir, reservation, pins, strays, said, run, file: reservation.file };
}

/** An io whose rename and write can be made to fail for the files a test names. */
const failing = ({ rename = () => false, write = () => false } = {}) => ({
  ...fs,
  renameSync: (from, to) => { if (rename(String(from), String(to))) throw new Error(`EXDEV: ${path.basename(String(to))}`); return fs.renameSync(from, to); },
  writeFileSync: (file, ...rest) => { if (write(String(file))) throw new Error(`ENOSPC: ${path.basename(String(file))}`); return fs.writeFileSync(file, ...rest); },
});
const onTarget = name => (_from, to) => to.endsWith(name);

test('The_record_is_the_last_write_the_stamp_is_in_place_before_it_and_nothing_is_left', t => {
  const s = setup(t);
  const before = s.pins();
  const events = [];
  const io = { ...fs, renameSync: (from, to) => { if (String(to) === s.file) events.push(`record-rename; pins stamped: ${s.pins().layer !== before.layer}; placeholder empty: ${fs.statSync(s.file).size === 0}`); return fs.renameSync(from, to); } };
  const code = s.run({ io, onWritten: written => events.push(`written ${written}`) });
  assert.equal(code, 0);
  assert.deepEqual(events, ['record-rename; pins stamped: true; placeholder empty: true', 'written 0'], 'both pins are stamped BEFORE the record is renamed into place, and the record is the last write');
  assert.match(s.pins().layer, /"probedOn": "2026-11-03T00:00:00.000Z"/);
  assert.deepEqual(checkRecord(s.inputsDir), { ok: true });
  assert.deepEqual(s.strays(), [], 'no temporary file, no lock');
  assert.equal(JSON.parse(fs.readFileSync(s.file, 'utf8')).exit.code, 0);
  assert.equal(s.reservation.held(), null, 'the reservation was taken after the record was in place');
  assert.deepEqual(fs.readdirSync(s.stageDir), ['specs.d'], 'only specs.d was staged');
  assert.deepEqual(fs.readdirSync(path.join(s.stageDir, 'specs.d')).filter(name => !['runner.json', 'exec-layer.json', 'format.json', 'spec-states.json', 'memory-policy.json'].includes(name)), [], 'and only the files `assemble` reads: never a copy of the probe records');
});

test('A_failed_second_rename_gives_a_record_FILE_that_says_exit_2_with_the_error_and_both_pins_as_they_were', t => {
  const s = setup(t);
  const before = s.pins();
  const code = s.run({ io: failing({ rename: onTarget('specs.json') }) });
  const written = JSON.parse(fs.readFileSync(s.file, 'utf8'));
  assert.equal(code, 2);
  assert.deepEqual([written.exit.code, written.exit.halt], [2, 'probed-on-stamp-failed'], 'read from the file: it never says 0 when the command says 2');
  assert.match(written.exit.stampError, /EXDEV: specs\.json/);
  assert.deepEqual(s.pins(), before, 'the first pin was put back');
  assert.deepEqual(s.strays(), []);
});

test('A_failed_first_rename_and_a_failed_prepare_are_records_that_say_exit_2_and_leave_both_pins', t => {
  for (const io of [failing({ rename: onTarget('exec-layer.json') }), failing({ write: file => file.endsWith('specs.json.stamp-' + process.pid) })]) {
    const s = setup(t);
    const before = s.pins();
    assert.equal(s.run({ io }), 2);
    const written = JSON.parse(fs.readFileSync(s.file, 'utf8'));
    assert.deepEqual([written.exit.code, written.exit.halt], [2, 'probed-on-stamp-failed']);
    assert.ok(written.exit.stampError);
    assert.deepEqual(s.pins(), before);
    assert.deepEqual(s.strays(), []);
  }
});

test('A_failed_put_back_is_never_reported_as_both_stay_as_they_were_it_names_the_stamped_pin', t => {
  const s = setup(t);
  const before = s.pins();
  const io = failing({ rename: (from, to) => to.endsWith('specs.json') || from.includes('.restore-') });
  const code = s.run({ io });
  const written = JSON.parse(fs.readFileSync(s.file, 'utf8'));
  assert.equal(code, 2);
  assert.match(written.exit.stampError, /specs\.d\/exec-layer\.json IS stamped and the put-back failed/, 'the record names the stamped pin');
  assert.match(s.said.join('\n'), /specs\.d\/exec-layer\.json is stamped, the other is not/, 'and so does stderr');
  assert.doesNotMatch(s.said.join('\n') + written.exit.stampError, /both stay as they were/);
  assert.notEqual(s.pins().layer, before.layer, 'the layer pin really is stamped: the message told the truth');
  assert.equal(s.pins().specs, before.specs);
});

test('A_failed_record_write_or_a_failed_record_rename_puts_both_pins_back_releases_the_placeholder_and_leaves_no_temporary_file', t => {
  for (const [name, io] of [['write', failing({ write: file => /exec-probe-x\.json\.tmp-/.test(file) })], ['rename', failing({ rename: (_from, to) => to.endsWith('exec-probe-x.json') })]]) {
    const s = setup(t);
    const before = s.pins();
    const code = s.run({ io });
    assert.equal(code, 2, name);
    assert.deepEqual(s.pins(), before, `${name}: both pins are byte-equal to before`);
    assert.equal(fs.existsSync(s.file), false, `${name}: no placeholder, no cut-off dated record`);
    assert.deepEqual(s.strays(), [], `${name}: no temporary file, no lock`);
    assert.match(s.said.join('\n'), /the record could not be written \(.*\); both pins were put back; the placeholder was released \(exit 2\)/, name);
  }
});

test('A_failed_record_write_AND_a_failed_put_back_name_the_pins_that_point_at_a_probe_with_no_record', t => {
  const s = setup(t);
  const io = failing({ write: file => /exec-probe-x\.json\.tmp-/.test(file), rename: from => from.includes('.restore-') });
  assert.equal(s.run({ io }), 2);
  const said = s.said.join('\n');
  assert.match(said, /specs\.d\/exec-layer\.json and specs\.json could not be put back: it points at a probe with NO record/);
  assert.equal(fs.existsSync(s.file), false, 'the placeholder is released all the same');
});

test('A_stop_after_the_record_is_written_only_closes_the_owned_folders_and_exits_with_the_records_code', async t => {
  const s = setup(t);
  let written = null;
  assert.equal(s.run({ onWritten: code => { written = code; } }), 0);
  assert.equal(written, 0);
  const said = [];
  const exits = [];
  const closed = [];
  await stopCleanly({ code: 143, recordedCode: () => written, bound: 1000, say: text => said.push(text), exit: code => exits.push(code), release: noop, describeLive: () => { throw new Error('no run is looked at'); }, cleanLive: () => { throw new Error('no cleanup runs'); }, closeScope: async () => closed.push('closed') });
  assert.deepEqual(exits, [0], 'the exit is the record\'s code, not 143');
  assert.deepEqual(closed, ['closed']);
  assert.match(said.join('\n'), /the record is already written/);
  const failed = setup(t);
  let failedCode = null;
  assert.equal(failed.run({ record: record(3), onWritten: code => { failedCode = code; } }), 3);
  assert.equal(failedCode, 3, 'a record that says 3 is exit 3 after a stop too');
});

test('A_stale_stamp_lock_with_stray_files_is_retired_and_a_live_lock_refuses_the_stamp', async t => {
  const dead = spawnSync(process.execPath, ['-e', 'process.stdout.write(String(process.pid))'], { encoding: 'utf8' }).stdout.trim();
  const stale = setup(t);
  fs.writeFileSync(path.join(stale.inputsDir, '.stamp.lock'), dead);
  fs.writeFileSync(path.join(stale.inputsDir, `specs.json.stamp-${dead}`), 'half');
  fs.writeFileSync(path.join(stale.inputsDir, 'specs.d', `exec-layer.json.restore-${dead}`), 'half');
  assert.equal(stale.run(), 0);
  assert.match(stale.said.join('\n'), new RegExp(`retired the stale stamp lock of pid ${dead} and 2 stray file\\(s\\)`));
  assert.deepEqual(stale.strays(), [], 'the lock and the stray files are gone');
  assert.match(stale.pins().layer, /probedOn/, 'and the stamp went on');
  const live = setup(t);
  const before = live.pins();
  fs.writeFileSync(path.join(live.inputsDir, '.stamp.lock'), String(process.pid)); // this test process is alive
  assert.equal(live.run(), 2);
  const written = JSON.parse(fs.readFileSync(live.file, 'utf8'));
  assert.equal(written.exit.code, 2);
  assert.match(written.exit.stampError, new RegExp(`another probe holds the stamp lock \\(pid ${process.pid}\\)`));
  assert.deepEqual(live.pins(), before);
  assert.equal(fs.existsSync(path.join(live.inputsDir, '.stamp.lock')), true, 'a live holder\'s lock is not touched');
});

test('No_stamp_is_made_for_an_exit_other_than_0_or_with_no_specs_and_the_record_is_still_written', t => {
  for (const [rec, noSpecs] of [[record(3), false], [record(0), true]]) {
    const s = setup(t);
    const before = s.pins();
    assert.equal(s.run({ record: rec, noSpecs }), rec.exit.code);
    assert.deepEqual(s.pins(), before);
    assert.equal(fs.existsSync(s.file), true);
    assert.deepEqual(s.strays(), []);
  }
  assert.equal(shouldStampProbedOn(record()), true);
});

test('The_probe_command_ends_through_endOfProbe_and_takes_no_second_path', () => {
  const source = fs.readFileSync(fileURLToPath(new URL('../exec-probe.mjs', import.meta.url)), 'utf8');
  assert.match(source, /endOfProbe\(/);
  assert.equal(source.includes('writeRecord'), false, 'the command writes no record by itself');
});
