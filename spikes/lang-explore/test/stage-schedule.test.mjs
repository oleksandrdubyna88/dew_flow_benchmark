// The schedule of Stage 1 (F3.S1 (a), PR 2): the order, the mask rule, the rids and the first stack of the 30 slots, the barrier and the round rule, the read-back gate and the pilot's plan. The module is pure, so every
// test hands it a seed and a run-map built here; nothing is read from disk but the pins `TASK_KINDS` is held against.
import assert from 'node:assert/strict';
import { test } from 'node:test';
import { sha256Of } from '../../minigit-bench/lib/inputs.mjs';
import { openSeries, planSession, transition } from '../../minigit-bench/lib/run-map.mjs';
import { MASKS, VOID_UNSCORED, maskOf, pairComplete, stackState } from '../lib/series.mjs';
import { STAGE_ROUNDS, firstStackOfSlot, maskOfSlot, nextSlot, ridOfSlot, seedProblem, slotProblem, slotsOf } from '../lib/stage-schedule.mjs';

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

// ---- the barrier, the round rule, the re-run and the gates ------------------------------------------------------------------------------------------------------------------------------------------------------

const SLOTS = slotsOf({ seed: SEED });
const GOOD = { class: 'completed', data: true, scored: true };
const VOID = { class: 'void: harness', data: false, scored: false };
const RAN = { class: 'completed', data: true, scored: false };
const GATES = { pilotGo: true, seedRevealed: true };

/** A run-map of kind `data` from a script of `[slot, stack, attempt, end]`: `end` is `planned`, `launching` or the patch of a `ran` session (scored when `end.scored`). */
function mapWith(script) {
  let map = openSeries({ seriesId: 'x-test', caps: {} });
  for (const [slot, stack, attempt, end] of script) {
    const rid = SLOTS[slot - 1].rids[stack][attempt - 1];
    map = planSession(map, { rid, pair: slot, stack, phase: 'v1', attempt, kind: 'data', label: attempt === 1 ? 'paired' : 'ran unpaired', mask: SLOTS[slot - 1].masks[stack] }).value;
    if (end === 'planned') continue;
    map = transition(map, rid, 'launching', {}).value;
    if (end === 'launching') continue;
    map = transition(map, rid, 'ran', { class: end.class, data: end.data }).value;
    if (end.scored) map = transition(map, rid, 'scored', {}).value;
  }
  return map;
}

/** Slots 1 to `last` finished: both stacks scored. */
const finished = last => Array.from({ length: last }, (_, i) => i + 1).flatMap(slot => [[slot, 'csharp', 1, GOOD], [slot, 'node', 1, GOOD]]);
const next = (script, extra = {}) => nextSlot({ map: mapWith(script), slots: SLOTS, ...GATES, ...extra });
const describeSlot = n => `slot ${n} (round ${SLOTS[n - 1].round}, task ${SLOTS[n - 1].task})`;

test('A_data_session_is_seen_through_the_kind_and_a_quick_run_pair_is_not_a_data_slot', () => {
  const map = mapWith(finished(1));
  assert.equal(stackState(map, 1, 'csharp', 'data'), 'done');
  assert.equal(pairComplete(map, 1, 'data'), true);
  assert.equal(stackState(map, 1, 'csharp'), 'none', 'the quick run asks for the kind pilot and sees no session of a Stage 1 slot');
  assert.equal(pairComplete(map, 1), false);
});

test('A_slot_is_refused_while_an_earlier_slot_is_not_finished_and_the_sentence_names_it', () => {
  const map = mapWith([...finished(1), [2, 'csharp', 1, GOOD], [2, 'node', 1, RAN]]);
  const ask = slot => slotProblem({ map, slots: SLOTS, slot, ...GATES });
  assert.match(ask(3), new RegExp(`^${describeSlot(2).replace(/[()]/g, '\\$&')} is not finished \\(csharp: done, node: open\\)`));
  assert.match(ask(3), /both sessions of a slot are scored, or void after the one re-run, before any later slot/);
  assert.match(ask(31), /Stage 1 has slots 1 to 30/);
  assert.match(ask(1), /slot 1 .* is finished/, 'a finished slot is never offered again');
  assert.equal(ask(2), null, 'the open slot itself is the one that may go on');
  assert.equal(slotProblem({ map: mapWith([]), slots: SLOTS, slot: 1, ...GATES }), null);
});

test('No_slot_of_the_next_round_is_offered_while_a_pair_of_this_round_is_unscored', () => {
  const script = [...finished(5), [6, 'csharp', 1, GOOD], [6, 'node', 1, RAN]];
  const refused = slotProblem({ map: mapWith(script), slots: SLOTS, slot: 7, ...GATES });
  assert.match(refused, /round 2 never starts while a pair of round 1 is unscored/);
  assert.ok(refused.includes(describeSlot(6)), refused);
  const offered = next(script);
  assert.equal(offered.value.slot, 6, 'the unscored pair is what is offered, never a slot of round 2');
  assert.equal(offered.value.action, 'resume');
  assert.equal(next([...finished(6)]).value.slot, 7, 'round 2 starts once round 1 is scored');
});

test('A_void_is_re_run_once_alone_before_the_next_slot_and_a_second_void_ends_the_cell', () => {
  const voided = [[1, 'csharp', 1, VOID], [1, 'node', 1, GOOD]];
  const offer = next(voided).value;
  assert.deepEqual([offer.slot, offer.action, offer.rerun, offer.attempt], [1, 'rerun', 'csharp', 2]);
  assert.deepEqual(offer.sessions.map(x => [x.rid, x.pair, x.stack, x.attempt, x.label, x.mask, x.kind]), [[SLOTS[0].rids.csharp[1], 1, 'csharp', 2, 'ran unpaired', SLOTS[0].masks.csharp, 'data']], 'one session, alone, under its own rid and the slot\'s mask');
  assert.match(slotProblem({ map: mapWith(voided), slots: SLOTS, slot: 2, ...GATES }), /a void is re-run once, alone, before the next slot: stage-run --what next/);
  assert.equal(next([...voided, [1, 'csharp', 2, GOOD]]).value.slot, 2, 'after the re-run the next slot is offered');
  const second = next([...voided, [1, 'csharp', 2, VOID]]).value;
  assert.deepEqual([second.slot, second.action], [2, 'plan'], 'a second void ends the cell: nothing loops, the next slot is offered');
  assert.equal(next([[1, 'csharp', 1, VOID], [1, 'node', 1, VOID]]).value.rerun, SLOTS[0].first, 'both stacks void: each is re-run alone, the stack that led first');
  const planned = next([...voided, [1, 'csharp', 2, 'planned']]).value;
  assert.deepEqual([planned.action, planned.resumed], ['resume', [SLOTS[0].rids.csharp[1]]], 'a re-run a halt left planned is resumed, never planned twice');
});

test('A_session_with_a_functional_failure_in_a_completed_pass_is_never_offered_a_re_run', () => {
  for (const cls of ['rejected', 'intermittently incorrect', 'killed: cap']) {
    const data = { class: cls, data: true, scored: true };
    const offer = next([[1, 'csharp', 1, data], [1, 'node', 1, data]]).value;
    assert.deepEqual([offer.slot, offer.action], [2, 'plan'], `${cls} is a data attempt and the slot is finished`);
    for (const failed of ['csharp', 'node']) {
      const other = failed === 'csharp' ? 'node' : 'csharp';
      const mixed = next([[1, failed, 1, data], [1, other, 1, VOID]]).value;
      assert.deepEqual([mixed.action, mixed.rerun, mixed.sessions.length], ['rerun', other, 1], `${cls} on ${failed}: only the void ${other} is re-run, never the data attempt`);
    }
  }
});

test('An_unscored_environment_session_is_a_void_and_is_offered_its_one_re_run', () => {
  const env = { class: VOID_UNSCORED, data: false, scored: false };
  const offer = next([[1, 'csharp', 1, GOOD], [1, 'node', 1, env]]).value;
  assert.deepEqual([offer.slot, offer.action, offer.rerun, offer.attempt], [1, 'rerun', 'node', 2]);
  assert.equal(next([[1, 'csharp', 1, GOOD], [1, 'node', 1, env], [1, 'node', 2, env]]).value.slot, 2, 'the second environment void ends the cell');
});

test('A_stop_is_never_re_run_and_a_slot_holding_one_stack_only_is_refused_by_name', () => {
  const stop = { class: 'stop: T1 breach', data: false, scored: false };
  assert.match(next([[1, 'csharp', 1, GOOD], [1, 'node', 1, stop]]).reason, /^slot 1 \(round 1, task \d\d\) node ended with a stop \(stop: T1 breach\): a stopped series is never run again/);
  assert.match(next([[1, 'csharp', 1, GOOD]]).reason, /holds sessions of one stack only/);
});

test('No_data_slot_is_offered_before_the_pilot_is_GO_and_the_seed_is_revealed', () => {
  const map = mapWith([]);
  assert.match(nextSlot({ map, slots: SLOTS, pilotGo: false, seedRevealed: true }).reason, /no data slot before the pilot is GO: a dated `pilot \| GO` line of the registered amendments comes first/);
  assert.match(nextSlot({ map, slots: SLOTS, pilotGo: true, seedRevealed: false }).reason, /no data slot before the seed is revealed/);
  assert.match(nextSlot({ map, slots: null, pilotGo: true, seedRevealed: false }).reason, /no data slot before the seed is revealed/, 'without the seed there are no slots to offer');
  assert.match(nextSlot({ map, slots: SLOTS, pilotGo: false, seedRevealed: false }).reason, /pilot is GO/, 'the pilot is asked first');
  assert.match(slotProblem({ map, slots: SLOTS, slot: 1, pilotGo: false, seedRevealed: true }), /pilot is GO/);
  assert.equal(nextSlot({ map, slots: SLOTS, ...GATES }).value.slot, 1);
  assert.equal(next(finished(30)).value, null, 'every slot finished: the series is done');
});

test('A_new_slot_plans_both_stacks_in_the_seeded_order_each_under_its_rid_and_mask', () => {
  for (const lead of ['csharp', 'node']) {
    const slot = SLOTS.find(s => s.slot > 1 && s.first === lead);
    const offer = next(finished(slot.slot - 1)).value;
    assert.deepEqual([offer.slot, offer.action, offer.round, offer.task], [slot.slot, 'plan', slot.round, slot.task]);
    assert.deepEqual(offer.sessions.map(x => x.stack), lead === 'csharp' ? ['csharp', 'node'] : ['node', 'csharp'], `${lead} leads: it is launched first`);
    assert.deepEqual(offer.sessions.map(x => [x.rid, x.attempt, x.label, x.mask, x.pair, x.kind, x.task, x.round]), offer.sessions.map(x => [slot.rids[x.stack][0], 1, 'paired', slot.masks[x.stack], slot.slot, 'data', slot.task, slot.round]));
  }
});
