// `stage-run` over several units (F3.S1 (a), PR 3): `--until round`, the read-back stop, a halted slot that is resumed, a rid that is already in the run-map, the dated `refuted` line, the pilot's own order. The fake loop of `stage-run-fixture.mjs`
// stands for the box, the CLI and the scoring.
import assert from 'node:assert/strict';
import { mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { test } from 'node:test';
import { transition, writeRunMap } from '../../minigit-bench/lib/run-map.mjs';
import { runMapFileOf } from '../lib/series.mjs';
import { stageRun } from '../lib/stage-run.mjs';
import { GO_ALL, GO_PILOT, PILOT_GO_LINE, SEED, SLOTS, mapOf, readbackHolds, runRig } from './stage-run-fixture.mjs';
import { readbackSlots } from '../lib/stage-schedule.mjs';

const next = (rig, extra = {}) => stageRun({ values: { series: rig.dir, what: 'next', ...extra }, io: rig.io });
const round = (rig, extra = {}) => stageRun({ values: { series: rig.dir, what: 'next', until: 'round', ...extra }, io: rig.io });
const BASE = [GO_PILOT, GO_ALL, PILOT_GO_LINE];

test('until_round_stops_at_the_rounds_last_slot_at_a_halt_and_at_the_pause_file', async () => {
  const rig = await runRig({ lines: [...BASE, ...readbackHolds()] });
  for (let i = 0; i < 12; i += 1) assert.equal((await next(rig)).exit, 0);
  const third = await round(rig);
  assert.equal(third.exit, 0, third.message);
  assert.match(third.message, /the round's last slot is finished/);
  assert.deepEqual(rig.log.slice(12).map(l => l.round), [3, 3, 3, 3, 3, 3], 'round 3 ran, all six slots, and not a slot of round 4');
  assert.equal(rig.log.length, 18);
  // a halt: the loop of the second slot ends in a resumable halt, the round loop ends there with the same exit
  const halting = await runRig({ lines: [...BASE, ...readbackHolds()] });
  for (let i = 0; i < 12; i += 1) assert.equal((await next(halting)).exit, 0);
  const real = halting.io.runSeams.loopFn;
  let calls = 0;
  halting.io.runSeams.loopFn = async a => { calls += 1; const r = await real(a); return calls === 2 ? { exit: 3, message: 'the machine was not quiet' } : r; };
  const halted = await round(halting);
  assert.deepEqual([halted.exit, halted.message, calls], [3, 'the machine was not quiet', 2]);
  // the pause file: checked between slots only, so the slot that was running is finished first
  const pausing = await runRig({ lines: [...BASE, ...readbackHolds()] });
  for (let i = 0; i < 12; i += 1) assert.equal((await next(pausing)).exit, 0);
  const inner = pausing.io.runSeams.loopFn;
  pausing.io.runSeams.loopFn = async a => { const r = await inner(a); writeFileSync(path.join(pausing.dir, 'pause'), ''); return r; };
  const paused = await round(pausing);
  assert.equal(paused.exit, 0);
  assert.match(paused.message, /the pause file .* exists: stopped between slots/);
  assert.equal(pausing.log.length, 13, 'the running slot was finished, and no slot was started after the file appeared');
  assert.equal(mapOf(pausing.dir).sessions[pausing.log[12].rids[0]].status, 'scored');
});

test('until_round_ends_after_a_read_back_slot_and_says_what_is_awaited_and_goes_on_once_the_lines_are_there', async () => {
  const rig = await runRig({ lines: BASE });
  const [first] = readbackSlots({ seed: SEED });
  const stopped = await round(rig);
  assert.equal(stopped.exit, 3, stopped.message);
  assert.match(stopped.message, new RegExp(`^slot ${first.slot} \\(round 1, task ${first.task}\\) is a read-back slot: nothing later runs until its two dated read-back lines are on main; the print is .node research/lang-v2/tools/readback.mjs --series ${rig.dir} --pair ${first.slot}.`));
  assert.equal(rig.log.length, first.slot, 'nothing later ran');
  const gate = await next(rig);
  assert.equal(gate.exit, 4, 'and `next` itself says the gate');
  assert.ok(gate.message.includes(`the print is \`node research/lang-v2/tools/readback.mjs --series <dir> --pair ${first.slot}\``), 'with the full command of the tool (PR 7\'s, under research/lang-v2/tools)');
  writeFileSync(rig.files.amendmentsFile, `${BASE.join('\n')}\n${readbackHolds().slice(0, 2).join('\n')}\n`);
  const again = await round(rig);
  assert.equal(again.exit, 0, again.message);
  assert.equal(rig.log.length, 6, 'once the lines are there the rest of round 1 runs');
  assert.deepEqual(rig.log.map(l => l.pair), [1, 2, 3, 4, 5, 6]);
});

/** A rig at the start of round 3 (twelve slots run, the read-back lines on main) whose next loop call ends with `after()` (a change the schedule meets only when it looks for the slot after). */
async function roundThreeRig(after) {
  const rig = await runRig({ lines: [...BASE, ...readbackHolds()] });
  for (let i = 0; i < 12; i += 1) assert.equal((await next(rig)).exit, 0);
  const inner = rig.io.runSeams.loopFn;
  rig.io.runSeams.loopFn = async a => { const r = await inner(a); after(rig); return r; };
  return rig;
}

test('until_round_does_not_say_the_round_is_finished_when_the_schedule_refuses_the_next_slot_and_ends_with_its_reason_and_exit', async () => {
  const stopped = await roundThreeRig(rig => writeRunMap(runMapFileOf(rig.dir), { ...mapOf(rig.dir), stopped: { rule: 'stop: auth', reason: '{}', at: '2026-11-20T10:00:00Z' } }));
  const stop = await round(stopped);
  assert.equal(stop.exit, 2, stop.message);
  assert.match(stop.message, /is stopped/);
  assert.doesNotMatch(stop.message, /the round's last slot is finished/, 'a stopped series is a stop, not the end of a round');
  const unreadable = await roundThreeRig(rig => { rmSync(rig.files.amendmentsFile); mkdirSync(rig.files.amendmentsFile); });
  const refused = await round(unreadable);
  assert.equal(refused.exit, 4, refused.message);
  assert.doesNotMatch(refused.message, /the round's last slot is finished/, 'an amendments file that cannot be read is a refusal');
  assert.equal(unreadable.log.length, 13, 'one slot ran, and the loop went no further');
  const control = await roundThreeRig(() => {});
  assert.match((await round(control)).message, /the round's last slot is finished/, 'the control: the next slot in another round is the end of the round, exit 0');
});

test('A_halted_slot_is_resumed_and_plans_no_new_session', async () => {
  const rig = await runRig();
  const real = rig.io.runSeams.loopFn;
  let first = true;
  rig.io.runSeams.loopFn = async a => {
    if (first) { first = false; writeRunMap(runMapFileOf(a.series.dir), a.planned.map); return { exit: 3, message: 'the machine was not quiet' }; }
    return real(a);
  };
  assert.equal((await next(rig)).exit, 3);
  const planned = Object.keys(mapOf(rig.dir).sessions);
  assert.equal(planned.length, 2, 'both sessions were planned by the halted call');
  assert.equal((await next(rig)).exit, 0);
  assert.deepEqual(rig.log[0].planned, [], 'the resume planned nothing');
  assert.deepEqual(rig.log[0].resumed.sort(), [...planned].sort(), 'it resumed the two that were open');
  assert.deepEqual(Object.keys(mapOf(rig.dir).sessions).sort(), [...planned].sort(), 'no new session in the run-map');
  assert.equal(rig.log[0].pair, 1);
});

test('A_slot_already_planned_is_never_planned_twice_and_a_rid_that_is_in_the_run_map_fails_closed', async () => {
  const rig = await runRig();
  const slot = SLOTS[0];
  const taken = slot.rids[slot.first][0];
  const map = mapOf(rig.dir);
  writeRunMap(runMapFileOf(rig.dir), { ...map, sessions: { [taken]: { rid: taken, pair: 0, stack: 'node', phase: 'v1', attempt: 1, kind: 'pilot', label: 'paired', mask: '0-5', status: 'scored', data: true, class: 'completed' } } });
  const before = readFileSync(runMapFileOf(rig.dir), 'utf8');
  const r = await next(rig);
  assert.equal(r.exit, 4);
  assert.match(r.message, new RegExp(`the rid ${taken} is already in the run-map, so nothing was planned \\(a data rid comes from the seed`));
  assert.equal(readFileSync(runMapFileOf(rig.dir), 'utf8'), before, 'nothing was planned, in the map or anywhere');
  assert.deepEqual(rig.log, []);
});

test('A_dated_refuted_line_makes_the_session_a_void_harness_and_offers_its_re_run', async () => {
  const rig = await runRig({ lines: BASE });
  const [first] = readbackSlots({ seed: SEED });
  for (let i = 0; i < first.slot; i += 1) assert.equal((await next(rig)).exit, 0);
  const csharp = SLOTS[first.slot - 1].rids.csharp[0];
  const node = SLOTS[first.slot - 1].rids.node[0];
  writeFileSync(rig.files.amendmentsFile, `${BASE.join('\n')}\n2026-11-23 | G-1 | refuted | ${csharp} | ~/e.md | the judge\n2026-11-23 | G-1 | holds | ${node} | ~/e.md | the judge\n`);
  const r = await next(rig);
  assert.equal(r.exit, 0, r.message);
  const last = rig.log.at(-1);
  assert.deepEqual([last.pair, last.stacks, last.attempts, last.planned], [first.slot, ['csharp'], [2], [SLOTS[first.slot - 1].rids.csharp[1]]], 'the re-run of that stack alone, attempt 2, before any later slot');
  const verdicts = last.unit.ctx.handVerdictsFor(csharp);
  assert.deepEqual(verdicts.map(v => [v.g, v.verdict]), [['G-1', 'refuted']], 'the loop reads the registered line for its hand verdicts: it makes the session void: harness when it applies them');
  assert.deepEqual(last.unit.ctx.handVerdictsFor(node).map(v => v.verdict), ['holds']);
});

test('The_pilot_steps_run_in_the_pilots_order_and_one_that_is_not_next_is_refused', async () => {
  const rig = await runRig({ lines: [GO_PILOT, GO_ALL, PILOT_GO_LINE], stage: { pilotRand: (() => { let n = 0; return () => `r${(n += 1)}`; })() } });
  const pilot = what => stageRun({ values: { series: rig.dir, what }, io: rig.io });
  const early = await pilot('pilot:10');
  assert.equal(early.exit, 4);
  assert.match(early.message, /--what pilot:10: the pilot's next step is aa \(the A\/A, the daemon pair and the web pair run in that order/);
  assert.equal((await pilot('aa')).exit, 0);
  const aa = rig.log[0];
  assert.deepEqual([aa.kind, aa.pair, aa.task, aa.attempts, aa.stacks], ['aa', 0, '05', [1, 1], ['node', 'node']], 'the A/A: two twins of task 05 on Node');
  assert.deepEqual(Object.values(mapOf(rig.dir).sessions).map(s => [s.twin, s.mask]).sort(), [['a', '0-5'], ['b', '8-13']], 'twin a on mask A, twin b on mask B');
  assert.ok(Object.values(mapOf(rig.dir).sessions).every(s => s.arm === 'bare' && s.round === null && s.kind === 'aa'), 'the kind, no round and the arm are in the run-map entry');
  assert.equal((await pilot('aa')).message.includes('pilot'), true);
  assert.equal((await pilot('pilot:10')).exit, 0);
  assert.equal((await pilot('pilot:04')).exit, 0);
  assert.deepEqual(rig.log.map(l => l.kind), ['aa', 'pilot', 'pilot']);
  assert.equal((await pilot('pilot:04')).finished, true, 'nothing more to run in the pilot');
  const kinds = Object.values(mapOf(rig.dir).sessions).map(s => `${s.kind}:${s.task}`);
  assert.deepEqual(kinds.sort(), ['aa:05', 'aa:05', 'pilot:04', 'pilot:04', 'pilot:10', 'pilot:10']);
  void transition;
});
