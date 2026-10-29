import assert from 'node:assert/strict';
import { cpSync, existsSync, mkdirSync, readFileSync, readdirSync, rmSync, writeFileSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { test } from 'node:test';
import { bind, devProc, isolation, roBind, roRoot, scoreBoxArgv, scoreEnv, setenvAll, tmpfs } from '../lib/bwrap.mjs';
import { runBounded } from '../lib/run.mjs';
import { installShim } from '../lib/visible.mjs';
import { BWRAP, sandboxGate, stubTrees, tmpDir } from './helpers.mjs';

const nodeToolchain = path.dirname(path.dirname(process.execPath));

/** A box over fresh dirs; `toolchainDirs` defaults to none, so nothing under the operator's home is visible. */
function boxDirs(extra = {}) {
  const base = tmpDir('box');
  const dirs = { copyDir: path.join(base, 'copy'), casesDir: path.join(base, 'cases'), home: path.join(base, 'home'), tmp: path.join(base, 'tmp') };
  for (const d of Object.values(dirs)) mkdirSync(d, { recursive: true });
  return { ...dirs, toolchainDirs: [], cacheDirs: [], ...extra };
}

async function inBox(opts, command, timeoutMs = 20_000) {
  const argv = scoreBoxArgv(opts);
  const r = await runBounded(argv[0], [...argv.slice(1), 'sh', '-c', command], { timeoutMs });
  assert.equal(r.ok, true, r.reason);
  return r.value;
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

test('The_box_hides_the_home_before_it_binds_what_lives_under_it_and_ends_at_the_double_dash', () => {
  const opts = { copyDir: '/work/copy', casesDir: '/work/cases', home: '/work/home', tmp: '/work/tmp', toolchainDirs: [`${os.homedir()}/.nvm/node`], cacheDirs: ['/work/nuget'], exists: p => p === '/run/WSL' };
  const argv = scoreBoxArgv(opts);
  assert.equal(argv[0], 'bwrap');
  assert.equal(argv.at(-1), '--');
  const at = (...frag) => argv.findIndex((_, i) => frag.every((f, j) => argv[i + j] === f));
  assert.ok(at('--tmpfs', os.homedir()) > at('--ro-bind', '/', '/'));
  assert.ok(at('--ro-bind', opts.toolchainDirs[0], opts.toolchainDirs[0]) > at('--tmpfs', os.homedir()));
  assert.ok(at('--bind', '/work/copy', '/work/copy') > at('--tmpfs', os.homedir()));
  assert.ok(at('--tmpfs', '/mnt') >= 0 && at('--tmpfs', '/run/WSL') >= 0);
  for (const d of ['/work/cases', '/work/home', '/work/tmp', '/work/nuget']) assert.ok(at('--bind', d, d) >= 0, d);
  assert.ok(at('--unshare-net') >= 0 && at('--unshare-pid') >= 0 && at('--die-with-parent') >= 0);
  assert.equal(scoreBoxArgv({ ...opts, exists: () => false }).includes('/run/WSL'), false);
  assert.deepEqual(scoreBoxArgv({ ...opts, cwd: '/work/copy' }).slice(-3), ['--chdir', '/work/copy', '--']);
});

test('The_score_env_is_the_allow_list_not_the_parents', () => {
  process.env.GH_TOKEN = 'ghp_parent_secret';
  process.env.CLAUDE_CODE_OAUTH_TOKEN = 'sk-ant-parent-secret';
  try {
    const argv = scoreBoxArgv({ copyDir: '/w/copy', casesDir: '/w/cases', home: '/w/home', tmp: '/w/tmp', toolchainDirs: ['/opt/t'], cacheDirs: ['/w/nuget', '/w/npm'], exists: () => false });
    const pairs = [];
    argv.forEach((a, i) => { if (a === '--setenv') pairs.push([argv[i + 1], argv[i + 2]]); });
    const keys = pairs.map(([k]) => k);
    assert.equal(keys.includes('GH_TOKEN') || keys.includes('CLAUDE_CODE_OAUTH_TOKEN'), false);
    assert.equal(argv.join(' ').includes('parent_secret'), false);
    assert.deepEqual(Object.keys(scoreEnv({ home: '/w/home', tmp: '/w/tmp', toolchainDirs: ['/opt/t'], cacheDirs: ['/w/nuget', '/w/npm'] })).sort(), [
      'DOTNET_CLI_HOME', 'DOTNET_CLI_TELEMETRY_OPTOUT', 'DOTNET_CLI_USE_MSBUILD_SERVER', 'DOTNET_NOLOGO', 'HOME', 'LANG', 'MSBUILDDISABLENODEREUSE',
      'NUGET_PACKAGES', 'PATH', 'TMPDIR', 'UseSharedCompilation', 'npm_config_cache', 'npm_config_offline'].sort());
    const env = scoreEnv({ home: '/w/home', tmp: '/w/tmp', toolchainDirs: ['/opt/t'], cacheDirs: ['/w/nuget', '/w/npm'] });
    assert.equal(env.NUGET_PACKAGES, '/w/nuget');
    assert.equal(env.npm_config_cache, '/w/npm');
    assert.equal(env.PATH, '/opt/t/bin:/opt/t:/usr/bin:/bin');
  } finally {
    delete process.env.GH_TOKEN;
    delete process.env.CLAUDE_CODE_OAUTH_TOKEN;
  }
});

test('The_scoring_box_has_no_network_and_no_operator_home', async t => {
  if (!sandboxGate(t)) return;
  const decoy = path.join(os.homedir(), `.mgb-test-decoy-${process.pid}`);
  writeFileSync(decoy, 'secret\n');
  try {
    const opts = boxDirs();
    const listing = await inBox(opts, `ls -A ${os.homedir()}`);
    assert.equal(listing.stdout, '', 'the operator home is an empty tmpfs');
    const decoyRead = await inBox(opts, `cat ${decoy}`);
    assert.notEqual(decoyRead.code, 0);
    assert.doesNotMatch(decoyRead.stdout, /secret/);
    const keyRead = await inBox(opts, `cat ${os.homedir()}/.config/minigit-bench/heldout.key`);
    assert.notEqual(keyRead.code, 0);
    const net = await inBox(opts, "timeout 5 bash -c 'exec 3<>/dev/tcp/1.1.1.1/443'");
    assert.notEqual(net.code, 0);
  } finally {
    rmSync(decoy, { force: true });
  }
});

test('The_scoring_box_can_build_and_write_where_it_must', async t => {
  if (!sandboxGate(t)) return;
  const opts = boxDirs({ toolchainDirs: [nodeToolchain] });
  const r = await inBox(opts, `echo a > ${opts.copyDir}/w && echo b > ${opts.casesDir}/w && echo c > ${opts.tmp}/w && node --version`);
  assert.equal(r.code, 0, r.stderr);
  assert.match(r.stdout, /^v\d+\./);
  assert.equal(readFileSync(path.join(opts.copyDir, 'w'), 'utf8'), 'a\n');
  assert.equal(readFileSync(path.join(opts.casesDir, 'w'), 'utf8'), 'b\n');
  const ro = await inBox(opts, 'echo x > /usr/bin/mgb-should-not-exist');
  assert.notEqual(ro.code, 0, 'the rest of the filesystem is read-only');
});

test('An_invocation_that_hangs_is_ended_at_its_limit_with_its_grandchildren', async t => {
  if (!sandboxGate(t)) return;
  const opts = boxDirs();
  cpSync(path.join(stubTrees, 'hang'), opts.copyDir, { recursive: true });
  assert.equal(installShim(opts.copyDir, 1).ok, true);
  const mark = `mgb-mark-${Math.random().toString(36).slice(2)}`;
  const r = await inBox({ ...opts, cwd: opts.copyDir }, `env BENCH_SCORE_MARK=${mark} ./minigit`, 15_000);
  assert.ok(r.ms < 4000, `ended in ${r.ms} ms`);
  assert.equal(r.timedOut, false);
  assert.deepEqual(processesCarrying(mark), []);
  assert.ok(existsSync(BWRAP));
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
