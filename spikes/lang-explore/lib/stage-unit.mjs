// What one `stage-run` runs (F3.S1 (a), PR 3): the UNIT, chosen by the schedule (`stage-schedule.mjs`), never by a flag that names a data pair. `--what next` is the schedule's next slot (a void's one re-run included, alone, before any later
// slot); `--what aa` and `--what pilot:<NN>` are steps of the pilot, which must be the step the pilot's own order offers. A unit knows which run-map sessions are its own, how its sessions are planned (the run-map entries), and the step function the
// loop asks (`ctx.stepOf`): launch what is planned of the unit, then score what ended with data. Pure over values.
import { randomBytes } from 'node:crypto';
import { pilotRid } from '../../minigit-bench/lib/bare-plan.mjs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { planSession } from '../../minigit-bench/lib/run-map.mjs';
import { STACK_ORDER } from './series.mjs';
import { DATA_KIND, nextSlot, pilotStep, slotsOf } from './stage-schedule.mjs';

export const ARM = 'bare';
/** The `pair` of every session of the A/A and the pilot (v1's own number for its fixed lists); a data slot's is its number 1 to 30. */
export const PILOT_PAIR = 0;

const WHAT = /^pilot:(\d\d)$/;

/** `--what` -> `ok('aa' | 'next' | 'pilot:<NN>')` | `fail` naming the legal values. */
export const whatOf = text => (['aa', 'next'].includes(text) || WHAT.test(text ?? '') ? ok(text) : fail(`--what is aa, pilot:<NN> or next (got ${text === undefined ? 'nothing' : `'${text}'`}): no flag names another data pair, the schedule offers the next slot`));

const slotUnit = slot => ({
  what: 'next', kind: DATA_KIND, pair: slot.slot, task: slot.task, round: slot.round, slot, action: slot.action, todo: slot.todo, order: [slot.first, ...STACK_ORDER.filter(s => s !== slot.first)],
  belongs: s => s.kind === DATA_KIND && s.pair === slot.slot,
});

const isOf = (kind, task) => s => s.kind === kind && s.task === task;
const isAaOf = ({ aa }) => s => isOf('aa', aa.task)(s) && s.stack === aa.stack;
const pilotBelongs = ({ what, pilot }) => (what === 'aa' ? isAaOf(pilot) : isOf('pilot', what.slice('pilot:'.length)));

/** A step of the pilot as a unit: the A/A's twins (`a` before `b`) or a pilot pair (the stacks in order); all its sessions carry pair 0. */
const pilotUnit = ({ step, pilot }) => ({
  what: step.what, kind: step.what === 'aa' ? 'aa' : 'pilot', pair: PILOT_PAIR, task: step.what === 'aa' ? pilot.aa.task : step.what.slice('pilot:'.length), round: null, action: step.action, todo: step.todo,
  order: step.what === 'aa' ? ['a', 'b'] : [...STACK_ORDER], belongs: pilotBelongs({ what: step.what, pilot }),
});

const unitOfSlot = next => (next.value ? ok(slotUnit(next.value)) : ok(null));

/** The slot the schedule offers, as a unit: `ok(unit)` | `ok(null)` when the series is done | `fail(sentence)`. */
function nextUnit({ map, gates }) {
  const slots = gates.seedRevealed ? slotsOf({ seed: gates.seed }) : [];
  const next = nextSlot({ map, slots, pilotGo: gates.pilotGo, seedRevealed: gates.seedRevealed, readbackLines: gates.readbackLines });
  return next.ok ? unitOfSlot(next) : next;
}

/** The pilot's step, as a unit, when it is the one `what` names. */
function pilotUnitOf({ what, state, map }) {
  const step = pilotStep({ map, pilot: state.pilot });
  if (!step.ok) return step;
  if (step.value === null) return ok(null);
  return step.value.what === what ? ok(pilotUnit({ step: step.value, pilot: state.pilot })) : fail(`--what ${what}: the pilot's next step is ${step.value.what} (the A/A, the daemon pair and the web pair run in that order, each finished before the next)`);
}

/**
 * The unit `what` names, from the run-map and the gates (`{ seed, seedRevealed, pilotGo, readbackLines }`): `ok(unit)` | `ok(null)` when nothing more is to run | `fail(sentence)` (a stopped series, a gate, a step that is not the next one).
 * A unit is `{ what, kind, pair, task, round, action, todo, order, belongs }` (a slot's also `slot`).
 */
export function unitOf({ what, state, map, gates }) {
  return what === 'next' ? nextUnit({ map, gates }) : pilotUnitOf({ what, state, map });
}

const plannedEntry = ({ unit, entry, rand }) => ({
  rid: unit.kind === DATA_KIND ? entry.rid : pilotRid(rand()), pair: unit.pair, stack: entry.stack, phase: 'v1', attempt: entry.attempt, kind: unit.kind, label: entry.label, mask: entry.mask, task: unit.task, round: unit.round, arm: ARM,
  ...(entry.twin === undefined ? {} : { twin: entry.twin }),
});

const collisionSentence = (entry, reason) => `${reason}: the rid ${entry.rid} is already in the run-map, so nothing was planned${entry.kind === DATA_KIND ? ' (a data rid comes from the seed and cannot be drawn again: the schedule names it for this slot, so a session of this series already holds it)' : ' (a pilot rid is drawn at random; run the command again to draw another)'}`;

/**
 * The unit's plan: the run-map with every `plan` step of its todo planned (data sessions under the schedule's rids, pilot and A/A sessions under a drawn `pilotRid`), and what was planned or resumed, in the shape `commitPlan` and `settleOpen`
 * of `run.mjs` take. A rid that is already in the run-map fails closed (`planSession`): nothing is planned twice. -> `ok({ map, planned, resumed? })` | `fail`.
 */
export function planUnit({ map, unit, rand = () => randomBytes(4).toString('hex') }) {
  let current = map;
  const planned = [];
  for (const entry of unit.todo.filter(e => e.step === 'plan')) {
    const made = plannedEntry({ unit, entry, rand });
    const next = planSession(current, made);
    if (!next.ok) return fail(collisionSentence(made, next.reason));
    current = next.value;
    planned.push(made);
  }
  const resumed = unit.todo.filter(e => e.step !== 'plan').map(e => e.rid);
  return ok({ map: current, planned, ...(resumed.length > 0 ? { resumed } : {}) });
}

const refOf = s => ({ stack: s.stack, phase: s.phase, attempt: s.attempt, rid: s.rid });
const rank = (unit, s) => unit.order.indexOf(unit.kind === 'aa' ? s.twin : s.stack);
const inOrder = (unit, list) => list.toSorted((a, b) => rank(unit, a) - rank(unit, b));

/**
 * The loop's step for the unit (`ctx.stepOf`, v1's one seam): launch what is planned of THIS unit (the leading stack first), then score what ended with data (in the same order), then done. A session of another unit, a scored one and a void
 * are never in a step, so a scored attempt is never repeated and a void's re-run (a new session) is launched alone.
 */
export const stepOfUnit = unit => map => {
  const mine = Object.values(map.sessions).filter(unit.belongs);
  const planned = inOrder(unit, mine.filter(s => s.status === 'planned'));
  if (planned.length > 0) return { action: 'launch', phase: 'v1', sessions: planned.map(refOf), label: planned[0].label };
  const unscored = inOrder(unit, mine.filter(s => s.status === 'ran' && s.data === true));
  return unscored.length > 0 ? { action: 'score', sessions: unscored.map(refOf) } : { action: 'done' };
};
