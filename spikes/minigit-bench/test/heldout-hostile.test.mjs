// The REAL runner (decrypted from the sealed suite) against binaries that make hostile state or output. The runner runs on the host and
// reads what the cell wrote: none of it may leak a host file, hang the run, exhaust memory or end the run with an uncaught exception.
// Every test needs the key (keyGate), python3 and bwrap; none of them contains a case, a reference line or a mutant.
import assert from 'node:assert/strict';
import { chmodSync, cpSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { scoreBoxArgv } from '../lib/bwrap.mjs';
import { heldoutScore, runSuite, withClearSuite } from '../lib/heldout-run.mjs';
import { homeDir, keyGate, pythonGate, sandboxGate, tmpDir } from './helpers.mjs';

const SECRET = 'HOST-SECRET-must-never-reach-a-result';

/** Writes `#!/bin/sh` + body as the cell's binary, runs the real runner on it in the real box. Resolves { run, text, elapsedMs }. */
async function runHostile(t, body, { phase = 'v1', timeoutS = 5, binaryOverride } = {}) {
  if (!keyGate(t) || !pythonGate(t) || !sandboxGate(t)) return null;
  const dir = tmpDir('hostile-copy');
  const binary = binaryOverride ?? path.join(dir, 'minigit');
  if (!binaryOverride) {
    writeFileSync(binary, `#!/bin/sh\n${body}\n`, { mode: 0o755 });
    chmodSync(binary, 0o755);
  }
  const work = tmpDir('hostile-cases');
  const box = [...scoreBoxArgv({ copyDir: dir, casesDir: work, home: tmpDir('hostile-home'), tmp: tmpDir('hostile-tmp'), roFiles: [] })];
  const outPath = path.join(tmpDir('hostile-out'), 'result.json');
  const started = Date.now();
  const ran = await withClearSuite({ workRoot: homeDir('hostile'), rid: 'hostile' }, suiteDir =>
    runSuite({ suiteDir, binary, phase, box, workDir: work, outPath, timeoutS, wallS: 300 }));
  assert.equal(ran.ok, true, ran.reason);
  const run = ran.value;
  assert.equal(run.ok, true, `the runner must finish with a result, got: ${run.reason}`);
  const text = JSON.stringify(run.value);
  assert.equal(text.includes(SECRET), false, 'a host file leaked into the result');
  assert.equal(text.includes('Traceback'), false);
  return { run: run.value, text, elapsedMs: Date.now() - started };
}

const INIT = 'mkdir -p .minigit/objects .minigit/commits; : > .minigit/index; : > .minigit/HEAD';
const first = (run, id) => run.cases.find(c => c.id === id);
const decoyFile = () => { const f = path.join(tmpDir('decoy'), 'decoy.txt'); writeFileSync(f, SECRET); return f; };
/** A sane `init`, and on `add` the hostile move (the state the runner reads next is H1-03's index). */
const onAdd = hostile => `case "$1" in init) ${INIT} ;; add) ${hostile} ;; esac; exit 0`;
const h103 = r => first(r.run, 'H1-03');

test('An_index_that_is_a_link_to_a_host_file_is_refused_and_never_read', async t => {
  const r = await runHostile(t, onAdd(`rm .minigit/index; ln -s ${decoyFile()} .minigit/index`));
  if (!r) return;
  assert.deepEqual([h103(r).status, h103(r).reason], ['fail', 'mismatch']);
});

test('A_whole_dot_minigit_that_is_a_link_is_refused', async t => {
  const decoyDir = tmpDir('decoy-dir');
  mkdirSync(path.join(decoyDir, 'objects')); mkdirSync(path.join(decoyDir, 'commits'));
  writeFileSync(path.join(decoyDir, 'HEAD'), SECRET); writeFileSync(path.join(decoyDir, 'index'), `${SECRET}\n`);
  const r = await runHostile(t, onAdd(`rm -rf .minigit; ln -s ${decoyDir} .minigit`));
  if (!r) return;
  assert.equal(h103(r).status, 'fail');
});

test('An_index_that_is_a_fifo_does_not_hang_the_runner', async t => {
  const r = await runHostile(t, onAdd('rm .minigit/index; mkfifo .minigit/index'));
  if (!r) return;
  assert.ok(r.elapsedMs < 120_000);
  assert.equal(h103(r).status, 'fail');
});

test('An_index_that_is_dev_zero_or_a_huge_sparse_file_is_refused_without_exhausting_memory', async t => {
  const zero = await runHostile(t, onAdd('rm .minigit/index; ln -s /dev/zero .minigit/index'));
  if (!zero) return;
  assert.equal(h103(zero).status, 'fail');
  const sparse = await runHostile(t, onAdd('rm .minigit/index; truncate -s 300M .minigit/index'));
  assert.equal(h103(sparse).status, 'fail');
});

test('A_working_file_that_is_a_link_cannot_make_the_runner_write_through_it', async t => {
  const victim = path.join(tmpDir('victim'), 'host-file.txt');
  writeFileSync(victim, 'host content');
  // a faithful-looking binary whose `add` then turns the added file into a link to a host file; H1-27 writes the same name again after adding it
  const body = `case "$1" in
  init) ${INIT} ;;
  add) echo "$2" >> .minigit/index; cp "$2" ".minigit/objects/o-$2"; rm -f "$2"; ln -s ${victim} "$2" ;;
  commit) h=0123456789abcdef; printf 'parent: NONE\\ntimestamp: 1\\nmessage: %s\\nfiles:\\n' "$3" > ".minigit/commits/$h"; echo $h > .minigit/HEAD; : > .minigit/index; echo "Committed $h" ;;
esac; exit 0`;
  const r = await runHostile(t, body);
  if (!r) return;
  assert.equal(readFileSync(victim, 'utf8'), 'host content', 'the host file was overwritten through a link');
  const rewrite = first(r.run, 'H1-27');
  assert.deepEqual([rewrite.status, rewrite.reason], ['fail', 'setup'], 'the rewrite was attempted and refused (the test reaches the write path)');
});

test('A_stdout_or_stderr_flood_is_cut_off_and_fails_the_case_as_a_crash', async t => {
  const out = await runHostile(t, 'head -c 60000000 /dev/zero | tr "\\0" x; exit 0');
  if (!out) return;
  assert.deepEqual([first(out.run, 'H1-01').status, first(out.run, 'H1-01').reason], ['fail', 'crash']);
  assert.ok(out.text.length < 2_000_000, `the result stays small (${out.text.length} bytes)`);
  const err = await runHostile(t, 'head -c 60000000 /dev/zero | tr "\\0" x >&2; exit 0');
  assert.deepEqual([first(err.run, 'H1-01').status, first(err.run, 'H1-01').reason], ['fail', 'crash']);
});

test('A_missing_binary_fails_every_scored_case_as_missing_and_blocks_none', async t => {
  const dir = tmpDir('nowhere');
  const r = await runHostile(t, '', { binaryOverride: path.join(dir, 'minigit'), phase: 'v2' });
  if (!r) return;
  const s = heldoutScore(r.run);
  assert.equal(s.totals.passed, 0);
  assert.equal(s.totals.blocked, 0);
  assert.equal(s.totals.failed, s.totals.total);
  assert.deepEqual([...new Set(r.run.cases.filter(c => c.scored).map(c => c.reason))], ['missing']);
});

test('A_hanging_binary_fails_its_case_on_timeout_and_the_run_ends', async t => {
  const r = await runHostile(t, 'sleep 60', { timeoutS: 1 });
  if (!r) return;
  assert.ok(r.elapsedMs < 60_000, 'the run did not wait the sleep out');
  assert.deepEqual([first(r.run, 'H1-01').status, first(r.run, 'H1-01').reason], ['fail', 'timeout']);
});

const FAKE_COMMIT = `
case "$1" in
  init) ${INIT}; : > .minigit/HEAD ;;
  add) echo "$2" >> .minigit/index; cp "$2" ".minigit/objects/blob-$2" ;;
  commit) h=0123456789abcdef; ts=$(head -c 5000 /dev/zero | tr '\\0' 7)
    printf 'parent: NONE\\ntimestamp: %s\\nmessage: %s\\nfiles:\\n' "$ts" "$3" > ".minigit/commits/$h"
    echo $h > .minigit/HEAD; : > .minigit/index; echo "Committed $h" ;;
esac
exit 0`;

test('An_unexpected_exception_in_one_case_fails_that_case_as_harness_error_and_the_run_goes_on', async t => {
  const r = await runHostile(t, FAKE_COMMIT);
  if (!r) return;
  const hit = r.run.cases.filter(c => c.reason === 'harness-error');
  assert.ok(hit.length >= 1, 'a 5000-digit timestamp reaches int() and must fail its case, not the run');
  assert.ok(r.run.cases.some(c => c.status === 'pass'), 'the other cases still ran');
  assert.ok(r.run.cases.every(c => !c.detail || !/Traceback|File "/.test(c.detail)));
});

/** The reference A of the decrypted suite behind a wrapper that breaks ONE command; the copy holds both (the box hides the home). */
async function runBrokenCommand(t, command) {
  if (!keyGate(t) || !pythonGate(t) || !sandboxGate(t)) return null;
  const dir = tmpDir('broken-copy');
  const work = tmpDir('broken-cases');
  const box = [...scoreBoxArgv({ copyDir: dir, casesDir: work, home: tmpDir('broken-home'), tmp: tmpDir('broken-tmp') })];
  const binary = path.join(dir, 'minigit');
  const ran = await withClearSuite({ workRoot: homeDir('broken'), rid: 'broken' }, async suiteDir => {
    cpSync(path.join(suiteDir, 'refs', 'a', 'minigit'), path.join(dir, 'real'));
    chmodSync(path.join(dir, 'real'), 0o755);
    writeFileSync(binary, `#!/bin/sh\n[ "$1" = ${command} ] && exit 1\nexec "$(dirname "$0")/real" "$@"\n`, { mode: 0o755 });
    return runSuite({ suiteDir, binary, phase: 'v2', box, workDir: work, outPath: path.join(tmpDir('broken-out'), 'r.json'), timeoutS: 10, wallS: 300 });
  });
  assert.equal(ran.ok && ran.value.ok, true, ran.reason ?? ran.value.reason);
  return heldoutScore(ran.value.value);
}

const statusOf = (score, id) => score.cases.find(c => c.id === id);

test('A_broken_checkout_reset_or_rm_fails_its_core_once_and_blocks_the_cases_that_use_it', async t => {
  // [core, the cases that use the command as setup, the command's own error-message case: a distinct SPEC sentence, so a distinct clause]
  const expected = { checkout: ['H2-11', ['H2-12', 'H2-13', 'H2-14', 'H2-15', 'H2-17'], 'H2-16'], reset: ['H2-18', ['H2-04', 'H2-19', 'H2-20', 'H2-21'], 'H2-22'], rm: ['H2-23', ['H2-25', 'H2-26'], 'H2-24'] };
  for (const [command, [core, dependents, errorCase]] of Object.entries(expected)) {
    const score = await runBrokenCommand(t, command);
    if (!score) return;
    assert.equal(statusOf(score, core).status, 'fail', `${command}: the core ${core}`);
    for (const id of dependents) assert.deepEqual([statusOf(score, id).status, statusOf(score, id).blockedBy], ['blocked', core], `${command}: ${id}`);
    assert.deepEqual(score.cases.filter(c => c.status === 'fail').map(c => c.id).sort(), [core, errorCase].sort(), `${command}: its core and its error-message case, nothing else`);
    assert.equal(score.totals.blocked, dependents.length);
    assert.equal(score.defects.count, 2, 'two SPEC clauses: the success path and the error message');
    assert.equal(score.primary100, false);
    assert.ok(score.totals.percent < 100);
  }
});
