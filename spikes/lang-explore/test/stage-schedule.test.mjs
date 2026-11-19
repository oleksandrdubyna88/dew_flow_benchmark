// The schedule of Stage 1 (F3.S1 (a), PR 2): the order, the mask rule, the rids and the first stack of the 30 slots, the barrier and the round rule, the read-back gate and the pilot's plan. The module is pure, so every
// test hands it a seed and a run-map built here; nothing is read from disk but the pins `TASK_KINDS` is held against.
import assert from 'node:assert/strict';
import { test } from 'node:test';
import { sha256Of } from '../../minigit-bench/lib/inputs.mjs';
import { MASKS, maskOf } from '../lib/series.mjs';
import { STAGE_ROUNDS, firstStackOfSlot, maskOfSlot, ridOfSlot, seedProblem, slotsOf } from '../lib/stage-schedule.mjs';

const COLLIDING_SEED = 'lv2-collide-2902';
const TASKS = ['04', '05', '06', '07', '08', '10'];
const SEED = 'lv2-0123456789abcdef';
/** The mask table of the parent's rule, written out: the stack of C# on each mask for rounds 1 to 5, A = `0-5` and B = `8-13` (task k = 0 for 04, 1 for 05, ... 5 for 10; A when round + k is odd). */
const CSHARP_ON = { '04': 'ABABA', '05': 'BABAB', '06': 'ABABA', '07': 'BABAB', '08': 'ABABA', 10: 'BABAB' };
const MASK_OF = { A: MASKS.low, B: MASKS.high };
const other = mask => (mask === MASKS.low ? MASKS.high : MASKS.low);
const seedsOf = (prefix, n) => Array.from({ length: n }, (_, i) => `${prefix}${i}`);

test('Each_round_holds_every_task_once_and_the_order_inside_a_round_comes_from_the_seed', () => {
  const slots = slotsOf({ seed: SEED });
  assert.equal(slots.length, 30);
  assert.deepEqual(slots.map(s => s.slot), Array.from({ length: 30 }, (_, i) => i + 1), 'numbered 1 to 30 in the order they run');
  for (let round = 1; round <= STAGE_ROUNDS; round += 1) {
    const inRound = slots.filter(s => s.round === round);
    assert.deepEqual([...inRound.map(s => s.task)].sort(), TASKS, `round ${round} holds every task once`);
    const byHash = [...TASKS].sort((a, b) => sha256Of(`${SEED}:lang-v2:r${round}:t${a}:order`).localeCompare(sha256Of(`${SEED}:lang-v2:r${round}:t${b}:order`)));
    assert.deepEqual(inRound.map(s => s.task), byHash, `round ${round} is sorted by the sha256 of the seed, round and task`);
  }
  assert.deepEqual(slots.map(s => s.round), Array.from({ length: 30 }, (_, i) => Math.floor(i / 6) + 1), 'the rounds run one after the other');
});

test('The_same_seed_gives_the_same_30_slots_and_another_seed_gives_another_order', () => {
  assert.deepEqual(slotsOf({ seed: SEED }), slotsOf({ seed: SEED }));
  const orders = new Set(seedsOf('lv2-order-', 20).map(seed => slotsOf({ seed }).map(s => s.task).join()));
  assert.ok(orders.size > 15, `twenty seeds gave ${orders.size} distinct orders of the 30 slots`);
});

test('In_round_r_task_k_runs_C_sharp_on_mask_A_when_r_plus_k_is_odd_and_Node_takes_the_other_mask', () => {
  for (const task of TASKS) {
    for (let round = 1; round <= STAGE_ROUNDS; round += 1) {
      const csharp = MASK_OF[CSHARP_ON[task][round - 1]];
      assert.equal(maskOfSlot({ round, task, stack: 'csharp' }), csharp, `task ${task} round ${round}: C#`);
      assert.equal(maskOfSlot({ round, task, stack: 'node' }), other(csharp), `task ${task} round ${round}: Node takes the other`);
    }
  }
});

test('The_mask_of_a_slot_never_depends_on_the_seed', () => {
  const tables = new Set(seedsOf('lv2-mask-', 100).map(seed => JSON.stringify(slotsOf({ seed }).map(s => [s.round, s.task, s.masks]).sort())));
  assert.equal(tables.size, 1, 'a hundred seeds, one mask table');
  assert.equal(slotsOf({ seed: SEED }).length, 30, 'the table is the 30 cells');
  assert.equal(slotsOf({ seed: SEED }).every(s => s.masks.csharp === maskOfSlot({ round: s.round, task: s.task, stack: 'csharp' })), true);
});

test('Over_the_30_pairs_each_stack_holds_each_mask_exactly_15_times', () => {
  const slots = slotsOf({ seed: SEED });
  for (const stack of ['csharp', 'node']) {
    for (const mask of Object.values(MASKS)) assert.equal(slots.filter(s => s.masks[stack] === mask).length, 15, `${stack} on ${mask}`);
  }
  assert.ok(slots.every(s => s.masks.csharp !== s.masks.node), 'the two stacks of a pair never share a mask');
});

test('Each_task_gives_a_stack_one_mask_3_times_and_the_other_2_times', () => {
  const slots = slotsOf({ seed: SEED });
  for (const task of TASKS) {
    const counts = Object.values(MASKS).map(mask => slots.filter(s => s.task === task && s.masks.csharp === mask).length).sort();
    assert.deepEqual(counts, [2, 3], `task ${task}: C# holds one mask 3 times and the other 2 times`);
  }
});

test('A_swap_per_pair_over_a_seeded_order_can_give_a_task_one_mask_five_times_and_that_is_the_point', () => {
  // The refuted approach: v1's swap per pair (`maskOf`: the pair's number decides) over the seeded order of the slots. Six slots a round is an even count, so a task whose place in the round has the same parity every round keeps one mask.
  const draw = { csharpHigh: true };
  const stuck = seed => TASKS.some(task => new Set(slotsOf({ seed }).filter(s => s.task === task).map(s => maskOf({ draw, pair: s.slot, stack: 'csharp' }))).size === 1);
  const seed = seedsOf('lv2-swap-', 200).find(stuck);
  assert.ok(seed, 'a seed under which the swap per pair gives a task one mask in all five rounds');
  const task = TASKS.find(t => new Set(slotsOf({ seed }).filter(s => s.task === t).map(s => maskOf({ draw, pair: s.slot, stack: 'csharp' }))).size === 1);
  assert.equal(new Set(slotsOf({ seed }).filter(s => s.task === task).map(s => s.masks.csharp)).size, 2, `the rule gives task ${task} both masks under the same seed`);
});

test('The_rids_of_a_seed_are_distinct_over_task_stack_round_and_attempt_and_a_colliding_seed_is_refused', () => {
  const rids = [];
  for (const task of TASKS) for (const stack of ['csharp', 'node']) for (let round = 1; round <= 5; round += 1) for (const attempt of [1, 2]) rids.push(ridOfSlot({ seed: SEED, task, stack, round, attempt }));
  assert.equal(new Set(rids).size, 120, 'six tasks, two stacks, five rounds, two attempts');
  assert.ok(rids.every(r => /^[0-9a-f]{6}$/.test(r)));
  assert.equal(ridOfSlot({ seed: SEED, task: '04', stack: 'csharp', round: 1, attempt: 1 }), sha256Of(`${SEED}:bare:t04:csharp:r1:a1`).slice(0, 6), 'the parent 4.4 form without a group');
  assert.equal(seedProblem(SEED), null);
  const colliding = COLLIDING_SEED;
  const problem = seedProblem(colliding);
  assert.match(problem, /rid [0-9a-f]{6} is the rid of both/, 'the sentence names the rid and the two sessions');
  assert.match(seedProblem(''), /seed/);
  assert.match(seedProblem(undefined), /seed/);
});

test('The_first_stack_of_a_slot_is_a_seeded_bit_with_the_task_and_the_round_in_its_text', () => {
  const first = ({ seed, round, task }) => firstStackOfSlot({ seed, round, task });
  assert.equal(first({ seed: SEED, round: 2, task: '05' }), first({ seed: SEED, round: 2, task: '05' }));
  const slots = slotsOf({ seed: SEED });
  assert.deepEqual(slots.map(s => s.first), slots.map(s => first({ seed: SEED, round: s.round, task: s.task })));
  assert.equal(new Set(slots.map(s => s.first)).size, 2, 'neither stack always leads');
  assert.equal(new Set(TASKS.map(task => first({ seed: SEED, round: 1, task }))).size, 2, 'the task is in the text: one round, six tasks, both stacks lead');
  assert.ok(new Set([1, 2, 3, 4, 5].map(round => TASKS.map(task => first({ seed: SEED, round, task })).join())).size > 1, 'the round is in the text: the six tasks lead differently in different rounds');
});
