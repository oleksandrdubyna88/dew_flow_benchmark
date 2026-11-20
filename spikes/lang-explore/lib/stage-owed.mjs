// The records an ENDED unit still owes (F3.S1 (a), PR 3, Opus review finding B). `runUnit` writes a unit's records (the facts of its sessions, the `explore` section of their records, a slot's pair record) in `finalizerOf`, after `runLoop`. A process that
// dies between the last scoring and that call leaves a unit that the schedule sees as finished (`pairComplete`), so the records would never be written. `stage-run` therefore finishes them FIRST, with the same finalizer, before it offers any unit: free,
// from the evidence the sessions kept (no session, no scoring, no charge); the ledger gets one `finished after interruption` event, which is no charge. Pure but for the files it reads; `finishOwedRecords` writes through `finalizerOf`.
import { existsSync } from 'node:fs';
import path from 'node:path';
import { appendLedger, readLedger } from '../../minigit-bench/lib/ledger.mjs';
import { finishedEvent } from './finish.mjs';
import { systemClock } from './clock.mjs';
import { prerequisitesOf } from './run.mjs';
import { DATA_KIND } from './stage-schedule.mjs';
import { ledgerFileOf, isOpenSession } from './series.mjs';
import { FACTS_FILE } from './stage-facts.mjs';
import { finalizerOf, hasEnded } from './stage-final.mjs';
import { unitOfSession } from './stage-unit.mjs';
import { taskFromValues } from './tasks.mjs';

const sessionDir = (dir, session) => path.join(dir, 's', session.rid);
const pairFile = (dir, pair) => path.join(dir, 'pairs', `p${pair}.json`);

/** An ended session whose directory is there and holds no facts file: the finalizer has not run over it. */
const lacksFacts = (dir, session) => hasEnded(session) && existsSync(sessionDir(dir, session)) && !existsSync(path.join(sessionDir(dir, session), FACTS_FILE));

/** A unit with nothing open and an ended session. */
const idle = mine => !mine.some(isOpenSession) && mine.some(hasEnded);

/** Does the unit owe records: an ended session lacks its facts, or a data slot lacks its pair record. */
const lacksRecords = ({ dir, unit, mine }) => mine.some(s => lacksFacts(dir, s)) || (unit.kind === DATA_KIND && !existsSync(pairFile(dir, unit.pair)));

const owes = ({ dir, unit, map }) => { const mine = Object.values(map.sessions).filter(unit.belongs); return idle(mine) && lacksRecords({ dir, unit, mine }); };

/** The units that owe records, one per slot / A/A / pilot pair, in the run-map's order: `[unit]`. */
export function owedUnits({ dir, state, map, gates }) {
  const units = Object.values(map.sessions).map(session => unitOfSession({ session, state, gates }));
  const first = units.filter((u, i) => units.findIndex(v => v.what === u.what && v.pair === u.pair && v.task === u.task) === i);
  return first.filter(unit => owes({ dir, unit, map }));
}

/** The owner's go line the slot was launched under, as its `owner-go` event recorded it (`null` for the A/A, the pilot and a rehearsal). */
function goOf({ dir, unit }) {
  const event = unit.kind === DATA_KIND ? readLedger(ledgerFileOf(dir)).filter(l => l.event === 'owner-go' && l.pair === unit.pair).at(-1) : undefined;
  return event === undefined ? null : { date: event.date, scope: event.scope, words: event.words };
}

/** The records of one owed unit, written by the unit's own finalizer over the run-map as it is, and the one event. The problems of the records are said, as `loopAndFinalize` leaves them (the exit is not theirs). -> `null`, or the refusal `{ exit, message }`. */
async function finishOne({ series, unit, io }) {
  const chosen = taskFromValues({ task: unit.task }, io.exploreFn);
  if (!chosen.ok) return { exit: 4, message: chosen.reason };
  const need = await prerequisitesOf({ series, io, task: chosen.value.task });
  if (!need.ok) return { exit: 4, message: `the records of ${unit.what} (pair ${unit.pair}) are owed and cannot be finished: ${need.reason}` };
  const done = finalizerOf({ unit, go: goOf({ dir: series.dir, unit }), alias: chosen.value.explore.cli.model })({ seriesDir: series.dir, map: series.map, pair: unit.pair, pause: null, world: need.value.world });
  appendLedger(ledgerFileOf(series.dir), finishedEvent({ pair: unit.pair, at: (io.now ?? systemClock)().toISOString() }));
  done.problems.forEach(p => io.say(`record problem (${unit.what}, pair ${unit.pair}): ${p}`));
  return null;
}

/** Finish the records every ended unit owes, one after the other. -> `null` when nothing was owed or all was written; else the first refusal. */
export async function finishOwedRecords({ series, read, io }) {
  for (const unit of owedUnits({ dir: series.dir, state: series.state, map: series.map, gates: read.gates })) {
    const refusal = await finishOne({ series, unit, io });
    if (refusal) return refusal;
  }
  return null;
}
