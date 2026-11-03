// Fix batch 4 — the probe's stop path and its end: W2 (an incomplete cleanup is not "clean": exit 75), W3 (`probedOn` is stamped BEFORE the record is written), W5 (the stop says so), N5 (the
// reservation is released once), B1 (the build box dies with its parent and every child the probe starts is ended).
import assert from 'node:assert/strict';
import { spawn, spawnSync } from 'node:child_process';
import { existsSync, mkdirSync, readdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { boxArgv } from '../lib/exec-layer/box.mjs';
import { computeIdentity, LAYER_DIR } from '../lib/exec-layer/identity.mjs';
import { makeReservation, reserveRecord } from '../lib/exec-probe/record-path.mjs';
import { INCOMPLETE_EXIT, runStop, stopCleanly } from '../lib/exec-probe/stop.mjs';
import { needs, spikeDir, toolchains } from './support/exec-layer-host.mjs';
import { idsOf, markerFolder, pidsMentioning, privateProbeEnv } from './support/probe-scope.mjs';
import { owned } from './support/owned.mjs';

const lines = () => { const said = []; return { said, say: text => said.push(text) }; };
const never = () => new Promise(() => {});

// W2 + W5
test('A_stop_says_it_is_stopping_at_once_and_says_when_the_cleanup_ended', async () => {
  const { said, say } = lines();
  const exits = [];
  await stopCleanly({ code: 143, bound: 1000, say, exit: code => exits.push(code), release: () => said.push('released'), describeLive: () => [{ id: 'abc', unitPrefix: 'u-abc', dir: '/r/abc' }, { id: 'def', unitPrefix: 'u-def', dir: '/r/def' }], cleanLive: async () => [{ clean: true, left: [] }, { clean: true, left: [] }], closeScope: async () => {} });
  assert.match(said[0], /^exec-probe: stopping — cleaning up 2 open run\(s\)$/, 'the first line, before anything is cleaned');
  assert.equal(said.indexOf('released') > 0, true);
  assert.match(said.at(-1), /^exec-probe: cleanup finished: nothing is left$/);
  assert.deepEqual(exits, [143], 'a complete cleanup exits with the signal\'s own code');
});

test('A_cleanup_that_hits_its_bound_is_not_clean_it_names_what_is_left_and_exits_75', async () => {
  const { said, say } = lines();
  const exits = [];
  const closed = [];
  const started = Date.now();
  await stopCleanly({ code: 143, bound: 60, say, exit: code => exits.push(code), release: () => {}, describeLive: () => [{ id: 'abc', unitPrefix: 'testsToDelete-u1-bench-lsc-abc', dir: '/r/testsToDelete-u1-bench-lrun-abc' }], cleanLive: never, closeScope: async () => closed.push('closed') });
  assert.ok(Date.now() - started < 3000, 'the bound ends the wait');
  assert.deepEqual(exits, [INCOMPLETE_EXIT], 'a distinct code: not 143, not 0');
  assert.equal(INCOMPLETE_EXIT, 75);
  const text = said.join('\n');
  assert.match(text, /cleanup incomplete after 60 ms/);
  assert.match(text, /testsToDelete-u1-bench-lsc-abc/, 'it names the scope of the run that is still there');
  assert.match(text, /testsToDelete-u1-bench-lrun-abc/, 'and its run folder');
  assert.match(text, /the next start's sweep retires them/);
  assert.deepEqual(closed, [], 'the run marker and the owned folders stay: the sweep needs them to find what is left');
});

test('A_cleanup_that_finishes_but_leaves_something_is_also_exit_75_and_names_it', async () => {
  const { said, say } = lines();
  const exits = [];
  await stopCleanly({ code: 130, bound: 1000, say, exit: code => exits.push(code), release: () => {}, describeLive: () => [{ id: 'x', unitPrefix: 'p', dir: '/r/x' }], cleanLive: async () => [{ clean: false, left: [{ kind: 'scope', name: 'testsToDelete-u1-bench-lsc-x-certify.scope', reason: 'still listed after systemctl stop' }] }], closeScope: async () => {} });
  assert.deepEqual(exits, [INCOMPLETE_EXIT]);
  assert.match(said.join('\n'), /testsToDelete-u1-bench-lsc-x-certify\.scope/);
});

// N5
test('A_reservation_is_released_once_and_never_unlinks_another_probes_new_placeholder', t => {
  const dir = owned.ownedDir(t, 'n5');
  const file = path.join(dir, 'r.json');
  const mine = makeReservation(reserveRecord(file));
  mine.release();
  assert.equal(readdirSync(dir).length, 0, 'the first release removes the empty placeholder');
  const theirs = reserveRecord(file); // another probe takes the same path
  mine.release(); // the exit handler runs release again
  assert.equal(readdirSync(dir).length, 1, 'the second release finds nothing held and touches nothing');
  assert.ok(theirs);
  const taken = makeReservation(reserveRecord(path.join(dir, 's.json')));
  assert.ok(taken.take(), 'a written record takes the reservation: nothing is left to release');
  taken.release();
  assert.equal(readdirSync(dir).length, 2, 'a taken reservation is never released');
});

// B1
test('The_build_box_dies_with_its_parent_and_a_program_box_does_not', () => {
  const dirs = toolchains;
  const build = boxArgv({ view: '/v', dirs, env: {}, prog: null });
  const program = boxArgv({ view: '/v', dirs, env: {}, prog: '/sys/fs/cgroup/x', tree: '/t' });
  assert.ok(build.includes('--die-with-parent'), 'a build box ends when its parent does: a build must not outlive the probe that started it');
  assert.ok(!program.includes('--die-with-parent'), 'the program box has none: the runner ends it by its cgroup (the layer\'s rule)');
});

test('Every_child_the_probe_starts_is_tracked_and_ended_by_the_stop_cleanup', async t => {
  const { markerFile } = markerFolder(t, 'tracked'); // the kill hook is registered BEFORE the folder, and the child writes its marker there FIRST (LOW 8, M2)
  const script = `
    import { runBounded } from ${JSON.stringify(new URL('../../minigit-bench/lib/run.mjs', import.meta.url).href)};
    import { cleanupLiveRuns, trackChild } from ${JSON.stringify(new URL('../lib/exec-layer/cleanup.mjs', import.meta.url).href)};
    const marker = 'testsToDelete-u0-bench-trackedchild-' + process.pid;
    process.getBuiltinModule('node:fs').writeFileSync(${JSON.stringify(markerFile)}, marker);
    const running = runBounded('bash', ['-c', 'exec -a ' + marker + ' sleep 30'], { timeoutMs: 100000, onChild: trackChild });
    await new Promise(resolve => setTimeout(resolve, 500));
    const pidOf = () => { const fs = process.getBuiltinModule('node:fs'); return fs.readdirSync('/proc').filter(e => /^\\d+$/.test(e)).filter(pid => { try { return fs.readFileSync('/proc/' + pid + '/cmdline', 'utf8').startsWith(marker); } catch { return false; } }); };
    const before = pidOf();
    await cleanupLiveRuns();
    await new Promise(resolve => setTimeout(resolve, 500));
    console.log(JSON.stringify({ before: before.length, after: pidOf().length, marker }));
    process.exit(0);
  `;
  const ran = spawnSync(process.execPath, ['--input-type=module', '-e', script], { encoding: 'utf8', timeout: 30_000 });
  const result = JSON.parse(ran.stdout.trim().split('\n').pop());
  assert.deepEqual({ before: result.before, after: result.after }, { before: 1, after: 0 }, ran.stderr);
});

test('A_probe_stopped_by_SIGTERM_during_the_C_sharp_build_leaves_no_build_process_behind', async t => {
  if (!needs(t)) return;
  const dir = owned.ownedDir(t, 'b1');
  const out = path.join(dir, 'record.json');
  const pin = path.join(dir, 'exec-layer.json');
  writeFileSync(pin, JSON.stringify({ execLayer: { treeSha256: computeIdentity(LAYER_DIR).value.execLayer.treeSha256, files: [] } }));
  const scoped = privateProbeEnv(t);
  const child = spawn(process.execPath, [path.join(spikeDir, 'exec-probe.mjs'), '--out', out, '--pinned-identity', pin, '--no-specs'], { stdio: ['ignore', 'pipe', 'pipe'], env: scoped.env });
  const builds = () => [...idsOf(scoped)].flatMap(id => pidsMentioning(`-probebuild-${id}`)); // the build box's owned marker is in its argv
  t.after(() => { child.kill('SIGKILL'); for (const pid of builds()) { try { process.kill(Number(pid), 'SIGKILL'); } catch { /* gone */ } } });
  const deadline = Date.now() + 60_000;
  while (builds().length === 0 && Date.now() < deadline) await new Promise(resolve => setTimeout(resolve, 50));
  assert.ok(builds().length > 0, 'the build is running, with the owned marker in its argv');
  const ids = idsOf(scoped);
  const closed = new Promise(resolve => child.once('close', (code) => resolve(code)));
  child.kill('SIGTERM');
  const code = await closed;
  assert.ok([143, 75].includes(code), `a clean stop (${code})`);
  const end = Date.now() + 5000;
  const left = () => [...ids].flatMap(id => pidsMentioning(`-probebuild-${id}`));
  while (left().length > 0 && Date.now() < end) await new Promise(resolve => setTimeout(resolve, 100));
  assert.deepEqual(left(), [], 'no `dotnet build` of the probe is left, whichever way the stop came: the build box died with its parent and the child was ended');
});

// C1 / C3 / C4 (fix batch 5)
const common = (said, exits) => ({ code: 143, bound: 1000, say: text => said.push(text), exit: code => exits.push(code), release: () => {}, describeLive: () => [], cleanLive: async () => [{ clean: true, left: [] }] });

test('A_scope_close_that_never_ends_or_rejects_is_an_incomplete_cleanup_exit_75_and_never_nothing_is_left', async () => {
  const hung = { said: [], exits: [] };
  await stopCleanly({ ...common(hung.said, hung.exits), closeBound: 50, closeScope: never });
  assert.deepEqual(hung.exits, [INCOMPLETE_EXIT], 'a close that hit its bound is not clean');
  assert.match(hung.said.join('\n'), /owned folders .*did not finish within 50 ms/);
  assert.doesNotMatch(hung.said.join('\n'), /nothing is left/, 'the clean line is printed only for a clean close');
  const rejected = { said: [], exits: [] };
  await stopCleanly({ ...common(rejected.said, rejected.exits), closeScope: async () => { throw new Error('EBUSY'); } });
  assert.deepEqual(rejected.exits, [INCOMPLETE_EXIT]);
  assert.match(rejected.said.join('\n'), /owned folders .*EBUSY/);
  assert.doesNotMatch(rejected.said.join('\n'), /nothing is left/);
});

test('The_timeout_path_names_only_the_runs_still_open_after_the_attempt', async () => {
  const said = [];
  const exits = [];
  const snapshots = [[{ id: 'one', unitPrefix: 'p-lsc-one', dir: '/r/one' }, { id: 'two', unitPrefix: 'p-lsc-two', dir: '/r/two' }], [{ id: 'two', unitPrefix: 'p-lsc-two', dir: '/r/two' }]];
  await stopCleanly({ ...common(said, exits), bound: 40, describeLive: () => snapshots.shift() ?? snapshots.at(-1) ?? [{ id: 'two', unitPrefix: 'p-lsc-two', dir: '/r/two' }], cleanLive: never });
  const text = said.join('\n');
  assert.match(text, /stopping — cleaning up 2 open run\(s\)/, 'the first line counts the runs open at the start');
  assert.match(text, /run two/, 'the run still open is named');
  assert.doesNotMatch(text.split('cleanup incomplete')[1] ?? '', /run one/, 'the run that ended during the attempt is not');
});

test('A_stuck_cleanup_with_no_handle_left_still_reaches_exit_75_because_the_timer_is_not_unreffed', () => {
  const script = `
    import { stopCleanly } from ${JSON.stringify(new URL('../lib/exec-probe/stop.mjs', import.meta.url).href)};
    await stopCleanly({ code: 143, bound: 300, say: text => process.stderr.write(text + '\\n'), exit: process.exit, release() {}, describeLive: () => [], cleanLive: () => new Promise(() => {}), closeScope: async () => {} });
  `;
  const ran = spawnSync(process.execPath, ['--input-type=module', '-e', script], { encoding: 'utf8', timeout: 15_000 });
  assert.equal(ran.status, INCOMPLETE_EXIT, `a process whose event loop is empty must not exit 0 on its own: ${ran.stderr}`);
  assert.match(ran.stderr, /cleanup incomplete after 300 ms/);
});

test('A_rejection_of_the_stop_itself_ends_the_process_with_75_and_one_stderr_line', async () => {
  const said = [];
  const exits = [];
  await runStop({ ...common(said, exits), describeLive: () => { throw new Error('boom'); } });
  assert.deepEqual(exits, [INCOMPLETE_EXIT]);
  assert.equal(said.length, 1);
  assert.match(said[0], /^exec-probe: the stop itself failed \(boom\); exit 75: the next start's sweep retires what is left$/);
});

// L1: the network lines' own children (the driver in the holder's namespace, the server in the box) are tracked, so a stop during them leaves no process of the probe.
test('A_probe_stopped_during_the_network_lines_leaves_no_driver_server_or_other_process_of_its_own', async t => {
  if (!needs(t)) return;
  const dir = owned.ownedDir(t, 'l1');
  const out = path.join(dir, 'record.json');
  const pin = path.join(dir, 'exec-layer.json');
  writeFileSync(pin, JSON.stringify({ execLayer: { treeSha256: computeIdentity(LAYER_DIR).value.execLayer.treeSha256, files: [] } }));
  const scoped = privateProbeEnv(t);
  const child = spawn(process.execPath, [path.join(spikeDir, 'exec-probe.mjs'), '--out', out, '--pinned-identity', pin, '--no-specs'], { stdio: ['ignore', 'pipe', 'pipe'], env: scoped.env });
  const own = () => [...idsOf(scoped)].flatMap(id => [`-probedriver-${id}`, `-probeserver-${id}`, `-lhold-${id}`].flatMap(text => pidsMentioning(text)));
  t.after(() => { child.kill('SIGKILL'); for (const pid of own()) { try { process.kill(Number(pid), 'SIGKILL'); } catch { /* gone */ } } });
  let stderr = '';
  child.stderr.on('data', chunk => { stderr += chunk; });
  const deadline = Date.now() + 120_000;
  const started = () => /network: start/.test(stderr) && own().length >= 2; // the network lines run: the holder and at least the driver (or the server) exist
  while (!started() && Date.now() < deadline) await new Promise(resolve => setTimeout(resolve, 10));
  const ids = new Set(idsOf(scoped));
  assert.ok(ids.size > 0 && own().length >= 2, 'the network lines are running: the probe\'s own processes exist');
  const closed = new Promise(resolve => child.once('close', code => resolve(code)));
  child.kill('SIGTERM');
  assert.ok([143, 75].includes(await closed));
  const left = () => [...ids].flatMap(id => [`-probedriver-${id}`, `-probeserver-${id}`, `-lhold-${id}`].flatMap(text => pidsMentioning(text)));
  const end = Date.now() + 5000;
  while (left().length > 0 && Date.now() < end) await new Promise(resolve => setTimeout(resolve, 100));
  assert.deepEqual(left(), [], 'no driver, server or holder of the probe is left');
});

// L4: a refusal inside the probe's `try` runs the `finally`: the owned folders are closed.
test('A_probe_whose_C_sharp_program_does_not_build_is_refused_with_exit_2_and_leaves_no_probebuild_folder', async t => {
  if (!needs(t)) return;
  const dir = owned.ownedDir(t, 'l4');
  const pin = path.join(dir, 'exec-layer.json');
  writeFileSync(pin, JSON.stringify({ execLayer: { treeSha256: computeIdentity(LAYER_DIR).value.execLayer.treeSha256, files: [] } }));
  const scoped = privateProbeEnv(t);
  const home = path.join(scoped.root, 'home'); // no toolchain store under it: the build box cannot bind the SDK, so the build fails
  mkdirSync(home);
  const ran = spawnSync(process.execPath, [path.join(spikeDir, 'exec-probe.mjs'), '--out', path.join(dir, 'record.json'), '--pinned-identity', pin, '--no-specs'], { encoding: 'utf8', timeout: 120_000, env: { ...scoped.env, HOME: home } });
  assert.equal(ran.status, 2, ran.stderr.slice(-400));
  assert.match(ran.stderr, /did not build/);
  assert.deepEqual(readdirSync(scoped.tmp), [], 'the refusal ran the finally: no probebuild folder, no claim, no owned folder is left in the probe\'s temp root');
  assert.equal(existsSync(path.join(dir, 'record.json')), false, 'and no empty record');
});

// K4 (coai 2): a SIGTERM after the record whose folder close fails says what may be left; the exit stays the record's code.
test('A_stop_after_the_record_whose_owned_folder_close_times_out_or_fails_says_so_and_keeps_the_records_code', async () => {
  for (const [name, closeScope] of [['hangs', never], ['rejects', async () => { throw new Error('EBUSY'); }]]) {
    const said = [];
    const exits = [];
    await stopCleanly({ code: 143, recordedCode: () => 0, closeBound: 50, bound: 1000, say: text => said.push(text), exit: code => exits.push(code), release: () => {}, describeLive: () => [], cleanLive: async () => [], closeScope });
    assert.deepEqual(exits, [0], `${name}: the exit stays the record's code`);
    const text = said.join('\n');
    assert.match(text, /owned folders did not finish closing \((did not finish within 50 ms|EBUSY)\)/, `${name}: one line names what may be left`);
    assert.equal(said.filter(line => /did not finish closing/.test(line)).length, 1);
  }
});

// K5 (Opus M2): `t.after` hooks run in REGISTRATION order, so a kill hook that reads a marker file from an owned folder must be registered BEFORE the folder (whose own removal hook is registered by `ownedDir`).
test('The_marker_kill_hook_really_kills_a_hanging_child_before_its_folder_is_removed', async t => {
  const marker = `testsToDelete-u0-bench-hangingchild-${process.pid}-${Date.now()}`;
  const { markerFile } = markerFolder(t, 'hang');
  t.after(() => assert.deepEqual(pidsMentioning(marker), [], 'the hook killed the hanging child: it is gone after the hooks')); // registered AFTER the helper: runs after the kill hook
  const child = spawn('bash', ['-c', `exec -a ${marker} sleep 30`], { stdio: 'ignore', detached: true });
  child.unref();
  writeFileSync(markerFile, marker);
  const end = Date.now() + 5000;
  while (pidsMentioning(marker).length === 0 && Date.now() < end) await new Promise(resolve => setTimeout(resolve, 20));
  assert.ok(pidsMentioning(marker).length > 0, 'the child hangs while the test body ends');
});
