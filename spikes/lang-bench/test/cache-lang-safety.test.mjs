// E1.S4, fix batch 1 (the code-round findings C0-C11 and the Opus review's M and L items), fast cases: no network. Each case names the mechanism and builds the unsafe shape beside the safe one.
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { cpSync, existsSync, lstatSync, mkdirSync, readFileSync, readdirSync, renameSync, rmSync, symlinkSync, writeFileSync } from 'node:fs';
import { spawnSync } from 'node:child_process';
import path from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { makeReadOnly, makeWritable, treeHash } from '../../minigit-bench/lib/cache-snapshot.mjs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { runBounded } from '../../minigit-bench/lib/run.mjs';
import { thisOwner } from '../../../.agents/conventions/tools/lib/owned.mjs';
import { readWrapped, wrappedArgv } from '../lib/cache-offline.mjs';
import { loadRecipeLang } from '../lib/cache-recipe-lang.mjs';
import { buildRecord, discardUnlessRecorded, manifestSha256Of, pruneSuperseded, registerBuilt, redactUserInfo, remoteUrlFor, sortPackages, supersede, sweepStaging, tagsOf } from '../lib/cache-store.mjs';
import { npmLicences } from '../lib/package-metadata.mjs';
import { takeGateLock } from '../lib/gate-lock.mjs';
import { loadStacksLang } from '../lib/toolchain-pins.mjs';
import { exitedPid, recordOf, writeClaim } from './support/owners.mjs';
import { ownedFolder } from './toolchain-fixtures.mjs';

const SPIKE = fileURLToPath(new URL('..', import.meta.url));
const REPO = path.resolve(SPIKE, '..', '..');
const readJson = rel => JSON.parse(readFileSync(path.join(SPIKE, rel), 'utf8'));
const stacks = loadStacksLang(readJson('inputs/stacks-lang.json')).value;
const realRecipe = () => readJson('inputs/cache-recipe-lang.json');
const recordFor = (hash, over = {}) => ({ treeSha256: hash, dirName: `x-${hash}`, parts: {}, sizeBytes: 1, packages: [], resolved: {}, steps: [], recipeSha256: 'r', projectSha256: null, madeOn: '2026-11-01', ...over });
const noTag = () => ok([]);
const taggedA = () => ok(['prereg/lang-v2-A-2026-12-01']);
const me = thisOwner();

// ---- C0 and M2: the staging sweep --------------------------------------------------------------------------------------------------

test('A_staging_folder_whose_owner_state_is_unknown_is_kept_and_named_never_removed', t => {
  const caches = ownedFolder(t, 'stk');
  const dir = path.join(caches, `.building-node-${process.pid}`);
  mkdirSync(dir);
  writeClaim(dir, { start: me.start - 500 }); // this pid, alive, with another start time: the conventions say `unknown`
  const lines = [];
  const swept = sweepStaging({ caches, log: line => lines.push(line) });
  assert.deepEqual(swept.removed, []);
  assert.equal(existsSync(dir), true, 'the folder is kept');
  assert.match(lines.join('\n'), /keep .*owner state unknown/, 'and named on the log');
});

test('An_orphan_staging_claim_is_judged_by_the_same_owner_rules_as_a_folder', t => {
  const caches = ownedFolder(t, 'orp');
  const gone = exitedPid();
  const claims = { dead: path.join(caches, `.building-csharp-${gone}`), live: path.join(caches, `.building-node-${process.pid}`), junk: path.join(caches, '.building-node-9101'), other: path.join(caches, '.building-node-9102') };
  writeClaim(claims.dead, { pid: gone });
  writeClaim(claims.live, {});
  writeFileSync(`${claims.junk}.owner`, 'not a claim\n');
  writeClaim(claims.other, { host: 'another@linux' });
  const lines = [];
  const swept = sweepStaging({ caches, log: line => lines.push(line) });
  assert.deepEqual(swept.removed.map(r => path.basename(r.dir)), [path.basename(claims.dead)]);
  assert.equal(existsSync(`${claims.dead}.owner`), false, 'the claim of a dead owner, with no folder, is removed');
  for (const kept of [claims.live, claims.junk, claims.other]) assert.equal(existsSync(`${kept}.owner`), true, `${kept}.owner is kept`);
  assert.equal(lines.length, 4, 'every claim looked at is one line');
});

// ---- M2: a failed build leaves no snapshot folder ----------------------------------------------------------------------------------

/** A made snapshot as v1's maker returns it, in `caches`, with the given nuget packages (`[id, version, hash | null]`). */
function madeSnapshot(caches, nuget) {
  const stage = path.join(caches, '.work-made');
  for (const [id, version, hash] of nuget) {
    mkdirSync(path.join(stage, 'nuget', id, version), { recursive: true });
    if (hash !== null) writeFileSync(path.join(stage, 'nuget', id, version, `${id}.${version}.nupkg.sha512`), `${hash}\n`);
    else writeFileSync(path.join(stage, 'nuget', id, version, 'x.txt'), 'x');
  }
  const treeSha256 = treeHash(stage);
  const dir = path.join(caches, `csharp-${treeSha256}`);
  mkdirSync(path.dirname(dir), { recursive: true });
  return { stage, dir, treeSha256 };
}

test('A_snapshot_folder_left_by_a_failed_registration_is_removed_unless_it_is_in_the_record', t => {
  const root = ownedFolder(t, 'fin');
  const file = path.join(root, 'rec.json');
  const mk = (name, nuget) => { const caches = path.join(root, name); mkdirSync(caches); const m = madeSnapshot(caches, nuget); return { ...m, caches, made: { dir: m.dir, treeSha256: m.treeSha256, parts: { nuget: 'p' }, sizeBytes: 1, packages: nuget.map(([n, v]) => ({ ecosystem: 'nuget', name: n, version: v })), resolved: {}, steps: [] } }; };
  const place = m => { renameSync(m.stage, m.dir); makeReadOnly(m.dir); };
  // fix batch 3 (F2): the discard needs a record that READS CORRECTLY and does not hold the pair; an empty record does
  writeFileSync(file, '{}\n');
  // a manifest failure after a successful snapshot (a package with no hash): no folder is left
  const bad = mk('c1', [['demo', '1.0.0', null]]);
  place(bad);
  const failed = registerBuilt({ made: bad.made, stack: 'csharp', recipeSha256: 'r', projectSha256: null, file, madeOn: '2026-11-04' });
  assert.equal(failed.ok, false);
  assert.match(failed.reason, /demo@1\.0\.0.*no integrity/);
  assert.equal(existsSync(bad.dir), false, 'the folder of a failed build is removed, although it is read-only');
  assert.equal(readFileSync(file, 'utf8'), '{}\n', 'and nothing is registered');
  // a good one registers and stays
  const good = mk('c2', [['demo', '1.0.0', 'QQ==']]);
  place(good);
  const done = registerBuilt({ made: good.made, stack: 'csharp', recipeSha256: 'r', projectSha256: null, file, madeOn: '2026-11-04' });
  assert.equal(done.ok, true, done.reason);
  assert.equal(existsSync(good.dir), true);
  assert.equal(JSON.parse(readFileSync(file, 'utf8')).csharp.treeSha256, good.treeSha256);
  // a second build whose registration is refused (the stack is already there) is removed; the registered folder stays
  const again = mk('c3', [['other', '2.0.0', 'QQ==']]);
  place(again);
  const refused = registerBuilt({ made: again.made, stack: 'csharp', recipeSha256: 'r', projectSha256: null, file, madeOn: '2026-11-04' });
  assert.equal(refused.ok, false);
  assert.equal(existsSync(again.dir), false, 'a refused registration leaves no folder either');
  assert.equal(existsSync(good.dir), true, 'the registered one is never touched');
  // a folder that IS in the record (registered or superseded) is never removed by a failure
  const same = mk('c4', [['demo', '1.0.0', 'QQ==']]);
  place(same);
  assert.equal(same.treeSha256, good.treeSha256, 'setup: the same content has the same hash');
  assert.equal(registerBuilt({ made: same.made, stack: 'csharp', recipeSha256: 'r', projectSha256: null, file, madeOn: '2026-11-04' }).ok, false);
  assert.equal(existsSync(same.dir), true, 'its (stack, hash) is registered: kept');
  makeWritable(root);
});

// ---- C4: the temporary name of the supersede write --------------------------------------------------------------------------------

test('A_stale_temporary_file_beside_the_record_does_not_stop_a_supersede_and_the_temporary_name_is_random', t => {
  const file = path.join(ownedFolder(t, 'tmp'), 'rec.json');
  writeFileSync(file, `${JSON.stringify({ csharp: recordFor('a'.repeat(64)), superseded: [] })}\n`);
  writeFileSync(`${file}.tmp-${process.pid}`, 'left by an earlier run that was killed');
  const done = supersede({ file, stack: 'csharp', reason: 'x', tagsOf: noTag, today: '2026-11-05' });
  assert.equal(done.ok, true, done.reason);
  assert.equal(JSON.parse(readFileSync(file, 'utf8')).superseded.length, 1);
  assert.equal(readFileSync(`${file}.tmp-${process.pid}`, 'utf8'), 'left by an earlier run that was killed', 'a file that is not ours is not touched');
});

// ---- C10: prune judges a folder by its OWN (stack, hash) pair -------------------------------------------------------------------

function snapshotAt(caches, stack, files) {
  const staging = path.join(caches, `.work-${stack}`);
  for (const [rel, text] of Object.entries(files)) { mkdirSync(path.dirname(path.join(staging, rel)), { recursive: true }); writeFileSync(path.join(staging, rel), text); }
  const hash = treeHash(staging);
  renameSync(staging, path.join(caches, `${stack}-${hash}`));
  makeReadOnly(path.join(caches, `${stack}-${hash}`));
  return hash;
}

test('A_hash_registered_under_another_stack_neither_keeps_nor_licenses_the_removal_of_a_folder', t => {
  const root = ownedFolder(t, 'pr2');
  const caches = path.join(root, 'caches');
  mkdirSync(caches);
  const hash = snapshotAt(caches, 'node', { 'npm/a': 'same bytes' });
  renameSync(path.join(caches, `node-${hash}`), path.join(caches, `csharp-${hash}`));
  mkdirSync(path.join(caches, '.copy'));
  cpSync(path.join(caches, `csharp-${hash}`), path.join(caches, `node-${hash}`), { recursive: true });
  makeReadOnly(path.join(caches, `node-${hash}`));
  const file = path.join(root, 'rec.json');
  // csharp is REGISTERED with this hash, node has it SUPERSEDED: the node folder may go, the csharp folder is the registered one
  writeFileSync(file, JSON.stringify({ csharp: recordFor(hash), superseded: [{ stack: 'node', supersededOn: '2026-11-05', reason: 'r', record: recordFor(hash) }] }));
  const pruned = pruneSuperseded({ caches, tagsOf: taggedA, recordAtTag: () => ok(JSON.parse(readFileSync(file, 'utf8'))) });
  assert.equal(pruned.ok, true, pruned.reason);
  assert.deepEqual(pruned.value.removed.map(r => r.name), [`node-${hash}`], 'the pair (node, hash) is superseded and not registered: removed');
  assert.equal(pruned.value.kept.find(k => k.name === `csharp-${hash}`).why, 'registered', 'the pair (csharp, hash) is the registered one: kept');
  assert.equal(existsSync(path.join(caches, `csharp-${hash}`)), true);
  makeWritable(root);
});

// ---- L2 and L3: the tag reader and the record at the tag --------------------------------------------------------------------------

const git = (cwd, ...args) => spawnSync('git', ['-c', 'user.name=t', '-c', 'user.email=t@example.com', ...args], { cwd, encoding: 'utf8', timeout: 20_000 });
const TAG = 'prereg/lang-v2-A-2026-12-01';

test('The_pre_registration_tag_is_read_from_the_remote_too_and_a_git_that_cannot_answer_is_a_refusal', t => {
  const root = ownedFolder(t, 'tag');
  const [repo, origin] = [path.join(root, 'repo'), path.join(root, 'origin.git')];
  mkdirSync(repo); mkdirSync(origin);
  assert.equal(git(origin, 'init', '--bare', '-q').status, 0);
  assert.equal(git(repo, 'init', '-q').status, 0);
  assert.equal(git(repo, 'commit', '--allow-empty', '-q', '-m', 'x').status, 0);
  assert.equal(tagsOf(repo).ok, false, 'no origin: git cannot answer about the remote, so the answer is a refusal, never "no tag"');
  assert.equal(git(repo, 'remote', 'add', 'origin', origin).status, 0);
  const none = tagsOf(repo);
  assert.equal(none.ok, true, none.reason);
  assert.deepEqual(none.value, []);
  assert.equal(git(repo, 'tag', TAG).status, 0);
  assert.equal(git(repo, 'push', '-q', 'origin', TAG).status, 0);
  assert.equal(git(repo, 'tag', '-d', TAG).status, 0);
  assert.deepEqual(tagsOf(repo).value, [TAG], 'a tag that exists only on the remote is seen');
  assert.equal(git(repo, 'remote', 'set-url', 'origin', path.join(root, 'nowhere.git')).status, 0);
  const unreachable = tagsOf(repo);
  assert.equal(unreachable.ok, false, 'an unreachable remote fails closed');
});

test('After_the_tag_prune_reads_the_record_at_the_tag_not_the_one_in_the_working_tree', t => {
  const root = ownedFolder(t, 'pat');
  const caches = path.join(root, 'caches');
  mkdirSync(caches);
  const hash = snapshotAt(caches, 'node', { 'npm/a': 'old' });
  const file = path.join(root, 'rec.json');
  const entry = { stack: 'node', supersededOn: '2026-11-05', reason: 'r', record: recordFor(hash) };
  writeFileSync(file, JSON.stringify({ superseded: [entry] })); // the working tree says "superseded"
  const seen = [];
  const atTag = tag => { seen.push(tag); return ok({ node: recordFor(hash), superseded: [] }); }; // the record at the tag says "registered"
  const pruned = pruneSuperseded({ caches, tagsOf: taggedA, recordAtTag: atTag });
  assert.equal(pruned.ok, true, pruned.reason);
  assert.deepEqual(seen, ['prereg/lang-v2-A-2026-12-01']);
  assert.deepEqual(pruned.value.removed, [], 'the tagged record decides: registered, kept');
  assert.equal(existsSync(path.join(caches, `node-${hash}`)), true);
  const unreadable = pruneSuperseded({ caches, tagsOf: taggedA, recordAtTag: () => fail('git show failed') });
  assert.equal(unreadable.ok, false, 'a record that cannot be read at the tag fails closed');
  makeWritable(root);
});

// ---- M4: the manifest hash --------------------------------------------------------------------------------------------------------

test('The_record_carries_the_hash_of_its_sorted_manifest_so_a_rebuild_can_be_matched_by_it', () => {
  const manifest = [{ ecosystem: 'npm', name: 'pino', version: '10.4.0', integrity: 'sha512-p' }, { ecosystem: 'npm', name: 'fastify', version: '5.12.7', integrity: 'sha512-f' }, { ecosystem: 'nuget', name: 'xunit.v3', version: '4.0.2', integrity: 'sha512-x' }];
  const snapshot = { dir: `/c/node-${'a'.repeat(64)}`, treeSha256: 'a'.repeat(64), parts: { npm: 'b'.repeat(64) }, sizeBytes: 1, packages: [], resolved: {}, steps: [] };
  const expected = createHash('sha256').update(JSON.stringify(sortPackages(manifest).map(p => [p.ecosystem, p.name, p.version, p.integrity]))).digest('hex');
  const record = buildRecord({ snapshot, manifest, recipeSha256: 'r', projectSha256: null });
  assert.equal(record.manifestSha256, expected, 'sha256 of the sorted (ecosystem, name, version, integrity) rows');
  assert.equal(buildRecord({ snapshot, manifest: [...manifest].reverse(), recipeSha256: 'r', projectSha256: null }).manifestSha256, expected, 'the order the packages were read in does not change it');
  assert.equal(manifestSha256Of(manifest.map((p, i) => (i === 0 ? { ...p, integrity: 'sha512-other' } : p))) === expected, false, 'one changed integrity changes it');
  const keys = Object.keys(record);
  assert.equal(keys[keys.indexOf('packages') + 1], 'manifestSha256', 'it sits right after the manifest it hashes');
});

// ---- C2, C5, C11, L4: the gate lock -----------------------------------------------------------------------------------------------

test('A_gate_lock_in_a_folder_that_is_a_link_or_not_this_users_is_refused_naming_the_folder', t => {
  const root = ownedFolder(t, 'glf');
  const real = path.join(root, 'real');
  mkdirSync(real);
  symlinkSync(real, path.join(root, 'link'));
  const viaLink = takeGateLock({ file: path.join(root, 'link', 'gate.lock') });
  assert.equal(viaLink.ok, false);
  assert.match(viaLink.reason, new RegExp(path.join(root, 'link').replace(/[.*+?^${}()|[\]\\]/g, '\\$&')), 'it names the folder');
  assert.equal(existsSync(path.join(real, 'gate.lock')), false, 'no lock was made through the link');
  const other = takeGateLock({ file: path.join(real, 'gate.lock'), uid: process.getuid() + 1 });
  assert.equal(other.ok, false, 'a folder of another uid is refused');
  assert.equal(takeGateLock({ file: path.join(real, 'gate.lock') }).ok, true, 'control: the same folder as this user\'s own is fine');
});

test('Releasing_a_lock_that_still_exists_but_cannot_be_read_says_so_with_the_manual_step', t => {
  const file = path.join(ownedFolder(t, 'glr'), 'gate.lock');
  const said = [];
  const mine = takeGateLock({ file, say: line => said.push(line) });
  assert.equal(mine.ok, true);
  writeFileSync(file, `${'x'.repeat(600)}\n`);
  mine.value.release();
  assert.equal(existsSync(file), true, 'it is not ours to remove');
  assert.equal(said.length, 1, 'but it is said once');
  assert.match(said[0], /cannot be read/);
  assert.match(said[0], /remove .* by hand if no gate run is going/);
  const taker = takeGateLock({ file: path.join(path.dirname(file), 'other.lock'), say: line => said.push(line) });
  writeFileSync(path.join(path.dirname(file), 'other.lock'), recordOf({ host: 'another@linux' }));
  taker.value.release();
  assert.equal(said.length, 1, 'a readable lock of another taker is not news');
});

test('A_failed_write_removes_the_lock_only_while_the_path_still_names_the_file_this_taker_created', t => {
  const file = path.join(ownedFolder(t, 'glw'), 'gate.lock');
  const io = { writeSync: () => { rmSync(file); writeFileSync(file, 'another taker\'s lock\n'); throw Object.assign(new Error('disk full'), { code: 'ENOSPC' }); } };
  const failed = takeGateLock({ file, io });
  assert.equal(failed.ok, false);
  assert.match(failed.reason, /disk full/);
  assert.equal(readFileSync(file, 'utf8'), 'another taker\'s lock\n', 'the file that took the path since is not removed');
  rmSync(file);
  const plain = takeGateLock({ file, io: { writeSync: () => { throw Object.assign(new Error('disk full'), { code: 'ENOSPC' }); } } });
  assert.equal(plain.ok, false);
  assert.equal(existsSync(file), false, 'control: the half-made lock of this taker is removed');
});

test('A_stale_lock_is_reclaimed_through_a_unique_sibling_that_is_compared_and_removed', t => {
  const dir = ownedFolder(t, 'glc');
  const file = path.join(dir, 'gate.lock');
  assert.equal(takeGateLock({ file }).ok, true);
  const stale = recordOf({ pid: exitedPid() });
  writeFileSync(file, stale);
  const taken = takeGateLock({ file, say: () => {} });
  assert.equal(taken.ok, true, taken.reason);
  assert.deepEqual(readdirSync(dir), ['gate.lock'], 'no sibling is left behind');
});

// ---- C3: the wrapper takes its paths as arguments ----------------------------------------------------------------------------------

test('The_box_command_wrapper_takes_its_output_paths_as_arguments_and_never_in_its_script_text', t => {
  const dir = ownedFolder(t, 'wrp');
  const out = path.join(dir, 'o"; touch PWNED; echo "');
  const code = path.join(dir, 'c$(touch PWNED2)');
  const argv = wrappedArgv({ command: ['echo', 'hello world'], out, code });
  assert.equal(argv[0], 'sh');
  assert.equal(argv[1], '-c');
  assert.equal(argv[2].includes(out) || argv[2].includes(code) || argv[2].includes(dir), false, 'the script text holds no path');
  const ran = spawnSync('sh', argv.slice(1), { cwd: dir, encoding: 'utf8', timeout: 20_000 });
  assert.equal(ran.status, 0, ran.stderr);
  assert.equal(readFileSync(out, 'utf8'), 'hello world\n');
  assert.equal(readFileSync(code, 'utf8'), '0\n');
  assert.deepEqual(readdirSync(dir).filter(n => n.startsWith('PWNED')), [], 'nothing in a path was run as a command');
});

// ---- C6, L9, C7: the seed projects and the recipe -----------------------------------------------------------------------------------

test('Every_seed_project_turns_warnings_into_errors_and_every_npm_install_step_runs_no_scripts', () => {
  const dir = path.join(SPIKE, 'inputs', 'cache-projects', 'csharp');
  const projects = readdirSync(dir, { recursive: true }).filter(f => f.endsWith('.csproj'));
  assert.equal(projects.length, 3, 'the scan finds the three seed projects');
  for (const proj of projects) assert.match(readFileSync(path.join(dir, proj), 'utf8'), /<TreatWarningsAsErrors>true<\/TreatWarningsAsErrors>/, proj);
  const installs = realRecipe().stacks.node.steps.filter(s => s.argv[0] === 'npm' && s.argv[1] === 'install' || s.argv[0] === 'npm' && s.argv.includes('install'));
  assert.equal(installs.length, 2, 'the scan finds the two npm install steps');
  for (const step of installs) assert.ok(step.argv.includes('--ignore-scripts'), `${step.argv.join(' ')}: the cache needs the tarballs only`);
});

test('Every_offered_package_names_its_licence_and_its_maintenance_state_and_a_malformed_global_json_is_a_problem_not_a_throw', t => {
  const changed = edit => { const copy = structuredClone(realRecipe()); edit(copy); return copy; };
  const refusal = r => { const x = loadRecipeLang(r, stacks); assert.equal(x.ok, false); return x.reason; };
  assert.equal(loadRecipeLang(realRecipe(), stacks).ok, true);
  assert.match(refusal(changed(r => { delete r.stacks.node.packages[0].licence; })), /stacks\.node\.packages\[0\]\.licence/);
  assert.match(refusal(changed(r => { delete r.stacks.csharp.packages[1].maintenance; })), /stacks\.csharp\.packages\[1\]\.maintenance/);
  assert.match(refusal(changed(r => { r.stacks.node.packages[2].maintenance.releasedOn = 'last week'; })), /stacks\.node\.packages\[2\]\.maintenance\.releasedOn/);
  assert.match(refusal(changed(r => { r.stacks.node.packages[3].maintenance = { latestVersion: '', releasedOn: '2026-01-01', readOn: '2026-11-04' }; })), /stacks\.node\.packages\[3\]\.maintenance\.latestVersion/);
  const spike = ownedFolder(t, 'gjs');
  mkdirSync(path.join(spike, 'proj'));
  writeFileSync(path.join(spike, 'proj', 'global.json'), '{ not json');
  const recipe = changed(r => { r.stacks.csharp.project = 'proj'; });
  const bad = loadRecipeLang(recipe, stacks, { spikeDir: spike });
  assert.equal(bad.ok, false, 'a malformed global.json is a refusal');
  assert.match(bad.reason, /proj\/global\.json is not JSON/);
});

// ---- the command: C9, L1, L6 -------------------------------------------------------------------------------------------------------

const MAKE = path.join(SPIKE, 'make-snapshots.mjs');
const runMake = args => runBounded(process.execPath, [MAKE, ...args], { timeoutMs: 120_000 });

test('Supersede_and_prune_take_the_same_gate_lock_as_a_build_and_release_it', async t => {
  const root = ownedFolder(t, 'lck');
  const lock = path.join(root, 'gate.lock');
  const rec = path.join(root, 'rec.json');
  writeFileSync(rec, JSON.stringify({ csharp: recordFor('a'.repeat(64)), superseded: [] }));
  mkdirSync(path.join(root, 'caches'));
  const held = takeGateLock({ file: lock });
  assert.equal(held.ok, true);
  for (const args of [['--supersede', 'csharp', '--reason', 'x'], ['--prune-superseded']]) {
    const r = await runMake([...args, '--record', rec, '--caches', path.join(root, 'caches'), '--gate-lock', lock]);
    assert.equal(r.value.code, 3, `${args[0]}: ${r.value.stderr}`);
    assert.match(r.value.stderr, /gate lock held by/, `${args[0]} refuses while a build holds the lock`);
  }
  assert.equal(JSON.parse(readFileSync(rec, 'utf8')).csharp.treeSha256, 'a'.repeat(64), 'the record is untouched');
  held.value.release();
  const unknownStack = await runMake(['--supersede', 'node', '--reason', 'x', '--record', rec, '--gate-lock', lock]);
  assert.equal(unknownStack.value.code, 3, unknownStack.value.stderr);
  assert.match(unknownStack.value.stderr, /no record/);
  assert.equal(existsSync(lock), false, 'the lock is released after a refused supersede');
  const noCaches = await runMake(['--prune-superseded', '--record', rec, '--caches', path.join(root, 'none'), '--gate-lock', lock]);
  assert.equal(noCaches.value.code, 3, noCaches.value.stderr);
  assert.equal(existsSync(lock), false, 'and after a refused prune');
});

test('A_recipe_or_stacks_file_that_is_missing_or_not_JSON_is_exit_2_with_one_line_and_no_stack_trace', async t => {
  const root = ownedFolder(t, 'inp');
  const junk = path.join(root, 'junk.json');
  writeFileSync(junk, '{ not json');
  const base = ['--caches', path.join(root, 'caches'), '--record', path.join(root, 'rec.json'), '--gate-lock', path.join(root, 'gate.lock'), '--work', path.join(root, 'work')];
  for (const flags of [['--recipe', path.join(root, 'absent.json')], ['--recipe', junk], ['--stacks', path.join(root, 'absent.json')], ['--stacks', junk]]) {
    const r = await runMake([...base, ...flags]);
    assert.equal(r.value.code, 2, `${flags.join(' ')}: ${r.value.stderr}`);
    const lines = r.value.stderr.trim().split('\n');
    assert.equal(lines.length, 1, `one line, got: ${r.value.stderr}`);
    assert.match(lines[0], new RegExp(flags[0]), 'it names the flag');
    assert.doesNotMatch(r.value.stderr, /\n\s+at /, 'no stack trace');
  }
});

test('The_command_lowers_its_own_priority_before_anything_else_as_the_test_runner_does', () => {
  const before = source => { const i = source.indexOf("await import('./lib/lower-priority.mjs')"); return { i, main: source.indexOf('process.exitCode = await main') }; };
  const runner = before(readFileSync(path.join(SPIKE, 'run-tests.mjs'), 'utf8'));
  assert.ok(runner.i > 0, 'the scan still finds the known instance in run-tests.mjs');
  const command = before(readFileSync(MAKE, 'utf8'));
  assert.ok(command.i > 0 && command.i < command.main, 'make-snapshots.mjs lowers itself before it runs');
});

// ======== fix batch 2 =====================================================================================================================

// ---- N1: the remote of the tag reader ----------------------------------------------------------------------------------------------

test('A_GitHub_SSH_remote_is_read_over_https_and_any_other_url_is_used_as_it_is', () => {
  assert.equal(remoteUrlFor('git@github.com:oleksandrdubyna88/dew_flow_benchmark.git'), 'https://github.com/oleksandrdubyna88/dew_flow_benchmark.git');
  assert.equal(remoteUrlFor('git@github.com:owner/repo'), 'https://github.com/owner/repo.git', 'a missing .git is added');
  assert.equal(remoteUrlFor('ssh://git@github.com/owner/repo.git'), 'https://github.com/owner/repo.git');
  assert.equal(remoteUrlFor('ssh://git@github.com:22/owner/repo.git'), 'https://github.com/owner/repo.git', 'an explicit port goes too');
  for (const same of ['https://github.com/owner/repo.git', '/srv/git/origin.git', 'git@example.com:owner/repo.git', 'ssh://git@gitlab.com/owner/repo.git']) assert.equal(remoteUrlFor(same), same, `${same} is used as it is`);
});

test('The_tag_reader_takes_the_remote_from_the_flag_before_origin_and_still_fails_closed', t => {
  const root = ownedFolder(t, 'trm');
  const [repo, bare] = [path.join(root, 'repo'), path.join(root, 'bare.git')];
  mkdirSync(repo); mkdirSync(bare);
  assert.equal(git(bare, 'init', '--bare', '-q').status, 0);
  assert.equal(git(repo, 'init', '-q').status, 0);
  assert.equal(git(repo, 'commit', '--allow-empty', '-q', '-m', 'x').status, 0);
  assert.equal(git(repo, 'remote', 'add', 'origin', 'git@nowhere.invalid:owner/repo.git').status, 0);
  assert.equal(git(repo, 'tag', TAG).status, 0);
  assert.equal(git(repo, 'push', '-q', bare, TAG).status, 0);
  assert.equal(git(repo, 'tag', '-d', TAG).status, 0);
  assert.equal(tagsOf(repo).ok, false, 'origin as configured cannot be reached: a refusal');
  assert.deepEqual(tagsOf(repo, { remote: bare }).value, [TAG], 'the flag names a remote that can be read: the remote-only tag is seen');
  assert.equal(tagsOf(repo, { remote: path.join(root, 'nowhere.git') }).ok, false, 'an unreachable flag remote is a refusal too');
});

// ---- Low 1: the discard also runs on an exception; an unreadable record never keeps a folder; an orphan folder is swept -----------

test('A_made_folder_is_kept_and_named_when_the_registration_throws_on_a_record_that_cannot_be_read', t => {
  // fix batch 3 (F2). A record that cannot be read (or is absent, or has the wrong shape) authorises NOTHING, neither keeping nor removing, and a C# rebuild can equal a
  // registered folder (C# is reproducible), so the made folder is KEPT and named. The cases that remove are in the next test.
  const root = ownedFolder(t, 'thr');
  const caches = path.join(root, 'caches');
  mkdirSync(caches);
  const m = madeSnapshot(caches, [['demo', '1.0.0', 'QQ==']]);
  renameSync(m.stage, m.dir);
  makeReadOnly(m.dir);
  const file = path.join(root, 'rec.json');
  writeFileSync(file, '{ not json');
  const made = { dir: m.dir, treeSha256: m.treeSha256, parts: { nuget: 'p' }, sizeBytes: 1, packages: [{ ecosystem: 'nuget', name: 'demo', version: '1.0.0' }], resolved: {}, steps: [] };
  const said = [];
  assert.throws(() => registerBuilt({ made, stack: 'csharp', recipeSha256: 'r', projectSha256: null, file, madeOn: '2026-11-04', log: line => said.push(line) }), /JSON/, 'the registration throws on a record that is not JSON');
  assert.equal(existsSync(m.dir), true, 'and the folder is KEPT: an unreadable record authorises no removal');
  assert.match(said.join('\n'), new RegExp(`kept.*${m.dir.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}`), 'and named on the log');
  makeWritable(root);
});

test('A_made_folder_is_removed_when_the_registration_throws_or_fails_with_a_record_that_reads_correctly_and_kept_when_the_record_is_absent_or_has_the_wrong_shape', t => {
  const root = ownedFolder(t, 'thk');
  const mk = name => { const caches = path.join(root, name); mkdirSync(caches); const m = madeSnapshot(caches, [['demo', '1.0.0', 'QQ==']]); renameSync(m.stage, m.dir); makeReadOnly(m.dir); return { ...m, made: { dir: m.dir, treeSha256: m.treeSha256, parts: { nuget: 'p' }, sizeBytes: 1, packages: [{ ecosystem: 'nuget', name: 'demo', version: '1.0.0' }], resolved: {}, steps: [] } }; };
  const run = (m, file, made = m.made) => registerBuilt({ made, stack: 'csharp', recipeSha256: 'r', projectSha256: null, file, madeOn: '2026-11-04', log: () => {} });
  // a record that reads correctly and does not hold the pair: an exception inside the steps removes the folder
  const readable = path.join(root, 'ok.json');
  writeFileSync(readable, '{}\n');
  const thrown = mk('c1');
  assert.throws(() => run(thrown, readable, { ...thrown.made, packages: null }), TypeError);
  assert.equal(existsSync(thrown.dir), false, 'an exception inside the steps removes the folder when the record reads correctly');
  // the record is absent, or `null`, or `[]`: the folder is kept (the failure is a refusal here: the manifest differs from the maker's list)
  for (const [label, content] of [['absent', null], ['null', 'null'], ['an array', '[]']]) {
    const m = mk(`k-${label.replace(' ', '')}`);
    const file = path.join(root, `${label.replace(' ', '')}.json`);
    if (content !== null) writeFileSync(file, content);
    const failed = run(m, file, { ...m.made, packages: [] });
    assert.equal(failed.ok, false, label);
    assert.equal(existsSync(m.dir), true, `${label}: the folder is kept`);
  }
  makeWritable(root);
});

test('An_unregistered_snapshot_folder_is_never_removed_and_is_named_when_the_claim_of_its_build_is_gone_and_the_record_does_not_hold_it', t => {
  // fix batch 3 (F1): the sweep never removes a `<stack>-<hash>` folder. The record path is per checkout and the caches folder is global, so no record the run happens to read can license a deletion of
  // an irreplaceable snapshot. The sweep LISTS each unregistered folder whose build's claim is gone, with the manual step, and removes the orphan claim file only.
  const caches = ownedFolder(t, 'osf');
  const gone = exitedPid();
  const folder = (stack, text) => { const stage = path.join(caches, `.w-${stack}-${text}`); mkdirSync(stage); writeFileSync(path.join(stage, 'f'), text); const h = treeHash(stage); const dir = path.join(caches, `${stack}-${h}`); renameSync(stage, dir); makeReadOnly(dir); return { dir, h }; };
  const stranded = folder('csharp', 'stranded');
  const registered = folder('csharp', 'registered');
  const superseded = folder('csharp', 'superseded');
  const mine = folder('node', 'claimed by a live owner');
  writeClaim(path.join(caches, `.building-csharp-${gone}`), { pid: gone });
  writeClaim(path.join(caches, `.building-node-${process.pid}`), {});
  const records = { csharp: recordFor(registered.h), superseded: [{ stack: 'csharp', supersededOn: '2026-11-05', reason: 'r', record: recordFor(superseded.h) }] };
  const lines = [];
  const swept = sweepStaging({ caches, log: line => lines.push(line), records });
  for (const kept of [stranded.dir, registered.dir, superseded.dir, mine.dir]) assert.equal(existsSync(kept), true, `${kept} stays`);
  assert.equal(existsSync(path.join(caches, `.building-csharp-${gone}.owner`)), false, 'the dead claim file goes');
  const named = lines.filter(l => l.includes(stranded.dir));
  assert.equal(named.length, 1, 'the unregistered folder is named once');
  assert.match(named[0], /unregistered.*NOT removed.*not registered anywhere.*chmod -R u\+w .* && rm -rf /s, 'with the manual step');
  assert.equal(lines.some(l => l.includes(registered.dir) || l.includes(superseded.dir) || l.includes(mine.dir)), false, 'the registered, superseded and live ones are not named');
  assert.equal(swept.removed.some(r => r.dir === stranded.dir), false, 'and it is not in the removed list');
  // the three records that were dangerous: ABSENT, OLDER (another hash), and the wrong shape. The folder stays in each, and the sweep may name it
  for (const [label, wrong] of [['absent', {}], ['older', { csharp: recordFor('f'.repeat(64)) }], ['an array', []], ['undefined', undefined]]) {
    writeClaim(path.join(caches, `.building-csharp-${gone}`), { pid: gone });
    sweepStaging({ caches, log: () => {}, records: wrong });
    for (const dir of [stranded.dir, registered.dir]) assert.equal(existsSync(dir), true, `${label} record: ${dir} stays`);
  }
  makeWritable(caches);
});

// ---- Low 2: the put-back of a stale lock is a value, not a throw -------------------------------------------------------------------

test('A_successor_that_takes_the_lock_between_the_rename_and_the_put_back_is_a_refusal_and_not_a_throw', t => {
  const dir = ownedFolder(t, 'gls');
  const file = path.join(dir, 'gate.lock');
  assert.equal(takeGateLock({ file }).ok, true);
  writeFileSync(file, recordOf({ pid: exitedPid() }));
  const successor = recordOf({ host: 'another@linux' });
  // the hook runs right after the stale file was renamed away: a successor takes the path, and the text of the renamed file is NOT the one that was judged
  const io = { afterRename: (_, sibling) => { writeFileSync(file, successor); writeFileSync(sibling, 'a text that is not the judged one\n'); } };
  const taken = takeGateLock({ file, io, say: () => {} });
  assert.equal(taken.ok, false);
  assert.equal(readFileSync(file, 'utf8'), successor, 'the successor\'s lock is untouched');
  assert.deepEqual(readdirSync(dir), ['gate.lock'], 'and the sibling is removed');
});

test('A_release_that_meets_a_lock_already_gone_is_silent_and_one_that_fails_for_another_reason_says_so', t => {
  const file = path.join(ownedFolder(t, 'glg'), 'gate.lock');
  const said = [];
  const enoent = () => { throw Object.assign(new Error('gone'), { code: 'ENOENT' }); };
  const mine = takeGateLock({ file, say: line => said.push(line), io: { rm: enoent } });
  assert.equal(mine.ok, true);
  assert.doesNotThrow(() => mine.value.release());
  assert.deepEqual(said, [], 'ENOENT on the unlink is not news');
  const other = takeGateLock({ file: path.join(path.dirname(file), 'b.lock'), say: line => said.push(line), io: { rm: () => { throw Object.assign(new Error('denied'), { code: 'EACCES' }); } } });
  other.value.release();
  assert.equal(said.length, 1, 'any other failure is said');
  assert.match(said[0], /denied/);
});

// ---- coai round 2: 1, 5, 7, 8, 11, 12 --------------------------------------------------------------------------------------------

test('A_box_that_ran_but_left_no_exit_code_or_output_is_a_typed_failure_naming_the_files', t => {
  const dir = ownedFolder(t, 'rdw');
  const [out, code] = [path.join(dir, 'o-abc'), path.join(dir, 'c-abc')];
  const missing = readWrapped({ out, code });
  assert.equal(missing.ok, false);
  assert.match(missing.reason, /o-abc/);
  assert.match(missing.reason, /c-abc/);
  writeFileSync(out, 'output');
  writeFileSync(code, 'not a number\n');
  assert.equal(readWrapped({ out, code }).ok, false, 'an exit code that is not a number is a failure too');
  writeFileSync(code, '3\n');
  assert.deepEqual(readWrapped({ out, code }).value, { code: 3, output: 'output' });
});

test('A_staging_folder_that_vanishes_between_the_listing_and_the_look_is_skipped_not_a_crash', t => {
  const caches = ownedFolder(t, 'vns');
  const [a, b] = [path.join(caches, '.building-node-9201'), path.join(caches, '.building-node-9202')];
  mkdirSync(a); mkdirSync(b);
  writeClaim(a, {}); writeClaim(b, {});
  const lines = [];
  const lstat = p => { if (p === a) throw Object.assign(new Error('gone'), { code: 'ENOENT' }); return lstatSync(p); };
  assert.doesNotThrow(() => sweepStaging({ caches, log: line => lines.push(line), lstat }));
  assert.equal(lines.some(l => l.includes('9202')), true, 'the next folder is still looked at');
});

test('Prune_says_which_snapshot_it_is_re_hashing_classifies_every_candidate_before_it_removes_and_reports_a_removal_that_failed', t => {
  const root = ownedFolder(t, 'prc');
  const caches = path.join(root, 'caches');
  mkdirSync(caches);
  const [h1, h2] = [snapshotAt(caches, 'node', { 'npm/a': 'one' }), snapshotAt(caches, 'csharp', { 'nuget/a': 'two' })];
  const reg = snapshotAt(caches, 'node', { 'npm/b': 'registered' });
  const records = { node: recordFor(reg), superseded: [{ stack: 'node', supersededOn: '2026-11-05', reason: 'a', record: recordFor(h1) }, { stack: 'csharp', supersededOn: '2026-11-05', reason: 'b', record: recordFor(h2) }] };
  const events = [];
  const remove = dir => { events.push(`remove ${path.basename(dir)}`); if (dir.includes('csharp-')) throw new Error('device busy'); makeWritable(dir); rmSync(dir, { recursive: true, force: true }); };
  const pruned = pruneSuperseded({ caches, tagsOf: taggedA, recordAtTag: () => ok(records), log: line => events.push(line), remove });
  assert.equal(pruned.ok, true, pruned.reason);
  const checks = events.filter(e => e.startsWith('checking snapshot'));
  assert.deepEqual(checks.map(e => e.split(' ')[2]), [`csharp-${h2}`, `node-${h1}`], 'one line before each re-hash, and none for the registered folder');
  assert.ok(events.lastIndexOf(checks.at(-1)) < events.findIndex(e => e.startsWith('remove ')), 'every candidate is classified and re-hashed before the first removal');
  assert.deepEqual(pruned.value.removed.map(r => r.name), [`node-${h1}`]);
  assert.equal(pruned.value.failed.length, 1, 'a removal that failed is reported');
  assert.match(pruned.value.failed[0].why, /device busy/);
  assert.equal(pruned.value.failed[0].name, `csharp-${h2}`);
  makeWritable(root);
});

test('A_placeholder_that_is_not_a_key_lists_the_legal_keys', () => {
  const bad = structuredClone(realRecipe());
  bad.stacks.csharp.steps[1].argv.push('-p:X={GHOST}');
  const refused = loadRecipeLang(bad, stacks);
  assert.equal(refused.ok, false);
  assert.match(refused.reason, /legal keys: HOSTING, JWT_BEARER, XUNIT, XUNIT_VS, TEST_SDK/);
});

test('The_npm_index_is_read_once_for_every_licence_of_a_snapshot', t => {
  const dir = ownedFolder(t, 'lic');
  let reads = 0;
  const readIndex = () => { reads += 1; return []; };
  const found = npmLicences(dir, [{ name: 'fastify', version: '5.12.7' }, { name: 'pino', version: '10.4.0' }, { name: 'eslint', version: '10.14.0' }], { readIndex });
  assert.equal(reads, 1, 'one read of the index for three packages');
  assert.deepEqual([...found.keys()].sort(), ['eslint@10.14.0', 'fastify@5.12.7', 'pino@10.4.0']);
  assert.deepEqual([...found.values()], [null, null, null], 'a package that is not in the index has no licence, never an invented one');
});

test('The_command_accepts_a_tags_remote_flag', async t => {
  const root = ownedFolder(t, 'tgr');
  const r = await runMake(['--prune-superseded', '--tags-remote', path.join(root, 'bare.git'), '--caches', path.join(root, 'none'), '--record', path.join(root, 'rec.json'), '--gate-lock', path.join(root, 'gate.lock')]);
  assert.doesNotMatch(r.value.stderr, /Unknown option/, 'the flag is known');
  assert.equal(r.value.code, 3, r.value.stderr);
  assert.match(r.value.stderr, /no caches folder/);
});

// ======== fix batch 3 =====================================================================================================================

test('A_record_with_bad_content_is_one_line_and_exit_2_before_anything_is_swept_or_built', async t => {
  const root = ownedFolder(t, 'brc');
  const rec = path.join(root, 'rec.json');
  const base = ['--caches', path.join(root, 'caches'), '--record', rec, '--gate-lock', path.join(root, 'gate.lock'), '--work', path.join(root, 'work')];
  for (const [label, content] of [['null', 'null'], ['not JSON', '{ not json'], ['an array', '[]'], ['a number', '3']]) {
    writeFileSync(rec, content);
    const r = await runMake(base);
    assert.equal(r.value.code, 2, `${label}: ${r.value.stderr}`);
    const lines = r.value.stderr.trim().split('\n');
    assert.equal(lines.length, 1, `${label}: one line, got: ${r.value.stderr}`);
    assert.match(lines[0], /--record/, 'it names the flag');
    assert.doesNotMatch(r.value.stderr, /\n\s+at /, 'no stack trace');
    assert.equal(existsSync(path.join(root, 'caches')), false, `${label}: nothing was made or swept`);
  }
});

test('A_remote_url_with_credentials_is_redacted_in_every_message', t => {
  assert.equal(redactUserInfo('https://user:token@github.com/o/r.git'), 'https://***@github.com/o/r.git');
  assert.equal(redactUserInfo('could not read https://tok@host:8443/x.git (git exited 128: fatal: unable to access \'https://u:p@host:8443/x.git/\')'), 'could not read https://***@host:8443/x.git (git exited 128: fatal: unable to access \'https://***@host:8443/x.git/\')');
  assert.equal(redactUserInfo('git@github.com:o/r.git'), 'git@github.com:o/r.git', 'an scp-style address has no secret and is not touched');
  const repo = ownedFolder(t, 'red');
  assert.equal(git(repo, 'init', '-q').status, 0);
  const failed = tagsOf(repo, { remote: 'https://user:s3cretToken@127.0.0.1:1/o/r.git' });
  assert.equal(failed.ok, false);
  assert.doesNotMatch(failed.reason, /s3cretToken|user:/, 'the token is not in the refusal');
  assert.match(failed.reason, /https:\/\/\*\*\*@127\.0\.0\.1:1/);
});
