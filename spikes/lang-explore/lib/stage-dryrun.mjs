// `stage-run --dry-run` (F3.S1 (a), PR 3): what the five rounds are, printed and claimed for nothing: no file is written, no session planned, no token read, no charge. The masks are the parent's rule and never the seed's, so they are printed
// before the seed is revealed; the order of the tasks inside a round comes from the seed, so before the reveal the line says it is not known yet. Pure: lines out.
import { STACK_NAMES } from './plan.mjs';
import { MASKS, STACK_ORDER } from './series.mjs';
import { STAGE_ROUNDS, maskOfSlot, slotsOf } from './stage-schedule.mjs';
import { FREEZE_TASKS } from './workspaces-sources.mjs';

const maskName = mask => (mask === MASKS.low ? `A (${mask})` : `B (${mask})`);
const cellOf = (round, task) => `task ${task}: ${STACK_ORDER.map(stack => `${STACK_NAMES[stack]} ${maskName(maskOfSlot({ round, task, stack }).value)}`).join(', ')}`;

/** The tasks of a round in the order they run when the seed is known, else in the freeze's order. */
const tasksOf = ({ seed, round }) => (seed === null ? [...FREEZE_TASKS] : slotsOf({ seed }).filter(s => s.round === round).map(s => s.task));

const roundLines = ({ seed, round }) => [`round ${round}${seed === null ? ' (the order of its tasks is not known yet)' : ''}`, ...tasksOf({ seed, round }).map(task => `  ${cellOf(round, task)}`)];

const minutes = ms => `${ms / 60_000} min`;

/** The lines of a dry run: the cap, the wall and the stall, the mask rule, and the five rounds. `seed` is the revealed seed or null; `caps` the series' (`{ capUsd, wallMs, stallMs }`). */
export function stageDryRun({ caps, seed }) {
  return [
    'dry run: nothing is launched, planned, written or charged',
    `per session: a cap of $${caps.capUsd}, a wall of ${minutes(caps.wallMs)}, a stall of ${minutes(caps.stallMs)}; no total ceiling and no pause point (D7, D53)`,
    `masks: A = ${MASKS.low}, B = ${MASKS.high}; in round r the C# session of the k-th task (04, 05, 06, 07, 08, 10 are k = 0 to 5) runs on A when r + k is odd, Node on the other; the seed does not decide a mask`,
    seed === null ? 'the seed is not revealed yet: the order of the tasks inside a round comes from it, so it is not printed' : 'the seed is revealed: the order of each round is the order printed',
    ...Array.from({ length: STAGE_ROUNDS }, (_, i) => roundLines({ seed, round: i + 1 })).flat(),
  ];
}
