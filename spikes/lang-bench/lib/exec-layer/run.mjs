// E1.S8b: the execution layer — one run of the runner (E1.S8a) through the delegated scope, the box and the holder's namespaces (plan "The shape, delegated scope"; parent D43).
//
//   startup sweep (an earlier killed run's scopes, holders, views)  ->  the run folder + the netns holder + the case views  ->  certification of the memory read
//   ->  the driver under `nsenter` OUTSIDE the scope (the runner, with the layer's prefix, `--cgroup-of prog/`, `--case-view`)  ->  ONE cleanup, whatever happened.
//
// One run = one runner process = one holder: the runner is the driver and the program of every case joins the namespace the driver is already in. One scope per case and one for the
// control `empty-program` (the unit name holds `{case}`); the view of every case is made before the runner starts (disk budget: see case-view.mjs). A data run (`data: true`) halts with
// `memory-not-certifiable`, exit 3, when `prog/memory.peak` cannot be read; any other run proceeds with `memoryCertified: false`. Nothing here changes the runner.
import { readFileSync, readlinkSync } from 'node:fs';
import path from 'node:path';
import { findOnPath } from '../../../minigit-bench/lib/find-on-path.mjs';
import { runBounded } from '../../../minigit-bench/lib/run.mjs';
import { fail, ok } from '../../../minigit-bench/lib/result.mjs';
import { makeViews } from './case-view.mjs';
import { certifyMemory, HALT } from './certify.mjs';
import { cleanup, sweepStale } from './cleanup.mjs';
import { boxEnv, driverEnv } from './env.mjs';
import { holderNameOf, RUN_WHAT, runIdOf, tooLongForUnit, unitPrefixOf } from './names.mjs';
import { startHolder } from './netns.mjs';
import { launchPrefix, programArgv, runnerArgv, holdSeconds } from './prefix.mjs';
import { progTemplate } from './scope.mjs';

export { launchPrefix, programArgv, runnerArgv };

const SBIN_PATH = '/usr/sbin:/usr/bin:/bin:/sbin';
const HOLDER_MARGIN_S = 60;
const EXIT_ENVIRONMENT = 3;
const EXIT_RUNNER = 5;
const RECORD = 'record.json';

const TOOLS = Object.freeze({ systemdRun: 'systemd-run', nsenter: 'nsenter', bwrap: 'bwrap', python: 'python3' });

/** The absolute path of each host tool the prefix names (absolute: the launch does not depend on a PATH); throws naming the ones missing. */
function findTools() {
  const found = Object.fromEntries(Object.entries(TOOLS).map(([key, name]) => [key, findOnPath(name, SBIN_PATH)]));
  const missing = Object.entries(found).filter(([, p]) => p === null).map(([key]) => TOOLS[key]);
  if (missing.length > 0) throw new Error(`${missing.join(', ')} not found: the layer needs systemd-run, nsenter, bwrap and python3`);
  return found;
}

function newRun(options, tools) {
  const { owned } = options;
  const scope = owned.cleanupScope();
  const dir = owned.ownedDir(scope, RUN_WHAT, { parent: options.parent });
  const id = runIdOf(owned, dir);
  return { owned, scope, dir, id, unitPrefix: unitPrefixOf(owned, id), holderName: holderNameOf(owned, id), options, tools, env: boxEnv({ dirs: options.dirs, extra: options.extraEnv, stack: options.stack, memoryMaxBytes: options.memoryMaxBytes }), holder: null, holderNet: null };
}

async function openSteps(run) {
  const { options } = run;
  const long = tooLongForUnit(run.unitPrefix, options.caseIds);
  if (long.length > 0) return fail(`the unit name of case ${long.join(', ')} would pass 63 characters; use shorter case ids`);
  const holder = await startHolder({ name: run.holderName, boundSeconds: options.runCeilingMs / 1000 + HOLDER_MARGIN_S });
  if (!holder.ok) return fail(`the network-namespace holder did not start: ${holder.reason}`);
  Object.assign(run, { holder: holder.value, holderNet: readlinkSync(`/proc/${holder.value.pid}/ns/net`) });
  const views = makeViews({ tree: options.tree, runDir: run.dir, caseIds: options.caseIds });
  return views.ok ? ok(run) : views;
}

/**
 * `ok(run)`: the run folder (owned, claimed), the netns holder and the case views, or `fail(reason)` after the same cleanup that ends a run has removed whatever was made.
 * `options`: `{ owned, parent, tree, samplesDir, caseIds, dirs, memoryMaxBytes, caseWallMs, runCeilingMs, stack?, limitsFile?, extraEnv?, extraRo? }`; `stack` (`'csharp'` or `'node'`) selects the per-stack keys of the box environment.
 */
export async function openRun(options) {
  let run = null;
  let opened;
  try {
    run = newRun(options, findTools());
    opened = await openSteps(run);
  } catch (error) {
    opened = fail(`the run could not be opened: ${error.message}`);
  }
  if (!opened.ok && run !== null) await cleanup(run);
  return opened;
}

const readJson = file => {
  try {
    return JSON.parse(readFileSync(file, 'utf8'));
  } catch {
    return null;
  }
};

function runnerResult(ran, run, certification) {
  if (!ran.ok) return { exitCode: EXIT_RUNNER, stdout: '', stderr: ran.reason, record: null };
  const { stdout, stderr, code, timedOut } = ran.value;
  const ceiling = `\nthe layer's ceiling of ${run.options.runCeilingMs} ms ended the run: the driver was stopped and nothing was judged`;
  return {
    exitCode: timedOut ? EXIT_RUNNER : code ?? EXIT_RUNNER, stdout, stderr: timedOut ? `${stderr}${ceiling}` : stderr,
    record: readJson(path.join(run.dir, RECORD)), memoryCertified: certification.certified,
  };
}

async function drive(run, certification) {
  const argv = runnerArgv(run, { cgroupOf: progTemplate(run.unitPrefix), jsonOut: path.join(run.dir, RECORD) });
  const ran = await runBounded(argv[0], argv.slice(1), { timeoutMs: run.options.runCeilingMs, env: driverEnv({ dirs: run.options.dirs, tmp: run.dir }) });
  return runnerResult(ran, run, certification);
}

async function body(run, options) {
  options.observe?.(run);
  const certification = await certifyMemory(run, options.deps);
  if (!certification.launched) {
    return { exitCode: EXIT_ENVIRONMENT, stdout: '', stderr: `launch failed: ${certification.reason}`, record: null, halt: null, launchFailed: true, memoryCertified: false };
  }
  if (options.data && !certification.certified) {
    return { exitCode: EXIT_ENVIRONMENT, stdout: '', stderr: `${HALT}: ${certification.reason}`, record: null, halt: HALT, memoryCertified: false };
  }
  return { halt: null, ...(await drive(run, certification)) };
}

/**
 * One run through the layer: `{ exitCode, stdout, stderr, record, halt, memoryCertified, sweep, cleanup }`. `options` are those of `openRun` plus `data` (a data run: certification refuses),
 * `minSweepAgeMs` (the startup sweep's age floor, default 10 minutes), `observe(run)` (called once the run is open and before anything starts) and `deps` (the seams of `certifyMemory`). `exitCode` is the runner's (0 passed, 1 a case failed, 3 environment, 4 configuration,
 * 5 the runner or the layer failed); `halt` is `'memory-not-certifiable'` for a data run that stopped before any case; `record` is the runner's `--json` or null. Never throws.
 */
export async function runCases(options) {
  const sweep = await sweepStale({ owned: options.owned, parent: options.parent, minAgeMs: options.minSweepAgeMs });
  const opened = await openRun(options);
  if (!opened.ok) return { exitCode: EXIT_ENVIRONMENT, stdout: '', stderr: opened.reason, record: null, halt: null, memoryCertified: false, sweep, cleanup: { evidence: {}, removed: [], left: [], clean: true } };
  const run = opened.value;
  let result;
  try {
    result = await body(run, options);
  } catch (error) {
    result = { exitCode: EXIT_RUNNER, stdout: '', stderr: `the layer failed: ${error.message}`, record: null, halt: null, memoryCertified: false };
  }
  return { ...result, sweep, cleanup: await cleanup(run), run: { id: run.id, unitPrefix: run.unitPrefix, holderNet: run.holderNet } };
}
