// A series of the pilot (plan D-X1): one directory `~/rsd/lang-v2/explore/x-<yyyymmdd>-<4 hex>/` holding the run-map (v1's, kind `pilot`), the ledger, the records and the sessions. The pure part decides the pair
// plan: which sessions a `run --pair k` plans, on which masks (the draw decides pair 1, the stacks swap every pair), and the BARRIER (pair k+1 is refused until both sessions of pair k are collected, scored and
// recorded; a void is re-run once, alone, as `ran unpaired`). The impure part creates and loads the directory.
import { randomBytes, randomInt } from 'node:crypto';
import { existsSync, lstatSync, mkdirSync, readFileSync, readdirSync, realpathSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { pilotRid } from '../../minigit-bench/lib/bare-plan.mjs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { openSeries, planSession, readRunMap, writeRunMap } from '../../minigit-bench/lib/run-map.mjs';
import { systemClock } from './clock.mjs';
import { exploreRoot } from './paths.mjs';
import { taskOfSeries } from './tasks.mjs';

export const MASKS = Object.freeze({ low: '0-5', high: '8-13' });
export const STACK_ORDER = Object.freeze(['csharp', 'node']);
export const MAX_ATTEMPTS = 2;

/** `<prefix>-<yyyymmdd>-<4 hex>`: `x` for a series of the pilot, `r` for a rehearsal (so no command picks a rehearsal by its name). */
export const seriesIdOf = ({ date, rand, prefix = 'x' }) => `${prefix}-${date.toISOString().slice(0, 10).replaceAll('-', '')}-${rand}`;

/** The draw that decides pair 1: C# on the high mask or not. Recorded in the series; a random draw, not a choice. */
export const drawMask = (rand = () => randomInt(2)) => ({ csharpHigh: rand() === 1, method: 'crypto.randomInt(2) at init' });

/** The CPU mask of a stack in a pair: the draw decides pair 1, the two stacks swap every pair. */
export function maskOf({ draw, pair, stack }) {
  const csharpHigh = draw.csharpHigh !== ((pair - 1) % 2 === 1);
  const high = stack === 'csharp' ? csharpHigh : !csharpHigh;
  return high ? MASKS.high : MASKS.low;
}

/** The sessions of a pair, by stack, in attempt order. */
export const sessionsOf = (map, pair, stack) => Object.values(map.sessions).filter(s => s.pair === pair && s.kind === 'pilot' && (stack === undefined || s.stack === stack)).sort((a, b) => a.attempt - b.attempt);

const isVoid = s => s.status === 'ran' && s.data === false && String(s.class).startsWith('void');
const isStopped = s => s.status === 'ran' && s.data === false;

/** The sessions of a pair that a halt or a failure left open: planned, launching (a crash), or ended with data and not yet scored. A resume launches, settles and scores these; it plans no new session. */
export const openSessions = (map, pair) => sessionsOf(map, pair).filter(s => s.status === 'planned' || s.status === 'launching' || (s.status === 'ran' && s.data === true));

const voidState = attempts => (attempts >= MAX_ATTEMPTS ? 'void-final' : 'void-rerunnable');

/** The state of a stack's LAST session in a pair (`attempts` sessions so far). */
function stateOfLast(last, attempts) {
  if (last.status === 'scored') return 'done';
  if (isVoid(last)) return voidState(attempts);
  return isStopped(last) ? 'stopped' : 'open';
}

/** A stack's state in a pair: `none` (never planned), `open` (planned or launching), `done` (a data session, scored), `void-rerunnable`, `void-final` (the one re-run was used) or `stopped`. */
export function stackState(map, pair, stack) {
  const all = sessionsOf(map, pair, stack);
  return all.length > 0 ? stateOfLast(all.at(-1), all.length) : 'none';
}

/** Is a pair finished: every stack scored, or void after its one re-run. */
export const pairComplete = (map, pair) => STACK_ORDER.every(stack => ['done', 'void-final'].includes(stackState(map, pair, stack)));

const validPair = (pair, pairs) => Number.isInteger(pair) && pair >= 1 && pair <= pairs;
const stateLine = (map, pair) => STACK_ORDER.map(s => `${s}: ${stackState(map, pair, s)}`).join(', ');
const rerunHint = (map, pair) => (STACK_ORDER.some(s => stackState(map, pair, s) === 'void-rerunnable') ? `; a void is re-run once, alone: run --pair ${pair} --rerun <stack>` : '');

/** The sentence that refuses pair `pair`, or null: an earlier pair that is not finished, an unknown number. */
export function barrierProblem({ map, pair, pairs }) {
  if (!validPair(pair, pairs)) return `pair ${pair}: the pilot has pairs 1 to ${pairs}`;
  const behind = Array.from({ length: pair - 1 }, (_, i) => i + 1).find(j => !pairComplete(map, j));
  return behind === undefined ? null : `pair ${behind} is not finished (${stateLine(map, behind)}): both sessions of a pair are collected, scored and recorded before the next pair is paid for${rerunHint(map, behind)}`;
}

const rerunProblem = (map, pair, stack) => {
  const state = stackState(map, pair, stack);
  if (state === 'void-rerunnable') return null;
  return `pair ${pair} ${stack} is ${state}: only a session that ended as a void, and only once, is re-run`;
};

/**
 * The plan of `run --pair k [--rerun <stack>]`: the run-map with its sessions planned, and what was planned. A new pair plans both stacks (attempt 1, `paired`, the draw's masks); a re-run plans the one stack
 * (attempt 2, `ran unpaired`) and only when its last session is a void. A pair with sessions a halt or a failure left open (see `openSessions`) is RESUMED: the map comes back unchanged with
 * `planned: []` and `resumed: [rid]`. -> `ok({ map, planned, resumed? })` | `fail(sentence)`.
 */
export function planPair({ map, pair, pairs, draw, rerun = null, kind = 'pilot', rand = () => randomBytes(4).toString('hex') }) {
  const refused = barrierProblem({ map, pair, pairs });
  if (refused) return fail(refused);
  const open = openSessions(map, pair);
  if (open.length > 0) return ok({ map, planned: [], resumed: open.map(s => s.rid) }); // F2: what a halt or a failure left open is resumed, never planned again
  return planNew({ map, pair, draw, rerun, kind, rand });
}

/** A pair with nothing open: a re-run of a void, or (never run) both stacks. */
function planNew({ map, pair, draw, rerun, kind, rand }) {
  if (rerun !== null) return planRerun({ map, pair, draw, stack: rerun, kind, rand });
  if (sessionsOf(map, pair).length > 0) return fail(`pair ${pair} has already been run (${stateLine(map, pair)}): a void is re-run with --rerun <stack>`);
  return planAll({ map, entries: STACK_ORDER.map(stack => ({ stack, attempt: 1, label: 'paired' })), pair, draw, kind, rand });
}

function planRerun({ map, pair, draw, stack, kind, rand }) {
  if (!STACK_ORDER.includes(stack)) return fail(`--rerun ${stack}: the stacks are ${STACK_ORDER.join(', ')}`);
  const problem = rerunProblem(map, pair, stack);
  if (problem) return fail(problem);
  const attempt = sessionsOf(map, pair, stack).length + 1;
  return planAll({ map, entries: [{ stack, attempt, label: 'ran unpaired' }], pair, draw, kind, rand });
}

function planAll({ map, entries, pair, draw, kind, rand }) {
  let current = map;
  const planned = [];
  for (const e of entries) {
    const rid = pilotRid(rand());
    const entry = { rid, pair, stack: e.stack, phase: 'v1', attempt: e.attempt, kind, label: e.label, mask: maskOf({ draw, pair, stack: e.stack }) };
    const next = planSession(current, entry);
    if (!next.ok) return next;
    current = next.value;
    planned.push(entry);
  }
  return ok({ map: current, planned });
}

// ---- the directory ----------------------------------------------------------------------------------------------------------------

const stateFile = dir => path.join(dir, 'explore-series.json');
export const runMapFileOf = dir => path.join(dir, 'run-map.json');
export const ledgerFileOf = dir => path.join(dir, 'ledger.ndjson');

const taskProblem = (explore, task) => (typeof task === 'string' && Object.hasOwn(explore.tasks ?? {}, task) ? null : `a series is made for a task of the pins (${Object.keys(explore.tasks ?? {}).join(', ')}), not for '${task}'`);

/** Create a series directory (mode 0700) with its state, run-map, empty ledger and the run's inputs. A series is made for ONE task (`task`, an id of the pins) and records it. -> `ok({ dir, state })` | `fail`. */
export function createSeries({ home, explore, calibration, task, inputsExtra = {}, draw = drawMask(), date = systemClock(), prefix = 'x', rand = () => randomBytes(2).toString('hex') }) {
  const unknownTask = taskProblem(explore, task);
  if (unknownTask) return fail(unknownTask);
  const id = seriesIdOf({ date, rand: rand(), prefix });
  const dir = path.join(exploreRoot(home), id);
  if (existsSync(dir)) return fail(`${dir} already exists: a series id is never reused`);
  mkdirSync(path.join(dir, 's'), { recursive: true, mode: 0o700 });
  mkdirSync(path.join(dir, 'pairs'), { mode: 0o700 });
  mkdirSync(path.join(dir, 'canaries'), { mode: 0o700 });
  mkdirSync(path.join(dir, 'private'), { mode: 0o700 });
  const state = { seriesId: id, createdAt: date.toISOString(), draw, pairs: explore.pairs, task };
  writeFileSync(stateFile(dir), `${JSON.stringify(state, null, 2)}\n`, { mode: 0o600 });
  writeFileSync(path.join(dir, 'inputs.json'), `${JSON.stringify({ seriesId: id, task, limits: calibration.limits, caps: explore.caps, locale: explore.locale, envExtra: explore.envExtra, ...(prefix === 'r' ? { rehearsal: true } : {}), ...inputsExtra }, null, 2)}\n`, { mode: 0o600 });
  writeFileSync(ledgerFileOf(dir), '', { mode: 0o600 });
  writeRunMap(runMapFileOf(dir), openSeries({ seriesId: id, caps: explore.caps }));
  return ok({ dir, state });
}

/** The series' state and run-map. -> `ok({ dir, state, map })` | `fail`. */
export function loadSeries(dir) {
  if (!existsSync(stateFile(dir))) return fail(`${dir} is not a series of the pilot (no explore-series.json)`);
  const state = readState(dir);
  const map = state.ok ? readRunMap(runMapFileOf(dir)) : state;
  return map.ok ? ok({ dir, state: state.value, map: map.value }) : map;
}

/** The series' state file: `ok(state)` | `fail` naming the file when it cannot be read or is not JSON (a series that cannot be read is never taken for task 1). */
function readState(dir) {
  try { return ok(JSON.parse(readFileSync(stateFile(dir), 'utf8'))); } catch (e) { return fail(`${stateFile(dir)} is unreadable: ${e.message}`); }
}

/** The name of a series a paid run works on: `x-<yyyymmdd>-<4 hex>`, nothing else (a rehearsal is `r-`, a test's `t-`; a copy or a rename is no series of the plan's to run). */
export const CANONICAL_SERIES = /^x-\d{8}-[0-9a-f]{4}$/;

/** Every `x-...` series directory under the explore root (never a rehearsal's, never a test's). */
const allSeriesDirs = home => {
  const root = exploreRoot(home);
  return existsSync(root) ? readdirSync(root).filter(n => CANONICAL_SERIES.test(n)).map(n => path.join(root, n)) : [];
};

/**
 * Every direct child of the explore root that is a real directory, or whose type is not known (a link of any kind, a pipe, a socket, a device, an entry that cannot be examined), whatever its name, with what it holds:
 * `{ dir, name, state, hasState, hasLedger, undeterminable }` (`state` is `ok(state)` | `fail`). A plain file (`spin.json`) is no child of interest; an entry that cannot be examined is never dropped.
 */
export function exploreChildren(home) {
  const root = exploreRoot(home);
  const entries = (existsSync(root) ? readdirSync(root) : []).map(name => path.join(root, name)).map(p => ({ p, type: typeOf(p) }));
  return entries.filter(({ type }) => type !== 'file').map(({ p, type }) => (type === 'unknown' ? undeterminable(p) : describeChild(p)));
}

/**
 * A directory child: whether it holds a state file and a ledger, and its state when it holds one (the file's existence is checked BEFORE it is read). A child that holds neither is "no series" only if it is STILL a real
 * directory (checked with lstat, links not followed): one that vanished or was replaced since the scan saw it is undeterminable, never silently "no series".
 */
export function describeChild(p) {
  const held = { hasState: existsSync(stateFile(p)), hasLedger: existsSync(ledgerFileOf(p)) };
  return !held.hasState && !held.hasLedger && !isRealDirectory(p) ? undeterminable(p) : { dir: p, name: path.basename(p), state: stateIfAny(p, held.hasState), ...held, undeterminable: false };
}

const stateIfAny = (p, hasState) => (hasState ? readState(p) : fail('it has no state file'));

const undeterminable = p => ({ dir: p, name: path.basename(p), state: fail('its type cannot be determined'), hasState: false, hasLedger: false, undeterminable: true });

/**
 * What an entry of the explore root is, WITHOUT following links: `dir` (a real directory), `file` (a plain file) or `unknown` (a link of any kind, a pipe, a socket, a device, or an entry that cannot be examined). Only the first two
 * are known; the pause check stops on the third.
 */
function typeOf(p) {
  try {
    const st = lstatSync(p);
    return st.isDirectory() ? 'dir' : (st.isFile() ? 'file' : 'unknown');
  } catch {
    return 'unknown';
  }
}

const isRealDirectory = p => typeOf(p) === 'dir';

/** The series directories of the pilot, oldest first by creation time, optionally only those of `task`. A series whose state cannot be read is in none of them (see `unreadableSeries`). */
export function seriesDirs(home, task = null) {
  const readable = allSeriesDirs(home).map(d => [d, readState(d)]).filter(([, s]) => s.ok);
  return readable.filter(([, s]) => task === null || taskOfSeries(s.value) === task).map(([d, s]) => [s.value.createdAt ?? '', d]).sort((a, b) => a[0].localeCompare(b[0])).map(([, d]) => d);
}

/** The sentence refusing a series directory that is not one of the explore root's own (a direct child, links resolved), or null. */
export function seriesRootProblem(home, dir) {
  const root = exploreRoot(home);
  try {
    return path.dirname(realpathSync(dir)) === realpathSync(root) ? null : `${dir} is not a series directly under ${root}: a command works on the explore root's own series only`;
  } catch {
    return `${dir} is not a series directly under ${root} (it cannot be resolved)`;
  }
}

/** The sentence refusing a RUN on a directory whose name is not canonical (`nameOk` is the test of the name, canonical unless a test says otherwise), or null. */
export const canonicalNameProblem = (dir, nameOk = n => CANONICAL_SERIES.test(n)) => (nameOk(path.basename(dir)) ? null : `${path.basename(dir)} is not a canonical series name (x-yyyymmdd-4 hex): a paid pair runs on a series made by \`explore.mjs init\`, never on a copy or a rename`);

/** The newest series of the pilot for `task` (never a rehearsal) by its creation time, or null. */
export function latestSeries(home, task) {
  const sorted = seriesDirs(home, task);
  return sorted.length > 0 ? sorted.at(-1) : null;
}

/** The series' `inputs.json` (limits, caps, subject, the registered prompts frozen at `init`). -> `ok(inputs)` | `fail`. */
export function seriesInputs(dir) {
  try { return ok(JSON.parse(readFileSync(path.join(dir, 'inputs.json'), 'utf8'))); } catch (e) { return fail(`${dir}/inputs.json is unreadable: ${e.message}`); }
}

/** The prompts frozen into the series at `init`: what a session of this series is sent and read back against, whatever `inputs/prompts.json` says later. */
export function seriesPrompts(dir) {
  const inputs = seriesInputs(dir);
  if (!inputs.ok) return inputs;
  return inputs.value.prompts ? ok(inputs.value.prompts) : fail(`${dir}/inputs.json holds no frozen prompts: make the series with \`explore.mjs init\``);
}

/** Is this series a rehearsal (its inputs say so)? */
export function isRehearsal(dir) {
  try { return JSON.parse(readFileSync(path.join(dir, 'inputs.json'), 'utf8')).rehearsal === true; } catch { return false; }
}
