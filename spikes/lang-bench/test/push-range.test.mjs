// The commits a push would add, and what each of them adds: the unit every scan of the push check runs over.
import assert from 'node:assert/strict';
import { test } from 'node:test';
import { changesOf, newCommits, readBlobs, readFileAt } from '../lib/push-range.mjs';
import { commit, git, makeOrigin, makeTaskRepo } from './support/push-fixtures.mjs';

test('The_range_is_exactly_the_commits_the_push_would_add_and_not_the_remotes_own', async t => {
  const origin = makeOrigin(t);
  const repo = makeTaskRepo(t, { origin });
  git(repo, 'checkout', '-q', '-b', 'topic');
  const first = commit(repo, 'feat: one', { 'a.txt': 'one\n' });
  const second = commit(repo, 'feat: two', { 'b.txt': 'two\n' });
  const range = await newCommits(repo, 'topic');
  assert.equal(range.ok, true, range.reason);
  assert.equal(range.value.length, 3, 'the local main commit is unrelated to the remote, so it is part of the push');
  assert.deepEqual(range.value.slice(1), [first, second], 'oldest first');
  const remoteSha = git(repo, 'rev-parse', 'origin/main');
  assert.equal(range.value.includes(remoteSha), false);
  // a branch that descends from the remote's main adds only its own commits
  git(repo, 'checkout', '-q', '-b', 'feature', 'origin/main');
  const own = commit(repo, 'feat: own', { 'c.txt': 'c\n' });
  assert.deepEqual((await newCommits(repo, 'feature')).value, [own], 'the remote\'s LICENSE commit is not part of the push');
});

test('A_branch_that_does_not_exist_is_a_configuration_failure_not_an_empty_range', async t => {
  const repo = makeTaskRepo(t, {});
  const range = await newCommits(repo, 'no-such-branch');
  assert.equal(range.ok, false);
  assert.equal(range.kind, 'config');
  assert.match(range.reason, /no-such-branch/);
});

test('Each_commit_reports_its_message_and_the_files_it_adds_or_changes_but_not_the_ones_it_removes', async t => {
  const repo = makeTaskRepo(t, {});
  commit(repo, 'feat: add', { 'keep.txt': 'k\n', 'gone.txt': 'g\n' });
  commit(repo, 'refactor: remove one, change one', { 'gone.txt': null, 'keep.txt': 'k2\n' });
  const range = (await newCommits(repo, 'main')).value;
  const { messages, files } = (await changesOf(repo, range)).value;
  assert.deepEqual(messages.map(m => m.text.trim()), ['chore: skeleton', 'feat: add', 'refactor: remove one, change one']);
  assert.deepEqual(files.map(f => [f.commit === range[2], f.path]).filter(([last]) => last).map(([, p]) => p), ['keep.txt']);
  assert.ok(files.some(f => f.path === 'gone.txt'), 'a file added by an earlier commit and removed later is still in the history a push publishes');
});

test('A_merge_commit_reports_what_it_brings_relative_to_its_first_parent', async t => {
  const origin = makeOrigin(t);
  const repo = makeTaskRepo(t, { origin });
  git(repo, 'checkout', '-q', '-b', 'chore/join');
  git(repo, 'merge', '-q', '--allow-unrelated-histories', '--no-edit', 'origin/main');
  const range = (await newCommits(repo, 'chore/join')).value;
  const merge = git(repo, 'rev-parse', 'HEAD');
  const { files } = (await changesOf(repo, range)).value;
  assert.deepEqual(files.filter(f => f.commit === merge).map(f => f.path), ['LICENSE']);
});

test('A_submodule_pointer_is_not_a_file_to_read', async t => {
  const repo = makeTaskRepo(t, {});
  git(repo, 'update-index', '--add', '--cacheinfo', '160000,1111111111111111111111111111111111111111,sub');
  git(repo, 'commit', '-q', '-m', 'chore: pin a submodule');
  const { files } = (await changesOf(repo, (await newCommits(repo, 'main')).value)).value;
  assert.equal(files.some(f => f.path === 'sub'), false);
});

test('Blobs_are_read_once_by_id_and_a_file_at_a_branch_tip_is_read_by_path', async t => {
  const repo = makeTaskRepo(t, { files: { 'README.md': 'tip readme\n', 'a.txt': 'same\n', 'b.txt': 'same\n' } });
  const { files } = (await changesOf(repo, (await newCommits(repo, 'main')).value)).value;
  const oids = [...new Set(files.map(f => f.oid))];
  assert.equal(oids.length, 2, 'two paths with identical bytes are one blob');
  const blobs = (await readBlobs(repo, oids)).value;
  assert.equal(blobs.get(files.find(f => f.path === 'a.txt').oid), 'same\n');
  assert.equal((await readFileAt(repo, 'main', 'README.md')).value, 'tip readme\n');
  assert.equal((await readFileAt(repo, 'main', 'NOPE.md')).value, null, 'absent is null, not an empty text');
});
