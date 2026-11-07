// F1.S3 (a), the offered set: the package matrix (`inputs/package-matrix.json`, written by `package-matrix.mjs`) is the one source of what each kind offers. An unsupported row takes its package out of that kind in the recipe,
// in `PACKAGES.md` and (the box cases) for real: adding it to the template and building offline fails with the recorded code. The pure cases read the real inputs; the box cases need the pinned toolchains and the registered
// snapshots and skip with their reason without them (under BENCH_REQUIRE_SANDBOX=1 a missing tool is a failure).
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { ownedFor } from '../../../.agents/conventions/tools/lib/owned.mjs';
import { KINDS } from '../lib/cache-recipe-lang.mjs';
import { probeConfirmed } from '../lib/matrix-probe.mjs';
import { isOffered, kindsOf, matrixProblems, packagesMdProblems } from '../lib/package-matrix.mjs';
import { recipeVersions, renderPackagesMd } from '../lib/packages-md.mjs';
import { templatesIdentity } from '../lib/templates-identity.mjs';
import { defaultCaches } from '../lib/workspace-box.mjs';
import { needs, toolchains } from './support/exec-layer-host.mjs';
import { helloWorkspace, inBox, outputOf, readJson, snapshotRecord, snapshotsMissing, STACKS } from './support/workspace-fixtures.mjs';
import { ensureTestRoot } from './support/cache-box.mjs';

const recipe = () => readJson('inputs/cache-recipe-lang.json');
const matrix = () => readJson('inputs/package-matrix.json');
const inputs = () => ({ matrix: matrix(), recipe: recipe(), record: snapshotRecord(), templatesIdentity: templatesIdentity() });
const render = ({ stack, kind, matrix: m = matrix() }) => { const r = renderPackagesMd({ stack, kind, record: snapshotRecord(), recipe: recipe(), matrix: m }); assert.equal(r.ok, true, r.reason); return r.value; };
const unsupportedRows = () => STACKS.flatMap(stack => Object.entries(matrix().rows[stack]).flatMap(([name, byKind]) => KINDS.filter(kind => byKind[kind].state === 'unsupported').map(kind => ({ stack, name, kind, code: byKind[kind].code }))));
const copyOf = x => structuredClone(x);

/** What an unsupported row requires of the recipe and of the kind's `PACKAGES.md` text: neither offers the package. */
function unsupportedProblems({ matrix: m, recipe: r, texts }) {
  return STACKS.flatMap(stack => Object.entries(m.rows[stack]).flatMap(([name, byKind]) => KINDS.filter(kind => byKind[kind].state === 'unsupported').flatMap(kind => {
    const entry = r.stacks[stack].packages.find(p => p.name === name);
    return [
      ...(entry.kinds.includes(kind) ? [`the recipe still offers ${stack}/${name} in ${kind}`] : []),
      ...(packagesMdProblems({ text: texts[`${stack}/${kind}`], matrix: m, stack, kind }).filter(p => p.startsWith(name.toLowerCase()) || p.startsWith(name))),
    ];
  })));
}

const allTexts = m => Object.fromEntries(STACKS.flatMap(stack => KINDS.map(kind => [`${stack}/${kind}`, render({ stack, kind, matrix: m })])));

// ---- the rows -------------------------------------------------------------------------------------------------------------------------

test('Every_offered_package_in_each_applicable_template_has_a_row_of_the_matrix', () => {
  assert.deepEqual(matrixProblems(inputs()), [], 'the matrix is complete, equals the recipe\'s kinds and was measured against the registered snapshots');
  for (const stack of STACKS) {
    for (const p of recipe().stacks[stack].packages) {
      for (const kind of KINDS) assert.match(matrix().rows[stack][p.name][kind].state, /^(works|unsupported)$/, `${stack}/${p.name}/${kind} has a row`);
    }
  }
  const missing = copyOf(inputs());
  delete missing.matrix.rows.csharp['Microsoft.AspNetCore.Mvc.Testing'].web;
  assert.match(matrixProblems(missing).join(';'), /Mvc\.Testing\.web has no row/, 'the check can fail: a missing row');
  const moved = copyOf(inputs());
  moved.templatesIdentity = 'f'.repeat(64);
  assert.match(matrixProblems(moved).join(';'), /measured against templates .* the templates now are/, 'the check can fail: the templates moved since the rows were measured');
  const unsaid = copyOf(inputs());
  delete unsaid.matrix.measuredAgainst.templatesIdentity;
  assert.match(matrixProblems(unsaid).join(';'), /names no templates identity/, 'a matrix that does not say which templates it was measured on is not a pass');
  const typo = copyOf(inputs());
  typo.matrix.rows.node.fastify.web = { state: 'unsupported' };
  assert.match(matrixProblems(typo).join(';'), /unsupported and must carry the code/, 'an unsupported row without its code');
});

test('JwtBearer_in_the_olympiad_kind_has_its_row_with_its_code', () => {
  const row = matrix().rows.csharp['Microsoft.AspNetCore.Authentication.JwtBearer'].olympiad;
  assert.deepEqual(row, { state: 'unsupported', code: 'NU1603' }, 'measured: Logging.Abstractions 8.0.0 wants Dependency Injection Abstractions 8.0.0, which the cache holds only at 10.0.14');
  const entry = recipe().stacks.csharp.packages.find(p => p.name === 'Microsoft.AspNetCore.Authentication.JwtBearer');
  assert.deepEqual(entry.kinds, ['daemon', 'web'], 'the recipe does not offer it in the olympiad kind');
  assert.deepEqual(kindsOf(matrix(), 'csharp', entry.name), ['daemon', 'web']);
  assert.equal(render({ stack: 'csharp', kind: 'olympiad' }).toLowerCase().includes('jwtbearer'), false, 'and the olympiad file does not list it');
  assert.equal(render({ stack: 'csharp', kind: 'web' }).includes('`Microsoft.AspNetCore.Authentication.JwtBearer`'), true, 'the web file does');
});

// ---- an unsupported row excludes the package --------------------------------------------------------------------------------------------

test('An_unsupported_row_takes_the_package_out_of_that_kinds_offered_set_in_the_recipe_PACKAGES_md_and_the_box', async t => {
  const rows = unsupportedRows();
  assert.ok(rows.length >= 2, 'the matrix holds unsupported rows: the cases below have something to read');
  assert.deepEqual(unsupportedProblems({ matrix: matrix(), recipe: recipe(), texts: allTexts(matrix()) }), [], 'recipe and PACKAGES.md leave every unsupported package out of its kind');
  // the test reads the matrix and not a copy of it: a works row flipped to unsupported fails the recipe assertion until the recipe follows, and a PACKAGES.md made before the flip fails until it is made again
  const flipped = copyOf(matrix());
  flipped.rows.node.pino.daemon = { state: 'unsupported', code: 'ENOTCACHED' };
  const stale = unsupportedProblems({ matrix: flipped, recipe: recipe(), texts: allTexts(matrix()) });
  assert.ok(stale.some(p => p.includes('recipe still offers node/pino in daemon')), `the recipe has not followed: ${stale}`);
  assert.ok(stale.some(p => p.startsWith('pino') && p.includes('node/daemon')), `the old PACKAGES.md has not followed: ${stale}`);
  const followed = copyOf(recipe());
  followed.stacks.node.packages.find(p => p.name === 'pino').kinds = ['olympiad', 'web'];
  assert.deepEqual(unsupportedProblems({ matrix: flipped, recipe: followed, texts: allTexts(flipped) }), [], 'once the recipe and the file follow, the flipped fixture is clean');
  if (!needs(t, snapshotsMissing())) return;
  for (const row of rows) {
    const probe = await probeOnceFor(t, row);
    assert.deepEqual(probe, { state: 'unsupported', code: row.code }, `${row.stack}/${row.kind}/${row.name}: adding it in that template and building offline fails with the recorded code`);
  }
}, { timeout: 15 * 60_000 });

/** One confirmed probe of a row in the registered snapshots. */
async function probeOnceFor(t, { stack, kind, name }) {
  const version = recipeVersions({ stack, record: snapshotRecord(), recipe: recipe() }).value.find(p => p.name === name).version;
  const work = path.join(ensureTestRoot(), 'mtx');
  mkdirSync(work, { recursive: true, mode: 0o700 });
  const result = await probeConfirmed({ owned: ownedFor({ token: 'bench' }), work, dirs: toolchains, stack, kind, name, version, record: snapshotRecord(), caches: defaultCaches() });
  assert.equal(result.ok, true, result.reason);
  return result.value;
}

// ---- PACKAGES.md of a kind ---------------------------------------------------------------------------------------------------------

test('PACKAGES_md_of_a_kind_lists_exactly_the_packages_whose_row_for_that_kind_is_works', () => {
  for (const stack of STACKS) {
    for (const kind of KINDS) {
      const text = render({ stack, kind });
      assert.deepEqual(packagesMdProblems({ text, matrix: matrix(), stack, kind }), [], `${stack}/${kind}`);
      for (const p of recipe().stacks[stack].packages) assert.equal(text.includes(`| \`${p.name}\` |`), isOffered(matrix(), stack, p.name, kind), `${stack}/${kind}/${p.name}`);
    }
  }
  const kindsDiffer = STACKS.some(stack => new Set(KINDS.map(kind => render({ stack, kind }))).size > 1);
  assert.equal(kindsDiffer, true, 'the files differ between the kinds of a stack, exactly where the matrix differs (it replaces the byte-equal rule)');
});

test('The_files_of_the_kinds_of_a_stack_are_equal_where_the_matrix_rows_are_equal', () => {
  for (const stack of STACKS) {
    for (const [a, b] of [['olympiad', 'daemon'], ['daemon', 'web'], ['olympiad', 'web']]) {
      const sameRows = recipe().stacks[stack].packages.every(p => isOffered(matrix(), stack, p.name, a) === isOffered(matrix(), stack, p.name, b));
      assert.equal(render({ stack, kind: a }) === render({ stack, kind: b }), sameRows, `${stack}: ${a} and ${b}`);
    }
  }
});

// ---- the test hosts ----------------------------------------------------------------------------------------------------------------

const TESTS_CSPROJ = (record) => `<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <IsPackable>false</IsPackable>
    <UseMicrosoftTestingPlatformRunner>true</UseMicrosoftTestingPlatformRunner>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="${record.resolved.MVC_TESTING}" />
    <PackageReference Include="xunit.v3" Version="${record.resolved.XUNIT}" />
  </ItemGroup>
  <ItemGroup>
    <Using Include="Xunit" />
    <ProjectReference Include="../App.csproj" />
  </ItemGroup>
</Project>
`;
const WEB_PROGRAM = 'var app = WebApplication.CreateBuilder(args).Build();\napp.MapGet("/", () => "ok");\napp.Run();\n\npublic partial class Program;\n';
const WEB_TEST = 'using Microsoft.AspNetCore.Mvc.Testing;\n\npublic sealed class AppTests\n{\n    [Fact]\n    public async Task The_app_answers_in_memory()\n    {\n        await using var factory = new WebApplicationFactory<Program>();\n        using var client = factory.CreateClient();\n        Assert.Equal("ok", await client.GetStringAsync("/", TestContext.Current.CancellationToken));\n    }\n}\n';

test('A_csharp_web_test_project_with_WebApplicationFactory_passes_offline_and_build_sh_still_ignores_tests', async t => {
  if (!needs(t, snapshotsMissing())) return;
  const w = helloWorkspace(t, { stack: 'csharp', kind: 'web', label: 'wafc' });
  writeFileSync(path.join(w, 'Program.cs'), WEB_PROGRAM);
  mkdirSync(path.join(w, 'tests'), { recursive: true });
  writeFileSync(path.join(w, 'tests', 'Tests.csproj'), TESTS_CSPROJ(snapshotRecord().csharp));
  writeFileSync(path.join(w, 'tests', 'AppTests.cs'), WEB_TEST);
  const tested = await inBox(w, 'csharp', ['dotnet', 'test', '--project', 'tests/Tests.csproj']);
  assert.equal(tested.code, 0, `dotnet test --project tests/Tests.csproj: ${outputOf(tested).slice(-3000)}`);
  assert.match(outputOf(tested), /(?:total|gesamt):\s*1\b/i, 'one test ran (the label is in the SDK\'s language)');
  const built = await inBox(w, 'csharp', ['bash', 'build.sh']);
  assert.equal(built.code, 0, `bash build.sh with tests/ present: ${outputOf(built).slice(-3000)}`);
}, { timeout: 15 * 60_000 });

const INJECT_APP = "import fastify from 'fastify';\n\nexport function build() {\n  const app = fastify();\n  app.get('/hello', () => ({ hello: 'world' }));\n  return app;\n}\n";
const INJECT_TEST = "import assert from 'node:assert/strict';\nimport { test } from 'node:test';\nimport { build } from '../src/app.ts';\n\ntest('the route answers through inject', async () => {\n  const app = build();\n  const res = await app.inject({ method: 'GET', url: '/hello' });\n  assert.equal(res.statusCode, 200);\n  assert.deepEqual(res.json(), { hello: 'world' });\n  await app.close();\n});\n";

test('A_node_test_with_inject_passes_in_the_web_kind_with_no_added_package', async t => {
  if (!needs(t, snapshotsMissing())) return;
  const w = helloWorkspace(t, { stack: 'node', kind: 'web', label: 'inj' });
  const before = readFileSync(path.join(w, 'package.json'), 'utf8');
  writeFileSync(path.join(w, 'src', 'app.ts'), INJECT_APP);
  mkdirSync(path.join(w, 'tests'), { recursive: true });
  writeFileSync(path.join(w, 'tests', 'app.test.ts'), INJECT_TEST);
  const built = await inBox(w, 'node', ['bash', 'build.sh']);
  assert.equal(built.code, 0, outputOf(built).slice(-3000));
  const tested = await inBox(w, 'node', ['node', '--test', 'tests/app.test.ts']);
  assert.equal(tested.code, 0, `node --test: ${outputOf(tested).slice(-3000)}`);
  assert.match(outputOf(tested), /pass 1/, 'the test ran');
  assert.equal(readFileSync(path.join(w, 'package.json'), 'utf8'), before, 'no package was added');
}, { timeout: 15 * 60_000 });

// ---- the registered snapshots ------------------------------------------------------------------------------------------------------------

/** sha256 of a record with its keys sorted at every depth: the whole record, not two of its fields. The two values are of the records of `cache-snapshots-lang.json` at the base commit bf542f1f. */
const sortKeys = x => (Array.isArray(x) ? x.map(sortKeys) : x !== null && typeof x === 'object' ? Object.fromEntries(Object.keys(x).sort().map(k => [k, sortKeys(x[k])])) : x);
const canonicalSha = record => createHash('sha256').update(JSON.stringify(sortKeys(record))).digest('hex');
const NODE_RECORD_AT_BASE = 'a2c104ba1485ec752cd5a0e199e8d5c94ad437d8121a3ba9c5cd23cfaa2efe35';
const CSHARP_RECORD_AT_BASE = 'c57b091d551d82a080f7ef3e16db381919a35f776a162d3c28cacf78c5c82ddd';

test('The_csharp_snapshot_is_registered_again_the_old_record_is_superseded_and_the_node_record_is_unchanged', () => {
  const record = snapshotRecord();
  const old = record.superseded.filter(s => s.stack === 'csharp');
  assert.equal(old.length, 1, 'one superseded C# record');
  assert.equal(old[0].record.treeSha256, '452c7a571ec0d11b2ab9d8fe4b038b9d3076374f5d0503d94a8e596378cf0cee', 'the snapshot registered on 2026-11-04');
  assert.match(old[0].reason, /Mvc\.Testing/);
  assert.notEqual(record.csharp.treeSha256, old[0].record.treeSha256, 'a new snapshot is registered');
  assert.equal(record.csharp.packages.some(p => p.name === 'microsoft.aspnetcore.mvc.testing' && p.version === record.csharp.resolved.MVC_TESTING), true, 'Mvc.Testing is in the manifest at the resolved version');
  assert.equal(record.node.treeSha256, 'bebdc7a20541b446c4efd09099a58354aa877e8137956fa66a67afad969fb2c6', 'the Node record is the one registered on 2026-11-04');
  assert.equal(record.node.dirName, `node-${record.node.treeSha256}`);
  assert.equal(canonicalSha(record.node), NODE_RECORD_AT_BASE, 'the whole Node record, every field and package, is the one at the base commit (bf542f1f)');
  assert.equal(canonicalSha(old[0].record), CSHARP_RECORD_AT_BASE, 'and the superseded C# record is the whole record the base commit registered');
  assert.notEqual(canonicalSha({ ...record.node, sizeBytes: record.node.sizeBytes + 1 }), NODE_RECORD_AT_BASE, 'the comparison can fail: one changed field is seen');
  const nuspec = path.join(defaultCaches(), record.csharp.dirName, 'nuget', 'microsoft.aspnetcore.mvc.testing', record.csharp.resolved.MVC_TESTING, 'microsoft.aspnetcore.mvc.testing.nuspec');
  if (existsSync(path.join(defaultCaches(), record.csharp.dirName))) assert.equal(existsSync(nuspec), true, 'and the snapshot folder on this host holds it');
});
