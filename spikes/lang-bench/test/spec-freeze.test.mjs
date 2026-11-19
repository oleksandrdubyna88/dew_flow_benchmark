// The freeze tools of F2.S2 (d), part one: the plan and the freeze commit, FORMAT's bytes, the guards of F3.S1's launch half, the git sequence under squash merges, the tag before and after the push, and the hash list.
// Synthetic task repositories made with `git init` in owned folders (test/support/freeze-fixtures.mjs); the GitHub remote is a local bare repository, a squash merge is `git merge --squash`. The record, the superseding
// tag and the interruptions are in spec-freeze-record.test.mjs; the order of the procedure in spec-freeze-order.test.mjs.
import assert from 'node:assert/strict';
import { chmodSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { spawnSync } from 'node:child_process';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { runFreeze } from '../lib/freeze-cli.mjs';
import { fetchTaskRepo } from '../lib/task-repo.mjs';
import { laterForm } from '../lib/conformance-check-structure.mjs';
import { readFacts } from '../lib/conformance-facts.mjs';
import { parseFormat } from '../lib/format-structure.mjs';
import { specBlobMatches, workspaceCommitProblems } from '../lib/spec-freeze.mjs';
import { annotate, baseFiles, formatText, freezeAndMerge, freezeBranch, fullFreeze, LATER_TAG, makeFreezeRepo, MESSAGE, pushTag, sha256, SLUGS, specPath, specText, squashMerge, TAG, workspaceFiles, workspacesBranch, FORMAT_PATH } from './support/freeze-fixtures.mjs';
import { commit, git } from './support/push-fixtures.mjs';
import { ownedFolder } from './toolchain-fixtures.mjs';

const said = result => `${result.stdout}${result.stderr}`;
const at = (repo, commitSha, file) => git(repo, 'show', `${commitSha}:${file}`);

/** The plan on main applied to the working tree on a new branch and committed; returns `{ patch, head }`. */
async function planAndApply(f, { date = '20261201', branch = 'freeze/by-plan' } = {}) {
  const planned = await f.run('plan', ['--ref', 'HEAD', '--date', date]);
  assert.equal(planned.exit, 0, said(planned));
  const patch = path.join(f.root, 'plan.patch');
  writeFileSync(patch, planned.stdout);
  git(f.repo, 'checkout', '-q', '-b', branch);
  git(f.repo, 'apply', patch);
  return { patch, head: commit(f.repo, `docs: freeze ${date}`, {}) };
}

// ---- the plan and the freeze commit ------------------------------------------------------------------------------------------------------------------

test('The_freeze_plan_changes_only_the_freeze_tag_row_of_each_spec', async t => {
  const f = makeFreezeRepo(t);
  const { head } = await planAndApply(f);
  assert.deepEqual(git(f.repo, 'diff', '--name-only', f.base, head).split('\n'), SLUGS.map(specPath), 'the six specs and nothing else');
  for (const line of git(f.repo, 'diff', '--numstat', f.base, head).split('\n')) assert.match(line, /^1\t1\t/, 'one line changed in each');
  for (const slug of SLUGS) {
    const text = at(f.repo, head, specPath(slug));
    assert.ok(text.includes('| Freeze tag | `spec-frozen-B-20261201` |'), slug);
    assert.equal(text.replace('`spec-frozen-B-20261201`', 'none yet'), at(f.repo, f.base, specPath(slug)), `${slug}: every other byte is the gated spec's`);
  }
  const checked = await f.run('check-commit', ['--before', f.base, '--after', head]);
  assert.equal(checked.exit, 0, said(checked));
});

/** The freeze commit with the six rows right and one more change in the spec of task 06 (a changed word on one line) or in another path; returns the head. */
function freezeWithExtra(f, extra) {
  const row = /^\| Freeze tag \| .* \|$/m;
  const edited = readFileSync(path.join(f.repo, specPath('06-blog-api')), 'utf8').replace(row, `| Freeze tag | \`${TAG}\` |`);
  return freezeBranch(f, { more: extra(edited) });
}

test('A_freeze_commit_that_changes_any_other_byte_of_a_spec_is_refused_naming_the_line', async t => {
  const f = makeFreezeRepo(t);
  const line = readFileSync(path.join(f.repo, specPath('06-blog-api')), 'utf8').split('\n').findIndex(text => text.includes('For n = 1')) + 1;
  const head = freezeWithExtra(f, edited => ({ [specPath('06-blog-api')]: edited.replace('For n = 1', 'For n = 2') }));
  const refused = await f.run('check-commit', ['--before', f.base, '--after', head]);
  assert.equal(refused.exit, 1, said(refused));
  assert.ok(refused.stderr.includes(specPath('06-blog-api')) && refused.stderr.includes(`line ${line}`), refused.stderr);
  const clean = await f.run('check-commit', ['--before', f.base, '--after', freezeBranch(f, { tag: TAG, from: f.base })]);
  assert.equal(clean.exit, 0, 'the six rows alone pass');
});

test('A_freeze_commit_with_another_path_a_missing_row_or_two_different_tags_is_refused', async t => {
  const f = makeFreezeRepo(t);
  const extraPath = await f.run('check-commit', ['--before', f.base, '--after', freezeBranch(f, { more: { 'README.md': 'a stray file\n' } })]);
  assert.equal(extraPath.exit, 1);
  assert.match(extraPath.stderr, /README\.md/);
  const fiveRows = await f.run('check-commit', ['--before', f.base, '--after', freezeBranch(f, { from: f.base, rows: SLUGS.slice(1) })]);
  assert.equal(fiveRows.exit, 1);
  assert.ok(fiveRows.stderr.includes(specPath(SLUGS[0])), 'names the spec whose row was not set');
  const mixed = freezeBranch(f, { from: f.base, more: { [specPath(SLUGS[2])]: `${at(f.repo, f.base, specPath(SLUGS[2])).replace('| Freeze tag | none yet |', `| Freeze tag | \`${LATER_TAG}\` |`)}\n` } });
  const two = await f.run('check-commit', ['--before', f.base, '--after', mixed]);
  assert.equal(two.exit, 1);
  assert.match(two.stderr, /one tag|different tag/);
  const notATag = freezeBranch(f, { tag: 'soon', from: f.base });
  assert.equal((await f.run('check-commit', ['--before', f.base, '--after', notATag])).exit, 1, 'a row that is not a freeze tag name');
});

test('A_freeze_commit_that_changes_FORMAT_by_one_byte_is_refused', async t => {
  const f = makeFreezeRepo(t);
  const changed = await f.run('check-commit', ['--before', f.base, '--after', freezeBranch(f, { more: { [FORMAT_PATH]: `${formatText()} ` } })]);
  assert.equal(changed.exit, 1, said(changed));
  assert.ok(changed.stderr.includes(FORMAT_PATH) && /FORMAT/.test(changed.stderr), changed.stderr);
  const rowsAlone = await f.run('check-commit', ['--before', f.base, '--after', freezeBranch(f, { from: f.base })]);
  assert.equal(rowsAlone.exit, 0, 'the six rows alone pass');
});

test('A_spec_whose_gate_verdict_is_still_its_placeholder_is_refused_before_any_edit', async t => {
  const f = makeFreezeRepo(t, { files: baseFiles({ specs: { '10': specText('10', { gated: false }) } }) });
  const refused = await f.run('plan', ['--ref', 'HEAD', '--date', '20261201']);
  assert.equal(refused.exit, 3, said(refused));
  assert.equal(refused.stdout, '', 'no edit is printed');
  assert.ok(refused.stderr.includes('10-dag-scheduler') && /Gate verdict/.test(refused.stderr), refused.stderr);
  const gated = await makeFreezeRepo(t).run('plan', ['--ref', 'HEAD', '--date', '20261201']);
  assert.equal(gated.exit, 0, 'the control, all six gated, plans');
});

test('A_spec_gated_under_another_FORMAT_record_is_refused_for_the_six', async t => {
  const format = formatText();
  const base = { version: 2, sha256: sha256(format), gate: 'inputs/gates/FORMAT-v2.md', grammarSha256: '8'.repeat(64) };
  const record = { A: { ...base, tasks: ['01', '02', '03', '06', '09'] }, B: { ...base, tasks: ['04', '05', '07', '08', '10'] } };
  const f = makeFreezeRepo(t, { record });
  const refused = await f.run('plan', ['--ref', 'HEAD', '--date', '20261201']);
  assert.equal(refused.exit, 3, said(refused));
  assert.match(refused.stderr, /06-blog-api.*record A.*not record B|06-blog-api.*gated under record A/s);
  assert.equal(refused.stdout, '');
});

test('A_conformance_check_that_is_not_all_ok_is_refused_naming_the_spec_and_the_check', async t => {
  const f = makeFreezeRepo(t, { files: baseFiles({ specs: { '05': specText('05').replace('**R2.** For n = 1 the output is the single term 1.\n', '') } }) });
  const refused = await f.run('plan', ['--ref', 'HEAD', '--date', '20261201']);
  assert.equal(refused.exit, 3, said(refused));
  assert.match(refused.stderr, /05-key-value-server.*FAIL /s);
  assert.equal(refused.stdout, '');
});

test('The_plan_refuses_a_foreign_FORMAT_a_wrong_day_a_dirty_tree_a_task_list_that_is_not_the_six_and_a_FORMAT_that_fails_the_class_check', async t => {
  const f = makeFreezeRepo(t);
  const plan = (flags, repo = f) => repo.run('plan', ['--ref', 'HEAD', ...flags]);
  assert.match((await plan(['--date', '20261130'])).stderr, /today's UTC date 20261201/);
  assert.match((await plan(['--date', '2026-12-01'])).stderr, /--date/);
  assert.equal((await plan(['--date', '20261201'])).exit, 0, 'control');
  writeFileSync(path.join(f.repo, 'dirty.txt'), 'x\n');
  assert.match((await plan(['--date', '20261201'])).stderr, /not clean|untracked|dirty/i);
  rmSync(path.join(f.repo, 'dirty.txt'));
  commit(f.repo, 'a commit', {});
  assert.match((await f.run('plan', ['--ref', f.base, '--date', '20261201'])).stderr, /HEAD .* is not .*--ref|--ref .* is not HEAD/i, 'the tree is at another commit than the ref');
  const pinned = makeFreezeRepo(t, { record: { ...{}, A: { version: 2, sha256: '1'.repeat(64), tasks: ['01'] }, B: { version: 2, sha256: '1'.repeat(64), tasks: ['04', '05', '06', '07', '08', '10'] } } });
  const foreign = await plan(['--date', '20261201'], pinned);
  assert.equal(foreign.exit, 3);
  assert.match(foreign.stderr, /FORMAT.*is not the FORMAT build step 2a records/);
  const withoutEight = baseFiles();
  delete withoutEight[specPath('08-ledger-api')];
  const missing = makeFreezeRepo(t, { files: withoutEight });
  assert.match((await plan(['--date', '20261201'], missing)).stderr, /08|task list/);
  const fiveTasks = makeFreezeRepo(t, { record: { A: { version: 2, sha256: sha256(formatText()), tasks: ['01'] }, B: { version: 2, sha256: sha256(formatText()), tasks: ['04', '05', '06', '07', '08'] } } });
  assert.match((await plan(['--date', '20261201'], fiveTasks)).stderr, /exactly the six/);
  const unclassed = makeFreezeRepo(t, { format: formatText({ brokenClasses: true }) });
  const classes = await plan(['--date', '20261201'], unclassed);
  assert.equal(classes.exit, 3);
  assert.match(classes.stderr, /class check/);
});

// ---- the guards F3.S1 calls --------------------------------------------------------------------------------------------------------------------------

test('A_session_spec_whose_blob_differs_from_the_tag_is_refused_and_one_that_differs_only_in_workspace_files_is_not', async t => {
  const f = makeFreezeRepo(t);
  const { c } = fullFreeze(f);
  const frozenSha = sha256(`${at(f.repo, c, specPath('05-key-value-server'))}\n`);
  const pinned = { repo: f.repo, tag: TAG, path: specPath('05-key-value-server'), sha256: frozenSha, states: null };
  assert.equal((await specBlobMatches(pinned)).ok, true, 'the blob at the tag has the pinned sha256');
  const gatedSha = sha256(`${at(f.repo, f.base, specPath('05-key-value-server'))}\n`);
  const refused = await specBlobMatches({ ...pinned, sha256: gatedSha });
  assert.equal(refused.ok, false, 'the gated bytes are not the frozen bytes');
  assert.ok(refused.reason.includes(specPath('05-key-value-server')) && refused.reason.includes(TAG), refused.reason);
  commit(f.repo, 'more workspace files after W', { 'csharp/05-key-value-server/extra.txt': 'only a workspace file\n' });
  assert.equal((await specBlobMatches(pinned)).ok, true, 'a difference in workspace files changes nothing about the spec');
  assert.equal((await specBlobMatches({ ...pinned, path: specPath('99-none') })).ok, false, 'a path the tag does not hold');
  assert.equal((await specBlobMatches({ ...pinned, tag: 'spec-frozen-B-20300101' })).ok, false, 'a tag the repository does not hold');
});

test('A_guard_given_the_superseded_tag_refuses_it', async t => {
  const f = makeFreezeRepo(t);
  const { c } = fullFreeze(f);
  const pinned = { repo: f.repo, tag: TAG, path: specPath('05-key-value-server'), sha256: sha256(`${at(f.repo, c, specPath('05-key-value-server'))}\n`) };
  const states = { freeze: { tag: LATER_TAG, superseded: [{ tag: TAG, tagObjectSha: 'a'.repeat(40), why: 'a cause was fixed' }] } };
  const refused = await specBlobMatches({ ...pinned, states });
  assert.equal(refused.ok, false);
  assert.match(refused.reason, new RegExp(`${TAG}.*superseded`));
  assert.ok((await workspaceCommitProblems({ repo: f.repo, tag: TAG, commit: c, states })).length > 0, 'the workspace guard refuses it too');
  const current = { freeze: { tag: TAG, tagObjectSha: git(f.repo, 'rev-parse', `refs/tags/${TAG}`), commitSha: c, superseded: [] } };
  assert.equal((await specBlobMatches({ ...pinned, states: current })).ok, true, 'the record\'s own tag passes');
  assert.deepEqual(await workspaceCommitProblems({ repo: f.repo, tag: TAG, commit: c, states: current }), [], 'and so does W with the record\'s own tag');
  const other = await specBlobMatches({ ...pinned, tag: LATER_TAG, states: current });
  assert.equal(other.ok, false, 'a tag that is not the record\'s freeze.tag');
});

// ---- the git sequence under squash merges ------------------------------------------------------------------------------------------------------------

test('The_tag_on_the_squash_commit_C_and_a_workspaces_branch_cut_from_C_and_squash_merged_give_a_W_that_descends_from_C', async t => {
  const f = makeFreezeRepo(t);
  const { c, w } = fullFreeze(f);
  assert.equal(git(f.repo, 'rev-parse', `${w}^`), c, 'W is a new commit on top of C');
  const byTag = await f.run('check-workspace', ['--tag', TAG, '--commit', w]);
  assert.equal(byTag.exit, 0, said(byTag));
  assert.equal((await f.run('check-workspace', ['--base', c, '--commit', w])).exit, 0, 'and by the base');
  assert.deepEqual(await workspaceCommitProblems({ repo: f.repo, tag: TAG, commit: w, states: null }), []);
});

test('A_tag_on_the_freeze_branch_head_instead_of_C_is_refused_because_it_is_not_an_ancestor_of_W', async t => {
  const f = makeFreezeRepo(t);
  const { head, c } = freezeAndMerge(f);
  annotate(f, { at: head });
  const w = squashMerge(f, workspacesBranch(f, { from: c }), 'T2');
  const refused = await f.run('check-workspace', ['--tag', TAG, '--commit', w]);
  assert.equal(refused.exit, 1, said(refused));
  assert.match(refused.stderr, /not an ancestor/);
  assert.notEqual(head, c, 'the pre-squash commit is not C');
  assert.ok((await workspaceCommitProblems({ repo: f.repo, tag: TAG, commit: w, states: null })).some(problem => /not an ancestor/.test(problem)));
});

test('A_workspace_commit_that_is_not_a_descendant_of_the_tag_is_refused', async t => {
  const f = makeFreezeRepo(t);
  const { c } = freezeAndMerge(f);
  annotate(f, { at: c });
  const sideBranch = workspacesBranch(f, { from: f.base });
  const side = git(f.repo, 'rev-parse', sideBranch);
  const refused = await f.run('check-workspace', ['--tag', TAG, '--commit', side]);
  assert.equal(refused.exit, 1);
  assert.match(refused.stderr, /not a descendant|not an ancestor/);
});

test('A_workspace_commit_whose_spec_blob_differs_from_the_tag_is_refused_and_one_that_adds_only_workspace_files_passes', async t => {
  const f = makeFreezeRepo(t);
  const { c } = freezeAndMerge(f);
  annotate(f, { at: c });
  const slug = '07-stream-processor';
  const editedSpec = workspacesBranch(f, { from: c, files: { ...workspaceFiles(), [specPath(slug)]: `${at(f.repo, c, specPath(slug))}\nA changed line.\n` } });
  const refused = await f.run('check-workspace', ['--tag', TAG, '--commit', git(f.repo, 'rev-parse', editedSpec)]);
  assert.equal(refused.exit, 1, said(refused));
  assert.ok(refused.stderr.includes(specPath(slug)), refused.stderr);
  const editedFormat = workspacesBranch(f, { tag: 'second', from: c, files: { ...workspaceFiles(), [FORMAT_PATH]: `${formatText()}\nA changed line.\n` } });
  const formatRefused = await f.run('check-workspace', ['--tag', TAG, '--commit', git(f.repo, 'rev-parse', editedFormat)]);
  assert.equal(formatRefused.exit, 1);
  assert.ok(formatRefused.stderr.includes(FORMAT_PATH));
  const clean = workspacesBranch(f, { tag: 'third', from: c });
  assert.equal((await f.run('check-workspace', ['--tag', TAG, '--commit', git(f.repo, 'rev-parse', clean)])).exit, 0, 'only the twelve workspace folders added');
});

test('A_merge_into_main_between_C_and_W_is_refused_by_the_changed_paths_rule', async t => {
  const f = makeFreezeRepo(t);
  const { c } = freezeAndMerge(f);
  annotate(f, { at: c });
  git(f.repo, 'checkout', '-q', '-b', 'docs/other', 'main');
  commit(f.repo, 'docs: another pull request', { 'docs/other.md': 'merged between C and W\n' });
  const between = squashMerge(f, 'docs/other', 'another PR');
  const w = squashMerge(f, workspacesBranch(f, { from: between }), 'T2');
  const refused = await f.run('check-workspace', ['--tag', TAG, '--commit', w]);
  assert.equal(refused.exit, 1, said(refused));
  assert.ok(refused.stderr.includes('docs/other.md'), refused.stderr);
  const stray = workspacesBranch(f, { tag: 'folders', from: c, files: workspaceFiles({ 'csharp/01-other/x.txt': 'a folder of a task that is not one of the six\n' }) });
  const strayRefused = await f.run('check-workspace', ['--tag', TAG, '--commit', git(f.repo, 'rev-parse', stray)]);
  assert.equal(strayRefused.exit, 1);
  assert.ok(strayRefused.stderr.includes('csharp/01-other/x.txt'));
});

test('check_workspace_with_base_C_accepts_the_scratch_W_and_refuses_one_not_cut_from_C', async t => {
  const f = makeFreezeRepo(t);
  const { c } = freezeAndMerge(f);
  const scratch = git(f.repo, 'rev-parse', workspacesBranch(f, { tag: 'scratch', from: c }));
  assert.equal((await f.run('check-workspace', ['--base', c, '--commit', scratch])).exit, 0, 'no tag exists yet and the scratch W is cut from C');
  const elsewhere = git(f.repo, 'rev-parse', workspacesBranch(f, { tag: 'elsewhere', from: f.base }));
  const refused = await f.run('check-workspace', ['--base', c, '--commit', elsewhere]);
  assert.equal(refused.exit, 1);
  assert.match(refused.stderr, /not a descendant|not an ancestor/);
  assert.equal((await f.run('check-workspace', ['--commit', scratch])).exit, 2, 'neither --tag nor --base is a usage error');
  assert.equal((await f.run('check-workspace', ['--tag', TAG, '--base', c, '--commit', scratch])).exit, 2, 'both is too');
});

// ---- the tag before and after the push ---------------------------------------------------------------------------------------------------------------

const localTag = (f, c, messageFile) => f.run('check-local-tag', ['--tag', TAG, '--commit', c, '--message-file', messageFile]);

test('check_local_tag_refuses_a_tag_on_another_commit_a_lightweight_tag_and_a_changed_annotation_and_passes_the_intended_one', async t => {
  const f = makeFreezeRepo(t);
  const { c, head } = freezeAndMerge(f);
  const intended = annotate(f, { at: c });
  const passed = await localTag(f, c, intended);
  assert.equal(passed.exit, 0, said(passed));
  git(f.repo, 'tag', '-d', TAG);
  annotate(f, { at: f.base });
  const elsewhere = await localTag(f, c, intended);
  assert.equal(elsewhere.exit, 1);
  assert.ok(elsewhere.stderr.includes(f.base) && elsewhere.stderr.includes(c), elsewhere.stderr);
  git(f.repo, 'tag', '-d', TAG);
  git(f.repo, 'tag', TAG, c);
  const lightweight = await localTag(f, c, intended);
  assert.equal(lightweight.exit, 1);
  assert.match(lightweight.stderr, /lightweight|not annotated/);
  git(f.repo, 'tag', '-d', TAG);
  annotate(f, { at: c, message: MESSAGE.replace('04', '05') });
  const changed = await localTag(f, c, intended);
  assert.equal(changed.exit, 1);
  assert.match(changed.stderr, /message/);
  git(f.repo, 'tag', '-d', TAG);
  annotate(f, { at: head });
  assert.equal((await localTag(f, head, intended)).exit, 1, 'a tag on the freeze branch head: it is on no path of main');
  git(f.repo, 'tag', '-d', TAG);
  annotate(f, { at: c });
  assert.equal((await localTag(f, c, intended)).exit, 0, 'the intended tag passes again');
});

test('check_local_tag_refuses_a_tag_already_on_the_remote', async t => {
  const f = makeFreezeRepo(t);
  const { c } = freezeAndMerge(f);
  const message = annotate(f, { at: c });
  pushTag(f);
  const refused = await localTag(f, c, message);
  assert.equal(refused.exit, 1, said(refused));
  assert.match(refused.stderr, /already on the remote/);
});

test('check_local_tag_refuses_a_name_that_is_not_the_checkers_form_and_a_date_that_is_not_the_rows_date_and_a_C_that_is_not_one_commit_on_main', async t => {
  const f = makeFreezeRepo(t);
  const { c } = freezeAndMerge(f);
  const message = annotate(f, { tag: 'spec-frozen-B-20261201-r2', at: c });
  const suffixed = await f.run('check-local-tag', ['--tag', 'spec-frozen-B-20261201-r2', '--commit', c, '--message-file', message]);
  assert.equal(suffixed.exit, 1);
  assert.match(suffixed.stderr, /name/);
  const wrongDay = annotate(f, { tag: LATER_TAG, at: c });
  const dated = await f.run('check-local-tag', ['--tag', LATER_TAG, '--commit', c, '--message-file', wrongDay]);
  assert.equal(dated.exit, 1);
  assert.match(dated.stderr, /date|row/);
  git(f.repo, 'checkout', '-q', '-b', 'side', f.base);
  const sideHead = commit(f.repo, 'a commit on no remote ref', { 'side.txt': 'x\n' });
  git(f.repo, 'tag', '-a', 'spec-frozen-B-20261203', sideHead, '-m', MESSAGE);
  const unpushed = await f.run('check-local-tag', ['--tag', 'spec-frozen-B-20261203', '--commit', sideHead, '--message-file', message]);
  assert.equal(unpushed.exit, 1);
  assert.match(unpushed.stderr, /main/);
});

test('A_lightweight_tag_or_a_tag_whose_date_differs_from_the_spec_rows_is_refused', async t => {
  const f = makeFreezeRepo(t);
  const { c } = freezeAndMerge(f);
  const message = annotate(f, { at: c });
  git(f.repo, 'tag', '-d', TAG);
  git(f.repo, 'tag', TAG, c);
  const lightweight = await localTag(f, c, message);
  assert.equal(lightweight.exit, 1, said(lightweight));
  assert.match(lightweight.stderr, /lightweight/);
  git(f.repo, 'tag', '-d', TAG);
  const otherDay = annotate(f, { tag: LATER_TAG, at: c });
  const dated = await f.run('check-local-tag', ['--tag', LATER_TAG, '--commit', c, '--message-file', otherDay]);
  assert.equal(dated.exit, 1, said(dated));
  assert.match(dated.stderr, /at C say spec-frozen-B-20261201 \(date 20261201\), not spec-frozen-B-20261202/);
  pushTag(f, LATER_TAG);
  assert.equal((await f.run('check-tag', ['--tag', LATER_TAG, '--commit', c])).exit, 1, 'and after the push too');
  annotate(f, { at: c });
  assert.equal((await localTag(f, c, message)).exit, 0, 'the control: the intended annotated tag of the rows\' own date');
});

test('A_tag_that_never_reached_the_remote_may_be_deleted_and_made_again_under_the_same_name', async t => {
  const f = makeFreezeRepo(t);
  const { c } = freezeAndMerge(f);
  const first = annotate(f, { at: c });
  assert.equal((await localTag(f, c, first)).exit, 0);
  git(f.repo, 'tag', '-d', TAG);
  const second = annotate(f, { at: c, message: 'Tasks 04, 05, 06, 07, 08 and 10.\n' });
  assert.equal((await localTag(f, c, first)).exit, 1, 'the new annotation is not the first message');
  assert.equal((await localTag(f, c, second)).exit, 0, 'the same name made again passes with its own message');
  pushTag(f);
  assert.equal((await f.run('check-tag', ['--tag', TAG, '--commit', c])).exit, 0, 'and the push of the remade tag checks');
});

test('check_tag_passes_after_the_push_and_refuses_a_remote_lightweight_tag_and_a_different_annotated_tag_object', async t => {
  const f = makeFreezeRepo(t);
  const { c } = freezeAndMerge(f);
  annotate(f, { at: c });
  assert.equal((await f.run('check-tag', ['--tag', TAG, '--commit', c])).exit, 1, 'before the push the remote has no such tag');
  pushTag(f);
  const passed = await f.run('check-tag', ['--tag', TAG, '--commit', c]);
  assert.equal(passed.exit, 0, said(passed));
  git(f.repo, 'tag', '-d', TAG);
  annotate(f, { at: c, date: '2020-01-01T00:00:00Z' });
  const sameWordsOtherObject = await f.run('check-tag', ['--tag', TAG, '--commit', c]);
  assert.equal(sameWordsOtherObject.exit, 1, 'the same message under another tagger date is another annotated tag object than the one on the remote');
  assert.match(sameWordsOtherObject.stderr, /tag object .* differs from the local tag object/);
  git(f.repo, 'tag', '-d', TAG);
  annotate(f, { at: c, message: 'Tasks 04, 05, 06, 07, 08 and 10, another annotation.\n' });
  const different = await f.run('check-tag', ['--tag', TAG, '--commit', c]);
  assert.equal(different.exit, 1, 'the remote holds another annotated tag object than the local one');
  assert.match(different.stderr, /object|differ/);
  const g = makeFreezeRepo(t);
  const merged = freezeAndMerge(g);
  git(g.repo, 'tag', TAG, merged.c);
  pushTag(g);
  const lightweight = await g.run('check-tag', ['--tag', TAG, '--commit', merged.c]);
  assert.equal(lightweight.exit, 1, said(lightweight));
  assert.match(lightweight.stderr, /lightweight|not annotated/);
});

test('check_tag_refuses_a_tag_whose_date_differs_from_the_spec_rows_and_a_commit_that_is_not_the_remote_tags_target', async t => {
  const f = makeFreezeRepo(t);
  const { c } = freezeAndMerge(f);
  annotate(f, { tag: LATER_TAG, at: c });
  pushTag(f, LATER_TAG);
  const dated = await f.run('check-tag', ['--tag', LATER_TAG, '--commit', c]);
  assert.equal(dated.exit, 1);
  assert.match(dated.stderr, /date|row/);
  const g = makeFreezeRepo(t);
  const merged = freezeAndMerge(g);
  annotate(g, { at: merged.c });
  pushTag(g);
  const wrong = await g.run('check-tag', ['--tag', TAG, '--commit', g.base]);
  assert.equal(wrong.exit, 1);
  assert.match(wrong.stderr, /peeled|points at/);
});

test('check_tag_refuses_a_spec_blob_that_is_not_the_gated_spec_with_only_its_row_set_and_passes_the_plain_freeze', async t => {
  const f = makeFreezeRepo(t);
  const head = freezeWithExtra(f, edited => ({ [specPath('06-blog-api')]: edited.replace('For n = 1', 'For n = 2') }));
  const c = squashMerge(f, `freeze/${TAG}`, 'T1');
  annotate(f, { at: c });
  pushTag(f);
  const refused = await f.run('check-tag', ['--tag', TAG, '--commit', c]);
  assert.equal(refused.exit, 1, said(refused));
  assert.ok(refused.stderr.includes(specPath('06-blog-api')), refused.stderr);
  assert.notEqual(head, c);
  const g = makeFreezeRepo(t);
  const plain = freezeAndMerge(g);
  annotate(g, { at: plain.c });
  pushTag(g);
  assert.equal((await g.run('check-tag', ['--tag', TAG, '--commit', plain.c])).exit, 0, 'the control');
});

test('check_tag_refuses_a_FORMAT_whose_blob_differs_from_the_step_2a_pin_even_when_the_freeze_commit_is_self_consistent', async t => {
  const pinnedFormat = formatText();
  const changedFormat = `${pinnedFormat}\nOne more line of the changelog, added before the freeze.\n`;
  const f = makeFreezeRepo(t, { files: baseFiles({ format: changedFormat }), record: undefined, format: pinnedFormat });
  const { c } = freezeAndMerge(f);
  annotate(f, { at: c });
  pushTag(f);
  assert.equal((await f.run('check-commit', ['--before', f.base, '--after', c])).exit, 0, 'the freeze commit alone is self-consistent: FORMAT is the same before and after');
  const refused = await f.run('check-tag', ['--tag', TAG, '--commit', c]);
  assert.equal(refused.exit, 1, said(refused));
  assert.ok(refused.stderr.includes(sha256(pinnedFormat)) && refused.stderr.includes(sha256(changedFormat)), 'names both hashes');
  const g = makeFreezeRepo(t);
  const control = freezeAndMerge(g);
  annotate(g, { at: control.c });
  pushTag(g);
  assert.equal((await g.run('check-tag', ['--tag', TAG, '--commit', control.c])).exit, 0, 'the pinned bytes pass');
});

test('A_superseding_tag_name_with_a_later_date_passes_the_checkers_pattern', () => {
  const parsed = parseFormat(Buffer.from(formatText()));
  assert.equal(parsed.ok, true);
  const facts = readFacts(parsed.format);
  assert.equal(facts.ok, true);
  const accepts = name => laterForm('Freeze tag', facts.value)(`\`${name}\``);
  assert.equal(accepts(TAG), true);
  assert.equal(accepts(LATER_TAG), true, 'the day after is accepted');
  assert.equal(accepts(`${TAG}-r2`), false, 'a suffixed name is refused, which is why the recovery never uses one');
});

test('A_superseding_freeze_commit_changes_only_the_six_rows_against_C', async t => {
  const f = makeFreezeRepo(t);
  const { c } = freezeAndMerge(f);
  annotate(f, { at: c });
  pushTag(f);
  freezeBranch(f, { tag: LATER_TAG, from: 'main' });
  const c2 = squashMerge(f, `freeze/${LATER_TAG}`, 'T1 again');
  const passed = await f.run('check-commit', ['--before', c, '--after', c2]);
  assert.equal(passed.exit, 0, said(passed));
  freezeBranch(f, { tag: TAG, from: 'main', more: {} });
  const sameDay = squashMerge(f, `freeze/${TAG}`, 'T1 the same day');
  assert.equal((await f.run('check-commit', ['--before', c2, '--after', sameDay])).exit, 1, 'an earlier date than the row it replaces');
});

// ---- the hash list -----------------------------------------------------------------------------------------------------------------------------------

test('The_hash_list_of_a_tree_lists_every_file_under_the_workspace_roots_and_fetchTaskRepo_accepts_it', async t => {
  const f = makeFreezeRepo(t);
  const { w } = fullFreeze(f);
  const listed = await f.run('hashes', ['--commit', w]);
  assert.equal(listed.exit, 0, said(listed));
  const lines = listed.stdout.trimEnd().split('\n');
  assert.deepEqual([...lines].sort((a, b) => (a.slice(66) < b.slice(66) ? -1 : 1)), lines, 'sorted by path');
  const expected = git(f.repo, 'ls-tree', '-r', '--name-only', w).split('\n').filter(file => /^(csharp|node|runner|tasks)\//.test(file) || file === FORMAT_PATH || SLUGS.map(specPath).includes(file));
  assert.deepEqual(lines.map(line => line.slice(66)), expected.sort(), 'every file under the roots, FORMAT and the six specs, nothing else');
  assert.match(lines[0], /^[0-9a-f]{64} {2}\S+$/);
  assert.equal(lines.find(line => line.endsWith('csharp/04-rule-api/STATEMENT.md')).slice(0, 64), sha256(at(f.repo, w, 'csharp/04-rule-api/STATEMENT.md') + '\n'));
  const fetched = await fetchTaskRepo({ sha: w, cacheDir: ownedFolder(t, 'cache'), repo: f.repo, hashesText: listed.stdout });
  assert.equal(fetched.ok, true, fetched.reason);
});

test('A_file_added_after_the_list_was_written_is_refused_by_the_fetch', async t => {
  const f = makeFreezeRepo(t);
  const { w } = fullFreeze(f);
  const listed = await f.run('hashes', ['--commit', w]);
  const later = commit(f.repo, 'a file added after the list was written', { 'node/04-rule-api/added.txt': 'late\n' });
  const refused = await fetchTaskRepo({ sha: later, cacheDir: ownedFolder(t, 'cache'), repo: f.repo, hashesText: listed.stdout });
  assert.equal(refused.ok, false);
  assert.match(refused.reason, /node\/04-rule-api\/added\.txt \(not on the hash list\)/);
  assert.equal((await f.run('hashes', ['--commit', later])).stdout === listed.stdout, false, 'the list of the later commit names it');
});

test('The_hash_list_reads_the_blobs_of_the_commit_and_a_modified_tracked_file_or_an_untracked_file_in_the_checkout_change_nothing', async t => {
  const f = makeFreezeRepo(t);
  const { w } = fullFreeze(f);
  const before = await f.run('hashes', ['--commit', w]);
  writeFileSync(path.join(f.repo, 'csharp/04-rule-api/STATEMENT.md'), 'edited in the checkout\n');
  writeFileSync(path.join(f.repo, 'node/04-rule-api/untracked.txt'), 'not committed\n');
  const after = await f.run('hashes', ['--commit', w]);
  assert.equal(after.exit, 0, said(after));
  assert.equal(after.stdout, before.stdout);
  assert.equal(after.stdout.includes('untracked.txt'), false);
});

test('The_hash_list_refuses_a_link_under_a_root_and_a_commit_that_does_not_exist', async t => {
  const f = makeFreezeRepo(t);
  const { c } = freezeAndMerge(f);
  git(f.repo, 'checkout', '-q', '-b', 'links', c);
  writeFileSync(path.join(f.repo, 'real.txt'), 'x\n');
  commit(f.repo, 'a file', {});
  git(f.repo, 'update-index', '--add', '--cacheinfo', `120000,${git(f.repo, 'hash-object', '-w', 'real.txt')},csharp/04-rule-api/link`);
  git(f.repo, 'commit', '-q', '-m', 'a link under a root');
  const refused = await f.run('hashes', ['--commit', git(f.repo, 'rev-parse', 'HEAD')]);
  assert.equal(refused.exit, 3, said(refused));
  assert.match(refused.stderr, /csharp\/04-rule-api\/link.*link|link.*csharp\/04-rule-api\/link/s);
  assert.equal((await f.run('hashes', ['--commit', '0'.repeat(40)])).exit, 3);
});

// ---- the command ---------------------------------------------------------------------------------------------------------------------------------------

const FREEZE = fileURLToPath(new URL('../freeze.mjs', import.meta.url));
const command = args => spawnSync(process.execPath, [FREEZE, ...args], { encoding: 'utf8', timeout: 120_000, killSignal: 'SIGKILL' });

test('The_command_prints_the_hash_list_and_a_plan_and_exits_2_for_usage_and_3_for_a_refusal', async t => {
  const f = makeFreezeRepo(t);
  const { w } = fullFreeze(f);
  const listed = command(['hashes', '--repo', f.repo, '--commit', w]);
  assert.equal(listed.status, 0, listed.stderr);
  assert.equal(listed.stdout, (await f.run('hashes', ['--commit', w])).stdout, 'the process prints what the function returns');
  assert.equal(command([]).status, 2, 'no verb');
  assert.equal(command(['no-such-verb']).status, 2);
  assert.equal(command(['hashes', '--repo', f.repo]).status, 2, 'a missing flag');
  assert.equal(command(['check-commit', '--repo', f.repo, '--before', 'a', '--after', 'b', '--write']).status, 2, 'a flag of another verb');
  assert.match(command(['hashes', '--repo', f.repo]).stderr, /missing --commit/);
  const refused = command(['hashes', '--repo', f.repo, '--commit', '0'.repeat(40)]);
  assert.equal(refused.status, 3);
  assert.doesNotMatch(refused.stderr, /\n\s+at /, 'a sentence, not a stack trace');
  assert.equal(command(['hashes', '--repo', path.join(f.root, 'no-such-repo'), '--commit', w]).status, 4, 'a repository git cannot read is the environment\'s');
});

// ---- a spec edited after its gate cannot be frozen ---------------------------------------------------------------------------------------------------

/** A repository whose spec of task 06 holds a changed line while the engine's pin names the gated text's sha256. */
const editedAfterGate = t => {
  const gated = baseFiles();
  const edited = baseFiles({ specs: { '06': specText('06').replace('**R2.** For n = 1', '**R2.** For n = 2') } });
  return makeFreezeRepo(t, { files: edited, pinned: gated });
};

test('A_spec_edited_after_its_gate_is_refused_by_plan_check_commit_and_check_tag_and_the_pinned_bytes_pass', async t => {
  const f = editedAfterGate(t);
  const planned = await f.run('plan', ['--ref', 'HEAD', '--date', '20261201']);
  assert.equal(planned.exit, 3, said(planned));
  assert.ok(planned.stderr.includes(specPath('06-blog-api')) && /packages\.json/.test(planned.stderr) && /specSha256/.test(planned.stderr) && /gate/.test(planned.stderr), planned.stderr);
  assert.equal(planned.stdout, '');
  const { c } = freezeAndMerge(f);
  const commitCheck = await f.run('check-commit', ['--before', f.base, '--after', c]);
  assert.equal(commitCheck.exit, 1, said(commitCheck));
  assert.ok(commitCheck.stderr.includes(specPath('06-blog-api')) && /packages\.json/.test(commitCheck.stderr) && /specSha256/.test(commitCheck.stderr), commitCheck.stderr);
  assert.equal(commitCheck.stderr.includes(specPath('05-key-value-server')), false, 'only the edited spec is named');
  annotate(f, { at: c });
  pushTag(f);
  const tagCheck = await f.run('check-tag', ['--tag', TAG, '--commit', c]);
  assert.equal(tagCheck.exit, 1, said(tagCheck));
  assert.ok(tagCheck.stderr.includes(specPath('06-blog-api')));
  const g = makeFreezeRepo(t);
  assert.equal((await g.run('plan', ['--ref', 'HEAD', '--date', '20261201'])).exit, 0, 'the control: the pinned bytes plan');
});

test('The_gated_hashes_are_read_from_the_registered_packages_and_a_missing_or_unreadable_file_or_task_is_a_refusal', async t => {
  const f = makeFreezeRepo(t);
  const { c } = freezeAndMerge(f);
  const run = pins => runFreeze(['check-commit', '--repo', f.repo, '--inputs', f.inputs, '--pins', pins, '--before', f.base, '--after', c], { now: () => TODAY });
  assert.equal((await run(f.pins)).exit, 0);
  assert.equal((await run(path.join(f.root, 'absent.json'))).exit, 1);
  writeFileSync(path.join(f.root, 'no-package.json'), '{"04": {}}');
  const missing = await run(path.join(f.root, 'no-package.json'));
  assert.equal(missing.exit, 1);
  assert.match(missing.stderr, /no-package\.json.*specSha256 for task 04/);
});

// ---- the review's findings of PR 4: the guards read the record, the tag and the modes -------------------------------------------------------------------

test('A_guard_called_without_states_reads_the_record_itself_and_refuses_a_superseded_tag_or_the_absence_of_a_record', async t => {
  const f = makeFreezeRepo(t);
  const { c } = fullFreeze(f);
  const pinned = { repo: f.repo, tag: TAG, path: specPath('05-key-value-server'), sha256: sha256(`${at(f.repo, c, specPath('05-key-value-server'))}\n`), inputs: f.inputs };
  const absent = await specBlobMatches(pinned);
  assert.equal(absent.ok, false, 'no record, no explicit null: the freeze is not accepted');
  assert.match(absent.reason, /spec-states\.json/);
  assert.ok((await workspaceCommitProblems({ repo: f.repo, tag: TAG, commit: c, inputs: f.inputs })).length > 0);
  const states = { freeze: { tag: LATER_TAG, superseded: [{ tag: TAG, tagObjectSha: 'a'.repeat(40), why: 'a cause was fixed' }] } };
  writeFileSync(path.join(f.inputs, 'specs.d', 'spec-states.json'), JSON.stringify(states));
  const refused = await specBlobMatches(pinned);
  assert.equal(refused.ok, false);
  assert.match(refused.reason, new RegExp(`${TAG}.*superseded`));
  assert.ok((await workspaceCommitProblems({ repo: f.repo, tag: TAG, commit: c, inputs: f.inputs })).some(problem => /superseded/.test(problem)));
  assert.equal((await specBlobMatches({ ...pinned, states: null })).ok, true, 'an explicit null is the freeze in progress');
});

test('A_guard_given_the_record_requires_the_local_tag_to_be_the_recorded_object_and_commit', async t => {
  const f = makeFreezeRepo(t);
  const { c } = fullFreeze(f);
  const call = states => specBlobMatches({ repo: f.repo, tag: TAG, path: specPath('05-key-value-server'), sha256: sha256(`${at(f.repo, c, specPath('05-key-value-server'))}\n`), states });
  const object = git(f.repo, 'rev-parse', `refs/tags/${TAG}`);
  assert.equal((await call({ freeze: { tag: TAG, tagObjectSha: object, commitSha: c, superseded: [] } })).ok, true);
  const otherObject = await call({ freeze: { tag: TAG, tagObjectSha: 'b'.repeat(40), commitSha: c, superseded: [] } });
  assert.equal(otherObject.ok, false);
  assert.match(otherObject.reason, /tag object/);
  const otherCommit = await call({ freeze: { tag: TAG, tagObjectSha: object, commitSha: f.base, superseded: [] } });
  assert.equal(otherCommit.ok, false);
  assert.match(otherCommit.reason, /commit/);
  assert.ok((await workspaceCommitProblems({ repo: f.repo, tag: TAG, commit: c, states: { freeze: { tag: TAG, tagObjectSha: 'b'.repeat(40), commitSha: c, superseded: [] } } })).some(problem => /tag object/.test(problem)));
});

test('check_tag_refuses_when_there_is_no_local_tag_even_with_a_message_file', async t => {
  const f = makeFreezeRepo(t);
  const { c } = freezeAndMerge(f);
  const message = annotate(f, { at: c });
  pushTag(f);
  assert.equal((await f.run('check-tag', ['--tag', TAG, '--commit', c, '--message-file', message])).exit, 0, 'with the local tag it passes');
  git(f.repo, 'tag', '-d', TAG);
  const refused = await f.run('check-tag', ['--tag', TAG, '--commit', c, '--message-file', message]);
  assert.equal(refused.exit, 1, said(refused));
  assert.match(refused.stderr, /no local tag/);
});

test('A_mode_change_of_a_spec_in_the_freeze_commit_or_of_a_spec_or_FORMAT_in_W_is_refused', async t => {
  const f = makeFreezeRepo(t);
  const spec = path.join(f.repo, specPath('06-blog-api'));
  const edited = freezeBranch(f, { more: {} });
  chmodSync(spec, 0o755);
  git(f.repo, 'add', '-A');
  git(f.repo, 'commit', '-q', '-m', 'the same bytes with the execute bit');
  const executable = git(f.repo, 'rev-parse', 'HEAD');
  const refused = await f.run('check-commit', ['--before', f.base, '--after', executable]);
  assert.equal(refused.exit, 1, said(refused));
  assert.ok(refused.stderr.includes(specPath('06-blog-api')) && /mode/.test(refused.stderr), refused.stderr);
  assert.equal((await f.run('check-commit', ['--before', f.base, '--after', edited])).exit, 0, 'the control: the six rows alone');
  const g = makeFreezeRepo(t);
  const { c } = freezeAndMerge(g);
  annotate(g, { at: c });
  for (const file of [specPath('07-stream-processor'), FORMAT_PATH]) {
    git(g.repo, 'checkout', '-q', '-B', 'modes', c);
    chmodSync(path.join(g.repo, file), 0o755);
    writeFileSync(path.join(g.repo, 'csharp/04-rule-api-extra.txt'), 'x\n');
    git(g.repo, 'add', '-A');
    git(g.repo, 'commit', '-q', '-m', `a mode change of ${file}`);
    const mode = await g.run('check-workspace', ['--tag', TAG, '--commit', git(g.repo, 'rev-parse', 'HEAD')]);
    assert.equal(mode.exit, 1, `${file}: ${said(mode)}`);
    assert.ok(mode.stderr.includes(file) && /mode/.test(mode.stderr), mode.stderr);
  }
});
