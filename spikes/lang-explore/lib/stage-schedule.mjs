// The schedule of Stage 1 (F3.S1 (a), PR 2; stories document `todo/PLAN_lang_comparison_v2_E9_F3.md` section 5): which of the 30 slots (five rounds, each task once a round) runs when, on which CPU masks, under which rids and with which stack first.
// PURE: nothing here reads a file, the clock or the environment. The caller hands in the seed (a data slot exists only after the seed is revealed), the run-map and the dated lines it parsed; the answer is a value.
// A slot's number is the run-map's `pair`, so v1's pair files and every reader of `pair` work unchanged.
import { sha256Of } from '../../minigit-bench/lib/inputs.mjs';
import { MAX_ATTEMPTS, MASKS, STACK_ORDER } from './series.mjs';
import { FREEZE_TASKS } from './workspaces-sources.mjs';

export const STAGE_ROUNDS = 5;
const ATTEMPTS = Array.from({ length: MAX_ATTEMPTS }, (_, i) => i + 1);

const digest = text => sha256Of(text);
/** A seeded bit: the parity of the sixteenth hex digit of the sha256, which is v1's `bitOf` (the low bit of the first eight bytes). */
const bitOf = text => Number.parseInt(digest(text)[15], 16) % 2 === 1;

const taskIndex = task => FREEZE_TASKS.indexOf(task);

const inRounds = round => Number.isInteger(round) && round >= 1 && round <= STAGE_ROUNDS;

function checkedCell({ round, task }) {
  if (!inRounds(round)) throw new Error(`round ${round}: the rounds are 1 to ${STAGE_ROUNDS}`);
  if (taskIndex(task) < 0) throw new Error(`task ${task}: the tasks of Stage 1 are ${FREEZE_TASKS.join(', ')}`);
}

/**
 * The CPU mask of a stack in a slot: the parent's rule, never read from the seed. With k = 0 for 04 ... 5 for 10, in round r the C# session of task k runs on mask A (`0-5`) when r + k is odd and on mask B (`8-13`) when it is
 * even; the Node session takes the other mask.
 */
export function maskOfSlot({ round, task, stack }) {
  checkedCell({ round, task });
  if (!STACK_ORDER.includes(stack)) throw new Error(`stack ${stack}: the stacks are ${STACK_ORDER.join(', ')}`);
  const csharpLow = (round + taskIndex(task)) % 2 === 1;
  return (stack === 'csharp') === csharpLow ? MASKS.low : MASKS.high;
}

/** The rid of a data session: the first six hex digits of `sha256('<seed>:bare:t<task>:<stack>:r<round>:a<attempt>')` (parent 4.4 without a group). */
export const ridOfSlot = ({ seed, task, stack, round, attempt }) => digest(`${seed}:bare:t${task}:${stack}:r${round}:a${attempt}`).slice(0, 6);

/** Which stack is launched and scored first in a slot: a seeded bit with the task and the round in its text, as v1's `firstStack`. */
export const firstStackOfSlot = ({ seed, round, task }) => STACK_ORDER[bitOf(`${seed}:lang-v2:r${round}:t${task}:first`) ? 1 : 0];

/** The tasks of round `round` in their order: sorted by the sha256 of `<seed>:lang-v2:r<round>:t<task>:order`. */
const orderOfRound = ({ seed, round }) => FREEZE_TASKS.map(task => [digest(`${seed}:lang-v2:r${round}:t${task}:order`), task]).sort(([a], [b]) => a.localeCompare(b)).map(([, task]) => task);

const ridsOf = ({ seed, task, round }) => Object.fromEntries(STACK_ORDER.map(stack => [stack, ATTEMPTS.map(attempt => ridOfSlot({ seed, task, stack, round, attempt }))]));

const slotOf = ({ seed, round, task, position }) => ({
  slot: (round - 1) * FREEZE_TASKS.length + position + 1, round, task,
  masks: Object.fromEntries(STACK_ORDER.map(stack => [stack, maskOfSlot({ round, task, stack })])),
  first: firstStackOfSlot({ seed, round, task }), rids: ridsOf({ seed, task, round }),
});

/** The 30 slots in the order they run: `{ slot, round, task, masks: { csharp, node }, first, rids: { csharp: [a1, a2], node: [a1, a2] } }`. */
export const slotsOf = ({ seed }) => Array.from({ length: STAGE_ROUNDS }, (_, i) => i + 1)
  .flatMap(round => orderOfRound({ seed, round }).map((task, position) => slotOf({ seed, round, task, position })));

/** The first two sessions of a seed that share a rid: `[rid, nameOfFirst, nameOfSecond]`, or null. */
function collision(seed) {
  const seen = new Map();
  for (const [name, rid] of ridsOfSeed(seed)) {
    if (seen.has(rid)) return [rid, seen.get(rid), name];
    seen.set(rid, name);
  }
  return null;
}

/** The sentence refusing a seed, or null: not a text, or two of its 120 possible rids (six tasks, two stacks, five rounds, two attempts) are equal. It is asked when the seed is drawn, before its hash is committed. */
export function seedProblem(seed) {
  if (typeof seed !== 'string' || seed === '') return 'the seed is not a non-empty text';
  const found = collision(seed);
  return found === null ? null : `rid ${found[0]} is the rid of both ${found[1]} and ${found[2]}: draw another seed`;
}

const ridsOfSeed = seed => FREEZE_TASKS.flatMap(task => STACK_ORDER.flatMap(stack => Array.from({ length: STAGE_ROUNDS }, (_, i) => i + 1).flatMap(round => ATTEMPTS.map(attempt => [`task ${task} ${stack} round ${round} attempt ${attempt}`, ridOfSlot({ seed, task, stack, round, attempt })]))));
