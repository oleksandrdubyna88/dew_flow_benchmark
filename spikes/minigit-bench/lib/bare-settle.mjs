// Settling a finished phase (E3b.S1): classify, charge and persist EVERY session of it, then act. A series stop is persisted ONLY when no session of the map is still `launching` (review C1): a stopped
// series refuses resume, so a stop written while a session of its phase is unread would leave that session paid for, uncharged and unsettleable. Each settled session carries its stop rules in the
// run-map (`stopRules`), so the stop is found again by the resume that settles the last one. A session whose evidence cannot be read does not block the others: it stays `launching`, uncharged, and
// `reconcile` adopts it on resume (review M8, G1); the phase then returns the collection failure (exit 5, resumable) naming the stop that is waiting.
import { collectSession } from './bare-collect.mjs';
import { EXIT, done } from './bare-exit.mjs';
import { writeSessionRecord } from './bare-records-io.mjs';
import { appendChargeOnce } from './ledger.mjs';
import { fail } from './result.mjs';
import { markStopped, pendingStops, transition } from './run-map.mjs';

/** The ledger kind of a session: its OWN kind (a resume or a settle-only may run under another `--what`), and `data` or `void` for a data-pair session. */
export function ledgerKind(kind, outcome) {
  if (kind !== 'data') return kind;
  return outcome.data ? 'data' : 'void';
}

/** The `launching` sessions of the map: paid for or not, not yet settled. */
export const unsettledOf = map => Object.values(map.sessions).filter(s => s.status === 'launching').map(s => s.rid);

/**
 * A series stop an ended session carries is recorded as soon as no session is unsettled (review MINOR-2), and the loop looks at the top of every pass and a resume at its end: a resume that plans an unread
 * partner again (nothing paid) never goes through `settleSessions`, which is where the stop was recorded, so the stop would be lost and the partner launched and paid for. -> the stops now recorded (`[]` for none).
 */
export function recordWaitingStop({ h, deps }) {
  const stops = pendingStops(h.get());
  if (stops.length === 0 || unsettledOf(h.get()).length > 0 || h.get().stopped) return [];
  h.commit(markStopped(h.get(), { rule: stops[0].rule, reason: JSON.stringify(stops[0].detail), at: new Date(deps.now()).toISOString() }));
  return stops;
}

/** The flags of a phase that every one of its sessions carries: the quiet check's, the sampler's and a start skew over 5 s. */
const phaseFlagsOf = ({ state, phase }) => [...(state.quiet?.flags ?? []), ...(state.sampler?.flags ?? []), ...(phase.startSkewMs > 5000 ? ['start skew'] : [])];

/** `collectSession` for one session of the phase; a throw is a `fail` like any evidence that cannot be read. */
function collectOne({ ctx, phase, signal, s }) {
  try {
    return collectSession({
      layout: s.layout, plan: s.plan, launched: s.launched, canaryTokens: s.canary.tokens, aliasBaseline: ctx.aliasStore ? ctx.aliasStore.get() : ctx.aliasBaseline, handVerdicts: ctx.handVerdictsFor(s.rid),
      abortReason: signal?.aborted ? ctx.abortReason() : null, partnerDirs: phase.sessions.filter(x => x !== s).map(x => x.layout.dir), table: ctx.table, policy: ctx.policy, home: ctx.home,
    });
  } catch (e) {
    return fail(e.message);
  }
}

/** The series stops a settled session carries: its own class when it is a stop, and the check stops (`seriesStops`). */
const stopRulesOf = (outcome, collected) => [...(outcome.stop ? [{ rule: outcome.class, detail: outcome.detail }] : []), ...collected.seriesStops];

/** What the run observed of a session, for its record. */
const runOf = ({ ctx, state, phase, s, flags }) => ({
  mask: s.mask, maskObserved: s.maskObserved?.observed, spawnStampMs: s.stampMs, startSkewMs: phase.startSkewMs, quiet: state.quiet ?? null, spin: ctx.spin ?? null, flags, harnessIdentity: ctx.harnessIdentity, inputs: ctx.inputHashes ?? null,
});

/** The fields the `ran` transition writes. */
const ranPatch = ({ ctx, signal, s, c, run, stopRules }) => ({
  class: c.outcome.class, data: c.outcome.data, usd: c.usd, run, dummy: s.plan.dummy, canaryTokens: s.canary.tokens, abortReason: signal?.aborted ? ctx.abortReason() : null,
  workspaceFlags: s.workspace.flags ?? [], workspaceExcluded: s.workspace.excludedDirs ?? null, ...(stopRules.length > 0 ? { stopRules } : {}),
});

/** Charge the session once, by its rid; a rehearsal costs nothing. */
function chargeSession({ ctx, deps, h, s, c }) {
  const kind = h.get().sessions[s.rid].kind;
  appendChargeOnce(ctx.ledgerFile, { kind: ledgerKind(kind, c.outcome), usd: kind === 'rehearsal' ? 0 : c.usd, what: c.outcome.class, rid: s.rid, costCaptured: c.costCaptured, t: new Date(deps.now()).toISOString() });
}

/** Charge, persist and say one collected session. */
function recordSettled({ ctx, deps, h, phase, state, signal, say, s, flags, c }) {
  if (ctx.aliasStore) ctx.aliasStore.update(c.alias, c.aliasCheck);
  chargeSession({ ctx, deps, h, s, c });
  const stopRules = stopRulesOf(c.outcome, c);
  h.apply(transition(h.get(), s.rid, 'ran', ranPatch({ ctx, signal, s, c, run: runOf({ ctx, state, phase, s, flags }), stopRules })));
  say(`${s.rid} (${s.session.stack} ${s.session.phase}): ${c.outcome.class}, $${c.usd.toFixed(4)}`);
  if (!c.outcome.data || stopRules.length > 0) writeSessionRecord({ ctx, entry: h.get().sessions[s.rid], score: null }); // a void has nothing to score; a session that stops the series is kept as it is
}

/** Settle one session of the phase. -> `null`, or the sentence saying its evidence could not be read (it stays `launching`). */
function settleOne(a) {
  const collected = collectOne(a);
  if (!collected.ok) return `collecting ${a.s.rid} failed: ${collected.reason}`;
  recordSettled({ ...a, c: collected.value });
  return null;
}

const stopList = stops => stops.map(x => `${x.rule} in ${x.rid}`).join(', ');

/** The phase ended in a series stop that is now recorded. */
function stoppedResult({ h, stops, waiting, failures }) {
  return h.get().stopped && waiting.length === 0 ? done(EXIT.stop, `${stopList(stops)}: the series stops${failures.length > 0 ? `; ${failures.join('; ')}` : ''}`) : null;
}

/** A session could not be read: exit 5, resumable, naming the stop that is waiting. */
function failedResult({ stops, waiting, failures }) {
  if (failures.length === 0) return null;
  const pending = stops.length > 0 ? `; a series stop is waiting (${stopList(stops)}): it is recorded when every session is settled, so resume settles ${waiting.join(', ')} first` : '';
  return done(EXIT.failed, `${failures.join('; ')}${pending}`);
}

/** `void: rate limit` is a resumable halt (contract section 9, review N2), taken once the whole phase is settled: the account's usage window is spent, so the re-run the schedule plans must not start in it. */
function rateLimitResult({ h, phase }) {
  const limited = phase.sessions.filter(x => h.get().sessions[x.rid]?.class === 'void: rate limit').map(x => x.rid);
  return limited.length === 0 ? null : done(EXIT.halt, `void: rate limit in ${limited.join(', ')}: the account's usage window is spent. The phase is settled and charged; nothing more is launched in this window. Resume after the window resets: the schedule then plans the re-run`);
}

const abortResult = ({ ctx, signal }) => (signal?.aborted ? done(ctx.abortExit(), 'cancelled; the sessions that ran are recorded as interrupted harness voids') : null);

/** Record a waiting stop, then the first of: the series stops, a session was unreadable, the rate limit, a cancel. -> `null` or `{ exit, message }`. */
function finishPhase(a) {
  const stops = pendingStops(a.h.get());
  const waiting = unsettledOf(a.h.get());
  recordWaitingStop(a);
  const facts = { ...a, stops, waiting };
  return [stoppedResult, failedResult, rateLimitResult, abortResult].map(f => f(facts)).find(Boolean) ?? null;
}

/** Classify, charge and persist EVERY session of a finished phase, then act (see the top of this file). -> `null` to go on, or `{ exit, message }`. */
export async function settleSessions({ ctx, deps, h, phase, state, signal, say }) {
  const flags = phaseFlagsOf({ state, phase });
  const failures = phase.sessions.map(s => settleOne({ ctx, deps, h, phase, state, signal, say, s, flags })).filter(Boolean);
  return finishPhase({ ctx, deps, h, phase, signal, failures });
}
