// The pre-push check over a branch, a text body and the bench's own branch: the named lines, the exit codes, and the promise that no
// report line carries the text it matched. Everything runs over fixture repositories with a bare `origin` and a synthetic aid.
import assert from 'node:assert/strict';
import { writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { checkBranch as checkBranchAgainst, checkSelf, checkText, EXIT, formatReport } from '../lib/push-check.mjs';
import { TASK_REPO_URL } from '../lib/push-remote.mjs';
import { commit, git, makeOrigin, makeTaskRepo } from './support/push-fixtures.mjs';
import { loadInputsForTest, realCredits } from './support/push-inputs.mjs';

const AID_TEXT = 'Header.\nA widget that frobnicates every sprocket twice before lunch will stall.\nFooter.\n';
const SECRET = `${'github_pat'}_${'aB3dE5gH7jK9mN1pQ3sT5vW7yZ'}_${'aB3dE5gH7jK9mN1pQ3sT5v'}`;
const inputs = loadInputsForTest({ aidText: AID_TEXT });
const credits = realCredits();
// The fixtures' origin is a local bare repository, so the expected URL is whatever their origin says (the real default is TASK_REPO_URL).
const originOf = repo => { try { return git(repo, 'remote', 'get-url', 'origin'); } catch { return TASK_REPO_URL; } };
const checkBranch = args => checkBranchAgainst({ expectedOrigin: originOf(args.repo), ...args });
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
  assert.deepEqual(result.checks.map(c => c.name), ['leakcheck', 'secrets', 'attribution', 'run-base-text', 'family-names', 'known-task-credits', 'stack-folders', 'tree-shape']);
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
  assert.deepEqual(checkOf(result, 'run-base-text').findings.map(f => [f.path, f.what]), [['tasks/01/NOTES.md', 'needle CSES']]);
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
    ['run-base-text', [['body.md', 4, 'plan story id']]],
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

test('A_placement_rule_reads_the_tip_tree_because_a_run_base_carries_the_tip_and_never_the_history', async t => {
  const { repo } = branchWith(t, { 'todo/tasks/FORMAT.md': 'Story E1.S8a and https://example.org and Redis\n', 'csharp/01/Program.cs': 'class P {}\n', 'runner/n.py': '# dew_flow_benchmark\n' });
  commit(repo, 'docs: clean it up again', { 'todo/tasks/FORMAT.md': 'Clean.\n', 'csharp/01/Program.cs': null, 'runner/n.py': 'pass\n' });
  const result = await run(repo);
  assert.equal(result.exit, EXIT.PASS, formatReport(result));
  // the same rule still reads the TIP: a file that is wrong at the tip is a finding
  commit(repo, 'docs: bring it back', { 'todo/tasks/FORMAT.md': 'Story E1.S8a again\n' });
  const again = await run(repo);
  assert.deepEqual(checkOf(again, 'run-base-text').findings.map(f => [f.path, f.line, f.what]), [['todo/tasks/FORMAT.md', 1, 'plan story id']]);
});

test('The_leak_and_secret_scans_read_every_commit_even_where_a_later_commit_removed_the_text', async t => {
  const { repo, sha } = branchWith(t, { 'old.txt': `key ${SECRET}\nthe widget that frobnicates every sprocket twice before dinner\n` });
  commit(repo, 'refactor: remove', { 'old.txt': null });
  const result = await run(repo);
  assert.equal(result.exit, EXIT.REFUSED);
  assert.deepEqual([checkOf(result, 'secrets').findings[0].commit, checkOf(result, 'leakcheck').findings[0].commit], [sha, sha]);
});

test('A_branch_check_refuses_when_origin_is_not_the_task_repository_because_the_range_is_measured_against_origin', async t => {
  const { repo } = branchWith(t, { 'a.txt': 'x\n' });
  const wrong = await checkBranchAgainst({ repo, branch: 'topic', inputs });
  assert.equal(wrong.exit, EXIT.CONFIG);
  assert.match(wrong.error, /origin's URL is .* not the task repository https:\/\/github\.com\/oleksandrdubyna88\/dew_flow_ai_language_comparison\.git/);
  git(repo, 'remote', 'set-url', '--push', 'origin', 'https://example.invalid/elsewhere.git');
  const pushElsewhere = await checkBranch({ repo, branch: 'topic', inputs });
  assert.equal(pushElsewhere.exit, EXIT.CONFIG, 'a push URL that differs is refused too');
  assert.match(pushElsewhere.error, /push URL/);
  const noOrigin = makeTaskRepo(t, {});
  assert.equal((await checkBranchAgainst({ repo: noOrigin, branch: 'main', inputs })).exit, EXIT.CONFIG);
});

test('A_submodule_other_than_the_conventions_mount_or_a_foreign_gitmodules_url_is_refused_by_the_tree_shape_check', async t => {
  const { repo } = branchWith(t, { '.gitmodules': '[submodule "x"]\n\tpath = csharp/01-x/sol\n\turl = https://github.com/someone/solutions.git\n' });
  git(repo, 'update-index', '--add', '--cacheinfo', '160000,1111111111111111111111111111111111111111,csharp/01-x/sol');
  git(repo, 'commit', '-q', '-m', 'chore: pin a submodule');
  const result = await run(repo);
  assert.equal(result.exit, EXIT.REFUSED);
  assert.deepEqual(checkOf(result, 'tree-shape').findings.map(f => [f.path, f.line, f.what]), [
    ['csharp/01-x/sol', null, 'submodule outside the conventions mount'],
    ['.gitmodules', 2, 'submodule path other than the conventions mount'],
    ['.gitmodules', 3, 'submodule URL other than the conventions repository'],
  ]);
});

test('The_conventions_mount_as_the_gitmodules_say_passes_the_tree_shape_check', async t => {
  const url = 'https://github.com/oleksandrdubyna88/dew_flow_conventions.git';
  const { repo } = branchWith(t, { '.gitmodules': `[submodule "c"]\n\tpath = .agents/conventions\n\turl = ${url}\n` });
  git(repo, 'update-index', '--add', '--cacheinfo', '160000,1111111111111111111111111111111111111111,.agents/conventions');
  git(repo, 'commit', '-q', '-m', 'chore: pin the conventions');
  const result = await run(repo);
  assert.equal(result.exit, EXIT.PASS, formatReport(result));
});

const LE16 = text => Buffer.concat([Buffer.from([0xff, 0xfe]), Buffer.from(text, 'utf16le')]);
const NUL_REASON = 'file holds a NUL byte (binary or UTF-16): the text scans cannot be trusted on it';

test('A_file_with_a_nul_byte_is_refused_in_a_task_repository_and_its_text_is_scanned_anyway', async t => {
  const { repo, sha } = branchWith(t, { 'tasks/a.bin': Buffer.from([0x1f, 0x8b, 0, 0, 1]), 'notes.md': LE16(`k=${SECRET}\nthe widget that frobnicates every sprocket twice before dinner\n`) });
  const result = await run(repo);
  assert.equal(result.exit, EXIT.REFUSED);
  assert.deepEqual(checkOf(result, 'tree-shape').findings.map(f => [f.path, f.what]), [['notes.md', NUL_REASON], ['tasks/a.bin', NUL_REASON]]);
  assert.deepEqual(checkOf(result, 'secrets').findings.map(f => [f.commit, f.path, f.line]), [[sha, 'notes.md', 1]], 'a UTF-16 file is decoded, so its token is found too');
  assert.equal(checkOf(result, 'leakcheck').findings.length, 1);
});

test('The_bench_branch_decodes_a_utf16_file_and_scans_other_binaries_as_latin1_without_refusing_them', async t => {
  const { repo, sha } = branchWith(t, { 'notes.md': LE16(`k=${SECRET}\n`), 'image.png': Buffer.from([0x89, 0x50, 0, 0, 0x0d]) });
  const result = await checkSelf({ repo, branch: 'topic', inputs });
  assert.deepEqual(checkOf(result, 'secrets').findings.map(f => [f.commit, f.path]), [[sha, 'notes.md']]);
  assert.equal(result.checks.length, 2, 'only the leak and the secret scans');
});

test('A_bench_body_runs_only_the_leak_and_secret_scans_so_its_plan_ids_and_family_names_are_not_refused', () => {
  const body = 'Bench PR: the E1.S4 story in dew_flow_benchmark, see https://github.com/o/r/pull/3 and Werewolf.\n';
  assert.equal(checkText({ text: body, label: 'b.md', inputs }).exit, EXIT.REFUSED, 'a task-repository body refuses a bench pointer');
  const bench = checkText({ text: body, label: 'b.md', inputs, self: true });
  assert.equal(bench.exit, EXIT.PASS);
  assert.deepEqual(bench.checks.map(c => c.name), ['leakcheck', 'secrets']);
  const leaky = checkText({ text: `${body}the widget that frobnicates every sprocket twice before dinner\ntoken ${SECRET}\n`, label: 'b.md', inputs, self: true });
  assert.deepEqual(leaky.checks.map(c => [c.name, c.findings.length]), [['leakcheck', 1], ['secrets', 1]]);
});

test('A_symlink_in_the_pushed_tree_is_refused_by_the_tree_shape_check_and_its_target_text_is_scanned', async t => {
  const { repo, sha } = branchWith(t, { 'tasks/ok.md': 'fine\n' });
  const { symlinkSync } = await import('node:fs');
  symlinkSync(`/home/someone/.config/${SECRET}`, path.join(repo, 'tasks', 'key'));
  const linked = commit(repo, 'feat: a link', {});
  const result = await run(repo);
  assert.equal(result.exit, EXIT.REFUSED);
  assert.deepEqual(checkOf(result, 'tree-shape').findings.map(f => [f.commit, f.path, f.what]), [[linked, 'tasks/key', 'symlink: a link can point outside the tree it is checked in']]);
  assert.equal(checkOf(result, 'secrets').findings.length, 1, 'the link target is the blob text and is scanned');
  assert.notEqual(sha, linked);
});

test('A_file_name_is_scanned_for_needles_family_names_and_secret_shapes_like_the_text_inside_it', async t => {
  const { repo, sha } = branchWith(t, { 'tasks/09-Werewolf/samples/s01.in': '1\n', 'research/dew_flow_benchmark-notes.md': 'x\n', [`research/${SECRET}.txt`]: 'x\n', 'tasks/01/plain.in': '1\n' });
  const result = await run(repo);
  assert.equal(result.exit, EXIT.REFUSED);
  assert.deepEqual(checkOf(result, 'run-base-text').findings.map(f => [f.commit, f.path, f.line, f.what]), [[sha, 'tasks/09-Werewolf/samples/s01.in', null, 'needle Werewolf']]);
  assert.deepEqual(checkOf(result, 'family-names').findings.map(f => [f.path, f.what]), [['research/dew_flow_benchmark-notes.md', 'needle dew_flow_benchmark']]);
  assert.deepEqual(checkOf(result, 'secrets').findings.map(f => [f.path, f.line, f.what]), [['research/[secret].txt', null, 'github-fine-grained-pat']], 'the path is the position, so the secret in it is masked');
  assert.equal(formatReport(result).includes(SECRET.slice(0, 16)), false);
});

test('The_report_names_the_tip_it_checked_and_the_push_that_publishes_exactly_that_commit', async t => {
  const { repo, sha } = branchWith(t, { 'a.txt': 'x\n' });
  const result = await run(repo);
  assert.equal(result.tip, sha);
  const report = formatReport(result);
  assert.match(report, new RegExp(`^tip +${sha}$`, 'm'));
  assert.match(report, new RegExp(`^push +git push <url> ${sha}:refs/heads/topic$`, 'm'));
  const self = await checkSelf({ repo, branch: 'topic', inputs });
  assert.match(formatReport(self), new RegExp(`^tip +${sha}$`, 'm'));
  assert.equal(formatReport(checkText({ text: 'x\n', label: 'b.md', inputs })).includes('refs/heads'), false, 'a body has no tip');
});

test('A_known_task_spec_added_in_an_old_commit_and_removed_later_still_needs_its_terms', async t => {
  const { repo } = branchWith(t, { 'todo/tasks/01-descent-to-one.md': '# Task 01\n' });
  commit(repo, 'refactor: remove it again', { 'todo/tasks/01-descent-to-one.md': null, 'README.md': '# T\n\n## Attribution\n\nnothing\n' });
  const result = await run(repo);
  assert.equal(result.exit, EXIT.REFUSED);
  assert.ok(checkOf(result, 'known-task-credits').findings.some(f => f.what.startsWith('credit line of task 01')), formatReport(result));
});

test('A_statement_copy_under_a_workspace_folder_is_a_known_task_text_for_the_credit_check', async t => {
  const { repo } = branchWith(t, { 'tasks/01-descent-to-one/STATEMENT.md': 'statement\n', 'README.md': '# T\n\n## Attribution\n\nnothing\n' });
  const result = await run(repo);
  assert.ok(checkOf(result, 'known-task-credits').findings.some(f => f.what.startsWith('credit line of task 01')), formatReport(result));
});

test('A_remote_tracking_ref_that_the_remote_no_longer_has_or_has_moved_is_refused_because_it_would_hide_commits', async t => {
  const { repo } = branchWith(t, { 'research/a.md': 'x\n' });
  assert.equal((await run(repo)).exit, EXIT.PASS, 'fresh tracking refs pass');
  git(repo, 'update-ref', 'refs/remotes/origin/old', git(repo, 'rev-parse', 'topic'));
  const stale = await run(repo);
  assert.equal(stale.exit, EXIT.CONFIG);
  assert.match(stale.error, /refs\/remotes\/origin\/old/);
  assert.match(stale.error, /--setup-remote/);
  git(repo, 'update-ref', '-d', 'refs/remotes/origin/old');
  assert.equal((await run(repo)).exit, EXIT.PASS);
  const bare = originOf(repo);
  const other = path.join(path.dirname(repo), 'other');
  git(path.dirname(repo), 'clone', '-q', bare, other);
  commit(other, 'feat: the remote moved', { 'z.txt': 'z\n' });
  git(other, 'push', '-q', 'origin', 'HEAD:main');
  const moved = await run(repo);
  assert.equal(moved.exit, EXIT.CONFIG, 'a tracking ref behind the remote is stale too');
  assert.match(moved.error, /refs\/remotes\/origin\/main/);
});

test('A_family_name_in_a_commit_message_is_refused_because_the_task_repository_is_public_history', async t => {
  const { repo, sha } = branchWith(t, { 'a.txt': 'x\n' }, { message: 'feat: add a, as the plan in dew_flow_benchmark says' });
  commit(repo, 'docs: a clean message', { 'b.txt': 'y\n' });
  const result = await run(repo);
  assert.equal(result.exit, EXIT.REFUSED);
  assert.deepEqual(checkOf(result, 'family-names').findings.map(f => [f.commit, f.path, f.line, f.what]), [[sha, '(commit message)', 1, 'needle dew_flow_benchmark']]);
  assert.equal((await checkSelf({ repo, branch: 'topic', inputs })).exit, EXIT.PASS, 'the bench branch may name its own family');
});

test('The_report_prints_the_token_span_of_a_shingle_finding_and_not_its_words', async t => {
  const { repo } = branchWith(t, { 'notes/x.md': 'a\nthe widget that frobnicates every sprocket twice before dinner\n' });
  const report = formatReport(await run(repo));
  assert.match(report, /notes\/x\.md:2 shingle of synthetic-aid\.md \[window spans \d+ tokens\]/);
});

test('A_remote_branch_that_was_never_fetched_is_refused_too_because_the_freshness_check_runs_both_ways', async t => {
  const { repo } = branchWith(t, { 'research/a.md': 'x\n' });
  const bare = originOf(repo);
  const other = path.join(path.dirname(repo), 'other2');
  git(path.dirname(repo), 'clone', '-q', bare, other);
  git(other, 'push', '-q', 'origin', 'HEAD:refs/heads/extra-branch');
  const stale = await run(repo);
  assert.equal(stale.exit, EXIT.CONFIG, 'the remote has a branch this repository has no tracking ref for');
  assert.match(stale.error, /refs\/remotes\/origin\/extra-branch/);
  assert.match(stale.error, /--setup-remote/);
  git(repo, 'fetch', '-q', 'origin');
  assert.equal((await run(repo)).exit, EXIT.PASS, 'after the fetch the two sides agree');
});

/** A task repository whose ORIGIN already holds `published` (as if pushed earlier), and a branch `topic` off it. */
function publishedThenBranch(t, published) {
  const bare = makeOrigin(t);
  const seed = path.join(path.dirname(bare), 'seed2');
  git(path.dirname(bare), 'clone', '-q', bare, seed);
  commit(seed, 'feat: published earlier', published);
  git(seed, 'push', '-q', 'origin', 'HEAD:main');
  const repo = makeTaskRepo(t, { origin: bare });
  git(repo, 'checkout', '-q', '-b', 'topic', 'origin/main');
  return repo;
}

const FULL_ATTRIBUTION = () => `# T\n\nIntro.\n\n## Attribution\n\n${credits.value.known.map(e => e.credit).join('\n\n')}\n\n${[credits.value.known[0], credits.value.known[1]].map(e => e.spec).join(' and ')} are licensed CC BY-NC-SA 4.0, an exception to the MIT licence.\n\n${credits.value.known.find(e => e.upstreamNotice).upstreamNotice.text}\n`;

test('A_known_task_path_anywhere_in_the_tip_tree_keeps_the_readme_bound_to_its_credit_terms_even_when_the_range_touches_none', async t => {
  const repo = publishedThenBranch(t, { 'README.md': FULL_ATTRIBUTION(), 'todo/tasks/01-descent-to-one.md': '# Task 01\n' });
  commit(repo, 'docs: an unrelated file', { 'research/n.md': 'notes\n' });
  assert.equal((await run(repo)).exit, EXIT.PASS, 'the README still carries every term');
  commit(repo, 'docs: the readme loses its attribution', { 'README.md': '# T\n\nIntro only.\n' });
  const result = await run(repo);
  assert.equal(result.exit, EXIT.REFUSED);
  assert.ok(checkOf(result, 'attribution').findings.length + checkOf(result, 'known-task-credits').findings.length > 0, formatReport(result));
  assert.ok(checkOf(result, 'known-task-credits').findings.some(f => f.what.startsWith('credit line of task 01')), formatReport(result));
});

test('A_symlink_or_a_stack_file_that_was_pushed_earlier_is_still_found_in_the_whole_tip_tree', async t => {
  const { symlinkSync } = await import('node:fs');
  const bare = makeOrigin(t);
  const seed = path.join(path.dirname(bare), 'seed3');
  git(path.dirname(bare), 'clone', '-q', bare, seed);
  symlinkSync('/etc/hostname', path.join(seed, 'link'));
  commit(seed, 'feat: published earlier, a link and a stack file', { 'csharp/01/Program.cs': 'class P {}\n' });
  git(seed, 'push', '-q', 'origin', 'HEAD:main');
  const repo = makeTaskRepo(t, { origin: bare });
  git(repo, 'checkout', '-q', '-b', 'topic', 'origin/main');
  commit(repo, 'docs: an unrelated file', { 'research/n.md': 'notes\n' });
  const result = await run(repo);
  assert.equal(result.exit, EXIT.REFUSED);
  assert.deepEqual(checkOf(result, 'stack-folders').findings.map(f => f.path), ['csharp/01/Program.cs'], 'a file of the tip that no commit of the range touched');
  assert.deepEqual(checkOf(result, 'tree-shape').findings.map(f => f.path), ['link']);
});

test('A_check_whose_blobs_together_exceed_the_ceiling_is_refused_with_exit_3_and_never_passed', async t => {
  const { repo } = branchWith(t, { 'a.txt': 'x'.repeat(2000), 'b.txt': 'y'.repeat(2000) });
  const result = await run(repo, { limits: { maxTotalBytes: 1000 } });
  assert.equal(result.exit, EXIT.ENVIRONMENT);
  assert.match(result.error, /more than 1000 bytes/);
});

test('A_check_reports_its_phases_through_the_progress_callback_in_order_and_says_how_much_it_reads', async t => {
  const { repo } = branchWith(t, { 'a.txt': 'x\n', 'b.txt': 'y\n' });
  const seen = [];
  const result = await checkBranch({ repo, branch: 'topic', inputs, progress: line => seen.push(line) });
  assert.equal(result.exit, EXIT.PASS);
  assert.deepEqual(seen.map(l => l.replace(/\d+/g, 'N')), ['verifying origin and the remote-tracking refs', 'scanning N commits', 'reading N blobs', 'scanning N files and N messages', 'checking the tip tree: README, credit terms, stack folders, tree shape']);
  assert.equal(seen[1], 'scanning 1 commits', 'the one commit of the branch off origin/main');
  const quiet = await checkBranch({ repo, branch: 'topic', inputs });
  assert.equal(quiet.exit, EXIT.PASS, 'no callback is no output and no failure');
  const self = [];
  await checkSelf({ repo, branch: 'topic', inputs, progress: line => self.push(line) });
  assert.deepEqual(self.map(l => l.replace(/\d+/g, 'N')), ['scanning N commits', 'reading N blobs', 'scanning N files and N messages']);
});
