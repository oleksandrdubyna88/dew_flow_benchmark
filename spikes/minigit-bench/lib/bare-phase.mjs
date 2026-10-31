// One LAUNCH phase of the bare runner (E3b.S1 step 5): claim the layouts, fill the workspaces, seed the caches, plant the canaries, then start every session in
// the same tick on its mask (the stack the seed names first), stamp each spawn, and when all have closed check and unplant the canaries. It runs sessions; it
// decides nothing about them (`bare-collect.mjs` classifies) and writes no run-map (`bare-series.mjs` does).
import { spawn } from 'node:child_process';
import { randomBytes } from 'node:crypto';
import { existsSync, mkdirSync, rmSync } from 'node:fs';
import path from 'node:path';
import { plantTargets, unplant } from '../../policy-ab-quick/lib/canaries.mjs';
import { bareSessionPlan } from './bare-plan.mjs';
import { NEUTRAL_NOTE, sessionCanaryTargets } from './canary-plan.mjs';
import { cellProcesses, realProcApi } from './janitor.mjs';
import { maskObserved } from './cpu-sampler.mjs';
import { fail, ok } from './result.mjs';
import { dummyToken, sessionLayout } from './sandbox.mjs';
import { seedCaches } from './cache-snapshot.mjs';
import { fillV1, fillV2 } from './workspace.mjs';
import { launchSession } from './session-launch.mjs';

export const DEFAULT_PHASE_DEPS = Object.freeze({
  launchSession, now: Date.now, spawn: (exe, argv, opts) => spawn(exe, argv, opts), procApi: realProcApi, maskCheckMs: 15_000, listProjectDirs: () => [],
});

/** The box's spawn: the same argv `launchSession` would run, behind `taskset -c <mask> nice -n 19` (both exec, so the pid stays bwrap's), stamped at the spawn. */
export function maskedSpawn({ mask, stamp, d }) {
  return (argv, env) => {
    stamp();
    return d.spawn('taskset', ['-c', mask, 'nice', '-n', '19', ...argv], { env, stdio: ['pipe', 'pipe', 'pipe'], detached: true });
  };
}

/** The workspace of a phase: v1 is exactly the pinned inputs, v2 a copy of the item's v1 tree plus the v2 inputs. */
const fillFor = ({ ctx, item, layout }) => (item.session.phase === 'v1'
  ? fillV1({ inputsRoot: ctx.inputsRoot, hashes: ctx.hashes, work: layout.work })
  : fillV2({ inputsRoot: ctx.inputsRoot, hashes: ctx.hashes, v1Work: ctx.v1WorkOf(item.session), work: layout.work, stack: item.session.stack }));

function prepare({ ctx, item, d }) {
  const made = sessionLayout(ctx.sessionsRoot, item.rid);
  if (!made.ok) return made;
  const layout = made.value;
  const release = failure => { rmSync(layout.dir, { recursive: true, force: true }); return failure; }; // this attempt claimed the dir and nothing is paid yet: a retry claims the same rid again
  const filled = fillFor({ ctx, item, layout });
  if (!filled.ok) return release(filled);
  const seeded = seedCaches({ layout, snapshots: ctx.snapshots });
  return seeded.ok ? ok({ ...item, layout, workspace: filled.value, seeded: seeded.value }) : release(seeded);
}

const manifestOf = ({ ctx, item }) => path.join(ctx.seriesDir, 'canaries', `${item.rid}.json`);

/** Unplant every manifest that exists (one that was never written has nothing planted); a failure on one is collected and thrown after the rest are done. */
/** Unplant one manifest: `null`, or the sentence of the error (a manifest that is not there is nothing to remove). */
function unplantOne(m, home, roots) {
  if (!existsSync(m)) return null;
  try {
    unplant(m, home, { roots });
    return null;
  } catch (e) {
    return `${m}: ${e.message}`;
  }
}

function unplantManifests({ manifests, home, roots }) {
  const errors = manifests.map(m => unplantOne(m, home, roots)).filter(Boolean);
  if (errors.length > 0) throw new Error(`canaries could not be removed: ${errors.join('; ')}`);
}

/** One canary per operator project folder and one in the partner's config dir; the manifest lists them BEFORE any file exists. -> `{ manifest, tokens }`. */
function plantFor({ ctx, item, partner, d }) {
  if (partner) mkdirSync(path.join(partner.layout.config, 'projects'), { recursive: true });
  const targets = sessionCanaryTargets({ operatorHome: ctx.home, partnerConfigDir: partner?.layout.config, existingProjectDirs: d.listProjectDirs(), excludeUnder: [item.layout.dir] });
  const manifest = manifestOf({ ctx, item });
  const planted = plantTargets(manifest, targets, { note: NEUTRAL_NOTE });
  return { manifest, tokens: planted.filter(t => t.status === 'planted').map(t => t.token), planted: planted.length };
}

function sessionPlan({ ctx, item }) {
  return bareSessionPlan({
    layout: item.layout, stack: item.session.stack, phase: item.session.phase, claudeBinary: ctx.claudeBinary, cliVersion: ctx.cliVersion, nodeDir: ctx.nodeDir, tsDir: ctx.tsDir,
    token: ctx.token, dummy: dummyToken(randomBytes(6).toString('hex')), prices: ctx.prices, operatorHome: ctx.home, harnessSources: ctx.harnessSources, caps: ctx.caps, envExtra: ctx.envExtra ?? {}, over: ctx.planOver ?? {},
  });
}

const removeLayouts = prepared => prepared.forEach(x => rmSync(x.layout.dir, { recursive: true, force: true }));

/** Prepare every item (a layout claimed, a workspace filled, the caches seeded). A failure releases what the earlier items claimed: nothing paid has started, so the layouts hold nothing worth keeping. -> `ok(prepared)` | the failing `fail`. */
function prepareAll({ ctx, items, d }) {
  const prepared = [];
  for (const item of items) {
    const p = prepare({ ctx, item, d });
    if (!p.ok) { removeLayouts(prepared); return p; }
    prepared.push(p.value);
  }
  return ok(prepared);
}

/** Start one session behind its mask: the mask is checked after `maskCheckMs`, the spawn is stamped, and the result is the item with its plan and what `launchSession` returned. */
function startOne({ item, plan, d, signal, stamps, observed }) {
  const timer = setTimeout(() => {
    const pids = cellProcesses(plan.cellId, d.procApi, { inner: true }).pids;
    observed[item.rid] = maskObserved({ pids, mask: item.mask, procApi: d.procApi });
  }, d.maskCheckMs);
  const launched = d.launchSession(plan, { spawnBox: maskedSpawn({ mask: item.mask, stamp: () => { stamps[item.rid] = d.now(); }, d }) }, { signal })
    .finally(() => clearTimeout(timer));
  return launched.then(result => ({ ...item, plan, launched: result }));
}

/**
 * The cleanup scope starts BEFORE the first canary is planted: whatever throws from here on (a planting, a plan, a launch), every manifest started is unplanted and, when nothing paid
 * had started, the claimed layouts are released (review G2). -> `{ settled, stamps, observed }`.
 */
async function launchPrepared({ ctx, prepared, d, signal }) {
  const manifests = [];
  const stamps = {};
  const observed = {};
  let launching = false;
  try {
    const withCanaries = prepared.map(item => {
      manifests.push(manifestOf({ ctx, item }));
      return { ...item, canary: plantFor({ ctx, item, partner: prepared.find(x => x !== item), d }) };
    });
    const plans = withCanaries.map(item => ({ item, plan: sessionPlan({ ctx, item }) })); // every plan is made before any launch starts: a throw here has paid for nothing
    launching = true;
    const settled = await Promise.all(plans.map(({ item, plan }) => startOne({ item, plan, d, signal, stamps, observed })));
    return { settled, stamps, observed };
  } catch (e) {
    if (!launching) removeLayouts(prepared); // nothing paid has started
    throw e;
  } finally {
    unplantManifests({ manifests, home: ctx.home, roots: [ctx.sessionsRoot] });
  }
}

/**
 * Launch `items` (`[{ session, rid, mask }]`, the first stack first) together. -> `ok({ sessions: [{ ...item, layout, plan, launched, stampMs, canary, maskObserved }], startSkewMs })`
 * after every session has closed and the canaries are unplanted; `fail` when a layout, a workspace or a seed could not be made (nothing paid has started then).
 */
export async function launchPhase({ ctx, items, deps = {}, signal }) {
  const d = { ...DEFAULT_PHASE_DEPS, ...deps };
  const prepared = prepareAll({ ctx, items, d });
  if (!prepared.ok) return prepared;
  const { settled, stamps, observed } = await launchPrepared({ ctx, prepared: prepared.value, d, signal });
  const sessions = settled.map(s => ({ ...s, stampMs: stamps[s.rid] ?? null, maskObserved: observed[s.rid] ?? null }));
  const spawned = sessions.map(s => s.stampMs).filter(x => x !== null);
  return ok({ sessions, startSkewMs: spawned.length === 2 ? Math.abs(spawned[0] - spawned[1]) : null });
}

/** Resume: unplant every canary of every manifest of the series FIRST (a crash may have left files in the operator's folders). -> the unplant results. */
export function unplantAll({ manifests, home, roots }) {
  return manifests.flatMap(m => unplant(m, home, { roots }));
}

