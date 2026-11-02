// E1.S8b: the box's environment is ONE allow-list (D15's base, the culture of D35, the base time zone, the documented per-stack keys). The unit half is here; the half
// that reads the environment from INSIDE the real box is in this file's last test, which needs the whole layer.
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { agentEnv, sessionLayout } from '../../minigit-bench/lib/sandbox.mjs';
import { ALLOW_LIST, allowFor, assertNoCaseKeys, BASE_ENV, boxEnv, caseKeysIn, driverEnv, heapKeys, STACK_KEYS, unlistedKeys } from '../lib/exec-layer/env.mjs';
import { chain, needs, SPAWN_ENV, start } from './support/exec-chain.mjs';
import { toolchains } from './support/exec-layer-host.mjs';
import { owned } from './support/owned.mjs';

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
    assert.deepEqual(Object.keys(env).sort(), [...allowFor(undefined)].sort(), 'every listed key of a stack-less box is set, none more');
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

test('Both_stacks_heap_keys_are_ninety_percent_of_the_limit_and_that_is_the_point', () => {
  // D55: the runtime's own memory needs room under the cgroup, so the GC's budget is 90 % of L, not L. A test at 100 % is red; so is a Node box with no key.
  const MIB = 1024 * 1024;
  for (const [mib, hex, oldSpace] of [[256, '0xe666666', 230], [512, '0x1ccccccc', 460], [100, '0x5a00000', 90]]) {
    const L = mib * MIB;
    assert.equal(boxEnv({ dirs, stack: 'csharp', memoryMaxBytes: L }).DOTNET_GCHeapHardLimit, hex, `.NET at ${mib} MiB: floor(0.9 L) bytes in hexadecimal`);
    assert.equal(parseInt(hex, 16), Math.floor(0.9 * L), `${hex} is floor(0.9 L)`);
    assert.equal(boxEnv({ dirs, stack: 'node', memoryMaxBytes: L }).NODE_OPTIONS, `--max-old-space-size=${oldSpace}`, `Node at ${mib} MiB: floor(0.9 L / 1 MiB)`);
    assert.equal(oldSpace, Math.floor((0.9 * L) / MIB));
    assert.deepEqual(heapKeys('csharp', L), { DOTNET_GCHeapHardLimit: hex });
    assert.deepEqual(heapKeys('node', L), { NODE_OPTIONS: `--max-old-space-size=${oldSpace}` });
  }
  assert.equal('DOTNET_GCHeapHardLimit' in boxEnv({ dirs, stack: 'node', memoryMaxBytes: 256 * MIB }), false, 'Node has no .NET key');
  assert.equal(boxEnv({ dirs, stack: 'csharp', memoryMaxBytes: 256 * MIB }).NODE_OPTIONS, '', 'a .NET box keeps the empty base NODE_OPTIONS');
  for (const bare of [boxEnv({ dirs }), driverEnv({ dirs, tmp: '/t/x' })]) {
    assert.equal('DOTNET_GCHeapHardLimit' in bare, false, 'a box for no stack (the build box, the driver) gets no hard limit');
    assert.equal(bare.NODE_OPTIONS, '', 'a box for no stack (the build box, the driver) gets no old-space flag');
    assert.deepEqual(caseKeysIn(bare), []);
  }
  assert.deepEqual(Object.keys(boxEnv({ dirs, stack: 'csharp', memoryMaxBytes: 256 * MIB })).sort(), [...ALLOW_LIST].sort(), 'the key is on the allow-list');
  assert.deepEqual(STACK_KEYS, { csharp: ['DOTNET_GCHeapHardLimit'] });
  assert.throws(() => boxEnv({ dirs, extra: { DOTNET_GCHeapHardLimit: '0x1' } }), /DOTNET_GCHeapHardLimit/, 'an extra cannot set it');
  assert.throws(() => boxEnv({ dirs, extra: { NODE_OPTIONS: '--max-old-space-size=1' } }), /NODE_OPTIONS/, 'an extra cannot set the flag');
  for (const key of ['COMPlus_GCHeapHardLimit', 'DOTNET_GCHeapHardLimitPercent', 'dotnet_gcheaphardlimit']) assert.throws(() => boxEnv({ dirs, extra: { [key]: '0x1' } }), new RegExp(key, 'i'), `an extra cannot size the .NET heap by ${key}`);
});

test('The_limit_of_a_scoring_box_is_a_whole_MiB_and_a_missing_one_throws_for_both_stacks', () => {
  const MIB = 1024 * 1024;
  for (const stack of ['csharp', 'node']) {
    for (const bad of [undefined, null, 0, -MIB, NaN, 1.5 * MIB, 256 * MIB + 1, '256']) {
      assert.throws(() => boxEnv({ dirs, stack, memoryMaxBytes: bad }), /memoryMaxBytes/, `${stack}: ${String(bad)}`);
      assert.throws(() => heapKeys(stack, bad), /memoryMaxBytes/);
    }
  }
  assert.deepEqual(heapKeys(undefined, 256 * MIB), {}, 'no stack named: no key');
});

test('A_misspelled_or_unknown_stack_throws_and_never_silently_drops_the_heap_keys', () => {
  const MIB = 1024 * 1024;
  for (const stack of ['dotnet', 'typescript', 'C#', 'Node', '', null, 'csharp ']) {
    assert.throws(() => heapKeys(stack, 256 * MIB), /stack/, `heapKeys(${JSON.stringify(stack)})`);
    assert.throws(() => boxEnv({ dirs, stack, memoryMaxBytes: 256 * MIB }), /stack/, `boxEnv(${JSON.stringify(stack)})`);
  }
  assert.deepEqual(heapKeys(undefined, 256 * MIB), {}, 'no stack named is still a box with no heap keys (the build, the driver)');
  assert.deepEqual(Object.keys(heapKeys('csharp', 256 * MIB)), ['DOTNET_GCHeapHardLimit']);
  assert.deepEqual(Object.keys(heapKeys('node', 256 * MIB)), ['NODE_OPTIONS']);
});

test('A_per_case_heap_key_in_the_agent_box_or_the_build_box_environment_fails_the_guard', t => {
  // T-L14: the two keys are the scoring box's alone. A test plants one in v1's agent box environment (an `extra` may add a key) and the guard names it.
  const root = owned.ownedDir(t, 'envg');
  guardAgentBox(sessionLayout(root, 'g1').value);
});

function guardAgentBox(layout) {
  const agent = extra => agentEnv({ layout, dummyToken: 'x', cellId: 'c', nodeDir: '/t/node', extra });
  assert.deepEqual(caseKeysIn(agent({})), [], 'the agent box as it is carries neither key');
  assert.doesNotThrow(() => assertNoCaseKeys(agent({}), 'the agent box'));
  assert.deepEqual(caseKeysIn(agent({ DOTNET_GCHeapHardLimit: '0xe666666' })), ['DOTNET_GCHeapHardLimit']);
  assert.throws(() => assertNoCaseKeys(agent({ DOTNET_GCHeapHardLimit: '0xe666666' }), 'the agent box'), /the agent box.*DOTNET_GCHeapHardLimit/);
  assert.throws(() => assertNoCaseKeys(agent({ NODE_OPTIONS: '--max-old-space-size=230' }), 'the agent box'), /NODE_OPTIONS/);
  assert.throws(() => assertNoCaseKeys(agent({ NODE_OPTIONS: '--trace-warnings --max-old-space-size=230' }), 'the agent box'), /NODE_OPTIONS/);
  assert.doesNotThrow(() => assertNoCaseKeys(agent({ NODE_OPTIONS: '--trace-warnings' }), 'the agent box'), 'another flag is not a case key');
  for (const key of ['COMPlus_GCHeapHardLimit', 'DOTNET_GCHeapHardLimitPercent', 'COMPlus_GCHeapHardLimitPercent', 'complus_gcheaphardlimit', 'dotnet_gcheaphardlimit']) {
    assert.deepEqual(caseKeysIn({ [key]: '0x10' }), [key], `${key} is a heap limit of the runtime, spelled another way`);
  }
  assert.deepEqual(caseKeysIn({ DOTNET_gcServer: '0', DOTNET_GCgen0size: '0x1000' }), [], 'other GC settings are not case keys');
  assert.throws(() => assertNoCaseKeys({ dotnet_gchardlimit: 1, DOTNET_GCHEAPHARDLIMIT: '0x1' }, 'a build box'), /a build box/, 'the guard is case-insensitive: a spelling in another case is refused as well');
}

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

// ---- from INSIDE the real box (the chain: holder, delegated scope, box) ----
const BASH_OWN_KEYS = ['PWD', 'OLDPWD', 'SHLVL', '_']; // set by bash itself before `exec env`: not the box's
const PLANTED_HOST = { WSL_INTEROP: '/run/WSL/1_interop', WSL_DISTRO_NAME: 'Ubuntu', SECRET_TOKEN: 'planted', HOME: '/home/someone', LD_PRELOAD: '/nonexistent.so', TESTSTODELETE_PLANTED_HOST_KEY: '1' };

async function envOfBox(t, boxTweak, options = {}) {
  const c = await chain(t, { files: { 'run.sh': '#!/bin/bash\nexec env -0 > env.txt\n' }, boxTweak, ...options });
  await start(t, c.launch(), { env: { ...SPAWN_ENV, ...PLANTED_HOST } }).exited;
  return Object.fromEntries(readFileSync(path.join(c.view, 'env.txt'), 'utf8').split('\0').filter(Boolean).map(line => [line.slice(0, line.indexOf('=')), line.slice(line.indexOf('=') + 1)]));
}

test('The_box_environment_is_the_allow_list_and_nothing_else', async t => {
  if (!needs(t)) return;
  const seen = await envOfBox(t, argv => argv);
  const own = Object.fromEntries(Object.entries(seen).filter(([key]) => !BASH_OWN_KEYS.includes(key)));
  assert.deepEqual(unlistedKeys(own), [], 'no key of the host (a planted WSL_INTEROP, HOME, SECRET_TOKEN, LD_PRELOAD) is in the box');
  assert.deepEqual(own, { ...boxEnv({ dirs: toolchains }) }, 'exactly the allow-listed keys, with the allow-listed values (NODE_OPTIONS present and empty, LANG and LC_ALL de_DE.UTF-8, TZ UTC)');
  const leaking = await envOfBox(t, argv => argv.filter(arg => arg !== '--clearenv'));
  assert.ok(unlistedKeys(Object.fromEntries(Object.entries(leaking).filter(([key]) => !BASH_OWN_KEYS.includes(key)))).includes('WSL_INTEROP'), 'the refuted shape — no --clearenv — leaks the host\'s WSL interop variable into the box');
});

test('The_box_runs_Node_in_the_declared_culture_and_without_an_invariant_switch', async t => {
  if (!needs(t)) return;
  const c = await chain(t, { files: { 'run.sh': '#!/bin/bash\nexec node -e \'console.log(Intl.DateTimeFormat().resolvedOptions().locale)\' > locale.txt\n' } });
  assert.equal((await start(t, c.launch()).exited).code, 0);
  assert.match(readFileSync(path.join(c.view, 'locale.txt'), 'utf8').trim(), /^de/, 'a Node probe\'s locale starts with de');
});

test('The_box_environment_of_each_stack_carries_ninety_percent_of_the_limit_at_256_and_512_MiB', async t => {
  if (!needs(t)) return;
  const MIB = 1024 * 1024;
  for (const [mib, hex, oldSpace] of [[256, '0xe666666', 230], [512, '0x1ccccccc', 460]]) {
    const dotnet = await envOfBox(t, argv => argv, { stack: 'csharp', memoryMax: mib * MIB, caseId: `d${mib}` });
    assert.equal(dotnet.DOTNET_GCHeapHardLimit, hex, `.NET at ${mib} MiB`);
    assert.equal(dotnet.NODE_OPTIONS, '', `.NET keeps the empty NODE_OPTIONS at ${mib} MiB`);
    const node = await envOfBox(t, argv => argv, { stack: 'node', memoryMax: mib * MIB, caseId: `n${mib}` });
    assert.equal('DOTNET_GCHeapHardLimit' in node, false, `Node at ${mib} MiB`);
    assert.equal(node.NODE_OPTIONS, `--max-old-space-size=${oldSpace}`, `Node at ${mib} MiB`);
  }
});
