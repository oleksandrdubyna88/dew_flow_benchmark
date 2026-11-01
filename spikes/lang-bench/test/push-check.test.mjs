// The pre-push check over a branch, a text body and the bench's own branch: the named lines, the exit codes, and the promise that no
// report line carries the text it matched. Everything runs over fixture repositories with a bare `origin` and a synthetic aid.
import assert from 'node:assert/strict';
import { writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { checkBranch, checkSelf, checkText, EXIT, formatReport } from '../lib/push-check.mjs';
import { commit, git, makeOrigin, makeTaskRepo } from './support/push-fixtures.mjs';
import { loadInputsForTest, realCredits } from './support/push-inputs.mjs';

const AID_TEXT = 'Header.\nA widget that frobnicates every sprocket twice before lunch will stall.\nFooter.\n';
const SECRET = `${'github_pat'}_${'aB3dE5gH7jK9mN1pQ3sT5vW7yZ'}_${'aB3dE5gH7jK9mN1pQ3sT5v'}`;
const inputs = loadInputsForTest({ aidText: AID_TEXT });
const credits = realCredits();
const lines = result => formatReport(result).split('\n');
const checkOf = (result, name) => result.checks.find(c => c.name === name);

/** A task repository with an origin and a branch `topic` off the remote's main, holding what `files` says. */
function branchWith(t, files, { message = 'feat: add files' } = {}) {
  const repo = makeTaskRepo(t, { origin: makeOrigin(t) });
  git(repo, 'checkout', '-q', '-b', 'topic', 'origin/main');
  const sha = commit(repo, message, files);
  return { repo, sha };
}
const run = (repo, extra = {}) => checkBranch({ repo, branch: 'topic', inputs: { ...inputs, ...extra } });

test('A_clean_branch_passes_every_named_check_with_exit_0', async t => {
  const { repo } = branchWith(t, { 'tasks/01/s01.in': '5\n', 'research/architecture.md': 'notes\n' });
  const result = await run(repo);
  assert.equal(result.exit, EXIT.PASS);
  assert.deepEqual(result.checks.map(c => c.name), ['leakcheck', 'secrets', 'attribution', 'agent-visible-paths', 'family-names', 'known-task-credits', 'stack-folders']);
  assert.ok(result.checks.every(c => c.findings.length === 0));
  assert.match(lines(result).join('\n'), /^leakcheck +PASS/m);
  assert.match(lines(result).at(-2), /^result +PASS \(exit 0\)/);
});

test('Push_check_refuses_known_task_text_without_credit_terms', async t => {
  const { repo } = branchWith(t, { 'todo/tasks/05-key-value-server.md': '# Task 05\n\nA server.\n' });
  const result = await checkBranch({ repo, branch: 'topic', inputs: { ...inputs, credits: { ok: false, reason: 'not recorded' } } });
  assert.equal(result.exit, EXIT.REFUSED);
  assert.match(lines(result).join('\n'), /known-task-credits +FAIL[\s\S]*todo\/tasks\/05-key-value-server\.md: known-task text without its credit terms/);
});

test('Push_check_reports_an_aid_shingle_in_an_old_commit_by_commit_path_and_line_and_never_by_text', async t => {
  const { repo, sha } = branchWith(t, { 'notes/x.md': 'one\ntwo\nthe widget that frobnicates every sprocket twice before dinner\n' });
  commit(repo, 'refactor: remove it again', { 'notes/x.md': null });
  const result = await run(repo);
  assert.equal(result.exit, EXIT.REFUSED);
  assert.deepEqual(checkOf(result, 'leakcheck').findings.map(f => [f.commit, f.path, f.line, f.what]), [[sha, 'notes/x.md', 3, 'shingle of synthetic-aid.md']]);
  const report = formatReport(result);
  assert.match(report, new RegExp(`${sha.slice(0, 10)} notes/x\\.md:3 shingle of synthetic-aid\\.md`));
  assert.equal(/frobnicates|sprocket|widget/i.test(report), false, 'the report holds no word of the aid');
});

test('Push_check_reports_a_secret_shape_by_commit_path_line_and_name_and_never_by_text', async t => {
  const { repo, sha } = branchWith(t, { 'cfg/app.ini': `[a]\nkey = ${SECRET}\n` });
  const result = await run(repo);
  assert.equal(result.exit, EXIT.REFUSED);
  assert.deepEqual(checkOf(result, 'secrets').findings.map(f => [f.commit, f.path, f.line, f.what]), [[sha, 'cfg/app.ini', 2, 'github-fine-grained-pat']]);
  assert.equal(formatReport(result).includes(SECRET.slice(0, 16)), false);
});

test('A_secret_in_a_commit_message_is_found_too', async t => {
  const { repo, sha } = branchWith(t, { 'a.txt': 'x\n' }, { message: `feat: add a\n\nthe token was ${SECRET}` });
  const result = await run(repo);
  assert.deepEqual(checkOf(result, 'secrets').findings.map(f => [f.commit, f.path, f.what]), [[sha, '(commit message)', 'github-fine-grained-pat']]);
});

test('Push_check_refuses_a_source_file_under_a_stack_folder_outside_the_template_set', async t => {
  const { repo } = branchWith(t, { 'csharp/01/Program.cs': 'class P {}\n', 'csharp/.gitkeep': '' });
  const result = await run(repo);
  assert.equal(result.exit, EXIT.REFUSED);
  assert.deepEqual(checkOf(result, 'stack-folders').findings.map(f => f.path), ['csharp/01/Program.cs']);
});

test('The_family_names_are_needles_except_in_gitmodules', async t => {
  const { repo } = branchWith(t, { 'runner/note.py': '# see dew_flow_benchmark\n', '.gitmodules': '[submodule "c"]\n\turl = https://github.com/o/dew_flow_conventions.git\n' });
  const result = await run(repo);
  assert.deepEqual(checkOf(result, 'family-names').findings.map(f => [f.path, f.line, f.what]), [['runner/note.py', 1, 'needle dew_flow_benchmark']]);
});

test('A_credit_planted_in_a_spec_or_the_readme_outside_the_attribution_section_is_refused', async t => {
  const readme = '# T\n\nSee https://example.org/p for details.\n\n## Attribution\n\nBased on CSES, https://cses.fi/x\n';
  const { repo } = branchWith(t, { 'README.md': readme, 'tasks/01/NOTES.md': 'adapted from CSES\n' });
  const result = await run(repo);
  assert.deepEqual(checkOf(result, 'attribution').findings.map(f => [f.path, f.line, f.what]), [['README.md', 3, 'link']]);
  assert.deepEqual(checkOf(result, 'agent-visible-paths').findings.map(f => [f.path, f.what]), [['tasks/01/NOTES.md', 'needle CSES']]);
});

test('A_branch_carrying_a_known_task_spec_needs_its_credit_lines_in_the_readme_at_the_tip', async t => {
  const bare = branchWith(t, { 'todo/tasks/09-switched-routes.md': '# Task 09\n' });
  const refused = await run(bare.repo, { credits });
  assert.equal(refused.exit, EXIT.REFUSED);
  assert.ok(checkOf(refused, 'known-task-credits').findings.some(f => f.what === 'the README is missing: the credit terms cannot be checked'));
  writeFileSync(path.join(bare.repo, 'README.md'), '# T\n\n## Attribution\n\nbased on IOI 2018 Werewolf\n');
  commit(bare.repo, 'docs: readme', {});
  assert.equal((await run(bare.repo, { credits })).exit, EXIT.PASS);
});

test('Push_check_scans_a_body_file_with_the_same_rules', () => {
  const body = `Summary\n\nthe widget that frobnicates every sprocket twice before dinner\nsee dew_flow_benchmark and E1.S4\ntoken ${SECRET}\n`;
  const result = checkText({ text: body, label: 'body.md', inputs });
  assert.equal(result.exit, EXIT.REFUSED);
  assert.deepEqual(result.checks.map(c => [c.name, c.findings.map(f => [f.path, f.line, f.what])]), [
    ['leakcheck', [['body.md', 3, 'shingle of synthetic-aid.md']]],
    ['secrets', [['body.md', 5, 'github-fine-grained-pat']]],
    ['agent-visible-paths', [['body.md', 4, 'plan story id']]],
    ['family-names', [['body.md', 4, 'needle dew_flow_benchmark']]],
  ]);
  assert.equal(checkText({ text: 'A plain body with https://github.com/o/r/pull/3\n', label: 'b.md', inputs }).exit, EXIT.PASS, 'links are what a body is for');
});

test('The_bench_branch_check_runs_only_the_leak_and_secret_scans_over_its_new_commits', async t => {
  const { repo, sha } = branchWith(t, { 'spikes/x/note.md': 'see dew_flow_benchmark E1.S4 CSES\n', 'inputs/probe.json': `{"k": "${SECRET}"}\n` });
  const result = await checkSelf({ repo, branch: 'topic', inputs });
  assert.deepEqual(result.checks.map(c => c.name), ['leakcheck', 'secrets']);
  assert.deepEqual(checkOf(result, 'secrets').findings.map(f => [f.commit, f.path]), [[sha, 'inputs/probe.json']]);
  assert.equal(result.exit, EXIT.REFUSED);
});

test('A_git_failure_is_exit_3_and_a_missing_branch_is_exit_4_never_a_pass', async t => {
  const repo = makeTaskRepo(t, {});
  assert.equal((await checkBranch({ repo, branch: 'nope', inputs })).exit, EXIT.CONFIG);
  assert.equal((await checkBranch({ repo: path.join(repo, 'no-such-dir'), branch: 'main', inputs })).exit, EXIT.ENVIRONMENT);
});

test('The_join_branch_holds_both_histories_and_the_license_and_its_push_needs_no_force', async t => {
  const origin = makeOrigin(t);
  const repo = makeTaskRepo(t, { origin });
  git(repo, 'checkout', '-q', '-b', 'chore/join-remote', 'main');
  git(repo, 'merge', '-q', '--allow-unrelated-histories', '--no-edit', 'origin/main');
  assert.equal(git(repo, 'show', 'chore/join-remote:LICENSE').startsWith('MIT License'), true);
  git(repo, 'merge-base', '--is-ancestor', 'main', 'chore/join-remote');
  git(repo, 'merge-base', '--is-ancestor', 'origin/main', 'chore/join-remote');
  const result = await checkBranch({ repo, branch: 'chore/join-remote', inputs });
  assert.equal(result.exit, EXIT.PASS);
  git(repo, 'push', '-q', 'origin', 'chore/join-remote:main'); // a fast-forward of the (local, bare) remote: git refuses a non-fast-forward without --force
  assert.equal(git(origin, 'rev-parse', 'main'), git(repo, 'rev-parse', 'chore/join-remote'));
});

test('A_report_line_never_carries_matched_text_even_for_a_path_with_odd_characters', async t => {
  const { repo } = branchWith(t, { 'notes/odd name.md': `${SECRET}\n` });
  const report = formatReport(await run(repo));
  assert.match(report, /notes\/odd name\.md:1 github-fine-grained-pat/);
  assert.equal(report.includes('aB3dE5gH'), false);
});
