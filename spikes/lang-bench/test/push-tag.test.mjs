// `push-check.mjs --tag <name>`: the freeze procedure checks a tag before it is pushed. An annotated tag's MESSAGE is text a push publishes and no branch check reads; the
// commit it points at must already be published (so checked when it was pushed), or the tag would carry unchecked commits to the remote.
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { checkTag } from '../lib/push-tag.mjs';
import { EXIT } from '../lib/push-exit.mjs';
import { formatReport } from '../lib/push-check.mjs';
import { commit, git, makeOrigin, makeTaskRepo, ownedFolder } from './support/push-fixtures.mjs';
import { loadInputsForTest } from './support/push-inputs.mjs';

const AID_TEXT = 'Header.\nA widget that frobnicates every sprocket twice before lunch will stall.\nFooter.\n';
const LEAK = 'the widget that frobnicates every sprocket twice before dinner';
const SECRET = `${'github_pat'}_${'aB3dE5gH7jK9mN1pQ3sT5vW7yZ'}_${'aB3dE5gH7jK9mN1pQ3sT5v'}`;
const inputs = loadInputsForTest({ aidText: AID_TEXT });
const checkOf = (result, name) => result.checks.find(c => c.name === name);

/** A task repository with a bare origin; `published` is origin/main's tip, `topic` an unpublished branch commit. */
function world(t) {
  const repo = makeTaskRepo(t, { origin: makeOrigin(t) });
  const published = git(repo, 'rev-parse', 'origin/main');
  git(repo, 'checkout', '-q', '-b', 'topic', 'origin/main');
  const unpublished = commit(repo, 'feat: an unpublished commit', { 'a.txt': 'x\n' });
  return { repo, published, unpublished };
}

test('An_annotated_tag_message_is_scanned_for_secrets_and_aid_text_and_never_printed', async t => {
  const { repo, published } = world(t);
  git(repo, 'tag', '-a', 'v-secret', '-m', `release ${SECRET}`, published);
  git(repo, 'tag', '-a', 'v-leak', '-m', `notes\n${LEAK}`, published);
  git(repo, 'tag', '-a', 'v-clean', '-m', 'the freeze of the ten specs', published);
  const secret = await checkTag({ repo, tag: 'v-secret', inputs });
  assert.equal(secret.exit, EXIT.REFUSED);
  assert.deepEqual(checkOf(secret, 'secrets').findings.map(f => [f.path, f.line, f.what]), [['tag v-secret', 1, 'github-fine-grained-pat']]);
  assert.equal(formatReport(secret).includes(SECRET.slice(0, 16)), false);
  const leak = await checkTag({ repo, tag: 'v-leak', inputs });
  assert.deepEqual(checkOf(leak, 'leakcheck').findings.map(f => [f.path, f.line]), [['tag v-leak', 2]]);
  assert.equal((await checkTag({ repo, tag: 'v-clean', inputs })).exit, EXIT.PASS);
});

test('A_tag_whose_commit_is_not_published_is_refused_whether_it_is_lightweight_or_annotated', async t => {
  const { repo, published, unpublished } = world(t);
  git(repo, 'tag', 'light-published', published);
  git(repo, 'tag', 'light-unpublished', unpublished);
  git(repo, 'tag', '-a', 'note-unpublished', '-m', 'a clean message', unpublished);
  assert.equal((await checkTag({ repo, tag: 'light-published', inputs })).exit, EXIT.PASS, 'a published commit was checked when it was pushed');
  const light = await checkTag({ repo, tag: 'light-unpublished', inputs });
  assert.equal(light.exit, EXIT.REFUSED);
  assert.deepEqual(checkOf(light, 'tag-target').findings.map(f => [f.commit, f.what]), [[unpublished, 'the tagged commit is on no remote ref: it was never checked; push its branch through the check first']]);
  assert.equal((await checkTag({ repo, tag: 'note-unpublished', inputs })).exit, EXIT.REFUSED);
});

test('A_tag_that_does_not_exist_is_a_configuration_failure_and_the_report_names_the_checks', async t => {
  const { repo, published } = world(t);
  const missing = await checkTag({ repo, tag: 'no-such-tag', inputs });
  assert.equal(missing.exit, EXIT.CONFIG);
  assert.match(missing.error, /no-such-tag/);
  git(repo, 'tag', '-a', 'v1', '-m', 'fine', published);
  const report = formatReport(await checkTag({ repo, tag: 'v1', inputs }));
  for (const name of ['leakcheck', 'secrets', 'tag-target']) assert.match(report, new RegExp(`^${name} +PASS`, 'm'));
});

test('The_command_checks_a_tag_with_the_same_aids_and_exits_with_the_same_codes', t => {
  const { repo, published } = world(t);
  git(repo, 'tag', '-a', 'v-secret', '-m', `release ${SECRET}`, published);
  const aid = path.join(ownedFolder(t, 'tagaid'), 'aid.md');
  writeFileSync(aid, AID_TEXT);
  const CLI = fileURLToPath(new URL('../push-check.mjs', import.meta.url));
  const publicFile = fileURLToPath(new URL('./fixtures/public-unrelated.md', import.meta.url));
  const dummy = fileURLToPath(new URL('./fixtures/own-secret-dummy.txt', import.meta.url));
  const r = spawnSync(process.execPath, [CLI, '--tag', 'v-secret', '--repo', repo, '--aid', aid, '--public', publicFile, '--secret-file', dummy], { encoding: 'utf8' });
  assert.equal(r.status, 1, r.stdout + r.stderr);
  assert.match(r.stdout, /^secrets +FAIL/m);
  assert.equal(spawnSync(process.execPath, [CLI, '--tag', 'v-secret', '--branch', 'x', '--repo', repo, '--aid', aid], { encoding: 'utf8' }).status, 4, 'two modes at once');
});

/** The bare repository behind `repo`'s origin. */
const bareOf = repo => git(repo, 'remote', 'get-url', 'origin');

test('A_tag_on_a_commit_whose_remote_branch_was_deleted_is_refused_although_the_local_tracking_ref_still_holds_it', async t => {
  const { repo } = world(t);
  const bare = bareOf(repo);
  git(repo, 'push', '-q', 'origin', 'topic');
  const pushed = git(repo, 'rev-parse', 'topic');
  git(repo, 'tag', 'v-pushed', pushed);
  assert.ok(git(repo, 'rev-parse', 'refs/remotes/origin/topic'), 'the push left a local tracking ref');
  assert.equal((await checkTag({ repo, tag: 'v-pushed', inputs })).exit, EXIT.PASS, 'on a live remote branch: fine');
  git(bare, 'branch', '-q', '-D', 'topic'); // the remote branch is deleted; the local origin/topic stays
  assert.equal(git(repo, 'rev-parse', 'refs/remotes/origin/topic'), pushed, 'the stale tracking ref still says the commit is published');
  const result = await checkTag({ repo, tag: 'v-pushed', inputs });
  assert.equal(result.exit, EXIT.REFUSED, formatReport(result));
  assert.deepEqual(checkOf(result, 'tag-target').findings.map(f => f.commit), [pushed]);
});

test('When_the_remote_head_moved_on_to_commits_not_fetched_even_a_tag_at_the_old_head_cannot_be_decided_and_says_fetch_first', async t => {
  const { repo, published } = world(t);
  const bare = bareOf(repo);
  const other = path.join(path.dirname(repo), 'other-clone');
  git(path.dirname(repo), 'clone', '-q', bare, other);
  commit(other, 'feat: the remote moved on', { 'z.txt': 'z\n' });
  git(other, 'push', '-q', 'origin', 'HEAD:main');
  git(repo, 'tag', 'v-old-head', published);
  const stale = await checkTag({ repo, tag: 'v-old-head', inputs });
  assert.equal(stale.exit, EXIT.REFUSED, 'the local origin/main still says published, but the remote head is a commit this clone does not have');
  assert.match(checkOf(stale, 'tag-target').findings[0].what, /fetch first/);
  git(repo, 'fetch', '-q', 'origin');
  assert.equal((await checkTag({ repo, tag: 'v-old-head', inputs })).exit, EXIT.PASS, 'after the user fetches, the old head is an ancestor of the live one');
});

test('When_a_remote_commit_is_not_present_locally_and_nothing_present_holds_the_tag_the_check_says_fetch_first', async t => {
  const { repo, unpublished } = world(t);
  const bare = bareOf(repo);
  const other = path.join(path.dirname(repo), 'other-clone2');
  git(path.dirname(repo), 'clone', '-q', bare, other);
  commit(other, 'feat: a remote branch this clone has never fetched', { 'y.txt': 'y\n' });
  git(other, 'push', '-q', 'origin', 'HEAD:refs/heads/unfetched');
  git(repo, 'tag', 'v-unpublished', unpublished);
  const result = await checkTag({ repo, tag: 'v-unpublished', inputs });
  assert.equal(result.exit, EXIT.REFUSED);
  const [finding] = checkOf(result, 'tag-target').findings;
  assert.match(finding.what, /fetch first/);
  assert.match(finding.what, /not present locally/);
  assert.equal(git(repo, 'for-each-ref', 'refs/remotes/origin/unfetched'), '', 'the check never fetched on the user\'s behalf');
});

test('A_remote_that_cannot_be_asked_is_exit_3_never_a_pass', async t => {
  const { repo, published } = world(t);
  git(repo, 'tag', 'v-p', published);
  git(repo, 'remote', 'set-url', 'origin', path.join(path.dirname(repo), 'no-such-remote.git'));
  const result = await checkTag({ repo, tag: 'v-p', inputs });
  assert.equal(result.exit, EXIT.ENVIRONMENT);
  assert.match(result.error, /ls-remote/);
});

test('A_tag_check_without_repo_says_what_to_give_and_exits_4', () => {
  const CLI = fileURLToPath(new URL('../push-check.mjs', import.meta.url));
  const publicFile = fileURLToPath(new URL('./fixtures/public-unrelated.md', import.meta.url));
  const aid = fileURLToPath(new URL('./fixtures/own-secret-dummy.txt', import.meta.url));
  const r = spawnSync(process.execPath, [CLI, '--tag', 'v1', '--aid', aid, '--public', publicFile, '--secret-file', aid], { encoding: 'utf8' });
  assert.equal(r.status, 4, r.stdout + r.stderr);
  assert.match(r.stdout, /--tag needs --repo <path of the task repository>/);
});
