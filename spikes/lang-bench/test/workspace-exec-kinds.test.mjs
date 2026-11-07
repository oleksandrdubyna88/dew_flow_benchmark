// E1.S3 phase A, the failure cases and the process shape on the execution layer (the real toolchains and the registered snapshots): a package outside the set, a lower version, the offered packages an agent
// may add, the type check and the lint of the Node workspace, the read-only npm cache that would look like a missing package, T-L31 (`run.sh` ends in `exec`, so the program has no bash parent and SIGTERM
// reaches it), the same templates in the agent box, and the stability of the lock generation. The hello builds, the verdicts and the build box itself are in workspace-exec.test.mjs.
import assert from 'node:assert/strict';
import { cpSync, existsSync, mkdirSync, readFileSync, readdirSync, writeFileSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { after, before, test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { sweepLeftovers } from '../../../.agents/conventions/tools/lib/leftovers-sweep.mjs';
import { runBounded } from '../../minigit-bench/lib/run.mjs';
import { nodeFs, excludedDirs } from '../../minigit-bench/lib/score-deps.mjs';
import { npmOfflineArgv, openBox } from '../lib/cache-offline.mjs';
import { boxEnv } from '../lib/exec-layer/env.mjs';
import { statOf } from '../lib/exec-layer/proc.mjs';
import { defaultCaches, snapshotDirOf } from '../lib/workspace-box.mjs';
import { runShProblem } from '../lib/workspace-build.mjs';
import { ensureTestRoot } from './support/cache-box.mjs';
import { chain, commOf, needs, pidsIn, waitFor } from './support/exec-chain.mjs';
import { toolchains } from './support/exec-layer-host.mjs';
import { assertSnapshotsUnchanged, buildHello, exeBase, helloWorkspace, inBox, outputOf, programPidIn, snapshotRecord, snapshotsMissing, STACKS, stopStubSource, TEMPLATES } from './support/workspace-fixtures.mjs';
import { ownedFolder } from './toolchain-fixtures.mjs';

const REPO = path.resolve(fileURLToPath(new URL('../../..', import.meta.url)));
const SPIKE = path.join(REPO, 'spikes', 'lang-bench');
const MIB = 1024 * 1024;
const TIMEOUT = { timeout: 15 * 60_000 };
const read = file => readFileSync(file, 'utf8');
const NPM_INSTALL_LOCK = ['npm', 'install', '--package-lock-only', '--offline', '--ignore-scripts', '--no-audit', '--no-fund'];

before(() => { sweepLeftovers({ repo: REPO, tmp: ensureTestRoot(), token: 'bench' }); });
after(() => { if (snapshotsMissing() === null) assertSnapshotsUnchanged(); });

/** Adds `<PackageReference Include=name Version=version />` before `</Project>` of the workspace's App.csproj. */
function addReference(w, name, version) {
  const file = path.join(w, 'App.csproj');
  writeFileSync(file, read(file).replace('</Project>', `  <ItemGroup>\n    <PackageReference Include="${name}" Version="${version}" />\n  </ItemGroup>\n</Project>`));
}

// ---- a read-only npm cache would look like a missing package -----------------------------------------------------------------------------------------

test('A_read_only_npm_cache_would_look_like_a_missing_package_and_that_is_the_point', TIMEOUT, async t => {
  if (!needs(t, snapshotsMissing())) return;
  const folder = label => {
    const dir = ownedFolder(t, label, ensureTestRoot());
    for (const name of ['package.json', '.npmrc']) cpSync(path.join(TEMPLATES, 'node', 'olympiad', name), path.join(dir, name));
    return dir;
  };
  const wrong = await inBox(folder('wrong'), 'node', NPM_INSTALL_LOCK, { seed: false });
  assert.notEqual(wrong.code, 0, 'a cache bound read-only and not copied cannot serve a packument');
  assert.match(outputOf(wrong), /ENOTCACHED/, 'the misleading error: the package IS in the cache');
  const right = await inBox(folder('right'), 'node', NPM_INSTALL_LOCK);
  assert.equal(right.code, 0, outputOf(right).slice(-2000));
});

// ---- a package outside the set, a lower version -----------------------------------------------------------------------------------------------------------

test('A_csharp_package_outside_the_set_fails_naming_it', TIMEOUT, async t => {
  if (!needs(t, snapshotsMissing())) return;
  const w = helloWorkspace(t, { stack: 'csharp', kind: 'olympiad' });
  addReference(w, 'Newtonsoft.Json', '13.0.3');
  assert.equal(snapshotRecord().csharp.packages.some(p => p.name === 'newtonsoft.json'), false, 'precondition: the package is not in the manifest');
  const ran = await inBox(w, 'csharp', ['bash', 'build.sh']);
  assert.notEqual(ran.code, 0);
  assert.match(outputOf(ran), /NU1101/);
  assert.match(outputOf(ran), /Newtonsoft\.Json/i);
});

test('A_lower_csharp_version_fails_with_NU1603', TIMEOUT, async t => {
  if (!needs(t, snapshotsMissing())) return;
  const w = helloWorkspace(t, { stack: 'csharp', kind: 'web' });
  const file = path.join(w, 'App.csproj');
  writeFileSync(file, read(file).replace('Authentication.JwtBearer" Version="10.0.14"', 'Authentication.JwtBearer" Version="10.0.1"'));
  const ran = await inBox(w, 'csharp', ['bash', 'build.sh']);
  assert.notEqual(ran.code, 0);
  assert.match(outputOf(ran), /NU1603/);
  assert.match(outputOf(ran), /JwtBearer/i);
  writeFileSync(file, read(file).replace('Version="10.0.1"', 'Version="10.0.99"'));
  const higher = await inBox(w, 'csharp', ['bash', 'build.sh']);
  assert.notEqual(higher.code, 0, 'a higher version is an error by default, with no policy of ours');
  assert.match(outputOf(higher), /NU1102/);
});

test('A_package_downgrade_fails_the_csharp_build_with_NU1605', TIMEOUT, async t => {
  if (!needs(t, snapshotsMissing())) return;
  const record = snapshotRecord().csharp;
  const lower = record.packages.filter(p => p.name === 'microsoft.extensions.logging.abstractions' && p.version !== record.resolved.HOSTING).map(p => p.version);
  assert.equal(lower.length, 1, `precondition: the manifest holds one older version of a package that Hosting needs at ${record.resolved.HOSTING} (${lower})`);
  const w = helloWorkspace(t, { stack: 'csharp', kind: 'daemon' });
  addReference(w, 'Microsoft.Extensions.Logging.Abstractions', lower[0]);
  const ran = await inBox(w, 'csharp', ['bash', 'build.sh']);
  assert.notEqual(ran.code, 0, 'a version below the one Hosting needs is a downgrade, and the SDK default makes it an error');
  assert.match(outputOf(ran), /NU1605/);
});

test('A_node_package_outside_the_set_fails_naming_it', TIMEOUT, async t => {
  if (!needs(t, snapshotsMissing())) return;
  const w = helloWorkspace(t, { stack: 'node', kind: 'olympiad' });
  const install = await inBox(w, 'node', npmOfflineArgv(['left-pad@1.3.0']));
  assert.notEqual(install.code, 0);
  assert.match(outputOf(install), /ENOTCACHED/);
  assert.match(outputOf(install), /left-pad/);
  const edited = helloWorkspace(t, { stack: 'node', kind: 'olympiad', label: 'edit' });
  const file = path.join(edited, 'package.json');
  const json = JSON.parse(read(file));
  writeFileSync(file, `${JSON.stringify({ ...json, dependencies: { 'left-pad': '1.3.0' } }, null, 2)}\n`);
  const build = await inBox(edited, 'node', ['bash', 'build.sh']);
  assert.notEqual(build.code, 0);
  assert.match(outputOf(build), /left-pad/);
  assert.match(outputOf(build), /ENOTCACHED/);
});

// ---- an agent may add a listed package ----------------------------------------------------------------------------------------------------------------------

test('An_agent_may_add_a_listed_package_and_the_build_still_passes', TIMEOUT, async top => {
  if (!needs(top, snapshotsMissing())) return;
  await top.test('csharp', async t => {
    const w = helloWorkspace(t, { stack: 'csharp', kind: 'olympiad' });
    addReference(w, 'Microsoft.Extensions.Hosting', snapshotRecord().csharp.resolved.HOSTING);
    const ran = await inBox(w, 'csharp', ['bash', 'build.sh']);
    assert.equal(ran.code, 0, outputOf(ran).slice(-3000));
  });
  await top.test('csharp with a lock file', async t => {
    for (const locked of [false, true]) {
      const w = helloWorkspace(t, { stack: 'csharp', kind: 'olympiad', label: locked ? 'lock' : 'free' });
      const props = path.join(w, 'Directory.Build.props');
      const mode = `<RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>${locked ? '<RestoreLockedMode>true</RestoreLockedMode>' : ''}`;
      writeFileSync(props, read(props).replace('</PropertyGroup>', `${mode}</PropertyGroup>`));
      const first = await inBox(w, 'csharp', ['bash', 'build.sh']);
      assert.equal(first.code, 0, outputOf(first).slice(-2000));
      addReference(w, 'Microsoft.Extensions.Hosting', snapshotRecord().csharp.resolved.HOSTING);
      const second = await inBox(w, 'csharp', ['bash', 'build.sh']);
      if (locked) {
        assert.notEqual(second.code, 0, 'with RestoreLockedMode a package added after the lock file was written fails: that is why the template does not set it');
        assert.match(outputOf(second), /NU1004/);
      } else {
        assert.equal(second.code, 0, outputOf(second).slice(-2000));
      }
    }
  });
  await top.test('node', async t => {
    const w = helloWorkspace(t, { stack: 'node', kind: 'olympiad' });
    const added = await inBox(w, 'node', npmOfflineArgv([`fastify@${snapshotRecord().node.packages.find(p => p.name === 'fastify').version}`]));
    assert.equal(added.code, 0, outputOf(added).slice(-3000));
    const ran = await inBox(w, 'node', ['bash', 'build.sh']);
    assert.equal(ran.code, 0, outputOf(ran).slice(-3000));
  });
});

// ---- the C# build policy (warnings, the command line, the tests folder) -------------------------------------------------------------------------------------

const PROGRAM = 'Program.cs';
const withProgram = (w, text) => writeFileSync(path.join(w, PROGRAM), text);
const NOT_AWAITED = 'Task.Delay(1);\n';

test('A_warning_does_not_fail_the_csharp_build_but_a_nullable_error_does', TIMEOUT, async t => {
  if (!needs(t, snapshotsMissing())) return;
  const w = helloWorkspace(t, { stack: 'csharp', kind: 'olympiad' });
  const original = read(path.join(w, PROGRAM));
  const clean = await inBox(w, 'csharp', ['bash', 'build.sh']);
  assert.equal(clean.code, 0, outputOf(clean).slice(-2000));
  assert.ok(existsSync(path.join(w, 'bin', 'Release', 'net10.0', 'App.dll')), 'the run still finds bin/Release/net10.0/App.dll');
  for (const [what, code, warning] of [['an unused local', `int unused = 1;\n${original}`, 'CS0219'], ['a call not awaited', `${NOT_AWAITED}${original}`, 'CS4014'], ['unreachable code', `${original}\nreturn;\nConsole.Out.Write("x");\n`, 'CS0162']]) {
    withProgram(w, code);
    const ran = await inBox(w, 'csharp', ['bash', 'build.sh']);
    assert.equal(ran.code, 0, `${what} is a warning and the build passes: ${outputOf(ran).slice(-1500)}`);
    assert.match(outputOf(ran), new RegExp(warning), `${what}: the warning is still shown`);
  }
  withProgram(w, `string s = null;\n${original}`);
  const nullable = await inBox(w, 'csharp', ['bash', 'build.sh']);
  assert.notEqual(nullable.code, 0, 'a nullable warning is an error');
  assert.match(outputOf(nullable), /CS8600/);
});

test('A_nullable_error_fails_bash_build_sh_even_after_a_plain_build_compiled_the_same_sources', TIMEOUT, async t => {
  if (!needs(t, snapshotsMissing())) return;
  const w = helloWorkspace(t, { stack: 'csharp', kind: 'olympiad' });
  withProgram(w, 'string s = null;\nConsole.Out.Write(s);\n');
  const plain = await inBox(w, 'csharp', ['dotnet', 'build', 'App.csproj', '-c', 'Release', '-m:4']);
  assert.equal(plain.code, 0, `a plain build compiles the sources with only a warning: ${outputOf(plain).slice(-1500)}`);
  assert.match(outputOf(plain), /CS8600/);
  const scripted = await inBox(w, 'csharp', ['bash', 'build.sh']);
  assert.notEqual(scripted.code, 0, 'the compile is not skipped as up to date: the agent sees the same result as a clean copy');
  assert.match(outputOf(scripted), /CS8600/);
});

test('A_clean_program_builds_twice_and_dotnet_test_between_two_builds_still_works', TIMEOUT, async t => {
  if (!needs(t, snapshotsMissing())) return;
  const w = helloWorkspace(t, { stack: 'csharp', kind: 'olympiad' });
  mkdirSync(path.join(w, 'tests'));
  writeFileSync(path.join(w, 'tests', 'Tests.csproj'), `<Project Sdk="Microsoft.NET.Sdk">\n  <PropertyGroup><OutputType>Exe</OutputType></PropertyGroup>\n  <ItemGroup><PackageReference Include="xunit.v3" Version="${snapshotRecord().csharp.resolved.XUNIT}" /></ItemGroup>\n</Project>\n`);
  writeFileSync(path.join(w, 'tests', 'UnitTest1.cs'), 'using Xunit;\npublic class UnitTest1 { [Fact] public void Passes() => Assert.True(true); }\n');
  const dll = path.join(w, 'bin', 'Release', 'net10.0', 'App.dll');
  for (const step of ['first build', 'dotnet test', 'second build']) {
    const ran = await inBox(w, 'csharp', step === 'dotnet test' ? ['dotnet', 'test', '--project', 'tests/Tests.csproj'] : ['bash', 'build.sh']);
    assert.equal(ran.code, 0, `${step}: ${outputOf(ran).slice(-1500)}`);
    assert.ok(existsSync(dll), `${step}: App.dll is present`);
  }
});

test('Nested_bin_and_obj_folders_are_not_compiled_so_a_build_in_place_and_on_the_clean_copy_agree', TIMEOUT, async t => {
  if (!needs(t, snapshotsMissing())) return;
  const w = helloWorkspace(t, { stack: 'csharp', kind: 'olympiad' });
  for (const folder of ['Lib/obj', 'Lib/bin', 'node_modules/pkg']) {
    mkdirSync(path.join(w, folder), { recursive: true });
    writeFileSync(path.join(w, folder, 'Extra.cs'), 'this is not a C# file\n');
  }
  const inPlace = await inBox(w, 'csharp', ['bash', 'build.sh']);
  assert.equal(inPlace.code, 0, `the sources in bin, obj and node_modules folders at any depth are not compiled: ${outputOf(inPlace).slice(-1500)}`);
  const copy = path.join(ownedFolder(t, 'cpy', ensureTestRoot()), 'w');
  nodeFs.copyTree(w, copy, excludedDirs('csharp'));
  assert.equal(existsSync(path.join(copy, 'Lib', 'obj')), false, 'the scorer copy leaves them out at any depth');
  const onCopy = await inBox(copy, 'csharp', ['bash', 'build.sh']);
  assert.equal(onCopy.code, 0, outputOf(onCopy).slice(-1500));
});

const BROKEN_SOURCE = 'this is not a C# file\n';

/** The daemon template's workspace with a broken `Extra.cs` in each of `folders`. */
function withBrokenIn(t, folders) {
  const w = helloWorkspace(t, { stack: 'csharp', kind: 'daemon' });
  for (const folder of folders) {
    mkdirSync(path.join(w, folder), { recursive: true });
    writeFileSync(path.join(w, folder, 'Extra.cs'), BROKEN_SOURCE);
  }
  return w;
}

test('A_folder_named_Bin_in_another_letter_case_is_left_out_of_the_csharp_compile_in_place_and_on_the_clean_copy', TIMEOUT, async t => {
  if (!needs(t, snapshotsMissing())) return;
  const folders = ['Bin', 'OBJ', 'Node_Modules'];
  const w = withBrokenIn(t, folders);
  const inPlace = await inBox(w, 'csharp', ['bash', 'build.sh']);
  assert.equal(inPlace.code, 0, `the default compile items leave these folders out whatever their letter case: ${outputOf(inPlace).slice(-1500)}`);
  const copy = path.join(ownedFolder(t, 'cpy', ensureTestRoot()), 'w');
  nodeFs.copyTree(w, copy, excludedDirs('csharp'));
  for (const folder of folders) assert.equal(existsSync(path.join(copy, folder, 'Extra.cs')), true, `the scorer copy (the scorer's own excludedDirs list; lang-bench never imports lang-explore) drops only the exact names, so ${folder} is kept`);
  const onCopy = await inBox(copy, 'csharp', ['bash', 'build.sh']);
  assert.equal(onCopy.code, 0, outputOf(onCopy).slice(-1500));
});

test('A_folder_of_another_name_holding_the_same_text_fails_the_build_and_that_is_the_point', TIMEOUT, async t => {
  if (!needs(t, snapshotsMissing())) return;
  const w = withBrokenIn(t, ['Bim']);
  const ran = await inBox(w, 'csharp', ['bash', 'build.sh']);
  assert.notEqual(ran.code, 0, 'a folder that is not one of the three names is compiled, so the text that does not compile fails the build');
  assert.match(outputOf(ran), /Extra\.cs/, 'the failure names the file');
});

test('A_second_project_built_first_does_not_break_the_root_build', TIMEOUT, async t => {
  if (!needs(t, snapshotsMissing())) return;
  const w = helloWorkspace(t, { stack: 'csharp', kind: 'olympiad' });
  mkdirSync(path.join(w, 'Helper'));
  writeFileSync(path.join(w, 'Helper', 'Helper.csproj'), '<Project Sdk="Microsoft.NET.Sdk">\n  <PropertyGroup><OutputType>Library</OutputType></PropertyGroup>\n</Project>\n');
  writeFileSync(path.join(w, 'Helper', 'Helper.cs'), 'namespace Helper;\npublic static class Greeting { public static string Of(string name) => $"hello {name}"; }\n');
  const helper = await inBox(w, 'csharp', ['dotnet', 'build', 'Helper/Helper.csproj', '-c', 'Release', '-m:4']);
  assert.equal(helper.code, 0, outputOf(helper).slice(-1500));
  assert.ok(existsSync(path.join(w, 'Helper', 'obj')), 'the helper left its own obj folder');
  const root = await inBox(w, 'csharp', ['bash', 'build.sh']);
  assert.equal(root.code, 0, `no CS0579 from the helper's generated files: ${outputOf(root).slice(-1500)}`);
});

test('All_three_kinds_build_then_dotnet_test_with_a_project_reference_then_build_again', TIMEOUT, async top => {
  if (!needs(top, snapshotsMissing())) return;
  for (const kind of ['olympiad', 'daemon', 'web']) {
    await top.test(kind, async t => {
      const w = helloWorkspace(t, { stack: 'csharp', kind });
      mkdirSync(path.join(w, 'tests'));
      writeFileSync(path.join(w, 'tests', 'Tests.csproj'), `<Project Sdk="Microsoft.NET.Sdk">\n  <PropertyGroup><OutputType>Exe</OutputType></PropertyGroup>\n  <ItemGroup><PackageReference Include="xunit.v3" Version="${snapshotRecord().csharp.resolved.XUNIT}" /><ProjectReference Include="../App.csproj" /></ItemGroup>\n</Project>\n`);
      writeFileSync(path.join(w, 'tests', 'UnitTest1.cs'), 'using Xunit;\npublic class UnitTest1 { [Fact] public void Passes() => Assert.True(true); }\n');
      for (const step of ['first build', 'dotnet test', 'second build']) {
        const ran = await inBox(w, 'csharp', step === 'dotnet test' ? ['dotnet', 'test', '--project', 'tests/Tests.csproj'] : ['bash', 'build.sh']);
        assert.equal(ran.code, 0, `${kind}, ${step}: ${outputOf(ran).slice(-1500)}`);
      }
    });
  }
});

// The command line forces the policy as global properties, which a plain property in a project file cannot override. Measured 2026-11-04 (exit code of `bash build.sh` for `string s = null;` with the override in App.csproj): with the first
// line (`TreatWarningsAsErrors` and `WarningsAsErrors` only) two overrides were stopped and four were not; with `Nullable`, `WarningsNotAsErrors`, `WarningLevel` and `NoWarn` forced as well, all six fail with CS8600.
// The further rows are pinned below. Not stopped, from App.csproj alone: a local `Nullable`, a target that sets it, a response file, `NoWarn` metadata on a package item, and a source pragma. The rule for E3's scorer is:
// App.csproj may differ from the template only by `<PackageReference Include Version>` items and nothing else (and a source pragma is symmetric with `@ts-ignore`). Superseded by parent D65 (the scorer scores as submitted, restores the frozen files and regenerates nothing); the text is kept as it was written.
const OVERRIDES = Object.freeze([
  ['Nullable=disable', '<Nullable>disable</Nullable>'], ['TreatWarningsAsErrors=false', '<TreatWarningsAsErrors>false</TreatWarningsAsErrors>'], ['WarningsAsErrors empty', '<WarningsAsErrors />'],
  ['WarningsNotAsErrors=nullable', '<WarningsNotAsErrors>nullable</WarningsNotAsErrors>'], ['NoWarn=CS8600', '<NoWarn>CS8600</NoWarn>'], ['WarningLevel=0', '<WarningLevel>0</WarningLevel>'],
]);

const NULL_PROGRAM = 'string s = null;\nConsole.Out.Write(s);\n';
const RELAX_TARGET = '<Project><Target Name="Relax" BeforeTargets="CoreCompile"><PropertyGroup><Nullable>disable</Nullable></PropertyGroup></Target></Project>\n';
const LOGIC_CLASS = 'static class Logic { public static string Make() { string s = null; return s; } }\n';
const editProject = (w, edit) => { const file = path.join(w, 'App.csproj'); writeFileSync(file, edit(read(file))); };
/** The rows after the six: what the doc table says (`doc`), whether the command line stops it, the kind it is tried in, and what puts it in the workspace. */
const FURTHER = Object.freeze([
  { doc: '`TreatAsLocalProperty="Nullable"` on `<Project>` with `<Nullable>disable</Nullable>`', stopped: false, kind: 'olympiad', setup: w => { withProgram(w, NULL_PROGRAM); editProject(w, text => text.replace('<Project Sdk="Microsoft.NET.Sdk">', '<Project Sdk="Microsoft.NET.Sdk" TreatAsLocalProperty="Nullable">').replace('<AssemblyName>', '<Nullable>disable</Nullable><AssemblyName>')); } },
  { doc: '`<Target BeforeTargets="CoreCompile">` setting `Nullable` to `disable`', stopped: false, kind: 'olympiad', setup: w => { withProgram(w, NULL_PROGRAM); editProject(w, text => text.replace('</Project>', '<Target Name="Relax" BeforeTargets="CoreCompile"><PropertyGroup><Nullable>disable</Nullable></PropertyGroup></Target></Project>')); } },
  { doc: '`<CompilerResponseFile>` naming a file with `-nowarn:CS8600`', stopped: false, kind: 'olympiad', setup: w => { withProgram(w, NULL_PROGRAM); writeFileSync(path.join(w, 'relax.rsp'), '-nowarn:CS8600\n'); editProject(w, text => text.replace('<AssemblyName>', '<CompilerResponseFile>relax.rsp</CompilerResponseFile><AssemblyName>')); } },
  { doc: '`NoWarn="NU1603"` metadata on a `<PackageReference>` of a lower version', stopped: false, kind: 'web', setup: w => editProject(w, text => text.replace('Version="10.0.14"', 'Version="10.0.1" NoWarn="NU1603"')) },
  { doc: '`NoWarn="NU1605"` metadata on a `<PackageReference>` that downgrades', stopped: false, kind: 'daemon', setup: w => editProject(w, text => text.replace('</Project>', '<ItemGroup><PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="8.0.0" NoWarn="NU1605" /></ItemGroup></Project>')) },
  { doc: '`.editorconfig` with `dotnet_diagnostic.CS8600.severity = none`', stopped: true, kind: 'olympiad', setup: w => { withProgram(w, NULL_PROGRAM); writeFileSync(path.join(w, '.editorconfig'), 'root = true\n[*.cs]\ndotnet_diagnostic.CS8600.severity = none\n'); } },
  { doc: '`.globalconfig` with `dotnet_diagnostic.CS8600.severity = none`', stopped: true, kind: 'olympiad', setup: w => { withProgram(w, NULL_PROGRAM); writeFileSync(path.join(w, '.globalconfig'), 'is_global = true\ndotnet_diagnostic.CS8600.severity = none\n'); } },
  { doc: '`Directory.Build.targets` with a static `<Nullable>disable</Nullable>`', stopped: true, kind: 'olympiad', setup: w => { withProgram(w, NULL_PROGRAM); writeFileSync(path.join(w, 'Directory.Build.targets'), '<Project><PropertyGroup><Nullable>disable</Nullable></PropertyGroup></Project>\n'); } },
  { doc: '`Directory.Build.rsp` with `-p:Nullable=disable` and `-p:NoWarn=CS8600`', stopped: true, kind: 'olympiad', setup: w => { withProgram(w, NULL_PROGRAM); writeFileSync(path.join(w, 'Directory.Build.rsp'), '-p:Nullable=disable\n-p:NoWarn=CS8600\n'); } },
  { doc: '`#pragma warning disable CS8600` at the top of `Program.cs`', stopped: false, kind: 'olympiad', setup: w => withProgram(w, `#pragma warning disable CS8600\n${NULL_PROGRAM}`) },
  { doc: '`Directory.Build.targets` with a `<Target BeforeTargets="CoreCompile">` setting `Nullable` to `disable`', stopped: false, kind: 'olympiad', setup: w => { withProgram(w, NULL_PROGRAM); writeFileSync(path.join(w, 'Directory.Build.targets'), RELAX_TARGET); } },
  { doc: '`App.csproj.user` with the same target', stopped: false, kind: 'olympiad', setup: w => { withProgram(w, NULL_PROGRAM); writeFileSync(path.join(w, 'App.csproj.user'), RELAX_TARGET); } },
  { doc: '`.editorconfig` with `[*.cs] generated_code = true`', stopped: false, kind: 'olympiad', setup: w => { withProgram(w, NULL_PROGRAM); writeFileSync(path.join(w, '.editorconfig'), 'root = true\n[*.cs]\ngenerated_code = true\n'); } },
  { doc: 'a file named `Logic.g.cs` holding `string s = null;` in a method', stopped: false, kind: 'olympiad', setup: w => writeFileSync(path.join(w, 'Logic.g.cs'), LOGIC_CLASS) },
  { doc: 'a `// <auto-generated/>` header at the top of `Logic.cs`, which holds `string s = null;` in a method', stopped: false, kind: 'olympiad', setup: w => writeFileSync(path.join(w, 'Logic.cs'), `// <auto-generated/>\n${LOGIC_CLASS}`) },
]);

test('The_command_line_policy_stops_all_six_project_file_overrides_with_the_nullable_error', TIMEOUT, async t => {
  if (!needs(t, snapshotsMissing())) return;
  const w = helloWorkspace(t, { stack: 'csharp', kind: 'olympiad' });
  withProgram(w, NULL_PROGRAM);
  const project = path.join(w, 'App.csproj');
  const original = read(project);
  const table = [];
  for (const [name, xml] of OVERRIDES) {
    writeFileSync(project, original.replace('<AssemblyName>', `${xml}<AssemblyName>`));
    const ran = await inBox(w, 'csharp', ['bash', 'build.sh']);
    table.push(`${name}: exit ${ran.code}`);
    assert.equal(ran.code, 1, `${name}: ${outputOf(ran).slice(-800)}`);
    assert.match(outputOf(ran), /CS8600/, `${name}: the nullable error is the reason`);
  }
  t.diagnostic(table.join('; '));
});

/** The rows of the doc's table of overrides, after its header: `[override, exit, stopped]` each. */
function recordedRows() {
  const doc = read(path.join(REPO, 'research', 'module_lang_bench.md'));
  const section = doc.slice(doc.indexOf('| Override in `App.csproj`')).split('\n').slice(2);
  return section.slice(0, section.findIndex(line => !line.startsWith('|'))).map(line => line.split('|').slice(1, -1).map(cell => cell.trim()));
}

test('The_recorded_table_says_the_command_line_stops_all_six_overrides', () => {
  const rows = recordedRows();
  for (const [i, [name, xml]] of OVERRIDES.entries()) {
    assert.equal(rows[i][0], `\`${xml}\``, `${name}: row ${i} names the override`);
    assert.equal(rows[i][1], '1 (CS8600)', `${name}: the recorded exit is 1 with CS8600`);
    assert.equal(rows[i][2], 'yes', `${name}: the recorded table says the command line stops it`);
  }
});

test('The_further_overrides_are_pinned_as_measured_and_recorded_in_the_table', TIMEOUT, async top => {
  if (!needs(top, snapshotsMissing())) return;
  const rows = recordedRows();
  assert.equal(rows.length, OVERRIDES.length + FURTHER.length, 'every row of the doc table is pinned here');
  for (const [i, row] of FURTHER.entries()) {
    await top.test(row.doc, async t => {
      const w = helloWorkspace(t, { stack: 'csharp', kind: row.kind });
      row.setup(w);
      const ran = await inBox(w, 'csharp', ['bash', 'build.sh']);
      assert.equal(ran.code, row.stopped ? 1 : 0, outputOf(ran).slice(-800));
      if (row.stopped) assert.match(outputOf(ran), /CS8600/);
      const recorded = rows[OVERRIDES.length + i];
      assert.deepEqual(recorded, [row.doc, row.stopped ? '1 (CS8600)' : '0', row.stopped ? 'yes' : 'no'], 'the doc row says what was measured');
    });
  }
});

test('A_use_of_BinaryFormatter_fails_the_csharp_build_with_SYSLIB0011', TIMEOUT, async t => {
  if (!needs(t, snapshotsMissing())) return;
  const w = helloWorkspace(t, { stack: 'csharp', kind: 'olympiad' });
  withProgram(w, 'using System.Runtime.Serialization.Formatters.Binary;\nvar formatter = new BinaryFormatter();\nConsole.Out.Write(formatter);\n');
  const ran = await inBox(w, 'csharp', ['bash', 'build.sh']);
  assert.notEqual(ran.code, 0, 'SYSLIB0011 is in the forced error list');
  assert.match(outputOf(ran), /SYSLIB0011/);
});

test('A_tests_folder_is_outside_the_csharp_build_and_dotnet_test_runs_it_offline', TIMEOUT, async t => {
  if (!needs(t, snapshotsMissing())) return;
  const w = helloWorkspace(t, { stack: 'csharp', kind: 'olympiad' });
  mkdirSync(path.join(w, 'tests'));
  const xunit = snapshotRecord().csharp.resolved.XUNIT;
  writeFileSync(path.join(w, 'tests', 'Tests.csproj'), `<Project Sdk="Microsoft.NET.Sdk">\n  <PropertyGroup><OutputType>Exe</OutputType></PropertyGroup>\n  <ItemGroup><PackageReference Include="xunit.v3" Version="${xunit}" /></ItemGroup>\n</Project>\n`);
  writeFileSync(path.join(w, 'tests', 'UnitTest1.cs'), 'using Xunit;\npublic class UnitTest1 { [Fact] public void Passes() => Assert.True(true); }\n');
  const built = await inBox(w, 'csharp', ['bash', 'build.sh']);
  assert.equal(built.code, 0, `tests/ is not part of the program: ${outputOf(built).slice(-1500)}`);
  const tested = await inBox(w, 'csharp', ['dotnet', 'test', '--project', 'tests/Tests.csproj']);
  assert.equal(tested.code, 0, outputOf(tested).slice(-2000));
  const rebuilt = await inBox(w, 'csharp', ['bash', 'build.sh']);
  assert.equal(rebuilt.code, 0, 'and the program still builds after the tests project has its own obj and bin');
});

// ---- the type check and the lint ---------------------------------------------------------------------------------------------------------------------------

test('The_type_check_and_the_lint_work_in_the_workspace', TIMEOUT, async t => {
  if (!needs(t, snapshotsMissing())) return;
  const { w } = await buildHello(t, { stack: 'node', kind: 'olympiad' });
  const main = path.join(w, 'src', 'main.ts');
  const original = read(main);
  const clean = await inBox(w, 'node', ['npx', '--offline', 'eslint', 'src']);
  assert.equal(clean.code, 0, outputOf(clean).slice(-2000));
  writeFileSync(main, `${original}\nPromise.resolve(1);\n`);
  const floating = await inBox(w, 'node', ['npx', '--offline', 'eslint', 'src']);
  assert.equal(floating.code, 1, outputOf(floating).slice(-2000));
  assert.match(outputOf(floating), /@typescript-eslint\/no-floating-promises/);
  writeFileSync(main, `${original}\nconst wrong: number = 'text';\nconsole.log(wrong);\n`);
  const typed = await inBox(w, 'node', ['bash', 'build.sh']);
  assert.notEqual(typed.code, 0);
  assert.match(outputOf(typed), /TS2322/);
});

// ---- T-L31: the program is what run.sh starts --------------------------------------------------------------------------------------------------------------

/** Builds the stop stub of `stack` into the template of `kind`, starts `bash run.sh` in the layer's chain and waits for `ready` on its standard output. -> `{ c, child, w, out() }`. */
async function startStub(t, { stack, kind, runSh }) {
  const w = helloWorkspace(t, { stack, kind, source: stopStubSource(stack) });
  if (runSh !== undefined) writeFileSync(path.join(w, 'run.sh'), runSh(read(path.join(w, 'run.sh'))));
  const built = await inBox(w, stack, ['bash', 'build.sh']);
  assert.equal(built.code, 0, outputOf(built).slice(-3000));
  const env = boxEnv({ dirs: toolchains, stack, memoryMaxBytes: 512 * MIB });
  const c = await chain(t, { view: ownedFolder(t, 'view'), caseId: 'k1', memoryMax: 512 * MIB, holdSeconds: 120, extraBox: { tree: w, env } });
  const child = c.start(['bash', 'run.sh'], { stdio: ['ignore', 'pipe', 'ignore'] });
  let out = '';
  child.stdout.on('data', chunk => { out += chunk; });
  assert.ok(await waitFor(() => out.includes('ready\n') || null, 30_000), 'the stub said ready');
  return { c, child, w, out: () => out };
}

test('The_process_run_sh_starts_is_the_program_with_no_bash_parent_and_SIGTERM_reaches_it', TIMEOUT, async top => {
  if (!needs(top, snapshotsMissing())) return;
  for (const stack of STACKS) {
    for (const kind of ['olympiad', 'daemon', 'web']) {
      await top.test(`${stack} ${kind}`, async t => {
        const { c, child, out } = await startStub(t, { stack, kind });
        const pid = programPidIn(c.prog);
        assert.ok(pid, `a dotnet or node process is in prog/: ${(pidsIn(c.prog) ?? []).map(p => commOf(p))}`);
        assert.match(exeBase(pid), /^(dotnet|node)$/, 'its executable is the pinned toolchain\'s');
        assert.deepEqual((pidsIn(c.prog) ?? []).filter(p => commOf(p) === 'bash'), [], 'no bash is left in prog/: run.sh became the program');
        assert.equal(commOf(statOf(pid).ppid), 'bwrap', 'the program\'s parent is the box\'s bwrap');
        process.kill(pid, 'SIGTERM');
        assert.ok(await waitFor(() => out().includes('TERM\n') || null, 5000), 'the program logged TERM');
        const ended = await Promise.race([child.exited, new Promise(resolve => setTimeout(() => resolve('not ended within 10 s'), 10_000))]);
        assert.deepEqual(ended, { code: 0, signal: null });
      });
    }
  }
});

test('A_run_sh_without_exec_leaves_a_bash_parent_that_eats_SIGTERM_and_that_is_the_point', TIMEOUT, async top => {
  if (!needs(top, snapshotsMissing())) return;
  for (const stack of STACKS) {
    await top.test(stack, async t => {
      const { c, w, out } = await startStub(t, { stack, kind: 'daemon', runSh: text => text.replace('exec ', '') });
      assert.match(runShProblem(read(path.join(w, 'run.sh'))), /exec/, 'the check names this run.sh');
      const pid = programPidIn(c.prog);
      const bash = (pidsIn(c.prog) ?? []).find(p => commOf(p) === 'bash');
      assert.ok(pid && bash, 'prog/ holds a bash and the program');
      assert.equal(statOf(pid).ppid, bash, 'the program\'s parent is that bash');
      process.kill(bash, 'SIGTERM');
      await new Promise(resolve => setTimeout(resolve, 2000));
      assert.equal(out().includes('TERM'), false, 'the program never saw the signal');
      assert.ok(statOf(pid) !== null && (pidsIn(c.prog) ?? []).includes(pid), 'and it is still running in prog/ (the chain\'s hook ends it)');
    });
  }
});

// ---- the same templates in the agent box -------------------------------------------------------------------------------------------------------------------

test('The_same_templates_build_in_the_agent_box', TIMEOUT, async top => {
  if (!needs(top, snapshotsMissing())) return;
  for (const stack of STACKS) {
    await top.test(stack, async t => {
      const w = helloWorkspace(t, { stack, kind: 'olympiad' });
      const record = snapshotRecord();
      const snap = snapshotDirOf({ stack, record, caches: defaultCaches() }).value;
      const opened = await openBox({ stack, snapshot: { dir: snap, parts: record[stack].parts }, root: ownedFolder(t, 'box', ensureTestRoot()), rid: 'a1', dirs: toolchains });
      assert.equal(opened.ok, true, opened.reason);
      const box = opened.value;
      try {
        cpSync(w, box.layout.work, { recursive: true });
        const ran = await box.run(['bash', 'build.sh'], { env: {} });
        assert.equal(ran.ok && ran.value.code, 0, ran.ok ? ran.value.output.slice(-3000) : ran.reason);
        assert.deepEqual(box.egress().allowed, {}, 'no registry was reached');
        assert.deepEqual(box.egress().refused, {}, 'and none was asked: the template files alone, with no extra environment, make no registry request');
      } finally {
        await box.close();
      }
    });
  }
});

// ---- the lock generation is stable ---------------------------------------------------------------------------------------------------------------------------

test('The_lock_generation_is_stable_and_leaves_no_folder_behind', TIMEOUT, async t => {
  if (!needs(t, snapshotsMissing())) return;
  const work = path.join(os.homedir(), '.cache', 'lang-v2', 'w');
  const listing = () => (existsSync(work) ? readdirSync(work) : []).filter(name => name.includes('-lock-')).sort();
  const existing = listing();
  const ran = await runBounded(process.execPath, [path.join(SPIKE, 'build-workspaces.mjs'), '--make-locks'], { timeoutMs: 300_000, env: { PATH: '/usr/sbin:/usr/bin:/bin', HOME: os.homedir() } });
  assert.equal(ran.ok, true, ran.reason);
  assert.equal(ran.value.code, 0, ran.value.stderr);
  assert.deepEqual(ran.value.stdout.trim().split('\n'), ['olympiad: unchanged', 'daemon: unchanged', 'web: unchanged'], 'the committed locks are what a fresh generation gives');
  assert.deepEqual(listing(), existing, 'the owned folders of the generation are removed');
});
