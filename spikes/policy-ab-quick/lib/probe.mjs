// Pure: the arm-parity probe. Before generation 1 both arms' trees are prepared (--verify-only), P's own suite is run on each, and the
// two trees' mounts are compared. The arms must fail the SAME tests (so the in-cell feedback is the same) and their mounts must
// differ in exactly the declared policy files.
import { assertArmsDifferOnlyInPolicy } from './mount.mjs';

const sorted = list => [...(list ?? [])].sort();

export function compareProbes({ suiteA, suiteB, mapA, mapB, policyFiles, sessionStartA = null, sessionStartB = null }) {
  const reasons = [];
  // The rule-load signal must be the same in both arms: if the SessionStart hook's output mentions the rule in one arm only, the arms are not
  // getting the same instructions at session start.
  const ruleLoadA = sessionStartA?.mentionsRule ?? null;
  const ruleLoadB = sessionStartB?.mentionsRule ?? null;
  if (ruleLoadA === null || ruleLoadB === null) reasons.push('a SessionStart probe is missing: the rule-load signal was not recorded');
  else if (ruleLoadA !== ruleLoadB) reasons.push(`the rule-load signal differs: SessionStart output mentions the rule in A=${ruleLoadA}, B=${ruleLoadB}`);
  const a = new Set(suiteA?.failedCases ?? []);
  const b = new Set(suiteB?.failedCases ?? []);
  for (const [name, suite] of [['A', suiteA], ['B', suiteB]]) if (!suite || !suite.tests) reasons.push(`arm ${name}: the suite produced no report`);
  const onlyA = sorted([...a].filter(k => !b.has(k)));
  const onlyB = sorted([...b].filter(k => !a.has(k)));
  if (onlyA.length > 0 || onlyB.length > 0) reasons.push(`the failing sets differ: only in A [${onlyA.join(' | ')}], only in B [${onlyB.join(' | ')}]`);
  if (suiteA?.tests !== suiteB?.tests) reasons.push(`the suites ran ${suiteA?.tests} (A) and ${suiteB?.tests} (B) tests`);
  let delta = [];
  try { delta = assertArmsDifferOnlyInPolicy(mapA, mapB, policyFiles); } catch (e) { reasons.push(e.message); }
  return { pass: reasons.length === 0, reasons, sessionStartMentionsRule: { A: ruleLoadA, B: ruleLoadB }, failingSetA: sorted([...a]), failingSetB: sorted([...b]), testsA: suiteA?.tests ?? null, testsB: suiteB?.tests ?? null, mountDelta: delta };
}

/** What a probe is made FOR: if any of it changed, the probe says nothing about the series about to start. */
export function probeSubject({ story, pin, armShas, harness }) {
  return { storyId: story.id, P: story.P, mountPin: pin, armShas, policyFiles: sorted(story.policyFiles), harnessSpikeTree: harness.spikeTree };
}

/** The pilot's gate: a passing probe, made for exactly this story, base pin, arm commits, policy files and harness version, on a clean folder. */
export function probeGate(record, expected, currentHarness = { dirty: false }) {
  if (!record) return { ok: false, why: 'no probe record: run probe-series.mjs for this story first' };
  if (!record.pass) return { ok: false, why: `the last probe failed: ${(record.reasons ?? []).join('; ')}` };
  const was = record.subject ?? {};
  const differs = Object.keys(expected).filter(k => JSON.stringify(was[k]) !== JSON.stringify(expected[k]));
  if (differs.length > 0) return { ok: false, why: `the probe was made for something else (${differs.join(', ')} differ): run probe-series.mjs again` };
  if (record.harness?.dirty || currentHarness.dirty) return { ok: false, why: 'the harness folder has uncommitted changes (at probe time or now): commit, then probe again' };
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
