// F3 PR 5c: the smoke on the PINNED CLI (`prereg/smoke-pinned.mjs`). The wrapper checks the pinned copy's sha256, then runs v1's smoke, sanitise and verdicts on that binary and version.
// Everything runs on fakes: a stand-in for the CLI copy, a fake home with a fake token, a fake of v1's `main` where a test reads what the wrapper asked of it, and v1's own `main` only where
// the subject is a check that is v1's (the out dir, the dry run). No real CLI, network or token is used and nothing is paid.
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { chmodSync, existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { fakeToken } from '../../minigit-bench/test/broker-stub.mjs';
import { homeWithToken } from '../../minigit-bench/test/smoke-stub.mjs';
import { tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { loadExplore } from '../lib/inputs.mjs';
import { INPUTS, REPO } from '../lib/paths.mjs';
import { FIXTURES_DIR, fixtureNameOf, main } from '../prereg/smoke-pinned.mjs';

const VERSION = '2.1.329';
const COPY_BYTES = '#!/bin/sh\necho 2.1.329 (stand-in for the pinned CLI)\n';
const sha = text => createHash('sha256').update(text).digest('hex');

/**
 * A home with a fake token and the pinned copy where `explore.cli.pathParts` says, and an `explore.json` that is the registered one with its `cli` entry replaced: the fixture is built against the real
 * validator (`loadExplore`), so a shape the wrapper would decline cannot make a test pass for the wrong reason.
 */
function pinnedWorld({ version = VERSION, bytes = COPY_BYTES, pinnedSha = sha(COPY_BYTES), copy = true } = {}) {
  const home = homeWithToken(fakeToken(), 'pinned-home');
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
  return { home, explore, binary, fixtures: path.join(tmpDir('pinned-fixtures'), 'real'), out: path.join(tmpDir('pinned-out'), 'r1') };
}

/** A fake of v1's `main` that records each call and answers the code the test chose for a verb (0 when it chose none). */
function fakeSmoke(codes = {}) {
  const calls = [];
  return { calls, fn: async (argv, opts) => { calls.push({ argv, opts }); return codes[argv[0]] ?? 0; } };
}

/** The wrapper run over a world: `{ code, lines }`. `smoke` is the fake or v1's own `main` (the default). */
async function runWrapper(world, extra = [], { smoke, withOut = true } = {}) {
  const lines = [];
  const argv = [...(withOut ? ['--out', world.out] : []), '--explore', world.explore, ...extra];
  const code = await main(argv, line => lines.push(line), { home: world.home, fixturesDir: world.fixtures, ...(smoke ? { smoke } : {}) });
  return { code, lines };
}

test('The_wrapper_runs_v1s_run_sanitise_and_verdicts_in_order_on_the_pinned_copy_and_names_the_fixture_after_its_version', async () => {
  const world = pinnedWorld();
  const smoke = fakeSmoke();
  const { code, lines } = await runWrapper(world, [], { smoke: smoke.fn });
  const fixture = path.join(world.fixtures, `claude-${VERSION}-bare-smoke`);
  assert.equal(code, 0, lines.join('\n'));
  assert.deepEqual(smoke.calls.map(c => c.argv), [['run', '--out', world.out], ['sanitise', '--from', world.out, '--to', fixture], ['verdicts', '--fixture', fixture]]);
  assert.deepEqual(smoke.calls[0].opts.pin, { claude: world.binary, cliPin: VERSION }, 'v1 runs the pinned copy and checks it against the pinned version');
  assert.equal(smoke.calls[0].opts.home, world.home);
  assert.match(smoke.calls[2].opts.command, /smoke-pinned\.mjs --out /, 'SMOKE.md names the command that was run, not v1\'s');
  assert.equal(fixtureNameOf(VERSION), `claude-${VERSION}-bare-smoke`, 'the name is the one B-T1 looks the fixture up by');
});

test('The_wrapper_refuses_a_copy_whose_sha256_differs_from_the_pin_and_runs_nothing', async () => {
  const world = pinnedWorld({ pinnedSha: sha('another file') });
  const smoke = fakeSmoke();
  const { code, lines } = await runWrapper(world, [], { smoke: smoke.fn });
  assert.equal(code, 3);
  assert.match(lines.join('\n'), /pinned CLI copy .* hashes to [0-9a-f]{12}, the pin says [0-9a-f]{12}/);
  assert.deepEqual(smoke.calls, [], 'not even v1\'s dry run was asked for');
});

test('The_wrapper_refuses_when_the_pinned_copy_is_not_there_and_runs_nothing', async () => {
  const world = pinnedWorld({ copy: false });
  const smoke = fakeSmoke();
  const { code, lines } = await runWrapper(world, [], { smoke: smoke.fn });
  assert.equal(code, 3);
  assert.match(lines.join('\n'), /pinned CLI copy .* is missing/);
  assert.deepEqual(smoke.calls, []);
});

test('The_wrapper_refuses_a_version_that_is_not_x_y_z_before_it_becomes_a_folder_name', async () => {
  const world = pinnedWorld({ version: '2.1.329/../../escape' });
  const smoke = fakeSmoke();
  const { code, lines } = await runWrapper(world, [], { smoke: smoke.fn });
  assert.equal(code, 3);
  assert.match(lines.join('\n'), /cli\.version must be x\.y\.z/);
  assert.deepEqual(smoke.calls, []);
});

test('The_wrapper_refuses_an_out_dir_inside_a_git_tree_through_v1s_own_check_and_writes_nothing', async () => {
  const world = pinnedWorld();
  const inside = { ...world, out: path.join(REPO, 'testsToDelete-pinned-smoke-out') };
  const { code, lines } = await runWrapper(inside);
  assert.equal(code, 3, lines.join('\n'));
  assert.match(lines.join('\n'), /is inside the git tree/);
  assert.equal(existsSync(inside.out), false, 'no session directory was made inside the repository');
  assert.equal(existsSync(path.join(world.fixtures, fixtureNameOf(VERSION))), false);
});

test('A_dry_run_asks_v1_for_its_dry_run_only_and_sanitises_and_writes_nothing', async () => {
  const world = pinnedWorld();
  const smoke = fakeSmoke();
  const { code, lines } = await runWrapper(world, ['--dry-run'], { smoke: smoke.fn });
  assert.equal(code, 0, lines.join('\n'));
  assert.deepEqual(smoke.calls.map(c => c.argv), [['run', '--out', world.out, '--dry-run']]);
  assert.match(lines.join('\n'), /nothing was launched/);
});

test('A_dry_run_through_v1_plans_the_pinned_version_and_binary_and_makes_no_session_directory', async () => {
  const world = pinnedWorld();
  const { code, lines } = await runWrapper(world, ['--dry-run']);
  assert.equal(code, 0, lines.join('\n'));
  const planned = JSON.parse(lines.find(l => l.startsWith('{')));
  assert.deepEqual([planned.pins.claude, planned.claude], [VERSION, world.binary]);
  assert.equal(existsSync(world.out), false, 'v1 claimed no directory');
  assert.equal(existsSync(world.fixtures), false, 'no fixture folder was made');
});

test('A_session_that_fails_stops_the_wrapper_before_it_sanitises_and_says_where_the_artefacts_are', async () => {
  const world = pinnedWorld();
  const smoke = fakeSmoke({ run: 5 });
  const { code, lines } = await runWrapper(world, [], { smoke: smoke.fn });
  assert.equal(code, 5);
  assert.deepEqual(smoke.calls.map(c => c.argv[0]), ['run']);
  assert.match(lines.join('\n'), new RegExp(`stopped after run.*${world.out.replaceAll('.', '\\.')}`));
});

test('A_fixture_folder_that_is_not_empty_refuses_before_the_paid_session_and_a_dry_run_says_so_too', async () => {
  const world = pinnedWorld();
  const fixture = path.join(world.fixtures, fixtureNameOf(VERSION));
  mkdirSync(fixture, { recursive: true });
  writeFileSync(path.join(fixture, 'meta.json'), '{}\n');
  for (const extra of [[], ['--dry-run']]) {
    const smoke = fakeSmoke();
    const { code, lines } = await runWrapper(world, extra, { smoke: smoke.fn });
    assert.equal(code, 3, `with [${extra}]`);
    assert.match(lines.join('\n'), /is not empty: a fixture is never overwritten/);
    assert.deepEqual(smoke.calls, []);
  }
});

test('The_wrapper_answers_a_usage_error_to_a_missing_out_an_unknown_flag_and_a_check_it_does_not_have', async () => {
  const world = pinnedWorld();
  for (const [extra, withOut] of [[[], false], [['--nope'], true], [['--check'], true]]) {
    const smoke = fakeSmoke();
    const { code } = await runWrapper(world, extra, { smoke: smoke.fn, withOut });
    assert.equal(code, 4, `with [${extra}] and out ${withOut}`);
    assert.deepEqual(smoke.calls, []);
  }
});

test('The_fixture_folder_is_the_one_v1_keeps_its_real_fixtures_in', () => {
  assert.equal(FIXTURES_DIR, path.join(REPO, 'spikes', 'minigit-bench', 'test', 'fixtures', 'real'));
  assert.equal(existsSync(path.join(FIXTURES_DIR, fixtureNameOf('2.1.310'))), true, 'the one fixture that is committed is found by the same name');
});
