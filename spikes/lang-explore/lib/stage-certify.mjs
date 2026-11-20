// Two checks before a launch of Stage 1 (F3.S1 (a), PR 4; stories document section 5, brief items 2 and 3), both a value and never a throw:
//   memoryCertification  the layer's own probe (`certifyMemory`: an empty program through the real prefix, then `prog/memory.peak` must be readable) runs ONCE on this host BEFORE a data slot or a pilot or A/A launch is planned. A host
//                        without a readable `memory.peak` refuses with the named halt `memory-not-certifiable` (exit 3, resumable on a certifying host, T-L29, D36) and nothing is planned. A rehearsal goes on and carries
//                        the flag `memory bound not certified`. The certification at scoring (the layer opens a data run) stays the second line.
//   agentEnvVerdict      D15's environment (T-L14's half): the agent box's environment is the registered base (the .NET server GC off, an empty `NODE_OPTIONS`, the declared culture) and holds neither per-case heap key of D55
//                        (`DOTNET_GCHeapHardLimit`, Node's old-space flag); a clash or a planted heap key refuses the launch. The answer lists the keys of both boxes, for the record.
import { cleanupScope, ownedFor } from '../../../.agents/conventions/tools/lib/owned.mjs';
import { HALT, certifyMemory } from '../../lang-bench/lib/exec-layer/certify.mjs';
import { cleanup } from '../../lang-bench/lib/exec-layer/cleanup.mjs';
import { BASE_ENV, STACK_KEYS, allowFor } from '../../lang-bench/lib/exec-layer/env.mjs';
import { openRun, stableRunsParent } from '../../lang-bench/lib/exec-layer/run.mjs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { layerDirs, quickCalibration, quickExplore, quickPaths } from './quick.mjs';
import { OWNED_TOKEN } from './score-quick.mjs';

export const HALT_EXIT = 3;
export const CONFIG_EXIT = 4;
export const NOT_CERTIFIED_FLAG = 'memory bound not certified';
const PAID_KINDS = Object.freeze(['data', 'pilot', 'aa']);
const KINDS = Object.freeze([...PAID_KINDS, 'rehearsal']);

const LAUNCH_NAMES = Object.freeze({ data: 'data slot', pilot: 'pilot launch', aa: 'A/A launch' });
const halted = (halt, reason) => fail(reason, { halt, exit: HALT_EXIT });

/**
 * One layer run opened over `options` (`openRun`'s), certified (`certifyMemory`) and ended (`cleanup`), whatever happens: `{ launched, certified, peakBytes?, reason? }`. `open`, `release` and `certify` are seams for the tests; a run that cannot
 * be opened is `launched: false` with the layer's own sentence.
 */
export async function certifyOnHost({ options, open = openRun, release = cleanup, certify = certifyMemory }) {
  const opened = await open(options);
  if (!opened.ok) return { launched: false, certified: false, reason: `the certification run could not be opened: ${opened.reason}` };
  try {
    return await certify(opened.value);
  } finally {
    await release(opened.value);
  }
}

const PROBE_CASE_WALL_MS = 20_000;
const PROBE_CEILING_MS = 120_000;

/** The options of the certification probe on THIS host: an empty tree and no case (the probe launches an empty program only), the pinned toolchain folders and the registered memory value, in owned temporary folders of `scope`. */
function probeOptions({ explore, home, owned, scope }) {
  const quick = quickExplore(explore, home);
  return { owned, parent: stableRunsParent(), tree: owned.ownedDir(scope, 'probe-tree'), samplesDir: owned.ownedDir(scope, 'probe-samples'), caseIds: [], dirs: layerDirs(quickPaths(quick, home)), memoryMaxBytes: quickCalibration(quick).limits.memoryBytes, caseWallMs: PROBE_CASE_WALL_MS, runCeilingMs: PROBE_CEILING_MS };
}

/**
 * The real probe of `stage-run` on this host (`certifyOnHost` over the layer, the pinned toolchain folders and the registered memory value): `{ launched, certified, peakBytes?, reason? }`, never a throw (what cannot be set up is `launched: false` with its sentence).
 * `explore` is the pins (`inputs/explore.json`), `home` the operator's home.
 */
export async function hostProbe({ explore, home }) {
  const scope = cleanupScope();
  try {
    return await certifyOnHost({ options: probeOptions({ explore, home, owned: ownedFor({ token: OWNED_TOKEN }), scope }) });
  } catch (e) {
    return { launched: false, certified: false, reason: `the certification probe could not be set up: ${e.message}` };
  } finally {
    await scope.close();
  }
}

/** The verdict of one launch kind over the probe's answer (`probe()` -> `certifyMemory`'s `{ launched, certified, peakBytes?, reason? }`). */
function verdictOf({ kind, result }) {
  if (!result.launched) return halted('layer-launch-failed', `layer-launch-failed: the layer could not run its empty program on this host: ${result.reason}; nothing is planned`);
  if (result.certified) return ok({ flags: [], peakBytes: result.peakBytes });
  if (!PAID_KINDS.includes(kind)) return ok({ flags: [NOT_CERTIFIED_FLAG] });
  return halted(HALT, `${HALT}: ${result.reason}; this host cannot certify the memory bound, so no ${LAUNCH_NAMES[kind]} is planned (resumable on a host that certifies)`);
}

/**
 * `memoryCertification({ kind, probe })` -> `ok({ flags, peakBytes? })` | `fail(sentence, { halt, exit })`. `kind` is `data`, `pilot`, `aa` (paid: a host that cannot certify refuses, `halt: 'memory-not-certifiable'`, exit 3) or `rehearsal` (goes on with the
 * flag `memory bound not certified`); another kind is refused naming the legal ones (exit 4). The probe runs once, before anything is planned.
 */
export async function memoryCertification({ kind, probe }) {
  if (!KINDS.includes(kind)) return fail(`kind ${kind}: the launch kinds are ${KINDS.join(', ')}`, { exit: CONFIG_EXIT });
  return verdictOf({ kind, result: await probe() });
}

// ---- the agent box's environment ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

/** The keys of the registered base that the agent box also holds (D15): the same values for both stacks and every task. */
export const D15_KEYS = Object.freeze(['DOTNET_gcServer', 'NODE_OPTIONS', 'LANG', 'LC_ALL']);
const lower = key => key.toLowerCase();
const HEAP_NAMES = Object.freeze(Object.values(STACK_KEYS).flat().map(lower));
const OLD_SPACE = /--max-old-space-size/;

const baseProblem = (env, key) => {
  if (env[key] === undefined) return `${key} is absent from the agent box's environment (the registered base has ${JSON.stringify(BASE_ENV[key])})`;
  return env[key] === BASE_ENV[key] ? null : `${key} is ${JSON.stringify(env[key])} in the agent box's environment, the registered base is ${JSON.stringify(BASE_ENV[key])}`;
};

const heapProblems = env => Object.entries(env).filter(([key, value]) => HEAP_NAMES.includes(lower(key)) || (lower(key) === 'node_options' && OLD_SPACE.test(String(value))))
  .map(([key]) => `${key} is a per-case heap key (D55): the agent box holds none, only the scoring box of a stack does`);

/** `ok({ keys: { agent, scoring } })` | `fail(sentence, { problems, exit: 3 })`: the agent box's environment `env` is the registered base and holds no per-case heap key. The record lists the keys of both boxes. */
export function agentEnvVerdict({ env }) {
  const problems = [...D15_KEYS.map(key => baseProblem(env, key)).filter(Boolean), ...heapProblems(env)];
  if (problems.length > 0) return fail(`the agent box's environment is not the registered base (${problems.join('; ')})`, { problems, exit: HALT_EXIT });
  return ok({ keys: { agent: Object.keys(env).sort(), scoring: { csharp: allowFor('csharp'), node: allowFor('node') } } });
}
