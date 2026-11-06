// E1.S8b: the launch prefix and the driver's command — the one place the delegated scope, the namespaces and the box are put together (plan, "The shape, delegated scope").
//   <systemd-run … scope-entry.sh <out-dir> <memory.max> <hold>> [nsenter -t <holder> -U -n --preserve-credentials] <bwrap … box> -- bash run.sh
// The runner IS the driver: it runs under `nsenter` already, so everything it starts is in the holder's user and network namespaces, and a second `nsenter -U` from there fails (EINVAL,
// "reassociate to namespaces failed"; measured 2026-11-02). The prefix handed to the runner therefore has no `nsenter`. A launch from OUTSIDE the namespaces (the certification launch,
// a test) takes the same prefix with the `nsenter` put in: `programArgv`. Both name the same chain.
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { boxArgv } from './box.mjs';
import { viewTemplate } from './case-view.mjs';
import { nsenterArgv } from './netns.mjs';
import { progTemplate, scopePrefix } from './scope.mjs';
import { assertRunning } from './stopping.mjs';

export const STATUS_FD = 7;
/** The one margin of the layer: the scope holder's bound is T + G + this, the netns holder's is the run ceiling + this. */
export const HOLD_MARGIN_S = 60;
const DEFAULT_T_S = 10;
const DEFAULT_G_S = 5;
export const RUNNER = fileURLToPath(new URL('../../runner/run_samples.py', import.meta.url));

/** The runner's wall for a case — the time limit T plus the stop grace G, in seconds — from the limits file (the series defaults T = 10 s and G = 5 s where a key is absent). */
export function requiredWallSeconds(limits) {
  return (limits.timeS ?? DEFAULT_T_S) + (limits.graceS ?? DEFAULT_G_S);
}

/**
 * The scope holder's bound. By default T + G of the runner's limits plus the margin, so a crashed layer's holder still ends but never before the runner's own end of a case. A caller whose cases run longer than T + G (a
 * daemon or web case lasts up to the runner's test bound) gives `options.holdSeconds`: when the holder ends, the scope and the program in it end with it. T and G are never raised for this: a scenario whose T or G differs
 * from the registered ones is refused.
 */
export const holdSeconds = run => run.options.holdSeconds ?? Math.ceil(requiredWallSeconds(run.limits)) + HOLD_MARGIN_S;

/** The hold for cases that may run `caseWallMs`: that long plus the margin. */
export const holdForCaseWall = caseWallMs => Math.ceil(caseWallMs / 1000) + HOLD_MARGIN_S;

/** `--launch-prefix`: everything before `bash run.sh`, `{case}` left for the runner. `outside` puts the `nsenter` in (a launch from outside the namespaces). */
export function launchPrefix(run, { statusFd = STATUS_FD, outside = false } = {}) {
  assertRunning('a scope');
  const scope = scopePrefix({ unit: `${run.unitPrefix}-{case}`, outDir: path.join(run.dir, 'out', '{case}'), memoryMaxBytes: run.options.memoryMaxBytes, holdSeconds: holdSeconds(run), systemdRun: run.tools.systemdRun });
  const join = outside ? nsenterArgv(run.holder, run.tools.nsenter) : [];
  return [...scope, ...join, ...boxArgv({ view: viewTemplate(run.dir), dirs: run.options.dirs, env: run.env, statusFd, extraRo: run.options.extraRo, bwrap: run.tools.bwrap, prog: progTemplate(run.unitPrefix), tree: run.options.tree })];
}

/** The whole argv of a program launched from OUTSIDE the namespaces, `{case}` filled in; no status descriptor unless `statusFd` is given. */
export function programArgv(run, caseId, program = ['bash', 'run.sh'], { statusFd = null } = {}) {
  return [...launchPrefix(run, { statusFd, outside: true }).map(arg => arg.replaceAll('{case}', caseId)), ...program];
}

/** The driver's command: the runner under `nsenter -t <holder> -U -n --preserve-credentials`, outside the scope, with the layer's prefix, `--cgroup-of prog/` and `--case-view`. */
export function runnerArgv(run, { cgroupOf, jsonOut }) {
  const { samplesDir, tree, limitsFile, kind = 'olympiad' } = run.options;
  return [
    ...nsenterArgv(run.holder, run.tools.nsenter), run.tools.python, '-I', RUNNER, '--workspace', tree, '--kind', kind, '--no-build',
    ...(limitsFile ? ['--limits', limitsFile] : []), '--launch-prefix', JSON.stringify(launchPrefix(run)), '--launch-status', String(STATUS_FD),
    '--cgroup-of', cgroupOf, '--case-view', viewTemplate(run.dir), '--json', jsonOut, samplesDir,
  ];
}
