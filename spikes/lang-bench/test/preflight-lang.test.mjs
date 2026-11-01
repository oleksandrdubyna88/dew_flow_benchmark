// E1.S5: the preflight — what must be true of the box before a gate, a reference or a session runs on it. The judgement is pure (probe outputs in,
// problems out, each naming the pin and the value seen); the probes themselves run inside a bwrap box that binds the pinned directories.
import assert from 'node:assert/strict';
import { chmodSync, existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { findOnPath } from '../../minigit-bench/lib/find-on-path.mjs';
import { apphostRootProblems, runApphostProbe } from '../lib/apphost-probe.mjs';
import { boxArgv, dotnetCandidates, judgeProbes, preflightLang } from '../lib/preflight-lang.mjs';
import { loadStacksLang } from '../lib/toolchain-pins.mjs';
import { ownedFolder } from './toolchain-fixtures.mjs';

const stacks = loadStacksLang(JSON.parse(readFileSync(fileURLToPath(new URL('../inputs/stacks-lang.json', import.meta.url)), 'utf8'))).value;
const good = () => ({
  node: 'v26.13.0\n',
  dotnetSdks: '10.0.403 [/store/dotnet-sdk-10.0.403/sdk]\n',
  dotnetRuntimes: 'Microsoft.AspNetCore.App 10.0.14 [/store/dotnet-sdk-10.0.403/shared/Microsoft.AspNetCore.App]\nMicrosoft.NETCore.App 10.0.14 [/store/dotnet-sdk-10.0.403/shared/Microsoft.NETCore.App]\n',
  tsc: 'Version 6.0.3\n',
  pythonSha256: `${stacks.python.sha256}  /usr/bin/python3\n`,
  locales: 'C.utf8\nde_DE.utf8\nPOSIX\n',
});
const problemsOf = probes => judgeProbes({ probes, stacks }).problems.join('\n');

test('Probes_that_read_the_pins_back_pass_and_report_what_they_read', () => {
  const verdict = judgeProbes({ probes: good(), stacks });
  assert.deepEqual(verdict.problems, []);
  assert.equal(verdict.ok, true);
  assert.deepEqual(verdict.facts, { node: '26.13.0', dotnetSdks: ['10.0.403'], dotnetRuntimes: ['10.0.14'], typescript: '6.0.3', pythonSha256: stacks.python.sha256, locale: stacks.locale });
});

test('Another_Node_patch_is_refused_naming_the_pin_and_the_value_seen', () => {
  assert.match(problemsOf({ ...good(), node: 'v26.12.1\n' }), /node.*26\.12\.1.*26\.13\.0/s);
});

test('A_second_SDK_in_dotnet_list_sdks_is_refused_naming_both', () => {
  const problems = problemsOf({ ...good(), dotnetSdks: '10.0.114 [/usr/lib/dotnet/sdk]\n10.0.403 [/store/sdk]\n' });
  assert.match(problems, /10\.0\.114/);
  assert.match(problems, /10\.0\.403/);
  assert.match(problemsOf({ ...good(), dotnetSdks: '10.0.114 [/usr/lib/dotnet/sdk]\n' }), /list-sdks lists 10\.0\.114.*SDK 10\.0\.403/s, 'the wrong SDK alone is refused too');
  assert.match(problemsOf({ ...good(), dotnetSdks: '' }), /SDK/);
});

test('A_runtime_other_than_the_pinned_one_is_refused', () => {
  assert.match(problemsOf({ ...good(), dotnetRuntimes: 'Microsoft.NETCore.App 10.0.13 [/x]\n' }), /runtime.*10\.0\.13.*10\.0\.14/s);
});

test('A_TypeScript_outside_the_pinned_line_is_refused', () => {
  const outside = problemsOf({ ...good(), tsc: 'Version 7.0.2\n' });
  assert.match(outside, /typescript is 7\.0\.2, outside the 6\.0 line/);
  assert.match(problemsOf({ ...good(), tsc: 'Version 6.0.2\n' }), /typescript.*6\.0\.2.*6\.0\.3/s, 'inside the line but not the pinned patch');
  assert.match(problemsOf({ ...good(), tsc: '' }), /typescript/);
});

test('A_changed_Python_binary_is_refused_naming_both_checksums', () => {
  const changed = 'f'.repeat(64);
  const problems = problemsOf({ ...good(), pythonSha256: `${changed}  /usr/bin/python3\n` });
  assert.ok(problems.includes(changed) && problems.includes(stacks.python.sha256), problems);
  assert.match(problems, /\/usr\/bin\/python3/);
});

test('A_box_without_the_German_locale_is_refused_with_the_command_that_generates_it', () => {
  const problems = problemsOf({ ...good(), locales: 'C.utf8\nPOSIX\n' });
  assert.match(problems, /de_DE\.utf8/);
  assert.match(problems, /sudo locale-gen de_DE\.UTF-8/);
});

test('A_probe_that_did_not_run_is_a_problem_and_never_a_pass', () => {
  const none = judgeProbes({ probes: { ...good(), node: null }, stacks });
  assert.equal(none.ok, false);
  assert.match(none.problems.join('\n'), /node.*did not run/s);
});

/** Two directories each holding an executable `dotnet`, standing for the pinned SDK and the distribution's. */
function twoDotnets(t) {
  const root = ownedFolder(t, 'pathscan');
  const dirs = { pinned: path.join(root, 'pinned'), apt: path.join(root, 'apt'), none: path.join(root, 'none') };
  for (const dir of Object.values(dirs)) mkdirSync(dir);
  for (const dir of [dirs.pinned, dirs.apt]) { writeFileSync(path.join(dir, 'dotnet'), '#!/bin/sh\n'); chmodSync(path.join(dir, 'dotnet'), 0o755); }
  return dirs;
}

test('A_PATH_that_still_finds_the_apt_SDK_is_refused', t => {
  const d = twoDotnets(t);
  const pinned = path.join(d.pinned, 'dotnet');
  assert.deepEqual(dotnetCandidates({ boxPath: [d.pinned, d.none], pinnedDotnet: pinned }).problems, [], 'the pinned directory alone is fine');
  const behind = dotnetCandidates({ boxPath: [d.pinned, d.apt], pinnedDotnet: pinned });
  assert.equal(behind.problems.length, 1, 'the pinned dotnet is found first, yet a second one is on the PATH');
  assert.match(behind.problems[0], new RegExp(`${path.join(d.apt, 'dotnet').replace(/[/\\]/g, '.')}.*${pinned.replace(/[/\\]/g, '.')}`, 's'));
  const first = dotnetCandidates({ boxPath: [d.apt, d.pinned], pinnedDotnet: pinned });
  assert.ok(first.problems.length >= 1 && first.problems.some(p => p.includes(path.join(d.apt, 'dotnet'))), 'a distribution dotnet found first');
});

test('A_pinned_directory_listed_twice_or_through_a_link_is_one_dotnet_not_two', t => {
  const d = twoDotnets(t);
  assert.deepEqual(dotnetCandidates({ boxPath: [d.pinned, d.pinned, d.none], pinnedDotnet: path.join(d.pinned, 'dotnet') }).problems, []);
});

test('A_box_PATH_with_no_dotnet_at_all_is_refused', t => {
  const d = twoDotnets(t);
  const none = dotnetCandidates({ boxPath: [d.none], pinnedDotnet: path.join(d.pinned, 'dotnet') });
  assert.equal(none.problems.length, 1);
  assert.match(none.problems[0], /no dotnet/);
});

test('The_preflight_box_has_no_network_a_hidden_home_and_only_the_pinned_directories_bound_back_read_only', () => {
  const dirs = { dotnetSdk: '/home/op/store/sdk', node: '/home/op/store/node', typescript: '/home/op/store/ts' };
  const argv = boxArgv({ bwrap: '/usr/bin/bwrap', dirs, boxPath: ['/home/op/store/sdk'], operatorHome: '/home/op', exists: () => true });
  const at = (...tokens) => argv.findIndex((_, i) => tokens.every((tok, k) => argv[i + k] === tok));
  assert.ok(argv.includes('--unshare-net') && argv.includes('--unshare-pid') && argv.includes('--die-with-parent'), 'no network, own pid namespace, dies with its parent');
  assert.notEqual(at('--ro-bind', '/', '/'), -1, 'the root is read-only');
  const homeHidden = at('--tmpfs', '/home/op');
  assert.notEqual(homeHidden, -1, 'the operator home is replaced by an empty tmpfs');
  for (const dir of Object.values(dirs)) assert.ok(at('--ro-bind', dir, dir) > homeHidden, `${dir} is bound back read-only AFTER the home is hidden, so it shows`);
  assert.ok(argv.includes('--clearenv'), 'the environment is the allow-list only');
  assert.equal(argv.at(-1), '--');
});

test('A_dotnet_root_other_than_the_pinned_one_in_an_apphost_trace_is_refused', () => {
  const pinned = '/store/dotnet-sdk-10.0.403';
  assert.deepEqual(apphostRootProblems(`Using environment variable DOTNET_ROOT=[${pinned}] as runtime location.\nUsing dotnet root path [${pinned}]\n`, pinned), []);
  const apt = apphostRootProblems('Using dotnet root path [/usr/lib/dotnet]\n', pinned);
  assert.equal(apt.length, 1);
  assert.match(apt[0], /\/usr\/lib\/dotnet.*\/store\/dotnet-sdk-10\.0\.403/s);
  assert.match(apphostRootProblems('nothing useful\n', pinned)[0], /no dotnet root/);
  assert.match(apphostRootProblems(null, pinned)[0], /did not run/);
});

test('The_box_hides_the_distributions_dotnet_and_sets_DOTNET_ROOT_to_the_pinned_directory', () => {
  const dirs = { dotnetSdk: '/home/op/store/sdk', node: '/home/op/store/node', typescript: '/home/op/store/ts' };
  const argv = boxArgv({ bwrap: '/usr/bin/bwrap', dirs, boxPath: ['/home/op/store/sdk'], operatorHome: '/home/op', exists: () => true });
  const at = (...tokens) => argv.findIndex((_, i) => tokens.every((tok, k) => argv[i + k] === tok));
  const pinnedBind = at('--ro-bind', dirs.dotnetSdk, dirs.dotnetSdk);
  for (const hidden of ['/usr/lib/dotnet', '/etc/dotnet']) assert.ok(at('--tmpfs', hidden) !== -1 && at('--tmpfs', hidden) < pinnedBind, `${hidden} is hidden`);
  assert.notEqual(at('--setenv', 'DOTNET_ROOT', dirs.dotnetSdk), -1, 'an apphost-launched program finds the pinned runtime, not /etc/dotnet/install_location');
  const absent = boxArgv({ bwrap: '/usr/bin/bwrap', dirs, boxPath: [], operatorHome: '/home/op', exists: p => p !== '/etc/dotnet' });
  assert.ok(!absent.includes('/etc/dotnet'), 'a directory the host lacks is not mounted over: bwrap cannot create it on a read-only root');
});

// ---- inside a real box, on the real store -----------------------------------------------------------------------------------------------

const storeRoot = path.join(os.homedir(), ...stacks.storeRoot);
const installed = () => Object.values(stacks.directories).every(name => existsSync(path.join(storeRoot, name)));

function gate(t) {
  if (findOnPath('bwrap') === null) return lacks(t, 'bwrap is not on PATH: the box cannot be built here');
  if (!installed()) return lacks(t, `the pinned toolchains are not installed under ${storeRoot} (node spikes/lang-bench/install-toolchains.mjs)`);
  return true;
}
function lacks(t, why) {
  if (process.env.BENCH_REQUIRE_SANDBOX === '1') throw new Error(`BENCH_REQUIRE_SANDBOX=1 and ${why}`);
  t.skip(why);
  return false;
}

test('The_preflight_passes_in_a_box_that_binds_the_pinned_toolchains_and_reads_every_pin_back', async t => {
  if (!gate(t)) return;
  const result = await preflightLang({ stacks, storeRoot });
  assert.equal(result.ok, true, JSON.stringify(result));
  assert.equal(result.value.facts.node, stacks.pins.node);
  assert.deepEqual(result.value.facts.dotnetSdks, [stacks.pins.dotnetSdk]);
  assert.equal(result.value.facts.typescript, stacks.pins.typescript);
  assert.equal(result.value.facts.pythonSha256, stacks.python.sha256);
  assert.equal(result.value.facts.apphostDotnetRoot, path.join(storeRoot, stacks.directories.dotnetSdk), 'a program launched through its apphost finds the pinned runtime');
});

test('The_preflight_in_a_real_box_refuses_a_pin_the_toolchain_does_not_meet_naming_the_value_seen', async t => {
  if (!gate(t)) return;
  const drifted = { ...stacks, pins: { ...stacks.pins, node: '26.12.1' }, python: { ...stacks.python, sha256: 'a'.repeat(64) } };
  const result = await preflightLang({ stacks: drifted, storeRoot });
  assert.equal(result.ok, false);
  assert.match(result.problems.join('\n'), /node.*26\.13\.0.*26\.12\.1/s);
  assert.match(result.problems.join('\n'), /a{64}/);
});

test('The_preflight_in_a_real_box_refuses_a_PATH_that_reaches_the_distributions_SDK', async t => {
  if (!gate(t)) return;
  if (!existsSync('/usr/bin/dotnet')) return lacks(t, '/usr/bin/dotnet is absent: there is no distribution SDK to reach');
  const dirs = Object.values(stacks.directories).map(name => path.join(storeRoot, name));
  const boxPath = [dirs[0], path.join(dirs[1], 'bin'), path.join(dirs[2], 'node_modules', '.bin'), '/usr/bin'];
  const result = await preflightLang({ stacks, storeRoot, boxPath });
  assert.equal(result.ok, false);
  assert.match(result.problems.join('\n'), /second dotnet.*\/usr\/bin\/dotnet/s);
});

test('The_preflight_refuses_a_store_that_is_not_there_without_starting_a_box', async t => {
  const empty = ownedFolder(t, 'nostore');
  const result = await preflightLang({ stacks, storeRoot: empty });
  assert.equal(result.ok, false);
  assert.match(result.problems.join('\n'), /not in the store/);
});

test('A_host_without_bwrap_is_refused_naming_it_and_no_probe_is_attempted', async t => {
  if (!installed()) return lacks(t, `the pinned toolchains are not installed under ${storeRoot}`);
  const before = process.env.PATH;
  process.env.PATH = path.join(ownedFolder(t, 'nopath'), 'nothing-here');
  try {
    const result = await preflightLang({ stacks, storeRoot });
    assert.equal(result.ok, false);
    assert.match(result.problems.join('\n'), /bwrap is not installed/);
    assert.doesNotMatch(result.problems.join('\n'), /did not run/, 'there was no box to probe in');
  } finally {
    process.env.PATH = before;
  }
});

test('An_apphost_in_a_box_without_the_hiding_resolves_the_distributions_dotnet_root_and_that_is_the_point', async t => {
  if (!gate(t)) return;
  if (!existsSync('/etc/dotnet/install_location') && !existsSync('/usr/lib/dotnet/dotnet')) return lacks(t, 'the host has no distribution dotnet for an apphost to find');
  const dirs = Object.fromEntries(Object.entries(stacks.directories).map(([name, dirName]) => [name, path.join(storeRoot, dirName)]));
  const bare = await runApphostProbe({ stacks, dirs, boxPath: [dirs.dotnetSdk], operatorHome: os.homedir(), protect: false });
  assert.notEqual(bare.root, dirs.dotnetSdk, `without DOTNET_ROOT and the hiding, the apphost used ${bare.root}`);
  assert.ok(bare.problems.length >= 1, 'and the probe says so');
  const guarded = await runApphostProbe({ stacks, dirs, boxPath: [dirs.dotnetSdk], operatorHome: os.homedir(), protect: true });
  assert.deepEqual(guarded.problems, []);
  assert.equal(guarded.root, dirs.dotnetSdk);
});
