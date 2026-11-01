// The olympiad pilot's task routing (plan research/PLAN_lang_explore_olympiad.md, D-O2, T-O1 and T-O2): `--task NN` routes every path and every command, a series records its task, and task 1 reads exactly as it did
// before the change. The fixture of T-O2 is a COPY of the real task-1 series made at run time; the real series is never written (a host without it is `hostLacks`).
import { realHome } from '../../minigit-bench/test/fake-home.mjs';
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { cpSync, existsSync, mkdirSync, readFileSync, readdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { ok } from '../../minigit-bench/lib/result.mjs';
import { hostLacks, tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { stripComments } from '../../minigit-bench/test/scan.mjs';
import { main } from '../explore.mjs';
import { EXIT, cmdInit, cmdNote, cmdPrune, cmdReadback, cmdStatus, notePathOf } from '../lib/commands.mjs';
import { loadExplore } from '../lib/inputs.mjs';
import { INPUTS, SPIKE, exploreRoot } from '../lib/paths.mjs';
import { noteFor } from '../lib/rehearse.mjs';
import { createSeries, latestSeries, loadSeries, seriesInputs } from '../lib/series.mjs';
import { resolveTask, taskOfSeries, taskPaths } from '../lib/tasks.mjs';
import { calibratedHome, toyCases, writeHeldout } from './olympiad-fixture.mjs';

const EXPLORE = loadExplore().value;
const CALIBRATION = { limits: { timeMs: 2000, memoryBytes: 536870912, buildTimeoutMs: 600000 } };
const sha = text => createHash('sha256').update(text).digest('hex');
const io = (home, over = {}) => { const said = []; return { home, said, say: l => said.push(l), ...over }; };
const task = id => resolveTask(EXPLORE, id).value;

test('A_missing_or_unknown_task_is_refused_naming_the_legal_ones_and_a_known_one_resolves', () => {
  for (const bad of [undefined, '', '1', '04', '2', 'all', 'heldout']) {
    const r = resolveTask(EXPLORE, bad);
    assert.equal(r.ok, false, String(bad));
    assert.match(r.reason, /the tasks are 01, 02, 03, 09/);
  }
  assert.deepEqual(['01', '02', '03', '09'].map(id => resolveTask(EXPLORE, id).value.id), ['01', '02', '03', '09']);
});

test('The_task_routes_the_held_out_folder_the_calibration_file_and_the_private_notes_and_no_two_tasks_share_one', () => {
  const home = '/home/someone';
  const want = {
    '01': { heldoutDir: 'rsd/lang-v2/explore/heldout', calibrationFile: 'rsd/lang-v2/explore/calibration.json', notesFile: 'rsd/lang-v2/test-design/01.md' },
    '02': { heldoutDir: 'rsd/lang-v2/explore/heldout-02', calibrationFile: 'rsd/lang-v2/explore/calibration-02.json', notesFile: 'rsd/lang-v2/test-design/02.md' },
    '03': { heldoutDir: 'rsd/lang-v2/explore/heldout-03', calibrationFile: 'rsd/lang-v2/explore/calibration-03.json', notesFile: 'rsd/lang-v2/test-design/03.md' },
    '09': { heldoutDir: 'rsd/lang-v2/explore/heldout-09', calibrationFile: 'rsd/lang-v2/explore/calibration-09.json', notesFile: 'rsd/lang-v2/test-design/09.md' },
  };
  for (const [id, paths] of Object.entries(want)) {
    assert.deepEqual(taskPaths(task(id), home), Object.fromEntries(Object.entries(paths).map(([k, v]) => [k, path.join(home, v)])), id);
  }
  const all = Object.keys(want).flatMap(id => Object.values(taskPaths(task(id), home)));
  assert.equal(new Set(all).size, all.length, 'twelve paths, twelve different ones');
});

test('A_series_records_its_task_and_the_latest_series_of_a_task_is_never_another_tasks', () => {
  const home = tmpDir('o-series');
  const legacy = createSeries({ home, explore: EXPLORE, calibration: CALIBRATION, task: '01', rand: () => 'aaaa', date: new Date('2026-11-01T01:00:00Z') });
  const second = createSeries({ home, explore: EXPLORE, calibration: CALIBRATION, task: '02', rand: () => 'bbbb', date: new Date('2026-11-01T02:00:00Z') });
  assert.equal(loadSeries(second.value.dir).value.state.task, '02');
  assert.equal(seriesInputs(second.value.dir).value.task, '02');
  assert.equal(latestSeries(home, '02'), second.value.dir);
  assert.equal(latestSeries(home, '01'), legacy.value.dir, 'the newer series of task 02 does not hide task 01\'s');
  assert.equal(latestSeries(home, '03'), null);
  assert.equal(taskOfSeries({ seriesId: 'x-20261101-13cc' }), '01', 'a series made before tasks existed is task 1\'s');
});

test('A_command_whose_task_differs_from_its_series_task_is_refused_with_exit_4_and_reads_nothing_of_it', async () => {
  const home = tmpDir('o-mismatch');
  const made = createSeries({ home, explore: EXPLORE, calibration: CALIBRATION, task: '02' });
  for (const [name, fn] of [['status', cmdStatus], ['readback', cmdReadback], ['prune', cmdPrune]]) {
    const wrong = io(home);
    assert.equal(await fn({ series: made.value.dir, task: '03' }, wrong), EXIT.config, name);
    assert.match(wrong.said.join('\n'), /series x-.* is a series of task 02, not of task 03/, name);
    const right = io(home);
    await fn({ series: made.value.dir, task: '02' }, right);
    assert.doesNotMatch(right.said.join('\n'), /is a series of task/, `${name}: the matching task is not refused for its task`);
  }
  const missing = io(home);
  assert.equal(await cmdStatus({ series: made.value.dir }, missing), EXIT.config);
  assert.match(missing.said[0], /--task is required/);
});

test('Every_command_that_works_on_a_task_refuses_a_missing_or_unknown_task_with_exit_4_before_anything_else', async () => {
  const stderr = [];
  const realWrite = process.stderr.write.bind(process.stderr);
  const realOut = process.stdout.write.bind(process.stdout);
  process.stderr.write = s => { stderr.push(String(s)); return true; };
  process.stdout.write = () => true;
  try {
    for (const command of ['init', 'calibrate', 'rehearse', 'run', 'readback', 'status', 'note', 'prune']) {
      assert.equal(await main([command], { home: tmpDir('o-none') }), EXIT.config, `${command} without --task`);
      assert.equal(await main([command, '--task', '04'], { home: tmpDir('o-none') }), EXIT.config, `${command} --task 04`);
    }
  } finally {
    process.stderr.write = realWrite;
    process.stdout.write = realOut;
  }
});

test('The_note_goes_to_EXPLORE_task_label_and_the_date_of_the_series_and_task_1_keeps_its_recorded_name', () => {
  const state = { createdAt: '2026-11-01T01:00:00.000Z' };
  assert.equal(path.relative(path.resolve(SPIKE, '..', '..'), notePathOf({}, state, task('01'))), path.join('research', 'lang-v2', 'EXPLORE_task1_2026-11-01.md'));
  for (const id of ['02', '03', '09']) assert.equal(path.basename(notePathOf({}, state, task(id))), `EXPLORE_task${id}_2026-11-01.md`);
  assert.equal(notePathOf({ out: '/x/y.md' }, state, task('02')), '/x/y.md');
});

const SITES = [
  [/heldout-(?:\d|\$\{)|(?:join|resolve)\([^)]*['"`]heldout['"`]/, 'a held-out folder name'],
  [/calibration(?:-\$\{|\.json)/, 'a calibration file name'],
  [/test-design/, 'the private notes folder'],
];
const libFiles = () => readdirSync(path.join(SPIKE, 'lib')).filter(f => f.endsWith('.mjs')).map(f => path.join(SPIKE, 'lib', f));
const where = re => libFiles().filter(f => re.test(stripComments(readFileSync(f, 'utf8')))).map(f => path.basename(f));

test('No_module_names_a_task_s_private_folder_or_calibration_file_itself_so_a_forgotten_task_cannot_fall_back_to_task_1', () => {
  for (const [re, what] of SITES) assert.deepEqual(where(re), [], `${what} must come from explore.json's tasks map only`);
  const json = readFileSync(path.join(INPUTS, 'explore.json'), 'utf8');
  for (const needle of ['"heldout"', '"heldout-02"', 'calibration.json', 'calibration-09.json', 'test-design/03.md']) assert.ok(json.includes(needle), `the scan's companion: ${needle} is in explore.json`);
});

// ---- T-O2: task 1 is unchanged ------------------------------------------------------------------------------------------------------------

/** The sha256 of task 1's pre-change `explore.json` content (every field but the two added keys), as JSON.stringify gives it. */
const TASK_1_FIELDS_SHA256 = '527b0fbf09a6b67f056c8324241f550311d1f5c8b00a1c8ce307646fd4d2aff9';
/** What `status`, `readback` and the note of the recorded series printed before this change (taken from the unchanged harness over a copy of the series). */
const GOLDEN = {
  status: '79cffca43f6d885d0a2e352c89be3a45c76f75759739688c03539924b00988eb', readback: '9886ac944e585a8efbbdf4cc3f8cb84874f5b805532b70ee8ae4a8bd61c3edbf', note: '55e3e927848f763fd4678dc14484a83d859bf302ffd95ed2564609ef783d9920',
};
const SERIES_ID = 'x-20261101-13cc';
const subject = { branch: 'test', sha: 'a'.repeat(40), harnessPath: 'spikes/lang-explore/explore.mjs', harnessIdentity: 'b'.repeat(12), pilotIdentity: 'c'.repeat(40) };

test('Task_1_s_pins_are_byte_for_byte_the_recorded_ones_and_the_tasks_map_repeats_them_exactly', () => {
  const raw = JSON.parse(readFileSync(path.join(INPUTS, 'explore.json'), 'utf8'));
  const { tasks, olympiad, ...recorded } = raw;
  assert.equal(sha(JSON.stringify(recorded)), TASK_1_FIELDS_SHA256, 'a field of task 1 moved');
  assert.deepEqual(tasks['01'].spec, recorded.specs.spec01);
  assert.deepEqual({ ...tasks['01'].format, label: undefined }, { ...recorded.specs.format, label: undefined });
  assert.deepEqual(tasks['01'].privateNotes, recorded.privateNotes);
  assert.deepEqual(Object.keys(tasks), ['01', '02', '03', '09'], 'task 01 is the first entry');
});

/** A copy of the real task-1 series in a temp home, or null when this host lacks it. */
function seriesCopy(t) {
  const real = path.join(exploreRoot(realHome), SERIES_ID);
  if (!existsSync(real)) return hostLacks(t, `${real} is missing: the task-1 series is not on this host`) && null;
  const home = tmpDir('o-copy');
  const copy = path.join(exploreRoot(home), SERIES_ID);
  mkdirSync(exploreRoot(home), { recursive: true });
  cpSync(real, copy, { recursive: true });
  return { home, copy };
}

test('Status_readback_and_the_note_over_a_copy_of_the_recorded_task_1_series_print_what_they_printed_before', async t => {
  const got = seriesCopy(t);
  if (!got) return;
  const { home, copy } = got;
  const status = io(home);
  assert.equal(await cmdStatus({ series: copy, task: '01' }, status), EXIT.done);
  assert.equal(sha(status.said.join('\n')), GOLDEN.status);
  const readback = io(home);
  assert.equal(await cmdReadback({ series: copy, task: '01' }, readback), EXIT.done);
  assert.equal(sha(readback.said.join('\n')), GOLDEN.readback);
  const out = path.join(tmpDir('o-note'), 'note.md');
  const note = io(home, { worldFn: async () => ok({ explore: EXPLORE }), subject, date: '2026-11-01' });
  assert.equal(await cmdNote({ series: copy, task: '01', out }, note), EXIT.done, note.said.join('\n'));
  assert.equal(sha(readFileSync(out, 'utf8')), GOLDEN.note);
  const again = noteFor({ series: loadSeries(copy).value.state, seriesDir: copy, map: loadSeries(copy).value.map, world: { calibration: { limits: seriesInputs(copy).value.limits } }, pins: EXPLORE, task: task('01'), rehearsal: false, subject, date: '2026-11-01' });
  assert.equal(sha(again), GOLDEN.note, 'the generator alone gives the same text');
});

test('Prune_of_a_task_removes_only_the_reproducible_copies_and_keeps_every_record_the_ledger_the_calibration_and_the_held_out_folder', () => {
  const home = tmpDir('o-prune');
  const made = createSeries({ home, explore: EXPLORE, calibration: CALIBRATION, task: '02' });
  const heldout = writeHeldout({ home, task: task('02'), cases: toyCases(3) });
  const calibration = taskPaths(task('02'), home).calibrationFile;
  writeFileSync(calibration, JSON.stringify({ limits: CALIBRATION.limits }));
  const session = path.join(made.value.dir, 's', 'abc123');
  const put = rel => { mkdirSync(path.dirname(path.join(session, rel)), { recursive: true }); writeFileSync(path.join(session, rel), 'x'); };
  for (const rel of ['box/nuget/a', 'box/npm/b', 'score/judge/tree/f', 'record.json', 'stream.ndjson', 'transcript/t.jsonl']) put(rel);
  const run = io(home);
  assert.equal(cmdPrune({ series: made.value.dir, task: '02' }, run), EXIT.done);
  for (const gone of ['box/nuget', 'box/npm', 'score']) assert.equal(existsSync(path.join(session, gone)), false, gone);
  for (const kept of [path.join(session, 'record.json'), path.join(session, 'stream.ndjson'), path.join(session, 'transcript', 't.jsonl'), path.join(made.value.dir, 'ledger.ndjson'), path.join(made.value.dir, 'inputs.json'), path.join(made.value.dir, 'run-map.json'), calibration, path.join(heldout.dir, 'manifest.json')]) {
    assert.equal(existsSync(kept), true, path.relative(home, kept));
  }
});

// ---- a series is one of the explore root's own, and one that cannot be read is never task 1 (review F3) ------------------------------------

import { cmdRun } from '../lib/commands.mjs';
import { pauseVerdict } from '../lib/pause.mjs';
import { seriesDirs } from '../lib/series.mjs';
import { symlinkSync } from 'node:fs';

test('A_series_directory_outside_the_explore_root_is_refused_by_every_command_that_takes_one_even_through_a_link', async () => {
  const home = tmpDir('o-outside');
  const made = createSeries({ home, explore: EXPLORE, calibration: CALIBRATION, task: '02' });
  const elsewhere = path.join(tmpDir('o-elsewhere'), 'x-20261101-abcd');
  cpSync(made.value.dir, elsewhere, { recursive: true });
  const link = path.join(exploreRoot(home), 'x-20261101-ffff');
  symlinkSync(elsewhere, link);
  for (const series of [elsewhere, link, path.join(exploreRoot(home), '..', 'explore', '..', 'explore-other')]) {
    for (const [name, fn] of [['status', cmdStatus], ['prune', cmdPrune], ['readback', cmdReadback], ['run', cmdRun]]) {
      const c = io(home, { worldFn: async () => { throw new Error('nothing is read for a refused series'); }, tokenFn: async () => { throw new Error('no token'); } });
      assert.equal(await fn({ task: '02', series, pair: '1' }, c), EXIT.config, `${name} ${series}`);
      assert.match(c.said.join('\n'), /is not a series directly under .*explore/, `${name} ${series}`);
    }
  }
  const inside = io(home);
  assert.equal(await cmdStatus({ task: '02', series: made.value.dir }, inside), EXIT.done, 'a series of the root is still taken');
});

test('The_pair_runner_itself_refuses_a_series_outside_the_explore_root_before_it_loads_it', async () => {
  const { runPair } = await import('../lib/run.mjs');
  const home = tmpDir('o-outside-run');
  const made = createSeries({ home, explore: EXPLORE, calibration: CALIBRATION, task: '02' });
  const elsewhere = path.join(tmpDir('o-elsewhere2'), 'x-20261101-abcd');
  cpSync(made.value.dir, elsewhere, { recursive: true });
  const r = await runPair({ seriesDir: elsewhere, pair: 1, explore: EXPLORE, task: task('02'), io: { home } });
  assert.equal(r.exit, 4);
  assert.match(r.message, /is not a series directly under/);
});

test('A_series_whose_state_file_cannot_be_read_is_a_refusal_never_task_1_and_is_not_counted_as_task_1_by_the_latest_series', async () => {
  const home = tmpDir('o-corrupt');
  const good = createSeries({ home, explore: EXPLORE, calibration: CALIBRATION, task: '01', rand: () => 'aaaa', date: new Date('2026-11-01T01:00:00Z') });
  const bad = createSeries({ home, explore: EXPLORE, calibration: CALIBRATION, task: '02', rand: () => 'bbbb', date: new Date('2026-11-01T02:00:00Z') });
  writeFileSync(path.join(bad.value.dir, 'explore-series.json'), '{ not json');
  const loaded = loadSeries(bad.value.dir);
  assert.equal(loaded.ok, false, 'a corrupt state is a failure value, not a thrown error');
  assert.match(loaded.reason, /explore-series\.json is unreadable/);
  const c = io(home);
  assert.equal(await cmdStatus({ task: '01', series: bad.value.dir }, c), EXIT.config, 'it is not task 1\'s series by default');
  assert.equal(latestSeries(home, '01'), good.value.dir, 'the newer unreadable series is not taken for task 1');
  assert.deepEqual(seriesDirs(home, '01'), [good.value.dir]);
  const verdict = pauseVerdict({ home, explore: EXPLORE, allowOver: 'the owner, 2026-11-01: go on' });
  assert.equal(verdict.stop, true, 'a series the pause check cannot read stops it, whatever --allow-over says');
  assert.match(verdict.message, /cannot be counted.*x-20261101-bbbb/);
});

test('Init_is_refused_for_a_task_that_is_not_a_task_of_the_plan_so_task_1_gets_no_second_series', async () => {
  const home = calibratedHome('01', EXPLORE);
  const c = io(home);
  assert.equal(await cmdInit({ task: '01' }, c), EXIT.config);
  assert.match(c.said.join('\n'), /task 01 is not a task of the olympiad plan \(02, 03, 09\): its series is closed/);
  assert.deepEqual(readdirSync(exploreRoot(home)).filter(n => n.startsWith('x-')), [], 'no series was made');
  for (const id of ['02', '03', '09']) {
    const ok = io(calibratedHome(id, EXPLORE));
    assert.equal(await cmdInit({ task: id }, ok), EXIT.done, `${id}: ${ok.said.join('\n')}`);
  }
});

// ---- the pins a series froze are the pins the task has now (coai finding 1) --------------------------------------------------------------

import { frozenPinsProblem } from '../lib/tasks.mjs';
import { runPair } from '../lib/run.mjs';

const pinsOf = t => ({ spec: t.spec, format: t.format, privateNotes: t.privateNotes });

test('A_series_that_froze_other_pins_than_the_task_has_now_is_refused_naming_what_moved_and_a_new_series_is_needed', () => {
  const t = task('02');
  assert.equal(frozenPinsProblem({ inputs: { taskPins: pinsOf(t) }, task: t, seriesId: 'x-1' }), null);
  const moved = { ...pinsOf(t), spec: { ...t.spec, sha256: '0'.repeat(64) }, privateNotes: { ...t.privateNotes, sha256: '1'.repeat(64) } };
  assert.match(frozenPinsProblem({ inputs: { taskPins: moved }, task: t, seriesId: 'x-1' }), /pins of task 02 changed since series x-1 was made \(spec, privateNotes\): a new series is needed after an amendment/);
  assert.match(frozenPinsProblem({ inputs: {}, task: t, seriesId: 'x-1' }), /froze no pins of task 02/, 'a draft task\'s series without frozen pins is refused');
  assert.equal(frozenPinsProblem({ inputs: {}, task: task('01'), seriesId: 'x-20261101-13cc' }), null, 'task 1\'s closed series predates the freeze');
});

test('Init_freezes_the_pins_and_the_pair_runner_refuses_a_series_after_an_amendment_before_it_reads_anything_else', async () => {
  const home = calibratedHome('02', EXPLORE);
  const c = io(home);
  assert.equal(await cmdInit({ task: '02' }, c), EXIT.done, c.said.join('\n'));
  const dir = c.said[0].split(' created at ')[1];
  assert.deepEqual(seriesInputs(dir).value.taskPins, pinsOf(task('02')), 'init froze the task\'s pins');
  const amended = JSON.parse(JSON.stringify(EXPLORE));
  amended.tasks['02'].spec.sha256 = '2'.repeat(64);
  const never = () => { throw new Error('nothing is read for an amended series'); };
  const r = await runPair({ seriesDir: dir, pair: 1, explore: amended, task: resolveTask(amended, '02').value, io: { home, say: () => {}, abort: {}, worldFn: never, tokenFn: never } });
  assert.equal(r.exit, 4);
  assert.match(r.message, /pins of task 02 changed since series x-.* was made \(spec\)/);
});

test('The_newest_series_a_command_takes_without_series_goes_through_the_same_root_check_as_an_explicit_one', async () => {
  const home = tmpDir('o-implicit');
  const real = createSeries({ home, explore: EXPLORE, calibration: CALIBRATION, task: '02', rand: () => 'aaaa', date: new Date('2026-11-01T01:00:00Z') });
  const elsewhere = path.join(tmpDir('o-implicit-out'), 'x-20261101-eeee');
  cpSync(real.value.dir, elsewhere, { recursive: true });
  writeFileSync(path.join(elsewhere, 'explore-series.json'), JSON.stringify({ ...loadSeries(real.value.dir).value.state, seriesId: 'x-20261101-eeee', createdAt: '2026-11-01T05:00:00.000Z' }));
  symlinkSync(elsewhere, path.join(exploreRoot(home), 'x-20261101-eeee'));
  assert.equal(latestSeries(home, '02'), path.join(exploreRoot(home), 'x-20261101-eeee'), 'the link is the newest series by its state');
  for (const [name, fn] of [['status', cmdStatus], ['prune', cmdPrune], ['readback', cmdReadback]]) {
    const c = io(home);
    assert.equal(await fn({ task: '02' }, c), EXIT.config, name);
    assert.match(c.said.join('\n'), /is not a series directly under/, name);
  }
});
