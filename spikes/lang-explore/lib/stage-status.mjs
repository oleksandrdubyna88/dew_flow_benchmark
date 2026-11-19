// `stage-status` (F3.S1 (a), PR 3; FREE and read only): the rounds, the slots with their state, the A/A and the pilot, and the spend on five lines. The spend lines are DERIVED from the ledger and the run-map, never a ledger kind of their own:
// `pilot` and `A/A` are the charges of the sessions of those kinds at attempt 1, `data` and `void` those of the data sessions at attempt 1 (the ledger's own `data` and `void` kinds), and `re-run` the charges of every session of attempt 2
// (open point 14, answered 2026-11-19: the split of the five lines is registered). The five lines add up to what the ledger holds. Pure over the run-map and the ledger lines.
import { isOwnCharge } from '../../minigit-bench/lib/ledger.mjs';
import { STACK_ORDER, pairComplete, stackState } from './series.mjs';
import { DATA_KIND, STAGE_ROUNDS } from './stage-schedule.mjs';
import { FREEZE_TASKS } from './workspaces-sources.mjs';

export const SPEND_LINES = Object.freeze(['pilot', 'A/A', 'data', 'void', 're-run']);
const LINE_OF_KIND = Object.freeze({ pilot: 'pilot', aa: 'A/A', data: 'data', void: 'void' });

/** The spend line a charge belongs to: `re-run` for a session of attempt 2 (whatever its kind), else the line of its ledger kind; `null` for a charge of a kind the five lines do not hold (a prior or rehearsal charge). */
const lineOf = (map, charge) => (map.sessions[charge.rid]?.attempt === 2 ? 're-run' : (LINE_OF_KIND[charge.kind] ?? null));

/** The spend on the five lines and the total the ledger holds: `{ lines: { pilot, 'A/A', data, void, 're-run' }, other, total }` (`other` is what no line holds; zero for a Stage 1 series). */
export function spendLines({ map, ledger }) {
  const lines = Object.fromEntries(SPEND_LINES.map(name => [name, 0]));
  const charges = ledger.filter(isOwnCharge);
  const other = charges.filter(c => lineOf(map, c) === null).reduce((sum, c) => sum + c.usd, 0);
  for (const charge of charges.filter(c => lineOf(map, c) !== null)) lines[lineOf(map, charge)] += charge.usd;
  return { lines, other, total: charges.reduce((sum, c) => sum + c.usd, 0) };
}

const usd = n => `$${n.toFixed(4)}`;

const statesOf = (map, slot) => STACK_ORDER.map(stack => `${stack} ${stackState(map, slot.slot, stack, DATA_KIND)}`).join(', ');
const slotLine = (map, slot) => `  slot ${slot.slot} (task ${slot.task}): ${pairComplete(map, slot.slot, DATA_KIND) ? 'finished' : statesOf(map, slot)}`;

/** The data slots of the run-map, from its sessions (a slot is known once it was planned): `[{ slot, round, task }]` in slot order. */
function slotsIn(map) {
  const seen = new Map(Object.values(map.sessions).filter(s => s.kind === DATA_KIND).map(s => [s.pair, { slot: s.pair, round: s.round, task: s.task }]));
  return [...seen.values()].toSorted((a, b) => a.slot - b.slot);
}

const countLine = (map, mine) => (mine.length === 0 ? 'not started' : `${mine.filter(s => pairComplete(map, s.slot, DATA_KIND)).length} of ${FREEZE_TASKS.length} slots finished`);

const roundLines = (map, slots, round) => {
  const mine = slots.filter(s => s.round === round);
  return [`round ${round}: ${countLine(map, mine)}`, ...mine.map(s => slotLine(map, s))];
};

const pilotLine = ([name, sessions]) => `  ${name}: ${sessions.map(s => `${s.twin ?? s.stack} a${s.attempt} ${s.status}${s.class ? ` (${s.class})` : ''}`).join(', ')}`;

/** The pilot's sessions by unit (`aa`, `pilot:<task>`). */
const pilotUnits = map => Object.entries(Object.groupBy(Object.values(map.sessions).filter(s => s.kind !== DATA_KIND), s => (s.kind === 'aa' ? 'aa' : `pilot:${s.task}`)));

const otherText = spend => (spend.other > 0 ? `, other ${usd(spend.other)}` : '');

/** The lines of `stage-status`: the series, the stop, the pilot, the five rounds, the five spend lines. */
export function stageStatusLines({ state, map, ledger }) {
  const spend = spendLines({ map, ledger });
  const slots = slotsIn(map);
  const units = pilotUnits(map);
  return [
    `series ${state.seriesId}: Stage ${state.stage}, arm ${state.arm}, tasks ${state.tasks.join(' ')}${map.stopped ? `, STOPPED: ${map.stopped.rule}` : ''}`,
    `pilot and A/A: ${units.length === 0 ? 'not started' : ''}`.trimEnd(), ...units.map(pilotLine),
    ...Array.from({ length: STAGE_ROUNDS }, (_, i) => roundLines(map, slots, i + 1)).flat(),
    `spend (${usd(spend.total)} in all): ${SPEND_LINES.map(name => `${name} ${usd(spend.lines[name])}`).join(', ')}${otherText(spend)}`,
  ];
}
