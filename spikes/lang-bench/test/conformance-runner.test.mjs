// E1.S8: the checker and the runner. The pairs are parsed by the bench's own runner (python3 -I -B run_samples.py --parse-only), through runBounded with a ceiling; a runner that cannot be started,
// that hangs or that ends in any other way than 0 or 4 is a refusal to run (exit 4), never a pass. The seams (`python`, `runner`, `timeoutMs`) are the checker's own.
import assert from 'node:assert/strict';
import { readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { setTimeout as sleep } from 'node:timers/promises';
import { test } from 'node:test';
import { RUNNER } from '../lib/conformance-runner.mjs';
import { check, failsOf, fixtureRepo } from './support/conformance-fixtures.mjs';

/** A stand-in runner (a Python script) written next to the fixture: `body` is its Python source. */
function standIn(fixture, body) {
  const file = path.join(fixture.repo, 'fake_runner.py');
  writeFileSync(file, body);
  return file;
}

const alive = pid => {
  try {
    process.kill(pid, 0);
    return true;
  } catch {
    return false;
  }
};

test('The_runner_the_checker_uses_is_the_benchs_own_and_not_one_found_through_repo', () => {
  assert.match(RUNNER, /spikes\/lang-bench\/runner\/run_samples\.py$/);
});

test('A_pair_that_does_not_parse_is_quoted_with_the_runners_file_line_and_message', async t => {
  const fixture = fixtureRepo(t, 'daemon', { 's01.in': [['connect c1 tcp', 'frobnicate c1 tcp']] });
  const fails = failsOf(await check(fixture), 'samples').filter(line => line.includes('the runner refuses the pairs'));
  assert.equal(fails.length, 1, fails.join('\n'));
  assert.match(fails[0], /^FAIL samples — the runner refuses the pairs — s01\.in:4: /);
});

test('A_runner_that_exits_4_has_every_line_it_printed_quoted', async t => {
  const fixture = fixtureRepo(t, 'daemon');
  const fake = standIn(fixture, 'import sys\nprint("s01.in:7: first problem", file=sys.stderr)\nprint("s02.out:2: second problem", file=sys.stderr)\nsys.exit(4)\n');
  const result = await check(fixture, [], { runner: fake });
  assert.deepEqual(failsOf(result, 'samples'), ['FAIL samples — the runner refuses the pairs — s01.in:7: first problem', 'FAIL samples — the runner refuses the pairs — s02.out:2: second problem']);
  assert.equal(result.exit, 1);
});

test('Python_missing_is_exit_4_never_a_pass', async t => {
  const result = await check(fixtureRepo(t, 'daemon'), [], { python: '/nonexistent/python3' });
  assert.equal(result.exit, 4);
  assert.deepEqual(result.lines, []);
  assert.match(result.errors.join('\n'), /python3 could not be started/);
});

test('The_runner_missing_is_exit_4_never_a_pass', async t => {
  const result = await check(fixtureRepo(t, 'web'), [], { runner: '/nonexistent/run_samples.py' });
  assert.equal(result.exit, 4);
  assert.match(result.errors.join('\n'), /the runner is missing at \/nonexistent\/run_samples\.py/);
});

test('A_runner_that_ends_any_other_way_than_0_or_4_is_exit_4', async t => {
  const fixture = fixtureRepo(t, 'daemon');
  const result = await check(fixture, [], { runner: standIn(fixture, 'import sys\nprint("the runner itself failed", file=sys.stderr)\nsys.exit(5)\n') });
  assert.equal(result.exit, 4);
  assert.match(result.errors.join('\n'), /the runner ended with exit 5: the runner itself failed/);
});

test('A_runner_that_hangs_is_stopped_at_the_ceiling_with_its_whole_process_tree_and_the_exit_is_4', async t => {
  const fixture = fixtureRepo(t, 'daemon');
  const pidFile = path.join(fixture.repo, 'child.pid');
  const fake = standIn(fixture, `import subprocess, sys, time
child = subprocess.Popen(["sleep", "300"], start_new_session=True)
open(${JSON.stringify(pidFile)}, "w").write(str(child.pid))
time.sleep(300)
`);
  const started = Date.now();
  const result = await check(fixture, [], { runner: fake, timeoutMs: 1500 });
  assert.equal(result.exit, 4);
  assert.match(result.errors.join('\n'), /did not finish within 1\.5 s and was stopped/);
  assert.ok(Date.now() - started < 20_000, 'the bounded run returned');
  const pid = Number(readFileSync(pidFile, 'utf8'));
  for (let waited = 0; alive(pid) && waited < 2000; waited += 100) await sleep(100);
  assert.equal(alive(pid), false, `the runner's own child (pid ${pid}, a session of its own) is gone when the checker has returned`);
});

test('An_olympiad_spec_needs_no_runner_and_no_python', async t => {
  const result = await check(fixtureRepo(t, 'olympiad'), [], { python: '/nonexistent/python3', runner: '/nonexistent/run_samples.py' });
  assert.equal(result.exit, 0, result.errors.join('\n'));
});
