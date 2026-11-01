// What must never be committed under the spike: compiled Python. A pyc in the runner's tree could run in place of the pinned source,
// and one in test/py is noise that changes with the interpreter.
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';

const spikeDir = fileURLToPath(new URL('..', import.meta.url));
const git = (...args) => spawnSync('git', args, { cwd: spikeDir, encoding: 'utf8' });
const inRepo = git('rev-parse', '--is-inside-work-tree').stdout?.trim() === 'true';
const skip = inRepo ? false : 'not inside a git work tree (an exported copy): nothing to list';

test('No_compiled_python_is_committed_under_the_spike', { skip }, () => {
  const tracked = git('ls-files', '--', '.').stdout.split('\n').filter(f => /(^|\/)__pycache__\/|\.py[co]$/.test(f));
  assert.deepEqual(tracked, []);
});

test('A_python_cache_folder_anywhere_in_the_spike_is_ignored_by_git', { skip }, () => {
  for (const file of ['runner/__pycache__/rs_lex.cpython-314.pyc', 'test/py/__pycache__/shape_scan.cpython-314.pyc', 'lib/__pycache__/x.pyc']) {
    const result = git('check-ignore', '-q', '--', file);
    assert.equal(result.status, 0, `${file} must be ignored`);
  }
});

test('The_hygiene_scan_still_finds_a_known_compiled_file_name', () => {
  const pattern = /(^|\/)__pycache__\/|\.py[co]$/;
  assert.ok(pattern.test('test/py/__pycache__/shape_scan.cpython-314.pyc'));
  assert.ok(pattern.test('runner/x.pyc'));
  assert.ok(!pattern.test('runner/rs_lex.py'));
});
