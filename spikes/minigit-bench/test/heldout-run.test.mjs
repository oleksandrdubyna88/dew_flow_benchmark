import assert from 'node:assert/strict';
import { existsSync, mkdirSync, readFileSync, readdirSync, statSync, symlinkSync, utimesSync, writeFileSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { test } from 'node:test';
import { scoreBoxArgv } from '../lib/bwrap.mjs';
import { packDir } from '../lib/heldout-archive.mjs';
import { generateKey, readKey, seal } from '../lib/heldout-crypto.mjs';
import { assertWorkRoot, heldoutScore, makeHeldoutDep, runSuite, sweepLeftovers, withClearSuite } from '../lib/heldout-run.mjs';
import { homeDir, pythonGate, sandboxGate, tmpDir } from './helpers.mjs';
import { writeToySuite } from './toy-suite.mjs';

/** A toy suite on disk, sealed with a throwaway key. */
function sealedToy() {
  const root = tmpDir('toy-sealed');
  const src = path.join(root, 'src');
  writeToySuite(src);
  const keyFile = path.join(root, 'k', 'heldout.key');
  assert.equal(generateKey(keyFile).ok, true);
  const packed = packDir(src);
  const suiteEnc = path.join(root, 'suite.enc');
  writeFileSync(suiteEnc, seal(packed.value, readKey(keyFile).value));
  return { root, src, keyFile, suiteEnc };
}

function copyWith(binaryText) {
  const dir = tmpDir('copy');
  writeFileSync(path.join(dir, 'minigit'), binaryText, { mode: 0o755 });
  return { dir, binary: path.join(dir, 'minigit'), work: tmpDir('cases') };
}

const pass = (id, group = 'H1', extra = {}) => ({ id, group, scored: true, status: 'pass', spec: 'S', captured: [], ...extra });
const resultOf = (cases, extra = {}) => ({ format: 1, phase: 'v2', python: '3', cases, latency: { known: false, why: 'x' }, ...extra });

test('A_blocked_case_is_neither_passed_nor_a_second_failure_and_still_denies_acceptance', () => {
  const s = heldoutScore(resultOf([
    { ...pass('H1-01'), status: 'fail', reason: 'mismatch' },
    ...['H1-02', 'H1-03', 'H1-04'].map(id => ({ ...pass(id), status: 'blocked', blockedBy: 'H1-01' })),
    pass('H2-01', 'H2'),
  ]));
  assert.equal(s.known, true);
  assert.deepEqual(s.groups.H1, { total: 4, passed: 0, failed: 1, blocked: 3 });
  assert.deepEqual(s.groups.H2, { total: 1, passed: 1, failed: 0, blocked: 0 });
  assert.deepEqual(s.totals, { total: 5, passed: 1, failed: 1, blocked: 3 });
  assert.equal(s.primary100, false);
  assert.deepEqual(s.cases.filter(c => c.status === 'blocked').map(c => c.blockedBy), ['H1-01', 'H1-01', 'H1-01']);
});

test('Only_all_scored_cases_passing_is_primary_100_and_secondary_cases_stay_out_of_it', () => {
  const secondary = { ...pass('R-01', 'R'), scored: false, status: 'fail', reason: 'crash' };
  const s = heldoutScore(resultOf([pass('H1-01'), pass('H2-01', 'H2'), secondary]));
  assert.equal(s.primary100, true);
  assert.deepEqual(s.totals, { total: 2, passed: 2, failed: 0, blocked: 0 });
  assert.deepEqual(s.secondary.map(c => [c.id, c.status, c.reason]), [['R-01', 'fail', 'crash']]);
  assert.equal(s.cases.some(c => c.id === 'R-01'), false);
});

test('An_unreadable_or_empty_result_is_unknown_never_zero', () => {
  for (const bad of [null, undefined, 'x', {}, { format: 2, cases: [] }, resultOf([]), resultOf([{ ...pass('R-01', 'R'), scored: false }]), resultOf([{ id: 'H1-01' }])]) {
    const s = heldoutScore(bad);
    assert.equal(s.known, false, JSON.stringify(bad));
    assert.equal(typeof s.why, 'string');
    assert.equal('primary100' in s, false);
  }
});

test('The_work_root_must_be_under_the_home_and_never_under_the_temp_directory', () => {
  assert.equal(assertWorkRoot(path.join(os.tmpdir(), 'x')).ok, false);
  assert.match(assertWorkRoot(path.join(os.tmpdir(), 'x')).reason, /temp/i);
  assert.equal(assertWorkRoot('/var/lib/x').ok, false);
  assert.equal(assertWorkRoot('relative/path').ok, false);
  assert.equal(assertWorkRoot(os.homedir()).ok, false, 'the home itself is too wide');
  assert.equal(assertWorkRoot(path.join(os.homedir(), 'rsd', 'minigit')).ok, true);
});

test('A_link_from_the_home_to_the_temp_directory_does_not_pass_as_under_the_home', () => {
  const link = path.join(homeDir('linkroot'), 'to-tmp');
  symlinkSync(os.tmpdir(), link);
  const r = assertWorkRoot(path.join(link, 'sub'));
  assert.equal(r.ok, false);
  assert.match(r.reason, /temp/i);
});

test('The_clear_suite_is_decrypted_only_under_the_home_and_removed_after_scoring', async () => {
  const { suiteEnc, keyFile } = sealedToy();
  const refused = await withClearSuite({ suiteEnc, keyFile, workRoot: path.join(os.tmpdir(), 'mgb-clear') }, () => assert.fail('must not run'));
  assert.equal(refused.ok, false);
  assert.equal(existsSync(path.join(os.tmpdir(), 'mgb-clear')), false, 'nothing created under the temp directory');

  const workRoot = homeDir('clear');
  let seen;
  await assert.rejects(withClearSuite({ suiteEnc, keyFile, workRoot }, dir => {
    seen = { dir, mode: statSync(dir).mode & 0o777, runner: existsSync(path.join(dir, 'runner.py')) };
    throw new Error('the runner blew up');
  }), /the runner blew up/);
  assert.equal(seen.mode, 0o700);
  assert.equal(seen.runner, true);
  assert.ok(seen.dir.startsWith(path.join(workRoot, 'score-tmp') + path.sep));
  assert.equal(existsSync(seen.dir), false, 'removed even though the runner threw');
});

test('A_wrong_key_or_a_missing_suite_is_a_value_and_leaves_no_directory', async () => {
  const { suiteEnc, root } = sealedToy();
  const otherKey = path.join(root, 'other', 'heldout.key');
  generateKey(otherKey);
  const workRoot = homeDir('badkey');
  const wrong = await withClearSuite({ suiteEnc, keyFile: otherKey, workRoot }, () => assert.fail('must not run'));
  assert.deepEqual(wrong, { ok: false, reason: 'authentication failed' });
  const missing = await withClearSuite({ suiteEnc: path.join(root, 'none.enc'), keyFile: otherKey, workRoot }, () => assert.fail('must not run'));
  assert.equal(missing.ok, false);
  assert.match(missing.reason, /none\.enc/);
  assert.deepEqual(readdirSync(path.join(workRoot, 'score-tmp')), []);
});

test('Sweep_removes_leftover_clear_directories_older_than_the_limit_only', () => {
  const workRoot = homeDir('sweep');
  const base = path.join(workRoot, 'score-tmp');
  for (const name of ['old-1', 'young-2']) mkdirSync(path.join(base, name), { recursive: true, mode: 0o700 });
  writeFileSync(path.join(base, 'old-1', 'leftover'), 'x');
  const longAgo = new Date(Date.now() - 25 * 3600 * 1000);
  utimesSync(path.join(base, 'old-1'), longAgo, longAgo);
  const swept = sweepLeftovers({ workRoot, olderThanMs: 24 * 3600 * 1000, nowMs: Date.now() });
  assert.deepEqual(swept, { removed: ['old-1'], kept: ['young-2'], failed: [] });
  assert.deepEqual(readdirSync(base), ['young-2']);
  assert.deepEqual(sweepLeftovers({ workRoot: homeDir('nothing'), olderThanMs: 1, nowMs: Date.now() }), { removed: [], kept: [], failed: [] });
});

async function runToy(binary, work, { phase = 'v1', box = [], timeoutS = 5, src } = {}) {
  const suiteDir = src ?? (() => { const d = tmpDir('toy-clear'); writeToySuite(d); return d; })();
  return runSuite({ suiteDir, binary, phase, box, workDir: work, outPath: path.join(work, '..', `r-${phase}.json`), timeoutS });
}

test('A_toy_binary_is_scored_case_by_case_and_v2_adds_the_h2_cases', async t => {
  if (!pythonGate(t)) return;
  const { binary, work } = copyWith(await toyProgram());
  const v1 = await runToy(binary, work);
  assert.equal(v1.ok, true, v1.reason);
  assert.deepEqual(heldoutScore(v1.value).totals, { total: 4, passed: 4, failed: 0, blocked: 0 });
  const v2 = await runToy(binary, work, { phase: 'v2' });
  assert.deepEqual(heldoutScore(v2.value).totals, { total: 5, passed: 5, failed: 0, blocked: 0 });
});

test('A_missing_binary_fails_every_case_and_blocks_none', async t => {
  if (!pythonGate(t)) return;
  const work = tmpDir('cases');
  const r = await runToy(path.join(tmpDir('nowhere'), 'minigit'), work, { phase: 'v2' });
  assert.equal(r.ok, true, r.reason);
  const s = heldoutScore(r.value);
  assert.deepEqual(s.totals, { total: 5, passed: 0, failed: 5, blocked: 0 });
  assert.deepEqual([...new Set(r.value.cases.map(c => c.reason))], ['missing']);
  assert.equal(s.primary100, false);
});

test('A_hanging_binary_fails_its_case_on_timeout', async t => {
  if (!pythonGate(t) || !sandboxGate(t)) return;
  const { dir, binary, work } = copyWith('#!/bin/sh\nsleep 60\n');
  const box = [...scoreBoxArgv({ copyDir: dir, casesDir: work, home: tmpDir('home'), tmp: tmpDir('tmp') })];
  const started = Date.now();
  const r = await runToy(binary, work, { box, timeoutS: 1 });
  assert.equal(r.ok, true, r.reason);
  assert.ok(Date.now() - started < 20_000, 'the run ended; it did not wait the sleep out');
  const first = r.value.cases.find(c => c.id === 'H1-01');
  assert.deepEqual([first.status, first.reason], ['fail', 'timeout']);
  assert.deepEqual(r.value.cases.filter(c => c.status === 'blocked').map(c => c.blockedBy), ['H1-01', 'H1-01', 'H1-01']);
});

test('A_runner_that_exits_nonzero_or_writes_garbage_is_a_failure_value', async t => {
  if (!pythonGate(t)) return;
  const bad = tmpDir('bad-runner');
  writeFileSync(path.join(bad, 'runner.py'), 'import sys\nsys.exit(3)\n');
  const { binary, work } = copyWith('#!/bin/sh\n');
  const crashed = await runToy(binary, work, { src: bad });
  assert.equal(crashed.ok, false);
  assert.match(crashed.reason, /exited 3/);
  writeFileSync(path.join(bad, 'runner.py'), 'import argparse\nap = argparse.ArgumentParser()\n[ap.add_argument("--" + f) for f in ("phase","binary","work","box","timeout","out")]\no = ap.parse_args()\nopen(o.out, "w").write("not json")\n');
  const garbage = await runToy(binary, work, { src: bad });
  assert.equal(garbage.ok, false);
  assert.match(garbage.reason, /result/);
});

test('The_scoring_dependency_answers_unknown_when_the_key_is_missing_and_a_score_when_it_is_not', async t => {
  if (!pythonGate(t)) return;
  const { suiteEnc, keyFile, root } = sealedToy();
  const workRoot = homeDir('dep');
  const { binary, work } = copyWith(await toyProgram());
  const scoreDir = tmpDir('score');
  const dep = makeHeldoutDep({ suiteEnc, keyFile, workRoot });
  const scored = await dep({ binary, phase: 'v2', box: [], casesDir: work, scoreDir });
  assert.equal(scored.known, true, scored.why);
  assert.equal(scored.primary100, true);
  assert.equal(existsSync(scored.resultPath), true);
  assert.deepEqual(readdirSync(path.join(workRoot, 'score-tmp')), [], 'the clear suite is gone');
  const noKey = await makeHeldoutDep({ suiteEnc, keyFile: path.join(root, 'absent.key'), workRoot })({ binary, phase: 'v1', box: [], casesDir: work, scoreDir });
  assert.equal(noKey.known, false);
  assert.match(noKey.why, /absent\.key/);
});

async function toyProgram() {
  const root = tmpDir('toy-prog');
  writeToySuite(root);
  return readFileSync(path.join(root, 'refs', 'a', 'minigit'), 'utf8');
}
