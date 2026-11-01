// push-check.mjs as a command: argument handling, the loading of the private aids from outside git at run time, and the exit codes.
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { commit, git, makeOrigin, makeTaskRepo, ownedFolder } from './support/push-fixtures.mjs';

const CLI = fileURLToPath(new URL('../push-check.mjs', import.meta.url));
const AID_TEXT = 'Header.\nA widget that frobnicates every sprocket twice before lunch will stall.\nFooter.\n';

function aidFile(t) {
  const file = path.join(ownedFolder(t, 'aid'), 'synthetic-aid.md');
  writeFileSync(file, AID_TEXT);
  return file;
}
const UNRELATED_PUBLIC = fileURLToPath(new URL('./fixtures/public-unrelated.md', import.meta.url));
/** Tests that do not exercise the published-plans default name an unrelated public text, so they never read the real bench repository. */
const withPublic = args => (args.includes('--public') || args.includes('--bench-repo') || args.includes('--setup-remote') ? args : [...args, '--public', UNRELATED_PUBLIC]);
// The fixtures' origin is a local bare repository: a branch check is told to expect it (the real default is the task repository's URL).
function withOrigin(args) {
  const repo = args[args.indexOf('--repo') + 1];
  if (!args.includes('--repo') || !args.includes('--branch') || args.includes('--self') || args.includes('--origin-url')) return args;
  const url = spawnSync('git', ['-C', repo, 'remote', 'get-url', 'origin'], { encoding: 'utf8' }).stdout.trim();
  return url === '' ? args : [...args, '--origin-url', url];
}
const cli = (args, { home } = {}) => spawnSync(process.execPath, [CLI, ...withOrigin(withPublic(args))], { encoding: 'utf8', env: { ...process.env, ...(home ? { HOME: home } : {}) }, timeout: 120_000 });

function topicBranch(t, files) {
  const repo = makeTaskRepo(t, { origin: makeOrigin(t) });
  git(repo, 'checkout', '-q', '-b', 'topic', 'origin/main');
  const sha = commit(repo, 'feat: add', files);
  return { repo, sha };
}

test('The_cli_prints_one_named_line_per_check_and_exits_0_on_a_clean_branch', t => {
  const { repo } = topicBranch(t, { 'tasks/01/s01.in': '5\n' });
  const r = cli(['--repo', repo, '--branch', 'topic', '--aid', aidFile(t)]);
  assert.equal(r.status, 0, r.stdout + r.stderr);
  for (const name of ['leakcheck', 'secrets', 'attribution', 'run-base-text', 'family-names', 'known-task-credits', 'stack-folders']) assert.match(r.stdout, new RegExp(`^${name} +PASS`, 'm'));
  assert.match(r.stdout, /^result +PASS \(exit 0\)/m);
});

test('The_cli_exits_1_naming_commit_path_and_line_and_never_the_aid_text', t => {
  const { repo, sha } = topicBranch(t, { 'notes/x.md': 'a\nthe widget that frobnicates every sprocket twice before dinner\n' });
  const r = cli(['--repo', repo, '--branch', 'topic', '--aid', aidFile(t)]);
  assert.equal(r.status, 1);
  assert.match(r.stdout, new RegExp(`^  ${sha.slice(0, 10)} notes/x\\.md:2 shingle of synthetic-aid\\.md`, 'm'));
  assert.equal(/frobnicates|sprocket/i.test(r.stdout + r.stderr), false);
});

test('The_cli_exits_3_when_an_aid_file_cannot_be_read_and_never_passes_without_the_aids', t => {
  const { repo } = topicBranch(t, { 'a.txt': 'x\n' });
  const missing = path.join(ownedFolder(t, 'nothing'), 'absent-aid.md');
  const r = cli(['--repo', repo, '--branch', 'topic', '--aid', missing]);
  assert.equal(r.status, 3);
  assert.match(r.stdout + r.stderr, /absent-aid\.md/);
});

test('With_no_aid_argument_the_two_default_aids_are_looked_for_under_the_private_area_by_name', t => {
  const { repo } = topicBranch(t, { 'a.txt': 'x\n' });
  const home = ownedFolder(t, 'home');
  const r = cli(['--repo', repo, '--branch', 'topic'], { home });
  assert.equal(r.status, 3);
  assert.ok((r.stdout + r.stderr).includes(path.join(home, 'rsd', 'lang-v2', 'test-design', 'PLAN-trap-table.md')), r.stdout + r.stderr);
});

test('The_cli_exits_4_for_a_missing_branch_an_unknown_option_or_no_mode', t => {
  const { repo } = topicBranch(t, { 'a.txt': 'x\n' });
  const aid = aidFile(t);
  assert.equal(cli(['--repo', repo, '--branch', 'nope', '--aid', aid]).status, 4);
  assert.equal(cli(['--repo', repo, '--branch', 'topic', '--aid', aid, '--frobnicate']).status, 4);
  assert.equal(cli(['--aid', aid]).status, 4);
  assert.equal(cli(['--setup-remote', '--text', 'x', '--aid', aid]).status, 4, 'two modes at once');
});

test('Text_mode_checks_a_body_file_and_self_mode_the_bench_branch', t => {
  const folder = ownedFolder(t, 'body');
  const body = path.join(folder, 'body.md');
  writeFileSync(body, 'A body that mentions dew_flow_benchmark.\n');
  const r = cli(['--text', body, '--aid', aidFile(t)]);
  assert.equal(r.status, 1);
  assert.match(r.stdout, /body\.md:1 needle dew_flow_benchmark/);
  writeFileSync(body, 'A clean body.\n');
  assert.equal(cli(['--text', body, '--aid', aidFile(t)]).status, 0);
  const { repo } = topicBranch(t, { 'spikes/x/readme.md': 'fine\n' });
  const self = cli(['--self', '--repo', repo, '--branch', 'topic', '--aid', aidFile(t)]);
  assert.equal(self.status, 0, self.stdout + self.stderr);
  assert.match(self.stdout, /^leakcheck +PASS/m);
  assert.equal(/run-base-text/.test(self.stdout), false, 'the bench check runs only the leak and secret scans');
});

test('Setup_remote_mode_runs_step_1_and_prints_what_it_did', t => {
  const origin = makeOrigin(t);
  const repo = makeTaskRepo(t, {});
  const r = cli(['--setup-remote', '--repo', repo, '--url', origin]);
  assert.equal(r.status, 0, r.stdout + r.stderr);
  assert.match(r.stdout, new RegExp(`^remote: added origin ${origin.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}$`, 'm'));
  assert.match(r.stdout, /^origin\/main: [0-9a-f]{12} holds LICENSE$/m);
});

test('A_public_text_named_with_public_exempts_the_shingles_it_holds', t => {
  const { repo } = topicBranch(t, { 'README.md': '# T\n\nthe widget that frobnicates every sprocket twice before lunch\n' });
  const folder = ownedFolder(t, 'two');
  const aid = path.join(folder, 'two-aid.md');
  writeFileSync(aid, `${AID_TEXT}Another private sentence about gadgets and their wobbling springs today.\n`);
  assert.equal(cli(['--repo', repo, '--branch', 'topic', '--aid', aid]).status, 1, 'without the exemption the line is a hit');
  const publicFile = path.join(folder, 'plan.md');
  writeFileSync(publicFile, 'Public plan: a widget that frobnicates every sprocket twice before lunch will stall.\n');
  const r = cli(['--repo', repo, '--branch', 'topic', '--aid', aid, '--public', publicFile]);
  assert.equal(r.status, 0, r.stdout + r.stderr);
  const wholly = path.join(folder, 'aid-only-public.md');
  writeFileSync(wholly, readFileSync(publicFile, 'utf8'));
  const refused = cli(['--repo', repo, '--branch', 'topic', '--aid', wholly, '--public', publicFile]);
  assert.equal(refused.status, 3, 'an aid that is wholly public would check nothing');
  assert.match(refused.stdout, /would check nothing/);
});

/** A bench-like repository whose origin/main holds the two plan files with `published`; `--bench-repo` points the check at it. */
function benchWithPlans(t, published) {
  const repo = makeTaskRepo(t, { origin: makeOrigin(t), files: { 'todo/PLAN_lang_comparison_v2.md': published, 'todo/PLAN_lang_comparison_v2_E1.md': 'stories\n' } });
  git(repo, 'push', '-q', 'origin', 'main:main', '--force');
  git(repo, 'fetch', '-q', 'origin');
  return repo;
}

test('The_default_public_texts_are_the_published_plans_and_a_plan_edited_in_the_working_tree_exempts_nothing', t => {
  const { repo } = topicBranch(t, { 'README.md': '# T\n\nthe widget that frobnicates every sprocket twice before lunch\n' });
  const folder = ownedFolder(t, 'pub');
  const aid = path.join(folder, 'two-aid.md');
  writeFileSync(aid, `${AID_TEXT}Another private sentence about gadgets and their wobbling springs today.\n`);
  const bench = benchWithPlans(t, 'nothing relevant\n');
  writeFileSync(path.join(bench, 'todo/PLAN_lang_comparison_v2.md'), 'a widget that frobnicates every sprocket twice before lunch will stall\n');
  const refused = cli(['--repo', repo, '--branch', 'topic', '--aid', aid, '--bench-repo', bench]);
  assert.equal(refused.status, 1, 'the working-tree edit exempts nothing\n' + refused.stdout);
  git(bench, 'checkout', '-q', '--', '.');
  writeFileSync(path.join(bench, 'todo/PLAN_lang_comparison_v2.md'), 'a widget that frobnicates every sprocket twice before lunch will stall\n');
  commit(bench, 'docs: publish it', {});
  git(bench, 'push', '-q', 'origin', 'main:main');
  git(bench, 'fetch', '-q', 'origin');
  assert.equal(cli(['--repo', repo, '--branch', 'topic', '--aid', aid, '--bench-repo', bench]).status, 0, 'a published text exempts what it holds');
});

test('A_check_whose_published_plans_cannot_be_read_does_not_run', t => {
  const { repo } = topicBranch(t, { 'a.txt': 'x\n' });
  const bare = makeTaskRepo(t, { origin: makeOrigin(t), files: { 'unrelated.txt': 'x\n' } });
  const r = cli(['--repo', repo, '--branch', 'topic', '--aid', aidFile(t), '--bench-repo', bare]);
  assert.equal(r.status, 3);
  assert.match(r.stdout, /public text todo\/PLAN_lang_comparison_v2\.md cannot be read at origin\/main/);
});
