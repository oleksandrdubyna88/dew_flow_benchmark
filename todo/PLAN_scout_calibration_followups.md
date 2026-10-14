# PLAN — Scout calibration follow-ups: the read API and tab, the alias watch, evidence 2, a decision run

> Status: **plan only, nothing implemented yet (2026-10-14).** Extracted from the implemented
> [PLAN_scout_model_calibration.md](../research/PLAN_scout_model_calibration.md) — its open tail.
> Scope: `src/Bench.Api`, `src/Bench.Ui`, the `bench scouts` verbs, `research/scout-calibration/`.
>
> Related: [module_scouts.md](../research/module_scouts.md), [RESULTS_scout_models.md](../research/RESULTS_scout_models.md),
> [architecture.md](../research/architecture.md).

## 1. The goal, before any solution

The calibration measured its answer from the command line: the results live in `research/`, and only `bench scouts report` shows a run.
Four things the parent plan named and did not ship:

1. **No one can see a scout run outside a terminal.** The gate and the probes each have a read API and a console tab
   (`GET /api/bench/probes/runs` — `src/Bench.Api/BenchApi.cs:21`, `src/Bench.Ui/Services/BenchConsoleApi.cs:91`,
   `src/Bench.Ui/Pages/ProbesBenchmark.razor`); the scouts have neither (parent plan D8).
2. **The `sonnet` and `opus` aliases can move under the rule without anyone noticing.** `bench scouts alias-check`
   (`hosts/Cli/ScoutsAliasCommand.cs:26`) exists and has run once; nothing runs it on a schedule (parent §3.14).
3. **The screened candidate list is not public.** The frozen draw is (`research/scout-calibration/tasks.json`, through the one
   projection with HMAC commitments), but evidence 2 — the whole screened list, private rows by label and commitment — has no writer.
4. **No decision run is possible yet.** The sizing (`src/Bench.Domain/Scouts/ScoutSizing.cs:110`) found no task count from 8 to 30
   at which the percentile bootstrap certifies a decision; the interval method is the operator's call. And the registered void rule
   (`src/Bench.Domain/Scouts/ScoutReadBack.cs:59`) voids a cell for a child's REFUSED call to a tool outside the read-only set — three
   Sonnet cells lost on one task for a `Bash` call that ran nothing — which a decision run should settle before it starts.

## 2. Build order

1. **S1 — read API.** `GET /api/bench/scouts/runs` and `/runs/{id}` from `ScoutReport` (the DTO `bench scouts report --json` prints),
   mapped in `MapBenchApi`, the read port resolved per request (503 when unregistered), as the probes do.
2. **S2 — the Scouts tab.** A page beside `ProbesBenchmark.razor`: run picker, the workload lines, the per-arm blocks with the cost
   split, polling through `LivePoller` while a cell is open; re-measuring stays a CLI verb shown as a command.
3. **S3 — the alias watch.** A weekly scheduled `bench scouts alias-check` (≈ $1) whose non-zero exit raises the operator's attention;
   the rule's suspension text points at it.
4. **S4 — evidence 2's writer.** `bench scouts candidates --publish <file>`: the screened list through the one projection.
5. **S5 — before any decision run:** the operator's interval method; a decision on the refused-call void rule (keep, or void only an
   out-of-set call that RAN); `--effort` recorded as part of the registered design.

## 3. Test plan

- S1: endpoint tests over a seeded store (the probes' `BenchApi` tests are the template) — list, one run, unknown id 404, unregistered 503.
- S2: bUnit page tests per the Blazor component rules — renders a finished run, polls an open one, disposes cleanly.
- S3: the schedule's wiring tested against `tests/FakeCli` (an alias that moved exits 3 and is reported).
- S4: the projection holds no private plaintext (`ScoutScrubberTests` over the written file) and reproduces the draw's input.
- S5: a RED test for whichever void rule the operator chooses, on the live refused-`Bash` stream.

## 4. Definition of Done

- [ ] A scout run is readable over HTTP and in the console, byte for byte what `report --json` prints.
- [ ] The alias watch runs weekly and its failure is visible.
- [ ] Evidence 2 is committed, hygiene-clean.
- [ ] The decision run's interval method and void rule are decided and recorded before it is planned.
- [ ] `research/module_scouts.md` and `research/architecture.md` describe what shipped; this plan promoted when done.
