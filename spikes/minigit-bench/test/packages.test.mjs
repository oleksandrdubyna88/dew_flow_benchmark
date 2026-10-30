// The packages a session pulled (E3b.S1 step 9, M10): listed from the npm cache index and the NuGet packages folder, flagged by the registered rules.
import assert from 'node:assert/strict';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { flagRules, packagesOf, readNpmPackages, readNugetPackages } from '../lib/packages.mjs';
import { spikeDir, tmpDir } from './helpers.mjs';

const RULES = JSON.parse(readFileSync(path.join(spikeDir, 'inputs', 'package-flags.json'), 'utf8')).rules;
const indexLine = key => `deadbeef\t${JSON.stringify({ key: `make-fetch-happen:request-cache:${key}`, integrity: 'sha512-x', time: 1, size: 1 })}\n`;
const tgz = (name, version) => `https://registry.npmjs.org/${name}/-/${name.split('/').at(-1)}-${version}.tgz`;

function fakeNpm(keys) {
  const dir = tmpDir('npm');
  mkdirSync(path.join(dir, '_cacache', 'index-v5', 'ab', 'cd'), { recursive: true });
  writeFileSync(path.join(dir, '_cacache', 'index-v5', 'ab', 'cd', 'entry1'), keys.map(indexLine).join(''));
  return dir;
}
function fakeNuget(list) {
  const dir = tmpDir('nuget');
  for (const [id, version] of list) mkdirSync(path.join(dir, id, version), { recursive: true });
  mkdirSync(path.join(dir, '.tools'), { recursive: true });
  return dir;
}

test('Packages_are_listed_and_flagged', () => {
  const npm = fakeNpm([tgz('minigit-core', '1.0.0'), tgz('typescript', '6.0.3'), tgz('tsx', '4.1.0'), tgz('typescript', '7.0.2'), 'https://registry.npmjs.org/typescript']);
  const nuget = fakeNuget([['newtonsoft.json', '13.0.3']]);
  const snapshot = [{ ecosystem: 'npm', name: 'typescript', version: '7.0.2' }, { ecosystem: 'nuget', name: 'newtonsoft.json', version: '13.0.3' }];
  const r = packagesOf({ npm: readNpmPackages(npm), nuget: readNugetPackages(nuget), snapshot, rules: RULES });
  assert.equal(r.packages.length, 5, 'four npm tarballs and one nuget package; the packument entry (metadata only) is not a package');
  assert.deepEqual(r.flagged.map(f => `${f.rule}:${f.package.name}@${f.package.version}`).sort(), ['minigit-name:minigit-core@1.0.0', 'other-ts-runner-or-compiler:tsx@4.1.0', 'typescript-not-pinned:typescript@6.0.3']);
  assert.deepEqual(r.added.map(p => `${p.name}@${p.version}`).sort(), ['minigit-core@1.0.0', 'tsx@4.1.0', 'typescript@6.0.3'], 'added excludes the snapshot\'s own packages');
  assert.deepEqual(r.flags, ['package: minigit-name', 'package: other-ts-runner-or-compiler', 'package: typescript-not-pinned']);
});

test('A_scoped_npm_package_and_a_nuget_pack_off_the_band_are_read_and_flagged', () => {
  const npm = readNpmPackages(fakeNpm([tgz('@swc/core', '1.3.0'), tgz('@types/node', '26.1.0')]));
  assert.deepEqual(npm.map(p => `${p.name}@${p.version}`).sort(), ['@swc/core@1.3.0', '@types/node@26.1.0']);
  const nuget = readNugetPackages(fakeNuget([['microsoft.netcore.app.ref', '10.0.1'], ['microsoft.netcore.app.runtime.linux-x64', '9.0.5'], ['microsoft.aspnetcore.app.ref', '8.0.0']]));
  const r = packagesOf({ npm, nuget, snapshot: [], rules: RULES });
  assert.deepEqual(r.flagged.map(f => `${f.rule}:${f.package.name}`).sort(), ['dotnet-pack-off-the-10-band:microsoft.aspnetcore.app.ref', 'dotnet-pack-off-the-10-band:microsoft.netcore.app.runtime.linux-x64', 'other-ts-runner-or-compiler:@swc/core']);
});

test('An_absent_cache_is_an_empty_list_and_a_name_matching_either_ecosystem_is_flagged_once_per_package', () => {
  assert.deepEqual(readNpmPackages(path.join(tmpDir('none'), 'absent')), []);
  assert.deepEqual(readNugetPackages(path.join(tmpDir('none'), 'absent')), []);
  const both = packagesOf({ npm: [{ ecosystem: 'npm', name: 'my-minigit', version: '1.0.0' }], nuget: [{ ecosystem: 'nuget', name: 'minigit.core', version: '2.0.0' }], snapshot: [], rules: RULES });
  assert.equal(both.flagged.length, 2);
});

test('The_rules_are_validated_and_an_unknown_ecosystem_names_the_legal_ones', () => {
  assert.throws(() => flagRules([{ id: 'x', ecosystem: 'pip', nameRegex: 'a' }]), /npm, nuget, any/);
  assert.throws(() => flagRules([{ id: 'x', ecosystem: 'npm', nameRegex: '(' }]), /x/);
  assert.equal(flagRules(RULES).length, RULES.length);
});
