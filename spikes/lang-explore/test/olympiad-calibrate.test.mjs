// Calibration per task (plan research/PLAN_lang_explore_olympiad.md D-O5, D-O3, T-O4 and T-O8): each task's limits follow the stated formula and the harness floors (2 s, 512 MiB) that override the specs' lower ones, are
// written to that task's own file, and a reference that disagrees with `gen.py` stops it; the independent oracle (`brute.py` against `gen.py`, `handcheck.md` with at least five cases) stops it too, naming the case id only.
// The held-out folders are fixtures in a temp home; the scorer is scripted (the real judge is `heldout-real.test.mjs`). Nothing here reads or writes a real `heldout-NN/`.
import '../../minigit-bench/test/fake-home.mjs';
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { existsSync, mkdirSync, readFileSync, statSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { ok } from '../../minigit-bench/lib/result.mjs';
import { tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { calibrateCommand } from '../lib/calibrate.mjs';
import { loadExplore } from '../lib/inputs.mjs';
import { exploreRoot } from '../lib/paths.mjs';
import { handcheckCases } from '../lib/oracle.mjs';
import { resolveTask, taskPaths } from '../lib/tasks.mjs';
import { bruteTwice, handcheckOf, toyCases, writeHeldout } from './olympiad-fixture.mjs';

const EXPLORE = loadExplore().value;
const MIB = 1024 * 1024;
const task = id => resolveTask(EXPLORE, id).value;
const caseOf = (id, over = {}) => ({ set: 'heldout', id, verdict: 'pass', wallMs: 100, peakBytes: 10 * MIB, ...over });
const scoreOf = (cases, build = true) => ({ build: { ok: build }, explore: { build: { outputTail: '' }, cases } });

/** A scripted world: every reference and every mutant passes its cases in `wallMs` ms with `peak` bytes unless `script` says otherwise; `calls` records the stacks scored. */
function scripted({ wallMs = 100, peak = 10 * MIB, walls = {}, peaks = {}, script = () => null } = {}) {
  const calls = [];
  const scoreFn = async args => {
    calls.push(args.stack);
    const made = script(args, calls.length);
    if (made) return made;
    const ids = args.sets[0].dir.endsWith('control-src/cases') ? ['c1', 'c2', 'c3'] : toyCases(15).map(c => c.id);
    return ok(scoreOf(ids.map(id => caseOf(id, { wallMs: walls[args.stack] ?? wallMs, peakBytes: peaks[args.stack] ?? peak }))));
  };
  return { calls, worldFn: async ({ task: t }) => ok({ explore: EXPLORE, task: t, filesFor: () => ({}), scoreFn }) };
}

const sixMutants = (extra = {}) => ({ ...Object.fromEntries(['M-01', 'M-02', 'M-03', 'M-04', 'M-05', 'M-06'].map(m => [m, { stack: 'node', expectedFail: [] }])), ...extra });

/** A home with a held-out folder for `id` whose oracle is sound unless told otherwise. */
function homeFor(id, over = {}) {
  const home = tmpDir(`o-cal-${id}`);
  writeHeldout({ home, task: task(id), cases: toyCases(15, { bigLast: 1 }), brute: bruteTwice(), handcheck: handcheckOf(6), mutants: sixMutants(), ...over });
  return home;
}

const run = (home, id, world) => calibrateCommand({ home, say: () => {}, worldFn: world.worldFn, task: task(id) });

test('Each_task_calibrates_into_its_own_file_with_the_harness_floors_overriding_the_specs_lower_ones', async () => {
  for (const [id, file] of [['02', 'calibration-02.json'], ['03', 'calibration-03.json'], ['09', 'calibration-09.json']]) {
    const home = homeFor(id);
    const world = scripted({ wallMs: 100, peak: 10 * MIB });
    const r = await run(home, id, world);
    assert.equal(r.ok, true, `${id}: ${r.reason}`);
    assert.equal(r.value.file, path.join(exploreRoot(home), file));
    const written = JSON.parse(readFileSync(r.value.file, 'utf8'));
    assert.equal(written.task, id);
    assert.equal(written.limits.timeMs, 2000, 'the spec floor is 1 s; the harness floor of 2 s overrides it, unconditionally');
    assert.equal(written.limits.memoryBytes, 512 * MIB, 'the spec floor is 256 MB; the harness floor of 512 MiB overrides it');
    assert.equal(statSync(r.value.file).mode & 0o777, 0o600);
    assert.equal(existsSync(path.join(exploreRoot(home), 'calibration.json')), false, `${id} never writes task 1's file`);
  }
});

test('The_limits_follow_three_times_the_slower_reference_and_twice_the_larger_peak_whichever_stack_that_is', async () => {
  // the two references differ in BOTH figures, so taking the faster one or the smaller peak is a different number
  const csharpFaster = await run(homeFor('03'), '03', scripted({ walls: { csharp: 300, node: 900 }, peaks: { csharp: 100 * MIB, node: 400 * MIB } }));
  assert.equal(csharpFaster.ok, true, csharpFaster.reason);
  assert.equal(csharpFaster.value.calibration.limits.timeMs, 2700, '3 x 900 ms (node, the slower), not 3 x 300 ms and not the 2 s floor');
  assert.equal(csharpFaster.value.calibration.limits.memoryBytes, 800 * MIB, '2 x 400 MiB (node, the larger), not 2 x 100 MiB and not the 512 MiB floor');
  const nodeFaster = await run(homeFor('09'), '09', scripted({ walls: { csharp: 1200, node: 400 }, peaks: { csharp: 450 * MIB, node: 120 * MIB } }));
  assert.equal(nodeFaster.value.calibration.limits.timeMs, 3600, '3 x 1200 ms (C#, the slower)');
  assert.equal(nodeFaster.value.calibration.limits.memoryBytes, 900 * MIB, '2 x 450 MiB (C#, the larger)');
  assert.deepEqual(nodeFaster.value.calibration.references.map(r => [r.stack, r.slowestWallMs, r.peakBytes]), [['csharp', 1200, 450 * MIB], ['node', 400, 120 * MIB]], 'each reference\'s own figures are recorded');
  assert.deepEqual(EXPLORE.limits.timeFloorMs, 2000);
  assert.deepEqual(EXPLORE.limits.memoryFloorBytes, 512 * MIB);
});

test('Task_1_still_calibrates_into_calibration_json_and_is_not_asked_for_an_oracle_it_never_had', async () => {
  const home = homeFor('01', { brute: null, handcheck: null });
  const r = await run(home, '01', scripted());
  assert.equal(r.ok, true, r.reason);
  assert.equal(r.value.file, path.join(exploreRoot(home), 'calibration.json'));
  assert.equal(r.value.calibration.oracle, null);
});

test('A_reference_that_disagrees_with_gen_py_stops_calibration_before_any_file_is_written', async () => {
  const home = homeFor('02');
  const world = scripted({ script: args => (args.stack === 'node' ? ok(scoreOf([caseOf('h003', { verdict: 'wrong answer' })])) : null) });
  const r = await run(home, '02', world);
  assert.equal(r.ok, false);
  assert.match(r.reason, /the node reference fails case heldout\/h003: wrong answer/);
  assert.deepEqual(world.calls, ['csharp', 'node'], 'nothing after the failing reference is scored');
  assert.equal(existsSync(taskPaths(task('02'), home).calibrationFile), false, 'no limits exist for a refuted reference');
});

// ---- T-O8: the independent oracle -----------------------------------------------------------------------------------------------------------

test('A_brute_py_that_disagrees_with_gen_py_on_one_case_stops_calibration_naming_the_case_id_only', async () => {
  const home = homeFor('09', { brute: bruteTwice({ wrongFor: [7] }) });
  const world = scripted();
  const r = await run(home, '09', world);
  assert.equal(r.ok, false);
  assert.match(r.reason, /brute\.py disagrees with gen\.py on case h007:/);
  assert.doesNotMatch(r.reason, /\b14\b|\b15\b|\bh006\b|\bh008\b/, 'neither the case\'s input, nor either output, nor another case is named');
  assert.deepEqual(world.calls, [], 'the oracle runs before any box is used');
  assert.equal(existsSync(taskPaths(task('09'), home).calibrationFile), false);
});

test('Brute_py_marks_the_cases_it_cannot_run_by_exit_3_and_agrees_with_gen_py_on_the_rest', async () => {
  const home = homeFor('02', { brute: bruteTwice({ limit: 10 }) });
  const r = await run(home, '02', scripted());
  assert.equal(r.ok, true, r.reason);
  assert.deepEqual(r.value.calibration.oracle, { ran: 10, refused: 5, handcheckCases: 6 });
});

test('An_oracle_that_cannot_judge_stops_calibration_a_missing_brute_a_crash_a_refusal_of_everything_a_missing_or_thin_handcheck', async () => {
  const stops = [
    ['no brute.py', { brute: null }, /brute\.py is missing/],
    ['a brute.py that crashes', { brute: 'import sys\nsys.exit(1)\n' }, /brute\.py failed on case h001 \(exited 1\)/],
    ['a brute.py that refuses every case', { brute: bruteTwice({ limit: 0 }) }, /brute\.py ran no case/],
    ['no handcheck.md', { handcheck: null }, /handcheck\.md is missing/],
    ['a handcheck.md of headings and bullets', { handcheck: '# Hand check\n\n## h001\n## h002\n## h003\n## h004\n## h005\n## h006\n' }, /lists 0 case\(s\)/],
    ['a handcheck.md of four cases', { handcheck: handcheckOf(4) }, /handcheck\.md lists 4 case\(s\); at least 5 are required/],
    ['a handcheck.md of prose', { handcheck: '# Hand check\n\nI checked a few cases by hand and they were fine.\n' }, /lists 0 case\(s\)/],
  ];
  for (const [name, over, why] of stops) {
    const world = scripted();
    const r = await run(homeFor('03', over), '03', world);
    assert.equal(r.ok, false, name);
    assert.match(r.reason, why, name);
    assert.deepEqual(world.calls, [], `${name}: no box is used`);
  }
});

test('A_hand_check_counts_the_table_rows_that_name_a_distinct_case_id_and_nothing_else', () => {
  assert.equal(handcheckCases(handcheckOf(5)), 5);
  assert.equal(handcheckCases(handcheckOf(7)), 7);
  assert.equal(handcheckCases('# t\n\n| id | in | out |\n|---|---|---|\n| h001 | 1 | 2 |\n| h001 | 1 | 2 |\n| h002 | 3 | 4 |\n'), 2, 'one id twice is one case');
  assert.equal(handcheckCases('# t\n\n| id | in | out |\n|---|---|---|\n| first | 1 | 2 |\n| second | 3 | 4 |\n'), 0, 'a row that names no case id is not a case');
  assert.equal(handcheckCases('# t\n\n## h001\n\n## h002\n\n- h003 works\n- h004 works\n1. h005 works\n'), 0, 'headings, bullets and numbered items are not rows');
  assert.equal(handcheckCases('# t\n\nI checked h001 h002 h003 h004 h005 by hand.\n'), 0, 'prose naming ids is not a table');
  assert.equal(handcheckCases('| h0011 | x |\n| xh002 | y |\n| H003 | z |\n'), 0, 'an id is h and three digits as a whole word, in lower case');
  assert.equal(handcheckCases(''), 0);
});

test('Brute_py_and_handcheck_md_must_be_in_the_manifest_like_every_other_file_of_the_held_out_side', async () => {
  const { loadHeldout } = await import('../lib/heldout.mjs');
  const home = homeFor('02');
  const dir = taskPaths(task('02'), home).heldoutDir;
  assert.equal(loadHeldout(home, task('02')).ok, true);
  const manifestFile = path.join(dir, 'manifest.json');
  const manifest = JSON.parse(readFileSync(manifestFile, 'utf8'));
  for (const file of ['brute.py', 'handcheck.md']) {
    const { [file]: dropped, ...rest } = manifest.files;
    writeFileSync(manifestFile, JSON.stringify({ ...manifest, files: rest }), { mode: 0o600 });
    assert.match(loadHeldout(home, task('02')).reason, new RegExp(`${file.replace('.', '\\.')} is present and not in the manifest`), file);
  }
  writeFileSync(manifestFile, JSON.stringify(manifest), { mode: 0o600 });
  writeFileSync(path.join(dir, 'handcheck.md'), handcheckOf(7), { mode: 0o600 });
  assert.match(loadHeldout(home, task('02')).reason, /handcheck\.md differs from the manifest/, 'listed, an edit after the manifest was made is a difference');
});

test('A_draft_task_with_fewer_than_15_cases_or_6_mutants_is_refused_by_counts_before_any_box_is_used', async () => {
  for (const [name, over, want] of [
    ['14 cases', { cases: toyCases(14) }, /task 02 holds 14 case\(s\); the plan needs at least 15/],
    ['5 mutants', { mutants: Object.fromEntries(['M-01', 'M-02', 'M-03', 'M-04', 'M-05'].map(m => [m, { stack: 'node', expectedFail: [] }])) }, /task 02 holds 5 mutant\(s\); the plan needs at least 6/],
  ]) {
    const world = scripted();
    const r = await run(homeFor('02', over), '02', world);
    assert.equal(r.ok, false, name);
    assert.match(r.reason, want, name);
    assert.deepEqual(world.calls, [], `${name}: no box is used`);
  }
  const exactly = await run(homeFor('02', { cases: toyCases(15), mutants: sixMutants() }), '02', scripted());
  assert.equal(exactly.ok, true, 'exactly 15 cases and 6 mutants is enough');
  const one = await run(homeFor('01', { cases: toyCases(3), mutants: {}, brute: null, handcheck: null }), '01', scripted());
  assert.equal(one.ok, true, 'task 1 predates the minimums');
});

// ---- a calibration is bound to the material it checked (review F1) ---------------------------------------------------------------------------

import { calibrationFor, loadCalibration } from '../lib/limits.mjs';
import { createSeries } from '../lib/series.mjs';

const calibrated = async id => {
  const home = homeFor(id);
  const r = await run(home, id, scripted());
  assert.equal(r.ok, true, r.reason);
  return { home, dir: taskPaths(task(id), home).heldoutDir, r };
};

test('A_calibration_records_the_hashes_of_the_manifest_brute_py_and_handcheck_md_it_checked', async () => {
  const { dir, r } = await calibrated('02');
  const sha = f => createHash('sha256').update(readFileSync(path.join(dir, f))).digest('hex');
  assert.deepEqual(r.value.calibration.material, { manifestSha256: sha('manifest.json'), bruteSha256: sha('brute.py'), handcheckSha256: sha('handcheck.md') });
});

test('A_calibration_is_refused_by_the_loader_and_by_calibrationFor_when_any_of_the_three_files_changed_afterwards', async () => {
  for (const [file, text] of [['brute.py', '# a different oracle\n'], ['handcheck.md', `${handcheckOf(6)}\nan extra line\n`], ['manifest.json', '{"changed": true}\n']]) {
    const { home, dir } = await calibrated('02');
    const good = loadCalibration(home, task('02'));
    assert.equal(good.ok, true, good.reason);
    const made = createSeries({ home, explore: EXPLORE, calibration: good.value, task: '02' });
    assert.equal(calibrationFor({ home, seriesDir: made.value.dir, task: task('02') }).ok, true);
    writeFileSync(path.join(dir, file), text, { mode: 0o600 });
    for (const got of [loadCalibration(home, task('02')), calibrationFor({ home, seriesDir: made.value.dir, task: task('02') })]) {
      assert.equal(got.ok, false, file);
      assert.match(got.reason, new RegExp(`held-out material of task 02 changed since it was calibrated \\(.*${file.replace('.', '\\.')}.*\\)`), file);
    }
  }
});

test('A_calibration_of_a_draft_task_that_records_no_material_is_refused_and_task_1s_older_file_is_not', async () => {
  const home = homeFor('02');
  writeFileSync(taskPaths(task('02'), home).calibrationFile, JSON.stringify({ limits: { timeMs: 2000, memoryBytes: 536870912, buildTimeoutMs: 600000 } }));
  assert.match(loadCalibration(home, task('02')).reason, /records no material hash: calibrate again/);
  const old = tmpDir('o-cal-old');
  mkdirSync(exploreRoot(old), { recursive: true });
  writeFileSync(taskPaths(task('01'), old).calibrationFile, JSON.stringify({ limits: { timeMs: 2000, memoryBytes: 536870912, buildTimeoutMs: 600000 } }));
  assert.equal(loadCalibration(old, task('01')).ok, true, 'task 1 calibration predates the binding and has none to check');
});

test('A_failed_recalibration_leaves_no_calibration_file_and_material_changed_during_calibration_fails_it', async () => {
  const { home, dir } = await calibrated('03');
  const file = taskPaths(task('03'), home).calibrationFile;
  assert.equal(existsSync(file), true);
  writeFileSync(path.join(dir, 'brute.py'), bruteTwice({ wrongFor: [7] }), { mode: 0o600 });
  const failed = await run(home, '03', scripted());
  assert.equal(failed.ok, false);
  assert.equal(existsSync(file), false, 'the old calibration is gone at the START of a recalibration');
  const racingHome = homeFor('09');
  const touch = () => { writeFileSync(path.join(taskPaths(task('09'), racingHome).heldoutDir, 'handcheck.md'), `${handcheckOf(6)}\nedited while calibrating\n`, { mode: 0o600 }); return null; };
  const racing = await run(racingHome, '09', scripted({ script: touch }));
  assert.equal(racing.ok, false);
  assert.match(racing.reason, /changed while it was being calibrated/);
  assert.equal(existsSync(taskPaths(task('09'), racingHome).calibrationFile), false);
});

test('The_calibration_file_records_the_injected_clocks_time', async () => {
  const fixed = () => new Date('2026-11-02T03:04:05.000Z');
  const world = scripted();
  const r = await calibrateCommand({ home: homeFor('02'), say: () => {}, worldFn: world.worldFn, task: task('02'), now: fixed });
  assert.equal(r.ok, true, r.reason);
  assert.equal(r.value.calibration.made, '2026-11-02T03:04:05.000Z');
});

// ---- brute.py runs in a box, and says how far it is (coai findings 2, 7 and 4) -----------------------------------------------------------------

import net from 'node:net';
import { checkOracle } from '../lib/oracle.mjs';
import { loadHeldout } from '../lib/heldout.mjs';
import { realHome } from '../../minigit-bench/test/fake-home.mjs';
import { sandboxGate } from '../../minigit-bench/test/helpers.mjs';

/** A brute.py that reports what the box lets it do: write beside its case, connect to a local server, see the operator's home, write to TMPDIR. */
const probeBrute = port => `import os, socket, sys
held = os.path.dirname(os.path.dirname(sys.argv[1]))
res = []
try:
    open(os.path.join(held, 'x.tmp'), 'w').write('x')
    res.append('rw')
except OSError:
    res.append('ro')
try:
    socket.create_connection(('127.0.0.1', ${port}), timeout=2).close()
    res.append('net')
except OSError:
    res.append('nonet')
res.append('home' if os.path.exists(${JSON.stringify(path.join(realHome, 'rsd'))}) else 'nohome')
try:
    open(os.path.join(os.environ['TMPDIR'], 't'), 'w').write('x')
    res.append('tmpw')
except OSError:
    res.append('tmpro')
print(','.join(res))
`;

test('Brute_py_runs_in_a_box_with_no_network_the_held_out_folder_read_only_no_home_and_a_writable_temp_only', async t => {
  if (!sandboxGate(t)) return;
  const server = net.createServer(socket => socket.end());
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  t.after(() => server.close());
  const home = tmpDir('o-box');
  const cases = toyCases(5).map(c => ({ ...c, output: 'ro,nonet,nohome,tmpw\n' }));
  writeHeldout({ home, task: task('02'), cases, brute: probeBrute(server.address().port), handcheck: handcheckOf(6) });
  const held = loadHeldout(home, task('02'));
  assert.equal(held.ok, true, held.reason);
  const seen = [];
  const r = await checkOracle({ heldout: held.value, python: '/usr/bin/python3', tmpRoot: tmpDir('o-box-tmp'), operatorHome: realHome, say: l => seen.push(l) });
  assert.equal(r.ok, true, `${r.reason}\\n(the box must give ro,nonet,nohome,tmpw)`);
  assert.equal(r.value.ran, 5);
  assert.equal(existsSync(path.join(held.value.dir, 'x.tmp')), false, 'nothing was written into the held-out folder');
});

test('The_oracle_says_which_case_it_is_on_so_a_long_run_is_not_silent', async t => {
  if (!sandboxGate(t)) return;
  const home = tmpDir('o-progress');
  writeHeldout({ home, task: task('02'), cases: toyCases(15), brute: bruteTwice(), handcheck: handcheckOf(6) });
  const held = loadHeldout(home, task('02')).value;
  const seen = [];
  const r = await checkOracle({ heldout: held, python: '/usr/bin/python3', tmpRoot: tmpDir('o-progress-tmp'), operatorHome: realHome, say: l => seen.push(l) });
  assert.equal(r.ok, true, r.reason);
  assert.deepEqual(seen.filter(l => /^oracle: case /.test(l)).map(l => l.replace(/ \(.*\)$/, '')), Array.from({ length: 15 }, (_, i) => `oracle: case ${i + 1} of 15`));
  assert.match(seen.at(-1), /^oracle: brute\.py agrees with gen\.py on 15 case\(s\)/);
});

// ---- long id lists are cut in a message (coai finding 13) ------------------------------------------------------------------------------------

import { listOf } from '../lib/idlist.mjs';

test('A_list_of_ids_in_a_message_is_the_first_ten_then_and_N_more_with_the_total', () => {
  const ids = Array.from({ length: 25 }, (_, i) => `h${String(i + 1).padStart(3, '0')}`);
  assert.equal(listOf(ids.slice(0, 10)), ids.slice(0, 10).join(', '), 'ten are all shown');
  assert.equal(listOf(ids), `${ids.slice(0, 10).join(', ')}, … and 15 more (25 in all)`);
  assert.equal(listOf(ids.slice(0, 11)), `${ids.slice(0, 10).join(', ')}, … and 1 more (11 in all)`, 'eleven is already cut');
  assert.equal(listOf([]), '');
});

test('An_oracle_that_disagrees_on_every_case_names_ten_ids_and_the_total_not_all_of_them', async () => {
  const cases = toyCases(25);
  const home = homeFor('09', { cases, brute: bruteTwice({ wrongFor: cases.map((c, i) => i + 1) }) });
  const r = await run(home, '09', scripted());
  assert.equal(r.ok, false);
  assert.match(r.reason, /brute\.py disagrees with gen\.py on case h001, h002, h003, h004, h005, h006, h007, h008, h009, h010, … and 15 more \(25 in all\):/);
  assert.doesNotMatch(r.reason, /h011/);
});
