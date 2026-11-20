// F3.S1 (b), PR 5, coai code round 2 (E2 and E3): `stage-sync --accept-harness` is killed in the middle of a take, and the next `stage-sync` finds the branch whole, either the old one or the new one. Every kill is a real SIGKILL of a child process
// that runs `acceptHarness` with a seam at the named step (`test/stage-take-child.mjs`): nothing in the dying process runs a rollback. A failed lookup is a `git` of the test's own, first on the child's PATH, that fails the one call it is told to.
// Every repository is a temporary one with a bare origin; nothing is real.
import assert from 'node:assert/strict';
import { existsSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { acceptHarness } from '../lib/stage-accept.mjs';
import { stageSync } from '../lib/stage-branch.mjs';
import { FIX_FILE, NEW_FILE, OLD_FILE, datedLine, fixedOnMain, fixedSubmodule, stateOf } from './stage-accept-fixture.mjs';
import { git } from './stage-prereg-fixture.mjs';
import { FIXED_TEXT, assertTaken, failingGit, gitFile, inChild, journalOf, leftovers, paidGuard } from './stage-take-helpers.mjs';

// ---- E2: a kill before the branch moves leaves the old branch whole -----------------------------------------------------------------------------------------------------------------------------------

for (const point of ['indexBuilt', 'journalWritten']) {
  test(`A_process_killed_at_${point}_leaves_the_old_branch_whole_and_the_next_stage_sync_runs_and_a_retry_takes`, async () => {
    const { f, hex, identity } = await fixedOnMain();
    const before = stateOf(f);
    const killed = inChild('accept', f, { hex, point });
    assert.equal(killed.killed, true, `the child was to die at ${point}: ${killed.stderr}`);
    assert.deepEqual(stateOf(f), before, 'the branch, the index and the tree are the old ones');
    const synced = await stageSync({ repo: f.repo });
    assert.equal(synced.ok, true, synced.reason);
    assert.deepEqual([stateOf(f), journalOf(f)], [before, false], 'the next stage-sync found nothing to take, dropped the journal the kill left and changed nothing');
    const retried = await acceptHarness({ repo: f.repo, hex });
    assert.equal(retried.ok, true, retried.reason);
    await assertTaken({ f, head: before[0], identity });
  });
}

// ---- E2: a kill after the branch moved is finished by the next stage-sync or the retry ---------------------------------------------------------------------------------------------------------

const AFTER_REF = ['refMoved', 'worktree:0', 'worktree:1', 'worktreeDone'];

test('A_process_killed_after_the_branch_moved_leaves_a_state_the_guard_names_and_the_next_stage_sync_finishes_at_every_step', async () => {
  for (const point of AFTER_REF) {
    const { f, hex, identity } = await fixedOnMain();
    const head = git(f.repo, 'rev-parse', 'HEAD');
    const killed = inChild('accept', f, { hex, point });
    assert.equal(killed.killed, true, `the child was to die at ${point}: ${killed.stderr}`);
    assert.equal(git(f.repo, 'rev-list', '--count', `${head}..HEAD`), '1', `${point}: the branch holds the new commit`);
    assert.match((await paidGuard(f)).reason, /an interrupted harness take is recorded in .*stage-take\.journal: .*run stage-sync, which finishes it, before any paid launch/, `${point}: the guard names the state`);
    const synced = await stageSync({ repo: f.repo });
    assert.equal(synced.ok, true, `${point}: ${synced.reason}`);
    await assertTaken({ f, head, identity });
    assert.equal((await acceptHarness({ repo: f.repo, hex })).value.commit, null, `${point}: a retry finds nothing to take`);
  }
});

test('A_process_killed_in_the_middle_of_the_working_tree_update_is_finished_by_the_retry_of_the_same_command', async () => {
  const { f, hex, identity } = await fixedOnMain();
  const head = git(f.repo, 'rev-parse', 'HEAD');
  assert.equal(inChild('accept', f, { hex, point: 'worktree:1' }).killed, true);
  assert.notEqual(git(f.repo, 'status', '--porcelain', '--untracked-files=all'), '', 'the tree is behind HEAD: a clean-tree check alone would refuse the retry');
  const retried = await acceptHarness({ repo: f.repo, hex });
  assert.deepEqual([retried.ok, retried.value.commit], [true, null], retried.reason);
  await assertTaken({ f, head, identity });
});

test('A_kill_during_the_move_of_the_shared_rules_checkout_is_finished_and_the_submodule_ends_at_the_new_commit', async () => {
  for (const point of ['refMoved', 'worktree:0', 'worktree:1', 'worktreeDone']) {
    const { f, sub, fixed, hex } = await fixedSubmodule();
    const killed = inChild('accept', f, { hex, point });
    assert.equal(killed.killed, true, `the child was to die at ${point}: ${killed.stderr}`);
    const synced = await stageSync({ repo: f.repo });
    assert.equal(synced.ok, true, `${point}: ${synced.reason}`);
    assert.deepEqual([git(f.repo, 'ls-tree', 'HEAD', '.agents/conventions').split(/\s+/)[2], git(sub, 'rev-parse', 'HEAD'), git(f.repo, 'status', '--porcelain', '--untracked-files=all'), leftovers(f)], [fixed, fixed, '', []], point);
  }
  const done = await fixedSubmodule();
  assert.equal(inChild('accept', done.f, { hex: done.hex, point: 'worktree:2' }).killed, false, 'the take has no step beyond worktree:1: the list of kill points is whole');
});

// ---- E2: the branch is moved only from the commit the take was built on ---------------------------------------------------------------------------------------------------------------------

test('A_branch_that_moves_while_the_take_is_built_is_refused_by_the_compare_and_swap_and_nothing_else_changes', async () => {
  const { f, hex, identity } = await fixedOnMain();
  const head = git(f.repo, 'rev-parse', 'HEAD');
  const seam = async step => { if (step === 'journalWritten') git(f.repo, 'commit', '-q', '--allow-empty', '-m', 'a commit of somebody else'); };
  const refused = await acceptHarness({ repo: f.repo, hex, seam });
  assert.equal(refused.ok, false);
  assert.match(refused.reason, /the branch refs\/heads\/series is no longer at [0-9a-f]{12}: nothing was taken/);
  assert.equal(git(f.repo, 'log', '-1', '--format=%s'), 'a commit of somebody else', 'the branch is where the other commit put it');
  assert.deepEqual([readFileSync(path.join(f.repo, FIX_FILE), 'utf8') === FIXED_TEXT, git(f.repo, 'status', '--porcelain', '--untracked-files=all'), leftovers(f)], [false, '', []], 'the tree is the old one, clean, and the journal is dropped');
  const retried = await acceptHarness({ repo: f.repo, hex });
  assert.equal(retried.ok, true, retried.reason);
  await assertTaken({ f, head: git(f.repo, 'rev-parse', 'HEAD~1'), identity });
});

test('A_journal_that_cannot_be_read_or_a_branch_that_moved_on_stops_stage_sync_naming_it_and_changes_nothing', async () => {
  const { f, hex } = await fixedOnMain();
  assert.equal(inChild('accept', f, { hex, point: 'journalWritten' }).killed, true);
  const before = stateOf(f);
  writeFileSync(gitFile(f, 'stage-take.journal'), 'not json\n');
  const unreadable = await stageSync({ repo: f.repo });
  assert.match(unreadable.reason, /stage-take\.journal cannot be read: /);
  assert.deepEqual(stateOf(f), before);
  const journal = JSON.stringify({ ref: 'refs/heads/series', old: 'a'.repeat(40), new: 'b'.repeat(40) });
  writeFileSync(gitFile(f, 'stage-take.journal'), `${journal}\n`);
  const moved = await stageSync({ repo: f.repo });
  assert.match(moved.reason, /the branch refs\/heads\/series is at [0-9a-f]{12}, which is neither aaaaaaaaaaaa nor bbbbbbbbbbbb/);
  assert.deepEqual([stateOf(f), existsSync(gitFile(f, 'stage-take.journal'))], [before, true], 'nothing was dropped or written');
});

// ---- E3: a lookup that fails is never taken for "the path is absent" ---------------------------------------------------------------------------------------------------------------------------------

test('A_failed_lookup_of_the_harness_entries_refuses_the_take_naming_it_and_writes_nothing', async () => {
  const { f, hex } = await fixedOnMain();
  const before = stateOf(f);
  const refused = inChild('accept', f, { hex, env: failingGit('ls-tree -r -z') });
  assert.equal(refused.result?.ok, false, `the lookup the code makes (ls-tree -r -z) failed: the take is refused: ${refused.stderr}`);
  assert.match(refused.result.reason, /git ls-tree exited 128: fatal: simulated lookup failure/);
  assert.deepEqual([stateOf(f), existsSync(path.join(f.repo, OLD_FILE)), existsSync(path.join(f.repo, NEW_FILE)), leftovers(f)], [before, true, false, []], 'no file was written or removed');
});

test('A_failed_lookup_of_either_tree_while_a_take_is_finished_refuses_naming_the_failure_and_removes_no_file', async () => {
  for (const which of ['old', 'new']) {
    const { f, hex, identity } = await fixedOnMain();
    const head = git(f.repo, 'rev-parse', 'HEAD');
    assert.equal(inChild('accept', f, { hex, point: 'refMoved' }).killed, true);
    const rev = which === 'old' ? head : git(f.repo, 'rev-parse', 'HEAD');
    const behind = git(f.repo, 'status', '--porcelain', '--untracked-files=all');
    const refused = inChild('sync', f, { env: failingGit(`ls-tree -r -z ${rev}`) });
    assert.equal(refused.result?.ok, false, `${which}: ${refused.stderr}`);
    assert.match(refused.result.reason, /git ls-tree exited 128: fatal: simulated lookup failure: the harness entries of the interrupted take cannot be read, so no file was removed or written/, which);
    assert.deepEqual([existsSync(path.join(f.repo, OLD_FILE)), existsSync(gitFile(f, 'stage-take.journal')), git(f.repo, 'status', '--porcelain', '--untracked-files=all')], [true, true, behind], `${which}: no file was removed, the journal is kept`);
    const synced = await stageSync({ repo: f.repo });
    assert.equal(synced.ok, true, `${which}: once git answers the take is finished: ${synced.reason}`);
    await assertTaken({ f, head, identity });
  }
});
