// `explore.mjs calibrate` (plan section 4, T-X6, T-X14): both references through the judge's held-out mode with a fixed 30 s ceiling per case; any case over it, or failing, stops with a sentence. Then the
// limits (TL = max(2 s, 3 x the slower reference's slowest case wall), ML = max(512 MB, 2 x the larger reference peak)) and every mutant must fail exactly the cases its manifest names. The limits go to
// `~/rsd/lang-v2/explore/calibration.json` (private) and, from there, into the run's inputs. The scoring itself is injected (`scoreFn`), so the arithmetic and the stops are tested without a box.
import { mkdirSync, rmSync, writeFileSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { calibrationFile, exploreRoot } from './paths.mjs';
import { loadHeldout, writeSolution } from './heldout.mjs';
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

/** A mutant against its manifest entry: `{ id, expected, actual, ok }`. */
export function mutantResult({ id, expected, score }) {
  const actual = failedHeldout(score);
  return { id, expected: [...expected].sort(), actual, ok: JSON.stringify(actual) === JSON.stringify([...expected].sort()) };
}

/** The empty-program control: what the scope, bwrap and the wrapper cost with a program that does nothing (`run.sh` is `exec true`). */
export const controlOf = score => ({ wallMs: median(score.explore.cases.map(c => c.wallMs)), peakBytes: Math.max(...score.explore.cases.map(c => c.peakBytes ?? 0)) });

/**
 * The whole calibration. `ctx` = `{ explore, heldout, files(stack) -> workspace files, scoreFn({ stack, source, files, limits, sets }) -> ok(score) | fail, work, say }`. -> `ok(calibration)` | `fail(sentence)`.
 * Nothing is written here; `writeCalibration` does that once the result is `ok`.
 */
export async function runCalibration(ctx) {
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
  return control.ok ? mutantsStep({ ctx, measured, limits, control: control.value }) : control;
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

async function mutantsStep({ ctx, measured, limits, control }) {
  const results = [];
  for (const [id, m] of Object.entries(ctx.heldout.manifest.mutants)) {
    const scored = await scoreTree1(ctx, { stack: m.stack, overlayDir: ctx.heldout.mutantDir(id), limits });
    if (!scored.ok) return fail(`mutant ${id} could not be scored: ${scored.reason}`);
    results.push(mutantResult({ id, expected: m.expectedFail, score: scored.value }));
  }
  const wrong = results.filter(r => !r.ok);
  if (wrong.length > 0) return fail(`a mutant does not fail exactly its predicted cases: ${wrong.map(r => `${r.id} expected [${r.expected}] actual [${r.actual}]`).join('; ')}`);
  return ok({ limits, references: measured, control, mutants: results, made: new Date().toISOString() });
}

/** Write the calibration, private (0600), under the explore root. */
export function writeCalibration({ calibration, home = os.homedir() }) {
  mkdirSync(exploreRoot(home), { recursive: true, mode: 0o700 });
  const file = calibrationFile(home);
  writeFileSync(file, `${JSON.stringify(calibration, null, 2)}\n`, { mode: 0o600 });
  return file;
}


/**
 * The command: load the world and the held-out side, calibrate, and write `calibration.json` when it is `ok`. -> `ok({ file, calibration })` | `fail(sentence)`.
 * Nothing is paid and nothing leaves the host: the references and the mutants are scored in the boxes of the judge.
 */
export async function calibrateCommand({ home = os.homedir(), say = () => {}, worldFn }) {
  const work = path.join(exploreRoot(home), 'calibration-work');
  mkdirSync(work, { recursive: true, mode: 0o700 });
  try {
    const world = await worldFn({ home, work });
    if (!world.ok) return world;
    const heldout = loadHeldout(home);
    if (!heldout.ok) return heldout;
    const done = await runCalibration({ explore: world.value.explore, heldout: heldout.value, files: world.value.filesFor, scoreFn: world.value.scoreFn, work, say });
    return done.ok ? ok({ file: writeCalibration({ calibration: done.value, home }), calibration: done.value }) : done;
  } finally {
    removeArea(work);
  }
}
