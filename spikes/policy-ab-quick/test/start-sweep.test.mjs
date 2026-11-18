// The start sweep of the minigit runner (common.test-leftovers §4): `spikes/minigit-bench/run-tests.mjs` is the runner policy-ab-quick's tests go through, and it calls the conventions' OWN `sweepLeftovers` with this repository's
// token, so what a KILLED test of any `bench` spike left in the temp root is retired before the next run starts. The first test kills a real test; the second reads the runner's text (the shape of lang-bench's `sweep.test.mjs`).
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { existsSync, readFileSync, utimesSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { ownedFor, parseOwner } from '../../../.agents/conventions/tools/lib/owned.mjs';

const owned = ownedFor({ token: 'bench' });
const here = path.dirname(fileURLToPath(import.meta.url));
const runner = path.join(here, '..', '..', 'minigit-bench', 'run-tests.mjs');

// A child `node --test` started from inside a test must not see the parent runner's context (it would skip its files as a recursive run).
// TESTSTODELETE_RUN is deleted too: when a caller's live run is named there, the killed fixture's claim would name that run instead of the dead process (the C# kill tests do the same).
const childEnv = root => { const env = { ...process.env, TMPDIR: root, TEMP: root, TMP: root }; delete env.NODE_TEST_CONTEXT; delete env.TESTSTODELETE_RUN; return env; };
const age = (target, seconds) => utimesSync(target, new Date(Date.now() - seconds * 1000), new Date(Date.now() - seconds * 1000));

test('The_minigit_runner_start_retires_a_killed_policy_ab_quick_tests_folder_and_is_red_without_the_sweep', t => {
  const root = owned.ownedDir(t, 'pabsweep');
  const killed = spawnSync(process.execPath, [path.join(here, 'fixtures', 'killed-owned.fixture.mjs')], { encoding: 'utf8', env: childEnv(root), timeout: 60_000 });
  assert.equal(killed.signal, 'SIGKILL', `the test was killed (status ${killed.status}): ${killed.stderr}`);
  const line = killed.stdout.split('\n').find(text => text.startsWith('KILLED_FOLDER='));
  assert.ok(line, `the killed test printed its folder: ${killed.stdout}`);
  const left = line.slice('KILLED_FOLDER='.length);
  assert.equal(path.dirname(left), root, 'it made its folder in the private temp root');
  assert.ok(path.basename(left).startsWith(owned.ownedPrefix('pabkilled')), `the folder is in the owner form: ${left}`);
  assert.ok(existsSync(left) && existsSync(`${left}.owner`), 'the killed test left its folder and its claim');
  assert.equal(parseOwner(readFileSync(`${left}.owner`, 'utf8')).pid, killed.pid, 'the claim names the dead process');
  age(left, 3600);
  age(`${left}.owner`, 3600);
  const ran = spawnSync(process.execPath, [runner, '../policy-ab-quick/test/fixtures/sweep-noop.fixture.mjs'], { encoding: 'utf8', env: childEnv(root), timeout: 60_000 });
  assert.equal(ran.status, 0, ran.stderr);
  // The sweep may retire more than this one folder (a dead-owned owner-form worktree of the repository, a folder of another killed run): the check is that THIS folder and claim are gone and that the line reports at least one removal.
  const reported = /run-tests: leftovers — removed (\d+),/.exec(ran.stderr);
  assert.ok(reported && Number(reported[1]) >= 1, `the runner start reports what it retired: ${ran.stderr}`);
  assert.equal(existsSync(left), false, 'the killed test\'s folder is gone before the first test runs');
  assert.equal(existsSync(`${left}.owner`), false, 'and its claim');
});

test('The_minigit_runner_start_names_each_folder_it_keeps_so_a_developer_can_act_on_it', t => {
  const root = owned.ownedDir(t, 'pabkeep');
  const killed = spawnSync(process.execPath, [path.join(here, 'fixtures', 'killed-owned.fixture.mjs')], { encoding: 'utf8', env: childEnv(root), timeout: 60_000 });
  const left = killed.stdout.split('\n').find(text => text.startsWith('KILLED_FOLDER=')).slice('KILLED_FOLDER='.length);
  const ran = spawnSync(process.execPath, [runner, '../policy-ab-quick/test/fixtures/sweep-noop.fixture.mjs'], { encoding: 'utf8', env: childEnv(root), timeout: 60_000 });
  assert.equal(ran.status, 0, ran.stderr);
  assert.match(ran.stderr, /run-tests: leftovers — removed \d+, preserved [1-9]\d*/, 'the count line keeps its text');
  assert.ok(ran.stderr.split('\n').includes(`run-tests:   preserved ${left} (young)`), `a young folder is kept and named, one per line: ${ran.stderr}`);
  assert.ok(existsSync(left), 'and it is kept');
});

test('The_minigit_runner_calls_the_conventions_sweep_with_the_bench_token_and_keeps_no_copy_of_its_rules', () => {
  const text = readFileSync(runner, 'utf8');
  assert.match(text, /leftovers-sweep\.mjs/, 'it imports the conventions\' sweep from the mount');
  assert.match(text, /sweepLeftovers\(\{[^}]*token: 'bench'/, 'with this repository\'s token (the `bench` row of the rule\'s ## Tokens table)');
  assert.doesNotMatch(text, /readdirSync|rmSync/, 'and holds no listing or removal of its own');
});
