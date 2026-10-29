import assert from 'node:assert/strict';
import { cpSync, existsSync, mkdirSync, readFileSync, readdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { defaultDeps } from '../lib/score-deps.mjs';
import { runScoring, scoringPlan } from '../lib/score-pipeline.mjs';
import { inputs, sandboxGate, stubTrees, tmpDir } from './helpers.mjs';

const MARKER = '.bench-score-owned';

/** Score a stub tree. `deps` overrides the default (real fs, real bwrap, held-out slot unknown). */
async function scoreStub(stub, { arm = 'process', phase = 'v1', stack = 'csharp', deps = {}, limits, prepare } = {}) {
  const sessionDir = tmpDir('session');
  const sourceDir = tmpDir('source');
  cpSync(path.join(stubTrees, stub), sourceDir, { recursive: true });
  prepare?.({ sessionDir, sourceDir });
  const planned = scoringPlan({ arm, stack, phase, sourceDir, sessionDir, limits });
  assert.equal(planned.ok, true, planned.reason);
  const base = defaultDeps({ inputs: await inputs(), toolchainDirs: [], cacheDirs: [] });
  const result = await runScoring(planned.value, { ...base, ...deps });
  return { sessionDir, sourceDir, plan: planned.value, result };
}

/** A `run` that answers every bwrap invocation without running anything: builds succeed, scripts print an all-green summary. */
function greenRun(calls) {
  return async (exe, args, opts) => {
    calls.push({ exe, args, opts });
    const script = args.find(a => /^test-v[12]\.sh$/.test(a));
    const total = script === 'test-v2.sh' ? 30 : 11;
    const stdout = script ? `PASSED: ${total}\nFAILED: 0\nTOTAL:  ${total}\n` : '';
    return { ok: true, value: { code: 0, signal: null, stdout, stderr: '', timedOut: false, truncated: false, ms: 1 } };
  };
}
const greenHeldout = async () => ({ known: true, primary100: true });

test('The_plan_is_the_ordered_steps_and_v2_runs_both_visible_scripts', () => {
  const common = { arm: 'bare', stack: 'node', sourceDir: '/s', sessionDir: '/x/sess' };
  const v1 = scoringPlan({ ...common, phase: 'v1' }).value;
  assert.deepEqual(v1.steps.map(s => s.id), ['sweep', 'copy', 'inputs', 'build', 'visible', 'heldout', 'upstream', 'verdict']);
  assert.deepEqual(v1.steps.find(s => s.id === 'visible').scripts.map(s => s.name), ['test-v1.sh']);
  const v2 = scoringPlan({ ...common, phase: 'v2' }).value;
  assert.deepEqual(v2.steps.find(s => s.id === 'visible').scripts.map(s => s.name), ['test-v2.sh', 'test-v1.sh']);
  assert.deepEqual(v2.steps.find(s => s.id === 'upstream').scripts.map(s => s.inputsPath), ['upstream/test-v2.sh', 'upstream/test-v1.sh']);
  assert.equal(v2.steps.find(s => s.id === 'upstream').referenceOnly, true);
  assert.equal(scoringPlan({ ...common, phase: 'v3' }).ok, false);
  assert.equal(scoringPlan({ ...common, phase: 'v1', arm: 'x' }).ok, false);
  assert.equal(scoringPlan({ ...common, phase: 'v1', sourceDir: '' }).ok, false);
});

test('The_scorer_runs_its_own_test_scripts_not_the_trees_copy', async t => {
  if (!sandboxGate(t)) return;
  const { result } = await scoreStub('claims-green');
  assert.equal(result.ok, true, result.reason);
  const score = result.value;
  assert.deepEqual(score.inputs.inputsTampered, ['test-v1.sh']);
  assert.equal(score.build.ok, true);
  const [visible] = score.visible;
  assert.equal(visible.source, 'harness');
  assert.equal(visible.summaryCaptured, true);
  assert.ok(visible.passed < 11, `passed ${visible.passed}: the harness script ran, not the tree's claim of 11`);
  assert.equal(score.upstream[0].referenceOnly, true);
});

test('A_build_failure_fails_every_case_and_the_session', async t => {
  if (!sandboxGate(t)) return;
  const { result } = await scoreStub('failing-build', { phase: 'v2' });
  const score = result.value;
  assert.equal(score.build.ok, false);
  assert.match(score.build.logTail, /stub build failure: boom/);
  assert.deepEqual(score.visible.map(v => [v.script, v.expectedTotal, v.failed, v.passed, v.reason]),
    [['test-v2.sh', 30, 30, 0, 'build failed'], ['test-v1.sh', 11, 11, 0, 'build failed']]);
  assert.deepEqual(score.upstream.map(v => [v.failed, v.reason, v.referenceOnly]), [[30, 'build failed', true], [11, 'build failed', true]]);
  assert.equal(score.heldout.reason, 'build failed');
  assert.deepEqual(score.accepted, { known: true, value: false });
});

test('A_rebuilt_shim_is_reported_not_hidden', async t => {
  if (!sandboxGate(t)) return;
  const { result } = await scoreStub('rebuild-shim');
  const [visible] = result.value.visible;
  assert.equal(visible.shimIntact, false);
  assert.equal(visible.perInvocationTimeout, 'not enforced');
  const clean = await scoreStub('silent');
  assert.equal(clean.result.value.visible[0].shimIntact, true);
  assert.equal(clean.result.value.visible[0].perInvocationTimeout, 'enforced');
});

test('Accepted_is_unknown_not_false_while_delivery_is_unknown', async () => {
  const calls = [];
  const deps = { run: greenRun(calls), heldout: greenHeldout };
  const process1 = (await scoreStub('silent', { arm: 'process', deps })).result.value;
  assert.deepEqual(process1.accepted, { known: false, missing: ['delivered'] });
  const delivered = (await scoreStub('silent', { arm: 'process', deps: { ...deps, delivered: true } })).result.value;
  assert.deepEqual(delivered.accepted, { known: true, value: true });
  const undelivered = (await scoreStub('silent', { arm: 'process', deps: { ...deps, delivered: false } })).result.value;
  assert.deepEqual(undelivered.accepted, { known: true, value: false });
  const bare = (await scoreStub('silent', { arm: 'bare', deps })).result.value;
  assert.deepEqual(bare.accepted, { known: true, value: true });
  const noHeldout = (await scoreStub('silent', { arm: 'bare', deps: { ...deps, heldout: async () => ({ known: false, why: 'not wired' }) } })).result.value;
  assert.deepEqual(noHeldout.accepted, { known: false, missing: ['heldout'] });
});

test('A_visible_script_below_100_percent_denies_acceptance_even_when_the_rest_is_green', async () => {
  const run = async (exe, args, opts) => {
    const r = await greenRun([])(exe, args, opts);
    if (args.includes('test-v1.sh')) r.value.stdout = 'PASSED: 10\nFAILED: 1\nTOTAL:  11\n';
    return r;
  };
  const { result } = await scoreStub('silent', { arm: 'bare', deps: { run, heldout: greenHeldout } });
  assert.deepEqual(result.value.accepted, { known: true, value: false });
});

test('The_build_runs_make_then_build_sh_then_chmod_and_each_visible_script_is_bounded_by_its_invocation_count', async () => {
  const calls = [];
  const prepare = ({ sourceDir }) => { writeFileSync(path.join(sourceDir, 'Makefile'), 'all:\n'); writeFileSync(path.join(sourceDir, 'build.sh'), 'true\n'); };
  await scoreStub('silent', { phase: 'v2', deps: { run: greenRun(calls), heldout: greenHeldout }, prepare });
  const inBox = calls.map(c => c.args.slice(c.args.indexOf('--') + 1));
  assert.deepEqual(inBox.slice(0, 3), [['make', '-s'], ['bash', 'build.sh'], ['chmod', '+x', 'minigit']]);
  const { root } = await inputs();
  const count = file => readFileSync(path.join(root, file), 'utf8').split('../minigit').length - 1;
  const scriptCalls = calls.filter(c => c.args.some(a => /^test-v[12]\.sh$/.test(a)));
  assert.deepEqual(scriptCalls.map(c => c.opts.timeoutMs), [
    (600 + 10 * count('test-v2.sh')) * 1000, (600 + 10 * count('test-v1.sh')) * 1000,
    (600 + 10 * count('upstream/test-v2.sh')) * 1000, (600 + 10 * count('upstream/test-v1.sh')) * 1000]);
  assert.equal(calls[0].opts.timeoutMs, 600_000);
  assert.equal(calls[0].exe, 'bwrap');
});

test('The_sweep_removes_old_marked_leftovers_and_never_touches_an_unmarked_directory', async () => {
  const wallS = 100;
  const prepare = ({ sessionDir }) => {
    const score = path.join(sessionDir, 'score');
    mkdirSync(path.join(score, 'run-old'), { recursive: true });
    writeFileSync(path.join(score, 'run-old', MARKER), JSON.stringify({ sessionId: 'x', startedMs: 0 }));
    writeFileSync(path.join(score, 'run-old', 'bulk.bin'), 'x');
    mkdirSync(path.join(score, 'run-foreign'), { recursive: true });
    writeFileSync(path.join(score, 'run-foreign', 'keep.txt'), 'x');
  };
  const calls = [];
  const deps = { run: greenRun(calls), heldout: greenHeldout, now: () => 10 * wallS * 1000 };
  const { sessionDir, result } = await scoreStub('silent', { limits: { wallS }, deps, prepare });
  assert.equal(result.ok, true, result.reason);
  assert.deepEqual(result.value.sweep.removed, ['run-old']);
  assert.deepEqual(result.value.sweep.unmarked, ['run-foreign']);
  assert.equal(existsSync(path.join(sessionDir, 'score', 'run-old')), false);
  assert.equal(readFileSync(path.join(sessionDir, 'score', 'run-foreign', 'keep.txt'), 'utf8'), 'x');
});

test('A_young_marked_copy_means_another_scoring_is_running_and_this_one_refuses', async () => {
  const prepare = ({ sessionDir }) => {
    const copy = path.join(sessionDir, 'score', 'copy');
    mkdirSync(copy, { recursive: true });
    writeFileSync(path.join(copy, MARKER), JSON.stringify({ sessionId: 'x', startedMs: 1_000_000 }));
  };
  const { result } = await scoreStub('silent', { deps: { run: greenRun([]), heldout: greenHeldout, now: () => 1_000_500 }, prepare });
  assert.equal(result.ok, false);
  assert.match(result.reason, /copy/);
  assert.match(result.reason, /in progress|younger/);
});

test('The_copies_are_removed_at_the_end_and_carry_the_marker_while_they_exist', async () => {
  const markers = [];
  const run = async (exe, args, opts) => {
    const mount = args[args.indexOf('--chdir') + 1];
    if (mount && existsSync(path.join(mount, MARKER))) markers.push(path.basename(mount));
    return greenRun([])(exe, args, opts);
  };
  const { sessionDir, result } = await scoreStub('silent', { deps: { run, heldout: greenHeldout } });
  assert.equal(result.ok, true);
  assert.ok(markers.includes('copy') && markers.some(n => n.startsWith('run-')), `marked while running: ${markers}`);
  const left = readdirSync(path.join(sessionDir, 'score'));
  assert.equal(left.includes('copy'), false);
  assert.equal(left.some(n => n.startsWith('run-')), false);
  assert.deepEqual(result.value.cleanup.failed, []);
});

test('The_copy_leaves_out_build_output_and_dependencies_by_stack', async () => {
  const mkTree = ({ sourceDir }) => {
    for (const d of ['bin', 'obj', 'node_modules', 'src/App/bin', 'src/App/obj', 'src/keep']) mkdirSync(path.join(sourceDir, d), { recursive: true });
    for (const d of ['bin', 'obj', 'node_modules', 'src/App/bin', 'src/keep']) writeFileSync(path.join(sourceDir, d, 'f.txt'), 'x');
  };
  const { fs: depsFs } = defaultDeps({ inputs: await inputs(), toolchainDirs: [], cacheDirs: [] });
  const src = tmpDir('cp-src');
  mkTree({ sourceDir: src });
  const list = dst => readdirSync(dst, { recursive: true }).filter(p => p.endsWith('f.txt')).sort();
  const cs = path.join(tmpDir('cp-dst'), 'c');
  depsFs.copyTree(src, cs, 'csharp');
  assert.deepEqual(list(cs), ['src/keep/f.txt']);
  const node = path.join(tmpDir('cp-dst'), 'n');
  depsFs.copyTree(src, node, 'node');
  assert.deepEqual(list(node), ['bin/f.txt', 'obj/f.txt', 'src/App/bin/f.txt', 'src/keep/f.txt']);
});

test('A_build_that_outlives_its_limit_is_ended_and_reported_as_timed_out', async t => {
  if (!sandboxGate(t)) return;
  const prepare = ({ sourceDir }) => writeFileSync(path.join(sourceDir, 'build.sh'), 'sleep 60\n');
  const started = Date.now();
  const { result } = await scoreStub('silent', { limits: { buildS: 1 }, prepare });
  assert.ok(Date.now() - started < 10_000);
  assert.equal(result.value.build.timedOut, true);
  assert.equal(result.value.build.ok, false);
  assert.equal(result.value.visible[0].reason, 'build failed');
});

test('A_build_that_leaves_no_executable_minigit_is_not_a_build', async t => {
  if (!sandboxGate(t)) return;
  const { result } = await scoreStub('failing-build', { prepare: ({ sourceDir }) => writeFileSync(path.join(sourceDir, 'build.sh'), 'true\n') });
  assert.equal(result.value.build.exitCode, 0);
  assert.equal(result.value.build.executable, false);
  assert.equal(result.value.build.ok, false);
});
