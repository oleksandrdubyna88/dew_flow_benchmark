// The affinity read-back (F1.S2, section 2.3). The two CPU profiles (`6-7` and `0-7`) are applied to the pass child with `taskset`; a mask that was asked for is not a mask that was applied, so the child's own
// `Cpus_allowed_list` is read back and compared with the profile. Compared as SETS in any spelling (`0-7` is `0,1,2,3,4,5,6,7`), never as strings; a superset or a subset is another mask.
import { fail, ok } from '../../minigit-bench/lib/result.mjs';
import { runBounded } from '../../minigit-bench/lib/run.mjs';

const PROBE_TIMEOUT_MS = 15_000;
const LIST_PART = /^(\d+)(?:-(\d+))?$/;

/** The cpu numbers a part names (`'3'` or `'2-7'`), or null. */
function numbersOfPart(part) {
  const m = LIST_PART.exec(part);
  if (m === null) return null;
  const from = Number(m[1]);
  const to = m[2] === undefined ? from : Number(m[2]);
  return to >= from ? Array.from({ length: to - from + 1 }, (_, i) => from + i) : null;
}

const rangeText = run => (run.length === 1 ? String(run[0]) : `${run[0]}-${run.at(-1)}`);

/** Runs of consecutive numbers: `[1,3,4]` -> `[[1],[3,4]]`. */
const runsOf = sorted => sorted.reduce((runs, n) => (runs.length > 0 && runs.at(-1).at(-1) + 1 === n ? [...runs.slice(0, -1), [...runs.at(-1), n]] : [...runs, [n]]), []);

/** The one spelling of a cpu list: sorted, merged, ranges of two or more (`'0,1,2-7'` -> `'0-7'`, `'6,7'` -> `'6-7'`); null for an empty or malformed text. */
export function normalizeCpuList(text) {
  if (typeof text !== 'string' || text.trim() === '') return null;
  const parts = text.trim().split(',').map(p => numbersOfPart(p.trim()));
  return parts.includes(null) ? null : runsOf([...new Set(parts.flat())].sort((a, b) => a - b)).map(rangeText).join(',');
}

/** null when the observed list is exactly the wanted profile (in any spelling), else the sentence naming both masks. */
export function affinityProblem({ wanted, observed }) {
  const same = normalizeCpuList(wanted) !== null && normalizeCpuList(wanted) === normalizeCpuList(observed);
  return same ? null : `affinity-not-applied: wanted ${wanted}, observed ${observed ?? '(nothing read)'}`;
}

/** `Cpus_allowed_list` of a `/proc/<pid>/status` text, or null. */
export const allowedListOf = text => /^Cpus_allowed_list:\s*(\S+)/m.exec(text)?.[1] ?? null;

const ranFine = v => v.code === 0 && !v.timedOut;

/** What a mask really is: `taskset -c <cpus> cat /proc/self/status`, read. `spawn(exe, args, { timeoutMs })` is `runBounded`'s shape (the seam). -> `ok(observed)` | `fail`. */
export async function probeMask({ cpus, spawn = runBounded }) {
  const ran = await spawn('taskset', ['-c', cpus, 'cat', '/proc/self/status'], { timeoutMs: PROBE_TIMEOUT_MS });
  if (!ran.ok) return fail(`the mask probe for ${cpus} did not run: ${ran.reason}`);
  const observed = ranFine(ran.value) ? allowedListOf(ran.value.stdout) : null;
  return observed === null ? fail(`the mask probe for ${cpus} read no Cpus_allowed_list (exit ${ran.value.code}): ${ran.value.stderr.trim().slice(-200)}`) : ok(observed);
}
