// The schedule of Stage 1 (F3.S1 (a), PR 2; stories document `todo/PLAN_lang_comparison_v2_E9_F3.md` section 5): which of the 30 slots (five rounds, each task once a round) runs when, on which CPU masks, under which rids and with which stack first.
// PURE: nothing here reads a file, the clock or the environment. The caller hands in the seed (a data slot exists only after the seed is revealed), the run-map and the dated lines it parsed; the answer is a value.
// A slot's number is the run-map's `pair`, so v1's pair files and every reader of `pair` work unchanged.
import { sha256Of } from '../../minigit-bench/lib/inputs.mjs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { MAX_ATTEMPTS, MASKS, STACK_ORDER, isOpenSession, pairComplete, sessionsOf, stackState, stateOfSessions } from './series.mjs';
import { FREEZE_TASKS } from './workspaces-sources.mjs';

/** The kind of each task of Stage 1 (`inputs/explore.json` pins it; a test holds this table equal to the pins). The read-back picks one daemon task and one web task. */
export const TASK_KINDS = Object.freeze({ '04': 'web', '05': 'daemon', '06': 'web', '07': 'daemon', '08': 'web', 10: 'daemon' });
/** The by-hand row of the read-back (v1's G-1: the read-back is ok on every init field), as a dated line of the registered amendments carries it. */
export const READBACK_ROW = 'G-1';

export const STAGE_ROUNDS = 5;
/** The run-map kind of a data session: the slot's number is its `pair`. */
export const DATA_KIND = 'data';
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
  slot: (round - 1) * FREEZE_TASKS.length + position + 1, round, task, kind: TASK_KINDS[task],
  masks: Object.fromEntries(STACK_ORDER.map(stack => [stack, maskOfSlot({ round, task, stack })])),
  first: firstStackOfSlot({ seed, round, task }), rids: ridsOf({ seed, task, round }),
});

const baseSlots = ({ seed }) => Array.from({ length: STAGE_ROUNDS }, (_, i) => i + 1)
  .flatMap(round => orderOfRound({ seed, round }).map((task, position) => slotOf({ seed, round, task, position })));

const KINDS = Object.freeze(['daemon', 'web']);

/** The task of `kind` the read-back takes in `round`: one of the kind's three, by the seed. */
function pickTask({ seed, round, kind }) {
  const tasks = FREEZE_TASKS.filter(task => TASK_KINDS[task] === kind);
  return tasks[Number.parseInt(digest(`${seed}:lang-v2:readback:r${round}:${kind}`).slice(0, 8), 16) % tasks.length];
}

/** The two cells of the read-back: round 1 and round 2, one daemon task and one web task, which kind comes first decided by the seed. */
function readbackCells(seed) {
  const kinds = bitOf(`${seed}:lang-v2:readback:kind`) ? [...KINDS].reverse() : KINDS;
  return kinds.map((kind, i) => ({ round: i + 1, kind, task: pickTask({ seed, round: i + 1, kind }) }));
}

/** The 30 slots in the order they run: `{ slot, round, task, kind, masks: { csharp, node }, first, rids: { csharp: [a1, a2], node: [a1, a2] }, readback }`. */
export function slotsOf({ seed }) {
  const picked = readbackCells(seed);
  return baseSlots({ seed }).map(s => ({ ...s, readback: picked.some(p => p.round === s.round && p.task === s.task) }));
}

/** The read-back pairs, named by the seed before any result exists: `[{ slot, round, task, kind }]`, one slot of round 1 and one of round 2, one daemon and one web. */
export const readbackSlots = ({ seed }) => slotsOf({ seed }).filter(s => s.readback).map(({ slot, round, task, kind }) => ({ slot, round, task, kind }));

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

// ---- which slot is next: the barrier, the round rule, the re-run and the gates ---------------------------------------------------------------------------------------------------------------------------------

const finished = (map, n) => pairComplete(map, n, DATA_KIND);
const describe = slot => `slot ${slot.slot} (round ${slot.round}, task ${slot.task})`;
const stateLine = (map, n) => STACK_ORDER.map(stack => `${stack}: ${stackState(map, n, stack, DATA_KIND)}`).join(', ');
const voidStacks = (map, n) => STACK_ORDER.filter(stack => stackState(map, n, stack, DATA_KIND) === 'void-rerunnable');

const hasSlots = slots => Array.isArray(slots) && slots.length > 0;

/** The sentence refusing every data slot, or null: the pilot is GO (a dated line of the registered amendments, read by the caller) and the seed is revealed (there are slots only after it). */
function gateProblem({ pilotGo, seedRevealed, slots }) {
  if (pilotGo !== true) return 'no data slot before the pilot is GO: a dated `pilot | GO` line of the registered amendments comes first';
  return seedRevealed === true && hasSlots(slots) ? null : 'no data slot before the seed is revealed: seed-reveal comes after the pilot, and the slots are made from it';
}

const isVerdictLine = l => l.g === READBACK_ROW && (l.verdict === 'holds' || l.verdict === 'refuted');

/** The rids of every session a read-back slot can hold (both stacks, both attempts). */
const readbackRidsOf = slots => new Set(slots.filter(s => s.readback).flatMap(s => STACK_ORDER.flatMap(stack => s.rids[stack])));

/** The verdict lines of the read-back slots' sessions, grouped by rid in file order (the first line of a rid is first in its group). */
function linesByRid({ slots, lines }) {
  const rids = readbackRidsOf(slots);
  const groups = new Map();
  for (const l of lines.filter(x => isVerdictLine(x) && rids.has(x.rid))) groups.set(l.rid, [...(groups.get(l.rid) ?? []), l]);
  return groups;
}

/** The sentence refusing a later read-back line that conflicts with the first dated line of its rid, or null. A repeated identical line is harmless; the first line is final, so the later one is never applied. */
function conflictProblem({ slots, lines }) {
  const group = [...linesByRid({ slots, lines }).values()].find(g => g.some(l => l.verdict !== g[0].verdict));
  if (group === undefined) return null;
  const [first, later] = [group[0], group.find(l => l.verdict !== group[0].verdict)];
  return `the read-back lines disagree about ${first.rid}: ${first.date ?? 'undated'} ${first.verdict}, then ${later.date ?? 'undated'} ${later.verdict}; the first dated line is final, so the later one is refused and never applied`;
}

const holds = (lines, rid) => lines.some(l => l.rid === rid && l.g === READBACK_ROW && l.verdict === 'holds');

/** A session of a read-back slot that a dated `refuted` line names: it ended as `void: harness`, so its one re-run is the next step. The run-map may not say so yet (the harness applies the line when it rescores), so the schedule reads the line. */
const refutesSession = ({ refuted, numbers }) => s => s.kind === DATA_KIND && s.status === 'scored' && numbers.has(s.pair) && refuted.has(s.rid);

const voided = s => ({ ...s, status: 'ran', data: false, class: 'void: harness' });

/** The run-map as the schedule reads it: a session of a read-back slot with a `refuted` line is a void. A line about any other session changes nothing. */
function withRefuted({ map, slots, lines }) {
  const refutes = refutesSession({ refuted: new Set(lines.filter(l => l.g === READBACK_ROW && l.verdict === 'refuted').map(l => l.rid)), numbers: new Set(slots.filter(s => s.readback).map(s => s.slot)) });
  return { ...map, sessions: Object.fromEntries(Object.entries(map.sessions).map(([rid, s]) => [rid, refutes(s) ? voided(s) : s])) };
}

/** The rids of a finished read-back slot whose session that counts (a scored last attempt of a stack) has no `holds` line yet. */
const unreadRids = ({ map, slot, lines }) => STACK_ORDER.filter(stack => stackState(map, slot.slot, stack, DATA_KIND) === 'done')
  .map(stack => sessionsOf(map, slot.slot, stack, DATA_KIND).at(-1).rid).filter(rid => !holds(lines, rid));

/** The first read-back slot inside `within` that still awaits lines: `[slot, rids]`, or undefined. */
const awaitingOf = ({ map, slots, lines, within }) => slots.filter(s => s.readback && within(s)).map(s => [s, unreadRids({ map, slot: s, lines })]).find(([, rids]) => rids.length > 0);

/** The parent's own rule, stated apart from the gate below so a later change to the barrier cannot lose it: round 3 does not start while a read-back slot of rounds 1 and 2 awaits a line. */
function roundThreeProblem({ map, slots, target, lines }) {
  const waiting = target.round < 3 ? undefined : awaitingOf({ map, slots, lines, within: s => s.round <= 2 });
  return waiting === undefined ? null : `${describe(target)}: round 3 never starts before the read-back lines of rounds 1 and 2 are on main; ${describe(waiting[0])} still awaits ${waiting[1].join(', ')}`;
}

/** The read-back gate: no slot after a read-back slot is offered until each of its sessions that counts has its dated line. */
function readbackGateProblem({ map, slots, target, lines }) {
  const waiting = awaitingOf({ map, slots, lines, within: s => s.slot < target.slot });
  return waiting === undefined ? null : `${describe(waiting[0])} is a read-back slot: no later slot is offered until each of its two sessions that counts has its dated read-back line (${READBACK_ROW}, holds or refuted); still unread: ${waiting[1].join(', ')}; the print is \`readback.mjs --series <dir> --pair ${waiting[0].slot}\``;
}

const linesProblem = args => roundThreeProblem(args) ?? readbackGateProblem(args);

const rerunHint = (map, n) => (voidStacks(map, n).length > 0 ? '; a void is re-run once, alone, before the next slot: stage-run --what next' : '');

/** The sentence for an earlier slot that is not finished: across a round boundary it is the round rule, inside a round the barrier. */
function behindSentence({ map, behind, target }) {
  const state = `${describe(behind)} is not finished (${stateLine(map, behind.slot)})`;
  if (behind.round < target.round) return `${describe(target)}: round ${target.round} never starts while a pair of round ${behind.round} is unscored; ${state}${rerunHint(map, behind.slot)}`;
  return `${state}: both sessions of a slot are scored, or void after the one re-run, before any later slot${rerunHint(map, behind.slot)}`;
}

/**
 * The sentence refusing slot number `slot` now, or null: the gates, then the order (a slot is offered only when every earlier slot is finished, and round r + 1 never starts while a pair of round r is unscored), and a finished slot is
 * never offered again. `slots` is `slotsOf({ seed })`, absent before the seed is revealed.
 */
export function slotProblem({ map, slots, slot, pilotGo, seedRevealed, readbackLines = [] }) {
  const gate = gateProblem({ pilotGo, seedRevealed, slots });
  if (gate) return gate;
  const target = slots.find(s => s.slot === slot);
  if (target === undefined) return `slot ${slot}: Stage 1 has slots 1 to ${slots.length}`;
  return conflictProblem({ slots, lines: readbackLines }) ?? orderProblem({ map: withRefuted({ map, slots, lines: readbackLines }), slots, target, lines: readbackLines });
}

function orderProblem({ map, slots, target, lines }) {
  const behind = slots.find(s => s.slot < target.slot && !finished(map, s.slot));
  if (behind !== undefined) return behindSentence({ map, behind, target });
  return finished(map, target.slot) ? `${describe(target)} is finished: a finished slot is never run again` : linesProblem({ map, slots, target, lines });
}

const sessionOf = ({ slot, stack, attempt, label }) => ({ rid: slot.rids[stack][attempt - 1], pair: slot.slot, stack, attempt, kind: DATA_KIND, label, mask: slot.masks[stack], task: slot.task, round: slot.round });

/** The stacks of a slot in launch order: the one that leads first. */
const launchOrder = slot => [slot.first, ...STACK_ORDER.filter(stack => stack !== slot.first)];

const offer = (slot, action, extra) => ok({ ...slot, ...extra, action });

const stateLineOf = ({ order, states }) => order.map(stack => `${stack}: ${states[stack]}`).join(', ');

/** What a session that exists still needs, by its run-map status: `launch` (planned, never started), `settle` (launching: a crash or a halt cut the launch short, v1's reconcile settles it) or `score` (ended with data, a killed or crashed one included, not yet scored). */
const STEP_OF_STATUS = Object.freeze({ planned: 'launch', launching: 'settle', ran: 'score' });

const resumeEntry = s => ({ ...s, step: STEP_OF_STATUS[s.status] });
const planEntry = entry => ({ ...entry, step: 'plan' });

/** The sentence for a stop (the series' launches end for good, v1's and the engine's rule), or null. */
function stopProblem({ name, order, of, states }) {
  const stopped = order.find(stack => states[stack] === 'stopped');
  return stopped === undefined ? null : `${name} ${stopped} ended with a stop (${of(stopped).at(-1).class}): a stopped series is never run again`;
}

/** The sentence for a unit with sessions of one stack and none of the other: the schedule plans the two stacks together, so it cannot say what is still to run. */
function halfProblem({ name, order, states }) {
  const started = order.some(stack => states[stack] !== 'none');
  return started && order.some(stack => states[stack] === 'none') ? `${name} holds sessions of one stack only (${stateLineOf({ order, states })}): the schedule plans both stacks together` : null;
}

/** Which sessions are still to run, per session state: none planned -> plan both stacks (attempt 1); planned -> launch it; launching -> settle it; ended with data and unscored -> score it; scored -> nothing, never again; void at attempt 1 -> re-run that stack alone (attempt 2, a new rid); void at attempt 2 -> nothing, the cell ends. */
function stepFor({ order, of, planned, rerunOf, states }) {
  const open = order.flatMap(stack => of(stack).filter(isOpenSession));
  if (open.length > 0) return ok({ action: 'resume', todo: open.map(resumeEntry) });
  const voided = order.find(stack => states[stack] === 'void-rerunnable');
  if (voided) return ok({ action: 'rerun', rerun: voided, attempt: 2, todo: [planEntry(rerunOf(voided))] });
  return order.every(stack => states[stack] === 'none') ? ok({ action: 'plan', todo: planned().map(planEntry) }) : ok(null);
}

/**
 * What to do with the sessions of ONE unit, a slot or a pilot pair (the one rule of both): `ok({ action, todo: [{ stack, attempt, rid, step, ... }] })` names exactly the sessions still to run and the step each needs, never the unit alone
 * (see `stepFor`); a stop or a half-planned unit is a refusal; `ok(null)` when the unit is finished. `of(stack)` is the unit's sessions of a stack in attempt order, `planned()` the entries of both stacks and `rerunOf(stack)` the entry of one stack's re-run.
 */
function unitStep(unit) {
  const states = Object.fromEntries(unit.order.map(stack => [stack, stateOfSessions(unit.of(stack))]));
  const refused = stopProblem({ ...unit, states }) ?? halfProblem({ ...unit, states });
  return refused ? fail(refused) : stepFor({ ...unit, states });
}

/** What to do with the first unfinished slot. */
function offerFor({ map, slot }) {
  const order = launchOrder(slot);
  const step = unitStep({
    name: describe(slot), order, of: stack => sessionsOf(map, slot.slot, stack, DATA_KIND),
    planned: () => order.map(stack => sessionOf({ slot, stack, attempt: 1, label: 'paired' })), rerunOf: stack => sessionOf({ slot, stack, attempt: 2, label: 'ran unpaired' }),
  });
  return step.ok && step.value ? offer(slot, step.value.action, step.value) : step;
}

/**
 * The next step of the series: `ok(slot + { action: 'plan' | 'resume' | 'rerun', todo })` for the first slot that is not finished, `ok(null)` when the series is done, `fail(sentence)` when nothing may run. The slot is the first
 * unfinished one, so the barrier holds by construction; `slotProblem` states the same order as a sentence for a verb that is asked for a slot by number.
 */
export function nextSlot({ map, slots, pilotGo, seedRevealed, readbackLines = [] }) {
  const refused = gateProblem({ pilotGo, seedRevealed, slots }) ?? conflictProblem({ slots, lines: readbackLines });
  if (refused) return fail(refused);
  const seen = withRefuted({ map, slots, lines: readbackLines });
  const slot = slots.find(s => !finished(seen, s.slot));
  if (slot === undefined) return ok(null);
  const waiting = linesProblem({ map: seen, slots, target: slot, lines: readbackLines });
  return waiting ? fail(waiting) : offerFor({ map: seen, slot });
}

// ---- the pilot's plan: the A/A and the two pilot pairs (F3.S2 (a), step P) ---------------------------------------------------------------------------------------------------------------------------------------
// The pilot runs before the seed is revealed, so its sessions carry no data rid: the caller draws a `pilotRid` for each entry when it plans it. A session is told apart by what the run-map entry carries: `kind` (`aa` or `pilot`),
// `task`, `stack`, `attempt`, and for an A/A twin its `twin` (`a` on mask A, `b` on mask B).

const PILOT_KIND = 'pilot';
const AA_KIND = 'aa';
const otherMask = mask => (mask === MASKS.low ? MASKS.high : MASKS.low);
/** In the daemon pair C# is on mask A, in the web pair on mask B. */
const PAIR_CSHARP_MASK = Object.freeze({ daemon: MASKS.low, web: MASKS.high });

/** The tasks the A/A and the daemon pair take between them, in either order (the daemon tasks of G-16), and the web pair's one task (G-16 needs a real pilot tree of each of 04, 05 and 10). */
const DRIVER_TASKS = Object.freeze(['05', '10']);
const WEB_TASK = '04';

const isText = value => typeof value === 'string' && value !== '';

/** The first required field of the answer that is missing, as its path. */
const missingField = pilot => [['pilot', pilot], ['pilot.aa', pilot?.aa], ['pilot.aa.task', pilot?.aa?.task], ['pilot.aa.stack', pilot?.aa?.stack], ['pilot.daemon', pilot?.daemon], ['pilot.web', pilot?.web]]
  .find(([path, value]) => (path === 'pilot' || path === 'pilot.aa' ? typeof value !== 'object' || value === null : !isText(value)))?.[0];

const taskIn = (field, task, legal) => (legal.includes(task) ? null : `${field}: ${task} is not one of ${legal.join(', ')} (G-16 needs a real pilot tree of each of 04, 05 and 10)`);

/**
 * The sentence refusing the pre-registration's answer for the pilot, naming the field, or null. The shape is `{ aa: { task, stack }, daemon, web }`, every field required: the A/A's task and the daemon pair's task are two different
 * tasks of 05 and 10 in either order, the web pair is task 04, the A/A's stack is one of the two stacks. The registered values come from the pre-registration (PR 6); this only validates them.
 */
function pilotProblem(pilot) {
  const missing = missingField(pilot);
  if (missing) return `${missing}: required`;
  if (!STACK_ORDER.includes(pilot.aa.stack)) return `pilot.aa.stack: ${pilot.aa.stack} is not one of ${STACK_ORDER.join(', ')}`;
  return taskIn('pilot.web', pilot.web, [WEB_TASK]) ?? taskIn('pilot.daemon', pilot.daemon, DRIVER_TASKS) ?? taskIn('pilot.aa.task', pilot.aa.task, DRIVER_TASKS) ?? distinctProblem(pilot);
}

const distinctProblem = pilot => (pilot.aa.task === pilot.daemon ? `pilot.daemon: ${pilot.daemon} is also the A/A's task; the A/A and the daemon pair are two distinct tasks of ${DRIVER_TASKS.join(' and ')}` : null);

const aaSessions = ({ aa, attempt }) => [['a', MASKS.low], ['b', MASKS.high]].map(([twin, mask]) => ({ kind: AA_KIND, task: aa.task, stack: aa.stack, twin, mask, attempt, label: attempt === 1 ? `twin ${twin}` : `twin ${twin}, A/A again` }));

const pairSessionOf = ({ kind, task, stack, attempt, label }) => ({ kind: PILOT_KIND, task, stack, mask: stack === 'csharp' ? PAIR_CSHARP_MASK[kind] : otherMask(PAIR_CSHARP_MASK[kind]), attempt, label });

const pairOf = (kind, task) => ({
  what: `pilot:${task}`, kind, task,
  sessions: STACK_ORDER.map(stack => pairSessionOf({ kind, task, stack, attempt: 1, label: 'paired' })),
});

/**
 * The pilot's plan: `ok({ aa: { what: 'aa', task, stack, sessions: [twin a on mask A, twin b on mask B] }, pairs: [daemon pair, web pair] })`, each session an entry without a rid. `pilot` is the pre-registration's answer
 * `{ aa: { task, stack }, daemon: <task>, web: <task> }`; an answer that leaves a G-16 task without a pilot tree, or a task of the wrong kind, is refused.
 */
export function pilotPlan({ pilot }) {
  const problem = pilotProblem(pilot);
  if (problem) return fail(problem);
  return ok({ aa: { what: 'aa', task: pilot.aa.task, stack: pilot.aa.stack, sessions: aaSessions({ aa: pilot.aa, attempt: 1 }) }, pairs: [pairOf('daemon', pilot.daemon), pairOf('web', pilot.web)] });
}

const pilotSessionsOf = ({ map, kind, task, stack }) => Object.values(map.sessions).filter(s => s.kind === kind && s.task === task && (stack === undefined || s.stack === stack)).sort((a, b) => a.attempt - b.attempt);

/** A pilot pair: the same unit rule as a slot (a stop ends it, a halt is resumed, a void is re-run once and alone). */
function pairStep({ map, pair }) {
  const step = unitStep({
    name: `pilot pair of task ${pair.task}`, order: STACK_ORDER, of: stack => pilotSessionsOf({ map, kind: PILOT_KIND, task: pair.task, stack }),
    planned: () => pair.sessions, rerunOf: stack => pairSessionOf({ kind: pair.kind, task: pair.task, stack, attempt: 2, label: 'ran unpaired' }),
  });
  return step.ok && step.value ? ok({ what: pair.what, ...step.value }) : step;
}

const isVoidSession = s => stateOfSessions([s]) === 'void-rerunnable';

/** The A/A run again: both twins, attempt 2, because one twin measures no spread. */
const aaAgain = aa => ok({ what: 'aa', action: 'again', todo: aaSessions({ aa, attempt: 2 }).map(planEntry) });

const voidedAt = (sessions, attempt) => sessions.some(s => s.attempt === attempt && isVoidSession(s));

const AFTER_REPEAT = 'the A/A has a void twin after its one repeat: the pilot cannot measure a spread; an instrument fix and a new go come before another run';

/** The A/A after its sessions ran: finished when no twin is void; a void twin makes it run again once, both twins; a void after that ends it. */
function aaSettled({ sessions, aa }) {
  if (!voidedAt(sessions, 1)) return ok(null);
  return sessions.every(s => s.attempt === 1) ? aaAgain(aa) : (voidedAt(sessions, 2) ? fail(AFTER_REPEAT) : ok(null));
}

/** The first run (attempt) of the A/A that holds one twin and not both, or undefined. */
const halfRunOf = sessions => [...new Set(sessions.map(s => s.attempt))].find(attempt => sessions.filter(s => s.attempt === attempt).length !== 2);

/** The sentence for an A/A that ended with a stop or holds one twin of a run only, or null. */
function aaProblem(sessions) {
  const stopped = sessions.find(s => stateOfSessions([s]) === 'stopped');
  if (stopped) return `the A/A ${stopped.twin} ended with a stop (${stopped.class}): a stopped series is never run again`;
  const half = halfRunOf(sessions);
  return half ? `the A/A holds one twin only of its run ${half}: the twins are planned together` : null;
}

/** The A/A: two sessions of one task and one stack, one on each mask; a halt is resumed, a stop ends the pilot. */
function aaStep({ map, plan }) {
  const sessions = pilotSessionsOf({ map, kind: AA_KIND, task: plan.task });
  const problem = aaProblem(sessions);
  if (problem) return fail(problem);
  const open = sessions.filter(isOpenSession);
  if (open.length > 0) return ok({ what: 'aa', action: 'resume', todo: open.map(resumeEntry) });
  return sessions.length === 0 ? ok({ what: 'aa', action: 'plan', todo: plan.sessions.map(planEntry) }) : aaSettled({ sessions, aa: plan });
}

/**
 * The next step of the pilot, in the order of step P: the A/A, the daemon pair, the web pair, each finished before the next. `ok({ what: 'aa' | 'pilot:<NN>', action: 'plan' | 'resume' | 'rerun' | 'again', todo })` (`todo` as for a slot: the sessions still to run and the step each needs),
 * `ok(null)` when nothing more is to run, `fail(sentence)`. A void pilot session is re-run once, alone; a void twin makes the A/A run again once, both twins, so the pilot is at most twelve sessions.
 */
export function pilotStep({ map, pilot }) {
  const plan = pilotPlan({ pilot });
  if (!plan.ok) return plan;
  return firstStep([() => aaStep({ map, plan: plan.value.aa }), ...plan.value.pairs.map(pair => () => pairStep({ map, pair }))]);
}

/** The first of the lazy steps that has something to say: a refusal or something to run; `ok(null)` when every one is finished. */
function firstStep(steps) {
  for (const step of steps) {
    const result = step();
    if (!result.ok || result.value !== null) return result;
  }
  return ok(null);
}
