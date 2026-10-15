// Pure: is a recorded score a real measurement, and does a tree have what scoring needs? A resume re-runs the pilot's closing steps; the
// reference tree was shed by the first run (node_modules gone), and scoring it bare produced compile errors and "no report" - a score of the
// environment, not of the code. The reference is scored once, or re-prepared completely first.

/** A score is valid when every hold-out file was compiled and emitted and the own suite produced a report. */
export function isValidScore(score) {
  const holdout = score?.holdout;
  if (!Array.isArray(holdout) || holdout.length === 0) return false;
  if (!holdout.every(h => h.emitted === true && ['pass', 'fail'].includes(h.verdict))) return false;
  return (score.ownSuite?.tests ?? 0) > 0;
}

/** What to do for the reference: keep a valid score; otherwise score it, installing first when its dependencies are gone. */
export function referencePlan({ score, treeExists, hasNodeModules, skipNpm }) {
  if (isValidScore(score)) return { action: 'keep' };
  return { action: 'score', build: !treeExists, install: !skipNpm && (!treeExists || !hasNodeModules) };
}
