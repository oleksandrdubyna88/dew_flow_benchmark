// The real wiring of a bare run (E3b.S1): the fixed configuration `runLoop` takes (`ctx`) and its edges (`deps`), built from the run-map, the registered inputs and the host.
// Everything here is an I/O edge or a lookup; the decisions live in the modules it hands to. The real token is read ONCE, from its mode-600 file, and goes only into `ctx.token`,
// which `launchPhase` hands to the credential broker (never to a box, an argv or a record).
import { createHash } from 'node:crypto';
import { readFileSync, readdirSync, existsSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { killTree } from '../../policy-ab-quick/lib/proc.mjs';
import { TOKEN_FILE_PARTS } from '../capture/capture-env.mjs';
import { crosscheckPolicyFrom } from '../capture/policy-input.mjs';
import { aliasStore } from './alias-store.mjs';
import { handVerdictsFor } from './amendments.mjs';
import { createSampler, liveSessionDirs, spinProbe, startPhaseSampler } from './cpu-sampler.mjs';
import { effectiveBudget } from './bare-limits.mjs';
import { fetchInputs } from './fetch-inputs.mjs';
import { loadStacks } from './host-checks.mjs';
import { endCell, realProcApi } from './janitor.mjs';
import { preflight } from './preflight.mjs';
import { harnessIdentity, preregGuard } from './prereg-guard.mjs';
import { quietWindow, loadQuiet, noisyOverride, waitForQuiet } from './quiet.mjs';
import { firstResult } from './bare-exit.mjs';
import { fail, ok } from './result.mjs';
import { readSecretFile } from './secrets.mjs';
import { scoreSession } from './score-run.mjs';
import { readRunMap } from './run-map.mjs';
import { retireStale } from './session-state.mjs';

export const SPIKE = fileURLToPath(new URL('..', import.meta.url));
export const REPO = path.resolve(SPIKE, '..', '..');
const sleep = ms => new Promise(r => setTimeout(r, ms));
/** A sleep that a signal cuts short: the quiet wait is up to 30 minutes and a Ctrl-C must not wait for it. */
const abortableSleep = (ms, signal) => new Promise(resolve => {
  if (signal?.aborted) { resolve(); return; }
  const timer = setTimeout(done, ms);
  function done() { clearTimeout(timer); signal?.removeEventListener('abort', done); resolve(); }
  signal?.addEventListener('abort', done, { once: true });
});
const readJson = file => JSON.parse(readFileSync(file, 'utf8'));
const sha256File = file => createHash('sha256').update(readFileSync(file)).digest('hex');

/** One real sampler of this machine (no series cells, this process as the harness), for `init`'s calibration window. -> `async () => sample`. */
export async function sampleOnceFor({ thresholds, probe }) {
  let n = 0;
  return createSampler({
    procApi: realProcApi, readText: p => (existsSync(p) ? readFileSync(p, 'utf8') : ''), cellIds: [], harnessPids: [String(process.pid)], liveSessions: () => liveSessionDirs(path.join(os.homedir(), 'rsd', 'minigit')),
    spin: () => probe(n++), now: Date.now, patterns: thresholds.agentPatterns,
  });
}

const secs = ms => Math.round(ms / 1000);
const failureList = (verdict, format) => verdict.failures.map(format).join('; ');

/** The spin probe of this machine: each call runs one probe on the next of the spin CPUs. */
function spinProbeOf(thresholds) {
  const cpus = thresholds.spinCpus ?? [6, 7];
  let probes = 0;
  return async () => {
    const r = await spinProbe({ iterations: thresholds.spinIterations, cpu: cpus[probes++ % cpus.length] });
    if (!r.ok) throw new Error(r.reason);
    return r.value;
  };
}

/** `cellIds => sampler` over the real process table. `cellIds` are the cells of THIS phase, handed in by the loop from the live run-map: the map read at start holds none of the sessions planned since. */
const samplerBuilder = ({ procApi, rsdRoot, spin, now, thresholds }) => cellIds => createSampler({
  procApi, readText: p => (existsSync(p) ? readFileSync(p, 'utf8') : ''), cellIds, harnessPids: [String(process.pid)],
  liveSessions: () => liveSessionDirs(rsdRoot), spin, now, patterns: thresholds.agentPatterns,
});

/** The quiet check ended without a quiet machine: refused, or started anyway under `--accept-noisy`. */
function notQuiet({ waited, phase, v1Quiet, acceptNoisy }) {
  if (!acceptNoisy) return fail(`the machine was not quiet for ${Math.round(waited.waitedMs / 60000)} min: ${failureList(waited.verdict, f => `${f.condition} (${f.detail})`)}`, { verdict: waited.verdict });
  const o = noisyOverride({ phase, v1Quiet, reason: acceptNoisy });
  return o.ok ? ok({ quiet: false, flags: o.value.flags, reason: o.value.reason, failures: waited.verdict.failures }) : o;
}

/** The pre-start quiet check: wait up to the limit for a quiet machine, saying so before each re-check. */
async function quietCheck({ m, phase, v1Quiet, cellIds = [], say = () => {}, signal }) {
  const sampler = await m.build(cellIds);
  const sleep = ms => abortableSleep(ms, signal);
  const onRetry = ({ verdict, waitedMs, nextCheckInMs }) => say(`the machine is not quiet (${failureList(verdict, f => `${f.condition}: ${f.detail}`)}); waited ${secs(waitedMs)} s of ${secs(m.thresholds.maxWaitMs)} s; next check in ${secs(nextCheckInMs)} s`);
  const waited = await waitForQuiet({ check: () => quietWindow({ thresholds: m.t, sampleOnce: sampler, sleep }), maxWaitMs: m.thresholds.maxWaitMs, recheckMs: m.thresholds.recheckMs, now: m.now, sleep, onRetry, signal });
  if (waited.aborted) return fail('cancelled while waiting for a quiet machine');
  return waited.quiet ? ok({ quiet: true, flags: [], numbers: waited.verdict.numbers, waitedMs: waited.waitedMs }) : notQuiet({ waited, phase, v1Quiet, acceptNoisy: m.acceptNoisy });
}

/** The phase sampler: one sample per interval into `pairs/p<k>-<phase>-cpu.ndjson`. */
function phaseSampler({ m, pair, phase, cellIds = [] }) {
  const ready = m.build(cellIds);
  return startPhaseSampler({ sample: async () => (await ready)(), file: path.join(m.seriesDir, 'pairs', `p${pair}-${phase}-cpu.ndjson`), intervalMs: m.thresholds.intervalMs, thresholds: m.t, sleep });
}

/** Whether the machine is loaded now (a short window): `scored under load`. */
async function loadedNow({ m, cellIds = [] } = {}) {
  const sampler = await m.build(cellIds);
  const v = await quietWindow({ thresholds: m.t, sampleOnce: sampler, sleep, windowMs: m.thresholds.intervalMs * 2 });
  return !v.quiet;
}

/** The quiet check, sampler and spin probe of this machine: `{ quiet(arg), startSampler(arg), loadFlag() }`. */
function machine({ map, thresholds, seriesDir, rsdRoot, acceptNoisy, now, procApi = realProcApi }) {
  const m = { thresholds, t: { ...thresholds, calibratedMs: map.spin?.medianMs ?? 1 }, seriesDir, acceptNoisy, now, build: samplerBuilder({ procApi, rsdRoot, spin: spinProbeOf(thresholds), now, thresholds }) };
  return { quiet: arg => quietCheck({ m, ...arg }), startSampler: arg => phaseSampler({ m, ...arg }), loadFlag: arg => loadedNow({ m, ...arg }) };
}

/** Record a step's result in `bag` under `key`, or return its `fail`. */
function keep(bag, key, result) {
  if (!result.ok) return result;
  bag[key] = result.value;
  return null;
}

/**
 * The inputs a run reads, in order, the first that cannot be read ending the build: the harness identity on disk, the stacks, the fetched inputs cache, the crosscheck policy, the quiet thresholds,
 * the token (only when `paid`: a rehearsal and a dry run never read the token file), and the bare budget with its one permitted raise. -> `ok({ identity, stacks, fetched, policy, quiet, token, budget, amendmentsText })` | `fail`.
 */
async function gather({ repo, home, inputsDir, map, pair, paid }) {
  const bag = {};
  const amendmentsFile = path.join(inputsDir, 'amendments.md');
  bag.amendmentsText = existsSync(amendmentsFile) ? readFileSync(amendmentsFile, 'utf8') : '';
  const failed = await firstResult([
    async () => keep(bag, 'identity', await harnessIdentity(repo)),
    () => keep(bag, 'stacks', loadStacks(readJson(path.join(inputsDir, 'stacks.json')))),
    async () => keep(bag, 'fetched', await fetchInputs({})),
    () => keep(bag, 'policy', crosscheckPolicyFrom(SPIKE, { prices: path.join(inputsDir, map.pricesFile), policy: path.join(inputsDir, 'crosscheck-policy.json') })),
    () => keep(bag, 'quiet', loadQuiet(readJson(path.join(inputsDir, 'quiet.json')))),
    () => keep(bag, 'token', paid ? readSecretFile({ home, parts: TOKEN_FILE_PARTS, label: 'token' }) : ok('not read in a dry run')),
    () => keep(bag, 'budget', effectiveBudget({ map, amendmentsText: bag.amendmentsText, pair })),
  ]);
  return failed ?? ok(bag);
}

/** The fixed configuration of a run (`ctx`): from the run-map, the registered inputs, what `gather` read and the host. The token goes only into `ctx.token`. */
function contextOf({ seriesDir, map, kind, pair, args, home, inputsDir, got, abort }) {
  const registered = readJson(path.join(inputsDir, 'cache-snapshots.json'));
  const seedFile = path.join(inputsDir, 'seed.txt');
  const sha = name => sha256File(path.join(inputsDir, name));
  return {
    seriesDir, sessionsRoot: path.join(seriesDir, 's'), home, kind, pair, seed: kind === 'data' && existsSync(seedFile) ? readFileSync(seedFile, 'utf8').trim() : null,
    inputsRoot: got.fetched.root, hashes: got.fetched.entries, snapshots: Object.fromEntries(Object.entries(map.snapshotDirs).map(([st, dir]) => [st, { dir, parts: registered[st].parts }])),
    snapshotPackages: Object.values(registered).flatMap(r => r.packages ?? []), snapshotBytes: Object.values(registered).reduce((acc, r) => acc + (r.sizeBytes ?? 0), 0),
    packageRules: readJson(path.join(inputsDir, 'package-flags.json')).rules, claudeBinary: map.claude.path, cliVersion: map.pins.claude, nodeDir: path.dirname(path.dirname(process.execPath)),
    tsDir: path.join(home, ...got.stacks.typescriptToolchainDir), token: got.token, prices: got.policy.table, table: got.policy.table, policy: got.policy.policy,
    harnessSources: { boxInit: path.join(SPIKE, 'harness', 'box-init.mjs'), audit: path.join(SPIKE, '..', 'policy-ab-quick', 'hooks', 'audit.mjs') },
    caps: map.caps, ceilingUsd: map.ceilingUsd, bareBudgetUsd: got.budget.bareBudgetUsd, harnessIdentity: got.identity, preregTag: map.preregTag, spin: map.spin,
    runMapFile: path.join(seriesDir, 'run-map.json'), ledgerFile: path.join(seriesDir, 'ledger.ndjson'), aliasStore: aliasStore(path.join(seriesDir, 'aliases.json')),
    handVerdictsFor: rid => handVerdictsFor(got.amendmentsText, rid, map.seriesId),
    abortReason: () => abort.reason, abortExit: () => abort.exit, inputsDir,
    inputHashes: { quietSha256: sha('quiet.json'), packageFlagsSha256: sha('package-flags.json') }, // the thresholds that applied to every session of this run
  };
}

/** The edges of a run (`deps`): the guard, the stale retirement, the preflight, the machine's quiet check and samplers, the scorer and the box's project folders. */
function depsOf({ ctx, mach, repo, home, got, map }) {
  const listProjectDirs = () => {
    const d = path.join(home, '.claude', 'projects');
    return existsSync(d) ? readdirSync(d, { withFileTypes: true }).filter(e => e.isDirectory()).map(e => path.join(d, e.name)) : [];
  };
  return {
    guard: ({ paid: p, requireSeed }) => preregGuard({ repo, runMap: readRunMap(ctx.runMapFile).value ?? map, paid: p, requireSeed }),
    retireStale: () => retireStale({ root: ctx.sessionsRoot, now: Date.now(), endCell: a => endCell({ ...a, killTree, procApi: realProcApi }) }),
    preflight: async () => {
      const r = await preflight({ home, nodeDir: ctx.nodeDir, tsDir: ctx.tsDir, claudeBinary: ctx.claudeBinary, stacks: got.stacks, install: false });
      return r.ok ? ok(null) : fail(`${r.reason}: ${(r.problems ?? []).join('; ')}`);
    },
    quiet: arg => mach.quiet(arg), startSampler: arg => mach.startSampler(arg), loadFlag: arg => mach.loadFlag(arg), now: Date.now,
    scoreSession: arg => scoreSession({ ...arg, sleep }), phase: { listProjectDirs },
  };
}

/**
 * `{ seriesDir, map, kind, pair, args, paid, seams }` -> `ok({ ctx, deps, abort })` | `fail`. `kind` is `data`, `aa` or `pilot`. Reads the registered inputs and the fetched inputs
 * cache; the token only when `paid`. `seams` (`{ repo, home, procApi }`) are for tests: a run uses this repository, the operator's home and the host's process table. The identity in `ctx` is the one the
 * harness has on DISK now, and the guard reads the run-map from disk at every call, so an acceptance written after this call is seen.
 */
export async function buildRun({ seriesDir, map, kind, pair, args, paid, seams = {} }) {
  const home = seams.home ?? os.homedir();
  const repo = seams.repo ?? REPO;
  const inputsDir = args.inputsDir ?? path.join(SPIKE, 'inputs');
  const got = await gather({ repo, home, inputsDir, map, pair, paid });
  if (!got.ok) return got;
  const abort = { reason: null, exit: 130 };
  const ctx = contextOf({ seriesDir, map, kind, pair, args, home, inputsDir, got: got.value, abort });
  const mach = machine({ map, thresholds: got.value.quiet, seriesDir, rsdRoot: path.dirname(seriesDir), acceptNoisy: args.acceptNoisy, now: Date.now, procApi: seams.procApi });
  return ok({ ctx, deps: depsOf({ ctx, mach, repo, home, got: got.value, map }), abort });
}
