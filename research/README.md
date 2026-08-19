# research/ — the system as it is

This folder holds **documentation of what exists**: architecture, module deep-dives, and the design
records of decisions that already shipped — including plans whose work is done, kept because the code
references them and they explain why the system looks the way it does.

Open work lives in [todo/](../todo/README.md). The test is a single question:

> Is someone still supposed to build this?

Yes → `todo/`. No → `research/`.

## Currently here

| Document | What it is |
|---|---|
| [architecture.md](architecture.md) | The system as it is — the layers and why two projects depend on nothing, the measurement tuple, one leg end to end as a sequence, the two vantage points on a tool call, the guards that shape the API, and an explicit list of what does NOT exist yet |
| [SPIKE_dotnet_eval_library.md](SPIKE_dotnet_eval_library.md) | Whether `Microsoft.Extensions.AI.Evaluation` could carry this benchmark's scoring, and what it does and does not answer — the record of a question asked before a dependency was taken |
| [MEASURED_LESSONS.md](MEASURED_LESSONS.md) | The evidence base this project is built on — carried over from an earlier measurement programme against a different codebase, so nothing here depends on that repository being checked out. Every guard in the domain traces to a numbered finding in it |
| [PLAN_run_report.md](PLAN_run_report.md) | Design record, IMPLEMENTED 2026-08-19 — the comparison comes out of the store. `SeedSplit.Proof`, `Discrimination.Over` and `MetricByDimension.Legs` were written, tested and called by NOTHING: the split that guards against this programme's three reversed conclusions had never once been consulted. Now `bench report` and `bench-api` answer with one object — the metric along four axes, every arm on both halves, `Unproven` printed as a word rather than as a smaller win, a thin mean printing its numbers and withholding the ranking, and no baseline ever nominated by score. Its build found three defects the plan had not predicted: an aggregate hydrating every prompt of a run to average one number, an exit code that called a bad invocation an environment failure, and a split that would have reshuffled at every suite freeze |
| [PLAN_question_authoring.md](PLAN_question_authoring.md) | Design record, IMPLEMENTED 2026-08-18 — three CLI agents author the bank and three review it. `ICliAgentRuntime` over the one launcher (grown a stdin), a hashed `prompts/` catalog, answers admitted through the bank's EXISTING rules and never repaired, reviewer slots that name their model as data, self-review refused with a cost printed when it is allowed, unanimity as the only promotion rule, and a mechanical gate that stops a launch on a broken anchor or a non-discriminating scoring term. Measured: **17 accepted of 22, ~1.5 min per review, ~7 min per accepted question** — and the panel found nothing the arithmetic could not. Open tail: `todo/PLAN_question_bank_coverage.md` |
| [PLAN_reliability_tail.md](PLAN_reliability_tail.md) | Design record, IMPLEMENTED 2026-08-16 — what the 24/7 audit left after its campaign-ending defects were fixed, and how each remaining item shipped: **one deadline per leg** (`LegDeadline`) so a looping lane cannot multiply a per-call wall by a turn count nobody bounded, budgets **confirmed by the runtime** before a cell exists, both `GetOrAdd`-forever dictionaries bounded, the run summary counted in SQL instead of hydrated, a chunked spool ingest, the `Win32Exception` a best-effort kill used to leak, and `logs/` retention owned by the host at startup |
| [PLAN_tool_telemetry_v0.md](PLAN_tool_telemetry_v0.md) | Design record, IMPLEMENTED 2026-08-15 — the founding plan's §5.4 made concrete: this repository owns the `telemetry/v0` schema, a local spool is the transport, `bench telemetry ingest` is idempotent and resumable, and the §7 AppHost stands up this benchmark's own Postgres. Emitter half: `dew_flow_mcp · research/PLAN_usage_telemetry.md` |

## Conventions

- A plan arrives here only when its work is done, with `> Status: **IMPLEMENTED, <YYYY-MM-DD>.**` and a
  record of what shipped **differently** from the plan. The deviations are the most valuable part.
- Architecture notes follow the `architecture.md` + `module_<name>.md` split once there is enough
  system to describe. Until then this folder holds only what is genuinely settled.
- Cross-repository citations are **paths, not links** (see [todo/README.md](../todo/README.md)): a
  relative link that resolves on one machine is worse than a citation that names its source.
