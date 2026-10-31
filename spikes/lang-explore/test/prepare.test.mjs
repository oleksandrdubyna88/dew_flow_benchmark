// `explore.mjs prepare` (plan D-X4): the one networked step, tested with its edges (the registry, the installer, the snapshot maker, the spin probe) replaced by stand-ins, and every file it writes
// pointed at a copy. Nothing here reaches the network, the operator's npm cache or the real token.
import '../../minigit-bench/test/fake-home.mjs';
import assert from 'node:assert/strict';
import { chmodSync, cpSync, existsSync, mkdirSync, readFileSync, statSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { treeHash } from '../../minigit-bench/lib/cache-snapshot.mjs';
import { tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { loadExplore } from '../lib/inputs.mjs';
import { FILES, calibrateSpin, csharpSnapshot, installToolchain, installedIntegrity, nodeSnapshot, prepare, recordIntegrity, registryIntegrity } from '../lib/prepare.mjs';

const INTEGRITY = `sha512-${'A'.repeat(86)}==`;
const OTHER = `sha512-${'B'.repeat(86)}==`;

/** Copies of the files `prepare` writes, in a temp directory. */
function files() {
  const dir = tmpDir('prep');
  const copy = name => { const to = path.join(dir, name); cpSync(path.join(path.dirname(FILES.explore), name), to); return to; };
  const explore = copy('explore.json');
  writeFileSync(explore, readFileSync(explore, 'utf8').replace(/"integrity": "sha512-[^"]*"/, '"integrity": null')); // the real file has it filled since `prepare` ran: a test starts from the state before
  return { dir, explore, recipe: copy('cache-recipe.json'), snapshots: path.join(dir, 'cache-snapshots.json'), v1Snapshots: FILES.v1Snapshots };
}

test('The_registry_integrity_is_read_with_a_fresh_npm_cache_so_the_operators_copy_is_never_used', async () => {
  const calls = [];
  const run = async (exe, argv, opts) => { calls.push({ exe, argv, env: opts.env }); return { ok: true, value: { code: 0, stdout: `${INTEGRITY}\n`, stderr: '' } }; };
  const cache = tmpDir('npm-cache');
  const r = await registryIntegrity({ version: '6.0.3', cacheDir: cache, run });
  assert.deepEqual([r.ok, r.value], [true, INTEGRITY]);
  assert.deepEqual([calls[0].exe, calls[0].argv], ['npm', ['view', 'typescript@6.0.3', 'dist.integrity']]);
  assert.equal(calls[0].env.npm_config_cache, cache, 'a directory of the run, not ~/.npm');
  for (const bad of ['', 'sha1-xyz', 'not an integrity']) {
    const refused = await registryIntegrity({ version: '6.0.3', cacheDir: cache, run: async () => ({ ok: true, value: { code: 0, stdout: bad, stderr: '' } }) });
    assert.equal(refused.ok, false, bad);
  }
  assert.equal((await registryIntegrity({ version: '6.0.3', cacheDir: cache, run: async () => ({ ok: true, value: { code: 1, stdout: INTEGRITY, stderr: 'E404' } }) })).ok, false, 'a failing npm is a failure even when it printed something');
});

test('The_recorded_integrity_is_filled_once_confirmed_and_never_overwritten', () => {
  const f = files();
  assert.equal(loadExplore(f.explore).value.typescript.integrity, null);
  assert.deepEqual(recordIntegrity({ file: f.explore, integrity: INTEGRITY }), { ok: true, value: { changed: true } });
  assert.equal(loadExplore(f.explore).value.typescript.integrity, INTEGRITY);
  assert.deepEqual(recordIntegrity({ file: f.explore, integrity: INTEGRITY }), { ok: true, value: { changed: false } });
  const before = readFileSync(f.explore, 'utf8');
  const moved = recordIntegrity({ file: f.explore, integrity: OTHER });
  assert.equal(moved.ok, false);
  assert.match(moved.reason, /never overwritten/);
  assert.equal(readFileSync(f.explore, 'utf8'), before, 'a refused amendment leaves the file as it was');
});

/** A toolchain dir whose package-lock carries `integrity`, in a home of the test. */
function toolchain(explore, home, integrity) {
  const dir = path.join(home, ...explore.typescript.toolchainDirParts);
  mkdirSync(dir, { recursive: true });
  writeFileSync(path.join(dir, 'package-lock.json'), JSON.stringify({ packages: { 'node_modules/typescript': { integrity } } }));
  return dir;
}

test('The_install_runs_with_a_fresh_npm_cache_a_fake_token_home_and_install_true_then_compares_the_integrity', async () => {
  const f = files();
  recordIntegrity({ file: f.explore, integrity: INTEGRITY });
  const explore = loadExplore(f.explore).value;
  const home = tmpDir('home');
  toolchain(explore, home, INTEGRITY);
  const seen = [];
  const preflightFn = async a => { seen.push({ ...a, cache: process.env.npm_config_cache, tokenFile: existsSync(path.join(a.home, '.config', 'minigit-bench', 'claude-oauth-token')) }); return { ok: true, value: { facts: { typescript: '6.0.3' } } }; };
  const realCache = process.env.npm_config_cache;
  const r = await installToolchain({ explore, home, preflightFn });
  assert.equal(r.ok, true, r.reason);
  assert.equal(seen[0].install, true);
  assert.notEqual(seen[0].home, home, 'the preflight is handed a throw-away home with a fake token file, not the operator\'s');
  assert.equal(seen[0].tokenFile, true);
  assert.ok(seen[0].cache && seen[0].cache !== realCache, 'npm gets a fresh cache for the install');
  assert.equal(process.env.npm_config_cache, realCache, 'and the environment is put back');
  assert.equal(seen[0].tsDir, path.join(home, ...explore.typescript.toolchainDirParts));
  const mismatch = await installToolchain({ explore: { ...explore, typescript: { ...explore.typescript, integrity: OTHER } }, home, preflightFn });
  assert.equal(mismatch.ok, false);
  assert.match(mismatch.reason, /integrity/);
  assert.equal(installedIntegrity(path.join(home, 'nowhere')), null);
});

test('A_failing_preflight_is_the_result_with_every_problem_named', async () => {
  const f = files();
  const explore = loadExplore(f.explore).value;
  const r = await installToolchain({ explore, home: tmpDir('home'), preflightFn: async () => ({ ok: false, reason: 'the preflight found 2 problem(s)', problems: ['tsc is 7.0.2, the pin is 6.0.3', 'bwrap is missing'] }) });
  assert.equal(r.ok, false);
  assert.match(r.reason, /tsc is 7\.0\.2.*bwrap is missing/);
});

/** A stand-in for makeSnapshot: a read-only-looking snapshot dir named like the real one. */
function fakeMake(out) {
  return async ({ stack, out: dest }) => {
    const dir = path.join(dest, 'cache-node-made');
    mkdirSync(path.join(dir, 'npm'), { recursive: true });
    writeFileSync(path.join(dir, 'npm', 'x'), 'x');
    return { ok: true, value: { dir, treeSha256: treeHash(dir), parts: { npm: treeHash(path.join(dir, 'npm')) }, sizeBytes: 1, packages: [{ ecosystem: 'npm', name: 'typescript', version: '6.0.3' }], resolved: { TYPES_NODE: '26.9.9' }, steps: [{ argv: ['node', '--version'], stdout: 'v26.12.1' }] } };
  };
}

test('The_node_snapshot_is_made_once_registered_by_its_tree_hash_and_accepted_again_only_while_it_still_hashes_the_same', async () => {
  const f = files();
  const home = tmpDir('home');
  const made = await nodeSnapshot({ home, makeFn: fakeMake(), files: f });
  assert.equal(made.ok, true, made.reason);
  assert.equal(made.value.made, true);
  const registered = JSON.parse(readFileSync(f.snapshots, 'utf8')).node;
  assert.equal(registered.treeSha256, treeHash(made.value.dir));
  assert.deepEqual(registered.resolved, { TYPES_NODE: '26.9.9' });
  const wantedDir = path.join(home, 'rsd', 'lang-v2', 'explore', 'toolchains', `cache-node-${registered.treeSha256.slice(0, 12)}`);
  mkdirSync(path.dirname(wantedDir), { recursive: true });
  cpSync(made.value.dir, wantedDir, { recursive: true });
  const again = await nodeSnapshot({ home, makeFn: () => { throw new Error('must not be made twice'); }, files: f });
  assert.equal(again.ok, true, again.reason);
  assert.equal(again.value.made, false);
  writeFileSync(path.join(wantedDir, 'npm', 'x'), 'changed');
  const drifted = await nodeSnapshot({ home, makeFn: () => { throw new Error('unused'); }, files: f });
  assert.equal(drifted.ok, false);
  assert.match(drifted.reason, /no longer hashes/);
});

test('The_csharp_snapshot_is_v1s_registered_one_and_a_directory_that_hashes_otherwise_is_refused', () => {
  const f = files();
  const home = tmpDir('home');
  const explore = loadExplore(f.explore).value;
  const dir = path.join(home, ...explore.csharpSnapshot.dirParts);
  mkdirSync(path.join(dir, 'nuget'), { recursive: true });
  writeFileSync(path.join(dir, 'nuget', 'a'), 'a');
  const refused = csharpSnapshot({ explore, home, files: f });
  assert.equal(refused.ok, false);
  assert.match(refused.reason, /hashes to/);
  const pinned = { ...explore, csharpSnapshot: { ...explore.csharpSnapshot, treeSha256: treeHash(dir) } };
  const ok = csharpSnapshot({ explore: pinned, home, files: f });
  assert.equal(ok.ok, true, ok.reason);
  const record = JSON.parse(readFileSync(f.snapshots, 'utf8')).csharp;
  assert.equal(record.rehashedTreeSha256, treeHash(dir));
  assert.deepEqual(record.parts, JSON.parse(readFileSync(f.v1Snapshots, 'utf8')).csharp.parts, 'the part hashes are v1\'s registered ones');
  assert.equal(csharpSnapshot({ explore: pinned, home, files: f }).value.made, false, 'registered once');
  assert.equal(csharpSnapshot({ explore: pinned, home: tmpDir('empty'), files: f }).ok, false, 'a directory that is not there is a failure');
});

test('The_spin_calibration_is_written_private_once_and_a_machine_that_is_not_quiet_is_a_failure_to_retry', async () => {
  const home = tmpDir('home');
  const busy = await calibrateSpin({ home, calibrateFn: async () => ({ ok: false, reason: 'the machine is not quiet, so the spin probe is not calibrated: mean cpu' }) });
  assert.equal(busy.ok, false);
  assert.equal(existsSync(path.join(home, 'rsd', 'lang-v2', 'explore', 'spin.json')), false, 'nothing is written for a loaded machine');
  const quiet = await calibrateSpin({ home, calibrateFn: async () => ({ ok: true, value: { calibration: { runs: 30, medianMs: 88, minMs: 80, maxMs: 99, spreadMs: 19 } } }) });
  assert.equal(quiet.value.medianMs, 88);
  const file = path.join(home, 'rsd', 'lang-v2', 'explore', 'spin.json');
  assert.equal(statSync(file).mode & 0o777, 0o600);
  const kept = await calibrateSpin({ home, calibrateFn: () => { throw new Error('a calibration that exists is kept'); } });
  assert.equal(kept.value.medianMs, 88);
});

test('Prepare_runs_the_steps_in_order_stops_at_the_first_failure_naming_it_and_keeps_a_busy_machine_resumable', async () => {
  const f = files();
  chmodSync(f.explore, 0o644);
  const home = tmpDir('home');
  const order = [];
  const explore0 = loadExplore(f.explore).value;
  toolchain(explore0, home, INTEGRITY);
  const csDir = path.join(home, ...explore0.csharpSnapshot.dirParts);
  mkdirSync(csDir, { recursive: true });
  writeFileSync(path.join(csDir, 'f'), 'f');
  writeFileSync(f.explore, readFileSync(f.explore, 'utf8').replace(explore0.csharpSnapshot.treeSha256, treeHash(csDir)));
  const deps = {
    files: f,
    registryIntegrity: async () => { order.push('registry'); return { ok: true, value: INTEGRITY }; },
    preflight: async () => { order.push('preflight'); return { ok: true, value: { facts: {} } }; },
    makeSnapshot: async a => { order.push('snapshot'); return fakeMake()(a); },
    calibrateSpin: async () => { order.push('spin'); return { ok: false, reason: 'the machine is not quiet' }; },
  };
  const r = await prepare({ home, deps });
  assert.deepEqual(order, ['registry', 'preflight', 'snapshot', 'spin'], 'network first, the spin probe last');
  assert.equal(r.ok, false);
  assert.equal(r.step, 'spin');
  assert.equal(r.resumable, true);
  assert.equal(r.facts.typescriptIntegrity, INTEGRITY);
  assert.ok(r.facts.nodeSnapshotTreeSha256 && r.facts.csharpSnapshotTreeSha256, 'everything before the spin step is done and reported');
  const early = await prepare({ home, deps: { ...deps, registryIntegrity: async () => ({ ok: false, reason: 'ENOTFOUND registry.npmjs.org' }) } });
  assert.deepEqual([early.ok, early.step, early.resumable], [false, 'integrity', false]);
  assert.match(early.reason, /ENOTFOUND/);
});
