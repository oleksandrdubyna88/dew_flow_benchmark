// A candidate publication commit (build step 2b; F2.S2's freeze commits): `--candidate-repo <path> --candidate <commit>` scans ONE commit that is about to be pushed: its tree, its message and its diff against its parent (against
// the empty tree when it has none), in either repository. With `--candidate` alone nothing else is scanned. An unknown commit is a refusal. INVENTED needle, temp repositories, temp private side.
import '../../minigit-bench/test/fake-home.mjs';
import assert from 'node:assert/strict';
import { writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { candidateTexts } from '../lib/leak-candidate.mjs';
import { NEEDLE, commit, git, holdsNeedle, newRepo, privateSide, runCheck } from './quick-leak-repos-fixture.mjs';

const withRepo = async (repo, sha, extra = {}) => runCheck({ ...privateSide(), benchRepo: newRepo(), values: { 'candidate-repo': repo, candidate: sha, ...extra } });
const head = repo => git(repo, 'rev-parse', 'HEAD').trim();

test('A_candidate_commit_with_a_needle_in_a_file_it_adds_fails_naming_the_candidate_and_the_clean_one_passes', async () => {
  const repo = newRepo();
  const dirty = commit(repo, { 'tasks/04/spec.md': `a spec\n${NEEDLE}` }, 'publish the spec');
  const r = await withRepo(repo, dirty);
  assert.equal(r.exit, 5, r.said);
  assert.match(r.said, /candidate/);
  assert.match(r.said, /tasks\/04\/spec\.md/);
  assert.equal(holdsNeedle(r.said + r.progress), false);
  const fine = newRepo({ 'README.md': 'a readme\n', 'tasks/04/spec.md': 'a spec\n' });
  assert.equal((await withRepo(fine, head(fine))).exit, 0, 'the control: the same commit without the needle');
});

test('A_candidate_is_scanned_by_its_tree_its_message_and_its_diff', async () => {
  const inTree = newRepo({ 'old.md': NEEDLE, 'README.md': 'a readme\n' });
  const unrelated = commit(inTree, { 'other.md': 'unrelated\n' }, 'an unrelated change');
  const tree = await withRepo(inTree, unrelated);
  assert.equal(tree.exit, 5, 'the needle is in the candidate tree and not in its diff: the tree is read');
  const message = newRepo();
  const messaged = commit(message, { 'other.md': 'unrelated\n' }, `a message with ${NEEDLE.split('\n')[0]}`);
  const m = await withRepo(message, messaged);
  assert.equal(m.exit, 5, 'the needle is in the commit message only');
  assert.match(m.said, /commit message/);
  const removal = newRepo({ 'old.md': NEEDLE, 'README.md': 'a readme\n' });
  const removed = commit(removal, { 'old.md': null }, 'remove the file');
  const d = await withRepo(removal, removed);
  assert.equal(d.exit, 5, 'the needle is only in the removed lines of the diff against the parent');
  assert.match(d.said, /diff of/);
  for (const r of [tree, m, d]) assert.equal(holdsNeedle(r.said), false);
});

test('A_candidate_with_no_parent_is_diffed_against_the_empty_tree_so_its_whole_content_is_scanned', async () => {
  const repo = newRepo({ 'README.md': 'a readme\n', 'tasks/04/spec.md': `a spec\n${NEEDLE}` });
  const texts = await candidateTexts({ repo, sha: head(repo) });
  assert.equal(texts.ok, true, texts.reason);
  const diff = texts.value.texts.find(t => t.diff);
  assert.match(diff.name, /against the empty tree/);
  assert.ok(diff.raw.includes(`+${NEEDLE.split('\n')[0]}`), 'every line of the root commit is an added line of its diff');
  const r = await withRepo(repo, head(repo));
  assert.equal(r.exit, 5, r.said);
  const fine = newRepo();
  assert.equal((await withRepo(fine, head(fine))).exit, 0, 'a clean root commit passes');
});

test('The_candidate_options_come_in_a_pair_and_an_unknown_or_unsafe_commit_is_a_refusal', async () => {
  const repo = newRepo();
  const sha = head(repo);
  const refused = [['--candidate alone', { candidate: sha }], ['--candidate-repo alone', { 'candidate-repo': repo }], ['an unknown commit', { 'candidate-repo': repo, candidate: 'no-such-commit' }], ['an option, not a commit', { 'candidate-repo': repo, candidate: '--all' }],
    ['a tree, not a commit', { 'candidate-repo': repo, candidate: git(repo, 'rev-parse', 'HEAD^{tree}').trim() }], ['a candidate repo that is not a repository', { 'candidate-repo': path.join(repo, 'nope'), candidate: sha }]];
  for (const [name, values] of refused) {
    const r = await runCheck({ ...privateSide(), benchRepo: newRepo(), values });
    assert.equal(r.exit, 4, `${name}: ${r.said}`);
    assert.doesNotMatch(r.said, /clean/, name);
  }
  assert.equal((await withRepo(repo, sha)).exit, 0, 'the control: a known commit of a clean repository');
  assert.equal((await withRepo(repo, 'HEAD')).exit, 0, 'a name that resolves to a commit is read as that commit');
});

test('With_a_candidate_alone_only_that_commit_is_scanned_and_with_a_task_repository_the_working_tree_is_scanned_too', async () => {
  const repo = newRepo();
  const sha = head(repo);
  commit(repo, { 'later.md': NEEDLE }, 'a later leak that is not the candidate');
  writeFileSync(path.join(repo, 'draft.md'), NEEDLE);
  const alone = await withRepo(repo, sha);
  assert.equal(alone.exit, 0, `an older clean commit is the candidate: ${alone.said}`);
  const both = await runCheck({ ...privateSide(), benchRepo: newRepo(), values: { 'task-repo': repo, 'candidate-repo': repo, candidate: sha } });
  assert.equal(both.exit, 5, both.said);
  assert.match(both.said, /working tree|FAILED/);
});

test('The_options_of_the_whole_scan_are_refused_with_a_candidate_alone', async () => {
  const repo = newRepo();
  for (const extra of [{ 'task-base': 'main' }, { range: 'a..b' }]) {
    const r = await withRepo(repo, head(repo), extra);
    assert.equal(r.exit, 4, r.said);
  }
});

test('A_candidate_of_the_bench_repository_is_named_the_bench_repository', async () => {
  const bench = newRepo();
  const sha = commit(bench, { 'docs/x.md': NEEDLE }, 'a bench commit');
  const r = await runCheck({ ...privateSide(), benchRepo: bench, values: { 'candidate-repo': bench, candidate: sha } });
  assert.equal(r.exit, 5, r.said);
  assert.match(r.said, /bench repository/);
});

test('A_candidate_of_the_task_repository_is_named_the_task_repository_when_the_option_or_the_variable_names_it', async () => {
  const repo = newRepo();
  const sha = commit(repo, { 'tasks/04/spec.md': NEEDLE }, 'publish');
  const viaEnv = await runCheck({ ...privateSide(), benchRepo: newRepo(), values: { 'candidate-repo': repo, candidate: sha }, env: { LANG_V2_TASK_REPO: repo } });
  assert.equal(viaEnv.exit, 5, viaEnv.said);
  assert.match(viaEnv.said, /\[candidate [0-9a-f]{12} of task repository\] FAILED/);
  const other = await runCheck({ ...privateSide(), benchRepo: newRepo(), values: { 'candidate-repo': repo, candidate: sha } });
  assert.match(other.said, /of repository /, 'with nothing naming it the report names the path');
});
