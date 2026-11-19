// The records of a finished unit (F3.S1 (a), PR 3, item 8): every session's record gains its `explore` section and, in Stage 1, its arm, the model the alias record resolved, its task, its round and its kind (`records.mjs`); a data slot's pair
// record gains the slot's task, round, masks and the owner's go line it was launched under. The A/A and the pilot have no pair record. `run.mjs`'s `finalizePair` does the work, narrowed to the unit's own sessions.
import { existsSync } from 'node:fs';
import path from 'node:path';
import { finalizePair } from './run.mjs';
import { writeSessionFacts } from './stage-facts.mjs';
import { ARM } from './stage-unit.mjs';

/** What the records of a unit gain: `of(entry)` for each session, `pairExtra` for the pair record (`null` for a unit that has none). */
export function stageOf({ unit, go, alias }) {
  const slot = unit.kind === 'data' ? { slot: unit.pair, task: unit.task, round: unit.round, masks: unit.slot.masks, ownerGo: go ? { date: go.date, scope: go.scope, words: go.words } : null } : null;
  return { of: e => ({ arm: ARM, task: e.task, round: e.round ?? null, kind: e.kind, alias }), pairExtra: slot === null ? null : { stage: slot } };
}

/** A session that has ENDED: not `planned` and not `launching` (a halt in `launching` is reset to `planned` and launched again on resume, so its facts would keep the unknowns of the halted state). */
const hasEnded = session => !['planned', 'launching'].includes(session.status);

/** The facts every ended session of the unit stores for the readers after the tag (`stage-facts.mjs`), written once, with the final evidence, before the records. */
function storeFacts({ args, unit }) {
  const ended = Object.values(args.map.sessions).filter(s => unit.belongs(s) && hasEnded(s));
  for (const entry of ended.filter(s => existsSync(path.join(args.seriesDir, 's', s.rid)))) writeSessionFacts({ sessionDir: path.join(args.seriesDir, 's', entry.rid), entry, world: args.world });
}

/** The `finalize` of a unit, as `loopAndFinalize` calls it (`{ seriesDir, map, pair, pause, world }`). */
export const finalizerOf = ({ unit, go, alias }) => args => {
  storeFacts({ args, unit });
  return finalizePair({ ...args, kinds: [unit.kind], only: unit.belongs, stage: stageOf({ unit, go, alias }) });
};
