// F2.S2 (b): the task repository's rulesets command, `github-rulesets.mjs`. `--check` (read-only, the default) and `--apply`, their exit codes, their lines, and the real process with a `gh` shim of the test's own on
// its PATH. No test makes a write to GitHub or touches the network: the in-process cases use the fake runner of test/support/github-fixtures.mjs, the spawned ones a shell shim that answers from files.
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { chmodSync, existsSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { BENCH_OWNER_GO, BENCH_REPO, BENCH_RULESETS, RULESETS, TASK_REPO } from '../lib/github-lang.mjs';
import { DEFAULT_PACKAGES_FILE, main } from '../github-rulesets.mjs';
import { ALL_CALLS, asGitHub, brokenRunner, fakeGitHub } from './support/github-fixtures.mjs';
import { ownedFolder, writeTree } from './toolchain-fixtures.mjs';

const COMMAND = fileURLToPath(new URL('../github-rulesets.mjs', import.meta.url));
const SECRET = 'gho_' + 'Z9y8X7w6V5u4T3s2R1q0P9o8N7m6L5k4J3i2';
const present = () => RULESETS.map((ruleset, index) => asGitHub(ruleset, 100 + index));
const SIX = ['04', '05', '06', '07', '08', '10'];

const PASSED = { identity: 'b'.repeat(64), archiveSha256: 'a'.repeat(64), referenceVerdicts: { csharp: 'accepted', node: 'accepted' } }; // a registered, sealed, validated package, as packages.json holds one
const registryOf = (tasks, entry = PASSED) => JSON.stringify(Object.fromEntries(tasks.map(id => [id, entry])));
const sha256Of = text => createHash('sha256').update(text).digest('hex');

/**
 * The two prerequisites of the owner's go of 2026-11-08, as files of the test's own: a `packages.json` (the passed packages of `tasks`, or `entry`s, or the raw `packagesText`) and a dry-run record whose first line is `line`, by default
 * `dry run clean 2026-11-20 packages <sha256 of that packages.json>`. -> `{ packagesFile, record, packagesText }`.
 */
function gateFiles(t, { tasks = SIX, entry = PASSED, packagesText = registryOf(tasks, entry), line, rest = 'the lines after the first are free text\n' } = {}) {
  const dir = ownedFolder(t, 'applygate');
  const packagesFile = path.join(dir, 'packages.json');
  const record = path.join(dir, 'dry-run.txt');
  writeFileSync(packagesFile, packagesText);
  writeFileSync(record, `${line ?? `dry run clean 2026-11-20 packages ${sha256Of(packagesText)}`}\n${rest}`);
  return { packagesFile, record, packagesText };
}

/** The command in this process: `{ exit, out, err, github }`. `extra` is more options of `main` (the `packagesFile` seam). */
async function runMain(argv, github, extra = {}) {
  const out = [];
  const err = [];
  const progress = [];
  const exit = await main({ argv, run: github.run, say: line => out.push(line), complain: line => err.push(line), progress: line => progress.push({ line, callsSoFar: github.calls.length }), ...extra });
  return { exit, out, err, progress, github };
}

/** An apply whose two prerequisites hold. */
async function runApply(t, github, files = gateFiles(t)) {
  return runMain(['--apply', '--dry-run-record', files.record], github, { packagesFile: files.packagesFile });
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

test('The_default_is_check_and_every_call_goes_to_the_task_repository', async () => {
  const got = await runMain([], fakeGitHub({ rulesets: present() }));
  assert.equal(got.exit, 0, got.out.join('|'));
  assert.ok(got.github.calls.length > 0 && got.github.calls.every(call => call.args.some(arg => arg.startsWith(`repos/${TASK_REPO}`))));
  assert.deepEqual(got.github.writes(), []);
});

test('The_repository_is_fixed_in_the_data_and_no_flag_names_another', async t => {
  const files = gateFiles(t);
  for (const argv of [['--repo', 'o/r'], ['--repo', TASK_REPO], [`--repo=${TASK_REPO}`], ['--check', '--repo', 'o/r'], ['--apply', '--dry-run-record', files.record, '--repo', 'o/r'], ['--repository', 'o/r'], ['-r', 'o/r']]) {
    const github = fakeGitHub({ rulesets: present() });
    const got = await runMain(argv, github, { packagesFile: files.packagesFile });
    assert.equal(got.exit, 2, argv.join(' '));
    assert.deepEqual(github.calls, [], argv.join(' '));
    assert.ok(got.err.join('\n').includes('usage'), argv.join(' '));
  }
  const applied = await runApply(t, fakeGitHub());
  assert.ok(applied.github.calls.length > 0 && applied.github.calls.every(call => call.args.some(arg => arg.startsWith(`repos/${TASK_REPO}`))), 'an apply writes to the task repository and to no other');
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
  const rulesets = present().map((ruleset, index) => (index === 1 ? { ...ruleset, rules: ruleset.rules.slice(0, -1) } : ruleset));
  const got = await runMain(['--check'], fakeGitHub({ rulesets }));
  assert.equal(got.exit, 1);
  assert.ok(got.out.includes('spec-frozen-tags: different'));
  assert.ok(got.out.includes('  - missing rule: non_fast_forward'), got.out.join('|'));
  assert.deepEqual(got.github.writes(), []);
});

for (const kind of ['launch', 'timeout', 'exit', 'truncated', 'garbage']) {
  test(`A_gh_that_fails_or_times_out_is_exit_3_in_both_modes_never_a_pass_${kind}`, async t => {
    const files = gateFiles(t);
    for (const argv of [['--check'], ['--apply', '--dry-run-record', files.record]]) {
      const broken = brokenRunner(kind);
      const out = [];
      const err = [];
      const exit = await main({ argv, run: broken.run, say: line => out.push(line), complain: line => err.push(line), packagesFile: files.packagesFile });
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
  const unreadable = await runMain(['--check'], fakeGitHub({ rulesets: present() }, { reply: call => (call.args.at(-1) === `repos/${TASK_REPO}` ? brokenRunner('exit').run('gh', call.args) : undefined) }));
  assert.equal(unreadable.exit, 0);
  assert.ok(unreadable.out.some(line => line.startsWith('merge settings (report only): could not be read') && line.includes('gh exited with code 1')), unreadable.out.join('|'));
});

// ---- --apply -------------------------------------------------------------------------------------------------------------------------

test('An_apply_on_an_empty_repository_creates_the_three_and_is_exit_0', async t => {
  const got = await runApply(t, fakeGitHub());
  assert.equal(got.exit, 0, got.err.join('|'));
  assert.equal(got.github.writes().length, 3);
  for (const ruleset of RULESETS) assert.ok(got.out.includes(`created ${ruleset.name}`), got.out.join('|'));
  assert.ok(got.out.includes('read back after the writes: equal'), got.out.join('|'));
});

test('An_apply_on_equal_rulesets_is_exit_0_with_no_write', async t => {
  const got = await runApply(t, fakeGitHub({ rulesets: present() }));
  assert.equal(got.exit, 0);
  assert.deepEqual(got.github.writes(), []);
  for (const ruleset of RULESETS) assert.ok(got.out.includes(`unchanged ${ruleset.name}`), got.out.join('|'));
});

test('An_apply_that_meets_a_different_ruleset_is_exit_1_and_writes_nothing', async t => {
  const rulesets = present().map((ruleset, index) => (index === 0 ? { ...ruleset, enforcement: 'evaluate' } : ruleset));
  const got = await runApply(t, fakeGitHub({ rulesets }));
  assert.equal(got.exit, 1);
  assert.deepEqual(got.github.writes(), []);
  assert.ok(got.err.join('\n').includes('refused'), got.err.join('|'));
  assert.ok(got.err.join('\n').includes('enforcement is "evaluate", not "active"'));
});

test('An_apply_whose_read_back_differs_is_exit_1', async t => {
  const got = await runApply(t, fakeGitHub({}, { onCreate: body => ({ ...body, rules: body.rules.slice(1) }) }));
  assert.equal(got.exit, 1);
  assert.ok(got.err.join('\n').includes('differs after the write'), got.err.join('|'));
});

// ---- --apply refuses until the owner's go of 2026-11-08 holds ---------------------------------------------------------------------------------

test('An_early_apply_makes_no_gh_call_at_all_while_a_package_is_missing', async t => {
  for (const missing of SIX) {
    const github = fakeGitHub();
    const files = gateFiles(t, { tasks: SIX.filter(id => id !== missing) });
    const got = await runApply(t, github, files);
    assert.equal(got.exit, 1, `without ${missing}`);
    assert.deepEqual(github.calls, [], `without ${missing}: not even a read`);
    assert.deepEqual(github.writes(), []);
    assert.ok(got.err.join('\n').includes('refused') && got.err.join('\n').includes(missing), got.err.join('|'));
  }
  const early = fakeGitHub();
  const real = await runApply(t, early, gateFiles(t, { tasks: ['05'] }));
  assert.equal(real.exit, 1);
  assert.ok(real.err.join('\n').includes('04, 06, 07, 08, 10'), 'every missing task is named, as today\'s registry (only 05) would show');
  assert.deepEqual(early.calls, []);
});

test('An_apply_whose_packages_file_cannot_be_read_or_is_not_an_object_makes_no_gh_call', async t => {
  for (const content of [null, 'not json', '[]', 'null', '"x"']) {
    const github = fakeGitHub();
    const made = gateFiles(t, content === null ? {} : { packagesText: content });
    const files = content === null ? { ...made, packagesFile: `${made.packagesFile}.missing` } : made;
    const got = await runApply(t, github, files);
    assert.equal(got.exit, 1, String(content));
    assert.deepEqual(github.calls, [], String(content));
    assert.ok(got.err.join('\n').includes('packages.json'), got.err.join('|'));
  }
});

test('An_apply_whose_dry_run_record_is_missing_or_not_clean_makes_no_gh_call', async t => {
  const lines = ['', 'dry run dirty 2026-11-20', 'dry run clean', 'dry run clean 2026-11-20 and more', 'dry run clean 2026-13-40', 'dry run clean 20261120', 'Dry run clean 2026-11-20', ' dry run clean 2026-11-20', 'dry run clean 2026-02-30'];
  for (const line of lines) {
    const github = fakeGitHub();
    const got = await runApply(t, github, gateFiles(t, { line }));
    assert.equal(got.exit, 1, JSON.stringify(line));
    assert.deepEqual(github.calls, [], JSON.stringify(line));
    assert.ok(got.err.join('\n').includes('dry run clean <yyyy-mm-dd>'), got.err.join('|'));
  }
  const later = gateFiles(t, { line: 'the dry run was run', rest: 'dry run clean 2026-11-20\n' });
  assert.equal((await runApply(t, fakeGitHub(), later)).exit, 1, 'only the first line counts');
  const absent = fakeGitHub();
  const files = gateFiles(t);
  const got = await runMain(['--apply', '--dry-run-record', `${files.record}.missing`], absent, { packagesFile: files.packagesFile });
  assert.equal(got.exit, 1);
  assert.deepEqual(absent.calls, []);
  const dirGithub = fakeGitHub();
  assert.equal((await runMain(['--apply', '--dry-run-record', path.dirname(files.record)], dirGithub, { packagesFile: files.packagesFile })).exit, 1, 'a folder is not a record');
  assert.deepEqual(dirGithub.calls, []);
});

test('An_apply_whose_task_entries_do_not_show_a_passed_package_makes_no_gh_call', async t => {
  const bad = {
    'an empty entry': {},
    'a missing archive': { identity: PASSED.identity, referenceVerdicts: PASSED.referenceVerdicts },
    'an archive that is not a sha256': { ...PASSED, archiveSha256: 'sealed' },
    'an identity that is an array of one 64-hex string': { ...PASSED, identity: [PASSED.identity] },
    'an archive that is an array of one 64-hex string': { ...PASSED, archiveSha256: [PASSED.archiveSha256] },
    'a missing identity': { archiveSha256: PASSED.archiveSha256, referenceVerdicts: PASSED.referenceVerdicts },
    'a csharp reference not accepted': { ...PASSED, referenceVerdicts: { csharp: 'rejected', node: 'accepted' } },
    'a node reference not accepted': { ...PASSED, referenceVerdicts: { csharp: 'accepted', node: 'pending' } },
  };
  for (const [what, entry] of Object.entries(bad)) {
    for (const id of SIX) {
      const registry = { ...JSON.parse(registryOf(SIX)), [id]: entry };
      const github = fakeGitHub();
      const got = await runApply(t, github, gateFiles(t, { packagesText: JSON.stringify(registry) }));
      assert.equal(got.exit, 1, `${what}, task ${id}`);
      assert.deepEqual(github.calls, [], `${what}, task ${id}: not even a read`);
      assert.ok(got.err.join('\n').includes(`task ${id}`) && got.err.join('\n').includes('passed package'), got.err.join('|'));
    }
  }
  const all = fakeGitHub();
  assert.equal((await runApply(t, all, gateFiles(t, { entry: {} }))).exit, 1);
  assert.deepEqual(all.calls, [], 'six empty objects');
});

test('A_dry_run_record_made_against_another_registry_makes_no_gh_call_and_the_matching_one_applies', async t => {
  const files = gateFiles(t);
  const matching = fakeGitHub();
  assert.equal((await runApply(t, matching, files)).exit, 0, 'the sha256 of the bytes read now');
  assert.equal(matching.writes().length, 3);
  const others = {
    'a record of an older registry': `dry run clean 2026-11-20 packages ${sha256Of('an older registry')}`,
    'a sha256 of zeros': `dry run clean 2026-11-20 packages ${'0'.repeat(64)}`,
    'no sha256': 'dry run clean 2026-11-20',
    'an upper-case sha256': `dry run clean 2026-11-20 packages ${sha256Of(files.packagesText).toUpperCase()}`,
  };
  for (const [what, line] of Object.entries(others)) {
    const github = fakeGitHub();
    const got = await runApply(t, github, gateFiles(t, { line }));
    assert.equal(got.exit, 1, what);
    assert.deepEqual(github.calls, [], `${what}: not even a read`);
    assert.ok(got.err.join('\n').includes('refused'), what);
  }
  // The registry changes after the dry run: the same record no longer matches the file it read.
  const stale = gateFiles(t);
  writeFileSync(stale.packagesFile, `${stale.packagesText}\n`);
  const github = fakeGitHub();
  const got = await runApply(t, github, stale);
  assert.equal(got.exit, 1);
  assert.deepEqual(github.calls, []);
  assert.ok(got.err.join('\n').includes('registry changed'), got.err.join('|'));
});

test('An_apply_without_the_dry_run_record_flag_is_a_usage_error_and_check_takes_none', async t => {
  const files = gateFiles(t);
  for (const argv of [['--apply'], ['--apply', '--dry-run-record'], ['--check', '--dry-run-record', files.record], ['--dry-run-record', files.record]]) {
    const github = fakeGitHub();
    const got = await runMain(argv, github, { packagesFile: files.packagesFile });
    assert.equal(got.exit, 2, argv.join(' '));
    assert.deepEqual(github.calls, [], argv.join(' '));
    assert.ok(got.err.join('\n').includes('usage'));
  }
});

test('An_apply_whose_packages_are_arrays_makes_no_gh_call', async t => {
  const github = fakeGitHub();
  const files = gateFiles(t, { packagesText: registryOf(SIX, []) });
  const got = await runApply(t, github, files);
  assert.equal(got.exit, 1);
  assert.deepEqual(github.calls, []);
  assert.ok(got.err.join('\n').includes('04, 05, 06, 07, 08, 10'), got.err.join('|'));
});

test('A_dry_run_record_flag_followed_by_nothing_or_by_another_flag_is_a_usage_error_naming_the_missing_value', async () => {
  for (const argv of [['--apply', '--dry-run-record', '--check'], ['--apply', '--dry-run-record', '--apply'], ['--apply', '--dry-run-record', '--x'], ['--apply', '--dry-run-record'], ['--dry-run-record'], ['--check', '--dry-run-record', '--check'], ['--apply', '--dry-run-record', '']]) {
    const github = fakeGitHub();
    const got = await runMain(argv, github);
    assert.equal(got.exit, 2, argv.join(' '));
    assert.deepEqual(github.calls, [], argv.join(' '));
    assert.deepEqual(got.progress, [], 'nothing was read');
    assert.ok(got.err.join('\n').includes('--dry-run-record needs a file name') || got.err.join('\n').includes('--apply needs --dry-run-record <file>'), `${argv.join(' ')}: ${got.err.join('|')}`);
  }
  const flagValue = await runMain(['--apply', '--dry-run-record', '--check'], fakeGitHub());
  assert.ok(flagValue.err.join('\n').includes('--dry-run-record needs a file name, and none was given'), 'the other flag is not taken as the file name');
});

test('The_usage_text_lists_every_accepted_flag_and_the_dry_run_line', async () => {
  const got = await runMain(['--bogus'], fakeGitHub());
  const text = got.err.join('\n');
  for (const word of ['--check', '--apply', '--dry-run-record <file>', 'dry run clean <yyyy-mm-dd> packages <sha256 of packages.json>', TASK_REPO]) assert.ok(text.includes(word), `${word} in: ${text}`);
  assert.ok(!text.includes('--repo'), 'the repository is data, not a flag');
});

test('One_progress_line_goes_to_stderr_before_the_first_read_and_stdout_stays_the_result', async t => {
  const line = 'reading the task repository\'s rulesets…';
  const check = await runMain(['--check'], fakeGitHub({ rulesets: present() }));
  assert.deepEqual(check.progress, [{ line, callsSoFar: 0 }], 'one line, before any call');
  assert.ok(!check.out.some(entry => entry.includes('reading')) && !check.err.some(entry => entry.includes('reading')), 'not in the result, not among the complaints');
  const applied = await runApply(t, fakeGitHub());
  assert.deepEqual(applied.progress, [{ line, callsSoFar: 0 }]);
  const usage = await runMain(['--bogus'], fakeGitHub());
  assert.deepEqual(usage.progress, [], 'a usage error reads nothing and says nothing of reading');
  const early = await runApply(t, fakeGitHub(), gateFiles(t, { tasks: ['05'] }));
  assert.deepEqual(early.progress, [], 'a refused apply reads nothing');
});

test('The_default_packages_file_is_the_registry_of_the_packages', () => {
  assert.ok(existsSync(DEFAULT_PACKAGES_FILE), DEFAULT_PACKAGES_FILE);
  assert.ok(DEFAULT_PACKAGES_FILE.endsWith(path.join('lang-explore', 'inputs', 'packages.json')));
  const packages = JSON.parse(readFileSync(DEFAULT_PACKAGES_FILE, 'utf8'));
  assert.ok(packages !== null && typeof packages === 'object' && '05' in packages, 'task 05 is registered, so the path is the registry and not an empty file');
});

test('An_apply_with_both_prerequisites_met_creates_the_rulesets', async t => {
  const github = fakeGitHub();
  const got = await runApply(t, github, gateFiles(t, { rest: '' }));
  assert.equal(got.exit, 0, got.err.join('|'));
  assert.equal(github.writes().length, 3);
});

test('An_apply_that_fails_half_way_exits_3_with_the_three_lists_and_a_second_apply_finishes_it', async t => {
  let posts = 0;
  const github = fakeGitHub({}, { reply: call => (call.method === 'POST' && ++posts === 2 ? { ok: true, value: { code: 1, stdout: '', stderr: 'gh: HTTP 502', timedOut: false, truncated: false } } : undefined) });
  const first = await runApply(t, github);
  assert.equal(first.exit, 3);
  assert.ok(first.err.join('\n').includes('Confirmed created: template-frozen. Uncertain: spec-frozen-tags. Not attempted: spec-frozen-tags-create.'), first.err.join('|'));
  assert.ok(first.out.includes('created template-frozen'));
  const second = await runApply(t, github);
  assert.equal(second.exit, 0, second.err.join('|'));
  assert.ok(second.out.includes('unchanged template-frozen') && second.out.includes('created spec-frozen-tags') && second.out.includes('created spec-frozen-tags-create'), second.out.join('|'));
  assert.equal(github.state.rulesets.length, 3);
});

// ---- the real process ----------------------------------------------------------------------------------------------------------------

/** A `gh` of the test's own: answers the four calls from files (or fails, when `failing`) and appends a line to `<dir>/ran` each time it starts, so a test can prove the shim ran (or did not). */
function shimFolder(t, files, { failing = false } = {}) {
  const dir = writeTree(ownedFolder(t, 'ghshim'), files);
  const script = failing
    ? `#!/bin/sh\necho ran >> "${dir}/ran"\necho "gh: HTTP 500 for gho_ShouldNeverBePrinted" >&2\nexit 1\n`
    : `#!/bin/sh\necho ran >> "${dir}/ran"\nfor a in "$@"; do case "$a" in repos/*) p="$a";; esac; done\ncase "$p" in\n  *rulesets\\?*) cat "${dir}/list.json";;\n  */rulesets/*) cat "${dir}/r-\${p##*/}.json";;\n  *) cat "${dir}/repo.json";;\nesac\n`;
  writeFileSync(path.join(dir, 'gh'), script);
  chmodSync(path.join(dir, 'gh'), 0o755);
  return dir;
}

const shimRan = dir => existsSync(path.join(dir, 'ran'));

function spawned(dir, home, argv) {
  return spawnSync(process.execPath, [COMMAND, ...argv], { encoding: 'utf8', timeout: 60_000, env: { PATH: `${dir}:${process.env.PATH}`, HOME: home, GH_TOKEN: SECRET } });
}

test('The_real_command_is_exit_2_on_a_usage_error_with_no_stack_trace', t => {
  const dir = shimFolder(t, {}, { failing: true });
  const got = spawned(dir, ownedFolder(t, 'home'), ['--check', '--apply']);
  assert.equal(got.status, 2);
  assert.equal(shimRan(dir), false, 'a usage error starts no gh');
  assert.ok(!/\bat .*\.mjs:\d+/.test(got.stderr), got.stderr);
  assert.ok(got.stderr.includes('usage'));
});

test('The_real_command_reads_through_a_gh_on_its_path_and_is_exit_0_when_equal_and_1_when_not', t => {
  const rulesets = present();
  const files = { 'list.json': JSON.stringify(rulesets.map(({ id, name }) => ({ id, name }))), 'repo.json': JSON.stringify({ allow_merge_commit: false, allow_squash_merge: true }), ...Object.fromEntries(rulesets.map(ruleset => [`r-${ruleset.id}.json`, JSON.stringify(ruleset)])) };
  const equalDir = shimFolder(t, files);
  const equal = spawned(equalDir, ownedFolder(t, 'home'), ['--check']);
  assert.equal(equal.status, 0, equal.stdout + equal.stderr);
  assert.ok(shimRan(equalDir), 'the answers came from the shim, so the shim ran');
  assert.ok(equal.stderr.includes('reading the task repository\'s rulesets…') && !equal.stdout.includes('reading'), 'the progress line is on stderr, never in the result');
  assert.ok(equal.stdout.includes('template-frozen: equal'));
  const stored = JSON.parse(files['r-101.json']);
  const lax = { ...stored, bypass_actors: [...stored.bypass_actors, { actor_id: 5, actor_type: 'RepositoryRole', bypass_mode: 'always' }] };
  const differentDir = shimFolder(t, { ...files, 'r-101.json': JSON.stringify(lax) });
  const different = spawned(differentDir, ownedFolder(t, 'home'), ['--check']);
  assert.equal(different.status, 1, different.stdout + different.stderr);
  assert.ok(shimRan(differentDir));
  assert.ok(different.stdout.includes('  - extra bypass actor: RepositoryRole 5 always'));
});

test('The_real_command_refuses_an_early_apply_before_it_starts_gh', t => {
  const dir = shimFolder(t, {}, { failing: true });
  const got = spawned(dir, ownedFolder(t, 'home'), ['--apply', '--dry-run-record', path.join(dir, 'no-such-record.txt')]);
  assert.equal(got.status, 1, `exit 1 (refused), not 3 (gh failed): ${got.stdout}${got.stderr}`);
  assert.ok(got.stderr.includes('refused') && !got.stderr.includes('HTTP 500'), got.stderr);
  assert.equal(shimRan(dir), false, 'the shim was never started: the gate stopped the command first');
});

test('The_real_command_is_exit_3_when_gh_fails_and_prints_no_token', t => {
  const dir = shimFolder(t, {}, { failing: true });
  const got = spawned(dir, ownedFolder(t, 'home'), ['--check']);
  assert.equal(got.status, 3, got.stdout + got.stderr);
  assert.ok(shimRan(dir), 'the failure came from the shim, so the shim ran');
  assert.ok(!(got.stdout + got.stderr).includes('gho_ShouldNeverBePrinted') && !(got.stdout + got.stderr).includes(SECRET), got.stderr);
});

test('No_command_test_reaches_the_network', () => {
  assert.ok(ALL_CALLS.length > 20, `${ALL_CALLS.length} recorded calls`);
  assert.deepEqual([...new Set(ALL_CALLS.map(call => call.exe))], ['gh']);
  assert.ok(ALL_CALLS.filter(call => call.method !== 'GET').every(call => call.method === 'POST'), 'the only write any test made was a POST to a fake');
});

// ---- --bench: the bench repository's pre-registration tag rulesets (F3.S1 (b), PR 5; the owner's go of 2026-11-19). Never a real call: every case runs over the fake. -----------------------------

const benchPresent = () => BENCH_RULESETS.map((ruleset, index) => asGitHub(ruleset, 200 + index));
const everyCallIs = (github, repo) => github.calls.length > 0 && github.calls.every(call => call.args.some(arg => arg.startsWith(`repos/${repo}/rulesets`)));

test('A_bench_check_of_equal_rulesets_is_exit_0_reads_the_bench_repository_only_and_reports_no_merge_settings', async () => {
  const got = await runMain(['--bench', '--check'], fakeGitHub({ rulesets: benchPresent() }));
  assert.equal(got.exit, 0, got.out.join('|'));
  for (const ruleset of BENCH_RULESETS) assert.ok(got.out.includes(`${ruleset.name}: equal`), got.out.join('|'));
  assert.ok(got.out.some(line => line.startsWith('ids: prereg-tags=200 prereg-tags-create=201')), got.out.join('|'));
  assert.ok(everyCallIs(got.github, BENCH_REPO), 'every call names the bench repository\'s rulesets, and none reads the repository\'s merge settings');
  assert.ok(!got.out.some(line => line.includes('merge settings')), 'the merge settings are the task repository\'s');
  assert.deepEqual(got.github.writes(), []);
  assert.deepEqual(got.progress.map(p => p.line), ['reading the bench repository\'s rulesets…']);
});

test('A_bench_check_with_an_absent_or_different_ruleset_is_exit_1_and_changes_nothing', async () => {
  const absent = await runMain(['--bench'], fakeGitHub({ rulesets: benchPresent().slice(0, 1) }));
  assert.equal(absent.exit, 1);
  assert.ok(absent.out.includes('prereg-tags-create: absent'), absent.out.join('|'));
  const different = await runMain(['--bench', '--check'], fakeGitHub({ rulesets: benchPresent().map((r, i) => (i === 0 ? { ...r, rules: r.rules.slice(0, 1) } : r)) }));
  assert.equal(different.exit, 1);
  assert.ok(different.out.includes('  - missing rule: deletion'), different.out.join('|'));
  assert.deepEqual([...absent.github.writes(), ...different.github.writes()], []);
});

test('A_bench_apply_needs_the_owners_go_date_and_makes_no_call_without_it', async () => {
  for (const argv of [['--bench', '--apply'], ['--bench', '--apply', '--owner-go'], ['--bench', '--check', '--owner-go', BENCH_OWNER_GO], ['--owner-go', BENCH_OWNER_GO], ['--bench', '--apply', '--owner-go', BENCH_OWNER_GO, '--dry-run-record', 'x'], ['--apply', '--owner-go', BENCH_OWNER_GO]]) {
    const github = fakeGitHub();
    const got = await runMain(argv, github);
    assert.equal(got.exit, 2, argv.join(' '));
    assert.deepEqual(github.calls, [], argv.join(' '));
    assert.ok(got.err.join('\n').includes('usage'), argv.join(' '));
  }
  const wrong = fakeGitHub();
  const refused = await runMain(['--bench', '--apply', '--owner-go', '2026-11-18'], wrong);
  assert.equal(refused.exit, 1, 'another date is a refusal');
  assert.match(refused.err.join('|'), /owner's go of 2026-11-19; nothing was read or written/);
  assert.deepEqual(wrong.calls, []);
  assert.deepEqual(refused.progress, [], 'a refused apply says nothing of reading');
});

test('A_bench_apply_under_the_go_creates_the_two_rulesets_and_reads_them_back_equal_with_their_ids', async () => {
  const github = fakeGitHub();
  const got = await runMain(['--bench', '--apply', '--owner-go', BENCH_OWNER_GO], github);
  assert.equal(got.exit, 0, got.err.join('|'));
  assert.ok(got.out.includes('created prereg-tags') && got.out.includes('created prereg-tags-create'), got.out.join('|'));
  assert.ok(got.out.includes('read back after the writes: equal'));
  assert.ok(got.out.some(line => line.startsWith('ids: prereg-tags=1000 prereg-tags-create=1001')), 'the ids step T records are printed');
  assert.ok(everyCallIs(github, BENCH_REPO));
  assert.deepEqual(github.writes().map(call => call.method), ['POST', 'POST']);
  const again = await runMain(['--bench', '--apply', '--owner-go', BENCH_OWNER_GO], fakeGitHub({ rulesets: github.state.rulesets }));
  assert.equal(again.exit, 0);
  assert.deepEqual(again.github.writes(), []);
  assert.ok(again.out.includes('read back: equal'));
});

test('The_usage_text_names_the_bench_flags_and_the_task_repository_stays_the_default', async () => {
  const text = (await runMain(['--bogus'], fakeGitHub())).err.join('\n');
  for (const word of ['--bench', '--owner-go <date>', BENCH_REPO]) assert.ok(text.includes(word), `${word} in: ${text}`);
  const plain = await runMain([], fakeGitHub({ rulesets: present() }));
  assert.ok(everyCallIs(plain.github, TASK_REPO) || plain.github.calls.every(call => call.args.some(arg => arg.startsWith(`repos/${TASK_REPO}`))), 'no flag: the task repository, as before');
});
