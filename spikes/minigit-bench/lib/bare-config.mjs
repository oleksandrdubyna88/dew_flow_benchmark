// The real wiring of a bare run (E3b.S1): the fixed configuration `runLoop` takes (`ctx`) and its edges (`deps`), built from the run-map, the registered inputs and the host.
// Everything here is an I/O edge or a lookup; the decisions live in the modules it hands to. The real token is read ONCE, from its mode-600 file, and goes only into `ctx.token`,
// which `launchPhase` hands to the credential broker (never to a box, an argv or a record).
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
import { fail, ok } from './result.mjs';
import { readSecretFile } from './secrets.mjs';
import { scoreSession } from './score-run.mjs';
import { readRunMap } from './run-map.mjs';
import { retireStale } from './session-state.mjs';

export const SPIKE = fileURLToPath(new URL('..', import.meta.url));
export const REPO = path.resolve(SPIKE, '..', '..');
const sleep = ms => new Promise(r => setTimeout(r, ms));
const readJson = file => JSON.parse(readFileSync(file, 'utf8'));

/** One real sampler of this machine (no series cells, this process as the harness), for `init`'s calibration window. -> `async () => sample`. */
export async function sampleOnceFor({ thresholds, probe }) {
  let n = 0;
  return createSampler({
    procApi: realProcApi, readText: p => (existsSync(p) ? readFileSync(p, 'utf8') : ''), cellIds: [], harnessPids: [String(process.pid)], liveSessions: () => liveSessionDirs(path.join(os.homedir(), 'rsd', 'minigit')),
    spin: () => probe(n++), now: Date.now, patterns: thresholds.agentPatterns,
  });
}

/** The quiet check, sampler and spin probe of this machine: `{ quiet(arg), startSampler(arg), loadFlag() }`. */
function machine({ map, thresholds, seriesDir, rsdRoot, acceptNoisy, now }) {
  const calibratedMs = map.spin?.medianMs ?? 1;
  const t = { ...thresholds, calibratedMs };
  const cpus = thresholds.spinCpus ?? [6, 7];
  let probes = 0;
  const spin = async () => { const r = await spinProbe({ iterations: thresholds.spinIterations, cpu: cpus[probes++ % cpus.length] }); if (!r.ok) throw new Error(r.reason); return r.value; };
  const build = () => createSampler({
    procApi: realProcApi, readText: p => (existsSync(p) ? readFileSync(p, 'utf8') : ''), cellIds: Object.keys(map.sessions).map(rid => `cell-${rid}`), harnessPids: [String(process.pid)],
    liveSessions: () => liveSessionDirs(rsdRoot), spin, now, patterns: thresholds.agentPatterns,
  });
  return {
    async quiet({ phase, v1Quiet }) {
      const sampler = await build();
      const check = () => quietWindow({ thresholds: t, sampleOnce: sampler, sleep });
      const waited = await waitForQuiet({ check, maxWaitMs: thresholds.maxWaitMs, recheckMs: thresholds.recheckMs, now, sleep });
      if (waited.quiet) return ok({ quiet: true, flags: [], numbers: waited.verdict.numbers, waitedMs: waited.waitedMs });
      if (!acceptNoisy) return fail(`the machine was not quiet for ${Math.round(waited.waitedMs / 60000)} min: ${waited.verdict.failures.map(f => `${f.condition} (${f.detail})`).join('; ')}`, { verdict: waited.verdict });
      const o = noisyOverride({ phase, v1Quiet, reason: acceptNoisy });
      return o.ok ? ok({ quiet: false, flags: o.value.flags, reason: o.value.reason, failures: waited.verdict.failures }) : o;
    },
    startSampler({ pair, phase }) {
      const ready = build();
      return startPhaseSampler({ sample: async () => (await ready)(), file: path.join(seriesDir, 'pairs', `p${pair}-${phase}-cpu.ndjson`), intervalMs: thresholds.intervalMs, thresholds: t, sleep });
    },
    async loadFlag() {
      const sampler = await build();
      const v = await quietWindow({ thresholds: t, sampleOnce: sampler, sleep, windowMs: thresholds.intervalMs * 2 });
      return !v.quiet;
    },
  };
}

/**
 * `{ seriesDir, map, kind, pair, args, paid, seams }` -> `ok({ ctx, deps, abort })` | `fail`. `kind` is `data`, `aa` or `pilot`. Reads the registered inputs and the fetched inputs
 * cache; the token only when `paid`. `seams` (`{ repo, home }`) are for tests: a run uses this repository and the operator's home. The identity in `ctx` is the one the
 * harness has on DISK now, and the guard reads the run-map from disk at every call, so an acceptance written after this call is seen.
 */
export async function buildRun({ seriesDir, map, kind, pair, args, paid, seams = {} }) {
  const home = seams.home ?? os.homedir();
  const repo = seams.repo ?? REPO;
  const identity = await harnessIdentity(repo);
  if (!identity.ok) return identity;
  const inputsDir = args.inputsDir ?? path.join(SPIKE, 'inputs');
  const stacks = loadStacks(readJson(path.join(inputsDir, 'stacks.json')));
  if (!stacks.ok) return stacks;
  const fetched = await fetchInputs({});
  if (!fetched.ok) return fetched;
  const policy = crosscheckPolicyFrom(SPIKE, { prices: path.join(inputsDir, map.pricesFile), policy: path.join(inputsDir, 'crosscheck-policy.json') });
  if (!policy.ok) return policy;
  const quiet = loadQuiet(readJson(path.join(inputsDir, 'quiet.json')));
  if (!quiet.ok) return quiet;
  const token = paid ? readSecretFile({ home, parts: TOKEN_FILE_PARTS, label: 'token' }) : ok('not read in a dry run');
  if (!token.ok) return token;
  const amendmentsText = existsSync(path.join(inputsDir, 'amendments.md')) ? readFileSync(path.join(inputsDir, 'amendments.md'), 'utf8') : '';
  const budget = effectiveBudget({ map, amendmentsText, pair });
  if (!budget.ok) return budget;
  const registered = readJson(path.join(inputsDir, 'cache-snapshots.json'));
  const snapshots = Object.fromEntries(Object.entries(map.snapshotDirs).map(([s, dir]) => [s, { dir, parts: registered[s].parts }]));
  const seedFile = path.join(inputsDir, 'seed.txt');
  const rsdRoot = path.dirname(seriesDir);
  const abort = { reason: null, exit: 130 };
  const ctx = {
    seriesDir, sessionsRoot: path.join(seriesDir, 's'), home, kind, pair, seed: kind === 'data' && existsSync(seedFile) ? readFileSync(seedFile, 'utf8').trim() : null,
    inputsRoot: fetched.value.root, hashes: fetched.value.entries, snapshots, snapshotPackages: Object.values(registered).flatMap(r => r.packages ?? []), snapshotBytes: Object.values(registered).reduce((a, r) => a + (r.sizeBytes ?? 0), 0),
    packageRules: readJson(path.join(inputsDir, 'package-flags.json')).rules, claudeBinary: map.claude.path, cliVersion: map.pins.claude, nodeDir: path.dirname(path.dirname(process.execPath)),
    tsDir: path.join(home, ...stacks.value.typescriptToolchainDir), token: token.value, prices: policy.value.table, table: policy.value.table, policy: policy.value.policy,
    harnessSources: { boxInit: path.join(SPIKE, 'harness', 'box-init.mjs'), audit: path.join(SPIKE, '..', 'policy-ab-quick', 'hooks', 'audit.mjs') },
    caps: map.caps, ceilingUsd: map.ceilingUsd, bareBudgetUsd: budget.value.bareBudgetUsd, harnessIdentity: identity.value, preregTag: map.preregTag, spin: map.spin,
    runMapFile: path.join(seriesDir, 'run-map.json'), ledgerFile: path.join(seriesDir, 'ledger.ndjson'), aliasStore: aliasStore(path.join(seriesDir, 'aliases.json')),
    handVerdictsFor: rid => handVerdictsFor(amendmentsText, rid),
    abortReason: () => abort.reason, abortExit: () => abort.exit, inputsDir,
  };
  const mach = machine({ map, thresholds: quiet.value, seriesDir, rsdRoot, acceptNoisy: args.acceptNoisy, now: Date.now });
  const deps = {
    guard: ({ paid: p, requireSeed }) => preregGuard({ repo, runMap: readRunMap(ctx.runMapFile).value ?? map, paid: p, requireSeed }),
    retireStale: () => retireStale({ root: ctx.sessionsRoot, now: Date.now(), endCell: a => endCell({ ...a, killTree, procApi: realProcApi }) }),
    preflight: async () => {
      const r = await preflight({ home, nodeDir: ctx.nodeDir, tsDir: ctx.tsDir, claudeBinary: ctx.claudeBinary, stacks: stacks.value, install: false });
      return r.ok ? ok(null) : fail(`${r.reason}: ${(r.problems ?? []).join('; ')}`);
    },
    quiet: arg => mach.quiet(arg), startSampler: arg => mach.startSampler(arg), loadFlag: () => mach.loadFlag(), now: Date.now,
    scoreSession: arg => scoreSession({ ...arg, sleep }), phase: { listProjectDirs: () => { const d = path.join(home, '.claude', 'projects'); return existsSync(d) ? readdirSync(d, { withFileTypes: true }).filter(e => e.isDirectory()).map(e => path.join(d, e.name)) : []; } },
  };
  return ok({ ctx, deps, abort });
}
