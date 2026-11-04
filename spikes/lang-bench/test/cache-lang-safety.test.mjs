// E1.S4, fix batch 1 (the code-round findings C0-C11 and the Opus review's M and L items), fast cases: no network. Each case names the mechanism and builds the unsafe shape beside the safe one.
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { cpSync, existsSync, mkdirSync, readFileSync, readdirSync, renameSync, rmSync, symlinkSync, writeFileSync } from 'node:fs';
import { spawnSync } from 'node:child_process';
import path from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { makeReadOnly, makeWritable, treeHash } from '../../minigit-bench/lib/cache-snapshot.mjs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { runBounded } from '../../minigit-bench/lib/run.mjs';
import { thisOwner } from '../../../.agents/conventions/tools/lib/owned.mjs';
import { wrappedArgv } from '../lib/cache-offline.mjs';
import { loadRecipeLang } from '../lib/cache-recipe-lang.mjs';
import { buildRecord, manifestSha256Of, pruneSuperseded, registerBuilt, sortPackages, supersede, sweepStaging, tagsOf } from '../lib/cache-store.mjs';
import { takeGateLock } from '../lib/gate-lock.mjs';
import { loadStacksLang } from '../lib/toolchain-pins.mjs';
import { assertBuiltForReal } from './support/cache-box.mjs';
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
  // a manifest failure after a successful snapshot (a package with no hash): no folder is left
  const bad = mk('c1', [['demo', '1.0.0', null]]);
  place(bad);
  const failed = registerBuilt({ made: bad.made, stack: 'csharp', recipeSha256: 'r', projectSha256: null, file, madeOn: '2026-11-04' });
  assert.equal(failed.ok, false);
  assert.match(failed.reason, /demo@1\.0\.0.*no integrity/);
  assert.equal(existsSync(bad.dir), false, 'the folder of a failed build is removed, although it is read-only');
  assert.equal(existsSync(file), false, 'and nothing is registered');
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
  const pruned = pruneSuperseded({ file, caches, tagsOf: taggedA, recordAtTag: () => ok(JSON.parse(readFileSync(file, 'utf8'))) });
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
  const pruned = pruneSuperseded({ file, caches, tagsOf: taggedA, recordAtTag: atTag });
  assert.equal(pruned.ok, true, pruned.reason);
  assert.deepEqual(seen, ['prereg/lang-v2-A-2026-12-01']);
  assert.deepEqual(pruned.value.removed, [], 'the tagged record decides: registered, kept');
  assert.equal(existsSync(path.join(caches, `node-${hash}`)), true);
  const unreadable = pruneSuperseded({ file, caches, tagsOf: taggedA, recordAtTag: () => fail('git show failed') });
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

// ---- M1: the development seam never reaches a completion run ----------------------------------------------------------------------

test('A_completion_run_refuses_the_reuse_seam_of_the_real_build', () => {
  const saved = { require: process.env.BENCH_REQUIRE_SANDBOX, reuse: process.env.LANG_V2_REUSE_REAL };
  const restore = () => { for (const [k, v] of [['BENCH_REQUIRE_SANDBOX', saved.require], ['LANG_V2_REUSE_REAL', saved.reuse]]) { if (v === undefined) delete process.env[k]; else process.env[k] = v; } };
  try {
    process.env.BENCH_REQUIRE_SANDBOX = '1';
    process.env.LANG_V2_REUSE_REAL = '/somewhere';
    assert.throws(() => assertBuiltForReal(), /dev seam.*completion run builds for real/);
    delete process.env.LANG_V2_REUSE_REAL;
    assert.doesNotThrow(() => assertBuiltForReal());
    process.env.LANG_V2_REUSE_REAL = '/somewhere';
    delete process.env.BENCH_REQUIRE_SANDBOX;
    assert.doesNotThrow(() => assertBuiltForReal(), 'without the completion flag the seam is only a development loop');
  } finally {
    restore();
  }
});
