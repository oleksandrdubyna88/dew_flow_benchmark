// A bench candidate gets the baseline exemption of the full bench scan (build step 2b, the candidate item): a line that was already in the same path at the task's baseline commit, and that the candidate holds no more often than the
// baseline did, is a collision and not a leak. The candidate's lines are read from ITS commit through git (the tree reader), never from the working file; one shared function (`applyBaseline`) decides, for both paths. The task
// repository's candidate path has no exemption. INVENTED needle in a temporary bench-like repository whose baseline commit holds the colliding line.
import '../../minigit-bench/test/fake-home.mjs';
import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { rmSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { ok } from '../../minigit-bench/lib/result.mjs';
import { tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { main } from '../explore.mjs';
import { NEEDLE, NEEDLE_LINES, captureStreams, commit, git, holdsNeedle, newRepo, privateSide, runCheck } from './quick-leak-repos-fixture.mjs';

const SHARED = 'docs/shared.md';
const baselineText = `a shared page\n${NEEDLE_LINES[0]}\nthe end\n`;

/** A bench-like repository whose first commit (the baseline) holds the colliding line, and the private side whose task 04 names that commit as its baseline. */
function benchWithBaseline() {
  const bench = newRepo({ 'README.md': 'a readme\n', [SHARED]: baselineText });
  const baseline = git(bench, 'rev-parse', 'HEAD').trim();
  const side = privateSide();
  const task = { ...side.explore.tasks['04'], baselineCommit: baseline, heldoutBorn: '2999-01-01T00:00:00Z' };
  return { bench, baseline, side: { ...side, explore: { ...side.explore, tasks: { '04': task } } } };
}

const candidateRun = ({ bench, side }, sha) => runCheck({ ...side, benchRepo: bench, values: { 'candidate-repo': bench, candidate: sha } });

test('A_bench_candidate_whose_only_finding_is_a_baseline_collision_is_exit_0_like_the_full_scan_and_says_how_many', async () => {
  const w = benchWithBaseline();
  const sha = commit(w.bench, { 'docs/other.md': 'another page\n' }, 'a candidate');
  const full = await runCheck({ ...w.side, benchRepo: w.bench, values: { 'task-repo': newRepo() } });
  assert.equal(full.exit, 0, `the full scan exempts the collision: ${full.said}`);
  const r = await candidateRun(w, sha);
  assert.equal(r.exit, 0, r.said);
  assert.match(r.said, /collisions with the baseline commit \(not leaks\): task 04: 1/);
  assert.equal(holdsNeedle(r.said + r.progress), false);
  const clone = path.join(tmpDir('r-clone'), 'task');
  execFileSync('git', ['clone', '-q', w.bench, clone]);
  const asTask = await runCheck({ ...w.side, benchRepo: w.bench, values: { 'candidate-repo': clone, candidate: sha }, env: { LANG_V2_TASK_REPO: clone } });
  assert.match(asTask.said, /of task repository\]/);
  assert.equal(asTask.exit, 5, `the task repository has no baseline, though its history holds the baseline commit: ${asTask.said}`);
});

test('The_same_collision_changed_in_the_candidate_commit_is_exit_5_though_the_working_file_is_the_baseline_one', async () => {
  const w = benchWithBaseline();
  const changed = baselineText.replace(NEEDLE_LINES[0], NEEDLE_LINES[0].toUpperCase());
  const sha = commit(w.bench, { [SHARED]: changed }, 'a candidate that changes the line');
  writeFileSync(path.join(w.bench, SHARED), baselineText);
  const r = await candidateRun(w, sha);
  assert.equal(r.exit, 5, `the lines are the commit's, not the working file's: ${r.said}`);
  assert.match(r.said, /candidate [0-9a-f]{12}:docs\/shared\.md/);
  assert.equal(holdsNeedle(r.said + r.progress), false);
});

test('The_reverse_the_working_file_changed_and_the_commit_clean_is_exit_0', async () => {
  const w = benchWithBaseline();
  const sha = commit(w.bench, { 'docs/other.md': 'another page\n' }, 'a candidate');
  writeFileSync(path.join(w.bench, SHARED), `${baselineText}${NEEDLE_LINES[0]}\n${NEEDLE_LINES[0]}\n`);
  const r = await candidateRun(w, sha);
  assert.equal(r.exit, 0, `the working file holds the line more often than the baseline, the commit does not: ${r.said}`);
});

test('A_line_the_candidate_holds_more_often_than_the_baseline_did_is_not_exempt', async () => {
  const w = benchWithBaseline();
  const sha = commit(w.bench, { [SHARED]: `${baselineText}${NEEDLE_LINES[0]}\n` }, 'a candidate that repeats the line');
  writeFileSync(path.join(w.bench, SHARED), baselineText);
  const r = await candidateRun(w, sha);
  assert.equal(r.exit, 5, r.said);
});

test('A_colliding_file_whose_blob_cannot_be_read_is_exit_4_never_exempt', async () => {
  const w = benchWithBaseline();
  const sha = commit(w.bench, { 'docs/other.md': 'another page\n' }, 'a candidate');
  const oid = git(w.bench, 'rev-parse', `${sha}:${SHARED}`).trim();
  rmSync(path.join(w.bench, '.git', 'objects', oid.slice(0, 2), oid.slice(2)));
  const r = await candidateRun(w, sha);
  assert.equal(r.exit, 4, r.said);
  assert.doesNotMatch(r.said, /clean/);
});

test('A_baseline_that_is_not_an_ancestor_of_the_candidate_gives_no_exemption_and_says_so', async () => {
  const bench = newRepo({ 'README.md': 'a readme\n' });
  const root = git(bench, 'rev-parse', 'HEAD').trim();
  const baseline = commit(bench, { [SHARED]: baselineText }, 'the baseline');
  commit(bench, { 'docs/more.md': 'a page after the baseline\n' }, 'after the baseline');
  git(bench, 'checkout', '-q', '-b', 'side', root);
  const side = commit(bench, { [SHARED]: baselineText }, 'the same page on a side branch');
  git(bench, 'checkout', '-q', 'main');
  const priv = privateSide();
  const task = { ...priv.explore.tasks['04'], baselineCommit: baseline, heldoutBorn: '2999-01-01T00:00:00Z' };
  const explore = { ...priv.explore, tasks: { '04': task } };
  const onMain = await runCheck({ ...priv, explore, benchRepo: bench, values: { 'candidate-repo': bench, candidate: git(bench, 'rev-parse', 'main').trim() } });
  assert.equal(onMain.exit, 0, `the baseline is in the history of main: ${onMain.said}`);
  const r = await runCheck({ ...priv, explore, benchRepo: bench, values: { 'candidate-repo': bench, candidate: side } });
  assert.equal(r.exit, 5, 'the baseline is not in the candidate\'s history, though it is in HEAD\'s: no exemption');
  assert.match(r.said, new RegExp(`baseline of task 04 gives no exemption: baselineCommit [0-9a-f]+ is not an ancestor of ${side}`));
});

test('A_bench_candidate_prints_no_text_on_stdout_or_stderr_with_or_without_an_exemption', async () => {
  const w = benchWithBaseline();
  const sha = commit(w.bench, { [SHARED]: baselineText.replace(NEEDLE_LINES[0], NEEDLE_LINES[0].toUpperCase()) }, 'a candidate that changes the line');
  const r = await captureStreams(() => main(['leakcheck', '--task', '04', '--candidate-repo', w.bench, '--candidate', sha], { home: w.side.home, repoRoot: w.bench, exploreFn: () => ok(w.side.explore), env: {} }));
  const [exit, out, err] = [r.value, [r.stdout], [r.stderr]];
  assert.equal(exit, 5, out.join(''));
  assert.notEqual(out.join('').length + err.join('').length, 0);
  assert.equal(holdsNeedle(out.join('')), false, 'stdout');
  assert.equal(holdsNeedle(err.join('')), false, 'stderr');
  assert.ok(NEEDLE.length > 0);
});
