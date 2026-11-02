// `explore.mjs calibrate --task NN` (plan section 4, T-X6, T-X14; olympiad plan D-O3, D-O5): for an ungated draft the independent oracle first (brute.py against gen.py, handcheck.md; lib/oracle.mjs); then both references through the judge's held-out mode with a fixed 30 s ceiling per case; any case over it, or failing, stops with a sentence. Then the
// limits (TL = max(2 s, 3 x the slower reference's slowest case wall), ML = max(512 MB, 2 x the larger reference peak)) and every mutant must fail exactly the cases its manifest names. The limits go to
// the task's calibration file under `~/rsd/lang-v2/explore/` (private; see tasks.mjs) and, from there, into the run's inputs. The scoring itself is injected (`scoreFn`), so the arithmetic and the stops are tested without a box.
import { mkdirSync, rmSync, writeFileSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { systemClock } from './clock.mjs';
import { exploreRoot } from './paths.mjs';
import { loadHeldout, writeSolution } from './heldout.mjs';
import { materialOf } from './material.mjs';
import { checkOracle } from './oracle.mjs';
import { taskPaths } from './tasks.mjs';
import { removeArea } from './score.mjs';

const median = xs => [...xs].sort((a, b) => a - b)[Math.floor(xs.length / 2)];

/** TL and ML from the references' measurements: `refs` is `[{ stack, slowestWallMs, peakBytes }]`. */
export function limitsFrom({ limits, refs }) {
  const slowest = Math.max(...refs.map(r => r.slowestWallMs));
  const peak = Math.max(...refs.map(r => r.peakBytes));
  return { timeMs: Math.max(limits.timeFloorMs, limits.timeFactor * slowest), memoryBytes: Math.max(limits.memoryFloorBytes, limits.memoryFactor * peak), buildTimeoutMs: limits.buildTimeoutMs };
}

/** The sentences saying why a reference's score stops the pilot: a failed build, a failing case, a case at the ceiling, an uncertified peak. */
export function referenceProblems({ stack, score, ceilingMs }) {
  const cases = score.explore.cases;
  const failing = cases.filter(c => c.verdict !== 'pass');
  return [
    ...(score.build.ok ? [] : [`the ${stack} reference does not build: ${score.explore.build.outputTail.slice(-200)}`]),
    ...failing.map(c => `the ${stack} reference fails case ${c.set}/${c.id}: ${c.verdict}${c.detail ? ` (${c.detail})` : ''}`),
    ...cases.filter(c => c.wallMs >= ceilingMs).map(c => `the ${stack} reference takes ${c.wallMs} ms on case ${c.set}/${c.id}, at or over the calibration ceiling of ${ceilingMs} ms`),
    ...cases.filter(c => typeof c.peakBytes !== 'number').map(c => `the ${stack} reference's case ${c.set}/${c.id} has no certified peak`),
  ];
}

/** What one reference measured: its slowest case wall and its largest certified peak. */
export const measuredOf = (stack, score) => ({
  stack, slowestWallMs: Math.max(...score.explore.cases.map(c => c.wallMs)), peakBytes: Math.max(...score.explore.cases.map(c => c.peakBytes ?? 0)), cases: score.explore.cases.length,
  walls: score.explore.cases.map(c => c.wallMs),
});

/** The held-out cases a scored mutant failed, sorted. */
export const failedHeldout = score => score.explore.cases.filter(c => c.set === 'heldout' && c.verdict !== 'pass').map(c => c.id).sort();

const sameSet = (a, b) => JSON.stringify(a) === JSON.stringify(b);

/**
 * A mutant against its manifest entry: `{ id, strict, expected, actual, ok }`. A STRICT mutant (`strict` absent or true) must fail exactly its predicted cases. An ADVISORY one (`strict: false`: a verdict that
 * depends on time, stack depth or pipe chunking, so its exact set is not a property of the logic) is `ok` when it fails at least one case, or when it predicted none; its actual set is recorded beside the prediction.
 */
export function mutantResult({ id, expected, score, strict = true }) {
  const actual = failedHeldout(score);
  const sorted = [...expected].sort();
  const ok = strict ? sameSet(actual, sorted) : actual.length > 0 || sorted.length === 0;
  return { id, strict, expected: sorted, actual, ok };
}

const counts = r => `predicted ${r.expected.length} failing case(s), ${r.actual.length} failed; ${r.expected.filter(x => !r.actual.includes(x)).length} predicted did not fail, ${r.actual.filter(x => !r.expected.includes(x)).length} unpredicted did`;

/** The sentence of a mutant that stops calibration: its id and the counts, never a case id. */
export const mutantProblem = r => (r.strict
  ? `mutant ${r.id} does not fail exactly its predicted cases (${counts(r)})`
  : `advisory mutant ${r.id} was predicted to fail ${r.expected.length} case(s) and failed none`);

/** The note of an advisory mutant whose actual set is not its prediction (a note, never a stop), or null. */
export const mutantNote = r => (!r.strict && r.ok && !sameSet(r.actual, r.expected) ? `advisory mutant ${r.id}: ${counts(r)} (recorded, not a stop)` : null);

/** The empty-program control: what the scope, bwrap and the wrapper cost with a program that does nothing (`run.sh` is `exec true`). */
export const controlOf = score => ({ wallMs: median(score.explore.cases.map(c => c.wallMs)), peakBytes: Math.max(...score.explore.cases.map(c => c.peakBytes ?? 0)) });

/** The plan's minimums of an ungated draft's material (olympiad.material in the pins): cases and mutants, counted from the manifest. -> the sentence, or null. */
function sizeProblem(ctx) {
  const min = ctx.explore.olympiad.material;
  const cases = ctx.heldout.caseIds.length;
  const mutants = Object.keys(ctx.heldout.manifest.mutants ?? {}).length;
  if (cases < min.minCases) return `task ${ctx.task.id} holds ${cases} case(s); the plan needs at least ${min.minCases}`;
  return mutants < min.minMutants ? `task ${ctx.task.id} holds ${mutants} mutant(s); the plan needs at least ${min.minMutants}` : null;
}

/** The checks before any box is used, for an ungated draft: the plan's minimums, then the oracle. An ungated draft's private material must pass `checkOracle` (`ctx.oracleFn`); the first task's predates it. -> `ok(counts | null)` | `fail`. */
async function oracleStep(ctx) {
  const small = ctx.task.draft ? sizeProblem(ctx) : null;
  if (small) return fail(small);
  if (!ctx.task.draft) { ctx.say('oracle: not required (the spec of this task is not an ungated draft)'); return ok(null); }
  return ctx.oracleFn({ heldout: ctx.heldout, say: ctx.say });
}

/**
 * The whole calibration. `ctx` = `{ explore, task, heldout, files(stack) -> workspace files, scoreFn({ stack, source, files, limits, sets }) -> ok(score) | fail, oracleFn({ heldout, say }) -> ok | fail, work, say }`.
 * -> `ok(calibration)` | `fail(sentence)`. The oracle comes first (no box is used when it fails). Nothing is written here; `writeCalibration` does that once the result is `ok`.
 */
export async function runCalibration(ctx) {
  const oracle = await oracleStep(ctx);
  return oracle.ok ? referencesAndMutants(ctx, oracle.value) : oracle;
}

async function referencesAndMutants(ctx, oracle) {
  const { explore, heldout } = ctx;
  const ceiling = { timeMs: explore.limits.calibrationCeilingMs, memoryBytes: 4 * 1024 * 1024 * 1024, buildTimeoutMs: explore.limits.buildTimeoutMs };
  const measured = [];
  for (const stack of ['csharp', 'node']) {
    const r = await calibrateReference(ctx, { stack, ceiling });
    if (!r.ok) return r;
    measured.push(r.value);
  }
  const limits = limitsFrom({ limits: explore.limits, refs: measured });
  const control = await controlRun(ctx, limits);
  return control.ok ? mutantsStep({ ctx, measured, limits, control: control.value, oracle }) : control;
}

/** One reference through the judge at the ceiling: `ok(measured)` or the sentence that stops the pilot. */
async function calibrateReference(ctx, { stack, ceiling }) {
  const scored = await scoreTree1(ctx, { stack, overlayDir: ctx.heldout.refDir(stack), limits: ceiling });
  if (!scored.ok) return fail(`the ${stack} reference could not be scored: ${scored.reason}`);
  const problems = referenceProblems({ stack, score: scored.value, ceilingMs: ctx.explore.limits.calibrationCeilingMs });
  if (problems.length > 0) return fail(problems.join('; '));
  const measured = measuredOf(stack, scored.value);
  ctx.say(`${stack} reference: ${measured.cases} cases pass, slowest ${measured.slowestWallMs} ms, peak ${measured.peakBytes} bytes`);
  return ok(measured);
}

async function scoreTree1(ctx, { stack, overlayDir, limits }) {
  const area = path.join(ctx.work, `tree-${stack}-${path.basename(path.dirname(overlayDir))}-${path.basename(overlayDir)}`);
  rmSync(area, { recursive: true, force: true });
  const files = ctx.files(stack);
  writeSolution({ files, overlayDir, dest: area });
  try {
    return await ctx.scoreFn({ stack, source: area, files, limits, sets: [{ name: 'heldout', dir: ctx.heldout.casesDir }] });
  } finally {
    removeArea(area);
  }
}

/** The control: a workspace whose `run.sh` is `exec true`, run on one empty case through the same scope and box. */
async function controlRun(ctx, limits) {
  const dir = path.join(ctx.work, 'control-src');
  rmSync(dir, { recursive: true, force: true });
  mkdirSync(path.join(dir, 'cases'), { recursive: true });
  writeFileSync(path.join(dir, 'run.sh'), '#!/usr/bin/env bash\nexec true\n');
  writeFileSync(path.join(dir, 'build.sh'), '#!/usr/bin/env bash\nexit 0\n');
  for (const n of ['c1', 'c2', 'c3']) { writeFileSync(path.join(dir, 'cases', `${n}.in`), ''); writeFileSync(path.join(dir, 'cases', `${n}.out`), ''); }
  const scored = await ctx.scoreFn({ stack: 'node', source: dir, files: {}, limits, sets: [{ name: 'heldout', dir: path.join(dir, 'cases') }] });
  rmSync(dir, { recursive: true, force: true });
  return scored.ok ? ok(controlOf(scored.value)) : fail(`the empty-program control could not be scored: ${scored.reason}`);
}

async function mutantsStep({ ctx, measured, limits, control, oracle }) {
  const results = [];
  for (const [id, m] of Object.entries(ctx.heldout.manifest.mutants)) {
    const scored = await scoreTree1(ctx, { stack: m.stack, overlayDir: ctx.heldout.mutantDir(id), limits });
    if (!scored.ok) return fail(`mutant ${id} could not be scored: ${scored.reason}`);
    results.push(mutantResult({ id, expected: m.expectedFail, score: scored.value, strict: m.strict !== false }));
  }
  const wrong = results.filter(r => !r.ok);
  if (wrong.length > 0) return fail(wrong.map(mutantProblem).join('; '));
  const notes = results.map(mutantNote).filter(Boolean);
  notes.forEach(n => ctx.say(`note: ${n}`));
  return ok({ task: ctx.task.id, limits, references: measured, control, mutants: results, notes, oracle, made: ctx.now().toISOString() });
}

/** Write the calibration of `task`, private (0600), into that task's calibration file under the explore root. */
export function writeCalibration({ calibration, home = os.homedir(), task }) {
  const file = taskPaths(task, home).calibrationFile;
  mkdirSync(path.dirname(file), { recursive: true, mode: 0o700 });
  writeFileSync(file, `${JSON.stringify(calibration, null, 2)}\n`, { mode: 0o600 });
  return file;
}


/**
 * The command, for one `task` (its pins entry): load the world and the task's held-out side, calibrate, and write the task's calibration file when it is `ok`. -> `ok({ file, calibration })` | `fail(sentence)`.
 * Nothing is paid and nothing leaves the host: the references and the mutants are scored in the boxes of the judge, the oracle runs `brute.py` on the host.
 */
export async function calibrateCommand({ home = os.homedir(), say = () => {}, worldFn, task, runFn, now = systemClock }) {
  rmSync(taskPaths(task, home).calibrationFile, { force: true }); // a failed recalibration leaves no calibration: the old one vouched for material that may be gone
  const before = materialOf(task, home);
  const work = path.join(exploreRoot(home), 'calibration-work');
  mkdirSync(work, { recursive: true, mode: 0o700 });
  try {
    const world = await worldFn({ home, work, task });
    if (!world.ok) return world;
    const heldout = loadHeldout(home, task);
    if (!heldout.ok) return heldout;
    const oracleFn = a => checkOracle({ ...a, python: world.value.explore.python3.path, tmpRoot: path.join(work, 'brute-tmp'), operatorHome: home, run: runFn });
    const done = await runCalibration({ explore: world.value.explore, task, heldout: heldout.value, files: world.value.filesFor, scoreFn: world.value.scoreFn, oracleFn, work, say, now });
    return done.ok ? bindAndWrite({ calibration: done.value, before, home, task }) : done;
  } finally {
    removeArea(work);
  }
}

/** A finished calibration is bound to the material it checked (recorded in the file); material that changed while it ran fails it. */
function bindAndWrite({ calibration, before, home, task }) {
  const after = materialOf(task, home);
  if (JSON.stringify(after) !== JSON.stringify(before)) return fail(`the held-out material of task ${task.id} changed while it was being calibrated: calibrate again on a folder nobody is editing`);
  const bound = { ...calibration, material: after };
  return ok({ file: writeCalibration({ calibration: bound, home, task }), calibration: bound });
}
