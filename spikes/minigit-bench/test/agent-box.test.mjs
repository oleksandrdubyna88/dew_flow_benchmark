import assert from 'node:assert/strict';
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import net from 'node:net';
import os from 'node:os';
import path from 'node:path';
import { test } from 'node:test';
import { hiddenPaths, isolation, scoreBoxArgv, tmpfs } from '../lib/bwrap.mjs';
import { runBounded } from '../lib/run.mjs';
import {
  AGENT_ENV_KEYS, BOX_API_PORT, BOX_OTEL_PORT, BOX_PROXY_PORT, SOCKET_PATH_MAX_BYTES, agentBoxArgv, agentBoxPrefix, agentEnv, dummyToken, layoutPaths, sessionLayout,
} from '../lib/sandbox.mjs';
import { boxOptions, envFor, newSession, nodeDir, probe, probeArgv, withoutBind, withoutFlag } from './box-helpers.mjs';
import { homeDir, sandboxGate, tmpDir } from './helpers.mjs';

const HOME = '/home/someone';

test('The_hide_list_is_the_home_and_the_four_host_prefixes_and_a_missing_host_dir_is_left_alone', () => {
  assert.deepEqual(hiddenPaths({ operatorHome: HOME, exists: () => true }), [HOME, '/mnt', '/run', '/tmp', '/var/tmp']);
  assert.deepEqual(hiddenPaths({ operatorHome: HOME, exists: () => false }), [HOME], 'the home is always hidden; the others only when the host has them');
  assert.deepEqual(hiddenPaths({ operatorHome: HOME, exists: p => p !== '/var/tmp' }), [HOME, '/mnt', '/run', '/tmp']);
  assert.equal(Object.isFrozen(hiddenPaths({ operatorHome: HOME, exists: () => true })), true);
});

test('Both_boxes_hide_the_same_paths_and_isolate_the_same_way', () => {
  const layout = layoutPaths('/r', 'a');
  const opts = { copyDir: '/w/copy', home: '/w/home', tmp: '/w/tmp', operatorHome: HOME, exists: () => true };
  const hidden = hiddenPaths(opts);
  const boxes = [['scoring', scoreBoxArgv(opts)], ['agent', agentBoxPrefix(boxOptions(layout, { operatorHome: HOME, exists: () => true }))]];
  for (const [name, argv] of boxes) {
    for (const p of hidden) assert.ok(argv.some((a, i) => a === '--tmpfs' && argv[i + 1] === p), `${name} box hides ${p}`);
    for (const flag of isolation()) assert.ok(argv.includes(flag), `${name} box carries ${flag}`);
    assert.equal(argv.at(-1), '--');
  }
  assert.ok(agentBoxPrefix(boxOptions(layout)).includes('--unshare-ipc'), 'the agent box adds --unshare-ipc');
  assert.equal(scoreBoxArgv(opts).includes('--unshare-ipc'), false, 'the scoring box is unchanged');
  assert.deepEqual(tmpfs('/mnt'), ['--tmpfs', '/mnt']);
});

test('The_box_argv_binds_an_explicit_list_never_the_session_dir_and_carries_no_setenv_and_no_secret', () => {
  const layout = layoutPaths('/r', 'a');
  const dummy = dummyToken('zzz');
  const real = 'sk-ant-fake-REAL-0001';
  const argv = agentBoxArgv({ ...boxOptions(layout, { toolchains: ['/tc'], extraRo: ['/x/ro'], extraRw: ['/x/rw'], exists: () => true, operatorHome: HOME }), command: ['claude', '-p'] });
  assert.equal(argv.includes('--setenv') || argv.includes('--clearenv'), false, 'no --setenv, no --clearenv: the env is the spawn\'s');
  assert.equal(argv.join('\0').includes(dummy) || argv.join('\0').includes(real), false);
  const binds = [];
  argv.forEach((a, i) => { if (a === '--bind' || a === '--ro-bind') binds.push([a, argv[i + 1], argv[i + 2]]); });
  assert.equal(binds.some(([, src]) => src === layout.dir), false, 'the session dir itself is never bound: it holds the instruments');
  const ro = binds.filter(([f]) => f === '--ro-bind').map(([, s]) => s);
  const rw = binds.filter(([f]) => f === '--bind').map(([, s]) => s);
  for (const p of [nodeDir, '/tc', layout.harness, layout.net, '/x/ro', process.execPath]) assert.ok(ro.includes(p), `${p} is bound read-only`);
  for (const p of [layout.work, layout.home, layout.config, layout.tmp, layout.nuget, layout.npm, layout.out, '/x/rw']) assert.ok(rw.includes(p), `${p} is bound read-write`);
  assert.deepEqual(binds.find(([f, s]) => f === '--bind' && s === layout.tmp), ['--bind', layout.tmp, '/tmp'], 'box/tmp is the box\'s /tmp');
  const iTmpBind = argv.findIndex((a, i) => a === '--bind' && argv[i + 1] === layout.tmp);
  const iHome = argv.findIndex((a, i) => a === '--bind' && argv[i + 1] === layout.home);
  assert.ok(iTmpBind < iHome, 'box/tmp is bound before any other bind that may live under the host /tmp');
  assert.ok(argv.indexOf('--chdir') > 0 && argv[argv.indexOf('--chdir') + 1] === layout.work);
  const tail = argv.slice(argv.indexOf('--') + 1);
  assert.deepEqual(tail.slice(0, 2), [`${nodeDir}/bin/node`, layout.files.boxInit]);
  assert.deepEqual(tail.slice(2, 5), [`${BOX_PROXY_PORT}:${layout.sockets.proxy}`, `${BOX_API_PORT}:${layout.sockets.api}`, `${BOX_OTEL_PORT}:${layout.sockets.otel}`]);
  assert.deepEqual(tail.slice(5), ['--', 'claude', '-p']);
});

test('The_bare_arm_blocks_gh_where_the_host_has_it_and_the_process_arm_does_not', () => {
  const layout = layoutPaths('/r', 'a');
  const has = argv => argv.some((a, i) => a === '--ro-bind' && argv[i + 1] === '/dev/null' && argv[i + 2] === '/usr/bin/gh');
  assert.equal(has(agentBoxPrefix(boxOptions(layout, { exists: () => true }))), true);
  assert.equal(has(agentBoxPrefix(boxOptions(layout, { exists: () => false }))), false, 'a path the host lacks is not mounted over');
  assert.equal(has(agentBoxPrefix(boxOptions(layout, { arm: 'process', exists: () => true }))), false);
});

test('The_env_is_the_allow_list_and_its_path_has_no_mnt', () => {
  const layout = layoutPaths('/r', 'a');
  const real = 'sk-ant-fake-REAL-0002';
  const saved = { ...process.env };
  Object.assign(process.env, { GH_TOKEN: 'ghp_x', WSL_INTEROP: '/run/WSL/1_interop', PATH: `/mnt/c/x:${process.env.PATH}`, CLAUDE_CODE_OAUTH_TOKEN: real });
  try {
    const env = agentEnv({ layout, dummyToken: dummyToken('d1'), cellId: 'c1', nodeDir: '/n', tsDir: '/ts' });
    assert.deepEqual(Object.keys(env).sort(), [...AGENT_ENV_KEYS].sort(), 'the key set equals AGENT_ENV_KEYS');
    assert.equal(env.PATH, '/n/bin:/ts/node_modules/.bin:/usr/local/bin:/usr/bin:/bin');
    assert.equal(env.PATH.includes('/mnt'), false);
    assert.equal(Object.keys(env).some(k => k.startsWith('GH_') || k === 'WSL_INTEROP'), false);
    assert.match(env.NO_PROXY, /127\.0\.0\.1/);
    assert.equal(env.ANTHROPIC_BASE_URL, 'http://127.0.0.1:8787');
    assert.equal(env.CLAUDE_CODE_OAUTH_TOKEN, dummyToken('d1'), 'the dummy, never the real token');
    assert.equal(JSON.stringify(env).includes(real), false);
    assert.equal(env.BENCH_CELL, 'c1');
    assert.equal(env.GIT_CONFIG_GLOBAL, layout.files.gitconfig);
    assert.deepEqual(Object.keys(agentEnv({ layout, dummyToken: 'd', cellId: 'c', nodeDir: '/n', extra: { GH_TOKEN: 'x' } })).sort(), [...AGENT_ENV_KEYS, 'GH_TOKEN'].sort(), 'extra is the process arm\'s slot');
    assert.equal(Object.isFrozen(env), true);
  } finally {
    for (const k of ['GH_TOKEN', 'WSL_INTEROP', 'CLAUDE_CODE_OAUTH_TOKEN']) { if (saved[k] === undefined) delete process.env[k]; else process.env[k] = saved[k]; }
    process.env.PATH = saved.PATH;
  }
});

test('A_session_dir_is_claimed_once', () => {
  const root = homeDir('lay');
  const first = sessionLayout(root, 'r1');
  assert.equal(first.ok, true, first.reason);
  for (const sub of ['harness', 'net', 'box/work', 'box/home', 'box/config', 'box/tmp', 'box/nuget', 'box/npm', 'box/out', 'otel']) assert.ok(existsSync(path.join(first.value.dir, sub)), sub);
  writeFileSync(first.value.files.state, '{"phase":"running"}');
  const again = sessionLayout(root, 'r1');
  assert.equal(again.ok, false);
  assert.match(again.reason, /EEXIST/);
  assert.equal(readFileSync(first.value.files.state, 'utf8'), '{"phase":"running"}', 'a rid holding a state.json is never written over');
  for (const bad of ['', 'a/b', '..', 'x y', 'a'.repeat(65)]) assert.equal(sessionLayout(root, bad).ok, false, bad);
  assert.equal(sessionLayout('relative', 'r2').ok, false);
});

test('The_socket_path_boundary_is_108_bytes_accepted_109_refused_and_counted_in_utf8_bytes', () => {
  const tail = '/net/proxy.sock'.length; // root + '/' + rid + tail
  const root = homeDir('lay-edge');
  const rootLen = Buffer.byteLength(root, 'utf8');
  assert.equal(SOCKET_PATH_MAX_BYTES, 108);
  const pad = n => 'r'.repeat(n);
  const ridFor = total => pad(total - rootLen - 1 - tail);
  const at108 = ridFor(108);
  if (at108.length > 64) { assert.ok(true, 'the test root is too short for a 64-char rid to reach the limit'); return; }
  const fits = layoutPaths(root, at108);
  assert.equal(Buffer.byteLength(fits.sockets.proxy, 'utf8'), 108);
  assert.equal(sessionLayout(root, at108).ok, true, '108 bytes is accepted');
  const over = layoutPaths(root, ridFor(109));
  assert.equal(Buffer.byteLength(over.sockets.proxy, 'utf8'), 109);
  const refused = sessionLayout(root, ridFor(109));
  assert.equal(refused.ok, false);
  assert.match(refused.reason, /109 bytes/);
  assert.equal(existsSync(over.dir), false, 'a refused layout claims nothing');
  const multi = sessionLayout(root, `${ridFor(108).slice(0, -1)}é`);
  assert.equal(multi.ok, false, '108 characters carrying one two-byte character are 109 bytes');
});

// ---- effect tests: the real bwrap -----------------------------------------------------------------------------------------------

test('The_agent_box_reaches_no_host_loopback_abstract_socket_or_address', async t => {
  if (!sandboxGate(t)) return;
  const layout = newSession();
  const abstractName = `\0mgb-abstract-${process.pid}`;
  const abstract = net.createServer(c => { c.on('error', () => {}); c.end(); }).unref();
  await new Promise(r => abstract.listen(abstractName, r));
  const tcp = net.createServer(c => { c.on('error', () => {}); c.end(); }).unref();
  await new Promise(r => tcp.listen(0, '127.0.0.1', r));
  const script = (kind, target) => `node -e "const s=require('net').connect(${target});s.on('connect',()=>{console.log('${kind}:REACHED');process.exit(0)});s.on('error',e=>{console.log('${kind}:'+e.code);process.exit(0)});setTimeout(()=>{console.log('${kind}:TIMEOUT');process.exit(0)},4000)"`;
  const probes = [
    ['abstract', script('abstract', `{path:'\\0mgb-abstract-${process.pid}'}`)],
    ['loopback', script('loopback', `${tcp.address().port},'127.0.0.1'`)],
    ['internet', script('internet', "443,'1.1.1.1'")],
  ];
  try {
    const env = envFor(layout);
    const isolated = await probeArgv(agentBoxPrefix(boxOptions(layout)), probes.map(p => p[1]).join('\n'), env);
    assert.match(isolated.out, /abstract:ECONNREFUSED/);
    assert.match(isolated.out, /loopback:ECONNREFUSED/);
    assert.match(isolated.out, /internet:(ENETUNREACH|EHOSTUNREACH)/, 'no route out of the namespace');
    const control = await probeArgv(withoutFlag(agentBoxPrefix(boxOptions(layout)), '--unshare-net'), probes.map(p => p[1]).join('\n'), env);
    assert.match(control.out, /abstract:REACHED/, 'control: in the host namespace the abstract socket is reached');
    assert.match(control.out, /loopback:REACHED/, 'control: and the host loopback listener');
    if (/internet:REACHED/.test(control.out)) assert.ok(true, 'control: and the internet');
    else t.diagnostic('the host itself cannot reach 1.1.1.1:443, so the internet half of the control is not shown on this run');
  } finally {
    abstract.close();
    tcp.close();
  }
});

test('A_bound_unix_socket_crosses_the_namespace_through_a_read_only_bind', async t => {
  if (!sandboxGate(t)) return;
  const layout = newSession();
  const server = net.createServer(c => c.on('data', d => c.write(`echo:${d}`)));
  await new Promise(r => server.listen(layout.sockets.proxy, r));
  try {
    const run = prefix => runBounded(prefix[0], [...prefix.slice(1), `${nodeDir}/bin/node`, layout.files.boxInit, `5000:${layout.sockets.proxy}`, '--', 'bash', '-c',
      "exec 3<>/dev/tcp/127.0.0.1/5000; printf hi >&3; timeout 3 head -c 7 <&3; echo END"], { env: envFor(layout), timeoutMs: 30_000 });
    const prefix = agentBoxPrefix(boxOptions(layout));
    const ok = await run(prefix);
    assert.match(ok.value.stdout, /^echo:hi\nEND\n$|^echo:hiEND\n$/, ok.value.stderr);
    const without = await run(withoutBind(prefix, '--ro-bind', layout.net));
    assert.equal(without.value.stdout.includes('echo:hi'), false, 'control: without the bind the socket path does not exist in the box');
  } finally {
    server.close();
  }
});

test('The_box_has_no_dns_and_that_is_the_point', async t => {
  if (!sandboxGate(t)) return;
  const layout = newSession();
  const inside = await probe(layout, 'getent hosts api.anthropic.com >/dev/null 2>&1; echo rc=$?; ls /etc/resolv.conf >/dev/null 2>&1; echo resolv=$?');
  assert.match(inside.out, /rc=[1-9]/, 'no name resolves inside');
  const outside = await runBounded('getent', ['hosts', 'api.anthropic.com'], { timeoutMs: 20_000 });
  if (!outside.ok || outside.value.code !== 0) { t.skip('the host itself resolves no names, so the control cannot show the difference'); return; }
  assert.ok(outside.value.stdout.length > 0, 'control: outside, the same name resolves');
});

test('The_agent_box_cannot_see_the_session_bus_or_the_user_manager', async t => {
  if (!sandboxGate(t)) return;
  const uid = process.getuid();
  const runDir = `/run/user/${uid}`;
  if (!existsSync(runDir)) { t.skip(`${runDir} does not exist on this host`); return; }
  const layout = newSession();
  const sock = path.join(runDir, `testsToDelete-mgb-agent-${process.pid}.sock`);
  const server = net.createServer().unref();
  await new Promise((resolve, reject) => server.once('error', reject).listen(sock, resolve));
  try {
    const command = `test -S ${sock} && echo VISIBLE || echo HIDDEN`;
    const argv = agentBoxPrefix(boxOptions(layout));
    assert.match((await probeArgv(withoutPair2(argv, '/run'), command, envFor(layout))).out, /VISIBLE/, 'control: without the tmpfs the same probe sees the socket');
    assert.match((await probe(layout, command)).out, /^HIDDEN\n$/);
    const host = await runBounded('systemd-run', ['--user', '--wait', '--pipe', 'true'], { timeoutMs: 20_000 });
    if (!host.ok || host.value.code !== 0) { t.diagnostic('the host has no working systemd user manager: the unit half is skipped'); return; }
    const escape = 'export DBUS_SESSION_BUS_ADDRESS=unix:path=/run/user/$(id -u)/bus XDG_RUNTIME_DIR=/run/user/$(id -u); systemd-run --user --wait --pipe true >/dev/null 2>&1; echo rc=$?';
    assert.match((await probeArgv(withoutPair2(argv, '/run'), escape, envFor(layout))).out, /rc=0/, 'control: without the tmpfs the box reaches the user manager');
    assert.match((await probe(layout, escape)).out, /rc=[1-9]/);
  } finally {
    server.close();
    (await import('node:fs')).rmSync(sock, { force: true });
  }
});

function withoutPair2(argv, p) {
  const i = argv.findIndex((a, j) => a === '--tmpfs' && argv[j + 1] === p);
  assert.ok(i >= 0, `--tmpfs ${p} is in the argv`);
  return [...argv.slice(0, i), ...argv.slice(i + 2)];
}

test('The_agent_box_sees_only_its_own_tmp', async t => {
  if (!sandboxGate(t)) return;
  const layout = newSession();
  const decoys = [path.join(os.tmpdir(), `testsToDelete-mgb-adecoy-${process.pid}`), `/var/tmp/mgb-adecoy-${process.pid}`];
  decoys.forEach(d => writeFileSync(d, 'x'));
  writeFileSync(path.join(layout.tmp, 'mine'), 'y');
  try {
    const own = await probe(layout, `ls -A ${os.tmpdir()}`);
    assert.deepEqual(own.out.split('\n').filter(Boolean), ['mine'], 'only box/tmp\'s content (control: it IS listed)');
    const varTmp = await probe(layout, 'ls -A /var/tmp');
    assert.equal(varTmp.out.includes(path.basename(decoys[1])), false);
    const argv = agentBoxPrefix(boxOptions(layout));
    const open = withoutBind(withoutPair2(argv, os.tmpdir()), '--bind', layout.tmp);
    assert.ok((await probeArgv(open, `ls -A ${os.tmpdir()}`, envFor(layout))).out.includes(path.basename(decoys[0])), 'control: without the tmpfs and the bind the host decoy is listed');
    assert.ok((await probeArgv(withoutPair2(argv, '/var/tmp'), 'ls -A /var/tmp', envFor(layout))).out.includes(path.basename(decoys[1])), 'control: and /var/tmp shows its decoy');
  } finally {
    decoys.forEach(d => (async () => (await import('node:fs')).rmSync(d, { force: true }))());
  }
});

test('The_toolchain_is_probed_inside_the_box_node_and_a_box_dir_is_writable_where_it_must', async t => {
  if (!sandboxGate(t)) return;
  const layout = newSession();
  const r = await probe(layout, `node --version; echo a > ${layout.work}/w && echo b > ${layout.out}/w && echo c > /tmp/w && echo d > ${layout.home}/w; echo rw=$?; touch ${layout.harness}/x 2>/dev/null; echo harness=$?; touch ${layout.net}/x 2>/dev/null; echo net=$?`);
  assert.match(r.out, /^v\d+\./);
  assert.match(r.out, /rw=0/);
  assert.match(r.out, /harness=[1-9]/, 'harness/ is read-only');
  assert.match(r.out, /net=[1-9]/, 'net/ is read-only');
  assert.equal(readFileSync(path.join(layout.work, 'w'), 'utf8'), 'a\n');
  assert.equal(readFileSync(path.join(layout.tmp, 'w'), 'utf8'), 'c\n');
  mkdirSync(path.join(tmpDir('unused'), 'x'));
});
