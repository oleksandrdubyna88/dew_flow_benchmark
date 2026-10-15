// Pure: a candidate's own suite judged against the reference tree's failing set. Several of a story's compiled tests read the
// conventions mount or the generated modules, so "zero failures" is the wrong bar in a scoring copy; "no NEW failure" is the right one.

/** referenceFailed / candidateFailed: test-case names. A missing report on either side is its own verdict, never a pass. */
export function ownSuiteVsReference(candidate, reference) {
  if (!candidate || candidate.tests === 0 || candidate.tests === undefined) return { verdict: 'no-report', newFailures: [], referenceFailed: null };
  if (!reference || reference.tests === 0 || reference.tests === undefined) return { verdict: 'no-reference', newFailures: [], referenceFailed: null };
  const known = new Set(reference.failedCases ?? []);
  const newFailures = (candidate.failedCases ?? []).filter(n => !known.has(n));
  return { verdict: newFailures.length === 0 ? 'pass' : 'fail', newFailures, referenceFailed: known.size, referenceTests: reference.tests, candidateTests: candidate.tests };
}
