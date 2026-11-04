// E1.S8: the spec conformance checker (conformance.mjs). A conforming synthetic spec of each kind gives one `ok` per check and exit 0; the command line, the exit codes and the usage and format
// errors (exit 4) are here. One failing fixture per check is in conformance-checks.test.mjs; the sentences read from FORMAT are in conformance-sentences.test.mjs; the runner in conformance-runner.test.mjs.
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import path from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { argsOf, check, CHECK_NAMES, fixtureRepo, TASKS } from './support/conformance-fixtures.mjs';

const CLI = fileURLToPath(new URL('../conformance.mjs', import.meta.url));

for (const kind of Object.keys(TASKS)) {
  test(`A_conforming_${kind}_spec_gives_one_ok_per_check_and_exit_0`, async t => {
    const result = await check(fixtureRepo(t, kind));
    assert.deepEqual(result.lines, CHECK_NAMES.map(name => `ok ${name}`), result.lines.join('\n'));
    assert.equal(result.exit, 0);
    assert.deepEqual(result.errors, []);
  });
}

test('The_command_prints_the_check_lines_on_standard_output_and_exits_0_for_a_conforming_spec', t => {
  const fixture = fixtureRepo(t, 'daemon');
  const run = spawnSync(process.execPath, [CLI, ...argsOf(fixture)], { encoding: 'utf8', timeout: 120_000 });
  assert.equal(run.status, 0, run.stderr);
  assert.equal(run.stdout, `${CHECK_NAMES.map(name => `ok ${name}`).join('\n')}\n`);
  assert.equal(run.stderr, '');
});

test('The_command_exits_1_and_prints_the_deviation_on_standard_output', t => {
  const fixture = fixtureRepo(t, 'olympiad', { spec: [['`s02` | `1⏎` | `1⏎`', '`s02` | `2⏎` | `1⏎`']] });
  const run = spawnSync(process.execPath, [CLI, ...argsOf(fixture)], { encoding: 'utf8', timeout: 120_000 });
  assert.equal(run.status, 1, run.stderr);
  assert.match(run.stdout, /^FAIL samples — .*sample s02 standard input/m);
  assert.match(run.stdout, /^ok file$/m);
});

const USAGE_CASES = [
  ['a_missing_option', fixture => ['--repo', fixture.repo, '--task', fixture.task], /missing --format/],
  ['an_unknown_option', fixture => [...argsOf(fixture), '--verbose'], /verbose/],
  ['a_task_that_climbs_out_of_the_repository', fixture => ['--repo', fixture.repo, '--task', '../x', '--format', fixture.format], /not a task name/],
  ['a_task_with_no_spec', fixture => ['--repo', fixture.repo, '--task', '09-none', '--format', fixture.format], /09-none\.md cannot be read/],
  ['a_format_that_does_not_exist', fixture => ['--repo', fixture.repo, '--task', fixture.task, '--format', `${fixture.format}.none`], /cannot be read/],
  ['a_needles_file_that_does_not_exist', fixture => [...argsOf(fixture), '--needles', `${fixture.repo}/none.txt`], /none\.txt cannot be read/],
];
for (const [name, argv, message] of USAGE_CASES) {
  test(`Exit_4_for_${name}`, async t => {
    const fixture = fixtureRepo(t, 'olympiad');
    const result = await import('../lib/conformance.mjs').then(module => module.run(argv(fixture)));
    assert.equal(result.exit, 4);
    assert.deepEqual(result.lines, [], 'a usage error prints no check line: nothing is passed');
    assert.match(result.errors.join('\n'), message);
  });
}

test('Exit_4_for_a_samples_folder_that_does_not_exist', async t => {
  const fixture = fixtureRepo(t, 'olympiad');
  fixture.remove('samples/s01.in');
  fixture.remove('samples/s01.out');
  fixture.remove('samples/s02.in');
  fixture.remove('samples/s02.out');
  const result = await check({ ...fixture, repo: path.join(fixture.repo, 'nowhere') });
  assert.equal(result.exit, 4);
  assert.match(result.errors.join('\n'), /cannot be read/);
});

test('An_olympiad_spec_conforms_under_A_and_B_FORMAT', async t => {
  // group B: a spec of A is checked again under B's FORMAT, which may have changed its daemon and web parts only (T-L39); here B's text changes F-READY, a sentence an olympiad spec does not carry
  const a = fixtureRepo(t, 'olympiad');
  const b = fixtureRepo(t, 'olympiad', { format: [['The program is ready when {readiness}.', 'The program is ready when {readiness}, and not before.']] });
  assert.equal((await check(a)).exit, 0);
  assert.equal((await check({ ...a, format: b.format })).exit, 0, 'the same spec passes under B');
});

test('A_Kind_row_that_names_no_kind_is_a_deviation_and_the_checks_that_need_the_kind_say_they_were_not_run', async t => {
  const result = await check(fixtureRepo(t, 'olympiad', { spec: [['| Kind | a program that reads standard input once and writes standard output once |', '| Kind | a script |']] }));
  assert.equal(result.exit, 1);
  for (const name of ['sections', 'sentences', 'T/G', 'samples', 'sample-forms', 'sample-env']) {
    assert.ok(result.lines.includes(`FAIL ${name} — not checked: the Kind row names none of the three kinds`), name);
  }
});
