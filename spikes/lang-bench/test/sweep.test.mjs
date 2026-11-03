// The start-of-run sweep of the suite (common.test-leftovers §4; fix batch 4): `run-tests.mjs` calls the conventions' OWN `sweepLeftovers({ repo, token: 'bench' })` (release cc24f9e, F22: the reference sweep takes the
// consumer's token) — no copy of its rules. It retires what a KILLED earlier run left in the temp root, on the two keys the rule names: the name and kernel owner are this repository's and the caller's (scope), and the
// claim's process is gone (abandonment). A live run's folder, a young one, an unmarked one and another token's are kept. These tests run against the real function.
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { existsSync, mkdirSync, readFileSync, utimesSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { foreignTokens } from './support/token-scan.mjs';
import { readdirSync } from 'node:fs';
import { sweepLeftovers } from '../../../.agents/conventions/tools/lib/leftovers-sweep.mjs';
import { owned } from './support/owned.mjs';

const REPO = fileURLToPath(new URL('../../..', import.meta.url));
const OWNED_HELPER = fileURLToPath(new URL('../../../.agents/conventions/tools/lib/owned.mjs', import.meta.url));

/** A folder a run made and then died without cleaning (SIGKILL): its claim names a process that is gone. */
function leftBehind(root, what, token = 'bench') {
  const script = `
    import { ownedFor } from ${JSON.stringify(`file://${OWNED_HELPER}`)};
    const o = ownedFor({ token: ${JSON.stringify(token)} });
    const scope = o.cleanupScope();
    console.log(o.ownedDir(scope, ${JSON.stringify(what)}));
    process.kill(process.pid, 'SIGKILL');
  `;
  const ran = spawnSync(process.execPath, ['--input-type=module', '-e', script], { encoding: 'utf8', env: { ...process.env, TMPDIR: root } });
  return ran.stdout.trim();
}

const age = (dir, seconds) => utimesSync(dir, new Date(Date.now() - seconds * 1000), new Date(Date.now() - seconds * 1000));

test('The_suite_sweep_retires_a_dead_runs_folder_and_keeps_a_young_an_unmarked_and_another_tokens', t => {
  const root = owned.ownedDir(t, 'sweeproot');
  const dead = leftBehind(root, 'reservenext');
  const young = leftBehind(root, 'young');
  const other = leftBehind(root, 'other', 'someoneelse');
  const unmarked = path.join(root, 'testsToDelete-u0-bench-unmarked-AAAAAA'); // not this caller's owner form, and with no claim
  mkdirSync(unmarked);
  assert.ok(existsSync(dead) && existsSync(young) && existsSync(other), 'the killed runs left their folders');
  for (const dir of [dead, other, unmarked]) age(dir, 3600);
  const report = sweepLeftovers({ repo: REPO, tmp: root, minAgeMs: 600_000, token: 'bench' });
  assert.deepEqual(report.removed.map(dir => path.basename(dir)), [path.basename(dead)], 'only the old folder of a dead run is removed');
  assert.equal(existsSync(dead), false);
  assert.equal(existsSync(`${dead}.owner`), false, 'and its claim');
  assert.ok(existsSync(young), 'a young one is kept: the age is a selector that keeps the sweep off a run between creation and claim');
  assert.ok(existsSync(other), 'another repository token\'s folder is not this sweep\'s');
  assert.ok(existsSync(unmarked), 'an unmarked folder is nobody\'s the helper knows: listed, never collected');
  assert.ok(report.preserved.length >= 1, 'the sweep names what it keeps');
});

test('The_suite_sweep_never_throws_on_a_missing_temp_root_and_a_live_folder_is_kept', t => {
  assert.deepEqual(sweepLeftovers({ repo: REPO, tmp: '/nonexistent-root-for-the-sweep', minAgeMs: 0, token: 'bench' }).removed, [], 'a missing temp root is no failure');
  const root = owned.ownedDir(t, 'sweeplive');
  const folder = owned.ownedDir(t, 'inuse', { parent: root }); // L8: inside the private root, so the sweep below can never look at the shared /tmp with minAgeMs 0
  const report = { errors: [] };
  assert.equal(path.dirname(folder), root, 'the live folder is inside the private root');
  age(folder, 3600);
  const aged = sweepLeftovers({ repo: REPO, tmp: root, minAgeMs: 0, token: 'bench' });
  assert.ok(existsSync(folder), 'the folder of a live run (this very process) is never removed, however old');
  assert.ok(aged.removed.every(dir => dir !== folder), 'and it is not among the removed');
  assert.ok(existsSync(root));
  writeFileSync(path.join(root, 'file'), 'x');
  assert.ok(Array.isArray(report.errors));
});

test('The_suite_start_calls_the_conventions_sweep_with_this_repositorys_token_and_keeps_no_copy_of_its_rules', () => {
  const runTests = readFileSync(path.join(fileURLToPath(new URL('..', import.meta.url)), 'run-tests.mjs'), 'utf8');
  assert.match(runTests, /leftovers-sweep\.mjs/, 'it imports the conventions\' sweep');
  assert.match(runTests, /sweepLeftovers\(\{[^}]*token: 'bench'/, 'with this repository\'s token (the `bench` row of the rule\'s ## Tokens table)');
  assert.equal(existsSync(fileURLToPath(new URL('./support/sweep.mjs', import.meta.url))), false, 'the repository\'s own copy of the rules is gone');
  assert.throws(() => sweepLeftovers({ repo: REPO, token: 'u1' }), TypeError, 'a token of the owner grammar is refused before anything is read');
  assert.throws(() => sweepLeftovers({ repo: REPO, token: 'Bad Token' }), TypeError);
});

// M1 and S3: only the registered token, in EVERY form a token can be bound.
test('No_test_binds_a_token_other_than_bench_and_the_scan_finds_a_planted_one_in_every_form', () => {
  for (const clean of ["ownedFor({ token: 'bench' })", 'ownedFor({ token: "bench" })', "sweepLeftovers({ repo, token: 'bench' })", "const TOKEN = 'bench'; ownedFor({ token: 'bench' })"]) assert.deepEqual(foreignTokens(clean), [], clean);
  const planted = {
    'a quoted literal': "ownedFor({ token: 'benchx' })",
    'a variable': "const TOKEN = 'benchsig';\nownedFor({ token: TOKEN })",
    'a constant alone': "const OWNED_TOKEN = 'benchbld';",
    'a shorthand': 'const token = pick();\nownedFor({ token })',
    'a template': 'ownedFor({ token: `bench${suffix}` })',
    'an expression': "ownedFor({ token: process.env.T ?? 'bench' })",
    'the --owned-token option': "spawn(node, ['exec-probe.mjs', '--owned-token', 'benchsig'])",
    'the --owned-token option with bench': "spawn(node, ['exec-probe.mjs', '--owned-token', 'bench'])",
  };
  for (const [name, text] of Object.entries(planted)) assert.ok(foreignTokens(text).length > 0, `the scan finds ${name}: ${text}`);
  assert.deepEqual(foreignTokens("const TOKEN = 'benchsig';\nownedFor({ token: TOKEN })").length, 2, 'the variable form is found twice: the constant and its use');
  const spikeDir = fileURLToPath(new URL('..', import.meta.url));
  const files = [...readdirSync(path.join(spikeDir, 'test'), { recursive: true }).filter(name => /\.m?js$/.test(name)).map(name => path.join(spikeDir, 'test', name)),
    ...readdirSync(spikeDir).filter(name => /\.mjs$/.test(name)).map(name => path.join(spikeDir, name))];
  const found = files.filter(file => !/(sweep\.test|token-scan)\.mjs$/.test(file)).flatMap(file => foreignTokens(readFileSync(file, 'utf8')).map(token => `${path.relative(spikeDir, file)}: ${token}`));
  assert.deepEqual(found, [], 'sweep.test.mjs alone binds other tokens, to show that another token\'s folders are kept');
});

/** What a killed probe test REALLY leaves now: its `private-<id>` root (a claimed owned folder) with the probe's runs parent and temp root inside, runs and builds in them. */
function killedProbeTestLeftovers(root) {
  const left = leftBehind(root, 'private');
  mkdirSync(path.join(left, 'tmp', 'testsToDelete-u1000-bench-probebuild-AbCdEf'), { recursive: true });
  writeFileSync(path.join(left, 'tmp', 'testsToDelete-u1000-bench-probebuild-AbCdEf', 'mem.csproj'), '<Project/>');
  mkdirSync(path.join(left, 'cache', 'dew_flow_bench', 'lang-bench-runs', 'testsToDelete-u1000-bench-lrun-GhIjKl', 'views'), { recursive: true });
  age(left, 3600);
  return left;
}

test('What_a_killed_probe_test_really_leaves_a_private_root_with_runs_inside_is_retired_by_the_sweep', t => {
  const root = owned.ownedDir(t, 'sweepprivate');
  const left = killedProbeTestLeftovers(root);
  assert.match(path.basename(left), /^testsToDelete-u\d+-bench-private-/);
  const report = sweepLeftovers({ repo: REPO, tmp: root, token: 'bench' });
  assert.deepEqual(report.removed.map(dir => path.basename(dir)), [path.basename(left)]);
  assert.equal(existsSync(left), false, 'the private root and everything inside it');
  assert.equal(existsSync(`${left}.owner`), false, 'and its claim');
});

test('The_suite_start_itself_retires_a_killed_probe_tests_leftovers_and_is_red_without_the_sweep', t => {
  const root = owned.ownedDir(t, 'sweepsuite');
  const left = killedProbeTestLeftovers(root);
  const spikeDir = fileURLToPath(new URL('..', import.meta.url));
  const ran = spawnSync(process.execPath, [path.join(spikeDir, 'run-tests.mjs'), 'test/fixtures/sweep-noop.fixture.mjs'], { encoding: 'utf8', env: { ...process.env, TMPDIR: root }, timeout: 60_000 });
  assert.equal(ran.status, 0, ran.stderr);
  assert.match(ran.stderr, /run-tests: leftovers — removed 1/, 'the suite start reports what it retired');
  assert.equal(existsSync(left), false, 'the leftovers are gone before the first test runs');
});
