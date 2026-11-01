// push-check.mjs as a command: argument handling, the loading of the private aids from outside git at run time, and the exit codes.
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { writeFileSync } from 'node:fs';
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
const cli = (args, { home } = {}) => spawnSync(process.execPath, [CLI, ...args], { encoding: 'utf8', env: { ...process.env, ...(home ? { HOME: home } : {}) }, timeout: 120_000 });

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
  for (const name of ['leakcheck', 'secrets', 'attribution', 'agent-visible-paths', 'family-names', 'known-task-credits', 'stack-folders']) assert.match(r.stdout, new RegExp(`^${name} +PASS`, 'm'));
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
  assert.equal(/agent-visible-paths/.test(self.stdout), false, 'the bench check runs only the leak and secret scans');
});

test('Setup_remote_mode_runs_step_1_and_prints_what_it_did', t => {
  const origin = makeOrigin(t);
  const repo = makeTaskRepo(t, {});
  const r = cli(['--setup-remote', '--repo', repo, '--url', origin]);
  assert.equal(r.status, 0, r.stdout + r.stderr);
  assert.match(r.stdout, new RegExp(`^remote: added origin ${origin.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}$`, 'm'));
  assert.match(r.stdout, /^origin\/main: [0-9a-f]{12} holds LICENSE$/m);
});
