// The judge (plan section 4, T-X5, T-X12, T-X13): `runner/run_samples.py` in both modes, driven from node:test over stub submissions (a `run.sh` of a few shell lines) in the real scoring box with a
// real systemd scope per run. The judge makes no temp path of its own: every directory it writes is under the one these tests give it.
import '../../minigit-bench/test/fake-home.mjs';
import assert from 'node:assert/strict';
import { chmodSync, cpSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { runBounded } from '../../minigit-bench/lib/run.mjs';
import { hostLacks, sandboxGate, tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { loadExplore } from '../lib/inputs.mjs';
import { JUDGE, scoreTree } from '../lib/score.mjs';

const NODE_DIR = path.dirname(path.dirname(process.execPath));
const EXPLORE = loadExplore().value;
const LIMITS = { timeMs: 2000, memoryBytes: 256 * 1024 * 1024, buildTimeoutMs: 30_000 };

/** A scope-capable host: the judge needs `systemd-run --user --scope` and the cgroup files. */
async function scopeGate(t) {
  if (!sandboxGate(t)) return false;
  const r = await runBounded('systemd-run', ['--user', '--scope', '--quiet', 'true'], { timeoutMs: 20_000 });
  return (r.ok && r.value.code === 0) || hostLacks(t, `systemd-run --user --scope does not work here: ${r.ok ? r.value.stderr : r.reason}`);
}

/** Write a stub submission (build.sh, run.sh) and a case directory; score it. `cases` is `{ id: [stdin, expected] }`. -> the score. */
async function judge({ run, build = 'exit 0', cases, limits = LIMITS, files = {} }) {
  const base = tmpDir('judge');
  const source = path.join(base, 'source');
  mkdirSync(source, { recursive: true });
  writeFileSync(path.join(source, 'build.sh'), `#!/usr/bin/env bash\n${build}\n`);
  writeFileSync(path.join(source, 'run.sh'), `#!/usr/bin/env bash\ncd "$(dirname "$0")"\n${run}\n`);
  for (const [name, text] of Object.entries(files)) writeFileSync(path.join(source, name), text);
  const dir = path.join(base, 'cases');
  mkdirSync(dir);
  for (const [id, [stdin, expected]] of Object.entries(cases)) { writeFileSync(path.join(dir, `${id}.in`), stdin); writeFileSync(path.join(dir, `${id}.out`), expected); }
  const r = await scoreTree({ explore: EXPLORE, stack: 'node', source, files: {}, sets: [{ name: 'heldout', dir }], limits, toolchains: { dirs: [NODE_DIR], tsDir: path.join(base, 'no-ts') }, caches: {}, work: path.join(base, 'work') });
  assert.equal(r.ok, true, r.reason);
  return r.value;
}

const verdicts = score => Object.fromEntries(score.explore.cases.filter(c => c.set === 'heldout').map(c => [c.id, c.verdict]));
const ONE = { c1: ['16\n', '16 8 4 2 1\n'] };
/** A case whose expected output is its input, for the stubs that are `cat`. */
const ECHO = { c1: ['16 8 4 2 1\n', '16 8 4 2 1\n'] };

test('A_program_that_prints_the_expected_bytes_passes_and_its_stderr_is_recorded_not_judged', async t => {
  if (!await scopeGate(t)) return;
  const s = await judge({ run: 'echo "some warning" >&2\nexec cat', cases: ECHO });
  assert.deepEqual(verdicts(s), { c1: 'pass' });
  const c = s.explore.cases.find(x => x.set === 'heldout');
  assert.match(c.stderrTail, /some warning/);
  assert.equal(typeof c.peakBytes, 'number');
  assert.ok(c.peakBytes > 0 && c.wallMs > 0 && c.exit === 0);
  assert.equal(s.heldout.primary100, true);
});

test('A_trailing_space_a_missing_line_feed_and_a_carriage_return_each_fail_as_a_wrong_answer', async t => {
  if (!await scopeGate(t)) return;
  const shapes = { space: 'printf "16 8 4 2 1 \\n"', noLf: 'printf "16 8 4 2 1"', crlf: 'printf "16 8 4 2 1\\r\\n"', extraLf: 'printf "16 8 4 2 1\\n\\n"' };
  for (const [name, cmd] of Object.entries(shapes)) {
    const s = await judge({ run: `${cmd}\nexit 0`, cases: ONE });
    assert.deepEqual(verdicts(s), { c1: 'wrong answer' }, name);
    assert.match(s.explore.cases.find(x => x.set === 'heldout').detail, /first difference at byte/);
  }
});

test('A_non_zero_exit_fails_whatever_was_written', async t => {
  if (!await scopeGate(t)) return;
  const s = await judge({ run: 'printf "16 8 4 2 1\\n"\nexit 3', cases: ONE });
  assert.deepEqual(verdicts(s), { c1: 'runtime error' });
  assert.equal(s.explore.cases.find(x => x.set === 'heldout').exit, 3);
});

/** The names of the scopes the judge started that are still alive. */
async function liveScopes() {
  const r = await runBounded('systemctl', ['--user', 'list-units', '--type=scope', '--all', '--no-legend', 'lang-explore-*'], { timeoutMs: 20_000 });
  return r.value.stdout.split('\n').filter(l => /lang-explore-/.test(l) && !/inactive|dead/.test(l));
}

test('A_timeout_kills_the_whole_scope_and_proves_it_is_gone', async t => {
  if (!await scopeGate(t)) return;
  const s = await judge({ run: '(sleep 60 &)\nexec sleep 60', cases: ONE, limits: { ...LIMITS, timeMs: 1200 } });
  assert.deepEqual(verdicts(s), { c1: 'time limit' });
  const c = s.explore.cases.find(x => x.set === 'heldout');
  assert.ok(c.wallMs >= 1200 && c.wallMs < 8000, `the wall is about the limit, not the sleep: ${c.wallMs}`);
  assert.deepEqual(await liveScopes(), [], 'no scope of the judge is left, and the background sleep died with it');
});

test('An_allocation_over_the_memory_limit_is_a_measured_memory_limit_with_an_oom_kill', async t => {
  if (!await scopeGate(t)) return;
  const s = await judge({ run: 'exec /usr/bin/python3 -I -c "import time; b = bytearray(700 * 1024 * 1024); time.sleep(5)"', cases: ONE, limits: { ...LIMITS, memoryBytes: 128 * 1024 * 1024 } });
  assert.deepEqual(verdicts(s), { c1: 'memory limit' });
  const c = s.explore.cases.find(x => x.set === 'heldout');
  assert.ok(c.events.oom_kill >= 1, JSON.stringify(c.events));
  assert.ok(c.peakBytes <= 128 * 1024 * 1024 + 4096, `the peak is bounded by the scope's limit: ${c.peakBytes}`);
});

test('The_certified_peak_of_a_run_that_allocates_is_larger_than_that_of_one_that_does_not', async t => {
  if (!await scopeGate(t)) return;
  const idle = await judge({ run: 'exec cat', cases: ECHO });
  const heavy = await judge({ run: 'exec /usr/bin/python3 -I -c "import sys; b = bytearray(60 * 1024 * 1024); sys.stdout.write(sys.stdin.read())"', cases: ECHO });
  assert.deepEqual([verdicts(idle), verdicts(heavy)], [{ c1: 'pass' }, { c1: 'pass' }]);
  const peak = s => s.explore.cases.find(x => x.set === 'heldout').peakBytes;
  assert.ok(peak(heavy) - peak(idle) > 50 * 1024 * 1024, `a 60 MiB allocation shows in memory.peak: ${peak(idle)} -> ${peak(heavy)}`);
});

test('A_failed_build_fails_every_case_and_no_case_runs', async t => {
  if (!await scopeGate(t)) return;
  const s = await judge({ build: 'echo broken >&2\nexit 1', run: 'exec cat', cases: { a: ['1\n', '1\n'], b: ['2\n', '2\n'] } });
  assert.deepEqual(verdicts(s), { a: 'runtime error', b: 'runtime error' });
  assert.equal(s.build.ok, false);
  assert.match(s.explore.build.outputTail, /broken/);
  assert.equal(s.explore.cases.every(c => c.detail === 'build failed' && c.exit === null), true);
  assert.equal(s.heldout.primary100, false);
});

test('Output_past_the_cap_is_cut_and_fails_and_the_run_does_not_run_on', async t => {
  if (!await scopeGate(t)) return;
  const s = await judge({ run: 'exec yes 1234567890', cases: ONE, limits: { ...LIMITS, timeMs: 8000 } });
  assert.deepEqual(verdicts(s), { c1: 'wrong answer' });
  const c = s.explore.cases.find(x => x.set === 'heldout');
  assert.match(c.detail, /output over the cap of 1048576 bytes/);
  assert.ok(c.wallMs < 6000, `the scope was killed at the cap, not at the time limit: ${c.wallMs} ms`);
  assert.deepEqual(await liveScopes(), []);
});

test('Each_case_runs_in_a_fresh_copy_of_the_built_tree_so_a_marker_a_run_leaves_never_reaches_the_next_case', async t => {
  if (!await scopeGate(t)) return;
  const run = 'if [ -e marker ]; then printf "second\\n"; else printf "first\\n"; fi\ntouch marker\nexit 0';
  const cases = { c1: ['x\n', 'first\n'], c2: ['x\n', 'first\n'], c3: ['x\n', 'first\n'] };
  const s = await judge({ run, cases });
  assert.deepEqual(verdicts(s), { c1: 'pass', c2: 'pass', c3: 'pass' }, 'case 2 and 3 give the verdict they give when run alone');
  const alone = await judge({ run, cases: { c2: cases.c2 } });
  assert.deepEqual(verdicts(alone), { c2: 'pass' });
});

test('The_visible_mode_prints_one_PASS_or_FAIL_line_per_case_and_judges_time_against_the_one_second_floor', async () => {
  const ws = tmpDir('visible');
  mkdirSync(path.join(ws, 'samples'));
  writeFileSync(path.join(ws, 'STATEMENT.md'), '# Task 01 — x\n\n| | |\n|---|---|\n| Kind | a program that reads standard input once and writes standard output once |\n');
  writeFileSync(path.join(ws, 'build.sh'), 'exit 0\n');
  writeFileSync(path.join(ws, 'run.sh'), 'read n\ncase "$n" in 1) echo 1;; 2) echo wrong;; 3) sleep 3; echo 3;; esac\n');
  for (const [id, i, o] of [['s01', '1\n', '1\n'], ['s02', '2\n', '2\n'], ['s03', '3\n', '3\n']]) { writeFileSync(path.join(ws, 'samples', `${id}.in`), i); writeFileSync(path.join(ws, 'samples', `${id}.out`), o); }
  const r = await runBounded('python3', ['-I', JUDGE, 'samples/'], { cwd: ws, timeoutMs: 60_000 });
  assert.equal(r.ok, true, r.reason);
  const lines = r.value.stdout.trim().split('\n');
  assert.equal(lines[0], 'PASS s01');
  assert.match(lines[1], /^FAIL s02 — output differs, first difference at byte 0$/);
  assert.match(lines[2], /^FAIL s03 — time limit: more than 1000 ms$/);
  assert.equal(r.value.code, 1);
  const noKind = tmpDir('visible-bad');
  writeFileSync(path.join(noKind, 'STATEMENT.md'), '# Task 01\n');
  mkdirSync(path.join(noKind, 'samples'));
  const bad = await runBounded('python3', ['-I', JUDGE, 'samples/'], { cwd: noKind, timeoutMs: 30_000 });
  assert.match(bad.value.stdout, /^FAIL — the Kind row of STATEMENT.md is not an olympiad kind/);
});

// ---- the kill path (T-X13) --------------------------------------------------------------------------------------------------------

/** Run a snippet with the judge loaded as a module (the file stays self-contained: it is read by path, `-I` adds nothing to the import path). */
async function inJudge(code) {
  const prog = `import importlib.util as u, sys\nspec = u.spec_from_file_location('judge', ${JSON.stringify(JUDGE)})\nj = u.module_from_spec(spec); spec.loader.exec_module(j)\n${code}`;
  const r = await runBounded('python3', ['-I', '-c', prog], { timeoutMs: 60_000 });
  assert.equal(r.ok, true, r.reason);
  assert.equal(r.value.code, 0, r.value.stderr);
  return r.value.stdout.trim();
}

test('Killing_a_unit_that_never_started_is_no_harness_failure_and_the_residual_check_passes', async t => {
  if (!await scopeGate(t)) return;
  const out = await inJudge('print(j.kill_scope("lang-explore-never-started"), j.scope_is_gone("lang-explore-never-started", 3))');
  assert.equal(out, 'None True');
});

test('A_kill_answered_not_loaded_or_not_found_is_fine_and_any_other_failure_is_reported', async () => {
  const out = await inJudge([
    'import subprocess',
    'def fake(code, text): return lambda *a: subprocess.CompletedProcess(a, code, "", text)',
    'for code, text in [(5, "Unit x.scope not loaded."), (1, "Unit x.scope not found."), (1, "Failed to kill unit: Access denied"), (4, "boom")]:',
    '    j.systemctl = fake(code, text)',
    '    print(code, j.kill_scope("x") is None)',
  ].join('\n'));
  assert.equal(out, '5 True\n1 True\n1 False\n4 False');
});

test('A_unit_with_a_process_left_is_a_failure_and_a_killed_one_is_gone', async t => {
  if (!await scopeGate(t)) return;
  const out = await inJudge([
    'import subprocess, time',
    'unit = "lang-explore-leftover-probe"',
    'p = subprocess.Popen(["systemd-run", "--user", "--scope", "--quiet", "--unit=" + unit, "sleep", "30"])',
    'time.sleep(1.5)',
    'print("alive:", j.scope_is_gone(unit, 1))',
    'print("kill:", j.kill_scope(unit))',
    'print("gone:", j.scope_is_gone(unit, 8))',
    'p.wait()',
  ].join('\n'));
  assert.equal(out, 'alive: False\nkill: None\ngone: True');
});

test('A_peak_that_cannot_be_read_is_a_harness_failure_so_memory_is_never_judged_descriptively', async () => {
  const out = await inJudge([
    'r = {"killedFor": None, "events": {}, "peakBytes": None, "stdoutOverflow": False, "code": 0, "stdout": b"x"}',
    'try:',
    '    j.classify(r, b"x", {"timeMs": 1000, "memoryBytes": 1 << 28})',
    '    print("no error")',
    'except j.HarnessFailure as e:',
    '    print("harness failure")',
  ].join('\n'));
  assert.equal(out, 'harness failure');
});
