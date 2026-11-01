// The commits a push would add, and what each of them adds: the unit every scan of the push check runs over.
import assert from 'node:assert/strict';
import { test } from 'node:test';
import { runBatchCheck } from '../lib/push-git.mjs';
import { changesOf, newCommits, readBlobs, readFileAt, tipTree } from '../lib/push-range.mjs';
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

test('A_submodule_pointer_is_reported_with_its_mode_so_that_it_is_never_read_as_a_file', async t => {
  const repo = makeTaskRepo(t, {});
  git(repo, 'update-index', '--add', '--cacheinfo', '160000,1111111111111111111111111111111111111111,sub');
  git(repo, 'commit', '-q', '-m', 'chore: pin a submodule');
  const { files } = (await changesOf(repo, (await newCommits(repo, 'main')).value)).value;
  assert.deepEqual(files.filter(f => f.path === 'sub').map(f => f.mode), ['160000']);
  assert.deepEqual(files.filter(f => f.path === 'README.md').map(f => f.mode), ['100644']);
});

test('Blobs_are_read_once_by_id_and_a_file_at_a_branch_tip_is_read_by_path', async t => {
  const repo = makeTaskRepo(t, { files: { 'README.md': 'tip readme\n', 'a.txt': 'same\n', 'b.txt': 'same\n' } });
  const { files } = (await changesOf(repo, (await newCommits(repo, 'main')).value)).value;
  const oids = [...new Set(files.map(f => f.oid))];
  assert.equal(oids.length, 2, 'two paths with identical bytes are one blob');
  const blobs = (await readBlobs(repo, oids)).value;
  assert.equal(blobs.get(files.find(f => f.path === 'a.txt').oid).toString(), 'same\n');
  assert.equal((await readFileAt(repo, 'main', 'README.md')).value, 'tip readme\n');
  assert.equal((await readFileAt(repo, 'main', 'NOPE.md')).value, null, 'absent is null, not an empty text');
});

test('The_tip_tree_lists_every_entry_at_the_branch_tip_with_its_mode_gitlinks_included', async t => {
  const repo = makeTaskRepo(t, { files: { 'a.txt': 'a\n', 'dir/b.txt': 'b\n' } });
  commit(repo, 'chore: drop a file', { 'a.txt': null });
  git(repo, 'update-index', '--add', '--cacheinfo', '160000,1111111111111111111111111111111111111111,sub');
  git(repo, 'commit', '-q', '-m', 'chore: pin a submodule');
  const tree = (await tipTree(repo, 'main')).value;
  assert.deepEqual([...tree.keys()].sort(), ['dir/b.txt', 'sub']);
  assert.deepEqual(tree.get('dir/b.txt'), { oid: git(repo, 'rev-parse', 'main:dir/b.txt'), mode: '100644' });
  assert.equal(tree.get('sub').mode, '160000');
});

test('The_blobs_of_one_check_are_read_in_one_process_under_an_aggregate_ceiling_that_refuses_clearly', async t => {
  const repo = makeTaskRepo(t, { files: { 'a.txt': 'x'.repeat(700), 'b.txt': 'y'.repeat(700), 'c.txt': 'z'.repeat(700) } });
  const { files } = (await changesOf(repo, (await newCommits(repo, 'main')).value)).value;
  const oids = files.map(f => f.oid);
  const fine = await readBlobs(repo, oids, { maxTotalBytes: 10_000 });
  assert.equal(fine.ok, true, fine.reason);
  assert.equal(fine.value.size, oids.length);
  const over = await readBlobs(repo, oids, { maxTotalBytes: 1_000 });
  assert.equal(over.ok, false);
  assert.equal(over.kind, 'env');
  assert.match(over.reason, /more than 1000 bytes/);
  assert.match(over.reason, /refused/);
  const missing = await readBlobs(repo, ['0'.repeat(40)]);
  assert.equal(missing.ok, false, 'a blob git does not have is a failure, never an empty text');
});

test('An_oversize_blob_is_refused_naming_sha_size_path_and_commit_never_content', async t => {
  const repo = makeTaskRepo(t, { files: { 'small.txt': 'ok\n', 'dir/big.txt': 'SECRETCONTENT'.repeat(100) } });
  const { files } = (await changesOf(repo, (await newCommits(repo, 'main')).value)).value;
  const big = files.find(f => f.path === 'dir/big.txt');
  const where = new Map(files.map(f => [f.oid, { path: f.path, commit: f.commit }]));
  const refused = await readBlobs(repo, files.map(f => f.oid), { maxObjectBytes: 1000, where });
  assert.equal(refused.ok, false);
  assert.equal(refused.kind, 'env');
  assert.match(refused.reason, new RegExp(big.oid));
  assert.match(refused.reason, /1300 bytes/);
  assert.match(refused.reason, /dir\/big\.txt in /);
  assert.ok(refused.reason.includes(big.commit.slice(0, 10)));
  assert.equal(refused.reason.includes('SECRETCONTENT'), false);
});

test('An_aggregate_over_the_ceiling_names_the_blob_that_crossed_it_with_its_sha_size_path_and_commit', async t => {
  const repo = makeTaskRepo(t, { files: { 'a.txt': 'a'.repeat(300), 'b.txt': 'b'.repeat(500), 'c.txt': 'c'.repeat(400) } });
  const { files } = (await changesOf(repo, (await newCommits(repo, 'main')).value)).value;
  const where = new Map(files.map(f => [f.oid, { path: f.path, commit: f.commit }]));
  const refused = await readBlobs(repo, files.map(f => f.oid), { maxTotalBytes: 1000, where });
  assert.equal(refused.ok, false);
  assert.match(refused.reason, /more than 1000 bytes/);
  const crossing = files.find(f => refused.reason.includes(f.oid));
  assert.ok(crossing, 'a blob sha is named');
  assert.match(refused.reason, new RegExp(`${crossing.path.replace('.', '\\.')} in `));
  assert.match(refused.reason, /\d+ bytes/);
});

test('The_sizes_come_from_batch_check_before_any_content_and_a_missing_object_is_named', async t => {
  const repo = makeTaskRepo(t, { files: { 'a.txt': 'alpha\n', 'big.txt': 'x'.repeat(5000) } });
  const oids = [git(repo, 'rev-parse', 'main:a.txt'), git(repo, 'rev-parse', 'main:big.txt'), '0'.repeat(40)];
  const sizes = await runBatchCheck(repo, oids);
  assert.equal(sizes.ok, true, sizes.reason);
  assert.deepEqual([sizes.value.get(oids[0]).size, sizes.value.get(oids[1]).size], [6, 5000]);
  assert.deepEqual(sizes.missing, [oids[2]]);
});

test('An_oversize_blob_is_refused_before_any_content_is_read_and_the_refusal_names_sha_size_path_and_commit_never_content', async t => {
  const repo = makeTaskRepo(t, { files: { 'small.txt': 'ok\n', 'dir/big.txt': 'SECRETCONTENT'.repeat(100) } });
  const { files } = (await changesOf(repo, (await newCommits(repo, 'main')).value)).value;
  const big = files.find(f => f.path === 'dir/big.txt');
  const where = new Map(files.map(f => [f.oid, { path: f.path, commit: f.commit }]));
  const calls = [];
  const refused = await readBlobs(repo, files.map(f => f.oid), { maxObjectBytes: 1000, where, readBatch: async () => { calls.push(1); throw new Error('the content reader must not run'); } });
  assert.equal(refused.ok, false);
  assert.equal(refused.kind, 'env');
  assert.equal(calls.length, 0, 'no content was read');
  assert.match(refused.reason, new RegExp(big.oid));
  assert.match(refused.reason, /1300 bytes/);
  assert.match(refused.reason, /dir\/big\.txt in /);
  assert.ok(refused.reason.includes(big.commit.slice(0, 10)));
  assert.equal(refused.reason.includes('SECRETCONTENT'), false);
});

test('An_aggregate_over_the_ceiling_is_refused_before_reading_and_names_the_largest_blobs', async t => {
  const repo = makeTaskRepo(t, { files: { 'a.txt': 'a'.repeat(300), 'b.txt': 'b'.repeat(500), 'c.txt': 'c'.repeat(400) } });
  const { files } = (await changesOf(repo, (await newCommits(repo, 'main')).value)).value;
  const where = new Map(files.map(f => [f.oid, { path: f.path, commit: f.commit }]));
  let reads = 0;
  const refused = await readBlobs(repo, files.map(f => f.oid), { maxTotalBytes: 1000, where, readBatch: async () => { reads += 1; return { ok: true, value: new Map(), missing: [] }; } });
  assert.equal(refused.ok, false);
  assert.equal(reads, 0);
  assert.match(refused.reason, /more than 1000 bytes/);
  assert.ok(refused.reason.indexOf('b.txt') < refused.reason.indexOf('c.txt') && refused.reason.indexOf('c.txt') < refused.reason.indexOf('a.txt'), 'largest first');
});
