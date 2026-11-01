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
