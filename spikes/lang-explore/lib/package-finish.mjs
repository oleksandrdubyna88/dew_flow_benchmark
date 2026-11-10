// The end of `package --register [--seal]` (F2.S1, the plan round's finding 5: THE SEAL IS LAST). The order is fixed: validate everything (a red item stops before registration and nothing is sealed), register (`package-registry.mjs`), then
// seal (`package-seal.mjs`, bench PR 4a: it publishes the archive first, writes the registry entry next and removes the clear cases only after both and a verified backup; a failed seal leaves the clear cases exactly as they were and `package
// --task NN --seal` resumes it). Registration precedes the seal, so the archive's hash joins the entry afterwards: the same function writes the entry again with the sealed archive's hash, which is a new identity (a seal is part of the identity).
import path from 'node:path';
import { systemClock } from './clock.mjs';
import { resolveTask, taskIds, taskPaths } from './tasks.mjs';
import { isQuick } from './quick.mjs';
import { PACKAGES_FILE, registerPackage, registrationProblem } from './package-registry.mjs';
import { sealPackage } from './package-seal.mjs';

const EXIT = Object.freeze({ done: 0, red: 1, halt: 3, config: 4, failed: 5 });
const quickTasks = explore => taskIds(explore).map(id => resolveTask(explore, id).value).filter(isQuick);

/** The registry seams of a run: the files and the pins (`io.package` and `io.frozenFile` are for tests); an absent one is the committed default. */
const registryOf = io => Object.fromEntries(Object.entries({ frozenFile: io.package?.frozenFile ?? io.frozenFile, packagesFile: io.package?.packagesFile, pinsFn: io.package?.pinsFn }).filter(([, v]) => v !== undefined));

/** Register the package: -> `ok({ state, identity })` | `fail`. */
export function registerStep({ io, explore, task, record, pkg }) {
  return registerPackage({ home: io.home, task, tasks: quickTasks(explore), record, pkg, now: io.now ?? systemClock, ...registryOf(io) });
}

/** Seal the clear folder of the task, the seal's own refusals and a stop in the middle of it said as a sentence (a stop is exit 5: the archive and the entry are each whole or absent, and a run of `--seal` resumes). -> `{ exit, state?, say }`. */
async function sealStep({ io, task }) {
  try {
    const r = await sealPackage({ ...(io.heldoutArgs ?? {}), home: io.home, task, manifestFile: path.join(taskPaths(task, io.home).heldoutDir, 'manifest.json') });
    return r.ok ? { exit: EXIT.done, state: r.value.state } : { exit: EXIT.config, say: `the seal of task ${task.id} stopped: ${r.reason}; the clear cases are as they were` };
  } catch (e) {
    return { exit: EXIT.failed, say: `the seal of task ${task.id} was interrupted (${e?.message ?? e}): the clear cases are as they were; run \`explore.mjs package --task ${task.id} --seal\` to resume it` };
  }
}

/** The registration step: -> `{ exit }` (a refusal) or `{ state }`. */
function registered({ io, explore, task, record, pkg }) {
  const r = registerStep({ io, explore, task, record, pkg });
  if (!r.ok) io.say(`not registered, not sealed: ${r.reason}`);
  else io.say(`registered: the package of task ${task.id} (${r.value.state}), identity ${r.value.identity.slice(0, 12)}`);
  return r.ok ? { state: r.value.state } : { exit: EXIT.config };
}

/** The seal step and the entry's archive hash after it: -> the exit code. */
async function sealedAfter({ io, explore, task, record, pkg }) {
  const sealed = await sealStep({ io, task });
  if (sealed.exit !== EXIT.done) { io.say(sealed.say); return sealed.exit; }
  io.say(`sealed: the archive of task ${task.id} is ${sealed.state}`);
  const synced = registerStep({ io, explore, task, record, pkg });
  io.say(synced.ok ? `the archive's hash is in the entry of task ${task.id} (${synced.value.state})` : `the archive's hash could not be added to the entry: ${synced.reason}`);
  return synced.ok ? EXIT.done : EXIT.config;
}

/**
 * Register and seal, in that order. `args` = `{ register, seal }`; `record` a complete run's record, `pkg` `loadPackage`'s value now. A package that cannot register (an item red or not run, the record older than the material, the pins moved) is not
 * sealed. -> the exit code; every line goes through `io.say`.
 */
export async function finishPackage(a) {
  const { args, io, explore, task, record, pkg } = a;
  const problem = registrationProblem({ record, pkg, task, home: io.home, tasks: quickTasks(explore), ...registryOf(io) });
  if (problem !== null) { io.say(`not registered, not sealed: ${problem}`); return EXIT.config; }
  const done = args.register ? registered(a) : {};
  return done.exit ?? (args.seal ? sealedAfter(a) : EXIT.done);
}
