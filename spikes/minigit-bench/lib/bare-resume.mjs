// Resume (E3b.S1 step 11): before a resumed run continues, look for a live session owner, unplant every canary, retire stale sessions, then settle each `launching` session by what its directory says
// (adopt a finished launch, charge and void a cut-short one, plan again one that never paid). `reconcile` is the one entry; `dirFacts` is what the resume plan reads of a directory.
import { existsSync, readFileSync, readdirSync, rmSync } from 'node:fs';
import path from 'node:path';
import { assertRemovable } from '../../policy-ab-quick/lib/safe-rm.mjs';
import { unplantAll } from './bare-phase.mjs';
import { EXIT, holder } from './bare-exit.mjs';
import { recordWaitingStop, settleSessions } from './bare-settle.mjs';
import { writeSessionRecord } from './bare-records-io.mjs';
import { fail, ok } from './result.mjs';
import { holdsPaidWork } from './session-launch.mjs';
import { ownerAlive, readState, stateOf } from './session-state.mjs';
import { resumePlan, transition, writeRunMap } from './run-map.mjs';
import { layoutPaths } from './sandbox.mjs';

/** What a session directory holds, for `resumePlan`: `{ exists, hasMeta, retired, paidWork, ownerLive }`. `ownerLive`: its state file names an owner (pid + start time) that is running now. */
export function dirFacts(ctx, rid) {
  const layout = layoutPaths(ctx.sessionsRoot, rid);
  if (!existsSync(layout.dir)) return { exists: false, hasMeta: false, retired: false, paidWork: false, ownerLive: false };
  const read = readState(layout.files.state); // a state file nobody can read is `malformed`: no live owner, never a crash of the resume
  const state = read.ok ? read.value : null;
  return { exists: true, hasMeta: existsSync(layout.files.meta), retired: state?.phase === 'retired', paidWork: holdsPaidWork(layout), ownerLive: state !== null && stateOf(state, { ownerAlive }) === 'live' };
}

const manifestsOf = ctx => {
  const dir = path.join(ctx.seriesDir, 'canaries');
  return existsSync(dir) ? readdirSync(dir).map(f => path.join(dir, f)) : [];
};

/** Remove the claimed directory of a session that provably never paid (`dirFacts.paidWork` false: no stream, no OTel, nothing in box/out, no `forwarding` line in the broker log). */
function removeUnpaidDir(ctx, rid) {
  const layout = layoutPaths(ctx.sessionsRoot, rid);
  if (!existsSync(layout.dir) || holdsPaidWork(layout)) return;
  if (assertRemovable(layout.dir, ctx.sessionsRoot)) rmSync(layout.dir, { recursive: true, force: true });
}

/** Does this ended session still need its record written? A void that ended (`ran`, no data) has its record written at settle time. */
const recordMissing = (ctx, entry) => entry.data !== true && entry.status === 'ran' && !existsSync(path.join(ctx.sessionsRoot, entry.rid, 'record.json'));

/** A void session that ended (`ran`, no data) has its record written at settle time; a crash between the transition and that write is repaired here, before the schedule advances. */
function rebuildMissingRecords({ ctx, h, plan, say }) {
  for (const a of plan.filter(x => x.action === 'rebuild-record')) {
    const entry = h.get().sessions[a.rid];
    if (!recordMissing(ctx, entry)) continue;
    const written = writeSessionRecord({ ctx, entry, score: null });
    say(`${a.rid}: the record of an ended void session was missing and is rebuilt${written.ok ? '' : ` (failed: ${written.reason})`}`);
  }
}

/** The session directories of the series (`s/<rid>`, in the map or not) whose `state.json` names an owner that is alive now (pid and start time): another runner is still going. */
function liveOwners(ctx) {
  const root = ctx.sessionsRoot;
  const rids = existsSync(root) ? readdirSync(root, { withFileTypes: true }).filter(e => e.isDirectory()).map(e => e.name).sort() : [];
  return rids.filter(rid => dirFacts(ctx, rid).ownerLive);
}

/** A live owner is looked for FIRST (review coai-3): unplanting the canaries and `retireStale` act on the series' directories and would end or strip a session that a runner is still running. -> a `fail` or null. */
function liveOwnerRefusal({ ctx, map }) {
  const owned = liveOwners(ctx);
  return owned.length === 0 ? null
    : fail(`${owned.join(', ')}: the state.json of the session directory names an owner that is still running (alive by pid and start time): nothing was unplanted, retired, removed or settled. Wait for that process to end, or stop it`, { exit: EXIT.halt, map });
}

/** A session the resume plans again: nothing was paid. The claim is non-recursive (a rid is never reused), so the same rid cannot be launched again over a leftover directory. */
function resetUnpaid({ ctx, h, a, say }) {
  removeUnpaidDir(ctx, a.rid);
  h.apply(transition(h.get(), a.rid, 'planned'));
  say(`${a.rid}: ${a.reason}; its claimed directory was removed`);
}

/** The item `settleSessions` takes for a session the resume found `launching`: from the run-map entry and its directory. */
function launchingItem({ ctx, entry, a }) {
  const layout = layoutPaths(ctx.sessionsRoot, a.rid);
  const launched = a.action === 'adopt' ? ok({ meta: JSON.parse(readFileSync(layout.files.meta, 'utf8')) }) : null;
  return {
    rid: a.rid, session: { stack: entry.stack, phase: entry.phase, attempt: entry.attempt }, layout, plan: { token: ctx.token, dummy: entry.dummy }, launched, canary: { tokens: entry.canaryTokens ?? [] },
    workspace: { flags: entry.workspaceFlags ?? [] }, mask: entry.mask, maskObserved: null, stampMs: entry.run?.spawnStampMs ?? null,
  };
}

/** Does the result of settling stop the resume? A series stop does not when only settling was asked for (`--settle-only` on a stopped series). */
const blocksResume = (stopped, settleOnly) => stopped && !(settleOnly && stopped.exit === EXIT.stop);

/** Settle one session the resume found `launching` (adopt, charge and void, or plan again). -> a `fail` to return, or null. */
async function settleLaunching({ ctx, deps, h, a, settleOnly, say }) {
  if (a.action === 'reset') { resetUnpaid({ ctx, h, a, say }); return null; }
  const item = launchingItem({ ctx, entry: h.get().sessions[a.rid], a });
  const stopped = await settleSessions({ ctx: { ...ctx, abortExit: () => EXIT.halt, abortReason: () => null }, deps, h, phase: { sessions: [item], startSkewMs: null }, state: { quiet: null, sampler: null }, signal: null, say });
  // settle-only on a stopped series: the stop is known, the charge is the point
  return blocksResume(stopped, settleOnly) ? fail(stopped.message, { exit: stopped.exit, map: h.get() }) : null;
}

const SETTLED_ACTIONS = new Set(['adopt', 'void-interrupted', 'reset']);

/** Settle every session the plan says to adopt, void or plan again. -> the first `fail`, or null. */
async function settleAll({ ctx, deps, h, plan, settleOnly, say }) {
  for (const a of plan.filter(x => SETTLED_ACTIONS.has(x.action))) {
    const failed = await settleLaunching({ ctx, deps, h, a, settleOnly, say });
    if (failed) return failed;
  }
  return null;
}

/**
 * Before a resumed run continues (step 11): look for a live session owner first and refuse if there is one, then unplant every canary of the manifests, retire stale sessions, then settle each `launching` session by what its directory
 * says (adopt a finished launch, charge and void a cut-short one, plan again one that never paid). `settleOnly` (resume --settle-only, review N12) tolerates a stopped series. -> `ok(map)` | `fail`.
 */
export async function reconcile({ ctx, deps, map, say = () => {}, settleOnly = false }) {
  const refused = liveOwnerRefusal({ ctx, map });
  if (refused) return refused;
  unplantAll({ manifests: manifestsOf(ctx), home: ctx.home, roots: [ctx.sessionsRoot] });
  await deps.retireStale();
  const h = holder({ map, write: next => writeRunMap(ctx.runMapFile, next) });
  const plan = resumePlan(map, Object.fromEntries(Object.keys(map.sessions).map(rid => [rid, dirFacts(ctx, rid)])));
  rebuildMissingRecords({ ctx, h, plan, say });
  const failed = await settleAll({ ctx, deps, h, plan, settleOnly, say });
  if (failed) return failed;
  recordWaitingStop({ h, deps }); // a reset partner never reaches `settleSessions`: the stop its partner carries is recorded here
  return ok(h.get());
}
