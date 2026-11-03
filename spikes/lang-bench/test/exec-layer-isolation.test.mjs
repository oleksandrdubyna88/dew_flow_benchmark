// E1.S8b: the box hides the host — no home, no /mnt, no /run, no WSL interop — and prints nothing to stderr for `bash -c true`.
// Every test that needs a toolchain-free box runs real bwrap; the wrong mechanisms (v1's `roRoot()` with a tmpfs over the home, the missing locale archive) are built here from
// v1's primitives so each defect has a shape in the suite.
import assert from 'node:assert/strict';
import { existsSync, writeFileSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { test } from 'node:test';
import { devProc, roRoot, setenvAll, tmpfs, chdir } from '../../minigit-bench/lib/bwrap.mjs';
import { boxArgv, unsafeBinds } from '../lib/exec-layer/box.mjs';
import { boxEnv } from '../lib/exec-layer/env.mjs';
import { owned } from './support/owned.mjs';
import { needs, sh, toolchains } from './support/exec-layer-host.mjs';

const HOME = os.homedir();
const env = () => boxEnv({ dirs: toolchains });

/** A view folder with `run.sh` and the box's argv for it (ending in `--`). */
function setup(t, what, extra = {}) {
  const view = owned.ownedDir(t, what);
  return { view, argv: boxArgv({ view, dirs: toolchains, env: env(), prog: null, ...extra }) };
}

const inBox = (box, script) => sh(box[0], [...box.slice(1), 'bash', '-c', script]);

test('The_box_argv_has_no_broad_bind_and_no_net_namespace_flag_and_no_die_with_parent', t => {
  const { view, argv: buildArgv } = setup(t, 'argv');
  assert.ok(buildArgv.includes('--die-with-parent'), 'B1: a BUILD box (no scope) dies with its parent, so a build never outlives the probe that started it');
  const argv = boxArgv({ view, dirs: toolchains, env: env(), prog: '/sys/fs/cgroup/prog', tree: view }); // the program's box: ended by its cgroup, never by its parent
  assert.deepEqual(unsafeBinds(argv, { home: HOME }), []);
  assert.deepEqual(argv.slice(1, 3), ['--tmpfs', '/'], 'the base is a tmpfs root');
  for (const flag of ['--unshare-net', '--die-with-parent']) assert.ok(!argv.includes(flag), `${flag} must not be in the box argv`);
  for (const flag of ['--unshare-pid', '--new-session']) assert.ok(argv.includes(flag), `${flag} stays`);
});

test('The_bind_scan_still_finds_a_broad_bind_a_home_bind_and_a_mnt_bind', () => {
  assert.equal(unsafeBinds(['bwrap', ...roRoot()], { home: HOME }).length, 1);
  assert.equal(unsafeBinds(['bwrap', '--ro-bind', HOME, HOME], { home: HOME }).length, 1);
  assert.equal(unsafeBinds(['bwrap', '--bind', path.dirname(HOME), path.dirname(HOME)], { home: HOME }).length, 1, 'a parent of the home is the home');
  assert.equal(unsafeBinds(['bwrap', '--ro-bind', '/mnt/c', '/mnt/c'], { home: HOME }).length, 1);
  assert.equal(unsafeBinds(['bwrap', '--ro-bind', '/run', '/run'], { home: HOME }).length, 1);
  assert.deepEqual(unsafeBinds(['bwrap', '--ro-bind', `${HOME}/rsd/lang-v2/toolchains/node`, `${HOME}/rsd/lang-v2/toolchains/node`], { home: HOME }), [], 'a toolchain tree under the home is bound by its full path only');
});

test('A_box_that_only_hides_the_home_would_still_show_mnt_c_and_that_is_the_point', async t => {
  if (!needs(t, existsSync('/mnt/c') ? null : 'this host has no /mnt/c to leak')) return;
  const view = owned.ownedDir(t, 'wrongbox');
  const wrong = ['bwrap', ...roRoot(), ...devProc(), ...tmpfs(HOME), '--bind', view, view, ...chdir(view), ...setenvAll({ PATH: '/usr/bin:/bin' }), '--'];
  const leaking = await inBox(wrong, 'test -d /mnt/c && echo VISIBLE');
  assert.equal(leaking.stdout.trim(), 'VISIBLE', 'v1\'s read-only root with the home hidden still shows /mnt/c — the leak this box exists to close');
  const { argv } = setup(t, 'rightbox');
  const right = await inBox(argv, 'echo BOX-RAN; test -e /mnt && echo VISIBLE; test -e /mnt/c && echo VISIBLE');
  assert.equal(right.stdout.trim(), 'BOX-RAN', `the explicit-bind box ran and has no /mnt at all (stderr: ${right.stderr})`);
});

test('The_program_cannot_read_a_sentinel_under_HOME', async t => {
  if (!needs(t)) return;
  const underHome = owned.ownedDir(t, 'sentinel', { parent: HOME });
  const driverOnly = owned.ownedDir(t, 'driveronly');
  writeFileSync(path.join(underHome, 'secret.txt'), 'HOME-SENTINEL');
  writeFileSync(path.join(driverOnly, 'secret.txt'), 'DRIVER-SENTINEL');
  const { argv } = setup(t, 'sentbox');
  const seen = await inBox(argv, `cat '${underHome}/secret.txt' '${driverOnly}/secret.txt' 2>&1; ls -A '${HOME}' 2>&1`);
  assert.doesNotMatch(seen.stdout, /SENTINEL/, 'neither sentinel is readable');
  assert.deepEqual(seen.stdout.split('\n').filter(line => !line.includes('No such file')).filter(Boolean), ['rsd'], 'the only thing of the home in the box is the path to the toolchains');
  const control = await sh('cat', [path.join(underHome, 'secret.txt')]);
  assert.equal(control.stdout, 'HOME-SENTINEL', 'the sentinel is readable outside the box (the positive control)');
});

test('The_box_has_no_mnt_no_run_no_user_bus_and_cmd_exe_does_not_start', async t => {
  if (!needs(t)) return;
  const { argv } = setup(t, 'nowsl');
  const script = 'ls /mnt /run /var/tmp 2>&1; cmd.exe /c ver 2>&1; /mnt/c/WINDOWS/system32/cmd.exe /c ver 2>&1; echo "interop=${WSL_INTEROP-unset}"; systemd-run --user true 2>&1; echo "bus=$?"';
  const seen = await inBox(argv, script);
  assert.match(seen.stdout, /ls: cannot access '\/mnt'.*\n.*cannot access '\/run'.*\n.*cannot access '\/var\/tmp'/);
  assert.match(seen.stdout, /cmd\.exe: command not found/);
  assert.match(seen.stdout, /interop=unset/);
  assert.doesNotMatch(seen.stdout, /bus=0/, 'a program cannot start a unit through the user bus');
  assert.doesNotMatch(seen.stdout, /Microsoft Windows/);
});

test('The_box_prints_nothing_to_stderr_for_bash_c_true', async t => {
  if (!needs(t)) return;
  const { argv } = setup(t, 'quiet');
  const quiet = await inBox(argv, 'true');
  assert.equal(quiet.stderr, '', 'no setlocale warning: the locale archive is bound');
  const locales = await inBox(argv, 'locale -a');
  assert.match(locales.stdout, /^de_DE\.utf8$/m, '`locale -a` lists de_DE.utf8 in the box');
  const wrong = argv.flatMap((arg, i) => (arg === '--chdir' ? [...tmpfs('/usr/lib/locale'), arg] : [arg]));
  const loud = await inBox(wrong, 'true');
  assert.match(loud.stderr, /setlocale/, 'with the archive hidden the same command warns — what the bound archive prevents');
});
