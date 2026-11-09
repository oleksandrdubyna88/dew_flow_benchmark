// `leakcheck` over BOTH repositories (build step 2b; merge conditions 3 and 5): the bench repository and the task repository, with the planted-needle check. Every path either finds the leak or refuses: no task repository given,
// a path that is not a repository's top folder, a base that cannot be resolved, a shallow repository, a history that is not all present. The repositories are `git init` folders in temp homes holding an INVENTED needle that a
// temp private folder also holds; the real repositories and the real private folders are never read here.
import '../../minigit-bench/test/fake-home.mjs';
import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { rmSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { NEEDLE, NEEDLE_LINES, commit, git, holdsNeedle, newRepo, privateSide, runCheck } from './quick-leak-repos-fixture.mjs';

const clean = () => newRepo({ 'README.md': 'a task repository readme\n', 'tasks/04/notes.md': 'a public note\n' });
const run = (benchRepo, values, env) => runCheck({ ...privateSide(), benchRepo, values, env });
const TASK_CLEAN = /\[task repository [^\]]*\] clean/;

test('Leakcheck_refuses_when_no_task_repository_is_given_by_the_option_or_the_environment', async () => {
  const r = await run(newRepo(), {}, {});
  assert.equal(r.exit, 4, r.said);
  assert.match(r.said, /--task-repo/);
  assert.match(r.said, /LANG_V2_TASK_REPO/);
  assert.doesNotMatch(r.said, /clean/, 'a check that scans one repository is never reported as clean');
});

test('The_refusal_without_a_task_repository_also_names_the_candidate_alone_as_a_valid_call', async () => {
  const r = await run(newRepo(), {}, {});
  assert.equal(r.exit, 4, r.said);
  assert.match(r.said, /--candidate-repo <path> --candidate <commit>/, 'a candidate alone needs no task repository, and the refusal says so');
  assert.match(r.said, /--task-repo <path>/);
  assert.match(r.said, /LANG_V2_TASK_REPO/);
});

test('The_environment_variable_names_the_task_repository_when_the_option_is_absent_and_the_option_wins', async () => {
  const leaky = newRepo({ 'notes/x.md': NEEDLE });
  const side = privateSide();
  const bench = newRepo();
  assert.equal((await runCheck({ ...side, benchRepo: bench, env: { LANG_V2_TASK_REPO: leaky } })).exit, 5, 'the variable alone names the repository to scan');
  assert.equal((await runCheck({ ...side, benchRepo: bench, values: { 'task-repo': clean() }, env: { LANG_V2_TASK_REPO: leaky } })).exit, 0, 'the option wins over the variable');
});

test('A_task_repository_that_is_missing_not_a_repository_not_a_top_folder_or_the_bench_itself_is_refused', async () => {
  const bench = newRepo();
  const inside = clean();
  const cases = { missing: path.join(tmpDir('r-none'), 'nope'), 'not a repository': tmpDir('r-plain'), 'a sub folder': path.join(inside, 'tasks'), 'the bench': bench };
  for (const [name, value] of Object.entries(cases)) {
    const r = await run(bench, { 'task-repo': value }, {});
    assert.equal(r.exit, 4, `${name}: ${r.said}`);
    assert.doesNotMatch(r.said, /clean/, name);
  }
});

test('A_needle_in_the_task_repository_fails_leakcheck_over_both_repositories_naming_it_and_the_same_run_without_it_passes', async () => {
  const side = privateSide();
  const bench = newRepo();
  const dirty = await runCheck({ ...side, benchRepo: bench, values: { 'task-repo': newRepo({ 'README.md': 'a readme\n', 'tasks/04/notes.md': `a note\n${NEEDLE}` }) } });
  assert.equal(dirty.exit, 5, dirty.said);
  assert.match(dirty.said, /\[task repository [^\]]*\] FAILED: held-out text found in/);
  assert.match(dirty.said, /tasks\/04\/notes\.md/, 'the place is named');
  assert.match(dirty.said, /\[bench repository\] clean/, 'the bench repository is scanned too, and is clean');
  assert.equal(holdsNeedle(dirty.said), false, 'never the text');
  const control = await runCheck({ ...side, benchRepo: bench, values: { 'task-repo': clean() } });
  assert.equal(control.exit, 0, control.said);
  assert.match(control.said, /\[bench repository\] clean/);
  assert.match(control.said, /\[task repository [^\]]*\] clean/);
});

test('A_needle_in_the_bench_repository_is_exit_5_naming_the_bench_while_the_task_repository_is_clean', async () => {
  const r = await run(newRepo({ 'README.md': 'a readme\n', 'docs/x.md': NEEDLE }), { 'task-repo': clean() });
  assert.equal(r.exit, 5, r.said);
  assert.match(r.said, /\[bench repository\] FAILED/);
  assert.match(r.said, /\[task repository [^\]]*\] clean/);
});

test('A_needle_that_was_committed_on_a_local_branch_and_removed_again_is_found_in_the_history_with_no_working_tree_trace', async () => {
  const leaky = clean();
  git(leaky, 'checkout', '-q', '-b', 'topic');
  commit(leaky, { 'oops.md': NEEDLE }, 'a leak');
  commit(leaky, { 'oops.md': null }, 'the leak removed');
  git(leaky, 'checkout', '-q', 'main');
  const r = await run(newRepo(), { 'task-repo': leaky });
  assert.equal(r.exit, 5, r.said);
  assert.match(r.said, /task repository/);
  assert.match(r.said, /patches of the local branches/, 'the history, not a file');
  assert.equal(holdsNeedle(r.said), false);
  const control = clean();
  git(control, 'checkout', '-q', '-b', 'topic');
  commit(control, { 'fine.md': 'fine\n' }, 'fine');
  git(control, 'checkout', '-q', 'main');
  assert.equal((await run(newRepo(), { 'task-repo': control })).exit, 0);
});

test('A_commit_that_origin_main_holds_is_not_scanned_and_an_explicit_task_base_widens_the_history', async () => {
  const repo = clean();
  const first = git(repo, 'rev-parse', 'HEAD').trim();
  commit(repo, { 'oops.md': NEEDLE }, 'a leak');
  commit(repo, { 'oops.md': null }, 'removed');
  git(repo, 'update-ref', 'refs/remotes/origin/main', 'HEAD');
  assert.equal((await run(newRepo(), { 'task-repo': repo })).exit, 0, 'the default base is origin/main: what it holds is published already');
  const r = await run(newRepo(), { 'task-repo': repo, 'task-base': first });
  assert.equal(r.exit, 5, r.said);
  assert.match(r.said, /patches of the local branches/);
});

test('A_base_that_cannot_be_resolved_scans_every_commit_of_every_local_branch_says_so_and_never_crashes', async () => {
  const leaky = newRepo({ 'README.md': 'a readme\n', 'oops.md': NEEDLE }, { published: false });
  commit(leaky, { 'oops.md': null }, 'removed');
  const noOrigin = await run(newRepo(), { 'task-repo': leaky });
  assert.equal(noOrigin.exit, 5, noOrigin.said);
  assert.match(noOrigin.said, /origin\/main cannot be resolved: every commit reachable from every local branch was scanned/);
  const named = await run(newRepo(), { 'task-repo': leaky, 'task-base': 'no-such-ref' });
  assert.equal(named.exit, 5, named.said);
  assert.match(named.said, /no-such-ref cannot be resolved: every commit reachable from every local branch was scanned/);
  const control = await run(newRepo(), { 'task-repo': newRepo({ 'README.md': 'a readme\n' }, { published: false }) });
  assert.equal(control.exit, 0, control.said);
  assert.match(control.said, /cannot be resolved: every commit reachable from every local branch was scanned/, 'a clean result says how much it read');
});

test('A_task_base_that_looks_like_an_option_is_refused', async () => {
  const r = await run(newRepo(), { 'task-repo': clean(), 'task-base': '--all' });
  assert.equal(r.exit, 4, r.said);
});

test('An_untracked_file_a_modified_file_and_a_staged_change_with_the_needle_are_found_before_any_commit', async () => {
  const untracked = clean();
  writeFileSync(path.join(untracked, 'draft.md'), NEEDLE);
  const u = await run(newRepo(), { 'task-repo': untracked });
  assert.equal(u.exit, 5, u.said);
  assert.match(u.said, /draft\.md \(working tree\)/);
  const modified = clean();
  writeFileSync(path.join(modified, 'README.md'), `a task repository readme\n${NEEDLE}`);
  const m = await run(newRepo(), { 'task-repo': modified });
  assert.equal(m.exit, 5, m.said);
  assert.match(m.said, /README\.md \(working tree\)/);
  const staged = clean();
  writeFileSync(path.join(staged, 'staged.md'), NEEDLE);
  git(staged, 'add', 'staged.md');
  writeFileSync(path.join(staged, 'staged.md'), 'the file on disk is clean again\n');
  const s = await run(newRepo(), { 'task-repo': staged });
  assert.equal(s.exit, 5, s.said);
  assert.match(s.said, /staged changes/);
  for (const r of [u, m, s]) assert.equal(holdsNeedle(r.said), false, 'never the text');
  assert.equal((await run(newRepo(), { 'task-repo': clean() })).exit, 0, 'the control: no change, no finding');
});

test('A_shallow_task_repository_is_a_refusal_and_a_full_clone_of_the_same_one_is_scanned', async () => {
  const source = clean();
  commit(source, { 'second.md': 'a second commit\n' }, 'second');
  const clone = (...flags) => { const dest = path.join(tmpDir('r-clone'), 'clone'); execFileSync('git', ['clone', '-q', ...flags, `file://${source}`, dest]); return dest; };
  const shallow = await run(newRepo(), { 'task-repo': clone('--depth', '1') });
  assert.equal(shallow.exit, 4, shallow.said);
  assert.match(shallow.said, /shallow/);
  assert.doesNotMatch(shallow.said, TASK_CLEAN, 'never a clean result over history it could not read');
  const full = await run(newRepo(), { 'task-repo': clone() });
  assert.equal(full.exit, 0, full.said);
});

test('A_history_that_is_not_all_present_is_a_refusal_never_a_clean_result', async () => {
  const build = () => { const repo = clean(); commit(repo, { 'later.md': 'a later file with its own text\n' }, 'later'); return repo; };
  const broken = build();
  const oid = git(broken, 'rev-parse', 'HEAD:later.md').trim();
  rmSync(path.join(broken, '.git', 'objects', oid.slice(0, 2), oid.slice(2)));
  git(broken, 'update-ref', 'refs/remotes/origin/main', 'HEAD~1');
  const r = await run(newRepo(), { 'task-repo': broken });
  assert.equal(r.exit, 4, r.said);
  assert.match(r.said, /not all present/);
  assert.doesNotMatch(r.said, TASK_CLEAN, 'never a clean result over history it could not read');
  const control = build();
  git(control, 'update-ref', 'refs/remotes/origin/main', 'HEAD~1');
  assert.equal((await run(newRepo(), { 'task-repo': control })).exit, 0, 'the same repository with every object there is scanned');
});

test('A_task_repository_with_no_commit_is_scanned_for_its_working_tree_and_does_not_crash', async () => {
  const empty = tmpDir('r-unborn');
  git(empty, 'init', '-q', '-b', 'main');
  const fine = await run(newRepo(), { 'task-repo': empty });
  assert.equal(fine.exit, 0, fine.said);
  assert.match(fine.said, /no commit/);
  writeFileSync(path.join(empty, 'draft.md'), NEEDLE);
  assert.equal((await run(newRepo(), { 'task-repo': empty })).exit, 5, 'an unborn branch hides no file of the working tree');
});

test('A_needle_committed_at_HEAD_is_found_in_its_tree_even_when_the_working_copy_no_longer_holds_it', async () => {
  const repo = clean();
  commit(repo, { 'notes.md': NEEDLE }, 'a leak');
  git(repo, 'update-ref', 'refs/remotes/origin/main', 'HEAD');
  writeFileSync(path.join(repo, 'notes.md'), 'the working copy is clean again\n');
  const r = await run(newRepo(), { 'task-repo': repo });
  assert.equal(r.exit, 5, r.said);
  assert.match(r.said, /HEAD:notes\.md/, 'the tracked file at HEAD is read from git, not from the disk');
  assert.equal(holdsNeedle(r.said), false);
});

test('A_tracked_file_that_is_gone_from_the_working_tree_makes_the_scan_incomplete_exit_4_and_a_finding_beside_it_is_still_exit_5', async () => {
  const repo = clean();
  rmSync(path.join(repo, 'tasks', '04', 'notes.md'));
  const gone = await run(newRepo(), { 'task-repo': repo });
  assert.equal(gone.exit, 4, gone.said);
  assert.match(gone.said, /the scan is incomplete: 1 file\(s\) could not be read \(tasks\/04\/notes\.md \(working tree\)\)/);
  assert.doesNotMatch(gone.said, TASK_CLEAN, 'a check that skipped a file is not clean');
  writeFileSync(path.join(repo, 'draft.md'), NEEDLE);
  assert.equal((await run(newRepo(), { 'task-repo': repo })).exit, 5, 'a finding beside a gap is a finding');
});

test('The_leak_lines_are_the_needle_of_the_fixture_and_the_fixture_is_the_one_both_sides_hold', () => {
  assert.ok(NEEDLE_LINES.every(l => NEEDLE.includes(l)));
});
