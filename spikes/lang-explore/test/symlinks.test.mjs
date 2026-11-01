// F1: the frozen-file restore never writes (or reads) through an agent's link. The agent owns its tree and can make `run.sh` a link to a host file, or `samples/` a link to a host directory; scoring then
// restores the frozen files, and a restore that followed the link would overwrite the HOST. Each test plants a sentinel outside the tree and asserts it is untouched.
import assert from 'node:assert/strict';
import { lstatSync, mkdirSync, readFileSync, rmSync, symlinkSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { pristineCopy } from '../lib/score.mjs';
import { frozenChanges, pathKind, restoreFrozen } from '../lib/workspace.mjs';

const FROZEN = {
  'run.sh': { bytes: Buffer.from('exec true\n'), mode: 0o755 },
  'samples/s01.in': { bytes: Buffer.from('1\n'), mode: 0o644 },
  'runner/run_samples.py': { bytes: Buffer.from('# judge\n'), mode: 0o644 },
};
const SENTINEL = 'HOST FILE: must never change\n';
const read = file => readFileSync(file, 'utf8');

/** An agent tree holding the frozen files, and a host directory with sentinels. */
function rig() {
  const base = tmpDir('symlink');
  const host = path.join(base, 'host');
  mkdirSync(path.join(host, 'dir'), { recursive: true });
  writeFileSync(path.join(host, 'sentinel.txt'), SENTINEL);
  writeFileSync(path.join(host, 'dir', 's01.in'), SENTINEL);
  writeFileSync(path.join(host, 'dir', 'extra'), SENTINEL);
  const source = path.join(base, 'agent');
  for (const [rel, f] of Object.entries(FROZEN)) { mkdirSync(path.dirname(path.join(source, rel)), { recursive: true }); writeFileSync(path.join(source, rel), f.bytes, { mode: f.mode }); }
  mkdirSync(path.join(base, 'copy'));
  return { host, source, dest: path.join(base, 'copy', 'tree') };
}

test('A_run_sh_that_is_a_link_to_a_host_file_is_replaced_in_the_copy_and_the_host_file_is_untouched', () => {
  const r = rig();
  rmSync(path.join(r.source, 'run.sh'));
  symlinkSync(path.join(r.host, 'sentinel.txt'), path.join(r.source, 'run.sh'));
  const changed = pristineCopy({ source: r.source, dest: r.dest, stack: 'node', files: FROZEN });
  assert.equal(read(path.join(r.host, 'sentinel.txt')), SENTINEL, 'the host file is unchanged');
  assert.deepEqual(changed, [{ path: 'run.sh', state: 'changed' }]);
  assert.equal(lstatSync(path.join(r.dest, 'run.sh')).isFile(), true, 'a regular file again, not a link');
  assert.equal(read(path.join(r.dest, 'run.sh')), 'exec true\n');
});

test('A_samples_directory_that_is_a_link_to_a_host_directory_is_replaced_and_nothing_of_the_host_changes', () => {
  const r = rig();
  rmSync(path.join(r.source, 'samples'), { recursive: true });
  symlinkSync(path.join(r.host, 'dir'), path.join(r.source, 'samples'));
  const changed = pristineCopy({ source: r.source, dest: r.dest, stack: 'node', files: FROZEN });
  assert.equal(read(path.join(r.host, 'dir', 's01.in')), SENTINEL, 'the file the link led to is unchanged');
  assert.equal(read(path.join(r.host, 'dir', 'extra')), SENTINEL);
  assert.deepEqual(changed, [{ path: 'samples/s01.in', state: 'changed' }], 'a path through a link is changed, and the host file was not read as if it were the frozen one');
  assert.equal(lstatSync(path.join(r.dest, 'samples')).isDirectory(), true, 'a real directory again');
  assert.equal(read(path.join(r.dest, 'samples', 's01.in')), '1\n');
});

test('A_dangling_link_and_a_link_at_a_directory_on_the_way_are_replaced_too', () => {
  const r = rig();
  rmSync(path.join(r.source, 'runner'), { recursive: true });
  symlinkSync(path.join(r.host, 'not-there'), path.join(r.source, 'runner'));
  rmSync(path.join(r.source, 'run.sh'));
  symlinkSync(path.join(r.host, 'also-not-there'), path.join(r.source, 'run.sh'));
  pristineCopy({ source: r.source, dest: r.dest, stack: 'node', files: FROZEN });
  assert.equal(lstatSync(path.join(r.host)).isDirectory(), true);
  assert.throws(() => lstatSync(path.join(r.host, 'not-there')), 'nothing was created at the link targets');
  assert.throws(() => lstatSync(path.join(r.host, 'also-not-there')));
  assert.equal(read(path.join(r.dest, 'runner', 'run_samples.py')), '# judge\n');
  assert.equal(read(path.join(r.dest, 'run.sh')), 'exec true\n');
});

test('A_frozen_file_replaced_by_a_directory_or_a_link_to_identical_bytes_is_reported_changed_never_read_through', () => {
  const r = rig();
  writeFileSync(path.join(r.host, 'same.sh'), 'exec true\n');
  rmSync(path.join(r.source, 'run.sh'));
  symlinkSync(path.join(r.host, 'same.sh'), path.join(r.source, 'run.sh'));
  assert.deepEqual(frozenChanges({ files: FROZEN, work: r.source }), [{ path: 'run.sh', state: 'changed' }], 'a link whose target has the right bytes is still not the frozen file');
  assert.equal(pathKind(r.source, 'run.sh'), 'odd');
  assert.equal(pathKind(r.source, 'samples/s01.in'), 'plain');
  assert.equal(pathKind(r.source, 'nope/x'), 'absent');
  rmSync(path.join(r.source, 'run.sh'));
  mkdirSync(path.join(r.source, 'run.sh'));
  restoreFrozen({ root: r.source, rel: 'run.sh', file: FROZEN['run.sh'] });
  assert.equal(lstatSync(path.join(r.source, 'run.sh')).isFile(), true, 'a directory in the file\'s place is removed');
});

test('An_untouched_tree_is_copied_with_no_change_reported_and_the_modes_kept', () => {
  const r = rig();
  const changed = pristineCopy({ source: r.source, dest: r.dest, stack: 'node', files: FROZEN });
  assert.deepEqual(changed, []);
  assert.equal(lstatSync(path.join(r.dest, 'run.sh')).mode & 0o777, 0o755);
});
