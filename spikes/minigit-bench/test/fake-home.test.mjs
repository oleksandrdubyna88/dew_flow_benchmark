// N18: the runner's tests run under a fake HOME. The helper (fake-home.mjs) is imported first by every test file that drives the runner, the series loop, the collector or the
// rehearsal; the two files that cannot use it say why and are kept from the one door to the token (`main`, `buildRun`).
import assert from 'node:assert/strict';
import { existsSync, readFileSync, readdirSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { test } from 'node:test';
import { TOKEN_FILE_PARTS } from '../capture/capture-env.mjs';
import { readSecretFile } from '../lib/secrets.mjs';
import { fakeHome, realHome } from './fake-home.mjs';
import { spikeDir } from './helpers.mjs';

const TEST_DIR = path.join(spikeDir, 'test');
/** The modules a runner test drives: importing one of them is what makes a file a runner test. */
const RUNNER_MODULES = ['bare-runner', 'lib/bare-config', 'lib/bare-series', 'lib/rehearsal', 'lib/bare-collect', 'lib/bare-phase', 'lib/bare-init'];
/** The two ways into the real token: `main` of the CLI and `buildRun` of the wiring. */
const TOKEN_DOORS = ['bare-runner', 'lib/bare-config'];
/** Files that keep the REAL home on purpose: the held-out key and the decrypted suite live under it by design, and they build their own context with a fake token. */
const REAL_HOME_ON_PURPOSE = Object.freeze({
  'bare-e2e.test.mjs': 'the real scorer decrypts the held-out suite under the real home',
  'rehearse-v2-e2e.test.mjs': 'the real scorer decrypts the held-out suite under the real home',
});

const importsOf = text => [...text.matchAll(/^import\s+(?:[^;]*?\sfrom\s+)?'([^']+)'(?:;\s*)?(?:\/\/.*)?$/gm)].map(m => m[1]);
const uses = (imports, mods) => imports.some(i => mods.some(m => i === `../${m}.mjs`));
const testFiles = () => readdirSync(TEST_DIR).filter(f => f.endsWith('.test.mjs'));

test('The_helper_gives_the_process_a_fake_HOME_and_XDG_CONFIG_HOME_that_hold_no_secret', () => {
  assert.notEqual(os.homedir(), realHome);
  assert.equal(os.homedir(), fakeHome);
  assert.equal(process.env.HOME, fakeHome);
  assert.equal(process.env.XDG_CONFIG_HOME.startsWith(fakeHome + path.sep), true);
  assert.match(path.basename(fakeHome), /^testsToDelete-/, 'common.test-leftovers');
  assert.equal(existsSync(path.join(fakeHome, ...TOKEN_FILE_PARTS)), false, 'no token file in the fake home');
  assert.equal(readSecretFile({ home: os.homedir(), parts: TOKEN_FILE_PARTS, label: 'token' }).ok, false, 'a runner that asks for the token of its HOME finds none, and is not refused');
  assert.equal(readdirSync(path.join(fakeHome, '.config')).length, 0, 'an empty config directory');
});

test('Every_test_file_that_drives_the_runner_imports_the_fake_home_before_any_module_of_the_spike', () => {
  const runnerTests = testFiles().filter(f => uses(importsOf(readFileSync(path.join(TEST_DIR, f), 'utf8')), RUNNER_MODULES));
  assert.ok(runnerTests.length >= 7, `${runnerTests.join(', ')}`);
  const wrong = runnerTests.filter(f => {
    const imports = importsOf(readFileSync(path.join(TEST_DIR, f), 'utf8'));
    if (f in REAL_HOME_ON_PURPOSE) return false;
    const firstRelative = imports.find(i => i.startsWith('.'));
    return firstRelative !== './fake-home.mjs';
  });
  assert.deepEqual(wrong, [], 'import "./fake-home.mjs" as the first relative import: bare-runner.mjs reads os.homedir() when it is loaded');
});

test('The_files_that_keep_the_real_home_on_purpose_cannot_reach_main_or_buildRun', () => {
  for (const [file, why] of Object.entries(REAL_HOME_ON_PURPOSE)) {
    assert.ok(why.length > 10);
    const path_ = path.join(TEST_DIR, file);
    assert.equal(existsSync(path_), true, `${file} exists`);
    const imports = importsOf(readFileSync(path_, 'utf8'));
    assert.equal(uses(imports, TOKEN_DOORS), false, `${file} must not import the CLI or the wiring: it holds the real home, so it must have no road to the token reader`);
    assert.equal(imports.includes('./fake-home.mjs'), false, `${file} keeps the real home: ${why}`);
  }
});
