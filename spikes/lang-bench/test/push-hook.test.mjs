// The enforced push path of the TASK repository: `push-check.mjs --install-hook` writes a pre-push hook that runs the check on exactly the
// sha git is about to publish. Real `git push` to a local bare remote; git itself refuses the push through the hook. The bench repository
// is never given a hook: its rule is the manual `--self` run.
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { chmodSync, existsSync, mkdirSync, readFileSync, statSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { HOOK_MARKER, hookScript } from '../lib/push-hook.mjs';
import { commit, git, makeOrigin, makeTaskRepo, ownedFolder } from './support/push-fixtures.mjs';

const CLI = fileURLToPath(new URL('../push-check.mjs', import.meta.url));
const UNRELATED_PUBLIC = fileURLToPath(new URL('./fixtures/public-unrelated.md', import.meta.url));
const DUMMY_SECRET = fileURLToPath(new URL('./fixtures/own-secret-dummy.txt', import.meta.url));
const AID = 'Header.\nA widget that frobnicates every sprocket twice before lunch will stall.\nFooter.\n';
const LEAK = 'the widget that frobnicates every sprocket twice before dinner\n';
const run = (args, env = {}) => spawnSync(process.execPath, [CLI, ...args], { encoding: 'utf8', env: { ...process.env, ...env }, timeout: 120_000 });
const gitRaw = (dir, ...args) => spawnSync('git', ['-C', dir, ...args], { encoding: 'utf8', env: { ...process.env, GIT_TERMINAL_PROMPT: '0' } });
const hookPath = repo => path.join(repo, '.git', 'hooks', 'pre-push');

/** A task repository with a bare origin, a hook installed (fixture aid, public text and secret file baked in), and a clean branch `topic`. */
function hooked(t) {
  const bare = makeOrigin(t);
  const repo = makeTaskRepo(t, { origin: bare });
  const aid = path.join(ownedFolder(t, 'hookaid'), 'aid.md');
  writeFileSync(aid, AID);
  const install = run(['--install-hook', '--repo', repo, '--origin-url', bare, '--aid', aid, '--public', UNRELATED_PUBLIC, '--secret-file', DUMMY_SECRET]);
  assert.equal(install.status, 0, install.stdout + install.stderr);
  git(repo, 'checkout', '-q', '-b', 'topic', 'origin/main');
  commit(repo, 'feat: a clean file', { 'tasks/01/s01.in': '5\n' });
  return { bare, repo, aid };
}

test('A_push_of_a_branch_holding_a_planted_leak_is_rejected_by_git_through_the_hook_and_the_remote_ref_is_unchanged', t => {
  const { bare, repo } = hooked(t);
  git(repo, 'checkout', '-q', '-b', 'leaky', 'topic');
  commit(repo, 'feat: a leak', { 'notes/x.md': `a\n${LEAK}` });
  const before = gitRaw(bare, 'for-each-ref').stdout;
  const pushed = gitRaw(repo, 'push', 'origin', 'leaky');
  assert.notEqual(pushed.status, 0, pushed.stdout + pushed.stderr);
  assert.match(pushed.stderr, /leakcheck +FAIL/);
  assert.match(pushed.stderr, /notes\/x\.md:2 shingle of aid\.md/);
  assert.equal(gitRaw(bare, 'for-each-ref').stdout, before, 'the remote ref is unchanged');
  assert.equal(gitRaw(bare, 'rev-parse', '--verify', '-q', 'refs/heads/leaky').status, 1);
});

test('A_clean_branch_is_accepted_through_the_hook_and_a_deleted_ref_passes', t => {
  const { bare, repo } = hooked(t);
  const pushed = gitRaw(repo, 'push', 'origin', 'topic');
  assert.equal(pushed.status, 0, pushed.stderr);
  assert.equal(gitRaw(bare, 'rev-parse', 'refs/heads/topic').stdout.trim(), git(repo, 'rev-parse', 'topic'));
  assert.match(pushed.stderr, /result +PASS/);
  const deleted = gitRaw(repo, 'push', 'origin', ':topic');
  assert.equal(deleted.status, 0, deleted.stderr);
  assert.equal(gitRaw(bare, 'rev-parse', '--verify', '-q', 'refs/heads/topic').status, 1);
});

test('A_sha_that_moved_after_the_check_began_is_refused_and_a_pushed_sha_with_no_branch_is_checked_as_given', t => {
  const { bare, repo, aid } = hooked(t);
  const old = git(repo, 'rev-parse', 'topic');
  commit(repo, 'feat: the branch moved', { 'tasks/01/s02.in': '6\n' });
  const base = ['--repo', repo, '--origin-url', bare, '--aid', aid, '--public', UNRELATED_PUBLIC, '--secret-file', DUMMY_SECRET];
  const moved = run([...base, '--branch', 'topic', '--tip', old]);
  assert.equal(moved.status, 4, moved.stdout);
  assert.match(moved.stdout, /moved/);
  assert.equal(run([...base, '--branch', 'topic', '--tip', git(repo, 'rev-parse', 'topic')]).status, 0, 'the branch at the sha it is checked at passes');
  const bySha = run([...base, '--branch', 'no-such-branch', '--tip', old]);
  assert.equal(bySha.status, 0, bySha.stdout);
  assert.match(bySha.stdout, new RegExp(`^tip +${old}$`, 'm'));
  const notSha = run([...base, '--branch', 'topic', '--tip', 'not-a-sha']);
  assert.equal(notSha.status, 4);
  assert.match(notSha.stdout, /full commit sha/);
  assert.equal(run([...base, '--branch', 'topic', '--tip', 'topic']).status, 4, 'a ref name is not a sha either');
  assert.equal(run([...base, '--branch', 'topic', '--tip', 'f'.repeat(40)]).status, 4, 'a sha that is not in the repository');
});

test('The_hook_checks_the_sha_git_pushes_not_the_branch_name_so_a_push_of_a_sha_is_checked_too', t => {
  const { bare, repo } = hooked(t);
  git(repo, 'checkout', '-q', '-b', 'leaky', 'topic');
  const leak = commit(repo, 'feat: a leak', { 'notes/x.md': `a\n${LEAK}` });
  git(repo, 'checkout', '-q', 'topic');
  const pushed = gitRaw(repo, 'push', 'origin', `${leak}:refs/heads/elsewhere`);
  assert.notEqual(pushed.status, 0, pushed.stderr);
  assert.equal(gitRaw(bare, 'rev-parse', '--verify', '-q', 'refs/heads/elsewhere').status, 1);
});

test('A_tag_or_any_ref_outside_refs_heads_is_refused_by_the_hook_with_the_manual_way_named', t => {
  const { bare, repo } = hooked(t);
  git(repo, 'tag', 'v-fixture');
  const pushed = gitRaw(repo, 'push', 'origin', 'v-fixture');
  assert.notEqual(pushed.status, 0);
  assert.match(pushed.stderr, /not a branch/);
  assert.match(pushed.stderr, /--no-verify/);
  assert.equal(gitRaw(bare, 'tag', '--list').stdout.trim(), '');
});

test('The_hook_refuses_when_the_bench_checkout_cannot_be_found_and_never_passes_silently', t => {
  const { repo } = hooked(t);
  const script = readFileSync(hookPath(repo), 'utf8');
  writeFileSync(hookPath(repo), script.replace(/CHECKER='[^']*'/, "CHECKER='/no/such/push-check.mjs'"));
  chmodSync(hookPath(repo), 0o755);
  const pushed = gitRaw(repo, 'push', 'origin', 'topic');
  assert.notEqual(pushed.status, 0);
  assert.match(pushed.stderr, /bench checkout not found at \/no\/such\/push-check\.mjs/);
});

test('Installing_twice_changes_nothing_and_the_hook_is_executable_with_the_marker_line', t => {
  const bare = makeOrigin(t);
  const repo = makeTaskRepo(t, { origin: bare });
  const args = ['--install-hook', '--repo', repo, '--origin-url', bare];
  assert.equal(run(args).status, 0);
  const first = readFileSync(hookPath(repo), 'utf8');
  const stamp = statSync(hookPath(repo)).mtimeMs;
  assert.match(first, new RegExp(`^${HOOK_MARKER}$`, 'm'));
  assert.ok((statSync(hookPath(repo)).mode & 0o111) !== 0);
  const second = run(args);
  assert.equal(second.status, 0);
  assert.match(second.stdout, /unchanged/);
  assert.equal(readFileSync(hookPath(repo), 'utf8'), first);
  assert.equal(statSync(hookPath(repo)).mtimeMs, stamp, 'not even rewritten');
  assert.match(first, /^#!\/bin\/sh/);
});

test('A_foreign_hook_is_refused_with_exit_4_unless_force_hook_and_uninstall_removes_only_our_own', t => {
  const bare = makeOrigin(t);
  const repo = makeTaskRepo(t, { origin: bare });
  mkdirSync(path.dirname(hookPath(repo)), { recursive: true });
  writeFileSync(hookPath(repo), '#!/bin/sh\necho someone else\n');
  chmodSync(hookPath(repo), 0o755);
  const refused = run(['--install-hook', '--repo', repo, '--origin-url', bare]);
  assert.equal(refused.status, 4);
  assert.equal(readFileSync(hookPath(repo), 'utf8'), '#!/bin/sh\necho someone else\n');
  assert.equal(run(['--uninstall-hook', '--repo', repo]).status, 4, 'not ours: not removed');
  assert.ok(existsSync(hookPath(repo)));
  const forced = run(['--install-hook', '--force-hook', '--repo', repo, '--origin-url', bare]);
  assert.equal(forced.status, 0, forced.stdout);
  assert.match(readFileSync(hookPath(repo), 'utf8'), new RegExp(HOOK_MARKER));
  assert.equal(readFileSync(`${hookPath(repo)}.foreign`, 'utf8'), '#!/bin/sh\necho someone else\n', 'the replaced hook is kept beside it');
  const removed = run(['--uninstall-hook', '--repo', repo]);
  assert.equal(removed.status, 0);
  assert.equal(existsSync(hookPath(repo)), false);
  assert.equal(run(['--uninstall-hook', '--repo', repo]).status, 0, 'nothing to remove is not an error');
});

test('The_hook_is_never_installed_into_the_bench_repository_or_a_repository_whose_origin_is_not_the_task_repository', t => {
  const bench = path.resolve(path.dirname(CLI), '..', '..');
  const refused = run(['--install-hook', '--repo', bench]);
  assert.equal(refused.status, 4);
  assert.match(refused.stdout, /bench/);
  const benchHook = gitRaw(bench, 'rev-parse', '--git-path', 'hooks/pre-push').stdout.trim();
  assert.equal(existsSync(path.resolve(bench, benchHook)), false, 'no hook was written in the bench');
  const repo = makeTaskRepo(t, { origin: makeOrigin(t) });
  const wrong = run(['--install-hook', '--repo', repo]);
  assert.equal(wrong.status, 4, 'the default expected origin is the task repository on GitHub');
  assert.equal(existsSync(hookPath(repo)), false);
  assert.equal(run(['--install-hook']).status, 4, '--repo is required');
});

test('An_option_holding_a_glob_a_space_or_a_quote_reaches_the_checker_literally', t => {
  const folder = ownedFolder(t, 'hookargs');
  const argsFile = path.join(folder, 'args.json');
  const fakeChecker = path.join(folder, 'fake-checker.mjs');
  writeFileSync(fakeChecker, `import { writeFileSync } from 'node:fs';\nwriteFileSync(${JSON.stringify(argsFile)}, JSON.stringify(process.argv.slice(2)));\n`);
  // files that a glob in the hook's working directory would match: an unquoted expansion would replace the `*` by their names
  for (const name of ['aXb', 'aYb']) writeFileSync(path.join(folder, name), '');
  const odd = ['dir/*', 'a b', "it's", '$HOME', '`id`', 'a*b'];
  const script = hookScript({ checker: fakeChecker, repo: folder, originUrl: 'https://example.invalid/r.git', extraArgs: odd.flatMap(v => ['--aid', v]) });
  const hook = path.join(folder, 'pre-push');
  writeFileSync(hook, script, { mode: 0o755 });
  const sha = 'a'.repeat(40);
  const r = spawnSync('sh', [hook, 'origin', 'url'], { cwd: folder, input: `refs/heads/topic ${sha} refs/heads/topic ${'0'.repeat(40)}\n`, encoding: 'utf8' });
  assert.equal(r.status, 0, r.stderr);
  const received = JSON.parse(readFileSync(argsFile, 'utf8'));
  assert.deepEqual(received.slice(received.indexOf('--origin-url')), ['--origin-url', 'https://example.invalid/r.git', ...odd.flatMap(v => ['--aid', v])], 'each baked argument is one literal word');
  assert.equal(received.includes('aXb'), false, 'no glob was expanded');
});
