// The E1 test-runner scaffold's smoke test (E1.S8a part i, first commit): the runner finds tests and refuses to pass over none,
// it runs lowered (common.local-cpu-budget), and a Python unit starts from a Node test file under `python3 -I`, standard library only.
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { existsSync, readdirSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';

const spikeDir = fileURLToPath(new URL('..', import.meta.url));
const mount = path.join(spikeDir, '..', '..', '.agents', 'conventions');
const lowered = !process.env.CI && existsSync(mount) && readdirSync(mount).length > 0;
const pythonProbe = spawnSync('python3', ['-I', '-c', 'pass'], { encoding: 'utf8' });
// A test that needs a toolchain skips with its reason; BENCH_REQUIRE_SANDBOX=1 (the epic's completion run) turns the skip into a failure.
const needsPython = pythonProbe.error && process.env.BENCH_REQUIRE_SANDBOX !== '1' ? `python3 is not on PATH (${pythonProbe.error.code})` : false;

const run = (command, args) => spawnSync(command, args, { cwd: spikeDir, encoding: 'utf8' });

test('The_runner_refuses_a_test_glob_that_matches_no_file_instead_of_passing_over_it', () => {
  const result = run(process.execPath, ['run-tests.mjs', 'test/no-such-file-*.test.mjs']);
  assert.equal(result.status, 2, `exit code (stdout: ${result.stdout}, stderr: ${result.stderr})`);
  assert.match(result.stderr, /no test file matches test\/no-such-file-\*\.test\.mjs/);
});

test('A_Node_test_started_by_the_runner_runs_at_lowered_priority', { skip: lowered ? false : 'CI, or the conventions mount is absent: nothing is lowered' }, () => {
  assert.ok(os.getPriority(0) >= 19 || process.platform === 'win32' && os.getPriority(0) > 0, `priority ${os.getPriority(0)}: run this through run-tests.mjs`);
});

test('A_Python_unit_started_from_a_Node_test_file_runs_isolated_and_is_counted', { skip: needsPython }, () => {
  const result = run('python3', ['-I', '-B', '-m', 'unittest', 'discover', '-v', '-s', 'test/py', '-p', 'test_scaffold.py']);
  assert.equal(result.status, 0, `exit code (stderr: ${result.stderr})`);
  assert.match(result.stderr, /test_The_scaffold_runs_python_isolated_from_the_environment_and_the_user_site \(test_scaffold\.ScaffoldTests[^)]*\) \.\.\. ok/);
  assert.match(result.stderr, /Ran 1 test in /, 'a run that found no test must not read as a pass');
});
