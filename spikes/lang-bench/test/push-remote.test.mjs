// `push-check.mjs --setup-remote`: step 1 of the push path, idempotent, over local bare repositories standing in for GitHub.
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { EXIT } from '../lib/push-check.mjs';
import { setupRemote, TASK_REPO_URL } from '../lib/push-remote.mjs';
import { commit, git, makeOrigin, makeTaskRepo, ownedFolder } from './support/push-fixtures.mjs';

const configOf = repo => readFileSync(path.join(repo, '.git', 'config'), 'utf8');
const heads = repo => git(repo, 'for-each-ref', '--format=%(refname) %(objectname)', 'refs/heads');

test('The_task_repository_url_is_the_https_one_of_the_plan', () => {
  assert.equal(TASK_REPO_URL, 'https://github.com/oleksandrdubyna88/dew_flow_ai_language_comparison.git');
});

test('No_origin_is_added_and_fetched_and_origin_main_is_checked_to_hold_the_license', async t => {
  const origin = makeOrigin(t);
  const repo = makeTaskRepo(t, {});
  const result = await setupRemote({ repo, url: origin });
  assert.equal(result.exit, EXIT.PASS, result.lines.join('\n'));
  assert.deepEqual(result.lines.map(l => l.replace(/[0-9a-f]{12}/, '<sha>')), [`remote: added origin ${origin}`, 'fetch: ok', 'origin/main: <sha> holds LICENSE']);
  assert.equal(git(repo, 'remote', 'get-url', 'origin'), origin);
});

test('An_origin_with_another_url_is_repointed_and_the_old_url_is_printed', async t => {
  const origin = makeOrigin(t);
  const repo = makeTaskRepo(t, {});
  git(repo, 'remote', 'add', 'origin', 'https://example.invalid/old.git');
  const result = await setupRemote({ repo, url: origin });
  assert.equal(result.exit, EXIT.PASS, result.lines.join('\n'));
  assert.equal(result.lines[0], `remote: re-pointed origin from https://example.invalid/old.git to ${origin}`);
  assert.equal(git(repo, 'remote', 'get-url', 'origin'), origin);
});

test('The_right_origin_is_left_alone_and_a_second_run_changes_nothing', async t => {
  const origin = makeOrigin(t);
  const repo = makeTaskRepo(t, {});
  await setupRemote({ repo, url: origin });
  const before = { config: configOf(repo), heads: heads(repo), tracking: git(repo, 'rev-parse', 'origin/main') };
  const second = await setupRemote({ repo, url: origin });
  assert.equal(second.exit, EXIT.PASS);
  assert.equal(second.lines[0], `remote: origin already ${origin}`);
  assert.deepEqual({ config: configOf(repo), heads: heads(repo), tracking: git(repo, 'rev-parse', 'origin/main') }, before);
});

test('A_failing_fetch_is_exit_3_with_the_cause_and_no_local_branch_changes', async t => {
  const repo = makeTaskRepo(t, {});
  commit(repo, 'feat: local work', { 'a.txt': 'a\n' });
  const before = heads(repo);
  const missing = path.join(ownedFolder(t, 'nowhere'), 'no-such-remote.git');
  const result = await setupRemote({ repo, url: missing });
  assert.equal(result.exit, EXIT.ENVIRONMENT);
  assert.ok(result.lines.some(l => l.startsWith('fetch: failed') && l.includes('no-such-remote.git')), result.lines.join('\n'));
  assert.equal(heads(repo), before);
});

test('An_origin_main_without_a_license_is_exit_3_not_a_pass', async t => {
  const origin = makeOrigin(t);
  const work = path.join(ownedFolder(t, 'clone'), 'w');
  git(path.dirname(work), 'clone', '-q', origin, work);
  commit(work, 'chore: drop the license', { LICENSE: null });
  git(work, 'push', '-q', 'origin', 'HEAD:main');
  const repo = makeTaskRepo(t, {});
  const result = await setupRemote({ repo, url: origin });
  assert.equal(result.exit, EXIT.ENVIRONMENT);
  assert.ok(result.lines.at(-1).startsWith('origin/main: no LICENSE'), result.lines.join('\n'));
});

test('A_remote_tracking_ref_whose_branch_is_gone_is_pruned_and_tags_are_not_fetched', async t => {
  const origin = makeOrigin(t);
  const work = path.join(ownedFolder(t, 'clone2'), 'w');
  git(path.dirname(work), 'clone', '-q', origin, work);
  git(work, 'push', '-q', 'origin', 'HEAD:refs/heads/old');
  const repo = makeTaskRepo(t, { origin });
  git(work, 'tag', 'v-fixture');
  git(work, 'push', '-q', 'origin', 'v-fixture');
  assert.ok(git(repo, 'for-each-ref', 'refs/remotes/origin/old'), 'the fixture starts with the tracking ref');
  git(origin, 'branch', '-q', '-D', 'old');
  const result = await setupRemote({ repo, url: origin });
  assert.equal(result.exit, EXIT.PASS, result.lines.join('\n'));
  assert.equal(git(repo, 'for-each-ref', 'refs/remotes/origin/old'), '', 'a stale tracking ref would hide commits from the range');
  assert.equal(git(repo, 'tag', '--list'), '', 'a tag is never fetched here (a tag is pushed alone)');
});
