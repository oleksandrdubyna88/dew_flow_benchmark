// `explore.mjs package` and `explore.mjs register-memory` (F2.S1). FREE: nothing here launches a session or spends; it re-executes under `taskset -c 6,7` (`REEXEC`), holds the plan lock of the explore root (the series lock `run` takes,
// so no session and no other scoring runs beside it; a held lock is a resumable halt, exit 3, naming the holder), checks that the machine is quiet (a package validation measures peaks and walls) and works on the clear held-out folder
// of a sealed task through `withOpenedHeldout` (made from the archive, removed after).
//
//   node spikes/lang-explore/explore.mjs package --task NN [--only refs|witnesses|suite|load|all] [--register] [--seal] [--accept-noisy <why>] [--task-repo <task repository>]
//   node spikes/lang-explore/explore.mjs register-memory                     writes the memory value's `registration` field once all six packages have both references accepted at it (P9; nothing else writes it)
//
// `--only suite` counts S and records it apart from registration. `--task-repo` runs the conformance checker over that checkout (the checker lives in lang-bench; `register` needs its green line). `--register` writes `inputs/packages.json`
// after every item is green; `--seal` (after the registration) seals the archive; `--seal` alone seals a package whose latest record is green and current. The record of a run is `package-NN/<run>/record.json`, private.
// Exit 0 green | 1 an item red | 3 a resumable halt (the lock, the machine not quiet, an environment halt) | 4 usage or a refusal | 5 a failure.
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { cleanupScope } from '../../../.agents/conventions/tools/lib/owned.mjs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { systemClock } from './clock.mjs';
import { heldoutAware } from './heldout-commands.mjs';
import { withOpenedHeldout } from './heldout-lifecycle.mjs';
import { machineFor, pilotQuiet } from './machine.mjs';
import { registerMemoryValue } from './memory-limit.mjs';
import { exploreRoot } from './paths.mjs';
import { runConformance } from './package-gate.mjs';
import { finishPackage } from './package-finish.mjs';
import { materialOf, loadPackage, packageIdentity } from './package-quick.mjs';
import { PACKAGES_FILE, pinsNow } from './package-registry.mjs';
import { latestRecord, openRun, sweepOwnedTrees, writeHalt, writeRecord } from './package-record.mjs';
import { loadStep, refsStep, suiteStep, witnessesStep } from './package-steps.mjs';
import { acquirePlanLock } from './plan-lock.mjs';
import { quickCalibration, isQuick } from './quick.mjs';
import { taskFromValues, taskPaths } from './tasks.mjs';
import { loadWorld } from './world.mjs';

const EXIT = Object.freeze({ done: 0, red: 1, halt: 3, config: 4, failed: 5 });
export const ONLY = Object.freeze(['refs', 'witnesses', 'suite', 'load', 'all']);
const STEPS_OF = Object.freeze({ suite: ['suite'], refs: ['refs'], load: ['refs', 'load'], witnesses: ['refs', 'witnesses'], all: ['suite', 'refs', 'load', 'witnesses'] });
const refused = (io, reason) => { io.say(reason); return EXIT.config; };
const maybe = (holds, part) => (holds ? part : {});

/** The arguments of `package`: `ok({ explore, task, only, register, seal })` | `fail(sentence)`. */
function argsOf(values, io) {
  const given = taskFromValues(values, io.exploreFn);
  if (!given.ok) return given;
  if (!isQuick(given.value.task)) return fail(`task ${given.value.task.id} is not a quick-run task: only the quick tasks have a package`);
  const only = values.only ?? 'all';
  if (!ONLY.includes(only)) return fail(`--only is one of ${ONLY.join(', ')} (got '${only}')`);
  return ok({ ...given.value, only, register: values.register === true, seal: values.seal === true, taskRepo: values['task-repo'] ?? null, acceptNoisy: values['accept-noisy'] ?? null });
}

/** The quiet check, as `measure-growth.mjs` runs it: a package validation is a measurement of the references' peaks and walls. -> `ok` | `fail(reason)`. */
async function quietCheck({ io, acceptNoisy, signal }) {
  if (io.quietFn) return io.quietFn({ acceptNoisy, signal });
  const thresholds = pilotQuiet();
  const spin = JSON.parse(readFileSync(path.join(exploreRoot(io.home), 'spin.json'), 'utf8'));
  const machine = machineFor({ thresholds: thresholds.value, calibratedMs: spin.medianMs, seriesDir: exploreRoot(io.home), acceptNoisy, home: io.home, ...(io.machineSeams ?? {}) });
  const r = await machine.quiet({ cellIds: [], say: io.say, signal });
  return r.ok ? ok(r.value) : fail(r.reason);
}

/** The needles file of a task's conformance run (`io.package.needlesFile`, `null` for none, else the private default). */
const needlesOf = ({ args, io }) => (io.package?.needlesFile === undefined ? path.join(path.dirname(taskPaths(args.task, io.home).notesFile), `needles-${args.task.id}.txt`) : io.package.needlesFile);

/** The conformance checker over `--task-repo`, or `null` (not run). */
async function conformanceOf({ args, io }) {
  if (io.package?.conformance !== undefined) return io.package.conformance;
  if (args.taskRepo === null) return null;
  const needles = needlesOf({ args, io });
  return runConformance({ repo: args.taskRepo, slug: path.basename(args.task.spec.path, '.md'), format: path.join(args.taskRepo, 'todo', 'tasks', 'FORMAT.md'), ...maybe(needles !== null, { needles }) });
}

const DETAIL = Object.freeze({
  suite: i => `${i.counts.problems} problem(s), ${i.counts.cases} case(s), S ${i.sBytes} bytes`,
  refs: i => ['csharp', 'node'].map(s => i[s].sentence).join('; '),
  load: i => i.sentence ?? (i.problems?.[0] ?? `slowest ${i.slowestWallMs} ms, twin ${i.twinWallMs} ms, duration ${i.durationS} s`),
  witnesses: i => `${i.caught} of ${i.witnesses} caught${i.notCaught?.length ? `: ${i.notCaught.join('; ')}` : ''}`,
  gate: i => i.sentence,
});

const itemLine = (name, item) => `${name}: ${item.ok ? 'ok' : 'RED'} - ${DETAIL[name](item)}`;

/** The merged items: what this run did over what the newest complete record of the same material already had. */
function mergedItems({ prev, material, now }) {
  const carried = prev !== null && prev.materialSha === material ? prev.items : {};
  return { ...carried, ...now };
}

const REFS_FIRST = 'the references are not accepted: the load measurement and the witnesses need both accepted, so they did not run';
const blockedBy = (name, out) => ['load', 'witnesses'].includes(name) && out.items.refs?.ok === false;
const wrapItem = (name, r) => (r.halted ? r : { items: { [name]: r.item } });

/** What runs each step, over `{ c, out, conformance }`: -> `{ items, results? }` | `{ halted }`. */
const RUNNERS = Object.freeze({
  suite: ({ c, conformance }) => ({ items: suiteStep({ pkg: c.pkg, home: c.home, task: c.task, conformance, now: c.now, recordFile: c.gateRecordFile, samplesDir: c.samplesDir }).item }),
  refs: async ({ c }) => { const r = await refsStep(c); return r.halted ? r : { items: { refs: r.item }, results: r.results }; },
  load: async ({ c, out }) => wrapItem('load', await loadStep(c, out.results)),
  witnesses: async ({ c }) => wrapItem('witnesses', await witnessesStep(c)),
});

/** The steps of `only`, in order, over the run's context. -> `{ items, results, halted?, stopped? }`. */
async function runSteps({ args, c, conformance }) {
  const out = { items: {}, results: {} };
  for (const name of STEPS_OF[args.only]) {
    if (blockedBy(name, out)) return { ...out, stopped: REFS_FIRST };
    const r = await RUNNERS[name]({ c, out, conformance });
    if (r.halted) return { ...out, halted: r.halted, detail: r.detail };
    out.items = { ...out.items, ...r.items };
    out.results = r.results ?? out.results;
  }
  return out;
}

/** The context every step reads. */
/** The visible samples of the task (`tasks/<slug>/samples` of `--task-repo`), or `null`: S counts them too. */
const samplesOf = ({ io, task, taskRepo }) => io.package?.samplesDir ?? (taskRepo === null ? null : path.join(taskRepo, 'tasks', path.basename(task.spec.path, '.md'), 'samples'));

function contextOf({ io, world, pkg, run, scope, task, taskRepo }) {
  const limits = world.limits ?? quickCalibration(world.explore).limits;
  return { world, pkg, run, scope, parent: io.package?.parent, limits, memoryBytes: limits.memoryBytes, say: io.say, signal: io.signal, seriesDir: exploreRoot(io.home), now: io.now ?? systemClock, home: io.home, task, heartbeatMs: io.package?.heartbeatMs, gateRecordFile: io.package?.gateRecordFile, samplesDir: samplesOf({ io, task, taskRepo }) };
}

/** The quiet check of a step that measures (`--only suite` measures nothing): `ok` | `fail(reason)`. */
const quietOf = ({ args, io }) => (args.only === 'suite' ? ok(null) : quietCheck({ io, acceptNoisy: args.acceptNoisy, signal: io.signal }));

/** The run folder (a halted run of the same material is resumed), with the lines that say so. */
function runFolder({ args, io, material }) {
  const run = openRun({ home: io.home, task: args.task, materialSha: material, now: io.now ?? systemClock, ...maybe(io.genId !== undefined, { genId: io.genId }) }).value;
  if (run.resumed) io.say(`resuming the run ${run.name}: the finished steps are read, not run again`);
  run.incomplete.forEach(name => io.say(`the run ${name} has no record: it is incomplete and is not read as a result`));
  return run;
}

/** The sweep at the start (an owned tree of a dead run), the quiet check and the run folder. -> `{ run }` | `{ exit }`. */
async function opened({ args, io, material }) {
  const swept = sweepOwnedTrees({ parent: io.package?.parent });
  swept.removed.forEach(() => io.say('removed a temporary tree of a run that is gone'));
  const quiet = await quietOf({ args, io });
  if (!quiet.ok) { io.say(`the machine is not quiet: ${quiet.reason}`); return { exit: EXIT.halt }; }
  return { run: runFolder({ args, io, material }) };
}


/** The record of a complete run: the material, the pins, the items and the numbers the registry copies. */
function recordOf({ pkg, run, pins, material, items, started, ended, memoryBytes, results }) {
  const both = results.csharp !== undefined && results.node !== undefined;
  return {
    task: pkg.task.id, run: run.name, materialSha: material, startedAt: started.toISOString(), endedAt: ended.toISOString(), resumed: run.resumed, incompleteRuns: run.incomplete, material: materialOf(pkg), pins, memoryBytes, items, s: items.suite?.sBytes ?? null,
    ...maybe(both, { referenceVerdicts: { csharp: results.csharp?.verdict, node: results.node?.verdict } }),
    ...maybe(items.load !== undefined, { load: { slowestWallMs: items.load?.slowestWallMs ?? null, twinWallMs: items.load?.twinWallMs ?? null, durationS: items.load?.durationS ?? null } }),
    ...maybe(items.gate?.gate !== undefined, { gate: items.gate?.gate }),
  };
}

/** The line that ends a run: green, red with the names, or incomplete. -> `{ green, line }`. */
function summaryOf({ done, task, run }) {
  const red = Object.entries(done.items).filter(([, i]) => i.ok !== true).map(([n]) => n);
  const green = red.length === 0 && !done.stopped;
  const how = red.length > 0 ? `red: ${red.join(', ')}` : 'incomplete';
  return { green, line: green ? `package of task ${task.id}: the items run are green (record ${run.name})` : `package of task ${task.id}: ${how} (record ${run.name})` };
}

/** The record of a run that went to its end, written once, and the lines said. -> `{ exit, record, pkg }`. */
function finished({ args, io, pkg, pins, material, run, c, started, done }) {
  Object.entries(done.items).forEach(([name, item]) => io.say(itemLine(name, item)));
  if (done.stopped) io.say(done.stopped);
  const items = mergedItems({ prev: latestRecord({ home: io.home, task: args.task }), material, now: done.items });
  const record = recordOf({ pkg, run, pins, material, items, started, ended: c.now(), memoryBytes: c.memoryBytes, results: done.results });
  writeRecord(run, record);
  const summary = summaryOf({ done, task: args.task, run });
  io.say(summary.line);
  return { exit: summary.green ? EXIT.done : EXIT.red, record, pkg };
}

/** The steps run, then the record and the lines (or the halt). -> `{ exit, record, pkg }`. */
async function executed({ args, io, world, pkg, pins, material, scope, run }) {
  const c = contextOf({ io, world, pkg, run, scope, task: args.task, taskRepo: args.taskRepo });
  const started = c.now();
  const done = await runSteps({ args, c, conformance: await conformanceOf({ args, io }) });
  if (!done.halted) return finished({ args, io, pkg, pins, material, run, c, started, done });
  writeHalt(run, { reason: done.halted, detail: done.detail ?? done.halted, materialSha: material });
  io.say(`halted (resumable): ${done.halted}`);
  return { exit: EXIT.halt };
}

/** The run inside the plan lock: the sweep, the quiet check, the run folder, the steps. */
async function inLock(a) {
  const scope = cleanupScope();
  try {
    const o = await opened(a);
    return o.exit === undefined ? await executed({ ...a, scope, run: o.run }) : o;
  } finally {
    await scope.close();
  }
}

/** The whole run inside the open clear folder: -> `{ exit, record?, pkg }`. */
async function runPackage({ args, io, world }) {
  const loaded = loadPackage({ home: io.home, task: args.task, world, frozenFile: io.frozenFile });
  if (!loaded.ok) return { exit: refused(io, loaded.reason) };
  const pins = (io.package?.pinsFn ?? pinsNow)();
  const held = acquirePlanLock({ home: io.home });
  if (!held.ok) { io.say(held.reason); return { exit: EXIT.halt }; }
  try {
    return await inLock({ args, io, world, pkg: loaded.value, pins, material: packageIdentity({ ...materialOf(loaded.value), pins }) });
  } finally {
    held.value.release();
  }
}

/** `--seal` alone: the latest complete record of the same material, no step run. */
function sealOnlyRun({ args, io, world }) {
  const loaded = loadPackage({ home: io.home, task: args.task, world, frozenFile: io.frozenFile });
  if (!loaded.ok) return { exit: refused(io, loaded.reason) };
  const record = latestRecord({ home: io.home, task: args.task });
  return record === null ? { exit: refused(io, `task ${args.task.id} has no complete record: run \`package --task ${args.task.id} --register\` first`) } : { exit: EXIT.done, record, pkg: loaded.value };
}

const worldOf = ({ io, task }) => (io.worldFn ?? loadWorld)({ home: io.home, work: path.join(exploreRoot(io.home), `package-${task.id}`), task });

/** The run of a command: `--seal` alone reads the latest record (a seal that was interrupted leaves an archive with no entry, which every reader refuses, so `--seal` is the one road out and does not go through the reader's wrapper); every other run works inside the opened clear folder. */
function runOf({ args, io, world }) {
  if (args.seal && !args.register) return sealOnlyRun({ args, io, world });
  return withOpenedHeldout({ ...(io.heldoutArgs ?? {}), home: io.home, task: args.task, fn: () => runPackage({ args, io, world }) });
}

/** The arguments that cannot go together. */
const clash = (args, values) => (args.seal && !args.register && values.only !== undefined ? '--seal alone seals the package of its latest record; it takes no --only' : null);

/** The arguments and the world of a command: `ok({ args, world })` | `fail`. */
async function prepared(values, io) {
  const args = argsOf(values, io);
  if (!args.ok) return args;
  const wrong = clash(args.value, values);
  if (wrong) return fail(wrong);
  const world = await worldOf({ io, task: args.value.task });
  return world.ok ? ok({ args: args.value, world: world.value }) : world;
}

/** The registration and the seal after a green run that asked for them; else the run's exit code. */
const afterRun = ({ args, io, result }) => (result.exit === EXIT.done && (args.register || args.seal) ? finishPackage({ args, io, explore: args.explore, task: args.task, record: result.record, pkg: result.pkg }) : result.exit);

/** `package --task NN [...]`. */
async function packageCommand(values, io) {
  const given = await prepared(values, io);
  if (!given.ok) return refused(io, given.reason);
  const { args, world } = given.value;
  const result = await runOf({ args, io, world });
  return result.exit === undefined ? refused(io, result.reason) : afterRun({ args, io, result });
}

export const cmdPackage = heldoutAware(packageCommand);

/** `packages.json` as an object, or `null` when it cannot be read. */
function readPackages(io) {
  try { return JSON.parse(readFileSync(io.package?.packagesFile ?? PACKAGES_FILE, 'utf8')); } catch { return null; }
}

/** The registration, said: -> the exit code. */
function registeredMemory({ packages, io }) {
  const r = registerMemoryValue({ packages, ...maybe(io.package?.memoryFile !== undefined, { file: io.package?.memoryFile }), now: io.now ?? systemClock });
  io.say(r.ok ? `memory value: ${r.value}` : r.reason);
  return r.ok ? EXIT.done : EXIT.config;
}

/** `register-memory`: the registration field of the memory value, once all six packages have both references accepted at it. */
export function cmdRegisterMemory(values, io) {
  const packages = readPackages(io);
  return packages === null ? refused(io, 'packages.json cannot be read: there is no package to register the value from') : registeredMemory({ packages, io });
}
