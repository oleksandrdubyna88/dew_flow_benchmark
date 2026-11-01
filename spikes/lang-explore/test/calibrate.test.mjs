// Calibration (plan section 4, T-X14): the limits arithmetic, the stops (a reference case over the 30 s ceiling, a failing one, a broken build, an uncertified peak) and the mutants' exact sets, over a scripted
// scorer. The real references and mutants, through the real judge, are `heldout-real.test.mjs`.
import assert from 'node:assert/strict';
import { mkdirSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { tmpDir } from '../../minigit-bench/test/helpers.mjs';
import { runCalibration, limitsFrom, mutantResult, referenceProblems } from '../lib/calibrate.mjs';
import { INPUTS } from '../lib/paths.mjs';

const EXPLORE = JSON.parse(readFileSync(path.join(INPUTS, 'explore.json'), 'utf8'));
const MIB = 1024 * 1024;

test('The_effective_time_limit_is_the_larger_of_2_s_and_3_times_the_slower_references_slowest_case', () => {
  const refs = (a, b) => [{ stack: 'csharp', slowestWallMs: a, peakBytes: 10 * MIB }, { stack: 'node', slowestWallMs: b, peakBytes: 20 * MIB }];
  assert.equal(limitsFrom({ limits: EXPLORE.limits, refs: refs(300, 250) }).timeMs, 2000, 'the floor holds when 3 x the slowest is under it');
  assert.equal(limitsFrom({ limits: EXPLORE.limits, refs: refs(300, 900) }).timeMs, 2700, '3 x the slower reference, whichever stack it is');
  assert.equal(limitsFrom({ limits: EXPLORE.limits, refs: refs(1500, 200) }).timeMs, 4500);
});

test('The_memory_limit_is_the_larger_of_512_MB_and_twice_the_larger_reference_peak', () => {
  const refs = peak => [{ stack: 'csharp', slowestWallMs: 1, peakBytes: 40 * MIB }, { stack: 'node', slowestWallMs: 1, peakBytes: peak }];
  assert.equal(limitsFrom({ limits: EXPLORE.limits, refs: refs(60 * MIB) }).memoryBytes, 512 * MIB);
  assert.equal(limitsFrom({ limits: EXPLORE.limits, refs: refs(400 * MIB) }).memoryBytes, 800 * MIB);
});

const caseOf = (id, over = {}) => ({ set: 'heldout', id, verdict: 'pass', wallMs: 120, peakBytes: 50 * MIB, ...over });
const scoreOf = (cases, build = true) => ({ build: { ok: build }, explore: { build: { outputTail: 'compiler said no' }, cases } });

test('A_reference_case_over_the_ceiling_a_failing_one_a_broken_build_or_an_uncertified_peak_stops_the_pilot_with_a_sentence', () => {
  const ceilingMs = 30_000;
  assert.deepEqual(referenceProblems({ stack: 'node', score: scoreOf([caseOf('k001'), caseOf('k002')]), ceilingMs }), []);
  assert.match(referenceProblems({ stack: 'node', score: scoreOf([caseOf('k001', { wallMs: 30_000 })]), ceilingMs })[0], /at or over the calibration ceiling of 30000 ms/);
  assert.match(referenceProblems({ stack: 'csharp', score: scoreOf([caseOf('k001', { verdict: 'time limit', wallMs: 30_000 })]), ceilingMs })[0], /fails case heldout\/k001: time limit/);
  assert.match(referenceProblems({ stack: 'csharp', score: scoreOf([], false), ceilingMs })[0], /does not build: .*compiler said no/);
  assert.match(referenceProblems({ stack: 'node', score: scoreOf([caseOf('k001', { peakBytes: null })]), ceilingMs })[0], /no certified peak/);
});

test('A_mutant_must_fail_exactly_the_cases_its_manifest_names_no_more_and_no_fewer', () => {
  const score = ids => scoreOf(['k001', 'k002', 'k003'].map(id => caseOf(id, ids.includes(id) ? { verdict: 'wrong answer' } : {})));
  assert.equal(mutantResult({ id: 'M', expected: ['k003', 'k001'], score: score(['k001', 'k003']) }).ok, true);
  assert.equal(mutantResult({ id: 'M', expected: ['k001'], score: score(['k001', 'k003']) }).ok, false, 'one case too many');
  assert.equal(mutantResult({ id: 'M', expected: ['k001', 'k003'], score: score(['k001']) }).ok, false, 'one case too few');
});

/** A scripted heldout side and scorer: `script(stack, kind)` returns the score of a reference, a mutant or the control. */
function world(script) {
  const calls = [];
  const base = tmpDir('cal-src');
  for (const d of ['refs/csharp', 'refs/node', 'mutants/M-1', 'mutants/M-2']) mkdirSync(path.join(base, d), { recursive: true });
  const heldout = { casesDir: '/cases', refDir: s => path.join(base, 'refs', s), mutantDir: id => path.join(base, 'mutants', id), manifest: { mutants: { 'M-1': { stack: 'node', expectedFail: ['k002'] }, 'M-2': { stack: 'csharp', expectedFail: [] } } } };
  const scoreFn = async args => { calls.push({ stack: args.stack, limits: args.limits }); return script(args, calls.length); };
  return { calls, ctx: { explore: EXPLORE, heldout, files: () => ({}), scoreFn, work: tmpDir('cal'), say: () => {} } };
}

const GOOD = args => ({ ok: true, value: scoreOf(args.sets[0].dir === '/cases' ? ['k001', 'k002'].map(id => caseOf(id, { wallMs: args.stack === 'node' ? 900 : 200 })) : ['c1', 'c2', 'c3'].map(id => caseOf(id, { wallMs: 60, peakBytes: 5 * MIB }))) });

test('Calibration_runs_both_references_under_the_30_s_ceiling_then_the_control_and_the_mutants_under_the_computed_limits', async () => {
  const mutantScore = (id, failing) => scoreOf(['k001', 'k002'].map(c => caseOf(c, failing.includes(c) ? { verdict: 'wrong answer' } : {})));
  const w = world((args, n) => {
    if (n <= 2) return GOOD(args);
    if (n === 3) return GOOD({ ...args, sets: [{ dir: '/control' }] });
    return { ok: true, value: mutantScore('', n === 4 ? ['k002'] : []) };
  });
  const r = await runCalibration(w.ctx);
  assert.equal(r.ok, true, r.reason);
  assert.deepEqual(w.calls.slice(0, 2).map(c => [c.stack, c.limits.timeMs]), [['csharp', 30_000], ['node', 30_000]], 'the references run under the fixed 30 s ceiling');
  assert.equal(r.value.limits.timeMs, 2700, '3 x the slower reference (900 ms)');
  assert.equal(r.value.limits.memoryBytes, 512 * MIB);
  assert.ok(w.calls.slice(2).every(c => c.limits.timeMs === 2700), 'the control and the mutants run under the computed limits');
  assert.deepEqual(r.value.mutants.map(m => [m.id, m.ok]), [['M-1', true], ['M-2', true]]);
  assert.ok(r.value.control.wallMs > 0);
});

test('A_reference_that_fails_a_case_or_exceeds_the_ceiling_stops_calibration_before_any_limits_exist', async () => {
  for (const [name, bad] of [['fails', { verdict: 'wrong answer' }], ['exceeds', { wallMs: 30_000 }]]) {
    const w = world(args => ({ ok: true, value: scoreOf([caseOf('k001', args.stack === 'csharp' ? bad : {})]) }));
    const r = await runCalibration(w.ctx);
    assert.equal(r.ok, false, name);
    assert.equal(w.calls.length, 1, 'it stops at the first reference: nothing else is scored');
    assert.match(r.reason, /csharp reference/);
  }
  const unscorable = world(() => ({ ok: false, reason: 'harness failure: a process remains' }));
  assert.match((await runCalibration(unscorable.ctx)).reason, /could not be scored: harness failure/);
});

test('A_mutant_that_does_not_fail_its_predicted_cases_stops_calibration_naming_it', async () => {
  const w = world((args, n) => (n <= 3 ? GOOD(n === 3 ? { ...args, sets: [{ dir: '/control' }] } : args) : { ok: true, value: scoreOf([caseOf('k001'), caseOf('k002')]) }));
  const r = await runCalibration(w.ctx);
  assert.equal(r.ok, false);
  assert.match(r.reason, /M-1 expected \[k002\] actual \[\]/);
});
