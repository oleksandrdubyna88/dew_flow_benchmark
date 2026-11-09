// D41 for the two-repository check: no needle text is ever printed. The planted-needle runs go through the command line's own entry (`main`) with `process.stdout` and `process.stderr` captured, for the history scan, the
// working-tree scan and the candidate scan, and neither output holds the needle, nor any line of it, nor a distinctive token of it. INVENTED needle, temp repositories.
import '../../minigit-bench/test/fake-home.mjs';
import assert from 'node:assert/strict';
import { writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { ok } from '../../minigit-bench/lib/result.mjs';
import { main } from '../explore.mjs';
import { NEEDLE, captureStreams, commit, git, holdsNeedle, newRepo, privateSide } from './quick-leak-repos-fixture.mjs';

/** `explore.mjs leakcheck ...` in this process with both output streams captured: `{ exit, stdout, stderr }`. */
async function captured(args, seams) {
  const r = await captureStreams(() => main(['leakcheck', ...args], seams));
  return { exit: r.value, stdout: r.stdout, stderr: r.stderr };
}

const seamsOf = (side, benchRepo) => ({ home: side.home, repoRoot: benchRepo, exploreFn: () => ok(side.explore), env: {} });

function assertNoText(r, what) {
  assert.notEqual(r.stdout.length + r.stderr.length, 0, `${what}: the run printed something (a capture of nothing proves nothing)`);
  assert.equal(holdsNeedle(r.stdout), false, `${what}: stdout`);
  assert.equal(holdsNeedle(r.stderr), false, `${what}: stderr`);
}

test('The_history_scan_finds_the_needle_and_neither_stdout_nor_stderr_holds_it', async () => {
  const side = privateSide();
  const repo = newRepo();
  git(repo, 'checkout', '-q', '-b', 'topic');
  commit(repo, { 'oops.md': NEEDLE }, `a leak, message: ${NEEDLE.split('\n')[0]}`);
  commit(repo, { 'oops.md': null }, 'removed');
  git(repo, 'checkout', '-q', 'main');
  const r = await captured(['--task', '04', '--task-repo', repo], seamsOf(side, newRepo()));
  assert.equal(r.exit, 5, r.stdout);
  assert.match(r.stdout, /task repository/);
  assertNoText(r, 'history');
});

test('The_working_tree_scan_finds_the_needle_and_neither_stdout_nor_stderr_holds_it', async () => {
  const side = privateSide();
  const repo = newRepo();
  writeFileSync(path.join(repo, 'draft.md'), NEEDLE);
  const r = await captured(['--task', '04', '--task-repo', repo], seamsOf(side, newRepo()));
  assert.equal(r.exit, 5, r.stdout);
  assertNoText(r, 'working tree');
});

test('The_candidate_scan_finds_the_needle_and_neither_stdout_nor_stderr_holds_it', async () => {
  const side = privateSide();
  const repo = newRepo();
  const sha = commit(repo, { 'tasks/04/spec.md': NEEDLE }, `publish, message: ${NEEDLE.split('\n')[1]}`);
  const r = await captured(['--task', '04', '--candidate-repo', repo, '--candidate', sha], seamsOf(side, newRepo()));
  assert.equal(r.exit, 5, r.stdout);
  assert.match(r.stdout, /candidate/);
  assertNoText(r, 'candidate');
});

test('The_command_line_takes_the_four_options_and_a_clean_run_prints_counts_and_names_only', async () => {
  const side = privateSide();
  const repo = newRepo();
  const sha = git(repo, 'rev-parse', 'HEAD').trim();
  const r = await captured(['--task', '04', '--task-repo', repo, '--task-base', 'origin/main', '--candidate-repo', repo, '--candidate', sha], seamsOf(side, newRepo()));
  assert.equal(r.exit, 0, r.stdout + r.stderr);
  assert.match(r.stdout, /\[task repository [^\]]*\] clean/);
  assert.match(r.stdout, /candidate/);
  assertNoText(r, 'a clean run');
});
