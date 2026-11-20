// The checks of ONE launch of Stage 1 that sit between PR 3's refusals and the plan, and the clear held-out folder around the run (F3.S1 (a), PR 4; stories document section 5, brief items 2, 3, 4 and 6). They are steps of `stage-step.mjs`, in this order,
// after the guard, the task pins and the pinned price file and policy, and before the plan, so each refusal leaves the run-map and the ledger as they were (the disk halt alone adds its own `disk-rule` / `disk-reserve` event line, an event and never a charge):
//   envStep       D15's environment: the agent box's environment is the registered base and holds neither per-case heap key (`agentEnvVerdict`); a clash or a planted heap key refuses the launch (exit 3);
//   certifyStep   memory certification: the layer's own probe runs ONCE on this host before the launch is planned (`memoryCertification`): a paid launch on a host that cannot certify halts `memory-not-certifiable` (exit 3, resumable on a
//                 certifying host); a rehearsal goes on and carries the flag `memory bound not certified`;
//   diskStep      the reserve and the per-pair rule before every PAID launch (`diskGate`), with its halt line in the ledger (exit 3, resumable once the space is there); a rehearsal is free and holds no reserve.
// `underHeldout` wraps the run of a PAID unit in the clear held-out folder of the unit's own task (`withSlotHeldout`): made for the unit, removed after it, a unit refused before its launch when the key's backup cannot be verified.
// The seams of the tests and of PR 5 are `io.stage`: `agentEnvFn`, `probeFn`, `growthFn`, `floorBytes`, `freeBytesFn`, `slotHeldoutFn`.
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { agentEnv, dummyToken, layoutPaths } from '../../minigit-bench/lib/sandbox.mjs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { systemClock } from './clock.mjs';
import { loadGrowth } from './growth.mjs';
import { REPO } from './paths.mjs';
import { isRehearsal, ledgerFileOf } from './series.mjs';
import { agentEnvVerdict, hostProbe, memoryCertification } from './stage-certify.mjs';
import { diskGate, freeBytesOf } from './stage-disk.mjs';
import { withSlotHeldout } from './stage-heldout.mjs';
import { PREREG_DIR } from './stage-record.mjs';
import { resolveTask } from './tasks.mjs';
import { FREEZE_TASKS } from './workspaces-sources.mjs';

const HALT_EXIT = 3;
const CONFIG_EXIT = 4;
const seam = (io, name, fallback) => io.stage?.[name] ?? fallback;

// ---- D15's environment -----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

/** The environment the agent box is launched with (`agentEnv`, as `session-launch.mjs` builds it): the pins' `envExtra` and culture over the base. The paths in it are placeholders: only its keys and the registered values are judged. */
const agentBoxEnv = explore => agentEnv({ layout: layoutPaths('/stage-env-check', 'check'), dummyToken: dummyToken('check'), cellId: 'check', nodeDir: '/node', locale: explore.locale, extra: explore.envExtra });

/** `ok(env)` | `fail` for an environment that cannot even be built (an extra that clashes with a base key). */
function envOf({ explore, io }) {
  try {
    return ok(seam(io, 'agentEnvFn', agentBoxEnv)(explore));
  } catch (e) {
    return fail(`the agent box's environment cannot be built: ${e.message}`, { exit: CONFIG_EXIT });
  }
}

/** The step: the agent box's environment is the registered base and holds no per-case heap key. */
export function envStep({ explore, io }) {
  const env = envOf({ explore, io });
  if (!env.ok) return env;
  const verdict = agentEnvVerdict({ env: env.value });
  return verdict.ok ? ok({ boxKeys: verdict.value.keys }) : verdict;
}

// ---- memory certification --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

/** The kind `memoryCertification` judges: a rehearsal's own, else the unit's (`data`, `pilot`, `aa`). */
const kindOf = ({ series, unit }) => (isRehearsal(series.dir) ? 'rehearsal' : unit.kind);

/** The step: the layer's probe runs once on this host before the launch is planned. A flag (a rehearsal on a host that cannot certify) is said and kept in the state as `flags`. */
export async function certifyStep({ series, unit, explore, io }) {
  const probe = seam(io, 'probeFn', () => hostProbe({ explore, home: io.home }));
  const verdict = await memoryCertification({ kind: kindOf({ series, unit }), probe });
  if (!verdict.ok) return fail(verdict.reason, { halt: verdict.halt, exit: verdict.exit });
  verdict.value.flags.forEach(flag => io.say(flag));
  return ok({ flags: verdict.value.flags });
}

// ---- the disk --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

/** The registered size of a session folder: the seam, else `sessionFloorBytes` of the pre-registration's manifest (PR 5's file; absent is no size, and the gate refuses a missing size). */
function floorOf({ io }) {
  if (io.stage?.floorBytes !== undefined) return io.stage.floorBytes;
  try {
    return JSON.parse(readFileSync(path.join(REPO, PREREG_DIR, 'manifest.json'), 'utf8')).sessionFloorBytes;
  } catch {
    return undefined;
  }
}

/** The free bytes under the series folder, or `null` when they cannot be read (the gate refuses `null`, never passes on it). */
function freeOf({ series, io }) {
  try {
    return seam(io, 'freeBytesFn', freeBytesOf)(series.dir);
  } catch {
    return null;
  }
}

/** The slot the halt line names: a data slot's number, else the pilot step. */
const slotOf = unit => (unit.kind === 'data' ? unit.pair : unit.what);

const gateArgs = ({ series, map, unit, growth, io }) => ({
  series: series.dir, map, pilot: series.state.pilot, growth, floorBytes: floorOf({ io }), freeBytes: freeOf({ series, io }), slot: slotOf(unit), extraCells: unit.extraCells ?? 0, ledgerFile: ledgerFileOf(series.dir), now: io.now ?? systemClock,
});

/** The step (PAID launches only): the reserve over everything still to run and the rule over this pair. `map` is the run-map as the schedule reads it. A halt is exit 3 with its event line in the ledger. */
export function diskStep({ series, map, unit, io }) {
  if (isRehearsal(series.dir)) return ok({});
  const growth = seam(io, 'growthFn', loadGrowth)();
  if (!growth.ok) return fail(`the disk check cannot be made: ${growth.reason}`, { exit: HALT_EXIT });
  const verdict = diskGate(gateArgs({ series, map, unit, growth: growth.value, io }));
  return verdict.go ? ok({}) : fail(`${verdict.halt}: ${verdict.reason}`, { halt: verdict.halt, exit: verdict.exit });
}

// ---- the clear held-out folder -----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

const tasksOf = explore => FREEZE_TASKS.map(id => resolveTask(explore, id).value);

/** The end of a unit whose folder could not be closed: the unit ran, so its own result stays and the series halts (exit 3 when it had ended well). */
const closedLate = ({ result, reason }) => ({ ...result, exit: result.exit === 0 ? HALT_EXIT : result.exit, message: `${result.message}; ${reason}` });

const heldoutEnd = refusal => (refusal.result === undefined ? { exit: CONFIG_EXIT, message: refusal.reason } : closedLate(refusal));

/**
 * `fn()` (the unit's run) inside the clear held-out folder of the unit's own task: a PAID unit only (a rehearsal's held-out cases are fixtures). A unit refused before its launch (the key's backup cannot be verified, another task is in clear, a
 * folder is held) is `{ exit: 4, message }` and `fn` did not run. -> what `fn` returns, or that refusal.
 */
export async function underHeldout({ series, task, explore, io, fn }) {
  if (isRehearsal(series.dir)) return fn();
  const ran = await seam(io, 'slotHeldoutFn', withSlotHeldout)({ task, tasks: tasksOf(explore), heldout: { home: io.home, ...io.heldoutArgs }, fn });
  return ran.ok ? ran.value : heldoutEnd(ran);
}
