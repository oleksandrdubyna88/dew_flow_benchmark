# Module — Tests: the scenario harness and its flow catalogue

> Status: **created 2026-10-12 with the scouts' scenario (E1 S3 of [PLAN_scout_model_calibration.md](PLAN_scout_model_calibration.md)); the
> private-task scenario and the `key` / `hygiene` rows added 2026-10-13 (E2 S4); the `access-check` row 2026-10-13 (E2 S5); the `tasks`,
> `access-check` and `hygiene` rows widened by the E2 code round, 2026-10-13.**
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
| `bench scouts tasks` | covered | `ScoutsTasksCommandTests` | the LAZY read (default, 2026-10-14) stopping a repository at its fifth admissible candidate and drawing exactly what `--read-all` draws, on a real repository (and in `ScoutLazySelectionTests` on a synthetic 24-repository pool over three seeds); a real local repository, the cut-off guard, the D10 draw seeded by the campaign string (five candidates → one main and its reserve in the known order, four → left out, `pilot-tasks.json` beside `--out`), a merge style the mirror contradicts excluded by id, a leaking statement refusing the selection; a PRIVATE candidate whose statement leaks refused by label and code with neither the word, the statement nor the token count on the console and all three in the withheld file; `--out`, `--private-tasks-file` and the `pilot-` files beside them naming one file refused (4) before any mirror is read (E2 code round); P per merge style on real merge/squash/rebase histories in `ScoutParentCheckTests`; a P whose own `.claude` settings grant outside the repository admitted, the settings left out of G, in `ScoutProjectSettingsAdmissionTests`; a PRIVATE repository of five candidates (label, language, commitments, the private file, the reserve) in `ScoutsPrivateTaskTests` |
| `bench scouts run` | covered | `ScoutsScenarioTests` | two tasks × A1/B1, the real store and trees, the fake claude per (task, arm), a PRELIMINARY run under `schedule.json`; also `ScoutsCampaignTests` (pilot runs); a cell whose main session or child fell back to another model after a safeguard refusal STOPS as `ModelRefusalFallback` (E2 S8, the live refused stream as fixture) in `ScoutModelFallbackTests`, read off the stream, not through the verb; a private task re-derived and verified under the campaign key, a mismatch refused by label, in `ScoutsPrivateTaskTests`; the money (S6) in `ScoutsMoneyTests` — a pilot's bounds refused by name, a preliminary run refusing a budget flag and a missing or foreign schedule, an overshoot settled at its real total and the next reservation refused |
| `bench scouts resume` | covered | `ScoutsScenarioTests` | after a crash once the first cell's result reached disk: settled once from the stream, the cell handed back and re-run |
| `bench scouts rerun` | covered | `ScoutsCampaignTests` | the next generation of a void cell, measured alone |
| `bench scouts sweep` | covered | `ScoutsCrashTests` | the entry step alone, one crash per money boundary |
| `bench scouts prune` | covered | `ScoutsScenarioTests` | the run's trees deleted at the end of the scenario, nothing else |
| `bench scouts alias-check` | covered | `ScoutsCampaignTests` | a replayed stream whose `resolvedModel` moved exits 3 naming the new id; since S5 launched with the pinned files (`--setting-sources "" --settings --plugin-dir`) under the artefact root |
| `bench scouts access-check` | covered | `ScoutsAccessCheckCommandTests` | real planting on disk (a one-commit tree, a sibling, a canary at the mirror root, a junction) and the fake claude replaying one stream for both agents: content returned from the sibling exits 3 naming target and agent; no attempt is inconclusive (3); denied everywhere passes (0); one agent denied everywhere and the other never attempting (one replayed stream per agent, chosen by the agent type its driver names) is inconclusive (3) — a target passes only when EVERY agent denied it (E2 code round); `launching <agent> …` printed before each launch's result (the fake holding 300 ms); the launch argv, the pinned settings file (two keys, written once), a drifted user settings file refused (4), nothing printed is a path, what was planted outside the work folder gone after. The judge alone, the conclusion over explicit agents, and the two 2026-10-13 grant-drop fixtures reading as denied, in `ScoutAccessCheckTests`; the 30 s cadence in `ElapsedTickerTests` |
| `bench scouts status` | covered | `ScoutsScenarioTests` | mid-run (a cell claimed by the crashed harness); also `ScoutsAnalysisCommandTests` (the ledger line) |
| `bench scouts score` | covered | `ScoutsScenarioTests` | recall recomputed from the streams; the hand-check draw in `ScoutsAnalysisCommandTests`; a private task's items by label and commitment with the local companion in `ScoutsPrivateTaskTests` |
| `bench scouts assess` | covered | `ScoutsScenarioTests` | the fake codex replaying verdicts in each task's tree; idempotency in `ScoutsAnalysisCommandTests`; a private claim stored as its HMAC in `ScoutsPrivateTaskTests` |
| `bench scouts report` | covered | `ScoutsScenarioTests` | `--json` read back and compared with the hand-computed table in the test; a private task as its label and language, nothing planted in the JSON or the text, in `ScoutsPrivateTaskTests`; the overshoot count in the ledger in `ScoutsMoneyTests` |
| `bench scouts schedule` | covered | `ScoutsMoneyTests` | the next run's caps and walls written from a pilot run's settled cells (the largest cost × 1.5, the slowest cell × 2), then taken by a preliminary run; refused (4) for a run that is not a pilot |
| `bench scouts sizing` | covered | `ScoutsSizingCommandTests` | E2 S8: over a run the real `run` settled with the fake claude, the simulation printed and written as Markdown, the same bytes for the same seed, every task count 8..30, the wrong-claim half marked *not simulated* while no claim is assessed, no task id, repository or statement in what is printed or written; a missing `--out`, an unknown run and bad settings refused (4) with nothing written. The simulation alone, as a table on synthetic pilots (a tight one finds a count, a noisy one none, determinism by seed), in `ScoutSizingTests` |
| `bench scouts key` | covered | `ScoutsPrivateTaskTests` | `key init` refused inside a checkout and a second time at the same path; a later verb refusing a missing key (4) and creating nothing; the placement rules alone in `ScoutCampaignKeyTests` |
| `bench scouts hygiene` | covered | `ScoutsHygieneCommandTests` | a staged blob and a staged file name carrying a deny-list entry, a home path or an e-mail refused by rule (1); a staged BINARY blob carrying an entry in its bytes — as UTF-8 in another case, as UTF-16LE — refused by index and encoding (1), a clean binary passing (E2 code round); a staged rename scanned under its new name; every blob announced `scanning [i/n]` off the one `cat-file --batch`; a commit range read at its end; no deny-list named (4), one that does not load (4), not a repository (3). The byte scan alone in `BinaryScanTests`; the batch reader, its listings and the no-deadlock property in `GitBlobBatchTests` |

### CLI — `model-arms <verb>`

The module under `tools/model-arms` is its own console (`model-arms`), driven here as a process by `ModelArmsScenarioTests` and in-process through the real parser by `ModelArmsProgramTests`. The list is derived from
`ModelArmsCommandLine.Verbs`, and `ModelArmsScenarioTests.The_catalogue_names_every_verb_the_command_line_accepts` fails when a verb has no row. (`ModuleTestsCatalogTests` reads the `bench` rows only; E5.S3 widens it.)

| flow | status | test | reason |
|---|---|---|---|
| `model-arms validate` | covered | `ModelArmsScenarioTests` | the binary started inside the target and with a target PATH entry runs nothing the target placed; `ValidateVerbTests` holds every finding |
| `model-arms prep-base` | covered | `ModelArmsScenarioTests` | the guard only (the same checks, then refused until E4.S4); the baseline measurement is not built |
| `model-arms version` | covered | `ModelArmsProgramTests` | in-process, through the real parser |
| `model-arms help` | covered | `ModelArmsProgramTests` | in-process, through the real parser |
| `model-arms init` | not covered | — | not built yet, story E1.S11 |
| `model-arms make` | not covered | — | not built yet, story E4.S4 |
| `model-arms lock` | not covered | — | not built yet, story E4.S1 |
| `model-arms unlock` | not covered | — | not built yet, story E4.S1 |
| `model-arms run-arm` | not covered | — | not built yet, story E2.S7 |
| `model-arms run-all` | not covered | — | not built yet, story E4.S3 |
| `model-arms chain` | not covered | — | not built yet, story E4.S5 |
| `model-arms checks` | not covered | — | not built yet, story E5.S2 |
| `model-arms status` | not covered | — | not built yet, story E4.S4 |
| `model-arms gate-lock` | not covered | — | not built yet, story E2.S6 |

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

## The private-task scenario (`ScoutsPrivateTaskTests`, E2 S4)

ONE synthetic private task (`p03`, language PHP, in a local `file://` repository) whose statement, three G paths, an identifier the diff added, a
claim and the repository's url carry planted strings that are on NO deny-list — the scrubber would not catch them, only the projection does.
Since S7 the repository lists five private candidates on that change (a repository needs five to be drawn), their PR numbers assigned by hash
order under the seed so `p03` is the main task and `p01` its reserve whatever the clone's folder is called.
It is driven through `tasks` (with the campaign key, the campaign string and the private tasks file), `run` (the fake claude, its KEY FILES and
CLAIMS carrying the plants), `status`, `score --handcheck` (the public file and the local companion), `assess` (the fake codex), `report`,
`report --json`, the `cells.jsonl` and RESULTS writers, the public export walk and the log sink; then through every error path — a malformed
private candidate, a missing mirror, an I/O failure injected into the tree (a locked file) and into the artefact root, and the unhandled
exception those raise through `Program.Run`. No planted string reaches stdout, stderr, a public file, an export row or the process's log file;
the local-only files (the private tasks file, the companion, the withheld detail, the fault file) do carry them, which is how the test knows its
plants are alive. A third test takes the key through its lifecycle: `key init` refused inside a checkout and a second time; a verb without the
key refused (4) with nothing created; an edited statement, a wrong key and the public file alone each refused by label.

## What the harness does NOT prove

- **That a real claude prints these streams.** The cells replay synthetic streams in claude 2.1.289's shape; the four LIVE streams under
  `tests/Bench.Tests/Fixtures/scouts/` pin the reader's shapes, and E2 S4's pilot is the first real cell.
- **That a real codex judges claims the way the replay does.** The fake answers recorded verdicts keyed by what a row says; §3.7's hand-check
  (≥ 18 of 20 verdicts agreeing with a person opening the file) is what licenses the assessor, and it has not run.
- **That the recall matcher agrees with a person** — §3.7's 19-of-20 hand-check, E2 S5.
- **The read API and the console** for the scouts (E3 S8), and the routes above marked `not covered`.
- **Concurrency of two harnesses on one run**: every scenario runs one campaign at a time, as the design does.
- **The `model-arms` harness drives `validate` and `prep-base` only.** Its shims are POSIX shell scripts and its fixture's git is a link to the host's: the scenarios skip on Windows. It proves that nothing the target placed
  ran for those two verbs; a launch added by a later story is seen by the structural test (`ModelArmsArchitectureTests.The_module_launches_nothing_by_a_bare_name`), not by a scenario. macOS and FreeBSD are refused by the trust check
  as unprobed and were never run; the product's own bare `git` launches in `src` are outside it. The fixture's `claude`, `dotnet`, `node` and `npm` are empty files.
- **Linux and macOS paths for the scouts**: the scenario runs where the suite runs; CI is ubuntu, local runs are Windows.

## Definition of Done for a new flow

A new CLI verb or route ships with its row here in the same task — `covered` naming its scenario test, or `not covered` with a reason.
`ModuleTestsCatalogTests` turns a forgotten row red.
