// The schedule of Stage 1 (F3.S1 (a), PR 2; stories document `todo/PLAN_lang_comparison_v2_E9_F3.md` section 5): which of the 30 slots (five rounds, each task once a round) runs when, on which CPU masks, under which rids and with which stack first.
// PURE: nothing here reads a file, the clock or the environment. The caller hands in the seed (a data slot exists only after the seed is revealed), the run-map and the dated lines it parsed; the answer is a value.
// A slot's number is the run-map's `pair`, so v1's pair files and every reader of `pair` work unchanged.
import { bitOf, hex6 } from '../../minigit-bench/lib/bare-plan.mjs';
import { sha256Of } from '../../minigit-bench/lib/inputs.mjs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { MAX_ATTEMPTS, MASKS, STACK_ORDER, isOpenSession, isVoid, pairComplete, sessionsOf, stackState, stateLine, stateOfSessions } from './series.mjs';
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
/** A plain code-point compare (never `localeCompare`: the order must not depend on a locale). */
const byCode = (a, b) => (a < b ? -1 : Number(a > b));

const taskIndex = task => FREEZE_TASKS.indexOf(task);

const inRounds = round => Number.isInteger(round) && round >= 1 && round <= STAGE_ROUNDS;

/** The sentence for a slot cell that is not one, naming the input and the legal values, or null. */
function cellProblem({ round, task, stack }) {
  if (!inRounds(round)) return `round ${round}: the rounds are 1 to ${STAGE_ROUNDS}`;
  if (taskIndex(task) < 0) return `task ${task}: the tasks of Stage 1 are ${FREEZE_TASKS.join(', ')}`;
  return STACK_ORDER.includes(stack) ? null : `stack ${stack}: the stacks are ${STACK_ORDER.join(', ')}`;
}

const maskFor = ({ round, task, stack }) => ((stack === 'csharp') === ((round + taskIndex(task)) % 2 === 1) ? MASKS.low : MASKS.high);

/**
 * The CPU mask of a stack in a slot: the parent's rule, never read from the seed. With k = 0 for 04 ... 5 for 10, in round r the C# session of task k runs on mask A (`0-5`) when r + k is odd and on mask B (`8-13`) when it is
 * even; the Node session takes the other mask. -> `ok(mask)` | `fail(sentence)` naming the input and the legal values (it never throws).
 */
export function maskOfSlot(input) {
  const cell = input ?? {};
  const problem = cellProblem(cell);
  return problem ? fail(problem) : ok(maskFor(cell));
}

/** The rid of a data session: the first six hex digits of `sha256('<seed>:bare:t<task>:<stack>:r<round>:a<attempt>')` (parent 4.4 without a group). */
export const ridOfSlot = ({ seed, task, stack, round, attempt }) => hex6(`${seed}:bare:t${task}:${stack}:r${round}:a${attempt}`);

/** Which stack is launched and scored first in a slot: a seeded bit with the task and the round in its text, as v1's `firstStack`. */
export const firstStackOfSlot = ({ seed, round, task }) => STACK_ORDER[bitOf(`${seed}:lang-v2:r${round}:t${task}:first`) ? 1 : 0];

/** The tasks of round `round` in their order: sorted by the sha256 of `<seed>:lang-v2:r<round>:t<task>:order`. */
const orderOfRound = ({ seed, round }) => FREEZE_TASKS.map(task => [digest(`${seed}:lang-v2:r${round}:t${task}:order`), task]).sort(([a], [b]) => byCode(a, b)).map(([, task]) => task);

const ridsOf = ({ seed, task, round }) => Object.fromEntries(STACK_ORDER.map(stack => [stack, ATTEMPTS.map(attempt => ridOfSlot({ seed, task, stack, round, attempt }))]));

const slotOf = ({ seed, round, task, position }) => ({
  slot: (round - 1) * FREEZE_TASKS.length + position + 1, round, task, kind: TASK_KINDS[task],
  masks: Object.fromEntries(STACK_ORDER.map(stack => [stack, maskFor({ round, task, stack })])),
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

/** The first two sessions of a seed that share a rid, `[rid, nameOfFirst, nameOfSecond]`, or null: sort by rid (a stable sort keeps the sessions' own order among equal rids) and compare neighbours; nothing is built up by mutation. */
function collision(seed) {
  const sorted = ridsOfSeed(seed).toSorted(([, a], [, b]) => byCode(a, b));
  const at = sorted.findIndex(([, rid], i) => i > 0 && sorted[i - 1][1] === rid);
  return at < 0 ? null : [sorted[at][1], sorted[at - 1][0], sorted[at][0]];
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
const voidStacks = (map, n) => STACK_ORDER.filter(stack => stackState(map, n, stack, DATA_KIND) === 'void-rerunnable');

const hasSlots = slots => Array.isArray(slots) && slots.length > 0;
const PILOT_GO_REFUSAL = 'no data slot before the pilot is GO: a dated `pilot | GO` line of the registered amendments comes first';
const SEED_REFUSAL = 'no data slot before the seed is revealed: seed-reveal comes after the pilot, and the slots are made from it';

const isObject = value => value !== null && typeof value === 'object';

/** The sentence for a missing or stopped run-map, or null. `map.stopped` is the backstop: the loop records a series stop there (v1's `markStopped`), and a stopped series is never run again. */
function runMapProblem(map) {
  if (!isObject(map) || !isObject(map.sessions)) return 'the run-map is missing: nothing can be said of the slots without it';
  return map.stopped ? `the series is stopped (${map.stopped.rule ?? 'no rule recorded'}): a stopped series is never run again` : null;
}

/** The sentence refusing every data slot, or null: the pilot is GO (a dated line of the registered amendments, read by the caller), the seed is revealed (there are slots only after it) and the run-map is there and not stopped. */
function gateProblem({ map, pilotGo, seedRevealed, slots }) {
  if (pilotGo !== true) return PILOT_GO_REFUSAL;
  return seedRevealed === true && hasSlots(slots) ? runMapProblem(map) : SEED_REFUSAL;
}

// ---- the read-back lines: dated, keyed by the attempt's rid, the first dated line of a rid final ------------------------------------------------------------------------------------------------------------------

const DATE = /^\d{4}-\d{2}-\d{2}$/;

/** A real calendar date `yyyy-mm-dd`: it survives a round trip through a UTC date (so `2026-02-30` and `2026-99-99` are no dates). */
const isCalendarDate = text => DATE.test(text) && utcDateText(text) === text;

/** The text a `yyyy-mm-dd` comes back as through a UTC date, or null when the date does not exist at all (`2026-99-99`). */
function utcDateText(text) {
  const time = Date.parse(`${text}T00:00:00Z`);
  return Number.isNaN(time) ? null : new Date(time).toISOString().slice(0, 10);
}

/** A line that counts: an object of the read-back row (`G-1`) with the verdict `holds` or `refuted` and a real calendar date. An undated line, or one whose date is no date (`2026-02-30`), is ignored (it neither opens the gate nor voids nor conflicts), as is anything that is no object. */
const isVerdict = word => word === 'holds' || word === 'refuted';
const isVerdictLine = l => isObject(l) && l.g === READBACK_ROW && isVerdict(l.verdict) && isCalendarDate(String(l.date));

/** The lines that count, in date order (a stable sort: lines of one date keep their order in the file), so "the first dated line" of a rid reads true. `lines` may be anything (null, undefined): it is never a throw. */
const verdictLines = lines => (Array.isArray(lines) ? lines.filter(isVerdictLine).toSorted((a, b) => byCode(a.date, b.date)) : []);

/** The rids of every session a read-back slot can hold (both stacks, both attempts). */
const readbackRidsOf = slots => new Set(slots.filter(s => s.readback).flatMap(s => STACK_ORDER.flatMap(stack => s.rids[stack])));

/** The sentence refusing a later read-back line that conflicts with the first dated line of its rid, or null. A repeated identical line is harmless; the first line is final, so the later one is never applied. */
function conflictProblem({ slots, lines }) {
  const rids = readbackRidsOf(slots);
  const group = [...Map.groupBy(lines.filter(l => rids.has(l.rid)), l => l.rid).values()].find(g => g.some(l => l.verdict !== g[0].verdict));
  if (group === undefined) return null;
  const [first, later] = [group[0], group.find(l => l.verdict !== group[0].verdict)];
  return `the read-back lines disagree about ${first.rid}: ${first.date} ${first.verdict}, then ${later.date} ${later.verdict}; the first dated line is final, so the later one is refused and never applied`;
}

const holds = (lines, rid) => lines.some(l => l.rid === rid && l.verdict === 'holds');

/** A session of a read-back slot that a dated `refuted` line names: it ended as `void: harness`, so its one re-run is the next step. The run-map may not say so yet (the harness applies the line when it rescores), so the schedule reads the line. */
const refutesSession = ({ refuted, numbers }) => s => s.kind === DATA_KIND && s.status === 'scored' && numbers.has(s.pair) && refuted.has(s.rid);

const voided = s => ({ ...s, status: 'ran', data: false, class: 'void: harness' });

/** The run-map as the schedule reads it: a session of a read-back slot with a `refuted` line is a void. A line about any other session changes nothing. */
function withRefuted({ map, slots, lines }) {
  const refutes = refutesSession({ refuted: new Set(lines.filter(l => l.verdict === 'refuted').map(l => l.rid)), numbers: new Set(slots.filter(s => s.readback).map(s => s.slot)) });
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

// ---- the order: the round rule and the barrier ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

const rerunHint = (map, n) => (voidStacks(map, n).length > 0 ? '; a void is re-run once, alone, before the next slot: stage-run --what next' : '');

/** The sentence for an unfinished slot: both its stacks' states, and the way out of a void. */
const unfinishedSentence = ({ map, slot }) => `${describe(slot)} is not finished (${stateLine(map, slot.slot, DATA_KIND)})`;

/**
 * The round rule as its own check, without the barrier's `behind` (a later change to the barrier cannot lose it): every slot of every round below the target's must be finished, so round r + 1 never starts while a pair of
 * round r is unscored. -> a sentence or null. `nextSlot` asks it of the slot it offers and `slotProblem` of the slot it is asked about.
 */
export function roundRuleProblem({ map, slots, target }) {
  const open = slots.find(s => s.round < target.round && !finished(map, s.slot));
  return open === undefined ? null : `${describe(target)}: round ${target.round} never starts while a pair of round ${open.round} is unscored; ${unfinishedSentence({ map, slot: open })}${rerunHint(map, open.slot)}`;
}

/** The sentence for an offered slot that is not the one asked about (`offered` is null when every slot is finished): a finished slot is never run again; a later slot waits for the round rule, then for the barrier (the offered slot is the first not finished). */
function mismatchProblem({ args, target, offered }) {
  const { slots } = args;
  const map = withRefuted({ map: args.map, slots, lines: verdictLines(args.readbackLines) });
  if (offered === null || target.slot < offered.slot) return `${describe(target)} is finished: a finished slot is never run again`;
  return roundRuleProblem({ map, slots, target }) ?? `${unfinishedSentence({ map, slot: offered })}: both sessions of a slot are scored, or void after the one re-run, before any later slot${rerunHint(map, offered.slot)}`;
}

/**
 * The sentence refusing slot number `slot` now, or null: ONE road in with `nextSlot`, so it is null for exactly the slot `nextSlot` offers. `{ map, slots, slot, pilotGo, seedRevealed, readbackLines }`. A refusal of `nextSlot` (a gate, a
 * stopped or half-planned slot, a conflicting or missing read-back line) is its sentence, whatever the number; otherwise a number outside 1 to 30, a finished slot, a later slot (the round rule, then the barrier naming the offered slot).
 */
export function slotProblem(args = {}) {
  const offered = nextSlot(args);
  if (!offered.ok) return offered.reason;
  const target = args.slots.find(s => s.slot === args.slot);
  if (target === undefined) return `slot ${args.slot}: Stage 1 has slots 1 to ${args.slots.length}`;
  return offered.value?.slot === target.slot ? null : mismatchProblem({ args, target, offered: offered.value });
}

// ---- what is still to run in one unit ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

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

/**
 * After a stop in one stack: the partner's session that is already underway is still finished (v1's precedent, `bare-settle.mjs` `recordWaitingStop`: the stop is recorded once no session is unsettled), so `settle` and `score`
 * entries are returned, never `plan` or `launch`; nothing underway is the refusal. `sessions` are the unit's sessions, `stopped` the refusal's sentence.
 */
function settleOnStop({ sessions, stopped }) {
  const underway = sessions.filter(s => isOpenSession(s) && s.status !== 'planned');
  return underway.length > 0 ? ok({ action: 'resume', todo: underway.map(resumeEntry), stop: stopped }) : fail(stopped);
}

/** The sentence for a unit with sessions of one stack and none of the other: the schedule plans the two stacks together, so it cannot say what is still to run. */
function halfProblem({ name, order, states }) {
  const started = order.some(stack => states[stack] !== 'none');
  return started && order.some(stack => states[stack] === 'none') ? `${name} holds sessions of one stack only (${stateLineOf({ order, states })}): the schedule plans both stacks together` : null;
}

/**
 * A crash between planning the two sessions of a unit leaves ONE stack with ONE session that is only `planned` (never launched) and the other stack with none: the missing stack is planned FIRST and the lone session launched after it (a second interruption
 * leaves one planned or both planned, never one launched without its partner). A lone session that got further than `planned` is no such crash and stays a refusal (`halfProblem`). -> `ok({ action: 'resume', todo })` or null.
 */
function lonePlanned({ order, of, planned }) {
  const held = order.filter(stack => of(stack).length > 0);
  return held.length === 1 && of(held[0]).length === 1 && of(held[0])[0].status === 'planned' ? loneOffer({ lone: of(held[0])[0], planned }) : null;
}

const loneOffer = ({ lone, planned }) => ok({ action: 'resume', todo: [...planned().filter(entry => entry.stack !== lone.stack).map(planEntry), resumeEntry(lone)] });

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
 * (see `stepFor`); a stop with nothing underway or a half-planned unit is a refusal (a stop with the partner underway finishes it, see `settleOnStop`); `ok(null)` when the unit is finished. `of(stack)` is the unit's sessions of a stack in attempt
 * order, `planned()` the entries of both stacks and `rerunOf(stack)` the entry of one stack's re-run.
 */
function unitStep(unit) {
  const states = Object.fromEntries(unit.order.map(stack => [stack, stateOfSessions(unit.of(stack))]));
  const stopped = stopProblem({ ...unit, states });
  if (stopped) return settleOnStop({ sessions: unit.order.flatMap(stack => unit.of(stack)), stopped });
  const lone = lonePlanned(unit);
  if (lone) return lone;
  const half = halfProblem({ ...unit, states });
  return half ? fail(half) : stepFor({ ...unit, states });
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

/** The first unfinished slot, after the read-back lines were taken into account: the round rule and the read-back rules are asked of it, then its step is worked out. */
function offerOf({ map, slots, lines }) {
  const seen = withRefuted({ map, slots, lines });
  const slot = slots.find(s => !finished(seen, s.slot));
  if (slot === undefined) return ok(null);
  const waiting = roundRuleProblem({ map: seen, slots, target: slot }) ?? linesProblem({ map: seen, slots, target: slot, lines });
  return waiting ? fail(waiting) : offerFor({ map: seen, slot });
}

/**
 * The next step of the series: `ok(slot + { action: 'plan' | 'resume' | 'rerun', todo })` for the first slot that is not finished, `ok(null)` when the series is done, `fail(sentence)` when nothing may run. `readbackLines` are the parsed
 * dated lines (`{ g, verdict, rid, date }`; null or anything else is no lines, an undated line is ignored). The slot is the first unfinished one, so the barrier holds by construction, and the round rule is asked of it all the same.
 */
export function nextSlot(args = {}) {
  const { slots } = args;
  const lines = verdictLines(args.readbackLines);
  const refused = gateProblem(args) ?? conflictProblem({ slots, lines });
  return refused ? fail(refused) : offerOf({ map: args.map, slots, lines });
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
export function pilotPlan({ pilot } = {}) {
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

/** The A/A run again: both twins, attempt 2, because one twin measures no spread. */
const aaAgain = aa => ok({ what: 'aa', action: 'again', todo: aaSessions({ aa, attempt: 2 }).map(planEntry) });

const voidedAt = (sessions, attempt) => sessions.some(s => s.attempt === attempt && isVoid(s));

const AFTER_REPEAT = 'the A/A has a void twin after its one repeat: the pilot cannot measure a spread; an instrument fix and a new go come before another run';

/** The A/A after its sessions ran: finished when no twin is void; a void twin makes it run again once, both twins; a void after that ends it. */
function aaSettled({ sessions, aa }) {
  if (!voidedAt(sessions, 1)) return ok(null);
  return sessions.every(s => s.attempt === 1) ? aaAgain(aa) : (voidedAt(sessions, 2) ? fail(AFTER_REPEAT) : ok(null));
}

const twinsOf = (sessions, attempt) => sessions.filter(s => s.attempt === attempt).map(s => s.twin).toSorted().join(',');

/** The first run (attempt) of the A/A whose twins are not exactly `a` and `b` (one twin only, or two of one letter), or undefined. */
const badRunOf = sessions => [...new Set(sessions.map(s => s.attempt))].find(attempt => twinsOf(sessions, attempt) !== 'a,b');

/** The sentence for an A/A run that does not hold the twins `a` and `b`, or null: the twins are planned together. */
function twinsProblem(sessions) {
  const run = badRunOf(sessions);
  return run === undefined ? null : `the A/A's run ${run} holds twins ${twinsOf(sessions, run)}, not a and b: the twins are planned together`;
}

/** The A/A: two twins of one task and one stack, one on each mask; a halt is resumed, a stop in one twin still lets the other, if underway, be finished (as for a slot) and nothing else runs. */
function aaStep({ map, plan }) {
  const sessions = pilotSessionsOf({ map, kind: AA_KIND, task: plan.task, stack: plan.stack });
  const stopped = sessions.find(s => stateOfSessions([s]) === 'stopped');
  if (stopped) return withWhat('aa', settleOnStop({ sessions, stopped: `the A/A ${stopped.twin} ended with a stop (${stopped.class}): a stopped series is never run again` }));
  return loneTwin({ sessions, plan }) ?? (twinsProblem(sessions) ? fail(twinsProblem(sessions)) : aaRun({ sessions, plan }));
}

/** A crash between planning the two twins of a run: the lone twin is only `planned`, so the other twin is planned first and then it is launched (as for a slot); a lone twin that got further is a refusal (`twinsProblem`). */
function loneTwin({ sessions, plan }) {
  const run = badRunOf(sessions);
  const held = sessions.filter(s => s.attempt === run);
  if (run === undefined || held.length !== 1 || held[0].status !== 'planned') return null;
  const missing = aaSessions({ aa: plan, attempt: run }).filter(entry => entry.twin !== held[0].twin);
  return ok({ what: 'aa', action: 'resume', todo: [...missing.map(planEntry), resumeEntry(held[0])] });
}

/** The A/A when no twin stopped and the twins are `a` and `b`: finish what is open, plan both, or judge the runs. */
function aaRun({ sessions, plan }) {
  const open = sessions.filter(isOpenSession);
  if (open.length > 0) return ok({ what: 'aa', action: 'resume', todo: open.map(resumeEntry) });
  return sessions.length === 0 ? ok({ what: 'aa', action: 'plan', todo: plan.sessions.map(planEntry) }) : aaSettled({ sessions, aa: plan });
}

const withWhat = (what, step) => (step.ok ? ok({ what, ...step.value }) : step);

/**
 * The next step of the pilot, in the order of step P: the A/A, the daemon pair, the web pair, each finished before the next. `ok({ what: 'aa' | 'pilot:<NN>', action: 'plan' | 'resume' | 'rerun' | 'again', todo })` (`todo` as for a slot: the sessions still to run and the step each needs),
 * `ok(null)` when nothing more is to run, `fail(sentence)`. A void pilot session is re-run once, alone; a void twin makes the A/A run again once, both twins, so the pilot is at most twelve sessions.
 */
export function pilotStep({ map, pilot } = {}) {
  const plan = pilotPlan({ pilot });
  if (!plan.ok) return plan;
  const missing = runMapProblem(map);
  if (missing) return fail(missing);
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
