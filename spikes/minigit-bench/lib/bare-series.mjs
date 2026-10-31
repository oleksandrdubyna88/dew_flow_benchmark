// The bare runner's loop (E3b.S1): drive a pair (or the A/A, or the Node pilot) from the run-map to its end. Each pass asks the schedule for the next step; a LAUNCH
// step goes through the gates (`bare-gates.mjs`: pre-registration guard, stale retirement, preflight, quiet check, spend gate), then `launchPhase`, then each session is classified
// and charged (`bare-settle.mjs`); a SCORE step runs the scorer on each data session in `firstStack` order. The run-map is persisted after every transition, so a crash resumes
// from what the files say (`bare-resume.mjs`). Exit codes: 0 done, 2 a series stop or the budget, 3 a resumable halt, 4 configuration (the guard), 5 the runner or the scorer failed.
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { firstStack, maskFor, ridOf } from './bare-plan.mjs';
import { launchPhase } from './bare-phase.mjs';
import { reclassifyByHand } from './bare-reclass.mjs';
import { EXIT, cellIdsOf, done, holder } from './bare-exit.mjs';
import { cancelled, gates } from './bare-gates.mjs';
import { finalSession, readSessionRecord, writePairRecord, writeSessionRecord } from './bare-records-io.mjs';
import { nextStep } from './bare-schedule.mjs';
import { recordWaitingStop, settleSessions } from './bare-settle.mjs';
import { appendLedger } from './ledger.mjs';
import { planSession, transition, writeRunMap } from './run-map.mjs';
import { layoutPaths } from './sandbox.mjs';

export { EXIT } from './bare-exit.mjs';
export { dirFacts, reconcile } from './bare-resume.mjs';

/** The persisted state of the run, held in one place: every change replaces the map and writes the file. */
const holderFor = ({ ctx, map }) => holder({ map, write: next => writeRunMap(ctx.runMapFile, next) });

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

// ---- a launch step --------------------------------------------------------------------------------------------------------------

/** The sessions of a launch step, planned in the run-map when they are new, and only those still `planned`: a session that already ended (its partner was reset by a resume) is never launched again (`ran -> launching` is not a transition). */
function planItems({ ctx, h, step }) {
  const all = ctx.kind === 'data' ? itemsOf({ ctx, step }) : step.sessions.map(s => ({ session: s, rid: s.rid, mask: h.get().sessions[s.rid].mask }));
  for (const item of all) {
    if (h.get().sessions[item.rid] === undefined) {
      h.apply(planSession(h.get(), { rid: item.rid, pair: ctx.pair, ...item.session, kind: ctx.kind, label: step.label, mask: item.mask }));
    }
  }
  return all.filter(item => h.get().sessions[item.rid].status === 'planned');
}

/** The barrier time of a pair: the first time a v2 phase of it starts (every later v2 start keeps it). */
function markBarrier({ ctx, deps, h, step }) {
  const pairs = h.get().pairs ?? {};
  if (step.phase !== 'v2' || pairs[ctx.pair]?.barrierAtMs !== undefined) return;
  h.commit({ ...h.get(), pairs: { ...pairs, [ctx.pair]: { ...(pairs[ctx.pair] ?? {}), barrierAtMs: deps.now() } } });
}

/** Every item goes `planned -> launching`, stamped with the start time. */
function startItems({ deps, h, items }) {
  for (const item of items) h.apply(transition(h.get(), item.rid, 'launching', { startedAt: new Date(deps.now()).toISOString() }));
}

/** `launchPhase` with the phase sampler running beside it; its flags go to `state.sampler` whatever happens. */
async function launchSampled({ ctx, deps, h, step, state, signal, items }) {
  const sampler = deps.startSampler({ pair: ctx.pair, phase: step.phase, cellIds: cellIdsOf(h) });
  try {
    return await launchPhase({ ctx, items, deps: deps.phase, signal });
  } finally {
    state.sampler = await sampler.stop();
  }
}

/** The phase could not start (nothing was paid): every item goes back to `planned`, and the run halts, resumably. */
function phaseNotStarted({ h, items, reason }) {
  for (const item of items) h.apply(transition(h.get(), item.rid, 'planned'));
  return done(EXIT.halt, `the phase could not start (nothing was paid): ${reason}`);
}

async function runLaunch(a) {
  const { ctx, deps, h, step, state, signal, say } = a;
  const items = planItems(a);
  const stopped = await gates({ ctx, deps, step: { ...step, sessions: items.map(i => i.session) }, state, h, say, signal });
  if (stopped) return stopped;
  markBarrier(a);
  startItems({ deps, h, items });
  say(`launching ${items.map(i => `${i.rid} (mask ${i.mask})`).join(' and ')}`);
  const phase = await launchSampled({ ...a, items });
  return phase.ok ? settleSessions({ ctx, deps, h, phase: phase.value, state, signal, say }) : phaseNotStarted({ h, items, reason: phase.reason });
}

// ---- a score step ---------------------------------------------------------------------------------------------------------------

/** Is `s` the session of this cell (pair, stack, phase, kind) of the run? */
const sameCell = (s, ref, ctx) => s.pair === ctx.pair && s.stack === ref.stack && s.phase === ref.phase && s.kind === ctx.kind;
/** The run-map entry a score step's reference names: by its rid, or by its cell and attempt. */
const entryOfRef = (map, ctx, ref) => (ref.rid ? map.sessions[ref.rid] : Object.values(map.sessions).find(s => sameCell(s, ref, ctx) && s.attempt === ref.attempt));

/** The sentence-bearing result of a scorer that did not score, or null: an environment fault or a timeout halts (resumable), anything else is the scorer failing. */
function scorerProblem(entry, r) {
  if (r.kind === 'environment' || r.kind === 'scorer-timeout') return done(EXIT.halt, `scoring ${entry.rid}: ${r.kind}: ${r.reason}`, { scorer: r.kind });
  return r.kind === 'scored' ? null : done(EXIT.failed, `scoring ${entry.rid} failed: ${r.reason}`);
}

/** Score one session and write its record. -> `null` or a `{ exit, message }`. */
async function scoreOne({ ctx, deps, h, ref, flag, say }) {
  const entry = entryOfRef(h.get(), ctx, ref);
  const layout = layoutPaths(ctx.sessionsRoot, entry.rid);
  const r = await deps.scoreSession({ session: layout.dir, source: layout.work, stack: entry.stack, phase: entry.phase, toolchains: [ctx.tsDir, ctx.nodeDir], nugetCache: layout.nuget, npmCache: layout.npm });
  const problem = scorerProblem(entry, r);
  if (problem) return problem;
  const scored = h.apply(transition(h.get(), entry.rid, 'scored', { scoreFlags: flag ? ['scored under load'] : [] }));
  writeSessionRecord({ ctx, entry: scored.sessions[entry.rid], score: r.score });
  say(`scored ${entry.rid}`);
  return null;
}

/** The refs of a score step, the stack the seed names first coming first. */
function scoreOrder({ ctx, step }) {
  const first = firstStack({ seed: ctx.seed ?? '', pair: ctx.pair, phase: step.sessions[0].phase });
  return [...step.sessions].sort((a, b) => (a.stack === first ? -1 : b.stack === first ? 1 : 0));
}

async function runScore({ ctx, deps, h, step, say, signal }) {
  const flag = await deps.loadFlag({ cellIds: cellIdsOf(h) });
  for (const ref of scoreOrder({ ctx, step })) {
    if (signal?.aborted) return cancelled(ctx, 'while scoring');
    const problem = await scoreOne({ ctx, deps, h, ref, flag, say });
    if (problem) return problem;
  }
  return null;
}

// ---- hand verdicts that arrive after a session ended -------------------------------------------------------------------------------

const scoreOf = layout => { try { return JSON.parse(readFileSync(path.join(layout.dir, 'score', 'score.json'), 'utf8')); } catch { return null; } };

/** What the run says of a reclassified session; a v2 that ran from a refuted v1 tree is named, and the operator refutes it by its own line. */
function reclassifyLine(c) {
  const dependents = c.dependents.length > 0 ? `; its cell's v2 session ${c.dependents.join(', ')} ran from this v1 tree: refute it by its own dated line if it must be re-run` : '';
  return `${c.rid} was ${c.from.class}${c.from.data ? ' (data)' : ''} and is reclassified to ${c.to.class}: the hand verdict of ${c.date} refuted ${c.g}${dependents}`;
}

/** The ledger event, the rewritten record and the line of one reclassified session. */
function recordReclassified({ ctx, map, c, say }) {
  appendLedger(ctx.ledgerFile, { event: 'reclassify', rid: c.rid, from: c.from.class, to: c.to.class, g: c.g, verdictDate: c.date, evidence: c.evidence, t: new Date().toISOString() });
  writeSessionRecord({ ctx, entry: map.sessions[c.rid], score: scoreOf(layoutPaths(ctx.sessionsRoot, c.rid)) });
  say(reclassifyLine(c));
}

/**
 * Apply the dated `refuted` lines to the sessions that already ended (review N4): the run-map is changed, one `reclassify` event line goes to the ledger, the session's record is rewritten to show it,
 * and the run says so. Done at the start of every pass, before `nextStep`, so the schedule plans the one re-run. A session's charge is untouched (it is charged once, by its rid).
 */
function applyHandVerdicts({ ctx, h, say }) {
  const { map, changes } = reclassifyByHand({ map: h.get(), handVerdictsFor: ctx.handVerdictsFor });
  if (changes.length === 0) return;
  h.commit(map);
  for (const c of changes) recordReclassified({ ctx, map, c, say });
}

// ---- the loop -------------------------------------------------------------------------------------------------------------------

/**
 * Pairs run in order (contract section 9: a pair is not skipped, repeated or re-ordered): while any pair j below this one has a next step (it never ran, or a hand verdict that arrived later made it need a
 * re-run), the run halts, resumably, and names the command that runs pair j first (review MINOR-6). -> a sentence or null; never for the A/A, the pilot or the rehearsal.
 */
function earlierPairNotDone({ ctx, map }) {
  if (ctx.kind !== 'data') return null;
  const behind = Array.from({ length: ctx.pair - 1 }, (_, i) => i + 1).find(j => nextStep(pairState(map, j)).action !== 'done');
  return behind === undefined ? null : `pair ${behind} is not done (its next step is ${describeStep(nextStep(pairState(map, behind)))}): run --what pair:${behind} first, then pair ${ctx.pair}. Pairs run in order, and a dated refuted line on an earlier pair needs its re-run before the next pair starts`;
}

/** A schedule step in words. */
function describeStep(step) {
  if (step.action === 'launch') return `to launch ${step.sessions.map(r => `${r.stack} ${r.phase} a${r.attempt}`).join(', ')}`;
  return step.action === 'score' ? 'to score' : step.reason;
}

/** What the loop checks before it asks the schedule: a cancel, a recorded stop, the hand verdicts that arrived, the earlier pairs. -> `null` to go on, or a `{ exit, message }`. */
function preStep({ ctx, deps, h, say, signal }) {
  if (signal?.aborted) return cancelled(ctx, 'between steps');
  recordWaitingStop({ h, deps });
  if (h.get().stopped) return done(EXIT.stop, `the series is stopped (${h.get().stopped.rule}): nothing more is launched. A stop is final for its series: fix the named cause and start a new series (init, then an A/A that is GO)`);
  applyHandVerdicts({ ctx, h, say });
  const behind = earlierPairNotDone({ ctx, map: h.get() });
  return behind ? done(EXIT.halt, behind) : null;
}

/** A pair that is done gets its record. */
function finishedPair({ ctx, h }) {
  if (ctx.kind === 'data') writePairRecord({ ctx, map: h.get(), pair: ctx.pair });
  return done(EXIT.done, 'done');
}

/** What a step of the schedule does: done, halt, launch or score. -> `{ exit, message }` for an end, `null` to go on. */
function stepResult(a, step) {
  if (step.action === 'done') return finishedPair(a);
  if (step.action === 'halt') return done(EXIT.stop, step.reason);
  return step.action === 'launch' ? runLaunch({ ...a, step }) : runScore({ ...a, step });
}

/** The quiet state of a v1 launch is kept for the v2 phase that follows it. */
function carryV1Quiet(state, step) {
  if (step.action === 'launch' && step.phase === 'v1') state.v1Quiet = state.quiet?.quiet ?? null;
}

/** One pass of the loop. -> `{ exit, message }` when the run ends, else `null`. */
async function loopPass(a) {
  const early = preStep(a);
  if (early) return early;
  const step = a.ctx.kind === 'data' ? nextStep(pairState(a.h.get(), a.ctx.pair)) : fixedStep(a.h.get(), a.ctx.kind);
  const result = await stepResult(a, step);
  if (!result) carryV1Quiet(a.state, step);
  return result;
}

/** The context of a loop: the v1 tree a v2 phase starts from is the pair's final v1 session, or for the v2 rehearsal the A/A twin's. */
const loopContext = (given, h) => ({ ...given, v1WorkOf: s => layoutPaths(given.sessionsRoot, given.kind === 'rehearsal' ? given.rehearsalOf : finalSession(h.get(), given.pair, s.stack, 'v1').rid).work });

/**
 * Drive the run to its end or its first halt. `ctx` is the fixed configuration of the run, `deps` its edges (see `bare-runner.mjs`), `map` the run-map.
 * -> `{ exit, message, map }`.
 */
export async function runLoop({ ctx: given, deps, map, signal, say = () => {} }) {
  const h = holderFor({ ctx: given, map });
  const ctx = loopContext(given, h);
  const state = { quiet: null, sampler: null, v1Quiet: h.get().pairs?.[ctx.pair]?.v1Quiet ?? null };
  for (let guard = 0; guard < 60; guard += 1) {
    const ended = await loopPass({ ctx, deps, h, state, signal, say });
    if (ended) return { ...ended, map: h.get() };
  }
  return { ...done(EXIT.failed, 'the loop did not end in 60 passes'), map: h.get() };
}

export { finalSession, readSessionRecord };
