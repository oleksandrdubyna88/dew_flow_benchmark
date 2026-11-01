// The leak check reads every task's private material (plan research/PLAN_lang_explore_olympiad.md D-O2, T-O5): the needles of task NN come from `heldout-NN/` only, `leakcheck` unions every task's folder (and every task's
// private notes), a task whose folder is absent is named and skipped, and a folder that does not match its manifest fails. Held-out folders are fixtures in a temp home; nothing here reads a real one.
import '../../minigit-bench/test/fake-home.mjs';
import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { mkdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { EXIT, cmdLeakcheck } from '../lib/commands.mjs';
import { loadExplore } from '../lib/inputs.mjs';
import { leakIds, leakMaterial, leakcheckCommand } from '../lib/leak.mjs';
import { scanRepository } from '../lib/leak-scan.mjs';
import { normLine } from '../lib/leak-text.mjs';
import { resolveTask, taskPaths } from '../lib/tasks.mjs';
import { handcheckOf, toyCases, writeHeldout } from './olympiad-fixture.mjs';

const EXPLORE = loadExplore().value;
const task = id => resolveTask(EXPLORE, id).value;
const marked = id => toyCases(4).map(c => ({ ...c, input: `input-${id}-${c.id}-QWERTY\nshort\n`, output: `answer-${id}-${c.id}-XYZZY\n` }));
const refLine = id => `const frobnicate${id} = quuxify(input, ${id}, extra, more);`;
const NOTES = { '02': 'the smallest grid whose first row separates two areas matters most here', '03': 'a division of the minimum value by minus one must wrap round to itself' };

/** A home with held-out folders for the tasks in `ids` (each with its own marked outputs, reference line and oracle files) and private notes for those in NOTES. */
function homeWith(ids) {
  const home = tmpDir('o-leak');
  for (const id of ids) {
    writeHeldout({ home, task: task(id), cases: marked(id), traps: ['h002'], mutants: Object.fromEntries(['M-01', 'M-02', 'M-03', 'M-04', 'M-05', 'M-06'].map(m => [m, { stack: 'node', expectedFail: [] }])), refs: { csharp: { 'src/Program.cs': `${refLine(id)}\n` }, node: { 'src/main.ts': `${refLine(id)}\n` } }, gen: `# generator\ngenerator_${id}_distinctive_line = build_the_cases_somehow(${id})\n`, brute: `# brute for ${id}\nnaive_${id}_solution_line = compute_it_the_slow_way(${id})\n`, handcheck: `${handcheckOf(5)}\nhand_${id}_worked_example_line = 'a case worked by hand ${id}'\n` });
    const notes = NOTES[id];
    if (notes) { mkdirSync(path.dirname(taskPaths(task(id), home).notesFile), { recursive: true }); writeFileSync(taskPaths(task(id), home).notesFile, notes); }
  }
  return home;
}

/** The substring needles (their normalised texts) of task `id` in the structured material. */
const substringsOf = (m, id) => m.material.substrings.filter(n => n.task === id).map(n => n.text);

test('The_needles_of_task_NN_come_from_its_own_folder_only_outputs_reference_lines_and_the_oracle_files', () => {
  const home = homeWith(['02', '03']);
  const m = leakMaterial({ home, explore: EXPLORE, publicText: '' }).value;
  const needles = id => substringsOf(m, id);
  const n02 = needles('02');
  assert.ok(n02.includes('answer-02-h001-xyzzy'), 'a case line, normalised (lower case)');
  assert.ok(n02.includes(normLine(refLine('02'))));
  assert.ok(n02.some(l => l.startsWith('naive_02_solution_line')), 'the oracle\'s brute.py lines are needles');
  assert.ok(n02.some(l => l.startsWith('hand_02_worked_example_line')), 'and the hand-checked cases');
  assert.deepEqual(n02.filter(l => /answer-03|_03_|frobnicate03/.test(l)), [], 'nothing of task 03 is a needle of task 02');
  assert.deepEqual(needles('03').filter(l => /answer-02|_02_|frobnicate02/.test(l)), [], 'nor the other way round');
});

test('The_needles_also_hold_the_held_out_input_lines_of_8_characters_or_more_and_the_generators_lines', () => {
  const home = homeWith(['02']);
  const n = substringsOf(leakMaterial({ home, explore: EXPLORE, publicText: '' }).value, '02');
  assert.ok(n.includes('input-02-h002-qwerty'), 'an input line of a case');
  assert.ok(!n.includes('short'), 'a line under 8 characters is no needle (v1 refuses it)');
  assert.ok(n.some(l => l.startsWith('generator_02_distinctive_line')), 'a line of gen.py, as the references\' lines are');
});

test('The_mutant_and_trap_ids_of_a_manifest_are_found_as_whole_words_in_the_public_notes_and_nowhere_else', () => {
  const home = homeWith(['02']);
  const ids = leakMaterial({ home, explore: EXPLORE, publicText: '' }).value.ids;
  assert.ok(ids.includes('M-01') && ids.includes('M-02') && ids.includes('M-06'), ids.join());
  const repo = tmpDir('o-leak-ids');
  const dir = path.join(repo, 'research', 'lang-v2');
  mkdirSync(dir, { recursive: true });
  writeFileSync(path.join(dir, 'clean.md'), 'mutant M-010 and xM-01 and M-01x and the word h0031 are no ids\n');
  writeFileSync(path.join(dir, 'dirty.md'), 'the second mutant, M-02, was caught\n');
  writeFileSync(path.join(repo, 'elsewhere.md'), 'M-03 here is not a public note\n');
  assert.deepEqual(leakIds({ repo, ids }), ['dirty.md'], 'the file is named, never the id');
});

test('A_present_task_whose_private_notes_are_missing_fails_the_check_instead_of_reading_no_shingles', () => {
  const home = homeWith(['02', '09']);
  const r = leakMaterial({ home, explore: EXPLORE, publicText: '' });
  assert.equal(r.ok, false);
  assert.match(r.reason, /the private notes of task 09 are missing/);
});

test('Leakcheck_reads_every_task_s_material_by_default_and_one_task_s_when_asked', () => {
  const home = homeWith(['02', '03']);
  const all = leakMaterial({ home, explore: EXPLORE, publicText: '' });
  assert.equal(all.ok, true, all.reason);
  assert.deepEqual([all.value.scanned, all.value.absent], [['02', '03'], ['01', '09']]);
  for (const id of ['02', '03']) assert.ok(substringsOf(all.value, id).includes(`answer-${id}-h001-xyzzy`), `task ${id}'s outputs are needles of the default scan`);
  assert.ok(all.value.material.shingleTexts.includes('the smallest grid whose first row') && all.value.material.shingleTexts.includes('a division of the minimum value'), 'every task\'s private notes are read');
  const one = leakMaterial({ home, explore: EXPLORE, only: '03', publicText: '' });
  assert.deepEqual([one.value.scanned, one.value.absent], [['03'], []], 'a named task is the only one loaded');
  assert.ok(!one.value.material.substrings.some(n => n.text.includes('answer-02-')), '--task 03 reads task 03 only');
});

test('A_line_of_one_task_copied_into_a_file_is_found_by_the_check_over_every_task_and_never_names_the_needle', async () => {
  const home = homeWith(['02', '03']);
  const { material } = leakMaterial({ home, explore: EXPLORE, publicText: '' }).value;
  const repo = tmpDir('o-leak-repo');
  const git = (...args) => execFileSync('git', ['-C', repo, '-c', 'user.name=t', '-c', 'user.email=t@invalid', ...args], { encoding: 'utf8' });
  git('init', '-q');
  writeFileSync(path.join(repo, 'clean.md'), 'nothing of the pilot here\n');
  git('add', '.');
  git('commit', '-q', '-m', 'clean');
  assert.equal((await scanRepository({ repoRoot: repo, material, range: null })).ok, true);
  for (const [name, content] of [['an output of task 03', 'result: answer-03-h002-XYZZY'], ['a reference line of task 02', `code ${refLine('02')} // copied`], ['a note sentence of task 03', 'see: a division of the minimum value by minus one must wrap round to itself']]) {
    writeFileSync(path.join(repo, 'note.md'), `${content}\n`);
    git('add', '.');
    git('commit', '-q', '-m', name);
    const dirty = await scanRepository({ repoRoot: repo, material, range: null });
    assert.equal(dirty.ok, false, name);
    assert.match(dirty.reason, /note\.md/, name);
    assert.ok(!/XYZZY|frobnicate|minimum value/.test(dirty.reason), `${name}: a hit names the file, never the needle`);
  }
});

test('A_folder_that_does_not_match_its_manifest_or_no_folder_at_all_fails_the_check_instead_of_scanning_less', () => {
  const home = homeWith(['02', '03']);
  writeFileSync(path.join(taskPaths(task('02'), home).heldoutDir, 'cases', 'h001.out'), 'tampered\n', { mode: 0o600 });
  const broken = leakMaterial({ home, explore: EXPLORE, publicText: '' });
  assert.equal(broken.ok, false);
  assert.match(broken.reason, /^task 02: the held-out side does not match its manifest/);
  const empty = leakMaterial({ home: tmpDir('o-leak-empty'), explore: EXPLORE, publicText: '' });
  assert.equal(empty.ok, false);
  assert.match(empty.reason, /no held-out folder is present/);
});

test('The_leakcheck_command_refuses_an_unknown_task_and_reports_what_it_could_not_read_with_exit_4', async () => {
  const say = [];
  const unknown = { home: tmpDir('o-leak-cmd'), say: l => say.push(l) };
  assert.equal(await cmdLeakcheck({ task: '04' }, unknown), EXIT.config);
  assert.match(say.join('\n'), /--task 04 is not a task of the pilot: the tasks are 01, 02, 03, 09/);
  assert.equal(await cmdLeakcheck({}, unknown), EXIT.config);
  assert.match(say.join('\n'), /no held-out folder is present/);
});

// ---- a code line the agents already have in their runner is not a leak (collision with spikes/lang-explore/runner) -------------------------------

import { committedRunnerLines } from '../lib/leak.mjs';
import { readFileSync } from 'node:fs';

const SHARED = 'path = os.path.join(base, name)  # a generic line two programs share';
const RUNNER = 'spikes/lang-explore/runner/run_samples.py';

/** A temp repository whose COMMITTED runner holds `committed` lines, with `working` written over it afterwards, uncommitted. */
function repoWithRunner(committed, working = null) {
  const repo = tmpDir('o-runner-repo');
  const git = (...args) => execFileSync('git', ['-C', repo, '-c', 'user.name=t', '-c', 'user.email=t@invalid', ...args], { encoding: 'utf8' });
  git('init', '-q');
  mkdirSync(path.join(repo, path.dirname(RUNNER)), { recursive: true });
  writeFileSync(path.join(repo, RUNNER), `${committed.join('\n')}\n`);
  git('add', '.');
  git('commit', '-q', '-m', 'runner');
  if (working) writeFileSync(path.join(repo, RUNNER), `${working.join('\n')}\n`);
  return repo;
}

const home02 = over => { const home = tmpDir('o-collide'); writeHeldout({ home, task: task('02'), cases: marked('02'), mutants: Object.fromEntries(['M-01', 'M-02', 'M-03', 'M-04', 'M-05', 'M-06'].map(m => [m, { stack: 'node', expectedFail: [] }])), brute: 'x = 1\n', handcheck: handcheckOf(5), ...over }); mkdirSync(path.dirname(taskPaths(task('02'), home).notesFile), { recursive: true }); writeFileSync(taskPaths(task('02'), home).notesFile, NOTES['02']); return home; }

/** The leak scan over `repo` with the material of task 02 under `runnerLines` (the exemption), or without any. */
async function check(home, repo, runnerLines) {
  const m = leakMaterial({ home, explore: EXPLORE, only: '02', publicText: '', ...(runnerLines ? { runnerLines } : {}) });
  assert.equal(m.ok, true, m.reason);
  return scanRepository({ repoRoot: repo, material: m.value.material, range: null });
}

test('A_private_code_line_that_is_also_a_line_of_the_committed_runner_is_dropped_from_the_needles_and_nothing_else_is', async () => {
  const home = home02({ gen: `# generator\n${SHARED}\ngenerator_distinctive_line_of_task_02 = build_the_cases_somehow(2)\n` });
  const repo = repoWithRunner(['import os', SHARED, 'def main(): pass']);
  const without = await check(home, repo, null);
  assert.equal(without.ok, false, 'today: the collision fails the check');
  assert.match(without.reason, /run_samples\.py/);
  const runnerLines = await committedRunnerLines({ repoRoot: repo });
  assert.equal(runnerLines.has(normLine(SHARED)), true);
  assert.equal((await check(home, repo, runnerLines)).ok, true, 'with the exemption the collision is no leak');
});

test('A_case_line_is_never_exempt_whatever_public_file_it_matches', async () => {
  const cases = marked('02').map((c, i) => (i === 0 ? { ...c, output: `${SHARED}\n` } : c));
  const home = home02({ cases });
  const repo = repoWithRunner(['import os', SHARED]);
  const runnerLines = await committedRunnerLines({ repoRoot: repo });
  const r = await check(home, repo, runnerLines);
  assert.equal(r.ok, false, 'the same line as a held-out .out line still fails');
  assert.match(r.reason, /run_samples\.py/);
  const inputs = home02({ cases: marked('02').map((c, i) => (i === 0 ? { ...c, input: `${SHARED}\n` } : c)) });
  assert.equal((await check(inputs, repo, runnerLines)).ok, false, 'and as a held-out .in line');
});

test('A_code_needle_that_matches_no_runner_line_and_an_uncommitted_runner_edit_do_not_exempt_anything', async () => {
  const own = 'generator_distinctive_line_of_task_02 = build_the_cases_somehow(2)';
  const home = home02({ gen: `# generator\n${own}\n` });
  const committed = repoWithRunner(['import os', 'def main(): pass'], ['import os', own]);
  const runnerLines = await committedRunnerLines({ repoRoot: committed });
  assert.equal(runnerLines.has(normLine(own)), false, 'the working copy is not read: an uncommitted edit creates no exemption');
  const r = await check(home, committed, runnerLines);
  assert.equal(r.ok, false);
  assert.match(r.reason, /run_samples\.py/);
  const clean = repoWithRunner(['import os', 'def main(): pass']);
  assert.equal((await check(home, clean, await committedRunnerLines({ repoRoot: clean }))).ok, true, 'control: with the needle in no file, the check is clean');
  assert.equal(readFileSync(path.join(committed, RUNNER), 'utf8').includes(own), true, 'the edit is really in the working copy');
});

test('A_note_sentence_is_never_exempt_even_when_the_runner_says_it_too', async () => {
  const home = home02({});
  const repo = repoWithRunner(['import os', `# ${NOTES['02']}`]);
  const r = await check(home, repo, await committedRunnerLines({ repoRoot: repo }));
  assert.equal(r.ok, false, 'shingles of the private notes are not touched by the runner exemption');
});

test('The_committed_runner_lines_are_read_from_git_head_and_a_repository_without_a_runner_gives_none', async () => {
  const none = tmpDir('o-no-runner');
  execFileSync('git', ['-C', none, 'init', '-q']);
  assert.equal((await committedRunnerLines({ repoRoot: none })).size, 0);
  const repo = repoWithRunner(['  indented line stays trimmed to its text  ', '', 'second line']);
  assert.deepEqual([...await committedRunnerLines({ repoRoot: repo })].sort(), ['indented line stays trimmed to its text', 'second line']);
});

// ---- the result line names counts only (redesign brief) ------------------------------------------------------------------------------------------

import { leakLine } from '../lib/leak.mjs';

test('The_result_line_reports_the_files_the_binaries_the_skipped_short_lines_and_the_baseline_collisions_as_counts', () => {
  const clean = { ok: true, scanned: 3380, binary: ['a.xz', 'b.xz'], history: ['git log messages of A..B'], skipped: [{ task: '09', lines: 5497382 }], exempt: { '03': 1 } };
  const m = { scanned: ['03', '09'], absent: [], material: { substrings: new Array(441) } };
  const line = leakLine(clean, m);
  assert.match(line, /^leakcheck clean: 3380 files \(2 binary: line tiers only\)/);
  assert.match(line, /short case lines left to the other tiers: task 09: 5497382/);
  assert.match(line, /collisions with the baseline commit \(not leaks\): task 03: 1/);
  assert.match(line, /substring needles: 441/);
  assert.match(leakLine({ ...clean, exempt: {} }, m), /collisions with the baseline commit \(not leaks\): none/);
  assert.equal(leakLine({ ok: false, reason: 'held-out text found in 1 place(s): x.md:1 (window, task 09, cases/h001.in lines 4-6)' }, m), 'leakcheck FAILED: held-out text found in 1 place(s): x.md:1 (window, task 09, cases/h001.in lines 4-6)');
});

// ---- the command maps an incomplete scan to exit 4 and a finding to exit 5 (coai L1) --------------------------------------------------------------

import { rmSync as rm } from 'node:fs';

test('The_leakcheck_command_exits_4_for_an_incomplete_scan_and_5_for_a_finding', async () => {
  const home = homeWith(['02']);
  writeFileSync(taskPaths(task('02'), home).notesFile, NOTES['02']);
  const run = async files => {
    const repo = tmpDir('o-leak-exit');
    const git = (...args) => execFileSync('git', ['-C', repo, '-c', 'user.name=t', '-c', 'user.email=t@invalid', ...args], { encoding: 'utf8' });
    git('init', '-q');
    for (const [rel, text] of Object.entries(files)) { mkdirSync(path.dirname(path.join(repo, rel)), { recursive: true }); writeFileSync(path.join(repo, rel), text); }
    git('add', '.');
    git('commit', '-q', '-m', 'x');
    const say = [];
    return { repo, say, exit: (await leakcheckCommandIn({ repo, home, say })).exit };
  };
  const clean = await run({ 'a.md': 'public\n' });
  assert.equal(clean.exit, 0);
  const incomplete = await run({ 'a.md': 'public\n', 'b.md': 'tracked\n' });
  rm(path.join(incomplete.repo, 'b.md'));
  const again = await leakcheckCommandIn({ repo: incomplete.repo, home, say: [] });
  assert.equal(again.exit, 4, 'a file listed and gone: the check is incomplete');
  const leak = await run({ 'a.md': 'answer-02-h001-XYZZY\n' });
  assert.equal(leak.exit, 5);
});

/** `leakcheckCommand` over a temp repository (`io.repoRoot`) and a fixture home. */
function leakcheckCommandIn({ repo, home, say }) {
  return leakcheckCommand({ values: { task: '02' }, io: { home, repoRoot: repo, progress: () => {} }, explore: EXPLORE, say: l => say.push(l) });
}

test('The_leakcheck_command_reports_the_material_it_built_in_counts_per_tier_before_it_scans', async () => {
  const home = homeWith(['02']);
  writeFileSync(taskPaths(task('02'), home).notesFile, NOTES['02']);
  const repo = tmpDir('o-leak-progress');
  const git = (...args) => execFileSync('git', ['-C', repo, '-c', 'user.name=t', '-c', 'user.email=t@invalid', ...args], { encoding: 'utf8' });
  git('init', '-q');
  writeFileSync(path.join(repo, 'a.md'), 'public\n');
  git('add', '.');
  git('commit', '-q', '-m', 'x');
  const progress = [];
  await leakcheckCommand({ values: { task: '02' }, io: { home, repoRoot: repo, progress: l => progress.push(l) }, explore: EXPLORE, say: () => {} });
  assert.match(progress[0], /^building the material of tasks 02$/);
  assert.match(progress[1], /^material: substring needles \d+, exact-line keys \d+, window keys \d+, chunk keys \d+, shingles \d+$/);
  assert.match(progress[2], /^scanning \d+ files$/);
  assert.ok(progress.length >= 5, progress.join(' | '));
});
