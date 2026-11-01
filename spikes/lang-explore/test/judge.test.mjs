// The judge (plan section 4, T-X5, T-X12, T-X13): `runner/run_samples.py` in both modes, driven from node:test over stub submissions (a `run.sh` of a few shell lines) in the real scoring box with a
// real systemd scope per run. The judge makes no temp path of its own: every directory it writes is under the one these tests give it.
import '../../minigit-bench/test/fake-home.mjs';
import assert from 'node:assert/strict';
import { chmodSync, cpSync, mkdirSync, readFileSync, statSync, writeFileSync } from 'node:fs';
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
  const said = [];
  const source = path.join(base, 'source');
  mkdirSync(source, { recursive: true });
  writeFileSync(path.join(source, 'build.sh'), `#!/usr/bin/env bash\n${build}\n`);
  writeFileSync(path.join(source, 'run.sh'), `#!/usr/bin/env bash\ncd "$(dirname "$0")"\n${run}\n`);
  for (const [name, text] of Object.entries(files)) writeFileSync(path.join(source, name), text);
  const dir = path.join(base, 'cases');
  mkdirSync(dir);
  for (const [id, [stdin, expected]] of Object.entries(cases)) { writeFileSync(path.join(dir, `${id}.in`), stdin); writeFileSync(path.join(dir, `${id}.out`), expected); }
  const r = await scoreTree({ explore: EXPLORE, stack: 'node', source, files: {}, sets: [{ name: 'heldout', dir }], limits, toolchains: { dirs: [NODE_DIR], tsDir: path.join(base, 'no-ts') }, caches: {}, work: path.join(base, 'work'), say: l => said.push(l) });
  assert.equal(r.ok, true, r.reason);
  return { ...r.value, outputsDir: path.join(base, 'work', 'cases'), progress: said };
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
  const rss = x => x.explore.cases.find(c => c.set === 'heldout').maxRssKb;
  assert.ok(rss(heavy) > 0 && rss(idle) > 0, `GNU time's ru_maxrss is kept beside the peak, as description (it is bwrap's own wait4 figure and under-reports the program: the cgroup peak certifies): ${rss(idle)} / ${rss(heavy)} KiB`);
});

test('A_failed_build_fails_every_case_and_no_case_runs', async t => {
  if (!await scopeGate(t)) return;
  const s = await judge({ build: 'echo broken >&2\nexit 1', run: 'exec cat', cases: { a: ['1\n', '1\n'], b: ['2\n', '2\n'] } });
  assert.deepEqual(verdicts(s), { a: 'build failed', b: 'build failed' }, 'a failed build is labelled as such, not as a runtime error');
  assert.equal(s.build.ok, false);
  assert.match(s.explore.build.outputTail, /broken/);
  assert.equal(s.explore.cases.every(c => c.exit === null), true);
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
  const prog = `import importlib.util as u, sys\nsys.dont_write_bytecode = True\nspec = u.spec_from_file_location('judge', ${JSON.stringify(JUDGE)})\nj = u.module_from_spec(spec); spec.loader.exec_module(j)\n${code}`;
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
    '    print("harness failure:", str(e).split(":")[0])',
  ].join('\n'));
  assert.equal(out, 'harness failure: memory-not-certifiable');
});

// ---- F5, F6 and the minors ---------------------------------------------------------------------------------------------------------

test('F5_each_cases_stdout_is_kept_capped_at_64_KiB_and_private_so_a_failing_output_can_be_read', async t => {
  if (!await scopeGate(t)) return;
  const s = await judge({ run: 'printf "not the answer\n"', cases: ONE });
  const file = path.join(s.outputsDir, 'heldout-c1.out');
  assert.equal(readFileSync(file, 'utf8'), 'not the answer\n');
  assert.equal(statSync(file).mode & 0o777, 0o600);
  assert.equal(statSync(s.outputsDir).mode & 0o777, 0o700);
  const flood = await judge({ run: 'exec yes 1234567890', cases: ONE, limits: { ...LIMITS, timeMs: 8000 } });
  assert.equal(statSync(path.join(flood.outputsDir, 'heldout-c1.out')).size, 65536, 'the flood is cut at 64 KiB');
});

test('A_failed_build_is_a_build_failed_verdict_and_the_cases_keep_no_output_file', async t => {
  if (!await scopeGate(t)) return;
  const s = await judge({ build: 'exit 1', run: 'exec cat', cases: ONE });
  assert.deepEqual(verdicts(s), { c1: 'build failed' });
  assert.equal(s.heldout.reason, 'the build failed: no case ran');
});

test('The_per_case_home_reaches_NUGET_PACKAGES_and_npm_config_cache_and_no_placeholder_is_left_in_the_environment', async t => {
  if (!await scopeGate(t)) return;
  const s = await judge({ run: 'printenv NUGET_PACKAGES npm_config_cache HOME TMPDIR', cases: ONE });
  const lines = readFileSync(path.join(s.outputsDir, 'heldout-c1.out'), 'utf8').trim().split('\n');
  assert.equal(lines.length, 4);
  assert.ok(lines.every(l => !l.includes('@@')), lines.join(' | '));
  const home = lines[2];
  assert.equal(lines[0], `${home}/.nuget/packages`, 'NUGET_PACKAGES is inside the per-case home');
  assert.equal(lines[1], `${home}/.npm`);
});

test('Scoring_reports_each_case_through_the_say_callback_as_it_ends', async t => {
  if (!await scopeGate(t)) return;
  const s = await judge({ run: 'exec cat', cases: { c1: ['a\n', 'a\n'], c2: ['b\n', 'b\n'] } });
  assert.ok(s.progress.some(l => /heldout\/c1 pass/.test(l)) && s.progress.some(l => /heldout\/c2 pass/.test(l)), s.progress.join(' | '));
  assert.ok(s.progress.some(l => /build ok/.test(l)));
});

test('F6_a_kill_that_raises_falls_back_to_the_process_group_the_wait_is_bounded_and_the_run_is_a_harness_failure', async t => {
  if (!await scopeGate(t)) return;
  const caseDir = JSON.stringify(tmpDir('f6'));
  const prog = [
    'import time',
    'j.kill_scope = lambda unit: (_ for _ in ()).throw(RuntimeError("systemctl is not answering"))',
    'started = time.monotonic()',
    'try:',
    `    j.run_in_scope(["sleep", "30"], ${caseDir}, b"", {"memoryBytes": 1 << 28, "timeMs": 700})`,
    '    print("no failure")',
    'except j.HarnessFailure as e:',
    '    print("harness failure after", int(time.monotonic() - started) < 15)',
  ].join('\n');
  const out = await inJudge(prog);
  assert.equal(out, 'harness failure after True');
  const flood = [
    'import time',
    'calls = []',
    'def failing(unit): calls.append(unit); raise RuntimeError("boom")',
    'j.kill_scope = failing',
    'started = time.monotonic()',
    'try:',
    `    j.run_in_scope(["yes", "x"], ${caseDir}, b"", {"memoryBytes": 1 << 28, "timeMs": 20000})`,
    '    print("no failure")',
    'except j.HarnessFailure as e:',
    '    print("harness failure", int(time.monotonic() - started) < 15, len(calls) >= 1)',
  ].join('\n');
  assert.equal(await inJudge(flood), 'harness failure True True', 'the output-cap path: the failed kill is retried by the fallback instead of waiting for the time limit');
});

test('save_output_closes_the_descriptor_when_fdopen_fails_and_reports_a_write_error_as_a_harness_failure_not_a_traceback', async () => {
  const dir = tmpDir('save-output');
  const out = await inJudge([
    'import os',
    `base = ${JSON.stringify(dir)}`,
    'seen = []',
    'real_open, real_fdopen = os.open, os.fdopen',
    'def spy_open(*a, **k):',
    '    fd = real_open(*a, **k); seen.append(fd); return fd',
    'def bad_fdopen(*a, **k):',
    '    raise OSError("fdopen refused")',
    'os.open, os.fdopen = spy_open, bad_fdopen',
    'try:',
    '    j.save_output({"outputsDir": os.path.join(base, "a")}, "heldout", "c1", b"x")',
    '    print("no error")',
    'except j.HarnessFailure as e:',
    '    print("harness failure:", "fdopen refused" in str(e))',
    'try:',
    '    os.fstat(seen[0]); print("fd leaked")',
    'except OSError:',
    '    print("fd closed")',
    'os.open, os.fdopen = real_open, real_fdopen',
    'open(os.path.join(base, "file"), "w").close()',
    'try:',
    '    j.save_output({"outputsDir": os.path.join(base, "file", "sub")}, "heldout", "c1", b"x")',
    '    print("no error")',
    'except j.HarnessFailure as e:',
    '    print("harness failure:", "heldout-c1" in str(e))',
  ].join('\n'));
  assert.equal(out, 'harness failure: True\nfd closed\nharness failure: True');
});
