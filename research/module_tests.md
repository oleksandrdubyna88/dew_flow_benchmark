# Module — Tests: the scenario harness and its flow catalogue

> Status: **created 2026-10-12 with the scouts' scenario (E1 S3 of [PLAN_scout_model_calibration.md](PLAN_scout_model_calibration.md)); the
> private-task scenario and the `key` / `hygiene` rows added 2026-10-13 (E2 S4); the `access-check` row 2026-10-13 (E2 S5); the `tasks`,
> `access-check` and `hygiene` rows widened by the E2 code round, 2026-10-13.**
> The record the shared rule `common.scenario-tests` requires in every repository: where the harness is, how it runs, every flow with
> `covered` / `not covered` and a reason, what it does not prove, and when it runs. Adopted the way that rule's *Adoption* section asks — the
> catalogue first, most flows `not covered` on day one, closed flow by flow.
>
> **The minigit stack bench's Node scenarios (E2.S1, E2.S2) were added 2026-10-30** — see *The minigit stack bench scenarios* below; the C# catalogue
> test ignores that section.

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
| `model-arms init` | covered | `ModelArmsScenarioTests` | the binary as a process: `init` makes a root, `validate` reads it and refuses the unfilled template (4) without a trust refusal, a second `init` is refused (4); a second scenario runs it under `umask 0277` (modes still owner-only); `InitVerbTests` holds every refusal (existing root, link root, link parent, git checkout, this repository, missing parent, exit 3), the owner-only modes read back, and `init` then `validate` over a filled experiment (0); the template against the example in `ExampleExperimentTests` |
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

## The minigit stack bench scenarios (E2.S1 and E2.S2, `spikes/minigit-bench/`)

A second harness lives in this repository: the Node test suite of the minigit stack bench's agent sandbox, in `spikes/minigit-bench/test/` (and, for
the process fixes of E2.S1 that live in the sibling spike, `spikes/policy-ab-quick/test/`). It is written in the spike's own language (ES modules, Node
26) and drives the real instruments: a real `bwrap` box, a real egress proxy and credential broker on real unix sockets, a real session launch around
a stub `claude`, and the committed fixture of one real paid session. What a scenario substitutes is only what it cannot have: the paid API (a local
stub upstream replays a recorded SSE body), the real token (a FAKE `sk-ant-fake-…` value), and the CLI (a script that does what the real one does at
its edges). **The flow list is derived**: the Node test `module-tests-catalog.test.mjs` reads the verdict ids out of `SMOKE_ASSUMPTIONS`
(`lib/smoke-verdicts.mjs`) and every `*.test.mjs` file of the spike's `test/` directory, and fails when one is not named in this section, or when a
row names a test file that does not exist. The design is [module_minigit_bench.md](module_minigit_bench.md).

**How it is run** (from the repository root; ~30 minutes at the lowered priority the local CPU budget requires; **0 failed and 0 skipped** is the bar):

```bash
BENCH_REQUIRE_SANDBOX=1 node spikes/minigit-bench/run-tests.mjs                          # everything in spikes/minigit-bench/test
node spikes/minigit-bench/run-tests.mjs test/broker.test.mjs test/egress.test.mjs        # a few files (globs are relative to the spike)
node spikes/minigit-bench/run-tests.mjs '../policy-ab-quick/test/proc.test.mjs' '../policy-ab-quick/test/audit.test.mjs'   # the E2.S1 process fixes
```

`BENCH_REQUIRE_SANDBOX=1` turns a test that skips because the host lacks something (`bwrap`, user namespaces, the network, the token file) into a
failure, so "0 skipped" is a claim about this machine and not about a gap. **When it runs:** on this machine only (it needs WSL2 with `bwrap`; CI does not
run it), by whoever changes the spike, before every commit that touches it and in full before a review gate; the paid re-smoke (one session, cap $0.20)
runs by the operator's word only, once per code round, and the fixture it produces is then re-checked by `smoke-real.test.mjs` on every run.

| Flow | Status | Test files |
|---|---|---|
| E2.S1: one group kill, no spike launcher reaches a shell | covered | `spikes/policy-ab-quick/test/proc.test.mjs`, `test/no-shell.test.mjs` |
| E2.S1: the audit hook's record and the span builder that reads it | covered | `spikes/policy-ab-quick/test/audit.test.mjs`, `test/audit-contract.test.mjs` |
| E2.S1: the contamination scan, the token scan, the canary targets (POSIX paths) | covered | `test/contamination.test.mjs`, `test/bare-collect-allow.test.mjs` (the box's own paths are no reach, the pilot 9804fc's commands replayed), `test/contamination-above-home.test.mjs` (a walk from `/` or above the home is high, a plain listing is low), `test/contamination-box-home.test.mjs` (`~` and `$HOME` are the box home, an explicit operator path stays high), `test/box-skeleton.test.mjs` (the skeleton never covers an ancestor of the harness or net dir, nor the home or above: the review's probe commands stay high), `test/token-scan.test.mjs`, `test/token-path.test.mjs`, `test/canary-plan.test.mjs` |
| E2.S2: the agent box's argv, hide list and environment (no `--setenv`, no secret on argv, the allow-listed env) | covered | `test/bwrap.test.mjs`, `test/agent-box.test.mjs`, `test/agent-env.test.mjs`, `test/reuse-scan.test.mjs` |
| E2.S2: what the box sees (no home, no key, no `/mnt`, no interop, no `gh`, its own `/tmp`, no instruments) and what it holds (the dummy only) | covered | `test/agent-box-fs.test.mjs`, `test/agent-box-token.test.mjs`, `test/box-init.test.mjs` |
| E2.S2: the egress proxy: exact CONNECT hosts, the SNI gate and its timeouts, the effect through a real box | covered | `test/egress.test.mjs`, `test/egress-hosts.test.mjs`, `test/tls-hello.test.mjs`, `test/egress-box.test.mjs` |
| E2.S2: the credential broker: the route list, the body check, signing, streaming, pricing, the budget, the drain and the seal | covered | `test/broker.test.mjs`, `test/broker-routes.test.mjs`, `test/broker-body.test.mjs`, `test/budget.test.mjs`, `test/prices.test.mjs`, `test/session-cost.test.mjs` |
| E2.S2: the host's reads of what the agent wrote (links, pipes, swapped directories) and the real-token scan | covered | `test/agent-files.test.mjs`, `test/agent-files-sites.test.mjs`, `test/link-class.test.mjs`, `test/session-scan.test.mjs`, `test/secrets.test.mjs`, `test/transcripts.test.mjs` |
| E2.S2: the launch: settings, argv, read-back, watchdogs, the stop, the janitor, disposers, the state file | covered | `test/launch.test.mjs`, `test/readback.test.mjs`, `test/watchdogs.test.mjs`, `test/janitor.test.mjs`, `test/session-launch.test.mjs`, `test/session-parts.test.mjs`, `test/session-state.test.mjs`, `test/preflight.test.mjs`, `test/host-checks.test.mjs` |
| E2.S2: the smoke's machinery rehearsed free, and the verdicts over the committed real fixture | covered | `test/smoke-run.test.mjs`, `test/smoke-real.test.mjs`, `test/smoke-k2.test.mjs`, `test/capture-session.test.mjs`, `test/verdicts.test.mjs` |
| E3b: the bare runner's decisions: the outcome table (how a session ENDED), the barrier and the void / re-run schedule, the spend gate, the alias baseline, the dated amendment lines | covered | `test/bare-outcome.test.mjs`, `test/bare-schedule.test.mjs`, `test/spend-gate.test.mjs`, `test/alias-store.test.mjs`, `test/amendments.test.mjs`, `test/bare-limits.test.mjs` |
| E3b: the runner's state and plan: rids, masks and launch order, the registered prompts, the workspaces, the run-map and resume, `init` and its dry run | covered | `test/bare-plan.test.mjs`, `test/workspace.test.mjs`, `test/run-map.test.mjs`, `test/bare-init.test.mjs`, `test/seed.test.mjs`, `test/series-lock.test.mjs` |
| E3b: one phase and the loop: layouts, canaries, the masked same-tick start and skew, collect and classify, charge, barrier, score, records, resume over the real schedule | covered | `test/bare-phase.test.mjs`, `test/bare-collect.test.mjs`, `test/bare-series.test.mjs`, `test/bare-record.test.mjs`, `test/score-run.test.mjs`, `test/stream-calls.test.mjs` |
| E3b: the CLI and a bare pair end to end (real boxes, broker, proxy and scorer behind a stub `claude`), the re-exec onto CPUs 6,7; the shape of the code E3b owns (every function of the runner, the `bare-*` modules, the run-map, the lock, the guard, the samplers, the cache snapshot, the rehearsal and the `prereg/` scripts: under 50 lines and complexity at most 4, no exemption; measured by a small scanner that is itself tested; the E1 and E2 files, which E3b did not write, are named in a second list, and a source file in neither list fails, so a new module cannot escape the rule) | covered | `test/bare-runner.test.mjs`, `test/bare-e2e.test.mjs`, `test/code-shape.test.mjs` |
| E3b.S4: `aa-verdicts`: the G table G-1..G-14 and the GO / NO GO / STOP verdict computed from synthetic series (each G broken once refutes exactly it; the allowed `not exercised` rows and their replacing evidence; by-hand lines, malformed lines reported; G-6 explained; D12 aliases; the T3 rule; a twin difference recorded; stops named; the command's exit codes 0, 2, 4 and that it writes only its own json) | covered | `test/aa-verdicts.test.mjs` (fixture `test/aa-fixture.mjs`) |
| E3b: the free v2 rehearsal (G-14 computed from a workspace, a record and a score; the command's refusals; the v2 path end to end in a real box with a stub CLI, a fake token and the real scorer) | covered | `test/rehearse-v2.test.mjs`, `test/rehearse-v2-e2e.test.mjs` |
| E3b: test hygiene: no test reaches the operator's real secret files (the reader's guard under the test runner, the explicit opt-in and who may use it, the fake HOME every runner test runs under) | covered | `test/secrets.test.mjs`, `test/secrets-opt-in.test.mjs`, `test/fake-home.test.mjs` |
| E3b: the real wiring itself, through `main` and `buildRun` with only the model and the network stubbed (the pins read from disk, the accepted harness, and the findings that stubs of this layer had hidden) | covered | `test/bare-wiring.test.mjs` |
| E3b: the machine-quiet check, the CPU sampler and spin probe, the mask effect | covered | `test/cpu-sampler.test.mjs`, `test/quiet.test.mjs` |
| E3b: the cache snapshots, the package list and its flags, the seeded copy, the build servers left at their defaults and ending with the box | covered | `test/cache-snapshot.test.mjs`, `test/packages.test.mjs`, `test/build-servers.test.mjs` |
| E3b: the pre-registration: the guard and the harness identity, the C# prompt's SDK against the pin (guard and `seed-commit`), the generated files and the quotes; the registered texts against the code (every flag the code emits is a row of the contract's flags table and every row is emitted, derived by a scan; the ceiling names every bare spend and the prior spend; T5 is `investigate` and fixed; the N11 exemption, `--settle-only`, a waiting stop, pair order and `init` as section 9 says them; the statement's list of files that may change after the tag; the amendments header's line formats) | covered | `test/prereg-guard.test.mjs`, `test/prereg.test.mjs`, `test/seed.test.mjs`, `test/registered-text.test.mjs` |
| This catalogue itself: every verdict id and test file is named, every named file exists | covered | `test/module-tests-catalog.test.mjs` |
| E1 instruments the sandbox runs on (the scorer, the held-out suite, the counters, the capture); catalogued in `module_minigit_bench.md`, listed here by file only | covered | `test/adjudicate.test.mjs`, `test/aliases.test.mjs`, `test/capture-real.test.mjs`, `test/capture-sanitise.test.mjs`, `test/capture-tools.test.mjs`, `test/classify.test.mjs`, `test/claude-usage.test.mjs`, `test/coai-usage.test.mjs`, `test/crosscheck.test.mjs`, `test/heldout-archive.test.mjs`, `test/heldout-calibration.test.mjs`, `test/heldout-crypto.test.mjs`, `test/heldout-hostile.test.mjs`, `test/heldout-leak.test.mjs`, `test/heldout-pipeline.test.mjs`, `test/heldout-run.test.mjs`, `test/heldout-tool.test.mjs`, `test/host-lacks.test.mjs`, `test/inputs.test.mjs`, `test/no-fitted-rates.test.mjs`, `test/otel.test.mjs`, `test/repo-scan.test.mjs`, `test/score-pipeline-isolation.test.mjs`, `test/score-pipeline.test.mjs`, `test/score-session.test.mjs`, `test/timeline.test.mjs`, `test/visible.test.mjs` |
| The process arm: coai in its own box, the stdio bridge, the conventions snapshot (E2.S3, E2.S4) | not covered | not built yet; the plan is `todo/PLAN_minigit_stack_bench_E2.md` |
| A fresh paid session against the real API, run by the suite | not covered | it costs money and needs the operator's word; the suite re-checks the last real one's fixture, it does not make a new one |
| `ToolSearch` and any other tool of the process arm in a real request body (does the CLI send a server tool?) | not covered | the broker's body check refuses a server tool; the bare arm's own requests are checked by B-S2 (0 `body` refusals) in each paid smoke; the process arm has had none |

The verdict ids of the smoke (derived from `SMOKE_ASSUMPTIONS`; each is computed by `computeSmokeVerdicts` over the loaded fixture, never typed):

| Verdict | Status | Test files |
|---|---|---|
| `B-R1`, `B-R2` | covered | `test/smoke-real.test.mjs`, `test/readback.test.mjs` |
| `B-N1`, `B-N2` | covered | `test/smoke-real.test.mjs`, `test/egress.test.mjs` |
| `B-S2` | covered | `test/smoke-real.test.mjs`, `test/broker-body.test.mjs` |
| `B-S3` | covered | `test/smoke-real.test.mjs`, `test/session-scan.test.mjs`, `test/link-class.test.mjs` |
| `B-S3b` | not covered | retired by design: the models refuse the in-box probe; B-S3's positive control covers the CLI's environ from the host |
| `B-$1` | covered | `test/smoke-real.test.mjs`, `test/budget.test.mjs`, `test/crosscheck.test.mjs` |
| `B-O1`, `B-T1`, `B-E1`, `B-A1`, `B-A2`, `B-A3`, `B-H1`, `B-C1` | covered | `test/smoke-real.test.mjs` |
| `B-K1` | covered | `test/smoke-real.test.mjs`, `test/janitor.test.mjs` |
| `B-K2` | covered | `test/smoke-k2.test.mjs`; not exercised by the committed fixture (the session ended by itself) |

**What this harness does NOT prove.**

- **That the real API behaves as the stub upstream does.** The broker's tests replay one recorded SSE body and scripted error answers; the real
  API is only seen in a paid smoke, once per code round. Error shapes, a stream cut at a given event, billing of a reset request: assumed, not measured.
- **Anything about a CLI other than 2.1.310** (the read-back pins the version) or a model the price table does not hold (the broker charges the dearest rates and says so).
- **That `bwrap` isolation holds on another kernel.** The box tests run against this machine's kernel and `bwrap`; a different host is a new measurement.
- **The TLS edge.** The SNI gate is tested against recorded real hellos and a local upstream; what a CDN edge does with a second hello, a renegotiation or an
  HTTP/2 `:authority` is not provided by the proxy (residual risk, written in `module_minigit_bench.md`).
- **Effect tests that need the network** (`egress-box.test.mjs`, the npm and dotnet restores) run against real hosts: they say so and, without
  `BENCH_REQUIRE_SANDBOX=1`, skip when the host has none.
- **One real session.** The committed fixture is one paid run on one day; it proves the verdict rules read a real stream, not that every session behaves so.
- **Concurrency beyond a few requests**, and a run of days: the broker's budget and the janitor are exercised at the scale of seconds.

## What the harness does NOT prove

- **That a real claude prints these streams.** The cells replay synthetic streams in claude 2.1.289's shape; the four LIVE streams under
  `tests/Bench.Tests/Fixtures/scouts/` pin the reader's shapes, and E2 S4's pilot is the first real cell.
- **That a real codex judges claims the way the replay does.** The fake answers recorded verdicts keyed by what a row says; §3.7's hand-check
  (≥ 18 of 20 verdicts agreeing with a person opening the file) is what licenses the assessor, and it has not run.
- **That the recall matcher agrees with a person** — §3.7's 19-of-20 hand-check, E2 S5.
- **The read API and the console** for the scouts (E3 S8), and the routes above marked `not covered`.
- **Concurrency of two harnesses on one run**: every scenario runs one campaign at a time, as the design does.
- **The `model-arms` harness drives `init`, `validate` and `prep-base` only.** Its shims are POSIX shell scripts and its fixture's git is a link to the host's: the scenarios skip on Windows. It proves that nothing the target placed
  ran for `validate` and `prep-base` (`init` launches nothing); a launch added by a later story is seen by the structural test (`ModelArmsArchitectureTests.The_module_launches_nothing_by_a_bare_name`), not by a scenario. macOS and FreeBSD are refused by the trust check
  as unprobed and were never run; the product's own bare `git` launches in `src` are outside it. The fixture's `claude`, `dotnet`, `node` and `npm` are empty files.
- **Linux and macOS paths for the scouts**: the scenario runs where the suite runs; CI is ubuntu, local runs are Windows.

## Definition of Done for a new flow

A new CLI verb or route ships with its row here in the same task — `covered` naming its scenario test, or `not covered` with a reason.
`ModuleTestsCatalogTests` turns a forgotten row red.
