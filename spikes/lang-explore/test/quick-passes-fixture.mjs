// The invented passes the verdict, driver and snapshot tests share: three invented cases (`s01`, `h001`, `h002`), pass and attempt builders in the shape of the plan's section 2. No quick-run tree, no reference, no private file.
import { runnerRecordPinNow } from '../lib/harness-pin.mjs';
import { CASE_VERDICT, PASS_COUNT, profileOfPass } from '../lib/verdict.mjs';
import { rec } from './quick-score-fixture.mjs';

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

// ---- the files of a scoring on disk (section 2.10): attempts and events written through the library's own writers ----------------------------------------------------------------------------------------------------
export const PINNED = runnerRecordPinNow();
export const sortedIds = () => [...IDS].sort();
/** The runner's record of an invented run: the pinned marker and one result per id. */
export const runnerRecord = (ids = sortedIds(), over = {}) => ({ format: PINNED.format, runner: PINNED.version, grammar: 2, kind: 'daemon', build: { ok: true }, launch: { failed: false, stderr: '' }, cases: ids.map(id => rec(id)), ...over });
export const completedBody = (over = {}) => ({ kind: 'completed', record: runnerRecord(), runner: PINNED.version, memoryCertified: true, observedCpus: '6-7', startedAt: '2026-11-07T10:00:00.000Z', ms: 1500, ...over });
export const environmentBody = (reason = 'the box did not start', over = {}) => ({ kind: 'environment', reason, observedCpus: null, startedAt: '2026-11-07T10:00:00.000Z', ms: 20, ...over });
export const BUILD_RECORD = Object.freeze({ treeSha256: 'a'.repeat(64), ok: true, exit: 0, timedOut: false, wallMs: 1234, tail: '', frozenChanges: [], harnessDrift: [], nonRegularLeftOut: [], committedAt: '2026-11-07T09:59:00.000Z' });
