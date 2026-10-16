# PLAN — Test leftovers, E6: the Testcontainers fixture, the scratch folders, the worktrees and the fakes of `tests/Bench.Tests`

> Status: **plan only, nothing implemented yet, 2026-10-16; blocked on the E1 release of `dew_flow_conventions`.**
> Stories of epic E6 of `dew_flow_conventions · todo/PLAN_test_leftovers_rollout.md` (§ *Epics*, E6). Scope: the
> `.claude/rules/shared` pin; a new `tests/Bench.Tests/Leftovers/` folder (helper, owner liveness, start-of-run sweep of
> folders and containers, end-of-run audit, guard); `tests/Bench.Tests/Infrastructure/PostgresFixture.cs`; the shared
> temp helpers (`TempRoot`, `DatedGitRepo`, `TempGitRepo`) and the two fakes (`tests/FakeCli`, `tests/FakeCoai`); one
> optional parameter on two product classes (`HarvestGates`, `SolutionSignals`); the 47 test files that make temp paths
> today; one CI step; `http/README.md`; `research/module_tests.md`.
>
> Split by Opus, 2026-10-16. Not a cornerstone (parent, E6 entry). Models per story below.

**Committed together with this document** (one commit, the shared `common/planning-docs.md` § *A plan split into
epics and stories*): its row in `todo/README.md` under *Currently open* —

```
| [PLAN_test_leftovers_E6.md](PLAN_test_leftovers_E6.md) | **plan only, 2026-10-16 — blocked on the E1 release of the shared rules** — stories of epic E6 of `dew_flow_conventions · todo/PLAN_test_leftovers_rollout.md`: every folder, worktree and container `tests/Bench.Tests` makes is named `testsToDelete-bench-…`, claimed, removed by its test, collected at the start of the next run when a run was killed, and kept so by a guard; the Testcontainers Postgres is named and labelled with Ryuk on, and its per-test databases carry the container-scoped exemption. 5 stories (S1–S5), no cornerstone |
```

**Parent, contract and shape are cited as paths, never links** (the shared `common/planning-docs.md` § *Across
repositories*): the parent `dew_flow_conventions · todo/PLAN_test_leftovers_rollout.md`; the contract
`dew_flow_conventions · common/test-leftovers.md` (mounted here at `.claude/rules/shared/common/test-leftovers.md` once
S1 moves the pin); the Node reference `dew_flow_conventions · tools/lib/owned.mjs`, `tools/lib/leftovers-sweep.mjs`,
`tools/leftovers-check.mjs`; E1's stories `dew_flow_conventions · todo/PLAN_test_leftovers_E1.md`; and **the C# shape
this epic copies: `dew_flow_mcp · todo/PLAN_test_leftovers_E3.md`, S2–S4** — E3 writes the family's first C# helper
FIRST; this document copies it by shape, not by file, and names below every place where this repository needs more.
*Rule §n* is a section of the contract.

## Goal

A run of `Bench.Tests` leaves nothing behind it did not mean to keep. Every folder, file, worktree and container a
test creates outside its process is named `testsToDelete-bench-<what>-<id>` by one helper, claimed by its owner
(`<host>@<os>:<pid>:<start>`), removed by the test that made it on every path out, and — when a run was killed —
collected at the start of the next run, only when its owner is provably gone. The Testcontainers Postgres carries the
name and the owner label, its reaper stays on, and its ~30 per-test databases are exempt because they live and die
with it (rule §6 (b)). The AppHost's persistent Postgres and the product's own scratch names are untouched.

**Done when:** the pin carries the E1 contract (S1); the helper, the liveness answer and the start-of-run sweep exist
with their proofs (S2); the fixture's container is named, labelled, reaped after a kill, and the exemption is written
beside it (S3); the shared helpers, the fakes and the worktree seam put every resource they make inside an owned
folder (S4); no test file makes a temp path outside the helper, a guard keeps it so, and a run that leaves something it
claimed is a red run (S5).

## Facts the split rests on (read on `origin/main` 20051c8, 2026-10-16)

**The container.**

- `tests/Bench.Tests/Infrastructure/PostgresFixture.cs:27-33` — `new PostgreSqlBuilder("postgres:17-alpine")`, no
  `.WithName`, no label, no reuse; `:56` disposes it; `:120-121` makes it the ONE collection fixture
  (`[CollectionDefinition("postgres")]`), so one container per test process. Nothing in the repository sets
  `TESTCONTAINERS_RYUK_DISABLED`; Ryuk is on. `Testcontainers.PostgreSql` 4.15.0 and `xunit.v3` 4.0.1
  (`Directory.Packages.props:39`, `:34`).
- `:84-92` `NewDatabaseAsync` and `:96-114` `NewEmptyDatabaseAsync` create a database per call (`CREATE DATABASE` at
  `:108`, name checked against `^[a-z0-9_]{1,60}$` at `:101`) and never drop it: **28 call sites in 16 test files**.
  They die with the container — rule §6 (b) exactly.
- `research/module_tests.md:14` says the fixture runs `postgres:17`; the fixture runs `postgres:17-alpine`.
- **Containers that are not this epic's:** the AppHost's Postgres is a documented persistent reuse
  (`hosts/AppHost/AppHost.cs:55-61`, `WithDataVolume("bench-postgres-data")`, `ContainerLifetime.Persistent`, reasons
  at `:50-60`); `scripts/bank-thirds.sh:71` and `scripts/telemetry-ingest.ps1:39` only `docker start` that existing
  container; the CI `contract` job's `services: postgres` (`.github/workflows/ci.yml:52-61`) is a runner-owned service
  container. None of them is created by a test.
- **A container the audit missed:** `http/README.md:32-33`, the contract suite's local recipe, `docker run -d --name
  bench-suite-pg … postgres:17` — no `--rm`, unprefixed, never stopped by the recipe; whoever follows it keeps a
  Postgres on port 55432 until they notice.

**The temp sites.** A search of `tests/`, `scripts/`, `.github/` and `http/` for every spelling rule §7's `raw-temp`
names (`GetTempPath`, `CreateTempSubdirectory`, `GetTempFileName`, `TEMP`/`TMP`/`TMPDIR`, `mkdtemp`, `tmpdir()`,
`mktemp`) finds **73 lines in 48 files, all under `tests/`**: 71 code lines in 47 files plus two comments
(`Authoring/VettingPassTests.cs:380`, `Cli/ScoutsOutOfProcessTests.cs:107`). There is no `tools/` folder on this
commit, and `scripts/` makes no temp path. (The parent's "73 mentions in 48 files under `tests/`, `tools/`, `scripts/`"
is this count; its "~45 sites" counted creation sites only.) They fall into five shapes:

| Shape | What it does | Sites |
|---|---|---|
| **Shared helpers** (S4) | folders other tests build on | `Infrastructure/GateStoreFixtures.cs:65` (`TempRoot`, 58 files use it through `GateStoreFixtures.NewRoot()`), `Infrastructure/DatedGitRepo.cs:22`, `Infrastructure/GitCheckoutTests.cs:20`, `:167` (`TempGitRepo`), `Infrastructure/CliSubjectRuntimeTests.cs:65` |
| **Fakes** (S4) | homes a CHILD process finds by convention | `Probes/Driver/FakeCli.cs:15` + `tests/FakeCli/Program.cs:554`, `Gate/Driver/FakeCoai.cs:16` + `tests/FakeCoai/Program.cs:417`, `Scouts/ScoutLaunchProcessTests.cs:129` (+ `:100`, `:107`) |
| **A — a FILE straight in the temp root**, deleted in a `Temp*` helper's `Dispose` (S5) | `bench-<x>-<guid>.json` / `.md` | `Cli/CliContractTests.cs:107`, `Cli/FixRunPlanningTests.cs:96`, `Cli/HarvestLandingTests.cs:193`, `Cli/ModelsCommandTests.cs:237`, `Cli/QuestionsCommandTests.cs:156`, `Cli/RunCheckoutTests.cs:116`, `Cli/RunCommandTests.cs:115`, `Cli/RunRetrievalTests.cs:246`, `Cli/SweepRecoveryTests.cs:115`, `Cli/TempBank.cs:16` |
| **B — a folder, removed today** (S5) | the name only | `ArchitectureTests.cs:347` (a FIXED name, `bench-arch-planted-vendors-literal` — two runs collide), `Authoring/PromptCatalogTests.cs:132`, `:138`, `Cli/CliLoggingTests.cs:127`, `Cli/RunCheckoutTests.cs:25`, `Cli/TelemetryCommandTests.cs:292`, `Gate/Assessment/GateRubricsTests.cs:75`, `Gate/Driver/SettingsCheckTests.cs:12`, `Infrastructure/FileSystemLinkTargetsTests.cs:14`, `Infrastructure/GitCheckoutLocalSourceTests.cs:23`, `Infrastructure/ScoutCampaignKeyTests.cs:14`, `Models/WorkspaceTrustTests.cs:21`, `Scouts/ScoutScrubberTests.cs:56` |
| **C — a folder NEVER removed** (S5) — leaks on every run | | `Authoring/VettingPassTests.cs:326`, `:339`; `Diagnostics/DailyRunFileSinkTests.cs:92`; `Engines/FilesystemEngineTests.cs:304`; `Engines/QlnEngineTests.cs:287`; `Trace/TracePortTests.cs:166`, `:183`, `:268` — **5 files, 8 sites** |
| **D — the temp root as a PATH, nothing created** (S5) | a cwd, a parent, an absent path | `Authoring/AuthoringPassTests.cs:280`, `:292` (a fake agent's `Worktree`); `Cli/GateAaCommandTests.cs:214`; `Cli/GateAssessCommandTests.cs:48`; `Cli/GateDriverCommandTests.cs:32`, `:33` (refused before anything is written, `:35-36`); `Engines/FilesystemEngineTests.cs:184`; `Engines/QlnEngineLiveTests.cs:174`; `Engines/QlnEngineTests.cs:301`; `Gate/Driver/ProductPinReaderTests.cs:72`; `Infrastructure/FileSystemGateArtifactStoreTests.cs:237`, `:238`, `:246` (string containment); `Infrastructure/GitCheckoutLocalSourceTests.cs:93`; `Infrastructure/ProcessRunnerCancellationTests.cs:25`; `Infrastructure/ProcessRunnerTests.cs:15`, `:26`, `:39`, `:56`, `:75`, `:92`, `:107`, `:138` (cwd); `Models/AssessorArgvTests.cs:78`; `Models/CliAgentLiveTests.cs:83`; `Suites/AnchorCheckTests.cs:103`, `:110`; `Trace/TracePortTests.cs:257` |

S4 removes 12 of the 71 findings (9 files); S5 the other **59 in 38 files**.

**Leaks the audit table did not name** — each reproducible on `main`:

- **Siblings.** `TempRoot.Sibling(name)` (`GateStoreFixtures.cs:71`) is a folder BESIDE the root,
  `bench-gate-tests/<guid>-<name>`, and `TempRoot.Dispose` (`:73-88`) removes only the root and `-outside`. Every
  other sibling depends on its rig's own hand-kept list — `ScoutsCliSetup.cs:217`, `GateDriverRig.cs:153`
  (`DeleteSiblings`, called from `GateDriverRig.cs:145`, `GateCloneRig.cs:44`, `ProbeDriverRig.cs:121`),
  `ScoutsTasksCommandTests.cs:309`, `ScoutTreesTests.cs:290`, `ProbesCliSetup.cs:125-126`, `ProbeDriverRig.cs:122` —
  and five siblings are on no list: `process-temp` (`ScoutsOutOfProcessTests.cs:31`), `alias`
  (`ScoutsCampaignTests.cs:163`), and `private-files`, `keys` (**a campaign key file**), `faults`
  (`ScoutsPrivateTaskTests.cs:64`, `:74`, `:77`; its rig disposes only the repository and the setup, `:45-49`).
- **Fixed parents never removed:** `bench-gate-tests/`, `bench-dated-git/`, `bench-gate-fake/`, `bench-fake-cli/`,
  `bench-scout-fake/`, `bench-gate-settings/` — every run's children are removed, the parents stay forever.
- **`fake-coai-data/`**, a fixed folder in the temp root: the fake coai writes `usage.jsonl` (append) and `sessions/`
  there whenever `COAI_DATA_DIR` is unset (`tests/FakeCoai/Program.cs:359-361`, `:378-379`, `:417`), and the test
  side strips every `COAI_*` variable from the fake's environment (`Gate/Driver/FakeCoai.cs:53`). Never removed.
- **Product-named temp paths reached by tests**, outside the guard (product code is not scanned):
  `src/Bench.Infrastructure/Gate/FileSystemGateAssessmentFiles.cs:33` (`bench-assess-<guid>`, removed by its
  `Dispose`, `:40-42` — but `Gate/Assessment/FindingAssessorTests.cs:44`, `Gate/Import/CalibImportTests.cs:237` and
  `Gate/Import/ImportRig.cs:68` never dispose it), `src/Bench.Infrastructure/Git/SolutionSignals.cs:179` (a patch
  file, removed in its `finally`, `:196-200`), `src/Bench.Infrastructure/Scouts/ScoutTrees.cs:178` (a zip, `finally` at
  `:195`).

**Worktrees and clones** — made by product code, which the parent asked not to change in a benchmark run:

- `src/Bench.Infrastructure/Git/GitCheckoutProvider.cs:53-57` puts BOTH the bare mirror and every worktree under
  `CheckoutCacheOptions.Root` (`:12-15`), and every test passes that root (`GitCheckoutTests.cs:137-139`,
  `GateCloneRig.cs:20`, `GateDriverRig.cs:38`, `ScoutsCliSetup.cs:123`, `ScoutTreesTests.cs:260`, …). Once the root is
  inside an owned folder, the clone and its worktrees die with it (rule §1, *what dies with its container needs no
  name of its own*). **No seam is needed here.**
- `src/Bench.Infrastructure/Git/ScratchWorktree.cs:13-16` — the default overload puts a worktree at
  `<temp>/bench-scratch/<guid v7>` (`:24`, `worktree add --detach --force` at `:28`), registered in the CALLER's
  repository, removed in the callers' `finally` (`HarvestGates.cs:81-84`, `SolutionSignals.cs:86-89`); a kill leaves the
  directory in a shared namespace under a GUID leaf. The overload with `underRoot` (`:21-22`) already exists and
  `CliSubjectRuntime` uses it (`src/Bench.Infrastructure/Models/CliSubjectRuntime.cs:19-26`, `:88-89`). The default
  is reached by `HarvestGates.RunAsync` (`HarvestGates.cs:50-58`, `:67`) and `SolutionSignals.RunAsync`
  (`SolutionSignals.cs:53-63`, `:72`), from `HarvestGatesTests.cs` (4 tests) and `SolutionSignalsTests.cs` (4 tests).
  The product's own callers are `hosts/Cli/HarvestCommand.cs:242` and `hosts/Cli/SolveCommand.cs:184`.
- A gate's FAILED build prints into the report: `HarvestGates.cs:133-141` puts `GateProcess.Tail(output)` (300
  characters) into `RedDetail` — which makes the worktree's location observable from a test (S4).

**The fakes' cross-process contract.** `tests/FakeCli/Program.cs:552-560`: the fake CLI finds its home at
`<temp>/bench-fake-cli/<model>` — or `FAKE_CLI_HOME` when the launcher let it through — because the probe runner launches
it under a minimal environment where only `TEMP`/`TMP`/`TMPDIR` pass (`Probes/Driver/FakeCli.cs:5-9`). The test side
mints `Model = fake-<guid>` (`FakeCli.cs:14`). `ScoutsOutOfProcessTests.cs:106-111` redirects a child bench's
`TEMP`/`TMP`/`TMPDIR` into a sibling folder — the precedent for steering a child's temp root.

**Owner liveness here.** The product's `src/Bench.Infrastructure/Process/WorkerLiveness.cs` (`:16` `ThisHost =>
Environment.MachineName`; `:25-45` `ProcessIsAlive`, two-valued, refusal ⇒ alive) is what
`Persistence/PostgresRunStore.cs:126` `SweepAsync` authorises with (`:256`). It is product code and is NOT changed and
NOT reused: its host is the NetBIOS name and its answer has two values; the contract wants the DNS host name and three.
`PostgresFixture.cs:129-137` (`TestWorkers.Dead`, `MachineName` + `int.MaxValue`) feeds that product sweep and stays.

**How tests run.** xUnit v3 on Microsoft Testing Platform, the executable, never `dotnet test` (`CLAUDE.md:30-36`);
locally through `node .claude/rules/shared/tools/low-priority.mjs`, ≤ 8 threads (`tests/Bench.Tests/xunit.runner.json:3`,
copied only off CI, `Bench.Tests.csproj:45`); `TreatWarningsAsErrors` (`Directory.Build.props:8`). Docker must be up
(`research/module_tests.md:29`), and the suite is run ALONE on a workstation (`module_tests.md:36-40`). The repository
documents tests in `research/module_tests.md` (a catalogue `ModuleTestsCatalogTests` parses — new prose sections are
safe, new tables are not).

**CI and the pin.** `build-test` (`.github/workflows/ci.yml:13-43`) runs the executable on `ubuntu-latest` with Docker
and does NOT fetch `.claude/rules/shared`; `contract` (`:68`) and `plans` (`:119-120`) do. `plans` runs `pin-check.mjs`
(`:137-138`), which compares the gitlink with the tip of the branch `.gitmodules` tracks (`release`). The pin is
`e29883f`, the pre-E1 text: no owner, no sweep, no `## Tokens` table. **The day E1 is released every pull request here
is red on `Pin check` until S1 lands.**

**Reusable neighbours** (the shared `common/reuse-first.md`): `tests/Bench.Tests/Infrastructure/TestRepository.cs:5-19`
finds the checkout root (three more copies of that loop: `LocalCpuBudgetTests.cs:20-27`,
`Cli/AppHostLaunchProfileTests.cs:~100-110`, `Scouts/ScoutTreesTests.cs:272`); `GateStoreFixtures.cs:90-103`
(`RemoveLinks` — every link at EVERY depth removed as itself before a recursive delete) and `GitCheckoutTests.cs:183-197`
(`TempGitRepo.DeleteTree` — git's read-only object files cleared first) are this repository's two measured removal
lessons; `src/Bench.Infrastructure/Process/ProcessRunner.cs:54` (`RunAsync(executable, arguments, workingDirectory,
timeout, ct)` → `Completed` / `NotFound` / `TimedOut`) is the one sanctioned launcher.

**Outside this epic, on purpose:** `spikes/policy-ab-quick/test/*.test.mjs` — 16 `os.tmpdir()`/`mkdtemp` sites in 7
Node test files, most removed through `t.after`/`finally`, run by hand, never in CI. Not in the parent's scope; S5's
guard excludes `spikes/` by name and the parent is told (note P8).

## Decisions

**D1 — The helper is E3's, by shape.** `tests/Bench.Tests/Leftovers/`, namespace `Bench.Tests.Leftovers`: `Owned`,
`Owner`, `OwnerState`, `Probe`, `Liveness`, `OwnedDir`, `OwnedScope`, `Removal`, `UnmatchablePid` — the names, the
order (claim FIRST with `FileMode.CreateNew` on the sibling `<folder>.owner`, then create — .NET has no exclusive
`mkdir`, and `CreateTempSubdirectory`'s Windows id carries a `.`, measured by E3), the three-valued liveness, the
never-throw teardown, `Dns.GetHostName()` (never `Environment.MachineName`), the three-field owner from
`Process.GetCurrentProcess().StartTime`, `TestContext.Current.AddWarning` for warnings — all as
`dew_flow_mcp · todo/PLAN_test_leftovers_E3.md` S2 specifies. `RepoToken = "bench"` (a row of the `## Tokens` table),
`OwnedPrefix = "testsToDelete-bench-"`, so `<what>` ≤ 36 characters (20 + what + 1 + 6 ≤ 63). **S2 starts after E3's
S2 has merged in `dew_flow_mcp`**, so the shape it copies has passed its own review; if the merged E3 code differs from
E3's document, the code wins and this document is corrected in the S2 PR.

**D2 — Where this repository needs more than E3's shape** (each one measured here, not invented):

| Widening | Why | Where |
|---|---|---|
| `Removal` removes links at **every** depth first (as itself, never followed), then clears read-only attributes, then deletes | E3 removes top-level links only; this repository already learned to unlink at any depth (`GateStoreFixtures.cs:90-103`) — the junction tests plant them deep — and makes git repositories, whose object files are read-only on Windows (`GitCheckoutTests.cs:183-197`). Links BEFORE the attribute walk, so the walk can never follow one. Both lessons move INTO `Removal`; `TempGitRepo.DeleteTree` becomes a one-line delegate (S4) | S2 |
| The start-of-run sweep has a **container pass** | this repository creates containers; rule §4 wants a sweep per resource kind. Ryuk collects a killed run's container within seconds, so this pass only ever finds what Ryuk could not (a daemon restart, a reaper killed with its run) | S2 |
| The end-of-run audit lists containers too | a container the fixture forgot to dispose is the costliest leftover there is | S5 |
| The guard's `--name` check is **E1's** (only inside a command that contains `docker`), not E3's bare `--name[ =]+` | this repository's own CLI has a `--name` flag: `Cli/LaneRunPlanningTests.cs:42`, `:57`, `:70`, `:71`, `:119`; `Cli/ResumeCommandTests.cs:110`, `:115`; `Cli/VariantsCommandTests.cs` ×12 — E3's regex would be red on all 19 | S5 |
| One repository class, `testcontainers` | a Testcontainers builder chain with no `.WithName(` or with `.WithReuse(` — rule §5/§6 (b) require both, and rule §7's `docker-name` sees only a `WithName` that exists | S5 |
| The §6 (b) marker `container-scoped` is accepted as a DECLARATION | rule §6 (b) prescribes `testsToDelete-exempt: container-scoped — …`, but §7's grammar knows no class `container-scoped` (E1's own `tools/leftovers-check.mjs:46` would report it as class `exemption`). This guard accepts it when its reason says `lives and dies with` and its file names the container (`.WithName(`) and never sets reuse. Contract defect reported (note P6) | S5 |

**D3 — Everything a test makes lives INSIDE one owned folder.** A helper never puts a second top-level folder in the
temp root: `TempRoot` becomes `<owned>/root` and its siblings `<owned>/root-<name>` (the formula at
`GateStoreFixtures.cs:71` is unchanged — `GetDirectoryName(Path)` is now the owned folder); `DatedGitRepo` becomes
`<owned>/repo` with `<owned>/scratch` beside it. Why it matters beyond tidiness: an UNCLAIMED prefixed folder is
`gone` to the sweep once ten minutes old (rule §4), so a prefixed sibling outside its claimed container would be swept
from under a live run that lasts longer than that. Inside, one claim covers everything, one `Dispose` removes
everything, and the five siblings no list remembers stop leaking with no list at all.

**D4 — The worktree seam is a ROOT, not a leaf name.** `HarvestGates.RunAsync` and `SolutionSignals.RunAsync` each gain
an overload with a trailing `string scratchRoot`, passed to the existing `ScratchWorktree.AddAsync(…, underRoot)`
(`ScratchWorktree.cs:21-22`); the existing signatures delegate with `ScratchWorktree.DefaultRoot` — a new public
property holding exactly today's `Path.Combine(Path.GetTempPath(), "bench-scratch")`, which `:16` then uses. The product
callers (`HarvestCommand.cs:242`, `SolveCommand.cs:184`) are not touched, so **a benchmark run names its worktrees
exactly as today**, and a test pins that (S4 sc. 5). Tests pass `DatedGitRepo.ScratchRoot`: the worktree then sits in
an owned folder and is registered in a repository in the SAME owned folder — rule §1's container clause, no prefix
and no lock needed; a kill is collected with the folder. *Loses:* a leaf-name seam (`testsToDelete-bench-wt-<id>`
directly in the temp root, locked with the owner): it would need the worktree pass E1's Node sweep has, whose
`other clone` rule then preserves every such leaf this C# sweep cannot list — more code, weaker collection.

**D5 — The fakes' homes are owned folders the CHILD can find.** `FakeCli`: `Model` becomes the owned folder's own name
(`testsToDelete-bench-fake-cli-<id>`, 35 characters) and `Home` that folder; `FakeHome.For(model)` returns
`<temp>/<model>` when `model` starts with `testsToDelete-` (ordinal, case-insensitive — the generic prefix, never the
token, so the token stays written once), else today's path (used only by `ScoutsOutOfProcessTests`, whose child's
`TEMP` is redirected into an owned folder). `FakeCoai`: `Root` becomes an owned folder; the fake's `DataDir()` falls
back to `<parent of FAKE_COAI_EVENTS>/data` (= `<Root>/data`) when `COAI_DATA_DIR` is unset, and throws when neither
is set — `fake-coai-data` disappears.

**D6 — The container sweep and the audit spawn `docker`; the owner never does.** Rule §3 forbids spawning to learn a
START time; listing containers is not that. One `docker ps -a -q --no-trunc --filter label=testsToDelete.owner` and,
when it lists anything, ONE `docker inspect` for all ids (never one per container — E1's janitor lesson); removal by
the captured id, `docker rm -f -v <id>` (the anonymous data volume of `postgres` goes with `-v`). Never `--filter
name=` (a substring match). No `docker` binary, a stopped daemon, a non-zero exit, a timeout (30 s) ⇒ one `Errors`
line and nothing removed; the run goes on.

**D7 — No old-prefix window.** None of the old names (`bench-*`, `scout-*`, `fake-coai-data`) carried the prefix, so
the sweep matches only `^teststodelete[-_]bench[-_]` and has no end date (E1's D5, E3, E4 alike). The old leftovers on
a machine are LISTED by hand in S5, shown, and deleted only on the owner's word. `bench-scratch/` is the PRODUCT's and
is never deleted by hand while a benchmark may be running.

**D8 — `spikes/` is out of the guard**, by a pinned exclusion with its reason (spike scaffolding, run by hand under its
own `node --test`, never in CI; a spike that graduates moves to `tests/`). The parent decides whether the spike's 16
sites get an epic of their own (note P8).

## Order

| Story | Model (brief · code · review) | Depends on | Lands |
|---|---|---|---|
| S1 — the pin on the E1 release | Opus (this section) · main session · Opus | **E1 released** | first, the day the release exists — every other merge waits for the required `plans` check |
| S2 — the C# helper, owner liveness, the start-of-run sweep (folders and containers) | Opus · Sonnet · Opus | S1; **E3's S2 merged** (D1) | second |
| S3 — the Testcontainers fixture: name, owner label, Ryuk after a kill, the §6 (b) exemption | Opus · Sonnet · Opus | S2 | third |
| S4 — the shared helpers, the fakes and the worktree seam inside owned folders | Opus · Sonnet · Opus | S2 | parallel with S3 |
| S5 — the guard, the 59 remaining sites, the end-of-run audit, the CI step, the recipe, the docs | Opus · Sonnet · Opus | S2, S3, S4 | last |

S2–S4 may be coded while S1 waits, against the contract text as E1's branch has it; they merge only after S1, and if
the released text differs from what they encode, stop and report — never resolve it in code. S5 is one story for E3's
reason: the guard's test over this repository is red until the last site moves, and two merges would leave `main` red
between them.

### Boundaries (the shared `common/planning-docs.md` § *A boundary between two plans*)

| Item | Built by | The other's part |
|---|---|---|
| The contract, the `## Tokens` table (`bench` row), `tools/janitor.sh` | E1 (`dew_flow_conventions`) | E6 reads them through the mount; never edits them here |
| The Node reference (`owned.mjs`, `leftovers-sweep.mjs`, `leftovers-check.mjs`) | E1 | E6 follows its order; the `--name`-inside-`docker` rule comes from `leftovers-check.mjs:231-233` |
| The C# helper's API and order | E3 (`dew_flow_mcp`), first | E6 copies it by shape (D1), widens it where D2 says, and reports each widening back to E3 as a note |
| The bench helper, the container sweep, the bench guard, this repository's CI step | E6 | E1 and E3 name E6 as the consumer that does it |
| The pin bump | E6 (S1) | E1 releases; consumers bump inside their own epics |

Disjoint otherwise: E6 changes nothing in `dew_flow_conventions` or `dew_flow_mcp`. A contract finding goes to the
parent as a note, not into the mounted copy.

## S1 — The pin on the E1 release

- **Models.** This section is the brief; the main session makes the commit; Opus reviews the diff.
- **Goal.** `.claude/rules/shared` points at the `release` tip that carries the contract, so `Pin check` is green again
  and the mount offers what S2–S5 read: the `## Tokens` table with the `bench` row, `tools/janitor.sh`.
- **Files.** `.claude/rules/shared` (the gitlink); `CLAUDE.md` only if `gate-snippet-check.mjs` says the gate block moved.
- **The change.**
  1. `git -C .claude/rules/shared fetch origin release`, then `git -C .claude/rules/shared checkout <sha>` — the sha
     `git ls-remote <the URL .gitmodules names> refs/heads/release` prints, never `main`'s.
  2. Read the released `common/test-leftovers.md` and confirm one by one: the `## Tokens` table, header `| token |
     family | repository |`, a row whose first cell is `` `bench` ``; §1 (63 characters, the anchored case-insensitive
     match), §2 (five tries 100 ms apart), §3 (`<host>@<os>:<pid>[:<start>]`, the sibling `.owner`, the 2-second
     tolerance, three-valued liveness), §4 (10 minutes), §5 (Testcontainers names its container, reaper on), §6 (b) (the
     `container-scoped` marker), §7 (classes and grammar); `tools/janitor.sh` exists. A difference is a stop: report it
     to the epic's owner.
  3. `git add .claude/rules/shared`; one commit: `chore(rules): pin shared conventions to release <short sha> — test
     leftovers: owner, start-of-run sweep, janitor, guard grammar, tokens table`.
- **Scenarios.** No behaviour changes; the checks below are the evidence, recorded in the PR. RED first: on the old pin,
  after the release, `pin-check.mjs` names `e29883f` as behind.
- **Commands.** `node .claude/rules/shared/tools/pin-check.mjs`; `plan-lifecycle.mjs`; `gate-snippet-check.mjs`;
  `build-flags-check.mjs`; `post-deploy-check.mjs`; `node .github/scripts/branch-protection.mjs --selftest`;
  `git -C .claude/rules/shared grep -n "^| \`bench\` |" common/test-leftovers.md` (one line); the build and the
  executable (the release may carry other rule changes).
- **DoD.** The pin equals the `release` tip; every `plans` step green locally and on CI; step 2's facts in the PR;
  nothing else in the diff; this document's status still *plan only*.

## S2 — The C# helper, owner liveness, the start-of-run sweep

- **Models.** Brief by Opus (this section); code by a Sonnet coder who reads E3's S2 and S3 first; review by Opus against
  rule §1–§4, D1, D2, D6 and the merged E3 code.
- **Goal.** One place through which every test here makes a scratch folder — named, claimed beside itself, removed on
  every path out without ever throwing — and a sweep at the start of each run that collects what a killed run left:
  only this token's, only on a `gone` owner, folders and containers both.
- **Files.** New, all under `tests/Bench.Tests/Leftovers/`: `Owned.cs`, `Liveness.cs`, `OwnedDir.cs` (`OwnedDir`,
  `OwnedScope`, `Removal`), `UnmatchablePid.cs`, `LeftoversSweep.cs`, `DockerLeftovers.cs`,
  `LeftoversSweepFixture.cs` (carries `[assembly: AssemblyFixture(typeof(LeftoversSweepFixture))]`); tests
  `OwnedTests.cs`, `LivenessTests.cs`, `LeftoversSweepTests.cs`, `DockerLeftoversTests.cs`. Nothing outside the folder.

**The change.**

- **E3's S2, verbatim in shape** (`dew_flow_mcp · todo/PLAN_test_leftovers_E3.md` § S2, *The change*): `Owned`
  (`Prefix`, `RepoToken = "bench"` — the ONLY place the token is written, `OwnedPrefix`, `MaxName = 63`, `IdLength = 6`,
  `StartToleranceSeconds = 2`, `Pattern = ^teststodelete[-_]bench[-_]` ignore-case culture-invariant, `TempRoot()`,
  `NameFor(what)`, `DefaultWarn(line)`), `Owner` (record `(Host, Pid, Start)`, `This`, `Format()`, `Parse(text)`),
  `Liveness` (`ProbeProcess`, `FromException`, `State` in rule §3's order), `OwnedDir.Create(what, parent?, warn?)` with
  the internal `nextName` seam, `OwnedScope`, `UnmatchablePid` (Windows `777770 + 4·i`, Linux `pid_max + 1 + i`,
  macOS `100000 + i`, `AssertNoSuchProcess`). Every rule E3 states holds here unchanged: `what` refused before anything
  exists (`ArgumentException`: empty, `/`, `\`, NUL, control characters, a leading `-`, longer than **36**); the id from
  `RandomNumberGenerator.GetString(<62 characters>, 6)`; claim first with `FileMode.CreateNew`, a taken name retried, at
  most five names; nothing ever written INSIDE the folder; `Owner.Parse` strips one trailing `\r?\n` and accepts only
  `^([^@:\s]+@(linux|macos|windows)):(\d+)(?::(\d+))?$`; `CA1416` respected (`SetUnixFileMode` only under
  `!OperatingSystem.IsWindows()`); every async call takes `TestContext.Current.CancellationToken`.
- **`Removal.Quietly(dir, warn)` — widened (D2).** Up to five tries 100 ms apart, each: (1) remove every link under
  `dir` at ANY depth as itself — a directory entry whose `LinkTarget is not null` (or whose attributes carry
  `ReparsePoint`) ⇒ `Directory.Delete(entry)` non-recursive, never descended into; a file link ⇒ `File.Delete` (the
  walk is `GateStoreFixtures.cs:91-103`'s, moved here); (2) set every remaining file's attributes to `Normal` (the walk
  is `GitCheckoutTests.cs:191-194`'s, moved here, and runs only AFTER step 1, so it can never follow a link);
  (3) `Directory.Delete(dir, recursive: true)`. Then the sibling `<dir>.owner`. Every exception caught; a path already
  gone is success; giving up ⇒ ONE line `owned: could not remove <path> (<type>: <message>) — left for the start-of-run
  sweep` and `false`, the folder still prefixed AND claimed.
- **`LeftoversSweep.Run(tmp, now, minAge, probe, docker) → SweepResult(Removed, Preserved, Errors)`** — E3's S3
  (folders: depth-1 directories of `tmp` matching `Owned.Pattern`; a `.git` FILE at the top ⇒ `other clone`; `young`;
  unmarked and old ⇒ remove; `unreadable`; `live`; `foreign host`; `unknown`; then old orphan `.owner` files), plus the
  **container pass** (D2, D6), all through `DockerLeftovers`:

```csharp
public sealed record DockerCall(bool Ran, int ExitCode, string Output);          // Ran = false: no docker binary
public sealed record ContainerFact(string Id, string Name, string OwnerLabel, DateTimeOffset Created);

public static class DockerLeftovers
{
    public const string OwnerLabel = "testsToDelete.owner";                          // rule §3
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);
    public static Func<IReadOnlyList<string>, CancellationToken, Task<DockerCall>> Real { get; }
        // ProcessRunner.RunAsync("docker", args, Owned.TempRoot(), Timeout, ct): Completed ⇒ (true, code, output);
        // NotFound ⇒ (false, -1, ""); TimedOut ⇒ (true, -1, "timed out")
    public static Task<(IReadOnlyList<ContainerFact> Facts, string Error)> ListAsync(
        Func<IReadOnlyList<string>, CancellationToken, Task<DockerCall>> docker, CancellationToken ct);
        // ps -a -q --no-trunc --filter label=testsToDelete.owner ; none ⇒ empty, no inspect
        // else ONE: inspect --format "{{.Id}}\t{{.Name}}\t{{index .Config.Labels \"testsToDelete.owner\"}}\t{{.Created}}" <ids…>
        // Name has its leading '/' trimmed; Created parsed as RFC 3339 (DateTimeOffset.Parse, invariant culture)
    public static Task<string> RemoveAsync(
        Func<IReadOnlyList<string>, CancellationToken, Task<DockerCall>> docker, string id, CancellationToken ct);
        // rm -f -v <id>; "" on success, else the error line
}
```

  The pass: only facts whose `Name` matches `Owned.Pattern`; `now − Created < minAge` ⇒ preserve `young`;
  `Owner.Parse(label)` null ⇒ `unreadable`; `Liveness.State`: `Gone` ⇒ `RemoveAsync`, its failure an `Errors` entry;
  `Live` ⇒ `live`; `Unknown` ⇒ `foreign host` or `unknown` as for folders. A listing that did not run, exited non-zero,
  timed out or did not parse ⇒ ONE `Errors` entry, no container touched. The arguments are argv elements (no shell);
  the `{{…}}` quotes are literal.
- **`LeftoversSweepFixture`** — `public sealed class`, public parameterless constructor, `IAsyncLifetime`:
  `InitializeAsync` calls `Run(Owned.TempRoot(), DateTimeOffset.UtcNow, LeftoversSweep.MinAge, Liveness.ProbeProcess,
  DockerLeftovers.Real)` inside `try/catch`, keeps `LastRun`, and — only when `Removed + Preserved + Errors > 0` —
  writes `bench tests: leftovers — removed N, preserved M` plus one line per error through a `report` seam (public:
  `Console.Error.WriteLine`, no test is running yet); **never throws**. `DisposeAsync` is a no-op until S5 adds the
  audit. An internal constructor `(Func<Task<SweepResult>> sweep, Action<string> report)` for tests.

**Scenarios** (RED first: each test written against the signatures before the bodies exist, the build failure
recorded, then each observed failing on its assertion. Every scenario that counts what a call left passes a PRIVATE
`parent`/`tmp` — an `OwnedDir` of its own; dead owners are `UnmatchablePid` with the premise asserted; "old" is
`SetLastWriteTimeUtc(now − 11 min)`; the container pass is driven through a fake `docker` function that records argv and
answers from fixture text — never the real daemon, except scenario 16):

1–11. **E3 S2's eleven scenarios**, with `mcp` → `bench` and the length bound 36/37: the name; a bad `what` creates
   nothing; folder and claim side by side, never inside (`path Unicode пробел`); disposal removes both, idempotent;
   unwind through `OwnedScope`; teardown never throws and leaves the leftover prefixed AND claimed (Windows: a file
   held with `FileShare.None`; POSIX: a parent made read-only, `Assert.Skip` as root); a link is unlinked, never
   followed; a claim that cannot be written creates nothing; the owner round-trips and refuses every other text;
   `State` follows rule §3's order (2 s ⇒ live, 3 s ⇒ unknown, never gone); the real probe.
12. **A link at depth three is unlinked, never followed** (D2's widening): a sentinel `OwnedDir` holding `keep.txt`; in
    the folder under test, `a/b/link` → sentinel (`Directory.CreateSymbolicLink`, on Windows falling back to `cmd.exe
    /c mklink /J`, the fallback this repository's junction tests use; `Assert.Skip` when neither can be made). After
    `Dispose()` the folder is gone and `keep.txt` still reads `must survive`. RED with E3's top-level-only walk.
13. **A git repository inside an owned folder is removed** (D2's widening): `git init` + one commit inside the folder
    (read-only object files on Windows); `Dispose()` ⇒ gone, no warning.
14. **E3 S3's sweep scenarios 1–6**, with `bench`: a killed run's folder is collected (with a top-level link to a
    sentinel that survives); concurrent-owner survival (`live`, `foreign host` ×2, `unreadable`, start 3 s off ⇒
    `unknown`, `young`, `.git` file ⇒ `other clone`, unmarked ⇒ removed, dead ⇒ removed — every preserved folder still
    there with exactly its reason); anchored and scoped (`x-testsToDelete-bench-a-abc123`, `testsToDeletebench-a-abc123`,
    `testsToDelete-mcp-a-abc123`, `testsToDelete-benchx-a-abc123`, `bench-gate-tests` untouched and unlisted;
    `TESTSTODELETE-BENCH-a-xyz123` and `teststodelete_bench_a_xyz123` removed); orphan claims; never fails the run; silence
    when there is nothing.
15. **A killed run's worktree goes with its folder** (the parent's "worktree killed before its finally"): in `tmp`, an
    old dead-claimed `testsToDelete-bench-gitrepo-abc123/` holding `repo/` (a real repository) and `scratch/<guid>/` (a
    real linked worktree of it, `git worktree add --detach`) ⇒ `Run` removes the whole folder; nothing named
    `testsToDelete-bench-` is left in `tmp`; the folder was NOT preserved as `other clone` (its `.git` FILE is at depth
    two, not at the top).
16. **The container pass decides by owner, removes by id** — fake `docker`, one `inspect` answer with six containers,
    all old unless stated: `/testsToDelete-bench-pg-aaaaaa` owned by a dead pid ⇒ exactly one `rm -f -v <its id>`;
    owned by THIS process ⇒ `live`; `other@<tag>:1:1` ⇒ `foreign host`; label `hands off` ⇒ `unreadable`; young and
    dead ⇒ `young`; `/testsToDelete-mcp-pg-bbbbbb` dead ⇒ untouched and unlisted. Asserted on the recorded argv: one
    `ps`, ONE `inspect` naming all six ids, one `rm`, never `--filter name=`. Then: `ps` answering no ids ⇒ no `inspect`
    at all; `Ran = false` ⇒ one `Errors` entry `no docker binary — containers not swept`, folders still swept; `ps`
    exiting 1 ⇒ one `Errors` entry, no `rm`; an `inspect` line that does not parse ⇒ one `Errors` entry, no `rm`.
17. **The fixture ran before this test, over the real temp root and the real daemon**: a class taking
    `LeftoversSweepFixture` asserts `LastRun` is not null; and, in `[Collection("postgres")]` (the fixture's container is
    up — before S3 it carries no label and is simply not listed), `DockerLeftovers.ListAsync(DockerLeftovers.Real, ct)`
    returns no error.

- **Commands.** `node .claude/rules/shared/tools/low-priority.mjs dotnet build dew_flow_benchmark.slnx -c Release -m:4`;
  `node .claude/rules/shared/tools/low-priority.mjs ./tests/Bench.Tests/bin/Release/net10.0/Bench.Tests.exe --filter-namespace "Bench.Tests.Leftovers"`;
  the whole executable, alone (`module_tests.md:36-40`); on Linux (WSL or CI) `./tests/Bench.Tests/bin/Release/net10.0/Bench.Tests --filter-namespace "Bench.Tests.Leftovers"`;
  `dotnet format dew_flow_benchmark.slnx --verify-no-changes --no-restore` in Linux or WSL.
- **DoD.** 0 warnings; every scenario observed RED, then GREEN, on Windows and on the Linux CI runner; the sweep's wall
  time on a full run recorded (one temp-root listing, two `docker` calls); every widening of D2 reported to E3 as a note
  in the PR; no caller outside the folder yet.

## S3 — The Testcontainers fixture: name, owner label, Ryuk after a kill, the §6 (b) exemption

- **Models.** Brief by Opus; code by a Sonnet coder; review by Opus against rule §3, §5, §6 (b) and D6.
- **Goal.** The fixture's container is `testsToDelete-bench-pg-<id>`, unique per process on a shared daemon, labelled
  with its owner, reaped by Ryuk when its process is killed; the per-test databases are declared container-scoped where
  they are created.
- **Files.** `tests/Bench.Tests/Infrastructure/PostgresFixture.cs`; new
  `tests/Bench.Tests/Infrastructure/PostgresFixtureLeftoversTests.cs`; `research/module_tests.md` (`:14`, and one
  sentence in § *Where the harness is*).

**The change.**

```csharp
/// The one place the container is built — the fixture's field and the kill harness's child use the same builder.
internal static PostgreSqlContainer NewContainer() => new PostgreSqlBuilder("postgres:17-alpine")
    .WithName(Owned.NameFor("pg"))                                   // rule §5: named with the prefix, random per process
    .WithLabel(DockerLeftovers.OwnerLabel, Owner.This.Format())     // rule §3: the owner as a label
    .WithDatabase("bench")
    .WithUsername("bench")
    .WithPassword("bench")
    .WithCommand("-c", "max_connections=400")                        // the comment at :31 moves with it
    .Build();

private readonly PostgreSqlContainer _container = NewContainer();
public string ContainerId => _container.Id;
public string ContainerName => _container.Name.TrimStart('/');
```

- No `.WithReuse(…)`, no `.WithAutoRemove(…)` change, no environment variable: Ryuk stays on by default.
- The §6 (b) marker on the line directly above `:108`'s `ExecuteSqlRawAsync($"CREATE DATABASE …")`, after the
  `#pragma` line: `// testsToDelete-exempt: container-scoped — every per-test database of NewDatabaseAsync and
  NewEmptyDatabaseAsync lives and dies with PostgresFixture's container` — and the class summary (`:9-18`) gains one
  sentence: the container is named and labelled for the leftovers rule, Ryuk reaps it after a kill, and the per-test
  databases are never dropped because they die with it.
- `module_tests.md:14`: `postgres:17` → `postgres:17-alpine`, and the sentence above.

**Scenarios** (RED first: the two properties are added first — no behaviour change — then scenarios 1–2 are written and
observed red on the unchanged builder, with the real symptom recorded in the PR):

1. **The fixture's container carries the owned name and this run's owner** — `[Collection("postgres")]`:
   `ContainerName` matches `^testsToDelete-bench-pg-[A-Za-z0-9]{6}$`; ONE `docker inspect --format "{{index
   .Config.Labels \"testsToDelete.owner\"}}" <ContainerId>` through `DockerLeftovers.Real` parses (`Owner.Parse`) to
   `Owner.This`. RED on `main`: a Docker-generated name (`adjective_surname`) and an empty label.
2. **A killed test process leaves no container behind** — the child harness. A child test
   `Ryuk_child_holds_a_named_container` in the same class returns at once unless `BENCH_LEFTOVERS_CHILD` is `ryuk` (so
   the normal suite runs it as a no-op pass); otherwise it starts `PostgresFixture.NewContainer()`, writes
   `<id>\n<name>\n<Environment.ProcessId>\n` to the file `BENCH_LEFTOVERS_CHILD_OUT` names, and awaits
   `Task.Delay(Timeout.InfiniteTimeSpan, ct)` — it never disposes. The parent test: `using var dir =
   OwnedDir.Create("ryuk-child")`; starts `Environment.ProcessPath` with `--filter-method
   "*Ryuk_child_holds_a_named_container*"`, the two variables, stdout and stderr redirected and drained; polls the out
   file (2 s steps, 120 s ceiling, a timeout fails naming the child's output); asserts the name matches the pattern and
   the label's pid is the CHILD's; `Kill(entireProcessTree: true)`, `WaitForExitAsync`; then polls `docker ps -a -q
   --no-trunc --filter id=<id>` every 2 s for at most 120 s ⇒ empty. `finally`: kills the child if still running and,
   if the container still exists, `docker rm -f -v <id>` — the test never leaks what it proves. RED on `main` on the
   name assertion; the removal itself is already green (Ryuk is on today) — recorded as such.
3. **A per-test database dies with the fixture's container** — the premise of the exemption, a characterization test:
   a SEPARATE `new PostgresFixture()`, `InitializeAsync`, `NewEmptyDatabaseAsync($"lifetime_{Guid.NewGuid():N}")` ⇒ a
   connection string; record `ContainerId`; `DisposeAsync`; then `docker ps -a -q --no-trunc --filter id=<id>` is empty
   AND opening an `NpgsqlConnection` on the recorded string (with `Timeout=3`) throws. Green on `main` once the
   property exists — it proves the exemption's claim, not a fix.
4. **Unique per process**: two `NewContainer()` builds (not started) carry different names.

- **Commands.** As S2, `--filter-class "*PostgresFixtureLeftoversTests"`; then the whole executable alone, twice in a
  row, and `docker ps -a --filter label=testsToDelete.owner` afterwards lists nothing of this run's.
- **DoD.** Scenarios 1–2 observed RED with the symptom above, then GREEN, on Windows (Docker Desktop) and the Linux CI
  runner; scenario 3 green; the wall time of scenario 2 recorded (expected well under a minute); `module_tests.md`
  updated.

## S4 — The shared helpers, the fakes and the worktree seam inside owned folders

- **Models.** Brief by Opus; code by a Sonnet coder; review by Opus against D3, D4, D5 and the leak list of the facts.
- **Goal.** The helpers most tests build on make everything inside ONE owned folder, so the sibling leaks, the fixed
  parents, `fake-coai-data` and the GUID-named worktrees in `bench-scratch` end without touching the tests that use
  them; and a benchmark run still names its worktrees exactly as today.
- **Files.** Tests: `Infrastructure/GateStoreFixtures.cs`, `Infrastructure/DatedGitRepo.cs`,
  `Infrastructure/GitCheckoutTests.cs`, `Probes/Driver/FakeCli.cs`, `Gate/Driver/FakeCoai.cs`,
  `Scouts/ScoutLaunchProcessTests.cs`, `Infrastructure/CliSubjectRuntimeTests.cs`, `Infrastructure/HarvestGatesTests.cs`,
  `Infrastructure/SolutionSignalsTests.cs`, `tests/FakeCli/Program.cs`, `tests/FakeCoai/Program.cs`; the sibling-list
  removals in `Cli/ScoutsCliSetup.cs`, `Cli/ScoutsTasksCommandTests.cs`, `Scouts/ScoutTreesTests.cs`,
  `Gate/Driver/GateDriverRig.cs`, `Gate/Driver/GateCloneRig.cs`, `Cli/ProbesCliSetup.cs`, `Probes/Driver/ProbeDriverRig.cs`;
  product: `src/Bench.Infrastructure/Git/ScratchWorktree.cs`, `HarvestGates.cs`, `SolutionSignals.cs`; new test
  `Leftovers/OwnedHelpersTests.cs`.

**The change.**

| Where | Becomes |
|---|---|
| `GateStoreFixtures.cs:61-104` `TempRoot` | holds `private readonly OwnedDir _owned = OwnedDir.Create("gate-tests")`; `Path = Path.Combine(_owned.Path, "root")`, created in the constructor; `Sibling` unchanged (`:71` — its parent is now the owned folder); `Dispose() => _owned.Dispose()`; `RemoveLinks` (`:90-103`) deleted — it lives in `Removal` now. `NewRoot()` (`:53`) unchanged |
| `GitCheckoutTests.cs:184-197` `TempGitRepo.DeleteTree` | one line: `Removal.Quietly(path, Owned.DefaultWarn);` — its ten callers keep compiling; S5 removes the ones it migrates |
| `GitCheckoutTests.cs:147-176` `TempGitRepo` | holds an `OwnedDir` (`"src-repo"`); `Path = <owned>/repo`; `Dispose()` disposes the `OwnedDir` |
| `GitCheckoutTests.cs:20`, `:25-30` | `_root` = `Path.Combine(_cache.Path, "cache")` of an `OwnedDir` field `_cache` (`"cache"`); `DisposeAsync` disposes `_source` and `_cache` |
| `DatedGitRepo.cs:16-22`, `:91-101` | `private readonly OwnedDir _owned = OwnedDir.Create("dated-git")`; `Root => Path.Combine(_owned.Path, "repo")`; new `ScratchRoot => Path.Combine(_owned.Path, "scratch")`; `Dispose() => _owned.Dispose()`; `_id` stays (it is the committer identity, `:51`) |
| `FakeCli.cs:12-18`, `:68-78` | `_owned = OwnedDir.Create("fake-cli")`; `Model = Path.GetFileName(_owned.Path)`; `Home = _owned.Path`; `Dispose() => _owned.Dispose()`; the class summary (`:5-9`) says the model id IS the home's name |
| `tests/FakeCli/Program.cs:554` `FakeHome.For` | `model.StartsWith("testsToDelete-", StringComparison.OrdinalIgnoreCase) ? Path.Combine(Path.GetTempPath(), model) : <today's expression>`, under `// testsToDelete-exempt: raw-temp — the fake child derives the home its parent test claimed, or reads the TEMP its parent redirected into an owned folder` on the line above; the summary (`:548-551`) updated |
| `FakeCoai.cs:14-22`, `:107-117` | `Root` = an `OwnedDir` (`"gate-fake"`); `Dispose()` disposes it |
| `tests/FakeCoai/Program.cs:417` `DataDir()` | `COAI_DATA_DIR` when set; else `Path.Combine(Path.GetDirectoryName(<FAKE_COAI_EVENTS trimmed of a trailing separator>)!, "data")`; neither set ⇒ `throw new InvalidOperationException("fake-coai: neither COAI_DATA_DIR nor FAKE_COAI_EVENTS is set — the fake writes only into a test-owned folder")` |
| `ScoutLaunchProcessTests.cs` | the class becomes `IDisposable` with `private readonly OwnedScope _owned = new();`; `Home()` (`:129`) an instance method `=> _owned.Dir("scout-fake")`; `:100` and `:107` use `Owned.TempRoot()` (the assertion's meaning — `__CWD__` is the launch's cwd — unchanged) |
| `CliSubjectRuntimeTests.cs:62-107` | `root` = `using var owned = OwnedDir.Create("subject-guard")` → `owned.Path`; the manual `Directory.Delete(root)` in `finally` (`:102-105`) goes; `repo.Dispose()` stays |
| `ScratchWorktree.cs:13-16` | `public static string DefaultRoot => Path.Combine(Path.GetTempPath(), "bench-scratch");` and the default overload passes it — the same string as today |
| `HarvestGates.cs:50-58`, `SolutionSignals.cs:53-63` | each gains an overload with a trailing `string scratchRoot` that passes it to `ScratchWorktree.AddAsync(…, scratchRoot)`; the existing signature delegates with `ScratchWorktree.DefaultRoot`. `HarvestCommand.cs`, `SolveCommand.cs` untouched |
| `HarvestGatesTests.cs`, `SolutionSignalsTests.cs` | every call passes `_repo.ScratchRoot` |
| The sibling lists | deleted, since one `Dispose` removes everything (D3): `ScoutsCliSetup.cs:217-227`; `ScoutsTasksCommandTests.cs:309-319`; `ScoutTreesTests.cs:290-300`; `GateDriverRig.cs:151-162` with its calls `GateDriverRig.cs:145`, `GateCloneRig.cs:44`, `ProbeDriverRig.cs:121`; `ProbesCliSetup.cs:125-126` with its `TryDelete` (`:131-140`); `ProbeDriverRig.cs:122` with its `TryDelete` (`:127-136`). Any other `Dispose` that deletes a `Sibling(…)` path is found by searching `tests/` for `Sibling(` (20 files hold one) and goes the same way; one that deletes something NOT under the rig's `TempRoot` stays |

Assertions do not change.

**Scenarios** (RED first: 1–4 written and run on the unchanged helpers, the failures recorded; the assertion never changes,
only the call shape where a new parameter exists):

1. **`A_sibling_no_rig_lists_is_gone_when_the_root_is_disposed`** — `var root = GateStoreFixtures.NewRoot(); var keys
   = root.Sibling("keys"); Directory.CreateDirectory(keys); File.WriteAllText(Path.Combine(keys, "scouts.key"), "k");
   root.Dispose();` ⇒ `keys` does not exist, and no folder named after the root's leaf remains in its parent. RED on
   `main`: `bench-gate-tests/<guid>-keys` with the key in it.
2. **`A_fake_cli_home_is_an_owned_folder_and_the_child_finds_it`** — `using var fake = new FakeCli(script)`; launch the
   real fake (`FakeCli.Executable`) with `--model <fake.Model>` and an environment that carries only the parent's
   `TEMP`/`TMP`/`TMPDIR` (the probes' minimal shape); `fake.Calls()` holds the call; `Path.GetFileName(fake.Home)`
   matches `Owned.Pattern` and `fake.Home + ".owner"` exists; after `Dispose()` neither exists and
   `<temp>/bench-fake-cli/<fake.Model>` was never created. RED on `main`: the home is `bench-fake-cli/fake-<guid>`.
3. **`The_fake_coai_writes_its_data_inside_its_owned_folder`** — open the fake through `FakeCoai.OpenAsync()` with no
   `COAI_DATA_DIR`, call the tool that writes usage rows; `<fake.Root>/data/usage.jsonl` exists and
   `<temp>/fake-coai-data` was not written by this call (compare its last-write time before and after, absent counts as
   unchanged). RED on `main`: the row lands in `fake-coai-data`.
4. **`The_gate_worktree_lives_inside_the_tests_owned_folder`** (in `HarvestGatesTests`, and the same in
   `SolutionSignalsTests`) — a build command that prints its tree and fails: `new GateCommand("git", ["rev-parse",
   "--show-toplevel", "--verify", "no-such-ref-for-the-worktree-probe"])`; the report's red detail
   (`HarvestGates.cs:137` / the solution's build detail) names the worktree; assert it contains
   `Path.GetFileName(Path.GetDirectoryName(_repo.Root))` — the owned folder's name — and does not contain
   `bench-scratch`. RED on `main` (default root): the detail names `…/bench-scratch/<guid>`.
5. **`A_benchmark_run_still_names_its_worktrees_as_before`** — `ScratchWorktree.DefaultRoot` equals
   `Path.Combine(Path.GetTempPath(), "bench-scratch")`, and the old `HarvestGates.RunAsync` signature still compiles and
   is what `HarvestCommand.cs:242` calls (a pin, not a red: it guards the parent's "the product's naming must not
   change").
6. **Nothing else moved:** the whole executable passes with the same test count, on Windows and the Linux CI runner —
   in particular `ScoutsOutOfProcessTests` (the redirected `TEMP` now inside an owned folder), the junction tests
   (`FileSystemGateArtifactStoreTests`, `ScoutsAccessCheckCommandTests`), and every `[Collection("postgres")]` rig.

- **Commands.** As S2; `--filter-class "*OwnedHelpersTests"`, `"*HarvestGatesTests"`, `"*SolutionSignalsTests"`,
  `"*ScoutsOutOfProcessTests"`; the whole executable alone, Windows and Linux.
- **DoD.** Scenarios 1–4 observed RED with the symptoms above, then GREEN; 5 and 6 green; the product diff is exactly
  the two overloads and `DefaultRoot`; no model id column rejects the 35-character fake model id (if one does: stop and
  report — do not shorten `what` below the rule's form silently).

## S5 — The guard, the 59 remaining sites, the end-of-run audit, the CI step, the recipe, the docs

- **Models.** Brief by Opus; code by a Sonnet coder; review by Opus against rule §1, §5–§7, D2, D7, D8.
- **Goal.** No file under the guard's roots makes a temp path, a worktree or a container outside the helper (or under a
  valid inline exemption); the token is registered; a scan of nothing is red; and a run that leaves a folder or a
  container it claimed is a red run.
- **Files.** New `tests/Bench.Tests/Leftovers/LeftoversGuard.cs`, `LeftoversGuardTests.cs`, `LeftoversAudit.cs`,
  `LeftoversAuditTests.cs`; `LeftoversSweepFixture.cs` (gains the audit); `Owned.cs` (one exemption line); the 38 files of
  the migration table; `.github/workflows/ci.yml`; `http/README.md`; `research/module_tests.md`.

**The guard.** `LeftoversGuard.Scan(string repoRoot) → GuardReport(int FilesScanned, IReadOnlyList<Finding> Findings)`,
`Finding(File, Line, Class, Text)`; pure apart from reading files; E3's S4 port with D2's three corrections.

- **Root.** `TestRepository.Root()` (`Infrastructure/TestRepository.cs:7`) — reused, not copied. (Its three copies are
  a neighbour finding for the summary, not this story's change — see *Reuse*.)
- **Discovery** — fixed roots, a list a test pins: `tests/**/*.cs`; `scripts/**/*.{sh,ps1}`; `.github/workflows/*.{yml,yaml}`;
  `.github/scripts/**/*.{mjs,js,cjs,sh,ps1}`; `http/**/*.{mjs,js,cjs}`; never entering `bin`, `obj`, `node_modules`,
  `.git`, `.claude`, `logs`. `spikes/`, `src/`, `hosts/` are not roots (D8; product code is not a test tree).
  **Excluded by name**, pinned to exactly one entry: `tests/Bench.Tests/Leftovers/LeftoversGuardTests.cs`. Zero files ⇒
  `FilesScanned == 0`, reported as `scanned nothing under <root> — discovery broke; the repository is not clean`.
- **Comments.** A line whose trimmed text starts with `//`, `///` or `#` is skipped for matching and read for exemptions.
- **Classes:**
  - `raw-temp` — `CreateTempSubdirectory\s*\(`, `GetTempPath2?\s*\(`, `GetTempFileName\s*\(`,
    `GetEnvironmentVariable\(\s*"(TEMP|TMP|TMPDIR)"`, `\bmkdtemp(Sync)?\s*\(`, `\btmpdir\s*\(`,
    `process\.env\.(TEMP|TMP|TMPDIR)\b`, `\btemp_dir\s*\(`; in `.sh` and workflows `\bmktemp\b`; in `.ps1`
    `New-TemporaryFile|\$env:TE?MP\b`. (`start.Environment["TEMP"] = …`, `ScoutsOutOfProcessTests.cs:109-111`, sets a
    child's temp root and is not a creation — not matched.)
  - `raw-worktree` — `["']worktree["']\s*,\s*["']add["']`, `\bworktree\s+add\b`.
  - `docker-run` — `\bdocker\s+run\b` on a logical command (trailing `\` joins lines) without `--rm`; the §5 carve-out
    is NOT recognised (E3's deviation, kept: this repository starts no container by hand; the first one re-opens this
    class).
  - `docker-name` — **only in a logical command containing `\bdocker\b`** (`dew_flow_conventions ·
    tools/leftovers-check.mjs:231-233`): `--name[ =]+(\S+)`; anywhere: `docker\s+(volume|network)\s+create\s+(\S+)`,
    `WithName\(\s*"([^"]*)"`; the captured name not matching `^teststodelete[-_]` (case-insensitive, quotes stripped).
    A `WithName(Owned.NameFor(…))` carries no literal and is not a finding.
  - `testcontainers` (this repository's, D2) — from a line matching `new \w+Builder\(` in a file that has
    `using Testcontainers` to the next `.Build()`: no `.WithName(` ⇒ finding `a Testcontainers container without a
    testsToDelete name`; any `.WithReuse(` in such a file ⇒ finding `a reused Testcontainers container outlives its run`.
  - `shared-store` — `dew_flow_conventions · tools/leftovers-check.mjs:131-144`'s four shapes, case-sensitive.
  - `ryuk-off` — `TESTCONTAINERS_RYUK_DISABLED\s*[:=]\s*["']?true` (case-insensitive), never exemptible.
- **Exemptions — inline only** (rule §7; E3's marker and parse regexes, `isReason` as
  `dew_flow_conventions · tools/ownership-check.mjs:83-91`): on the finding's line or the line above; class one of
  `raw-temp`, `raw-worktree`, `docker-run`, `docker-name`, `shared-store`, `testcontainers`; a reason ≥ 10 characters,
  ≥ 3 words, not a placeholder; `shared-store` needs `lock:`. **Plus the §6 (b) declaration** (D2): `container-scoped`
  is valid when its reason contains `lives and dies with` and the same file has a builder chain with `.WithName(` and no
  `.WithReuse(`; otherwise a finding of class `exemption`. Anything else after the marker ⇒ class `exemption`.
- **The token** — E3's `TokenFindings(ruleText, token)` over `<root>/.claude/rules/shared/common/test-leftovers.md`:
  `Owned.RepoToken` matches `^[a-z0-9]{2,12}$`, the table header is exactly `| token | family | repository |`, the token
  is the first cell of exactly one row, no token twice; the file missing ⇒ a finding (`git submodule update --init
  .claude/rules/shared`).

**The migration** (59 findings, 38 files; `what` is the old name without `bench-`; every class that gains a scope
becomes `public sealed class X : IDisposable` with `private readonly OwnedScope _owned = new();` and `public void
Dispose() => _owned.Dispose();` — xUnit v3 disposes the instance after each test, passed or thrown; a class already
`IDisposable` disposes `_owned` in its existing `Dispose`):

| Shape | Sites | Becomes |
|---|---|---|
| A — a file in the temp root | `CliContractTests.cs:107`, `FixRunPlanningTests.cs:96`, `HarvestLandingTests.cs:193`, `ModelsCommandTests.cs:237`, `QuestionsCommandTests.cs:156`, `RunCheckoutTests.cs:116`, `RunCommandTests.cs:115`, `RunRetrievalTests.cs:246`, `SweepRecoveryTests.cs:115`, `TempBank.cs:16` | the `Temp*` helper holds an `OwnedDir` (`"suite"`, `"fixbank"`, `"stmt"`, `"models-suite"`, `"bank"`, `"checkout-suite"`, `"run-suite"`, `"retrieval-suite"`, `"sweep-suite"`, `"lanebank"`); `Path = Path.Combine(_dir.Path, "<the old file name without its guid>")` (e.g. `suite.json`); its `Dispose` disposes the `OwnedDir` instead of `File.Delete` |
| B — a folder removed today | `ArchitectureTests.cs:347`; `PromptCatalogTests.cs:132`, `:138`; `CliLoggingTests.cs:127`; `RunCheckoutTests.cs:25`; `TelemetryCommandTests.cs:292`; `GateRubricsTests.cs:75`; `SettingsCheckTests.cs:12`; `FileSystemLinkTargetsTests.cs:14`; `GitCheckoutLocalSourceTests.cs:23`; `ScoutCampaignKeyTests.cs:14`; `WorkspaceTrustTests.cs:21`; `ScoutScrubberTests.cs:56` | an `OwnedDir` (or `_owned.Dir(…)`) in place of the path, its existing removal replaced by the `OwnedDir`'s disposal; `ArchitectureTests.cs:347` — `using var root = OwnedDir.Create("arch-planted")`, the `finally` (`:355-358`) goes (the fixed name ends: two runs no longer share it); `DirectoryInfo` fields (`FileSystemLinkTargetsTests.cs:14`, `ScoutCampaignKeyTests.cs:14`) become `new DirectoryInfo(_owned.Dir(…))` |
| C — never removed | `VettingPassTests.cs:326`, `:339`; `DailyRunFileSinkTests.cs:92`; `FilesystemEngineTests.cs:304`; `QlnEngineTests.cs:287`; `TracePortTests.cs:166`, `:183`, `:268` | `_owned.Dir("empty-tree")`, `_owned.Dir("anchored-tree")`, `_owned.Dir("daily-log")`, `_owned.Dir("fs-engine")`, `_owned.Dir("qln")`, `Path.Combine(_owned.Dir("fixture"), "<file>.json")`; a `private static` factory becomes an instance method |
| D — the temp root as a path | `AuthoringPassTests.cs:280`, `:292`; `GateAaCommandTests.cs:214`; `GateAssessCommandTests.cs:48`; `GateDriverCommandTests.cs:32`, `:33`; `FilesystemEngineTests.cs:184`; `QlnEngineLiveTests.cs:174`; `QlnEngineTests.cs:301`; `ProductPinReaderTests.cs:72`; `FileSystemGateArtifactStoreTests.cs:237`, `:238`, `:246`; `GitCheckoutLocalSourceTests.cs:93`; `ProcessRunnerCancellationTests.cs:25`; `ProcessRunnerTests.cs:15`, `:26`, `:39`, `:56`, `:75`, `:92`, `:107`, `:138`; `AssessorArgvTests.cs:78`; `CliAgentLiveTests.cs:83`; `AnchorCheckTests.cs:103`, `:110`; `TracePortTests.cs:257` | `Owned.TempRoot()` in place of `Path.GetTempPath()` — meaning unchanged (E3's precedent); `GateDriverCommandTests.cs:33`'s `--artifact-root` too (the verb refuses before writing, `:35-36`) |
| The helper itself | `Owned.cs` `TempRoot()` | the line above `Path.GetTempPath()`: `// testsToDelete-exempt: raw-temp — the helper is the one place a test learns the temp root` |

**The end-of-run audit.** `LeftoversAudit.ClaimedFolders(string tmp, Owner owner)` — every depth-1 folder of `tmp`
matching `Owned.Pattern` whose claim parses to exactly `owner` (host, pid, start), plus every such orphan claim (E3's);
`LeftoversAudit.ClaimedContainersAsync(docker, owner, ct)` — every container `DockerLeftovers.ListAsync` returns whose
label parses to exactly `owner`. `LeftoversSweepFixture.DisposeAsync` (after the last collection, the postgres one
included, has been disposed) computes both, removes each (`Removal.Quietly`, `DockerLeftovers.RemoveAsync`) so the
machine is clean either way, and when either list was not empty throws `InvalidOperationException("bench tests: this run
left <n> folder(s) and <m> container(s) it claimed — <names>; a test made scratch outside a disposed scope")`. A docker
listing that fails is ONE warning line, never the verdict.

**CI.** `build-test` gains, after checkout and before `Formatted`, the step `plans` already has (`ci.yml:119-120`):

```yaml
      - name: Fetch the shared rules
        run: git submodule update --init --depth 1 .claude/rules/shared
```

**The recipe.** `http/README.md:32-33` becomes

```bash
PG="testsToDelete-bench-suitepg-$(od -An -N4 -tx1 /dev/urandom | tr -d ' \n')"
docker run -d --rm --name "$PG" -e POSTGRES_PASSWORD=suite -e POSTGRES_USER=suite \
  -e POSTGRES_DB=bench -p 55432:5432 postgres:17
```

and the block ends with `docker stop "$PG"   # --rm removes the container and its anonymous volume`. No owner label: a
person's shell is not an owner the janitor can judge (D-note P7); unmarked and prefixed, the janitor collects it after
its one-hour window.

**Docs.** `research/module_tests.md` gains a section *Test leftovers*: the helper (`tests/Bench.Tests/Leftovers`,
`testsToDelete-bench-<what>-<id>`, the sibling `.owner`, everything a helper makes inside one owned folder), the
start-of-run sweep (assembly fixture; folders and containers; owner liveness; 10 minutes), the end-of-run audit, the
fixture's name, label and Ryuk, the §6 (b) declaration, the guard and its classes, the CI step, the excluded `spikes/`,
and the janitor `bash .claude/rules/shared/tools/janitor.sh --dry-run` (Git for Windows' bash on Windows, never WSL's).
Prose only — `ModuleTestsCatalogTests` parses the tables.

**Hand clean-up**, once per machine, recorded in the PR, not code (D7): list the temp root's `bench-gate-tests/`,
`bench-dated-git/`, `bench-gate-fake/`, `bench-fake-cli/`, `bench-scout-fake/`, `bench-gate-settings/`,
`fake-coai-data/`, `bench-arch-planted-vendors-literal/`, and the `bench-*-<32 hex>` / `bench-*-<32 hex>.json` /
`scout-{links,key,hygiene}-*` / `bench-{empty,anchored}-tree*`, `bench-daily-log*`, `bench-fs-engine*`, `bench-qln*`,
`bench-fixture*`, `bench-spool*` names the old helpers made; show the list to the owner; delete only on the owner's
word. `bench-scratch/` and `bench-assess-*` are the PRODUCT's: listed, never deleted by hand while a benchmark may run.

**Scenarios** (RED first: scenario 1 is written and run on the last `main` commit AFTER S4 merged and BEFORE any S5
migration):

1. **This repository passes the guard.** `Scan(TestRepository.Root())` ⇒ `FilesScanned > 0`, no findings; the failure
   lists every finding as `file:line — class — text`. **RED: exactly 59 `raw-temp` findings in 38 files, and nothing
   else** (the S4 exemption in `tests/FakeCli/Program.cs` valid, the S3 declaration valid, no `docker-name` from the
   CLI's own `--name` flags) — the list goes in the PR.
2. **The never-removed sites stop leaking** — one new test per class C file is not needed: the audit (scenario 8) is
   the proof. RED: before the migration, run `--filter-class "*VettingPassTests"`, `"*DailyRunFileSinkTests"`,
   `"*FilesystemEngineTests"`, `"*QlnEngineTests"`, `"*TracePortTests"` and count the new `bench-empty-tree*`,
   `bench-anchored-tree*`, `bench-daily-log*`, `bench-fs-engine*`, `bench-qln*`, `bench-fixture*` entries in the temp
   root (record N per class); GREEN: after, zero new entries and zero `testsToDelete-bench-` entries.
3. **Break-it per class**, each on a tree written at run time into a private `OwnedDir`: E3 S4 scenario 3's list, with
   `bench`; plus `Run("lanes", "add", "--name", lane)` in `tests/X/A.cs` ⇒ 0 (no `docker` on the command); `docker run
   --rm --name cache img` in `scripts/a.sh` ⇒ 1 `docker-name`; `new PostgreSqlBuilder("postgres:17").WithDatabase("x")
   .Build()` in a file with `using Testcontainers.PostgreSql;` ⇒ 1 `testcontainers`; the same with
   `.WithName(Owned.NameFor("pg"))` ⇒ 0; with `.WithReuse(true)` ⇒ 1; `// testsToDelete-exempt: container-scoped —
   the databases live and dies with the fixture container` beside a named, unreused builder ⇒ 0; the same in a file
   whose builder has no `.WithName(` ⇒ 1 `exemption`; `// testsToDelete-exempt: container-scoped — some fine reason
   here` ⇒ 1 `exemption`; a file under `spikes/` ⇒ not scanned.
4. **A scan of nothing is red** — an empty tree and a root that does not exist ⇒ `FilesScanned == 0`, no throw.
5. **The token is registered** — on the real mount, `bench` ⇒ none; E3 S4 scenario 5's break-its with `bench`.
6. **The exclusion list is exactly one file, and the roots list is exactly the six above** — pinned.
7. **The audit names only what THIS run claimed** — folders: E3 S4 scenario 7; containers, through a fake `docker`: one
   labelled with `Owner.This` ⇒ listed; a dead pid, `other@<tag>`, another token ⇒ not listed.
8. **The audit has teeth** (by hand, recorded in the PR, then reverted): remove `_owned.Dispose()` from ONE migrated
   class (`TracePortTests`) ⇒ the executable exits non-zero with the audit's message naming its
   `testsToDelete-bench-fixture-…` folders; restore ⇒ green, none left. Then comment out `PostgresFixture.DisposeAsync`'s
   body ⇒ the audit names the `testsToDelete-bench-pg-…` container; restore. If xUnit v3 does not fail the run on an
   assembly-fixture cleanup exception, or disposes the assembly fixture before the postgres collection: stop and report
   — never turn the audit into a warning silently.
9. **Break-it on the real tree** (by hand, recorded): one `Path.GetTempPath()` added to a test ⇒ red naming
   `file:line`; reverted ⇒ green; `.claude/rules/shared` de-initialised ⇒ red with the submodule sentence.
10. **No leftover after a full run** (by hand, recorded): before S5 (on the post-S4 `main`), a full run's NEW temp-root
    entries are counted by name; after S5, a full run adds zero entries of any old name, zero `testsToDelete-bench-`
    entries, no container labelled with its owner, and `bash .claude/rules/shared/tools/janitor.sh --dry-run`, run from
    this repository, prints no `testsToDelete-bench-` line. A product-named entry that appears (`bench-assess-*`,
    `bench-scratch/<guid>`) is traced to its test: a missing `using`/`Dispose` in the TEST is fixed here
    (`FindingAssessorTests.cs:44`, `CalibImportTests.cs:237`, `ImportRig.cs:68` are the known candidates); a product
    path with no seam is reported to the owner — this story does not change product code.

- **Commands.** As S2, `--filter-class "*LeftoversGuardTests"` (RED, then GREEN), `"*LeftoversAuditTests"`; the whole
  executable alone, Windows and Linux; `dotnet format … --verify-no-changes --no-restore` in Linux or WSL;
  `node .claude/rules/shared/tools/plan-lifecycle.mjs`; `actionlint` passes on the changed `ci.yml` (the `workflows` job).
- **DoD.** Scenario 1 RED with exactly 59 in 38 files, then GREEN; scenario 2's counts recorded; every break-it observed
  red; scenario 8's teeth observed and reverted; scenario 10 recorded; CI green on all jobs WITH the new `build-test`
  step; `module_tests.md` updated; this document's status becomes `> Status: **E6 shipped, <YYYY-MM-DD>; promotes with
  its parent.**` — never *implemented* while it sits in `todo/` — and its `todo/README.md` row says the same.

## Test plan

| The parent's "proven by" | Where |
|---|---|
| A fixture test that starts the container, kills the test process (a child-process harness), and asserts Ryuk removed a container named `testsToDelete-bench-pg-*` | S3 sc. 2 — observed by the container's ID (`--filter id=`), never by a name substring; the name asserted before the kill |
| A per-test-database test that the database is gone once the fixture is disposed | S3 sc. 3 (container gone AND the database's own connection string refused) |
| A worktree test killed before its `finally` whose leaf the rule's sweep then removes | S2 sc. 15 — the kill's end state built by hand (a folder holding a repository and its linked worktree, owner dead), because D4 puts every test worktree inside an owned folder: the sweep removes the folder, the worktree and its registry together. Plus S4 sc. 4: the worktree really is inside |
| RED-GREEN per no-cleanup file | S5 sc. 2 (counts per class) and sc. 8 (the audit's teeth); S4 sc. 1–3 for the leaks the audit table missed (siblings, the fake homes, `fake-coai-data`) |
| The guard's break-it | S5 sc. 3, 4, 5, 9 |
| (contract, not in the parent) the owner label, the start-of-run sweep of folders AND containers, concurrent-owner survival, never failing the run | S2 sc. 14, 16, 17; S3 sc. 1 |

## Growth

- **Folders.** 2 559 `[Fact]`/`[Theory]` attributes in 325 files; 140 construction sites of the shared helpers in 58
  files plus the 59 migrated sites — on the order of 500–1 500 owned folders per full run, each created and removed by its
  own test; at most the in-flight ones exist at once (≤ 8 threads). The largest are the checkout caches and bare
  mirrors of the git fixtures (a few MB each). S5 sc. 10 records the measured count and the peak size from one full run.
  A killed run leaves at most its in-flight folders; the next run's sweep removes them once 10 minutes old with their
  owner gone; the janitor removes them on any machine after an hour.
- **Containers.** One per run (the fixture), plus two short-lived ones in S3's tests and Ryuk; each removed by
  `DisposeAsync`, by Ryuk after a kill, by the next run's container pass after a daemon restart, by the janitor otherwise.
- **Databases.** ~30 per run inside the fixture's container (28 call sites, theory rows multiply some); retired with it.
- Nothing here grows without an owner.

## Definition of Done

- [ ] The pin carries the E1 release; the mounted rule lists `bench` (S1).
- [ ] Every folder, file, worktree and container a test makes is named `testsToDelete-bench-<what>-<id>` by the helper
      (or lives inside a folder that is), claimed, and removed by its test on every path out; a teardown that gives up
      leaves it prefixed and claimed with one warning line (S2, S4, S5).
- [ ] The fixture's container is named and labelled, Ryuk stays on and reaps it after a kill, and the per-test databases
      carry the container-scoped declaration beside `CREATE DATABASE` (S3).
- [ ] The first act of every run sweeps this token's folders and containers on owner liveness and never fails the run;
      the last act fails the run if it left anything it claimed (S2, S5).
- [ ] The guard is red on every class of rule §7 plus `testcontainers`, on an unregistered token and on a scan of
      nothing; CI fetches the rules it reads (S5).
- [ ] A benchmark run names its worktrees exactly as before; the AppHost's Postgres is untouched (S4 sc. 5).
- [ ] `research/module_tests.md` describes all of it; the hand clean-up was listed and done on the owner's word.
- [ ] When the last story merges, this document's status says `E6 shipped, <date>; promotes with its parent`, and the
      parent's E6 entry gets the same sentence. Parent and stories documents promote together, after E7.

## Reuse (the shared `common/reuse-first.md`) — what was found and what is asked

- Reused: `TestRepository.Root()` (guard root), `ProcessRunner.RunAsync` (docker calls), `ScratchWorktree.AddAsync(…,
  underRoot)` (the existing overload behind the seam), the `TempRoot.Sibling` formula (unchanged, now inside the owned
  folder), the two removal lessons (moved into `Removal`, not copied).
- Not reused, on purpose: `WorkerLiveness` (product; NetBIOS host, two-valued — the facts).
- **A badly written neighbour, named and not changed:** the checkout-root loop exists four times
  (`TestRepository.cs:7-18`, `LocalCpuBudgetTests.cs:20-27`, `AppHostLaunchProfileTests.cs:~100-110`,
  `ScoutTreesTests.cs:272`). It should be the one `TestRepository.Root()`. **Should the three copies be redone?** — asked
  in S5's summary, not done.

## Notes for the parent (`dew_flow_conventions · todo/PLAN_test_leftovers_rollout.md`, E6 entry)

| # | The entry says | What is so on `origin/main` 20051c8 | Resolution here |
|---|---|---|---|
| P1 | "73 temp mentions in 48 files under `tests/`, `tools/`, `scripts/`"; "the guard scans `tests/` and `tools/`" | there is no `tools/` folder; `scripts/` makes no temp path; all 73 lines are in `tests/` (71 code lines in 47 files, 2 comments) | the guard's roots are `tests/`, `scripts/`, `.github/`, `http/` (S5) |
| P2 | "~8 files without cleanup" | 5 files, 8 sites, never removed; PLUS leaks the audit did not name: five siblings no rig lists (one of them a campaign key file), six fixed parent folders never removed, `fake-coai-data` (append-only, fixed name), files written straight into the temp root by 10 helpers, a fixed shared name in `ArchitectureTests.cs:347` | S4 (helpers, fakes), S5 (sites) |
| P3 | "worktrees are made by product code — `ScratchWorktree.cs:28` and `GitCheckoutProvider.cs:265` — tests need a seam for the leaf name" | `GitCheckoutProvider` needs no seam: its mirror and worktrees live under the cache root every test already passes. `ScratchWorktree` already has a root overload; its default is reached through `HarvestGates`/`SolutionSignals` | D4: a ROOT seam on those two, product default unchanged and pinned |
| P4 | the proofs | no owner label, no start-of-run sweep, no token check — rule §3, §4, §7 require all three of every repository that creates a resource kind | S2, S3, S5 |
| P5 | (nothing) | `http/README.md:32-33` starts a container by hand with no `--rm`, unprefixed, never stopped — the audit's "0 containers without `--rm`" missed it (markdown) | S5 recipe |
| P6 | (nothing — a contract defect) | rule §6 (b) prescribes `testsToDelete-exempt: container-scoped — …`, but §7's grammar has no class `container-scoped`, and E1's own guard (`tools/leftovers-check.mjs:46`, `EXEMPTIBLE`) reports it as class `exemption` | E1 needs one sentence (§7 accepts `container-scoped` as a declaration without a class, or §6 (b) uses another marker); E6 accepts it as a declaration meanwhile (D2) |
| P7 | (nothing — for E1) | a hand recipe cannot write a valid owner from Git for Windows' bash: `$$` there is the MSYS pid, not the Windows pid the janitor's `tasklist` sees | E6 writes no owner label in the recipe; E1 may want a sentence on shell-written owners |
| P8 | (nothing) | `spikes/policy-ab-quick/test/*.test.mjs`: 16 raw temp sites in 7 Node test files, outside the parent's scope | D8: excluded by name; an owner decision whether they get an epic |
| P9 | "`hosts/AppHost/AppHost.cs` documents its persistent data volume in its header" | it is at `:55-61` (the reason at `:58-60`), not the header — and the AppHost is product, never scanned | no change |
| P10 | the C# helper "from E3's shape" | E3's guard regex for `--name` (`--name[ =]+`) is wider than E1's (`--name` only inside a `docker` command) and would be red on 19 lines of this repository's own CLI tests; E3's top-level-only link removal is narrower than this repository's measured walk | D2; both reported to E3 as notes |
| P11 | E6 depends on E1 | it also depends on E3's S2 (D1) — the dependency table names E3 only for E2 | Order table |
| P12 | (nothing) | the release turns `Pin check` red here until the pin moves; `build-test` does not fetch the submodule the token check reads | S1 first; S5 CI step |
| P13 | (nothing) | product-named temp paths reached by tests (`bench-assess-*`, the solution patch, the scout zip) are outside any guard; three test callers never dispose `FileSystemGateAssessmentFiles` | S5 sc. 10 traces them; product code is not changed |

**Sign-off: yes**, on S1–S5 as written. Two judgements are mine and are recorded as deviations for the promotion: the
worktree seam is a root, not a leaf name (D4), and the guard accepts §6 (b)'s `container-scoped` as a declaration (D2)
until the contract says how.
