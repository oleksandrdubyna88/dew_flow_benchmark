// E1.S3 phase A, the template files (no box, no toolchain): the file set of each template, the contract lines of its two scripts, the offline NuGet source, what the project files set and what they never set,
// and the exact registered version of every package they name. The builder, the refusals and the agent-visible text are in templates-build.test.mjs. Each check is a function of the template ROOT, so a
// planted copy of the tree can be given to it.
import assert from 'node:assert/strict';
import { readFileSync, statSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { NPM_OFFLINE_ENV, offlineNugetConfig, workspaceNugetConfig } from '../lib/cache-offline.mjs';
import { caseKeysIn, unlistedKeys } from '../lib/exec-layer/env.mjs';
import { snapshotBoxOf, workspaceBuildEnv } from '../lib/workspace-box.mjs';
import { NPMRC_WORKSPACE, lockProblems, runShProblem } from '../lib/workspace-build.mjs';
import { EXEC_FILE, KINDS, STACKS, TEMPLATES, TEMPLATE_FILES, filesUnder, readJson, readTemplate, snapshotRecord, stacksRecord, templateDir, trackedMode } from './support/workspace-fixtures.mjs';

const everyKind = fn => STACKS.flatMap(stack => KINDS.flatMap(kind => fn(stack, kind)));

// ---- the file set -----------------------------------------------------------------------------------------------------------------

function fileSetProblems(root) {
  return everyKind((stack, kind) => {
    const found = filesUnder(templateDir(stack, kind, root));
    return JSON.stringify(found) === JSON.stringify(TEMPLATE_FILES[stack]) ? [] : [`${stack}/${kind} holds ${found.join(', ')}, expected ${TEMPLATE_FILES[stack].join(', ')}`];
  });
}

test('Each_template_holds_exactly_its_file_set', () => {
  assert.deepEqual(fileSetProblems(TEMPLATES), []);
});

// ---- the two scripts ----------------------------------------------------------------------------------------------------------------

const BUILD_LINES = Object.freeze({
  csharp: ['set -euo pipefail', 'cd "$(dirname "$0")"', 'dotnet build App.csproj -c Release --nologo -m:4 -p:TreatWarningsAsErrors=false "-p:WarningsAsErrors=nullable%3BNU1603%3BNU1605%3BSYSLIB0011" -p:Nullable=enable -p:WarningsNotAsErrors= -p:WarningLevel=10 "-p:NoWarn=1701%3B1702"'],
  node: ['set -euo pipefail', 'cd "$(dirname "$0")"', 'npm ci --offline', 'npx --offline tsc --noEmit -p tsconfig.json'],
});
const VARIABLE_LINE = /^\s*(export\s+)?[A-Za-z_]+=|^\s*env\s/;
const codeLines = text => text.split('\n').filter(line => line.trim() !== '' && !line.startsWith('#'));

function buildProblems(stack, text) {
  const lines = text.split('\n');
  const problems = [];
  if (lines[0] !== '#!/usr/bin/env bash') problems.push('first line is not the bash shebang');
  if (JSON.stringify(codeLines(text)) !== JSON.stringify(BUILD_LINES[stack])) problems.push(`command lines are ${JSON.stringify(codeLines(text))}`);
  if (!lines.some(line => line.startsWith('#') && /sources/.test(line))) problems.push('no comment says where the sources go');
  return problems;
}

function runProblems(stack, text) {
  const code = codeLines(text);
  const problems = [];
  if (text.split('\n')[0] !== '#!/usr/bin/env bash') problems.push('first line is not the bash shebang');
  if (JSON.stringify(code.slice(0, 2)) !== JSON.stringify(['set -euo pipefail', 'cd "$(dirname "$0")"'])) problems.push('does not start with set -euo pipefail and the cd');
  if (code.at(-1) !== EXEC_FILE[stack] || code.length !== 3) problems.push(`last command line is ${JSON.stringify(code.at(-1))}`);
  return problems;
}

async function scriptProblems(root) {
  const problems = [];
  for (const stack of STACKS) {
    for (const kind of KINDS) {
      const at = `${stack}/${kind}`;
      for (const [name, check] of [['build.sh', buildProblems], ['run.sh', runProblems]]) {
        const file = path.join(templateDir(stack, kind, root), name);
        const text = readFileSync(file, 'utf8');
        problems.push(...check(stack, text).map(p => `${at}/${name}: ${p}`));
        problems.push(...text.split('\n').filter(line => VARIABLE_LINE.test(line)).map(line => `${at}/${name}: sets a variable: ${line}`));
        if ((statSync(file).mode & 0o777) !== 0o755) problems.push(`${at}/${name}: mode on disk is not 755`);
        const tracked = await trackedMode(file);
        if (tracked !== null && tracked !== '100755') problems.push(`${at}/${name}: git mode is ${tracked}`);
        if (!text.endsWith('\n') || text.endsWith('\n\n') || text.includes('\r')) problems.push(`${at}/${name}: not LF with one final newline`);
      }
    }
  }
  return problems;
}

test('The_scripts_hold_the_contract', async () => {
  assert.deepEqual(await scriptProblems(TEMPLATES), []);
});

test('A_run_sh_whose_last_line_does_not_exec_fails_the_check', () => {
  const real = everyKind((stack, kind) => readTemplate(stack, kind, 'run.sh'));
  assert.equal(real.length, 6);
  assert.deepEqual(real.map(runShProblem), [null, null, null, null, null, null], 'the six real run.sh files end with exec');
  const without = readTemplate('csharp', 'web', 'run.sh').replace('exec dotnet', 'dotnet');
  assert.match(runShProblem(without), /exec/);
  assert.match(runShProblem('#!/usr/bin/env bash\n\n'), /exec/, 'a script with no command at all');
  assert.equal(runShProblem('#!/usr/bin/env bash\nexec node src/main.ts\n\n'), null, 'trailing empty lines do not count');
});

// ---- the frozen files, named in build.sh ----------------------------------------------------------------------------------------------------------

const AGENT_EDITABLE = Object.freeze({ csharp: ['App.csproj'], node: ['package-lock.json', 'package.json'] });
const DERIVED_OR_SCRIPT = Object.freeze(['build.sh', 'run.sh', 'PACKAGES.md', 'STATEMENT.md']);

/** The names a build.sh lists after "pin files ...:" in its comment line. */
const frozenNamesIn = text => (text.split('\n').find(line => line.startsWith('#') && /pin files/.test(line)) ?? '').replace(/^.*:\s*/, '').replace(/\.$/, '').split(', ').filter(Boolean).sort();

test('The_build_script_names_the_frozen_files_of_its_stack_and_the_list_agrees_with_the_push_checks_list', () => {
  const listed = readJson('templates/stack-files.json');
  for (const stack of STACKS) {
    const names = listed.filter(pattern => pattern.startsWith(`${stack}/<t>/`)).map(pattern => pattern.slice(`${stack}/<t>/`.length)).filter(name => !name.includes('/'));
    const expected = names.filter(name => !DERIVED_OR_SCRIPT.includes(name) && !AGENT_EDITABLE[stack].includes(name)).sort();
    assert.ok(expected.length >= 3, `${stack}: the list names the pin files`);
    for (const kind of KINDS) assert.deepEqual(frozenNamesIn(readTemplate(stack, kind, 'build.sh')), expected, `${stack}/${kind}: the build.sh line is the frozen files of the list`);
  }
});

// ---- the offline NuGet source -----------------------------------------------------------------------------------------------------

test('The_nuget_config_is_the_offline_source_only', () => {
  for (const kind of KINDS) assert.equal(readTemplate('csharp', kind, 'nuget.config'), workspaceNugetConfig(), `csharp/${kind}`);
  assert.match(workspaceNugetConfig(), /<clear \/>[\s\S]*<add key="offline" value="%NUGET_PACKAGES%" \/>[\s\S]*<fallbackPackageFolders>\s*<clear \/>/);
  assert.equal(offlineNugetConfig('%NUGET_PACKAGES%').ok, false, 'the percent form is not a path the checked function takes');
  assert.match(offlineNugetConfig('/w/n').value, /value="\/w\/n"/);
});

// ---- the project files --------------------------------------------------------------------------------------------------------------

const FORBIDDEN = Object.freeze(['IsAotCompatible', 'EnableTrimAnalyzer', 'PublishTrimmed', 'PublishAot', 'InvariantGlobalization', 'RestoreLockedMode', 'RestorePackagesWithLockFile']);

function projectProblems(root) {
  const sdk = stacksRecord().pins.dotnetSdk;
  const problems = [];
  for (const kind of KINDS) {
    const props = readTemplate('csharp', kind, 'Directory.Build.props', root);
    if (props.includes('TreatWarningsAsErrors') || props.includes('WarningsAsErrors')) problems.push(`csharp/${kind}: the props file sets a warnings-as-errors policy: build.sh forces it on the command line`);
    if (!props.includes('<DefaultItemExcludes>$(DefaultItemExcludes);tests/**</DefaultItemExcludes>')) problems.push(`csharp/${kind}: tests/ is not excluded from the default items`);
    if (!props.includes('<Nullable>enable</Nullable>')) problems.push(`csharp/${kind}: Nullable is not enabled in the props file`);
    if (!props.includes('<NuGetAudit>false</NuGetAudit>')) problems.push(`csharp/${kind}: NuGetAudit is not false`);
    const global = JSON.parse(readTemplate('csharp', kind, 'global.json', root));
    if (global.sdk?.version !== sdk || global.sdk?.rollForward !== 'disable') problems.push(`csharp/${kind}: global.json is ${JSON.stringify(global)}, expected SDK ${sdk} with rollForward disable`);
    if (global.test?.runner !== 'Microsoft.Testing.Platform') problems.push(`csharp/${kind}: global.json does not name the Microsoft.Testing.Platform test runner`);
  }
  for (const [stack, kind] of everyKind((s, k) => [[s, k]])) {
    for (const name of TEMPLATE_FILES[stack]) {
      const text = readTemplate(stack, kind, name, root);
      problems.push(...FORBIDDEN.filter(word => text.includes(word)).map(word => `${stack}/${kind}/${name} names ${word}`));
    }
  }
  return problems;
}

test('The_project_files_set_what_the_contract_sets_and_none_of_what_it_forbids', () => {
  assert.deepEqual(projectProblems(TEMPLATES), []);
});

// ---- the Node pins -------------------------------------------------------------------------------------------------------------------

function nodePinProblems(root) {
  const problems = [];
  for (const kind of KINDS) {
    const ts = JSON.parse(readTemplate('node', kind, 'tsconfig.json', root)).compilerOptions ?? {};
    for (const flag of ['strict', 'noEmit', 'erasableSyntaxOnly', 'verbatimModuleSyntax']) if (ts[flag] !== true) problems.push(`node/${kind}: tsconfig ${flag} is not true`);
    if (ts.module !== 'nodenext') problems.push(`node/${kind}: tsconfig module is ${ts.module}`);
    if (readTemplate('node', kind, '.npmrc', root) !== NPMRC_WORKSPACE) problems.push(`node/${kind}: .npmrc is not the offline file without the update check`);
    if (JSON.parse(readTemplate('node', kind, 'package.json', root)).engines?.node !== stacksRecord().pins.node) problems.push(`node/${kind}: engines.node is not the pinned Node`);
    const lint = readTemplate('node', kind, 'eslint.config.mjs', root);
    for (const word of ['@eslint/js', 'eslint/config', 'typescript-eslint', 'projectService']) if (!lint.includes(word)) problems.push(`node/${kind}: eslint config does not name ${word}`);
  }
  return problems;
}

test('The_node_pins_set_what_the_contract_sets', () => {
  assert.deepEqual(nodePinProblems(TEMPLATES), []);
});

// ---- the packages --------------------------------------------------------------------------------------------------------------------

const EXACT = /^\d+\.\d+\.\d+$/;

/** `{ ecosystem, name, version }` of every package a template names: each `PackageReference` of an `App.csproj`, each entry of a `package.json`. */
function referencesOf(root) {
  return everyKind((stack, kind) => {
    if (stack === 'csharp') {
      const text = readTemplate(stack, kind, 'App.csproj', root);
      return [...text.matchAll(/<PackageReference Include="([^"]+)" Version="([^"]+)" \/>/g)].map(m => ({ at: `csharp/${kind}`, ecosystem: 'nuget', name: m[1], version: m[2] }));
    }
    const json = JSON.parse(readTemplate(stack, kind, 'package.json', root));
    return Object.entries({ ...json.dependencies, ...json.devDependencies }).map(([name, version]) => ({ at: `node/${kind}`, ecosystem: 'npm', name, version }));
  });
}

function packageProblems(root) {
  const record = snapshotRecord();
  const rows = [...record.csharp.packages, ...record.node.packages];
  return referencesOf(root).flatMap(ref => {
    if (!EXACT.test(ref.version)) return [`${ref.at}: ${ref.name} ${ref.version} is not an exact version`];
    const same = rows.filter(row => row.ecosystem === ref.ecosystem && row.name.toLowerCase() === ref.name.toLowerCase() && row.version === ref.version);
    return same.length === 1 ? [] : [`${ref.at}: ${ref.name}@${ref.version} matches ${same.length} manifest rows, expected 1`];
  });
}

test('Every_referenced_package_is_in_the_registered_manifest_at_its_exact_version', () => {
  const refs = referencesOf(TEMPLATES);
  for (const kind of ['daemon', 'web']) assert.ok(refs.some(r => r.at === `csharp/${kind}`), `csharp/${kind} names a package: the scan found it`);
  for (const kind of KINDS) assert.ok(refs.filter(r => r.at === `node/${kind}`).length >= 5, `node/${kind} names its packages: the scan found them`);
  assert.deepEqual(packageProblems(TEMPLATES), []);
});

// ---- the locks -------------------------------------------------------------------------------------------------------------------------

const lockOf = kind => JSON.parse(readTemplate('node', kind, 'package-lock.json'));
const packageOf = kind => JSON.parse(readTemplate('node', kind, 'package.json'));
const packagesOf = lock => Object.entries(lock.packages).filter(([key]) => key.includes('node_modules/'));

test('The_lock_pins_the_exact_versions_and_integrities_of_the_manifest', () => {
  const manifest = snapshotRecord().node.packages;
  const sizes = [];
  for (const kind of KINDS) {
    const lock = lockOf(kind);
    assert.deepEqual(lockProblems(lock, manifest, packageOf(kind)), [], `node/${kind}`);
    assert.equal(lock.lockfileVersion, 3);
    assert.equal(packagesOf(lock).some(([, entry]) => entry.hasInstallScript === true), false, `node/${kind}: the lock records no install script`);
    for (const [key, entry] of packagesOf(lock)) {
      const name = key.slice(key.lastIndexOf('node_modules/') + 'node_modules/'.length);
      const rows = manifest.filter(row => row.name === name && row.version === entry.version);
      assert.equal(rows.length, 1, `${key} is one manifest row`);
      assert.equal(entry.integrity, rows[0].integrity, `${key}: the integrity is the manifest's`);
    }
    sizes.push(packagesOf(lock).length);
  }
  assert.ok(sizes.every(n => n > 0), `every lock lists packages: ${sizes}`);
});

test('A_lock_that_is_not_the_manifests_is_reported_by_the_check', () => {
  const manifest = snapshotRecord().node.packages;
  const lock = lockOf('olympiad');
  const changed = edit => { const copy = structuredClone(lock); edit(copy); return lockProblems(copy, manifest, packageOf('olympiad')); };
  const [key, entry] = packagesOf(lock)[0];
  assert.match(changed(copy => { copy.packages[key].integrity = 'sha512-AAAA'; })[0], /integrity/);
  assert.match(changed(copy => { copy.packages[key].hasInstallScript = true; })[0], /install script/);
  assert.match(changed(copy => { copy.packages['node_modules/left-pad'] = { version: '1.3.0', integrity: entry.integrity }; })[0], /left-pad@1\.3\.0 is not in the registered manifest/);
  assert.match(changed(copy => { delete copy.packages[''].devDependencies.eslint; })[0], /root devDependencies differ/);
});

// ---- the environment of a build box --------------------------------------------------------------------------------------------------

const FAKE_DIRS = Object.freeze({ dotnetSdk: '/t/sdk', node: '/t/node', typescript: '/t/ts' });
const EXTRAS = Object.freeze({
  csharp: ['DOTNET_CLI_USE_MSBUILD_SERVER', 'DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE', 'DOTNET_GENERATE_ASPNET_CERTIFICATE', 'MSBUILDDISABLENODEREUSE', 'NUGET_CERT_REVOCATION_MODE', 'NUGET_PACKAGES', 'TESTINGPLATFORM_TELEMETRY_OPTOUT', 'UseSharedCompilation'],
  node: ['npm_config_cache', ...Object.keys(NPM_OFFLINE_ENV)],
});

test('The_build_box_environment_holds_no_case_key', () => {
  for (const stack of STACKS) {
    const env = workspaceBuildEnv({ dirs: FAKE_DIRS, stack, snap: '/s/snap' });
    assert.deepEqual(caseKeysIn(env), [], `${stack}: no per-case heap key`);
    assert.equal('DOTNET_GCHeapHardLimit' in env, false);
    assert.doesNotMatch(env.NODE_OPTIONS ?? '', /old[-_]space/);
    assert.deepEqual(unlistedKeys(env).sort(), [...EXTRAS[stack]].sort(), `${stack}: the keys off the layer's list are exactly the snapshot box's extras`);
  }
  assert.equal(snapshotBoxOf({ stack: 'node', snap: '/s/snap' }).extra.npm_config_cache, '/tmp/npm', 'the writable copy is the cache of every Node command');
  assert.equal(snapshotBoxOf({ stack: 'csharp', snap: '/s/snap' }).extra.NUGET_PACKAGES, '/s/snap/nuget', 'the read-only snapshot is the packages folder');
});
