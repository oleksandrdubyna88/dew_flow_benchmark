// Steps 4-6 of the scoring pipeline: a visible/upstream test script run on its own fresh copy of the built tree, and the held-out slot.
import path from 'node:path';
import { expectedTotal } from './inputs.mjs';
import { boxFor, inBox, markOwned } from './score-steps.mjs';
import { installShim, parseVisibleOutput, shimIntact } from './visible.mjs';

const tail = (text, n = 1500) => (text.length > n ? text.slice(-n) : text);
const failedRecord = (script, expected, extra) => ({
  script: script.name, source: script.source, referenceOnly: script.referenceOnly, expectedTotal: expected,
  summaryCaptured: false, passed: 0, failed: expected, total: expected, buildFailed: false, failLines: [], ...extra,
});

/** The whole-script limit: the base plus the per-invocation limit once for every `../minigit` in the script. */
export const scriptLimitS = (text, limits) => limits.scriptBaseS + limits.perInvocationS * (text.split('../minigit').length - 1);

/**
 * One script, on a fresh copy of the BUILT tree (marked first, nothing left out: build output is what runs), with the harness-side
 * script dropped in and bound read-only over the copy, and the timeout shim in front of minigit.
 */
async function runOneScript(ctx, script, text) {
  const { plan, deps } = ctx;
  const expected = expectedTotal(text);
  const name = `run-${script.kind}-${script.name}`;
  const runDir = path.join(plan.scoreDir, name);
  markOwned(ctx, name);
  deps.fs.copyTree(plan.copyDir, runDir, []);
  deps.fs.writeFile(path.join(runDir, script.name), text, 0o755);
  const shim = installShim(runDir, plan.limits.perInvocationS);
  if (!shim.ok) return failedRecord(script, expected, { reason: shim.reason });
  const limitS = scriptLimitS(text, plan.limits);
  const started = deps.now();
  const r = await inBox(ctx, runDir, ['bash', script.name], limitS, { roFiles: [path.join(runDir, script.name)] });
  const intact = shimIntact(runDir, shim.value.sha256);
  return {
    ...failedRecord(script, expected, {}), ...parseVisibleOutput(r.stdout, expected),
    exitCode: r.code, signal: r.signal, timedOut: r.timedOut, limitS, ms: deps.now() - started,
    shimIntact: intact, perInvocationTimeout: intact ? 'enforced' : 'not enforced', outputTail: tail(r.stdout + r.stderr),
  };
}

/** Steps 4 and 6. `built` false: every case failed with the reason "build failed" and the script's expected total. */
export async function stepScripts(ctx, scripts, built) {
  const records = [];
  for (const script of scripts) {
    const text = ctx.deps.fs.readFile(path.join(ctx.deps.inputs.root, script.inputsPath));
    if (text === null) throw new Error(`pinned input ${script.inputsPath} is missing from ${ctx.deps.inputs.root}`);
    const body = text.toString('utf8');
    records.push(built ? await runOneScript(ctx, script, body) : failedRecord(script, expectedTotal(body), { reason: 'build failed' }));
  }
  return records;
}

/**
 * Step 5. The held-out slot: `deps.heldout({ binary, phase, box, casesDir, scoreDir })` (heldout-run.mjs `makeHeldoutDep`) answers
 * `{ known: true, primary100, ... }` or `{ known: false, why }` (no key, no suite, a failed runner), never a pass by default.
 */
export async function stepHeldout(ctx, built) {
  const { plan, deps } = ctx;
  if (!built) return { known: true, primary100: false, reason: 'build failed' };
  const box = boxFor(ctx, plan.copyDir, { cwd: false, cases: true });
  return deps.heldout({ binary: path.join(plan.copyDir, 'minigit'), phase: plan.phase, box, casesDir: plan.casesDir, scoreDir: plan.scoreDir });
}
