// The flags only the quick run's seams raise. They live apart from the emitters of the registered flags table (`inputs/analysis-contract.md` section 2, frozen with the pre-registration) because they are not part of it: they are
// documented in research/module_lang_bench.md, and tested in spikes/lang-explore/test/quick-wiring.test.mjs and quick-rescore-run.test.mjs.

/** The flags only the quick run's seams can raise: relative walks the scan could not place (low hits, not contamination), and large files outside the evidence paths that were not read. */
export function quickFlags({ scan, lowHits }) {
  const skipped = scan.skippedOutside ?? [];
  const skipFlags = skipped.length > 0 ? [`${skipped.length} large file(s) outside the evidence paths not scanned (${skipped.reduce((n, f) => n + f.size, 0)} bytes)`] : [];
  const lowFlags = lowHits.length > 0 ? [`low outside-path: ${lowHits.length} relative walk(s) in an unknown directory (not contamination)`] : [];
  return [...lowFlags, ...skipFlags];
}

/** Append only: a session a `rescore` re-classified says so, from its run-map entry (`reclassifiedFrom`, see lang-explore `rescore`). */
export const rescoreFlags = entry => (entry.reclassifiedFrom ? ['reclassified by rescore'] : []);
