// The texts that exempt shingles are public by definition, so they are read from what is PUBLISHED - `origin/main` of the bench
// repository - and never from the working tree, which the branch under check can edit. Unreadable means the check does not run.
import assert from 'node:assert/strict';
import { writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { aidNeedles, findAidHits } from '../lib/push-needles.mjs';
import { readPublicPlans } from '../lib/push-public.mjs';
import { commit, git, makeOrigin, makeTaskRepo } from './support/push-fixtures.mjs';

const PATHS = ['plans/one.md', 'plans/two.md'];

/** A bench-like repository whose origin/main holds both plans; returns its path. */
function benchRepo(t) {
  const origin = makeOrigin(t);
  const repo = makeTaskRepo(t, { origin, files: { 'plans/one.md': 'published one\n', 'plans/two.md': 'published two\n' } });
  git(repo, 'push', '-q', 'origin', 'main:main', '--force'); // the local fixture remote only
  git(repo, 'fetch', '-q', 'origin');
  return repo;
}

test('The_public_texts_come_from_origin_main_and_never_from_the_working_tree', async t => {
  const repo = benchRepo(t);
  writeFileSync(path.join(repo, 'plans/one.md'), 'an aid sentence pasted into the working tree\n');
  commit(repo, 'docs: a branch edits the plan to exempt its own leak', { 'plans/two.md': 'and committed here too\n' });
  const texts = await readPublicPlans({ repo, ref: 'origin/main', paths: PATHS });
  assert.equal(texts.ok, true, texts.reason);
  assert.deepEqual(texts.value, ['published one\n', 'published two\n']);
});

test('A_public_text_that_cannot_be_read_from_the_published_ref_fails_closed', async t => {
  const repo = benchRepo(t);
  const missingFile = await readPublicPlans({ repo, ref: 'origin/main', paths: [...PATHS, 'plans/absent.md'] });
  assert.equal(missingFile.ok, false);
  assert.equal(missingFile.kind, 'env');
  assert.match(missingFile.reason, /plans\/absent\.md/);
  const missingRef = await readPublicPlans({ repo, ref: 'origin/nowhere', paths: PATHS });
  assert.equal(missingRef.ok, false);
  assert.equal(missingRef.kind, 'env');
});

const LIST = 'lists/public-texts.json';
const listed = texts => JSON.stringify({ schema: 1, texts });

/** A repository whose origin/main holds the two plans, a list file naming `names`, and every file of `files`. */
function benchWith(t, names, files = {}) {
  const origin = makeOrigin(t);
  const repo = makeTaskRepo(t, { origin, files: { 'plans/one.md': 'published one\n', 'plans/two.md': 'published two\n', [LIST]: listed(names), ...files } });
  git(repo, 'push', '-q', 'origin', 'main:main', '--force');
  git(repo, 'fetch', '-q', 'origin');
  return repo;
}

test('The_public_texts_are_the_two_plans_and_the_files_the_reviewed_list_names_and_no_other_markdown', async t => {
  const repo = benchWith(t, ['notes/listed.md'], { 'notes/listed.md': 'a listed note\n', 'notes/unlisted.md': 'an unlisted note\n', 'research/also-unlisted.md': 'another\n' });
  const texts = await readPublicPlans({ repo, ref: 'origin/main', paths: PATHS, listPath: LIST });
  assert.equal(texts.ok, true, texts.reason);
  assert.deepEqual(texts.value, ['published one\n', 'published two\n', 'a listed note\n']);
});

test('A_list_file_that_is_absent_at_the_ref_means_no_extra_texts_but_a_listed_file_that_is_absent_fails_closed', async t => {
  const repo = benchWith(t, []);
  const none = await readPublicPlans({ repo, ref: 'origin/main', paths: PATHS, listPath: 'lists/not-there.json' });
  assert.deepEqual(none.value, ['published one\n', 'published two\n']);
  const bad = benchWith(t, ['notes/ghost.md']);
  const failed = await readPublicPlans({ repo: bad, ref: 'origin/main', paths: PATHS, listPath: LIST });
  assert.equal(failed.ok, false);
  assert.equal(failed.kind, 'env');
  assert.match(failed.reason, /notes\/ghost\.md/);
});

test('A_list_file_that_is_not_valid_is_a_fail_closed_and_a_listed_path_may_not_climb_out_or_name_a_non_text', async t => {
  for (const content of ['{', JSON.stringify({ schema: 2, texts: [] }), JSON.stringify({ schema: 1, texts: 'x' }), listed(['../outside.md']), listed(['/abs.md']), listed(['a.md', 'a.md'])]) {
    const repo = benchWith(t, [], { [LIST]: content });
    const r = await readPublicPlans({ repo, ref: 'origin/main', paths: PATHS, listPath: LIST });
    assert.equal(r.ok, false, content);
    assert.equal(r.kind, 'env');
    assert.match(r.reason, /public-texts list/);
  }
});

test('A_private_paragraph_whose_windows_are_spread_over_two_unlisted_public_files_is_still_a_hit', async t => {
  const sentence = 'the violet engine whistles beneath seven copper bridges while the lantern keeper counts falling marbles';
  const words = sentence.split(' ');
  const repo = benchWith(t, [], { 'notes/first-half.md': `${words.slice(0, 8).join(' ')}.\n`, 'notes/second-half.md': `${words.slice(3).join(' ')}.\n` });
  const texts = await readPublicPlans({ repo, ref: 'origin/main', paths: PATHS, listPath: LIST });
  const needles = aidNeedles({ aids: [{ name: 'aid.md', text: `Notes.\n${sentence}.\n` }], publicTexts: texts.value }).value;
  assert.ok(findAidHits('p', `${sentence}\n`, needles).length > 0, 'two unlisted files do not make the paragraph public');
  const both = benchWith(t, ['notes/first-half.md', 'notes/second-half.md'], { 'notes/first-half.md': `${words.slice(0, 8).join(' ')}.\n`, 'notes/second-half.md': `${words.slice(3).join(' ')}.\n` });
  const listedTexts = await readPublicPlans({ repo: both, ref: 'origin/main', paths: PATHS, listPath: LIST });
  assert.equal(listedTexts.value.length, 4);
});

test('A_ref_that_does_not_resolve_is_a_fail_closed_even_when_no_text_is_required', async t => {
  const repo = benchRepo(t);
  const texts = await readPublicPlans({ repo, ref: 'origin/nowhere', paths: [] });
  assert.match(texts.reason ?? '', /does not resolve/);
  assert.equal(texts.ok, false);
  assert.equal(texts.kind, 'env');
});
