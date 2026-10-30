import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { sourceFiles } from './scan.mjs';
import { spikeDir } from './helpers.mjs';

// The operator's token path is written once, as TOKEN_FILE_PARTS in capture/capture-env.mjs. Retyped, it drifts: the preflight and its test
// each carried their own copy. secrets.test.mjs asserts the value on purpose, so it holds the literal too.
const RETYPED = /'minigit-bench'\s*,\s*'claude-oauth-token'/;
const OWNER = 'capture/capture-env.mjs';
const ASSERTS_THE_VALUE = 'test/secrets.test.mjs';
const rel = f => path.relative(spikeDir, f).split(path.sep).join('/');
const holders = () => sourceFiles(spikeDir, ['node_modules']).filter(f => RETYPED.test(readFileSync(f, 'utf8'))).map(rel);

test('The_token_path_is_retyped_nowhere_but_where_it_is_defined_and_where_its_value_is_asserted', () => {
  assert.deepEqual(holders().filter(f => ![OWNER, ASSERTS_THE_VALUE].includes(f)), []);
});

test('The_token_path_scan_still_finds_its_known_holders', () => {
  assert.deepEqual(holders().filter(f => [OWNER, ASSERTS_THE_VALUE].includes(f)).sort(), [OWNER, ASSERTS_THE_VALUE]);
});
