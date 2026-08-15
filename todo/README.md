# todo/ — open work

This folder holds **plans for work that is not finished**: proposals, task breakdowns, and implementation
plans that are still open.

Once this repository has documentation of the system *as it is*, that lives in `research/` —
architecture, module deep-dives, and the design records of decisions that already shipped. The convention is
the one used across the `dew_flow_*` family and `claudeRag`.

## The lifecycle

```
new plan  ──►  todo/PLAN_<topic>.md  ──►  implemented  ──►  research/PLAN_<topic>.md
                                                            + research/module_*.md updated
```

When a plan is fully implemented, move it with `git mv`, set its status line to `IMPLEMENTED <date>`, record
what shipped differently from the plan, and fix relative links in both directions. A partially implemented
plan stays where the **majority of its value** lives; its unfinished phases are extracted into a new `todo/`
plan rather than holding the whole document hostage.

## Naming

`PLAN_<snake_case_topic>.md`.

## What every plan carries

- A `> Status:` line on the second or third line — the first thing a reader needs.
- The symptom or goal, stated before any solution.
- Verified references to real code as `file.cs:line` — not guesses.
- A build order, a test plan, and a Definition of Done checklist.

## Related documents

- [../research/README.md](../research/README.md) — what belongs in `research/`, and what is there now.
  Most recently promoted: [PLAN_tool_telemetry_v0.md](../research/PLAN_tool_telemetry_v0.md) — the
  §5.4 tool-telemetry contract, its spool ingest, and the AppHost that stands up this benchmark's own
  Postgres (2026-08-15).
- [../research/MEASURED_LESSONS.md](../research/MEASURED_LESSONS.md) — the evidence base the founding
  plan cites. Carried over so no plan here depends on another checkout being present.
- [../CLAUDE.md](../CLAUDE.md) — the project rules; [../.claude/rules/](../.claude/rules/) holds the
  always-loaded and path-scoped ones.

## Cross-repository citations

This repository benchmarks software that lives elsewhere, so its plans cite other checkouts. Those citations
are written as **paths, not links** — `DewFlow · research/RESULTS_rag_eval_v3.md:1111-1114` — because a
relative link that only resolves on one machine is worse than a citation that names its source. `DewFlow ·`
means the `claudeRag` repository, where the measurement history that justifies this project lives.

## Currently open

| Plan | Status |
|---|---|
| [PLAN_rag_bench_repo.md](PLAN_rag_bench_repo.md) | **plan only, 2026-08-14** — the founding plan for this repository. A benchmark for **any repository at any commit, measured by any engine**: target is `(repoUrl, commitSha, exclusions)`, the engine is a parameter (ours, mindex, any HTTP service, and *no retrieval at all* as a first-class engine), suites are frozen and hashed with commit-scoped ground truth, and results are only comparable inside one measurement tuple. Two out-of-band metric modules — a hardware sampler with runs serialised on the accelerator, and a trace port with **both** black-box and white-box implementations, the latter carrying the retrieval funnel. Time in three buckets (tools · thinking · infrastructure wait). CLI first and shaped for an agent, API alongside, UI last in an RCL. Carries a §2 table of nine lessons already paid for in wrong numbers and near-misses, as specification rather than as copied code |
