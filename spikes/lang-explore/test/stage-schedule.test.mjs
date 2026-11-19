// The schedule of Stage 1 (F3.S1 (a), PR 2): the order, the mask rule, the rids and the first stack of the 30 slots, the barrier and the round rule, the read-back gate and the pilot's plan. The module is pure, so every
// test hands it a seed and a run-map built here; nothing is read from disk but the pins `TASK_KINDS` is held against.
import assert from 'node:assert/strict';
import { test } from 'node:test';
import { sha256Of } from '../../minigit-bench/lib/inputs.mjs';
import { openSeries, planSession, transition } from '../../minigit-bench/lib/run-map.mjs';
import { loadExplore } from '../lib/inputs.mjs';
import { MASKS, VOID_UNSCORED, isOpenSession, maskOf, openSessions, pairComplete, sessionsOf, stackState, stateOfSessions } from '../lib/series.mjs';
import { STAGE_ROUNDS, TASK_KINDS, firstStackOfSlot, maskOfSlot, nextSlot, pilotPlan, pilotStep, readbackSlots, ridOfSlot, seedProblem, slotProblem, slotsOf } from '../lib/stage-schedule.mjs';

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
const RB = readbackSlots({ seed: SEED });
const line = (slot, stack, attempt, verdict, extra = {}) => ({ g: 'G-1', verdict, rid: SLOTS[slot - 1].rids[stack][attempt - 1], ...extra });
const holds = (slot, stack, attempt = 1) => line(slot, stack, attempt, 'holds');
const refuted = (slot, stack, attempt = 1) => line(slot, stack, attempt, 'refuted');
const rid = (slot, stack, attempt = 1) => SLOTS[slot - 1].rids[stack][attempt - 1];
/** The read-back lines of both read-back slots, `holds` for the first attempt of both stacks: what the tests of the barrier and the re-run leave in place, so the read-back gate stays out of their way. */
const READ = RB.flatMap(p => [holds(p.slot, 'csharp'), holds(p.slot, 'node')]);

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
const next = (script, extra = {}) => nextSlot({ map: mapWith(script), slots: SLOTS, ...GATES, readbackLines: READ, ...extra });
const asked = (script, lines) => nextSlot({ map: mapWith(script), slots: SLOTS, ...GATES, readbackLines: lines });
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
  assert.deepEqual(offer.todo.map(x => [x.rid, x.pair, x.stack, x.attempt, x.label, x.mask, x.kind]), [[SLOTS[0].rids.csharp[1], 1, 'csharp', 2, 'ran unpaired', SLOTS[0].masks.csharp, 'data']], 'one session, alone, under its own rid and the slot\'s mask');
  assert.match(slotProblem({ map: mapWith(voided), slots: SLOTS, slot: 2, ...GATES }), /a void is re-run once, alone, before the next slot: stage-run --what next/);
  assert.equal(next([...voided, [1, 'csharp', 2, GOOD]]).value.slot, 2, 'after the re-run the next slot is offered');
  const second = next([...voided, [1, 'csharp', 2, VOID]]).value;
  assert.deepEqual([second.slot, second.action], [2, 'plan'], 'a second void ends the cell: nothing loops, the next slot is offered');
  assert.equal(next([[1, 'csharp', 1, VOID], [1, 'node', 1, VOID]]).value.rerun, SLOTS[0].first, 'both stacks void: each is re-run alone, the stack that led first');
  const planned = next([...voided, [1, 'csharp', 2, 'planned']]).value;
  assert.deepEqual([planned.action, planned.todo.map(x => [x.rid, x.step])], ['resume', [[SLOTS[0].rids.csharp[1], 'launch']]], 'a re-run a halt left planned is resumed, never planned twice');
});

test('A_session_with_a_functional_failure_in_a_completed_pass_is_never_offered_a_re_run', () => {
  for (const cls of ['rejected', 'intermittently incorrect', 'killed: cap']) {
    const data = { class: cls, data: true, scored: true };
    const offer = next([[1, 'csharp', 1, data], [1, 'node', 1, data]]).value;
    assert.deepEqual([offer.slot, offer.action], [2, 'plan'], `${cls} is a data attempt and the slot is finished`);
    for (const failed of ['csharp', 'node']) {
      const other = failed === 'csharp' ? 'node' : 'csharp';
      const mixed = next([[1, failed, 1, data], [1, other, 1, VOID]]).value;
      assert.deepEqual([mixed.action, mixed.rerun, mixed.todo.length], ['rerun', other, 1], `${cls} on ${failed}: only the void ${other} is re-run, never the data attempt`);
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
    assert.deepEqual(offer.todo.map(x => x.stack), lead === 'csharp' ? ['csharp', 'node'] : ['node', 'csharp'], `${lead} leads: it is launched first`);
    assert.deepEqual(offer.todo.map(x => [x.rid, x.attempt, x.label, x.mask, x.pair, x.kind, x.task, x.round]), offer.todo.map(x => [slot.rids[x.stack][0], 1, 'paired', slot.masks[x.stack], slot.slot, 'data', slot.task, slot.round]));
  }
});

// ---- the read-back pairs and the read-back gate ------------------------------------------------------------------------------------------------------------------------------------------------------------------


test('The_read_back_pairs_are_named_by_the_seed_one_of_each_kind_in_rounds_1_and_2', () => {
  for (const seed of seedsOf('lv2-readback-', 60)) {
    const picks = readbackSlots({ seed });
    assert.deepEqual(picks.map(p => p.round), [1, 2], `${seed}: one slot of round 1 and one of round 2`);
    assert.deepEqual(picks.map(p => p.kind).sort(), ['daemon', 'web'], `${seed}: one daemon task and one web task`);
    const all = slotsOf({ seed });
    assert.deepEqual(all.filter(s => s.readback).map(s => s.slot), picks.map(p => p.slot), 'the slots carry the flag the picks name');
    for (const p of picks) assert.deepEqual([all[p.slot - 1].task, all[p.slot - 1].round, TASK_KINDS[p.task]], [p.task, p.round, p.kind]);
  }
  assert.deepEqual(readbackSlots({ seed: SEED }), readbackSlots({ seed: SEED }));
  const distinct = new Set(seedsOf('lv2-readback-', 60).map(seed => JSON.stringify(readbackSlots({ seed }).map(p => [p.task, p.round]))));
  assert.ok(distinct.size > 6, `sixty seeds name ${distinct.size} different pairs of slots: the seed decides, nothing is fixed`);
  assert.deepEqual(new Set(seedsOf('lv2-readback-', 60).map(seed => readbackSlots({ seed })[0].kind)), new Set(['daemon', 'web']), 'the seed decides which kind is read back in round 1');
});

test('The_kinds_the_schedule_holds_are_the_kinds_of_the_pins', () => {
  const pins = loadExplore().value.tasks;
  assert.deepEqual(Object.keys(TASK_KINDS).sort(), TASKS);
  for (const task of TASKS) assert.equal(TASK_KINDS[task], pins[task].kind, `task ${task}`);
});

test('No_slot_after_a_read_back_slot_is_offered_until_both_its_sessions_have_their_dated_line', () => {
  const [rb] = RB;
  const script = finished(rb.slot);
  const none = asked(script, []);
  assert.equal(none.ok, false);
  assert.ok(none.reason.includes(describeSlot(rb.slot)), none.reason);
  assert.match(none.reason, /is a read-back slot: no later slot is offered until each of its two sessions that counts has its dated read-back line \(G-1, holds or refuted\)/);
  assert.ok(none.reason.includes(rid(rb.slot, 'csharp')) && none.reason.includes(rid(rb.slot, 'node')), 'both rids are named');
  const one = asked(script, [holds(rb.slot, 'csharp')]);
  assert.ok(one.reason.includes(rid(rb.slot, 'node')) && !one.reason.includes(rid(rb.slot, 'csharp')), `one line of two is not enough: ${one.reason}`);
  assert.match(slotProblem({ map: mapWith(script), slots: SLOTS, slot: rb.slot + 1, ...GATES, readbackLines: [holds(rb.slot, 'csharp')] }), /still unread/);
  const both = asked(script, [holds(rb.slot, 'csharp'), holds(rb.slot, 'node')]);
  assert.deepEqual([both.ok, both.value.slot, both.value.action], [true, rb.slot + 1, 'plan']);
  assert.equal(asked(finished(rb.slot - 1), []).value.slot, rb.slot, 'the read-back slot itself is offered without lines: the lines come after it was scored');
});

test('A_refuted_read_back_makes_the_session_a_void_and_its_re_run_is_the_next_step_before_any_later_slot', () => {
  const [rb] = RB;
  for (const stack of ['csharp', 'node']) {
    const other = stack === 'csharp' ? 'node' : 'csharp';
    const step = asked(finished(rb.slot), [refuted(rb.slot, stack), holds(rb.slot, other)]).value;
    assert.deepEqual([step.slot, step.action, step.rerun, step.attempt], [rb.slot, 'rerun', stack, 2], `${stack} refuted`);
    assert.equal(step.todo[0].rid, rid(rb.slot, stack, 2));
  }
  const map = mapWith([...finished(rb.slot - 1), [rb.slot, 'csharp', 1, { class: 'void: harness', data: false, scored: false }], [rb.slot, 'node', 1, GOOD]]);
  assert.equal(nextSlot({ map, slots: SLOTS, ...GATES, readbackLines: [refuted(rb.slot, 'csharp'), holds(rb.slot, 'node')] }).value.rerun, 'csharp', 'the same step when the harness already wrote the void into the run-map');
  assert.match(slotProblem({ map: mapWith(finished(rb.slot)), slots: SLOTS, slot: rb.slot + 1, ...GATES, readbackLines: [refuted(rb.slot, 'node'), holds(rb.slot, 'csharp')] }), /is not finished \(csharp: done, node: void-rerunnable\)/, 'no later slot before the re-run');
});

test('The_re_run_of_a_refuted_session_is_read_back_too_before_the_next_slot_is_offered', () => {
  const [rb] = RB;
  const script = [...finished(rb.slot), [rb.slot, 'csharp', 2, GOOD]];
  const lines = [refuted(rb.slot, 'csharp'), holds(rb.slot, 'node')];
  const waiting = asked(script, lines);
  assert.equal(waiting.ok, false);
  assert.ok(waiting.reason.includes(rid(rb.slot, 'csharp', 2)) && !waiting.reason.includes(rid(rb.slot, 'csharp', 1)), `the re-run's own rid is what is unread: ${waiting.reason}`);
  assert.equal(asked(script, [...lines, holds(rb.slot, 'csharp', 2)]).value.slot, rb.slot + 1);
});

test('A_second_refuted_read_back_ends_the_cell_and_the_next_slot_is_offered_only_then', () => {
  const [rb] = RB;
  const script = [...finished(rb.slot), [rb.slot, 'csharp', 2, GOOD]];
  const lines = [refuted(rb.slot, 'csharp'), refuted(rb.slot, 'csharp', 2), holds(rb.slot, 'node')];
  const step = asked(script, lines).value;
  assert.deepEqual([step.slot, step.action], [rb.slot + 1, 'plan'], 'nothing loops: the cell is missing and the next slot is offered');
  assert.equal(asked(script, [refuted(rb.slot, 'csharp'), refuted(rb.slot, 'csharp', 2)]).ok, false, 'the other stack is still unread');
});

test('A_read_back_line_for_a_slot_that_is_no_read_back_slot_opens_nothing_and_closes_nothing', () => {
  const [rb] = RB;
  assert.ok(SLOTS.slice(0, rb.slot - 1).every(s => !s.readback) && rb.slot > 2, 'the fixture seed puts two plain slots before the first read-back slot');
  const reopened = asked(finished(rb.slot - 1), [refuted(1, 'csharp'), refuted(2, 'node')]);
  assert.deepEqual([reopened.value.slot, reopened.value.action], [rb.slot, 'plan'], 'a refuted line about a plain slot reopens nothing: no re-run, the next slot goes on');
  const stray = asked(finished(rb.slot), [holds(1, 'csharp'), holds(1, 'node'), holds(2, 'csharp'), holds(2, 'node')]);
  assert.equal(stray.ok, false, 'lines about other slots do not satisfy the read-back slot\'s gate');
  assert.ok(stray.reason.includes(describeSlot(rb.slot)) && stray.reason.includes(rid(rb.slot, 'csharp')), stray.reason);
  const bothOf = patch => ['csharp', 'node'].map(stack => ({ ...holds(rb.slot, stack), ...patch }));
  assert.equal(asked(finished(rb.slot), bothOf({ g: 'G-2' })).ok, false, 'a line of another row does not close the read-back');
  assert.equal(asked(finished(rb.slot), bothOf({ verdict: 'explained' })).ok, false, 'only holds or refuted close it');
  const voidOnly = asked(finished(rb.slot), [holds(rb.slot, 'csharp'), { ...refuted(rb.slot, 'node'), g: 'G-10' }]);
  assert.ok(voidOnly.reason.includes(rid(rb.slot, 'node')), 'a refuted line of another row voids nothing here and closes nothing');
});

test('Round_3_is_refused_until_the_read_back_lines_of_rounds_1_and_2_exist', () => {
  const [first, second] = RB;
  const handMade = finished(12);
  const partial = asked(handMade, [holds(first.slot, 'csharp'), holds(first.slot, 'node')]);
  assert.equal(partial.ok, false);
  assert.match(partial.reason, /round 3 never starts before the read-back lines of rounds 1 and 2 are on main/);
  assert.ok(partial.reason.includes(describeSlot(second.slot)) && partial.reason.includes(rid(second.slot, 'csharp')), partial.reason);
  assert.equal(slotProblem({ map: mapWith(handMade), slots: SLOTS, slot: 13, ...GATES, readbackLines: [] }).includes('round 3 never starts'), true);
  const all = [first, second].flatMap(p => [holds(p.slot, 'csharp'), holds(p.slot, 'node')]);
  assert.equal(asked(handMade, all).value.slot, 13);
});

// ---- the pilot's plan --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

/** The pre-registration's answer (open point 2), as a fixture: the web pair is task 04, the daemon pair and the A/A take 05 and 10 between them. */
const PILOT = { aa: { task: '10', stack: 'csharp' }, daemon: '05', web: '04' };

/** A run-map of the pilot's sessions from `[kind, task, stack, attempt, end]` (kind `aa` or `pilot`; the twin is the mask, so `aa` entries carry `twin`). */
function pilotMap(script) {
  let map = openSeries({ seriesId: 'x-test', caps: {} });
  script.forEach(([kind, task, stack, attempt, end, twin], i) => {
    const rid = `p${String(i).padStart(5, '0')}`;
    const mask = twin === 'b' || (kind === 'pilot' && (task === '04') === (stack === 'csharp')) ? MASKS.high : MASKS.low;
    map = planSession(map, { rid, stack, phase: 'v1', attempt, kind, task, mask, twin, label: 'test' }).value;
    if (end === 'planned') return;
    map = transition(map, rid, 'launching', {}).value;
    if (end === 'launching') return;
    map = transition(map, rid, 'ran', { class: end.class, data: end.data }).value;
    if (end.scored) map = transition(map, rid, 'scored', {}).value;
  });
  return map;
}

const AA_DONE = [['aa', '10', 'csharp', 1, GOOD, 'a'], ['aa', '10', 'csharp', 1, GOOD, 'b']];
const pairDone = (task, attempt = 1) => [['pilot', task, 'csharp', attempt, GOOD], ['pilot', task, 'node', attempt, GOOD]];
const stepOfPilot = script => pilotStep({ map: pilotMap(script), pilot: PILOT });

test('The_pilot_plan_holds_the_A_A_twins_on_the_two_masks_and_one_pair_per_kind_and_no_data_rid', () => {
  const plan = pilotPlan({ pilot: PILOT });
  assert.equal(plan.ok, true, plan.reason);
  const { aa, pairs } = plan.value;
  assert.deepEqual(aa.sessions.map(x => [x.kind, x.task, x.stack, x.twin, x.mask, x.attempt]), [['aa', '10', 'csharp', 'a', MASKS.low, 1], ['aa', '10', 'csharp', 'b', MASKS.high, 1]], 'one task, one stack, the twins on the two masks');
  assert.deepEqual(pairs.map(p => [p.what, p.kind, p.task]), [['pilot:05', 'daemon', '05'], ['pilot:04', 'web', '04']], 'one pair per kind');
  assert.deepEqual(pairs[0].sessions.map(x => [x.kind, x.stack, x.mask]), [['pilot', 'csharp', MASKS.low], ['pilot', 'node', MASKS.high]], 'in the daemon pair C# is on mask A');
  assert.deepEqual(pairs[1].sessions.map(x => [x.kind, x.stack, x.mask]), [['pilot', 'csharp', MASKS.high], ['pilot', 'node', MASKS.low]], 'in the web pair C# is on mask B');
  assert.equal(JSON.stringify(plan.value).includes('rid'), false, 'no rid: the pilot runs before the seed is revealed, its sessions take `pilotRid` at planning and never a data rid');
});

test('The_pilot_runs_the_A_A_then_the_daemon_pair_then_the_web_pair_and_each_must_be_finished_first', () => {
  const first = stepOfPilot([]).value;
  assert.deepEqual([first.what, first.action, first.todo.map(x => x.twin)], ['aa', 'plan', ['a', 'b']]);
  assert.deepEqual(first.todo.map(x => x.step), ['plan', 'plan']);
  const second = stepOfPilot(AA_DONE).value;
  assert.deepEqual([second.what, second.action, second.todo.map(x => x.stack)], ['pilot:05', 'plan', ['csharp', 'node']]);
  assert.equal(stepOfPilot([...AA_DONE, ...pairDone('05')]).value.what, 'pilot:04');
  assert.equal(stepOfPilot([...AA_DONE, ...pairDone('05'), ...pairDone('04')]).value, null, 'nothing more to run: the GO table judges');
  const halted = stepOfPilot([['aa', '10', 'csharp', 1, GOOD, 'a'], ['aa', '10', 'csharp', 1, 'planned', 'b']]).value;
  assert.deepEqual([halted.what, halted.action, halted.todo.length], ['aa', 'resume', 1], 'a halt leaves the twin planned: it is resumed, never planned twice');
});

test('A_void_pilot_session_is_re_run_once_alone_and_a_void_twin_runs_the_A_A_again_once_with_both_twins', () => {
  const rerun = stepOfPilot([...AA_DONE, ['pilot', '05', 'csharp', 1, GOOD], ['pilot', '05', 'node', 1, VOID]]).value;
  assert.deepEqual([rerun.what, rerun.action, rerun.rerun, rerun.attempt, rerun.todo.map(x => [x.stack, x.attempt, x.mask, x.label])], ['pilot:05', 'rerun', 'node', 2, [['node', 2, MASKS.high, 'ran unpaired']]]);
  const again = stepOfPilot([['aa', '10', 'csharp', 1, GOOD, 'a'], ['aa', '10', 'csharp', 1, VOID, 'b']]).value;
  assert.deepEqual([again.what, again.action, again.todo.map(x => [x.twin, x.attempt, x.mask])], ['aa', 'again', [['a', 2, MASKS.low], ['b', 2, MASKS.high]]], 'both twins run again, the good one too: one twin measures no spread');
  const afterAgain = [['aa', '10', 'csharp', 1, GOOD, 'a'], ['aa', '10', 'csharp', 1, VOID, 'b'], ['aa', '10', 'csharp', 2, GOOD, 'a'], ['aa', '10', 'csharp', 2, GOOD, 'b']];
  assert.equal(stepOfPilot(afterAgain).value.what, 'pilot:05', 'the second run of the twins settles the A/A');
  const second = stepOfPilot([...afterAgain.slice(0, 3), ['aa', '10', 'csharp', 2, VOID, 'b']]);
  assert.match(second.reason, /the A\/A has a void twin after its one repeat/, 'the A/A is run again once and never twice');
});

test('The_pilot_is_at_most_twelve_sessions_even_when_every_pilot_session_and_one_twin_voids_once', () => {
  let script = [];
  for (let i = 0; i < 40; i += 1) {
    const step = stepOfPilot(script);
    assert.equal(step.ok, true, step.reason);
    if (step.value === null) break;
    const voidFirst = x => (x.attempt === 1 && (x.kind === 'pilot' || x.twin === 'b') ? VOID : GOOD);
    script = [...script, ...step.value.todo.map(x => [x.kind, x.task, x.stack, x.attempt, voidFirst(x), x.twin])];
  }
  assert.equal(script.length, 12, 'A/A: four, daemon pair: four, web pair: four');
  assert.equal(stepOfPilot(script).value, null);
});

test('A_pilot_stop_is_never_re_run_and_a_pair_holding_one_stack_only_is_refused', () => {
  const stop = { class: 'stop: T1 breach', data: false, scored: false };
  assert.match(stepOfPilot([...AA_DONE, ['pilot', '05', 'csharp', 1, GOOD], ['pilot', '05', 'node', 1, stop]]).reason, /^pilot pair of task 05 node ended with a stop \(stop: T1 breach\): a stopped series is never run again/);
  assert.match(stepOfPilot([...AA_DONE, ['pilot', '05', 'csharp', 1, GOOD]]).reason, /holds sessions of one stack only/);
  assert.match(stepOfPilot([['aa', '10', 'csharp', 1, GOOD, 'a'], ['aa', '10', 'csharp', 1, stop, 'b']]).reason, /^the A\/A b ended with a stop \(stop: T1 breach\): a stopped series is never run again/);
});

// ---- what the offer says: the stack, the attempt, the rid and the step ----------------------------------------------------------------------------------------------------------------------------------------

const shape = offer => [offer.slot, offer.action, offer.todo.map(x => [x.stack, x.attempt, x.rid, x.step])];

test('A_scored_attempt_with_a_refuted_line_becomes_its_re_run_through_nextSlot_by_the_attempts_rid', () => {
  const [rb] = RB;
  const script = finished(rb.slot);
  const before = asked(script, [holds(rb.slot, 'csharp'), holds(rb.slot, 'node')]).value;
  assert.deepEqual([before.slot, before.action], [rb.slot + 1, 'plan'], 'both attempts scored and read: the next slot');
  const after = asked(script, [refuted(rb.slot, 'csharp'), holds(rb.slot, 'node')]).value;
  assert.deepEqual(shape(after), [rb.slot, 'rerun', [['csharp', 2, rid(rb.slot, 'csharp', 2), 'plan']]], 'the same map, the line added: the scored attempt is void: harness and only its re-run is offered');
  const strayRid = asked(script, [{ ...refuted(rb.slot, 'csharp'), rid: 'ffffff' }, holds(rb.slot, 'csharp'), holds(rb.slot, 'node')]).value;
  assert.equal(strayRid.slot, rb.slot + 1, 'a refuted line whose rid is no session of the slot reconciles nothing');
});

test('The_offer_names_the_stack_the_attempt_the_rid_and_the_step_still_to_run_and_never_the_slot_alone', () => {
  const [c1, n1] = [rid(1, 'csharp'), rid(1, 'node')];
  const lead = SLOTS[0].first;
  const order = stacks => (lead === 'csharp' ? stacks : [...stacks].reverse());
  assert.deepEqual(shape(next([]).value), [1, 'plan', order([['csharp', 1, c1, 'plan'], ['node', 1, n1, 'plan']])], 'absent: both stacks are planned together, the leading one first');
  assert.deepEqual(shape(next([[1, 'csharp', 1, GOOD], [1, 'node', 1, 'planned']]).value), [1, 'resume', [['node', 1, n1, 'launch']]], 'a restart after the first stack was scored and before the second started: only the second');
  assert.deepEqual(shape(next([[1, 'node', 1, GOOD], [1, 'csharp', 1, 'launching']]).value), [1, 'resume', [['csharp', 1, c1, 'settle']]], 'launching (interrupted): settled, not launched again');
  assert.deepEqual(shape(next([[1, 'csharp', 1, GOOD], [1, 'node', 1, { class: 'killed: cap', data: true, scored: false }]]).value), [1, 'resume', [['node', 1, n1, 'score']]], 'a killed or crashed session is a data attempt: it is scored, not re-run');
  assert.deepEqual(shape(next([[1, 'csharp', 1, GOOD], [1, 'node', 1, GOOD]]).value).slice(0, 2), [2, 'plan'], 'scored: nothing of slot 1 is offered again');
  assert.deepEqual(shape(next([[1, 'csharp', 1, VOID], [1, 'node', 1, GOOD]]).value), [1, 'rerun', [['csharp', 2, rid(1, 'csharp', 2), 'plan']]], 'a restart after a void: only that stack\'s re-run, never the scored stack');
  assert.deepEqual(shape(next([[1, 'csharp', 1, VOID], [1, 'node', 1, 'planned']]).value), [1, 'resume', [['node', 1, n1, 'launch']]], 'what a halt left open is finished before the void is re-run');
  assert.deepEqual(shape(next([[1, 'csharp', 1, VOID], [1, 'csharp', 2, 'planned'], [1, 'node', 1, GOOD]]).value), [1, 'resume', [['csharp', 2, rid(1, 'csharp', 2), 'launch']]], 'a re-run left planned is launched, not planned again');
  assert.match(next([[1, 'csharp', 1, GOOD]]).reason, /holds sessions of one stack only/, 'a stack with no session beside one with sessions is a refusal, whatever the other one is');
  assert.match(next([[1, 'csharp', 1, 'planned']]).reason, /holds sessions of one stack only/);
});

test('A_line_of_the_original_attempt_never_releases_the_re_run_and_each_attempt_has_its_own_rid', () => {
  const [rb] = RB;
  assert.notEqual(rid(rb.slot, 'csharp', 1), rid(rb.slot, 'csharp', 2), 'the rids differ per attempt by construction');
  const script = [...finished(rb.slot - 1), [rb.slot, 'csharp', 1, VOID], [rb.slot, 'node', 1, GOOD], [rb.slot, 'csharp', 2, GOOD]];
  const strayFirst = asked(script, [holds(rb.slot, 'csharp', 1), holds(rb.slot, 'node')]);
  assert.equal(strayFirst.ok, false, 'the line of attempt 1 does not release attempt 2');
  assert.ok(strayFirst.reason.includes(rid(rb.slot, 'csharp', 2)) && !strayFirst.reason.includes(rid(rb.slot, 'csharp', 1)), strayFirst.reason);
  assert.equal(asked(script, [holds(rb.slot, 'csharp', 2), holds(rb.slot, 'node')]).value.slot, rb.slot + 1, 'its own line releases it');
  const scored = [...finished(rb.slot), [rb.slot, 'csharp', 2, GOOD]];
  const refutedFirst = asked(scored, [refuted(rb.slot, 'csharp', 1), holds(rb.slot, 'node')]);
  assert.equal(refutedFirst.ok, false, 'the refuted line of attempt 1 is not a line of attempt 2');
  assert.ok(refutedFirst.reason.includes(rid(rb.slot, 'csharp', 2)), refutedFirst.reason);
  assert.equal(asked(scored, [refuted(rb.slot, 'csharp', 1), holds(rb.slot, 'csharp', 2), holds(rb.slot, 'node')]).value.slot, rb.slot + 1, 'and attempt 2\'s holds is not undone by attempt 1\'s refuted');
});

test('A_void_attempt_needs_no_line_and_its_re_run_does', () => {
  const [rb] = RB;
  const open = [...finished(rb.slot - 1), [rb.slot, 'csharp', 1, VOID], [rb.slot, 'node', 1, GOOD]];
  assert.deepEqual(shape(asked(open, [holds(rb.slot, 'node')]).value), [rb.slot, 'rerun', [['csharp', 2, rid(rb.slot, 'csharp', 2), 'plan']]], 'the void attempt has no line to wait for: its re-run is the next step');
  const final = [...open, [rb.slot, 'csharp', 2, VOID]];
  assert.equal(asked(final, [holds(rb.slot, 'node')]).value.slot, rb.slot + 1, 'a void attempt after its re-run (the cell is missing) needs no line either');
  const unread = asked([...open, [rb.slot, 'csharp', 2, GOOD]], [holds(rb.slot, 'node')]);
  assert.ok(unread.ok === false && unread.reason.includes(rid(rb.slot, 'csharp', 2)) && !unread.reason.includes(rid(rb.slot, 'node')), `the scored re-run waits for its own line: ${unread.reason}`);
});

test('The_pilot_plans_argument_is_validated_field_by_field_and_each_refusal_names_its_field', () => {
  const refused = (pilot, field) => {
    const r = pilotPlan({ pilot });
    assert.equal(r.ok, false, JSON.stringify(pilot));
    assert.ok(r.reason.startsWith(`${field}: `), `${field}: ${r.reason}`);
  };
  assert.equal(pilotPlan({ pilot: PILOT }).ok, true, 'the control');
  assert.equal(pilotPlan({ pilot: { aa: { task: '05', stack: 'node' }, daemon: '10', web: '04' } }).ok, true, 'the A/A and the daemon pair take 05 and 10 in either order, and the A/A either stack');
  refused(undefined, 'pilot');
  refused({ daemon: '05', web: '04' }, 'pilot.aa');
  refused({ ...PILOT, aa: { stack: 'csharp' } }, 'pilot.aa.task');
  refused({ ...PILOT, aa: { task: '10' } }, 'pilot.aa.stack');
  refused({ ...PILOT, daemon: undefined }, 'pilot.daemon');
  refused({ ...PILOT, web: undefined }, 'pilot.web');
  refused({ ...PILOT, aa: { task: '10', stack: 'rust' } }, 'pilot.aa.stack');
  refused({ ...PILOT, aa: { task: '04', stack: 'csharp' } }, 'pilot.aa.task');
  refused({ ...PILOT, aa: { task: '99', stack: 'csharp' } }, 'pilot.aa.task');
  refused({ ...PILOT, daemon: '04' }, 'pilot.daemon');
  refused({ ...PILOT, daemon: '06' }, 'pilot.daemon');
  refused({ ...PILOT, web: '05' }, 'pilot.web');
  refused({ ...PILOT, web: '06' }, 'pilot.web');
  refused({ ...PILOT, daemon: '10' }, 'pilot.daemon');
  assert.match(pilotPlan({ pilot: { ...PILOT, daemon: '10' } }).reason, /distinct/, 'the A/A and the daemon pair are two different tasks');
  assert.match(pilotPlan({ pilot: { ...PILOT, web: '06' } }).reason, /G-16/, 'the web pair is task 04 because G-16 needs its real pilot tree');
});

// ---- plan round 2, second pass: the immutable verdict, the series.mjs kind, the pilot's tables, slotProblem -----------------------------------------------------------------------------------------------------

const dated = (l, date) => ({ ...l, date });

test('The_first_read_back_line_of_a_rid_is_final_and_a_later_line_that_conflicts_is_refused_never_applied', () => {
  const [rb] = RB;
  const later = finished(rb.slot + 3);
  const firstHolds = [dated(holds(rb.slot, 'csharp'), '2026-11-20'), dated(holds(rb.slot, 'node'), '2026-11-20')];
  assert.equal(asked(later, firstHolds).value.slot, rb.slot + 4, 'without the later line the series goes on');
  const refutedAfter = asked(later, [...firstHolds, dated(refuted(rb.slot, 'csharp'), '2026-11-25')]);
  assert.equal(refutedAfter.ok, false, 'a refutation after a holds never voids an attempt once the gate opened');
  assert.ok(refutedAfter.reason.includes(rid(rb.slot, 'csharp')) && refutedAfter.reason.includes('2026-11-20') && refutedAfter.reason.includes('2026-11-25') && /first dated line is final/.test(refutedAfter.reason), refutedAfter.reason);
  assert.match(slotProblem({ map: mapWith(later), slots: SLOTS, slot: rb.slot + 4, ...GATES, readbackLines: [...firstHolds, dated(refuted(rb.slot, 'csharp'), '2026-11-25')] }), /first dated line is final/, 'the sentence form asks the same');
  const holdsAfter = asked(finished(rb.slot), [dated(refuted(rb.slot, 'csharp'), '2026-11-20'), dated(holds(rb.slot, 'csharp'), '2026-11-26'), dated(holds(rb.slot, 'node'), '2026-11-20')]);
  assert.equal(holdsAfter.ok, false, 'a holds after a refuted is refused too: the re-run is not cancelled');
  assert.ok(holdsAfter.reason.includes('2026-11-26'), holdsAfter.reason);
  assert.equal(asked(later, [...firstHolds, dated(holds(rb.slot, 'csharp'), '2026-11-27')]).value.slot, rb.slot + 4, 'a repeated identical line is harmless');
  assert.equal(asked(later, [...firstHolds, dated(holds(1, 'csharp'), '2026-11-20'), dated(refuted(1, 'csharp'), '2026-11-25')]).value.slot, rb.slot + 4, 'a conflict about a session of no read-back slot is nothing to this gate');
  assert.equal(asked(later, [...firstHolds, dated(refuted(rb.slot, 'csharp', 2), '2026-11-25')]).value.slot, rb.slot + 4, 'a line of another attempt is another rid: no conflict');
});

test('Omitting_the_kind_leaves_the_quick_runs_pairs_as_they_were_and_kind_data_is_seen_only_when_asked', () => {
  let map = openSeries({ seriesId: 'x-test', caps: {} });
  for (const [stack, rid_, scored] of [['csharp', 'q1', true], ['node', 'q2', false]]) {
    map = planSession(map, { rid: rid_, pair: 1, stack, phase: 'v1', attempt: 1, kind: 'pilot', label: 'paired', mask: MASKS.low }).value;
    map = transition(map, rid_, 'launching', {}).value;
    map = transition(map, rid_, 'ran', { class: 'completed', data: true }).value;
    if (scored) map = transition(map, rid_, 'scored', {}).value;
  }
  assert.deepEqual([stackState(map, 1, 'csharp'), stackState(map, 1, 'node')], ['done', 'open'], 'the default kind is the quick run\'s `pilot`');
  assert.deepEqual([stackState(map, 1, 'csharp', 'pilot'), stackState(map, 1, 'node', 'pilot')], ['done', 'open'], 'naming it changes nothing');
  assert.deepEqual([pairComplete(map, 1), sessionsOf(map, 1).map(s => s.rid), openSessions(map, 1).map(s => s.rid)], [false, ['q1', 'q2'], ['q2']]);
  assert.deepEqual([stackState(map, 1, 'csharp', 'data'), sessionsOf(map, 1, undefined, 'data').length, openSessions(map, 1, 'data').length], ['none', 0, 0], 'the quick run\'s sessions are no Stage 1 slot');
  assert.deepEqual([isOpenSession(map.sessions.q1), isOpenSession(map.sessions.q2)], [false, true]);
  assert.deepEqual([stateOfSessions([]), stateOfSessions([map.sessions.q1])], ['none', 'done']);
});

const pairRows = task => (script, expect, name) => {
  const step = stepOfPilot([...AA_DONE, ...(task === '04' ? pairDone('05') : []), ...script]);
  assert.deepEqual(expect(step), true, `${name}: ${JSON.stringify(step)}`);
};
const todoShape = step => step.value.todo.map(x => [x.twin ?? x.stack, x.attempt, x.step]);
const aaScript = rows => rows.map(([twin, attempt, end]) => ['aa', '10', 'csharp', attempt, end, twin]);

test('The_A_A_decision_table_per_state_of_the_twins', () => {
  const rows = [
    ['both absent: both twins are planned', [], 'plan', [['a', 1, 'plan'], ['b', 1, 'plan']]],
    ['one planned, the other scored: only the planned one is launched', aaScript([['a', 1, GOOD], ['b', 1, 'planned']]), 'resume', [['b', 1, 'launch']]],
    ['one launching: settled', aaScript([['a', 1, GOOD], ['b', 1, 'launching']]), 'resume', [['b', 1, 'settle']]],
    ['one ended with data, unscored: scored', aaScript([['a', 1, RAN], ['b', 1, GOOD]]), 'resume', [['a', 1, 'score']]],
    ['one scored and the other void: BOTH twins run again, attempt 2', aaScript([['a', 1, GOOD], ['b', 1, VOID]]), 'again', [['a', 2, 'plan'], ['b', 2, 'plan']]],
    ['both void: both twins run again', aaScript([['a', 1, VOID], ['b', 1, VOID]]), 'again', [['a', 2, 'plan'], ['b', 2, 'plan']]],
    ['one open and the other void: the open one is finished first, nothing else', aaScript([['a', 1, 'planned'], ['b', 1, VOID]]), 'resume', [['a', 1, 'launch']]],
    ['the repeat run planned: it is launched, not planned again', aaScript([['a', 1, GOOD], ['b', 1, VOID], ['a', 2, 'planned'], ['b', 2, 'planned']]), 'resume', [['a', 2, 'launch'], ['b', 2, 'launch']]],
    ['the repeat run half scored: only the open twin', aaScript([['a', 1, GOOD], ['b', 1, VOID], ['a', 2, GOOD], ['b', 2, 'launching']]), 'resume', [['b', 2, 'settle']]],
  ];
  for (const [name, script, action, shape] of rows) {
    const step = stepOfPilot(script);
    assert.deepEqual([step.ok, step.value?.what, step.value?.action, step.ok ? todoShape(step) : null], [true, 'aa', action, shape], name);
  }
  assert.equal(stepOfPilot(aaScript([['a', 1, GOOD], ['b', 1, GOOD]])).value.what, 'pilot:05', 'both scored: the A/A is finished');
  assert.equal(stepOfPilot(aaScript([['a', 1, GOOD], ['b', 1, VOID], ['a', 2, GOOD], ['b', 2, GOOD]])).value.what, 'pilot:05', 'the repeat settles it');
  assert.match(stepOfPilot(aaScript([['a', 1, GOOD], ['b', 1, VOID], ['a', 2, GOOD], ['b', 2, VOID]])).reason, /after its one repeat/, 'a void at attempt 2: the A/A ends and the pilot cannot go on; an instrument fix and a new go come first');
  assert.match(stepOfPilot(aaScript([['a', 1, GOOD]])).reason, /holds one twin only of its run 1/, 'one twin of a run without the other is a refusal, whatever it is');
  assert.match(stepOfPilot(aaScript([['a', 1, GOOD], ['b', 1, VOID], ['a', 2, GOOD]])).reason, /holds one twin only of its run 2/);
});

test('The_pilot_pairs_follow_the_slot_decision_table', () => {
  const cases = [
    ['both absent: both stacks planned', [], 'plan', [['csharp', 1, 'plan'], ['node', 1, 'plan']]],
    ['one planned, the other scored: only the planned one', [['pilot', '05', 'csharp', 1, GOOD], ['pilot', '05', 'node', 1, 'planned']], 'resume', [['node', 1, 'launch']]],
    ['launching: settled', [['pilot', '05', 'csharp', 1, 'launching'], ['pilot', '05', 'node', 1, GOOD]], 'resume', [['csharp', 1, 'settle']]],
    ['ended with data, unscored: scored', [['pilot', '05', 'csharp', 1, GOOD], ['pilot', '05', 'node', 1, RAN]], 'resume', [['node', 1, 'score']]],
    ['void at attempt 1: that stack alone, attempt 2', [['pilot', '05', 'csharp', 1, VOID], ['pilot', '05', 'node', 1, GOOD]], 'rerun', [['csharp', 2, 'plan']]],
    ['an open session is finished before a void is re-run', [['pilot', '05', 'csharp', 1, VOID], ['pilot', '05', 'node', 1, 'planned']], 'resume', [['node', 1, 'launch']]],
    ['the re-run planned: launched, not planned again', [['pilot', '05', 'csharp', 1, VOID], ['pilot', '05', 'csharp', 2, 'planned'], ['pilot', '05', 'node', 1, GOOD]], 'resume', [['csharp', 2, 'launch']]],
  ];
  for (const [name, script, action, shape] of cases) {
    const step = stepOfPilot([...AA_DONE, ...script]);
    assert.deepEqual([step.ok, step.value?.what, step.value?.action, step.ok ? todoShape(step) : null], [true, 'pilot:05', action, shape], name);
  }
  const final = [...AA_DONE, ['pilot', '05', 'csharp', 1, VOID], ['pilot', '05', 'csharp', 2, VOID], ['pilot', '05', 'node', 1, GOOD]];
  assert.equal(stepOfPilot(final).value.what, 'pilot:04', 'a void at attempt 2 ends the pair: nothing, the web pair is next');
  assert.match(stepOfPilot([...AA_DONE, ['pilot', '05', 'csharp', 1, GOOD]]).reason, /holds sessions of one stack only/);
  assert.match(stepOfPilot([...AA_DONE, ['pilot', '05', 'csharp', 1, GOOD], ['pilot', '05', 'node', 1, { class: 'stop: T1 breach', data: false, scored: false }]]).reason, /ended with a stop/);
});

test('slotProblem_refuses_what_nextSlot_would_not_offer_and_accepts_what_it_offers', () => {
  const [rb] = RB;
  const ask = (script, slot, lines = READ, gates = GATES) => slotProblem({ map: mapWith(script), slots: SLOTS, slot, ...gates, readbackLines: lines });
  // acceptance: exactly the slot nextSlot offers, in every state the offers were tested in
  for (const script of [[], [[1, 'csharp', 1, GOOD], [1, 'node', 1, 'planned']], [[1, 'csharp', 1, VOID], [1, 'node', 1, GOOD]], finished(2), finished(rb.slot - 1)]) {
    const offered = next(script).value.slot;
    assert.equal(ask(script, offered), null, `slot ${offered} is accepted`);
    assert.equal(ask(script, offered + 1) === null, false, `slot ${offered + 1} is refused`);
  }
  // the refusals, each its own sentence
  assert.match(ask([], 1, [], { pilotGo: false, seedRevealed: true }), /pilot is GO/);
  assert.match(ask([], 1, [], { pilotGo: true, seedRevealed: false }), /seed is revealed/);
  for (const bad of [0, 31, 1.5, '1', undefined]) assert.match(ask([], bad), /Stage 1 has slots 1 to 30/, `slot ${String(bad)}`);
  assert.match(ask([[1, 'csharp', 1, GOOD]], 3), /slot 1 .* is not finished/, 'the barrier');
  assert.match(ask(finished(5).concat([[6, 'csharp', 1, GOOD]]), 7), /round 2 never starts while a pair of round 1 is unscored/, 'the round rule');
  assert.match(ask(finished(2), 2), /is finished: a finished slot is never run again/);
  assert.match(ask(finished(rb.slot), rb.slot + 1, []), /is a read-back slot/, 'the read-back gate');
  assert.match(ask(finished(12), 13, []), /round 3 never starts before the read-back lines/, 'the parent\'s round 3 rule');
  assert.match(ask(finished(rb.slot), rb.slot + 1, [...READ, dated(refuted(rb.slot, 'csharp'), '2026-11-25')]), /first dated line is final/, 'a conflicting line');
  assert.equal(slotProblem({ map: mapWith([]), slots: null, slot: 1, ...GATES }) === null, false, 'no slots before the reveal is a refusal and never a throw');
});
