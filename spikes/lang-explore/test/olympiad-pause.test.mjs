// The $60 pause point (plan research/PLAN_lang_explore_olympiad.md D-O7, T-O7), exactly as written: the sum over THIS plan's series ledgers, the projected pair cost, exit 2 before anything is launched, and `--allow-over`
// recorded in the pair record. The ledgers are fixtures in a temp home; no session is launched and no token is read.
import '../../minigit-bench/test/fake-home.mjs';
import assert from 'node:assert/strict';
import { existsSync, readFileSync, renameSync, rmSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { appendLedger } from '../../minigit-bench/lib/ledger.mjs';
import { tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { EXIT, cmdRun } from '../lib/commands.mjs';
import { loadExplore } from '../lib/inputs.mjs';
import { finalizePair } from '../lib/run.mjs';
import { pauseVerdict, planSeriesDirs, planSpend, projectedUsd } from '../lib/pause.mjs';
import { createSeries, ledgerFileOf, loadSeries, runMapFileOf } from '../lib/series.mjs';

const EXPLORE = loadExplore().value;
const CALIBRATION = { limits: { timeMs: 2000, memoryBytes: 536870912, buildTimeoutMs: 600000 } };
let counter = 0;

/** A series of `task` in `home` whose ledger holds one charge per entry of `usds` (a session each, unless `rid` repeats). */
function seriesWith(home, task, usds, { prefix = 'x', rids } = {}) {
  const t = EXPLORE.tasks[task];
  const made = createSeries({ home, explore: EXPLORE, calibration: CALIBRATION, task, prefix, inputsExtra: { taskPins: { spec: t.spec, format: t.format, privateNotes: t.privateNotes } }, rand: () => (counter++).toString(16).padStart(4, '0') });
  usds.forEach((usd, i) => appendLedger(ledgerFileOf(made.value.dir), { kind: 'pilot', usd, what: 'completed', rid: rids?.[i] ?? `r${i}`, t: `2026-11-01T0${i % 10}:00:00Z` }));
  return made.value.dir;
}

const verdict = (home, allowOver = null) => pauseVerdict({ home, explore: EXPLORE, allowOver });

test('A_ledger_summing_to_58_dollars_with_a_dearest_session_of_1_dollar_refuses_the_next_pair_naming_the_spend', () => {
  const home = tmpDir('o-pause');
  seriesWith(home, '02', Array(20).fill(1));
  seriesWith(home, '03', Array(20).fill(1));
  seriesWith(home, '09', Array(18).fill(1));
  assert.deepEqual(planSpend({ home, explore: EXPLORE }), { series: 3, sessions: 58, spentUsd: 58, maxSessionUsd: 1, invalid: [] });
  const v = verdict(home);
  assert.equal(v.stop, true);
  assert.match(v.message, /spend is \$58\.0000 over 3 series and 58 session\(s\)/);
  assert.match(v.message, /projected at \$10\.0000 \(2 x max\(\$5, 3 x the most expensive session so far, \$1\.0000\)\)/);
  assert.match(v.message, /\$68\.0000 is over the pause point of \$60/);
  assert.match(v.message, /--allow-over "<the owner's words and date>"/);
});

test('The_rule_is_strictly_over_60_and_the_projection_follows_three_times_the_dearest_session_with_a_5_dollar_floor', () => {
  const pause = EXPLORE.olympiad.pausePoint;
  assert.equal(projectedUsd(pause, 1), 10, '2 x max(5, 3)');
  assert.equal(projectedUsd(pause, 0), 10, 'nothing spent yet still projects the floor');
  assert.equal(projectedUsd(pause, 4), 24, '2 x max(5, 12)');
  const at60 = tmpDir('o-at60');
  seriesWith(at60, '02', [1, ...Array(49).fill(1)]);
  assert.equal(verdict(at60).stop, false, '50 + 10 = 60 is not over 60');
  const over = tmpDir('o-over');
  seriesWith(over, '02', [1, ...Array(48).fill(1), 1.01]);
  assert.equal(verdict(over).stop, true, '50.01 + 10 is over');
  const dear = tmpDir('o-dear');
  seriesWith(dear, '09', [4, ...Array(32).fill(1)]);
  assert.equal(verdict(dear).stop, false, '36 + 2 x 12 = 60');
  seriesWith(dear, '09', [0.01]);
  assert.equal(verdict(dear).stop, true, '36.01 + 24 is over');
});

test('A_session_is_its_charges_summed_per_series_and_rid_and_the_dearest_session_sets_the_projection', () => {
  const home = tmpDir('o-rid');
  seriesWith(home, '02', [3, 3, 0.5], { rids: ['a', 'a', 'b'] });
  const spend = planSpend({ home, explore: EXPLORE });
  assert.equal(spend.sessions, 2);
  assert.equal(spend.maxSessionUsd, 6, 'a void and its re-run of one rid are one session\'s charges');
});

test('The_sum_reads_every_series_of_the_plan_and_never_another_plans_task_1_or_a_rehearsal', () => {
  const home = tmpDir('o-other');
  seriesWith(home, '01', Array(100).fill(1));
  seriesWith(home, '02', [0, 0], { prefix: 'r' });
  assert.deepEqual(planSpend({ home, explore: EXPLORE }), { series: 1, sessions: 2, spentUsd: 0, maxSessionUsd: 0, invalid: [] }, 'a rehearsal series names a plan task and is counted; its ledger holds $0');
  assert.equal(verdict(home).stop, false, 'task 1\'s $100 and a rehearsal\'s $0 are not this plan\'s spend');
  seriesWith(home, '02', [4, 4]);
  seriesWith(home, '09', [4, 4]);
  assert.equal(planSpend({ home, explore: EXPLORE }).spentUsd, 16, 'tasks 02 and 09 both count');
  assert.equal(verdict(home).stop, false, '16 + 2 x 12 = 40 is under 60');
});

test('Run_refuses_before_launching_with_exit_2_leaves_the_run_map_alone_and_passes_only_with_allow_over', async () => {
  const home = tmpDir('o-run');
  const dir = seriesWith(home, '02', Array(55).fill(1));
  const before = readFileSync(runMapFileOf(dir), 'utf8');
  const touched = [];
  const io = (over = {}) => { const said = []; return { home, said, say: l => said.push(l), worldFn: async () => { touched.push('world'); return { ok: false, reason: 'the world is not there (scripted)' }; }, tokenFn: async () => { touched.push('token'); return { ok: false, reason: 'no token (scripted)' }; }, ...over }; };
  const stopped = io();
  assert.equal(await cmdRun({ series: dir, pair: '1', task: '02' }, stopped), EXIT.stop);
  assert.match(stopped.said.join('\n'), /the plan's spend is \$55\.0000/);
  assert.deepEqual(touched, [], 'neither the world nor the token was read');
  assert.equal(readFileSync(runMapFileOf(dir), 'utf8'), before, 'nothing was planned');
  const passed = io();
  assert.equal(await cmdRun({ series: dir, pair: '1', task: '02', 'allow-over': 'the owner, 2026-11-01: go on' }, passed), EXIT.config);
  assert.doesNotMatch(passed.said.join('\n'), /pause point/);
  assert.match(passed.said.join('\n'), /is not there: run `explore\.mjs calibrate --task 02`/, 'it went past the pause point to the next prerequisite (this temp home has no calibration)');
  assert.deepEqual(touched, [], 'and the world and the token still were not read');
  const blank = io();
  assert.equal(await cmdRun({ series: dir, pair: '1', task: '02', 'allow-over': '  ' }, blank), EXIT.config);
  assert.match(blank.said[0], /--allow-over needs the owner's words and the date/);
});

test('An_allow_over_text_goes_into_the_pair_record_whether_or_not_it_was_needed_and_a_resume_keeps_it', () => {
  const home = tmpDir('o-record');
  const dir = seriesWith(home, '02', Array(55).fill(1));
  const series = loadSeries(dir).value;
  const record = verdict(home, 'the owner, 2026-11-01: go on').record;
  assert.equal(record.needed, true);
  assert.equal(record.text, 'the owner, 2026-11-01: go on');
  const file = finalizePair({ seriesDir: dir, map: series.map, pair: 1, pause: record }).pairFile;
  assert.deepEqual(JSON.parse(readFileSync(file, 'utf8')).pauseOverrides.map(o => [o.text, o.needed, o.spentUsd, o.projectedUsd, o.pausePointUsd]), [['the owner, 2026-11-01: go on', true, 55, 10, 60]]);
  finalizePair({ seriesDir: dir, map: series.map, pair: 1, pause: null });
  assert.equal(JSON.parse(readFileSync(file, 'utf8')).pauseOverrides.length, 1, 'a resume without the flag does not erase what the owner said');
  const cheap = tmpDir('o-cheap');
  assert.deepEqual([verdict(cheap).record, verdict(cheap, 'ok').record.needed], [null, false], 'given when not needed it is still recorded, as not needed');
  assert.equal(path.basename(file), 'p1.json');
});

// ---- one plan-wide lock around the pause check and the pair launch (review F4, coai finding 3) ---------------------------------------------

import { exploreRoot } from '../lib/paths.mjs';
import { acquirePlanLock } from '../lib/plan-lock.mjs';
import { runPair } from '../lib/run.mjs';
import { resolveTask } from '../lib/tasks.mjs';

const task = id => resolveTask(EXPLORE, id).value;
const runIo = (home, over = {}) => ({ home, say: () => {}, abort: {}, worldFn: async () => ({ ok: false, reason: 'no world (scripted)' }), tokenFn: async () => ({ ok: false, reason: 'no token (scripted)' }), ...over });

test('A_second_run_on_another_series_of_the_plan_is_refused_while_the_first_holds_the_plan_lock_and_runs_again_after', async () => {
  const home = tmpDir('o-lock');
  const a = seriesWith(home, '02', []);
  const b = seriesWith(home, '03', []);
  let release;
  const gate = new Promise(resolve => { release = resolve; });
  let entered;
  const inside = new Promise(resolve => { entered = resolve; });
  const first = runPair({ seriesDir: a, pair: 1, explore: EXPLORE, task: task('02'), io: runIo(home, { afterPauseCheck: async () => { entered(); await gate; } }) });
  const reached = await Promise.race([inside.then(() => true), first.then(() => false)]);
  assert.equal(reached, true, 'the first run reaches the point after the pause check (otherwise it ended early and would have left this test waiting)');
  const second = await runPair({ seriesDir: b, pair: 1, explore: EXPLORE, task: task('03'), io: runIo(home) });
  assert.equal(second.exit, 4);
  assert.match(second.message, /the plan lock .*series\.lock is held .*pause check and the pair launch/);
  assert.equal(readFileSync(runMapFileOf(b), 'utf8').includes('"sessions": {}'), true, 'the refused run planned nothing');
  release();
  await first;
  const third = await runPair({ seriesDir: b, pair: 1, explore: EXPLORE, task: task('03'), io: runIo(home) });
  assert.doesNotMatch(third.message, /plan lock/, 'the lock was released when the first run ended');
});

test('The_plan_lock_is_a_file_in_the_explore_root_taken_exclusively_and_a_task_1_series_is_not_under_it', async () => {
  const home = tmpDir('o-lock2');
  const held = acquirePlanLock({ home });
  assert.equal(held.ok, true, held.reason);
  assert.equal(existsSync(path.join(exploreRoot(home), 'series.lock')), true);
  assert.equal(acquirePlanLock({ home }).ok, false, 'two holders at once are impossible');
  const legacy = seriesWith(home, '01', []);
  const r = await runPair({ seriesDir: legacy, pair: 1, explore: EXPLORE, task: task('01'), io: runIo(home) });
  assert.doesNotMatch(r.message, /plan lock/, 'task 1\'s own series never waits for the plan\'s lock');
  held.value.release();
  assert.equal(acquirePlanLock({ home }).ok, true, 'released, it can be taken again');
});

// ---- the clock is injected, never read inside the code that persists a time (coai finding 0) ------------------------------------------------

import { readdirSync } from 'node:fs';
import { stripComments } from '../../minigit-bench/test/scan.mjs';
import { SPIKE } from '../lib/paths.mjs';

test('The_allow_over_timestamp_is_the_injected_clocks_time', () => {
  const home = tmpDir('o-clock');
  seriesWith(home, '02', [1]);
  const fixed = () => new Date('2026-11-02T03:04:05.000Z');
  assert.equal(pauseVerdict({ home, explore: EXPLORE, allowOver: 'the owner, 2026-11-02', now: fixed }).record.at, '2026-11-02T03:04:05.000Z');
});

test('No_module_of_the_pilot_but_the_clock_and_prepare_reads_the_time_with_new_Date', () => {
  const dir = path.join(SPIKE, 'lib');
  const hits = readdirSync(dir).filter(f => f.endsWith('.mjs') && !['clock.mjs', 'prepare.mjs'].includes(f)).filter(f => /new Date\(\)/.test(stripComments(readFileSync(path.join(dir, f), 'utf8'))));
  assert.deepEqual(hits, [], 'a time that is persisted comes from the injected clock (lib/clock.mjs)');
  assert.match(readFileSync(path.join(dir, 'clock.mjs'), 'utf8'), /new Date\(\)/, 'the scan\'s companion: the clock itself does');
});

// ---- a ledger amount that is not a finite number of at least 0 fails the check closed (coai finding 9) ---------------------------------------------

test('A_ledger_amount_that_is_no_finite_number_of_at_least_0_stops_the_pause_check_whatever_allow_over_says', () => {
  for (const bad of ['1.5', null, -1, 'NaN', { usd: 1 }, undefined, '1e999']) {
    const home = tmpDir('o-bad-usd');
    const dir = seriesWith(home, '02', [1]);
    appendLedger(ledgerFileOf(dir), { kind: 'pilot', ...(bad === undefined ? {} : { usd: bad }), what: 'completed', rid: 'bad', t: '2026-11-01T09:00:00Z' });
    for (const allowOver of [null, 'the owner, 2026-11-01: go on']) {
      const v = pauseVerdict({ home, explore: EXPLORE, allowOver });
      assert.equal(v.stop, true, `${JSON.stringify(bad)} ${allowOver}`);
      assert.match(v.message, new RegExp(`ledger of series ${path.basename(dir)} holds an amount that is not a finite number of at least 0 \\(line 2\\)`));
    }
  }
  const fine = tmpDir('o-good-usd');
  const d = seriesWith(fine, '02', [0, 0.5, 1]);
  assert.equal(pauseVerdict({ home: fine, explore: EXPLORE }).stop, false, 'zero and fractions are amounts');
  assert.equal(planSpend({ home: fine, explore: EXPLORE }).spentUsd, 1.5);
  assert.ok(d);
});

test('Twelve_series_that_cannot_be_read_are_named_as_ten_and_a_count', () => {
  const home = tmpDir('o-many-bad');
  for (let i = 0; i < 12; i += 1) {
    const dir = seriesWith(home, '02', [], { });
    writeFileSync(path.join(dir, 'explore-series.json'), '{ not json');
  }
  const v = pauseVerdict({ home, explore: EXPLORE });
  assert.equal(v.stop, true);
  assert.match(v.message, /^12 series cannot be counted \((x-\d{8}-[0-9a-f]{4}: its state cannot be read, ){10}… and 2 more \(12 in all\)\)/);
});

// ---- canonical series names and a pause check that counts every child of the root (coai 0, Opus residual 1) ------------------------------------

import { mkdirSync } from 'node:fs';


/** A directory of the explore root that holds a series' files under ANY name: a copy, a rename, a leftover. */
function seriesNamed(home, name, taskId, usds, extra = {}) {
  const made = createSeries({ home, explore: EXPLORE, calibration: CALIBRATION, task: taskId, rand: () => (counter++).toString(16).padStart(4, '0') });
  const target = path.join(exploreRoot(home), name);
  renameSync(made.value.dir, target);
  usds.forEach((usd, i) => appendLedger(ledgerFileOf(target), { kind: 'pilot', usd, what: 'completed', rid: `r${i}`, t: `2026-11-01T0${i % 10}:00:00Z` }));
  return target;
}

test('The_pause_check_counts_every_child_of_the_explore_root_whose_state_names_a_plan_task_whatever_its_name', () => {
  const home = tmpDir('o-names');
  seriesNamed(home, 'old-copy-of-02', '02', Array(30).fill(1));
  seriesNamed(home, 't-20261101-abcd', '03', Array(26).fill(1));
  seriesNamed(home, 'r-20261101-abcd', '09', [0, 0]);
  seriesNamed(home, 'x-20261101-ABCD', '02', [1, 1]);
  seriesNamed(home, 'legacy-copy', '01', Array(100).fill(1));
  mkdirSync(path.join(exploreRoot(home), 'toolchains', 'x'), { recursive: true });
  mkdirSync(path.join(exploreRoot(home), 'heldout-02'), { recursive: true });
  assert.deepEqual(planSpend({ home, explore: EXPLORE }), { series: 4, sessions: 60, spentUsd: 58, maxSessionUsd: 1, invalid: [] });
  const v = pauseVerdict({ home, explore: EXPLORE });
  assert.equal(v.stop, true, '58 + 10 is over 60 although no counted series has a canonical name');
  assert.match(v.message, /spend is \$58\.0000 over 4 series/);
});

test('A_child_of_the_root_with_a_ledger_whose_state_cannot_be_read_or_classified_stops_the_check_whatever_its_name', () => {
  for (const [name, write] of [
    ['no state file at all', dir => rmSync(path.join(dir, 'explore-series.json'))],
    ['a corrupt state file', dir => writeFileSync(path.join(dir, 'explore-series.json'), '{ not json')],
    ['a state of a task nobody knows', dir => writeFileSync(path.join(dir, 'explore-series.json'), JSON.stringify({ seriesId: 'x', task: '77' }))],
    ['a state that is not an object', dir => writeFileSync(path.join(dir, 'explore-series.json'), '[1, 2]')],
  ]) {
    const home = tmpDir('o-unclassified');
    const dir = seriesNamed(home, 'stray-dir', '02', [1]);
    write(dir);
    for (const allowOver of [null, 'the owner, 2026-11-01: go on']) {
      const v = pauseVerdict({ home, explore: EXPLORE, allowOver });
      assert.equal(v.stop, true, `${name} ${allowOver}`);
      assert.match(v.message, /1 series cannot be counted \(stray-dir: /, name);
    }
  }
  const clean = tmpDir('o-not-series');
  mkdirSync(path.join(exploreRoot(clean), 'toolchains'), { recursive: true });
  writeFileSync(path.join(exploreRoot(clean), 'spin.json'), '{}');
  assert.equal(pauseVerdict({ home: clean, explore: EXPLORE }).stop, false, 'directories that hold no ledger and no state are not series');
});

test('A_run_needs_the_canonical_series_name_and_a_rehearsal_series_keeps_its_own_refusal', async () => {
  const home = tmpDir('o-canonical');
  const runIoLocal = over => ({ home, say: () => {}, abort: {}, worldFn: async () => ({ ok: false, reason: 'no world (scripted)' }), tokenFn: async () => ({ ok: false, reason: 'no token (scripted)' }), ...over });
  for (const name of ['x-copy', 't-20261101-abcd', 'x-20261101-ABCD', 'x-2026110-abcd', 'x-20261101-abcde', 'old-02']) {
    const dir = seriesNamed(home, name, '02', []);
    const r = await runPair({ seriesDir: dir, pair: 1, explore: EXPLORE, task: task('02'), io: runIoLocal() });
    assert.equal(r.exit, 4, name);
    assert.match(r.message, /is not a canonical series name \(x-yyyymmdd-4 hex\)/, name);
    const c = { home, said: [], say: l => c.said.push(l) };
    assert.equal(await cmdRun({ task: '02', series: dir, pair: '1' }, c), EXIT.config, name);
    assert.match(c.said.join('\n'), /canonical series name/, name);
  }
  const good = seriesNamed(home, 'x-20261101-abcd', '02', []);
  const ok = await runPair({ seriesDir: good, pair: 1, explore: EXPLORE, task: task('02'), io: runIoLocal() });
  assert.doesNotMatch(ok.message, /canonical/, 'a canonical name goes on to the next prerequisite');
  const rehearsal = createSeries({ home, explore: EXPLORE, calibration: CALIBRATION, task: '02', prefix: 'r', inputsExtra: { rehearsal: true } });
  const refused = await runPair({ seriesDir: rehearsal.value.dir, pair: 1, explore: EXPLORE, task: task('02'), io: runIoLocal() });
  assert.match(refused.message, /is a rehearsal series/, 'a rehearsal series is refused as one, not for its name');
});

// ---- a plan series without a ledger is never $0 (Opus residual 2) ---------------------------------------------------------------------------------

test('A_plan_series_whose_ledger_is_missing_stops_the_check_closed_instead_of_counting_as_0', () => {
  const home = tmpDir('o-no-ledger');
  const dir = seriesWith(home, '02', [1]);
  const ledger = ledgerFileOf(dir);
  assert.equal(pauseVerdict({ home, explore: EXPLORE }).stop, false);
  rmSync(ledger);
  for (const allowOver of [null, 'the owner, 2026-11-01: go on']) {
    const v = pauseVerdict({ home, explore: EXPLORE, allowOver });
    assert.equal(v.stop, true, String(allowOver));
    assert.match(v.message, new RegExp(`1 series cannot be counted \\(${path.basename(dir)}: its ledger is missing\\)`));
  }
  const other = tmpDir('o-no-ledger-task1');
  rmSync(ledgerFileOf(seriesWith(other, '01', [1])));
  assert.equal(pauseVerdict({ home: other, explore: EXPLORE }).stop, false, 'task 1\'s series is not the plan\'s: its ledger does not matter');
});

// ---- a ledger line that is not JSON is a named stop with the locks released (Opus spot-check) -----------------------------------------------------

import { appendFileSync } from 'node:fs';
import { acquireSeriesLock } from '../../minigit-bench/lib/series-lock.mjs';

test('A_ledger_line_that_is_not_a_JSON_object_stops_the_run_with_exit_2_naming_series_and_line_and_both_locks_are_released', async () => {
  for (const bad of ['not json at all', '{"kind":"pilot","usd":1', 'null', '[1, 2]', '5']) {
    const home = tmpDir('o-bad-json');
    const dir = seriesWith(home, '02', [1, 1]);
    appendFileSync(ledgerFileOf(dir), `${bad}\n`);
    const before = readFileSync(runMapFileOf(dir), 'utf8');
    const r = await runPair({ seriesDir: dir, pair: 1, explore: EXPLORE, task: task('02'), io: runIo(home, { allowOver: 'the owner, 2026-11-01: go on' }) });
    assert.equal(r.exit, 2, bad);
    assert.match(r.message, new RegExp(`the ledger of series ${path.basename(dir)} holds a line that is not a JSON object \\(line 3\\)`), bad);
    assert.equal(readFileSync(runMapFileOf(dir), 'utf8'), before, `${bad}: nothing was planned`);
    const plan = acquirePlanLock({ home });
    assert.equal(plan.ok, true, `${bad}: the plan lock was released (${plan.reason})`);
    plan.value.release();
    const series = acquireSeriesLock({ seriesDir: dir });
    assert.equal(series.ok, true, `${bad}: the series lock was released (${series.reason})`);
    series.value.release();
  }
  const home = tmpDir('o-bad-json-two');
  const dir = seriesWith(home, '02', [1]);
  appendFileSync(ledgerFileOf(dir), '\n\nbroken\n');
  assert.match(pauseVerdict({ home, explore: EXPLORE }).message, /holds a line that is not a JSON object \(line 4\)/, 'the number is the physical line of the file: a charge, two blank lines, then the broken one');
  const amount = tmpDir('o-physical-amount');
  const adir = seriesWith(amount, '02', [1]);
  appendFileSync(ledgerFileOf(adir), '\n\n{"kind":"pilot","usd":"x","rid":"r9"}\n');
  assert.match(pauseVerdict({ home: amount, explore: EXPLORE }).message, /holds an amount that is not a finite number of at least 0 \(line 4\)/, 'an invalid amount is numbered the same way');
});

// ---- a child whose type cannot be determined is bad, never dropped (coai round 3, item 1) ---------------------------------------------------------

import { symlinkSync } from 'node:fs';

test('A_child_of_the_root_whose_type_cannot_be_determined_stops_the_check_closed_and_a_plain_file_is_ignored', () => {
  const home = tmpDir('o-undeterminable');
  seriesWith(home, '02', [1]);
  writeFileSync(path.join(exploreRoot(home), 'spin.json'), '{}');
  assert.equal(pauseVerdict({ home, explore: EXPLORE }).stop, false, 'a plain file such as spin.json is ignored');
  symlinkSync(path.join(tmpDir('o-gone'), 'nowhere'), path.join(exploreRoot(home), 'dangling-entry'));
  for (const allowOver of [null, 'the owner, 2026-11-01: go on']) {
    const v = pauseVerdict({ home, explore: EXPLORE, allowOver });
    assert.equal(v.stop, true, String(allowOver));
    assert.match(v.message, /1 series cannot be counted \(dangling-entry: its type cannot be determined\)/);
  }
});

// ---- the existence of the state file is checked before it is read (coai round 3, item 3) ---------------------------------------------------------

import { exploreChildren } from '../lib/series.mjs';

test('A_child_with_neither_a_state_file_nor_a_ledger_is_described_without_an_attempt_to_read_a_missing_file', () => {
  const home = tmpDir('o-children');
  mkdirSync(path.join(exploreRoot(home), 'heldout-02'), { recursive: true });
  const stray = path.join(exploreRoot(home), 'stray');
  mkdirSync(stray);
  writeFileSync(path.join(stray, 'ledger.ndjson'), '');
  const byName = Object.fromEntries(exploreChildren(home).map(c => [c.name, c]));
  assert.deepEqual([byName['heldout-02'].hasState, byName['heldout-02'].hasLedger, byName['heldout-02'].state.ok, byName['heldout-02'].state.reason], [false, false, false, 'it has no state file']);
  assert.equal(byName.stray.state.reason, 'it has no state file', 'a ledger with no state file says so, not an ENOENT');
  assert.doesNotMatch(JSON.stringify(byName), /ENOENT/);
  assert.equal(pauseVerdict({ home, explore: EXPLORE }).stop, true, 'a ledger with no state file is still bad');
});

// ---- the explore root is classified ONCE per pause check (coai round 3, items 1, 3 and 7) -------------------------------------------------------

test('The_pause_check_scans_the_explore_root_once_and_sums_the_directories_it_classified', () => {
  const home = tmpDir('o-once');
  seriesWith(home, '02', [1, 2]);
  seriesWith(home, '03', [3]);
  let scans = 0;
  const children = h => { scans += 1; return exploreChildren(h); };
  const v = pauseVerdict({ home, explore: EXPLORE, children });
  assert.equal(v.stop, false);
  assert.equal(scans, 1, 'one scan: the bad check and the spend sum cannot disagree');
  // the one scan is what is summed: a child that appears only in a second scan would not be counted
  const first = exploreChildren(home);
  let n = 0;
  const flaky = () => { n += 1; return n === 1 ? first : [...first, ...exploreChildren(home).filter(() => false)]; };
  assert.equal(pauseVerdict({ home, explore: EXPLORE, children: flaky }).stop, false);
  assert.equal(n, 1);
});

test('A_ledger_that_vanishes_between_the_scan_and_the_sum_is_a_named_stop_not_a_thrown_error', () => {
  const home = tmpDir('o-vanish');
  const dir = seriesWith(home, '02', [1]);
  const stale = exploreChildren(home);
  rmSync(ledgerFileOf(dir));
  const v = pauseVerdict({ home, explore: EXPLORE, children: () => stale, allowOver: 'the owner, 2026-11-01: go on' });
  assert.equal(v.stop, true);
  assert.match(v.message, new RegExp(`the ledger of series ${path.basename(dir)} cannot be read \\(ENOENT\\)`), 'the cause is kept: the error code');
});

// ---- nothing is sorted in place (coai round 4, item 1) --------------------------------------------------------------------------------------

const IN_PLACE_SORT = /(?<![)\]\w.])[A-Za-z_][\w.]*\.sort\(/;

test('No_module_of_the_pilot_sorts_an_array_it_does_not_own_in_place', () => {
  assert.match('const a = spendOf(classified.plan.sort());', IN_PLACE_SORT, 'the scan\'s companion: it finds the in-place sort of a named array');
  for (const fine of ['[...classified.plan].sort()', 'xs.map(f).sort()', 'Object.keys(o).sort()', 'rows.filter(g).sort((a, b) => a - b)']) assert.doesNotMatch(fine, IN_PLACE_SORT, fine);
  const dir = path.join(SPIKE, 'lib');
  const hits = readdirSync(dir).filter(f => f.endsWith('.mjs')).filter(f => IN_PLACE_SORT.test(stripComments(readFileSync(path.join(dir, f), 'utf8'))));
  assert.deepEqual(hits, [], 'sort a copy: `[...list].sort()` (coding-style: never mutate)');
});

// ---- the one-scan seam belongs to the pause check only (coai round 4, item 2) -----------------------------------------------------------------

test('The_exported_spend_functions_scan_the_root_themselves_and_take_no_scan_to_inject', () => {
  const home = tmpDir('o-no-seam');
  seriesWith(home, '02', [2]);
  const provider = () => [];
  assert.deepEqual(planSpend({ home, explore: EXPLORE, children: provider }), planSpend({ home, explore: EXPLORE }), 'planSpend ignores a provider: it scans through the normal path');
  assert.equal(planSpend({ home, explore: EXPLORE, children: provider }).spentUsd, 2);
  assert.equal(planSeriesDirs(home, EXPLORE, provider).length, 1, 'so does planSeriesDirs');
  assert.equal(pauseVerdict({ home, explore: EXPLORE, children: provider }).stop, false, 'the pause check, which owns the seam, still uses it (no children: nothing to count)');
});

// ---- a child that vanishes between the scan and its description is bad, not "no series" (coai round 4, item 3) -----------------------------------

import { describeChild } from '../lib/series.mjs';

test('A_directory_seen_by_the_scan_that_is_gone_or_replaced_when_it_is_described_is_bad_and_a_real_empty_directory_is_not', () => {
  const home = tmpDir('o-vanished');
  const root = exploreRoot(home);
  mkdirSync(root, { recursive: true });
  const empty = path.join(root, 'heldout-02');
  mkdirSync(empty);
  const real = describeChild(empty);
  assert.deepEqual([real.undeterminable, real.hasState, real.hasLedger], [false, false, false], 'an empty real directory is just no series');
  const gone = path.join(root, 'gone');
  mkdirSync(gone);
  rmSync(gone, { recursive: true });
  assert.equal(describeChild(gone).undeterminable, true, 'removed after the scan');
  const replaced = path.join(root, 'replaced');
  writeFileSync(replaced, 'now a file');
  assert.equal(describeChild(replaced).undeterminable, true, 'replaced by a file after the scan');
  const linked = path.join(root, 'linked');
  symlinkSync(empty, linked);
  assert.equal(describeChild(linked).undeterminable, true, 'replaced by a link after the scan');
  const v = pauseVerdict({ home, explore: EXPLORE, children: () => [describeChild(gone)] });
  assert.equal(v.stop, true);
  assert.match(v.message, /gone: its type cannot be determined/);
});

test('A_ledger_that_cannot_be_read_says_why_with_the_error_code_whatever_it_is', () => {
  const home = tmpDir('o-eisdir');
  const dir = seriesWith(home, '02', [1]);
  rmSync(ledgerFileOf(dir));
  mkdirSync(ledgerFileOf(dir));
  const v = pauseVerdict({ home, explore: EXPLORE, allowOver: 'the owner, 2026-11-01: go on' });
  assert.equal(v.stop, true);
  assert.match(v.message, new RegExp(`the ledger of series ${path.basename(dir)} cannot be read \\(EISDIR\\)`));
});

// ---- only real directories and plain files are known children; anything else is bad (coai round 4, items 6 and 9) -------------------------------------

import { execFileSync } from 'node:child_process';

test('A_pipe_a_link_to_a_file_or_a_link_to_a_directory_under_the_root_stops_the_check_and_allow_over_does_not_pass_it', () => {
  const fileTarget = path.join(tmpDir('o-file-target'), 'target.txt');
  writeFileSync(fileTarget, 'x');
  const makers = {
    'a named pipe': p => execFileSync('mkfifo', [p]),
    'a link to a file': p => symlinkSync(fileTarget, p),
    'a link to a directory': p => symlinkSync(tmpDir('o-linked-dir'), p),
  };
  for (const [name, make] of Object.entries(makers)) {
    const home = tmpDir('o-odd-child');
    seriesWith(home, '02', [1]);
    make(path.join(exploreRoot(home), 'odd-entry'));
    for (const allowOver of [null, 'the owner, 2026-11-01: go on']) {
      const v = pauseVerdict({ home, explore: EXPLORE, allowOver });
      assert.equal(v.stop, true, `${name} ${allowOver}`);
      assert.match(v.message, /1 series cannot be counted \(odd-entry: its type cannot be determined\)/, name);
    }
  }
  const plain = tmpDir('o-plain-file');
  seriesWith(plain, '02', [1]);
  writeFileSync(path.join(exploreRoot(plain), 'spin.json'), '{}');
  assert.equal(pauseVerdict({ home: plain, explore: EXPLORE }).stop, false, 'a plain file is still just a file');
});
