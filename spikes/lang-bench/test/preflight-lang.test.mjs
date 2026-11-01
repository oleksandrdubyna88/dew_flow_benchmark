// E1.S5: the preflight — what must be true of the box before a gate, a reference or a session runs on it. The judgement is pure (probe outputs in,
// problems out, each naming the pin and the value seen); the probes themselves run inside a bwrap box that binds the pinned directories.
import assert from 'node:assert/strict';
import { chmodSync, existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { findOnPath } from '../../minigit-bench/lib/find-on-path.mjs';
import { dotnetCandidates, judgeProbes, preflightLang } from '../lib/preflight-lang.mjs';
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
  assert.match(problemsOf({ ...good(), dotnetSdks: '10.0.114 [/usr/lib/dotnet/sdk]\n' }), /SDK.*10\.0\.114.*10\.0\.403/s, 'the wrong SDK alone is refused too');
  assert.match(problemsOf({ ...good(), dotnetSdks: '' }), /SDK/);
});

test('A_runtime_other_than_the_pinned_one_is_refused', () => {
  assert.match(problemsOf({ ...good(), dotnetRuntimes: 'Microsoft.NETCore.App 10.0.13 [/x]\n' }), /runtime.*10\.0\.13.*10\.0\.14/s);
});

test('A_TypeScript_outside_the_pinned_line_is_refused', () => {
  const outside = problemsOf({ ...good(), tsc: 'Version 7.0.2\n' });
  assert.match(outside, /typescript.*7\.0\.2.*6\.0/s);
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
