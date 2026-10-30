import assert from 'node:assert/strict';
import { chmodSync, cpSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { runBounded } from '../lib/run.mjs';
import { installShim, parseVisibleOutput, shimIntact } from '../lib/visible.mjs';
import { inputs, stubTrees, tmpDir } from './helpers.mjs';

/** A stub tree copied to a temp dir with `script` (a file of the fetched inputs) dropped in as test-vN.sh. */
async function treeWithScript(stub, rel, as) {
  const { root } = await inputs();
  const dir = tmpDir('vis');
  cpSync(path.join(stubTrees, stub), dir, { recursive: true });
  cpSync(path.join(root, rel), path.join(dir, as));
  return dir;
}

async function runScript(dir, script) {
  const r = await runBounded('bash', [script], { cwd: dir, timeoutMs: 60_000 });
  assert.equal(r.ok, true, r.reason);
  return r.value;
}

test('The_upstream_build_block_swallows_a_failing_build_and_that_is_the_point', async () => {
  const dir = await treeWithScript('failing-build', 'upstream/test-v1.sh', 'test-v1.sh');
  const r = await runScript(dir, 'test-v1.sh');
  assert.match(r.stdout, /^FAIL: /m);
  assert.doesNotMatch(r.stdout, /BUILD FAILED/);
});

test('A_failing_build_is_loud_in_the_harness_scripts', async () => {
  const dir = await treeWithScript('failing-build', 'test-v1.sh', 'test-v1.sh');
  const r = await runScript(dir, 'test-v1.sh');
  assert.match(r.stdout, /^BUILD FAILED$/m);
  assert.match(r.stdout, /^PASSED: 0$/m);
  assert.match(r.stdout, /^FAILED: 1$/m);
  assert.equal(r.code, 1);
  assert.match(r.stderr, /stub build failure: boom/);
});

test('The_upstream_set_e_reports_nothing_after_a_crash_and_that_is_the_point', async () => {
  const dir = await treeWithScript('crash-on-add', 'upstream/test-v1.sh', 'test-v1.sh');
  const r = await runScript(dir, 'test-v1.sh');
  assert.doesNotMatch(r.stdout, /^PASSED:/m);
});

test('A_crash_mid_script_still_counts_every_test', async () => {
  const dir = await treeWithScript('crash-on-add', 'test-v1.sh', 'test-v1.sh');
  const r = await runScript(dir, 'test-v1.sh');
  const parsed = parseVisibleOutput(r.stdout, 11);
  assert.equal(parsed.summaryCaptured, true);
  assert.ok(parsed.total >= 11, `total ${parsed.total}`);
  assert.ok(parsed.failed >= 1);
  assert.equal(parsed.passed + parsed.failed, parsed.total);
});

/** Test N's block (header to the next header) of a script, run alone against a stub whose commit records `parent`. */
async function runParentBlock(rel, testNo, { commitVar, parentHash, expectedHash }) {
  const { root } = await inputs();
  const lines = readFileSync(path.join(root, rel), 'utf8').split('\n');
  const from = lines.findIndex(l => l.startsWith(`# Test ${testNo}:`));
  const to = lines.findIndex(l => l.startsWith(`# Test ${testNo + 1}:`));
  assert.ok(from > 0 && to > from, `Test ${testNo} block found in ${rel}`);
  const dir = tmpDir('parent');
  mkdirSync(path.join(dir, 'repo'));
  writeFileSync(path.join(dir, 'minigit'), [
    '#!/bin/sh',
    'if [ "$1" = commit ]; then',
    '  mkdir -p .minigit/commits',
    '  echo cccc > .minigit/HEAD',
    `  printf 'parent: ${parentHash}\\n' > .minigit/commits/cccc`,
    'fi',
    'exit 0', ''].join('\n'));
  chmodSync(path.join(dir, 'minigit'), 0o755);
  const prelude = `pass() { echo "PASS: $1"; }\nfail() { echo "FAIL: $1"; }\n${commitVar}=${expectedHash}\n`;
  const script = `${prelude}${lines.slice(from, to).join('\n')}\n`;
  const r = await runBounded('bash', ['-c', script], { cwd: path.join(dir, 'repo'), timeoutMs: 20_000 });
  assert.equal(r.ok, true, r.reason);
  return { stdout: r.value.stdout };
}

const A = 'a'.repeat(40);
const B = 'b'.repeat(40);

const parentScript = 'test-v2.sh';

/** The message upstream's Test N passes with, read from the fetched upstream script at run time (the text lives only in the fork). */
async function upstreamMessage(testNo) {
  const { root } = await inputs();
  const lines = readFileSync(path.join(root, 'upstream/test-v2.sh'), 'utf8').split('\n');
  const from = lines.findIndex(l => l.startsWith(`# Test ${testNo}:`));
  const to = lines.findIndex(l => l.startsWith(`# Test ${testNo + 1}:`));
  const found = /pass "([^"]+)"/.exec(lines.slice(from, to).join('\n'));
  assert.ok(found, `Test ${testNo} of upstream has a pass message`);
  return found[1];
}
const escapeRe = text => text.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
const CASES = [[20, 'COMMIT1'], [24, 'COMMIT2']];

// The defect, kept reproducible: upstream's Test 20 and Test 24 pass a commit whose parent is wrong. (This is the RED the fixed
// script's test was first watched failing against.)
test('The_unedited_upstream_test_v2_passes_a_commit_whose_parent_is_wrong_and_that_is_the_point', async () => {
  for (const [no, commitVar] of CASES) {
    const r = await runParentBlock('upstream/test-v2.sh', no, { commitVar, parentHash: B, expectedHash: A });
    assert.match(r.stdout, new RegExp(`^PASS: ${escapeRe(await upstreamMessage(no))}$`, 'm'));
  }
});

test('The_fixed_test_v2_fails_a_commit_whose_parent_is_wrong', async () => {
  for (const [no, commitVar] of CASES) {
    const message = await upstreamMessage(no);
    const r = await runParentBlock(parentScript, no, { commitVar, parentHash: B, expectedHash: A });
    assert.match(r.stdout, new RegExp(`^FAIL: ${escapeRe(message)} \\(parent is not ${A}\\)$`, 'm'), `Test ${no} printed: ${r.stdout}`);
    assert.doesNotMatch(r.stdout, /^PASS:/m);
  }
});

test('The_fixed_test_v2_still_passes_a_commit_whose_parent_is_right', async () => {
  for (const [no, commitVar] of CASES) {
    const r = await runParentBlock(parentScript, no, { commitVar, parentHash: A, expectedHash: A });
    assert.match(r.stdout, new RegExp(`^PASS: ${escapeRe(await upstreamMessage(no))}$`, 'm'));
  }
});

test('A_missing_summary_scores_as_all_failed_never_zero_of_zero', () => {
  const r = parseVisibleOutput('FAIL: something\nno summary here\n', 11);
  assert.equal(r.summaryCaptured, false);
  assert.match(r.reason, /no PASSED\/FAILED\/TOTAL summary/);
  assert.equal(r.passed, 0);
  assert.equal(r.failed, 11);
  assert.equal(r.total, 11);
});

test('A_captured_summary_is_read_from_its_last_block_and_must_agree_with_the_expected_total', () => {
  const block = (p, f) => `PASSED: ${p}\nFAILED: ${f}\nTOTAL:  ${p + f}\n`;
  const good = parseVisibleOutput(`${block(11, 0)}noise from a binary\nFAIL: x\n${block(9, 2)}ALL TESTS PASSED\n`, 11);
  assert.deepEqual([good.summaryCaptured, good.passed, good.failed, good.total, good.failLines], [true, 9, 2, 11, ['FAIL: x']]);
  const wrongTotal = parseVisibleOutput(block(3, 0), 11);
  assert.equal(wrongTotal.summaryCaptured, false);
  assert.match(wrongTotal.reason, /does not add up|expected 11/);
  assert.equal(good.reason, undefined, 'a captured summary carries no reason');
  assert.equal(wrongTotal.failed, 11);
});

test('A_build_failure_line_scores_every_test_failed', () => {
  const r = parseVisibleOutput('BUILD FAILED\nPASSED: 0\nFAILED: 1\n', 30);
  assert.equal(r.buildFailed, true);
  assert.equal(r.summaryCaptured, false);
  assert.equal(r.reason, 'build failed');
  assert.deepEqual([r.passed, r.failed, r.total], [0, 30, 30]);
});

test('A_shim_keeps_the_real_binary_aside_and_reports_when_it_was_replaced', () => {
  const dir = tmpDir('shim');
  cpSync(path.join(stubTrees, 'silent'), dir, { recursive: true });
  const shim = installShim(dir, 10);
  assert.equal(shim.ok, true);
  assert.equal(shimIntact(dir, shim.value.sha256), true);
  assert.match(readFileSync(path.join(dir, 'minigit'), 'utf8'), /timeout -k 2 10 /);
  assert.equal(readFileSync(path.join(dir, '.minigit-scored-real'), 'utf8').includes('exit 0'), true);
  writeFileSync(path.join(dir, 'minigit'), '#!/bin/sh\nexit 0\n');
  assert.equal(shimIntact(dir, shim.value.sha256), false);
  assert.equal(installShim(tmpDir('empty'), 10).ok, false);
});
