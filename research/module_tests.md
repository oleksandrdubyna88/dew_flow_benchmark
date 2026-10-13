# Module — Tests: the scenario harness and its flow catalogue

> Status: **created 2026-10-12 with the scouts' scenario (E1 S3 of [PLAN_scout_model_calibration.md](../todo/PLAN_scout_model_calibration.md)).**
> The record the shared rule `common.scenario-tests` requires in every repository: where the harness is, how it runs, every flow with
> `covered` / `not covered` and a reason, what it does not prove, and when it runs. Adopted the way that rule's *Adoption* section asks — the
> catalogue first, most flows `not covered` on day one, closed flow by flow.

## Where the harness is

In this repository, beside the unit tests: the scenario classes live in `tests/Bench.Tests/Cli/` (C#, the product's own language, holding the
product's own types and entry points). A scenario drives a verb exactly as `bench …` parses it — `ScoutsCommand.RunAsync(CommandLine.Parse([...]))`
or `Program.Run(args)` — against a **real Postgres** (Testcontainers `postgres:17`, `PostgresFixture`), **real git** repositories and real
history-free trees, and the real artefact root on disk. The only doubles are the collaborators a scenario cannot have: the paid CLIs, replaced
by `tests/FakeCli` (`fake-cli`), which replays recorded claude streams by the SHA-256 of their prompt and plays a codex assessor answering
recorded verdicts. Nothing in a scenario substitutes the subject.

## How it is run

The same command CI runs (`.github/workflows/ci.yml`), the test project's executable — never `dotnet test`:

```bash
dotnet build dew_flow_benchmark.slnx -c Release -m:4
./tests/Bench.Tests/bin/Release/net10.0/Bench.Tests.exe                                   # everything, scenarios included
./tests/Bench.Tests/bin/Release/net10.0/Bench.Tests.exe --filter-class "*ScoutsScenarioTests"   # the scouts scenario alone
```

Docker must be up (Testcontainers). The scenario needs no network and no paid model.

## When it runs

On every push and pull request, in CI, as part of the one test executable — no flow here needs a GPU, a paid model or a deployed target. A
flow that would (a real `claude` or `codex`) is exercised by its plan's pilot instead, named below.

**Run it alone on a workstation.** Measured 2026-10-12: two copies of the scout CLI classes started at once on one machine (CPU at 100 %, both
starting their Postgres containers, the suite at BelowNormal priority per the local CPU budget) failed three of 52 tests, every one in a
fixture's git call (`git init`, `git commit`, `git rev-parse`) reaching `DatedGitRepo`'s 30 s wall while the containers started; alone, the
same classes and the whole suite pass. No state is shared between those tests (each owns its repository, its trees, its fake CLI home and its
database), so the failure is the machine's contention, not the code's — and widening the wall would only hide the next such run.

## The flow catalogue

The list is **derived, not typed**: `ModuleTestsCatalogTests` reads the CLI's verbs out of `hosts/Cli/Program.cs`'s dispatch, the scouts'
sub-verbs out of `hosts/Cli/ScoutsCommand.cs`, and every route out of `src/Bench.Api/*.cs`, and fails when a flow is missing here, when a row
names a flow that no longer exists, or when a row marked `covered` names a test class the test assembly does not hold.

### CLI — `bench <verb>`

| flow | status | test | reason |
|---|---|---|---|
| `bench plan` | not covered | — | no scenario catalogued yet; `PlanCommand` is unit-tested in `tests/Bench.Tests/Cli` |
| `bench run` | not covered | — | `RunCommandTests` / `RunRetrievalTests` drive it against fakes; not yet reviewed against the scenario rule |
| `bench resume` | not covered | — | `ResumeCommandTests` exist; not yet reviewed against the scenario rule |
| `bench judge` | not covered | — | no scenario catalogued yet |
| `bench solve` | not covered | — | `SolveCommandTests` exist; not yet reviewed against the scenario rule |
| `bench report` | not covered | — | `ReportCommandTests` exist; not yet reviewed against the scenario rule |
| `bench sweep` | not covered | — | `SweepRecoveryTests` exist; not yet reviewed against the scenario rule |
| `bench prune` | not covered | — | no scenario catalogued yet |
| `bench rescore` | not covered | — | no scenario catalogued yet |
| `bench telemetry` | not covered | — | `TelemetryCommandTests` exist; not yet reviewed against the scenario rule |
| `bench sessions` | not covered | — | no scenario catalogued yet; the hook and the collector are separate hosts |
| `bench variants` | not covered | — | `VariantsCommandTests` exist; not yet reviewed against the scenario rule |
| `bench lanes` | not covered | — | no scenario catalogued yet |
| `bench questions` | not covered | — | `QuestionsCommandTests` exist; not yet reviewed against the scenario rule |
| `bench models` | not covered | — | `ModelsCommandTests` exist; not yet reviewed against the scenario rule |
| `bench gate` | not covered | — | the gate's driver tests (`GateDriverCommandTests`) drive a fake coai; not yet reviewed against the scenario rule |
| `bench probes` | not covered | — | `ProbesDriverCommandTests` drive the verbs against the fake CLI and a real store; not yet reviewed against the scenario rule |
| `bench scouts` | covered | `ScoutsScenarioTests` | the sub-verbs below |
| `bench version` | not covered | — | prints a constant; nothing to drive end to end |
| `bench help` | not covered | — | prints the usage text; `CliContractTests` reads it, no flow behind it |

### CLI — `bench scouts <sub-verb>`

| flow | status | test | reason |
|---|---|---|---|
| `bench scouts tasks` | covered | `ScoutsTasksCommandTests` | a real local repository, the cut-off guard, the pilot exclusion, a leaking statement refusing the selection |
| `bench scouts run` | covered | `ScoutsScenarioTests` | two tasks × A1/B1, the real store and trees, the fake claude per (task, arm); also `ScoutsCampaignTests` |
| `bench scouts resume` | covered | `ScoutsScenarioTests` | after a crash once the first cell's result reached disk: settled once from the stream, the cell handed back and re-run |
| `bench scouts rerun` | covered | `ScoutsCampaignTests` | the next generation of a void cell, measured alone |
| `bench scouts sweep` | covered | `ScoutsCrashTests` | the entry step alone, one crash per money boundary |
| `bench scouts prune` | covered | `ScoutsScenarioTests` | the run's trees deleted at the end of the scenario, nothing else |
| `bench scouts alias-check` | covered | `ScoutsCampaignTests` | a replayed stream whose `resolvedModel` moved exits 3 naming the new id |
| `bench scouts status` | covered | `ScoutsScenarioTests` | mid-run (a cell claimed by the crashed harness); also `ScoutsAnalysisCommandTests` (the ledger line) |
| `bench scouts score` | covered | `ScoutsScenarioTests` | recall recomputed from the streams; the hand-check draw in `ScoutsAnalysisCommandTests` |
| `bench scouts assess` | covered | `ScoutsScenarioTests` | the fake codex replaying verdicts in each task's tree; idempotency in `ScoutsAnalysisCommandTests` |
| `bench scouts report` | covered | `ScoutsScenarioTests` | `--json` read back and compared with the hand-computed table in the test |

### API — `bench-api` and `bench-collector` routes (`src/Bench.Api`)

| flow | status | test | reason |
|---|---|---|---|
| `GET /api/bench/health` | not covered | — | a constant `ok` that never touches the store — `POST_DEPLOY.md` deliberately does not probe it |
| `POST /api/bench/plan` | not covered | — | route tests in `tests/Bench.Tests/Api` exercise it in-process; not yet reviewed against the scenario rule |
| `GET /api/bench/runs` | not covered | — | route tests in `tests/Bench.Tests/Api`; not yet reviewed against the scenario rule |
| `GET /api/bench/runs/{id:guid}/scoreboard` | not covered | — | route tests in `tests/Bench.Tests/Api`; not yet reviewed against the scenario rule |
| `GET /api/bench/runs/{id:guid}/metrics` | not covered | — | route tests in `tests/Bench.Tests/Api`; not yet reviewed against the scenario rule |
| `GET /api/bench/runs/{id:guid}/report` | not covered | — | route tests in `tests/Bench.Tests/Api`; not yet reviewed against the scenario rule |
| `GET /api/bench/arms` | not covered | — | route tests in `tests/Bench.Tests/Api`; not yet reviewed against the scenario rule |
| `GET /api/bench/arms/metrics` | not covered | — | route tests in `tests/Bench.Tests/Api`; not yet reviewed against the scenario rule |
| `GET /api/bench/gate/scopes` | not covered | — | route tests in `tests/Bench.Tests/Api`; not yet reviewed against the scenario rule |
| `GET /api/bench/gate/runs/{id:guid}` | not covered | — | route tests in `tests/Bench.Tests/Api`; not yet reviewed against the scenario rule |
| `GET /api/bench/gate/{gate}/models` | not covered | — | route tests in `tests/Bench.Tests/Api`; not yet reviewed against the scenario rule |
| `GET /api/bench/gate/{gate}/runs` | not covered | — | route tests in `tests/Bench.Tests/Api`; not yet reviewed against the scenario rule |
| `GET /api/bench/probes/runs` | not covered | — | `ProbeApiTests` in-process; not yet reviewed against the scenario rule |
| `GET /api/bench/probes/runs/{id:guid}` | not covered | — | `ProbeApiTests` in-process; not yet reviewed against the scenario rule |
| `POST /api/bench/sessions/events` | not covered | — | the collector's write door; route tests in `tests/Bench.Tests/Api`, not yet reviewed against the scenario rule |
| `GET /api/bench/sessions/health` | not covered | — | the collector's liveness; `POST_DEPLOY.md` item 3 reads it against the running collector, after a deploy only |
| `GET /api/bench/sessions/` | not covered | — | route tests in `tests/Bench.Tests/Api`; not yet reviewed against the scenario rule |
| `GET /api/bench/sessions/{id:guid}` | not covered | — | route tests in `tests/Bench.Tests/Api`; not yet reviewed against the scenario rule |

The scouts have no route yet: the read API and the console's Scouts tab are E3 S8 (plan decision D8), which extends `ScoutsScenarioTests` so the
API's bytes equal the CLI report's.

## The scouts scenario (`ScoutsScenarioTests`)

`bench scouts run` over two tasks (t01, t02 of one local repository; |G| = 3) × the arms A1 and B1, the real store and real history-free trees,
the fake claude replaying a recorded stream per (task, arm). The harness crashes once the first cell's `result` line reached disk (a dead
owner, as a killed bench leaves); `status` shows the claimed cell; `resume` settles that attempt once from its stream and re-runs the cell; the
cell (t02, B1) goes VOID — its driver launched the scouts on the wrong model. Then `score`, `assess` (the fake codex calls a claim wrong when it
names `README.md` or `src/Beta.cs`), `report --json`, `report`, `prune`. The test carries the hand computation as a comment table and asserts
the report against it: the verdict label (`preliminary`), W1's intervals (one repository → not estimable), each arm's recall, wrong claims per
10, team time, team cost, cost per right file, caller load, the void count, the claim counts, and the ledger identity over five attempts.

## What the harness does NOT prove

- **That a real claude prints these streams.** The cells replay synthetic streams in claude 2.1.289's shape; the four LIVE streams under
  `tests/Bench.Tests/Fixtures/scouts/` pin the reader's shapes, and E2 S4's pilot is the first real cell.
- **That a real codex judges claims the way the replay does.** The fake answers recorded verdicts keyed by what a row says; §3.7's hand-check
  (≥ 18 of 20 verdicts agreeing with a person opening the file) is what licenses the assessor, and it has not run.
- **That the recall matcher agrees with a person** — §3.7's 19-of-20 hand-check, E2 S5.
- **The read API and the console** for the scouts (E3 S8), and the routes above marked `not covered`.
- **Concurrency of two harnesses on one run**: every scenario runs one campaign at a time, as the design does.
- **Linux and macOS paths for the scouts**: the scenario runs where the suite runs; CI is ubuntu, local runs are Windows.

## Definition of Done for a new flow

A new CLI verb or route ships with its row here in the same task — `covered` naming its scenario test, or `not covered` with a reason.
`ModuleTestsCatalogTests` turns a forgotten row red.
