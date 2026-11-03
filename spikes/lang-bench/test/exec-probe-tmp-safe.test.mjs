// P3 (fix batch 11): no temporary write follows a symlink. Every temporary write goes through `writeTmp` with the flag `wx` (an existing name — a planted symlink included, even a dangling one — is refused, never opened), and
// `writeTmp` refuses a `.exec-probe-tmp` folder that fails the sweep's own check (a symlink, not a directory, not this user's). A refusal is the probe's exit 2 naming the folder.
import assert from 'node:assert/strict';
import * as fs from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { endOfProbe } from '../lib/exec-probe/command-end.mjs';
import { makeReservation, reserveRecord } from '../lib/exec-probe/record-path.mjs';
import { ownId, TMP_DIR, tmpPath, whyUnsafeFolder, writeTmp } from '../lib/exec-probe/tmp-files.mjs';
import { owned } from './support/owned.mjs';

const REAL_INPUTS = fileURLToPath(new URL('../inputs', import.meta.url));
const record = () => ({ probedOn: '2026-11-03T00:00:00.000Z', host: { hostname: 'h' }, lines: [{ id: 'a', ok: true }], exit: { code: 0, halt: null } });

function setup(t) {
  const root = owned.ownedDir(t, 'tmpsafe');
  const inputsDir = path.join(root, 'inputs');
  fs.mkdirSync(path.join(inputsDir, 'specs.d'), { recursive: true });
  for (const name of fs.readdirSync(path.join(REAL_INPUTS, 'specs.d'))) fs.copyFileSync(path.join(REAL_INPUTS, 'specs.d', name), path.join(inputsDir, 'specs.d', name));
  fs.copyFileSync(path.join(REAL_INPUTS, 'specs.json'), path.join(inputsDir, 'specs.json'));
  const stageDir = path.join(root, 'stage');
  fs.mkdirSync(stageDir);
  const recordsDir = path.join(root, 'out');
  const reservation = makeReservation(reserveRecord(path.join(recordsDir, 'exec-probe-x.json')));
  const victims = owned.ownedDir(t, 'tmpvictim');
  const said = [];
  const run = (noSpecs, io = fs) => endOfProbe({ record: record(), reservation, noSpecs, inputsDir, stageDir, say: text => said.push(text), report: () => {}, io });
  return { root, inputsDir, recordsDir, reservation, victims, said, run, file: reservation.file };
}

const escaped = text => text.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');

test('A_planted_symlink_at_the_records_temporary_name_is_never_written_through_and_the_probe_exits_2', t => {
  const s = setup(t);
  const target = path.join(s.victims, 'victim.json');
  const temp = tmpPath(s.file, 'record', ownId());
  fs.mkdirSync(path.dirname(temp), { recursive: true });
  fs.symlinkSync(target, temp); // dangling: a write that follows it would create the victim
  assert.equal(s.run(true), 2);
  assert.equal(fs.existsSync(target), false, 'nothing was written through the link');
  assert.match(s.said.join('\n'), /the record could not be written \(.*EEXIST/);
  assert.equal(fs.existsSync(target), false, 'and the victim is still not there after the cleanup (which removes the link, never what it points at)');
});

test('A_symlinked_tmp_folder_beside_the_record_is_refused_naming_the_folder_and_nothing_is_written_through_it', t => {
  const s = setup(t);
  fs.mkdirSync(s.recordsDir, { recursive: true });
  const folder = path.join(s.recordsDir, TMP_DIR);
  fs.symlinkSync(s.victims, folder);
  assert.equal(s.run(true), 2);
  assert.deepEqual(fs.readdirSync(s.victims), [], 'nothing was written through the linked folder');
  assert.match(s.said.join('\n'), new RegExp(`${escaped(folder)} is a symbolic link`), 'the message names the folder');
  assert.equal(fs.existsSync(s.file), false, 'the placeholder was released: no record was written');
  fs.rmSync(folder);
});

test('A_symlinked_tmp_folder_of_inputs_is_a_record_that_says_exit_2_naming_the_folder_and_stamps_nothing', t => {
  const s = setup(t);
  const folder = path.join(s.inputsDir, TMP_DIR);
  fs.symlinkSync(s.victims, folder);
  const before = fs.readFileSync(path.join(s.inputsDir, 'specs.json'), 'utf8');
  assert.equal(s.run(false), 2);
  const written = JSON.parse(fs.readFileSync(s.file, 'utf8'));
  assert.equal(written.exit.code, 2);
  assert.match(written.exit.stampError, new RegExp(`${escaped(folder)} is a symbolic link`));
  assert.deepEqual(fs.readdirSync(s.victims), [], 'nothing was written through the linked folder');
  assert.equal(fs.readFileSync(path.join(s.inputsDir, 'specs.json'), 'utf8'), before);
  fs.rmSync(folder);
});

test('The_tmp_file_writer_refuses_an_unsafe_folder_and_an_existing_name_and_writes_a_safe_one_with_wx', t => {
  const dir = owned.ownedDir(t, 'tmpwriter');
  const file = path.join(dir, TMP_DIR, 'x.json.record-1-1');
  writeTmp(file, 'one', undefined);
  assert.equal(fs.readFileSync(file, 'utf8'), 'one');
  assert.throws(() => writeTmp(file, 'two', undefined), /EEXIST/, 'an existing name is never opened: wx');
  assert.equal(whyUnsafeFolder(path.join(dir, TMP_DIR), process.getuid(), fs), null);
  assert.match(whyUnsafeFolder(path.join(dir, TMP_DIR), process.getuid() + 1, fs), /not owned by this user/);
  assert.throws(() => writeTmp(file, 'three', undefined, fs, process.getuid() + 1), /not owned by this user/, 'a folder of another user is refused');
});
