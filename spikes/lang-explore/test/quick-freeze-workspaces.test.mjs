// The freeze workspaces (F2.S2 (d), D70, the lang-explore half): `explore.mjs workspaces` writes the twelve workspaces of the six tasks exactly as a session gets them, `workspaces-copy` is the separate, reviewed and resumable copy
// into a worktree of the task repository, and `workspaces --check --root` builds them again and compares. Every fixture is invented: specs and FORMAT in `git init` repositories in owned folders; nothing real is read.
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { existsSync, mkdirSync, readFileSync, readdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { test } from 'node:test';
import { owned } from '../lib/workspaces-owned.mjs';
import { readFolderFiles, treeHashOfFiles } from '../lib/workspaces-tree.mjs';
import { FORMAT, TAG, changeAndCommit, committedCheckout, folder, git, made, pinsFor, run, sessionWorkspaces, taskRepo, worktreeAt } from './quick-freeze-fixture.mjs';

const CHILD = fileURLToPath(new URL('./quick-freeze-child.mjs', import.meta.url));
const FOLDERS = Object.keys(sessionWorkspaces());
const stagingIn = dir => readdirSync(dir).filter(n => n.startsWith(owned.ownedPrefix('stage')));
const claimsIn = dir => readdirSync(dir).filter(n => n.startsWith(owned.ownedPrefix('stage')) && n.endsWith('.owner'));
const treeOf = dir => treeHashOfFiles(readFolderFiles(dir).value.files);
const statusOf = dir => git(dir, 'status', '--porcelain', '--untracked-files=all').split('\n').filter(l => l !== '');

/** The verb as its own process, killed at `hook` (the call `at`, or the first). */
function killed(argv, hook, at) {
  const r = spawnSync(process.execPath, [CHILD, JSON.stringify({ argv, hook, at })], { encoding: 'utf8' });
  assert.equal(r.signal, 'SIGKILL', `the process was killed at ${hook}: ${r.stdout}${r.stderr}`);
}

test('The_written_workspace_equals_what_a_session_gets_byte_for_byte', async t => {
  const m = await made(t);
  assert.equal(m.code, 0, m.lines.join('\n'));
  const want = sessionWorkspaces();
  assert.equal(FOLDERS.length, 12, 'six tasks, both stacks');
  for (const [name, files] of Object.entries(want)) {
    const read = readFolderFiles(path.join(m.out, name));
    assert.equal(read.ok, true, `${name}: ${read.reason}`);
    assert.deepEqual(Object.keys(read.value.files).sort(), Object.keys(files).sort(), `${name}: the same files`);
    for (const [rel, f] of Object.entries(files)) assert.equal(read.value.files[rel].bytes.equals(f.bytes), true, `${name}/${rel}: the same bytes`);
  }
  const manifest = JSON.parse(readFileSync(path.join(m.out, 'workspaces.json'), 'utf8'));
  assert.equal(manifest.commit, m.repo.commit, 'the manifest names the commit of the tag');
  assert.deepEqual(Object.keys(manifest.trees), FOLDERS, 'and one tree hash per workspace, in order');
  assert.deepEqual(readdirSync(m.out).sort(), ['csharp', 'node', 'workspaces.json'], 'nothing else is in the output folder');
  assert.equal(m.lines.some(l => /wrote 12 workspaces/.test(l)), true);
});

test('A_workspace_whose_grammar_is_not_the_frozen_FORMATs_is_refused', async t => {
  const other = FORMAT.replace('## 4.', '## 4. (another grammar)');
  assert.notEqual(other, FORMAT, 'the planted FORMAT is another text');
  const bad = taskRepo(t, { format: other });
  const out = path.join(folder(t, 'wsout'), 'ws');
  const refused = await run(['workspaces', '--tag', bad.tag, '--task-repo', bad.dir, '--out', out]);
  assert.equal(refused.code, 4, refused.lines.join('\n'));
  assert.match(refused.lines.join('\n'), /not the frozen FORMAT/);
  assert.equal(existsSync(out), false, 'nothing is written from it');
  const good = await made(t);
  assert.equal(good.code, 0, 'the frozen FORMAT is permitted');
  const checkout = await committedCheckout(t);
  const grammar = path.join(checkout.dir, 'csharp/04-rule-api/runner/GRAMMAR.md');
  const planted = changeAndCommit(checkout.dir, 'csharp/04-rule-api/runner/GRAMMAR.md', bytes => Buffer.from(bytes.toString('utf8').replace('## 4.', '## 4. (another grammar)')));
  const checked = await run(['workspaces', '--check', '--root', checkout.dir]);
  assert.equal(checked.code, 1, checked.lines.join('\n'));
  assert.match(checked.lines.join('\n'), /csharp\/04-rule-api\/runner\/GRAMMAR\.md: differs.*the grammar is not the frozen FORMAT's/);
  assert.equal(existsSync(grammar), true);
  assert.notEqual(planted, checkout.head);
});

test('The_check_finds_one_changed_byte_in_a_committed_workspace_and_passes_an_equal_one', async t => {
  const checkout = await committedCheckout(t);
  const clean = await run(['workspaces', '--check', '--root', checkout.dir]);
  assert.equal(clean.code, 0, clean.lines.join('\n'));
  assert.equal(clean.lines.at(-1), `workspaces-check ok commit ${checkout.head} tasks 04,05,06,07,08,10 stacks csharp,node`, 'the last line is the one line the freeze record reads');
  changeAndCommit(checkout.dir, 'node/06-blog-api/STATEMENT.md', bytes => { const b = Buffer.from(bytes); b[b.length - 3] ^= 1; return b; });
  const changed = await run(['workspaces', '--check', '--root', checkout.dir]);
  assert.equal(changed.code, 1, changed.lines.join('\n'));
  assert.match(changed.lines.join('\n'), /^node\/06-blog-api: the tree hash differs/m);
  assert.match(changed.lines.join('\n'), /^node\/06-blog-api\/STATEMENT\.md: differs at byte \d+ \(expected \d+ bytes, found \d+\)$/m);
  assert.equal(changed.lines.some(l => l.startsWith('workspaces-check ok')), false, 'a finding never prints the success line');
  assert.equal(changed.lines.filter(l => l.includes('STATEMENT.md')).length, 1, 'only the changed file is named');
});

test('The_check_names_a_missing_file_an_added_file_and_a_lost_execute_bit_and_refuses_a_dirty_checkout', async t => {
  const checkout = await committedCheckout(t);
  git(checkout.dir, 'rm', '-q', 'csharp/05-key-value-server/PACKAGES.md');
  writeFileSync(path.join(checkout.dir, 'node/10-dag-scheduler/extra.txt'), 'added');
  git(checkout.dir, 'add', '-A');
  git(checkout.dir, 'commit', '-q', '-m', 'a missing and an added file');
  const checked = await run(['workspaces', '--check', '--root', checkout.dir]);
  assert.equal(checked.code, 1);
  assert.match(checked.lines.join('\n'), /csharp\/05-key-value-server\/PACKAGES\.md: is missing/);
  assert.match(checked.lines.join('\n'), /node\/10-dag-scheduler\/extra\.txt: is not part of the workspace/);
  writeFileSync(path.join(checkout.dir, 'stray.txt'), 'x');
  const dirty = await run(['workspaces', '--check', '--root', checkout.dir]);
  assert.equal(dirty.code, 4, 'a checkout with an untracked file is refused, not judged');
  assert.match(dirty.lines.join('\n'), /not clean/);
});

test('The_check_function_is_exported_for_the_launch_half_and_gives_the_commit_and_the_findings', async t => {
  const { checkWorkspaces } = await import('../lib/workspaces-check.mjs');
  const { freezeContext } = await import('../lib/workspaces-build.mjs');
  const checkout = await committedCheckout(t);
  const ctx = freezeContext({ home: '/home/nobody', exploreFn: () => ({ ok: true, value: pinsFor() }) });
  const ok = await checkWorkspaces({ root: checkout.dir, ctx: ctx.value });
  assert.deepEqual([ok.ok, ok.value.commit, ok.value.findings], [true, checkout.head, []]);
  changeAndCommit(checkout.dir, 'csharp/04-rule-api/STATEMENT.md', bytes => Buffer.concat([bytes, Buffer.from('x')]));
  const bad = await checkWorkspaces({ root: checkout.dir, ctx: ctx.value });
  assert.equal(bad.ok && bad.value.findings.length > 1, true, 'a finding is a value, not a throw');
});

test('The_copy_refuses_a_dirty_worktree_a_worktree_at_another_commit_and_an_existing_workspace', async t => {
  const m = await made(t);
  assert.equal(m.code, 0, m.lines.join('\n'));
  const dirty = worktreeAt(t, m.repo);
  writeFileSync(path.join(dirty, 'stray.txt'), 'x');
  const d = await run(['workspaces-copy', '--from', m.out, '--into', dirty]);
  assert.equal(d.code, 4, d.lines.join('\n'));
  assert.match(d.lines.join('\n'), /is not clean: stray\.txt/);
  assert.equal(existsSync(path.join(dirty, 'csharp')), false, 'nothing was copied into a dirty worktree');

  const moved = worktreeAt(t, m.repo);
  writeFileSync(path.join(moved, 'other.txt'), 'x');
  git(moved, 'add', '-A');
  git(moved, 'commit', '-q', '-m', 'one more commit');
  const o = await run(['workspaces-copy', '--from', m.out, '--into', moved]);
  assert.equal(o.code, 4, o.lines.join('\n'));
  assert.match(o.lines.join('\n'), /not [0-9a-f]{12}, the commit the workspaces were made from/);
  assert.equal(existsSync(path.join(moved, 'csharp')), false);

  const present = worktreeAt(t, m.repo);
  mkdirSync(path.join(present, 'csharp/04-rule-api'), { recursive: true });
  writeFileSync(path.join(present, 'csharp/04-rule-api/App.csproj'), 'a workspace of another text');
  const p = await run(['workspaces-copy', '--from', m.out, '--into', present]);
  assert.equal(p.code, 4, p.lines.join('\n'));
  assert.match(p.lines.join('\n'), /csharp\/04-rule-api is already in the worktree and differs/);
  assert.equal(existsSync(path.join(present, 'node')), false, 'a differing folder refuses all: not one other folder was copied');

  const clean = worktreeAt(t, m.repo);
  const c = await run(['workspaces-copy', '--from', m.out, '--into', clean]);
  assert.equal(c.code, 0, c.lines.join('\n'));
  for (const name of FOLDERS) assert.equal(treeOf(path.join(clean, name)), treeOf(path.join(m.out, name)), `${name} is in place and equal`);
  assert.deepEqual(statusOf(clean).filter(l => !FOLDERS.some(f => l.includes(f.split('/')[0]))), [], 'nothing but the twelve folders');
});

test('The_copy_refuses_a_source_folder_changed_after_it_was_made_and_a_manifest_that_is_not_the_twelve', async t => {
  const m = await made(t);
  const target = worktreeAt(t, m.repo);
  writeFileSync(path.join(m.out, 'node/08-ledger-api/STATEMENT.md'), 'edited by hand');
  const r = await run(['workspaces-copy', '--from', m.out, '--into', target]);
  assert.equal(r.code, 4, r.lines.join('\n'));
  assert.match(r.lines.join('\n'), /no longer hashes to the manifest's tree hash/);
  const manifest = path.join(m.out, 'workspaces.json');
  const parsed = JSON.parse(readFileSync(manifest, 'utf8'));
  delete parsed.trees['node/08-ledger-api'];
  writeFileSync(manifest, JSON.stringify(parsed));
  const fewer = await run(['workspaces-copy', '--from', m.out, '--into', target]);
  assert.equal(fewer.code, 4);
  assert.match(fewer.lines.join('\n'), /not the manifest of the twelve workspaces/);
  assert.equal(existsSync(path.join(target, 'csharp')), false);
});

test('The_writer_refuses_a_destination_inside_a_git_worktree', async t => {
  const repo = taskRepo(t);
  const inside = path.join(repo.dir, 'ws');
  const r = await run(['workspaces', '--tag', repo.tag, '--task-repo', repo.dir, '--out', inside]);
  assert.equal(r.code, 4, r.lines.join('\n'));
  assert.match(r.lines.join('\n'), /the destination .* is under .*holds a \.git entry|inside the git worktree/);
  assert.equal(existsSync(inside), false, 'nothing was made inside the worktree');
  const wt = worktreeAt(t, repo);
  const linked = await run(['workspaces', '--tag', repo.tag, '--task-repo', repo.dir, '--out', path.join(wt, 'ws')]);
  assert.equal(linked.code, 4, 'a linked worktree is refused as well');
  const outside = await made(t);
  assert.equal(outside.code, 0, 'a destination outside every repository is accepted');
});

test('A_workspace_writer_killed_before_its_rename_leaves_no_output_folder_and_the_restart_writes_it_whole', async t => {
  const repo = taskRepo(t);
  const parent = folder(t, 'wsout');
  const out = path.join(parent, 'ws');
  const argv = ['workspaces', '--tag', repo.tag, '--task-repo', repo.dir, '--out', out];
  killed(argv, 'beforeRename');
  assert.equal(existsSync(out), false, 'no output folder, whole or half');
  assert.equal(stagingIn(parent).length, 2, 'only an owned staging folder and its claim are left');
  const again = await run(argv);
  assert.equal(again.code, 0, again.lines.join('\n'));
  assert.deepEqual(stagingIn(parent), [], 'the restart removed the dead owner\'s staging folder and its claim');
  assert.equal(again.lines.some(l => /removed the staging folder/.test(l)), true, 'and said so');
  for (const [name, files] of Object.entries(sessionWorkspaces())) assert.equal(treeOf(path.join(out, name)), treeHashOfFiles(files), `${name}: the whole workspace`);
  const second = path.join(folder(t, 'wsout'), 'ws');
  assert.equal((await run(['workspaces', '--tag', repo.tag, '--task-repo', repo.dir, '--out', second])).code, 0);
  assert.equal(readFileSync(path.join(second, 'workspaces.json'), 'utf8'), readFileSync(path.join(out, 'workspaces.json'), 'utf8'), 'a rerun after an interruption gives the same bytes');
});

test('A_restart_leaves_a_staging_folder_whose_owner_still_runs_and_names_it', async t => {
  const repo = taskRepo(t);
  const parent = folder(t, 'wsout');
  const live = path.join(parent, owned.ownedName('stage'));
  owned.writeOwner(live);
  mkdirSync(live);
  const r = await run(['workspaces', '--tag', repo.tag, '--task-repo', repo.dir, '--out', path.join(parent, 'ws')]);
  assert.equal(r.code, 0, r.lines.join('\n'));
  assert.equal(existsSync(live), true, 'a live owner\'s folder is never removed');
  assert.equal(r.lines.some(l => l.includes(live) && /still running/.test(l)), true, 'it is named');
  owned.removeQuietly(live);
});

test('A_copy_killed_mid_folder_leaves_only_an_owned_staging_folder_that_the_restart_removes_and_no_half_workspace', async t => {
  const m = await made(t);
  const target = worktreeAt(t, m.repo);
  const argv = ['workspaces-copy', '--from', m.out, '--into', target];
  killed(argv, 'beforeRename', 5);
  assert.equal(stagingIn(target).length, 2, 'one owned staging folder and its claim');
  const present = FOLDERS.filter(f => existsSync(path.join(target, f)));
  assert.equal(present.length, 5, 'five folders were renamed before the kill');
  for (const name of present) assert.equal(treeOf(path.join(target, name)), treeOf(path.join(m.out, name)), `${name}: whole, never half`);
  const again = await run(argv);
  assert.equal(again.code, 0, again.lines.join('\n'));
  assert.deepEqual(stagingIn(target), [], 'the restart removed the dead owner\'s staging folder and its claim');
  assert.deepEqual(claimsIn(target), []);
});

test('A_copy_killed_after_3_of_12_folders_is_resumed_by_a_rerun_that_skips_the_3_and_copies_the_other_9', async t => {
  const m = await made(t);
  const target = worktreeAt(t, m.repo);
  const argv = ['workspaces-copy', '--from', m.out, '--into', target];
  killed(argv, 'beforeRename', 3);
  const done = FOLDERS.slice(0, 3);
  assert.deepEqual(FOLDERS.filter(f => existsSync(path.join(target, f))), done, 'the first three were copied');
  const rerun = await run(argv);
  assert.equal(rerun.code, 0, rerun.lines.join('\n'));
  assert.deepEqual(rerun.lines.filter(l => l.startsWith('skipped ')).map(l => l.split(' ')[1].replace(/:$/, '')), done, 'the three are skipped and named');
  assert.deepEqual(rerun.lines.filter(l => l.startsWith('copied ')).map(l => l.split(' ')[1].replace(/:$/, '')), FOLDERS.slice(3), 'the other nine are copied');
  assert.match(rerun.lines.at(-1), /9 copied, 3 skipped/);
  for (const name of FOLDERS) assert.equal(treeOf(path.join(target, name)), treeOf(path.join(m.out, name)), `${name}: equal to the source`);
  assert.deepEqual(stagingIn(target), []);
  const third = await run(argv);
  assert.match(third.lines.at(-1), /0 copied, 12 skipped/, 'a second rerun changes nothing');
});

test('A_resumed_copy_refuses_a_present_folder_that_differs_even_when_other_folders_are_equal', async t => {
  const m = await made(t);
  const target = worktreeAt(t, m.repo);
  const argv = ['workspaces-copy', '--from', m.out, '--into', target];
  killed(argv, 'beforeRename', 3);
  writeFileSync(path.join(target, FOLDERS[1], 'STATEMENT.md'), 'changed after the kill');
  const rerun = await run(argv);
  assert.equal(rerun.code, 4, rerun.lines.join('\n'));
  assert.match(rerun.lines.join('\n'), new RegExp(`${FOLDERS[1]} is already in the worktree and differs`));
  assert.equal(existsSync(path.join(target, FOLDERS[3])), false, 'nothing more was copied');
});

test('The_write_mode_and_the_check_mode_refuse_each_others_flags', async t => {
  const repo = taskRepo(t);
  const a = await run(['workspaces', '--check', '--root', repo.dir, '--out', '/x']);
  assert.equal(a.code, 4);
  const b = await run(['workspaces', '--tag', TAG, '--task-repo', repo.dir]);
  assert.equal(b.code, 4);
  assert.match(b.lines.join('\n'), /needs --out/);
  const c = await run(['workspaces-copy', '--from', '/x']);
  assert.equal(c.code, 4);
});
