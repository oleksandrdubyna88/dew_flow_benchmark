// D2 (fix batch 9): every temporary file of the probe lives in a `.exec-probe-tmp/` folder BESIDE its target (the same file system, so the rename still works): the lock's link file in `inputs/.exec-probe-tmp/`, the
// pins' stamp and put-back files in `inputs/specs.d/.exec-probe-tmp/` and `inputs/.exec-probe-tmp/`, the record's file in `<out dir>/.exec-probe-tmp/`. The name carries `<pid>-<start time>`. The start sweep lists ONLY those
// folders (never `inputs/` or a shared `--out` folder) and removes the files of dead probes (a dead pid, or a live pid with another start time: pid reuse).
import assert from 'node:assert/strict';
import * as fs from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { endOfProbe } from '../lib/exec-probe/command-end.mjs';
import { makeReservation, reserveRecord } from '../lib/exec-probe/record-path.mjs';
import { ownId, parseTmpName, TMP_DIR, tmpPath } from '../lib/exec-probe/tmp-files.mjs';
import { retireStartupStrays } from '../lib/exec-probe/strays.mjs';
import { checkRecord } from '../lib/specs-record.mjs';
import { fileURLToPath } from 'node:url';
import { owned } from './support/owned.mjs';

const REAL_INPUTS = fileURLToPath(new URL('../inputs', import.meta.url));
const record = () => ({ probedOn: '2026-11-03T00:00:00.000Z', host: { hostname: 'h' }, lines: [{ id: 'a', ok: true }], exit: { code: 0, halt: null } });

function setup(t) {
  const root = owned.ownedDir(t, 'tmpf');
  const inputsDir = path.join(root, 'inputs');
  fs.mkdirSync(path.join(inputsDir, 'specs.d'), { recursive: true });
  for (const name of fs.readdirSync(path.join(REAL_INPUTS, 'specs.d'))) fs.copyFileSync(path.join(REAL_INPUTS, 'specs.d', name), path.join(inputsDir, 'specs.d', name));
  fs.copyFileSync(path.join(REAL_INPUTS, 'specs.json'), path.join(inputsDir, 'specs.json'));
  const stageDir = path.join(root, 'stage');
  fs.mkdirSync(stageDir);
  const recordsDir = path.join(root, 'out');
  const reservation = makeReservation(reserveRecord(path.join(recordsDir, 'exec-probe-x.json')));
  return { inputsDir, stageDir, recordsDir, reservation, file: reservation.file };
}

test('A_temporary_file_name_carries_pid_and_start_time_and_lives_in_the_tmp_folder_beside_its_target', () => {
  assert.equal(TMP_DIR, '.exec-probe-tmp');
  assert.equal(tmpPath('/i/specs.json', 'stamp', '12-34'), '/i/.exec-probe-tmp/specs.json.stamp-12-34');
  assert.equal(tmpPath('/i/specs.d/exec-layer.json', 'restore', '12--'), '/i/specs.d/.exec-probe-tmp/exec-layer.json.restore-12--');
  assert.deepEqual(parseTmpName('specs.json.stamp-12-34'), { pid: 12, start: '34' });
  assert.deepEqual(parseTmpName('stamp.lock.link-7--'), { pid: 7, start: '-' });
  assert.equal(parseTmpName('specs.json'), null);
  assert.equal(parseTmpName('x.tmp-12'), null, 'the old names (no start time) are not this design\'s');
  assert.match(ownId(() => ({ state: 'live', start: 99 }), 5), /^5-99$/);
  assert.equal(ownId(() => ({ state: 'live' }), 5), '5--');
});

test('Every_temporary_file_of_a_stamping_probe_is_written_in_a_tmp_folder_beside_its_target_and_none_beside_the_target', t => {
  const s = setup(t);
  const written = [];
  const io = { ...fs, writeFileSync: (file, ...rest) => { written.push(String(file)); return fs.writeFileSync(file, ...rest); }, copyFileSync: fs.copyFileSync };
  assert.equal(endOfProbe({ record: record(), reservation: s.reservation, noSpecs: false, inputsDir: s.inputsDir, stageDir: s.stageDir, say: () => {}, report: () => {}, io }), 0);
  const temps = written.filter(file => /\.(link|stamp|restore|record)-\d+-/.test(file));
  assert.ok(temps.length >= 4, `the lock link, both stamp files and the record's file: ${temps.join(', ')}`);
  for (const file of temps) assert.equal(path.basename(path.dirname(file)), TMP_DIR, `${file} is in a ${TMP_DIR} folder`);
  const dirs = new Set(temps.map(file => path.dirname(path.dirname(file))));
  assert.deepEqual([...dirs].sort(), [s.inputsDir, path.join(s.inputsDir, 'specs.d'), s.recordsDir].sort(), 'beside inputs/, inputs/specs.d/ and the out folder');
  assert.deepEqual(checkRecord(s.inputsDir), { ok: true }, 'specs.d holds no stray folder after the stamp: the assembly still reads');
  for (const dir of dirs) assert.equal(fs.existsSync(path.join(dir, TMP_DIR)), false, `${dir}: the tmp folder is removed when empty`);
});

test('The_startup_sweep_lists_only_the_tmp_folders_and_removes_the_files_of_dead_probes_by_pid_and_start_time', t => {
  const inputs = owned.ownedDir(t, 'sweepin');
  const out = owned.ownedDir(t, 'sweepout');
  const specsD = path.join(inputs, 'specs.d');
  fs.mkdirSync(specsD);
  const probe = pid => (pid === 111 ? { state: 'dead' } : pid === 222 ? { state: 'live', start: 500 } : { state: 'live', start: 900 });
  const plant = (target, kind, id) => { const file = tmpPath(target, kind, id); fs.mkdirSync(path.dirname(file), { recursive: true }); fs.writeFileSync(file, 'x'); return file; };
  const dead = [plant(path.join(inputs, 'specs.json'), 'stamp', '111-5'), plant(path.join(specsD, 'exec-layer.json'), 'restore', '111-5'), plant(path.join(inputs, 'stamp.lock'), 'link', '111-5'), plant(path.join(out, 'exec-probe-x.json'), 'record', '111-5'),
    plant(path.join(out, 'exec-probe-y.json'), 'record', '222-400')]; // pid 222 is alive but started at 500, not 400: pid reuse
  const alive = [plant(path.join(inputs, 'specs.json'), 'stamp', '222-500'), plant(path.join(out, 'exec-probe-z.json'), 'record', '333-900')];
  const unrelated = [path.join(inputs, 'exec-probe-2026-11-03.json'), path.join(out, 'mine.json'), path.join(inputs, 'specs.json.stamp-111-5')]; // not in a tmp folder: never looked at
  unrelated.forEach(file => fs.writeFileSync(file, 'x'));
  const listed = [];
  const io = { ...fs, readdirSync: (d, ...rest) => { listed.push(String(d)); return fs.readdirSync(d, ...rest); } };
  const report = retireStartupStrays({ dirs: [inputs, specsD, out, inputs], probe, io });
  assert.deepEqual(report.removed.sort(), dead.sort(), 'the dead pid\'s files and the reused pid\'s file (another start time)');
  alive.forEach(file => assert.equal(fs.existsSync(file), true, `${file} belongs to a live probe`));
  unrelated.forEach(file => assert.equal(fs.existsSync(file), true, `${file} is outside a tmp folder`));
  assert.ok(listed.length > 0 && listed.every(dir => path.basename(dir) === TMP_DIR), `only tmp folders were listed: ${listed.join(', ')}`);
  assert.equal(fs.existsSync(path.join(specsD, TMP_DIR)), false, 'an emptied tmp folder is removed (specs.d must hold no stray)');
  assert.equal(fs.existsSync(path.join(inputs, TMP_DIR)), true, 'a tmp folder that still holds a live probe\'s file stays');
});

test('The_startup_sweep_never_throws_and_reports_a_tmp_folder_that_cannot_be_read', t => {
  const inputs = owned.ownedDir(t, 'sweeperr');
  fs.mkdirSync(path.join(inputs, TMP_DIR));
  const io = { ...fs, readdirSync: () => { throw Object.assign(new Error('EACCES: no'), { code: 'EACCES' }); } };
  const report = retireStartupStrays({ dirs: [inputs], probe: () => ({ state: 'dead' }), io });
  assert.match(report.errors[0], /EACCES/);
  assert.deepEqual(retireStartupStrays({ dirs: [path.join(inputs, 'absent')], probe: () => ({ state: 'dead' }) }), { removed: [], errors: [] }, 'a folder with no tmp folder is nothing');
});
