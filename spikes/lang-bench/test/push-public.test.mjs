// The texts that exempt shingles are public by definition, so they are read from what is PUBLISHED - `origin/main` of the bench
// repository - and never from the working tree, which the branch under check can edit. Unreadable means the check does not run.
import assert from 'node:assert/strict';
import { writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
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
