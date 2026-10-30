// The bare runner's loop (E3b.S1): drive a pair (or the A/A, or the Node pilot) from the run-map to its end. Each pass asks the schedule for the next step; a LAUNCH
// step goes through the gates (pre-registration guard, stale retirement, preflight, quiet check, spend gate), then `launchPhase`, then each session is classified
// and charged; a SCORE step runs the scorer on each data session in `firstStack` order. The run-map is persisted after every transition, so a crash resumes
// from what the files say. Exit codes: 0 done, 2 a series stop or the budget, 3 a resumable halt, 4 configuration (the guard), 5 the runner or the scorer failed.
import { existsSync, readFileSync, readdirSync, rmSync } from 'node:fs';
import path from 'node:path';
import { assertRemovable } from '../../policy-ab-quick/lib/safe-rm.mjs';
import { firstStack, maskFor, ridOf } from './bare-plan.mjs';
import { launchPhase, unplantAll } from './bare-phase.mjs';
import { reclassifyByHand } from './bare-reclass.mjs';
import { collectSession } from './bare-collect.mjs';
import { finalSession, readSessionRecord, writePairRecord, writeSessionRecord } from './bare-records-io.mjs';
import { nextStep } from './bare-schedule.mjs';
import { appendChargeOnce, appendLedger, readLedger } from './ledger.mjs';
import { fail, ok } from './result.mjs';
import { holdsPaidWork } from './session-launch.mjs';
import { ownerAlive, stateOf } from './session-state.mjs';
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

/** A fixed list (the A/A, the pilot, the rehearsal): one launch of everything of ITS OWN KIND still planned, then one score of what ended with data (review N15: a run-map holds the sessions of every kind, and `--what aa` must not launch the pilot's or the rehearsal's). */
function fixedStep(map, kind) {
  const all = Object.values(map.sessions).filter(s => s.kind === kind);
  const planned = all.filter(s => s.status === 'planned');
  if (planned.length > 0) return { action: 'launch', phase: planned[0].phase, sessions: planned.map(s => ({ stack: s.stack, phase: s.phase, attempt: s.attempt, rid: s.rid })), label: 'paired' };
  const unscored = all.filter(s => s.status === 'ran' && s.data === true);
  return unscored.length > 0 ? { action: 'score', sessions: unscored.map(s => ({ stack: s.stack, phase: s.phase, attempt: s.attempt, rid: s.rid })) } : { action: 'done' };
}

/** The cell ids of every session the live run-map holds: what the sampler must count as ours (the map the run started from holds none of the sessions planned since). */
const cellIdsOf = h => Object.keys(h.get().sessions).map(rid => `cell-${rid}`);

/** The `void: contaminated` sessions of the whole series: the second one stops it (registered series stop; analysis contract section 9). */
const contaminatedVoids = map => Object.values(map.sessions).filter(s => s.class === 'void: contaminated').length;

/** The ledger kind of a session: its OWN kind (a resume or a settle-only may run under another `--what`), and `data` or `void` for a data-pair session. */
function ledgerKind(kind, outcome) {
  if (kind !== 'data') return kind;
  return outcome.data ? 'data' : 'void';
}

/** Whether the v1 phase started quiet, kept in the run-map (`pairs[k].v1Quiet`): `--accept-noisy` is refused for a v2 whose v1 was quiet, and a resume is a new process. */
function rememberV1Quiet({ ctx, h, quiet }) {
  const pairs = h.get().pairs ?? {};
  h.commit({ ...h.get(), pairs: { ...pairs, [ctx.pair]: { ...(pairs[ctx.pair] ?? {}), v1Quiet: quiet } } });
}

/** A cancel that comes OUTSIDE a launch (between steps, in a gate, while scoring): nothing is launched and no session is voided, so no re-run is spent; resume continues. */
const cancelled = (ctx, where) => done(ctx.abortExit(), `cancelled ${where} (${ctx.abortReason() ?? 'no reason recorded'}): nothing was launched and no session was voided; resume continues from the run-map`);

// ---- the gates ------------------------------------------------------------------------------------------------------------------

/** -> `null` (go) or a `{ exit, message }`. */
async function gates({ ctx, deps, step, state, h, say, signal }) {
  if (signal?.aborted) return cancelled(ctx, 'before the gates');
  const guard = await deps.guard({ paid: true, requireSeed: ctx.kind === 'data' });
  if (!guard.ok) return done(EXIT.config, guard.reason);
  await deps.retireStale();
  const pre = await deps.preflight();
  if (!pre.ok) return done(EXIT.halt, `the preflight refuses: ${pre.reason}`);
  if (signal?.aborted) return cancelled(ctx, 'in the gates');
  if (ctx.kind === 'rehearsal') return null; // a rehearsal measures nothing and pays nothing: no quiet check, no spend gate
  const quiet = await deps.quiet({ phase: step.phase, v1Quiet: state.v1Quiet ?? null, cellIds: cellIdsOf(h), say, signal });
  if (signal?.aborted) return cancelled(ctx, 'in the quiet check');
  if (!quiet.ok) return done(EXIT.halt, quiet.reason, { verdict: quiet.verdict });
  state.quiet = quiet.value;
  if (step.phase === 'v1') rememberV1Quiet({ ctx, h, quiet: quiet.value.quiet });
  const gate = spendGate({ ledger: readLedger(ctx.ledgerFile), liveUsd: 0, caps: step.sessions.map(() => ctx.caps.capUsd), ceilingUsd: ctx.ceilingUsd, bareBudgetUsd: ctx.bareBudgetUsd });
  if (gate.stop) return done(EXIT.stop, `the spend gate stops the phase (${gate.stoppedBy.join(', ')}): ${gate.lines.join(' | ')}`, { gate });
  return signal?.aborted ? cancelled(ctx, 'at the spend gate') : null;
}

// ---- a launch step --------------------------------------------------------------------------------------------------------------

async function runLaunch({ ctx, deps, h, step, state, signal, say }) {
  const items = ctx.kind === 'data' ? itemsOf({ ctx, step }) : step.sessions.map(s => ({ session: s, rid: s.rid, mask: h.get().sessions[s.rid].mask }));
  for (const item of items) {
    if (h.get().sessions[item.rid] === undefined) {
      h.apply(planSession(h.get(), { rid: item.rid, pair: ctx.pair, ...item.session, kind: ctx.kind, label: step.label, mask: item.mask }));
    }
  }
  const stopped = await gates({ ctx, deps, step, state, h, say, signal });
  if (stopped) return stopped;
  if (step.phase === 'v2' && h.get().pairs?.[ctx.pair]?.barrierAtMs === undefined) h.commit({ ...h.get(), pairs: { ...(h.get().pairs ?? {}), [ctx.pair]: { ...(h.get().pairs?.[ctx.pair] ?? {}), barrierAtMs: deps.now() } } });
  for (const item of items) h.apply(transition(h.get(), item.rid, 'launching', { startedAt: new Date(deps.now()).toISOString() }));
  say(`launching ${items.map(i => `${i.rid} (mask ${i.mask})`).join(' and ')}`);
  const sampler = deps.startSampler({ pair: ctx.pair, phase: step.phase, cellIds: cellIdsOf(h) });
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

/** The series stops the settled sessions of the map carry (`stopRules`, written with the session's `ran` transition), plus the second `void: contaminated`: `[{ rid, rule, detail }]`. */
function pendingStops(map) {
  const carried = Object.values(map.sessions).flatMap(s => (s.stopRules ?? []).map(x => ({ rid: s.rid, ...x })));
  if (carried.length === 0 && contaminatedVoids(map) >= 2) return [{ rid: Object.values(map.sessions).filter(s => s.class === 'void: contaminated').map(s => s.rid).join(', '), rule: 'second void: contaminated', detail: { voids: contaminatedVoids(map) } }];
  return carried;
}

const unsettledOf = map => Object.values(map.sessions).filter(s => s.status === 'launching').map(s => s.rid);

/**
 * Classify, charge and persist EVERY session of a finished phase, then act. A series stop is persisted ONLY when no session of the map is still `launching` (review C1): a stopped
 * series refuses resume, so a stop written while a session of its phase is unread would leave that session paid for, uncharged and unsettleable. Each settled session carries its stop
 * rules in the run-map (`stopRules`), so the stop is found again by the resume that settles the last one. A session whose evidence cannot be read does not block the others: it stays
 * `launching`, uncharged, and `reconcile` adopts it on resume (review M8, G1); the loop then returns the collection failure (exit 5, resumable) naming the stop that is waiting.
 */
async function settleSessions({ ctx, deps, h, phase, state, signal, say }) {
  const flags = [...(state.quiet?.flags ?? []), ...(state.sampler?.flags ?? []), ...(phase.startSkewMs > 5000 ? ['start skew'] : [])];
  const failures = [];
  for (const s of phase.sessions) {
    let collected;
    try {
      collected = collectSession({
        layout: s.layout, plan: s.plan, launched: s.launched, canaryTokens: s.canary.tokens, aliasBaseline: ctx.aliasStore ? ctx.aliasStore.get() : ctx.aliasBaseline, handVerdicts: ctx.handVerdictsFor(s.rid),
        abortReason: signal?.aborted ? ctx.abortReason() : null, partnerDirs: phase.sessions.filter(x => x !== s).map(x => x.layout.dir), table: ctx.table, policy: ctx.policy, home: ctx.home,
      });
    } catch (e) {
      failures.push(`collecting ${s.rid} failed: ${e.message}`);
      continue;
    }
    if (!collected.ok) { failures.push(`collecting ${s.rid} failed: ${collected.reason}`); continue; }
    const { outcome, usd, costCaptured } = collected.value;
    if (ctx.aliasStore) ctx.aliasStore.update(collected.value.alias, collected.value.aliasCheck);
    appendChargeOnce(ctx.ledgerFile, { kind: ledgerKind(h.get().sessions[s.rid].kind, outcome), usd: h.get().sessions[s.rid].kind === 'rehearsal' ? 0 : usd, what: outcome.class, rid: s.rid, costCaptured, t: new Date(deps.now()).toISOString() });
    const run = { mask: s.mask, maskObserved: s.maskObserved?.observed, spawnStampMs: s.stampMs, startSkewMs: phase.startSkewMs, quiet: state.quiet ?? null, spin: ctx.spin ?? null, flags, harnessIdentity: ctx.harnessIdentity, inputs: ctx.inputHashes ?? null };
    const stopRules = [...(outcome.stop ? [{ rule: outcome.class, detail: outcome.detail }] : []), ...collected.value.seriesStops];
    h.apply(transition(h.get(), s.rid, 'ran', { class: outcome.class, data: outcome.data, usd, run, dummy: s.plan.dummy, canaryTokens: s.canary.tokens, abortReason: signal?.aborted ? ctx.abortReason() : null, workspaceFlags: s.workspace.flags ?? [], workspaceExcluded: s.workspace.excludedDirs ?? null, ...(stopRules.length > 0 ? { stopRules } : {}) }));
    say(`${s.rid} (${s.session.stack} ${s.session.phase}): ${outcome.class}, $${usd.toFixed(4)}`);
    if (!outcome.data || stopRules.length > 0) writeSessionRecord({ ctx, entry: h.get().sessions[s.rid], score: null }); // a void has nothing to score; a session that stops the series is kept as it is
  }
  const stops = pendingStops(h.get());
  const waiting = unsettledOf(h.get());
  if (stops.length > 0 && waiting.length === 0 && !h.get().stopped) {
    const [first] = stops;
    h.commit(markStopped(h.get(), { rule: first.rule, reason: JSON.stringify(first.detail), at: new Date(deps.now()).toISOString() }));
  }
  if (h.get().stopped && waiting.length === 0) return done(EXIT.stop, `${stops.map(x => `${x.rule} in ${x.rid}`).join(', ')}: the series stops${failures.length > 0 ? `; ${failures.join('; ')}` : ''}`);
  if (failures.length > 0) {
    const pending = stops.length > 0 ? `; a series stop is waiting (${stops.map(x => `${x.rule} in ${x.rid}`).join(', ')}): it is recorded when every session is settled, so resume settles ${waiting.join(', ')} first` : '';
    return done(EXIT.failed, `${failures.join('; ')}${pending}`);
  }
  // `void: rate limit` is a resumable halt (contract section 9, review N2), taken once the whole phase is settled: the account's usage window is spent, so the re-run the schedule plans must not start in it
  const limited = phase.sessions.filter(x => h.get().sessions[x.rid]?.class === 'void: rate limit').map(x => x.rid);
  if (limited.length > 0) return done(EXIT.halt, `void: rate limit in ${limited.join(', ')}: the account's usage window is spent. The phase is settled and charged; nothing more is launched in this window. Resume after the window resets: the schedule then plans the re-run`);
  return signal?.aborted ? done(ctx.abortExit(), 'cancelled; the sessions that ran are recorded as interrupted harness voids') : null;
}

// ---- a score step ---------------------------------------------------------------------------------------------------------------

async function runScore({ ctx, deps, h, step, say, signal }) {
  const first = firstStack({ seed: ctx.seed ?? '', pair: ctx.pair, phase: step.sessions[0].phase });
  const ordered = [...step.sessions].sort((a, b) => (a.stack === first ? -1 : b.stack === first ? 1 : 0));
  const flag = await deps.loadFlag({ cellIds: cellIdsOf(h) });
  for (const ref of ordered) {
    if (signal?.aborted) return cancelled(ctx, 'while scoring');
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

// ---- hand verdicts that arrive after a session ended -------------------------------------------------------------------------------

const scoreOf = layout => { try { return JSON.parse(readFileSync(path.join(layout.dir, 'score', 'score.json'), 'utf8')); } catch { return null; } };

/**
 * Apply the dated `refuted` lines to the sessions that already ended (review N4): the run-map is changed, one `reclassify` event line goes to the ledger, the session's record is rewritten to show it,
 * and the run says so. Done at the start of every pass, before `nextStep`, so the schedule plans the one re-run. A session's charge is untouched (it is charged once, by its rid).
 */
function applyHandVerdicts({ ctx, h, say }) {
  const { map, changes } = reclassifyByHand({ map: h.get(), handVerdictsFor: ctx.handVerdictsFor });
  if (changes.length === 0) return;
  h.commit(map);
  for (const c of changes) {
    appendLedger(ctx.ledgerFile, { event: 'reclassify', rid: c.rid, from: c.from.class, to: c.to.class, g: c.g, verdictDate: c.date, evidence: c.evidence, t: new Date().toISOString() });
    const layout = layoutPaths(ctx.sessionsRoot, c.rid);
    writeSessionRecord({ ctx, entry: map.sessions[c.rid], score: scoreOf(layout) });
    say(`${c.rid} was ${c.from.class}${c.from.data ? ' (data)' : ''} and is reclassified to ${c.to.class}: the hand verdict of ${c.date} refuted ${c.g}${c.dependents.length > 0 ? `; its cell's v2 session ${c.dependents.join(', ')} ran from this v1 tree: refute it by its own dated line if it must be re-run` : ''}`);
  }
}

// ---- the loop -------------------------------------------------------------------------------------------------------------------

/**
 * Drive the run to its end or its first halt. `ctx` is the fixed configuration of the run, `deps` its edges (see `bare-runner.mjs`), `map` the run-map.
 * -> `{ exit, message, map }`.
 */
export async function runLoop({ ctx: given, deps, map, signal, say = () => {} }) {
  const h = holder({ ctx: given, map });
  // the v1 tree a v2 phase starts from: the pair's final v1 session, or for the v2 rehearsal the A/A twin's
  const ctx = { ...given, v1WorkOf: s => layoutPaths(given.sessionsRoot, given.kind === 'rehearsal' ? given.rehearsalOf : finalSession(h.get(), given.pair, s.stack, 'v1').rid).work };
  const state = { quiet: null, sampler: null, v1Quiet: h.get().pairs?.[ctx.pair]?.v1Quiet ?? null };
  for (let guard = 0; guard < 60; guard += 1) {
    if (signal?.aborted) return { ...cancelled(ctx, 'between steps'), map: h.get() };
    if (h.get().stopped) return { ...done(EXIT.stop, `the series is stopped (${h.get().stopped.rule}): nothing more is launched. A stop is final for its series: fix the named cause and start a new series (init, then an A/A that is GO)`), map: h.get() };
    applyHandVerdicts({ ctx, h, say });
    const step = ctx.kind === 'data' ? nextStep(pairState(h.get(), ctx.pair)) : fixedStep(h.get(), ctx.kind);
    if (step.action === 'done') {
      if (ctx.kind === 'data') writePairRecord({ ctx, map: h.get(), pair: ctx.pair });
      return { ...done(EXIT.done, 'done'), map: h.get() };
    }
    if (step.action === 'halt') return { ...done(EXIT.stop, step.reason), map: h.get() };
    const stopped = step.action === 'launch' ? await runLaunch({ ctx, deps, h, step, state, signal, say }) : await runScore({ ctx, deps, h, step, say, signal });
    if (stopped) return { ...stopped, map: h.get() };
    if (step.action === 'launch' && step.phase === 'v1') state.v1Quiet = state.quiet?.quiet ?? null;
  }
  return { ...done(EXIT.failed, 'the loop did not end in 60 passes'), map: h.get() };
}

// ---- resume ---------------------------------------------------------------------------------------------------------------------

/** What a session directory holds, for `resumePlan`: `{ exists, hasMeta, retired, paidWork, ownerLive }`. `ownerLive`: its state file names an owner (pid + start time) that is running now. */
export function dirFacts(ctx, rid) {
  const layout = layoutPaths(ctx.sessionsRoot, rid);
  if (!existsSync(layout.dir)) return { exists: false, hasMeta: false, retired: false, paidWork: false, ownerLive: false };
  const state = existsSync(layout.files.state) ? JSON.parse(readFileSync(layout.files.state, 'utf8')) : null;
  return { exists: true, hasMeta: existsSync(layout.files.meta), retired: state?.phase === 'retired', paidWork: holdsPaidWork(layout), ownerLive: state !== null && stateOf(state, { ownerAlive }) === 'live' };
}

const manifestsOf = ctx => (existsSync(path.join(ctx.seriesDir, 'canaries')) ? readdirSync(path.join(ctx.seriesDir, 'canaries')).map(f => path.join(ctx.seriesDir, 'canaries', f)) : []);

/** Remove the claimed directory of a session that provably never paid (`dirFacts.paidWork` false: no stream, no OTel, nothing in box/out, no `forwarding` line in the broker log). */
function removeUnpaidDir(ctx, rid) {
  const layout = layoutPaths(ctx.sessionsRoot, rid);
  if (!existsSync(layout.dir) || holdsPaidWork(layout)) return;
  if (assertRemovable(layout.dir, ctx.sessionsRoot)) rmSync(layout.dir, { recursive: true, force: true });
}

/** A void session that ended (`ran`, no data) has its record written at settle time; a crash between the transition and that write is repaired here, before the schedule advances. */
function rebuildMissingRecords({ ctx, h, plan, say }) {
  for (const a of plan.filter(x => x.action === 'rebuild-record')) {
    const entry = h.get().sessions[a.rid];
    if (entry.data === true || entry.status !== 'ran' || existsSync(path.join(ctx.sessionsRoot, a.rid, 'record.json'))) continue;
    const written = writeSessionRecord({ ctx, entry, score: null });
    say(`${a.rid}: the record of an ended void session was missing and is rebuilt${written.ok ? '' : ` (failed: ${written.reason})`}`);
  }
}

/**
 * Before a resumed run continues (step 11): unplant every canary of the manifests first, retire stale sessions, then settle each `launching` session by what its directory
 * says (adopt a finished launch, charge and void a cut-short one, plan again one that never paid). `settleOnly` (resume --settle-only, review N12) tolerates a stopped series. -> `ok(map)` | `fail`.
 */
export async function reconcile({ ctx, deps, map, say = () => {}, settleOnly = false }) {
  unplantAll({ manifests: manifestsOf(ctx), home: ctx.home, roots: [ctx.sessionsRoot] });
  await deps.retireStale();
  const h = holder({ ctx, map });
  const dirs = Object.fromEntries(Object.keys(map.sessions).map(rid => [rid, dirFacts(ctx, rid)]));
  const plan = resumePlan(map, dirs);
  // a directory whose state.json names an owner that is ALIVE belongs to a runner that is still going: resume would remove or settle what that runner is running (review N13)
  const owned = Object.values(map.sessions).filter(x => x.status === 'launching' && dirs[x.rid].ownerLive).map(x => x.rid);
  if (owned.length > 0) return fail(`${owned.join(', ')}: the state.json of the session directory names an owner that is still running (alive by pid and start time): not removed and not settled. Wait for that process to end, or stop it`, { exit: EXIT.halt, map });
  rebuildMissingRecords({ ctx, h, plan, say });
  const actions = plan.filter(a => a.action === 'adopt' || a.action === 'void-interrupted' || a.action === 'reset');
  for (const a of actions) {
    if (a.action === 'reset') {
      removeUnpaidDir(ctx, a.rid); // the claim is non-recursive (a rid is never reused): the same rid cannot be launched again over a leftover directory
      h.apply(transition(h.get(), a.rid, 'planned'));
      say(`${a.rid}: ${a.reason}; its claimed directory was removed`);
      continue;
    }
    const entry = h.get().sessions[a.rid];
    const layout = layoutPaths(ctx.sessionsRoot, a.rid);
    const launched = a.action === 'adopt' ? ok({ meta: JSON.parse(readFileSync(layout.files.meta, 'utf8')) }) : null;
    const item = { rid: a.rid, session: { stack: entry.stack, phase: entry.phase, attempt: entry.attempt }, layout, plan: { token: ctx.token, dummy: entry.dummy }, launched, canary: { tokens: entry.canaryTokens ?? [] }, workspace: { flags: entry.workspaceFlags ?? [] }, mask: entry.mask, maskObserved: null, stampMs: entry.run?.spawnStampMs ?? null };
    const stopped = await settleSessions({ ctx: { ...ctx, abortExit: () => EXIT.halt, abortReason: () => null }, deps, h, phase: { sessions: [item], startSkewMs: null }, state: { quiet: null, sampler: null }, signal: null, say });
    if (stopped && !(settleOnly && stopped.exit === EXIT.stop)) return fail(stopped.message, { exit: stopped.exit, map: h.get() }); // settle-only on a stopped series: the stop is known, the charge is the point
  }
  return ok(h.get());
}

export { finalSession, readSessionRecord };
