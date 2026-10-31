// The samplers (E2.S2 B-S3): once a second the harness reads every /proc/<pid>/cmdline and /proc/<pid>/environ it may read and counts the
// PROCESSES whose cmdline or environment hold the REAL token and the dummy. Only counts leave this module: the values are compared in memory
// and never written, so the sampler cannot become the leak it looks for. Unreadable entries are counted, never assumed clean.
const GONE = new Set(['ENOENT', 'ESRCH']);

export const emptyCounts = () => Object.freeze({ samples: 0, realInCmdline: 0, realInEnviron: 0, dummyInCmdline: 0, dummyInEnviron: 0, unreadable: 0 });

/** Counts for one pass over the process table (not including `samples`). */
function pass(procApi, { real, dummy }) {
  const counts = { realInCmdline: 0, realInEnviron: 0, dummyInCmdline: 0, dummyInEnviron: 0, unreadable: 0 };
  for (const pid of procApi.pids()) {
    try {
      const cmdline = procApi.read(pid, 'cmdline');
      const environ = procApi.read(pid, 'environ');
      if (real && cmdline.includes(real)) counts.realInCmdline += 1;
      if (real && environ.includes(real)) counts.realInEnviron += 1;
      if (dummy && cmdline.includes(dummy)) counts.dummyInCmdline += 1;
      if (dummy && environ.includes(dummy)) counts.dummyInEnviron += 1;
    } catch (e) {
      if (!GONE.has(e.code)) counts.unreadable += 1;
    }
  }
  return counts;
}

/** A new totals object: `totals` plus one sample. */
export function sampleOnce(totals, procApi, secrets) {
  const c = pass(procApi, secrets);
  return Object.freeze({
    samples: totals.samples + 1, realInCmdline: totals.realInCmdline + c.realInCmdline, realInEnviron: totals.realInEnviron + c.realInEnviron,
    dummyInCmdline: totals.dummyInCmdline + c.dummyInCmdline, dummyInEnviron: totals.dummyInEnviron + c.dummyInEnviron, unreadable: totals.unreadable + c.unreadable,
  });
}
