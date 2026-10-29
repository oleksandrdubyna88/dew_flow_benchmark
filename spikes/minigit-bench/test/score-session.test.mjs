import assert from 'node:assert/strict';
import { cpSync, existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { runBounded } from '../lib/run.mjs';
import { parseCli } from '../score-session.mjs';
import { sandboxGate, spikeDir, stubTrees, tmpDir } from './helpers.mjs';

const cli = fileURLToPath(new URL('../score-session.mjs', import.meta.url));
const good = ['--session', '/s', '--source', '/src', '--arm', 'bare', '--stack', 'node', '--phase', 'v1', '--toolchain', '/t'];

async function runCli(args, env) {
  const r = await runBounded(process.execPath, [cli, ...args], { cwd: spikeDir, env, timeoutMs: 120_000 });
  assert.equal(r.ok, true, r.reason);
  return r.value;
}

test('A_usage_error_exits_4_and_names_what_is_wrong', async () => {
  assert.equal(parseCli(good).ok, true);
  assert.match(parseCli(good.slice(2)).reason, /--session/);
  assert.match(parseCli(['--bogus']).reason, /bogus/);
  assert.match(parseCli(good.slice(0, -2)).reason, /toolchain/);
  const r = await runCli(good.slice(0, 4));
  assert.equal(r.code, 4);
  assert.match(r.stdout, /usage error/);
  const bad = await runCli(good.map(a => (a === 'v1' ? 'v9' : a)));
  assert.equal(bad.code, 4);
});

test('A_missing_toolchain_exits_3_as_an_environment_problem', async () => {
  const { args } = setup();
  const r = await runCli(args.map((a, i) => (args[i - 1] === '--toolchain' ? '/no/such/toolchain' : a)));
  assert.equal(r.code, 3);
  assert.match(r.stdout, /\/no\/such\/toolchain/);
});

test('One_command_scores_a_session_and_writes_score_json', async t => {
  if (!sandboxGate(t)) return;
  const session = tmpDir('cli-session');
  const source = tmpDir('cli-source');
  cpSync(path.join(stubTrees, 'silent'), source, { recursive: true });
  const repo = process.env.BENCH_INPUTS_REPO;
  const r = await runCli(['--session', session, '--source', source, '--arm', 'bare', '--stack', 'node', '--phase', 'v1',
    '--toolchain', path.dirname(path.dirname(process.execPath)), ...(repo ? ['--inputs-repo', repo] : [])]);
  assert.equal(r.code, 0, r.stdout + r.stderr);
  assert.match(r.stdout, /^build: ok/m);
  assert.match(r.stdout, /^visible: test-v1\.sh \d+\/11/m);
  assert.match(r.stdout, /^heldout: (unknown|primary100=false)/m, 'the slot is wired: no key reads unknown, a silent binary reads primary100=false');
  const score = JSON.parse(readFileSync(path.join(session, 'score', 'score.json'), 'utf8'));
  assert.equal(score.format, 1);
  assert.deepEqual(score.accepted, { known: true, value: false }, 'a silent binary fails the visible tests');
  assert.ok(score.heldout.known === false || score.heldout.primary100 === false, 'a silent binary is never held-out 100 %');
  assert.equal(existsSync(path.join(session, 'score', 'copy')), false);
});

/** A session, a source tree and the options that score them; `extra` appends flags. */
function setup(stub = 'silent') {
  const session = tmpDir('cli-session');
  const source = tmpDir('cli-source');
  cpSync(path.join(stubTrees, stub), source, { recursive: true });
  const repo = process.env.BENCH_INPUTS_REPO;
  const args = ['--session', session, '--source', source, '--arm', 'bare', '--stack', 'node', '--phase', 'v1',
    '--toolchain', path.dirname(path.dirname(process.execPath)), ...(repo ? ['--inputs-repo', repo] : [])];
  return { session, source, args };
}

test('The_caches_are_named_options_and_the_old_cache_option_is_a_usage_error', async () => {
  const parsed = parseCli([...good, '--nuget-cache', '/n', '--npm-cache', '/m']);
  assert.equal(parsed.ok, true);
  assert.deepEqual([parsed.value['nuget-cache'], parsed.value['npm-cache']], ['/n', '/m']);
  assert.equal(parseCli([...good, '--cache', '/n']).ok, false);
  const r = await runCli([...good, '--cache', '/n']);
  assert.equal(r.code, 4);
});

test('A_missing_source_directory_exits_4_a_configuration_error', async () => {
  const { args } = setup();
  const r = await runCli(args.map((a, i) => (args[i - 1] === '--source' ? '/no/such/source' : a)));
  assert.equal(r.code, 4);
  assert.match(r.stdout, /\/no\/such\/source/);
});

test('A_missing_cache_directory_exits_3_with_the_toolchains_environment_check', async () => {
  const { args } = setup();
  const nuget = await runCli([...args, '--nuget-cache', '/no/such/nuget']);
  assert.equal(nuget.code, 3);
  assert.match(nuget.stdout, /\/no\/such\/nuget/);
  const npm = await runCli([...args, '--npm-cache', '/no/such/npm']);
  assert.equal(npm.code, 3);
});

test('Bwrap_is_looked_up_on_PATH_the_way_it_is_spawned', async () => {
  const { args } = setup();
  const r = await runCli(args, { PATH: path.dirname(process.execPath) });
  assert.equal(r.code, 3);
  assert.match(r.stdout, /bwrap/);
});

test('Another_scoring_in_progress_exits_3_resumable', async t => {
  if (!sandboxGate(t)) return;
  const { session, args } = setup();
  mkdirSync(path.join(session, 'score', 'copy'), { recursive: true });
  writeFileSync(path.join(session, 'score', 'copy', 'keep.txt'), 'x');
  const r = await runCli(args);
  assert.equal(r.code, 3, r.stdout);
  assert.match(r.stdout, /already exists/);
  assert.equal(readFileSync(path.join(session, 'score', 'copy', 'keep.txt'), 'utf8'), 'x', 'the foreign directory is untouched');
});

test('A_box_that_cannot_start_exits_3_and_writes_no_score', async () => {
  const { session, args } = setup();
  const fakeBin = tmpDir('fake-bwrap');
  writeFileSync(path.join(fakeBin, 'bwrap'), '#!/bin/sh\necho "bwrap: No permissions to create new namespace" >&2\nexit 1\n', { mode: 0o755 });
  const r = await runCli(args, { PATH: `${fakeBin}:${path.dirname(process.execPath)}:/usr/bin:/bin` });
  assert.equal(r.code, 3, r.stdout);
  assert.match(r.stdout, /No permissions to create new namespace/);
  assert.equal(existsSync(path.join(session, 'score', 'score.json')), false);
});

// Only discriminating when the test runner itself is not already lowered (run-tests.mjs lowers it, and children inherit): then it checks
// the module's own import; run under plain `node --test` it proves the CLI module lowers itself.
test('The_scorer_lowers_its_own_priority_like_every_local_run', async () => {
  const env = Object.fromEntries(Object.entries(process.env).filter(([k]) => k !== 'CI'));
  const code = `import os from 'node:os'; await import(${JSON.stringify(cli)}); console.log(os.getPriority(0));`;
  const r = await runBounded(process.execPath, ['--input-type=module', '-e', code], { env, timeoutMs: 20_000 });
  assert.equal(r.ok, true, r.reason);
  assert.equal(Number(r.value.stdout.trim()), 19, r.value.stderr);
});
