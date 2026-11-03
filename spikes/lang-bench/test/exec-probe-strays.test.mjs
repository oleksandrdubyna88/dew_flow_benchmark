// L3 and D2 (fix batches 8 and 9): the strays of dead probes, swept at the probe's START. Since D2 every temporary file lives in a `.exec-probe-tmp/` folder beside its target, so the sweep lists ONLY those
// folders (the unit tests of the sweep are in `exec-probe-tmp.test.mjs`); this file keeps the command-level tests and the tests that were written for the first design and are REWRITTEN for it:
//   The_startup_sweep_retires_the_three_stray_kinds_of_dead_pids_in_inputs_and_beside_an_out_file_and_leaves_live_pids_and_other_files
//     -> The_startup_sweep_retires_the_temporary_files_of_dead_probes_in_the_tmp_folders_beside_inputs_and_an_out_file_and_leaves_live_probes_and_other_files
//   A_dead_probes_retire_file_that_says_committing_is_reported_so_the_may_be_stamped_warning_is_printed
//     -> A_file_named_like_an_old_stray_beside_inputs_is_never_listed_or_touched   (there are no retire files any more: D1; the MAY warning belongs to the refusal of a stale lock)
//   The_probe_command_sweeps_the_strays_of_dead_probes_beside_its_out_file_at_its_start_even_when_it_then_refuses
//     -> The_probe_command_sweeps_the_tmp_folder_beside_its_out_file_at_its_start_even_when_it_then_refuses_and_never_touches_the_real_inputs   (it uses the `--inputs-dir` seam)
//   A_folder_that_cannot_be_listed_is_a_named_error_and_never_a_throw   (same name, adapted to the tmp folder)
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import * as fs from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { retireStartupStrays } from '../lib/exec-probe/strays.mjs';
import { tmpPath, TMP_DIR } from '../lib/exec-probe/tmp-files.mjs';
import { owned } from './support/owned.mjs';

const probe = p => (p === 111111 || p === 222222 ? { state: 'dead' } : { state: 'live', start: 5 });
const plant = (target, kind, id) => { const file = tmpPath(target, kind, id); fs.mkdirSync(path.dirname(file), { recursive: true }); fs.writeFileSync(file, 'x'); return file; };

test('The_startup_sweep_retires_the_temporary_files_of_dead_probes_in_the_tmp_folders_beside_inputs_and_an_out_file_and_leaves_live_probes_and_other_files', t => {
  const inputs = owned.ownedDir(t, 'strayin');
  const out = owned.ownedDir(t, 'strayout');
  const dead = [plant(path.join(inputs, 'stamp.lock'), 'link', '111111-5'), plant(path.join(inputs, 'specs.json'), 'stamp', '111111-5'), plant(path.join(out, 'exec-probe-a.json'), 'record', '222222-5')];
  const alive = [plant(path.join(inputs, 'specs.json'), 'restore', '333333-5'), plant(path.join(out, 'exec-probe-b.json'), 'record', '333333-5')];
  const other = [path.join(inputs, 'exec-probe-2026-11-03.json'), path.join(out, 'mine.json')];
  other.forEach(file => fs.writeFileSync(file, 'x'));
  const report = retireStartupStrays({ dirs: [inputs, out, inputs], probe });
  assert.deepEqual(report.removed.sort(), dead.sort());
  alive.forEach(file => assert.equal(fs.existsSync(file), true, `${file}: a live probe's file`));
  other.forEach(file => assert.equal(fs.existsSync(file), true, `${file}: not a temporary file`));
});

test('A_file_named_like_an_old_stray_beside_inputs_is_never_listed_or_touched', t => {
  const inputs = owned.ownedDir(t, 'strayold');
  const old = ['.stamp.lock.retire-111111', '.stamp.lock.link-111111', '.exec-probe-record.tmp-111111', 'specs.json.stamp-111111'].map(name => path.join(inputs, name));
  old.forEach(file => fs.writeFileSync(file, 'x'));
  const listed = [];
  const io = { ...fs, readdirSync: (d, ...rest) => { listed.push(String(d)); return fs.readdirSync(d, ...rest); } };
  const report = retireStartupStrays({ dirs: [inputs], probe, io });
  assert.deepEqual(report, { removed: [], errors: [] });
  assert.ok(listed.every(dir => path.basename(dir) === TMP_DIR), `inputs/ itself is never listed, only its tmp folder: ${listed.join(', ')}`);
  old.forEach(file => assert.equal(fs.existsSync(file), true));
});

test('A_folder_that_cannot_be_listed_is_a_named_error_and_never_a_throw', t => {
  const inputs = owned.ownedDir(t, 'straymissing');
  fs.mkdirSync(path.join(inputs, TMP_DIR));
  const io = { ...fs, readdirSync: () => { throw Object.assign(new Error('EACCES: no'), { code: 'EACCES' }); } };
  const failing = retireStartupStrays({ dirs: [inputs], probe, io });
  assert.match(failing.errors[0], /EACCES/);
  assert.equal(retireStartupStrays({ dirs: [path.join(inputs, 'absent')], probe }).errors.length, 0, 'a folder with no tmp folder is nothing to sweep');
});

test('The_probe_command_sweeps_the_tmp_folder_beside_its_out_file_at_its_start_even_when_it_then_refuses_and_never_touches_the_real_inputs', t => {
  const dir = owned.ownedDir(t, 'straycli');
  const inputs = owned.ownedDir(t, 'straycliin'); // the seam: the sweep and the stamp use this folder, not the real inputs/
  const dead = spawnSync(process.execPath, ['-e', 'process.stdout.write(String(process.pid))'], { encoding: 'utf8' }).stdout.trim();
  const stray = plant(path.join(dir, 'x.json'), 'record', `${dead}-5`);
  fs.writeFileSync(path.join(dir, 'x.json'), '{}\n'); // the out file exists: the probe refuses (exit 3) right after its start sweep
  const ran = spawnSync(process.execPath, [path.join(path.dirname(fileURLToPath(import.meta.url)), '..', 'exec-probe.mjs'), '--out', path.join(dir, 'x.json'), '--inputs-dir', inputs, '--no-specs'], { encoding: 'utf8', timeout: 30_000 });
  assert.equal(ran.status, 3, ran.stderr);
  assert.match(ran.stderr, /retired 1 temporary file\(s\) of dead probes/);
  assert.equal(fs.existsSync(stray), false, 'the dead probe\'s file in the out folder\'s tmp folder is retired');
  assert.equal(fs.existsSync(path.join(dir, TMP_DIR)), false, 'and its emptied tmp folder');
  assert.equal(fs.readFileSync(path.join(dir, 'x.json'), 'utf8'), '{}\n', 'the existing record is untouched');
});
