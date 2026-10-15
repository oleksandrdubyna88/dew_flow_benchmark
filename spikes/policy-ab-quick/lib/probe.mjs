// Pure: the arm-parity probe. Before generation 1 both arms' trees are prepared (--verify-only), P's own suite is run on each, and the
// two trees' mounts are compared. The arms must fail the SAME tests (so the in-cell feedback is the same) and their mounts must
// differ in exactly the declared policy files.
import { assertArmsDifferOnlyInPolicy } from './mount.mjs';

const sorted = list => [...(list ?? [])].sort();

export function compareProbes({ suiteA, suiteB, mapA, mapB, policyFiles }) {
  const reasons = [];
  const a = new Set(suiteA?.failedCases ?? []);
  const b = new Set(suiteB?.failedCases ?? []);
  for (const [name, suite] of [['A', suiteA], ['B', suiteB]]) if (!suite || !suite.tests) reasons.push(`arm ${name}: the suite produced no report`);
  const onlyA = sorted([...a].filter(k => !b.has(k)));
  const onlyB = sorted([...b].filter(k => !a.has(k)));
  if (onlyA.length > 0 || onlyB.length > 0) reasons.push(`the failing sets differ: only in A [${onlyA.join(' | ')}], only in B [${onlyB.join(' | ')}]`);
  if (suiteA?.tests !== suiteB?.tests) reasons.push(`the suites ran ${suiteA?.tests} (A) and ${suiteB?.tests} (B) tests`);
  let delta = [];
  try { delta = assertArmsDifferOnlyInPolicy(mapA, mapB, policyFiles); } catch (e) { reasons.push(e.message); }
  return { pass: reasons.length === 0, reasons, failingSetA: sorted([...a]), failingSetB: sorted([...b]), testsA: suiteA?.tests ?? null, testsB: suiteB?.tests ?? null, mountDelta: delta };
}

/** The pilot's gate: a probe record that passed and was made for the story's current policy file list. */
export function probeGate(record, policyFiles) {
  if (!record) return { ok: false, why: 'no probe record: run probe-series.mjs for this story first' };
  if (!record.pass) return { ok: false, why: `the last probe failed: ${(record.reasons ?? []).join('; ')}` };
  if (JSON.stringify(sorted(record.policyFiles)) !== JSON.stringify(sorted(policyFiles))) return { ok: false, why: 'the probe was made for a different policy file list' };
  return { ok: true, why: '' };
}

/** Pure: the compiled test files (relative to the source folder) that hold the failing case keys (file::suites::name), once each. */
export function rerunTargets(failedCases) {
  return [...new Set(failedCases.map(k => k.split('::')[0]).filter(Boolean))].sort().map(f => `out/${f}`);
}

/**
 * Pure: a failure that does not repeat when its file runs ALONE is load-dependent, not the tree's. The probe compares what still
 * fails after the rerun, and records what did not repeat, so a timing test cannot split the arms.
 */
export function settleFailures(firstFailed, rerunFailed) {
  const again = new Set(rerunFailed);
  return { failedCases: firstFailed.filter(k => again.has(k)), flaky: firstFailed.filter(k => !again.has(k)) };
}
