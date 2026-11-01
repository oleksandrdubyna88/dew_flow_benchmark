// The runner's pin (E1.S8a): version, per-file sha256 and one manifest hash in inputs/specs.d/runner.json, and the check S3 reuses.
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { mkdirSync, readdirSync, readFileSync, symlinkSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { computePin, manifestText, verifyPin } from '../lib/runner-pin.mjs';
import { owned } from './support/owned.mjs';

const spikeDir = fileURLToPath(new URL('..', import.meta.url));
const sha = text => createHash('sha256').update(text).digest('hex');

/** A runner folder with the given files, in an owned folder removed when the test ends. */
function runnerFolder(t, files) {
  const dir = path.join(owned.ownedDir(t, 'pin'), 'runner');
  mkdirSync(dir);
  for (const [name, text] of Object.entries(files)) writeFileSync(path.join(dir, name), text);
  return dir;
}

const TREE = { 'a.py': 'print(1)\n', 'b.py': 'x = 2\n', 'rs_report.py': 'RUNNER_VERSION = "3.4.5"\n' };

test('The_runner_pin_lists_every_python_file_sorted_with_its_sha256_and_the_version_read_from_the_runner', t => {
  const pin = computePin(runnerFolder(t, TREE));
  assert.equal(pin.ok, true, pin.reason);
  assert.deepEqual(pin.value.runner.files, [
    { path: 'a.py', sha256: sha('print(1)\n') }, { path: 'b.py', sha256: sha('x = 2\n') },
    { path: 'rs_report.py', sha256: sha('RUNNER_VERSION = "3.4.5"\n') },
  ]);
  assert.equal(pin.value.runner.version, '3.4.5');
});

test('The_files_are_listed_in_byte_order_whatever_order_the_file_system_returns_them_in', t => {
  const dir = runnerFolder(t, TREE);
  const reversed = folder => readdirSync(folder).sort().reverse();
  const listed = computePin(dir, reversed).value.runner.files.map(f => f.path);
  assert.deepEqual(listed, ['a.py', 'b.py', 'rs_report.py']);
});

test('The_manifest_hash_is_the_sha256_of_the_sha256sum_text_of_the_files', t => {
  const pin = computePin(runnerFolder(t, TREE)).value.runner;
  const text = pin.files.map(f => `${f.sha256}  runner/${f.path}\n`).join('');
  assert.equal(manifestText(pin.files), text);
  assert.equal(pin.manifestSha256, sha(text));
});

test('A_single_changed_byte_changes_the_manifest_hash_and_the_check_names_the_file', t => {
  const dir = runnerFolder(t, TREE);
  const pinned = computePin(dir).value.runner;
  writeFileSync(path.join(dir, 'b.py'), 'x = 3\n');
  const now = computePin(dir).value.runner;
  assert.notEqual(now.manifestSha256, pinned.manifestSha256);
  const verdict = verifyPin(dir, { runner: pinned });
  assert.equal(verdict.ok, false);
  assert.match(verdict.reason, /b\.py/);
});

test('A_file_added_or_removed_after_the_pin_fails_the_check', t => {
  const dir = runnerFolder(t, TREE);
  const pinned = computePin(dir).value.runner;
  writeFileSync(path.join(dir, 'c.py'), '\n');
  assert.match(verifyPin(dir, { runner: pinned }).reason, /c\.py/);
});

test('The_check_passes_for_the_tree_it_was_taken_from', t => {
  const dir = runnerFolder(t, TREE);
  assert.equal(verifyPin(dir, computePin(dir).value).ok, true);
});

test('A_python_cache_folder_is_refused_because_an_unchecked_cache_would_run_instead_of_the_pinned_source', t => {
  const dir = runnerFolder(t, TREE);
  assert.equal(computePin(dir).ok, true);
  mkdirSync(path.join(dir, '__pycache__'));
  writeFileSync(path.join(dir, '__pycache__', 'a.cpython-314.pyc'), 'x');
  const planted = computePin(dir);
  assert.equal(planted.ok, false);
  assert.match(planted.reason, /__pycache__/);
  assert.equal(verifyPin(dir, computePin(runnerFolder(t, TREE)).value).ok, false, 'a planted cache fails the check the workspace copy is held to');
});

test('A_stray_file_that_is_not_python_is_refused', t => {
  const dir = runnerFolder(t, TREE);
  writeFileSync(path.join(dir, 'notes.txt'), 'x');
  const pin = computePin(dir);
  assert.equal(pin.ok, false);
  assert.match(pin.reason, /notes\.txt/);
});

test('A_symbolic_link_in_the_runner_folder_is_refused_because_the_pinned_tree_is_files_only', t => {
  const dir = runnerFolder(t, TREE);
  symlinkSync('a.py', path.join(dir, 'link.py'));
  const pin = computePin(dir);
  assert.equal(pin.ok, false);
  assert.match(pin.reason, /link\.py/);
});

test('A_runner_without_a_version_or_without_files_is_refused', t => {
  assert.equal(computePin(runnerFolder(t, { 'a.py': '\n' })).ok, false);
  assert.equal(computePin(runnerFolder(t, {})).ok, false);
});

test('The_committed_runner_pin_equals_the_runner_tree_so_a_runner_edit_without_a_new_pin_fails_here', () => {
  const recordPath = path.join(spikeDir, 'inputs', 'specs.d', 'runner.json');
  const record = JSON.parse(readFileTextOrEmpty(recordPath) || '{}');
  assert.ok(record.runner, `${recordPath} does not exist: run node spikes/lang-bench/pin-runner.mjs`);
  const verdict = verifyPin(path.join(spikeDir, 'runner'), record);
  assert.equal(verdict.ok, true, verdict.reason);
});

function readFileTextOrEmpty(file) {
  try { return readFileSync(file, 'utf8'); } catch { return ''; }
}
