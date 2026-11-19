// The disk side of a paid launch of Stage 1 (F3.S1 (a), PR 4; stories document section 5, brief item 4). Two checks, both answering `{ go: true }` or `{ go: false, halt, exit: 3, reason }` (exit 3: a resumable halt, the same command goes on once the
// space is there), and neither ever passing on a size it could not read:
//   reserveGate   before EVERY paid launch: free disk is at least the bound of everything still to run, which is every session the series still holds (the launch's own included) times the worst case of ONE session with its retries and its
//                 one void re-run. Before the A/A that is the parent's whole-series bound, the 66 sessions; a pilot repeat after a NO GO adds its own two sessions (`extraCells: 2`) to the launch that runs it.
//   ruleGate      before every slot: v1's `diskRuleVerdict`, free disk of at least twice the pair's measured worst case.
//   diskGate      both, in that order; a halt writes a `disk-rule` / `disk-reserve` EVENT line into the ledger (an event, never a charge).
// The worst case of a pair is `growth.json`'s figures (the scoring's built copy and the retained records) plus TWO session folders (the parent's *Growth*: a kept session is about 1.4 GB with its box). The session folder's size is computed
// live and written nowhere: the larger of the floor the manifest registered and the largest ENDED session folder of this series as it is on disk, a freed session counted at the size its `freed.json` lists.
import { existsSync, readFileSync, statfsSync } from 'node:fs';
import path from 'node:path';
import { appendLedger } from '../../minigit-bench/lib/ledger.mjs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { systemClock } from './clock.mjs';
import { diskRuleVerdict, growthProblem } from './growth.mjs';
import { MAX_ATTEMPTS, STACK_ORDER, isVoid, stateOfSessions } from './series.mjs';
import { STAGE_ROUNDS, pilotPlan } from './stage-schedule.mjs';
import { FREEZE_TASKS } from './workspaces-sources.mjs';
import { walkEntries } from './workspaces-tree.mjs';

export const HALT_EXIT = 3;
export const RESERVE_HALT = 'disk-reserve';
export const RULE_HALT = 'disk-rule';
const FREED_FILE = 'freed.json';
const DATA_SLOTS = STAGE_ROUNDS * FREEZE_TASKS.length;
const FINAL_STATES = new Set(['done', 'void-final', 'stopped']);

const wholePositive = n => Number.isSafeInteger(n) && n > 0;
const halted = (halt, reason) => ({ go: false, halt, exit: HALT_EXIT, reason });
const sessionDir = (series, rid) => path.join(series, 's', rid);

/** The free bytes of the volume holding `dir` (what a normal user may still write). */
export function freeBytesOf(dir) {
  const st = statfsSync(dir);
  return Number(st.bavail) * Number(st.bsize);
}

/** The bytes of every regular file under `dir` (links are never followed; a folder that is not there holds 0). */
export const folderBytes = dir => (existsSync(dir) ? walkEntries(dir).reduce((n, e) => (e.kind === 'file' ? n + e.size : n), 0) : 0);

/** `freed.json` of a session folder, parsed: `{ removed: [...] }` | null when there is none | `{ unreadable }`. */
function freedOf(dir) {
  const file = path.join(dir, FREED_FILE);
  if (!existsSync(file)) return null;
  try { return JSON.parse(readFileSync(file, 'utf8')); } catch (e) { return { unreadable: e.message }; }
}

/** The bytes a freed session's list still owes: the listed files that are no longer on disk, at their listed size. */
function freedBytes(dir) {
  const freed = freedOf(dir);
  return (freed?.removed ?? []).filter(f => !existsSync(path.join(dir, f.path))).reduce((n, f) => n + (Number.isSafeInteger(f.bytes) ? f.bytes : 0), 0);
}

/** One session folder's size: what it holds plus what its `freed.json` lists as removed (a freed session counts at the size it had). */
export const sessionBytesOf = dir => folderBytes(dir) + freedBytes(dir);

const isEnded = s => s.status === 'ran' || s.status === 'scored';

/** The sentences for a `freed.json` that cannot be read (its sizes would be missing from the count). */
const freedListProblems = ({ series, map }) => Object.values(map?.sessions ?? {}).filter(isEnded)
  .filter(s => freedOf(sessionDir(series, s.rid))?.unreadable !== undefined).map(s => `${path.join(sessionDir(series, s.rid), FREED_FILE)} cannot be read: what the session held is unknown`);

/** The size the rule reads for one session folder, from the registered floor and this series: `{ bytes, source: 'floor' | 'series' }`. A session that has not ended is not read. Written nowhere. */
export function sessionFloor({ floorBytes, series, map }) {
  const own = Object.values(map?.sessions ?? {}).filter(isEnded).map(s => sessionBytesOf(sessionDir(series, s.rid)));
  const largest = Math.max(0, ...own);
  return largest > floorBytes ? { bytes: largest, source: 'series' } : { bytes: floorBytes, source: 'floor' };
}

// ---- what is still to run ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

const voidsOf = sessions => sessions.filter(isVoid).length;

/** The sessions one cell (a stack of a slot, a pilot session) may still need: none when it is final, else its attempts less its voids (a void is re-run once). */
const cellFactor = sessions => (FINAL_STATES.has(stateOfSessions(sessions)) ? 0 : MAX_ATTEMPTS - voidsOf(sessions));

const sessionsAt = (map, pick) => Object.values(map.sessions).filter(pick).sort((a, b) => a.attempt - b.attempt);

const dataCells = map => Array.from({ length: DATA_SLOTS }, (_, i) => i + 1).flatMap(slot => STACK_ORDER.map(stack => sessionsAt(map, s => s.kind === 'data' && s.pair === slot && s.stack === stack)));

const lastScored = sessions => sessions.at(-1)?.status === 'scored';

const twinsOf = ({ map, aa }) => ['a', 'b'].map(twin => sessionsAt(map, s => s.kind === 'aa' && s.task === aa.task && s.stack === aa.stack && s.twin === twin));
const twinFactor = sessions => (stateOfSessions(sessions) === 'stopped' ? 0 : MAX_ATTEMPTS - voidsOf(sessions));

/** The A/A's two twins: both scored ends it; otherwise each twin may still run (twice less its voids), because a void twin runs the A/A again with both twins. */
function aaCells({ map, aa }) {
  const twins = twinsOf({ map, aa });
  return twins.every(lastScored) ? twins.map(() => 0) : twins.map(twinFactor);
}

const pairCells = ({ map, pair }) => STACK_ORDER.map(stack => cellFactor(sessionsAt(map, s => s.kind === 'pilot' && s.task === pair.task && s.stack === stack)));

/**
 * `ok(n)`: the most sessions the series may still run, the sessions of the launch included: for each cell of the 66 (60 data cells, the A/A's two twins and the four sessions of the pilot's two pairs) what it may still need, a void's one
 * re-run counted; `extraCells` (2 for a pilot repeat pair) adds that many cells at the full factor. `pilot` is the pre-registration's answer (`{ aa: { task, stack }, daemon, web }`). -> `ok(n)` | `fail(sentence)`.
 */
export function sessionsToRun({ map, pilot, extraCells = 0 }) {
  const plan = pilotPlan({ pilot });
  if (!plan.ok) return fail(`the sessions still to run cannot be counted: ${plan.reason}`);
  const data = dataCells(map).map(cellFactor);
  const pilotCells = [...aaCells({ map, aa: plan.value.aa }), ...plan.value.pairs.flatMap(pair => pairCells({ map, pair }))];
  return ok([...data, ...pilotCells].reduce((n, f) => n + f, 0) + extraCells * MAX_ATTEMPTS);
}

// ---- the sizes -----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

const FLOOR_REFUSAL = 'floorBytes (the registered size of a session folder) must be a positive whole number of bytes: a missing size is never read as 0';

/** The sentence for a size that is missing or zero (the growth record, the registered floor, a `freed.json` that cannot be read), or null. */
const sizeProblem = ({ series, map, growth, floorBytes }) => growthProblem(growth) ?? (wholePositive(floorBytes) ? null : FLOOR_REFUSAL) ?? freedListProblems({ series, map })[0] ?? null;

/** `ok({ pair, session })`: the worst case of a pair and of one session, in bytes, or `fail(sentence)` naming what is missing (a missing or zero size is a refusal, never a rule that passes). */
function sizing(args) {
  const problem = sizeProblem(args);
  if (problem !== null) return fail(`the disk check cannot be made: ${problem}`);
  const { bytes } = sessionFloor(args);
  const pair = args.growth.pairWorstCaseBytes + 2 * bytes;
  return ok({ pair, session: Math.ceil(pair / 2) });
}

const freeCheck = freeBytes => (wholePositive(freeBytes) ? ok(freeBytes) : fail(`the free disk cannot be read (${String(freeBytes)}): freeBytes must be a positive whole number of bytes`));

/** `ok({ session, left })` when the sizes, the count and the free bytes are all known, else the first `fail` that names what is not. */
function reserveFacts(args) {
  const sized = sizing(args);
  if (!sized.ok) return sized;
  const left = sessionsToRun(args);
  if (!left.ok) return left;
  const free = freeCheck(args.freeBytes);
  return free.ok ? ok({ session: sized.value.session, left: left.value }) : free;
}

const reserveSentence = ({ args, facts, need }) => `${args.freeBytes} bytes are free and the rest of the series needs ${need} bytes (${facts.left} sessions at most, each ${facts.session} bytes with its retries and its one void re-run${(args.extraCells ?? 0) > 0 ? `, a pilot repeat of ${args.extraCells} sessions included` : ''})`;

/** The reserve before a paid launch (see the head of this file). `{ series, map, pilot, growth, floorBytes, freeBytes, extraCells }`. */
export function reserveGate(args) {
  const facts = reserveFacts(args);
  if (!facts.ok) return halted(RESERVE_HALT, facts.reason);
  const need = facts.value.left * facts.value.session;
  return args.freeBytes >= need ? { go: true, need } : halted(RESERVE_HALT, reserveSentence({ args, facts: facts.value, need }));
}

/** The disk rule before a slot: free disk of at least twice the pair's worst case (v1's `diskRuleVerdict`, over the live session size). */
export function ruleGate(args) {
  const sized = sizing(args);
  const verdict = sized.ok ? diskRuleVerdict({ freeBytes: args.freeBytes, pairWorstCaseBytes: sized.value.pair }) : { go: false, reason: sized.reason };
  return verdict.go ? { go: true } : halted(RULE_HALT, verdict.reason);
}

/** The event line of a halt: an event, never a charge (it carries no `usd` and no `rid`). */
const eventOf = ({ verdict, args, now }) => ({ event: verdict.halt, t: now().toISOString(), freeBytes: args.freeBytes ?? null, ...(args.slot === undefined ? {} : { slot: args.slot }), reason: verdict.reason });

/**
 * The reserve, then the rule: the first that halts. A halt writes its event line into `ledgerFile` when one is given; a go writes nothing. `slot` names the slot in the line. Pure but for the ledger.
 */
export function diskGate(args) {
  const verdict = [reserveGate, ruleGate].map(gate => gate(args)).find(v => !v.go) ?? { go: true };
  if (!verdict.go && args.ledgerFile) appendLedger(args.ledgerFile, eventOf({ verdict, args, now: args.now ?? systemClock }));
  return verdict;
}
