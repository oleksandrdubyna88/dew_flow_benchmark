// F1.S3 (b), T-L62: a package's lifecycle script cannot change the build (D65). With the Node templates' `.npmrc` (`ignore-scripts=true`, `save-exact=true`), a `sed` on `tsconfig.json` run as `preinstall`, `install`,
// `postinstall` or `prepare` leaves the frozen file as it is, and the type error beside it (TS2322) fails the build. The control without `ignore-scripts` exits 0 with the file changed, which is why the key is there.
// The real Node templates, the build box and the registered Node snapshot; the cases skip with their reason without them (BENCH_REQUIRE_SANDBOX=1 makes a missing tool a failure).
import assert from 'node:assert/strict';
import { readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { runBounded } from '../../minigit-bench/lib/run.mjs';
import { NPMRC_OFFLINE } from '../lib/cache-offline.mjs';
import { lockProblems, NPMRC_WORKSPACE } from '../lib/workspace-build.mjs';
import { needs } from './support/exec-layer-host.mjs';
import { KINDS, helloWorkspace, inBox, outputOf, readJson, readTemplate, snapshotRecord, snapshotsMissing } from './support/workspace-fixtures.mjs';

const LIFECYCLES = Object.freeze(['preinstall', 'install', 'postinstall', 'prepare']);
const TYPE_ERROR = "const s: string = null;\nconsole.log(s);\n";
const STRICT_OFF = "sed -i 's/\"strict\": true/\"strict\": false/' tsconfig.json";
const TIMEOUT = { timeout: 15 * 60_000 };
const SPIKE = fileURLToPath(new URL('..', import.meta.url));
const fileOf = (w, name) => path.join(w, name);

/** A Node workspace of `kind` whose program has a type error and whose `package.json` runs `STRICT_OFF` as `lifecycle`; `npmrc` replaces the template's `.npmrc` when given. */
function workspaceWithScript(t, { kind, lifecycle, npmrc, label }) {
  const w = helloWorkspace(t, { stack: 'node', kind, label });
  const pkg = JSON.parse(readFileSync(fileOf(w, 'package.json'), 'utf8'));
  writeFileSync(fileOf(w, 'package.json'), `${JSON.stringify({ ...pkg, scripts: { [lifecycle]: STRICT_OFF } }, null, 2)}\n`);
  writeFileSync(path.join(w, 'src', 'main.ts'), TYPE_ERROR);
  if (npmrc !== undefined) writeFileSync(fileOf(w, '.npmrc'), npmrc);
  return w;
}

test('A_lifecycle_script_that_rewrites_a_frozen_file_leaves_it_unchanged_and_the_type_error_fails_the_build', TIMEOUT, async t => {
  if (!needs(t, snapshotsMissing())) return;
  for (const lifecycle of LIFECYCLES) {
    const w = workspaceWithScript(t, { kind: 'olympiad', lifecycle, label: `sc${lifecycle.slice(0, 3)}` });
    const before = readFileSync(fileOf(w, 'tsconfig.json'));
    assert.equal(before.equals(Buffer.from(readTemplate('node', 'olympiad', 'tsconfig.json'))), true, `${lifecycle}: the frozen file is in place at the start`);
    const ran = await inBox(w, 'node', ['bash', 'build.sh']);
    assert.notEqual(ran.code, 0, `${lifecycle}: the type error fails the build: ${outputOf(ran).slice(-1500)}`);
    assert.match(outputOf(ran), /TS2322/, `${lifecycle}: and it is the type error that fails it`);
    assert.equal(readFileSync(fileOf(w, 'tsconfig.json')).equals(before), true, `${lifecycle}: tsconfig.json is byte for byte unchanged`);
  }
});

test('The_control_without_ignore_scripts_still_exits_0_and_that_is_the_point', TIMEOUT, async t => {
  if (!needs(t, snapshotsMissing())) return;
  for (const lifecycle of LIFECYCLES) {
    const w = workspaceWithScript(t, { kind: 'olympiad', lifecycle, npmrc: `${NPMRC_OFFLINE}update-notifier=false\n`, label: `ct${lifecycle.slice(0, 3)}` });
    const before = readFileSync(fileOf(w, 'tsconfig.json'));
    assert.equal(before.equals(Buffer.from(readTemplate('node', 'olympiad', 'tsconfig.json'))), true, `${lifecycle}: the frozen file is in place at the start`);
    const ran = await inBox(w, 'node', ['bash', 'build.sh']);
    assert.equal(ran.code, 0, `${lifecycle}: without the key the script runs, strict is off, and the build passes: ${outputOf(ran).slice(-1500)}`);
    assert.equal(readFileSync(fileOf(w, 'tsconfig.json')).equals(before), false, `${lifecycle}: the frozen file was rewritten`);
  }
});

test('No_lock_entry_of_the_templates_or_the_offered_set_has_an_install_script', () => {
  const offered = readJson('inputs/cache-recipe-lang.json').stacks.node.packages.filter(p => p.kinds.includes('web')).map(p => p.name);
  const lockOf = kind => JSON.parse(readTemplate('node', kind, 'package-lock.json'));
  for (const kind of KINDS) {
    const entries = Object.entries(lockOf(kind).packages);
    assert.deepEqual(entries.filter(([, entry]) => entry.hasInstallScript === true).map(([key]) => key), [], `node/${kind}: no entry has an install script`);
    assert.deepEqual(lockProblems(lockOf(kind), snapshotRecord().node.packages, JSON.parse(readTemplate('node', kind, 'package.json'))), [], `node/${kind}: the lock is inside the manifest`);
  }
  const web = Object.keys(lockOf('web').packages).map(key => key.replace(/^.*node_modules\//, ''));
  assert.deepEqual(offered.filter(name => !web.includes(name)), [], 'the web lock holds the whole offered set, so the check above covers it');
  const planted = lockOf('web');
  planted.packages['node_modules/fastify'].hasInstallScript = true;
  assert.ok(lockProblems(planted, snapshotRecord().node.packages, JSON.parse(readTemplate('node', 'web', 'package.json'))).some(p => /install script/.test(p)), 'the check can fail');
});

test('A_plain_npm_install_of_a_listed_package_writes_an_exact_version', TIMEOUT, async t => {
  if (!needs(t, snapshotsMissing())) return;
  const version = snapshotRecord().node.resolved.PINO;
  const installed = async (label, npmrc) => {
    const w = helloWorkspace(t, { stack: 'node', kind: 'olympiad', label });
    if (npmrc !== undefined) writeFileSync(fileOf(w, '.npmrc'), npmrc);
    const ran = await inBox(w, 'node', ['npm', 'install', `pino@${version}`, '--no-audit', '--no-fund']);
    assert.equal(ran.code, 0, outputOf(ran).slice(-1500));
    return JSON.parse(readFileSync(fileOf(w, 'package.json'), 'utf8')).dependencies.pino;
  };
  assert.equal(await installed('exact'), version, 'under the template\'s .npmrc the version is written exact');
  assert.equal(await installed('range', `${NPMRC_OFFLINE}update-notifier=false\n`), `^${version}`, 'control: without save-exact npm writes a range');
});

test('All_three_kinds_still_pass_npm_ci_offline_tsc_eslint_and_npx_offline', TIMEOUT, async t => {
  if (!needs(t, snapshotsMissing())) return;
  assert.equal(NPMRC_WORKSPACE.includes('ignore-scripts=true'), true);
  for (const kind of KINDS) {
    const w = helloWorkspace(t, { stack: 'node', kind, label: `ok${kind.slice(0, 3)}` });
    const built = await inBox(w, 'node', ['bash', 'build.sh']);
    assert.equal(built.code, 0, `${kind}: npm ci --offline and tsc: ${outputOf(built).slice(-1500)}`);
    const linted = await inBox(w, 'node', ['npx', '--offline', 'eslint', 'src']);
    assert.equal(linted.code, 0, `${kind}: ESLint: ${outputOf(linted).slice(-1500)}`);
    const version = await inBox(w, 'node', ['npx', '--offline', 'tsc', '--version']);
    assert.equal(version.code, 0, `${kind}: npx --offline: ${outputOf(version).slice(-500)}`);
  }
});

test('A_plain_npm_test_still_runs_its_own_script_though_scripts_are_stopped', TIMEOUT, async t => {
  if (!needs(t, snapshotsMissing())) return;
  const w = helloWorkspace(t, { stack: 'node', kind: 'olympiad', label: 'npmt' });
  const pkg = JSON.parse(readFileSync(fileOf(w, 'package.json'), 'utf8'));
  writeFileSync(fileOf(w, 'package.json'), `${JSON.stringify({ ...pkg, scripts: { test: 'node -e "require(\'node:fs\').writeFileSync(\'ran.txt\', \'yes\')"' } }, null, 2)}\n`);
  const ran = await inBox(w, 'node', ['npm', 'test']);
  assert.equal(ran.code, 0, outputOf(ran).slice(-1500));
  assert.equal(readFileSync(fileOf(w, 'ran.txt'), 'utf8'), 'yes', 'the script ran: ignore-scripts stops the lifecycle scripts, not the command the agent runs');
});

test('The_node_locks_do_not_move', TIMEOUT, async t => {
  if (!needs(t, snapshotsMissing())) return;
  const ran = await runBounded('node', [path.join(SPIKE, 'build-workspaces.mjs'), '--make-locks'], { timeoutMs: 10 * 60_000, env: { PATH: process.env.PATH, HOME: process.env.HOME, XDG_RUNTIME_DIR: process.env.XDG_RUNTIME_DIR ?? '' } });
  assert.equal(ran.ok, true, ran.reason);
  assert.equal(ran.value.code, 0, ran.value.stderr);
  assert.deepEqual(ran.value.stdout.trim().split('\n'), ['olympiad: unchanged', 'daemon: unchanged', 'web: unchanged'], 'the locks were generated with --ignore-scripts, so the key changes none of them');
});
