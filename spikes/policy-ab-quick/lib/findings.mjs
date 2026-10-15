// Pure: what the report says that a table of numbers would hide - how each cell ended, what stopped the series, which numbers are
// estimates, and which scoring comparisons are not like for like.

/** How a cell ended: a crash the pilot recorded outranks the run's own outcome; a cell that never ran has none. */
export function outcomeOfCell(mapEntry, summary) {
  return mapEntry?.outcome ?? summary?.outcome ?? summary?.run?.outcome ?? null;
}

/**
 * USD spent on attempts that measured the account's limit, not the policy: earlier attempts of a cell that was re-run (kept as history), and
 * cells whose current outcome is void. Reported APART from the cells' own cost, never counted toward --stop-after.
 */
export function voidSpend(cells) {
  const usd = cells.filter(c => c.cell !== 'reference').reduce((sum, c) => sum
    + (c.attempts ?? []).filter(a => a.void).reduce((s, a) => s + (a.spendUsd ?? 0), 0)
    + (String(c.outcome ?? '').startsWith('void') ? (c.summary?.usd ?? 0) : 0), 0);
  return Math.round(usd * 100) / 100;
}

/** Every cell in the report was void (or the report has no cell): there is no data point to publish. */
export const allVoid = cells => {
  const real = cells.filter(c => c.cell !== 'reference');
  return real.length > 0 && real.every(c => String(c.outcome ?? '').startsWith('void'));
};

export function reportFindings({ cells, stop = null }) {
  const findings = [];
  if (stop) findings.push({ kind: 'series-stopped', before: stop.before, line: stop.line });
  for (const c of cells.filter(x => x.cell !== 'reference')) {
    if (c.outcome && c.outcome !== 'completed') findings.push({ kind: 'cell-outcome', cell: c.cell, outcome: c.outcome });
    if ((c.attempts ?? []).length > 0) findings.push({ kind: 'attempts', cell: c.cell, attempts: c.attempts.map(a => ({ n: a.n, outcome: a.outcome, spendUsd: a.spendUsd })) });
    if (c.summary && c.summary.costSource && c.summary.costSource !== 'envelope') findings.push({ kind: 'estimated-cost', cell: c.cell, costSource: c.summary.costSource, usd: c.summary.usd });
    if (c.ownSuiteVsReference?.fewerTestsThanReference) findings.push({ kind: 'fewer-tests-than-reference', cell: c.cell, candidateTests: c.ownSuiteVsReference.candidateTests, referenceTests: c.ownSuiteVsReference.referenceTests });
    // Blinding tripwires. The user-level guard in ~/.claude must stay out of every cell (--setting-sources project); a guard denial in an
    // arm A cell, or a plugin in the init event, would mean it did not.
    if (c.arm === 'A' && c.summary?.denials?.modelGuard > 0) findings.push({ kind: 'guard-denial-in-arm-A', cell: c.cell, denials: c.summary.denials.modelGuard });
    if ((c.summary?.init?.plugins ?? []).length > 0) findings.push({ kind: 'plugins-in-session', cell: c.cell, plugins: c.summary.init.plugins });
    if (c.ownSuiteVsReference?.verdict === 'fail') findings.push({ kind: 'new-test-failures', cell: c.cell, newFailures: c.ownSuiteVsReference.newFailures });
  }
  return findings;
}
