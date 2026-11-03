// D2 (fix batch 9): every temporary file of the probe lives in a `.exec-probe-tmp/` folder BESIDE its target (the same file system, so the rename still works): the lock's link file in `inputs/.exec-probe-tmp/`, the
// pins' stamp and put-back files BOTH in `inputs/.exec-probe-tmp/` (N1, fix batch 10: `specs.d` never holds a temporary entry), the record's file in `<out dir>/.exec-probe-tmp/`. The name carries `<pid>-<start time>`. The start sweep lists ONLY those
// folders (never `inputs/` or a shared `--out` folder) and removes the files of dead probes (a dead pid, or a live pid with another start time: pid reuse).
import assert from 'node:assert/strict';
import * as fs from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { endOfProbe } from '../lib/exec-probe/command-end.mjs';
import { makeReservation, reserveRecord } from '../lib/exec-probe/record-path.mjs';
import { besideTmpPath, ownId, parseTmpName, pinTmpPath, TMP_DIR, tmpPath } from '../lib/exec-probe/tmp-files.mjs';
import { retireStartupStrays } from '../lib/exec-probe/strays.mjs';
import { checkRecord } from '../lib/specs-record.mjs';
import { fileURLToPath } from 'node:url';
import { owned } from './support/owned.mjs';

const REAL_INPUTS = fileURLToPath(new URL('../inputs', import.meta.url));
const record = () => ({ probedOn: '2026-11-03T00:00:00.000Z', host: { hostname: 'h' }, lines: [{ id: 'a', ok: true }], exit: { code: 0, halt: null } });

const escaped = text => text.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');

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
  assert.equal(besideTmpPath('/i/specs.json', 'stamp', '12-34'), '/i/.exec-probe-tmp/specs.json.stamp-12-34');
  assert.equal(tmpPath('/i/specs.d/exec-layer.json', 'restore', '12--', '/i'), '/i/.exec-probe-tmp/exec-layer.json.restore-12--', 'N1: a pin of specs.d has its temporary file in the tmp folder of inputs/');
  assert.equal(besideTmpPath('/i/out/r.json', 'record', '12--'), '/i/out/.exec-probe-tmp/r.json.record-12--', 'a record\'s file is beside its target');
  assert.throws(() => tmpPath('/i/specs.d/exec-layer.json', 'restore', '12--'), /base folder/, 'P4: tmpPath has no default that places a pin\'s temporary beside it');
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
  const io = { ...fs, writeFileSync: (file, ...rest) => { written.push(String(file)); return fs.writeFileSync(file, ...rest); }, mkdirSync: (dir, ...rest) => { written.push(`mkdir ${dir}`); return fs.mkdirSync(dir, ...rest); } };
  assert.equal(endOfProbe({ record: record(), reservation: s.reservation, noSpecs: false, inputsDir: s.inputsDir, stageDir: s.stageDir, say: () => {}, report: () => {}, io }), 0);
  assert.deepEqual(written.filter(entry => entry.startsWith('mkdir ') && entry.includes(path.join(s.inputsDir, 'specs.d'))), [], 'nor makes a folder in specs.d');
  const temps = written.filter(file => /\.(link|stamp|restore|record)-\d+-/.test(file));
  assert.ok(temps.length >= 4, `the lock link, both stamp files and the record's file: ${temps.join(', ')}`);
  for (const file of temps) assert.equal(path.basename(path.dirname(file)), TMP_DIR, `${file} is in a ${TMP_DIR} folder`);
  const dirs = new Set(temps.map(file => path.dirname(path.dirname(file))));
  assert.deepEqual([...dirs].sort(), [s.inputsDir, s.recordsDir].sort(), 'N1: beside inputs/ and the out folder only: specs.d never holds a temporary entry');
  assert.deepEqual(written.filter(file => file.startsWith(path.join(s.inputsDir, 'specs.d'))), [], 'the stamp writes nothing under specs.d (the pin itself is renamed into place)');
  assert.deepEqual(checkRecord(s.inputsDir), { ok: true }, 'specs.d holds no stray folder after the stamp: the assembly still reads');
  for (const dir of dirs) assert.equal(fs.existsSync(path.join(dir, TMP_DIR)), false, `${dir}: the tmp folder is removed when empty`);
});

test('The_startup_sweep_lists_only_the_tmp_folders_and_removes_the_files_of_dead_probes_by_pid_and_start_time', t => {
  const inputs = owned.ownedDir(t, 'sweepin');
  const out = owned.ownedDir(t, 'sweepout');
  const specsD = path.join(inputs, 'specs.d');
  fs.mkdirSync(specsD);
  const probe = pid => (pid === 111 ? { state: 'dead' } : pid === 222 ? { state: 'live', start: 500 } : { state: 'live', start: 900 });
  const plant = (target, kind, id) => { const file = besideTmpPath(target, kind, id); fs.mkdirSync(path.dirname(file), { recursive: true }); fs.writeFileSync(file, 'x'); return file; };
  const dead = [plant(path.join(inputs, 'specs.json'), 'stamp', '111-5'), plant(path.join(inputs, 'exec-layer.json'), 'restore', '111-5'), plant(path.join(inputs, 'stamp.lock'), 'link', '111-5'), plant(path.join(inputs, '.stamp.lock'), 'mark', '111-5'), // P2: the committing mark's temporary file of a dead probe
    plant(path.join(out, 'exec-probe-x.json'), 'record', '111-5'),
    plant(path.join(out, 'exec-probe-y.json'), 'record', '222-400')]; // pid 222 is alive but started at 500, not 400: pid reuse
  const alive = [plant(path.join(inputs, 'specs.json'), 'stamp', '222-500'), plant(path.join(out, 'exec-probe-z.json'), 'record', '333-900')];
  const unrelated = [path.join(inputs, 'exec-probe-2026-11-03.json'), path.join(out, 'mine.json'), path.join(inputs, 'specs.json.stamp-111-5')]; // not in a tmp folder: never looked at
  unrelated.forEach(file => fs.writeFileSync(file, 'x'));
  const listed = [];
  const io = { ...fs, readdirSync: (d, ...rest) => { listed.push(String(d)); return fs.readdirSync(d, ...rest); } };
  const report = retireStartupStrays({ dirs: [inputs, out, inputs], probe, io });
  assert.deepEqual(report.removed.sort(), dead.sort(), 'the dead pid\'s files and the reused pid\'s file (another start time)');
  alive.forEach(file => assert.equal(fs.existsSync(file), true, `${file} belongs to a live probe`));
  unrelated.forEach(file => assert.equal(fs.existsSync(file), true, `${file} is outside a tmp folder`));
  assert.ok(listed.length > 0 && listed.every(dir => path.basename(dir) === TMP_DIR), `only tmp folders were listed: ${listed.join(', ')}`);
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

// N1 (Opus LOW 3): a probe killed in the middle of a stamp leaves its files in `inputs/.exec-probe-tmp/` and nowhere in `specs.d`, so `specs-record --check` still reads the assembly; the next start's sweep removes them.
test('A_killed_probes_leftover_in_the_tmp_folder_of_inputs_leaves_the_record_check_ok_and_the_next_sweep_removes_it', t => {
  const s = setup(t);
  const left = [besideTmpPath(path.join(s.inputsDir, 'specs.json'), 'stamp', '999999-1'), tmpPath(path.join(s.inputsDir, 'specs.d', 'exec-layer.json'), 'stamp', '999999-1', s.inputsDir)];
  fs.mkdirSync(path.dirname(left[0]), { recursive: true });
  left.forEach(file => fs.writeFileSync(file, 'half'));
  assert.deepEqual(left.map(file => path.dirname(file)), [path.join(s.inputsDir, TMP_DIR), path.join(s.inputsDir, TMP_DIR)], 'both pins\' files are in the tmp folder of inputs/');
  assert.deepEqual(fs.readdirSync(path.join(s.inputsDir, 'specs.d')).filter(name => name.startsWith('.')), [], 'specs.d holds nothing of the leftover');
  assert.deepEqual(checkRecord(s.inputsDir), { ok: true }, 'the check still passes with the leftover');
  const report = retireStartupStrays({ dirs: [s.inputsDir], probe: () => ({ state: 'dead' }) });
  assert.deepEqual(report.removed.sort(), left.sort());
  assert.equal(fs.existsSync(path.join(s.inputsDir, TMP_DIR)), false, 'and the emptied folder');
});

// N2 (coai 0): the sweep never follows a symlink.
test('The_startup_sweep_skips_a_tmp_folder_that_is_a_symlink_or_not_owned_and_a_file_that_is_not_regular_and_names_each', t => {
  const inputs = owned.ownedDir(t, 'sweeplink');
  const elsewhere = owned.ownedDir(t, 'sweepelse');
  const victim = path.join(elsewhere, 'specs.json.stamp-111-5');
  fs.writeFileSync(victim, 'not yours to remove');
  fs.symlinkSync(elsewhere, path.join(inputs, TMP_DIR));
  const dead = () => ({ state: 'dead' });
  const linked = retireStartupStrays({ dirs: [inputs], probe: dead });
  assert.equal(fs.existsSync(victim), true, 'the file behind a symlinked tmp folder survives');
  assert.deepEqual(linked.removed, []);
  assert.match(linked.errors.join('\n'), new RegExp(`${path.join(inputs, TMP_DIR).replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}.*symbolic link`), 'the message names the link');
  const own = owned.ownedDir(t, 'sweepown');
  const real = besideTmpPath(path.join(own, 'specs.json'), 'stamp', '111-5');
  fs.mkdirSync(path.dirname(real));
  fs.writeFileSync(real, 'x');
  const foreign = retireStartupStrays({ dirs: [own], probe: dead, uid: process.getuid() + 1 });
  assert.equal(fs.existsSync(real), true, 'a tmp folder of another user is skipped');
  assert.match(foreign.errors.join('\n'), /not owned by this user/);
  fs.rmSync(real);
  const link = besideTmpPath(path.join(own, 'specs.json'), 'restore', '111-5');
  fs.symlinkSync(victim, link); // a dead pid's name, but a link inside a real folder
  const inside = retireStartupStrays({ dirs: [own], probe: dead });
  assert.equal(fs.existsSync(victim), true, 'the target of a link inside the folder survives');
  assert.equal(fs.lstatSync(link).isSymbolicLink(), true, 'the link itself is skipped, not removed');
  assert.match(inside.errors.join('\n'), new RegExp(`${escaped(link)} is not a regular file: skipped`), 'P5: the message reads `<file> is not a regular file: skipped`, no colon after the path');
});

// P4 (coai 0): the pins' temporaries go through ONE helper that always uses `inputs/.exec-probe-tmp/`.
test('The_pin_temporary_helper_never_returns_a_path_under_specs_d_for_any_pin_or_kind', () => {
  for (const pin of ['specs.d/exec-layer.json', 'specs.json']) {
    for (const kind of ['stamp', 'restore']) {
      const file = pinTmpPath('/i', pin, kind, '7-9');
      assert.equal(file, `/i/.exec-probe-tmp/${path.basename(pin)}.${kind}-7-9`);
      assert.equal(file.includes('/specs.d/'), false, `${file} is not under specs.d`);
      assert.deepEqual(parseTmpName(path.basename(file)), { pid: 7, start: '9' });
    }
  }
});
