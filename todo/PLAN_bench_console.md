# PLAN — the benchmark console, mounted as DewFlow's *Benchmarking* section

> Status: **steps 1-5 implemented 2026-08-20; two items of step 5 open.** The console exists, is tested
> (29 new tests: 7 pure grouping, 6 client, 10 rendered pages, 2 route mounting, 2 summary contract, 2
> metric names -- the suite went 1010 to 1039) and is wired into the DewFlow daemon as its **Benchmarking** section. Open: the
> sibling-path `ProjectReference` is not yet the submodule this should end at (pinning a commit needs the
> benchmark pushed to its remote, which this session may not do), and nothing has yet rendered it LIVE --
> that needs `ConnectionStrings:bench` on the daemon and a restart, both of which are the operator's
> environment rather than code. Scope: `src/Bench.Ui`, a route-prefix change in `src/Bench.Api`, and the
> mount in `dew_flow_rag_qln`.
>
> Related docs: [PLAN_run_report.md](../research/PLAN_run_report.md),
> [PLAN_variant_matrix.md](PLAN_variant_matrix.md).

## The goal, and the thing it is NOT

The measurements have no screen. `bench report` renders one run to a terminal and `bench-api` answers
JSON; a person asking *"is the WSL sidecar slower than the Windows one"* has nowhere to look.

So: build the console pages here, and mount them as a **Benchmarking** section in the DewFlow left menu —
beside Companies, Search, Settings, Runtime, Infrastructure, MCP.

**What this plan does not do, stated first because it is what the request was really about.** It does not
declare a winner between sidecars. The compute arm is recorded on the RUN
(`EngineRef.Backend`, `src/Bench.Domain/Runs/Axes.cs:116`), so comparing two arms means comparing two
RUNS — and cross-run comparison was deliberately excluded from the report
([PLAN_run_report.md](../research/PLAN_run_report.md) §3.9), because two runs are only comparable inside a
`ComparisonScope` that nothing yet establishes. Two runs on different machines, suites or index
fingerprints produce a difference that says nothing about sidecars.

This plan therefore puts the arms **side by side and refuses to rank them**, naming the reason on screen.
That refusal is the deliverable, not a placeholder: a console that quietly ranked two incomparable runs
would manufacture exactly the false winner this harness exists to catch. The ranking needs a
cross-run use case, and that is its own plan.

## The shape is not invented — the MCP slice already is it

`dew_flow_rag_qln` mounts a sibling repository's console through a seam built for it,
`IDaemonModule` (`src/Rag.Api/DaemonModule.cs:19`): a module declares its endpoints, its Razor page
assembly, and its published pages. `McpModule` (`hosts/Daemon/McpModule.cs`) is thirty lines.

| Piece | The MCP precedent | This plan |
|---|---|---|
| the sibling repo arrives | git submodule `external/dew_flow_mcp` | submodule `external/dew_flow_benchmark` |
| the pages | `Mcp.Ui`, `Microsoft.NET.Sdk.Razor`, WASM-safe, references only `Mcp.Contracts` | `Bench.Ui`, same |
| registration | `AddMcpUi()`, `hosts/Daemon/Program.cs:98` | `AddBenchUi()` |
| the API | `MapMcpApi()` under `/api/mcp` | `MapBenchApi()` under `/api/bench` |
| the module | `McpModule : IDaemonModule` | `BenchModule : IDaemonModule` |
| routing | `Routes.razor` `_slices` + `NavMenu.razor` | one entry each |

Following it rather than inventing a second mechanism is the whole reason this plan is short.

`src/Bench.Contracts/Bench.Contracts.csproj` has **no dependencies at all** and its comment already says
*"shared by the CLI, the API and (later) the UI"* — the library this plan needs was anticipated.

## Build order

### 1. `/api` → `/api/bench`

`src/Bench.Api/BenchApi.cs:23` maps its group at `/api`, which collides with the daemon's own namespace
the moment it is mounted beside it. One line. The prefix is asserted **nowhere** (verified: the string
occurs only at its definition), so nothing else moves.

### 2. The arm becomes its own field on the run summary

`RunSummaryDto` (`src/Bench.Contracts/RunReportContracts.cs:87`) carries `EngineCanonical`, a pipe-joined
string that *contains* the arm. Add `ComputeArm` beside it, filled from
`ComputeBackend.Canonical` (`{Host}/{Provider}/{Device}`, `src/Bench.Domain/Retrieval/ComputeBackend.cs:54`)
in `RunReportContract.From(BenchRun)` (`src/Bench.Application/RunReportContract.cs:48`).

A console that had to split a canonical string to find the axis it groups by would be parsing a format
that exists for equality, not for reading — and an undeclared backend must arrive as its own state rather
than as an empty segment between two pipes.

### 3. `Bench.Ui`, and the decisions pulled out of the markup

New RCL, mirroring `Mcp.Ui`'s csproj exactly: `SupportedPlatform browser`,
`Microsoft.AspNetCore.Components.Web`, `DependencyInjection.Abstractions`, `Bench.Contracts`, nothing more.
It must not see `Bench.Infrastructure` — Npgsql and a PowerShell probe do not run in a browser.

- `Services/BenchConsoleApi.cs` — the read side, following `McpConsoleApi`: every method answers a
  `Read<T>` carrying whether it arrived and why it did not, so "the API is down" and "no runs yet" cannot
  render as the same empty table. `Read<T>` is re-declared here rather than shared: the two repositories
  have no common ancestor to put it in, and a submodule of MCP inside the benchmark to borrow one record
  would be a worse trade.
- `ArmGrouping.cs` — **pure**, and the only place that decides anything: fold runs onto their arm, order
  them, and produce the refusal sentence. Extracted for the reason `SampleWindow` was: the rules become
  testable with the xUnit already here, and no bUnit dependency is needed for markup that is a table.

### 4. Two pages

- `Pages/Benchmarking.razor` at `/benchmarking` — runs grouped by compute arm, newest first, each row
  linking to its report. The refusal banner sits above the groups, not in a footnote.
- `Pages/BenchmarkRun.razor` at `/benchmarking/runs/{id}` — one run's report: the dimensions with their
  arms and proof states, the machine, the load, the spread, the warnings. A metric must be named; there is
  no default (`RunReport.NoMetricNamed`), and the page says so rather than guessing one.

### 5. The qln wiring

Submodule at `external/dew_flow_benchmark`; `Daemon.Client.csproj` gains one `ProjectReference`;
`Routes.razor` gains one assembly; `NavMenu.razor` gains `Benchmarking`; `Program.cs` gains `AddBenchUi()`
and the bench store registrations; `BenchModule : IDaemonModule` declares the rest.

The bench database is **its own** — a second connection string, not the daemon's. The daemon gains a
reader for it and nothing more: nothing in `Bench.Api` starts a run, and that boundary is the point
(`src/Bench.Api/BenchApi.cs`, class comment).

## Test plan

| What | How |
|---|---|
| grouping and the refusal | `Bench.Tests` against `ArmGrouping`, pure — no host, no browser |
| an undeclared backend is its own group, never an empty label | same |
| `ComputeArm` reaches the summary contract | assert `RunReportContract.From(run)` for a declared and an undeclared engine |
| the route prefix moved | assert `MapBenchApi` mounts under `/api/bench` |
| markup, DI, submodule | **not** unit-tested; verified by running the console. Said out loud per the testing rule rather than left as a silent gap. |

## Definition of Done

- [ ] `/api/bench` is the prefix, and nothing referenced the old one.
- [ ] `RunSummaryDto.ComputeArm` is populated, with an undeclared backend arriving as its own state.
- [ ] `Bench.Ui` compiles against `Bench.Contracts` only, and references no infrastructure.
- [ ] `ArmGrouping` is pure and covered, including the undeclared-backend group.
- [ ] DewFlow's left menu shows **Benchmarking**, and both pages render live against a real run.
- [ ] The console shows the arms side by side and **refuses to rank them**, naming the reason on screen.
- [ ] The whole suite passes, run as the executable, never `dotnet test`.
