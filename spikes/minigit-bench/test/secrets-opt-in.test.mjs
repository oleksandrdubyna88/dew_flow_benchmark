// N18: the one way a test may read the operator's real secret files: an explicit opt-in with a reason, in a test file that is named below. The opt-in is per process (a test file is
// its own process), so this file proves it apart from secrets.test.mjs, whose refusals must stay in force.
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { test } from 'node:test';
import { allowRealSecretUnderTest, readSecretFile } from '../lib/secrets.mjs';
import { sourceFiles, stripComments } from './scan.mjs';
import { spikeDir } from './helpers.mjs';

const NOT_A_SECRET = ['.config', 'minigit-bench', 'testsToDelete-no-such-secret'];
/** The only test files that may read the operator's real token: tests whose whole subject is that token. */
const MAY_OPT_IN = Object.freeze(['test/smoke-real.test.mjs']);

test('A_test_that_opts_in_with_a_reason_reads_the_real_home_and_one_without_a_reason_does_not', () => {
  assert.throws(() => allowRealSecretUnderTest(''), /reason/);
  assert.throws(() => allowRealSecretUnderTest(), /reason/);
  assert.throws(() => readSecretFile({ home: os.userInfo().homedir, parts: NOT_A_SECRET }), /test runner/, 'control: still refused before the opt-in');
  allowRealSecretUnderTest('this test asserts what the operator\'s own token file holds');
  assert.equal(readSecretFile({ home: os.userInfo().homedir, parts: NOT_A_SECRET }).ok, false, 'after the opt-in the reader goes on to look at the file (an absent one is a plain failure)');
});

test('Only_the_named_test_files_opt_in_to_the_real_secret_files', () => {
  const callers = sourceFiles(spikeDir, ['node_modules'])
    .filter(f => /allowRealSecretUnderTest\(/.test(stripComments(readFileSync(f, 'utf8'))))
    .map(f => path.relative(spikeDir, f).split(path.sep).join('/'))
    .filter(f => f !== 'lib/secrets.mjs' && f !== 'test/secrets-opt-in.test.mjs');
  assert.deepEqual(callers, [...MAY_OPT_IN], 'a new opt-in is a decision of its own: name the file in MAY_OPT_IN with the reason');
});
