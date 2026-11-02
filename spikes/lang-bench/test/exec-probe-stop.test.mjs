// Fix batch 4 — the probe's stop path and its end: W2 (an incomplete cleanup is not "clean": exit 75), W3 (`probedOn` is stamped BEFORE the record is written), W5 (the stop says so), N5 (the
// reservation is released once), B1 (the build box dies with its parent and every child the probe starts is ended).
import assert from 'node:assert/strict';
import { spawn, spawnSync } from 'node:child_process';
import { readdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { boxArgv } from '../lib/exec-layer/box.mjs';
import { computeIdentity, LAYER_DIR } from '../lib/exec-layer/identity.mjs';
import { shouldStampProbedOn, stampThenWrite } from '../lib/exec-probe/probe.mjs';
import { makeReservation, reserveRecord } from '../lib/exec-probe/record-path.mjs';
import { INCOMPLETE_EXIT, stopCleanly } from '../lib/exec-probe/stop.mjs';
import { needs, spikeDir, toolchains } from './support/exec-layer-host.mjs';
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

// W3
test('probedOn_is_stamped_before_the_record_is_written_and_a_failed_stamp_gives_a_record_that_says_exit_2', () => {
  const order = [];
  const record = { exit: { code: 0, halt: null }, lines: [{ id: 'a', ok: true }], probedOn: 'now' };
  const good = stampThenWrite({ record, noSpecs: false, stamp: () => order.push('stamp'), write: written => { order.push('write'); order.push(written.exit.code); } });
  assert.deepEqual(order, ['stamp', 'write', 0], 'stamp first, then write');
  assert.equal(good.code, 0);
  const failing = [];
  const bad = stampThenWrite({ record, noSpecs: false, stamp: () => { throw new Error('disk full'); }, write: written => failing.push(written) });
  assert.equal(failing.length, 1, 'the record is still written');
  assert.deepEqual([failing[0].exit.code, failing[0].exit.halt, failing[0].exit.stampError], [2, 'probed-on-stamp-failed', 'disk full'], 'and it says 2 with the error: the file never says 0 when the command says 2');
  assert.equal(bad.code, 2);
  const calls = [];
  stampThenWrite({ record: { ...record, exit: { code: 3, halt: 'memory-view-mismatch' } }, noSpecs: false, stamp: () => calls.push('stamp'), write: () => calls.push('write') });
  stampThenWrite({ record, noSpecs: true, stamp: () => calls.push('stamp'), write: () => calls.push('write') });
  assert.deepEqual(calls, ['write', 'write'], 'no stamp on an exit other than 0, nor with --no-specs');
  assert.equal(shouldStampProbedOn(record), true);
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

test('Every_child_the_probe_starts_is_tracked_and_ended_by_the_stop_cleanup', async () => {
  const script = `
    import { runBounded } from ${JSON.stringify(new URL('../../minigit-bench/lib/run.mjs', import.meta.url).href)};
    import { cleanupLiveRuns, trackChild } from ${JSON.stringify(new URL('../lib/exec-layer/cleanup.mjs', import.meta.url).href)};
    const marker = 'testsToDelete-u0-bench-trackedchild-' + process.pid;
    const running = runBounded('bash', ['-c', 'exec -a ' + marker + ' sleep 300'], { timeoutMs: 100000, onChild: trackChild });
    await new Promise(resolve => setTimeout(resolve, 500));
    const pidOf = () => { const fs = process.getBuiltinModule('node:fs'); return fs.readdirSync('/proc').filter(e => /^\\d+$/.test(e)).filter(pid => { try { return fs.readFileSync('/proc/' + pid + '/cmdline', 'utf8').startsWith(marker); } catch { return false; } }); };
    const before = pidOf();
    await cleanupLiveRuns();
    await new Promise(resolve => setTimeout(resolve, 500));
    console.log(JSON.stringify({ before: before.length, after: pidOf().length }));
    process.exit(0);
  `;
  const ran = spawnSync(process.execPath, ['--input-type=module', '-e', script], { encoding: 'utf8', timeout: 30_000 });
  assert.deepEqual(JSON.parse(ran.stdout.trim().split('\n').pop()), { before: 1, after: 0 }, ran.stderr);
});

test('A_probe_stopped_by_SIGTERM_during_the_C_sharp_build_leaves_no_build_process_behind', async t => {
  if (!needs(t)) return;
  const dir = owned.ownedDir(t, 'b1');
  const out = path.join(dir, 'record.json');
  const pin = path.join(dir, 'exec-layer.json');
  writeFileSync(pin, JSON.stringify({ execLayer: { treeSha256: computeIdentity(LAYER_DIR).value.execLayer.treeSha256, files: [] } }));
  const TOKEN = 'benchbld';
  const marked = () => readdirSync('/proc').filter(entry => /^\d+$/.test(entry)).filter(pid => { try { return readFileSync(`/proc/${pid}/cmdline`, 'utf8').split('\0').some(arg => arg.includes(`-${TOKEN}-probebuild-`)); } catch { return false; } });
  assert.deepEqual(marked(), [], 'nothing of the kind before');
  const child = spawn(process.execPath, [path.join(spikeDir, 'exec-probe.mjs'), '--out', out, '--pinned-identity', pin, '--owned-token', TOKEN, '--no-specs'], { stdio: ['ignore', 'pipe', 'pipe'] });
  t.after(() => { child.kill('SIGKILL'); for (const pid of marked()) { try { process.kill(Number(pid), 'SIGKILL'); } catch { /* gone */ } } });
  const deadline = Date.now() + 60_000;
  while (marked().length === 0 && Date.now() < deadline) await new Promise(resolve => setTimeout(resolve, 50));
  assert.ok(marked().length > 0, 'the build is running, with the owned marker in its argv');
  const closed = new Promise(resolve => child.once('close', (code) => resolve(code)));
  child.kill('SIGTERM');
  const code = await closed;
  assert.ok([143, 75].includes(code), `a clean stop (${code})`);
  const end = Date.now() + 5000;
  while (marked().length > 0 && Date.now() < end) await new Promise(resolve => setTimeout(resolve, 100));
  assert.deepEqual(marked(), [], 'no `dotnet build` of the probe is left, whichever way the stop came: the build box died with its parent and the child was ended');
});
