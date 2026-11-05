// E1.S3 phase A, the workspaces on the execution layer (the real toolchains and the registered snapshots): the hello programs build in the build box of a workspace, which has no route off the host and binds the
// snapshot read-only; the olympiad samples are judged by the pinned runner; the daemon and web hello programs are served and stopped by the test's own driver through the layer's chain (the pinned runner runs
// olympiad only until part ii-b); a hung build is killed whole. The failure cases of the package sets, the type check and T-L31 are in workspace-exec-kinds.test.mjs.
// Every folder is an owned folder of token `bench` under ~/.cache/lang-v2/t (a tmpfs /tmp could not hold a node_modules). Run with BENCH_REQUIRE_SANDBOX=1 so that a missing tool is a failure, not a skip.
import assert from 'node:assert/strict';
import { rmSync, writeFileSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { after, before, test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { sweepLeftovers } from '../../../.agents/conventions/tools/lib/leftovers-sweep.mjs';
import { treeHash } from '../../minigit-bench/lib/cache-snapshot.mjs';
import { runBounded } from '../../minigit-bench/lib/run.mjs';
import { boxEnv } from '../lib/exec-layer/env.mjs';
import { endProcess, scanProcesses, statOf } from '../lib/exec-layer/proc.mjs';
import { runCases } from '../lib/exec-layer/run.mjs';
import { buildBoxArgv, defaultCaches, snapshotDirOf } from '../lib/workspace-box.mjs';
import { runnerHash } from '../lib/workspace-build.mjs';
import { ensureTestRoot } from './support/cache-box.mjs';
import { chain, needs, sh, waitFor } from './support/exec-chain.mjs';
import { toolchains } from './support/exec-layer-host.mjs';
import { owned } from './support/owned.mjs';
import { assertSnapshotsUnchanged, buildHello, helloWorkspace, inBox, outputOf, programPidIn, readJson, snapshotRecord, snapshotsMissing, STACKS } from './support/workspace-fixtures.mjs';
import { ownedFolder } from './toolchain-fixtures.mjs';

const REPO = path.resolve(fileURLToPath(new URL('../../..', import.meta.url)));
const MIB = 1024 * 1024;
const CHILD = fileURLToPath(new URL('./support/hung-build-child.mjs', import.meta.url));
const TIMEOUT = { timeout: 15 * 60_000 };

before(() => { sweepLeftovers({ repo: REPO, tmp: ensureTestRoot(), token: 'bench' }); });
after(() => { if (snapshotsMissing() === null) assertSnapshotsUnchanged(); });

// ---- the olympiad hello, build and verdicts -------------------------------------------------------------------------------------------------------------

const limitsFile = t => {
  const file = path.join(ownedFolder(t, 'lim'), 'limits.json');
  writeFileSync(file, '{"timeS":10}\n');
  return file;
};

test('The_hello_olympiad_builds_within_600_s_on_the_layer_with_no_registry_and_the_runner_reports_its_verdicts', TIMEOUT, async top => {
  if (!needs(top, snapshotsMissing())) return;
  for (const stack of STACKS) {
    await top.test(stack, async t => {
      const { w, ran } = await buildHello(t, { stack, kind: 'olympiad' });
      assert.equal(ran.timedOut, false);
      assert.ok(ran.ms < 600_000, `the build took ${ran.ms} ms`);
      assert.doesNotMatch(outputOf(ran), /orkload/, 'no line about workloads: the CLI home is the seeded copy');
      t.diagnostic(`${stack} olympiad build: ${ran.ms} ms`);

      const fresh = helloWorkspace(t, { stack, kind: 'olympiad' });
      const literal = await inBox(fresh, stack, ['python3', '-I', 'runner/run_samples.py', 'samples/']);
      assert.equal(literal.code, 0, outputOf(literal).slice(-3000));
      for (const line of ['PASS s01', 'PASS s02', '2 of 2 cases passed']) assert.ok(literal.stdout.includes(line), `${line}: ${literal.stdout.slice(-800)}`);

      const before = treeHash(w);
      const scored = await runCases({
        owned, parent: ownedFolder(t, 'runs'), tree: w, samplesDir: path.join(w, 'samples'), caseIds: ['s01', 's02'], dirs: toolchains, stack, data: true, memoryMaxBytes: 512 * MIB,
        limitsFile: limitsFile(t), caseWallMs: 20_000, runCeilingMs: 120_000, minSweepAgeMs: 0,
    });
    assert.equal(scored.exitCode, 0, scored.stderr);
    assert.deepEqual(scored.record.cases.map(c => [c.id, c.status]), [['s01', 'PASS'], ['s02', 'PASS']]);
    assert.equal(scored.memoryCertified, true);
    assert.equal(scored.cleanup.clean, true, JSON.stringify(scored.cleanup.left));
    assert.equal(treeHash(w), before, 'the built tree is the same after the run: the layer wrote into an overlay');
    const pin = readJson('inputs/specs.json').runner;
    const hash = runnerHash(path.join(w, 'runner'));
    assert.equal(hash.ok && hash.value.runner.manifestSha256, pin.manifestSha256, 'no bytecode was left beside the runner');
    });
  }
});

// ---- the daemon and web hello, served and stopped by the test's driver -----------------------------------------------------------------------------------------

const PORT = 39391;
const CLIENTS = Object.freeze({
  daemon: `import socket, sys, time
for _ in range(200):
    try:
        c = socket.create_connection(('127.0.0.1', ${PORT}), timeout=5)
        break
    except OSError:
        time.sleep(0.05)
else:
    sys.exit(3)
c.sendall(b'world\\n')
data = b''
while len(data) < 12:
    chunk = c.recv(64)
    if not chunk:
        break
    data += chunk
sys.stdout.write(data.decode())
`,
  web: `import http.client, sys, time
for _ in range(200):
    try:
        c = http.client.HTTPConnection('127.0.0.1', ${PORT}, timeout=5)
        c.request('GET', '/hello')
        r = c.getresponse()
        break
    except OSError:
        time.sleep(0.05)
else:
    sys.exit(3)
sys.stdout.write(str(r.status) + ' ' + r.read().decode())
`,
});
const EXPECTED = Object.freeze({ daemon: 'hello world\n', web: '200 {"hello":"world"}' });

test('The_daemon_and_web_hello_build_on_the_layer_and_serve_and_stop', TIMEOUT, async top => {
  if (!needs(top, snapshotsMissing())) return;
  for (const stack of STACKS) {
    for (const kind of ['daemon', 'web']) {
      await top.test(`${stack} ${kind}`, async t => {
        const { w } = await buildHello(t, { stack, kind });
        const parsed = await inBox(w, stack, ['python3', '-I', 'runner/run_samples.py', '--parse-only', 'samples/']);
        assert.equal(parsed.code, 0, outputOf(parsed).slice(-2000));
        assert.ok(parsed.stdout.includes('1 pairs parsed'), parsed.stdout);
        const env = boxEnv({ dirs: toolchains, extra: { PORT: String(PORT) }, stack, memoryMaxBytes: 512 * MIB });
        const c = await chain(t, { view: ownedFolder(t, 'view'), caseId: 'k1', memoryMax: 512 * MIB, holdSeconds: 120, extraBox: { tree: w, env } });
        const child = c.start(['bash', 'run.sh'], { stdio: ['ignore', 'ignore', 'ignore'] });
        const served = await sh(c.driver[0], [...c.driver.slice(1), 'python3', '-I', '-c', CLIENTS[kind]], { timeoutMs: 30_000 });
        assert.equal(served.code, 0, served.stderr);
        assert.equal(served.stdout, EXPECTED[kind]);
        const pid = await waitFor(() => programPidIn(c.prog));
        assert.ok(pid, 'the program is in prog/');
        process.kill(pid, 'SIGTERM');
        const ended = await Promise.race([child.exited, new Promise(resolve => setTimeout(() => resolve('not ended within 10 s'), 10_000))]);
        assert.deepEqual(ended, { code: 0, signal: null }, 'SIGTERM to the program ends it with exit 0');
      });
    }
  }
});

// ---- no route off the host ---------------------------------------------------------------------------------------------------------------------------------

const interfacesOf = text => text.split('\n').slice(2).map(line => line.split(':')[0].trim()).filter(Boolean);

test('The_build_box_has_no_route_off_the_host', TIMEOUT, async t => {
  if (!needs(t, snapshotsMissing())) return;
  const view = ownedFolder(t, 'ws', ensureTestRoot());
  const inside = await inBox(view, 'node', ['cat', '/proc/net/dev']);
  assert.equal(inside.code, 0, outputOf(inside));
  assert.deepEqual(interfacesOf(inside.stdout), ['lo'], 'the build box lists the loopback only');
  const snap = snapshotDirOf({ stack: 'node', record: snapshotRecord(), caches: defaultCaches() }).value;
  const argv = buildBoxArgv({ holder: { pid: 1 }, view, dirs: toolchains, stack: 'node', snap, command: ['cat', '/proc/net/dev'] }).slice(6);
  assert.equal(argv[0], 'bwrap', 'the wrong shape is the same box without the holder\'s namespace');
  const outside = await runBounded(argv[0], argv.slice(1), { timeoutMs: 30_000, env: { PATH: '/usr/sbin:/usr/bin:/bin' } });
  assert.equal(outside.ok && outside.value.code, 0, outside.ok ? outside.value.stderr : outside.reason);
  assert.ok(interfacesOf(outside.value.stdout).some(name => name !== 'lo'), `without the namespace the host's interfaces show: ${interfacesOf(outside.value.stdout)}`);
});

// ---- the snapshot is read-only --------------------------------------------------------------------------------------------------------------------------------

test('A_snapshot_bound_read_only_cannot_be_written_and_is_unchanged', TIMEOUT, async t => {
  if (!needs(t, snapshotsMissing())) return;
  const view = ownedFolder(t, 'ws', ensureTestRoot());
  for (const [stack, part] of [['csharp', 'nuget'], ['csharp', 'dotnet'], ['node', 'npm']]) {
    const snap = snapshotDirOf({ stack, record: snapshotRecord(), caches: defaultCaches() }).value;
    const ran = await inBox(view, stack, ['touch', path.join(snap, part, 'x')]);
    assert.notEqual(ran.code, 0, `${stack}/${part}: a write into the snapshot must fail`);
    const readable = await inBox(view, stack, ['ls', path.join(snap, part)]);
    assert.equal(readable.code, 0, `${stack}/${part}: and it can be read`);
  }
  assertSnapshotsUnchanged();
});

// ---- a hung build ------------------------------------------------------------------------------------------------------------------------------------------------

const hungBuild = (a, b) => `#!/usr/bin/env bash\nsleep ${a} &\nexec sleep ${b}\n`;
const sleepsOf = (a, b) => scanProcesses(argv => argv[0] === 'sleep' && [String(a), String(b)].includes(argv[1]));
const holders = () => scanProcesses(argv => argv[0].startsWith(owned.ownedPrefix('wsh')));

test('A_hung_build_is_killed_whole_and_leaves_no_process_on_the_test_folder', TIMEOUT, async t => {
  if (!needs(t, snapshotsMissing())) return;
  const view = ownedFolder(t, 'ws', ensureTestRoot());
  writeFileSync(path.join(view, 'build.sh'), hungBuild(40001, 40002));
  const pending = inBox(view, 'csharp', ['bash', 'build.sh'], { timeoutMs: 8000 });
  assert.ok(await waitFor(() => sleepsOf(40001, 40002).length === 2 || null, 7000), 'both sleeps of the build were seen running before the timeout: the case cannot pass without them');
  const ran = await pending;
  assert.equal(ran.timedOut, true);
  assert.ok(await waitFor(() => sleepsOf(40001, 40002).length === 0 || null, 5000), `a sleep of the hung build is still running: ${JSON.stringify(sleepsOf(40001, 40002))}`);
  assert.ok(await waitFor(() => holders().filter(h => statOf(h.pid)?.ppid === process.pid).length === 0 || null, 5000), 'the holder of the build box is gone');
  assert.doesNotThrow(() => rmSync(view, { recursive: true, force: true }), 'the folder is removed without an error');
});

/** Starts the hung build in a child process, SIGKILLs that child alone and reports whether the sleeps outlived it. What is left is ended by name. */
async function afterParentKill(t, { keep, sleeps: [a, b] }) {
  const view = ownedFolder(t, 'ws', ensureTestRoot());
  writeFileSync(path.join(view, 'build.sh'), hungBuild(a, b));
  let child = null;
  const finished = runBounded(process.execPath, [CHILD, view, keep ? 'yes' : 'no'], { timeoutMs: 90_000, env: { PATH: '/usr/sbin:/usr/bin:/bin', HOME: os.homedir() }, onChild: started => { child = started; } });
  assert.ok(await waitFor(() => sleepsOf(a, b).length === 2 || null, 30_000), 'both sleeps were seen running');
  const holder = holders().find(identity => statOf(identity.pid)?.ppid === child.pid);
  process.kill(child.pid, 'SIGKILL');
  await finished;
  const gone = (await waitFor(() => sleepsOf(a, b).length === 0 || null, 5000)) === true;
  for (const identity of scanProcesses(argv => argv[0].endsWith('bwrap') && argv.includes(view))) await endProcess(identity, { graceMs: 200 });
  for (const identity of [...sleepsOf(a, b), ...(holder ? [holder] : [])]) await endProcess(identity, { graceMs: 200 });
  return gone;
}

test('A_build_box_ends_with_its_parent_killed_by_SIGKILL_and_that_is_the_point', TIMEOUT, async t => {
  if (!needs(t, snapshotsMissing())) return;
  assert.equal(await afterParentKill(t, { keep: true, sleeps: [40011, 40012] }), true, 'with --die-with-parent no sleep outlives the process that started the box');
  assert.equal(await afterParentKill(t, { keep: false, sleeps: [40021, 40022] }), false, 'the refuted shape, beside the right one: without the flag the sleeps live on');
});
