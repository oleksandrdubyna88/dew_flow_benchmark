// One command scores one finished session the same way for both arms: input hashes, a loud build from clean, the harness's own fixed
// visible tests, the held-out slot, and the upstream originals as reference only -- every agent-written process in the scoring box.
// `scoringPlan` is pure; `runScoring(plan, deps)` takes its dependencies (see score-deps.mjs) so tests inject stubs.
import { randomBytes } from 'node:crypto';
import path from 'node:path';
import { fail, ok } from './result.mjs';
import { stepScripts, stepHeldout } from './score-scripts.mjs';
import { ScorerEnvironmentError, stepBuild, stepCleanup, stepCopy, stepInputs, stepPreflight, stepSweep } from './score-steps.mjs';

export const DEFAULT_LIMITS = Object.freeze({ buildS: 600, scriptBaseS: 600, perInvocationS: 10, wallS: 7200 });
const ARMS = ['process', 'bare'];
const STACKS = ['csharp', 'node'];
const PHASES = ['v1', 'v2'];

const scriptsFor = (phase, kind, dir) =>
  (phase === 'v2' ? ['test-v2.sh', 'test-v1.sh'] : ['test-v1.sh']).map(name => ({
    name, kind, source: kind === 'visible' ? 'harness' : 'upstream', referenceOnly: kind === 'upstream', inputsPath: `${dir}${name}` }));

/** The ordered steps for one session. Pure. A bad argument is a `fail`, never a throw. */
export function scoringPlan({ arm, stack, phase, sourceDir, sessionDir, limits = {}, nugetCache, npmCache }) {
  const bad = [[ARMS, arm, 'arm'], [STACKS, stack, 'stack'], [PHASES, phase, 'phase']].find(([allowed, v]) => !allowed.includes(v));
  if (bad) return fail(`${bad[2]} must be one of ${bad[0].join('|')}, got ${JSON.stringify(bad[1])}`);
  if (!sourceDir || !sessionDir) return fail('sourceDir and sessionDir are required');
  const scoreDir = path.join(sessionDir, 'score');
  return ok(Object.freeze({
    arm, stack, phase, sourceDir, sessionDir, scoreDir, nugetCache, npmCache,
    nugetCopy: nugetCache ? path.join(scoreDir, 'cache-nuget') : undefined,
    npmCopy: npmCache ? path.join(scoreDir, 'cache-npm') : undefined,
    sessionId: path.basename(sessionDir),
    copyDir: path.join(scoreDir, 'copy'), homeDir: path.join(scoreDir, 'home'), tmpDir: path.join(scoreDir, 'tmp'), casesDir: path.join(scoreDir, 'cases'),
    limits: Object.freeze({ ...DEFAULT_LIMITS, ...limits }),
    steps: Object.freeze([
      { id: 'sweep' }, { id: 'copy' }, { id: 'preflight' }, { id: 'inputs' }, { id: 'build' },
      { id: 'visible', scripts: scriptsFor(phase, 'visible', '') },
      { id: 'heldout' },
      { id: 'upstream', referenceOnly: true, scripts: scriptsFor(phase, 'upstream', 'upstream/') },
      { id: 'verdict' },
    ]),
  }));
}

/** A script's result is 100 % only when its summary says so AND the script itself ended cleanly: exit 0, no signal, no timeout. */
export const hundredPercent = s => s.summaryCaptured && !s.buildFailed && s.failed === 0 && s.passed === s.expectedTotal
  && s.exitCode === 0 && s.timedOut === false && s.signal == null;

/**
 * accepted = build ok AND every visible script 100 % AND held-out primary 100 % AND (process) delivered. A definite failure is
 * `{ known: true, value: false }`; otherwise a missing ingredient makes it `{ known: false, missing }` -- never `false`.
 */
export function verdict({ arm, build, visible, heldout, delivered }) {
  const heldoutFalse = heldout.known && !heldout.primary100;
  const deliveredFalse = arm === 'process' && delivered === false;
  if (!build.ok || !visible.every(hundredPercent) || heldoutFalse || deliveredFalse) return { known: true, value: false };
  const missing = [...(heldout.known ? [] : ['heldout']), ...(arm === 'process' && delivered === undefined ? ['delivered'] : [])];
  return missing.length > 0 ? { known: false, missing } : { known: true, value: true };
}

const line = (id, text) => `${id}: ${text}`;
const summary = rec => `${rec.passed}/${rec.expectedTotal}${rec.shimIntact === false ? ' (shim replaced)' : ''}`;

async function runSteps(ctx) {
  const { plan, deps } = ctx;
  const trace = [];
  const sweep = stepSweep(ctx);
  trace.push(line('sweep', `removed ${sweep.removed.length}, unmarked ${sweep.unmarked.join(',') || 'none'}`));
  const copied = stepCopy(ctx, sweep);
  if (!copied.ok) return copied;
  const started = await stepPreflight(ctx);
  if (!started.ok) return started;
  const inputs = stepInputs(ctx);
  trace.push(line('inputs', `tampered [${inputs.inputsTampered}] absent [${inputs.inputsAbsent}]`));
  const build = await stepBuild(ctx);
  trace.push(line('build', build.ok ? `ok in ${build.ms} ms` : `FAILED (exit ${build.exitCode}${build.timedOut ? ', timed out' : ''})`));
  const visible = await stepScripts(ctx, plan.steps.find(s => s.id === 'visible').scripts, build.ok);
  visible.forEach(v => trace.push(line('visible', `${v.script} ${summary(v)}`)));
  const heldout = await stepHeldout(ctx, build.ok);
  trace.push(line('heldout', heldout.known ? `primary100=${heldout.primary100}` : `unknown (${heldout.why})`));
  const upstream = await stepScripts(ctx, plan.steps.find(s => s.id === 'upstream').scripts, build.ok);
  upstream.forEach(v => trace.push(line('upstream (reference only)', `${v.script} ${summary(v)}`)));
  const accepted = verdict({ arm: plan.arm, build, visible, heldout, delivered: deps.delivered });
  trace.push(line('verdict', JSON.stringify(accepted)));
  return ok({ sweep, ...inputs, build, visible, heldout, upstream, accepted, trace });
}

/** Run the plan. Always cleans up the copies it made. Resolves to ok(score) or fail(reason); an unexpected error is thrown. */
export async function runScoring(plan, deps) {
  const ctx = { plan, deps, runId: randomBytes(6).toString('hex'), startedMs: deps.now() };
  let body;
  try {
    body = await runSteps(ctx);
  } catch (e) {
    if (!(e instanceof ScorerEnvironmentError)) throw e;
    body = fail(`the scoring box could not run: ${e.message}`, { kind: 'environment' });
  } finally {
    ctx.cleanup = stepCleanup(ctx);
  }
  if (!body.ok) return body;
  const { inputsTampered, inputsAbsent, ...rest } = body.value;
  return ok({ format: 1, sessionId: plan.sessionId, arm: plan.arm, stack: plan.stack, phase: plan.phase, startedMs: ctx.startedMs,
    inputs: { inputsTampered, inputsAbsent }, ...rest, cleanup: ctx.cleanup });
}
