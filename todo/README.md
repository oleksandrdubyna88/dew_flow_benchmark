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
| [PLAN_variant_matrix.md](PLAN_variant_matrix.md) | **step 1 (the variant catalog) implemented 2026-08-16; steps 2–11 open** — the variant matrix: a Postgres question bank in five named groups (~100 questions each) with per-reviewer marks (`claude`/`codex`/`gemini`, extensible as data); retrieval variants as an immutable catalog so the engine configuration becomes a matrix axis beside subject and repeat; a test = frozen question selection × **all active variants** at a pinned commit, whose completion is derived — adding a variant reopens a finished test for exactly the new cells; every cell runs the full pipeline (retrieve → ask → score → multi-arbiter judge) with funnel, hits, thinking and sampling-as-sent persisted publication-safe; a library-first console (`Bench.Ui` RCL + thin `hosts/Web` shell in the existing AppHost, API-first as a gate); a model registry with per-test subject and ordered-arbiter roles (bridge-local included); an agent lane — cloud CLIs with native tools plus our MCP server. Engine half: `dew_flow_rag_qln · todo/PLAN_search_variant_axes.md` |
| [PLAN_code_lane.md](PLAN_code_lane.md) | **plan only, 2026-08-16** — group 6 of the bank: tasks a model must SOLVE (fix a live bug · implement a stated TODO), because reading saturated at 89–100 %. Authoring is a model's job with three gates (the bug reproduces, the reference fix works, the tree is rebuilt to the buggy state) and the **reviewers write the hidden tests**; solving runs Investigate → Implement → Verify → Judge with per-phase budgets in a per-leg sandbox worktree. Scored by two independent instruments: five mechanical signals (compiles · right file · hidden tests green · the solver's own test has teeth · neighbours intact) and an inflation-resistant delivered-work score ported — mechanism, not repository — from `scoreMeter` V2, whose zero band took 160 of 160 padded steps and made ×10 padding buy ×0.88. Its 13.2 % sampling spread is why the code lane repeats ≥ 2 |
| [PLAN_scoremeter_port.md](PLAN_scoremeter_port.md) | **plan only, 2026-08-16** — three things ported from the operator's `scoreMeter` V2, as ONE independent leaf module (`src/Bench.Delivered`, zero references, architecture-tested): the cleaned-LOC family (case-sensitive first-match-wins path fates, line normalization with continuation joining — both adapted for C#: `#if` is not a comment, obj/bin/Designer/g.cs get fates), the zero-band anchored weighting protocol (×10 padding bought ×0.88 in the source's measured arms; 160/160 padded steps zeroed) with coverage gate and deterministic policy over raw scores (raw + applied + rule persisted, `bench rescore` recomputes with zero model calls), and the frozen-arms measurement method — parity fixtures against published numbers, all inherited constants named with their source, an *inherited calibration* badge until the inflation property is re-verified on this corpus. Grain is deliberately NOT ported |
| [PLAN_reliability_tail.md](PLAN_reliability_tail.md) | **plan only, 2026-08-16** — the remainder of the 24/7 audit taken on the eve of the first long unattended runs. The four defects that could END a campaign (the unguarded drain loop, the sweep called by nothing, the missing signal handling, the null logger) are fixed in their own task; this is what is left: a circuit breaker so a dead endpoint fails the campaign in minutes instead of burning a 10-minute wall budget per remaining cell, two `GetOrAdd`-forever dictionaries that are inert only until the long-running worker wires them in, a run summary that hydrates every result row to print two integers, a spool ingest bounded by the emitter's productivity rather than by a chunk size, and the `logs/` retention owner the shared rule now demands be named |
| [PLAN_corpus_litter.md](PLAN_corpus_litter.md) | **plan only, 2026-08-15** — a benchmark run must not leave a corpus behind. Raised from a measurement in `dew_flow_rag_qln`: Qdrant held 24.38 GB of which **22 GB was rubbish**, nineteen collections left by test runs that minted a fresh project id each time. One aspnetcore corpus is ~2 GB and a 24-variant matrix is ~1.2 M points, so a sweep that leaks one per cell fills a disk in an afternoon — and reports it as "no space" during an unrelated run. Proposes a `bench_` namespace so "ours" is decidable from the name, delete-on-finish including the failure path, and a retention listing with a button rather than a silent timed sweep |
