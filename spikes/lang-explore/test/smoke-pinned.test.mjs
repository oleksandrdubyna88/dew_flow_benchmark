// F3 PR 5c: the smoke on the PINNED CLI (`prereg/smoke-pinned.mjs`). The wrapper checks the pinned copy, then runs v1's smoke, sanitise and verdicts on that binary and version; the sanitised capture is built beside the fixture folder and
// renamed into place only when sanitise and verdicts both succeeded, and a later step can be redone from the raw capture (`--from sanitise|verdicts --raw <dir>`) without a second paid session.
// Everything runs on fakes: a stand-in for the CLI copy, a fake home with a fake token, a fake of v1's `main` where a test reads what the wrapper asked of it (it can leave what v1's steps leave), and v1's own `main` where the subject
// is v1's (the out dir, the dry run, a real sanitise of a stub capture). No real CLI, network or token is used and nothing is paid.
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { chmodSync, cpSync, existsSync, mkdirSync, readFileSync, readdirSync, realpathSync, rmSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { TOKEN_FILE_PARTS } from '../../minigit-bench/capture/capture-env.mjs';
import { crosscheckPolicyFrom } from '../../minigit-bench/capture/policy-input.mjs';
import { renderVerdicts, verdictBlockOf } from '../../minigit-bench/capture/verdicts.mjs';
import { smokeEvidence } from '../../minigit-bench/lib/aa-facts.mjs';
import { loadSmoke } from '../../minigit-bench/lib/smoke-load.mjs';
import { computeSmokeVerdicts } from '../../minigit-bench/lib/smoke-verdicts.mjs';
import { fakeToken } from '../../minigit-bench/test/broker-stub.mjs';
import { sandboxGate, spikeDir, tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { binaryNamed, homeWithToken, stubSmokeSession } from '../../minigit-bench/test/smoke-stub.mjs';
import { loadExplore } from '../lib/inputs.mjs';
import { INPUTS, REPO } from '../lib/paths.mjs';
import { FIXTURES_DIR, fixtureNameOf, main, stagingOf } from '../prereg/smoke-pinned.mjs';

const VERSION = '2.1.329';
const COPY_BYTES = '#!/bin/sh\necho 2.1.329 (stand-in for the pinned CLI)\n';
const sha = text => createHash('sha256').update(text).digest('hex');
const VERDICTS = ['holds', 'refuted', 'not exercised'];

/**
 * A home with a token and the pinned copy where `explore.cli.pathParts` says, and an `explore.json` that is the registered one with its `cli` entry replaced: the fixture is built against the real validator (`loadExplore`), so a shape the
 * wrapper would decline cannot make a test pass for the wrong reason.
 */
function pinnedWorld({ version = VERSION, bytes = COPY_BYTES, pinnedSha = sha(COPY_BYTES), copy = true, token = fakeToken() } = {}) {
  const home = homeWithToken(token, 'pinned-home');
  const parts = ['rsd', 'lang-v2', 'toolchains', `claude-${version}`];
  const binary = path.join(home, ...parts);
  if (copy) {
    mkdirSync(path.dirname(binary), { recursive: true });
    writeFileSync(binary, bytes, { mode: 0o555 });
    chmodSync(binary, 0o555);
  }
  const registered = JSON.parse(readFileSync(path.join(INPUTS, 'explore.json'), 'utf8'));
  const explore = path.join(tmpDir('pinned-explore'), 'explore.json');
  writeFileSync(explore, JSON.stringify({ ...registered, cli: { ...registered.cli, version, pathParts: parts, sha256: pinnedSha } }, null, 2));
  assert.equal(loadExplore(explore).ok, true, loadExplore(explore).reason);
  const fixtures = path.join(tmpDir('pinned-fixtures'), 'real');
  return { home, explore, binary, fixtures, fixture: path.join(fixtures, fixtureNameOf(version)), staging: stagingOf(path.join(fixtures, fixtureNameOf(version))), out: path.join(tmpDir('pinned-out'), 'r1') };
}

/** What v1's steps leave when they succeed (`argv` is the call): the raw session dir with a finished run's meta.json, the sanitised capture where `--to` says, SMOKE.md where `--fixture` says. */
const leaves = {
  run: argv => rawCapture(argv[2]),
  sanitise: argv => { mkdirSync(argv[4], { recursive: true }); writeFileSync(path.join(argv[4], 'meta.json'), '{}\n'); },
  verdicts: argv => writeFileSync(path.join(argv[2], 'SMOKE.md'), 'table\n'),
};

/** A fake of v1's `main` that records each call, runs `effects[verb](argv)` and answers the code the test chose for the verb (0 when it chose none). */
function fakeSmoke({ codes = {}, effects = {} } = {}) {
  const calls = [];
  return { calls, fn: async (argv, opts) => { calls.push({ argv, opts }); effects[argv[0]]?.(argv); return codes[argv[0]] ?? 0; } };
}
const succeeding = () => fakeSmoke({ effects: leaves });

/** A raw session dir as the paid run leaves it: a meta.json with the outcome and the version the CLI reported, and one more file to see it untouched. */
function rawCapture(dir, { outcome = { ok: true, label: 'ok' }, cliVersion = VERSION } = {}) {
  mkdirSync(dir, { recursive: true });
  writeFileSync(path.join(dir, 'meta.json'), `${JSON.stringify({ outcome, cliVersion })}\n`);
  writeFileSync(path.join(dir, 'stream.ndjson'), '{"type":"system"}\n');
  return dir;
}

/** Every file under a folder with its hash, sorted: what "untouched" means for a raw capture. */
const treeOf = dir => readdirSync(dir, { recursive: true, withFileTypes: true }).filter(e => e.isFile()).map(e => `${path.relative(dir, path.join(e.parentPath, e.name))} ${sha(readFileSync(path.join(e.parentPath, e.name)))}`).sort();

/** The wrapper run over a world with these flags: `{ code, lines }`. `smoke` is a fake, or v1's own `main` by default. */
async function runWrapper(world, flags, smoke) {
  const lines = [];
  const code = await main([...flags, '--explore', world.explore], line => lines.push(line), { home: world.home, fixturesDir: world.fixtures, ...(smoke ? { smoke } : {}) });
  return { code, lines, text: lines.join('\n') };
}
const fresh = (world, ...more) => ['--out', world.out, ...more];
const resume = (world, from, raw) => ['--from', from, '--raw', raw ?? world.out];

test('The_wrapper_runs_v1s_run_then_sanitise_and_verdicts_on_a_staging_folder_and_renames_it_into_place_as_the_last_step', async () => {
  const world = pinnedWorld();
  const smoke = fakeSmoke({ effects: { ...leaves, verdicts: argv => { assert.equal(existsSync(world.fixture), false, 'no fixture folder while the steps are running'); leaves.verdicts(argv); } } });
  const { code, text } = await runWrapper(world, fresh(world), smoke.fn);
  assert.equal(code, 0, text);
  assert.deepEqual(smoke.calls.map(c => c.argv), [['run', '--out', world.out], ['sanitise', '--from', world.out, '--to', world.staging], ['verdicts', '--fixture', world.staging]]);
  assert.deepEqual(readdirSync(world.fixture).sort(), ['SMOKE.md', 'meta.json'], 'the fixture folder is the staging folder, renamed');
  assert.equal(existsSync(world.staging), false);
  assert.equal(path.basename(world.fixture), `claude-${VERSION}-bare-smoke`, 'the name is the one B-T1 looks the fixture up by');
  assert.match(smoke.calls[2].opts.command, /smoke-pinned\.mjs --out /, 'SMOKE.md names the command that was run, not v1\'s');
});

test('The_binary_v1_is_told_to_run_is_the_verified_copys_absolute_path_with_the_pinned_version_and_never_one_from_PATH', async () => {
  const world = pinnedWorld();
  const smoke = succeeding();
  await runWrapper(world, fresh(world), smoke.fn);
  const { pin } = smoke.calls[0].opts;
  assert.deepEqual(pin, { claude: world.binary, cliPin: VERSION });
  assert.equal(path.isAbsolute(pin.claude), true);
  const decoy = binaryNamed('claude');
  const second = pinnedWorld();
  const was = process.env.PATH;
  process.env.PATH = `${path.dirname(decoy)}${path.delimiter}${was}`;
  try {
    const dry = await runWrapper(second, fresh(second, '--dry-run'));
    const planned = JSON.parse(dry.lines.find(l => l.startsWith('{')));
    assert.equal(planned.claude, realpathSync(second.binary), 'v1\'s own plan names the verified copy, though another claude is first on PATH');
    assert.notEqual(planned.claude, realpathSync(decoy));
    assert.equal(planned.pins.claude, VERSION);
  } finally {
    process.env.PATH = was;
  }
});

test('The_wrapper_refuses_a_copy_whose_sha256_differs_from_the_pin_and_runs_nothing', async () => {
  const world = pinnedWorld({ pinnedSha: sha('another file') });
  const smoke = fakeSmoke();
  const { code, text } = await runWrapper(world, fresh(world), smoke.fn);
  assert.equal(code, 3);
  assert.match(text, /pinned CLI copy .* hashes to [0-9a-f]{12}, the pin says [0-9a-f]{12}/);
  assert.deepEqual(smoke.calls, [], 'not even v1\'s dry run was asked for');
});

test('A_dry_run_runs_every_check_of_the_copy_so_a_bad_sha_a_missing_copy_a_wrong_mode_and_a_bad_version_are_each_refused_in_it', async () => {
  const worlds = [pinnedWorld({ pinnedSha: sha('x') }), pinnedWorld({ copy: false }), pinnedWorld(), pinnedWorld({ version: '2.1.329/../../escape' })];
  chmodSync(worlds[2].binary, 0o755);
  const expected = [/hashes to/, /is missing/, /is not mode 0555/, /cli\.version must be x\.y\.z/];
  for (const [i, world] of worlds.entries()) {
    const smoke = fakeSmoke();
    const { code, text } = await runWrapper(world, fresh(world, '--dry-run'), smoke.fn);
    assert.equal(code, 3, `check ${i}`);
    assert.match(text, expected[i]);
    assert.deepEqual(smoke.calls, [], `check ${i}: v1 was not asked for anything`);
  }
});

test('The_wrapper_refuses_an_out_dir_inside_a_git_tree_through_v1s_own_check_and_writes_nothing', async () => {
  const world = pinnedWorld();
  const inside = { ...world, out: path.join(REPO, 'testsToDelete-pinned-smoke-out') };
  const { code, text } = await runWrapper(inside, fresh(inside));
  assert.equal(code, 3, text);
  assert.match(text, /is inside the git tree/);
  assert.equal(existsSync(inside.out), false, 'no session directory was made inside the repository');
  assert.equal(existsSync(world.fixture), false);
});

test('A_dry_run_asks_v1_for_its_dry_run_only_and_sanitises_and_writes_nothing', async () => {
  const world = pinnedWorld();
  const smoke = fakeSmoke();
  const { code, text } = await runWrapper(world, fresh(world, '--dry-run'), smoke.fn);
  assert.equal(code, 0, text);
  assert.deepEqual(smoke.calls.map(c => c.argv), [['run', '--out', world.out, '--dry-run']]);
  assert.match(text, /nothing was launched/);
});

test('A_dry_run_does_not_refuse_an_existing_fixture_folder_it_only_says_the_folder_exists_and_leaves_it_as_it_is', async () => {
  const world = pinnedWorld();
  mkdirSync(world.fixture, { recursive: true });
  writeFileSync(path.join(world.fixture, 'meta.json'), '{}\n');
  const before = treeOf(world.fixture);
  const smoke = fakeSmoke();
  const fake = await runWrapper(world, fresh(world, '--dry-run'), smoke.fn);
  assert.equal(fake.code, 0, fake.text);
  assert.match(fake.text, /fixture folder .* exists and is not empty: a real run would refuse it/);
  assert.deepEqual(smoke.calls.map(c => c.argv), [['run', '--out', world.out, '--dry-run']]);
  const real = await runWrapper(world, fresh(world, '--dry-run'));
  assert.equal(real.code, 0, `through v1's own dry run: ${real.text}`);
  assert.match(real.text, /a real run would refuse it/);
  assert.deepEqual(treeOf(world.fixture), before);
  assert.equal(existsSync(world.out), false, 'a dry run claims no session directory');
});

test('A_dry_run_through_v1_plans_the_pinned_version_and_binary_and_makes_no_session_directory', async () => {
  const world = pinnedWorld();
  const { code, text, lines } = await runWrapper(world, fresh(world, '--dry-run'));
  assert.equal(code, 0, text);
  const planned = JSON.parse(lines.find(l => l.startsWith('{')));
  assert.deepEqual([planned.pins.claude, planned.claude], [VERSION, world.binary]);
  assert.equal(existsSync(world.out), false, 'v1 claimed no directory');
  assert.equal(existsSync(world.fixtures), false, 'no fixture folder was made');
});

test('A_dry_run_reads_the_token_as_v1s_dry_run_does_so_a_home_without_one_is_refused_by_v1', async () => {
  const world = pinnedWorld();
  rmSync(path.join(world.home, ...TOKEN_FILE_PARTS));
  const { code, text } = await runWrapper(world, fresh(world, '--dry-run'));
  assert.equal(code, 3);
  assert.match(text, /no token/);
});

test('A_session_that_fails_stops_the_wrapper_before_it_sanitises_and_says_where_the_artefacts_are', async () => {
  const world = pinnedWorld();
  const smoke = fakeSmoke({ codes: { run: 5 } });
  const { code, text } = await runWrapper(world, fresh(world), smoke.fn);
  assert.equal(code, 5);
  assert.deepEqual(smoke.calls.map(c => c.argv[0]), ['run']);
  assert.match(text, new RegExp(`stopped after run.*${world.out.replaceAll('.', '\\.')}`));
});

test('A_sanitise_that_fails_leaves_no_fixture_folder_and_no_staging_folder_the_raw_capture_untouched_and_the_resume_line_in_the_message', async () => {
  const world = pinnedWorld();
  const smoke = fakeSmoke({ effects: leaves, codes: { sanitise: 5 } });
  const { code, text } = await runWrapper(world, fresh(world), smoke.fn);
  const raw = treeOf(world.out);
  assert.equal(code, 5);
  assert.deepEqual(smoke.calls.map(c => c.argv[0]), ['run', 'sanitise'], 'verdicts never ran');
  assert.deepEqual([existsSync(world.fixture), existsSync(world.staging)], [false, false]);
  assert.deepEqual(raw.map(l => l.split(' ')[0]).sort(), ['meta.json', 'stream.ndjson'], 'the raw capture is there');
  assert.match(text, new RegExp(`--from sanitise --raw ${world.out.replaceAll('.', '\\.')}`), 'the message names the way on, which needs no second session');
});

test('A_verdicts_that_fails_leaves_no_fixture_folder_keeps_the_sanitised_staging_and_names_the_verdicts_resume', async () => {
  const world = pinnedWorld();
  const smoke = fakeSmoke({ effects: leaves, codes: { verdicts: 5 } });
  const { code, text } = await runWrapper(world, fresh(world), smoke.fn);
  assert.equal(code, 5);
  assert.deepEqual([existsSync(world.fixture), existsSync(world.staging)], [false, true], 'the fixture folder is never partial; the sanitised copy waits for the redo');
  assert.match(text, new RegExp(`--from verdicts --raw ${world.out.replaceAll('.', '\\.')}`));
});

test('A_resume_from_sanitise_redoes_sanitise_and_verdicts_from_the_raw_capture_removes_a_stale_staging_folder_and_launches_no_session', async () => {
  const world = pinnedWorld();
  rawCapture(world.out);
  mkdirSync(world.staging, { recursive: true });
  writeFileSync(path.join(world.staging, 'half.txt'), 'left by a kill\n');
  const smoke = fakeSmoke({ effects: { ...leaves, sanitise: argv => { assert.equal(existsSync(world.staging), false, 'the stale staging folder was removed first'); leaves.sanitise(argv); } } });
  const { code, text } = await runWrapper(world, resume(world, 'sanitise'), smoke.fn);
  assert.equal(code, 0, text);
  assert.deepEqual(smoke.calls.map(c => c.argv), [['sanitise', '--from', world.out, '--to', world.staging], ['verdicts', '--fixture', world.staging]], 'no run verb: nothing is launched');
  assert.deepEqual(readdirSync(world.fixture).sort(), ['SMOKE.md', 'meta.json']);
  assert.equal(existsSync(world.staging), false);
});

test('A_resume_from_verdicts_redoes_only_the_verdicts_on_the_kept_staging_folder_and_refuses_when_there_is_none', async () => {
  const world = pinnedWorld();
  rawCapture(world.out);
  const none = fakeSmoke();
  const refused = await runWrapper(world, resume(world, 'verdicts'), none.fn);
  assert.equal(refused.code, 3);
  assert.match(refused.text, /no sanitised copy .*--from sanitise/);
  assert.deepEqual(none.calls, []);
  leaves.sanitise(['sanitise', '--from', world.out, '--to', world.staging]);
  const smoke = succeeding();
  const done = await runWrapper(world, resume(world, 'verdicts'), smoke.fn);
  assert.equal(done.code, 0, done.text);
  assert.deepEqual(smoke.calls.map(c => c.argv), [['verdicts', '--fixture', world.staging]]);
  assert.deepEqual(readdirSync(world.fixture).sort(), ['SMOKE.md', 'meta.json']);
});

test('A_resume_refuses_a_raw_dir_that_holds_no_completed_run_of_the_pinned_CLI_and_names_it', async () => {
  const world = pinnedWorld();
  const cases = [
    [path.join(tmpDir('raw-none'), 'r1'), /holds no readable meta\.json/],
    [rawCapture(path.join(tmpDir('raw-failed'), 'r1'), { outcome: { ok: false, label: 'stopped' } }), /outcome is 'stopped', not ok/],
    [rawCapture(path.join(tmpDir('raw-other'), 'r1'), { cliVersion: '2.1.310' }), /captured on CLI 2\.1\.310, the pin is 2\.1\.329/],
  ];
  for (const [raw, why] of cases) {
    const smoke = fakeSmoke();
    const { code, text } = await runWrapper(world, resume(world, 'sanitise', raw), smoke.fn);
    assert.equal(code, 3, raw);
    assert.match(text, why);
    assert.ok(text.includes(raw), 'the sentence names the raw dir');
    assert.deepEqual(smoke.calls, [], 'nothing was asked of v1');
  }
});

test('A_resume_refuses_a_fixture_folder_that_is_not_empty_and_a_fresh_run_does_too_before_the_paid_session', async () => {
  const world = pinnedWorld();
  rawCapture(world.out);
  mkdirSync(world.fixture, { recursive: true });
  writeFileSync(path.join(world.fixture, 'meta.json'), '{}\n');
  for (const flags of [fresh(world), resume(world, 'sanitise')]) {
    const smoke = fakeSmoke();
    const { code, text } = await runWrapper(world, flags, smoke.fn);
    assert.equal(code, 3, `with [${flags}]`);
    assert.match(text, /is not empty: a fixture is never overwritten/);
    assert.deepEqual(smoke.calls, []);
  }
});

test('The_wrapper_answers_a_usage_error_to_each_flag_set_that_is_not_a_run_or_a_resume', async () => {
  const world = pinnedWorld();
  const raw = rawCapture(path.join(tmpDir('usage-raw'), 'r1'));
  const sets = [[], ['--nope'], fresh(world, '--check'), [...fresh(world), '--from', 'bogus'], [...fresh(world), '--raw', raw], [...resume(world, 'sanitise', raw), '--out', world.out], [...resume(world, 'verdicts', raw), '--dry-run'], ['--from', 'sanitise']];
  for (const flags of sets) {
    const smoke = fakeSmoke();
    const { code } = await runWrapper(world, flags, smoke.fn);
    assert.equal(code, 4, `with [${flags}]`);
    assert.deepEqual(smoke.calls, []);
  }
});

/** The stub capture of one whole stub session on 2.1.329 (the real box, the stub CLI), made once for the tests that need a real raw dir; null where the host cannot run the box. */
let stubbed;
async function stubCapture(t) {
  if (!sandboxGate(t)) return null;
  stubbed ??= await stubSmokeSession({ version: VERSION });
  assert.equal(stubbed.r.ok && stubbed.r.value.result.ok, true, stubbed.r.reason);
  return { ...stubbed, raw: stubbed.r.value.layout.dir };
}

test('A_fixture_made_through_the_wrappers_own_steps_from_a_stub_capture_is_G_4_evidence_that_names_the_given_CLI_and_gives_B_T1_a_verdict', async t => {
  const session = await stubCapture(t);
  if (session === null) return;
  const world = pinnedWorld({ token: session.token });
  const { code, text } = await runWrapper(world, resume(world, 'sanitise', session.raw));
  assert.equal(code, 0, text);
  assert.equal(existsSync(world.staging), false, 'the staging folder was renamed into place');
  const loaded = loadSmoke(world.fixture);
  assert.equal(loaded.ok, true, loaded.reason);
  const policy = crosscheckPolicyFrom(spikeDir, {});
  const rows = computeSmokeVerdicts(loaded.value, policy.value);
  const b = rows.find(r => r.id === 'B-T1');
  assert.ok(VERDICTS.includes(b.verdict), `B-T1: ${b.verdict}`);
  const evidence = smokeEvidence({ fixtureDir: world.fixture, compute: dir => computeSmokeVerdicts(loadSmoke(dir).value, policy.value) });
  assert.equal(evidence.cli, VERSION, 'the evidence names the CLI the fixture was captured on');
  assert.equal(evidence.fixture, `test/fixtures/real/claude-${VERSION}-bare-smoke`);
  assert.equal(evidence.ok, b.verdict === 'holds');
  assert.equal(evidence.observed, b.observed);
  assert.equal(verdictBlockOf(readFileSync(path.join(world.fixture, 'SMOKE.md'), 'utf8')), renderVerdicts(rows), 'SMOKE.md is the computed table, byte for byte');
});

test('A_real_sanitise_that_fails_leaves_no_fixture_folder_and_the_raw_capture_untouched_and_a_resume_from_the_whole_raw_capture_then_completes', async t => {
  const session = await stubCapture(t);
  if (session === null) return;
  const world = pinnedWorld({ token: session.token });
  const damaged = path.join(tmpDir('raw-damaged'), 'r1');
  cpSync(session.raw, damaged, { recursive: true });
  rmSync(path.join(damaged, 'broker.ndjson'));
  const before = treeOf(damaged);
  const failed = await runWrapper(world, resume(world, 'sanitise', damaged));
  assert.equal(failed.code, 5, failed.text);
  assert.match(failed.text, /lacks broker\.ndjson/);
  assert.deepEqual([existsSync(world.fixture), existsSync(world.staging)], [false, false]);
  assert.deepEqual(treeOf(damaged), before, 'the raw capture was not touched');
  const again = await runWrapper(world, resume(world, 'sanitise', session.raw));
  assert.equal(again.code, 0, again.text);
  assert.equal(existsSync(path.join(world.fixture, 'SMOKE.md')), true);
});

test('The_fixture_folder_is_the_one_v1_keeps_its_real_fixtures_in', () => {
  assert.equal(FIXTURES_DIR, path.join(REPO, 'spikes', 'minigit-bench', 'test', 'fixtures', 'real'));
  assert.equal(existsSync(path.join(FIXTURES_DIR, fixtureNameOf('2.1.310'))), true, 'the one fixture that is committed is found by the same name');
});
