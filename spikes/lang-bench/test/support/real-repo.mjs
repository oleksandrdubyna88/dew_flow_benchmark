// The task repository's checkout for the cases that run on the real repository (F2.S2 (f)): one helper, so every such case skips and fails the same way. lang-explore's tests may import it; lang-bench never imports lang-explore code.

/** The checkout named by `LANG_V2_TASK_REPO`, or `null` when the case skipped (`t.skip`): under BENCH_REQUIRE_SANDBOX=1 an unset variable throws instead. */
export function realRepo(t) {
  const repo = process.env.LANG_V2_TASK_REPO;
  if (repo !== undefined && repo !== '') return repo;
  const why = 'LANG_V2_TASK_REPO does not name the task repository checkout';
  if (process.env.BENCH_REQUIRE_SANDBOX === '1') throw new Error(`BENCH_REQUIRE_SANDBOX=1 and ${why}`);
  t.skip(why);
  return null;
}
