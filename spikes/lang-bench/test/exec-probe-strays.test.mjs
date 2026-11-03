// L3 (fix batch 8): strays that no stale lock names. A probe that died before it took the lock (`.stamp.lock.link-<pid>`), that threw during a retire (`.stamp.lock.retire-<pid>`), or that died with its record's
// temporary file (`.exec-probe-record.tmp-<pid>`, also when it did not stamp and also beside an `--out` outside `inputs/`) leaves files no lock points to. At the probe's START, the folders it will use are swept for
// those three names of DEAD pids only; a retire file whose content says `committing` gets the MAY-be-stamped warning.
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import * as fs from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { retireStartupStrays } from '../lib/exec-probe/strays.mjs';
import { owned } from './support/owned.mjs';

const probe = p => (p === 111111 || p === 222222 ? { state: 'dead' } : { state: 'live', start: 5 });
const plant = (dir, names) => names.forEach(name => fs.writeFileSync(path.join(dir, name), 'x'));

test('The_startup_sweep_retires_the_three_stray_kinds_of_dead_pids_in_inputs_and_beside_an_out_file_and_leaves_live_pids_and_other_files', t => {
  const inputs = owned.ownedDir(t, 'strayin');
  const out = owned.ownedDir(t, 'strayout');
  const kinds = pid => [`.stamp.lock.link-${pid}`, `.stamp.lock.retire-${pid}`, `.exec-probe-record.tmp-${pid}`];
  plant(inputs, [...kinds(111111), ...kinds(333333), 'exec-probe-2026-11-03.json', '.stamp.lock', 'specs.json.stamp-111111', 'not-a-stray.tmp-111111']);
  plant(out, [...kinds(222222), ...kinds(333333), 'mine.json']);
  const report = retireStartupStrays({ dirs: [inputs, out, inputs], probe });
  assert.deepEqual(report.removed.map(file => path.basename(file)).sort(), [...kinds(111111), ...kinds(222222)].sort(), 'the dead pids\' three kinds, in both folders (the repeated folder is looked at once)');
  for (const dir of [inputs, out]) for (const name of kinds(333333)) assert.equal(fs.existsSync(path.join(dir, name)), true, `${name}: a live pid's file is not touched`);
  for (const name of ['exec-probe-2026-11-03.json', '.stamp.lock', 'specs.json.stamp-111111', 'not-a-stray.tmp-111111']) assert.equal(fs.existsSync(path.join(inputs, name)), true, `${name} is not one of the three kinds`);
  assert.equal(fs.existsSync(path.join(out, 'mine.json')), true);
  assert.deepEqual(report.committing, [], 'none said committing');
});

test('A_dead_probes_retire_file_that_says_committing_is_reported_so_the_may_be_stamped_warning_is_printed', t => {
  const inputs = owned.ownedDir(t, 'straycommit');
  fs.writeFileSync(path.join(inputs, '.stamp.lock.retire-111111'), '111111 5\ncommitting');
  fs.writeFileSync(path.join(inputs, '.stamp.lock.retire-222222'), '222222 5');
  const report = retireStartupStrays({ dirs: [inputs], probe });
  assert.equal(report.removed.length, 2);
  assert.deepEqual(report.committing.map(file => path.basename(file)), ['.stamp.lock.retire-111111']);
});

test('A_folder_that_cannot_be_listed_is_a_named_error_and_never_a_throw', t => {
  const report = retireStartupStrays({ dirs: [path.join(owned.ownedDir(t, 'straymissing'), 'absent')], probe });
  assert.deepEqual(report.removed, []);
  assert.equal(report.errors.length, 0, 'a folder that does not exist yet is nothing to sweep');
  const io = { ...fs, readdirSync: () => { throw Object.assign(new Error('EACCES: no'), { code: 'EACCES' }); } };
  const failing = retireStartupStrays({ dirs: [owned.ownedDir(t, 'strayerr')], probe, io });
  assert.match(failing.errors[0], /EACCES/);
});

test('The_probe_command_sweeps_the_strays_of_dead_probes_beside_its_out_file_at_its_start_even_when_it_then_refuses', t => {
  const dir = owned.ownedDir(t, 'straycli');
  const dead = spawnSync(process.execPath, ['-e', 'process.stdout.write(String(process.pid))'], { encoding: 'utf8' }).stdout.trim();
  fs.writeFileSync(path.join(dir, `.exec-probe-record.tmp-${dead}`), 'cut off');
  fs.writeFileSync(path.join(dir, 'x.json'), '{}\n'); // the out file exists: the probe refuses (exit 3) right after its start sweep
  const ran = spawnSync(process.execPath, [path.join(path.dirname(fileURLToPath(import.meta.url)), '..', 'exec-probe.mjs'), '--out', path.join(dir, 'x.json'), '--no-specs'], { encoding: 'utf8', timeout: 30_000 });
  assert.equal(ran.status, 3, ran.stderr);
  assert.match(ran.stderr, /retired 1 stray file\(s\) of dead probes/);
  assert.equal(fs.existsSync(path.join(dir, `.exec-probe-record.tmp-${dead}`)), false, 'the dead probe\'s temporary file beside the out file is retired');
  assert.equal(fs.readFileSync(path.join(dir, 'x.json'), 'utf8'), '{}\n', 'and the existing record is untouched');
});
