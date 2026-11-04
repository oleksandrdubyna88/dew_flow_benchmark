// E1.S3 phase A, the template files (no box, no toolchain): the file set of each template, the contract lines of its two scripts, the offline NuGet source, what the project files set and what they never set,
// and the exact registered version of every package they name. The builder, the refusals and the agent-visible text are in templates-build.test.mjs. Each check is a function of the template ROOT, so a
// planted copy of the tree can be given to it.
import assert from 'node:assert/strict';
import { readFileSync, statSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { NPMRC_OFFLINE, offlineNugetConfig, workspaceNugetConfig } from '../lib/cache-offline.mjs';
import { EXEC_FILE, KINDS, STACKS, TEMPLATES, TEMPLATE_FILES, filesUnder, readTemplate, snapshotRecord, stacksRecord, templateDir, trackedMode } from './support/workspace-fixtures.mjs';

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
  csharp: ['set -euo pipefail', 'cd "$(dirname "$0")"', 'dotnet build App.csproj -c Release --nologo -m:4'],
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
    if (!props.includes('<TreatWarningsAsErrors>true</TreatWarningsAsErrors>')) problems.push(`csharp/${kind}: TreatWarningsAsErrors is not true`);
    if (!props.includes('<NuGetAudit>false</NuGetAudit>')) problems.push(`csharp/${kind}: NuGetAudit is not false`);
    const global = JSON.parse(readTemplate('csharp', kind, 'global.json', root));
    if (global.sdk?.version !== sdk || global.sdk?.rollForward !== 'disable') problems.push(`csharp/${kind}: global.json is ${JSON.stringify(global)}, expected SDK ${sdk} with rollForward disable`);
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
    if (readTemplate('node', kind, '.npmrc', root) !== NPMRC_OFFLINE) problems.push(`node/${kind}: .npmrc is not the offline file`);
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
