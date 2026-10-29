import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import { existsSync, mkdirSync, readFileSync, readdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { endCell, realProcApi } from '../lib/janitor.mjs';
import { killTree } from '../../policy-ab-quick/lib/proc.mjs';
import { HEARTBEAT_MS, heartbeat, parseStartTime, processStartTime, readState, retireStale, stateOf, writeState } from '../lib/session-state.mjs';
import { sleep } from './box-helpers.mjs';
import { tmpDir } from './helpers.mjs';

const NOW = Date.parse('2026-10-29T22:00:00Z');
const iso = ms => new Date(ms).toISOString();
const st = (over = {}) => ({ phase: 'running', hostPid: 4242, hostStartTime: '9000', cell: 'c1', startedAt: iso(NOW - 600_000), heartbeatAt: iso(NOW - 1000), ...over });
const aliveIf = (pid, start) => (p, s) => p === pid && s === start;

test('The_start_time_is_field_22_of_stat_read_after_the_last_paren', () => {
  const line = '4242 (we)ird (name) S 1 4242 4242 0 -1 4194560 100 0 0 0 5 2 0 0 20 0 1 0 987654 12345678 100 18446744073709551615 0 0 0 0 0 0 0 0 0 0 0 0 17 3 0 0 0 0 0';
  assert.equal(parseStartTime(line), '987654');
  assert.equal(parseStartTime('garbage'), null);
  assert.equal(processStartTime(process.pid), parseStartTime(readFileSync(`/proc/${process.pid}/stat`, 'utf8')));
  assert.equal(processStartTime(2 ** 22 + 12345), null, 'a pid that does not exist has no start time');
});

test('A_live_owner_is_live_whatever_its_heartbeat_and_a_reused_pid_is_not_the_owner', () => {
  const rows = [
    ['running, owner gone', st(), () => false, 'stale'],
    ['running, owner alive, heartbeat 1 s old', st(), aliveIf(4242, '9000'), 'live'],
    ['running, owner alive, heartbeat 10 min old', st({ heartbeatAt: iso(NOW - 600_000) }), aliveIf(4242, '9000'), 'live'],
    ['running, the pid was reused by another process (start time differs)', st(), aliveIf(4242, '31337'), 'stale'],
    ['starting, owner gone', st({ phase: 'starting' }), () => false, 'stale'],
    ['starting, owner alive', st({ phase: 'starting' }), aliveIf(4242, '9000'), 'live'],
    ['closed', st({ phase: 'closed' }), () => false, 'closed'],
    ['failed', st({ phase: 'failed' }), () => false, 'closed'],
    ['retired', st({ phase: 'retired' }), () => false, 'closed'],
    ['no hostPid', st({ hostPid: undefined }), () => true, 'malformed'],
    ['no start time', st({ hostStartTime: undefined }), () => true, 'malformed'],
    ['an unknown phase', st({ phase: 'dreaming' }), () => true, 'malformed'],
    ['not an object', 'x', () => true, 'malformed'],
    ['null', null, () => true, 'malformed'],
  ];
  for (const [name, json, ownerAlive, expected] of rows) assert.equal(stateOf(json, { now: NOW, ownerAlive }), expected, name);
  assert.equal(HEARTBEAT_MS, 5000);
});

test('State_is_written_atomically_and_a_heartbeat_changes_only_its_stamp', () => {
  const dir = tmpDir('ss');
  const file = path.join(dir, 'state.json');
  assert.deepEqual(readState(file), { ok: false, reason: `no state file at ${file}` });
  writeState(file, st());
  assert.deepEqual(readState(file), { ok: true, value: st() });
  assert.deepEqual(readdirSync(dir), ['state.json'], 'no temp file is left beside it');
  const next = heartbeat(file, NOW);
  assert.equal(next.ok, true);
  assert.deepEqual(readState(file).value, { ...st(), heartbeatAt: iso(NOW) });
  writeFileSync(file, '{not json');
  assert.equal(readState(file).ok, false);
  assert.match(readState(file).reason, /unreadable/);
});

/** A runs root with the four dirs of the story. `net/*.sock` files stand for the sockets a crashed owner left. */
function runsRoot() {
  const root = tmpDir('ss-root');
  const mk = (rid, state) => {
    const dir = path.join(root, rid);
    mkdirSync(path.join(dir, 'net'), { recursive: true });
    writeFileSync(path.join(dir, 'net', 'proxy.sock'), '');
    writeFileSync(path.join(dir, 'net', 'api.sock'), '');
    writeFileSync(path.join(dir, 'stream.ndjson'), 'evidence\n');
    writeFileSync(path.join(dir, 'state.json'), `${JSON.stringify(state)}\n`);
    return dir;
  };
  return { root, mk };
}

test('A_stale_session_is_detected_and_retired_before_reuse', async () => {
  const { root, mk } = runsRoot();
  const deadChild = spawn('true', []);
  await new Promise(r => deadChild.on('close', r));
  const me = { hostPid: process.pid, hostStartTime: processStartTime(process.pid) };
  const marker = `cell-${Math.random().toString(36).slice(2, 9)}`;
  const survivor = spawn('sleep', ['300'], { env: { ...process.env, BENCH_CELL: marker }, stdio: 'ignore' });
  await sleep(150);
  const dirs = {
    deadOwner: mk('dead', st({ hostPid: deadChild.pid, hostStartTime: '1', cell: marker })),
    reusedPid: mk('reused', st({ ...me, hostStartTime: '1', cell: 'cell-reused' })),
    liveOld: mk('liveold', st({ ...me, cell: 'cell-live-old', heartbeatAt: iso(Date.now() - 600_000) })),
    liveFresh: mk('livefresh', st({ ...me, cell: 'cell-live', heartbeatAt: iso(Date.now()) })),
    closed: mk('closed', st({ phase: 'closed', cell: 'cell-closed' })),
  };
  const before = Object.fromEntries(Object.entries(dirs).map(([k, d]) => [k, readFileSync(path.join(d, 'state.json'), 'utf8')]));
  const ended = [];
  try {
    const report = await retireStale({
      root, now: Date.now(), procApi: realProcApi,
      endCell: async args => { ended.push(args.cellId); return endCell({ ...args, graceMs: 300, killTree, procApi: realProcApi }); },
    });
    assert.deepEqual(report.map(r => [r.rid, r.verdict]).sort(), [['closed', 'closed'], ['dead', 'stale'], ['livefresh', 'live'], ['liveold', 'live'], ['reused', 'stale']]);
    assert.deepEqual(ended.sort(), [marker, 'cell-reused'].sort(), 'endCell ran for the stale dirs only, with their cell ids');
    await sleep(300);
    assert.equal(survivor.exitCode !== null || survivor.signalCode !== null, true, 'the survivor carrying the first dir\'s marker was ended');
    for (const k of ['deadOwner', 'reusedPid']) {
      const state = JSON.parse(readFileSync(path.join(dirs[k], 'state.json'), 'utf8'));
      assert.equal(state.phase, 'retired');
      assert.equal(state.retiredBy, process.pid);
      assert.ok(Date.parse(state.retiredAt) > 0);
      assert.deepEqual(readdirSync(path.join(dirs[k], 'net')), [], 'net/*.sock unlinked');
      assert.equal(readFileSync(path.join(dirs[k], 'stream.ndjson'), 'utf8'), 'evidence\n', 'its artefacts are evidence of what a crash cost: nothing else is touched');
    }
    for (const k of ['liveOld', 'liveFresh', 'closed']) {
      assert.equal(readFileSync(path.join(dirs[k], 'state.json'), 'utf8'), before[k], `${k} is untouched`);
      assert.equal(readdirSync(path.join(dirs[k], 'net')).length, 2);
    }
    assert.equal(readdirSync(root).length, 5, 'deletes nothing');
    assert.equal((await retireStale({ root, now: Date.now(), procApi: realProcApi, endCell: async () => assert.fail('a retired dir is not retired twice') })).filter(r => r.verdict === 'stale').length, 0);
  } finally {
    survivor.kill('SIGKILL');
  }
});

test('Dirs_without_a_state_file_or_with_a_malformed_one_are_reported_and_never_touched', async () => {
  const { root, mk } = runsRoot();
  mkdirSync(path.join(root, 'nostate'));
  const bad = mk('bad', {});
  writeFileSync(path.join(bad, 'state.json'), '{nope');
  const report = await retireStale({ root, now: Date.now(), procApi: realProcApi, endCell: async () => assert.fail('nothing to end') });
  assert.deepEqual(report.map(r => [r.rid, r.verdict]).sort(), [['bad', 'malformed'], ['nostate', 'malformed']]);
  assert.equal(existsSync(path.join(bad, 'net', 'proxy.sock')), true);
  assert.deepEqual(await retireStale({ root: path.join(root, 'absent'), now: Date.now(), procApi: realProcApi, endCell: async () => {} }), [], 'a runs root that does not exist yet has nothing stale');
});
