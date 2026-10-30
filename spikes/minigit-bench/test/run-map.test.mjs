// The series run-map and the resume state machine (E3b.S1 step 11): written atomically, transitions checked, resume computed from what the files say.
import assert from 'node:assert/strict';
import { existsSync, readdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { checkResumable, markStopped, openSeries, planSession, readRunMap, resumePlan, transition, writeRunMap } from '../lib/run-map.mjs';
import { tmpDir } from './helpers.mjs';

const PINS = { claude: '2.1.310', node: '26.12.1', dotnetSdk: '10.0.113', typescript: '7.0.2' };
const identity = 'a'.repeat(64);
const series = () => openSeries({
  seriesId: 's-20261030-ab12', seedSha256: 'b'.repeat(64), harnessIdentity: identity, preregTag: 'prereg/minigit-bare-20261030', preregCommit: 'c'.repeat(40), pins: PINS,
  stacksSha256: 'd'.repeat(64), pricesSha256: 'e'.repeat(64), snapshots: { csharp: 'f'.repeat(64), node: '1'.repeat(64) }, caps: { capUsd: 6, wallMs: 3_600_000, stallMs: 2_700_000 },
  bareBudgetUsd: 40, ceilingUsd: 500, spin: { medianMs: 20 },
});
const session = (rid, over = {}) => ({ rid, pair: 1, stack: 'csharp', phase: 'v1', attempt: 1, kind: 'data', label: 'paired', ...over });
const must = r => { assert.equal(r.ok, true, r.reason); return r.value; };

test('The_run_map_is_written_atomically_and_read_back_equal', () => {
  const dir = tmpDir('series');
  const file = path.join(dir, 'run-map.json');
  const map = must(planSession(series(), session('aaaaaa')));
  writeRunMap(file, map);
  assert.deepEqual(readdirSync(dir), ['run-map.json'], 'no temp file is left beside it');
  assert.deepEqual(readRunMap(file), { ok: true, value: map });
  writeFileSync(file, '{ not json');
  const bad = readRunMap(file);
  assert.equal(bad.ok, false);
  assert.match(bad.reason, /unreadable/);
  assert.equal(readRunMap(path.join(dir, 'absent.json')).ok, false);
});

test('A_session_moves_planned_launching_ran_scored_and_no_other_way', () => {
  let map = must(planSession(series(), session('aaaaaa')));
  assert.equal(map.sessions.aaaaaa.status, 'planned');
  map = must(transition(map, 'aaaaaa', 'launching', { startedAt: 't1' }));
  map = must(transition(map, 'aaaaaa', 'ran', { class: 'completed', data: true }));
  assert.equal(map.sessions.aaaaaa.class, 'completed');
  assert.equal(transition(map, 'aaaaaa', 'launching').ok, false, 'a ran session is never launched again');
  map = must(transition(map, 'aaaaaa', 'scored'));
  assert.equal(transition(map, 'aaaaaa', 'ran').ok, false);
  assert.equal(transition(map, 'zzzzzz', 'ran').ok, false, 'an unknown rid');
  assert.equal(planSession(map, session('aaaaaa')).ok, false, 'a rid is never planned twice');
  const reset = must(transition(must(transition(must(planSession(series(), session('bbbbbb'))), 'bbbbbb', 'launching')), 'bbbbbb', 'planned'));
  assert.equal(reset.sessions.bbbbbb.status, 'planned', 'launching -> planned: the dir is gone and nothing was paid');
});

test('The_map_is_a_new_value_and_the_old_one_is_unchanged', () => {
  const a = series();
  const b = must(planSession(a, session('aaaaaa')));
  assert.deepEqual(a.sessions, {});
  assert.notEqual(a, b);
});

// ---- resume ---------------------------------------------------------------------------------------------------------------------

const withStatuses = list => list.reduce((m, [rid, status, patch = {}]) => {
  let next = must(planSession(m, session(rid, { stack: patch.stack ?? 'csharp' })));
  if (status !== 'planned') next = must(transition(next, rid, 'launching'));
  if (['ran', 'scored'].includes(status)) next = must(transition(next, rid, 'ran', { class: 'completed', data: true }));
  if (status === 'scored') next = must(transition(next, rid, 'scored'));
  return next;
}, series());

test('Resume_after_a_crash_continues_where_the_files_say', () => {
  const map = withStatuses([['p00001', 'planned'], ['l00001', 'launching'], ['l00002', 'launching'], ['l00003', 'launching'], ['r00001', 'ran'], ['s00001', 'scored']]);
  const dirs = {
    l00001: { exists: true, hasMeta: true, retired: false, paidWork: true }, // finished: meta.json written
    l00002: { exists: true, hasMeta: false, retired: true, paidWork: true }, // crashed with paid work
    l00003: { exists: false, hasMeta: false, retired: false, paidWork: false }, // dir gone: nothing paid
    r00001: { exists: true, hasMeta: true, retired: false, paidWork: true },
    s00001: { exists: true, hasMeta: true, retired: false, paidWork: true },
  };
  const plan = resumePlan(map, dirs);
  const by = Object.fromEntries(plan.map(a => [a.rid, a.action]));
  assert.deepEqual(by, { p00001: 'launch', l00001: 'adopt', l00002: 'void-interrupted', l00003: 'reset', r00001: 'score', s00001: 'rebuild-record' });
  assert.ok(Object.values(by).every(a => a !== 'launch' || ['p00001'].includes(Object.keys(by).find(k => by[k] === a))), 'no session with a data outcome is launched again');
});

test('A_launching_session_whose_dir_exists_without_meta_or_paid_work_is_reset_not_charged', () => {
  const map = withStatuses([['l00001', 'launching']]);
  assert.equal(resumePlan(map, { l00001: { exists: true, hasMeta: false, retired: true, paidWork: false } })[0].action, 'reset');
});

test('Every_cut_of_a_session_history_resumes_to_exactly_the_remaining_steps', () => {
  const order = ['planned', 'launching', 'ran', 'scored'];
  const expected = { planned: 'launch', ran: 'score', scored: 'rebuild-record' };
  for (const status of ['planned', 'ran', 'scored']) {
    const map = withStatuses([['x00001', status]]);
    const dirs = { x00001: { exists: status !== 'planned', hasMeta: status !== 'planned', retired: false, paidWork: status !== 'planned' } };
    assert.equal(resumePlan(map, dirs)[0].action, expected[status], `cut at ${status} (${order.indexOf(status)})`);
  }
});

test('A_ran_session_whose_directory_is_gone_is_lost_never_silently_re_run', () => {
  const map = withStatuses([['x00001', 'ran']]);
  assert.equal(resumePlan(map, { x00001: { exists: false, hasMeta: false, retired: false, paidWork: false } })[0].action, 'lost');
  assert.equal(resumePlan(map, {})[0].action, 'lost', 'a session with no directory record at all');
});

// ---- the refusal -----------------------------------------------------------------------------------------------------------------

const current = () => ({ pins: PINS, stacksSha256: 'd'.repeat(64), pricesSha256: 'e'.repeat(64), snapshots: { csharp: 'f'.repeat(64), node: '1'.repeat(64) }, preregTag: 'prereg/minigit-bare-20261030', seedSha256: 'b'.repeat(64), harnessIdentity: identity });

test('Resume_refuses_a_changed_pin_snapshot_or_seed_hash', () => {
  const map = series();
  assert.equal(checkResumable({ map, current: current() }).ok, true);
  const changes = {
    pins: { ...PINS, claude: '2.1.311' }, stacksSha256: '0'.repeat(64), pricesSha256: '0'.repeat(64), snapshots: { csharp: '0'.repeat(64), node: '1'.repeat(64) },
    preregTag: 'prereg/minigit-bare-20261101', seedSha256: '0'.repeat(64),
  };
  for (const [field, value] of Object.entries(changes)) {
    const r = checkResumable({ map, current: { ...current(), [field]: value } });
    assert.equal(r.ok, false, field);
    assert.match(r.reason, new RegExp(field), `the refusal names ${field}`);
  }
});

test('A_harness_change_is_accepted_only_with_a_flag_and_a_dated_amendment_line_naming_it', () => {
  const map = series();
  const changed = { ...current(), harnessIdentity: '9'.repeat(64) };
  assert.equal(checkResumable({ map, current: changed }).ok, false);
  const amendments = `# header\n2026-11-02 | instrument | harness:${'9'.repeat(12)} | the sampler interval was fixed (G-11)\n`;
  const accepted = checkResumable({ map, current: changed, acceptHarness: { identity: '9'.repeat(64), amendmentDate: '2026-11-02' }, amendments });
  assert.equal(accepted.ok, true, accepted.reason);
  assert.deepEqual(accepted.value.accepted, { from: identity, to: '9'.repeat(64), amendmentDate: '2026-11-02' }, 'recorded for the ledger');
  assert.equal(checkResumable({ map, current: changed, acceptHarness: { identity: '9'.repeat(64), amendmentDate: '2026-11-03' }, amendments }).ok, false, 'no line of that date');
  assert.equal(checkResumable({ map, current: changed, acceptHarness: { identity: '8'.repeat(64), amendmentDate: '2026-11-02' }, amendments }).ok, false, 'the flag names another identity than the one running');
  assert.equal(checkResumable({ map, current: changed, acceptHarness: { identity: '9'.repeat(64), amendmentDate: '2026-11-02' }, amendments: '# header only\n' }).ok, false, 'no amendment line at all');
  assert.equal(checkResumable({ map: markStopped(map, { rule: 'stop: auth', reason: 'x', at: 't' }), current: current() }).ok, false, 'a series stop is not passed by resume');
});
