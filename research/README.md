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
| [PLAN_tool_telemetry_v0.md](PLAN_tool_telemetry_v0.md) | Design record, IMPLEMENTED 2026-08-15 — the founding plan's §5.4 made concrete: this repository owns the `telemetry/v0` schema, a local spool is the transport, `bench telemetry ingest` is idempotent and resumable, and the §7 AppHost stands up this benchmark's own Postgres. Emitter half: `dew_flow_mcp · research/PLAN_usage_telemetry.md` |

## Conventions

- A plan arrives here only when its work is done, with `> Status: **IMPLEMENTED, <YYYY-MM-DD>.**` and a
  record of what shipped **differently** from the plan. The deviations are the most valuable part.
- Architecture notes follow the `architecture.md` + `module_<name>.md` split once there is enough
  system to describe. Until then this folder holds only what is genuinely settled.
- Cross-repository citations are **paths, not links** (see [todo/README.md](../todo/README.md)): a
  relative link that resolves on one machine is worse than a citation that names its source.
