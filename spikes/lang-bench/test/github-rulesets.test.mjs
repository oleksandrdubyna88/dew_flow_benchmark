// E1.S7 group A slice: the `github-rulesets.mjs` command. `--check` (read-only, the default) and `--apply`, their exit codes, their lines, and the real process with a `gh` shim of the test's own on
// its PATH. No test makes a write to GitHub or touches the network: the in-process cases use the fake runner of test/support/github-fixtures.mjs, the spawned ones a shell shim that answers from files.
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { chmodSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { RULESETS, TASK_REPO } from '../lib/github-lang.mjs';
import { main } from '../github-rulesets.mjs';
import { ALL_CALLS, asGitHub, brokenRunner, fakeGitHub } from './support/github-fixtures.mjs';
import { ownedFolder, writeTree } from './toolchain-fixtures.mjs';

const COMMAND = fileURLToPath(new URL('../github-rulesets.mjs', import.meta.url));
const SECRET = 'gho_' + 'Z9y8X7w6V5u4T3s2R1q0P9o8N7m6L5k4J3i2';
const present = () => RULESETS.map((ruleset, index) => asGitHub(ruleset, 100 + index));

/** The command in this process: `{ exit, out, err, github }`. */
async function runMain(argv, github) {
  const out = [];
  const err = [];
  const exit = await main({ argv, run: github.run, say: line => out.push(line), complain: line => err.push(line) });
  return { exit, out, err, github };
}

// ---- usage ---------------------------------------------------------------------------------------------------------------------------

test('Usage_errors_are_exit_2_and_make_no_call', async () => {
  for (const argv of [['--check', '--apply'], ['--bogus'], ['extra'], ['--repo'], ['--repo', 'not-a-repo'], ['--repo', 'a/b/c'], ['--apply', '--repo', '../x']]) {
    const github = fakeGitHub();
    const got = await runMain(argv, github);
    assert.equal(got.exit, 2, argv.join(' '));
    assert.deepEqual(github.calls, [], argv.join(' '));
    assert.ok(got.err.join('\n').includes('usage'), got.err.join('|'));
  }
});

test('The_default_is_check_and_the_default_repository_is_the_task_repository', async () => {
  const got = await runMain([], fakeGitHub({ rulesets: present() }));
  assert.equal(got.exit, 0, got.out.join('|'));
  assert.ok(got.github.calls.length > 0 && got.github.calls.every(call => call.args.some(arg => arg.startsWith(`repos/${TASK_REPO}`))));
  assert.deepEqual(got.github.writes(), []);
  const other = await runMain(['--check', '--repo', 'o/r'], fakeGitHub({ rulesets: present() }));
  assert.ok(other.github.calls.every(call => call.args.some(arg => arg.startsWith('repos/o/r'))));
});

// ---- --check -------------------------------------------------------------------------------------------------------------------------

test('A_check_of_equal_rulesets_is_exit_0_and_says_so_per_ruleset', async () => {
  const got = await runMain(['--check'], fakeGitHub({ rulesets: present() }));
  assert.equal(got.exit, 0);
  for (const ruleset of RULESETS) assert.ok(got.out.includes(`${ruleset.name}: equal`), got.out.join('|'));
  assert.deepEqual(got.github.writes(), []);
});

test('A_check_with_an_absent_ruleset_is_exit_1', async () => {
  const got = await runMain(['--check'], fakeGitHub({ rulesets: present().slice(0, 2) }));
  assert.equal(got.exit, 1);
  assert.ok(got.out.includes('spec-frozen-tags-create: absent'), got.out.join('|'));
  assert.deepEqual(got.github.writes(), []);
});

test('A_check_with_a_different_ruleset_is_exit_1_and_names_the_difference', async () => {
  const rulesets = present();
  rulesets[1].rules.pop();
  const got = await runMain(['--check'], fakeGitHub({ rulesets }));
  assert.equal(got.exit, 1);
  assert.ok(got.out.includes('spec-frozen-tags: different'));
  assert.ok(got.out.includes('  - missing rule: non_fast_forward'), got.out.join('|'));
  assert.deepEqual(got.github.writes(), []);
});

for (const kind of ['launch', 'timeout', 'exit', 'truncated', 'garbage']) {
  test(`A_gh_that_fails_or_times_out_is_exit_3_in_both_modes_never_a_pass_${kind}`, async () => {
    for (const argv of [['--check'], ['--apply']]) {
      const broken = brokenRunner(kind);
      const out = [];
      const err = [];
      const exit = await main({ argv, run: broken.run, say: line => out.push(line), complain: line => err.push(line) });
      assert.equal(exit, 3, `${argv} ${kind}`);
      assert.ok(!out.some(line => line.endsWith(': equal')), 'nothing was read, so nothing is called equal');
      assert.ok(err.length > 0);
      assert.deepEqual(broken.calls.filter(call => call.method !== 'GET'), []);
    }
  });
}

test('Merge_settings_are_reported_but_never_change_the_exit', async () => {
  const wrong = await runMain(['--check'], fakeGitHub({ rulesets: present(), repository: { allow_merge_commit: true, allow_squash_merge: true } }));
  assert.equal(wrong.exit, 0);
  assert.ok(wrong.out.includes('merge settings (report only): merge setting allow_merge_commit is true, not false'), wrong.out.join('|'));
  const right = await runMain(['--check'], fakeGitHub({ rulesets: present() }));
  assert.ok(right.out.includes('merge settings: ok'), right.out.join('|'));
  const unreadable = await runMain(['--check'], fakeGitHub({ rulesets: present() }, { reply: call => (call.args.at(-1) === `repos/${TASK_REPO}` ? brokenRunner('exit').run().then(value => value) : undefined) }));
  assert.equal(unreadable.exit, 0);
  assert.ok(unreadable.out.some(line => line.startsWith('merge settings (report only): could not be read')), unreadable.out.join('|'));
});

// ---- --apply -------------------------------------------------------------------------------------------------------------------------

test('An_apply_on_an_empty_repository_creates_the_three_and_is_exit_0', async () => {
  const got = await runMain(['--apply'], fakeGitHub());
  assert.equal(got.exit, 0, got.err.join('|'));
  assert.equal(got.github.writes().length, 3);
  for (const ruleset of RULESETS) assert.ok(got.out.includes(`created ${ruleset.name}`), got.out.join('|'));
  assert.ok(got.out.includes('read back after the writes: equal'), got.out.join('|'));
});

test('An_apply_on_equal_rulesets_is_exit_0_with_no_write', async () => {
  const got = await runMain(['--apply'], fakeGitHub({ rulesets: present() }));
  assert.equal(got.exit, 0);
  assert.deepEqual(got.github.writes(), []);
  for (const ruleset of RULESETS) assert.ok(got.out.includes(`unchanged ${ruleset.name}`), got.out.join('|'));
});

test('An_apply_that_meets_a_different_ruleset_is_exit_1_and_writes_nothing', async () => {
  const rulesets = present();
  rulesets[0].enforcement = 'evaluate';
  const got = await runMain(['--apply'], fakeGitHub({ rulesets }));
  assert.equal(got.exit, 1);
  assert.deepEqual(got.github.writes(), []);
  assert.ok(got.err.join('\n').includes('refused'), got.err.join('|'));
  assert.ok(got.err.join('\n').includes('enforcement is "evaluate", not "active"'));
});

test('An_apply_whose_read_back_differs_is_exit_1', async () => {
  const got = await runMain(['--apply'], fakeGitHub({}, { onCreate: body => ({ ...body, rules: body.rules.slice(1) }) }));
  assert.equal(got.exit, 1);
  assert.ok(got.err.join('\n').includes('differs after the write'), got.err.join('|'));
});

// ---- the real process ----------------------------------------------------------------------------------------------------------------

/** A `gh` of the test's own: answers the four calls from files, ends with 7 on anything else, records nothing real. */
function shimFolder(t, files, { failing = false } = {}) {
  const dir = writeTree(ownedFolder(t, 'ghshim'), files);
  const script = failing
    ? '#!/bin/sh\necho "gh: HTTP 500 for gho_ShouldNeverBePrinted" >&2\nexit 1\n'
    : `#!/bin/sh\nfor a in "$@"; do case "$a" in repos/*) p="$a";; esac; done\ncase "$p" in\n  *rulesets\\?*) cat "${dir}/list.json";;\n  */rulesets/*) cat "${dir}/r-\${p##*/}.json";;\n  *) cat "${dir}/repo.json";;\nesac\n`;
  writeFileSync(path.join(dir, 'gh'), script);
  chmodSync(path.join(dir, 'gh'), 0o755);
  return dir;
}

function spawned(dir, home, argv) {
  return spawnSync(process.execPath, [COMMAND, ...argv], { encoding: 'utf8', timeout: 60_000, env: { PATH: `${dir}:${process.env.PATH}`, HOME: home, GH_TOKEN: SECRET } });
}

test('The_real_command_is_exit_2_on_a_usage_error_with_no_stack_trace', t => {
  const dir = shimFolder(t, {}, { failing: true });
  const got = spawned(dir, ownedFolder(t, 'home'), ['--check', '--apply']);
  assert.equal(got.status, 2);
  assert.ok(!/\bat .*\.mjs:\d+/.test(got.stderr), got.stderr);
  assert.ok(got.stderr.includes('usage'));
});

test('The_real_command_reads_through_a_gh_on_its_path_and_is_exit_0_when_equal_and_1_when_not', t => {
  const rulesets = present();
  const files = { 'list.json': JSON.stringify(rulesets.map(({ id, name }) => ({ id, name }))), 'repo.json': JSON.stringify({ allow_merge_commit: false, allow_squash_merge: true }) };
  for (const ruleset of rulesets) files[`r-${ruleset.id}.json`] = JSON.stringify(ruleset);
  const equal = spawned(shimFolder(t, files), ownedFolder(t, 'home'), ['--check']);
  assert.equal(equal.status, 0, equal.stdout + equal.stderr);
  assert.ok(equal.stdout.includes('template-frozen: equal'));
  const lax = JSON.parse(files['r-101.json']);
  lax.bypass_actors.push({ actor_id: 5, actor_type: 'RepositoryRole', bypass_mode: 'always' });
  const different = spawned(shimFolder(t, { ...files, 'r-101.json': JSON.stringify(lax) }), ownedFolder(t, 'home'), ['--check']);
  assert.equal(different.status, 1, different.stdout + different.stderr);
  assert.ok(different.stdout.includes('  - extra bypass actor: RepositoryRole 5 always'));
});

test('The_real_command_is_exit_3_when_gh_fails_and_prints_no_token', t => {
  const got = spawned(shimFolder(t, {}, { failing: true }), ownedFolder(t, 'home'), ['--check']);
  assert.equal(got.status, 3, got.stdout + got.stderr);
  assert.ok(!(got.stdout + got.stderr).includes('gho_ShouldNeverBePrinted') && !(got.stdout + got.stderr).includes(SECRET), got.stderr);
});

test('No_command_test_reaches_the_network', () => {
  assert.ok(ALL_CALLS.length > 20, `${ALL_CALLS.length} recorded calls`);
  assert.deepEqual([...new Set(ALL_CALLS.map(call => call.exe))], ['gh']);
  assert.ok(ALL_CALLS.filter(call => call.method !== 'GET').every(call => call.method === 'POST'), 'the only write any test made was a POST to a fake');
});
