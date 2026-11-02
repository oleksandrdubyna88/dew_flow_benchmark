// E1.S8b: the box's environment is ONE allow-list (D15's base, the culture of D35, the base time zone, the documented per-stack keys). The unit half is here; the half
// that reads the environment from INSIDE the real box is in this file's last test, which needs the whole layer.
import assert from 'node:assert/strict';
import { test } from 'node:test';
import { ALLOW_LIST, BASE_ENV, boxEnv, unlistedKeys } from '../lib/exec-layer/env.mjs';

const dirs = { dotnetSdk: '/t/dotnet-sdk-10.0.403', node: '/t/node-v26.13.0-linux-x64', typescript: '/t/typescript-6.0.3' };

test('The_allow_list_still_carries_LANG', () => {
  assert.equal(BASE_ENV.LANG, 'de_DE.UTF-8');
  assert.equal(BASE_ENV.LC_ALL, 'de_DE.UTF-8');
  assert.equal(BASE_ENV.TZ, 'UTC');
  assert.equal(BASE_ENV.NODE_OPTIONS, '');
  assert.equal(BASE_ENV.DOTNET_gcServer, '0');
  assert.equal(BASE_ENV.DOTNET_CLI_TELEMETRY_OPTOUT, '1');
  assert.equal(BASE_ENV.DOTNET_NOLOGO, '1');
  assert.ok(ALLOW_LIST.includes('LANG') && ALLOW_LIST.includes('LC_ALL'), 'the scan must have the culture keys to look for');
  assert.deepEqual(unlistedKeys({ LANG: 'x', WSL_INTEROP: '/run/WSL/1_interop', HOME: '/tmp' }), ['WSL_INTEROP'], 'the scan still finds a planted host key');
});

test('The_box_environment_is_built_from_the_allow_list_and_never_from_the_host', () => {
  process.env.TESTSTODELETE_PLANTED_HOST_KEY = '1';
  try {
    const env = boxEnv({ dirs });
    assert.deepEqual(unlistedKeys(env), []);
    assert.deepEqual(Object.keys(env).sort(), [...ALLOW_LIST].sort(), 'every listed key is set, none more');
    assert.equal(env.TESTSTODELETE_PLANTED_HOST_KEY, undefined);
  } finally {
    delete process.env.TESTSTODELETE_PLANTED_HOST_KEY;
  }
});

test('The_box_PATH_names_the_pinned_toolchains_and_no_mnt_entry', () => {
  const entries = boxEnv({ dirs }).PATH.split(':');
  assert.deepEqual(entries, [dirs.dotnetSdk, `${dirs.node}/bin`, `${dirs.typescript}/node_modules/.bin`, '/usr/bin', '/bin']);
  assert.equal(boxEnv({ dirs }).DOTNET_ROOT, dirs.dotnetSdk);
  assert.ok(!entries.some(entry => entry.startsWith('/mnt')));
});

test('No_invariant_globalization_switch_is_in_the_allow_list', () => {
  assert.deepEqual(ALLOW_LIST.filter(key => /INVARIANT/i.test(key)), []);
});

test('An_extra_key_may_add_a_key_and_may_replace_TZ_only', () => {
  assert.equal(boxEnv({ dirs, extra: { PORT: '8080' } }).PORT, '8080');
  assert.equal(boxEnv({ dirs, extra: { TZ: 'Europe/Berlin' } }).TZ, 'Europe/Berlin');
  assert.throws(() => boxEnv({ dirs, extra: { LANG: 'C' } }), /LANG/);
  assert.throws(() => boxEnv({ dirs, extra: { lc_all: 'C' } }), /LC_ALL/, 'a base key in another case is the same key to a program');
  assert.throws(() => boxEnv({ dirs, extra: { tz: 'C' } }), /TZ/, 'only the exact key TZ is replaceable: tz would sit beside it');
});
