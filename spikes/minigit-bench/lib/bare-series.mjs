// The bare runner's loop (E3b.S1): drive a pair (or the A/A, or the Node pilot) from the run-map to its end. Each pass asks the schedule for the next step; a LAUNCH
// step goes through the gates (pre-registration guard, stale retirement, preflight, quiet check, spend gate), then `launchPhase`, then each session is classified
// and charged; a SCORE step runs the scorer on each data session in `firstStack` order. The run-map is persisted after every transition, so a crash resumes
// from what the files say. Exit codes: 0 done, 2 a series stop or the budget, 3 a resumable halt, 4 configuration (the guard), 5 the runner or the scorer failed.
import { existsSync, readFileSync, readdirSync } from 'node:fs';
import path from 'node:path';
import { firstStack, maskFor, ridOf } from './bare-plan.mjs';
import { launchPhase, unplantAll } from './bare-phase.mjs';
import { collectSession } from './bare-collect.mjs';
import { finalSession, readSessionRecord, writePairRecord, writeSessionRecord } from './bare-records-io.mjs';
import { nextStep } from './bare-schedule.mjs';
import { appendLedger, readLedger } from './ledger.mjs';
import { holdsPaidWork } from './session-launch.mjs';
import { planSession, markStopped, resumePlan, transition, writeRunMap } from './run-map.mjs';
import { layoutPaths } from './sandbox.mjs';
import { spendGate } from './spend-gate.mjs';

export const EXIT = Object.freeze({ done: 0, stop: 2, halt: 3, config: 4, failed: 5 });
const done = (exit, message, extra = {}) => ({ exit, message, ...extra });
const STACKS = ['csharp', 'node'];

/** The persisted state of the run, held in one place: every change replaces the map and writes the file. */
function holder({ ctx, map }) {
  let current = map;
  return {
    get: () => current,
    commit(next) { current = next; writeRunMap(ctx.runMapFile, current); return current; },
    apply(result) { if (!result.ok) throw new Error(result.reason); return this.commit(result.value); },
  };
}

const pairState = (map, pair) => ({
  pair, sessions: Object.values(map.sessions).filter(s => s.pair === pair && s.kind === 'data').map(s => ({ stack: s.stack, phase: s.phase, attempt: s.attempt, status: s.status, class: s.class, data: s.data, label: s.label })),
});

/** The sessions of a launch step as `[{ session, rid, mask }]`, the stack the seed names first coming first. */
function itemsOf({ ctx, step }) {
  const first = firstStack({ seed: ctx.seed, pair: ctx.pair, phase: step.phase });
  const refs = [...step.sessions].sort((a, b) => (a.stack === first ? -1 : b.stack === first ? 1 : 0));
  return refs.map(ref => ({ session: { ...ref }, rid: ridOf({ seed: ctx.seed, pair: ctx.pair, ...ref }), mask: maskFor({ seed: ctx.seed, pair: ctx.pair, stack: ref.stack }) }));
}

/** A fixed list (the A/A, the pilot): one launch of everything still planned, then one score of what ended with data. */
function fixedStep(map) {
  const all = Object.values(map.sessions);
  const planned = all.filter(s => s.status === 'planned');
  if (planned.length > 0) return { action: 'launch', phase: planned[0].phase, sessions: planned.map(s => ({ stack: s.stack, phase: s.phase, attempt: s.attempt, rid: s.rid })), label: 'paired' };
  const unscored = all.filter(s => s.status === 'ran' && s.data === true);
  return unscored.length > 0 ? { action: 'score', sessions: unscored.map(s => ({ stack: s.stack, phase: s.phase, attempt: s.attempt, rid: s.rid })) } : { action: 'done' };
}

function ledgerKind(ctx, outcome) {
  if (ctx.kind !== 'data') return ctx.kind;
  return outcome.data ? 'data' : 'void';
}

// ---- the gates ------------------------------------------------------------------------------------------------------------------

/** -> `null` (go) or a `{ exit, message }`. */
async function gates({ ctx, deps, step, state }) {
  const guard = await deps.guard({ paid: true, requireSeed: ctx.kind === 'data' });
  if (!guard.ok) return done(EXIT.config, guard.reason);
  await deps.retireStale();
  const pre = await deps.preflight();
  if (!pre.ok) return done(EXIT.halt, `the preflight refuses: ${pre.reason}`);
  const quiet = await deps.quiet({ phase: step.phase, v1Quiet: state.v1Quiet ?? null });
  if (!quiet.ok) return done(EXIT.halt, quiet.reason, { verdict: quiet.verdict });
  state.quiet = quiet.value;
  const gate = spendGate({ ledger: readLedger(ctx.ledgerFile), liveUsd: 0, caps: step.sessions.map(() => ctx.caps.capUsd), ceilingUsd: ctx.ceilingUsd, bareBudgetUsd: ctx.bareBudgetUsd });
  return gate.stop ? done(EXIT.stop, `the spend gate stops the phase (${gate.stoppedBy.join(', ')}): ${gate.lines.join(' | ')}`, { gate }) : null;
}

// ---- a launch step --------------------------------------------------------------------------------------------------------------

async function runLaunch({ ctx, deps, h, step, state, signal, say }) {
  const items = ctx.kind === 'data' ? itemsOf({ ctx, step }) : step.sessions.map(s => ({ session: s, rid: s.rid, mask: h.get().sessions[s.rid].mask }));
  for (const item of items) {
    if (h.get().sessions[item.rid] === undefined) {
      h.apply(planSession(h.get(), { rid: item.rid, pair: ctx.pair, ...item.session, kind: ctx.kind, label: step.label, mask: item.mask }));
    }
  }
  const stopped = await gates({ ctx, deps, step, state });
  if (stopped) return stopped;
  if (step.phase === 'v2' && h.get().pairs?.[ctx.pair]?.barrierAtMs === undefined) h.commit({ ...h.get(), pairs: { ...(h.get().pairs ?? {}), [ctx.pair]: { barrierAtMs: deps.now() } } });
  for (const item of items) h.apply(transition(h.get(), item.rid, 'launching', { startedAt: new Date(deps.now()).toISOString() }));
  say(`launching ${items.map(i => `${i.rid} (mask ${i.mask})`).join(' and ')}`);
  const sampler = deps.startSampler({ pair: ctx.pair, phase: step.phase });
  let phase;
  try {
    phase = await launchPhase({ ctx, items, deps: deps.phase, signal });
  } finally {
    state.sampler = await sampler.stop();
  }
  if (!phase.ok) {
    for (const item of items) h.apply(transition(h.get(), item.rid, 'planned'));
    return done(EXIT.halt, `the phase could not start (nothing was paid): ${phase.reason}`);
  }
  return settleSessions({ ctx, deps, h, phase: phase.value, state, signal, say });
}

/** Classify, charge and persist every session of a finished phase; a `stop:` class ends the series here. */
async function settleSessions({ ctx, deps, h, phase, state, signal, say }) {
  const flags = [...(state.quiet?.flags ?? []), ...(state.sampler?.flags ?? []), ...(phase.startSkewMs > 5000 ? ['start skew'] : [])];
  for (const s of phase.sessions) {
    let collected;
    try {
      collected = collectSession({
        layout: s.layout, plan: s.plan, launched: s.launched, canaryTokens: s.canary.tokens, aliasBaseline: ctx.aliasStore ? ctx.aliasStore.get() : ctx.aliasBaseline, handVerdicts: ctx.handVerdictsFor(s.rid),
        abortReason: signal?.aborted ? ctx.abortReason() : null, partnerDirs: phase.sessions.filter(x => x !== s).map(x => x.layout.dir), table: ctx.table, policy: ctx.policy, home: ctx.home,
      });
    } catch (e) {
      return done(EXIT.failed, `collecting ${s.rid} failed: ${e.message}`);
    }
    if (!collected.ok) return done(EXIT.failed, `collecting ${s.rid} failed: ${collected.reason}`);
    const { outcome, usd } = collected.value;
    if (ctx.aliasStore) ctx.aliasStore.update(collected.value.alias, collected.value.aliasCheck);
    appendLedger(ctx.ledgerFile, { kind: ledgerKind(ctx, outcome), usd, what: outcome.class, rid: s.rid, t: new Date(deps.now()).toISOString() });
    const run = { mask: s.mask, maskObserved: s.maskObserved?.observed, spawnStampMs: s.stampMs, startSkewMs: phase.startSkewMs, quiet: state.quiet ?? null, spin: ctx.spin ?? null, flags };
    h.apply(transition(h.get(), s.rid, 'ran', { class: outcome.class, data: outcome.data, usd, run, dummy: s.plan.dummy, canaryTokens: s.canary.tokens, abortReason: signal?.aborted ? ctx.abortReason() : null, workspaceFlags: s.workspace.flags ?? [] }));
    say(`${s.rid} (${s.session.stack} ${s.session.phase}): ${outcome.class}, $${usd.toFixed(4)}`);
    if (!outcome.data) writeSessionRecord({ ctx, entry: h.get().sessions[s.rid], score: null });
    if (outcome.stop) {
      h.commit(markStopped(h.get(), { rule: outcome.class, reason: JSON.stringify(outcome.detail), at: new Date(deps.now()).toISOString() }));
      return done(EXIT.stop, `${outcome.class} in ${s.rid}: the series stops`);
    }
  }
  return signal?.aborted ? done(ctx.abortExit(), 'cancelled; the sessions that ran are recorded as interrupted harness voids') : null;
}

// ---- a score step ---------------------------------------------------------------------------------------------------------------

async function runScore({ ctx, deps, h, step, say }) {
  const first = firstStack({ seed: ctx.seed ?? '', pair: ctx.pair, phase: step.sessions[0].phase });
  const ordered = [...step.sessions].sort((a, b) => (a.stack === first ? -1 : b.stack === first ? 1 : 0));
  const flag = await deps.loadFlag();
  for (const ref of ordered) {
    const entry = ref.rid ? h.get().sessions[ref.rid] : Object.values(h.get().sessions).find(s => s.pair === ctx.pair && s.stack === ref.stack && s.phase === ref.phase && s.attempt === ref.attempt && s.kind === ctx.kind);
    const layout = layoutPaths(ctx.sessionsRoot, entry.rid);
    const r = await deps.scoreSession({ session: layout.dir, source: layout.work, stack: entry.stack, phase: entry.phase, toolchains: [ctx.tsDir, ctx.nodeDir], nugetCache: layout.nuget, npmCache: layout.npm });
    if (r.kind === 'environment' || r.kind === 'scorer-timeout') return done(EXIT.halt, `scoring ${entry.rid}: ${r.kind}: ${r.reason}`, { scorer: r.kind });
    if (r.kind !== 'scored') return done(EXIT.failed, `scoring ${entry.rid} failed: ${r.reason}`);
    const scored = h.apply(transition(h.get(), entry.rid, 'scored', { scoreFlags: flag ? ['scored under load'] : [] }));
    writeSessionRecord({ ctx, entry: scored.sessions[entry.rid], score: r.score });
    say(`scored ${entry.rid}`);
  }
  return null;
}

// ---- the loop -------------------------------------------------------------------------------------------------------------------

/**
 * Drive the run to its end or its first halt. `ctx` is the fixed configuration of the run, `deps` its edges (see `bare-runner.mjs`), `map` the run-map.
 * -> `{ exit, message, map }`.
 */
export async function runLoop({ ctx: given, deps, map, signal, say = () => {} }) {
  const h = holder({ ctx: given, map });
  const ctx = { ...given, v1WorkOf: s => layoutPaths(given.sessionsRoot, finalSession(h.get(), given.pair, s.stack, 'v1').rid).work };
  const state = { quiet: null, sampler: null, v1Quiet: null };
  for (let guard = 0; guard < 60; guard += 1) {
    const step = ctx.kind === 'data' ? nextStep(pairState(h.get(), ctx.pair)) : fixedStep(h.get());
    if (step.action === 'done') {
      if (ctx.kind === 'data') writePairRecord({ ctx, map: h.get(), pair: ctx.pair });
      return { ...done(EXIT.done, 'done'), map: h.get() };
    }
    if (step.action === 'halt') return { ...done(EXIT.stop, step.reason), map: h.get() };
    const stopped = step.action === 'launch' ? await runLaunch({ ctx, deps, h, step, state, signal, say }) : await runScore({ ctx, deps, h, step, say });
    if (stopped) return { ...stopped, map: h.get() };
    if (step.action === 'launch' && step.phase === 'v1') state.v1Quiet = state.quiet?.quiet ?? null;
  }
  return { ...done(EXIT.failed, 'the loop did not end in 60 passes'), map: h.get() };
}

// ---- resume ---------------------------------------------------------------------------------------------------------------------

/** What a session directory holds, for `resumePlan`: `{ exists, hasMeta, retired, paidWork }`. */
export function dirFacts(ctx, rid) {
  const layout = layoutPaths(ctx.sessionsRoot, rid);
  if (!existsSync(layout.dir)) return { exists: false, hasMeta: false, retired: false, paidWork: false };
  const state = existsSync(layout.files.state) ? JSON.parse(readFileSync(layout.files.state, 'utf8')) : null;
  return { exists: true, hasMeta: existsSync(layout.files.meta), retired: state?.phase === 'retired', paidWork: holdsPaidWork(layout) };
}

const manifestsOf = ctx => (existsSync(path.join(ctx.seriesDir, 'canaries')) ? readdirSync(path.join(ctx.seriesDir, 'canaries')).map(f => path.join(ctx.seriesDir, 'canaries', f)) : []);

/**
 * Before a resumed run continues (step 11): unplant every canary of the manifests first, retire stale sessions, then settle each `launching` session by what its directory
 * says (adopt a finished launch, charge and void a cut-short one, plan again one that never paid). -> `ok(map)` | `fail`.
 */
export async function reconcile({ ctx, deps, map, say = () => {} }) {
  unplantAll({ manifests: manifestsOf(ctx), home: ctx.home, roots: [ctx.sessionsRoot] });
  await deps.retireStale();
  const h = holder({ ctx, map });
  const dirs = Object.fromEntries(Object.keys(map.sessions).map(rid => [rid, dirFacts(ctx, rid)]));
  const actions = resumePlan(map, dirs).filter(a => a.action === 'adopt' || a.action === 'void-interrupted' || a.action === 'reset');
  for (const a of actions) {
    if (a.action === 'reset') { h.apply(transition(h.get(), a.rid, 'planned')); say(`${a.rid}: ${a.reason}`); continue; }
    const entry = h.get().sessions[a.rid];
    const layout = layoutPaths(ctx.sessionsRoot, a.rid);
    const launched = a.action === 'adopt' ? { ok: true, value: { meta: JSON.parse(readFileSync(layout.files.meta, 'utf8')) } } : null;
    const item = { rid: a.rid, session: { stack: entry.stack, phase: entry.phase, attempt: entry.attempt }, layout, plan: { token: ctx.token, dummy: entry.dummy }, launched, canary: { tokens: entry.canaryTokens ?? [] }, workspace: { flags: entry.workspaceFlags ?? [] }, mask: entry.mask, maskObserved: null, stampMs: entry.run?.spawnStampMs ?? null };
    const stopped = await settleSessions({ ctx: { ...ctx, abortExit: () => EXIT.halt, abortReason: () => null }, deps, h, phase: { sessions: [item], startSkewMs: null }, state: { quiet: null, sampler: null }, signal: null, say });
    if (stopped) return { ok: false, reason: stopped.message, exit: stopped.exit, map: h.get() };
  }
  return { ok: true, value: h.get() };
}

export { finalSession, readSessionRecord };
