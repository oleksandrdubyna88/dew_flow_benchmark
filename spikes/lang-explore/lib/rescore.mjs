// `explore.mjs rescore --task NN --series DIR --rid R [--wait S]`: FREE. Re-classifies a session that already ended from its own evidence under the rule as it is now, scores its kept tree through
// `lib/score-quick.mjs`, and writes the same records a run writes. Born of the first quick run: session ec6637 was voided as contaminated by a relative walk in a directory the scan had lost (`cd -`), a false
// positive in a box where nothing outside is visible.
//   - It changes a session only from an ended VOID that the evidence now classes as DATA; every other outcome is reported and nothing is written.
//   - Append only: the run-map entry keeps `reclassifiedFrom: [{ class, data, reason, at }, ...]`, the record carries it too, and the ledger gets an `event` line. The ledger's SPEND is not touched (the session was
//     charged when it ended; an event line is not a charge).
//   - It refuses a session that has no workspace, one that is not `ran`, one whose cell already has another data session, and a series whose lock a live run holds (`--wait S` waits for that run to end).
//   - The scoring comes BEFORE any write: a layer or runner failure (kind `environment`) leaves every file as it was, exit 3.
import { existsSync } from 'node:fs';
import path from 'node:path';
import { collectAgain, writeSessionRecord } from '../../minigit-bench/lib/bare-records-io.mjs';
import { appendLedger } from '../../minigit-bench/lib/ledger.mjs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { transition, writeRunMap } from '../../minigit-bench/lib/run-map.mjs';
import { layoutPaths } from '../../minigit-bench/lib/sandbox.mjs';
import { acquireSeriesLock } from '../../minigit-bench/lib/series-lock.mjs';
import { allOk } from './chain.mjs';
import { systemClock } from './clock.mjs';
import { loadExplore } from './inputs.mjs';
import { quickPrompts } from './init-quick.mjs';
import { loadHeldout } from './heldout.mjs';
import { calibrationFor } from './limits.mjs';
import { pinsOk } from './pins.mjs';
import { isQuick, quickExplore } from './quick.mjs';
import { finalizePair, quickHeldoutProblem } from './run.mjs';
import { frozenFill, runContext, scoreSessionDep } from './run-ctx.mjs';
import { runnerDriftPossible, templateDriftPossible } from './harness-pin.mjs';
import { isRehearsal, loadSeries, seriesInputs, seriesPrompts, seriesRootProblem } from './series.mjs';
import { frozenPinsProblem, seriesTaskProblem } from './tasks.mjs';

const EXIT = Object.freeze({ done: 0, halt: 3, config: 4, failed: 5 });
const refuse = (message, exit = EXIT.config) => ({ exit, message });
const sleep = (ms, signal) => new Promise(resolve => {
  if (signal?.aborted) { resolve(); return; }
  const done = () => { clearTimeout(timer); signal?.removeEventListener('abort', done); resolve(); };
  const timer = setTimeout(done, ms);
  signal?.addEventListener('abort', done, { once: true });
});

/** Whether a stop signal has come, and the answer that says the rescore ended on it with nothing changed (`where` says at which step). */
const stoppedBy = io => io.signal?.aborted === true;
const stopAnswer = (io, where) => ({ exit: io.abort?.exit ?? 130, message: `rescore stopped by ${io.abort?.reason ?? 'a stop signal'} ${where}: nothing was changed` });
const RETRY_MS = 5000;

/** The edges of a rescore that tests replace: how the run context is made, how evidence is collected, how a record is written, how a pair is finalized, how the wait sleeps. */
const seamsOf = io => ({ context: io.contextFn ?? runContext, collect: io.collectFn ?? collectAgain, record: io.recordFn ?? writeSessionRecord, finalize: io.finalizeFn ?? finalizePair, sleep: io.sleepFn ?? sleep });

const ANNOUNCE_EVERY = 6;
const RESCORABLE = 'void: contaminated';

function announceWait({ got, announced, say }) {
  if (announced % ANNOUNCE_EVERY === 0) say(`waiting for the run that holds the series: ${got.reason.slice(0, 160)}`);
}

/** Whether the wait for the series lock is over: its time is up, or a stop signal came. */
const waitOver = ({ until, signal }) => Date.now() >= until || signal?.aborted === true;

/** The series lock, waiting up to `waitMs` for a live holder to end (a stop signal ends the wait). -> `ok({ release })` | `fail(reason)`. */
async function lockWaiting({ seriesDir, waitMs, say, sleep: pause = sleep, signal }) {
  const until = Date.now() + waitMs;
  for (let announced = 0; ; announced += 1) {
    const got = acquireSeriesLock({ seriesDir });
    if (got.ok || waitOver({ until, signal })) return got;
    announceWait({ got, announced, say });
    await pause(RETRY_MS, signal);
  }
}

/** Why the entry itself cannot be rescored, or null: it must exist, have ended unscored, be a contamination void (the one class whose rule the quick run changed) and have kept its workspace. */
function entryProblem({ entry, layout, rid }) {
  if (entry === undefined) return `the run-map has no session ${rid}`;
  if (entry.status !== 'ran') return `session ${rid} is ${entry.status}: only a session that ended and was not scored can be rescored (a scored one is final; a launching one is settled by \`run\`'s resume)`;
  if (entry.class !== RESCORABLE) return `session ${rid} is ${entry.class}: only a ${RESCORABLE} session can be rescored (its class was decided by a rule the quick run changed; every other class stays as it was)`;
  return null;
}

const workspaceProblem = ({ layout, rid }) => (existsSync(layout.work) ? null : `session ${rid} has no workspace (${layout.work}): there is no tree to score`);

const sameCell = (a, b) => a.pair === b.pair && a.stack === b.stack && a.kind === b.kind;
const rivalOf = ({ map, entry, rid }) => Object.values(map.sessions).find(s => s.rid !== rid && sameCell(s, entry) && s.data === true);

/** Why this session cannot be rescored, or null: it must be an ended contamination void that kept its workspace and is not beaten to the cell by another data session. */
export function sessionProblem({ map, entry, layout, rid }) {
  const own = entryProblem({ entry, layout, rid }) ?? workspaceProblem({ layout, rid });
  if (own) return own;
  const rival = rivalOf({ map, entry, rid });
  return rival ? `session ${rival.rid} already holds the data of this cell (pair ${entry.pair}, ${entry.stack}): rescoring ${rid} would give the cell two data sessions` : null;
}

/** The series' own held-out set, checked against the hash recorded at `init` (a quick task) -> `ok(heldout)` | `fail`. */
async function seriesHeldout({ home, seriesDir, task }) {
  const held = loadHeldout(home, task);
  const problem = held.ok && isQuick(task) ? await quickHeldoutProblem({ seriesDir, heldout: held.value, task }) : null;
  return problem ? fail(problem) : held;
}

/** A rehearsal's fixture cases. */
function fixtureHeldout({ seriesDir, task }) {
  const casesDir = path.join(seriesDir, 'rehearsal', 'fixture', 'cases');
  return existsSync(casesDir) ? ok({ task: task.id, casesDir, traps: [], rehearsal: true }) : fail(`${casesDir} is not there: this rehearsal series has no fixture`);
}

/** The held-out set the session is scored over: the series' own, or a rehearsal's fixture. */
export const heldoutOf = a => (isRehearsal(a.seriesDir) ? fixtureHeldout(a) : seriesHeldout(a));

/** The prompts frozen into the series; a rehearsal series froze none (it never sends one), so it gets the quick prompts as `init` would have made them. */
function promptsOf({ seriesDir, home }) {
  const frozen = seriesPrompts(seriesDir);
  if (frozen.ok || !isRehearsal(seriesDir)) return frozen;
  const explore = loadExplore();
  return explore.ok ? ok(quickPrompts(quickExplore(explore.value, home))) : explore;
}

/** What a rescore needs: the limits, the held-out set, the world, the token, the frozen prompts. -> `ok({ calibration, heldout, world, token, prompts })` | `fail`. */
async function prerequisites({ io, seriesDir, task }) {
  const home = io.home;
  const got = await allOk([() => calibrationFor({ home, seriesDir, task }), () => heldoutOf({ home, seriesDir, task }), () => io.worldFn({ home, work: path.join(seriesDir, 'private'), task }), () => io.tokenFn(home), () => promptsOf({ seriesDir, home })]);
  if (!got.ok) return got;
  const [calibration, heldout, world, token, prompts] = got.value;
  const pins = isRehearsal(seriesDir) ? ok(null) : pinsOk({ explore: world.explore, task, home, snapshots: world.snapshots });
  return pins.ok ? ok({ calibration, heldout, world, token, prompts }) : pins;
}

const reasonOf = (entry, now) => `re-collected from the session's own evidence under the quick-run rule (a relative walk in a directory the scan lost is a low hit, not contamination); it was ${entry.class}`
  + `, it is ${now.class}`;

/** The run-map entry as the rescore leaves it: the new class and data, and the old ones appended to `reclassifiedFrom`. */
export const reclassifiedEntry = (entry, now, at) => Object.freeze({
  ...entry, class: now.class, data: now.data, reclassifiedFrom: [...(entry.reclassifiedFrom ?? []), { class: entry.class, data: entry.data, reason: reasonOf(entry, now), at }],
});

/** The ledger line of a rescore: an `event`, never a charge, so the spend of the ledger is untouched. */
export const rescoreEvent = ({ entry, now, at }) => Object.freeze({ event: 'rescore', rid: entry.rid, from: entry.class, to: now.class, reason: reasonOf(entry, now), t: at });

/** The three writes after a good scoring: the run-map (the entry reclassified, then `scored`), the ledger event, the records. -> the session's record. */
function writeAll({ ctx, map, entry, now, score, layout, at, seams }) {
  const ranMap = Object.freeze({ ...map, sessions: Object.freeze({ ...map.sessions, [entry.rid]: reclassifiedEntry(entry, now, at) }) });
  const scored = transition(ranMap, entry.rid, 'scored', { scoreFlags: [] });
  if (!scored.ok) throw new Error(scored.reason);
  writeRunMap(ctx.runMapFile, scored.value);
  appendLedger(ctx.ledgerFile, rescoreEvent({ entry, now, at }));
  const written = seams.record({ ctx, entry: scored.value.sessions[entry.rid], score });
  if (!written.ok) throw new Error(`the record could not be written: ${written.reason}`);
  return { map: scored.value, layout };
}

const dataMark = x => (x.data ? ' (data)' : '');

/** The one line saying what the evidence reads now. */
const evidenceLine = (entry, now) => `${entry.rid}: was ${entry.class}${dataMark(entry)}; the evidence reads ${now.class}${dataMark(now)} under the current rule${now.flags.length > 0 ? `; flags: ${now.flags.join('; ')}` : ''}`;

/** `ok(now)` when the evidence reads data where the session was not, else the refusal that says what it is. */
function becameData({ entry, now }) {
  if (now.data !== true) return fail(`${entry.rid} stays ${now.class}: nothing was changed`, { unchanged: true });
  return entry.data === true ? fail(`${entry.rid} is already data`, { unchanged: true }) : ok(now);
}

/** Collect the evidence again and decide: `ok(outcome)` when the session is data now, else the refusal that says what it is. */
function reclassify({ ctx, entry, layout, say, seams }) {
  const collected = seams.collect({ ctx, entry, layout });
  if (!collected.ok) return fail(`the evidence of ${entry.rid} cannot be collected: ${collected.reason}`);
  say(evidenceLine(entry, collected.value.outcome));
  return becameData({ entry, now: collected.value.outcome });
}

/** The context of the rescore's collect. -> `ok(ctx)` | `fail`. */
function contextFor({ need, io, series, seriesDir, entry, seams }) {
  const { calibration, world, token, prompts } = need;
  return seams.context({ world, calibration, series: series.state, seriesDir, kind: entry.kind, pair: entry.pair, token, claudeBinary: world.paths.claudeBinary, fill: frozenFill(world), planOver: {}, abort: { reason: null, exit: 130 }, home: io.home, prompts });
}

/** Score the kept tree (nothing is written yet). -> the scoring dep's answer. */
const scoreKept = ({ need, io, entry, layout, drift }) => scoreSessionDep({ world: need.world, calibration: need.calibration, heldout: need.heldout, home: io.home, say: io.say, scoreExtra: drift })({ session: layout.dir, source: layout.work, stack: entry.stack, nugetCache: layout.nuget, npmCache: layout.npm });

/** The three writes and the pair's records, after a good scoring. -> `{ exit, message, pairFile }`. */
function recordRescore({ io, ctx, series, seriesDir, entry, layout, now, score, seams }) {
  const at = (io.now ?? systemClock)().toISOString();
  const done = writeAll({ ctx, map: series.map, entry, now, score, layout, at, seams });
  const fin = seams.finalize({ seriesDir, map: done.map, pair: entry.pair, kinds: [entry.kind] });
  fin.problems.forEach(p => io.say(`record problem: ${p}`));
  return { exit: fin.problems.length === 0 ? EXIT.done : EXIT.failed, message: `${entry.rid} is ${now.class} (data) and scored; was ${entry.class}; the ledger's spend is unchanged`, pairFile: fin.pairFile };
}

/** The answer when the evidence did not turn the session into data: unchanged is a success, anything else a halt. */
const notChanged = now => (now.unchanged ? { exit: EXIT.done, message: now.reason } : refuse(now.reason, EXIT.halt));

/** What a rescore needs before it looks at the evidence: the pins of the series still the task's, the prerequisites and the run context. -> `ok({ need, ctx })` | `fail`. */
async function gather({ io, task, series, seriesDir, entry, seams }) {
  const moved = pinsProblem({ seriesDir, series, task });
  if (moved) return fail(moved);
  const need = await prerequisites({ io, seriesDir, task });
  if (!need.ok) return need;
  const ctx = await contextFor({ need: need.value, io, series, seriesDir, entry, seams });
  return ctx.ok ? ok({ need: need.value, ctx: ctx.value }) : ctx;
}

/** The refusal of a series whose frozen pins are not the task's pins now (an amendment since `init`): the bytes of today are not the bytes the session was made under. */
function pinsProblem({ seriesDir, series, task }) {
  const inputs = seriesInputs(seriesDir);
  return inputs.ok ? frozenPinsProblem({ inputs: inputs.value, task, seriesId: series.state.seriesId }) : inputs.reason;
}

async function rescoreLocked({ io, task, series, seriesDir, entry, layout }) {
  const seams = seamsOf(io);
  const got = await gather({ io, task, series, seriesDir, entry, seams });
  if (!got.ok) return refuse(got.reason);
  const now = reclassify({ ctx: got.value.ctx, entry, layout, say: io.say, seams });
  if (!now.ok) return notChanged(now);
  const scored = await scoreStep({ need: got.value.need, io, entry, layout, seriesDir });
  if (scored.answer) return scored.answer;
  return recordRescore({ io, ctx: got.value.ctx, series, seriesDir, entry, layout, now: now.value, score: scored.score, seams });
}

/** The scoring of the kept tree. -> `{ score }`, or `{ answer }` when the rescore ends here: a stop signal (checked after the scoring, before any write) or a harness failure. */
async function scoreStep({ need, io, entry, layout, seriesDir }) {
  const inputs = seriesInputs(seriesDir).value;
  const drift = { driftPossible: runnerDriftPossible({ inputs }), templateDriftPossible: templateDriftPossible({ inputs }) };
  const scored = await scoreKept({ need, io, entry, layout, drift });
  if (stoppedBy(io)) return { answer: stopAnswer(io, 'during the scoring') };
  if (scored.kind !== 'scored') return { answer: refuse(`scoring ${entry.rid}: ${scored.kind}: ${scored.reason}; nothing was changed (a harness failure is a void, never a verdict)`, EXIT.halt) };
  return { score: scored.score };
}

/** The refusal of a series that cannot be rescored as asked, or null: unreadable, or of another task. */
const seriesProblem = ({ task, series }) => (series.ok ? seriesTaskProblem({ state: series.value.state, task }) : series.reason);

/** The refusal of the series and the session, read AFTER the lock is held (the run-map a waiting rescore read before the wait would be stale). -> `{ refusal }` | `{ series, entry, layout }`. */
function checked({ task, seriesDir, rid }) {
  const series = loadSeries(seriesDir);
  const layout = layoutPaths(path.join(seriesDir, 's'), rid);
  const entry = series.ok ? series.value.map.sessions[rid] : undefined;
  const problem = seriesProblem({ task, series }) ?? sessionProblem({ map: series.value.map, entry, layout, rid });
  return problem ? { refusal: problem } : { series: series.value, entry, layout };
}

/** The answer when the series lock was not got: the stop signal that ended the wait, or the holder's refusal. */
const lockRefusal = (io, lock) => (stoppedBy(io) ? stopAnswer(io, 'while waiting for the series') : refuse(lock.reason));

/**
 * Rescore session `rid` of the series in `seriesDir` (a series of `task`). `io` = `{ home, say, worldFn, tokenFn, now?, contextFn?, collectFn?, recordFn?, finalizeFn?, sleepFn? }` (the last five are for tests).
 * The series lock is taken first (`waitMs` waits for a live holder), and only then are the run-map and the session read, so nothing a run wrote meanwhile is lost to a stale copy.
 * -> `{ exit, message, pairFile? }`: 0 done (or unchanged), 3 a halt (scoring failed, the evidence cannot be read; nothing changed), 4 a refusal, 5 the records have a problem.
 */
export async function rescore({ task, seriesDir, rid, io, waitMs = 0 }) {
  const outside = seriesRootProblem(io.home, seriesDir);
  if (outside) return refuse(outside);
  const lock = await lockWaiting({ seriesDir, waitMs, say: io.say, sleep: seamsOf(io).sleep, signal: io.signal });
  if (!lock.ok) return lockRefusal(io, lock);
  try {
    const now = checked({ task, seriesDir, rid });
    return now.refusal ? refuse(now.refusal) : await rescoreLocked({ io, task, ...now, seriesDir });
  } finally {
    lock.value.release();
  }
}
