// E1.S5: the preflight — what must be true of the box before a gate, a reference or a session runs on it. The judgement is pure (probe outputs in,
// problems out, each naming the pin and the value seen); the probes themselves run inside a bwrap box that binds the pinned directories.
import assert from 'node:assert/strict';
import { chmodSync, existsSync, mkdirSync, readFileSync, symlinkSync, writeFileSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { findOnPath } from '../../minigit-bench/lib/find-on-path.mjs';
import { apphostRootProblems, runApphostProbe } from '../lib/apphost-probe.mjs';
import { boxArgv, judgeDotnetHits, judgeProbes, preflightLang, probeArgvFor, scanDotnetInBox } from '../lib/preflight-lang.mjs';
import { loadStacksLang } from '../lib/toolchain-pins.mjs';
import { sealTree, treeRecord, unsealTree } from '../lib/toolchain-store.mjs';
import { ownedFolder, writeTree } from './toolchain-fixtures.mjs';

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

test('The_locale_remedy_is_derived_from_the_pinned_locale_not_written_down_for_one_locale', () => {
  const french = { ...stacks, locale: 'fr_FR.utf8' };
  const problems = judgeProbes({ probes: { ...good(), locales: 'C.utf8\nPOSIX\n' }, stacks: french }).problems.join('\n');
  assert.match(problems, /fr_FR\.utf8/);
  assert.match(problems, /sudo locale-gen fr_FR\.UTF-8/);
  assert.doesNotMatch(problems, /de_DE/);
});

test('The_Python_probe_hashes_the_pinned_path_not_a_hard_coded_one', () => {
  const elsewhere = { ...stacks, python: { ...stacks.python, path: '/opt/py/python3' } };
  assert.deepEqual(probeArgvFor('pythonSha256', elsewhere), ['/usr/bin/sha256sum', '/opt/py/python3']);
  assert.deepEqual(probeArgvFor('pythonSha256', stacks), ['/usr/bin/sha256sum', stacks.python.path]);
  assert.deepEqual(probeArgvFor('node', stacks), stacks.probes.node, 'every other probe is its argv as pinned');
  assert.match(judgeProbes({ probes: { ...good(), pythonSha256: `${'a'.repeat(64)}  /opt/py/python3\n` }, stacks: elsewhere }).problems.join('\n'), /\/opt\/py\/python3 has sha256/);
});

test('A_probe_that_did_not_run_is_a_problem_and_never_a_pass', () => {
  const none = judgeProbes({ probes: { ...good(), node: null }, stacks });
  assert.equal(none.ok, false);
  assert.match(none.problems.join('\n'), /node.*did not run/s);
});

const PINNED = '/store/dotnet-sdk-10.0.403/dotnet';
const hit = (shown, real = shown) => ({ shown, real });

test('The_hits_of_which_a_dotnet_are_judged_by_where_they_really_are_and_a_second_real_file_is_refused', () => {
  assert.deepEqual(judgeDotnetHits({ hits: [hit(PINNED)], pinnedReal: PINNED, pinnedDotnet: PINNED, boxPath: ['/store/dotnet-sdk-10.0.403'] }), [], 'the pinned directory alone is fine');
  const behind = judgeDotnetHits({ hits: [hit(PINNED), hit('/usr/bin/dotnet', '/usr/lib/dotnet/dotnet')], pinnedReal: PINNED, pinnedDotnet: PINNED, boxPath: [] });
  assert.equal(behind.length, 1, 'the pinned dotnet is found first, yet a second one is on the PATH');
  assert.match(behind[0], /second dotnet.*\/usr\/bin\/dotnet.*\/usr\/lib\/dotnet\/dotnet.*only \/store\/dotnet-sdk-10\.0\.403\/dotnet/s);
  const first = judgeDotnetHits({ hits: [hit('/usr/bin/dotnet', '/usr/lib/dotnet/dotnet'), hit(PINNED)], pinnedReal: PINNED, pinnedDotnet: PINNED, boxPath: [] });
  assert.ok(first.some(p => p.includes('/usr/bin/dotnet')), 'a distribution dotnet found first');
});

test('One_real_dotnet_reached_through_two_names_is_one_dotnet_not_two', () => {
  assert.deepEqual(judgeDotnetHits({ hits: [hit(PINNED), hit('/link/dotnet', PINNED), hit(PINNED)], pinnedReal: PINNED, pinnedDotnet: PINNED, boxPath: [] }), []);
});

test('A_box_PATH_with_no_dotnet_at_all_is_refused_and_so_is_one_without_the_pinned_dotnet', () => {
  const none = judgeDotnetHits({ hits: [], pinnedReal: PINNED, pinnedDotnet: PINNED, boxPath: ['/x'] });
  assert.equal(none.length, 1);
  assert.match(none[0], /no dotnet/);
  const other = judgeDotnetHits({ hits: [hit('/opt/dotnet', '/opt/real/dotnet')], pinnedReal: PINNED, pinnedDotnet: PINNED, boxPath: ['/opt'] });
  assert.ok(other.some(p => /pinned dotnet .* is not on the box's PATH/.test(p)), other.join('\n'));
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

/** The prefix of a box over the real store, with the fixture folder bound read-only at its own path when one is given. */
function realBox(fixture, { protect = true } = {}) {
  const dirs = Object.fromEntries(Object.entries(stacks.directories).map(([name, dirName]) => [name, path.join(storeRoot, dirName)]));
  const extraRoBinds = fixture ? [{ host: fixture, box: fixture }] : [];
  return { dirs, pinned: path.join(dirs.dotnetSdk, 'dotnet'), prefix: (boxPath) => boxArgv({ bwrap: findOnPath('bwrap'), dirs, boxPath, operatorHome: os.homedir(), extraRoBinds, protect }) };
}

/** A folder holding an executable `dotnet` (a script: only its being found matters). */
function folderWithDotnet(t, what) {
  const dir = ownedFolder(t, what);
  writeFileSync(path.join(dir, 'dotnet'), '#!/bin/sh\n');
  chmodSync(path.join(dir, 'dotnet'), 0o755);
  return dir;
}

test('A_PATH_that_still_finds_the_apt_SDK_is_refused', async t => {
  if (!gate(t)) return;
  if (!existsSync('/usr/lib/dotnet/dotnet')) return lacks(t, 'the host has no apt SDK to reach');
  const box = realBox(null, { protect: false });
  const found = await scanDotnetInBox({ prefix: box.prefix([box.dirs.dotnetSdk, '/usr/bin']), pinnedDotnet: box.pinned });
  assert.equal(found.problems.length, 1, found.problems.join('\n'));
  assert.match(found.problems[0], /second dotnet.*\/usr\/bin\/dotnet/s);
  const alone = await scanDotnetInBox({ prefix: box.prefix([box.dirs.dotnetSdk]), pinnedDotnet: box.pinned });
  assert.deepEqual(alone.problems, [], 'green with the pinned directory alone');
});

test('A_hidden_apt_SDK_is_not_a_second_dotnet', async t => {
  if (!gate(t)) return;
  const box = realBox(null);
  const found = await scanDotnetInBox({ prefix: box.prefix([box.dirs.dotnetSdk, '/usr/bin']), pinnedDotnet: box.pinned });
  assert.deepEqual(found.problems, [], '/usr/bin/dotnet leads nowhere inside the box: the distribution is hidden there');
  const whole = await preflightLang({ stacks, storeRoot, boxPath: [box.dirs.dotnetSdk, path.join(box.dirs.node, 'bin'), path.join(box.dirs.typescript, 'node_modules', '.bin'), '/usr/bin'], withApphost: false });
  assert.equal(whole.ok, true, JSON.stringify(whole));
});

test('The_preflight_refuses_a_box_whose_PATH_reaches_a_second_dotnet_in_the_box', async t => {
  if (!gate(t)) return;
  const fixture = folderWithDotnet(t, 'second');
  const box = realBox(fixture);
  const result = await preflightLang({ stacks, storeRoot, boxPath: [box.dirs.dotnetSdk, fixture], withApphost: false, extraRoBinds: [{ host: fixture, box: fixture }] });
  assert.equal(result.ok, false);
  assert.match(result.problems.join('\n'), new RegExp(`second dotnet.*${path.join(fixture, 'dotnet')}`, 's'));
});

test('A_symlinked_directory_on_the_PATH_leading_to_the_pinned_dotnet_is_one_dotnet_and_one_leading_elsewhere_is_two', async t => {
  if (!gate(t)) return;
  const fixture = ownedFolder(t, 'linkdirs');
  const box = realBox(fixture);
  symlinkSync(box.dirs.dotnetSdk, path.join(fixture, 'to-pinned'));
  const elsewhere = folderWithDotnet(t, 'elsewhere');
  symlinkSync(elsewhere, path.join(fixture, 'to-other'));
  const sameFile = await scanDotnetInBox({ prefix: box.prefix([path.join(fixture, 'to-pinned'), box.dirs.dotnetSdk]), pinnedDotnet: box.pinned });
  assert.deepEqual(sameFile.problems, [], 'two names, one real file');
  assert.equal(sameFile.hits.length, 2, 'the scan did see both names');
  const wide = boxArgv({ bwrap: findOnPath('bwrap'), dirs: box.dirs, boxPath: [path.join(fixture, 'to-other'), box.dirs.dotnetSdk], operatorHome: os.homedir(), extraRoBinds: [{ host: fixture, box: fixture }, { host: elsewhere, box: elsewhere }] });
  const different = await scanDotnetInBox({ prefix: wide, pinnedDotnet: box.pinned });
  assert.ok(different.problems.some(p => p.includes(path.join(fixture, 'to-other', 'dotnet')) && p.includes(path.join(elsewhere, 'dotnet'))), different.problems.join('\n'));
});

test('A_PATH_entry_that_exists_only_on_the_host_is_not_counted', async t => {
  if (!gate(t)) return;
  const box = realBox(null);
  const hostOnly = folderWithDotnet(t, 'hostonly');
  const found = await scanDotnetInBox({ prefix: box.prefix([box.dirs.dotnetSdk, hostOnly]), pinnedDotnet: box.pinned });
  assert.deepEqual(found.problems, [], `${hostOnly} is hidden by the box's /tmp: its dotnet is not there for the box`);
});

test('A_second_dotnet_that_is_visible_in_the_box_is_counted_wherever_it_sits_on_the_PATH', async t => {
  if (!gate(t)) return;
  const fixture = folderWithDotnet(t, 'visible');
  const box = realBox(fixture);
  const found = await scanDotnetInBox({ prefix: box.prefix([fixture, box.dirs.dotnetSdk]), pinnedDotnet: box.pinned });
  assert.ok(found.problems.some(p => p.includes(path.join(fixture, 'dotnet'))), found.problems.join('\n'));
});

test('The_preflight_refuses_a_store_that_is_not_there_without_starting_a_box', async t => {
  const empty = ownedFolder(t, 'nostore');
  const result = await preflightLang({ stacks, storeRoot: empty });
  assert.equal(result.ok, false);
  assert.match(result.problems.join('\n'), /not in the store/);
});

test('A_host_without_bwrap_is_refused_naming_it_and_no_probe_is_attempted', async t => {
  if (!installed()) return lacks(t, `the pinned toolchains are not installed under ${storeRoot}`);
  const hostPath = path.join(ownedFolder(t, 'nopath'), 'nothing-here'); // given as a parameter: the test never touches process.env
  const before = process.env.PATH;
  const result = await preflightLang({ stacks, storeRoot, hostPath });
  assert.equal(process.env.PATH, before);
  assert.equal(result.ok, false);
  assert.match(result.problems.join('\n'), /bwrap is not installed/);
  assert.doesNotMatch(result.problems.join('\n'), /did not run/, 'there was no box to probe in');
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

/** A store of three tiny script "toolchains" that print the pinned versions, sealed, with their tree hashes recorded in a copy of the pins. */
function scriptStore(t) {
  const root = ownedFolder(t, 'scriptstore');
  const d = stacks.directories;
  const exe = (dir, rel, body) => { writeTree(dir, { [rel]: `#!/bin/sh\n${body}\n` }); chmodSync(path.join(dir, rel), 0o755); };
  exe(path.join(root, d.dotnetSdk), 'dotnet', `case "$1" in --list-sdks) echo "${stacks.pins.dotnetSdk} [/x/sdk]";; --list-runtimes) echo "Microsoft.NETCore.App ${stacks.pins.dotnetRuntime} [/x/shared]";; esac`);
  mkdirSync(path.join(root, d.dotnetSdk, 'sdk', stacks.pins.dotnetSdk), { recursive: true });
  exe(path.join(root, d.node), 'bin/node', `echo v${stacks.pins.node}`);
  exe(path.join(root, d.typescript), 'node_modules/.bin/tsc', `echo "Version ${stacks.pins.typescript}"`);
  const recorded = { ...stacks, treeSha256: Object.fromEntries(Object.entries(d).map(([name, dir]) => [name, treeRecord(path.join(root, dir))])) };
  for (const dir of Object.values(d)) sealTree(path.join(root, dir));
  return { root, recorded, dirs: Object.fromEntries(Object.entries(d).map(([name, dir]) => [name, path.join(root, dir)])) };
}

test('A_binary_replaced_in_place_that_still_prints_the_pinned_version_is_refused', async t => {
  if (!gate(t)) return;
  const store = scriptStore(t);
  const before = await preflightLang({ stacks: store.recorded, storeRoot: store.root, withApphost: false });
  assert.equal(before.ok, true, JSON.stringify(before));
  const node = path.join(store.dirs.node, 'bin', 'node');
  unsealTree(store.dirs.node);
  writeFileSync(node, `#!/bin/sh\n# swapped\necho v${stacks.pins.node}\n`);
  chmodSync(node, 0o755);
  sealTree(store.dirs.node);
  const after = await preflightLang({ stacks: store.recorded, storeRoot: store.root, withApphost: false });
  assert.equal(after.ok, false, 'the replaced node still answers --version with the pin');
  assert.match(after.problems.join('\n'), /node-v26\.13\.0-linux-x64.*tree hash/s);
});
