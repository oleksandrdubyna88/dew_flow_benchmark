// The run context and the edges of one loop run (v1's `runLoop` over the pilot's sessions: kind `pilot`, or `rehearsal`). The context is the fixed configuration (`ctx`) and `deps` its edges, as v1's `bare-config.mjs`
// builds them for the bare runner, with the pilot's differences: its own fill and plan (seam W1), no spend total (the caps are the guard), no registered seed or prereg, the machine of `lib/machine.mjs`, and the
// judge of `lib/score.mjs` for the scoring step. The real token goes only into `ctx.token`; a rehearsal hands a fake one and a stub CLI.
import { existsSync, mkdirSync, readFileSync, readdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { killTree } from '../../policy-ab-quick/lib/proc.mjs';
import { crosscheckPolicyFrom } from '../../minigit-bench/capture/policy-input.mjs';
import { aliasStore } from '../../minigit-bench/lib/alias-store.mjs';
import { endCell, realProcApi } from '../../minigit-bench/lib/janitor.mjs';
import { preflight } from '../../minigit-bench/lib/preflight.mjs';
import { harnessIdentity } from '../../minigit-bench/lib/prereg-guard.mjs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { retireStale } from '../../minigit-bench/lib/session-state.mjs';
import { probeMask } from './affinity.mjs';
import { systemClock } from './clock.mjs';
import { maskReadBack } from './mask-preflight.mjs';
import { scorePasses } from './passes.mjs';
import { ledgerFileOf, runMapFileOf } from './series.mjs';
import { fillWorkspace, fillerFor } from './workspace.mjs';
import { loadPrompts, planForSession } from './plan.mjs';
import { recordPlan } from './readback.mjs';
import { stacksOf } from './inputs.mjs';
import { machineFor, pilotQuiet } from './machine.mjs';
import { INPUTS, REPO, V1, spinFile } from './paths.mjs';
import { DISTRIBUTION_DOTNET } from './quick.mjs';

const readJson = file => JSON.parse(readFileSync(file, 'utf8'));
/** A spend ceiling no session reaches: the pilot has no total (the per-session cap is the guard), and v1's spend gate asks for one. */
const NO_TOTAL_USD = 1e9;

/** The registered snapshots' package lists and bytes, for the record's `packages` and `egress.cacheGrowthBytes`. */
function snapshotFacts(snapshots) {
  const records = [snapshots.csharp.record, snapshots.node.record];
  return { snapshotPackages: records.flatMap(r => r.packages ?? []), snapshotBytes: records.reduce((n, r) => n + (r.sizeBytes ?? 0), 0) };
}

/**
 * The fixed configuration of a loop run. `world` is `loadWorld`'s value; `kind` is `pilot` or `rehearsal`; `fill` the workspace filler (the pilot's frozen workspace, or the rehearsal's reference tree).
 * -> `ok(ctx)` | `fail`.
 */
export async function runContext(a) {
  const policy = crosscheckPolicyFrom(V1, {});
  if (!policy.ok) return policy;
  const identity = await harnessIdentity(REPO);
  return identity.ok ? ok(contextOf({ ...a, policy: policy.value, identity: identity.value })) : identity;
}

const readSpin = home => (existsSync(spinFile(home)) ? readJson(spinFile(home)) : { medianMs: 1 });
const snapshotsOfWorld = world => Object.fromEntries(['csharp', 'node'].map(s => [s, { dir: world.snapshots[s].dir, parts: world.snapshots[s].parts }]));

/** The context object, from the pieces `runContext` gathered. */
export function contextOf({ world, calibration, series, seriesDir, kind, pair, token, claudeBinary, fill, planOver = {}, abort, home, policy, identity, prompts = loadPrompts() }) {
  return {
    seriesDir, sessionsRoot: path.join(seriesDir, 's'), home, kind, pair, seed: null, inputsRoot: null, hashes: [], snapshots: snapshotsOfWorld(world), ...snapshotFacts(world.snapshots),
    packageRules: readJson(path.join(INPUTS, 'package-flags.json')).rules, claudeBinary, cliVersion: world.explore.cli.version, nodeDir: world.paths.nodeDir, tsDir: world.paths.tsDir, token,
    ...(world.paths.dotnetDir ? { dotnetDir: world.paths.dotnetDir, hide: DISTRIBUTION_DOTNET } : {}),
    prices: policy.table, table: policy.table, policy: policy.policy,
    harnessSources: { boxInit: path.join(V1, 'harness', 'box-init.mjs'), audit: path.join(V1, '..', 'policy-ab-quick', 'hooks', 'audit.mjs') },
    caps: world.explore.caps, ceilingUsd: NO_TOTAL_USD, bareBudgetUsd: NO_TOTAL_USD, harnessIdentity: identity, preregTag: null, spin: readSpin(home),
    runMapFile: runMapFileOf(seriesDir), ledgerFile: ledgerFileOf(seriesDir), aliasStore: kind === 'rehearsal' ? null : aliasStore(path.join(seriesDir, 'aliases.json')),
    handVerdictsFor: () => [], abortReason: () => abort.reason, abortExit: () => abort.exit, planOver,
    ...(world.quick === true ? { lostCwdLow: true } : {}),
    fill, planFor: a => recordPlan(planForSession(a)), prompts, locale: world.explore.locale, envExtra: world.explore.envExtra, calibration, series,
  };
}

/** The pilot's `ctx.fill`: the frozen workspace of the item's stack. */
export const frozenFill = world => (world.quick ? quickFiller(world) : fillerFor({ explore: world.explore, sources: world.sources, typesNode: world.snapshots.typesNode }));

/** The quick tasks' `ctx.fill`: the stack's workspace files (`world.filesFor`) written into the session's work directory. */
export const quickFiller = world => ({ item, layout }) => fillWorkspace({ files: world.filesFor(item.session.stack), work: layout.work });

const projectDirs = home => () => {
  const dir = path.join(home, '.claude', 'projects');
  return existsSync(dir) ? readdirSync(dir, { withFileTypes: true }).filter(e => e.isDirectory()).map(e => path.join(dir, e.name)) : [];
};

/** The loop's answer for a quick scoring by ten passes (`scorePasses`): a decided scoring is `scored`; an exhausted one a `void` (the series offers its one re-run); a halt or a stop is `environment`, which halts the loop resumably. */
function loopAnswerOf(run) {
  if (run.kind === 'scored') return { kind: 'scored', score: run.score };
  if (run.kind === 'void') return { kind: 'void', class: run.class, reason: run.reason };
  return { kind: 'environment', reason: run.reason };
}

/** The quick world's scoring: ten passes from one committed build, under the series lock the caller holds. */
async function scoreByPasses({ world, calibration, heldout, home, say, scoreExtra, signal, call }) {
  const seriesDir = path.dirname(path.dirname(call.session));
  const seams = world.passSeams({ stack: call.stack, source: call.source, limits: calibration.limits, sets: [{ name: 'heldout', dir: heldout.casesDir }], traps: heldout.traps, say, driftPossible: scoreExtra.driftPossible === true, seriesDir });
  const run = await scorePasses({ ...seams, work: path.join(call.session, 'score'), seriesDir, signal, now: systemClock });
  return loopAnswerOf(run);
}

/**
 * The scoring step of the loop: the judge over the session's tree, with its own caches. Writes `<session>/score/score.json`. A harness failure (a process left in a scope) is `failed`, which stops the pilot.
 * A quick world scores through the ten passes (`world.passSeams`, F1.S2); the olympiad's world and a test's fake one through `world.scoreFn`, as they always did. `signal` ends the passes between two attempts; `singlePass` (the rehearsal, a free check of the pipeline and no verdict) keeps the one-pass scoring.
 */
export function scoreSessionDep({ world, calibration, heldout, home, say = () => {}, scoreExtra = {}, signal, singlePass = false }) {
  const byPasses = world.passSeams !== undefined && !singlePass;
  return async ({ session, source, stack, nugetCache, npmCache }) => {
    mkdirSync(path.join(session, 'score'), { recursive: true });
    if (byPasses) return scoreByPasses({ world, calibration, heldout, home, say, scoreExtra, signal, call: { session, source, stack } });
    return scoreOnce({ world, calibration, heldout, home, say, scoreExtra, call: { session, source, stack, nugetCache, npmCache } });
  };
}

/** The one-pass scoring (the olympiad, a rehearsal, a test's fake world): `world.scoreFn` once, `score.json` beside the session. */
async function scoreOnce({ world, calibration, heldout, home, say, scoreExtra, call }) {
  const work = path.join(call.session, 'score');
  const scored = await world.scoreFn({ stack: call.stack, source: call.source, limits: calibration.limits, sets: [{ name: 'heldout', dir: heldout.casesDir }], traps: heldout.traps, caches: { nuget: call.nugetCache, npm: call.npmCache }, work, operatorHome: home, say, ...scoreExtra });
  if (!scored.ok) return { kind: ['scorer-timeout', 'environment'].includes(scored.kind) ? scored.kind : 'failed', reason: scored.reason };
  writeFileSync(path.join(work, 'score.json'), `${JSON.stringify(scored.value, null, 2)}\n`);
  return { kind: 'scored', score: scored.value };
}

/**
 * The preflight edge: v1's, over the pilot's `stacks`, looking at `preflightHome`'s token file (that it exists, never what it holds), then (a paid run: `maskProbe` is given) the read-back of the pair's two CPU masks, so a
 * mask that is not applied refuses the launch before anything is paid (F1.S2). `baseFn` is the seam of the tests.
 */
export function preflightDep({ ctx, world, preflightHome, maskProbe = null, baseFn = preflight }) {
  return async () => {
    const stacks = stacksOf(world.explore);
    // always the PINNED CLI copy, also in a rehearsal (the stub CLI cannot answer `--version` without a broker)
    const r = await baseFn({ home: preflightHome, nodeDir: ctx.nodeDir, tsDir: ctx.tsDir, dotnetDir: ctx.dotnetDir ?? null, hide: ctx.hide ?? [], claudeBinary: world.paths.claudeBinary, stacks: stacks.value, install: false });
    if (!r.ok) return fail(`${r.reason}: ${(r.problems ?? []).join('; ')}`);
    return maskProbe === null ? ok(null) : maskReadBack({ ctx, probe: maskProbe });
  };
}

/** The machine edges: the real ones, or (a rehearsal measures nothing) the ones that ask for nothing. */
function machineDeps(machine) {
  if (machine) return { quiet: a => machine.quiet(a), startSampler: a => machine.startSampler(a), loadFlag: a => machine.loadFlag(a) };
  return { quiet: () => Promise.reject(new Error('a rehearsal measures nothing: no quiet check')), startSampler: () => ({ stop: async () => ({ samples: 0, flags: [], error: null }) }), loadFlag: async () => false };
}

/**
 * The edges of a loop run. `preflightHome` is the home whose token file the preflight looks at (it checks the file EXISTS, never its content): the operator's for a paid run, a throw-away one holding a fake
 * file for the rehearsal. `machine` is `machineFor`'s value, or `null` for the rehearsal (it measures nothing, so the loop never asks). `stub` leaves the operator's project folders alone (no canary is planted).
 */
export function runDeps({ ctx, world, scoreDep, machine, preflightHome, stub = false, maskProbe = stub ? null : cpus => probeMask({ cpus }) }) {
  return {
    guard: async () => ok(null), retireStale: () => retireStale({ root: ctx.sessionsRoot, now: Date.now(), endCell: a => endCell({ ...a, killTree, procApi: realProcApi }) }),
    preflight: preflightDep({ ctx, world, preflightHome, maskProbe }), ...machineDeps(machine), now: Date.now,
    scoreSession: scoreDep, phase: { listProjectDirs: stub ? () => [] : projectDirs(ctx.home), maskCheckMs: 15_000 },
  };
}

/** The machine of a real run: the quiet check, the phase sampler and the spin probe over the pilot's two roots. */
export function realMachine({ ctx, acceptNoisy }) {
  const thresholds = pilotQuiet();
  if (!thresholds.ok) throw new Error(thresholds.reason);
  mkdirSync(path.join(ctx.seriesDir, 'pairs'), { recursive: true });
  return machineFor({ thresholds: thresholds.value, calibratedMs: ctx.spin.medianMs, seriesDir: ctx.seriesDir, acceptNoisy, home: ctx.home });
}
