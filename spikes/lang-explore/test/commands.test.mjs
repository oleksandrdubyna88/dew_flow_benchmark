// The commands around the loop: `init` (the draw is made and recorded), `status`, `prune` (the reproducible copies go, the records stay), `quiet`, `leakcheck` (the held-out outputs as v1's needles), and the
// command line's usage errors. Each runs over a temp home; nothing here touches the operator's files.
import '../../minigit-bench/test/fake-home.mjs';
import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { chmodSync, existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { main } from '../explore.mjs';
import { EXIT, cmdInit, cmdPrune, cmdQuiet, cmdRun, decidedPublicText, leakShingles, statusLines } from '../lib/commands.mjs';
import { buildMaterial } from '../lib/leak-material.mjs';
import { scanRepository } from '../lib/leak-scan.mjs';
import { exploreRoot } from '../lib/paths.mjs';
import { resolveTask, taskPaths } from '../lib/tasks.mjs';
import { calibratedHome } from './olympiad-fixture.mjs';
import { createSeries, drawMask, loadSeries } from '../lib/series.mjs';
import { loadExplore } from '../lib/inputs.mjs';

const EXPLORE = loadExplore().value;
const TASK = resolveTask(EXPLORE, '01').value;
const calibrationFile = home => taskPaths(TASK, home).calibrationFile;
const CALIBRATION = { limits: { timeMs: 2000, memoryBytes: 536870912, buildTimeoutMs: 600000 } };
const io = (home, over = {}) => { const said = []; return { home, said, say: l => said.push(l), ...over }; };

function homeWithCalibration() {
  const home = tmpDir('cmd-home');
  mkdirSync(exploreRoot(home), { recursive: true });
  writeFileSync(calibrationFile(home), JSON.stringify(CALIBRATION));
  return home;
}

test('Init_creates_a_series_with_the_mask_draw_recorded_and_refuses_without_a_calibration', async () => {
  const empty = io(tmpDir('no-cal'));
  assert.equal(await cmdInit({ task: '02' }, empty), EXIT.config);
  assert.match(empty.said[0], /explore\.mjs calibrate/);
  const home = calibratedHome('02', EXPLORE);
  const ok = io(home);
  assert.equal(await cmdInit({ task: '02' }, ok), EXIT.done, ok.said.join('\n'));
  assert.match(ok.said[0], /^series x-\d{8}-[0-9a-f]{4} created at /);
  assert.match(ok.said[1], /the mask draw \(recorded\): C# starts on CPUs (8-13|0-5) in pair 1/);
  const dir = ok.said[0].split(' created at ')[1];
  const series = loadSeries(dir).value;
  assert.equal(typeof series.state.draw.csharpHigh, 'boolean');
  assert.match(series.state.draw.method, /randomInt/);
});

test('Status_lists_the_series_the_pairs_and_every_session', () => {
  const home = homeWithCalibration();
  const made = createSeries({ home, explore: EXPLORE, calibration: CALIBRATION, task: '01', draw: drawMask(() => 0) });
  const lines = statusLines({ dir: made.value.dir, ...loadSeries(made.value.dir).value });
  assert.match(lines[0], /^series x-.* spend \$0\.0000; draw: C# 0-5 in pair 1$/);
  assert.deepEqual(lines.slice(1), ['pair 1: csharp none, node none', 'pair 2: csharp none, node none', 'pair 3: csharp none, node none']);
});

test('Prune_removes_the_cache_and_scoring_copies_of_every_session_and_keeps_the_records', () => {
  const home = homeWithCalibration();
  const made = createSeries({ home, explore: EXPLORE, calibration: CALIBRATION, task: '01' });
  const session = path.join(made.value.dir, 's', 'abc123');
  for (const rel of ['box/nuget/a/f', 'box/npm/_cacache/x', 'box/home/.dotnet/state', 'score/judge/tree/f', 'box/work/src/main.ts', 'record.json', 'stream.ndjson']) {
    mkdirSync(path.dirname(path.join(session, rel)), { recursive: true });
    writeFileSync(path.join(session, rel), 'x');
  }
  chmodSync(path.join(session, 'box/nuget/a/f'), 0o444);
  chmodSync(path.join(session, 'box/nuget/a'), 0o555);
  const run = io(home);
  assert.equal(cmdPrune({ series: made.value.dir, task: '01' }, run), EXIT.done);
  assert.match(run.said[0], /^pruned 4 directories: /);
  for (const gone of ['box/nuget', 'box/npm', 'box/home/.dotnet', 'score']) assert.equal(existsSync(path.join(session, gone)), false, gone);
  for (const kept of ['box/work/src/main.ts', 'record.json', 'stream.ndjson']) assert.equal(existsSync(path.join(session, kept)), true, kept);
  const again = io(home);
  cmdPrune({ series: made.value.dir, task: '01' }, again);
  assert.match(again.said[0], /^pruned 0 directories: nothing to remove$/);
  assert.equal(cmdPrune({ task: '01' }, io(tmpDir('nothing'))), EXIT.config, 'no series is a refusal');
});

const QUIET = { hostCpus: 0.1, harnessCpus: 0.05, foreignCpus: 0, foreignAgents: [], memAvailablePct: 80, liveSessions: [], spinMs: 20 };

test('Quiet_prints_each_verdict_and_exits_3_when_the_machine_never_is_and_0_when_it_is_or_when_accepted_noisy', async () => {
  const home = homeWithCalibration();
  writeFileSync(path.join(exploreRoot(home), 'spin.json'), JSON.stringify({ medianMs: 20 }));
  const seams = sample => ({ samplerFn: async () => async () => sample, sleepFn: async () => {}, now: (() => { let t = 0; return () => (t += 7000); })() });
  const loud = io(home, { machineSeams: seams({ ...QUIET, hostCpus: 6, foreignCpus: 5.9 }) });
  assert.equal(await cmdQuiet({}, loud), EXIT.halt);
  assert.ok(loud.said.length > 3 && /not quiet/.test(loud.said[0]));
  assert.match(loud.said.at(-1), /the machine was not quiet/);
  const noisy = io(home, { machineSeams: seams({ ...QUIET, hostCpus: 6, foreignCpus: 5.9 }) });
  assert.equal(await cmdQuiet({ 'accept-noisy': 'testing' }, noisy), EXIT.done);
  assert.match(noisy.said.at(-1), /started anyway: started not quiet \(testing\)/);
  const quiet = io(home, { machineSeams: seams(QUIET) });
  assert.equal(await cmdQuiet({}, quiet), EXIT.done);
  assert.equal(quiet.said.at(-1), 'the machine is quiet');
  assert.equal(await cmdQuiet({}, io(tmpDir('no-spin'))), EXIT.config, 'an uncalibrated spin probe is a refusal');
});

test('The_leak_needles_are_held_out_output_lines_and_reference_lines_of_8_or_more_characters_and_v1s_leak_check_finds_them', async () => {
  const dir = tmpDir('leak-held');
  const casesDir = path.join(dir, 'cases');
  mkdirSync(casesDir);
  mkdirSync(path.join(dir, 'refs', 'csharp', 'src'), { recursive: true });
  mkdirSync(path.join(dir, 'mutants', 'M-1', 'src'), { recursive: true });
  writeFileSync(path.join(casesDir, 'a.out'), '1\n');
  writeFileSync(path.join(casesDir, 'b.out'), 'xyzzy 11 22 33 44 55 66\n');
  writeFileSync(path.join(casesDir, 'c.out'), 'xyzzy 11 22 33 44 55 66\n');
  writeFileSync(path.join(dir, 'refs', 'csharp', 'src', 'Program.cs'), 'using System.Globalization;\n\nlong frobnicate = quuxify(input, 7, extra);\nvar sb = new Thing();\n}\n');
  writeFileSync(path.join(dir, 'mutants', 'M-1', 'src', 'Program.cs'), 'int frobnicate = quuxify(input, 7, extra);\n');
  const built = buildMaterial({ tasks: [{ id: '01', heldout: { dir, casesDir }, shinglesText: '' }] });
  assert.equal(built.ok, true, built.reason);
  const needles = built.value.substrings.map(n => n.text).sort();
  assert.deepEqual(needles, ['int frobnicate = quuxify(input, 7, extra);', 'long frobnicate = quuxify(input, 7, extra);', 'xyzzy 11 22 33 44 55 66'], 'a short line, an import and bare punctuation are no needles; a repeated line is one');
  const repo = tmpDir('leak-repo');
  const git = (...args) => execFileSync('git', ['-C', repo, '-c', 'user.name=t', '-c', 'user.email=t@invalid', ...args], { encoding: 'utf8' });
  git('init', '-q');
  writeFileSync(path.join(repo, 'clean.md'), 'nothing here\n');
  git('add', '.');
  git('commit', '-q', '-m', 'clean');
  const clean = await scanRepository({ repoRoot: repo, material: built.value, range: null });
  assert.equal(clean.ok, true, clean.reason);
  for (const [name, content] of [['output', 'a result: xyzzy 11 22 33 44 55 66 and more'], ['reference', 'code: long frobnicate = quuxify(input, 7, extra); // copied']]) {
    writeFileSync(path.join(repo, 'note.md'), `${content}\n`);
    git('add', '.');
    git('commit', '-q', '-m', name);
    const dirty = await scanRepository({ repoRoot: repo, material: built.value, range: null });
    assert.equal(dirty.ok, false, name);
    assert.match(dirty.reason, /held-out text found in .*note\.md/);
    assert.ok(!dirty.reason.includes('xyzzy') && !dirty.reason.includes('frobnicate'), 'a hit names the file, never the needle');
  }
});

test('A_plan_promoted_to_research_stays_public_by_decision_beside_the_plans_in_todo', () => {
  const repo = tmpDir('public-text');
  for (const d of ['todo', 'research', path.join('spikes', 'lang-explore', 'inputs')]) mkdirSync(path.join(repo, d), { recursive: true });
  writeFileSync(path.join(repo, 'todo', 'PLAN_open.md'), 'open plan wording\n');
  writeFileSync(path.join(repo, 'research', 'PLAN_done.md'), 'promoted plan wording\n');
  writeFileSync(path.join(repo, 'research', 'notes.md'), 'research prose that is no plan\n');
  const text = decidedPublicText(repo);
  assert.ok(text.includes('open plan wording'));
  assert.ok(text.includes('promoted plan wording'), 'moving a plan to research/ does not turn its public wording into needles');
  assert.ok(!text.includes('research prose that is no plan'), 'only plans are public by decision');
});

test('The_private_notes_become_six_word_shingles_that_find_a_copied_sentence', async () => {
  const notes = 'The smallest case whose peak passes the first bound separates the two failure classes. A second line.';
  const shingles = leakShingles(notes);
  assert.ok(!leakShingles(notes, 'Our plan says: the smallest case whose peak passes the first bound, in public.').includes('smallest case whose peak passes the'), 'a window the plans already say in public is no needle');
  assert.ok(leakShingles(notes, 'Our plan says: the smallest case whose peak passes the first bound, in public.').includes('separates the two failure classes a'));
  assert.ok(shingles.split('\n').every(l => l.split(' ').length === 6 && l === l.toLowerCase()));
  assert.ok(shingles.includes('smallest case whose peak passes the'));
  const repo = tmpDir('shingle-repo');
  const git = (...args) => execFileSync('git', ['-C', repo, '-c', 'user.name=t', '-c', 'user.email=t@invalid', ...args], { encoding: 'utf8' });
  git('init', '-q');
  writeFileSync(path.join(repo, 'a.md'), 'Prose of our own, with no shared run of six words.\n');
  git('add', '.');
  git('commit', '-q', '-m', 'a');
  const empty = tmpDir('shingle-held');
  mkdirSync(path.join(empty, 'cases'));
  const material = buildMaterial({ tasks: [{ id: '01', heldout: { dir: empty, casesDir: path.join(empty, 'cases') }, shinglesText: shingles }] }).value;
  assert.equal((await scanRepository({ repoRoot: repo, material, range: null })).ok, true);
  writeFileSync(path.join(repo, 'b.md'), 'See: the smallest case, whose peak passes the first bound!\n');
  git('add', '.');
  git('commit', '-q', '-m', 'b');
  const dirty = await scanRepository({ repoRoot: repo, material, range: null });
  assert.equal(dirty.ok, false);
  assert.match(dirty.reason, /b\.md/);
});

test('The_command_line_refuses_an_unknown_command_and_an_unknown_flag_with_the_usage_exit', async () => {
  const quiet = process.stderr.write.bind(process.stderr);
  const written = [];
  process.stderr.write = chunk => { written.push(String(chunk)); return true; };
  try {
    assert.equal(await main(['frobnicate']), EXIT.config);
    assert.equal(await main(['status', '--nonsense']), EXIT.config);
    assert.equal(await main([]), EXIT.config);
  } finally {
    process.stderr.write = quiet;
  }
  assert.match(written.join(''), /the commands are prepare, calibrate, rehearse, init, quiet, run, status, readback, note, prune, leakcheck/);
  assert.match(written.join(''), /usage error/);
});

test('A_pair_is_refused_before_anything_is_launched_when_there_is_no_calibration_or_a_barrier_is_not_met', async () => {
  const { runPair } = await import('../lib/run.mjs');
  const home = tmpDir('norun-home');
  mkdirSync(exploreRoot(home), { recursive: true });
  const made = createSeries({ home, explore: EXPLORE, calibration: CALIBRATION, task: '01' });
  const never = () => { throw new Error('the world must not be loaded for a refused pair'); };
  const base = { home, say: () => {}, abort: { reason: null, exit: 130 }, worldFn: never, tokenFn: never };
  const noCal = await runPair({ seriesDir: made.value.dir, pair: 1, explore: EXPLORE, task: TASK, io: base });
  assert.equal(noCal.exit, 4);
  assert.match(noCal.message, /explore\.mjs calibrate/, 'no limits, no paid session');
  const early = await runPair({ seriesDir: made.value.dir, pair: 2, explore: EXPLORE, task: TASK, io: base });
  assert.match(early.message, /pair 1 is not finished/);
  assert.deepEqual(Object.keys(loadSeries(made.value.dir).value.map.sessions), [], 'nothing was planned');
});

test('F3_run_requires_series_and_a_rehearsal_series_is_refused_by_run', async () => {
  const home = homeWithCalibration();
  const real = createSeries({ home, explore: EXPLORE, calibration: CALIBRATION, task: '01' });
  assert.ok(real.ok);
  const noSeries = io(home);
  assert.equal(await cmdRun({ task: '01', pair: '1' }, noSeries), EXIT.config, 'with a series present, run still does not guess it');
  assert.match(noSeries.said[0], /run needs --series <dir>/);
  const rehearsal = createSeries({ home, explore: EXPLORE, calibration: CALIBRATION, task: '01', prefix: 'r' });
  const { runPair } = await import('../lib/run.mjs');
  const refused = await runPair({ seriesDir: rehearsal.value.dir, pair: 1, explore: EXPLORE, task: TASK, io: { home, say: () => {}, abort: {}, worldFn: () => { throw new Error('unused'); }, tokenFn: () => { throw new Error('unused'); } } });
  assert.equal(refused.exit, 4);
  assert.match(refused.message, /rehearsal series/);
});

test('Run_needs_a_positive_whole_number_for_pair_and_says_what_it_got_before_anything_else_is_read', async () => {
  const home = homeWithCalibration();
  const real = createSeries({ home, explore: EXPLORE, calibration: CALIBRATION, task: '01' });
  for (const pair of [undefined, '', '0', '-1', '1.5', 'abc', '1e3', ' 1', '01']) {
    const c = io(home);
    assert.equal(await cmdRun({ task: '01', series: real.value.dir, ...(pair === undefined ? {} : { pair }) }, c), EXIT.config, `pair ${JSON.stringify(pair)}`);
    assert.match(c.said[0], /run needs --pair <k>, a positive whole number/, `pair ${JSON.stringify(pair)}`);
  }
  const named = io(home);
  await cmdRun({ task: '01', series: real.value.dir, pair: 'x' }, named);
  assert.match(named.said[0], /\(got 'x'\)/);
  const none = io(home);
  await cmdRun({ task: '01', series: real.value.dir }, none);
  assert.match(none.said[0], /\(got nothing\)/);
});

// ---- the coai review's items on the commands -------------------------------------------------------------------------------------------

test('The_note_goes_to_a_filename_dated_by_the_series_never_over_an_existing_file_without_force_and_under_the_series_limits', async () => {
  const { notePathOf, cmdNote } = await import('../lib/commands.mjs');
  assert.match(notePathOf({}, { createdAt: '2026-10-15T08:00:00Z' }, TASK), /research\/lang-v2\/EXPLORE_task1_2026-10-15\.md$/, 'the date of the series, not of the day the command runs');
  assert.equal(notePathOf({ out: '/x/y.md' }, { createdAt: '2026-11-01T08:00:00Z' }, TASK), '/x/y.md');
  const home = homeWithCalibration();
  writeFileSync(calibrationFile(home), JSON.stringify({ limits: { ...CALIBRATION.limits, timeMs: 5000 } })); // calibrated again after init
  const made = createSeries({ home, explore: EXPLORE, calibration: CALIBRATION, task: '01', date: new Date('2026-11-01T08:00:00Z') });
  const out = path.join(tmpDir('note-out'), 'note.md');
  const worldFn = async () => ({ ok: true, value: { explore: EXPLORE } });
  const first = io(home, { worldFn });
  assert.equal(await cmdNote({ series: made.value.dir, task: '01', out }, first), EXIT.done, first.said.join('\n'));
  const text = readFileSync(out, 'utf8');
  assert.match(text, /time limit 2000 ms/, 'the limits the series was created with, not the live calibration file\'s 5000');
  writeFileSync(out, 'precious');
  const again = io(home, { worldFn });
  assert.equal(await cmdNote({ series: made.value.dir, task: '01', out }, again), EXIT.config);
  assert.match(again.said[0], /exists: the note is not overwritten without --force/);
  assert.equal(readFileSync(out, 'utf8'), 'precious');
  assert.equal(await cmdNote({ series: made.value.dir, task: '01', out, force: true }, io(home, { worldFn })), EXIT.done);
  assert.match(readFileSync(out, 'utf8'), /^# Exploratory pilot/);
});

test('Readback_compares_a_session_with_the_prompts_frozen_in_its_series_not_with_the_live_file', async () => {
  const { cmdReadback } = await import('../lib/commands.mjs');
  const { loadPrompts } = await import('../lib/plan.mjs');
  const { planForSession } = await import('../lib/plan.mjs');
  const { recordPlan } = await import('../lib/readback.mjs');
  const { planPair, runMapFileOf } = await import('../lib/series.mjs');
  const { openSeries, writeRunMap } = await import('../../minigit-bench/lib/run-map.mjs');
  const home = homeWithCalibration();
  const frozen = loadPrompts();
  const made = createSeries({ home, explore: EXPLORE, calibration: CALIBRATION, task: '01', inputsExtra: { prompts: frozen } });
  const planned = planPair({ map: openSeries({ seriesId: 'x', caps: {} }), pair: 1, pairs: 3, draw: made.value.state.draw });
  writeRunMap(runMapFileOf(made.value.dir), planned.value.map);
  for (const e of planned.value.planned) {
    const dir = path.join(made.value.dir, 's', e.rid);
    mkdirSync(dir, { recursive: true });
    const ctx = { claudeBinary: '/x', cliVersion: '2.1.310', nodeDir: '/n', tsDir: '/t', token: 't', prices: {}, home: '/h', harnessSources: {}, caps: EXPLORE.caps, envExtra: EXPLORE.envExtra, locale: EXPLORE.locale, prompts: frozen };
    recordPlan(planForSession({ ctx, item: { session: { stack: e.stack }, layout: { dir, rid: e.rid } } }));
    writeFileSync(path.join(dir, 'argv.json'), JSON.stringify({ envKeys: ['LANG', 'LC_ALL'] }));
  }
  const ok = io(home);
  assert.equal(cmdReadback({ series: made.value.dir, task: '01' }, ok), EXIT.done, ok.said.join('\n'));
  // the series' frozen prompt for node changes: the session was sent the other one
  const file = path.join(made.value.dir, 'inputs.json');
  const inputs = JSON.parse(readFileSync(file, 'utf8'));
  inputs.prompts.prompts.node.sha256 = 'f'.repeat(64);
  writeFileSync(file, JSON.stringify(inputs));
  const refuted = io(home);
  assert.equal(cmdReadback({ series: made.value.dir, task: '01' }, refuted), EXIT.failed);
  assert.ok(refuted.said.some(l => /FAIL {2}the planned prompt is the registered one/.test(l)), 'the live prompts.json still agrees; the series\' frozen copy does not');
  delete inputs.prompts;
  writeFileSync(file, JSON.stringify(inputs));
  assert.equal(cmdReadback({ series: made.value.dir, task: '01' }, io(home)), EXIT.config, 'a series without frozen prompts is refused');
});

test('An_unknown_command_is_echoed_back_in_the_usage_error', async () => {
  const written = [];
  const real = process.stderr.write.bind(process.stderr);
  process.stderr.write = chunk => { written.push(String(chunk)); return true; };
  try { await main(['frobnicate']); } finally { process.stderr.write = real; }
  assert.match(written.join(''), /'frobnicate' is not a command/);
});
