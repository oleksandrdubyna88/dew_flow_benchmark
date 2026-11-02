// E1.S8b: the execution layer — one run of the runner (E1.S8a) through the delegated scope, the box and the holder's namespaces (plan "The shape, delegated scope"; parent D43).
//
//   startup sweep (an earlier killed run's scopes, holders, views)  ->  the run folder + the netns holder + the case views  ->  certification of the memory read
//   ->  the driver under `nsenter` OUTSIDE the scope (the runner, with the layer's prefix, `--cgroup-of prog/`, `--case-view`)  ->  ONE cleanup, whatever happened.
//
// One run = one runner process = one holder: the runner is the driver and the program of every case joins the namespace the driver is already in. One scope per case and one for the
// control `empty-program` (the unit name holds `{case}`); the view of every case is made before the runner starts (disk budget: see case-view.mjs). A data run (`data: true`) halts with
// `memory-not-certifiable`, exit 3, when `prog/memory.peak` cannot be read; any other run proceeds with `memoryCertified: false`. Nothing here changes the runner.
import { mkdirSync, readFileSync, readlinkSync, statSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { findOnPath } from '../../../minigit-bench/lib/find-on-path.mjs';
import { runBounded } from '../../../minigit-bench/lib/run.mjs';
import { fail, ok } from '../../../minigit-bench/lib/result.mjs';
import { makeViews } from './case-view.mjs';
import { certifyMemory, HALT } from './certify.mjs';
import { cleanup, registerRun, sweepStale, trackChild } from './cleanup.mjs';
import { computeIdentity, LAYER_DIR } from './identity.mjs';
import { boxEnv, driverEnv } from './env.mjs';
import { assertRunning } from './stopping.mjs';
import { HALT_POLICY, MEMORY_POLICY, policyVerdict } from './policy.mjs';
import { holderNameOf, RUN_WHAT, runIdOf, tooLongForUnit, unitPrefixOf } from './names.mjs';
import { startHolder } from './netns.mjs';
import { HOLD_MARGIN_S, launchPrefix, programArgv, requiredWallSeconds, runnerArgv } from './prefix.mjs';
import { progTemplate } from './scope.mjs';

export { launchPrefix, programArgv, runnerArgv };

/**
 * The ONE stable parent of run folders for callers that have none of their own: `$XDG_CACHE_HOME` or `~/.cache`, `dew_flow_bench/lang-bench-runs`, private to the caller. The startup sweep reads the
 * parent at the next run's start, so a caller MUST pass the same parent every run (a fresh temp folder per process is never swept: its killed run leaks until the host restarts).
 */
export function stableRunsParent() {
  const folder = path.join(process.env.XDG_CACHE_HOME || path.join(os.homedir(), '.cache'), 'dew_flow_bench', 'lang-bench-runs');
  mkdirSync(folder, { recursive: true, mode: 0o700 });
  return folder;
}

/** `ok(parent)` when the sweep can read it at the next run (absolute, an existing directory, the caller's, not writable by others), else `fail` naming the stable-parent rule. */
export function checkParent(parent) {
  const rule = 'the sweep reads the run folder\'s parent at every run\'s start: pass a stable one (stableRunsParent())';
  if (!path.isAbsolute(parent)) return fail(`the runs parent ${parent} is not absolute; ${rule}`);
  const stat = statSync(parent, { throwIfNoEntry: false });
  if (stat === undefined) return fail(`the runs parent ${parent} does not exist; ${rule}`);
  if (!stat.isDirectory()) return fail(`the runs parent ${parent} is not a directory; ${rule}`);
  if (stat.uid !== process.getuid()) return fail(`the runs parent ${parent} is not the caller's; ${rule}`);
  return (stat.mode & 0o022) === 0 ? ok(parent) : fail(`the runs parent ${parent} is writable by others (a shared temp root); ${rule}`);
}

const PINNED_IDENTITY = fileURLToPath(new URL('../../inputs/specs.d/exec-layer.json', import.meta.url));
export const IDENTITY_HALT = 'layer-identity-mismatch';

/** `{ treeSha256, matches }`: the live hash of the layer and whether it is the pinned identity (false when the file is missing or unreadable: no identity to check against). */
function identityVerdict(pinnedFile) {
  const live = computeIdentity(LAYER_DIR);
  const treeSha256 = live.ok ? live.value.execLayer.treeSha256 : null;
  try {
    return { treeSha256, matches: treeSha256 !== null && JSON.parse(readFileSync(pinnedFile, 'utf8')).execLayer.treeSha256 === treeSha256 };
  } catch {
    return { treeSha256, matches: false };
  }
}

const LIMIT_KEYS = Object.freeze(['timeS', 'readyS', 'graceS']);

/** `ok(limits)` read from the runner's limits file (`{}` when none is given), or `fail` naming a key the runner would refuse. Only whole or decimal numbers of seconds. */
function readLimits(file) {
  if (file === undefined) return ok({});
  try {
    const limits = JSON.parse(readFileSync(file, 'utf8'));
    const bad = LIMIT_KEYS.filter(key => key in limits && !(typeof limits[key] === 'number' && limits[key] > 0));
    return bad.length === 0 ? ok(limits) : fail(`the limits file ${file}: ${bad.join(', ')} must be a number of seconds`);
  } catch (error) {
    return fail(`the limits file ${file} cannot be read (${error.code ?? error.message})`);
  }
}

const SBIN_PATH = '/usr/sbin:/usr/bin:/bin:/sbin';
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
  return { owned, scope, dir, id, unitPrefix: unitPrefixOf(owned, id), holderName: holderNameOf(owned, id), options, tools, env: boxEnv({ dirs: options.dirs, extra: options.extraEnv, stack: options.stack, memoryMaxBytes: options.memoryMaxBytes }), holder: null, holderNet: null, limits: {} };
}

async function openSteps(run) {
  const { options } = run;
  if (!statSync(options.tree, { throwIfNoEntry: false })?.isDirectory()) return fail(`the built tree ${options.tree} is not a directory: the box mounts it read-only as the workspace`);
  const limits = readLimits(options.limitsFile);
  if (!limits.ok) return limits;
  const required = requiredWallSeconds(limits.value);
  if (options.caseWallMs !== undefined && options.caseWallMs / 1000 < required) return fail(`caseWallMs ${options.caseWallMs} is shorter than the time limit ${limits.value.timeS ?? 10} s plus the stop grace ${limits.value.graceS ?? 5} s the runner gives a case`);
  run.limits = limits.value;
  const long = tooLongForUnit(run.unitPrefix, options.caseIds);
  if (long.length > 0) return fail(`the unit name of case ${long.join(', ')} would pass 63 characters; use shorter case ids`);
  assertRunning('a holder');
  const holder = await (options.startHolder ?? startHolder)({ name: run.holderName, boundSeconds: options.runCeilingMs / 1000 + HOLD_MARGIN_S });
  if (!holder.ok) return fail(`the network-namespace holder did not start: ${holder.reason}`);
  Object.assign(run, { holder: holder.value, holderNet: readlinkSync(`/proc/${holder.value.pid}/ns/net`) });
  const views = makeViews({ runDir: run.dir, caseIds: options.caseIds });
  return views.ok ? ok(run) : views;
}

/**
 * `ok(run)`: the run folder (owned, claimed), the netns holder and the case views, or `fail(reason)` after the same cleanup that ends a run has removed whatever was made.
 * `options`: `{ owned, parent, tree, samplesDir, caseIds, dirs, memoryMaxBytes, caseWallMs, runCeilingMs, stack?, limitsFile?, extraEnv?, extraRo? }`; `stack` (`'csharp'` or `'node'`) selects the per-stack keys of the box environment.
 */
export async function openRun(options) {
  const parent = checkParent(options.parent);
  if (!parent.ok) return parent;
  let run = null;
  let opened;
  try {
    run = newRun(options, findTools());
    registerRun(run);
    const opening = openSteps(run);
    run.opening = opening.catch(() => null); // `cleanupLiveRuns` waits for it (V5)
    opened = await opening;
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
  const ran = await runBounded(argv[0], argv.slice(1), { timeoutMs: run.options.runCeilingMs, env: driverEnv({ dirs: run.options.dirs, tmp: run.dir }), onChild: trackChild });
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

const DATA_STACKS = Object.freeze(['csharp', 'node']);
export const HALT_STACK = 'stack-not-named';

/** A run that stopped before anything started: exit 3 naming the halt, no record, nothing to clean. */
const refusal = (stderr, halt) => ({ exitCode: EXIT_ENVIRONMENT, stdout: '', stderr, record: null, halt, memoryCertified: false, sweep: { retired: [], kept: [] }, cleanup: { evidence: {}, removed: [], left: [], clean: true } });

/**
 * One run through the layer: `{ exitCode, stdout, stderr, record, halt, memoryCertified, sweep, cleanup }`. `options` are those of `openRun` plus `data` (a data run: certification refuses, and so does a layer that is not the pinned identity, halt `layer-identity-mismatch`, `pinnedIdentityFile` to name another; and a memory policy other than the pinned one, halt `memory-view-mismatch`, `policyFile` to name another file), 
 * `minSweepAgeMs` (the startup sweep's age floor, default 10 minutes), `observe(run)` (called once the run is open and before anything starts) and `deps` (the seams of `certifyMemory`). `exitCode` is the runner's (0 passed, 1 a case failed, 3 environment, 4 configuration,
 * 5 the runner or the layer failed); `halt` is `'memory-not-certifiable'` for a data run that stopped before any case; `record` is the runner's `--json` or null. Never throws.
 */
export async function runCases(options) {
  const identity = identityVerdict(options.pinnedIdentityFile ?? PINNED_IDENTITY);
  if (options.data && !identity.matches) {
    return { ...refusal(`${IDENTITY_HALT}: the layer's tree ${identity.treeSha256} is not the pinned identity (${options.pinnedIdentityFile ?? PINNED_IDENTITY})`, IDENTITY_HALT), treeSha256: identity.treeSha256 };
  }
  const policy = options.data ? policyVerdict(options.policyFile) : { matches: true };
  if (!policy.matches) return { ...refusal(`${HALT_POLICY}: the memory policy is ${policy.found === null ? 'missing, undecided or unreadable' : `"${policy.found}"`}, the pinned one is "${MEMORY_POLICY}" (D55): a data run refuses another`, HALT_POLICY), treeSha256: identity.treeSha256 };
  if (options.data && !DATA_STACKS.includes(options.stack)) return { ...refusal(`${HALT_STACK}: a data run names its stack, 'csharp' or 'node' (got ${JSON.stringify(options.stack)}): the heap keys of D55 are the stack's, and a run without them is a run under no policy`, HALT_STACK), treeSha256: identity.treeSha256 };
  const result = await runCasesChecked(options);
  return { ...result, treeSha256: identity.treeSha256 };
}

async function runCasesChecked(options) {
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
