// The invented passes the verdict, driver and snapshot tests share: three invented cases (`s01`, `h001`, `h002`), pass and attempt builders in the shape of the plan's section 2. No quick-run tree, no reference, no private file.
import { CASE_VERDICT, PASS_COUNT, profileOfPass } from '../lib/verdict.mjs';

export const MIB = 1024 * 1024;
export const LIMIT_BYTES = 512 * MIB;
export const IDS = Object.freeze(['s01', 'h001', 'h002']);

/** A case in the pilot's shape (what `casesOfRecord` makes): `verdict` a word of `CASE_VERDICT`. */
export const caseOf = (id, verdict = CASE_VERDICT.pass, over = {}) => ({
  id, set: id.startsWith('s') ? 'visible' : 'heldout', verdict, wallMs: 300, peakBytes: 100 * MIB, memoryMaxBytes: LIMIT_BYTES, exit: null, events: null,
  reason: verdict === CASE_VERDICT.pass ? '' : 'invented reason', rIds: [], comparison: null, firstMismatch: null, leftover: null, oomKill: null, ...over,
});
export const casesWith = (failing = {}) => IDS.map(id => caseOf(id, failing[id] ?? CASE_VERDICT.pass));
export const completed = (attempt, cases = casesWith()) => ({ attempt, kind: 'completed', cases, runner: '0.x', memoryCertified: true });
export const environment = (attempt, reason = 'the box did not start') => ({ attempt, kind: 'environment', reason });
export const passOf = (n, attempts) => ({ n, profile: profileOfPass(n).name, cpus: profileOfPass(n).cpus, attempts });
export const cleanPass = n => passOf(n, [completed(1)]);
export const failingPass = (n, failing = { h001: CASE_VERDICT.wrongAnswer }) => passOf(n, [completed(1, casesWith(failing))]);
export const exhaustedPass = n => passOf(n, [environment(1), environment(2), environment(3)]);

/** Ten passes, clean unless `over` replaces pass n (`{ 3: failingPass(3) }`). */
export const passesOf = (over = {}) => Array.from({ length: PASS_COUNT }, (_, i) => over[i + 1] ?? cleanPass(i + 1));
export const BUILD_OK = Object.freeze({ ok: true, exit: 0, timedOut: false, wallMs: 1234, tail: '' });
