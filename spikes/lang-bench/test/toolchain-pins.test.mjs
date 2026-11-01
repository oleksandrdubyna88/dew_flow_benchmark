// E1.S5: the pinned toolchains' bookkeeping. Pure parsers over the three official sources (the .NET release metadata, Node's SHASUMS256.txt,
// npm's packument) and the validator of inputs/stacks-lang.json. No network, no toolchain: fixtures are written by the test.
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { dotnetOfficial, loadStacksLang, newestPatch, nodeOfficial, typescriptOfficial } from '../lib/toolchain-pins.mjs';

const stacksJson = () => JSON.parse(readFileSync(fileURLToPath(new URL('../inputs/stacks-lang.json', import.meta.url)), 'utf8'));
const SHA512_A = 'a'.repeat(128);
const SHA512_B = 'b'.repeat(128);

const sdkFile = (rid, name, hash) => ({ name, rid, url: `https://example.invalid/${rid}/${name}`, hash });
const releaseWith = (releaseVersion, sdkVersion, files) => ({ 'release-version': releaseVersion, sdk: { version: sdkVersion, files }, sdks: [{ version: sdkVersion, files }] });

test('The_pins_are_the_parents_decisions_and_nothing_else', () => {
  const loaded = loadStacksLang(stacksJson());
  assert.equal(loaded.ok, true, JSON.stringify(loaded));
  const { pins } = loaded.value;
  assert.equal(pins.dotnetSdk, '10.0.403');
  assert.equal(pins.dotnetRuntime, '10.0.14');
  assert.equal(pins.node, '26.13.0');
  assert.match(pins.typescript, /^6\.0\.\d+$/);
  assert.deepEqual(loaded.value.directories, { dotnetSdk: 'dotnet-sdk-10.0.403', node: 'node-v26.13.0-linux-x64', typescript: `typescript-${pins.typescript}` });
});

test('A_TypeScript_pin_outside_the_6_0_line_is_refused_naming_the_line', () => {
  const seven = stacksJson();
  seven.pins.typescript = '7.0.2';
  seven.directories.typescript = 'typescript-7.0.2';
  const loaded = loadStacksLang(seven);
  assert.equal(loaded.ok, false);
  assert.match(loaded.reason, /typescript.*7\.0\.2.*6\.0/s);
});

test('A_directory_name_that_does_not_carry_its_pin_is_refused', () => {
  const drifted = stacksJson();
  drifted.directories.node = 'node-v26.12.1-linux-x64';
  const loaded = loadStacksLang(drifted);
  assert.equal(loaded.ok, false);
  assert.match(loaded.reason, /directories\.node.*node-v26\.13\.0-linux-x64/s);
});

test('A_probe_is_exe_plus_argv_never_a_shell_string', () => {
  const shelled = stacksJson();
  shelled.probes.node = 'node --version';
  const loaded = loadStacksLang(shelled);
  assert.equal(loaded.ok, false);
  assert.match(loaded.reason, /probes\.node.*argv/s);
});

test('The_SDK_checksum_is_the_one_the_release_metadata_lists_for_that_exact_SDK_and_file', () => {
  const metadata = {
    releases: [
      releaseWith('10.0.13', '10.0.402', [sdkFile('linux-x64', 'dotnet-sdk-linux-x64.tar.gz', SHA512_B)]),
      releaseWith('10.0.14', '10.0.403', [sdkFile('win-x64', 'dotnet-sdk-win-x64.zip', SHA512_B), sdkFile('linux-x64', 'dotnet-sdk-linux-x64.zip', SHA512_B), sdkFile('linux-x64', 'dotnet-sdk-linux-x64.tar.gz', SHA512_A)]),
    ],
  };
  const found = dotnetOfficial(metadata, { version: '10.0.403', rid: 'linux-x64', name: 'dotnet-sdk-linux-x64.tar.gz' });
  assert.equal(found.ok, true, JSON.stringify(found));
  assert.equal(found.value.sha512, SHA512_A, 'not the first release, not another rid, not another archive type');
  assert.equal(found.value.url, 'https://example.invalid/linux-x64/dotnet-sdk-linux-x64.tar.gz');
});

test('An_SDK_the_release_metadata_does_not_list_is_refused_naming_it', () => {
  const metadata = { releases: [releaseWith('10.0.14', '10.0.403', [sdkFile('linux-x64', 'dotnet-sdk-linux-x64.tar.gz', SHA512_A)])] };
  const found = dotnetOfficial(metadata, { version: '10.0.404', rid: 'linux-x64', name: 'dotnet-sdk-linux-x64.tar.gz' });
  assert.equal(found.ok, false);
  assert.match(found.reason, /10\.0\.404/);
});

test('The_Node_checksum_is_the_SHASUMS256_line_of_exactly_the_pinned_tarball', () => {
  const sums = `${'1'.repeat(64)}  node-v26.13.0-linux-x64.tar.gz\n${'2'.repeat(64)}  node-v26.13.0-linux-x64.tar.xz\n${'3'.repeat(64)}  node-v26.13.0-linux-arm64.tar.xz\n`;
  const found = nodeOfficial(sums, 'node-v26.13.0-linux-x64.tar.xz');
  assert.equal(found.ok, true, JSON.stringify(found));
  assert.equal(found.value, '2'.repeat(64), 'not the .tar.gz line and not another architecture');
  const missing = nodeOfficial(sums, 'node-v26.13.0-darwin-x64.tar.xz');
  assert.equal(missing.ok, false);
  assert.match(missing.reason, /node-v26\.13\.0-darwin-x64\.tar\.xz/);
});

const packument = versions => ({ versions: Object.fromEntries(versions.map(v => [v, { dist: { tarball: `https://registry.invalid/typescript-${v}.tgz`, integrity: `sha512-${v}` } }])) });

test('The_TypeScript_integrity_is_npms_dist_integrity_of_exactly_the_pinned_version', () => {
  const found = typescriptOfficial(packument(['6.0.2', '6.0.3', '7.0.2']), '6.0.3');
  assert.equal(found.ok, true, JSON.stringify(found));
  assert.deepEqual(found.value, { tarball: 'https://registry.invalid/typescript-6.0.3.tgz', integrity: 'sha512-6.0.3' });
  assert.equal(typescriptOfficial(packument(['6.0.2']), '6.0.3').ok, false);
});

test('The_newest_patch_of_a_line_is_the_highest_plain_release_never_a_dev_or_candidate_build', () => {
  const versions = ['6.0.0-dev.20260416', '6.0.1-rc', '6.0.2', '6.0.10', '6.0.3', '6.1.0', '7.0.2', '6.0.0-beta'];
  assert.equal(newestPatch(packument(versions), '6.0'), '6.0.10', 'numeric, not lexical: 6.0.10 is newer than 6.0.3');
  assert.equal(newestPatch(packument(['6.0.1-rc']), '6.0'), null);
});
