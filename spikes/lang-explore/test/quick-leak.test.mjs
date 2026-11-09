// The leak check reads the quick tasks' private side: `heldout-NN/` (cases, traps, the generator) and `refs-NN/` (the reference solutions) for 04, 05, 06, 07, 08 and 10, as it reads the olympiad tasks'. FORMAT grammar a held-out set
// shares with the public examples is not content; a rule makes it a collision, never a list of lines. The private side is a fixture of INVENTED content in a temp home; nothing real is read, and no git repository is made here
// (the scan over a repository is `quick-leak-scan.test.mjs`).
import '../../minigit-bench/test/fake-home.mjs';
import assert from 'node:assert/strict';
import { mkdirSync, rmSync, symlinkSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { leakMaterial } from '../lib/leak.mjs';
import { scanBenchHalf } from './bench-half-seam.mjs';
import { refsDirOf } from '../lib/heldout-quick.mjs';
import { normLine } from '../lib/leak-text.mjs';
import { resolveTask, taskPaths } from '../lib/tasks.mjs';
import { writeHeldout } from './olympiad-fixture.mjs';
import { tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { EXPLORE, presentMaterial, quickHome, quickTask, writeQuickHeldout } from './quick-leak-fixture.mjs';

const QUICK = ['04', '05', '06', '07', '08', '10'];
const marked = id => [{ id: 'h001', input: `scenario 2\ndefault within 2s\nconnect c tcp\nsend c "invented-${id}-input-QWERTY"\n`, output: `transcript 2\nready\nrecv c "invented-${id}-answer-XYZZY"\nexit 0\n` }, { id: 'h002', input: `scenario 2\n`, output: `transcript 2\n` }];
const refLine = id => `def invented_reference_${id}(state, event): return fold_the_events(state, event, ${id})`;
const refs = id => ({ 'ref.py': `import os\n${refLine(id)}\n`, 'ws/src/Program.cs': `// invented ${id}\nvar sumOfInventedThings${id} = Things.Fold(events, ${id}, seedValue);\n`, 'ws/nested/deeper/main.ts': `const invented${id} = foldEvents(events, ${id}, seed);\n`, '__pycache__/ref.cpython-314.pyc': Buffer.from('\0\0binary refs line that must not be read as text at all, ever\n') });

/** A home with the private side of every quick task in `ids`. */
function homeWith(ids, over = {}) {
  const home = quickHome();
  for (const id of ids) writeQuickHeldout({ home, task: quickTask(id), cases: marked(id), traps: ['h002'], refs: refs(id), ...over });
  return home;
}

const substringsOf = (m, id) => m.material.substrings.filter(n => n.task === id);

test('The_needles_of_every_quick_task_come_from_its_cases_its_generator_and_its_reference_solutions', () => {
  const m = presentMaterial({ home: homeWith(QUICK), publicText: '' });
  assert.equal(m.ok, true, m.reason);
  assert.deepEqual(m.value.scanned.sort(), QUICK);
  for (const id of QUICK) {
    const texts = substringsOf(m.value, id).map(n => n.text);
    assert.ok(texts.includes(`recv c "invented-${id}-answer-xyzzy"`), `${id}: a case line, normalised`);
    assert.ok(texts.includes(normLine(refLine(id))), `${id}: a line of refs-${id}/ref.py`);
    assert.ok(texts.some(t => t.startsWith(`var sumofinventedthings${id}`)), `${id}: a nested reference file of another language`);
    assert.ok(texts.some(t => t.startsWith(`const invented${id} `)), `${id}: a deeply nested reference file`);
    assert.deepEqual(texts.filter(t => /binary refs line/.test(t)), [], `${id}: build output and binary files are not read`);
    assert.deepEqual(texts.filter(t => QUICK.some(other => other !== id && t.includes(`invented-${other}-`))), [], `${id}: nothing of another task is its needle`);
  }
});

test('A_reference_line_is_named_by_its_refs_path_and_line_never_by_its_text', () => {
  const m = presentMaterial({ home: homeWith(['04']), publicText: '' }).value;
  const needle = substringsOf(m, '04').find(n => n.text === normLine(refLine('04')));
  assert.deepEqual([needle.source, needle.line, needle.kind], ['refs/ref.py', 2, 'code']);
});

test('The_trap_ids_of_a_quick_task_are_ids_to_keep_out_of_the_public_notes', () => {
  const m = presentMaterial({ home: homeWith(['06']), publicText: '' });
  assert.deepEqual(m.value.ids, ['h002']);
});

// Changed by step 2b (the plan: a task whose folder is absent is never skipped while another task's folder is present). The title is the one the test always had; what it pins now is the refusal.
test('A_quick_task_whose_folders_are_absent_is_named_and_skipped_like_an_olympiad_task', () => {
  const r = leakMaterial({ home: homeWith(['04', '05']), explore: EXPLORE, publicText: '' });
  assert.equal(r.ok, false, 'the absent tasks are named and the scan is refused, never run over the two that are there');
  assert.match(r.reason, /task 06, 07, 08, 10 .*not present|tasks? .*06.*not present/);
  const named = presentMaterial({ home: homeWith(['04', '05']), publicText: '' }).value;
  assert.deepEqual([named.scanned.sort(), named.absent], [['04', '05'], []], 'with the pins that name the two, both are read and none is absent');
});

test('A_quick_task_with_a_stray_file_in_its_cases_folder_fails_the_check_instead_of_reading_something_else', () => {
  const home = homeWith(['05']);
  writeFileSync(path.join(taskPaths(quickTask('05'), home).heldoutDir, 'cases', 'notes.txt'), 'stray');
  const r = presentMaterial({ home, publicText: '' });
  assert.equal(r.ok, false);
  assert.match(r.reason, /task 05: .*malformed.*not a pair or cases\.txt: notes\.txt/);
});

test('A_quick_task_whose_private_notes_are_missing_fails_the_check_like_an_olympiad_task', () => {
  const home = quickHome();
  writeQuickHeldout({ home, task: quickTask('07'), cases: marked('07') });
  rmSync(taskPaths(quickTask('07'), home).notesFile);
  const r = presentMaterial({ home, publicText: '' });
  assert.equal(r.ok, false);
  assert.match(r.reason, /the private notes of task 07 are missing/);
});

// ---- FORMAT grammar a held-out set shares with the public examples ------------------------------------------------------------------------

const GRAMMAR = new Set(['scenario 2', 'default within 2s', 'connect c tcp', 'transcript 2', 'ready', 'exit 0', 'import os'].map(normLine));

test('A_held_out_line_that_the_public_examples_also_hold_is_grammar_and_no_needle_but_a_line_with_a_value_still_is', () => {
  const m = presentMaterial({ home: homeWith(['04']), publicText: '', grammarLines: GRAMMAR }).value;
  const texts = substringsOf(m, '04').map(n => n.text);
  assert.deepEqual(texts.filter(t => GRAMMAR.has(t)), [], 'grammar lines are not needles');
  assert.ok(texts.includes('connect c tcp') === false);
  assert.ok(texts.includes('default within 2s') === false);
  assert.ok(texts.includes('recv c "invented-04-answer-xyzzy"'), 'a line with a held-out value is still a needle');
  assert.ok(texts.some(t => t.startsWith('send c "invented-04-input-qwerty"')), 'and an input line with a value too');
});

test('Without_the_rule_the_same_grammar_lines_are_needles_so_the_rule_is_what_removes_them', () => {
  const m = presentMaterial({ home: homeWith(['04']), publicText: '' }).value;
  assert.ok(substringsOf(m, '04').map(n => n.text).includes('default within 2s'));
});

test('The_grammar_rule_is_for_the_quick_tasks_only_an_olympiad_task_keeps_every_case_line', () => {
  const home = quickHome();
  const olympiad = EXPLORE.tasks['02'];
  assert.ok(olympiad.quick !== true);
  writeHeldout({ home, task: resolveTask(EXPLORE, '02').value, cases: [{ id: 'h001', input: 'short\n', output: 'answer-02-h001-XYZZY\nsecond line of it\n' }], traps: ['h001'] });
  const notes = taskPaths(resolveTask(EXPLORE, '02').value, home).notesFile;
  mkdirSync(path.dirname(notes), { recursive: true });
  writeFileSync(notes, 'invented notes');
  const m = presentMaterial({ home, publicText: '', grammarLines: new Set(['answer-02-h001-xyzzy', 'second line of it']) }).value;
  assert.ok(m.material.substrings.some(n => n.text === 'answer-02-h001-xyzzy'), 'the grammar set does not touch an olympiad task');
});

test('The_refs_folder_of_a_task_sits_beside_its_held_out_folder_with_the_same_number', () => {
  assert.equal(refsDirOf('/h/rsd/lang-v2/explore/heldout-07'), '/h/rsd/lang-v2/explore/refs-07');
});

// ---- the exemption is built from what main already holds, never from the branch under scan --------------------------------------------------

import { PUBLIC_DIRS, PUBLIC_REF, committedLinesIn, publicLines } from '../lib/leak.mjs';

/** A fake `git`: `trees` is `{ ref: { file: text } }`; every call is recorded. */
function fakeGit(trees) {
  const calls = [];
  const run = async (exe, args) => {
    calls.push(args);
    const ref = args[0] === 'ls-tree' ? args[args.indexOf('--name-only') + 1] : args[1].split(':')[0];
    const files = trees[ref];
    if (files === undefined) return { ok: true, value: { code: 128, stdout: '', stderr: 'unknown ref' } };
    if (args[0] === 'ls-tree') return { ok: true, value: { code: 0, stdout: Object.keys(files).join('\n'), stderr: '' } };
    return { ok: true, value: { code: 0, stdout: files[args[1].split(':')[1]], stderr: '' } };
  };
  return { run, calls };
}

test('The_public_lines_are_read_at_origin_main_by_default_so_a_line_committed_only_on_the_branch_is_not_exempt', async () => {
  const git = fakeGit({
    'origin/main': { 'spikes/lang-bench/test/hello/samples/a.in': 'default within 2s\nconnect c tcp\n' },
    HEAD: { 'spikes/lang-bench/test/hello/samples/a.in': 'default within 2s\nconnect c tcp\n', 'spikes/lang-bench/test/hello/samples/leak.in': 'recv c "a-held-out-value-committed-on-the-branch"\n' },
  });
  const onMain = await committedLinesIn({ ref: PUBLIC_REF, dirs: PUBLIC_DIRS, run: git.run });
  assert.deepEqual([...onMain].sort(), ['connect c tcp', 'default within 2s']);
  assert.equal(PUBLIC_REF, 'origin/main');
  const onHead = await committedLinesIn({ ref: 'HEAD', dirs: PUBLIC_DIRS, run: git.run });
  assert.ok(onHead.has(normLine('recv c "a-held-out-value-committed-on-the-branch"')), 'read at HEAD the branch would exempt its own line');
});

test('A_ref_that_cannot_be_read_gives_no_exemption_the_stricter_check', async () => {
  const git = fakeGit({});
  assert.equal((await committedLinesIn({ ref: 'origin/main', dirs: PUBLIC_DIRS, run: git.run })).size, 0);
});

test('The_command_builds_its_grammar_set_from_origin_main_unless_told_otherwise', async () => {
  const git = fakeGit({ 'origin/main': { 'spikes/lang-bench/runner/x.py': 'import os\n' } });
  const lines = await publicLines({ repoRoot: '/r', io: { gitRun: git.run } });
  assert.deepEqual([...lines], ['import os']);
  assert.ok(git.calls.every(a => (a[0] === 'ls-tree' ? a[a.indexOf('--name-only') + 1] : a[1].split(':')[0]) === 'origin/main'));
  const other = fakeGit({ 'refs/x': { 'spikes/lang-bench/runner/x.py': 'from y import z\n' } });
  assert.deepEqual([...await publicLines({ repoRoot: '/r', io: { gitRun: other.run, publicRef: 'refs/x' } })], ['from y import z']);
});

test('A_window_of_three_grammar_lines_is_grammar_too_and_a_window_with_a_held_out_value_in_it_is_still_a_key', () => {
  const grammar = new Set(['scenario 2', 'default within 2s', 'connect c tcp'].map(normLine));
  const withValueR = presentMaterial({ home: homeWith(['04'], { traps: [], cases: [{ id: 'h001', input: 'scenario 2\ndefault within 2s\nconnect c tcp\n', output: 'transcript 2\nrecv c "first-invented-value-AAAA"\nrecv c "second-invented-value-BBBB"\n' }] }), publicText: '', grammarLines: grammar });
  assert.equal(withValueR.ok, true, withValueR.reason);
  const withValue = withValueR.value.material;
  const without = presentMaterial({ home: homeWith(['04'], { traps: [], cases: [{ id: 'h001', input: 'scenario 2\ndefault within 2s\nconnect c tcp\n', output: 'transcript 2\nready\nexit 0\n' }] }), publicText: '' }).value.material;
  assert.ok(without.windowKeys.size >= 1, 'without the rule the three grammar lines of the input are a window');
  const onlyGrammar = presentMaterial({ home: homeWith(['04'], { traps: [], cases: [{ id: 'h001', input: 'scenario 2\ndefault within 2s\nconnect c tcp\n', output: 'transcript 2\n' }] }), publicText: '', grammarLines: grammar }).value.material;
  assert.equal(onlyGrammar.windowKeys.size, 0, 'with it, a window of grammar lines only is no key');
  assert.ok(withValue.windowKeys.size >= 1, 'a window that holds a value line is');
});

// ---- a link under a private folder is named, never skipped --------------------------------------------------------------------------------

/** A home whose task 05 holds a link in `refs-05/` and one in `mutants/`, both to an outside file with invented text. */
function homeWithLinks() {
  const home = homeWith(['05']);
  const paths = taskPaths(quickTask('05'), home);
  const outside = path.join(home, 'outside.py');
  writeFileSync(outside, 'def invented_outside_reference(state): return fold_the_invented_events(state)\n');
  mkdirSync(path.join(paths.heldoutDir, 'mutants'), { recursive: true });
  symlinkSync(outside, path.join(refsDirOf(paths.heldoutDir), 'ws', 'linked.py'));
  symlinkSync(outside, path.join(paths.heldoutDir, 'mutants', 'M-01.py'));
  return home;
}

test('A_linked_file_under_a_private_folder_is_named_by_the_leak_check_and_never_skipped', async () => {
  const home = homeWithLinks();
  const r = presentMaterial({ home, publicText: '' });
  assert.equal(r.ok, false, 'a link is not skipped in silence');
  assert.match(r.reason, /task 05/);
  assert.match(r.reason, /refs\/ws\/linked\.py/);
  assert.match(r.reason, /mutants\/M-01\.py/);
  const said = [];
  const ran = await scanBenchHalf({ values: { task: '05' }, io: { home, repoRoot: tmpDir('q-leak-link'), progress: () => {} }, explore: EXPLORE, say: l => said.push(l) });
  assert.notEqual(ran.exit, 0, 'the command does not report clean');
  assert.match(ran.message, /refs\/ws\/linked\.py/, 'the path is reported');
});

test('A_private_folder_without_a_link_is_read_as_before_and_that_is_the_point', () => {
  const r = presentMaterial({ home: homeWith(['05']), publicText: '' });
  assert.equal(r.ok, true, r.reason);
});
