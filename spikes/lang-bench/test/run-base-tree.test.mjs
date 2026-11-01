// The run base's tree (parent D39): the tree at a ref minus the README's `## Attribution` section and minus any credit file, made from
// `git archive` (never a checkout in a working tree), and the needle scan over it. A run base is what an agent sees.
import assert from 'node:assert/strict';
import { existsSync, lstatSync, mkdirSync, readFileSync, readdirSync, symlinkSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { cutAttribution, generateRunBaseTree, needleScan } from '../lib/run-base-tree.mjs';
import { parseKnownNeedles } from '../lib/push-needles.mjs';
import { commit, makeTaskRepo, ownedFolder } from './support/push-fixtures.mjs';

const known = parseKnownNeedles(JSON.parse(readFileSync(fileURLToPath(new URL('../inputs/needles-lang.json', import.meta.url)), 'utf8'))).value;
const README_HEAD = '# Title\n\nWhat this is.\n\n## Tasks\n\nA list of ten.\n\n## Licence\n\nMIT.\n\n';
const ATTRIBUTION = '## Attribution\n\nAdapted from the CSES Problem Set (https://cses.fi/problemset/task/1068).\nThe plan: dew_flow_benchmark and then PLAN_lang_comparison_v2.md\n';
const README = README_HEAD + ATTRIBUTION;
const out = t => path.join(ownedFolder(t, 'runbase'), 'tree');
const read = (dir, rel) => readFileSync(path.join(dir, rel), 'utf8');

function walk(dir, base = dir) {
  return readdirSync(dir, { withFileTypes: true }).flatMap(e => (e.isDirectory() ? walk(path.join(dir, e.name), base) : [path.relative(base, path.join(dir, e.name))])).sort();
}

test('A_README_copied_whole_would_carry_the_credits_into_every_run_base_and_that_is_the_point', async t => {
  const repo = makeTaskRepo(t, { files: { 'README.md': README, 'tasks/01/s01.in': '5\n' } });
  const dir = out(t);
  const made = await generateRunBaseTree({ repo, ref: 'main', out: dir, needles: known });
  assert.equal(made.ok, true, made.reason);
  assert.equal(read(dir, 'README.md'), README_HEAD, 'every other section is byte for byte the same');
  assert.deepEqual(needleScan({ root: dir, known }).value, []);
  // the refuted approach: a generator that copies the tree whole carries the credit, the link and the pointer into the run base
  const copied = path.join(ownedFolder(t, 'copied'), 'tree');
  mkdirSync(copied);
  writeFileSync(path.join(copied, 'README.md'), README);
  assert.deepEqual(needleScan({ root: copied, known }).value.map(h => [h.path, h.line, h.needle]), [
    ['README.md', 15, 'link'], ['README.md', 15, 'needle CSES'], ['README.md', 15, 'needle cses.fi'], ['README.md', 16, 'needle dew_flow_benchmark'], ['README.md', 16, 'needle PLAN_lang_comparison'],
  ]);
});

test('A_README_without_the_attribution_section_or_with_it_twice_is_refused', async t => {
  const none = makeTaskRepo(t, { files: { 'README.md': README_HEAD } });
  const twice = makeTaskRepo(t, { files: { 'README.md': README + ATTRIBUTION } });
  const closing = makeTaskRepo(t, { files: { 'README.md': `${README}\n## Appendix\n\nafter\n` } });
  const missing = makeTaskRepo(t, { files: { 'a.txt': 'x\n' } });
  assert.match((await generateRunBaseTree({ repo: none, ref: 'main', out: out(t), needles: known })).reason, /no `## Attribution`/);
  assert.match((await generateRunBaseTree({ repo: twice, ref: 'main', out: out(t), needles: known })).reason, /more than once/);
  assert.match((await generateRunBaseTree({ repo: closing, ref: 'main', out: out(t), needles: known })).reason, /closing section/);
  assert.match((await generateRunBaseTree({ repo: missing, ref: 'main', out: out(t), needles: known })).reason, /README\.md/);
});

test('A_credit_planted_under_tasks_runner_or_a_spec_makes_the_needle_scan_fail_naming_file_and_line', async t => {
  const repo = makeTaskRepo(t, { files: { 'README.md': README, 'tasks/01/NOTES.md': 'a\nadapted from CSES\n', 'runner/x.py': '# fine\n# see https://example.org\n', 'todo/tasks/05-x.md': 'x\ny\nthe Redis way\n' } });
  const made = await generateRunBaseTree({ repo, ref: 'main', out: out(t), needles: known });
  assert.equal(made.ok, false);
  assert.deepEqual(made.hits.map(h => [h.path, h.line, h.needle]), [['runner/x.py', 2, 'link'], ['tasks/01/NOTES.md', 2, 'needle CSES'], ['todo/tasks/05-x.md', 3, 'needle Redis']]);
  assert.match(made.reason, /runner\/x\.py:2/);
  assert.equal(JSON.stringify(made).includes('adapted from'), false, 'never the text');
});

test('The_needle_scan_still_finds_a_planted_needle_in_every_file_kind_and_ids_in_the_agent_visible_folders', t => {
  const root = path.join(ownedFolder(t, 'scan'), 'tree');
  mkdirSync(path.join(root, 'tasks', 'x'), { recursive: true });
  mkdirSync(path.join(root, 'research'), { recursive: true });
  writeFileSync(path.join(root, 'tasks', 'x', 'a.md'), 'Story E1.S4 and T-L31 and G-12 and D41.\n');
  writeFileSync(path.join(root, 'research', 'b.md'), 'E1.S4 here is not an agent-visible id, but Werewolf is.\n');
  const hits = needleScan({ root, known }).value.map(h => [h.path, h.line, h.needle]);
  assert.deepEqual(hits, [['research/b.md', 1, 'needle Werewolf'], ['tasks/x/a.md', 1, 'plan story id'], ['tasks/x/a.md', 1, 'probe id'], ['tasks/x/a.md', 1, 'gate id'], ['tasks/x/a.md', 1, 'decision id']]);
  assert.deepEqual(needleScan({ root, known, literals: ['frob'] }).ok, true);
});

test('The_needle_scan_reads_extra_literals_by_word_boundary_and_a_symlink_or_binary_is_a_hit_not_a_skip', t => {
  const root = path.join(ownedFolder(t, 'scan'), 'tree');
  mkdirSync(root, { recursive: true });
  writeFileSync(path.join(root, 'a.md'), 'the Quixotic word, and Quix alone\n');
  writeFileSync(path.join(root, 'bin.dat'), Buffer.from([0, 1, 2]));
  symlinkSync('/etc/hostname', path.join(root, 'link'));
  const hits = needleScan({ root, known, literals: ['Quix'] }).value.map(h => [h.path, h.line, h.needle]);
  assert.deepEqual(hits, [['a.md', 1, 'needle Quix'], ['bin.dat', null, 'binary file (NUL byte)'], ['link', null, 'symlink']]);
});

test('The_cut_removes_a_credit_file_and_keeps_everything_else_byte_for_byte', async t => {
  const repo = makeTaskRepo(t, { files: { 'README.md': README, 'CREDITS.md': 'credit\n', 'tasks/credits.json': '{}\n', 'tasks/01/s01.in': '5\n', 'runner/run.py': 'pass\n' } });
  const dir = out(t);
  const made = await generateRunBaseTree({ repo, ref: 'main', out: dir, needles: known });
  assert.equal(made.ok, true, made.reason);
  assert.deepEqual(made.value.removed, ['CREDITS.md', 'tasks/credits.json']);
  assert.deepEqual(walk(dir), ['README.md', 'runner/run.py', 'tasks/01/s01.in']);
  assert.equal(read(dir, 'runner/run.py'), 'pass\n');
});

test('The_tree_is_made_from_the_ref_and_not_from_a_working_tree_and_a_dirty_folder_is_refused', async t => {
  const repo = makeTaskRepo(t, { files: { 'README.md': README, 'a.txt': 'committed\n' } });
  writeFileSync(path.join(repo, 'a.txt'), 'edited, not committed\n');
  writeFileSync(path.join(repo, 'untracked.txt'), 'u\n');
  const dir = out(t);
  assert.equal((await generateRunBaseTree({ repo, ref: 'main', out: dir, needles: known })).ok, true);
  assert.equal(read(dir, 'a.txt'), 'committed\n');
  assert.equal(existsSync(path.join(dir, 'untracked.txt')), false);
  const again = await generateRunBaseTree({ repo, ref: 'main', out: dir, needles: known });
  assert.equal(again.ok, false, 'an output folder that already holds a tree is refused, never merged into');
  assert.match(again.reason, /not empty|exists/);
  assert.equal((await generateRunBaseTree({ repo, ref: 'no-such-ref', out: out(t), needles: known })).ok, false);
});

test('A_symlink_in_the_archived_tree_is_refused_by_the_generator', async t => {
  const repo = makeTaskRepo(t, { files: { 'README.md': README } });
  symlinkSync('/etc/hostname', path.join(repo, 'link'));
  commit(repo, 'feat: add a link', {});
  const made = await generateRunBaseTree({ repo, ref: 'main', out: out(t), needles: known });
  assert.equal(made.ok, false);
  assert.ok(made.hits.some(h => h.path === 'link' && h.needle === 'symlink'));
  assert.equal(lstatSync(path.join(repo, 'link')).isSymbolicLink(), true);
});

test('The_cut_function_keeps_the_text_before_the_heading_exactly', () => {
  assert.equal(cutAttribution(README).value, README_HEAD);
  assert.equal(cutAttribution('# T\n\n## Attribution').value, '# T\n\n');
  assert.equal(cutAttribution('## Attribution\nx\n').value, '');
});
