// The series run-map and the resume state machine (E3b.S1 step 11): written atomically, transitions checked, resume computed from what the files say.
import assert from 'node:assert/strict';
import { existsSync, readdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { checkResumable, hasDataSession, markStopped, openSeries, planSession, readRunMap, resumePlan, transition, writeRunMap } from '../lib/run-map.mjs';
import { tmpDir } from './helpers.mjs';

const PINS = { claude: '2.1.310', node: '26.12.1', dotnetSdk: '10.0.113', typescript: '7.0.2' };
const identity = 'a'.repeat(64);
const series = () => openSeries({
  seriesId: 's-20261030-ab12', seedSha256: 'b'.repeat(64), harnessIdentity: identity, preregTag: 'prereg/minigit-bare-20261030', preregCommit: 'c'.repeat(40), pins: PINS,
  stacksSha256: 'd'.repeat(64), pricesSha256: 'e'.repeat(64), quietSha256: '2'.repeat(64), packageFlagsSha256: '3'.repeat(64), snapshots: { csharp: 'f'.repeat(64), node: '1'.repeat(64) }, caps: { capUsd: 6, wallMs: 3_600_000, stallMs: 2_700_000 },
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

const current = () => ({ pins: PINS, stacksSha256: 'd'.repeat(64), pricesSha256: 'e'.repeat(64), quietSha256: '2'.repeat(64), packageFlagsSha256: '3'.repeat(64), snapshots: { csharp: 'f'.repeat(64), node: '1'.repeat(64) }, preregTag: 'prereg/minigit-bare-20261030', seedSha256: 'b'.repeat(64), harnessIdentity: identity });

test('Resume_refuses_a_changed_pin_snapshot_or_seed_hash', () => {
  const map = series();
  assert.equal(checkResumable({ map, current: current() }).ok, true);
  const changes = {
    pins: { ...PINS, claude: '2.1.311' }, stacksSha256: '0'.repeat(64), pricesSha256: '0'.repeat(64), snapshots: { csharp: '0'.repeat(64), node: '1'.repeat(64) },
    preregTag: 'prereg/minigit-bare-20261101', seedSha256: '0'.repeat(64), quietSha256: '0'.repeat(64), packageFlagsSha256: '0'.repeat(64),
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
  const stoppedRun = checkResumable({ map: markStopped(map, { rule: 'stop: auth', reason: 'x', at: 't' }), current: current() });
  assert.equal(stoppedRun.ok, false, 'a series stop is not passed by resume');
  assert.match(stoppedRun.reason, /start a NEW series/, 'it says what to do: the stop is final for its series (contract section 9)');
  assert.doesNotMatch(stoppedRun.reason, /until the named fix/, 'no promise that a fix lets this series go on');
});

// ---- input amendments (contract section 10; review N5) ------------------------------------------------------------------------------

const NEW_QUIET = '7'.repeat(64);
const inputLine = (file, sha, date = '2026-11-02') => `${date} | instrument | input:${file}:${sha.slice(0, 12)} | the sampler window (G-11)\n`;

test('A_changed_quiet_or_package_flags_file_is_accepted_only_by_a_dated_input_line_naming_its_content_and_only_before_any_data_session', () => {
  const map = series();
  const changed = { ...current(), quietSha256: NEW_QUIET };
  const bare = checkResumable({ map, current: changed });
  assert.equal(bare.ok, false, 'no line: it is a pin that differs');
  assert.match(bare.reason, /quietSha256 differs/);
  const wrongContent = checkResumable({ map, current: changed, amendments: inputLine('quiet.json', '8'.repeat(64)) });
  assert.equal(wrongContent.ok, false, 'a line that names another content');
  const wrongFile = checkResumable({ map, current: changed, amendments: inputLine('package-flags.json', NEW_QUIET) });
  assert.equal(wrongFile.ok, false, 'a line that names another file');
  const accepted = checkResumable({ map, current: changed, amendments: inputLine('quiet.json', NEW_QUIET) });
  assert.equal(accepted.ok, true, accepted.reason);
  assert.deepEqual(accepted.value.inputs.map(i => [i.file, i.sha256, i.hash12, i.date]), [['quiet.json', NEW_QUIET, NEW_QUIET.slice(0, 12), '2026-11-02']], 'recorded for the run-map and the ledger');
  const flags = checkResumable({ map, current: { ...current(), packageFlagsSha256: NEW_QUIET }, amendments: inputLine('package-flags.json', NEW_QUIET) });
  assert.equal(flags.ok, true, flags.reason);
  // once a data session exists in the series the same line is refused: the instrument may change after the A/A and BEFORE pair 1
  const started = must(transition(must(planSession(map, session('aaaaaa'))), 'aaaaaa', 'launching')); // pair 1 STARTS when its first session leaves `planned` (contract section 10: "after pair 1 starts")
  const late = checkResumable({ map: started, current: changed, amendments: inputLine('quiet.json', NEW_QUIET) });
  assert.equal(late.ok, false);
  assert.match(late.reason, /data session/);
  assert.match(late.reason, /before pair 1/);
  // an A/A twin is not a data session
  const aa = must(planSession(map, session('bbbbbb', { kind: 'aa', pair: 0 })));
  assert.equal(checkResumable({ map: aa, current: changed, amendments: inputLine('quiet.json', NEW_QUIET) }).ok, true, 'the A/A and the pilot are instrument checks');
  // what was accepted before the data sessions stays accepted afterwards
  const acceptedEarlier = { ...started, inputsAccepted: [{ file: 'quiet.json', sha256: NEW_QUIET, hash12: NEW_QUIET.slice(0, 12), date: '2026-11-02', reason: 'r', at: 't' }] };
  const again = checkResumable({ map: acceptedEarlier, current: changed, amendments: inputLine('quiet.json', NEW_QUIET) });
  assert.equal(again.ok, true, again.reason);
  assert.deepEqual(again.value.inputs, [], 'nothing new to record');
});

// ---- the harness acceptance reads instrument lines only, and only before pair 1 (contract section 10; review N6) ----------------------

test('A_harness_acceptance_reads_the_instrument_lines_only_not_any_line_that_mentions_the_identity', () => {
  const map = series();
  const id12 = '9'.repeat(12);
  const changed = { ...current(), harnessIdentity: '9'.repeat(64) };
  const accept = amendments => checkResumable({ map, current: changed, acceptHarness: { identity: '9'.repeat(64), amendmentDate: '2026-11-02' }, amendments });
  assert.equal(accept(`2026-11-02 | instrument | harness:${id12} | the sampler fix (G-11)\n`).ok, true, 'control: the instrument line');
  const imposters = {
    'a by-hand verdict that names it in a field': `2026-11-02 | G-11 | holds | harness:${id12} | scratch/x.md | me\n`,
    'an input line that names it in its reason': `2026-11-02 | instrument | input:quiet.json:0123456789ab | see harness:${id12}\n`,
    'a budget line that names it in its reason': `2026-11-02 | budget-raise | pair:1 | n:1 | usd:6.6 | harness:${id12}\n`,
    'an instrument line with no reason (malformed)': `2026-11-02 | instrument | harness:${id12} |\n`,
    'a line without a valid date': `soon | instrument | harness:${id12} | the sampler fix\n`,
    'prose': `The sampler fix is harness:${id12} (dated 2026-11-02).\n`,
  };
  for (const [why, text] of Object.entries(imposters)) {
    const r = accept(`# header\n${text}`);
    assert.equal(r.ok, false, why);
    assert.match(r.reason, /no instrument line dated 2026-11-02/, why);
  }
});

test('A_harness_change_after_a_data_session_exists_is_refused_even_with_its_line_and_one_accepted_earlier_stays_accepted', () => {
  const id = '9'.repeat(64);
  const line = `# header\n2026-11-02 | instrument | harness:${id.slice(0, 12)} | the sampler fix (G-11)\n`;
  const acceptHarness = { identity: id, amendmentDate: '2026-11-02' };
  const changed = { ...current(), harnessIdentity: id };
  const started = must(transition(must(planSession(series(), session('aaaaaa'))), 'aaaaaa', 'launching'));
  const late = checkResumable({ map: started, current: changed, acceptHarness, amendments: line });
  assert.equal(late.ok, false);
  assert.match(late.reason, /data session/);
  assert.match(late.reason, /before pair 1/);
  const aa = must(planSession(series(), session('bbbbbb', { kind: 'aa', pair: 0 })));
  assert.equal(checkResumable({ map: aa, current: changed, acceptHarness, amendments: line }).ok, true, 'the A/A and the pilot are not data sessions');
  const accepted = { ...started, harnessAccepted: [id] };
  const again = checkResumable({ map: accepted, current: changed });
  assert.equal(again.ok, true, 'an identity accepted before the data sessions needs no flag and no line again');
  assert.equal(again.value.accepted, null);
});

// ---- settle-only on a stopped series (review N12) -------------------------------------------------------------------------------------

const launching = map => must(transition(must(planSession(map, session('aaaaaa'))), 'aaaaaa', 'launching'));

test('Settle_only_is_allowed_on_a_stopped_series_and_does_not_need_the_harness_to_be_the_pinned_one', () => {
  const stopped = launching(markStopped(series(), { rule: 'stop: auth', reason: 'x', at: 't' }));
  assert.equal(checkResumable({ map: stopped, current: current() }).ok, false, 'control: a plain resume is refused');
  const settle = checkResumable({ map: stopped, current: current(), settleOnly: true });
  assert.equal(settle.ok, true, settle.reason);
  const moved = checkResumable({ map: stopped, current: { ...current(), harnessIdentity: '9'.repeat(64) }, settleOnly: true });
  assert.equal(moved.ok, true, 'settling reads evidence and launches nothing: a reader fix after the data sessions must not leave a paid session uncharged');
  assert.deepEqual([moved.value.accepted, moved.value.inputs], [null, []], 'and it accepts nothing');
  assert.equal(checkResumable({ map: stopped, current: { ...current(), pricesSha256: '0'.repeat(64) }, settleOnly: true }).ok, false, 'the prices still must be the pinned ones: they decide what the session cost');
  assert.equal(checkResumable({ map: stopped, current: { ...current(), seedSha256: '0'.repeat(64) }, settleOnly: true }).ok, false, 'every other pin is still checked');
});

test('A_data_session_that_is_only_planned_is_not_pair_1_started_and_an_instrument_line_is_still_accepted', () => {
  const planned = must(planSession(series(), session('aaaaaa'))); // the loop plans the pair's sessions before its gates run: a halt there paid nothing
  assert.equal(hasDataSession(planned), false, 'planned is not started');
  assert.equal(hasDataSession(must(transition(planned, 'aaaaaa', 'launching'))), true, 'launching is');
  const id = '9'.repeat(64);
  const changed = { ...current(), harnessIdentity: id, quietSha256: NEW_QUIET };
  const lines = `2026-11-02 | instrument | harness:${id.slice(0, 12)} | the preflight fix (G-1)\n${inputLine('quiet.json', NEW_QUIET)}`;
  const r = checkResumable({ map: planned, current: changed, acceptHarness: { identity: id, amendmentDate: '2026-11-02' }, amendments: lines });
  assert.equal(r.ok, true, r.reason);
  assert.equal(r.value.accepted.to, id, 'the harness change and the input change are both accepted: the pair halted at its gates before any session started');
  assert.equal(r.value.inputs.length, 1);
  const reset = must(transition(must(transition(planned, 'aaaaaa', 'launching')), 'aaaaaa', 'planned')); // launching -> planned when nothing was paid
  assert.equal(hasDataSession(reset), false, 'a session that went back to planned never ran');
});

test('Settle_only_is_refused_unless_some_session_is_launching_and_compares_the_harness_unless_the_series_is_stopped', () => {
  const changed = { ...current(), harnessIdentity: '9'.repeat(64) };
  const none = checkResumable({ map: must(planSession(series(), session('aaaaaa'))), current: current(), settleOnly: true });
  assert.equal(none.ok, false, 'a planned session is nothing to settle');
  assert.match(none.reason, /nothing to settle/);
  assert.equal(checkResumable({ map: markStopped(series(), { rule: 'stop: auth', reason: 'x', at: 't' }), current: current(), settleOnly: true }).ok, false, 'a stopped series with nothing launching has nothing to settle either');
  const open = launching(series()); // not stopped: the harness that settles is the one that classes the session, so it is the pinned one (or an accepted one)
  assert.equal(checkResumable({ map: open, current: current(), settleOnly: true }).ok, true, 'control: the pinned harness settles');
  const drift = checkResumable({ map: open, current: changed, settleOnly: true });
  assert.equal(drift.ok, false, 'a settle on a series that is not stopped does not accept a changed harness');
  assert.match(drift.reason, /harnessIdentity differs/);
  assert.equal(checkResumable({ map: { ...open, harnessAccepted: ['9'.repeat(64)] }, current: changed, settleOnly: true }).ok, true, 'an identity accepted before by a plain resume needs nothing more');
  assert.equal(checkResumable({ map: launching(markStopped(series(), { rule: 'stop: auth', reason: 'x', at: 't' })), current: changed, settleOnly: true }).ok, true, 'a stopped series keeps the reader-fix rule (N12)');
});
