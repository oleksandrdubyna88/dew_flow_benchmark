// The records of a finished unit (F3.S1 (a), PR 3, item 8): every session's record gains its `explore` section and, in Stage 1, its arm, the model the alias record resolved, its task, its round and its kind (`records.mjs`); a data slot's pair
// record gains the slot's task, round, masks and the owner's go line it was launched under. The A/A and the pilot have no pair record. `run.mjs`'s `finalizePair` does the work, narrowed to the unit's own sessions.
import { ARM } from './stage-unit.mjs';
import { finalizePair } from './run.mjs';

/** What the records of a unit gain: `of(entry)` for each session, `pairExtra` for the pair record (`null` for a unit that has none). */
export function stageOf({ unit, go, alias }) {
  const slot = unit.kind === 'data' ? { slot: unit.pair, task: unit.task, round: unit.round, masks: unit.slot.masks, ownerGo: go ? { date: go.date, scope: go.scope, words: go.words } : null } : null;
  return { of: e => ({ arm: ARM, task: e.task, round: e.round ?? null, kind: e.kind, alias }), pairExtra: slot === null ? null : { stage: slot } };
}

/** The `finalize` of a unit, as `loopAndFinalize` calls it (`{ seriesDir, map, pair, pause }`). */
export const finalizerOf = ({ unit, go, alias }) => args => finalizePair({ ...args, kinds: [unit.kind], only: unit.belongs, stage: stageOf({ unit, go, alias }) });
