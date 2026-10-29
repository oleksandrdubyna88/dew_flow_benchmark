import assert from 'node:assert/strict';
import { cpSync, existsSync, mkdirSync, mkdtempSync, readFileSync, readdirSync, rmSync, writeFileSync } from 'node:fs';
import net from 'node:net';
import os from 'node:os';
import path from 'node:path';
import { test } from 'node:test';
import { bind, devProc, isolation, roBind, roRoot, scoreBoxArgv, scoreEnv, setenvAll, tmpfs } from '../lib/bwrap.mjs';
import { runBounded } from '../lib/run.mjs';
import { installShim } from '../lib/visible.mjs';
import { sandboxGate, stubTrees, tmpDir } from './helpers.mjs';

const nodeToolchain = path.dirname(path.dirname(process.execPath));
const uid = process.getuid();

/** A box over fresh dirs; `toolchainDirs` defaults to none, so nothing under the operator's home is visible. */
function boxDirs(extra = {}) {
  const base = tmpDir('box');
  const dirs = { copyDir: path.join(base, 'copy'), casesDir: path.join(base, 'cases'), home: path.join(base, 'home'), tmp: path.join(base, 'tmp') };
  for (const d of Object.values(dirs)) mkdirSync(d, { recursive: true });
  return { base, ...dirs, toolchainDirs: [], ...extra };
}

/** Run `command` in the box. Every probe ends in a sentinel, so a box that failed to start can never look like a passing probe. */
async function probe(opts, command, timeoutMs = 20_000) {
  const argv = scoreBoxArgv(opts);
  const r = await runBounded(argv[0], [...argv.slice(1), 'sh', '-c', `${command}\necho BOX-ALIVE`], { timeoutMs });
  assert.equal(r.ok, true, r.reason);
  assert.match(r.value.stdout, /BOX-ALIVE\n$/, `the box did not run the probe: ${r.value.stderr}`);
  return { ...r.value, out: r.value.stdout.replace(/BOX-ALIVE\n$/, '') };
}

/** The same box argv with one `<flag> <path>` pair removed: the control that shows a probe can see what the fix hides. */
function withoutPair(argv, flag, p) {
  const i = argv.findIndex((a, j) => a === flag && argv[j + 1] === p);
  assert.ok(i >= 0, `${flag} ${p} is in the argv`);
  return [...argv.slice(0, i), ...argv.slice(i + 2)];
}
async function probeArgv(argv, command) {
  const r = await runBounded(argv[0], [...argv.slice(1), 'sh', '-c', `${command}\necho BOX-ALIVE`], { timeoutMs: 20_000 });
  assert.equal(r.ok, true, r.reason);
  assert.match(r.value.stdout, /BOX-ALIVE\n$/, `the control box did not run: ${r.value.stderr}`);
  return r.value.stdout.replace(/BOX-ALIVE\n$/, '');
}

test('The_pure_builders_return_exact_argv_fragments', () => {
  assert.deepEqual(roRoot(), ['--ro-bind', '/', '/']);
  assert.deepEqual(tmpfs('/mnt'), ['--tmpfs', '/mnt']);
  assert.deepEqual(bind('/a'), ['--bind', '/a', '/a']);
  assert.deepEqual(roBind('/a'), ['--ro-bind', '/a', '/a']);
  assert.deepEqual(devProc(), ['--dev', '/dev', '--proc', '/proc']);
  assert.deepEqual(isolation(), ['--unshare-net', '--unshare-pid', '--die-with-parent', '--new-session']);
  assert.deepEqual(setenvAll({ B: '2', A: '1' }), ['--clearenv', '--setenv', 'A', '1', '--setenv', 'B', '2']);
});

/** Index of the consecutive fragment in argv; a fragment that is not there is a test failure, never -1. */
function at(argv, ...frag) {
  const i = argv.findIndex((_, k) => frag.every((f, j) => argv[k + j] === f));
  assert.ok(i >= 0, `${frag.join(' ')} is in the argv`);
  return i;
}

test('The_box_hides_the_home_run_tmp_before_it_binds_what_lives_under_them_and_ends_at_the_double_dash', () => {
  const opts = { copyDir: '/work/copy', casesDir: '/work/cases', home: '/work/home', tmp: '/work/tmp', toolchainDirs: [`${os.homedir()}/.nvm/node`],
    nugetCache: '/work/cache-nuget', npmCache: '/work/cache-npm', roFiles: ['/work/copy/test-v1.sh'], exists: () => true };
  const argv = scoreBoxArgv(opts);
  assert.equal(argv[0], 'bwrap');
  assert.equal(argv.at(-1), '--');
  const root = at(argv, '--ro-bind', '/', '/');
  const hides = [os.homedir(), '/mnt', '/run', '/tmp', '/var/tmp'].map(p => at(argv, '--tmpfs', p));
  hides.forEach(i => assert.ok(i > root));
  const lastHide = Math.max(...hides);
  const binds = [at(argv, '--ro-bind', opts.toolchainDirs[0], opts.toolchainDirs[0]), at(argv, '--bind', '/work/copy', '/work/copy'), at(argv, '--bind', '/work/cases', '/work/cases'),
    at(argv, '--bind', '/work/home', '/work/home'), at(argv, '--bind', '/work/tmp', '/work/tmp'), at(argv, '--bind', '/work/cache-nuget', '/work/cache-nuget'),
    at(argv, '--bind', '/work/cache-npm', '/work/cache-npm')];
  binds.forEach(i => assert.ok(i > lastHide, 'every bind comes after the last tmpfs, or the tmpfs would hide it'));
  assert.ok(at(argv, '--ro-bind', '/work/copy/test-v1.sh', '/work/copy/test-v1.sh') > at(argv, '--bind', '/work/copy', '/work/copy'), 'the read-only file overlays the writable copy');
  at(argv, '--unshare-net'); at(argv, '--unshare-pid'); at(argv, '--die-with-parent');
  assert.equal(scoreBoxArgv({ ...opts, exists: () => false }).includes('/var/tmp'), false, 'a directory the host lacks is not mounted over');
  assert.deepEqual(scoreBoxArgv({ ...opts, cwd: '/work/copy' }).slice(-3), ['--chdir', '/work/copy', '--']);
});

test('Cases_are_bound_only_when_asked_for', () => {
  const base = { copyDir: '/w/copy', home: '/w/home', tmp: '/w/tmp' };
  assert.equal(scoreBoxArgv({ ...base, exists: () => false }).includes('/w/cases'), false);
  assert.ok(at(scoreBoxArgv({ ...base, casesDir: '/w/cases', exists: () => false }), '--bind', '/w/cases', '/w/cases') > 0);
});

test('The_score_env_is_the_allow_list_not_the_parents', () => {
  process.env.GH_TOKEN = 'ghp_parent_secret';
  process.env.CLAUDE_CODE_OAUTH_TOKEN = 'sk-ant-parent-secret';
  try {
    const given = { home: '/w/home', tmp: '/w/tmp', toolchainDirs: ['/opt/t'], nugetCache: '/w/nuget', npmCache: '/w/npm' };
    const argv = scoreBoxArgv({ copyDir: '/w/copy', casesDir: '/w/cases', ...given, exists: () => false });
    const pairs = [];
    argv.forEach((a, i) => { if (a === '--setenv') pairs.push([argv[i + 1], argv[i + 2]]); });
    const keys = pairs.map(([k]) => k);
    assert.equal(keys.includes('GH_TOKEN') || keys.includes('CLAUDE_CODE_OAUTH_TOKEN'), false);
    assert.equal(argv.join(' ').includes('parent_secret'), false);
    assert.deepEqual(Object.keys(scoreEnv(given)).sort(), [
      'DOTNET_CLI_HOME', 'DOTNET_CLI_TELEMETRY_OPTOUT', 'DOTNET_CLI_USE_MSBUILD_SERVER', 'DOTNET_NOLOGO', 'HOME', 'LANG', 'MSBUILDDISABLENODEREUSE',
      'NUGET_PACKAGES', 'PATH', 'TMPDIR', 'UseSharedCompilation', 'npm_config_cache', 'npm_config_offline'].sort());
    const env = scoreEnv(given);
    assert.equal(env.NUGET_PACKAGES, '/w/nuget');
    assert.equal(env.npm_config_cache, '/w/npm');
    assert.equal(env.PATH, '/opt/t/bin:/opt/t:/usr/bin:/bin');
    assert.equal(scoreEnv({ home: '/w/home', tmp: '/w/tmp' }).NUGET_PACKAGES, '/w/home/.nuget/packages', 'with no cache given the cache is private to the scratch home');
  } finally {
    delete process.env.GH_TOKEN;
    delete process.env.CLAUDE_CODE_OAUTH_TOKEN;
  }
});

test('The_scoring_box_has_no_network_and_no_operator_home', async t => {
  if (!sandboxGate(t)) return;
  // a decoy "home" holding a decoy key, outside /tmp (which the box hides on its own): the box must hide a directory it is told is the home
  const fakeHome = mkdtempSync(path.join(os.homedir(), 'testsToDelete-mgb-home-'));
  mkdirSync(path.join(fakeHome, '.config', 'minigit-bench'), { recursive: true });
  const key = path.join(fakeHome, '.config', 'minigit-bench', 'heldout.key');
  writeFileSync(key, 'secret-key-bytes\n');
  try {
    assert.equal(readFileSync(key, 'utf8'), 'secret-key-bytes\n', 'control: the key is readable outside the box');
    const opts = boxDirs({ operatorHome: fakeHome });
    const hidden = await probe(opts, `ls -A ${fakeHome}; cat ${key}`);
    assert.equal(hidden.out, '', 'the home is an empty tmpfs and the key cannot be read');
    assert.doesNotMatch(hidden.stdout, /secret-key-bytes/);
    const realHome = await probe(boxDirs(), `ls -A ${os.homedir()}`);
    assert.equal(realHome.out, '', 'the real operator home is empty too');
    const net = await probe(opts, "timeout 5 bash -c 'exec 3<>/dev/tcp/1.1.1.1/443' 2>&1; echo net-rc=$?");
    assert.doesNotMatch(net.out, /net-rc=0/);
    assert.match(net.out, /net-rc=[1-9]/);
  } finally {
    rmSync(fakeHome, { recursive: true, force: true });
  }
});

test('The_scoring_box_can_build_and_write_where_it_must', async t => {
  if (!sandboxGate(t)) return;
  const opts = boxDirs({ toolchainDirs: [nodeToolchain] });
  const r = await probe(opts, `echo a > ${opts.copyDir}/w && echo b > ${opts.casesDir}/w && echo c > ${opts.tmp}/w && node --version`);
  assert.match(r.out, /^v\d+\./);
  assert.equal(readFileSync(path.join(opts.copyDir, 'w'), 'utf8'), 'a\n');
  assert.equal(readFileSync(path.join(opts.casesDir, 'w'), 'utf8'), 'b\n');
  const ro = await probe(opts, 'echo x > /usr/bin/mgb-should-not-exist 2>/dev/null; echo rc=$?');
  assert.match(ro.out, /rc=[1-9]/, 'the rest of the filesystem is read-only');
});

/** A unix socket the host can see under `dir`, listening: what a session bus looks like. */
async function listenOn(dir, name) {
  const file = path.join(dir, name);
  const server = net.createServer().unref();
  await new Promise((resolve, reject) => server.once('error', reject).listen(file, resolve));
  return { file, close: () => { server.close(); rmSync(file, { force: true }); } };
}

test('The_box_cannot_see_the_users_runtime_dir_where_the_session_bus_lives', async t => {
  if (!sandboxGate(t)) return;
  const runDir = `/run/user/${uid}`;
  if (!existsSync(runDir)) { t.skip(`${runDir} does not exist on this host`); return; }
  const sock = await listenOn(runDir, `testsToDelete-mgb-test-${process.pid}.sock`);
  try {
    const command = `test -S ${sock.file} && echo VISIBLE || echo HIDDEN`;
    const opts = boxDirs();
    const control = await probeArgv(withoutPair(scoreBoxArgv(opts), '--tmpfs', '/run'), command);
    assert.match(control, /VISIBLE/, 'control: without the tmpfs the same probe sees the socket');
    assert.match((await probe(opts, command)).out, /^HIDDEN\n$/);
    assert.match((await probe(opts, `test ! -S ${runDir}/bus && echo NO-BUS`)).out, /NO-BUS/);
  } finally {
    sock.close();
  }
});

test('The_box_cannot_start_a_unit_on_the_hosts_user_manager', async t => {
  if (!sandboxGate(t)) return;
  const host = await runBounded('systemd-run', ['--user', '--wait', '--pipe', 'true'], { timeoutMs: 20_000 });
  if (!host.ok || host.value.code !== 0) { t.skip('the host has no working systemd user manager to escape through'); return; }
  const command = 'export DBUS_SESSION_BUS_ADDRESS=unix:path=/run/user/$(id -u)/bus XDG_RUNTIME_DIR=/run/user/$(id -u); systemd-run --user --wait --pipe true >/dev/null 2>&1; echo rc=$?';
  const control = await probeArgv(withoutPair(scoreBoxArgv(boxDirs()), '--tmpfs', '/run'), command);
  assert.match(control, /rc=0/, 'control: without the tmpfs the box reaches the user manager');
  assert.match((await probe(boxDirs(), command)).out, /rc=[1-9]/);
});

test('The_box_shows_only_its_own_bound_directories_under_tmp_and_var_tmp', async t => {
  if (!sandboxGate(t)) return;
  const decoys = [path.join(os.tmpdir(), `testsToDelete-mgb-decoy-${process.pid}`), `/var/tmp/mgb-decoy-${process.pid}`];
  decoys.forEach(d => writeFileSync(d, 'x'));
  try {
    const opts = boxDirs();
    const tmpListing = await probe(opts, `ls -A ${os.tmpdir()}`);
    assert.deepEqual(tmpListing.out.split('\n').filter(Boolean), [path.basename(opts.base)], 'only the bound directories (control: they ARE listed)');
    const control = await probeArgv(withoutPair(scoreBoxArgv(opts), '--tmpfs', os.tmpdir()), `ls -A ${os.tmpdir()}`);
    assert.ok(control.includes(path.basename(decoys[0])), 'control: without the tmpfs the host decoy is listed');
    const varTmp = await probe(opts, 'ls -A /var/tmp');
    assert.equal(varTmp.out.includes(path.basename(decoys[1])), false);
    const varControl = await probeArgv(withoutPair(scoreBoxArgv(opts), '--tmpfs', '/var/tmp'), 'ls -A /var/tmp');
    assert.ok(varControl.includes(path.basename(decoys[1])), 'control: without the tmpfs /var/tmp shows its host decoy');
  } finally {
    decoys.forEach(d => rmSync(d, { force: true }));
  }
});

/** Pids whose environment carries `mark`. Only "gone" (ENOENT/ESRCH) and "not ours" (EACCES) are expected read errors. */
function processesCarrying(mark) {
  const hits = [];
  for (const pid of readdirSync('/proc').filter(n => /^\d+$/.test(n))) {
    try {
      if (readFileSync(`/proc/${pid}/environ`, 'utf8').includes(mark)) hits.push(Number(pid));
    } catch (e) {
      if (!['ENOENT', 'ESRCH', 'EACCES', 'EPERM'].includes(e.code)) throw e;
    }
  }
  return hits;
}
const sleep = ms => new Promise(r => setTimeout(r, ms));
async function waitFor(fn, ms) {
  for (const end = Date.now() + ms; Date.now() < end; await sleep(25)) if (fn()) return true;
  return fn();
}

/** Start a command in the box, wait until the mark is seen on a live process (the control), then resolve with the run's result. */
async function runMarked(opts, command, timeoutMs) {
  const mark = `mgb-mark-${Math.random().toString(36).slice(2)}`;
  const argv = scoreBoxArgv(opts);
  const running = runBounded(argv[0], [...argv.slice(1), 'sh', '-c', `env BENCH_SCORE_MARK=${mark} ${command}`], { timeoutMs });
  const seen = await waitFor(() => processesCarrying(mark).length > 0, 5000);
  const r = await running;
  assert.equal(r.ok, true, r.reason);
  return { mark, seen, ...r.value };
}

test('An_invocation_that_hangs_is_ended_at_its_limit_with_its_grandchildren', async t => {
  if (!sandboxGate(t)) return;
  const opts = boxDirs();
  cpSync(path.join(stubTrees, 'hang'), opts.copyDir, { recursive: true });
  assert.equal(installShim(opts.copyDir, 1).ok, true);
  const r = await runMarked({ ...opts, cwd: opts.copyDir }, './minigit', 15_000);
  assert.equal(r.seen, true, 'control: the marked process was alive while the invocation ran');
  assert.ok(r.ms < 4000, `ended in ${r.ms} ms`);
  assert.equal(r.timedOut, false);
  assert.equal(await waitFor(() => processesCarrying(r.mark).length === 0, 3000), true, 'no process carries the mark afterwards');
});

test('Killing_the_bwrap_process_leaves_no_marked_process_behind', async t => {
  if (!sandboxGate(t)) return;
  const r = await runMarked(boxDirs(), 'sh -c "sleep 300 & wait"', 1500);
  assert.equal(r.seen, true, 'control: the marked grandchild was alive before the kill');
  assert.equal(r.timedOut, true, 'runBounded SIGKILLed the bwrap process');
  assert.equal(await waitFor(() => processesCarrying(r.mark).length === 0, 3000), true, 'the namespace collapsed with it');
});
