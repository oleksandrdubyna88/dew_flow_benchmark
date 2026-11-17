# E6.S3 brief — the fixture's container, the container pass and the marker (rows E6-d, E6-f1, E6-f2; D9)

> Status: **IMPLEMENTED, 2026-11-17 (built on `feat/e6-s3-fixture-e10`, PR #219; merge facts recorded after the merge).** The third hosted CI run on `336e1284` (`build-test`) passed: 5846 tests, 0 failed; on a real daemon the container pass ran, Ryuk removed a SIGKILLed child's container in 12 s and its volumes at once, the disposal proof and the reuse counterexample held, and entry 10.1's configuration pin stayed False. The second run (`2a218dc9`) had failed only in cleanup: `docker volume rm` says "no such volume" in lower case, now matched case-insensitively.
>
> Was: Status: **built on `feat/e6-s3-fixture-e10` 2026-11-16, fixes of 2026-11-17 (progress line: seven commits of § 12 (the six of the series and a
> Status: **built on `feat/e6-s3-fixture-e10` 2026-11-16, fixes of 2026-11-17 (progress line: seven commits of § 12 (the six of the series and a
> `dotnet format` indent fix) were pushed as draft PR #219; the first hosted CI run, `43977483603` (`build-test`), failed 5 of 5838, all in
> `FixtureContainerTests`, while the pass on a real daemon, the reaper children and the gate child passed and Ryuk removed the killed child's
> container in 12 s. The fix round after it: B1 `docker volume ls` has no `--no-trunc` flag (exit 125; names are never truncated), B2 the runner trims
> its output, so the empty reuse-hash value is no longer the last field; the coai code gate's G1 to G7 and the Opus review's R1 to R3 (`ContainerPass` is async,
> `OnceAsync`; removals at most four at a time with a progress line; a timed-out removal is "outcome unknown"; an id that vanished between `ps` and `inspect`
> no longer drops the pass; scenario (7) scans each file whole; cleanup failures are reported). Docker proofs still to be observed green on hosted CI; not promoted).**
> Earlier the same day: **plan only, nothing implemented yet; rewritten to the parent's epoch 10, with the item 8.1
> observations made before its plan gate (§ 11).** The brief of story S3 of `todo/PLAN_test_leftovers_E6.md` (§ 6 S3;
> rows E6-d, E6-f1, E6-f2; D9; claims C2, C7, C9). Its scope is S3's Files line, as named paths (§ 0.1): a file outside
> them would be the parent's next counted change, which after entry 10.1 is the TENTH FULL STOP, and the work STOPS there.
>
> **Plan gate passed and decisions folded in, 2026-11-16 (§ 16).** coai plan gate session `e4eef27b`: `proceed`. The
> coordinator accepted Q1–Q7 as recommended (Q4: the container's own size in the disposal test, no `ci.yml` step) and the
> gate's findings G1 (the anonymous database volume ends with the container), G2 (the effective reaper setting checked
> again immediately before `Build()`; test (7) also reads `ResourceReaperEnabled = false` and the name assembled from split
> literals) and G3 (the once-per-process gate proved by two fixture starts in a controlled process). The configuration-reuse
> result (§ 11, O11) is the parent's entry **10.1** (conventions PR #174, `0ca0825d`): row E6-f2's counterexample "reuse
> enabled through configuration alone" becomes a GREEN PIN; the builder counterexample and the run-time disposal proof stay
> red-first. 10.1 is epoch 10's first change, so the next accepted change is the tenth full stop: nothing beyond the Files
> line.
>
> Judge's brief (Opus) for a Sonnet coder. Written 2026-11-16. Read-only inputs, all verified for this brief: benchmark
> worktree `/home/jinx/git/wt/bench-e6s3`, branch `feat/e6-s3-fixture-e10` from `main` `5070beda` (#217: E6's stories
> amended to the parent's epoch 10; #218: F42-c, the adapted copy re-synced to mcp `53f65130`), mount
> `.agents/conventions` at `b1746499` (F41's release); `Testcontainers` and `Testcontainers.PostgreSql` 4.16.0
> (`4.16.0+df0f931e`, from the build output; XML docs from the NuGet cache). The parent is
> `dew_flow_conventions · todo/PLAN_test_leftovers_rollout.md` at conventions `main` `af607536` (§ *Epoch 10*: rows E6-f1,
> F42-c; items 3.2, 8.1, 8.2, 8.3; the re-review record's two rows for this brief). "Stories" is
> `todo/PLAN_test_leftovers_E6.md`; every `file:line` is at `5070beda`. **[M]** is measured for this brief, read-only;
> **[O]** a claim OBSERVED for this brief in a scratch clone, with the six things item 8.1 asks for (§ 11); **[R]** read
> from code and NOT run (never observed; written open-ended, "red or not, the run says which"); **[D]** taken from a named
> document.
>
> *Material, not carried as written:* the local brief of 2026-11-12 (`wt/bench-e6-s3`, untracked, at `84d4953e`). Its
> Testcontainers reading and its C5 (no guard reads `.WithReuse(`) are carried and re-observed here (§ 11, O9, O11); its
> parts the parent's re-review record cut are not: the mount at `4c9a684`; the marker written beside an `exemption`
> finding taken as expected ("209 → 210"); no E6-f1 file-scoped check, no E6-f2 run-time reuse proof, no C7 C# half; and
> its `StartOfRunSweep` wiring and `ci.yml` changes, which D9 (the plan gate `215fc3a2`) and S3's Files line replace.

## 0. What you build, in one paragraph

`PostgresFixture` refuses to start when the Testcontainers reaper (Ryuk) is switched off in its environment, gives its
container the owner-form name `testsToDelete-<owner>-bench-pg-<id>` from the helper and the owner label
`testsToDelete.owner=<record>`, never sets reuse, and builds and starts its container only inside `InitializeAsync`, after
the container pass. A new benchmark-own `ContainerPass` runs ONCE per process, inside `PostgresFixture.InitializeAsync`
before the container exists (D9): it lists the containers that carry the owner label, judges each by the two keys of rule
§4, and removes a dead owner's container of this token, older than 10 minutes, by its captured id; it never fails the run.
The `container-scoped` marker goes on the line above `NewEmptyDatabaseAsync` (`PostgresFixture.cs:96`), and both guards
accept it at these pins (§ 11, O2–O4). You add the pass's tests (a fake `docker` script), a source test that no `.cs` turns
the reaper off, and the Docker proofs that only CI can run (they skip locally and FAIL under GitHub Actions without a
daemon). You update `research/module_tests.md`. ONE pull request, in this repository only.

## 0.1 Scope — S3's Files line as named paths, each with the dependency that needs it

The stories' S3 *Files* (`:461-466`): `PostgresFixture.cs`, a benchmark-own `ContainerPass.cs` in
`tests/Bench.Tests/Leftovers/` on the copy check's own list, its tests, `research/module_tests.md`, and
`TheAdaptedCopyTests.cs`'s `BenchOwn`, where `ContainerPass.cs` "and each new test file of the folder" are added. So:

| Path | Needed for | Bound |
|---|---|---|
| `tests/Bench.Tests/Infrastructure/PostgresFixture.cs` | E6-d (name, label, reaper check), E6-f1 (the marker), D9 (the pass before the container) | § 4 |
| `tests/Bench.Tests/Leftovers/ContainerPass.cs` (NEW, benchmark-own) | D9; the label key built from `Owned.Prefix`, which only the helper's folder may name (§ 3 K2) | § 5 |
| `tests/Bench.Tests/Leftovers/ContainerPassTests.cs` (NEW) | scenarios (2) and (6) with the fake `docker` | § 6 |
| `tests/Bench.Tests/Leftovers/FixtureContainerTests.cs` (NEW) | scenarios (1), (2) on a real daemon, (3), (4); the reaper check's child test | § 6 |
| `tests/Bench.Tests/Leftovers/ContainerRulesTests.cs` (NEW) | scenario (7) | § 6 |
| `tests/Bench.Tests/Leftovers/TheAdaptedCopyTests.cs` | claim C2: the four new files on `BenchOwn` (`:32-46`), and the count in its summary (`:28`) | those lines only |
| `research/module_tests.md` | the section `## Test leftovers — …` (`:632-664`) | § 8 |
| the plan records: this brief, its `todo/README.md` row, its promotion (`research/…`, `research/README.md`), the stories' progress lines | `common/planning-docs.md` | Q2 |

**Not touched, by name:** `.github/workflows/ci.yml` (the Docker proofs need no variable there, § 3 K9; § 8 of the stories'
size reading is taken per container, Q4), `StartOfRunSweep.cs` (D9 puts the pass in the fixture), every manifest target
and `template-manifest.json`, `.gitattributes`, `src/`, `hosts/`. **A file the build, a test or a check needs outside the
table above would be the parent's next counted change, the TENTH FULL STOP (entry 10.1 is taken, § 16): STOP, write the finding into this brief, and hand it to the coordinator;
do not add the file.**

## 1. Rules you must read first

- `.agents/PROJECT.md` (commands; never `dotnet test`; exe + argv with a timeout, § 11; warnings are errors).
- `.agents/conventions/common/test-leftovers.md`, its detail (§3 the record and "a daemon object's label is its only
  evidence", §4 two keys / age selects / removal by captured id / never fails the run, §5 Testcontainers keeps the reaper
  on, §6 (b), §7) and `common/test-leftovers-exemptions.md` (the `container-scoped` kind).
- The stories: rows E6-d (`:68`), E6-f1 (`:70`), E6-f2 (`:71`); *The claims* C2, C7, C9 (`:131`, `:136`, `:138`); D9
  (`:272-285`); § 5 *Epoch 10* (`:320-323`); § 6 S3 (`:425-468`); § 7 (`:544-563`); § 8 (`:565-575`).
- The parent § *Epoch 10*: item 8.1 (the six things; a plant must be able to fail), item 8.2 (E6.S3's row), item 8.3.
- The S2 brief `research/PLAN_test_leftovers_E6_S2_brief.md` § 4.3 (benchmark-own files), § 6 (the gate's shape), § 14
  (commands); the F42-c brief `research/PLAN_test_leftovers_E6_F42c_brief.md` § 10.1 (how an observation is written).
- No Python anywhere (scripts in Node or bash). No Docker on this machine. No `git stash`. Commit only; the coordinator
  pushes and opens the PR.

## 2. Preconditions and merge conditions

1. S2 merged (#207), F41-b merged (#216, mount `b1746499`), F42-c merged (#218, the copy at mcp `53f65130`): met, `main`
   `5070beda` **[M]** (`git log --oneline -3`; `TheAdaptedCopyTests` pins `53f651309a27…`, `TheAdaptedCopyTests.cs:79`).
2. Every claim this brief relies on is observed before its plan gate (the parent's item 8.1): § 11. The claims C2, C7, C9
   of the stories are observed there; the stories' text for them stays as it is (it is written open-ended).
3. At most three open PRs here, or the owner's allowance for E6 PRs over the cap (2026-11-16, as F42-c's brief § 2 item 3).
4. CI checks of the head SHA exist and are green (`build-test`, `contract`, `plans`, `workflows`), with no Docker proof
   skipped (§ 6, `DockerGate`); the coai code gate and the Opus review are done; every counterexample of (3), (5) and (7)
   seen red once and quoted on the PR (the stories' Done-when).

## 3. Decisions of this brief (each with its reason)

- **K1 — The name comes from the helper: `Owned.NameFor("pg")`** (`Owned.cs:126-130`): `testsToDelete-<owner>-bench-pg-<6
  of [A-Za-z0-9]>`, at most 63 characters (`CheckWhat`), a valid Docker name (starts with a letter; letters, digits, `-`).
  Random per fixture, so two suites on one daemon get two names. Both guards read `.WithName(Owned.NameFor("pg"))` in
  the fixture as clean (§ 11, O6), and a literal `.WithName("bench-pg")` there as `docker-name` (the control, O6).
- **K2 — The label: key `ContainerPass.LabelKey => Owned.Prefix[..^1] + ".owner"`, value `Owner.RunRecord().Format()`.**
  The literal `"testsToDelete.owner"` in a `.cs` is `owner-blind` in the copied C# guard (§ 11, O7: `testsToDelete.ow`;
  the Node guard reads it as clean), and `owner-blind` takes no exemption. `Owned.Prefix` may be named only in the
  helper's folder (`HelperReference`, `LeftoversGuard.cs:131`), so the key lives in `Leftovers/ContainerPass.cs` and the
  fixture uses `ContainerPass.LabelKey` (O6: clean in both). The record is the same one a folder claim carries
  (`Owner.cs:31`); its pid is the test process (or a live run named by `TESTSTODELETE_RUN`), whose life bounds the container.
- **K3 — The EFFECTIVE reaper setting is checked at fixture start, first thing in the constructor** (carried from the
  stories' plan gate `215fc3a2`). `TestcontainersSettings.ResourceReaperEnabled` is the value Testcontainers itself acts
  on: observed `True` with nothing set, `False` with `TESTCONTAINERS_RYUK_DISABLED=true`, `False` with `ryuk.disabled=true`
  in `~/.testcontainers.properties`, and readable with no Docker at all (§ 11, O10). It is "Gets or sets" (its XML doc
  **[D]**), so code that switches it off is read the same way. When it is `false` the constructor throws
  `InvalidOperationException` before anything else, with the text of § 4 step 2. **And again immediately before the
  container is built** (G2, § 16): in `InitializeAsync`, after `ContainerPass.Once()` and before `Builder().Build()`,
  outside the `try` (so the refusal is never reworded as "needs Docker"): a setting switched off between construction and
  start (code, or a static initialiser that ran late) is refused there too. Not read by this check: a builder's
  `.WithCleanUp(false)` (per container, **[R]**); for the fixture's own builder, scenario (1) is the run-time proof (Q7).
- **K4 — `Build()` moves into `InitializeAsync`, inside the `try`, after the pass.** Observed: `PostgreSqlBuilder.Build()`
  throws `DockerUnavailableException` when no daemon answers (§ 11, O12). Today the build is a field initializer
  (`:27-33`), so on a machine without Docker the fixture's construction throws Testcontainers' message, the loud text of
  `:45-47` is never seen, and a pass placed in `InitializeAsync` would never run. Moved, scenario (6) holds as the stories
  write it: the pass says its one line, then the fixture fails with its own text (the Testcontainers exception inner).
- **K5 — Reuse is never set; Testcontainers 4.16.0 has NO configuration path that turns it on.** Settled by a run (the
  carried input of the plan gate `215fc3a2`): with `TESTCONTAINERS_REUSE_ENABLE=true`, and with
  `testcontainers.reuse.enable=true` in the properties file, the fixture's builder reads `Reuse = False`; only
  `.WithReuse(true)` makes it `True`. The configuration interface (`ICustomConfiguration`) has no reuse getter, and the only
  members of the assembly that name reuse are `WithReuse`, the configuration's `Reuse`/`ReuseHashProvider`/`GetReuseHash`
  and the reuse-hash label constant (§ 11, O11). So row E6-f2's "RED through configuration alone" has **no instance at
  4.16.0**. By the parent's entry **10.1** (conventions PR #174) that half is a GREEN PIN (§ 6 (3b)): both configuration
  spellings set, the built container's `Reuse` stays `False` and the container still ends with the fixture. The builder
  half stays RED-first by a committed counterexample test (§ 6 (3), Q3), and so does the run-time disposal proof (4). A
  dependency bump re-runs O11, and the pin then says whether a configuration path appeared.
- **K6 — The pass runs where D9 puts it:** `ContainerPass.Once()` is the FIRST statement of `PostgresFixture.InitializeAsync`,
  a once-per-process gate in `ContainerPass.cs` (a `Lazy<SweepResult>` with `ExecutionAndPublication`, whose factory catches
  everything, as `StartOfRunSweep.Gate` does, `StartOfRunSweep.cs:30-38`: a cached exception would fail every later
  fixture). Every later fixture in the process awaits the same result. Not in `StartOfRunSweep` (D9: xUnit initialises a
  collection fixture before any `Before`; and that file is outside § 0.1).
- **K7 — The pass reuses, it does not re-implement:** `Owner.Parse` (`Owner.cs:56`), `Owned.Pattern` and `Owned.Classify`
  (`Owned.cs:86`, `:103`), `Liveness.State` with `Facts(NameOwner: …)` and `Liveness.ProbeProcess` (`Liveness.cs:69`,
  `:36`, `:24`), `LeftoversSweep.MinAge` (`LeftoversSweep.cs:81`), `SweepResult` (`:56-66`), and `ProcessRunner.RunAsync`
  (`src/Bench.Infrastructure/Process/ProcessRunner.cs:69-75`): exe + argv, a timeout, the process tree killed on the
  timeout, a missing executable answered as `ProcessAttempt.NotFound` (`:40`). The docker CLI shape follows the janitor
  (`.agents/conventions/tools/janitor.sh`: the label read through `%q`, an id of lower-case hex, `docker rm -f -v <id>`);
  the listing follows D9: `ps` by label, then `inspect` in batches of at most 100.
- **K8 — The fake `docker` is a Node script started by its ABSOLUTE path**, never found on `PATH`: .NET resolves an
  executable against the PARENT's `PATH`, and the suite runs on up to 8 threads, so changing `PATH` in-process would change
  it for every other test. "No docker" is the same code path (`NotFound`): a test passes a path that does not exist; the
  real form is the local whole-suite run on this machine, which has no `docker` (`which docker` → nothing **[M]**).
- **K9 — Docker proofs skip locally and FAIL under GitHub Actions**, with no `ci.yml` change: `DockerGate.Decide(reason,
  githubActions)` → run when a daemon answered; `Fail` when none did and `GITHUB_ACTIONS` is `"true"` (Actions sets it on
  every job; the repository's precedent is `DarwinLayoutTests.cs:29`); else `Skip` with the reason. Each Docker proof
  starts its own `PostgresFixture` outside the `"postgres"` collection (a collection fixture that throws fails its tests
  and cannot skip them), in a collection of its own so they run one at a time.
- **K10 — The marker**, on its own line directly above `public async Task<string> NewEmptyDatabaseAsync(string name)`
  (`:96`), four spaces, the em dash U+2014: `// testsToDelete-exempt: container-scoped — the per-test databases live and die
  with the PostgresFixture container`. Both guards: clean; without `live and die with`, one `exemption` at that line (§ 11,
  O2–O4). Under F41 it clears nothing there: the fixture drops no database (the stories' S3 *What*).
- **K11 — Scenario (7)'s source test reads every `.cs` of the repository** through the copied walk
  (`GuardFiles.Walk(RepositoryRoot.Path)`, `LeftoversGuardFiles.cs:49`, which skips `bin`, `obj`, `node_modules`, `.git`,
  `.claude`, `.agents`, `.http-artifacts`, `logs`) filtered to `.cs`, so `hosts/`, `tools/` and `spikes/` are read too, not
  only the guard's `tests/` and `src/`. Per line, after joining string literals that are concatenated with `+` (the text
  between a literal's closing quote, a `+` and the next literal's opening quote, `@` and `$` prefixes included, is removed
  before the search), it is red on two forms (G2, § 16): **(a)** the variable named, case-insensitively, whole or so
  assembled from split literals; **(b)** an assignment `TestcontainersSettings.ResourceReaperEnabled = false` (an `=`
  that is not `==`, any whitespace around it, the `TestcontainersSettings.` qualifier optional). The manifest's
  targets are skipped, read from `template-manifest.json` (never a typed list): five lines of the copied guard's own test
  data name the variable today (`LeftoversGuardScenarios.cs:78`, `LeftoversGuardTests.cs:92-93`, `:112-113` **[M]**), and
  the copied guard itself assembles it from split literals (`LeftoversGuard.cs:93`, `"TESTCONTAINERS_RYUK_" + @"DISABLED…"`
  **[M]**); `TheAdaptedCopyTests` holds those files to the template's bytes. So benchmark's own files never assemble the
  name from `+`-joined literals: the test's own pieces and `ContainerPass`'s message (§ 4 step 2) take it from ONE
  constant built without a `+` between literals (`string.Concat("TESTCONTAINERS", "_RYUK", "_DISABLED")`), and the
  planted lines of the theory are built the same way. Its limits, written in the test's summary and in *The limits*: a
  name assembled from identifiers or through `string.Concat`, and a setter reached by reflection, are not read. Both guards
  read a file holding the name built from pieces as clean (§ 11, O8).
- **K12 — The pass's REDs are mutations by hand, not committed** (the S2 brief's B7 shape): before the pass exists its tests
  do not compile, and a compile error is not a RED for the right reason. The RED of each scenario is quoted on the PR.

## 4. `PostgresFixture.cs` — the exact change

1. `using Bench.Tests.Leftovers;` and `using DotNet.Testcontainers.Configurations;` with the other usings (`:1-5`).
2. A constructor, its FIRST statement the reaper check (K3), then the name:

```csharp
    public PostgresFixture() : this(static builder => builder) { }

    /// <summary>The test seam of scenario (3) only: <paramref name="extra"/> is applied after the fixture's own lines. Production code never passes one.</summary>
    internal PostgresFixture(Func<PostgreSqlBuilder, PostgreSqlBuilder> extra)
    {
        // Rule §5: the reaper stays on. This reads the setting Testcontainers acts on — the environment variable, ~/.testcontainers.properties
        // or code — so a fixture whose container nothing would collect after a kill never starts (E6 S3, K3).
        ContainerPass.RequireReaper(TestcontainersSettings.ResourceReaperEnabled);
        ContainerName = Owned.NameFor("pg");
        _extra = extra;
    }
```

   `ContainerPass.RequireReaper(bool enabled)` throws `InvalidOperationException` when `enabled` is false, with exactly:
   `The Testcontainers reaper (Ryuk) is switched off here (TESTCONTAINERS_RYUK_DISABLED=true in the environment, or
   ryuk.disabled=true in ~/.testcontainers.properties, or code): rule §5 keeps it on, so this fixture refuses to start a
   container that nothing would collect after a kill. Unset it and re-run.` The variable comes from K11's one constant
   (`ContainerPass.RyukVariable`, built with `string.Concat`, never `+` between literals), so neither the guards' `ryuk-off`
   (which takes no exemption) nor test (7) reads the message as a setting.
3. The builder moves into a method; the fields become the name, the extra and the started container:

```csharp
    private readonly Func<PostgreSqlBuilder, PostgreSqlBuilder> _extra;
    private PostgreSqlContainer? _container;

    /// <summary>The container's name, <c>testsToDelete-&lt;owner&gt;-bench-pg-&lt;id&gt;</c>, known before it starts (rule §1).</summary>
    internal string ContainerName { get; }

    /// <summary>The started container's id: every Docker proof checks and removes by it (rule §4).</summary>
    internal string ContainerId => Started.Id;

    public string ConnectionString => Started.GetConnectionString();

    private PostgreSqlContainer Started => _container ?? throw new InvalidOperationException("the fixture has not started its container");

    /// <summary>Internal for scenario (3b): its pin reads the builder's configured reuse (by reflection, as § 11 O11 did).</summary>
    internal PostgreSqlBuilder Builder() => _extra(new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("bench")
        .WithUsername("bench")
        .WithPassword("bench")
        // Appended to the image's own `postgres` command — spelling `postgres` here again made the server refuse its own name.
        .WithCommand("-c", "max_connections=400")
        // Rule §1 and §3: the owner-form name and the owner label the container pass reads (E6 S3, D9). Reuse is never set and the reaper stays on.
        .WithName(ContainerName)
        .WithLabel(ContainerPass.LabelKey, Owner.RunRecord().Format()));
```

   Keep the `<summary>` of `:21-26` (it explains `max_connections`) on `Builder()`, and the comment of `:31` word for word.
4. `InitializeAsync` (`:37`): its first statement `ContainerPass.Once();` (K6); then, outside the `try`,
   `ContainerPass.RequireReaper(TestcontainersSettings.ResourceReaperEnabled);` again (K3, G2: immediately before the
   build); then inside the existing `try`: `_container = Builder().Build(); await _container.StartAsync();` (K4). The
   catch's text (`:45-47`) does not change.
5. `DisposeAsync` (`:56`): `if (_container is not null) await _container.DisposeAsync();` — a fixture that never started
   disposes nothing.
6. The marker of K10 above `:96`. Nothing else changes: `NewContext`, the stores, `NewDatabaseAsync`, the
   `NewEmptyDatabaseAsync` body, `PostgresCollection`, `TestWorkers`, `TestClock`.

## 5. `ContainerPass.cs` (benchmark-own, namespace `Bench.Tests.Leftovers`)

Header: "benchmark's own file: it is not a manifest target"; the docker shape after the janitor. About 220 lines; cyclomatic
complexity at most 4 per method (switch expressions), as the copied code.

```csharp
public sealed record ContainerPassOptions(string Docker, DateTimeOffset Now, TimeSpan MinAge, Func<long, Probe> Probe, TimeSpan CallTimeout,
    Func<string, bool>? Consider = null);   // Consider: a TEST seam, which captured ids may be judged; the real run never sets it

public static class ContainerPass
{
    public static string LabelKey => Owned.Prefix[..^1] + ".owner";        // K2: never one literal
    public static readonly TimeSpan CallTimeout = TimeSpan.FromSeconds(30);  // D9
    public const int MaxBatch = 100;                                       // D9
    public static ContainerPassOptions Real(DateTimeOffset now) => new("docker", now, LeftoversSweep.MinAge, Liveness.ProbeProcess, CallTimeout);
    public static SweepResult Once();                  // K6: the gate; runs Run(Real(now)) once per process, prints the report, never throws
    public static int Runs { get; }                    // how many times the real pass ran in this process (1 after any fixture started)
    public static SweepResult Run(ContainerPassOptions options);   // never throws: every failure is one line of the result
    public static void RequireReaper(bool enabled);    // K3
    public static string RyukVariable { get; } = string.Concat("TESTCONTAINERS", "_RYUK", "_DISABLED");   // K11: the one constant; no `+` between literals
    internal sealed class Gate(Func<SweepResult> pass) { public SweepResult Await(); public int Runs { get; } }   // K6's gate as a seam: the real one is new Gate(() => Run(Real(now))); G3's unit test passes a counting pass
    internal static IReadOnlyList<string> ListArgs => ["ps", "-a", "-q", "--no-trunc", "--filter", "label=" + LabelKey];
    internal static IReadOnlyList<string> InspectArgs(IEnumerable<string> ids) => ["inspect", "--type", "container", "--format", Template, .. ids];
    internal static IReadOnlyList<string> RemoveArgs(string id) => ["rm", "-f", "-v", id];
    internal static ContainerVerdict Judge(ContainerRow row, ContainerPassOptions options);   // § 5.2
}
```

- `Template` = `{{.Id}}` TAB `{{printf "%q" .Name}}` TAB `{{printf "%q" (index .Config.Labels "<LabelKey>")}}` TAB
  `{{.Created}}`, built from `LabelKey`, a real tab (`\t`) between the fields (an argv carries it as it is).
- `ContainerRow.Parse(string line) → ContainerRow?` (null = unreadable): four tab-separated fields; the id
  `^[0-9a-f]{64}\z`; the name and the label unquoted only when they are `"…"` with no backslash inside (else the name is
  unreadable → a `notice:` line; the label → null); one leading `/` taken off the name; `Created` read to whole seconds UTC
  from `^(\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2})(\.\d+)?Z\z`, else null.
- `ContainerVerdict`: a closed record hierarchy (private constructor, nested sealed records): `Remove(row)`,
  `Keep(row, reason)`, `List(line)`.
- Every docker call: `Task.Run(() => ProcessRunner.RunAsync(options.Docker, args, AppContext.BaseDirectory,
  options.CallTimeout, CancellationToken.None)).GetAwaiter().GetResult()` (`Task.Run` keeps the wait off the test framework's
  context). `Once()` writes the report to `Console.Error` in the folder sweep's shape (`LeftoversSweepFixture.cs:47-52`):
  a header `bench tests: containers — removed <n>, preserved <n>, listed <n>`, then `  <listed line>`,
  `  preserved: <what> — <reason>`, `  error: <line>`; nothing when the result is empty.

### 5.1 The order of one pass

1. **List** `ListArgs`. Never `--filter name=`. A line that is not 64 lower-case hex is one `notice:` line, never used. No id
   → `SweepResult.Empty`, no `inspect`.
2. **Read** `InspectArgs` for the ids in `Chunk(MaxBatch)` batches, only when step 1 listed any. A line that does not parse →
   one `notice:`; an id with no row → one `notice:`.
3. **Judge** each row (§ 5.2); `Consider`, when set, drops the other ids silently first.
4. **Remove** each `Remove` row by `RemoveArgs` with its full captured id, one call per id.

### 5.2 The decision per container (`Judge`), in this order

| # | The row | Verdict | Line or reason (exact) |
|---|---|---|---|
| 1 | name not readable | List | `notice: container <id12> — its name is not readable, ignored` |
| 2 | `Owned.Classify(name)` is not the owner form nor legacy | List | `foreign: container <name> (<id12>) — not a bench owner-form name` |
| 3 | legacy form | List | `legacy: container <name> (<id12>) — the old form is listed, never collected; after checking: docker rm -f -v <id>` |
| 4 | owner form, but `Owned.Pattern().IsMatch(name)` is false (another owner or token) | List | `foreign: container <name> (<id12>) — another owner's or another repository's` |
| 5 | `Created` is null | Keep | `unknown age` |
| 6 | `Now - Created < MinAge` | Keep | `young` |
| 7 | `Owner.Parse(label)` is null | Keep | `unreadable record` |
| 8 | `Liveness.State(record, Probe, new Facts(NameOwner: <the name's owner>))` is `Live` | Keep | `live` |
| 9 | the same is `Unknown` | Keep | `unknown` |
| 10 | the same is `Gone` | Remove | — |

`<id12>` is the first 12 characters. `KernelMine` stays null: a daemon object has no kernel owner, its label is its only
evidence (rule §3). Kept → `Preserved` `($"container {name} ({id12})", reason)`; removed → `Removed`; listed → `Listed`.

### 5.3 What never fails the run (each is ONE line, and the pass returns)

| Event | Line | Removed |
|---|---|---|
| `NotFound` on any call | `Listed`: `notice: containers not swept — <attempt description>` (e.g. `'docker' is not on PATH`) | nothing |
| `ps` exits non-zero | `Errors`: `containers: docker ps failed (exit <n>): <first stderr line>` | nothing |
| a call times out (30 s) | `Errors`: `containers: docker <verb> timed out after 30s — nothing removed` | nothing after it |
| an `inspect` batch exits non-zero | `Errors`: `containers: docker inspect failed (exit <n>): <first stderr line> — nothing removed` | nothing in this pass (Q6) |
| one `rm` exits non-zero | `Errors`: `containers: docker rm <id12> failed (exit <n>): <first stderr line>`; the next row goes on | that one stays |
| any exception | `Errors`: `containers: failed: <first line of the message>` (a catch-all in `Run` and in the gate) | what was done |

A stderr text is cut to its first line and its control characters become `?`. The timeout's tree kill is `ProcessRunner`'s own.

## 6. The scenarios — RED, GREEN, RED again (the stories' (1)–(7))

Every Docker query is a call that must SUCCEED to count: `docker ps -a -q --no-trunc --filter id=<id>`, exit 0, empty
when nothing is found (the stories' S3 *Scenarios*). A failed query or a timeout fails the test, never passes as
"absent". "Local" is this WSL machine (no Docker) and CI; "CI" is where a daemon answers (`ubuntu-latest`). "RED again"
is the defect put back once after GREEN, by hand, and quoted on the PR (the stories' § 7).

| # | Test (file) | Where | GREEN asserts | RED (first) / RED again |
|---|---|---|---|---|
| (1) E6-d (1) | `Ryuk_removes_the_container_and_its_volumes_of_a_child_test_process_killed_with_SIGKILL` (`FixtureContainerTests`) | CI; skips on Windows first | a child (`SuiteChild.StartInfoFor(Environment.ProcessPath, ["--filter-method", "*RyukChild_starts_a_fixture_and_waits_to_be_killed"], env)` with `BENCH_E6_RYUK_CHILD_OUT=<TempDir>/child.txt`; both streams drained) writes `<id>\n<name>\n` (temp file then `File.Move`); the query finds the id (exit 0, one line); **the container's volumes captured before the kill** (G1): `docker inspect --type container --format "{{range .Mounts}}{{if eq .Type \"volume\"}}{{.Name}}{{println}}{{end}}{{end}}" <id>`, exit 0, at least one name (the image declares its data directory a volume; zero names fails the test as a fixture that no longer proves this); `child.Kill()` (SIGKILL), exit 137; the container query every 2 s up to 120 s succeeds and finds nothing; then `docker volume ls -q` (exit 0) every 2 s up to 120 s until none of the captured names is listed; a warning names the seconds of each. A volume still listed at the deadline FAILS the test, naming it: no silent retention. `finally`: kill the child's tree; `docker rm -f -v <id>` if it still exists; `docker volume rm <name>` for each captured name still listed | a characterisation: green on the defaults, or red naming the survivor (then a finding for the coordinator: a fix inside `PostgresFixture.cs`, such as the data directory on a tmpfs mount so no volume exists, stays within the Files line). Its counterexample is the reaper check's child tests below, which run locally |
| (1) K3, G2 | `A_fixture_refuses_to_start_when_the_environment_switches_the_reaper_off` and `A_fixture_refuses_to_build_when_the_reaper_is_switched_off_after_construction` (`FixtureContainerTests`) | local and CI | (i) a child of this executable with the Ryuk variable set to `true` (its name from `ContainerPass.RyukVariable`, K11) and `BENCH_E6_REAPER_CHILD=construct` runs `*ReaperChild` (which returns at once when that variable is unset, so it is green in every normal run): exit non-zero, its output holds `The Testcontainers reaper (Ryuk) is switched off here`; the same child with the Ryuk variable removed (`null`) passes the constructor and stops there. (ii) `BENCH_E6_REAPER_CHILD=initialize`, the Ryuk variable removed: the child constructs a fixture, then switches the setting off through its setter by reflection (`typeof(TestcontainersSettings).GetProperty(nameof(TestcontainersSettings.ResourceReaperEnabled))!.SetValue(null, false)`, never an assignment that (7) would read), then calls `InitializeAsync`: exit non-zero with the same message and NOT the `:45-47` text, so the refusal came before `Build()` (locally `Build()` would throw `DockerUnavailableException`, O12); in a child, so the parent's setting is untouched | RED first: before `RequireReaper` exists the child of (i) gets past the constructor, and the child of (ii) fails with the `:45-47` text instead (each assertion on the message red). RED again: comment out each call in turn → its child red. Also a pure theory over `RequireReaper(true/false)` |
| (2) E6-d (2) | `ThePass_removes_only_a_dead_owners_old_container_by_its_captured_id` (`ContainerPassTests`, fake `docker`, § 7) | local and CI; skip on Windows (shebang) | the fake lists seven ids: dead+old ours, live, another owner's name, another token's name, young, empty label, legacy. Calls in order: one `ps` whose argv equals `ListArgs`; ONE `inspect` with the seven ids; ONE `rm` whose argv is `["rm","-f","-v",<the dead id>]`; no call holds `name=`. `Removed` = that one; `Preserved` `live`, `young`, `unreadable record`; `Listed` two `foreign:` and one `legacy:` | by hand: remove on name and age alone (rows 8–9 return `Remove`) → the live row red; drop row 6 → the young row red; remove by name → the argv assertion red; drop `-v` → red |
| (2) | `TheJudge_gives_each_container_its_verdict` (theory, one row per line of § 5.2) and `TheRow_is_read_only_from_a_well_formed_inspect_line` (`ContainerPassTests`) | local and CI, every OS | each verdict and its exact text; dead = `Owner.This with { Pid = UnmatchablePid.Next() }` (premise `UnmatchablePid.AssertNoSuchProcess`); the parser's bad shapes (3 fields, a 63-char or upper-case id, an escaped name, an empty label, a `Created` without `Z`; nine fraction digits parse) | by hand, as the row above; make `Parse` accept upper case → red |
| (2) | `ThePass_inspects_in_batches_of_at_most_a_hundred`, `ThePass_calls_nothing_but_ps_when_ps_lists_nothing`, `ThePass_fails_softly` (theory over § 5.3) | local and CI; skip on Windows | 101 young ids → two `inspect` calls (100, 1), no `rm`; an empty list → one call; each § 5.3 row its one line, `Run` returns (the timeout row with `CallTimeout` 2 s returns within 15 s) | batch 1000 → red; always inspect → red; remove a catch → the row throws |
| (2) on a daemon | `TheFixturesContainer_has_the_owner_form_name_and_the_runs_record_as_its_label` and `ThePass_on_a_real_daemon_removes_a_dead_owners_container_by_its_id` (`FixtureContainerTests`) | CI | the started fixture's container: `docker inspect --type container --format <name>TAB<label>TAB<reuse-hash label> <id>`: name (one `/` off) = `ContainerName` and matches `Owned.FullName()`; label = `Owner.RunRecord().Format()`; the `org.testcontainers.reuse-hash` label empty (an ADDED read, never instead of (3)). The pass on three containers `docker create`d with `"--name", name` argv (`Owned.NameFor("pass-dead")` with a dead record, `pass-live` with this record, `pass-bare` with no label), `Real(now) with { MinAge = TimeSpan.Zero, Consider = planted.Contains }`: the dead one removed and the query finds nothing; the live one kept `live`; the bare one never listed. `finally`: `docker rm -f -v` each remaining id | RED first, in CI on a commit with the tests and NOT the fixture's lines (Q3): name and label red. RED again by hand only for the fake (the row above) |
| (3) E6-f2 (3), builder | `A_reused_container_outlives_disposal_so_the_disposal_check_catches_reuse` (`FixtureContainerTests`) | CI | the counterexample, committed: `new PostgresFixture(b => b.WithReuse(true))`, started, id and volume names captured, disposed; the disposal check of (4) reports the container still there (the test asserts the check's RED). `finally`: `docker rm -f -v <id>` and `docker volume rm` of each captured volume still listed. Expected **[D]/[R]**: "Enabling reuse will disable the resource reaper" (`WithReuse` XML doc) and a reused container is not removed by disposal; red or not, CI says which, and if disposal DOES remove a reused container, (4) cannot see reuse and this is a finding for the coordinator before merge | RED-first, as entry 10.1 keeps it |
| (3b) E6-f2 (3), configuration: a GREEN PIN (the parent's entry 10.1) | `Reuse_set_through_configuration_alone_stays_off_and_the_container_still_ends_with_the_fixture` (`FixtureContainerTests`) | local (the configuration read) and CI (the container) | a child of this executable with BOTH spellings set: `TESTCONTAINERS_REUSE_ENABLE=true`, and `HOME=<TempDir>` holding `.testcontainers.properties` with `testcontainers.reuse.enable=true` (the fixture's source unchanged); `BENCH_E6_REUSE_CHILD=1` runs `*ReuseChild`: it reads `new PostgresFixture().Builder()`'s configured `Reuse` by reflection (the non-public `DockerResourceConfiguration`, as O11 did) and prints `reuse=False`; where a daemon answers it then starts the fixture, prints the id, the volume names and the empty `org.testcontainers.reuse-hash` label, disposes it, and the parent's query finds neither the container nor a volume. The parent asserts `reuse=False` (and, in CI, the rest) | none: a pin, green by design (entry 10.1: no configuration path turns reuse on at 4.16.0, O11). If a dependency bump adds one, this pin goes red, which is the point |
| (4) E6-f2 (1), (2), G1 | `TheDisposedFixturesContainer_is_gone_by_its_id_and_with_it_its_databases_and_its_volumes` (`FixtureContainerTests`) | CI | start; `NewEmptyDatabaseAsync("e6s3_" + 12 hex)`; the query finds the id; the volume names captured as in (1) (exit 0, at least one); one warning with the container's size (`docker ps -a -s --no-trunc --filter id=<id> --format {{.Size}}`, Q4); `DisposeAsync`; the container query succeeds and finds nothing; `docker volume ls -q` (exit 0) lists none of the captured names (polled every 2 s up to 30 s; a survivor at the deadline FAILS the test, naming it) | RED: the (3) counterexample above is this check red. RED again by hand: skip `DisposeAsync` → red (then `docker rm -f -v` by id and `docker volume rm` in `finally`) |
| (5) E6-f1 | the marker (`PostgresFixture.cs`), read by both guards by hand (§ 9) | local | no finding in `PostgresFixture.cs` from either guard; the Node guard's repository count equals the base's (209 with `--profile node`, 3 without, **[M]** at `5070beda`) | RED: the same file with `live and die with` removed → one `exemption` at the marker's line in each guard (observed before the gate, § 11, O3–O4; again on the head and quoted) |
| (6) | `ThePass_is_one_notice_line_when_there_is_no_docker` (`ContainerPassTests`) and the whole local suite | local | `Docker` = a path in a fresh `TempDir` that does not exist → exactly one `Listed` line starting `notice: containers not swept — `, no `Errors`, nothing thrown. The whole suite on this machine: stderr holds `bench tests: containers — removed 0, preserved 0, listed 1` and `  notice: containers not swept — 'docker' is not on PATH`, then the `"postgres"` classes fail with the fixture's own `:45-47` text (K4) | make `NotFound` throw → red. The whole suite's FAILED list compared with the base's (S2 brief § 14): no new failure but the message change K4 describes, quoted |
| (7) E6-d / F43, G2 | `No_cs_file_switches_the_reaper_off` and `The_reaper_check_finds_each_planted_form` (`ContainerRulesTests`) | local and CI | K11's walk and both forms: no line outside the manifest targets names the variable (any case, whole or assembled from `+`-joined literals) or assigns `ResourceReaperEnabled = false`; red with `file:line — <form>` lines; red when it read no `.cs` ("scanned nothing — discovery broke"). The theory, over in-memory lines built with `string.Concat` (never `+` between literals): finds a `SetEnvironmentVariable(<the name>, "true")` line; finds the name assembled as `"TESTCONTAINERS_RYUK_" + "DISABLED"` and as `@"TESTCONTAINERS_" + "RYUK_DISABLED"`; finds `TestcontainersSettings.ResourceReaperEnabled = false;` and `ResourceReaperEnabled=false`; does NOT find `ResourceReaperEnabled == false`, `ResourceReaperEnabled = true`, or the name built from identifiers | RED by hand, one at a time, each reverted with `git checkout -- tests/Bench.Tests/Infrastructure/PostgresFixture.cs`: the `SetEnvironmentVariable` line; the split-literal assembly; the `= false` assignment → each red naming its line. Neither guard reads the first (C7, § 11, O5), so this test is its ONLY reader |
| two names | `TwoFixtures_get_two_names` (`FixtureContainerTests`) | local and CI | two `new PostgresFixture()` (never started; construction needs no daemon after K4): two different names, both matching `Owned.FullName()`, each at most 63 characters | — |
| C2 | `TheAdaptedCopyTests` | local and CI | green with the four new files on `BenchOwn` | RED first, observed before the gate: a `ContainerPass.cs` on neither list → 1 of 58 red naming it (§ 11, O1) |
| the gate, G3 | `The_container_pass_runs_once_for_two_fixture_starts_in_one_process` and `TheGate_runs_its_pass_once_for_eight_callers` (`FixtureContainerTests`) | local and CI | (i) a CONTROLLED process: a child of this executable with `BENCH_E6_GATE_CHILD=1` runs only `*GateChild`, which constructs TWO fixtures and calls `InitializeAsync` on each (locally each fails after the pass with the `:45-47` text, caught; where a daemon answers each starts, then is disposed), then prints `passes=<ContainerPass.Runs>`; the parent asserts the child printed `passes=1` — no other test shares that process. (ii) the seam: `new ContainerPass.Gate(counting pass)` awaited by 8 threads at once → the pass ran once and every caller got the same result; a pass that throws → one `Errors` line, cached, never thrown | remove the gate (`Once` calls `Run` each time) → (i) prints `passes=2`, red; a factory without `ExecutionAndPublication` → (ii) red |

`[Collection("docker-real")]` with `[CollectionDefinition("docker-real")] public sealed class DockerRealCollection;` in
`FixtureContainerTests.cs`. `DockerGate`: a `Lazy<string?>` running `docker version --format {{.Server.Version}}` once
through `ProcessRunner` (30 s); `Decide` is pure and has its own theory (three rows, local and CI). Every wait has a
ceiling: `[Fact(Timeout = 900_000)]` on the Ryuk test (async). Never dispose a fixture you did not start.

## 7. The fake `docker` (a Node script; no Python)

`FakeDocker.Write(TempDir dir, object scenario)` (private, in `ContainerPassTests.cs`) writes `scenario.json` and the
script `docker` into `dir.Path` (one `TempDir.For("fake-docker")` per test, disposed by it), makes it
`UserRead | UserWrite | UserExecute` with `File.SetUnixFileMode` after the Windows skip, and returns its absolute path.
`FakeDocker.Calls(dir)` reads `calls.jsonl` back. The script: `#!/usr/bin/env node`, CommonJS; its folder from
`process.argv[1]`; appends `JSON.stringify(process.argv.slice(2))` to `calls.jsonl`; reads `scenario.json`
(`{ ps: {stdout, stderr, exit, sleepMs}, rows: {<id>: "<inspect line>"}, inspectExit, rmExit: {<id>: <code>} }`); `ps`
prints and exits as told (after `sleepMs`); `inspect` prints `rows[id]` per id after the template, an unknown id →
`Error: No such object: <id>` on stderr and exit 1; `rm` exits `rmExit[<last arg>] ?? 0`; any other verb exits 2 with
`fake docker: unexpected <verb>`. It never reads `TMPDIR` or `os.tmpdir()` and never spells `/tmp` (both guards read the
`.cs` text that carries it).

## 8. `research/module_tests.md`

In `## Test leftovers — the C# helper, the start-of-run sweep, the audit (E6)` (`:632`), prose, no table row (a row
starting with a backticked `bench ` is parsed by `ModuleTestsCatalogTests`):

- The heading gains "the container pass".
- After **The start-of-run sweep.** (`:638`), a paragraph **The fixture's container and the container pass.**: the
  reaper check at construction and again just before the build (the setting Testcontainers acts on; the message); the
  name and label; reuse never set (4.16.0 reads no reuse configuration, observed 2026-11-16, pinned green by (3b), the
  parent's entry 10.1); the container's volumes end with it (G1); the marker; the pass, once per process, inside the fixture
  before its container (D9), its two keys, age, removal by id, 30-second calls, one line per failure.
- In **The limits.** (`:642`): the Docker proofs run only where a daemon answers and fail under GitHub Actions without
  one; the pass sees the daemon the `docker` CLI sees, and Testcontainers may be pointed elsewhere (a context, the
  properties file); a builder's `.WithCleanUp(false)` is not read by the reaper check (scenario (1) covers the fixture's own
  builder); the source test (7) skips the manifest targets, which the copy check holds to the template's bytes, and reads
  neither a name assembled from identifiers or through `string.Concat` nor a setter reached by reflection.
- In its commands: the three new classes, and that on a machine without Docker the Docker proofs skip with their reason.

## 9. The guards in S3

- **The Node guard**, at `5070beda` **[M]**: `--profile node` 209, the default profile 3, none in `PostgresFixture.cs`.
  Expected after S3: 209 and 3, none in `PostgresFixture.cs` or in the new files (§ 11, O2, O6, O8 read each planned line
  as clean). Any new line: fix the NEW file; never a baseline (D10), never a copied file.
- **The copied C# guard by hand** (`--explicit only --filter-class "*LeftoversRepositoryGuardTests"`, information; S7 turns
  it on): its count on the base build and on the head, expected equal (173 at F42-c's build, its brief § 10.1 O10 **[D]**;
  re-read on this base by the coder), and no finding naming `PostgresFixture.cs` or a new file. Then the two plants of (5)
  and (7) in the worktree, run, recorded, reverted with `git checkout -- <file>`, never committed.
- Watch for: the prefix word in a literal (K2), `/tmp` in a literal (`TempFolderLiteral`), `Owned.TempRoot` outside the
  helper folder, a `.WithName("…")` literal, `docker run`, and the Ryuk variable whole in one literal (K11).

## 10. Commands (from the worktree root; WSL, no `.exe`, no Docker; one build at a time)

```bash
nice -n 19 dotnet build dew_flow_benchmark.slnx -c Release -m:4 -p:AppHostRuntimeIdentifier=linux-x64 --disable-build-servers
nice -n 19 dotnet format dew_flow_benchmark.slnx --verify-no-changes --no-restore
git diff --check
T=./tests/Bench.Tests/bin/Release/net10.0/Bench.Tests
nice -n 19 $T --filter-class "*ContainerPassTests"
nice -n 19 $T --filter-class "*ContainerRulesTests"
nice -n 19 $T --filter-class "*FixtureContainerTests"      # locally: the pure facts run, the Docker proofs skip with their reason
nice -n 19 $T --filter-class "*TheAdaptedCopyTests"
nice -n 19 $T --filter-namespace "Bench.Tests.Leftovers"
nice -n 19 $T --explicit only --filter-class "*LeftoversRepositoryGuardTests"   # information only
nice -n 19 $T                                                # the whole suite: compare the FAILED list with the base's
node .agents/conventions/tools/leftovers-check.mjs --repo . --profile node --token bench --identity "the measuring harness" | head -1   # 209
node .agents/conventions/tools/leftovers-check.mjs --repo . --token bench --identity "the measuring harness" | head -1                  # 3
node .agents/conventions/tools/plan-lifecycle.mjs
```

Build, then run: never a suite over a stale `bin/`. Never `dotnet test`. Only CI observes the Docker proofs; the PR quotes
their lines.

## 11. Observations — made before this brief's plan gate (the parent's item 8.1)

**Common to all rows:** consumer **benchmark `5070beda`** (`main`: #217 and #218), cloned (`git clone --no-hardlinks`) into a
scratchpad folder outside every worktree and checked out at that SHA; **upstream pin mcp `53f65130`** (the copy's
manifest commit, `TheAdaptedCopyTests.cs:79`; `TheManifest_IsWellFormed` and the 55 entry rows green in O1's run) and the
mount **`b1746499`** (`git -C .agents/conventions rev-parse HEAD` in this worktree, with its `node_modules`); Node
`v26.12.1`, .NET SDK `10.0.114`, WSL `Ubuntu`, uid 1000, no `docker` binary and no daemon. Two scratch files were added to
the clone and never committed: `tests/Bench.Tests/Infrastructure/S3ScratchObservations.cs` (two facts: `Obs_Guard_Cases`
calls `LeftoversGuard.Scan(tree, tree)` over each private tree; `Obs_Testcontainers` reads Testcontainers by reflection
and builds the fixture's builder) and a stand-in `tests/Bench.Tests/Leftovers/ContainerPass.cs` (one namespace line and one
empty static class). Build: `nice -n 19 dotnet build tests/Bench.Tests/Bench.Tests.csproj -c Release -m:4
-p:AppHostRuntimeIdentifier=linux-x64 --disable-build-servers` → 0 warnings, 0 errors, 58 s. `T` =
`./tests/Bench.Tests/bin/Release/net10.0/Bench.Tests`, run as `nice -n 19 $T …`. The private trees were written by a
scratchpad Node script from `tests/Bench.Tests/Infrastructure/PostgresFixture.cs` at `5070beda`, one tree per case, each
holding ONE file at the fixture path named in the row (the marker and plants inserted at line 96, or after line 32, the
`.WithCommand` line); the Node guard read the same files with `scanText(<fixture path>, <text>)` of the mounted
`leftovers-check.mjs`. The scratch is not kept (a reboot clears it); the results are here.

| # | Claim (where) | Invocation | Fixture path, plant | Expected | Observed |
|---|---|---|---|---|---|
| O1 | C2, the "without" half (stories `:131`; § 6 row C2) | `$T --filter-class "*TheAdaptedCopyTests" --no-ansi` | `tests/Bench.Tests/Leftovers/ContainerPass.cs`, a file on neither list | the copy check red, naming it | exit 2; total 58, failed **1**: `EveryLeftoversFile_IsInTheManifestOrIsBenchsOwn`, "… but found at least one item {"tests/Bench.Tests/Leftovers/ContainerPass.cs"}"; 57 succeeded. The "with" half (the entry on `BenchOwn`) is the PR's GREEN |
| O2 | C9, the C# half, candidate (stories `:138`, `:70`; K10) | `$T --filter-method "*Obs_Guard_Cases"` (`LeftoversGuard.Scan`) | `tests/Bench.Tests/Infrastructure/PostgresFixture.cs`, K10's marker above `NewEmptyDatabaseAsync` (line 96); and the file as it is | no finding | marker: scanned 1, findings **0**; the file as it is: **0** |
| O3 | C9, the C# half, counterexample | the same | the same path; the marker's reason `the per-test databases are kept by the PostgresFixture container`; and `… live and die with the` (no owner) | one `exemption` each | **1** each: `PostgresFixture.cs:96 — exemption — the exemption of 'container-scoped' must name what it lives and dies with: …` (both cases, the same text) |
| O4 | E6-f1's Node half, re-run at this pin (stories `:70`, observed at `83f166d6`) | `scanText` | the three files of O2 and O3 | `[]`; one `exemption`; one `exemption` | `[]` (and `[]` for the file as it is); 1 `exemption` at line 96; 1 `exemption` at line 96, the same text as the C# guard's |
| O5 | C7, both halves (stories `:136`, `:68`; scenario (7)) | `Scan` and `scanText` | the fixture path; line 96 `private static void Plant() => Environment.SetEnvironmentVariable("TESTCONTAINERS_RYUK_DISABLED", "true");` | not read by either (**[R]**: `RyukOff` wants `:` or `=` after the name) | C#: **0** findings; Node: `[]` — **not read by either guard**, so scenario (7)'s test is its ONLY reader. Control, the same line position with `"TESTCONTAINERS_RYUK_DISABLED=true"`: C# 1 `ryuk-off` ("the reaper is switched off"), Node 1 `ryuk-off`, so the location is read and the zero is about the spelling |
| O6 | K1, K2: the fixture's planned lines | `Scan` and `scanText` | the fixture path; after line 32: `.WithName(Owned.NameFor("pg"))` and `.WithLabel(ContainerPass.LabelKey, Owner.RunRecord().Format())` | clean | C# **0**, Node `[]`. Control, `.WithName("bench-pg")` at the same place: C# 1 `docker-name` ("the fixture name "bench-pg" is not an owner-form leftover name"), Node 1 `docker-name` |
| O7 | K2: the label key as one literal | `Scan` and `scanText` | the fixture path; after line 32: `.WithLabel("testsToDelete.owner", Owner.RunRecord().Format())` | C#: `owner-blind` (**[R]**, `AllowedAfterWord`) | C#: **1** `owner-blind` at line 33 (`testsToDelete.ow`); Node: `[]`. So the key is built in the helper's folder (K2) |
| O8 | the new files' shapes (§ 5, K11) | `Scan` and `scanText` | `tests/Bench.Tests/Leftovers/ContainerPass.cs` with `LabelKey`, `ListArgs`, `RemoveArgs` and a `["create", "--name", name, "--label", LabelKey + "=" + record, …]` argv; `tests/Bench.Tests/Leftovers/ContainerRulesTests.cs` with the variable built from two pieces and a planted line built from pieces | clean | C# **0** and Node `[]` for each |
| O9 | the old brief's C5, re-observed: no guard reads reuse (E6-f2 (3)) | `Scan` and `scanText` | the fixture path; after line 32: `.WithReuse(true)` | not read | C# **0**, Node `[]` |
| O10 | K3: the effective reaper setting (the carried input "check it at fixture start") | `OBS_TAG=<t> … $T --filter-method "*Obs_Testcontainers"`: `TestcontainersSettings` read by reflection, each property | five processes: nothing set; `TESTCONTAINERS_RYUK_DISABLED=true`; `HOME=<scratch>` with `.testcontainers.properties` holding `ryuk.disabled=true`; `TESTCONTAINERS_REUSE_ENABLE=true`; `HOME=<scratch>` with `testcontainers.reuse.enable=true` (no `~/.testcontainers.properties` exists for this user **[M]**) | `ResourceReaperEnabled` false under both Ryuk settings | `ResourceReaperEnabled` = **True**, **False**, **False**, True, True; every property read without a daemon (none threw); `ResourceReaperPrivilegedModeEnabled` True; `DockerHostOverride` null |
| O11 | K5: does 4.16.0 have ANY configuration path that turns reuse on (the carried input; E6-f2 (3)) | the same five processes; reflection over every type of `Testcontainers.dll` (`4.16.0+df0f931ea921…`) for members whose name holds `reuse`; the members of `ICustomConfiguration` and `CustomConfiguration`; the fixture's builder (`:27-32`) and the same plus `.WithReuse(true)` as a control, its `DockerResourceConfiguration.Reuse` read by reflection | the builder's configuration in each environment | none, if the DLL strings (stories `:71`) hold | **None.** `Reuse` = **False** in all five environments for the fixture's builder; **True** only with `.WithReuse(true)`, in all five. `ICustomConfiguration`: `GetDockerApiVersion`, `GetDockerAuthConfig`, `GetDockerCertPath`, `GetDockerConfig`, `GetDockerContext`, `GetDockerHost`, `GetDockerHostOverride`, `GetDockerSocketOverride`, `GetDockerTls`, `GetDockerTlsVerify`, `GetHubImageNamePrefix`, `GetNamedPipeConnectionTimeout`, `GetRyukContainerImage`, `GetRyukContainerPrivileged`, `GetRyukDisabled`, `GetWaitStrategyInterval`, `GetWaitStrategyRetries`, `GetWaitStrategyTimeout` — no reuse getter. Members naming reuse: `AbstractBuilder<…>.WithReuse` (2), `IAbstractBuilder<…>.WithReuse` (2), `TestcontainersClient.TestcontainersReuseHashLabel`, `IResourceConfiguration<…>` and `ResourceConfiguration<…>` `Reuse`, `ReuseHashProvider`, `GetReuseHash` (and their accessors and backing fields); the same list in every environment. So row E6-f2's "through configuration alone" has **no instance at 4.16.0** |
| O12 | K4: `Build()` without a daemon (the old brief's Q8) | the same runs | the fixture's builder | not read before | `Build()` threw `DotNet.Testcontainers.Builders.DockerUnavailableException: Docker is either not running or misconfigured. …` in all five; the builder's `DockerEndpointAuthConfig` is null |
| O13 | the base counts of (5) and § 9 | `node …/leftovers-check.mjs --repo . --profile node --token bench --identity "the measuring harness"`, and without `--profile node`, in this worktree at `5070beda` | the repository | 209 and 3 (F42-c brief § 10.1 O8 **[D]**) | **209** and **3**; 0 of the 209 name `PostgresFixture.cs` |
| O14 | K11: where the variable is spelled whole today | `git grep -n -c "TESTCONTAINERS_RYUK_DISABLED" -- '*.cs'`, and the same over `*.json`, `*.yml`, `*.properties`, `*.sh`, `*.ps1`, `*.mjs` outside `.agents` | the repository at `5070beda` | — | 5 lines, all in manifest targets: `LeftoversGuardScenarios.cs` 1, `LeftoversGuardTests.cs` 4 (the guard's own test data, `TESTCONTAINERS_RYUK_DISABLED: \"true\"` inside a C# string, which neither guard reports: O13's counts hold none) |

**Written open-ended, not observable here (hosted CI only; red or not, the PR's run says which):** scenario (1) — Ryuk
removes a SIGKILLed child's container within 120 s, and how long it takes, and whether its volumes go too (G1: a survivor
fails the test); (2) on a daemon — the real `inspect` template's
output, the label on a started container, the pass's removal by id; (3) — whether disposal of a reused container leaves
it (expected, **[D]** the `WithReuse` XML doc: "Enabling reuse will disable the resource reaper"; **[R]** disposal); (4) —
that disposal removes the container and its volumes (G1: a survivor fails the test), and its size; (3b) — the pin's
container half (reuse-hash label empty, the container and its volumes gone after disposal), its configuration half being
observed already (O11) and run locally by the pin itself; C9 and C7 again on the head (the same lines, expected as O2–O5); `RequireReaper` in both children on the head
(K3, G2; expected as O10); the gate's controlled process (G3). Windows: no leg.

## 12. The commit series, size and split

**One PR, one story.** Each commit builds and its new tests pass, except the CI RED of commit 4 (Q3).

1. `ContainerPass.cs` (§ 5), `ContainerPassTests.cs` (§ 6 rows (2), (6)), the four `BenchOwn` lines and the summary count
   (C2: RED first by a build before the lines, as O1). Mutation REDs by hand.
2. `ContainerRulesTests.cs` (7), both forms and the split-literal join (G2). RED by hand (the three planted lines).
3. `PostgresFixture.cs`, part one: the constructor with `RequireReaper` and the name, `Builder()`, the build in
   `InitializeAsync` after `ContainerPass.Once()` and the second `RequireReaper`, `DisposeAsync`, the seam, `Builder()`
   internal; NOT yet `.WithName`/`.WithLabel`. The two reaper children, the reuse pin's configuration half (3b), the gate's
   two tests (G3) and `TwoFixtures_get_two_names` (local).
4. `FixtureContainerTests.cs`'s Docker proofs (with G1's volume checks), `DockerGate`. Pushed alone (Q3): CI shows the name and label proof red for
   the right reason (no owner-form name, empty label).
5. `PostgresFixture.cs`, part two: `.WithName(ContainerName)`, `.WithLabel(…)`, the marker (K10). CI green.
6. `research/module_tests.md`; this brief's progress line.

**Size.** About 1,100 lines: `ContainerPass.cs` ~220, `ContainerPassTests.cs` ~350, `FixtureContainerTests.cs` ~350,
`ContainerRulesTests.cs` ~100, the fixture ~40 changed, the docs ~40. One Sonnet coder.

## 13. Done when, and what the PR body quotes

- (1) the Ryuk proof green on `ubuntu-latest`, not skipped, with its seconds and its volumes gone (G1); both reaper
  children green (G2), their REDs quoted.
- (2) the name and label proof and the real-daemon pass green in CI, the commit-4 RED quoted; the fake-docker facts green,
  their hand REDs quoted.
- (3) the builder counterexample green (the disposal check red on a reused container); (3b) the configuration pin green
  locally and in CI (entry 10.1), with O11 cited.
- (4) the disposal proof green, its volumes gone (G1); the size warning quoted (Q4).
- (5) both guards clean on `PostgresFixture.cs`, the Node guard at 209 and 3; the malformed marker's `exemption` quoted
  from each guard.
- (6) the no-docker fact green; the local whole-suite run's first stderr lines and the `"postgres"` failure text quoted.
- (7) the source test green; its three hand REDs quoted (the `SetEnvironmentVariable` line, the split-literal name, the
  `= false` assignment).
- `TheAdaptedCopyTests` green with the four entries; the gate's controlled process printed `passes=1` (G3); two names; 0 warnings; `dotnet
  format` clean; `research/module_tests.md` updated; `plan-lifecycle.mjs` clean; `git diff --name-only 5070beda..HEAD`
  within § 0.1.

## 14. Open questions — ANSWERED 2026-11-16: each accepted as recommended (§ 16)

1. **Q1 — "Its tests" covers three new test files in `tests/Bench.Tests/Leftovers/`.** The Files line names
   "`ContainerPass.cs` … its tests" and adds "each new test file of the folder" to `BenchOwn`; the fixture's Docker proofs and
   the source test (7) are tests of the fixture, not of the pass. [Recommend: yes, within the line, as "each new test file of
   the folder"; one test file per subject keeps each under 400 lines. If read narrowly, putting them all in
   `ContainerPassTests.cs` keeps the scope at the cost of a ~800-line file; any other folder would have been a counted change.]
2. **Q2 — The plan records.** S3's Files line names no plan document, while F42-c's bound (the parent's item 4.1) named
   them by path. [Recommend: the brief, its `todo/README.md` row, its promotion to `research/` with a `research/README.md`
   row, and the stories' progress lines are within the PR, as `common/planning-docs.md` requires them in the task that
   finishes a plan, and as S2 did; the coordinator confirms, or says it is a counted change, before the gate.]
3. **Q3 — The builder half of E6-f2 (3) RED is a committed counterexample through an internal test seam**
   (`internal PostgresFixture(Func<PostgreSqlBuilder, PostgreSqlBuilder> extra)`), and the name/label RED is seen by
   pushing commit 4 alone. [Recommend: yes to both. The seam keeps the RED permanent and runnable, where a hand plant in the
   fixture would be a pushed commit and a revert; the fixture's production path never passes an extra. The extra CI run of
   commit 4 is one run.]
4. **Q4 — The size of § 8 of the stories** ("`docker system df -v` before and after one suite run, quoted on the PR") needs
   a `ci.yml` step, outside § 0.1. [Recommend: read the container's own size in the disposal proof (`docker ps -s`, a
   warning on the run) and quote it; § 8's exact form would be a counted change, and the per-container size answers its question
   (the fixture's growth) without it.]
5. **Q5 — Scenario (7)'s exclusion of the manifest targets.** The alternative is to read only C# that SETS the variable
   (an assignment, a `SetEnvironmentVariable`, an environment dictionary) and not a bare mention; that needs no exclusion
   but reads fewer spellings. [Recommend: K11 — any mention in any case, outside the manifest targets read from the
   manifest; those files are byte-held to the template, and today they hold the five lines of O14.]
6. **Q6 — An `inspect` batch that fails removes nothing in that pass** (an id that vanished between `ps` and `inspect`
   fails the whole call). [Recommend: yes, as D9 says; the next run retries.]
7. **Q7 — `.WithCleanUp(false)` is read by nothing** (K3): not by the guards, not by the reaper check. [Recommend: leave it
   to scenario (1) for the fixture's own builder and say so in *The limits* (§ 8); adding a source check for it to (7)
   would widen the stories' check, which is the coordinator's call.]

## 15. Notes for the parent (`dew_flow_conventions`; a finding about the rule goes there, stories § 9)

- **N1 — Row E6-f2's configuration half has no instance at Testcontainers 4.16.0** (O11): the RED "through configuration
  alone" cannot be produced. Recorded by the parent as entry **10.1** (conventions PR #174, `0ca0825d`): that half is a
  GREEN PIN, scenario (3b) here; a dependency bump re-runs O11, and the pin says whether a configuration path appeared.
- **N2 — Neither guard reads `Environment.SetEnvironmentVariable("TESTCONTAINERS_RYUK_DISABLED", "true")`** (O5, both
  halves of C7 observed), nor `.WithReuse(true)` (O9), nor the label key as one literal in the Node guard (O7, the C# guard
  reads it as `owner-blind`). The first two are the parent's F43; benchmark covers them with its own tests (7) and (3).

## 16. Decisions and the plan gate (2026-11-16)

**The plan gate.** coai session `e4eef27b`: `proceed`. Its accepted findings, folded in above:

- **G1 — the anonymous database volume ends with the container.** The container's volume names are captured by a
  successful `inspect` before disposal and before the kill, and `docker volume ls -q` (exit 0) must list none
  of them after disposal (4), after the killed child (1), in the builder counterexample's cleanup (3) and in the pin (3b).
  A survivor fails the test: no silent retention. Hosted CI only (Docker).
- **G2 — the effective reaper setting immediately before the build**, in `InitializeAsync` after the pass and before
  `Build()` (K3, § 4 step 4, scenario (1) K3 (ii)); and test (7) also reads `ResourceReaperEnabled = false` assignments
  and the name assembled from split literals (K11, scenario (7)), with planted examples red, still skipping the manifest
  targets.
- **G3 — the once-per-process gate proved in a controlled process**: two fixture starts in a child that runs nothing else
  (`passes=1`), and the gate's seam under 8 callers; never by other tests' order (scenario "the gate").

**The coordinator's decisions.** Q1–Q7 of § 14 accepted as recommended. Q4: the container's own size in the disposal test,
no `ci.yml` step. The configuration-reuse result is the parent's entry 10.1 (conventions PR #174): row E6-f2's "through
configuration alone" counterexample is a GREEN PIN (scenario (3b): both spellings set → the built container's `Reuse`
stays `False` and the container still ends with the fixture); the builder counterexample (3) and the run-time disposal
proof (4) stay RED-first. 10.1 is epoch 10's first change, so the next accepted change is the tenth full stop: nothing
beyond § 0.1's paths. Every finding above stays inside them (the fixture, `ContainerPass.cs` and the three new test files
of the folder); a need for any other file is a STOP and a report, never an added path.
