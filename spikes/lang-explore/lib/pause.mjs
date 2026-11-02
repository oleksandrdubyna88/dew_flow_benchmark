// The $60 pause point of the olympiad plan (research/PLAN_lang_explore_olympiad.md D-O7), exactly as written. The plan's spend is the sum of the broker-charged USD in the `ledger.ndjson` of every series THIS plan created
// (a series whose recorded task is one of `olympiad.planTasks`: never task 1's series, never a rehearsal's). `run` refuses BEFORE launching (exit 2, a stop, naming the spend) when `spent + projected > $60`, where
// `projected = 2 x max($5, 3 x the most expensive session of the plan so far)`: a pair's plausible worst case, since both sessions run at once and cannot be stopped between pairs mid-flight. Only `--allow-over "<the
// owner's words and date>"` passes it, and the text goes into the pair record. The per-session cap ($50, 75 min, 45 min stall) is a different guard and is untouched.
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { isOwnCharge } from '../../minigit-bench/lib/ledger.mjs';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { systemClock } from './clock.mjs';
import { listOf } from './idlist.mjs';
import { exploreChildren, ledgerFileOf } from './series.mjs';
import { taskOfSeries } from './tasks.mjs';

const usdText = n => `$${n.toFixed(4)}`;

const isObject = v => v !== null && typeof v === 'object' && !Array.isArray(v);

/**
 * What one direct child of the explore root is to the plan, whatever its name: `{ kind: 'plan' }` (its state names a task of the plan), `{ kind: 'other' }` (a series of another task, or a directory that holds no series),
 * or `{ kind: 'bad', why }` (it holds a state file or a ledger but its state cannot be read or classified: the spend cannot be counted).
 */
function classify(child, explore) {
  const task = taskNamed(child, explore);
  if (!task.ok) return { kind: task.absent ? 'other' : 'bad', why: task.reason };
  return explore.olympiad.planTasks.includes(task.value) ? planKind(child) : { kind: 'other' };
}

/** A series of the plan counts by its ledger; one without a ledger is never $0. */
const planKind = child => (child.hasLedger ? { kind: 'plan' } : { kind: 'bad', why: 'its ledger is missing' });

const knownTask = (task, explore) => typeof task === 'string' && Object.hasOwn(explore.tasks, task);

/** The task a state names, `ok(id)` (a state without one is task 1's), or why that cannot be told. */
function stateTask(state, explore) {
  if (!state.ok) return fail('its state cannot be read');
  const task = isObject(state.value) ? taskOfSeries(state.value) : null;
  return knownTask(task, explore) ? ok(task) : fail('its state names no task of the pilot');
}

/** The task a child names; a directory that holds neither a state file nor a ledger is no series (`absent`). */
function taskNamed(child, explore) {
  if (child.undeterminable) return fail('its type cannot be determined');
  return child.hasState || child.hasLedger ? stateTask(child.state, explore) : fail('it is no series', { absent: true });
}

/** The given children of the explore root classified: `{ plan: [dir], bad: ['name: why'] }`. */
function classifyAll(children, explore) {
  const all = children.map(child => ({ child, c: classify(child, explore) }));
  return { plan: all.filter(({ c }) => c.kind === 'plan').map(({ child }) => child.dir), bad: all.filter(({ c }) => c.kind === 'bad').map(({ child, c }) => `${child.name}: ${c.why}`) };
}

/** The series directories of the plan: every direct child of the explore root whose state names a task in `explore.olympiad.planTasks`, whatever its name. */
export const planSeriesDirs = (home, explore) => classifyAll(exploreChildren(home), explore).plan;

const validAmount = usd => typeof usd === 'number' && Number.isFinite(usd) && usd >= 0;

const parsed = text => { try { const v = JSON.parse(text); return isObject(v) ? ok(v) : fail('not an object'); } catch { return fail('not JSON'); } };

/**
 * The lines of a series' ledger as `{ rid, usd, line, valid, why? }` (own charges only: v1's events and carried lines are not spend). A line that is not a JSON object is `valid: false, why: 'json'` (v1's `readLedger` would
 * throw on it); an amount that is not a finite number of at least 0 is `valid: false, why: 'amount'`. Neither is ever read as 0, which would let a corrupt ledger pass the check.
 */
function chargesOf(dir) {
  const read = readLedgerText(dir);
  if (!read.ok) return [{ line: 0, valid: false, why: 'read', cause: read.reason }];
  const lines = read.value.split('\n').map((text, i) => ({ text, line: i + 1 })).filter(({ text }) => text.trim() !== '').map(({ text, line }) => ({ line, got: parsed(text) })); // physical line numbers: numbered BEFORE the blank lines are dropped
  const own = lines.filter(({ got }) => !got.ok || isOwnCharge(got.value));
  return own.map(({ line, got }) => (got.ok ? { rid: got.value.rid ?? `line-${line}`, usd: got.value.usd, line, valid: validAmount(got.value.usd), why: 'amount' } : { line, valid: false, why: 'json' }));
}

/** The text of a ledger: `ok(text)` | `fail(cause)`, the cause being the error code (ENOENT, EACCES, EISDIR, ...) or, without one, its message: a ledger that vanished or cannot be read since the scan is a stop, never a thrown error. */
function readLedgerText(dir) {
  try { return ok(readFileSync(ledgerFileOf(dir), 'utf8')); } catch (e) { return fail(e.code ?? e.message); }
}

/** What the plan has spent, scanning the explore root itself (the one-scan seam `children` belongs to `pauseVerdict` alone): the total, the most expensive session (its charges summed per series and rid), the counts and the ledger lines whose amount is invalid. */
export const planSpend = ({ home, explore }) => spendOf(planSeriesDirs(home, explore));

/** The spend of the given plan series directories: the total, the most expensive session, the counts and the ledger lines that cannot be added up. */
function spendOf(dirs) {
  const perSession = new Map();
  const invalid = [];
  for (const dir of dirs) {
    for (const c of chargesOf(dir)) {
      if (c.valid) perSession.set(`${path.basename(dir)}/${c.rid}`, (perSession.get(`${path.basename(dir)}/${c.rid}`) ?? 0) + c.usd);
      else invalid.push({ series: path.basename(dir), line: c.line, why: c.why, cause: c.cause });
    }
  }
  const costs = [...perSession.values()];
  return { series: dirs.length, sessions: costs.length, spentUsd: costs.reduce((a, b) => a + b, 0), maxSessionUsd: Math.max(0, ...costs), invalid };
}

/** `projected = pairSessions x max(floor, factor x the most expensive session so far)`. */
export const projectedUsd = (pause, maxSessionUsd) => pause.pairSessions * Math.max(pause.projectedFloorUsd, pause.projectedFactor * maxSessionUsd);

/** The sentence of a stop: the spend, the projection and the way past it. */
function stopSentence({ spend, projected, pause }) {
  return `the plan's spend is ${usdText(spend.spentUsd)} over ${spend.series} series and ${spend.sessions} session(s); this pair is projected at ${usdText(projected)} (${pause.pairSessions} x max($${pause.projectedFloorUsd}, ${pause.projectedFactor} x the most expensive session so far, ${usdText(spend.maxSessionUsd)})); ${usdText(spend.spentUsd + projected)} is over the pause point of $${pause.usd}. Stop and ask the owner: only --allow-over "<the owner's words and date>" passes it`;
}

/** Why the spend cannot be counted (children of the root whose state cannot be read or classified), or null: a stop that `--allow-over` does not pass. */
function unreadableProblem(bad) {
  return bad.length === 0 ? null : `${bad.length} series cannot be counted (${listOf(bad)}): the plan's spend cannot be counted, so the pause point stops here whatever --allow-over says; repair or remove the directory`;
}

/** The stop for ledger lines that cannot be added up (fail closed: `--allow-over` does not pass it). */
const invalidWhat = { amount: 'an amount that is not a finite number of at least 0', json: 'a line that is not a JSON object' };
const invalidLine = i => (i.why === 'read' ? `the ledger of series ${i.series} cannot be read (${i.cause})` : `the ledger of series ${i.series} holds ${invalidWhat[i.why]} (line ${i.line})`);
const invalidSentence = invalid => `${listOf(invalid.map(invalidLine))}: the plan's spend cannot be counted, so the pause point stops here whatever --allow-over says; repair the ledger`;

const blank = text => typeof text !== 'string' || text.trim() === '';

/**
 * The pause point's verdict for the next pair: `{ stop: true, message }`, or `{ stop: false, record }` where `record` is what the pair record keeps (`null` when no `--allow-over` was given).
 * `allowOver` is the owner's text or null.
 */
export function pauseVerdict({ home, explore, allowOver = null, now = systemClock, children = exploreChildren }) {
  const counted = countSpend({ home, explore, children });
  if (!counted.ok) return { stop: true, message: counted.reason };
  const spend = counted.value;
  const pause = explore.olympiad.pausePoint;
  const projected = projectedUsd(pause, spend.maxSessionUsd);
  const over = spend.spentUsd + projected > pause.usd;
  if (over && blank(allowOver)) return { stop: true, message: stopSentence({ spend, projected, pause }) };
  return { stop: false, record: recordOf({ allowOver, over, spend, projected, pause, now }) };
}

/**
 * The plan's spend, or the sentence saying it cannot be counted (a child that cannot be read or classified, a ledger that is invalid): `ok(spend)` | `fail`. The explore root is classified ONCE (`children`, the scan, is
 * called once) and the plan directories it collected are what is summed, so the check for bad children and the sum can never disagree.
 */
function countSpend({ home, explore, children }) {
  const classified = classifyAll(children(home), explore);
  const bad = unreadableProblem(classified.bad);
  if (bad) return fail(bad);
  const spend = spendOf([...classified.plan].sort());
  return spend.invalid.length > 0 ? fail(invalidSentence(spend.invalid)) : ok(spend);
}

/** What the pair record keeps of `--allow-over`: its text with the spend, the projection and the time (null when none was given). */
const recordOf = ({ allowOver, over, spend, projected, pause, now }) => (blank(allowOver) ? null : { text: allowOver.trim(), needed: over, spentUsd: spend.spentUsd, projectedUsd: projected, pausePointUsd: pause.usd, at: now().toISOString() });
