// What a session's scoring costs on disk and how that bounds the series (F1.S2, *Growth*): the pair's worst case, the disk rule that halts before a pair, the bootstrap's own temporary guard, and `inputs/growth.json`, the
// measured record the rule reads. The record is validated on write and on load (a missing or zero size is a refusal naming the field, never a default of 0), and written last, atomically, so an ordinary run cannot start on a
// half-measured number.
import { readFileSync, renameSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { INPUTS } from './paths.mjs';

export const GIB = 1024 ** 3;
/** The bootstrap's temporary guard (it holds for the first measurement only): the free disk required before the first subject is copied, and the most the measurement folder may hold. */
export const MEASURE_FREE_FLOOR_BYTES = 60 * GIB;
export const MEASURE_WRITE_CEILING_BYTES = 20 * GIB;
export const MOST_ATTEMPTS = 30;
export const GROWTH_FILE = path.join(INPUTS, 'growth.json');

const wholePositive = n => Number.isSafeInteger(n) && n > 0;
const missing = fields => fields.filter(f => !wholePositive(f.value)).map(f => f.name);

/** The sentence naming the numbers that are not positive whole numbers, or null. */
function numbersProblem(fields) {
  const bad = missing(fields);
  return bad.length === 0 ? null : `${bad.join(', ')} must be a positive whole number of bytes (a missing number is never read as 0)`;
}

/** `ok(2 * sessionTreeBytes + 2 * retainedTenPassesWorstBytes)`: two sessions' trees and twice the measured retained size of ten passes with every re-run. A missing number is a refusal naming it. */
export function pairWorstCaseBytes({ sessionTreeBytes, retainedTenPassesWorstBytes } = {}) {
  const problem = numbersProblem([{ name: 'sessionTreeBytes', value: sessionTreeBytes }, { name: 'retainedTenPassesWorstBytes', value: retainedTenPassesWorstBytes }]);
  return problem ? fail(problem) : ok(2 * sessionTreeBytes + 2 * retainedTenPassesWorstBytes);
}

/** The disk rule before a pair: go when the free bytes are at least twice the pair's measured worst case. -> `{ go: true } | { go: false, reason }`. */
export function diskRuleVerdict({ freeBytes, pairWorstCaseBytes: worst } = {}) {
  const problem = numbersProblem([{ name: 'freeBytes', value: freeBytes }, { name: 'pairWorstCaseBytes', value: worst }]);
  if (problem) return { go: false, reason: `the disk rule cannot be read: ${problem}` };
  return freeBytes >= 2 * worst ? { go: true } : { go: false, reason: `${freeBytes} bytes are free and the rule needs twice the pair's measured worst case, ${2 * worst} bytes` };
}

/** The bootstrap's guard: the free disk under the floor, or the measurement folder past the ceiling. -> `{ go: true } | { go: false, reason }`. */
export function measureGuard({ freeBytes, folderBytes }) {
  if (freeBytes < MEASURE_FREE_FLOOR_BYTES) return { go: false, reason: `${freeBytes} bytes are free, under the measurement's floor of ${MEASURE_FREE_FLOOR_BYTES} bytes` };
  return folderBytes > MEASURE_WRITE_CEILING_BYTES ? { go: false, reason: `the measurement folder holds ${folderBytes} bytes, past its ceiling of ${MEASURE_WRITE_CEILING_BYTES} bytes` } : { go: true };
}

const text = v => typeof v === 'string' && v.trim() !== '';
const isMap = x => x !== null && typeof x === 'object';

const textProblem = r => (text(r.measuredOn) && text(r.host) ? null : 'measuredOn and host must be non-empty text');
const sizesProblem = r => numbersProblem(['sessionTreeBytes', 'retainedTenPassesBytes', 'retainedTenPassesWorstBytes', 'pairWorstCaseBytes'].map(name => ({ name, value: r[name] })));
const attemptsProblem = r => (wholePositive(r.mostAttempts) && r.mostAttempts <= MOST_ATTEMPTS ? null : `mostAttempts must be a whole number from 1 to ${MOST_ATTEMPTS}`);

const wallEntries = r => Object.entries(isMap(r.tenPassesWallMs) ? r.tenPassesWallMs : {});
const wallsSentence = bad => `tenPassesWallMs must name at least one subject, each with a positive whole number of milliseconds (${bad.join(', ') || 'none named'})`;

function wallsProblem(r) {
  const entries = wallEntries(r);
  const bad = entries.filter(([, ms]) => !wholePositive(ms)).map(([name]) => name);
  return entries.length > 0 && bad.length === 0 ? null : wallsSentence(bad);
}

function formulaProblem(r) {
  const formula = pairWorstCaseBytes(r);
  return formula.ok && r.pairWorstCaseBytes === formula.value ? null : `pairWorstCaseBytes is ${r.pairWorstCaseBytes} and the formula (2 * sessionTreeBytes + 2 * retainedTenPassesWorstBytes) gives ${formula.value}`;
}

const GROWTH_CHECKS = Object.freeze([textProblem, sizesProblem, wallsProblem, attemptsProblem, formulaProblem]);

/** The first thing wrong with a growth record, as a sentence naming the field, or null. */
export const growthProblem = record => GROWTH_CHECKS.values().map(check => check(record ?? {})).find(Boolean) ?? null;

/** Validate `record`, write it to a temp name beside `file`, rename. A refused record writes nothing. -> `ok(file)` | `fail`. */
export function writeGrowth({ file = GROWTH_FILE, record }) {
  const problem = growthProblem(record);
  if (problem) return fail(`${file} was not written: ${problem}`);
  const tmp = `${file}.${process.pid}.tmp`;
  writeFileSync(tmp, `${JSON.stringify(record, null, 2)}\n`);
  renameSync(tmp, file);
  return ok(file);
}

/** The growth record, read and validated by the same check. -> `ok(record)` | `fail` naming the file. */
export function loadGrowth({ file = GROWTH_FILE } = {}) {
  let record;
  try { record = JSON.parse(readFileSync(file, 'utf8')); } catch (e) { return fail(`${file} is missing or unreadable (${e.code ?? e.message}): the first measurement writes it, and no ordinary scoring run starts without it`); }
  const problem = growthProblem(record);
  return problem ? fail(`${file} is not a valid growth record: ${problem}`) : ok(record);
}
