// E1.S4, the network cases (T-L13): the REAL snapshot build of both stacks (one run of `make-snapshots.mjs`, shared by every case of this file) and every box case on it. They run only under
// BENCH_REQUIRE_SANDBOX=1 (about 10-20 min): without it every case skips with its reason, and under it a missing bwrap or toolchain, or no network, is a failure. The order of the cases is the order of the story:
// the build, the offered set offline, the missing package, the type-aware lint. Every folder is an owned folder of token `bench` under ~/.cache/lang-v2/t (the agent box replaces /tmp).
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { cpSync, existsSync, readFileSync, readdirSync, lstatSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { after, before, test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { sweepLeftovers } from '../../../.agents/conventions/tools/lib/leftovers-sweep.mjs';
import { treeHash } from '../../minigit-bench/lib/cache-snapshot.mjs';
import { NPM_OFFLINE_ENV, NPMRC_OFFLINE, npmOfflineArgv, offlineNugetConfig, openBox, outsideManifest, usedNpm, usedNuget } from '../lib/cache-offline.mjs';
import { nugetLicence, npmLicences } from '../lib/package-metadata.mjs';
import { manifestSha256Of } from '../lib/cache-store.mjs';
import { closeReal, ensureTestRoot, realBuild, realGate, snapshotOf, toolchains } from './support/cache-box.mjs';
import { ownedFolder } from './toolchain-fixtures.mjs';

const SPIKE = fileURLToPath(new URL('..', import.meta.url));
const REPO = path.resolve(SPIKE, '..', '..');
const STACKS = ['csharp', 'node'];
const HOSTS = ['api.nuget.org', 'registry.npmjs.org'];
const recipe = JSON.parse(readFileSync(path.join(SPIKE, 'inputs', 'cache-recipe-lang.json'), 'utf8'));
const NO_FLAGS = ['--no-audit', '--no-fund', '--no-update-notifier'];

before(() => { sweepLeftovers({ repo: REPO, tmp: ensureTestRoot(), token: 'bench' }); });
after(() => closeReal());

/** A fresh box (own folder, own layout, only the seeded snapshot) for one case. `allow` is the registry list of the refuted shapes; the right one has none. */
async function fresh(t, real, stack, rid, allow = []) {
  const { dirs } = toolchains();
  const opened = await openBox({ stack, snapshot: snapshotOf(real, stack), root: ownedFolder(t, 'off', ensureTestRoot()), rid, dirs, allow });
  assert.equal(opened.ok, true, opened.reason);
  t.after(() => opened.value.close());
  return opened.value;
}

const ran = (r, what) => { assert.equal(r.ok, true, `${what}: ${r.reason}`); return r.value; };
const filled = (text, resolved) => text.replace(/\{([A-Z_]+)\}/g, (m, key) => resolved[key]);
const idsOf = (record, ecosystem) => record.packages.filter(p => p.ecosystem === ecosystem).map(p => `${p.name}@${p.version}`);
const offeredSpecs = real => recipe.stacks.node.packages.map(p => `${p.name}@${filled(p.spec, real.record.node.resolved)}`);
const writeJson = (file, value) => writeFileSync(file, `${JSON.stringify(value, null, 2)}\n`);

// ---- 1. the real build ------------------------------------------------------------------------------------------------------------

function hasNoWriteBits(root) {
  const entries = readdirSync(root, { recursive: true }).map(rel => lstatSync(path.join(root, rel)));
  return [lstatSync(root), ...entries].filter(s => !s.isSymbolicLink()).every(s => (s.mode & 0o222) === 0);
}

test('The_real_build_registers_both_stacks_read_only_and_each_snapshot_re_hashes_to_its_record', { timeout: 45 * 60_000 }, async t => {
  if (!realGate(t)) return;
  const real = await realBuild();
  assert.equal(real.run.code, 0, `the command exited ${real.run.code}: ${real.run.stderr.slice(-3000)}`);
  t.diagnostic(`the real build took ${Math.round(real.run.ms / 1000)} s; ${real.run.stdout.split('\n').filter(l => l.startsWith('built ') || l.startsWith('registered ')).join(' | ')}`);
  assert.deepEqual(Object.keys(real.record).filter(k => k !== 'superseded').sort(), STACKS);
  for (const stack of STACKS) {
    const rec = real.record[stack];
    const folder = path.join(real.caches, `${stack}-${rec.treeSha256}`);
    assert.equal(rec.dirName, `${stack}-${rec.treeSha256}`, 'the folder is named by the whole hash');
    assert.equal(hasNoWriteBits(folder), true, `${stack}: read-only, every entry`);
    assert.equal(treeHash(folder), rec.treeSha256, `${stack}: the folder re-hashes to its record`);
    assert.equal(rec.manifestSha256, manifestSha256Of(rec.packages), `${stack}: the record carries the hash of its sorted manifest`);
    for (const [part, hash] of Object.entries(rec.parts)) assert.equal(treeHash(path.join(folder, part)), hash, `${stack}/${part}`);
    assert.ok(rec.packages.length > 0 && rec.packages.every(p => /^sha512-/.test(p.integrity)), `${stack}: every manifest package has an integrity`);
    assert.equal(rec.recipeSha256, createHash('sha256').update(readFileSync(path.join(SPIKE, 'inputs', 'cache-recipe-lang.json'))).digest('hex'));
    assert.equal(rec.projectSha256, stack === 'csharp' ? treeHash(path.join(SPIKE, recipe.stacks.csharp.project)) : null);
    assert.deepEqual(Object.keys(rec.resolved).sort(), Object.keys(recipe.stacks[stack].resolve).sort(), `${stack}: every x-range was resolved once and recorded`);
    t.diagnostic(`${stack}: ${rec.sizeBytes} bytes, ${rec.packages.length} packages, ${rec.treeSha256}`);
    assert.deepEqual(Object.keys(real.egress[stack].refused), [], `${stack}: no host was refused`);
    assert.deepEqual(Object.keys(real.egress[stack].allowed).filter(h => !HOSTS.includes(h)), [], `${stack}: only the registries were reached`);
  }
  assert.deepEqual(readdirSync(real.caches).filter(n => !/^(csharp|node)-[0-9a-f]{64}$/.test(n)), [], 'no staging folder or claim is left');
  const logs = path.join(real.caches, real.record.node.dirName, 'npm', '_logs');
  assert.equal(existsSync(logs) ? readdirSync(logs).length : 0, 0, 'the npm part holds no log: it would name the operator\'s home in every session');
});

// ---- 2. the offered set, offline ---------------------------------------------------------------------------------------------------

async function nodeOffline(t, real) {
  const box = await fresh(t, real, 'node', 'n1');
  writeJson(path.join(box.layout.work, 'package.json'), { name: 'ws', version: '1.0.0', private: true });
  writeFileSync(path.join(box.layout.work, '.npmrc'), NPMRC_OFFLINE);
  const specs = offeredSpecs(real);
  assert.equal(specs[0], 'fastify@5.12.7', 'T-L13 names fastify first');
  const r = ran(await box.run(npmOfflineArgv(specs), { env: NPM_OFFLINE_ENV }), 'the offline install');
  assert.equal(r.code, 0, r.output.slice(-3000));
  const used = usedNpm(JSON.parse(readFileSync(path.join(box.layout.work, 'package-lock.json'), 'utf8')));
  assert.deepEqual(outsideManifest(used, real.record.node.packages, 'npm'), [], 'every package the install used is in the registered manifest');
  t.diagnostic(`node: manifest minus used (not a failure): ${idsOf(real.record.node, 'npm').filter(id => !used.includes(id)).join(', ') || '(none)'}`);
  for (const name of ['pino', 'typebox']) {
    const held = real.record.node.packages.filter(p => p.name === name).map(p => p.version);
    assert.deepEqual(held, [real.record.node.resolved[name.toUpperCase()]], `exactly one ${name} version in the manifest, the one that was resolved`);
  }
  assert.equal(real.record.node.packages.some(p => p.name === '@sinclair/typebox'), false, '@sinclair/typebox is not in the set (the owner\'s decision)');
  assert.deepEqual(box.egress().allowed, {}, 'no registry was reached');
  assert.deepEqual(box.egress().refused, {}, 'and none was even asked: a refused attempt would be in the log');
}

const props = resolved => Object.entries({ SnapHostingVersion: 'HOSTING', SnapJwtBearerVersion: 'JWT_BEARER', SnapXunitVersion: 'XUNIT', SnapXunitVsVersion: 'XUNIT_VS', SnapTestSdkVersion: 'TEST_SDK' }).map(([prop, key]) => `-p:${prop}=${resolved[key]}`);

async function csharpOffline(t, real) {
  const box = await fresh(t, real, 'csharp', 'n1');
  cpSync(path.join(SPIKE, recipe.stacks.csharp.project), box.layout.work, { recursive: true });
  writeFileSync(path.join(box.layout.work, 'nuget.config'), offlineNugetConfig(box.layout.nuget).value);
  const p = props(real.record.csharp.resolved);
  const steps = [['dotnet', 'restore', 'Snap.slnx', ...p], ['dotnet', 'build', 'Snap.slnx', '-c', 'Release', '--nologo', '-m:4', '-nr:false', '--no-restore', ...p], ['dotnet', 'tests/SnapTests/bin/Release/net10.0/SnapTests.dll']];
  for (const command of steps) {
    const r = ran(await box.run(command), command.slice(0, 2).join(' '));
    assert.equal(r.code, 0, `${command.slice(0, 2).join(' ')}: ${r.output.slice(-3000)}`);
  }
  const used = ['daemon/obj', 'web/obj', 'tests/SnapTests/obj'].flatMap(dir => usedNuget(JSON.parse(readFileSync(path.join(box.layout.work, dir, 'project.assets.json'), 'utf8'))));
  assert.deepEqual(outsideManifest(used, real.record.csharp.packages, 'nuget'), [], 'every package the restore used is in the registered manifest');
  t.diagnostic(`csharp: manifest minus used (not a failure): ${idsOf(real.record.csharp, 'nuget').filter(id => !used.includes(id)).join(', ') || '(none)'}`);
  assert.deepEqual(box.egress().allowed, {}, 'no registry was reached');
  assert.deepEqual(box.egress().refused, {}, 'and none was even asked: a refused attempt would be in the log');
}

test('The_offered_set_restores_builds_and_runs_offline_from_the_snapshot_alone', { timeout: 15 * 60_000 }, async t => {
  if (!realGate(t)) return;
  const real = await realBuild();
  assert.equal(real.run.code, 0, 'the shared build must have succeeded');
  await t.test('node', { timeout: 15 * 60_000 }, st => nodeOffline(st, real));
  await t.test('csharp', { timeout: 15 * 60_000 }, st => csharpOffline(st, real));
});

// ---- 3. a package outside the set --------------------------------------------------------------------------------------------------

async function npmHole(t, real) {
  assert.equal(real.record.node.packages.some(p => p.name === 'left-pad'), false, 'precondition: left-pad is not in the manifest');
  const box = await fresh(t, real, 'node', 'n1');
  writeJson(path.join(box.layout.work, 'package.json'), { name: 'ws', version: '1.0.0', private: true });
  writeFileSync(path.join(box.layout.work, '.npmrc'), NPMRC_OFFLINE);
  const r = ran(await box.run(npmOfflineArgv(['left-pad@1.3.0']), { env: NPM_OFFLINE_ENV }), 'the offline install of a package outside the set');
  assert.notEqual(r.code, 0, `expected a failure naming left-pad, got exit ${r.code}`);
  assert.match(r.output, /ENOTCACHED/);
  assert.match(r.output, /left-pad/);
  assert.deepEqual(box.egress().allowed, {}, 'the registry was never reached');
  // the refuted shape: a box that can reach the registry, without the offline flags, fills the hole silently
  const open = await fresh(t, real, 'node', 'n2', ['registry.npmjs.org']);
  writeJson(path.join(open.layout.work, 'package.json'), { name: 'ws', version: '1.0.0', private: true });
  const filledIn = ran(await open.run(['npm', 'install', ...NO_FLAGS, 'left-pad@1.3.0'], { env: { npm_config_update_notifier: 'false', npm_config_logs_max: '0' } }), 'the same install with a reachable registry');
  assert.equal(filledIn.code, 0, filledIn.output.slice(-2000));
  assert.equal(existsSync(path.join(open.layout.work, 'node_modules', 'left-pad')), true, 'a reachable registry fills the hole silently: that is why the box refuses it');
  assert.deepEqual(Object.keys(open.egress().allowed), ['registry.npmjs.org']);
}

const consoleProject = `<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework></PropertyGroup>
  <ItemGroup><PackageReference Include="Newtonsoft.Json" Version="13.0.3" /></ItemGroup>
</Project>
`;

async function nugetHole(t, real) {
  assert.equal(real.record.csharp.packages.some(p => p.name === 'newtonsoft.json'), false, 'precondition: newtonsoft.json is not in the manifest');
  const box = await fresh(t, real, 'csharp', 'n1');
  writeFileSync(path.join(box.layout.work, 'App.csproj'), consoleProject);
  writeFileSync(path.join(box.layout.work, 'nuget.config'), offlineNugetConfig(box.layout.nuget).value);
  const r = ran(await box.run(['dotnet', 'restore', 'App.csproj']), 'the offline restore of a package outside the set');
  assert.notEqual(r.code, 0, `expected a failure naming Newtonsoft.Json, got exit ${r.code}`);
  assert.match(r.output, /NU1101/);
  assert.match(r.output, /Newtonsoft\.Json/i);
  assert.deepEqual(box.egress().allowed, {}, 'the registry was never reached');
  const open = await fresh(t, real, 'csharp', 'n2', ['api.nuget.org']);
  writeFileSync(path.join(open.layout.work, 'App.csproj'), consoleProject);
  cpSync(path.join(SPIKE, recipe.stacks.csharp.project, 'nuget.config'), path.join(open.layout.work, 'nuget.config'));
  const filledIn = ran(await open.run(['dotnet', 'restore', 'App.csproj']), 'the same restore with a reachable registry');
  assert.equal(filledIn.code, 0, filledIn.output.slice(-2000));
  assert.deepEqual(Object.keys(open.egress().allowed), ['api.nuget.org']);
}

/** The version cases the agent-visible sentence of PACKAGES.md states (fix batch 1, M3). */
async function versionCases(t, real) {
  const npm = await fresh(t, real, 'node', 'n1');
  writeJson(path.join(npm.layout.work, 'package.json'), { name: 'ws', version: '1.0.0', private: true });
  writeFileSync(path.join(npm.layout.work, '.npmrc'), NPMRC_OFFLINE);
  const lower = ran(await npm.run(npmOfflineArgv(['pino@9.14.0']), { env: NPM_OFFLINE_ENV }), 'npm: a version below the listed one');
  assert.notEqual(lower.code, 0);
  assert.match(lower.output, /ENOTCACHED/, 'npm: a version that is not listed fails ENOTCACHED (the message may name a dependency)');
  const cs = await fresh(t, real, 'csharp', 'n2');
  const project = version => `<Project Sdk="Microsoft.NET.Sdk">\n  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework></PropertyGroup>\n  <ItemGroup><PackageReference Include="Microsoft.Extensions.Hosting" Version="${version}" /></ItemGroup>\n</Project>\n`;
  writeFileSync(path.join(cs.layout.work, 'nuget.config'), offlineNugetConfig(cs.layout.nuget).value);
  writeFileSync(path.join(cs.layout.work, 'App.csproj'), project('10.0.0'));
  const below = ran(await cs.run(['dotnet', 'restore', 'App.csproj']), 'C#: a version below the listed one');
  assert.equal(below.code, 0, below.output.slice(-2000));
  assert.match(below.output, /NU1603/, 'C#: a lower version resolves to the listed one, with the warning NU1603');
  const assets = JSON.parse(readFileSync(path.join(cs.layout.work, 'obj', 'project.assets.json'), 'utf8'));
  assert.ok(Object.keys(assets.libraries).includes(`Microsoft.Extensions.Hosting/${real.record.csharp.resolved.HOSTING}`), 'and the listed version is what was restored');
  // fix batch 2 (N2): where warnings are errors the same lower version fails the build, and NU1603 is the error
  writeFileSync(path.join(cs.layout.work, 'App.csproj'), project('10.0.0').replace('<OutputType>', '<TreatWarningsAsErrors>true</TreatWarningsAsErrors><OutputType>'));
  const strict = ran(await cs.run(['dotnet', 'restore', 'App.csproj']), 'C#: a lower version where warnings are errors');
  assert.equal(strict.code, 1, strict.output.slice(-2000));
  assert.match(strict.output, /error NU1603/, 'NU1603 is an error under TreatWarningsAsErrors');
  writeFileSync(path.join(cs.layout.work, 'App.csproj'), project('10.0.99'));
  const above = ran(await cs.run(['dotnet', 'restore', 'App.csproj']), 'C#: a version above the listed one');
  assert.notEqual(above.code, 0);
  assert.match(above.output, /NU1102/, 'C#: a higher version fails with NU1102');
  assert.match(above.output, /Microsoft\.Extensions\.Hosting/, 'and the error names the package');
}

test('A_registry_reachable_from_the_box_would_hide_a_missing_package_and_that_is_the_point', { timeout: 15 * 60_000 }, async t => {
  if (!realGate(t)) return;
  const real = await realBuild();
  assert.equal(real.run.code, 0, 'the shared build must have succeeded');
  await t.test('npm', { timeout: 15 * 60_000 }, st => npmHole(st, real));
  await t.test('nuget', { timeout: 15 * 60_000 }, st => nugetHole(st, real));
  await t.test('versions', { timeout: 15 * 60_000 }, st => versionCases(st, real));
});

// ---- 4. the type-aware lint under the pinned TypeScript --------------------------------------------------------------------------

const LINT_PACKAGES = ['typescript', 'typescript-eslint', 'eslint', '@eslint/js', '@types/node'];
const lintWorkspace = (box, real, over = {}) => {
  cpSync(path.join(SPIKE, 'test', 'fixtures', 'lint-ts'), box.layout.work, { recursive: true });
  const versions = Object.fromEntries(LINT_PACKAGES.map(name => [name, real.record.node.packages.find(p => p.name === name).version]));
  writeJson(path.join(box.layout.work, 'package.json'), { name: 'lint', version: '1.0.0', private: true, type: 'module', devDependencies: { ...versions, ...over } });
};
const eslint = box => box.run(['node', 'node_modules/eslint/bin/eslint.js', 'src/main.ts']);

test('Type_aware_lint_runs_under_the_pinned_TypeScript', { timeout: 15 * 60_000 }, async t => {
  if (!realGate(t)) return;
  const real = await realBuild();
  assert.equal(real.run.code, 0, 'the shared build must have succeeded');
  const box = await fresh(t, real, 'node', 'n1');
  lintWorkspace(box, real);
  const installed = ran(await box.run(npmOfflineArgv([]), { env: NPM_OFFLINE_ENV }), 'the offline install of the lint set');
  assert.equal(installed.code, 0, installed.output.slice(-3000));
  const linted = ran(await eslint(box), 'eslint under the pinned TypeScript');
  assert.equal(linted.code, 1, linted.output.slice(-2000));
  assert.match(linted.output, /@typescript-eslint\/no-floating-promises/, 'a rule that needs type information fired: the parser had the TS program');
  // the other arm, kept: TypeScript 7.0.2 (outside the linter's peer range and outside the set): typescript-eslint refuses it outright ("does not support TS 7.0"), so the lint cannot run with types
  const open = await fresh(t, real, 'node', 'n2', ['registry.npmjs.org']);
  lintWorkspace(open, real, { typescript: '7.0.2' });
  const seven = ran(await open.run(['npm', 'install', '--legacy-peer-deps', ...NO_FLAGS], { env: { npm_config_update_notifier: 'false', npm_config_logs_max: '0' } }), 'the install of TypeScript 7.0.2');
  assert.equal(seven.code, 0, seven.output.slice(-2000));
  const unlinted = ran(await eslint(open), 'eslint under TypeScript 7.0.2');
  assert.doesNotMatch(unlinted.output, /no-floating-promises/, 'under 7.0.2 the type-aware rule cannot fire');
  assert.equal(unlinted.code, 2, 'typescript-eslint refuses to run: ESLint exits 2, not 1 (a finding)');
  assert.match(unlinted.output, /typescript-eslint does not support TS 7\.0/, 'with its own refusal text (observed with typescript-eslint 8.72.1 and typescript 7.0.2)');
});

// ---- the licences of the offered packages are the ones the snapshot's own metadata declares (fix batch 1, C7) ----------------------------

test('The_licence_of_every_offered_package_is_the_one_its_own_metadata_in_the_snapshot_declares', { timeout: 15 * 60_000 }, async t => {
  if (!realGate(t)) return;
  const real = await realBuild();
  assert.equal(real.run.code, 0, 'the shared build must have succeeded');
  for (const stack of STACKS) {
    const [rec, part] = [real.record[stack], stack === 'node' ? 'npm' : 'nuget'];
    const dir = path.join(real.caches, rec.dirName, part);
    const wanted = recipe.stacks[stack].packages.map(p => ({ name: p.name, version: filled(p.spec, rec.resolved) }));
    const npm = stack === 'node' ? npmLicences(dir, wanted) : null; // the npm index is read once for the whole stack
    for (const p of recipe.stacks[stack].packages) {
      const version = filled(p.spec, rec.resolved);
      const declared = stack === 'node' ? npm.get(`${p.name}@${version}`) : nugetLicence(dir, p.name, version);
      assert.equal(p.licence, declared, `${stack}: ${p.name}@${version} declares ${declared}, the recipe says ${p.licence}`);
      assert.match(p.maintenance.releasedOn, /^\d{4}-\d{2}-\d{2}$/, `${p.name}: the date of the latest release the registry showed`);
    }
  }
});
