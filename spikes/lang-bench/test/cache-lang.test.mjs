// E1.S4, the fast cases (no network, no box): the recipe's checks, the manifest, the used-versus-manifest check, the offline configuration texts, the store (staging sweep, supersede, prune) and the command's refusals.
// The real build and every box case are in cache-lang-offline.test.mjs (BENCH_REQUIRE_SANDBOX=1 only).
import assert from 'node:assert/strict';
import { chmodSync, existsSync, mkdirSync, readFileSync, readdirSync, renameSync, symlinkSync, writeFileSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { loadRecipe, makeReadOnly, makeSnapshot, makeWritable, pickHighest, registerSnapshot, treeHash } from '../../minigit-bench/lib/cache-snapshot.mjs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { runBounded } from '../../minigit-bench/lib/run.mjs';
import { takeGateLock } from '../lib/gate-lock.mjs';
import { renderPackagesMd } from '../lib/packages-md.mjs';
import { START_TOLERANCE_S, parseOwner, thisOwner } from '../../../.agents/conventions/tools/lib/owned.mjs';
import { sandboxGate } from '../../minigit-bench/test/helpers.mjs';
import { loadRecipeLang } from '../lib/cache-recipe-lang.mjs';
import { NPM_OFFLINE_ENV, NPMRC_OFFLINE, npmOfflineArgv, offlineNugetConfig, outsideManifest, usedNpm, usedNuget } from '../lib/cache-offline.mjs';
import { checkManifestMatches, manifestOf, pruneSuperseded, stagingVerdict, supersede, sweepStaging } from '../lib/cache-store.mjs';
import { loadStacksLang } from '../lib/toolchain-pins.mjs';
import { exitedPid, recordOf, writeClaim } from './support/owners.mjs';
import { ownedFolder } from './toolchain-fixtures.mjs';

const SPIKE = fileURLToPath(new URL('..', import.meta.url));
const readJson = rel => JSON.parse(readFileSync(path.join(SPIKE, rel), 'utf8'));
const stacks = loadStacksLang(readJson('inputs/stacks-lang.json')).value;
const realRecipe = () => readJson('inputs/cache-recipe-lang.json');
/** A deep copy of the real recipe, changed by `edit`. */
const changed = edit => { const copy = structuredClone(realRecipe()); edit(copy); return copy; };
const refusal = recipe => { const r = loadRecipeLang(recipe, stacks); assert.equal(r.ok, false, 'the recipe is refused'); return r.reason; };

test('The_recipe_names_every_offered_package_with_a_purpose_and_pins_the_toolchains_of_stacks_lang', () => {
  const real = loadRecipeLang(realRecipe(), stacks);
  assert.equal(real.ok, true, real.reason);
  assert.deepEqual(Object.keys(real.value.stacks), ['csharp', 'node']);
  for (const [stack, spec] of Object.entries(real.value.stacks)) for (const p of spec.packages) assert.ok(p.purpose.length > 0, `${stack}: ${p.name} has a purpose`);
  const typebox = real.value.stacks.node.packages.find(p => p.name === 'typebox');
  assert.match(typebox.purpose, /imported as `typebox`/, 'the owner\'s decision: the import name is typebox');
  assert.equal(real.value.stacks.node.packages.some(p => p.name === '@sinclair/typebox'), false, 'and @sinclair/typebox is not in the set');
  assert.match(refusal(changed(r => { delete r.stacks.node.packages[0].purpose; })), /stacks\.node\.packages\[0\]\.purpose/);
  assert.match(refusal(changed(r => { r.stacks.node.packages[1].purpose = 'a | b'; })), /stacks\.node\.packages\[1\]\.purpose.*\|/);
  assert.match(refusal(changed(r => { r.stacks.node.packages[1].purpose = 'two\nlines'; })), /stacks\.node\.packages\[1\]\.purpose/);
  assert.match(refusal(changed(r => { r.stacks.node.packages = []; })), /stacks\.node\.packages/);
  assert.match(refusal(changed(r => { r.stacks.csharp.packages[0].spec = '{NO_SUCH}'; })), /stacks\.csharp\.packages\[0\]\.spec.*NO_SUCH/);
  assert.match(refusal(changed(r => { r.stacks.node.steps[2].argv.push('x@{GHOST}'); })), /stacks\.node\.steps\[2\]\.argv.*GHOST/);
  assert.match(refusal(changed(r => { r.stacks.node.resolve.PINO.pick = 'lowest'; })), /stacks\.node\.resolve\.PINO\.pick/);
  assert.match(refusal(changed(r => { r.stacks.csharp.project = '../../elsewhere'; })), /stacks\.csharp\.project/);
  assert.match(refusal(changed(r => { r.stacks.csharp.project = 'inputs/cache-projects/none'; })), /stacks\.csharp\.project.*exist/);
  assert.match(refusal(changed(r => { r.stacks.superseded = r.stacks.node; })), /superseded/);
  assert.match(refusal(changed(r => { r.hosts = 'mystery'; })), /hosts/, 'v1\'s own checks run first');
  // the toolchain pins agree with stacks-lang.json: each comparison is its own refusal
  assert.match(refusal(changed(r => { r.stacks.node.steps[0].expectStdout = 'v26.12.1'; })), /node --version.*v26\.13\.0/);
  assert.match(refusal(changed(r => { r.stacks.csharp.steps[0].expectStdout = '10.0.114'; })), /dotnet --version.*10\.0\.403/);
  assert.match(refusal(changed(r => { r.stacks.node.steps[4].expectStdout = 'Version 7.0.2'; })), /tsc --version.*Version 6\.0\.3/);
  assert.match(refusal(changed(r => { r.stacks.node.packages.find(p => p.name === 'typescript').spec = '7.0.2'; })), /typescript.*6\.0\.3/);
  assert.match(refusal(changed(r => { r.stacks.node.steps.splice(4, 1); })), /tsc --version/, 'a missing pin step is a refusal, not a pass');
});

/** The parent of every box folder a test makes: under the home (the agent box replaces /tmp), created once, owner only. */
const boxParent = () => { const dir = path.join(os.homedir(), '.cache', 'lang-v2', 't'); mkdirSync(dir, { recursive: true, mode: 0o700 }); return dir; };

test('A_placeholder_with_a_digit_is_refused_because_the_filler_would_leave_it_in_the_argv_and_that_is_the_point', async t => {
  // v1's filler knows only `{[A-Z_]+}`: a name with a digit is neither filled nor detected as unresolved, and reaches the argv as text
  const recipe = changed(r => {
    r.stacks.csharp.resolve.XUNIT_V3 = r.stacks.csharp.resolve.XUNIT;
    r.stacks.csharp.steps[1].argv = r.stacks.csharp.steps[1].argv.map(a => a.replace('{XUNIT}', '{XUNIT_V3}'));
  });
  const v1 = loadRecipe(recipe);
  assert.equal(v1.ok, true, 'v1\'s loader accepts it: that is the hole');
  assert.ok(recipe.stacks.csharp.steps[1].argv.includes('-p:SnapXunitVersion={XUNIT_V3}'));
  assert.match(refusal(recipe), /XUNIT_V3.*\[A-Z_\]\+/, 'the v2 loader refuses it, naming the field');
  assert.equal(pickHighest(JSON.stringify({ versions: ['4.0.2'] }), { line: '4' }), '4.0.2', 'control: the same list resolves under a name without a digit');
  if (!sandboxGate(t)) return;
  // the refuted shape, run for real: v1's step runner passes `{XUNIT_V3}` to the program as text and the step still succeeds
  const stub = { hosts: 'bare', stepTimeoutMs: 60_000, stacks: { csharp: { steps: [{ argv: ['sh', '-c', 'mkdir -p "$NUGET_PACKAGES" && printf %s "$0"', '{XUNIT_V3}'], expectStdout: '{XUNIT_V3}' }], capture: { nuget: 'box/nuget' } } } };
  const made = await makeSnapshot({ stack: 'csharp', recipe: stub, root: ownedFolder(t, 'fill', boxParent()), out: ownedFolder(t, 'fillo', boxParent()) });
  assert.equal(made.ok, true, made.reason);
  assert.equal(made.value.steps[0].stdout, '{XUNIT_V3}', 'the placeholder reached the argv unfilled and unnoticed');
});

// ---- the manifest, the used-versus-manifest check, the offline texts ---------------------------------------------------------------

const indexLine = (key, integrity) => `deadbeef\t${JSON.stringify({ key: `make-fetch-happen:request-cache:${key}`, ...(integrity === null ? {} : { integrity }), time: 1, size: 1 })}\n`;
const tarball = (name, version) => `https://registry.npmjs.org/${name}/-/${name.split('/').at(-1)}-${version}.tgz`;

/** A snapshot folder as the real maker leaves it: `npm/_cacache/index-v5/...` and `nuget/<id>/<version>/<id>.<version>.nupkg.sha512`. `null` for an integrity means the entry has none. */
function snapshotFolder(t, { npm = [], nuget = [] }) {
  const dir = ownedFolder(t, 'man');
  const index = path.join(dir, 'npm', '_cacache', 'index-v5', 'ab', 'cd');
  mkdirSync(index, { recursive: true });
  writeFileSync(path.join(index, 'e1'), npm.map(([n, v, h]) => indexLine(tarball(n, v), h)).join('') + indexLine('https://registry.npmjs.org/fastify', 'sha512-packument'));
  for (const [id, version, hash] of nuget) {
    mkdirSync(path.join(dir, 'nuget', id, version), { recursive: true });
    if (hash !== null) writeFileSync(path.join(dir, 'nuget', id, version, `${id}.${version}.nupkg.sha512`), `${hash}\n`);
  }
  return dir;
}

test('The_manifest_carries_every_package_with_its_integrity_in_a_stable_order', t => {
  const dir = snapshotFolder(t, {
    npm: [['pino', '10.1.0', 'sha512-p10'], ['pino', '9.14.0', 'sha512-p9'], ['fastify', '5.12.7', 'sha512-f'], ['@fastify/jwt', '10.2.6', 'sha512-j']],
    nuget: [['xunit.v3', '4.0.2', 'QQ=='], ['microsoft.extensions.hosting', '10.0.14', 'AA==']],
  });
  const m = manifestOf(dir);
  assert.equal(m.ok, true, m.reason);
  assert.deepEqual(m.value.map(p => `${p.ecosystem}:${p.name}@${p.version}:${p.integrity}`), [
    'npm:@fastify/jwt@10.2.6:sha512-j', 'npm:fastify@5.12.7:sha512-f', 'npm:pino@9.14.0:sha512-p9', 'npm:pino@10.1.0:sha512-p10',
    'nuget:microsoft.extensions.hosting@10.0.14:sha512-AA==', 'nuget:xunit.v3@4.0.2:sha512-QQ==',
  ], 'ecosystem, then name in plain string order, then version by its numeric parts (9.14.0 before 10.1.0)');
  assert.deepEqual(manifestOf(dir).value, m.value, 'the same folder gives the same manifest');
  const noNpmHash = manifestOf(snapshotFolder(t, { npm: [['left-pad', '1.3.0', null]] }));
  assert.equal(noNpmHash.ok, false);
  assert.match(noNpmHash.reason, /npm:left-pad@1\.3\.0.*no integrity/, 'an npm package without a hash fails naming it');
  const noNugetHash = manifestOf(snapshotFolder(t, { nuget: [['newtonsoft.json', '13.0.3', null]] }));
  assert.equal(noNugetHash.ok, false);
  assert.match(noNugetHash.reason, /nuget:newtonsoft\.json@13\.0\.3.*no integrity/, 'and so does a NuGet one');
  const same = checkManifestMatches(m.value, m.value.map(({ ecosystem, name, version }) => ({ ecosystem, name, version })));
  assert.equal(same.ok, true, same.reason);
  const differs = checkManifestMatches(m.value, [{ ecosystem: 'npm', name: 'fastify', version: '5.12.7' }]);
  assert.equal(differs.ok, false);
  assert.match(differs.reason, /npm:pino@10\.1\.0/, 'a difference between the manifest and what the maker listed is named');
});

const LOCK = { packages: { '': { name: 'ws', version: '1.0.0' }, 'node_modules/fastify': { version: '5.12.7' }, 'node_modules/@fastify/jwt': { version: '10.2.6' }, 'node_modules/fastify/node_modules/pino': { version: '9.14.0' }, 'node_modules/ws-link': { link: true, resolved: '../x' } } };
const ASSETS = { libraries: { 'Microsoft.Extensions.Hosting/10.0.14': { type: 'package' }, 'SnapDaemon/1.0.0': { type: 'project', path: '../daemon/SnapDaemon.csproj' }, 'XUnit.V3/4.0.2': { type: 'package' } } };

test('What_an_offline_restore_used_is_checked_against_the_manifest_naming_any_package_outside_it', () => {
  assert.deepEqual(usedNpm(LOCK), ['@fastify/jwt@10.2.6', 'fastify@5.12.7', 'pino@9.14.0'], 'every node_modules entry, nested ones by their own name; the root and a link are not packages');
  assert.deepEqual(usedNuget(ASSETS), ['microsoft.extensions.hosting@10.0.14', 'xunit.v3@4.0.2'], 'only type package, in lower case: a project reference is not a package');
  const manifest = [{ ecosystem: 'npm', name: 'fastify', version: '5.12.7' }, { ecosystem: 'npm', name: '@fastify/jwt', version: '10.2.6' }, { ecosystem: 'nuget', name: 'microsoft.extensions.hosting', version: '10.0.14' }];
  assert.deepEqual(outsideManifest(usedNpm(LOCK), manifest, 'npm'), ['pino@9.14.0'], 'a package the registered manifest does not list is named');
  assert.deepEqual(outsideManifest(usedNuget(ASSETS), manifest, 'nuget'), ['xunit.v3@4.0.2']);
  assert.deepEqual(outsideManifest(['fastify@5.12.7'], manifest, 'nuget'), ['fastify@5.12.7'], 'a name of the other ecosystem does not count');
  assert.deepEqual(outsideManifest([], manifest, 'npm'), []);
});

test('The_offline_nuget_config_clears_every_inherited_source', () => {
  const text = offlineNugetConfig('/w/box/nuget');
  assert.equal(text.ok, true, text.reason);
  assert.equal(text.value, `<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="offline" value="/w/box/nuget" />
  </packageSources>
  <fallbackPackageFolders>
    <clear />
  </fallbackPackageFolders>
</configuration>
`);
  assert.equal((text.value.match(/<add /g) ?? []).length, 1, 'exactly one source');
  assert.equal((text.value.match(/<clear \/>/g) ?? []).length, 2, 'both lists are cleared: a user-level source or fallback folder cannot come back');
  for (const bad of ['/w/"x"', '/w/a b', '/w/<x>', 'rel/../x"', '', '/w/a&b']) assert.equal(offlineNugetConfig(bad).ok, false, `${JSON.stringify(bad)} is refused`);
  assert.deepEqual(npmOfflineArgv(['fastify@5.12.7']), ['npm', 'install', '--offline', '--no-audit', '--no-fund', '--no-update-notifier', 'fastify@5.12.7']);
  assert.deepEqual(NPM_OFFLINE_ENV, { npm_config_offline: 'true', npm_config_update_notifier: 'false', npm_config_logs_max: '0' });
  assert.equal(NPMRC_OFFLINE, 'offline=true\n');
});

// ---- the store: the staging sweep, supersede, prune -----------------------------------------------------------------------------

const dirStat = over => ({ isDirectory: () => true, isSymbolicLink: () => false, uid: process.getuid(), ...over });
const me = thisOwner();
const verdictOf = ({ name = '.building-node-4242', stat = dirStat(), owner, probe = () => ({ state: 'live', start: me.start }) }) => stagingVerdict({ name, stat, owner, probe, me, uid: process.getuid() });
const claimOf = over => parseOwner(recordOf(over));

test('A_staging_folder_is_removed_only_when_its_owner_is_proven_gone', t => {
  for (const name of ['.building-ruby-12', '.building-node-0', '.building-node-12.owner', '.building-node-012', 'cache-node-12', '.building-node-12x']) assert.equal(verdictOf({ name, owner: claimOf({}) }), null, `${name} is not looked at`);
  assert.deepEqual(verdictOf({ stat: dirStat({ isDirectory: () => false }), owner: claimOf({ pid: 1 }), probe: () => ({ state: 'dead' }) }), { action: 'keep', why: 'not this user\'s staging folder' }, 'a file or a symlink is kept even with a dead owner');
  assert.equal(verdictOf({ stat: dirStat({ isSymbolicLink: () => true }), owner: claimOf({}) }).action, 'keep');
  assert.equal(verdictOf({ stat: dirStat({ uid: process.getuid() + 1 }), owner: claimOf({}), probe: () => ({ state: 'dead' }) }).why, 'not this user\'s staging folder', 'another uid, by the injected uid');
  for (const owner of [null, { unreadable: true }]) assert.deepEqual(verdictOf({ owner, probe: () => ({ state: 'dead' }) }), { action: 'keep', why: 'no readable owner claim' }, 'no claim: kept even when nothing could be alive');
  for (const other of [{ owner: 'u1' }, { host: 'another@linux' }, { ns: me.ns + 1 }]) assert.deepEqual(verdictOf({ owner: claimOf(other), probe: () => ({ state: 'dead' }) }), { action: 'keep', why: 'claimed by another owner or host' });
  assert.deepEqual(verdictOf({ owner: claimOf({ pid: 4242 }), probe: () => ({ state: 'dead' }) }), { action: 'remove', why: 'owner pid 4242 is gone' });
  const reused = verdictOf({ owner: claimOf({ pid: 4242, start: me.start - 500 }), probe: () => ({ state: 'live', start: me.start }) });
  // fix batch 1 (C0): the conventions' own `ownerState` calls a live pid with another start time `unknown`, and unknown KEEPS the folder (the accepted code-round finding replaces the old rule 6)
  assert.equal(reused.action, 'keep', 'a live pid with another start is `unknown`: kept, never removed');
  assert.match(reused.why, /owner state unknown/, 'and the log line says why');
  for (const probe of [() => ({ state: 'live', start: me.start }), () => ({ state: 'live', start: me.start + START_TOLERANCE_S }), () => ({ state: 'live' })]) {
    assert.deepEqual(verdictOf({ owner: claimOf({}), probe }), { action: 'keep', why: 'owner may be alive' }, 'this process\'s own claim, a start within the tolerance, a start not known');
  }
  assert.match(verdictOf({ owner: claimOf({}), probe: () => ({ state: 'unknown' }) }).why, /owner state unknown/, 'a pid that cannot be asked is unknown (fix batch 1, C0): kept and named');
  // on disk: only the folder of a dead owner goes, with its claim; everything else stays and every folder is named on the log
  const caches = ownedFolder(t, 'stg');
  const make = (name, claim) => { const d = path.join(caches, name); mkdirSync(path.join(d, 'sub'), { recursive: true }); writeFileSync(path.join(d, 'sub', 'f'), 'x'); if (claim !== undefined) writeClaim(d, claim); return d; };
  const gone = exitedPid();
  const dead = make(`.building-csharp-${gone}`, { pid: gone });
  chmodSync(path.join(dead, 'sub', 'f'), 0o444); chmodSync(path.join(dead, 'sub'), 0o555);
  const own = make(`.building-node-${process.pid}`, {});
  const unclaimed = make('.building-node-9001');
  symlinkSync(own, path.join(caches, '.building-csharp-9002'));
  const lines = [];
  const swept = sweepStaging({ caches, log: line => lines.push(line) });
  assert.deepEqual(swept.removed.map(r => path.basename(r.dir)), [path.basename(dead)]);
  assert.equal(existsSync(dead) || existsSync(`${dead}.owner`), false, 'the folder and its claim are gone, although it held a read-only tree');
  for (const kept of [own, unclaimed, path.join(caches, '.building-csharp-9002')]) assert.equal(existsSync(kept), true, `${kept} is kept`);
  assert.equal(lines.length, 4, 'every folder looked at is one line');
  assert.match(lines.find(l => l.includes(path.basename(dead))), /remove.*owner pid \d+ is gone/);
  assert.match(lines.find(l => l.includes('9001')), /keep.*no readable owner claim/);
  assert.equal(sweepStaging({ caches, log: () => {}, uid: process.getuid() + 1 }).removed.length, 0, 'another uid removes nothing');
});

const recordFor = (hash, over = {}) => ({ treeSha256: hash, dirName: `x-${hash}`, parts: {}, sizeBytes: 1, packages: [], resolved: {}, steps: [], recipeSha256: 'r', projectSha256: null, madeOn: '2026-11-01', ...over });
const noTag = () => ok([]);
const taggedA = () => ok(['prereg/lang-v2-A-2026-12-01']);

test('A_stack_is_superseded_by_moving_its_record_never_by_deleting_it', t => {
  const file = path.join(ownedFolder(t, 'sup'), 'rec.json');
  writeFileSync(file, `${JSON.stringify({ csharp: recordFor('a'.repeat(64)), node: recordFor('b'.repeat(64)), superseded: [] }, null, 2)}\n`);
  const before = readFileSync(file, 'utf8');
  for (const [why, args] of [['no record for the stack', { stack: 'ruby', reason: 'x' }], ['an empty reason', { stack: 'csharp', reason: '  ' }], ['a pre-registration tag', { stack: 'csharp', reason: 'x', tagsOf: taggedA }], ['a tag that cannot be read', { stack: 'csharp', reason: 'x', tagsOf: () => fail('git did not answer') }]]) {
    const refused = supersede({ file, tagsOf: noTag, today: '2026-11-05', ...args });
    assert.equal(refused.ok, false, why);
    assert.equal(readFileSync(file, 'utf8'), before, `${why}: the file is untouched`);
  }
  const first = supersede({ file, stack: 'csharp', reason: 'the set changed', tagsOf: noTag, today: '2026-11-05' });
  assert.equal(first.ok, true, first.reason);
  const now = JSON.parse(readFileSync(file, 'utf8'));
  assert.deepEqual(Object.keys(now), ['node', 'superseded'], 'the stack key is gone so the next build may register again');
  assert.deepEqual(now.superseded, [{ stack: 'csharp', supersededOn: '2026-11-05', reason: 'the set changed', record: recordFor('a'.repeat(64)) }], 'the whole record is moved');
  assert.equal(supersede({ file, stack: 'node', reason: 'again', tagsOf: noTag, today: '2026-11-06' }).ok, true);
  const later = JSON.parse(readFileSync(file, 'utf8')).superseded;
  assert.deepEqual(later.map(s => s.stack), ['csharp', 'node'], 'append only: the first entry is still there');
  assert.deepEqual(later[0], now.superseded[0]);
  assert.deepEqual(readdirSync(path.dirname(file)), ['rec.json'], 'no temporary file is left beside it');
  assert.equal(registerSnapshot({ file, stack: 'csharp', record: recordFor('c'.repeat(64)), madeOn: '2026-11-07' }).ok, true, 'a superseded stack registers again');
});

/** A snapshot folder `<stack>-<its own tree hash>` made read-only, as the maker leaves it. */
function snapshotAt(caches, stack, files) {
  const staging = path.join(caches, `.work-${stack}-${Object.keys(files).join('')}`);
  for (const [rel, text] of Object.entries(files)) { mkdirSync(path.dirname(path.join(staging, rel)), { recursive: true }); writeFileSync(path.join(staging, rel), text); }
  const hash = treeHash(staging);
  renameSync(staging, path.join(caches, `${stack}-${hash}`));
  makeReadOnly(path.join(caches, `${stack}-${hash}`));
  return hash;
}

test('Pruning_removes_only_a_superseded_snapshot_that_re_hashes_to_its_record_and_never_the_registered_one', t => {
  const root = ownedFolder(t, 'prn');
  const caches = path.join(root, 'caches');
  mkdirSync(caches);
  const registered = snapshotAt(caches, 'csharp', { 'nuget/a': 'reg' });
  const old = snapshotAt(caches, 'node', { 'npm/a': 'old' });
  const tampered = snapshotAt(caches, 'node', { 'npm/b': 'tampered' });
  makeWritable(path.join(caches, `node-${tampered}`));
  writeFileSync(path.join(caches, `node-${tampered}`, 'npm', 'b'), 'changed');
  const aLink = `node-${'e'.repeat(64)}`;
  symlinkSync(path.join(caches, `node-${old}`), path.join(caches, aLink));
  mkdirSync(path.join(caches, `node-${'f'.repeat(64)}`));
  mkdirSync(path.join(caches, '.building-node-77'));
  const file = path.join(root, 'rec.json');
  const superseded = [[old, '2026-11-05', 'a'], [tampered, '2026-11-05', 'b'], ['e'.repeat(64), '2026-11-06', 'c']].map(([h, on, why]) => ({ stack: 'node', supersededOn: on, reason: why, record: recordFor(h) }));
  writeFileSync(file, JSON.stringify({ csharp: recordFor(registered), superseded }));
  const refusedEarly = pruneSuperseded({ file, caches, tagsOf: noTag });
  assert.equal(refusedEarly.ok, false);
  assert.match(refusedEarly.reason, /pre-registration tag/, 'before the tag the rule is named');
  assert.equal(existsSync(path.join(caches, `node-${old}`)), true, 'nothing was removed');
  const pruned = pruneSuperseded({ file, caches, tagsOf: taggedA, recordAtTag: () => ok(JSON.parse(readFileSync(file, 'utf8'))) });
  assert.equal(pruned.ok, true, pruned.reason);
  assert.deepEqual(pruned.value.removed, [{ name: `node-${old}`, supersededOn: '2026-11-05', reason: 'a' }], 'only the superseded snapshot that still hashes to its record');
  const keptWhy = Object.fromEntries(pruned.value.kept.map(k => [k.name, k.why]));
  assert.equal(keptWhy[`csharp-${registered}`], 'registered');
  assert.equal(keptWhy[`node-${tampered}`], 'hash differs');
  assert.equal(keptWhy[aLink], 'not this user\'s', 'a link is never followed');
  assert.equal(keptWhy[`node-${'f'.repeat(64)}`], 'not a snapshot of this series');
  assert.equal(keptWhy['.building-node-77'], 'not a snapshot of this series');
  assert.equal(treeHash(path.join(caches, `csharp-${registered}`)), registered, 'the registered snapshot is untouched');
  assert.equal(existsSync(path.join(caches, `node-${tampered}`)), true);
  const otherUser = pruneSuperseded({ file, caches, tagsOf: taggedA, recordAtTag: () => ok(JSON.parse(readFileSync(file, 'utf8'))), uid: process.getuid() + 1 });
  assert.deepEqual(otherUser.value.removed, [], 'a folder of another uid is never removed');
  writeFileSync(file, JSON.stringify({ node: recordFor(old), superseded }));
  assert.equal(pruneSuperseded({ file, caches, tagsOf: taggedA, recordAtTag: () => ok(JSON.parse(readFileSync(file, 'utf8'))) }).value.kept.some(k => k.name === `node-${old}`), false, 'already removed: nothing left to name');
  makeWritable(caches);
});

test('Pruning_before_the_pre_registration_tag_is_refused', t => {
  const root = ownedFolder(t, 'prb');
  mkdirSync(path.join(root, 'caches'));
  const file = path.join(root, 'rec.json');
  writeFileSync(file, JSON.stringify({ superseded: [] }));
  for (const tagsOf of [noTag, () => fail('git did not answer')]) {
    const refused = pruneSuperseded({ file, caches: path.join(root, 'caches'), tagsOf });
    assert.equal(refused.ok, false);
    assert.match(refused.reason, /pre-registration tag|git did not answer/);
  }
});

// ---- the command's refusals (child process; no build starts in any of these) --------------------------------------------------------

const MAKE = path.join(SPIKE, 'make-snapshots.mjs');
const REPO = path.resolve(SPIKE, '..', '..');
const runMake = args => runBounded(process.execPath, [MAKE, ...args], { timeoutMs: 120_000 });

test('The_command_refuses_a_held_gate_lock_an_already_registered_stack_and_bad_usage_with_its_exit_codes', async t => {
  const root = ownedFolder(t, 'cmd');
  // every run names a store that is not there: whatever a refusal under test fails to stop is stopped by the preflight, and no build can start from this test
  const noStore = { ...readJson('inputs/stacks-lang.json'), storeRoot: ['.cache', 'lang-v2', 'no-store-here'] };
  writeFileSync(path.join(root, 'stacks.json'), JSON.stringify(noStore));
  const flags = { '--caches': path.join(root, 'caches'), '--record': path.join(root, 'rec.json'), '--gate-lock': path.join(root, 'gate.lock'), '--work': path.join(root, 'work'), '--stacks': path.join(root, 'stacks.json') };
  const argsOf = (...more) => [...Object.entries(flags).flat(), ...more];
  const noBuild = () => { assert.equal(existsSync(flags['--caches']), false, 'no caches folder was made'); assert.equal(existsSync(flags['--work']), false, 'no work folder was made'); };
  // bad usage first: each is exit 2 and names what is wrong, before any lock or folder
  for (const [args, named] of [[['--bogus'], /bogus/], [['--stack', 'ruby'], /ruby.*csharp, node|csharp, node.*ruby/], [['--supersede', 'csharp', '--prune-superseded'], /one of/], [['--stack', 'csharp', '--supersede', 'node'], /one of/], [['--caches', path.join(REPO, 'spikes', 'lang-bench', 'no-caches-here')], /worktree/]]) {
    const r = await runMake(argsOf(...args));
    assert.equal(r.value.code, 2, `${args.join(' ')}: ${r.value.stderr}`);
    assert.match(r.value.stderr, named);
  }
  noBuild();
  // a held gate lock: exit 3, naming the owner, and nothing started
  const held = takeGateLock({ file: flags['--gate-lock'] });
  assert.equal(held.ok, true, held.reason);
  const locked = await runMake(argsOf());
  assert.equal(locked.value.code, 3, locked.value.stderr);
  assert.match(locked.value.stderr, new RegExp(`gate lock held by ${held.value.text.trim().split(':')[0]}`));
  noBuild();
  held.value.release();
  // an already registered stack: refused before the preflight (which would refuse this store and say so instead)
  writeFileSync(flags['--record'], JSON.stringify({ csharp: recordFor('a'.repeat(64)), superseded: [] }));
  const registered = await runMake(argsOf('--stack', 'csharp'));
  assert.equal(registered.value.code, 3, registered.value.stderr);
  assert.match(registered.value.stderr, /csharp is already registered/);
  assert.doesNotMatch(registered.value.stderr, /preflight/, 'the registration check comes before the build, and so before the preflight');
  assert.equal(existsSync(path.join(flags['--work'], 'snapshot-csharp-1')), false, 'no build was started');
  const beforeBuild = await runMake(argsOf('--stack', 'node'));
  assert.equal(beforeBuild.value.code, 3, 'control: a stack that is not registered goes on to the preflight, which refuses this store');
  assert.match(beforeBuild.value.stderr, /preflight/);
  assert.equal(readFileSync(flags['--record'], 'utf8'), JSON.stringify({ csharp: recordFor('a'.repeat(64)), superseded: [] }), 'the record is untouched');
});

test('The_command_prints_the_PACKAGES_md_of_a_registered_stack_from_the_record_and_refuses_one_that_is_not_there', async t => {
  const root = ownedFolder(t, 'prt');
  const real = realRecipe();
  const resolved = Object.fromEntries(Object.keys(real.stacks.node.resolve).map(k => [k, '1.2.3']));
  const packages = real.stacks.node.packages.map(p => ({ ecosystem: 'npm', name: p.name, version: p.spec.replace(/\{([A-Z_]+)\}/, (m, k) => resolved[k]), integrity: 'sha512-x' }));
  const rec = path.join(root, 'rec.json');
  writeFileSync(rec, JSON.stringify({ node: recordFor('d'.repeat(64), { packages, resolved }), superseded: [] }));
  const printed = await runMake(['--print-packages-md', 'node', '--record', rec]);
  assert.equal(printed.value.code, 0, printed.value.stderr);
  const expected = renderPackagesMd({ stack: 'node', record: JSON.parse(readFileSync(rec, 'utf8')), recipe: real });
  assert.equal(expected.ok, true, expected.reason);
  assert.equal(printed.value.stdout, expected.value, 'the command prints exactly what the renderer derives from the record');
  const missing = await runMake(['--print-packages-md', 'csharp', '--record', rec]);
  assert.equal(missing.value.code, 3, 'a stack with no record is refused');
  assert.match(missing.value.stderr, /csharp/);
  assert.equal((await runMake(['--print-packages-md', 'ruby', '--record', rec])).value.code, 2, 'an unknown stack is a usage error');
});
