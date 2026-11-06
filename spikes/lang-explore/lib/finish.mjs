// A run that stopped between `scored` and `record.json` (or between the last record and the pair's record) is finished by the next `run` of that pair: FREE, from the kept `score.json` and the session's evidence, by the series'
// own code (`writeSessionRecord`, then the pair record). No session is launched, nothing is scored again and nothing is charged: the ledger gets one `event` line, which is no charge.
//   - It acts only on a pair with nothing open (a planned, launching or unscored session is the resume's business), whose scored session lacks its `record.json` or whose pair record is not there.
//   - A scored session with no kept `score.json` (ANY scored session of the pair, also one whose record is there) cannot be finished from evidence that is not there: the pair is refused, and nothing is written.
//   - Each record's event goes to the ledger as that record is written, so a finish that stops half way has still logged what it wrote.
//   - Any other state of a pair (finished, void, stopped) is refused as before.
import { existsSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { appendLedger } from '../../minigit-bench/lib/ledger.mjs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { writeSessionRecord } from '../../minigit-bench/lib/bare-records-io.mjs';
import { allOk } from './chain.mjs';
import { systemClock } from './clock.mjs';
import { calibrationFor } from './limits.mjs';
import { pinsOk } from './pins.mjs';
import { frozenFill, runContext } from './run-ctx.mjs';
import { openSessions, seriesPrompts, sessionsOf } from './series.mjs';

export const FINISH_EVENT = 'finished after interruption';
const REASON = 'the run stopped between scored and record.json; written from the kept evidence, no session, no scoring and no charge';

const sessionDirOf = (seriesDir, rid) => path.join(seriesDir, 's', rid);
const recordFileOf = (seriesDir, rid) => path.join(sessionDirOf(seriesDir, rid), 'record.json');
const scoreFileOf = (seriesDir, rid) => path.join(sessionDirOf(seriesDir, rid), 'score', 'score.json');
const pairFileOf = (seriesDir, pair) => path.join(seriesDir, 'pairs', `p${pair}.json`);

/** The ledger line of a finish: an `event`, never a charge (`rid` of the session whose record was written, else the `pair` whose record was). */
export const finishedEvent = ({ rid, pair, at }) => Object.freeze({ event: FINISH_EVENT, ...(rid === undefined ? { pair } : { rid }), reason: REASON, t: at });

/** The scored sessions without a record, and whether anything at all is lacking (those, or the pair's own record). */
function lackOf({ seriesDir, scored, pair }) {
  const missing = scored.filter(s => !existsSync(recordFileOf(seriesDir, s.rid)));
  return { missing, any: missing.length > 0 || !existsSync(pairFileOf(seriesDir, pair)) };
}

/**
 * What a pair that was interrupted lacks, or null when it is not such a pair: `{ missing: [entry], scored: [entry] }`, the scored sessions without a `record.json` (empty when only the pair's record is lacking) and all the scored sessions of the pair. A pair with nothing scored,
 * or with anything open, is not one.
 */
export function interruptedPair({ seriesDir, map, pair }) {
  const scored = sessionsOf(map, pair).filter(s => s.status === 'scored');
  if (scored.length === 0 || openSessions(map, pair).length > 0) return null;
  const lack = lackOf({ seriesDir, scored, pair });
  return lack.any ? { missing: lack.missing, scored } : null;
}

/** The sentence refusing the finish, or null: EVERY scored session of the pair needs its kept score, not only the ones that lack a record (the pair's records are finalized from the scores, and a missing score would replace a record's `explore` section with "not scored"). */
function evidenceProblem({ seriesDir, scored }) {
  const lacking = scored.filter(s => !existsSync(scoreFileOf(seriesDir, s.rid)));
  return lacking.length === 0 ? null : `${lacking.map(s => s.rid).join(', ')} ended scored but kept no score/score.json, so the pair cannot be finished from evidence: nothing was written`;
}

/** What a finish needs: the limits, the world, the token (the evidence scan looks for it), the frozen prompts, and the hash pins. -> `ok({ calibration, world, token, prompts })` | `fail`. */
async function needs({ io, series, task, pinsFn }) {
  const got = await allOk([() => calibrationFor({ home: io.home, seriesDir: series.dir, task }), () => io.worldFn({ home: io.home, work: path.join(series.dir, 'private'), task }), () => io.tokenFn(io.home), () => seriesPrompts(series.dir)]);
  if (!got.ok) return got;
  const [calibration, world, token, prompts] = got.value;
  const pins = pinsFn({ explore: world.explore, task, home: io.home, snapshots: world.snapshots });
  return pins.ok ? ok({ calibration, world, token, prompts }) : pins;
}

const scoreOf = (seriesDir, rid) => JSON.parse(readFileSync(scoreFileOf(seriesDir, rid), 'utf8'));

/** The edges a test replaces: the run context, the record writer, the pair finalizer, the hash pins. */
const seamsOf = (io, finalize) => ({ context: io.finishSeams?.contextFn ?? runContext, record: io.finishSeams?.recordFn ?? writeSessionRecord, finalize: io.finishSeams?.finalizeFn ?? finalize, pins: io.finishSeams?.pinsFn ?? pinsOk });

/** The records of the sessions that lack one, from their kept scores; `logged(rid)` runs as each record is written, so a failure later loses no event. -> `ok(rids written)` | `fail`. */
function writeRecords({ ctx, seriesDir, missing, record, logged }) {
  for (const entry of missing) {
    const written = record({ ctx, entry, score: scoreOf(seriesDir, entry.rid) });
    if (!written.ok) return fail(`the record of ${entry.rid} could not be written: ${written.reason}`);
    logged(entry.rid);
  }
  return ok(missing.map(s => s.rid));
}

/** The run context of the finish. -> `ok(ctx)` | `fail`. */
function contextFor({ need, series, pair, io, seams }) {
  const { calibration, world, token, prompts } = need;
  return seams.context({ world, calibration, series: series.state, seriesDir: series.dir, kind: 'pilot', pair, token, claudeBinary: world.paths.claudeBinary, fill: frozenFill(world), planOver: {}, abort: { reason: null, exit: 130 }, home: io.home, prompts });
}

/** The ledger event of a finish, written when its record is: `{ rid }` for a session record, `{ pair }` for the pair's own (written only when no session record was). */
const logEvent = ({ ledgerFile, who, at }) => appendLedger(ledgerFile, finishedEvent({ ...who, at }));

/** The pair record after the session records: written by the series' own finalizer, with its event when it is all that was lacking. -> the result. */
function finishPair({ series, pair, io, seams, written, log }) {
  const fin = seams.finalize({ seriesDir: series.dir, map: series.map, pair, kinds: ['pilot'] });
  if (written.length === 0) log({ pair });
  fin.problems.forEach(p => io.say(`record problem: ${p}`));
  return { exit: fin.problems.length === 0 ? 0 : 5, message: `pair ${pair} finished from the kept evidence (${written.length} record(s) and the pair record); no session, no scoring, no charge`, pairFile: fin.pairFile, problems: fin.problems };
}

/** Everything after the checks: the context, the records (each with its event as it is written), the pair record. -> the result. */
async function finishChecked({ series, pair, io, found, need, seams }) {
  const ctx = await contextFor({ need, series, pair, io, seams });
  if (!ctx.ok) return { exit: 4, message: ctx.reason };
  const log = who => logEvent({ ledgerFile: ctx.value.ledgerFile, who, at: (io.now ?? systemClock)().toISOString() });
  const written = writeRecords({ ctx: ctx.value, seriesDir: series.dir, missing: found.missing, record: seams.record, logged: rid => log({ rid }) });
  return written.ok ? finishPair({ series, pair, io, seams, written: written.value, log }) : { exit: 5, message: written.reason };
}

/**
 * Finish the interrupted pair `pair` of `series` (`found` is `interruptedPair`'s answer). `finalize` is `run.mjs`'s `finalizePair`. -> `{ exit, message, pairFile?, problems? }`: 0 done, 4 a refusal (nothing written), 5 the records have a problem.
 */
export async function finishInterrupted({ series, pair, task, io, found, finalize }) {
  const lacking = evidenceProblem({ seriesDir: series.dir, scored: found.scored });
  if (lacking) return { exit: 4, message: lacking };
  const seams = seamsOf(io, finalize);
  const need = await needs({ io, series, task, pinsFn: seams.pins });
  return need.ok ? finishChecked({ series, pair, io, found, need: need.value, seams }) : { exit: 4, message: need.reason };
}
