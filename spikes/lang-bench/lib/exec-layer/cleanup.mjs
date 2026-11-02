// E1.S8b: ONE cleanup, called on every path (normal, timeout, exception), that never throws and leaves nothing of the case, in the delegated order (plan, "Cleanup"):
//   (1) the driver, if it still runs, as a whole process group (the runner's own SIGTERM step kills its launchers); (2) per scope (and again after the holder: the final confirmation), with the scope still present, the evidence is read live (`prog/`'s
//   `memory.peak`, `memory.events`, `cgroup.procs`) and then `prog/cgroup.kill`, then `holder/cgroup.kill` so the scope ends with the scope holder, `systemctl --user stop` only for a
//   scope that does not end; (3) the netns holder (SIGTERM, SIGKILL after 2 s); (4) the case views; (5) the run folder and its claim; then a confirmation that no process, scope, holder or
//   view is left. A resource it cannot remove stays named and reported in `left`, and the run folder stays claimed so the next sweep can judge it.
// The same function retires a KILLED run (`sweepStale`, the startup sweep): it rebuilds the run from its owned names alone, so a resource is found by name and by the run marker (the run
// folder's claim, whose process is gone), never by age. It touches only this user's `bench` names whose claim says `gone` (common.test-leftovers §3, §4), younger than `minAgeMs` left alone.
import { existsSync, lstatSync, readdirSync } from 'node:fs';
import path from 'node:path';
import { removeViews } from './case-view.mjs';
import { holderNameOf, runIdOf, unitPrefixOf } from './names.mjs';
import { endProcess, endProcessGroup, scanProcesses, sleep } from './proc.mjs';
import { holderCgroup, killCgroup, listScopes, progCgroup, readProg, scopeCgroup, stopUnit } from './scope.mjs';

const SCOPE_END_MS = 3000;
const POLL_MS = 25;
const TEN_MINUTES_MS = 600_000;

const entry = (kind, name, extra = {}) => ({ kind, name, ...extra });
const isDriverOf = dir => argv => argv.some(arg => arg.endsWith('run_samples.py')) && argv.some(arg => arg.includes(dir));

/** Run one step; an exception is a `left` entry, never a throw. */
async function guarded(kind, step) {
  try {
    return await step();
  } catch (error) {
    return { removed: [], left: [entry(kind, kind, { reason: error.code ?? error.message })] };
  }
}

async function endMatching(kind, match, graceMs, end = endProcess) {
  const result = { removed: [], left: [] };
  for (const identity of scanProcesses(match)) {
    const how = await end(identity, { graceMs });
    const reported = entry(kind, identity.argv0, { pid: identity.pid, how });
    if (how === 'stuck') result.left.push({ ...reported, reason: 'still there after SIGKILL' });
    else result.removed.push(reported);
  }
  return result;
}

const killLeaf = dir => { try { killCgroup(dir); } catch { /* the leaf is gone, or the stop below is the answer */ } };

async function waitAllGone(units) {
  for (let waited = 0; waited < SCOPE_END_MS; waited += POLL_MS) {
    if (units.every(unit => !existsSync(scopeCgroup(unit)))) return;
    await sleep(POLL_MS);
  }
}

async function endScopes(run, evidence) {
  const units = await listScopes(run.unitPrefix);
  for (const unit of units) {
    evidence[unit] = readProg(progCgroup(unit));
    killLeaf(progCgroup(unit));
    killLeaf(holderCgroup(unit));
  }
  await waitAllGone(units);
  for (const unit of await listScopes(run.unitPrefix)) await stopUnit(unit); // the last resort for a scope that does not end, and the reset of one that failed
  const still = new Set(await listScopes(run.unitPrefix));
  return {
    removed: units.filter(unit => !still.has(unit)).map(unit => entry('scope', unit)),
    left: units.filter(unit => still.has(unit)).map(unit => entry('scope', unit, { reason: 'still listed after systemctl stop' })),
  };
}

function dropViews(run) {
  const report = removeViews(run.dir);
  return { removed: report.removed.map(id => entry('view', id)), left: report.left.map(item => entry('view', path.basename(item.path), item)) };
}

async function dropRun(run) {
  await run.scope.close();
  return existsSync(run.dir) ? { removed: [], left: [entry('run', path.basename(run.dir), { reason: 'the run folder could not be removed; it stays claimed for the next sweep' })] } : { removed: [entry('run', path.basename(run.dir))], left: [] };
}

const liveRuns = new Set();

/** Remember an open run so a stopping process can end every run it started (V5): `cleanup` forgets it. */
export function registerRun(run) {
  liveRuns.add(run);
}

/** End every run this process opened and has not cleaned up yet (a SIGTERM mid-probe): the driver, the scopes, the holder, the views and the run folder of each. Never throws. */
export async function cleanupLiveRuns(options) {
  return Promise.all([...liveRuns].map(run => cleanup(run, options).catch(() => null)));
}

/**
 * End everything of `run` (`{ owned, dir, id, unitPrefix, holderName, scope }`): `{ evidence, removed, left, clean }`. `clean` is true when nothing is left. Idempotent: a second call finds nothing.
 * `evidence` is, per scope unit, what `prog/` said live before the kill.
 */
export async function cleanup(run, { graceMs = 2000 } = {}) {
  liveRuns.delete(run);
  const evidence = {};
  const steps = [
    ['driver', () => endMatching('driver', isDriverOf(run.dir), graceMs, endProcessGroup)],
    ['scope', () => endScopes(run, evidence)],
    ['holder', () => endMatching('holder', argv => argv[0] === run.holderName, graceMs)],
    ['scope', () => endScopes(run, {})], // the final confirmation: listed AGAIN after the holder step, so a scope registered while the driver was being ended is ended too
    ['view', async () => dropViews(run)],
    ['run', () => dropRun(run)],
  ];
  const parts = [];
  for (const [kind, step] of steps) parts.push(await guarded(kind, step));
  const left = parts.flatMap(part => part.left);
  return { evidence, removed: parts.flatMap(part => part.removed), left, clean: left.length === 0 };
}

/** A run rebuilt from its folder name alone: what the sweep needs to retire it. */
function runFromFolder(owned, dir) {
  const id = runIdOf(owned, dir);
  return { owned, dir, id, unitPrefix: unitPrefixOf(owned, id), holderName: holderNameOf(owned, id), scope: { close: async () => { owned.removeQuietly(dir); } } };
}

function stateOf(owned, dir) {
  return owned.ownerState(owned.readOwner(dir, 'folder'), undefined, { nameOwner: owned.classifyName(path.basename(dir)).owner, kernelMine: owned.isMine(dir) });
}

function whyKept(owned, dir, { minAgeMs, now }) {
  const state = stateOf(owned, dir);
  if (state !== 'gone') return state === 'live' ? 'its run is live' : `unmarked or unknown: the run marker does not say gone (${state})`;
  return now - lstatSync(dir).mtimeMs < minAgeMs ? `younger than ${Math.round(minAgeMs / 60_000)} minutes` : null;
}

/** This user's `lrun` folders of the `bench` token directly in `parent` (never a claim file), or [] when `parent` cannot be read. */
function runFolders(owned, parent) {
  try {
    return readdirSync(parent).filter(name => name.startsWith(owned.ownedPrefix('lrun')) && !name.endsWith('.owner'));
  } catch {
    return [];
  }
}

/** Why a run folder is NOT retired (a string), or null when it is: not a directory, not `gone`, or younger than `minAgeMs`. */
function whyNot(owned, dir, limits) {
  return lstatSync(dir).isDirectory() ? whyKept(owned, dir, limits) : 'not a directory';
}

/**
 * The startup sweep: retire the runs of an earlier layer process that was killed. Considers only this user's `lrun` folders of the `bench` token directly in `parent`; a run is retired when its
 * claim says `gone` (the pid is not a process any more) and it is older than `minAgeMs` (default 10 minutes; age selects, never authorises). Retires its scopes, holder, driver, views and
 * folder. `{ retired, kept }`: every retired resource by kind and name, every run kept with its reason. Never throws and never fails the run.
 */
export async function sweepStale({ owned, parent, minAgeMs = TEN_MINUTES_MS, now = Date.now() }) {
  const report = { retired: [], kept: [] };
  for (const name of runFolders(owned, parent)) {
    const dir = path.join(parent, name);
    const why = await guardedWhy(() => whyNot(owned, dir, { minAgeMs, now }));
    if (why !== null) {
      report.kept.push({ name, reason: why });
      continue;
    }
    const done = await cleanup(runFromFolder(owned, dir));
    report.retired.push(...done.removed);
    report.kept.push(...done.left.map(item => ({ name, reason: `${item.kind} ${item.name}: ${item.reason}` })));
  }
  return report;
}

/** `why`, or a reason naming the exception: one folder that cannot be judged never ends the sweep. */
async function guardedWhy(judge) {
  try {
    return judge();
  } catch (error) {
    return `could not be judged: ${error.code ?? error.message}`;
  }
}
