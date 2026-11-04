// E1.S3 phase A, the task repository (T-L1, T-L2): the fetch into `<cache>/<sha>/` from a LOCAL repository only, its verification against the hash list (both rules, each file named), the cache hit that is
// checked and never fetched again, and the bare copy of one task folder. Every repository here is a synthetic one in an owned folder with its own hash list; only the last fetch case reads the real task
// repository (LANG_V2_TASK_REPO, as `realDraft` does) at the pinned sha. Needs only `git`.
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { existsSync, lstatSync, mkdirSync, readFileSync, readdirSync, statSync, symlinkSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { runBounded } from '../../minigit-bench/lib/run.mjs';
import { PINNED_TASK_SHA, copyTaskFolder, fetchTaskRepo } from '../lib/task-repo.mjs';
import { filesUnder, git, SPIKE } from './support/workspace-fixtures.mjs';
import { realDraft } from './support/format-fixtures.mjs';
import { ownedFolder, writeTree } from './toolchain-fixtures.mjs';

const sha256 = text => createHash('sha256').update(text).digest('hex');
/** A synthetic repository holding `files` (and `links`: `{ path: target }`) in one commit. -> `{ repo, sha }`. */
async function makeRepo(t, files, links = {}) {
  const repo = writeTree(ownedFolder(t, 'repo'), files);
  for (const [rel, target] of Object.entries(links)) {
    mkdirSync(path.dirname(path.join(repo, rel)), { recursive: true });
    symlinkSync(target, path.join(repo, rel));
  }
  await git(repo, ['init', '-q', '-b', 'main']);
  await git(repo, ['add', '-A']);
  await git(repo, ['commit', '-q', '-m', 'files']);
  return { repo, sha: await git(repo, ['rev-parse', 'HEAD']) };
}

const listOf = files => Object.entries(files).map(([rel, text]) => `${sha256(text)}  ${rel}`).sort().join('\n') + '\n';
const FILES = Object.freeze({ 'csharp/01-x/build.sh': 'build\n', 'node/01-x/run.sh': 'run\n', 'README.md': 'root file\n' });
const LISTED = Object.freeze({ 'csharp/01-x/build.sh': 'build\n', 'node/01-x/run.sh': 'run\n' });
const cacheOf = t => ownedFolder(t, 'cache');
const fetched = ({ repo, sha }, cacheDir, hashesText = listOf(LISTED)) => fetchTaskRepo({ sha, cacheDir, repo, hashesText });

// ---- T-L1: the two rules -----------------------------------------------------------------------------------------------------------

test('A_workspace_file_off_the_hash_list_is_refused', async t => {
  const made = await makeRepo(t, { ...FILES, 'csharp/01-x/extra.txt': 'not listed\n' });
  const cache = cacheOf(t);
  const r = await fetched(made, cache);
  assert.equal(r.ok, false);
  assert.deepEqual(r.mismatches, ['csharp/01-x/extra.txt']);
  assert.match(r.reason, /csharp\/01-x\/extra\.txt \(not on the hash list\)/);
  assert.deepEqual(readdirSync(cache), [], 'nothing stays at <cache>/<sha>, and no scratch folder');
});

test('A_file_outside_the_workspace_roots_is_checked_only_when_it_is_listed', async t => {
  const made = await makeRepo(t, { ...FILES, 'todo/notes.md': 'a spec on its own road\n' });
  const r = await fetched(made, cacheOf(t));
  assert.equal(r.ok, true, r.reason);
  assert.deepEqual(r.value.entries.map(e => e.path).sort(), ['csharp/01-x/build.sh', 'node/01-x/run.sh']);
});

test('A_listed_workspace_file_with_other_bytes_is_refused_naming_it', async t => {
  const made = await makeRepo(t, FILES);
  const cache = cacheOf(t);
  const wrong = await fetched(made, cache, listOf({ ...LISTED, 'csharp/01-x/build.sh': 'other\n' }));
  assert.equal(wrong.ok, false);
  assert.deepEqual(wrong.mismatches, ['csharp/01-x/build.sh']);
  assert.match(wrong.reason, /csharp\/01-x\/build\.sh \(sha256 differs\)/);
  const missing = await fetched(made, cache, listOf({ ...LISTED, 'csharp/01-x/gone.sh': 'listed but not in the repository\n' }));
  assert.match(missing.reason, /csharp\/01-x\/gone\.sh \(missing\)/);
  assert.deepEqual(readdirSync(cache), []);
});

test('A_link_under_a_workspace_root_is_refused', async t => {
  const made = await makeRepo(t, FILES, { 'node/01-x/link': '/etc/hostname' });
  const r = await fetched(made, cacheOf(t));
  assert.equal(r.ok, false);
  assert.match(r.reason, /node\/01-x\/link \(not a regular file\)/);
  const outside = await makeRepo(t, FILES, { 'todo/link': '/etc/hostname' });
  assert.equal((await fetched(outside, cacheOf(t))).ok, true, 'a link outside the four roots and off the list is not looked at');
});

// ---- the fetch ---------------------------------------------------------------------------------------------------------------------

test('A_fetch_lands_in_a_folder_named_for_the_sha_and_holds_no_repository', async t => {
  const made = await makeRepo(t, FILES);
  const cache = cacheOf(t);
  const r = await fetched(made, cache);
  assert.equal(r.ok, true, r.reason);
  assert.equal(r.value.root, path.join(cache, made.sha));
  assert.equal(r.value.sha, made.sha);
  assert.deepEqual(filesUnder(r.value.root), Object.keys(FILES).sort(), 'the files of the commit');
  for (const [rel, text] of Object.entries(FILES)) assert.equal(readFileSync(path.join(r.value.root, rel), 'utf8'), text);
  assert.equal(existsSync(path.join(r.value.root, '.git')), false, 'no repository a later git could walk up to');
  assert.deepEqual(readdirSync(cache), [made.sha]);
});

test('A_cached_tree_changed_later_is_refused_and_not_fetched_again', async t => {
  const made = await makeRepo(t, FILES);
  const cache = cacheOf(t);
  assert.equal((await fetched(made, cache)).ok, true);
  const file = path.join(cache, made.sha, 'node/01-x/run.sh');
  writeFileSync(file, 'edited later\n');
  const r = await fetched(made, cache);
  assert.equal(r.ok, false);
  assert.match(r.reason, /node\/01-x\/run\.sh \(sha256 differs\)/);
  assert.equal(readFileSync(file, 'utf8'), 'edited later\n', 'the edit is still there: the cache was not fetched again');
});

test('A_sha_that_is_not_forty_hex_and_a_repo_that_is_not_a_local_folder_are_refused', async t => {
  const made = await makeRepo(t, FILES);
  const cache = cacheOf(t);
  const short = await fetchTaskRepo({ sha: made.sha.slice(0, 12), cacheDir: cache, repo: made.repo, hashesText: listOf(LISTED) });
  assert.match(short.reason, /40 hex/);
  for (const repo of ['https://example.test/x.git', `file://${made.repo}`, 'rel/path', path.join(cache, 'no-such-folder'), path.join(made.repo, 'csharp/01-x/build.sh')]) {
    const r = await fetchTaskRepo({ sha: made.sha, cacheDir: cache, repo, hashesText: listOf(LISTED) });
    assert.equal(r.ok, false, repo);
    assert.match(r.reason, /absolute path to an existing local folder/, repo);
  }
  assert.deepEqual(readdirSync(cache), [], 'nothing was made');
});

test('A_failed_clone_leaves_no_scratch_folder', async t => {
  const notRepo = writeTree(ownedFolder(t, 'plain'), { 'a.txt': 'x\n' });
  const cache = cacheOf(t);
  const first = await fetchTaskRepo({ sha: 'a'.repeat(40), cacheDir: cache, repo: notRepo, hashesText: listOf(LISTED) });
  assert.equal(first.ok, false);
  const made = await makeRepo(t, FILES);
  const second = await fetchTaskRepo({ sha: 'b'.repeat(40), cacheDir: cache, repo: made.repo, hashesText: listOf(LISTED) });
  assert.equal(second.ok, false, 'the clone worked, the sha is not in it');
  assert.match(second.reason, /checkout/);
  assert.deepEqual(readdirSync(cache), [], 'no .fetch-* folder is left');
});

test('The_real_task_repository_at_the_pin_matches_the_hash_list', async t => {
  if (realDraft(t) === null) return;
  const cache = cacheOf(t);
  const list = readFileSync(path.join(SPIKE, 'inputs', 'hashes-lang.txt'), 'utf8');
  const r = await fetchTaskRepo({ cacheDir: cache, repo: process.env.LANG_V2_TASK_REPO });
  assert.equal(r.ok, true, r.reason);
  assert.equal(r.value.sha, PINNED_TASK_SHA);
  assert.equal(list.trim().split('\n').length, r.value.entries.length);
  const wrong = list.replace('d293bafc', 'd293bafd');
  const other = await fetchTaskRepo({ cacheDir: cacheOf(t), repo: process.env.LANG_V2_TASK_REPO, hashesText: wrong });
  assert.equal(other.ok, false);
  assert.match(other.reason, /todo\/tasks\/FORMAT\.md \(sha256 differs\)/);
});

// ---- T-L2: the bare copy ---------------------------------------------------------------------------------------------------------------

function taskRoot(t) {
  const root = writeTree(ownedFolder(t, 'root'), {
    'README.md': 'root\n', 'LICENSE': 'licence\n', 'todo/tasks/spec.md': 'spec\n', 'node/01-x/src/main.ts': 'node\n',
    'csharp/02-y/Program.cs': 'other task\n', 'csharp/01-x/Program.cs': 'task\n', 'csharp/01-x/sub/deep.txt': 'deep\n', 'csharp/01-x/run.sh': '#!/bin/sh\n',
  });
  return root;
}

test('The_bare_workspace_holds_exactly_the_task_folders_files', async t => {
  const root = taskRoot(t);
  await runBounded('chmod', ['755', path.join(root, 'csharp/01-x/run.sh')], { timeoutMs: 10_000, env: { PATH: '/usr/bin:/bin' } });
  const out = path.join(ownedFolder(t, 'out'), 'w');
  const r = copyTaskFolder({ root, stack: 'csharp', task: '01-x', out });
  assert.equal(r.ok, true, r.reason);
  assert.deepEqual(filesUnder(out), ['Program.cs', 'run.sh', 'sub/deep.txt']);
  assert.deepEqual(r.value.files, ['Program.cs', 'run.sh', 'sub/deep.txt']);
  assert.equal(statSync(path.join(out, 'run.sh')).mode & 0o777, 0o755, 'modes are kept');
  assert.equal(readFileSync(path.join(out, 'sub/deep.txt'), 'utf8'), 'deep\n');
});

test('The_bare_workspace_refuses_a_task_with_a_path_in_it_and_a_destination_in_a_worktree', async t => {
  const root = taskRoot(t);
  const parent = ownedFolder(t, 'out');
  for (const task of ['../01-x', '01-x/sub', '', '.hidden', '01 x', '/csharp']) {
    const r = copyTaskFolder({ root, stack: 'csharp', task, out: path.join(parent, 'w') });
    assert.equal(r.ok, false, JSON.stringify(task));
  }
  assert.equal(copyTaskFolder({ root, stack: 'rust', task: '01-x', out: path.join(parent, 'w') }).ok, false, 'a stack that is not csharp or node');
  assert.equal(copyTaskFolder({ root, stack: 'node', task: '09-none', out: path.join(parent, 'w') }).ok, false, 'a task folder that does not exist');
  assert.deepEqual(readdirSync(parent), [], 'nothing was written');
  const worktree = ownedFolder(t, 'tree');
  await git(worktree, ['init', '-q']);
  const inside = copyTaskFolder({ root, stack: 'csharp', task: '01-x', out: path.join(worktree, 'a', 'b') });
  assert.equal(inside.ok, false);
  assert.match(inside.reason, new RegExp(worktree.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')));
  assert.equal(existsSync(path.join(worktree, 'a')), false);
  const linked = writeTree(ownedFolder(t, 'root2'), { 'csharp/01-x/a.txt': 'x\n' });
  symlinkSync('/etc/hostname', path.join(linked, 'csharp/01-x/link'));
  const withLink = copyTaskFolder({ root: linked, stack: 'csharp', task: '01-x', out: path.join(parent, 'w') });
  assert.equal(withLink.ok, false);
  assert.match(withLink.reason, /link/);
  assert.equal(existsSync(path.join(parent, 'w')), false, 'a link is refused before anything is copied');
  assert.equal(lstatSync(parent).isDirectory(), true);
});
