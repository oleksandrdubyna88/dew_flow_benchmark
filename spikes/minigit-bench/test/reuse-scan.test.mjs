// Structural scans for the decisions that must be made in ONE place (reuse-first): where bwrap and python are located, how a value
// result is built, how a raw OTLP line is shaped. Each prohibition has a companion that proves the scan still finds the sanctioned site.
import assert from 'node:assert/strict';
import { mkdirSync, readFileSync, readdirSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { siblingCheckout, spikeDir, tmpDir } from './helpers.mjs';
import { stripComments } from './scan.mjs';

const mjs = dir => readdirSync(path.join(spikeDir, dir), { withFileTypes: true }).filter(e => e.isFile() && e.name.endsWith('.mjs')).map(e => path.join(spikeDir, dir, e.name));
const PRODUCTION = [...mjs('.'), ...mjs('lib'), ...mjs('capture')];
const WITH_TESTS = [...PRODUCTION, ...mjs('test').filter(f => !f.endsWith('reuse-scan.test.mjs'))];
const code = file => stripComments(readFileSync(file, 'utf8'));
const where = (files, pattern) => files.filter(f => pattern.test(code(f))).map(f => path.relative(spikeDir, f)).sort();
const occurrences = (files, pattern) => files.reduce((n, f) => n + (code(f).match(new RegExp(pattern, 'g')) ?? []).length, 0);

test('Bwrap_is_located_by_PATH_everywhere_and_no_path_to_it_is_written_down', () => {
  assert.deepEqual(where(WITH_TESTS, /\/usr\/bin\/bwrap/), []);
  assert.deepEqual(where(WITH_TESTS, /findOnPath\(\s*'bwrap'/), ['heldout-tool.mjs', 'score-session.mjs', 'test/helpers.mjs']);
});

test('The_python_interpreter_is_named_in_one_place', () => {
  assert.equal(occurrences(WITH_TESTS, String.raw`'/usr/bin/python3'`), 1);
  assert.deepEqual(where(WITH_TESTS, /'\/usr\/bin\/python3'/), ['lib/heldout-run.mjs']);
  assert.deepEqual(where(WITH_TESTS, /\bPYTHON\b/), ['lib/heldout-run.mjs', 'test/helpers.mjs'], 'the scan still sees the constant and its one test user');
});

test('A_value_result_is_built_by_result_mjs_never_by_hand', () => {
  const byHand = /\{\s*ok:\s*(?:true|false)\s*,\s*(?:reason|value|buffer|values)\b/;
  assert.deepEqual(where(PRODUCTION.filter(f => !f.endsWith(`${path.sep}result.mjs`)), byHand), []);
  assert.ok(where(PRODUCTION, /\bfail\(/).length >= 8, 'the scan still sees the sanctioned builders in use');
  assert.match(code(path.join(spikeDir, 'lib', 'result.mjs')), byHand, 'and still recognises the shape it forbids');
});

test('A_raw_otlp_request_is_turned_into_its_file_line_in_one_place', () => {
  const shape = /receivedAt[^}]*contentType:\s*req\.headers[^}]*body/; // the 415 `misconfigured` record has no body and is a different record
  assert.deepEqual(where(PRODUCTION, shape), ['lib/http-body.mjs']);
  assert.deepEqual(where(PRODUCTION, /rawBodyLine\(/), ['capture/otlp-sink.mjs', 'lib/otel-receiver.mjs']);
});

test('Case_repositories_of_the_real_suite_are_built_under_the_home_regime_never_in_tmp', () => {
  const realSuite = mjs('test').filter(f => /\bkeyGate\(/.test(code(f)) && !f.endsWith('helpers.mjs'));
  assert.deepEqual(realSuite.map(f => path.basename(f)).sort(), ['bare-e2e.test.mjs', 'heldout-hostile.test.mjs', 'prereg.test.mjs', 'repo-scan.test.mjs'], 'the scan still finds the tests that decrypt the real suite (the runner\'s end-to-end test and the prereg leak check call the scorer and the leak tool, which build their own case repositories under the home regime)');
  assert.deepEqual(where(realSuite, /tmpDir\('[^']*cases'\)/), [], 'a cases directory is where the runner builds case repositories');
  assert.ok(where(realSuite, /homeDir\('[^']*cases'\)/).length >= 1, 'and the scan still finds one built under the home');
});

test('A_sibling_checkout_is_resolved_beside_the_repository_not_from_a_written_down_home_path', () => {
  const parent = tmpDir('siblings');
  mkdirSync(path.join(parent, 'repo'));
  mkdirSync(path.join(parent, 'other_repo', 'src'), { recursive: true });
  assert.equal(siblingCheckout('other_repo', { repoRoot: path.join(parent, 'repo') }), path.join(parent, 'other_repo'));
  assert.equal(siblingCheckout('absent_repo', { repoRoot: path.join(parent, 'repo') }), null, 'a missing checkout is null, never a guessed path');
  const worktree = path.join(tmpDir('wt'), 'feature');
  mkdirSync(worktree);
  assert.equal(siblingCheckout('other_repo', { repoRoot: worktree, commonRoot: path.join(parent, 'repo') }), path.join(parent, 'other_repo'), 'a git worktree looks beside the main checkout too');
  assert.deepEqual(where([path.join(spikeDir, 'test', 'coai-usage.test.mjs')], /['"`]\/home\//), []);
  assert.deepEqual(where([path.join(spikeDir, 'test', 'coai-usage.test.mjs')], /siblingCheckout\(/), ['test/coai-usage.test.mjs']);
});

test('Every_directory_a_test_creates_outside_its_own_process_carries_the_testsToDelete_prefix', () => {
  const creators = /\b(?:mkdtempSync|writeFileSync|mkdirSync)\(\s*path\.join\(\s*(?:os\.homedir\(\)|os\.tmpdir\(\))[^)]*\)/g;
  const unprefixed = [];
  for (const f of mjs('test').filter(x => !x.endsWith('reuse-scan.test.mjs'))) {
    for (const m of code(f).matchAll(creators)) if (!/testsToDelete-/.test(m[0])) unprefixed.push(`${path.basename(f)}: ${m[0]}`);
  }
  assert.deepEqual(unprefixed, []);
  assert.ok([...code(path.join(spikeDir, 'test', 'bwrap.test.mjs')).matchAll(creators)].length >= 1, 'the scan still finds a creator it judges');
  assert.deepEqual(where([path.join(spikeDir, 'test', 'bwrap.test.mjs')], /\.mgb-|['"`]mgb-(?:test|decoy)/), [], 'no socket or decoy keeps the unprefixed mgb- name');
});
