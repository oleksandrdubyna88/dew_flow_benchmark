// E1.S8, check 9: the `env` lines of every daemon and web sample are refused for the keys the execution layer's own environment refuses (T-L58). The lists are imported from the layer
// (lib/exec-layer/env.mjs: ALLOW_LIST, CASE_KEYS), never typed again here or in the checker.
import assert from 'node:assert/strict';
import { test } from 'node:test';
import { refusedEnvKey } from '../lib/conformance-check-samples.mjs';
import { ALLOW_LIST, CASE_KEYS } from '../lib/exec-layer/env.mjs';
import { check, failsOf, fixtureRepo, isOk } from './support/conformance-fixtures.mjs';

const ENV_LINE = 'env TZ "Europe/Berlin"';
const withEnv = (t, line) => fixtureRepo(t, 'daemon', { 's02.in': [[ENV_LINE, line]] });

test('A_sample_env_line_the_layer_refuses_fails_conformance_and_a_retyped_list_would_miss_a_new_key', async t => {
  // a key the layer gets later: a retyped copy of today's list does not know it, the layer's list (the seam stands in for a changed env.mjs) does
  const retyped = ['LANG', 'LC_ALL', 'TZ', 'HOME', 'TMPDIR', 'PATH', 'NODE_OPTIONS'];
  const later = [...ALLOW_LIST, 'NEW_BASE_KEY'];
  assert.equal(refusedEnvKey('NEW_BASE_KEY', { allowList: retyped }), false, 'a retyped list misses the new key');
  assert.equal(refusedEnvKey('NEW_BASE_KEY', { allowList: later }), true);
  const fixture = withEnv(t, 'env NEW_BASE_KEY "1"');
  assert.ok(isOk(await check(fixture), 'sample-env'), 'with the layer as it is today the key is outside the lists');
  const result = await check(fixture, [], { envLists: { allowList: later } });
  assert.match(failsOf(result, 'sample-env').join('\n'), /sample s02\.in line 3: the env line sets NEW_BASE_KEY/);
  assert.equal(result.exit, 1);
});

test('The_checker_refuses_every_key_of_the_layers_lists_in_any_case_except_TZ', () => {
  for (const key of [...ALLOW_LIST, ...CASE_KEYS].filter(entry => entry !== 'TZ')) {
    for (const spelling of [key, key.toLowerCase(), key.toUpperCase()]) assert.equal(refusedEnvKey(spelling), true, `${spelling} is refused`);
  }
  assert.equal(refusedEnvKey('TZ'), false, 'TZ may be replaced');
  for (const free of ['PORT_NAME', 'MY_FLAG', 'DOTNET_FOO', 'APP_MODE']) assert.equal(refusedEnvKey(free), false, `${free} is outside the lists`);
});

test('The_lists_the_checker_uses_are_the_layers_and_the_layer_exports_them', () => {
  assert.ok(ALLOW_LIST.length >= 10 && CASE_KEYS.length >= 4);
  assert.ok(ALLOW_LIST.includes('LANG') && ALLOW_LIST.includes('TZ') && CASE_KEYS.includes('NODE_OPTIONS'));
});

test('A_scenario_may_replace_TZ_and_add_a_key_outside_the_lists', async t => {
  assert.ok(isOk(await check(withEnv(t, 'env TZ "Asia/Tokyo"\nenv APP_MODE "fast"')), 'sample-env'));
});

for (const line of ['env DOTNET_gcServer "1"', 'env HOME "/x"', 'env TMPDIR "/x"', 'env DOTNET_GCHeapHardLimit "0x1000"', 'env COMPlus_GCHeapHardLimit "0x1000"', 'env dotnet_gcheaphardlimitpercent "50"']) {
  test(`The_env_line_${line.split(' ')[1]}_is_refused`, async t => {
    assert.equal((await check(withEnv(t, line))).lines.includes('ok sample-env'), false);
  });
}
