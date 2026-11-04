# PLAN — the analysis half of the part-6 and part-7 experiment, in C#

> Status: **plan only, nothing implemented yet, 2026-11-01.** Scope: the analysis half that [PLAN_model_arms.md](../research/PLAN_model_arms.md) §5 defers.

The readers of what a `model-arms` run leaves — metrics, survival, comparison, tables, the coai findings and cost — ported from the shell harness's node scripts; the run half is the module [research/module_model_arms.md](../research/module_model_arms.md), and until this plan lands the pinned shell readers run unmodified over a C# arm (E6.S1's consumer check). Only this status line and scope are written (E6.S3); the goal, the boundary table mirrored from the parent's §5 and the plan's build order are for the plan role to write when it is taken up, and it must carry from its first line: the rename `task-<id>-haiku.*` → `task-<id>-coder.*` (D5); a resumed step's cost is the session's LAST result, not the sum of its attempts (E2 §8 item 62 — `metrics.mjs` sums, `metrics6.mjs` does not); `compare.mjs` lists no `CR*`, `CH*` or `SR*` arm; delegation is read from tool events and subagent messages, never from `modelUsage` alone, and the A–D re-run of E2 §8 item 43 WAS made on 2026-11-04 (claude 2.1.310): the deny holds; a resumed session's `result` carries the session's cumulative `modelUsage` and `total_cost_usd` (run D's sonnet line equals run C's), which explains the 2026-10-30 anomaly (C's 2026-10-30 cost was not recorded, so the equality is not checked for that day) — take the last result per session, never the sum; if the first real experiment uses a build other than 2.1.310, the A–D re-run is repeated on it (E6 step 14); a start line is counted per stream (E2 §8 items 55–56); the published record never carries `mcp-coai.json`, `mcp-none.json` or `coai-data/` (E2 §8 item 14); the readers E6.S1 did not run, each with its reason (E6 §8 item 6); and that a run after E6.S4 (2026-11-01, the commit `feat(model-arms): E6.S4` on #132) ran WITHOUT the operator's ssh agent and inherited `GIT_*` in the arm's session while part 6 ran with both, so a comparison across that line says so (ledger 3.2; no marker in the arm's files — the line is a date and a commit).

2026-11-04: the boundary table below was added, mirrored from the parent's §5 (item 20 of E6 §8 and the E6 Definition of Done ask for it); the goal and the build order are still for the plan role to write.

## Boundary (mirrored from the parent's §5, 2026-11-04)

The same table stands in [PLAN_model_arms.md](../research/PLAN_model_arms.md) §5, so the boundary is named on both sides. In it "this plan" is the parent (the run half) and "the analysis plan" is this document. Order: the parent first, because it lands the layout this plan reads. The last column says the two parts are disjoint in every row.

| item | this plan | the analysis plan | disjoint? |
|---|---|---|---|
| the files an arm leaves (§4.3) | writes them, pins the layout | reads them, never writes | yes |
| `timing.jsonl` | writer | reader (wall per attempt, the price fit) | yes |
| `objective.json` | writer (§4.9) | reader (tables, the judge's input) | yes |
| session transcripts under `~/.claude` | records the id and window | reads tokens from them | yes |
| judges, mutation, survival, tables | — | all | yes |
| the seven checked-in readers (`PinnedReaders.Run`: `metrics.mjs`, `t-survival.mjs`, `compare.mjs`, `make-tables.mjs`, part 6's `metrics6.mjs`, `coai-findings.mjs`, `coai-cost.mjs`) | RUNS them over a fake arm as the compatibility proof (D28, E6) — never edits them | ports them; carries the one rename (`task-<id>-haiku.*` → `task-<id>-coder.*`, D5) into the port | yes |
| the part-7 templates and the CHH scheme | shipped and configured (D15, D25) | the judges' templates (`5-judge-abs`, `judge-coai`) and their runs | yes |
