// The leak scan over a temp git repository with the quick tasks' private side (invented content, a temp home): a held-out value or a reference line in a public file is a finding that names task, file and line and never the
// text; FORMAT grammar that the committed public examples also hold is not; the command reads the public examples from the repository's own committed files. These tests make git repositories (they start `git`).
import '../../minigit-bench/test/fake-home.mjs';
import assert from 'node:assert/strict';
import { test } from 'node:test';
import { leakMaterial, leakcheckCommand } from '../lib/leak.mjs';
import { scanRepository } from '../lib/leak-scan.mjs';
import { normLine } from '../lib/leak-text.mjs';
import { commitFiles, git, repoWith } from './leak-scan-fixture.mjs';
import { EXPLORE, presentMaterial, quickHome, quickTask, writeQuickHeldout } from './quick-leak-fixture.mjs';

const VALUE = 'recv c "invented-answer-for-the-scan-XYZZY-918273"';
const REF = 'def invented_reference_for_the_scan(state, event): return fold(state, event, 918273)';
const CASES = [{ id: 'h001', input: 'scenario 2\ndefault within 2s\nconnect c tcp\n', output: `transcript 2\nready\n${VALUE}\nexit 0\n` }];
const GRAMMAR_FILE = 'spikes/lang-bench/test/hello/samples/daemon-pass/s01.in';

function homeWithTask04() {
  const home = quickHome();
  writeQuickHeldout({ home, task: quickTask('04'), cases: CASES, traps: ['h001'], refs: { 'ref.py': `${REF}\n` } });
  return home;
}

const materialFor = (home, grammarLines = new Set()) => {
  const m = presentMaterial({ home, publicText: '', grammarLines });
  assert.equal(m.ok, true, m.reason);
  return m.value.material;
};

test('A_held_out_answer_line_in_a_public_file_is_a_finding_naming_task_case_file_and_line_not_the_text', async () => {
  const material = materialFor(homeWithTask04());
  assert.equal((await scanRepository({ repoRoot: repoWith(), material })).ok, true);
  const r = await scanRepository({ repoRoot: repoWith({ 'notes/run.md': `the program printed\n${VALUE}\non our laptop\n` }), material });
  assert.equal(r.ok, false);
  assert.deepEqual(r.findings.map(f => [f.file, f.line, f.task, f.caseFile]), [['notes/run.md', 2, '04', 'cases/h001.out']]);
  assert.doesNotMatch(r.reason, /XYZZY|918273/);
});

test('A_reference_solution_line_in_a_public_file_is_a_finding_that_names_the_refs_file', async () => {
  const material = materialFor(homeWithTask04());
  const r = await scanRepository({ repoRoot: repoWith({ 'src/copy.py': `x = 1\n${REF}\n` }), material });
  assert.equal(r.ok, false);
  assert.deepEqual(r.findings.map(f => [f.file, f.line, f.task, f.caseFile, f.fromLine]), [['src/copy.py', 2, '04', 'refs/ref.py', 1]]);
});

test('FORMAT_grammar_in_a_public_example_is_not_a_finding_once_the_rule_holds_it_and_is_one_without_it', async () => {
  const home = homeWithTask04();
  const repo = repoWith({ [GRAMMAR_FILE]: 'scenario 2\ndefault within 2s\nconnect c tcp\n' });
  assert.equal((await scanRepository({ repoRoot: repo, material: materialFor(home) })).ok, false, 'without the rule a grammar line of the held-out set is a hit in a public file');
  const grammar = new Set(['scenario 2', 'default within 2s', 'connect c tcp'].map(normLine));
  const r = await scanRepository({ repoRoot: repo, material: materialFor(home, grammar) });
  assert.equal(r.ok, true, r.reason);
});

test('The_command_reads_the_public_examples_from_the_committed_files_of_the_repository_and_exits_clean', async () => {
  const home = homeWithTask04();
  const repoRoot = repoWith({ [GRAMMAR_FILE]: 'scenario 2\ndefault within 2s\nconnect c tcp\n', 'README.md': 'a public readme\n' });
  git(repoRoot, 'update-ref', 'refs/remotes/origin/main', 'HEAD'); // what main already holds
  const said = [];
  const run = () => leakcheckCommand({ values: { task: '04' }, io: { home, repoRoot, progress: () => {} }, explore: EXPLORE, say: l => said.push(l) });
  assert.equal((await run()).exit, 0, said.join('\n'));
  commitFiles(repoRoot, { 'docs/oops.md': `pasted by mistake:\n${VALUE}\n` }, 'a leak');
  assert.equal((await run()).exit, 5, 'a held-out value in a public file fails the command');
  assert.doesNotMatch(said.join('\n'), /XYZZY|918273/, 'the output never holds the text');
});

test('A_held_out_line_committed_under_test_hello_on_the_branch_is_still_found_because_the_exemption_is_what_main_holds', async () => {
  const home = homeWithTask04();
  const repoRoot = repoWith({ [GRAMMAR_FILE]: 'scenario 2\ndefault within 2s\nconnect c tcp\n' });
  git(repoRoot, 'update-ref', 'refs/remotes/origin/main', 'HEAD');
  commitFiles(repoRoot, { [GRAMMAR_FILE]: `scenario 2\ndefault within 2s\nconnect c tcp\n${VALUE}\n` }, 'a held-out line added to a public example on the branch');
  const said = [];
  const r = await leakcheckCommand({ values: { task: '04' }, io: { home, repoRoot, progress: () => {} }, explore: EXPLORE, say: l => said.push(l) });
  assert.equal(r.exit, 5, said.join('\n'));
  assert.doesNotMatch(said.join('\n'), /XYZZY|918273/);
});

test('A_reference_line_committed_under_the_runner_directory_on_the_branch_is_still_found_because_a_quick_task_reads_the_exemption_from_main_alone', async () => {
  const home = homeWithTask04();
  const repoRoot = repoWith({ 'spikes/lang-explore/runner/keep.py': 'import os\n' });
  git(repoRoot, 'update-ref', 'refs/remotes/origin/main', 'HEAD'); // main holds the runner without the reference line
  commitFiles(repoRoot, { 'spikes/lang-explore/runner/keep.py': `import os\n${REF}\n` }, 'a reference line added to the runner on the branch');
  const said = [];
  const r = await leakcheckCommand({ values: { task: '04' }, io: { home, repoRoot, progress: () => {} }, explore: EXPLORE, say: l => said.push(l) });
  assert.equal(r.exit, 5, said.join('\n'));
  assert.doesNotMatch(said.join('\n'), /918273/, 'the output never holds the text');
});
