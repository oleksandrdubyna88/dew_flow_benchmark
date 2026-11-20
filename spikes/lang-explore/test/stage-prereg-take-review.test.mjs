// F3.S1 (b), PR 5, the Opus review of the take (ten findings): `stage-sync` moves only the series' own branch, `origin/main` is the full ref name, the freeze check and the locks cover every series, a path the take removes is checked before it is
// removed, a person's edit is never overwritten, a folder that is not the submodule's own checkout is never given to git, the `commit:` token is a whole word, a git child that outlives a kill is waited for, and a few small gaps. Every repository
// is a temporary one with a bare origin; nothing is real. A kill is a real SIGKILL of a child (`stage-take-helpers.mjs`).
import assert from 'node:assert/strict';
import { existsSync, mkdirSync, readFileSync, renameSync, rmSync, symlinkSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { git as guardGit } from '../../minigit-bench/lib/prereg-guard.mjs';
import { acquireSeriesLock } from '../../minigit-bench/lib/series-lock.mjs';
import { tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { acceptHarness } from '../lib/stage-accept.mjs';
import { stageSync } from '../lib/stage-branch.mjs';
import { gone } from '../lib/stage-take.mjs';
import { cmdStageSync } from '../lib/stage-prereg-commands.mjs';
import { runMapFileOf } from '../lib/series.mjs';
import { loadSeries } from '../lib/series.mjs';
import { readRunMap, writeRunMap } from '../../minigit-bench/lib/run-map.mjs';
import { FIX_FILE, OLD_FILE, REASON, accept, datedLine, fixedOnMain, fixedSubmodule, seriesOf, stateOf } from './stage-accept-fixture.mjs';
import { AMENDMENTS_FILE, SEED, SEED_FILE, git, onMain, write } from './stage-prereg-fixture.mjs';
import { assertTaken, gitFile, inChild, journalOf, leftovers } from './stage-take-helpers.mjs';

const syncLines = async (f, io, values = {}) => {
  const lines = [];
  const code = await cmdStageSync(values, { repo: f.repo, home: io.home, say: l => lines.push(l) });
  return { code, lines: lines.join('|') };
};

// ---- 1: the series' own branch ---------------------------------------------------------------------------------------------------------------------------------------------------------------------

test('A_clean_feature_branch_that_the_series_does_not_name_is_refused_by_both_forms_of_stage_sync_and_nothing_moves', async () => {
  const { f, hex } = await fixedOnMain();
  const { io } = await seriesOf(f, { branch: 'the-series-branch' });
  onMain(f, () => write(f.repo, SEED_FILE, `${SEED}\n`), 'main: the seed, which a sync would take');
  git(f.repo, 'checkout', '-q', '-b', 'feature');
  const before = stateOf(f);
  const plain = await syncLines(f, io);
  const accepted = await accept(f, hex, { home: io.home });
  for (const [name, r] of [['plain', plain], ['accept', accepted]]) {
    assert.equal(r.code, 4, `${name}: ${r.lines}`);
    assert.match(r.lines, /the branch feature is not the series' own branch: a Stage 1 series records the-series-branch/, name);
  }
  assert.deepEqual(stateOf(f), before, 'nothing moved');
});

test('The_series_own_branch_passes_and_a_series_that_records_no_branch_in_the_history_of_HEAD_or_no_series_at_all_is_refused', async () => {
  const { f, hex } = await fixedOnMain();
  const own = await seriesOf(f);
  assert.equal((await syncLines(f, own.io)).code, 0, 'the control: the branch the series records');
  assert.equal((await accept(f, hex, { home: own.io.home })).code, 0, 'and the acceptance');
  const other = await fixedOnMain();
  const elsewhere = await seriesOf(other.f, { commit: 'b'.repeat(40) });
  const unknown = await syncLines(other.f, elsewhere.io);
  assert.equal(unknown.code, 4, unknown.lines);
  assert.match(unknown.lines, /the tag's commit bbbbbbbbbbbb of the series is not in the history of the branch series/);
  const none = await syncLines(other.f, { home: tmpDir('stage-home') });
  assert.equal(none.code, 4, none.lines);
  assert.match(none.lines, /no Stage 1 series is made under the explore root: there is nothing to sync for/);
});

// ---- 2: the full name of the remote ref ---------------------------------------------------------------------------------------------------------------------------------------------------------

test('A_local_branch_named_origin_main_does_not_stand_for_the_remote_ref_and_an_unreviewed_commit_is_refused', async () => {
  const { f, hex, commit: fix } = await fixedOnMain({ merged: false });
  git(f.repo, 'branch', 'origin/main', fix);
  const before = stateOf(f);
  const refused = await acceptHarness({ repo: f.repo, hex });
  assert.equal(refused.ok, false, 'the commit is on a branch of the origin that is not main');
  assert.match(refused.reason, new RegExp(`commit ${fix.slice(0, 12)} is not reachable from origin/main`));
  assert.deepEqual(stateOf(f), before);
});

// ---- 3: every series is locked and freeze-checked ----------------------------------------------------------------------------------------------------------------------------------------------

const brokenSeries = home => { const dir = path.join(home, 'rsd', 'lang-v2', 'explore', 'x-20261120-dead'); mkdirSync(dir, { recursive: true }); writeFileSync(path.join(dir, 'explore-series.json'), 'not json'); return dir; };

test('A_series_whose_state_cannot_be_read_refuses_both_forms_of_stage_sync_naming_it', async () => {
  const { f, hex } = await fixedOnMain();
  const { io } = await seriesOf(f);
  const dir = brokenSeries(io.home);
  const before = stateOf(f);
  for (const [name, r] of [['plain', await syncLines(f, io)], ['accept', await accept(f, hex, { home: io.home })]]) {
    assert.equal(r.code, 4, `${name}: ${r.lines}`);
    assert.match(r.lines, new RegExp(`the series ${dir} cannot be read \\(.*\\), so stage-sync cannot be told whether a unit runs or a data session has left planned`), name);
  }
  assert.deepEqual(stateOf(f), before);
});

test('With_a_series_flag_every_Stage_1_series_is_freeze_checked_and_locked_and_a_series_outside_the_root_is_refused', async () => {
  const { f, hex } = await fixedOnMain();
  const first = await seriesOf(f);
  const second = await seriesOf(f, { home: first.io.home, rand: () => 'cd34' });
  const file = runMapFileOf(second.dir);
  writeRunMap(file, { ...readRunMap(file).value, sessions: { a1b2c3: { rid: 'a1b2c3', kind: 'data', status: 'ran' } } });
  const before = stateOf(f);
  const frozen = await accept(f, hex, { home: first.io.home, repo: f.repo });
  assert.equal(frozen.code, 4, 'the series flag is not given: the frozen one is found');
  const named = await syncLines(f, first.io, { series: first.dir });
  assert.equal(named.code, 0, 'plain sync of a named series passes: nothing is frozen for it');
  const lines = [];
  const code = await cmdStageSync({ 'accept-harness': hex, series: first.dir }, { repo: f.repo, home: first.io.home, say: l => lines.push(l) });
  assert.equal(code, 4, lines.join('|'));
  assert.match(lines.join('|'), new RegExp(`a data session has left planned in the series ${second.dir}`), 'the other series is checked though --series names the first');
  const unit = acquireSeriesLock({ seriesDir: second.dir });
  assert.equal(unit.ok, true, unit.reason);
  const locked = await syncLines(f, first.io, { series: first.dir });
  unit.value.release();
  assert.equal(locked.code, 4, locked.lines);
  assert.match(locked.lines, new RegExp(`stage-sync does not run while a unit runs: .*${path.basename(second.dir)}`), 'the lock of the other series is taken too');
  const outside = (await seriesOf(f)).dir;
  const away = await syncLines(f, first.io, { series: outside });
  assert.equal(away.code, 4, away.lines);
  assert.match(away.lines, /is not a series under the explore root of this home/);
  assert.deepEqual(stateOf(f).slice(1), before.slice(1));
});

// ---- 5: removing a path is checked first -----------------------------------------------------------------------------------------------------------------------------------------------------------

test('A_removed_path_that_is_now_a_folder_is_refused_by_name_and_the_journal_is_kept', async () => {
  const { f, hex } = await fixedOnMain();
  assert.equal(inChild('accept', f, { hex, point: 'refMoved' }).killed, true);
  const file = path.join(f.repo, OLD_FILE);
  rmSync(file);
  mkdirSync(file);
  writeFileSync(path.join(file, 'kept.txt'), 'a person\'s\n');
  const refused = await stageSync({ repo: f.repo });
  assert.equal(refused.ok, false);
  assert.match(refused.reason, /spikes\/lang-explore\/lib\/old\.mjs is a folder, not a file: it is not removed/);
  assert.deepEqual([existsSync(path.join(file, 'kept.txt')), journalOf(f)], [true, true], 'the folder is untouched and the next run retries');
});

test('A_removal_that_fails_is_a_refusal_value_naming_the_path_and_the_error_never_a_throw', () => {
  const folder = tmpDir('not-a-file');
  const refused = gone(folder);
  assert.equal(refused.ok, false);
  assert.match(refused.reason, new RegExp(`^cannot remove ${folder}: .*(is a directory|EISDIR)`));
  assert.equal(gone(path.join(folder, 'absent.txt')).ok, true, 'a file that is not there is gone');
});

test('A_parent_folder_that_is_a_link_is_refused_and_nothing_is_written_or_removed_through_it', async () => {
  const { f, hex } = await fixedOnMain();
  assert.equal(inChild('accept', f, { hex, point: 'refMoved' }).killed, true);
  const lib = path.join(f.repo, 'spikes', 'lang-explore', 'lib');
  renameSync(lib, `${lib}.real`);
  symlinkSync(`${lib}.real`, lib);
  const refused = await stageSync({ repo: f.repo });
  assert.equal(refused.ok, false);
  assert.match(refused.reason, /spikes\/lang-explore\/lib is a link on the way to spikes\/lang-explore\/lib\/(old|x)\.mjs, a path of the take: nothing is written or removed through a link/);
  assert.deepEqual([existsSync(path.join(`${lib}.real`, 'old.mjs')), readFileSync(path.join(`${lib}.real`, 'x.mjs'), 'utf8') === '// the fix, reviewed on main\n', journalOf(f)], [true, false, true]);
});

// ---- 6: a person's edit is never overwritten --------------------------------------------------------------------------------------------------------------------------------------------------

test('A_file_edited_between_the_build_and_the_working_tree_step_refuses_the_take_and_keeps_the_edit', async () => {
  const { f, hex } = await fixedOnMain();
  const head = git(f.repo, 'rev-parse', 'HEAD');
  const seam = async step => { if (step === 'indexBuilt') write(f.repo, FIX_FILE, '// a person is editing this\n'); };
  const refused = await acceptHarness({ repo: f.repo, hex, seam });
  assert.equal(refused.ok, false, 'the tree is checked again just before the working tree is touched');
  assert.match(refused.reason, /the tree is not clean: spikes\/lang-explore\/lib\/x\.mjs is staged or changed/);
  assert.deepEqual([git(f.repo, 'rev-parse', 'HEAD'), readFileSync(path.join(f.repo, FIX_FILE), 'utf8'), leftovers(f)], [head, '// a person is editing this\n', []], 'the branch did not move, the edit is there');
});

test('A_working_file_that_is_neither_the_old_nor_the_new_blob_stops_the_recovery_naming_it_and_touches_nothing', async () => {
  const { f, hex } = await fixedOnMain();
  assert.equal(inChild('accept', f, { hex, point: 'refMoved' }).killed, true);
  write(f.repo, FIX_FILE, '// a person edited this after the kill\n');
  const refused = await stageSync({ repo: f.repo });
  assert.equal(refused.ok, false);
  assert.match(refused.reason, /the working file spikes\/lang-explore\/lib\/x\.mjs is neither the old nor the new blob of the interrupted take: a person edited it/);
  assert.deepEqual([readFileSync(path.join(f.repo, FIX_FILE), 'utf8'), existsSync(path.join(f.repo, OLD_FILE)), journalOf(f)], ['// a person edited this after the kill\n', true, true]);
});

// ---- 7: a folder that is not the submodule's own checkout ---------------------------------------------------------------------------------------------------------------------------------

test('A_submodule_folder_that_is_a_plain_directory_is_refused_and_the_parents_HEAD_is_not_detached', async () => {
  const { f, sub, fixed, hex } = await fixedSubmodule();
  git(f.repo, 'fetch', '-q', sub, 'main');
  rmSync(sub, { recursive: true });
  mkdirSync(sub);
  const before = [git(f.repo, 'rev-parse', 'HEAD'), git(f.repo, 'symbolic-ref', 'HEAD')];
  const refused = await acceptHarness({ repo: f.repo, hex });
  assert.equal(refused.ok, false);
  assert.match(refused.reason, /the submodule \.agents\/conventions is not checked out: run `git submodule update --init`/);
  assert.deepEqual([git(f.repo, 'rev-parse', 'HEAD'), git(f.repo, 'symbolic-ref', 'HEAD')], before, 'the parent repository keeps its branch and its commit');
});

// ---- 8: the commit token is a whole word ----------------------------------------------------------------------------------------------------------------------------------------------------------

test('A_commit_token_followed_by_more_characters_is_refused_and_one_in_prose_is_not_a_second_token', async () => {
  for (const tail of ['junk', '0']) {
    const { f, hex } = await fixedOnMain({ line: fix => datedLine(fix, `commit:${fix.commit}${tail} ${REASON}`) });
    const refused = await accept(f, hex);
    assert.equal(refused.code, 4, `${tail}: ${refused.lines}`);
    assert.match(refused.lines, /does not begin its reason with `commit:<40 hex>`/, tail);
  }
  const { f, hex } = await fixedOnMain({ line: fix => datedLine(fix, `commit:${fix.commit} ${REASON}; this is not a recommit: of anything`) });
  const taken = await accept(f, hex);
  assert.equal(taken.code, 0, taken.lines);
});

// ---- 9: a git child that outlives a kill, and a stale index lock ------------------------------------------------------------------------------------------------------------------------------

test('A_branch_lock_file_with_the_branch_still_at_the_old_commit_stops_the_recovery_and_keeps_the_journal', async () => {
  const { f, hex } = await fixedOnMain();
  assert.equal(inChild('accept', f, { hex, point: 'journalWritten' }).killed, true);
  const lock = path.join(f.repo, '.git', 'refs', 'heads', 'series.lock');
  writeFileSync(lock, '');
  const refused = await stageSync({ repo: f.repo });
  assert.equal(refused.ok, false);
  assert.match(refused.reason, /refs\/heads\/series\.lock exists: a git process may still be moving the branch: nothing was dropped/);
  assert.equal(journalOf(f), true, 'the journal is kept');
  rmSync(lock);
  assert.equal((await stageSync({ repo: f.repo })).ok, true, 'without the lock the take never landed and the journal is dropped');
  assert.equal(journalOf(f), false);
});

test('A_stale_index_lock_of_the_take_with_no_journal_is_removed_and_the_take_proceeds', async () => {
  const { f, hex, identity } = await fixedOnMain();
  const head = git(f.repo, 'rev-parse', 'HEAD');
  writeFileSync(gitFile(f, 'stage-take.index.lock'), '');
  const taken = await acceptHarness({ repo: f.repo, hex });
  assert.equal(taken.ok, true, taken.reason);
  await assertTaken({ f, head, identity });
  assert.equal(existsSync(gitFile(f, 'stage-take.index.lock')), false);
});

// ---- 10: small items ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

test('A_harness_path_with_a_newline_in_its_name_is_taken_with_the_others', async () => {
  const odd = 'spikes/lang-explore/lib/odd\nname.mjs';
  const { f, hex } = await fixedOnMain({ plus: repo => write(repo, odd, '// odd\n') });
  const taken = await acceptHarness({ repo: f.repo, hex });
  assert.equal(taken.ok, true, taken.reason);
  assert.equal(taken.value.changed.includes(odd), true, 'the path is among the changed ones');
  assert.equal(git(f.repo, 'ls-tree', '-r', '--name-only', '-z', 'HEAD', '--', 'spikes/lang-explore/lib').split('\0').includes(odd), true, 'and in the commit');
});

test('A_branch_that_holds_a_commit_whose_harness_is_not_the_one_its_message_names_is_refused_by_the_recovery', async () => {
  const { f, hex } = await fixedOnMain();
  assert.equal(inChild('accept', f, { hex, point: 'journalWritten' }).killed, true);
  const journal = JSON.parse(readFileSync(gitFile(f, 'stage-take.journal'), 'utf8'));
  const forged = git(f.repo, 'commit-tree', git(f.repo, 'rev-parse', `${journal.new}^{tree}`), '-p', journal.old, '-m', 'forged', '-m', `commit:${'1'.repeat(40)}\nharness:aaaaaaaaaaaa`);
  writeFileSync(gitFile(f, 'stage-take.journal'), `${JSON.stringify({ ...journal, new: forged })}\n`);
  git(f.repo, 'update-ref', journal.ref, forged);
  const refused = await stageSync({ repo: f.repo });
  assert.equal(refused.ok, false);
  assert.match(refused.reason, /the harness folders of the interrupted take's commit [0-9a-f]{12} have the identity [0-9a-f]{12}, not the aaaaaaaaaaaa its message names: nothing was written/);
  assert.deepEqual([existsSync(path.join(f.repo, OLD_FILE)), journalOf(f)], [true, true], 'the working tree is the old one and the journal is kept');
});

test('A_git_answer_that_was_cut_at_the_size_limit_is_a_failure_that_says_so_never_a_shorter_text', async () => {
  const repo = tmpDir('big-repo');
  guardGit(repo, ['init', '-q']);
  writeFileSync(path.join(repo, 'big.txt'), 'x'.repeat(9 * 1024 * 1024));
  git(repo, 'add', 'big.txt');
  git(repo, '-c', 'user.name=t', '-c', 'user.email=t@example.invalid', 'commit', '-q', '-m', 'big');
  const shown = await guardGit(repo, ['show', 'HEAD:big.txt']);
  assert.equal(shown.ok, false, 'a text that was cut is no answer');
  assert.match(shown.reason, /git show output was truncated at 8388608 bytes/);
  assert.equal((await guardGit(repo, ['rev-parse', 'HEAD'])).ok, true, 'a short answer is still an answer');
});

test('The_state_of_a_series_made_by_stage_init_records_the_branch_it_was_made_on', async () => {
  const { f } = await fixedOnMain();
  const { dir } = await seriesOf(f, { branch: 'series' });
  assert.equal(loadSeries(dir).value.state.branch, 'series');
});
