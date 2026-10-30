import assert from 'node:assert/strict';
import { readFileSync, readdirSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { hostLacks, spikeDir } from './helpers.mjs';
import { stripComments } from './scan.mjs';

const withRequire = (value, fn) => {
  const before = process.env.BENCH_REQUIRE_SANDBOX;
  if (value === undefined) delete process.env.BENCH_REQUIRE_SANDBOX; else process.env.BENCH_REQUIRE_SANDBOX = value;
  try { return fn(); } finally { if (before === undefined) delete process.env.BENCH_REQUIRE_SANDBOX; else process.env.BENCH_REQUIRE_SANDBOX = before; }
};

test('A_host_that_lacks_something_skips_with_the_reason_and_under_BENCH_REQUIRE_SANDBOX_it_fails', () => {
  const skipped = [];
  const t = { skip: why => skipped.push(why) };
  assert.equal(withRequire(undefined, () => hostLacks(t, 'no network')), false);
  assert.deepEqual(skipped, ['no network']);
  assert.throws(() => withRequire('1', () => hostLacks(t, 'no network')), /BENCH_REQUIRE_SANDBOX=1 and no network/);
  assert.deepEqual(skipped, ['no network'], 'the failing case did not also skip');
  assert.equal(withRequire('0', () => hostLacks(t, 'x')), false, 'only the value 1 turns a skip into a failure');
});

const testsDir = path.join(spikeDir, 'test');
const callsOf = file => stripComments(readFileSync(path.join(testsDir, file), 'utf8')).split('\n').flatMap((l, i) => (/\bt\.skip\(/.test(l) ? [`${file}:${i + 1}`] : []));

test('No_test_skips_by_itself_because_every_host_conditional_skip_goes_through_hostLacks', () => {
  const files = readdirSync(testsDir).filter(f => f.endsWith('.mjs') && f !== 'helpers.mjs');
  assert.deepEqual(files.flatMap(callsOf), []);
});

test('The_skip_scan_still_finds_the_one_sanctioned_skip_call', () => {
  assert.equal(callsOf('helpers.mjs').length, 1);
});
