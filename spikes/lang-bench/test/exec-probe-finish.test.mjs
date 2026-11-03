// W3 and L3 (fix batches 4 and 5): the end of a probe. The stamp is PREPARED before the record is written (a failure is a record that says exit 2), the record is written, the reservation is taken only after the
// write succeeded, and the stamp is COMMITTED last; a failure of any step leaves both pins as they were and no empty record. The tests run on a real copy of `inputs/`.
import assert from 'node:assert/strict';
import * as fs from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { finishProbe } from '../lib/exec-probe/finish.mjs';
import { shouldStampProbedOn, writeRecord } from '../lib/exec-probe/probe.mjs';
import { makeReservation, reserveRecord } from '../lib/exec-probe/record-path.mjs';
import { checkRecord } from '../lib/specs-record.mjs';
import { owned } from './support/owned.mjs';

const REAL_INPUTS = fileURLToPath(new URL('../inputs', import.meta.url));
const record = (code = 0) => ({ probedOn: '2026-11-03T00:00:00.000Z', host: { hostname: 'h', kernel: 'k' }, lines: [{ id: 'a', ok: true }], exit: { code, halt: code === 0 ? null : 'memory-view-mismatch' } });

/** A real copy of `inputs/` (the pins only), a staging folder and an empty reservation. */
function setup(t) {
  const root = owned.ownedDir(t, 'finish');
  const inputsDir = path.join(root, 'inputs');
  fs.mkdirSync(path.join(inputsDir, 'specs.d'), { recursive: true });
  for (const name of fs.readdirSync(path.join(REAL_INPUTS, 'specs.d'))) fs.copyFileSync(path.join(REAL_INPUTS, 'specs.d', name), path.join(inputsDir, 'specs.d', name));
  fs.copyFileSync(path.join(REAL_INPUTS, 'specs.json'), path.join(inputsDir, 'specs.json'));
  const stageDir = path.join(root, 'stage');
  fs.mkdirSync(stageDir);
  const recordsDir = path.join(root, 'records');
  const reservation = makeReservation(reserveRecord(path.join(recordsDir, 'exec-probe-x.json')));
  const pins = () => ({ layer: fs.readFileSync(path.join(inputsDir, 'specs.d', 'exec-layer.json'), 'utf8'), specs: fs.readFileSync(path.join(inputsDir, 'specs.json'), 'utf8') });
  const stray = () => [...fs.readdirSync(path.join(inputsDir, 'specs.d')), ...fs.readdirSync(inputsDir)].filter(name => name.includes('.stamp-'));
  return { inputsDir, stageDir, reservation, pins, stray, recordsDir, file: reservation.file, stage: { inputsDir, stageDir } };
}

const noop = () => {};

test('The_stamp_is_prepared_before_the_record_is_written_and_committed_only_after_it', t => {
  const s = setup(t);
  const before = s.pins();
  const events = [];
  const result = finishProbe({
    record: record(), reservation: s.reservation, noSpecs: false, stage: s.stage, report: () => events.push('report'),
    write: (file, final, held) => {
      assert.deepEqual(s.pins(), before, 'while the record is being written both pins are as they were');
      assert.equal(s.stray().length, 2, 'the new contents wait in temporary files beside their targets: the stamp was prepared BEFORE the write');
      writeRecord(file, final, held);
      events.push('written');
    },
  });
  assert.equal(result.code, 0);
  assert.deepEqual(events, ['written', 'report']);
  const after = s.pins();
  assert.match(after.layer, /"probedOn": "2026-11-03T00:00:00.000Z"/);
  assert.deepEqual(checkRecord(s.inputsDir), { ok: true }, 'specs.json is the assembly of the stamped writers');
  assert.deepEqual(s.stray(), [], 'no temporary file is left');
  assert.equal(JSON.parse(fs.readFileSync(s.file, 'utf8')).exit.code, 0);
  assert.equal(s.reservation.held(), null, 'the reservation was taken after the write');
});

test('A_write_that_throws_releases_the_placeholder_discards_the_stamp_and_leaves_both_pins', t => {
  const s = setup(t);
  const before = s.pins();
  assert.throws(() => finishProbe({ record: record(), reservation: s.reservation, noSpecs: false, stage: s.stage, report: noop, write: () => { throw new Error('disk full'); } }), /disk full/);
  assert.equal(fs.existsSync(s.file), false, 'no empty exec-probe-…-N.json stays');
  assert.deepEqual(s.pins(), before);
  assert.deepEqual(s.stray(), []);
});

test('A_failing_second_stamp_file_gives_a_record_that_says_exit_2_and_leaves_both_pins', t => {
  const s = setup(t);
  const before = s.pins();
  let writes = 0;
  const io = { ...fs, writeFileSync: (file, ...rest) => { if (String(file).endsWith('specs.json.stamp-t')) throw new Error('no space for specs.json'); writes += 1; return fs.writeFileSync(file, ...rest); }, readFileSync: fs.readFileSync, cpSync: fs.cpSync, rmSync: fs.rmSync, renameSync: fs.renameSync };
  const result = finishProbe({ record: record(), reservation: s.reservation, noSpecs: false, stage: { ...s.stage, io, tag: 't' }, io, report: noop, write: writeRecord });
  const written = JSON.parse(fs.readFileSync(s.file, 'utf8'));
  assert.equal(result.code, 2);
  assert.deepEqual([written.exit.code, written.exit.halt, written.exit.stampError], [2, 'probed-on-stamp-failed', 'no space for specs.json'], 'the file says 2 with the error, like the command');
  assert.deepEqual(s.pins(), before, 'both pins stay as they were');
  assert.deepEqual(s.stray(), [], 'the first temporary file was removed');
  assert.ok(writes >= 1);
});

test('A_commit_that_fails_on_its_second_rename_puts_the_first_pin_back_and_says_exit_2', t => {
  const s = setup(t);
  const before = s.pins();
  let renames = 0;
  const io = { ...fs, renameSync: (from, to) => { renames += 1; if (String(to).endsWith('specs.json') && renames === 2) throw new Error('EXDEV'); return fs.renameSync(from, to); } };
  const result = finishProbe({ record: record(), reservation: s.reservation, noSpecs: false, stage: { ...s.stage }, io, report: noop, write: writeRecord });
  assert.equal(result.code, 2);
  assert.match(result.error, /both stay as they were/);
  assert.deepEqual(s.pins(), before, 'exec-layer.json was put back: neither pin changed');
  assert.deepEqual(s.stray(), []);
});

test('No_stamp_is_made_for_an_exit_other_than_0_or_with_no_specs_and_the_record_is_still_written', t => {
  for (const [rec, noSpecs] of [[record(3), false], [record(0), true]]) {
    const s = setup(t);
    const before = s.pins();
    const result = finishProbe({ record: rec, reservation: s.reservation, noSpecs, stage: s.stage, report: noop, write: writeRecord });
    assert.equal(result.code, rec.exit.code);
    assert.deepEqual(s.pins(), before);
    assert.equal(fs.existsSync(s.file), true);
  }
  assert.equal(shouldStampProbedOn(record()), true);
});

test('The_probe_command_ends_through_finishProbe_the_wiring_of_W3_and_L3', () => {
  const source = fs.readFileSync(fileURLToPath(new URL('../exec-probe.mjs', import.meta.url)), 'utf8');
  assert.match(source, /finishProbe\(\{[^}]*reservation: reservationHolder/, 'the command ends through the tested function');
  assert.doesNotMatch(source, /recordProbedOn|stampThenWrite|reservationHolder\.take\(\)/, 'no second, untested path: it neither stamps by itself nor takes the reservation before the write');
});
