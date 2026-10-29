// Pure interval helpers on [startMs, endMs] pairs. Nothing here sums spans: `lengthOf` measures ONE merged union.

/** Merge overlapping or touching intervals into a sorted, disjoint list. */
export function mergeIntervals(intervals) {
  const sorted = intervals.filter(([s, e]) => e > s).sort((a, b) => a[0] - b[0] || a[1] - b[1]);
  const merged = [];
  for (const [s, e] of sorted) {
    const last = merged.at(-1);
    if (last && s <= last[1]) merged[merged.length - 1] = [last[0], Math.max(last[1], e)];
    else merged.push([s, e]);
  }
  return merged;
}

/** The intersection of two DISJOINT sorted lists (as `mergeIntervals` returns them). */
export function intersectMerged(a, b) {
  const out = [];
  let i = 0;
  let j = 0;
  while (i < a.length && j < b.length) {
    const s = Math.max(a[i][0], b[j][0]);
    const e = Math.min(a[i][1], b[j][1]);
    if (e > s) out.push([s, e]);
    if (a[i][1] < b[j][1]) i += 1; else j += 1;
  }
  return out;
}

/** Total length of a disjoint list. */
export const lengthOf = merged => merged.reduce((n, [s, e]) => n + (e - s), 0);
