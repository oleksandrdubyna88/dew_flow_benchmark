# E6.S2 brief — the adapted copy, the start-of-run sweep, the audit (rows E6-e, E6-c, E6-j)

> Status: **plan only, nothing implemented yet, 2026-11-12.** The brief of story S2 of `todo/PLAN_test_leftovers_E6.md`.
>
> Judge's brief (Opus) for a Sonnet coder. Written 2026-11-12. Read-only inputs, all verified for this brief:
> benchmark worktree `/home/jinx/git/wt/bench-e6-s2`, branch `feat/e6-s2-adapted-copy` at `239e078e`, mount
> `.agents/conventions` at `d23a2ea`; template `dew_flow_mcp` at `21553ce5c5729e57557504c4a59e0311e69bc489`;
> coai `dew_flow_connect_other_ais` `origin/main` = `e060d77e` (it descends from `8a036eac`; nothing under
> `src_mcp/tests_leftovers/` or `src_mcp/tests/Leftovers/` changed between the two; the files this brief copies last
> changed at `825504eb`); creds `origin/main` (its `todo/PLAN_test_leftovers_E2.3_brief.md` and its manifest).
> "Plan" is `todo/PLAN_test_leftovers_E6.md` in the worktree. "T:" is the template at `21553ce`. "C:" is coai at `e060d77e`.

## 0. What you build, in one paragraph

You copy the C# test-leftovers helper of `dew_flow_mcp` (`tests/Mcp.Tests/Leftovers/`, 51 files, plus
`tests/Mcp.Tests/DirectoryLinks.cs` and `tests/Mcp.Tests/RepositoryRoot.cs`) into `tests/Bench.Tests/Leftovers/`. You change
the copy ONLY by the exact-replace operations of a committed `template-manifest.json`. A test (`TheAdaptedCopyTests`)
undoes those operations and compares the git blob SHA. You add benchmark's own files: coai's Windows sweep lock
(`SweepLock`, F29), a start-of-run sweep that runs ONCE per process behind a gate that every test thread waits for
(`StartOfRunSweep` + an assembly-level `StartOfRunOrderAttribute`), a `TempDir` entry for the later stories, coai's
two-sweeps race test, and the case-15 test (a killed run's git-repository folder with a linked worktree inside is removed
whole). You wire the end-of-run audit and its CI proof. You add the mount checkout to the CI job `build-test`. You write
the *Leftovers* section of `research/module_tests.md`. The repository guard test is copied but stays OFF (S7 turns it on).
ONE pull request. Do not change any other repository.

## 1. Rules you must read first

- `.agents/PROJECT.md` (commands; never `dotnet test`; builds with `-m:4` through `low-priority.mjs`).
- `.agents/conventions/common/test-leftovers.md` and `test-leftovers-detail.md` §1–§4, § Tokens (`bench` = "the measuring harness").
- The plan: § 1 rows E6-c (`:26`), E6-e (`:28`), E6-j (`:38`); § 4 D2 (`:117-126`), D3 (`:128-133`), D5 (`:145-148`), D10
  (`:178-179`); § 6 S2 (`:218-239`); § 12 item 3 (`:384-388`); § *Plan gate* (`:391-405`).
- No Python anywhere (scripts in Node or bash). No `git stash`. Commit only; the coordinator pushes and opens the PR.

## 2. Preconditions and merge conditions (do not merge S2 without all of them)

1. **S1 is merged first** (plan § 5, rule § *The barrier*: every sweeper stage lands before any producer stage). S2 makes
   owner-form folders, so S2 is a producer. At `239e078e` S1 is NOT merged.
2. **The owner's Windows machine** (plan § 12 item 3): before S2 merges, the owner states that the machine does not build or
   test this repository, or the machine gets the scan and the checklist. The coordinator gets this answer; the coder does not.
3. At most three open PRs in this repository (`common/pull-requests.md` rule 7).
4. CI checks of the head SHA exist and are green (`build-test`, `contract`, `plans`); the coai code gate and the Opus
   review are done.

## 3. Decisions of this brief (each with its reason)

- **B1 — One target folder.** Every copied file and every benchmark-own file goes to `tests/Bench.Tests/Leftovers/`.
  Benchmark has ONE test project, so coai's split into a library and a test project is not needed. Namespace
  `Bench.Tests.Leftovers` for the 51 folder files; `Bench.Tests` for `DirectoryLinks.cs` and `RepositoryRoot.cs` (their
  template namespace is `Mcp.Tests`, the parent; the copied code finds them from the child namespace with no `using`).
  Checked: no type named `RepositoryRoot`, `DirectoryLinks`, `TempDir` or `SuiteChild` exists in `tests/` today; the only
  `RepositoryRoot` is a private method (`Scouts/ScoutTreesTests.cs:272`), and a member wins over a type, so it still binds.
  Checked: no simple name in the template collides with a sibling namespace (`Bench.Tests.Trace`, `Bench.Diagnostics`, …);
  the only hits are class members (`OwnedDir.Registry`) and an enum member (`SourceKind.Tests`).
- **B2 — `DirectoryLinksTests.cs` is NOT copied.** The plan's list is the 51 files plus the two support files (`:220-221`);
  coai did the same.
- **B3 — The repository guard test is copied, and an op makes it `Explicit`.** `LeftoversRepositoryGuardTests.cs` is one of
  the 51 files, and S2 says "copied but NOT switched on" (`:225-226`, D10). One manifest op changes `[Fact]` to
  `[Fact(Explicit = true)]` with a comment that names S7. Why `Explicit` and not `Skip`: xUnit v3 (4.0.3 here) does not
  run an explicit test by default, so the run stays green, but a person can run it by hand
  (`--explicit only`), and S3–S6 can read the guard's count that way. S7 deletes this one op: the file then is the
  template's bytes again. Why not leave the file out until S7: the plan says it is copied in S2, and the op is visible
  in the manifest, where the diff check pins it.
- **B4 — F29 is closed by copy** (D3): the three `LeftoversSweep.cs` ops of coai's manifest apply to the SAME template blob
  (`64f97f5f…`, unchanged between `197cc76` and `21553ce`), plus coai's `SweepLock.cs` as a benchmark-own file. The mutex
  name is built from `OwnedToken.RepoToken`, so the token stays written once and a coai sweep never waits for a benchmark one.
- **B5 — The start-of-run sweep has no switch.** Coai's `StartOfRunSweep` reads a list-only switch (coai's own D11). E6
  has no such decision, and rule §4 says the sweep collects. Benchmark's copy keeps coai's ledger, its end stamp and the
  order proof, drops the switch, and adds the once-only gate the plan asks for (`:223-224`; plan gate, code round 1).
- **B6 — Case 15 plants in a PRIVATE sweep root**, an `OwnedScope` folder, never the machine's temp root. This is the
  template's own practice (T:`LeftoversSweepTests.cs` summary: "Every scenario passes a PRIVATE `Tmp`") and coai's.
  The sweep code does not depend on which root it gets. "In the temp root" in S2's text is read as "in the root the sweep
  is handed". Not a plan change.
- **B7 — Case 15's RED is a mutation of the copied sweep, by hand, not committed** (§ 8). A test written before the copy
  exists does not compile, and a compile error is not a RED for the right reason.
- **B8 — The audit's proof is a CI step**, the template's own (`dew_flow_mcp` `.github/workflows/ci.yml` at `21553ce`,
  step "The audit fails a run that leaves a claimed folder"), with benchmark's names. `ci.yml` is in S2's Files list.
- **B9 — No `raw-temp` baseline** (D10). Measured for this brief (§ 13): the 53 copied files add ZERO findings to the Node
  guard. Every new benchmark-own line that the guards would read either uses the helper or carries one inline exemption.

## 4. The files

### 4.1 Copied (53 manifest entries; upstream → `tests/Bench.Tests/Leftovers/<same file name>`)

The 51 files of `git -C /home/jinx/git/dew_flow_mcp ls-tree -r --name-only 21553ce tests/Mcp.Tests/Leftovers` (all of
them, `LeftoversRepositoryGuardTests.cs` included), plus `tests/Mcp.Tests/DirectoryLinks.cs` (blob `5512b98e…`) and
`tests/Mcp.Tests/RepositoryRoot.cs` (blob `2884381a…`). Upstream paths in the manifest are the template's paths.

**Operations.** Every entry starts with its namespace op. The others follow in the order given. `count` is the number of
matches in the template (and of the new text in the copy). Every op that is not a namespace line carries a `reason`.

| File | Ops after the namespace op (old → new, count) |
|---|---|
| all 51 folder files | namespace: `namespace Mcp.Tests.Leftovers;` → `namespace Bench.Tests.Leftovers;` (1) |
| `DirectoryLinks.cs`, `RepositoryRoot.cs` | namespace: `namespace Mcp.Tests;` → `namespace Bench.Tests;` (1) |
| `OwnedToken.cs` | two ops, T:`:9` and T:`:11`, exact text in the block below the table (1 each) |
| `LeftoversSweep.cs` | F29 ops 1–3, exact text in § 4.2 |
| `LeftoversSweepFixture.cs` | `typeof(Mcp.Tests.Leftovers.LeftoversSweepFixture)` → `typeof(Bench.Tests.Leftovers.LeftoversSweepFixture)` (1); `LeftoversSweep.Run(new SweepOptions(Owned.TempRoot(), DateTimeOffset.UtcNow, LeftoversSweep.MinAge, Liveness.ProbeProcess, Scope.IsMine))` → `StartOfRunSweep.Run()` (1) — T:`:78`, inside `RealSweep` |
| `LeftoversAuditTests.cs` | `mcp tests:` → `bench tests:` (6); `--token mcp --dry-run` → `--token bench --dry-run` (1) |
| `LeftoversSelfTest.cs` | `MCP_LEFTOVERS_SELFTEST` → `BENCH_LEFTOVERS_SELFTEST` (2) |
| `OwnedTests.cs` | `-mcp-pg-` → `-bench-pg-` (1) |
| `LeftoversGuard.cs` | `tests/Mcp.Tests/` → `tests/Bench.Tests/` (5) — `HelperFile`, `HelperFolder`, three `FoldingAllowances` (T:`:47-48`, `:63-65`) |
| `LeftoversGuardTokens.cs` | `tests/Mcp.Tests/` → `tests/Bench.Tests/` (1) — `TokenFile` |
| `LeftoversGuardTests.cs` | `tests/Mcp.Tests/` → `tests/Bench.Tests/` (11); `RepositoryIdentity.Row` → `OwnedToken.RepoRow` (1) — T:`:395`; `RepositoryIdentity` is an mcp type outside the folder (the parent's item 4 permits this op; coai and creds made it) |
| `LeftoversRepositoryGuardTests.cs` | `    [Fact]\n    public void This_repository_passes_the_guard()` → `    [Fact(Explicit = true)] // E6 S7 switches this on (plan D10); until then run it by hand with --explicit only\n    public void This_repository_passes_the_guard()` (1) — T:`:11-12` |

The two `OwnedToken.cs` ops (old line, then new line; four spaces of indent; the comments keep their em dash and
backticks):
```
    public const string RepoToken = "mcp"; // rule §1 — the ONLY place the token is written
    public const string RepoToken = "bench"; // rule §1 — the ONLY place the token is written

    public const string RepoRow = "the MCP server"; // the `repository` column of the `mcp` row: the guard's identity check
    public const string RepoRow = "the measuring harness"; // the `repository` column of the `bench` row: the guard's identity check
```
Reasons: "the repository token (rule §1; the bench row of the mounted Tokens table)" and "the repository column of the
bench row: the guard's identity check".

**Not changed, on purpose:** the mount path (already `.agents/conventions` at `21553ce`); `MCP_DARWIN_FIFO_CHILD`
(T:`DarwinFifoChild.cs:14`, a private handshake between a test and its own child, not a token — creds kept it); the
doc comment `mcp tests:` at T:`LeftoversAuditException.cs:4` (the message itself is built from `OwnedToken.RepoToken`,
T:`:31` — coai kept it); the `mcp` rows and `-u1000-mcp-…` strings inside `LeftoversGuardTests.cs` fixtures (they test
the guard with the registered `mcp` row; coai and creds kept them); `LeftoversGuardFiles.cs` (its roots `tests/` and `src/`
fit benchmark; S7 adds `tools/` by its own op); the ownership, liveness and deletion code.

Counts above were measured with `grep -o` on each template blob. The diff check is the final judge: if a count is wrong,
it says which op and where.

### 4.2 The three F29 ops on `LeftoversSweep.cs` (from C:`src_mcp/tests_leftovers/template-manifest.json`, the same blob)

Op F1 — old (T:`:51-52`, the second line):
```
    string Tmp, DateTimeOffset Now, TimeSpan MinAge, Func<long, Probe> Probe, Func<string, bool>? Mine = null, SweepHooks? Hooks = null);
```
new:
```
    string Tmp, DateTimeOffset Now, TimeSpan MinAge, Func<long, Probe> Probe, Func<string, bool>? Mine = null, SweepHooks? Hooks = null, TimeSpan? SweepLockWait = null);
```
Op F2 — old (T:`:86-96`):
```
        var pass = new Pass(options);
        try
        {
            pass.Go();
        }
        catch (Exception ex)
        {
            pass.Fail("sweep", ex);
        }

        return pass.Result();
```
new: the same, with two lines after the first:
```
        var pass = new Pass(options);
        using var lease = SweepLock.Take(options.Tmp, options.SweepLockWait, pass.Note); // Windows: one sweep at a time over a root (F29, coai's fix, SweepLock.cs); POSIX: holds nothing
        if (!lease.Granted) return pass.Result();
        try
        …(the rest unchanged)
```
Op F3 — old (T:`:114`): `        public void Fail(string label, Exception ex) => _errors.Add($"{label}: {FirstLine(ex)}");`
new: that line, a blank line, then `        public void Note(string line) => _listed.Add(line);`

Each `reason`: "F29 (conventions follow-ups plan; plan D3): on Windows Directory.Move has no single winner (coai run
41873954643), so the whole pass runs under coai's per-root named mutex, SweepLock.cs, copied from coai e060d77e."

### 4.3 Benchmark-own files (on `TheAdaptedCopyTests`' own list, never manifest targets)

Each file's header names its source by repository, path and commit (`dew_flow_connect_other_ais`
`src_mcp/…` at `e060d77e`) and what was changed.

| File | Namespace | What it does | Source |
|---|---|---|---|
| `SweepLock.cs` | `Bench.Tests.Leftovers` | Windows only: a named mutex per real root over the whole pass; 30 s wait; abandoned = acquired; no lock = one listed line, nothing swept, never a throw. POSIX: holds nothing | C:`src_mcp/tests_leftovers/SweepLock.cs` (92 lines), verbatim but the namespace and the name prefix: `@"Global\" + OwnedToken.RepoToken + "-leftovers-sweep-"` instead of `@"Global\coai-leftovers-sweep-"` (C:`:74`) |
| `StartOfRunSweep.cs` | `Bench.Tests.Leftovers` | The once-only gate, the real sweep, the ledger (§ 6) | C:`src_mcp/tests_leftovers/StartOfRunSweep.cs`: ledger `:36-54`, `RecordTestStart` `:74-86`, the template call `:149-150`; the switch (`:20-27`, `:88-146`, `:152-197`) NOT carried (B5) |
| `StartOfRunOrderAttribute.cs` | `Bench.Tests.Leftovers` | The assembly-level `BeforeAfterTestAttribute`; holds `[assembly: Bench.Tests.Leftovers.StartOfRunOrder]` (§ 6) | C:`src_mcp/tests_leftovers/StartOfRunOrderAttribute.cs` `:12-30`; the declaration form C:`src_mcp/tests/RunTempStart.cs:6` |
| `StartOfRunSweepTests.cs` | `Bench.Tests.Leftovers` | The gate's tests and the order proof (§ 6.4) | new; the order test after C:`src_mcp/tests/RunTempTests.cs` (`TheProducerAndTheSweep_LandTogether`, `TheSweep_EndedBeforeTheFirstTestStarted`) |
| `SweepPlants.cs` | `Bench.Tests.Leftovers` | The plant helpers of the race test | C:`src_mcp/tests/Leftovers/StartOfRunSweepPlants.cs` (64 lines), verbatim but the namespace and the summary's file list |
| `SuiteChild.cs` | `Bench.Tests.Leftovers` | Starts this test executable as a child: no shell, `ArgumentList`, UTF-8 streams | C:`src_mcp/tests_leftovers/SuiteChild.cs` (82 lines), verbatim but the namespace; drop the `MatchesNothing` member if nothing uses it |
| `TwoSweepsAtOnceTests.cs` | `Bench.Tests.Leftovers` | Coai's two-sweeps race test (§ 7) | C:`src_mcp/tests/Leftovers/StartOfRunSweepRaceAndRootTests.cs` `:1-334` and `Unlink` `:590-604` |
| `LeftoversSweepCase15Tests.cs` | `Bench.Tests.Leftovers` | Case 15, E6-c (§ 8) | new |
| `TempDir.cs` | `Bench.Tests` | `TempDir.For(what)`: a thin, disposable wrapper over `OwnedDir` (§ 10) | shape of C:`src_mcp/tests_leftovers/TempDir.cs`, built on `OwnedDir` instead of `CreateTempSubdirectory` |
| `TempDirTests.cs` | `Bench.Tests.Leftovers` | § 10 | new |
| `WindowsNewFileOwnerLimitTests.cs` | `Bench.Tests.Leftovers` | The F31 pin (§ 19 item 2): makes a folder through the helper (`OwnedDir`), reads its owner SID DIRECTLY (`DirectoryInfo.GetAccessControl().GetOwner(typeof(SecurityIdentifier))` against `WindowsIdentity.GetCurrent().User`, never through `Scope.IsMine`), skips unless Windows and skips with its reason where the caller owns it; otherwise asserts `Scope.IsMine(folder)` and `Scope.IsMine(folder + ".owner")` are false (the limit holds: such a run's folders wait for a person). On Linux CI it skips, saying why | the check of C:`src_mcp/tests/RunTempTests.cs` `:426-468` (`AnUntouchedClaim_StaysUnseen_OnAnElevatedWindowsRun`, `OwnedByTheCaller`), WITHOUT its child run: no `OrphanClaimChild` here. If `GetAccessControl` needs a package, use what coai's test project references (read its csproj) |
| `TheAdaptedCopyTests.cs` | `Bench.Tests.Leftovers` | The diff check (§ 5) | C:`src_mcp/tests/Leftovers/TheAdaptedCopyTests.cs` |
| `template-manifest.json` | — | The manifest (§ 5.1) | coai's format |

### 4.4 Other files

- `tests/Bench.Tests/Bench.Tests.csproj`: add `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>` in the first `PropertyGroup`
  (`:3-8`), with the comment the template has (T:`tests/Mcp.Tests/Mcp.Tests.csproj`: "The leftovers helper calls the OS
  through [LibraryImport] (Native.cs); the generated marshalling is unsafe code. This project only."). Without it, the
  `[LibraryImport]` source generator does not compile. Do NOT carry mcp's CA1502 (`CodeMetricsConfig.txt` and its
  `.editorconfig` block): it is a gate of the template repository; a copy cannot be edited to answer it anyway.
- `.github/workflows/ci.yml` (§ 11).
- `research/module_tests.md` (§ 12).
- `.gitattributes` — ONLY if the coordinator says yes (§ 18, Q1).

## 5. The manifest and `TheAdaptedCopyTests`

### 5.1 How to build the copy and the manifest

1. Write a throw-away Node script in YOUR scratchpad (never committed). For each upstream path `P` (the 53):
   - read the raw bytes with `git -C /home/jinx/git/dew_flow_mcp cat-file blob 21553ce:P` (raw, no textconv, no line-end
     change; keep them as a `Buffer`);
   - the blob SHA is `git -C /home/jinx/git/dew_flow_mcp rev-parse 21553ce:P`;
   - apply the ops of § 4.1/4.2 in order; for each op, count the OLD text first and stop with an error if the count is
     not the listed one;
   - write the bytes to the target; never let an editor or formatter touch a copied file after that.
2. Write `tests/Bench.Tests/Leftovers/template-manifest.json`, keys exactly `about`, `repository`, `commit`, `entries`:
   - `repository`: `"dew_flow_mcp"`; `commit`: `"21553ce5c5729e57557504c4a59e0311e69bc489"`;
   - per entry `upstream`, `target` (repository-relative, forward slashes), `blob`, `replace` (`old`, `new`, `count`,
     and `reason` for every op that is not the namespace line);
   - `about` (plain ASCII hyphens are fine; this is the text the reader sees):
     > E6 S2: the adapted copy of dew_flow_mcp tests/Mcp.Tests/Leftovers at 21553ce (plan D2), plus DirectoryLinks.cs and
     > RepositoryRoot.cs. Per file: upstream path, target path, upstream git blob SHA, ordered exact-replace operations
     > with their match counts and, beyond the namespace line, a reason. TheAdaptedCopyTests undoes them in reverse on the
     > raw bytes, compares the git blob SHA, and applies them forward again. Limits (plan D3): F29 is closed by copy - on
     > Windows the sweep holds coai's per-root named mutex (SweepLock.cs, benchmark's own file) over its whole pass, so two
     > sweeps of one temp root never interleave. F31 - on Windows, what a run leaves in a process whose NEW files and
     > folders are owned by BUILTIN\Administrators (measured: pwsh on windows-latest) is not removed by the sweep and not
     > named by the audit; it waits for a person. An elevated Git Bash makes the caller's files (measured), and the limit
     > does not apply there.
     (In JSON the backslash is written `\\`.) Never write "an elevated process" as the condition (D3).
3. The copied files must keep LF and their exact bytes. Run `git diff --check` and `dotnet format … --verify-no-changes`
   (§ 14); if the formatter wants to change a COPIED file, stop and report: the answer is project-level, never an edit.

### 5.2 `TheAdaptedCopyTests` (coai's shape, benchmark's names)

Copy C:`src_mcp/tests/Leftovers/TheAdaptedCopyTests.cs` and change only:
- namespace `Bench.Tests.Leftovers`; `TemplateRepoVariable = "BENCH_TEMPLATE_REPO"` (C:`:23`);
- `CopiedUnder = ["tests/Bench.Tests/Leftovers/"]` (C:`:25`);
- the own list (C:`:31`, rename `CoaiOwn` → `BenchOwn`, test `…IsInTheManifestOrIsBenchsOwn`): the twelve `.cs` files of
  § 4.3 by full path. Write the WHOLE list in the first commit, even for files that come later: the test only refuses a
  file that is on neither list; it does not require an own file to exist;
- `ManifestPath` → `tests/Bench.Tests/Leftovers/template-manifest.json` (C:`:64`);
- `WhereItDiffersAsync` (C:`:274`): coai's `ProcessLauncher` is not here. Use benchmark's own launcher,
  `Bench.Infrastructure.Process.ProcessRunner.RunAsync("git", ["-C", repo, "cat-file", "blob", entry.Blob],
  AppContext.BaseDirectory, TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken)`; a
  `ProcessAttempt.Completed { Result.Ok: true }` gives `Result.StandardOutput`; anything else is the "(… failed: …)" line
  (`attempt.Describe`). The pattern is `tests/Bench.Tests/Infrastructure/GitCheckoutTests.cs:210-217`.
- `Operation` gains `string? Reason`. `TheManifest_IsWellFormed` adds: `Repository == "dew_flow_mcp"`; `Commit ==
  "21553ce5c5729e57557504c4a59e0311e69bc489"`; every op whose old text does not start with `namespace ` has a non-empty
  reason; `about` contains `F29`, `F31`, `BUILTIN\Administrators` and `Git Bash` (so F31 stays stated by its true
  condition). Keep coai's `ACountBelowTheTemplatesOccurrences_IsRefused…` as it is.

### 5.3 The diff check's scenarios (RED first)

| # | Scenario | RED |
|---|---|---|
| M1 | every manifest target equals the template blob under its listed ops (a theory row per file) | commit 1 (§ 15): the manifest and the test exist, the 52 other targets do not: each row is red (file not found) |
| M2 | a `.cs` file in the folder that is on neither list is red | by hand after green: add `tests/Bench.Tests/Leftovers/Stray.cs` → red naming it; delete it |
| M3 | one copied byte changed is red, naming the file and both SHAs | by hand after green: change one character in a comment of `Owner.cs` → `EveryCopiedFile_…(…Owner.cs)` red; set `BENCH_TEMPLATE_REPO=/home/jinx/git/dew_flow_mcp` once and see the first differing line named; restore |
| M4 | an op count lowered is red, naming the op | by hand: set the `mcp tests:` count to 5 in the manifest → red; restore |

Record M2–M4 on the PR (the command and the first line of each red message). Never commit them.

## 6. The start-of-run wiring (once per process; every thread waits; never fails the run)

### 6.1 How it is wired

- The copied `LeftoversSweepFixture` is an assembly fixture (T:`:3`, kept by the `typeof` op). xUnit v3 builds every
  assembly fixture before any test collection starts. Its constructor calls `RealSweep()` = `GuardedSweep(DarwinLayout.Require,
  () => StartOfRunSweep.Run())` (the manifest op), prints the report lines when the sweep did something, and never throws.
- `StartOfRunSweep.Run()` is the ONCE-only gate: a `Lazy<SweepResult>` with `LazyThreadSafetyMode.ExecutionAndPublication`.
  The first caller runs the sweep; every other caller, on any thread, blocks in `.Value` until it ended and gets the SAME
  result object.
- `StartOfRunOrderAttribute.Before` (assembly level, so it runs before EVERY test of the assembly) FIRST calls
  `StartOfRunSweep.Run()` (it waits on the gate), then takes `Stopwatch.GetTimestamp()`, then checks `EndedAt != 0 &&
  EndedAt <= now`. It NEVER throws (the sweep never fails the run, and a throw in `Before` would fail a test): a
  violation is recorded (`StartOfRunSweep.OrderViolations`, a thread-safe list of `{test.TestDisplayName}` lines) and G3
  asserts that list is empty, so the order proof is a test, not an abort. Then `RecordTestStart(now)`. `Gate.Await()` sets
  `EndedAt` on EVERY path (the sweep's success, its error line, a caught exception) before it returns. So no test body on any thread
  starts before the sweep ended — even if a future xUnit builds the fixture later. `After` does nothing.
- `[assembly: Bench.Tests.Leftovers.StartOfRunOrder]` at the top of `StartOfRunOrderAttribute.cs`.

### 6.2 `StartOfRunSweep` (shape)

```csharp
public static class StartOfRunSweep
{
    internal sealed class Gate(Func<SweepResult> sweep)            // a seam: the tests make their own gate
    {
        private readonly Lazy<SweepResult> _once = …(() => Once(sweep), LazyThreadSafetyMode.ExecutionAndPublication);
        private long _endedAt; private int _runs;
        internal SweepResult Await() => _once.Value;
        internal long EndedAt => Volatile.Read(ref _endedAt);
        internal int Runs => Volatile.Read(ref _runs);
        // Once: Interlocked.Increment(ref _runs); try { return sweep(); }
        //       catch (Exception ex) { return new SweepResult([], [], [], [$"sweep failed: {FirstLine(ex.Message)}"]); }
        //       finally { Volatile.Write(ref _endedAt, Stopwatch.GetTimestamp()); }
    }
    private static readonly Gate Process = new(RealSweep);
    public static SweepResult Run() => Process.Await();
    public static long EndedAt => Process.EndedAt;
    public static int Runs => Process.Runs;                          // the counter the test reads
    public static long FirstTestStart => …;                          // coai C:`:51`
    internal static void RecordTestStart(long timestamp) { … }       // coai C:`:74-86`, verbatim
    private static SweepResult RealSweep() =>
        LeftoversSweepFixture.GuardedSweep(DarwinLayout.Require, () =>
            LeftoversSweep.Run(new SweepOptions(Owned.TempRoot(), DateTimeOffset.UtcNow, LeftoversSweep.MinAge, Liveness.ProbeProcess, Scope.IsMine)));
}
```
- The gate NEVER throws: a `Lazy` caches an exception and would then throw on every test's `Before`, which would fail the
  run (rule §4). The catch turns it into one `Errors` line; the fixture prints it as `  error: sweep failed: …`.
- `DarwinLayout.Require` is INSIDE the gate's sweep. Reason: if it failed in the fixture, the first `Before` would otherwise
  run the sweep without the layout check on macOS. `Require` throws again on every call when the layout does not hold
  (T:`DarwinLayout.cs:29-38`), so no sweep runs there; that is one error line, not a failed run.
- The real sweep's root is `Owned.TempRoot()` (the helper's one door; T:`Owned.cs:117-121`). Never `Path.GetTempPath()`
  in this file.

### 6.3 What it reports

At the start, on stderr, only when the sweep did something: `bench tests: leftovers — removed <n>, preserved <n>,
listed <n>`, then the listed lines (`foreign:`, `legacy:`, `unmarked:`, `notice:`), at most 20 `preserved: <path> —
<reason>` lines and one line for the rest, and the error lines (T:`LeftoversSweepFixture.cs`). It considers only this
user's `bench` owner form: `testsToDelete-modelarms-*`, `bench-*`, `fake-coai-data` and other legacy or foreign names are
not touched (D11).

### 6.4 Its tests (`StartOfRunSweepTests.cs`)

| # | Test | Asserts | RED |
|---|---|---|---|
| G1 | `TheGate_RunsTheSweepOnce_WhenEightThreadsArriveTogether` | a `Gate` whose sweep increments a counter and sleeps 300 ms; 8 dedicated `Thread`s (not the xUnit pool: CI has fewer threads) meet at a `Barrier(8)` and call `Await()`; each records `Stopwatch.GetTimestamp()` after it returns. Counter == 1; `gate.Runs` == 1; all 8 results are the same object (`ReferenceEquals`); every return stamp ≥ `gate.EndedAt`. Every wait has a ceiling (barrier and joins: 60 s) | by hand: replace the `Lazy` with an unsynchronised `if (_result is null) _result = Once(sweep);` → counter > 1 or a thread returns before the end; record; restore |
| G2 | `TheGate_NeverThrows_ASweepThatThrowsIsOneErrorLine_OnEveryThread` | the sweep throws `InvalidOperationException("boom\nsecond")`; 8 threads; each gets `Errors == ["sweep failed: boom"]`, nothing thrown; `Runs == 1`; `EndedAt != 0` | by hand: remove the catch → red (the `Lazy` rethrows on every thread) |
| G3 | `TheRealSweep_RanOnceInThisProcess_AndEndedBeforeTheFirstTestStarted` | `StartOfRunSweep.Runs == 1`; `EndedAt != 0`; `FirstTestStart > EndedAt`; one `TestContext.Current.AddWarning` with the margin in ms (coai's `TheSweep_EndedBeforeTheFirstTestStarted`). This is the "many test classes at once" proof in the real process (8 threads locally by `xunit.runner.json`; the vCPU count on CI) | by hand: make `Gate.Await()` call `Once(sweep)` on every call instead of `_once.Value` → in the whole run `Runs` > 1 (every test's `Before` sweeps) |
| G4 | `TheAssembly_RegistersTheSweepFixtureAndTheOrderProof` | the assembly's `AssemblyFixtureAttribute`s contain `typeof(LeftoversSweepFixture)`; exactly one `StartOfRunOrderAttribute` | by hand: remove the `[assembly: …StartOfRunOrder]` line → red |

## 7. The two-sweeps race test (F29), `TwoSweepsAtOnceTests.cs`

Copy C:`StartOfRunSweepRaceAndRootTests.cs` `:1-334` (the B8 test, the four `OnWindows_…` lock tests, the child test and
the two-process test) and its `Unlink` helper (`:590-604`). Do NOT copy B10, B11, B13, B13-W (`:336` onward): they are
coai's spec of its own sweep. Changes: namespace; class name `TwoSweepsAtOnceTests`; `using static
Bench.Tests.Leftovers.SweepPlants;`; the four environment names `COAI_E5_SWEEP_*` → `BENCH_E6_SWEEP_*` (C:`:257-260`);
the header comment names the source (coai path, `e060d77e`) and F29.

What it asserts (the plan's two F29 outcomes, `:232-233`): no removal reported that did not happen (each of 20 dead-claimed
folders removed exactly once across two sweeps; nothing foreign removed), and no folder restored without its claim (the
root is EMPTY afterwards: folders, claims and quarantines all gone). On POSIX the B8 test runs (rename has one winner;
an error line, if any, names the lost rename). The `OnWindows_…` tests skip off Windows with coai's reasons (C:`:124`,
`:149`, `:185`, `:221`, `:290`). Benchmark has no Windows CI leg (plan `:234-235`): they run where the owner runs the
suite on Windows.

The child test starts THIS executable with `--filter-method "*SweepChild_SweepsTheGivenRootOnce_WhenTheProcessTestStartsIt"`.
The child builds only the assembly fixtures (`LeftoversSweepFixture`); `PostgresFixture` is a collection fixture and is not
built for that filter. Without the variables the child test returns at once.

RED: on POSIX there is no defect to pin (`rename` has one winner): B8 is a characterisation, green on the template's
sweep and on the copy. Say so on the PR. The RED of F29 is coai's measurement on Windows (run 41873954643: both moves
succeeded in 39951 of 40000 races), cited, not repeated. If the owner runs the suite on Windows, the RED to watch there is
`OnWindows_TwoSweepsAtOnce_25Rounds_NeverInterleave` with op F2 taken out by hand.

## 8. Case 15 (row E6-c), `LeftoversSweepCase15Tests.cs`

**Why.** E6-c is "by reading, not run" (plan `:26`): `HasGitFile` (T:`LeftoversSweep.cs:470-480`) reads only the
candidate's OWN `.git` as a regular file, so the `other clone` rule (T:`:171-175`) does not fire for an owned folder that
HOLDS a clone and its linked worktree; the folder is quarantined and removed by `Removal.Quietly` →
`Directory.Delete(recursive)` (T:`Removal.cs:40-54`). Case 15 turns that reading into a test.

**Setup.** The class holds `private readonly OwnedScope _owned = DarwinLayout.FreshOwnedScope();` and disposes it. Each
test has its own private root `_owned.Dir("case15-…")`. Names come from `SweepPlants.Name(what)` (`Owned.NameBase +
what + "-abc123"`), never a spelled prefix. Dead owner: `SweepPlants.Dead()` (asserts the premise). Claim:
`SweepPlants.Rec(pid)`. Sweep options: `SweepPlants.Options(root, mine: Scope.IsMine)` — the REAL kernel-owner key, not
the `_ => true` seam.

`PlantGitRepoFolder(root, name, claim)` (a private helper in the file):
1. `mkdir <root>/<name>`; `git init -q <name>/repo` with `-c init.defaultBranch=main`; write one file; `git -C repo add`;
   `git -C repo -c user.name=bench-tests -c user.email=bench-tests@example.invalid -c commit.gpgsign=false commit -q -m one`
   (a real commit, so real read-only object files exist).
2. The linked worktree INSIDE the folder (D4: inside an owned folder, no prefix and no lock), the line above it carrying the
   exemption:
   ```csharp
   // testsToDelete-exempt: raw-worktree — case 15 makes this linked worktree inside the owned gitrepo folder it plants
   await Git(repo, "worktree", "add", "--detach", Path.Combine(dir, "scratch", id), "HEAD");
   ```
3. Write the claim `<dir>.owner`; then age the folder and the claim (`SweepPlants.AgeFolder(dir)`;
   `File.SetLastWriteTimeUtc(dir + ".owner", DateTime.UtcNow - SweepPlants.Old)`). Age LAST: the git commands made the
   folder new again.
4. Premises, asserted: `File.Exists(<dir>/scratch/<id>/.git)` is a FILE; `Directory.Exists(<dir>/repo/.git/worktrees)`;
   `git -C repo worktree list --porcelain` names `<dir>/scratch/<id>`.

Every `git` call goes through `ProcessRunner.RunAsync("git", args, cwd, TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken)`
(an argument array, never a shell string), as `GitCheckoutTests.cs:210-217` does.

**The tests.**

| # | Test | Asserts |
|---|---|---|
| 15a | `Case15_ADeadClaimedGitRepoFolder_WithALinkedWorktreeInside_IsRemovedWhole` | `result.Removed` equals `[dir]`; the folder and its claim are gone; no quarantine is left in the root; `result.Errors` empty; no `Preserved` entry for `dir` (so it was NOT kept as `other clone`); `UnmatchablePid.AssertNoSuchProcess(dead)` at the end |
| 15b | `Case15_ALiveClaimedGitRepoFolder_IsKept` | the same plant with `Rec(Environment.ProcessId)` (via `Owner.This with { Pid = … }`): kept with reason `live`; `<dir>/scratch/<id>/.git` and `<dir>/repo/.git` still exist; nothing removed |
| 15c | `Case15_AFolderWhoseOwnGitIsAFile_IsKeptAsOtherClone` | a repo planted in a SEPARATE owned folder of the scope (not swept), and a real linked worktree made at `<root>/<Name("wt")>` (top level of the swept root) with a dead claim beside it, aged: kept with reason `other clone`; still there. The `worktree add` line carries the same exemption form, naming the scope folder |

**Windows.** All three skip on Windows FIRST, before anything is made, with this reason: "git marks its object files
read-only, and the copied Removal (Directory.Delete recursive) does not clear that flag on Windows, so neither the sweep nor
the scope could remove the folder (by reading; coai's TempDir note measured it); benchmark's CI has no Windows leg". This
limit is a template finding: § 17.

**RED (B7), by hand, not committed, recorded on the PR:**
- R15-1 — a variant that keeps folders with a nested `.git`: in the copied `LeftoversSweep.cs`, make `HasGitFile(dir)` also
  return true when `Directory.EnumerateFileSystemEntries(dir, ".git", SearchOption.AllDirectories)` finds any entry.
  15a goes red: the folder is kept as `other clone`. This proves 15a tells the template's rule (own `.git` FILE only) from a
  wider one — the defect E6-c guards against (an owned folder never collected). Restore.
- R15-2 — remove the `if (HasGitFile(dir)) { … }` block (T:`:171-175`): 15c goes red (the dead-claimed top-level worktree
  is removed). Restore.
- During both, `TheAdaptedCopyTests` is red too; that is expected and is part of the record. A "sweep not yet wired" is NOT
  a useful RED: case 15 calls `LeftoversSweep.Run` directly on its private root, so wiring cannot change its result.
- The plan's Done-when says "was red before the copy's sweep existed" (`:238`). Before the copy, case 15 does not compile.
  R15-1 is the RED that shows the same thing for the right reason. Say so on the PR (Q3).

## 9. What never fails the run, and the exit paths

- The gate never throws (§ 6.2); the fixture's constructor never throws (T:`LeftoversSweepFixture.cs`, its `catch`); a
  sweep that cannot take the Windows lock lists one line and sweeps nothing (`SweepLock`); a report line that cannot be
  written is dropped (T: `Say`). The exit code is the tests'.
- The end-of-run audit DOES throw, on purpose: a run that leaves a folder it claimed fails (§ 11.1).
- A run killed by SIGKILL leaves its owned folders claimed. The next run's sweep collects them once they are 10 minutes old
  and their record is `gone` — on Linux and macOS, and on Windows except F31's condition and N1 (§ 17).

## 10. `TempDir` (the entry S3–S6 use)

```csharp
namespace Bench.Tests;
public sealed class TempDir : IDisposable
{
    private readonly Leftovers.OwnedDir _owned;   // one claimed folder testsToDelete-<owner>-bench-<what>-<id>
    public string Path => _owned.Path;
    public static string Root => Leftovers.Owned.TempRoot();   // the same string as the temp root (coai's shape G)
    public static TempDir For(string what) => new(Leftovers.OwnedDir.Create(what));
    public string At(params string[] parts) => System.IO.Path.Combine([Path, .. parts]);
    public static implicit operator string(TempDir d) => d.Path;
    public void Dispose() => _owned.Dispose();      // never throws (T:OwnedDir); a failure leaves it claimed, and the audit names it
}
```
`what` obeys the template's `CheckWhat` (length budget, no separators; T:`Owned.cs:135-140`). No `Path.GetTempPath()`,
no `CreateTempSubdirectory` (D5: one owned folder per helper, never a second top-level folder).

`TempDirTests.cs`: (T1) `For("tempdir-test")` makes a folder whose name matches `^` + `Regex.Escape(Owned.NameBase)` +
`tempdir-test-[A-Za-z0-9]{6}$`, with a claim beside it; (T2) a file and a subfolder inside are removed with it by
`Dispose`, and the claim goes too; (T3) `Root == Owned.TempRoot()`; (T4) `At("a", "b")` is under `Path`. RED by hand:
make `Dispose` do nothing → T2 red, and the end-of-run audit names the folder as `never disposed`; restore.

## 11. The audit and CI

### 11.1 The audit (already in the copied fixture; nothing new to write)

`LeftoversSweepFixture.DisposeAsync` runs after the last test: `RealAudit(Owned.TempRoot(), OwnedDir.Unreclaimed())`
(T:`:21`). It throws `LeftoversAuditException` when this PROCESS left a folder it claimed, and xUnit then fails the run. The
message (T:`LeftoversAuditException.cs:25-35`): first line `bench tests: this run left <n> folder(s) it claimed — <k> never
disposed, <k> disposed but not removed, <k> claimed in the temp root` (only the groups that are not empty); then one line
per folder, `  <group words>: <path>`; then `remedy: dispose the OwnedDir or OwnedScope that made each (a test class
field, disposed by xUnit); list what is left with: bash .agents/conventions/tools/janitor.sh --token bench --dry-run`. It
looks for this process's own record, never `TESTSTODELETE_RUN`'s. F31 limit: on Windows where new files are owned by
`BUILTIN\Administrators`, the claim scan does not see them.

### 11.2 `.github/workflows/ci.yml`, job `build-test` (`:13-43`) — two changes

1. **E6-j, the mount.** After the checkout step (`:17-19`) and before `actions/setup-dotnet` (`:21`), add the same line the
   `contract` (`:68`) and `plans` (`:120`) jobs use:
   ```yaml
      # The leftovers tests read the mounted conventions (the owner vectors, the `## Tokens` table, the janitor, the
      # sweeper classifier), and a missing file FAILS them — an uninitialised submodule is a broken setup, not a clean one.
      - run: git submodule update --init --depth 1 .agents/conventions
   ```
   No `npm ci`: none of the files the C# tests start (`leftovers-check.mjs`'s import chain, `lib/sweeper-scan.mjs`,
   `janitor.sh`) imports an npm package at load (checked: only `lib/rule-catalog.mjs` and a test file import
   `picomatch`/`yaml`, and `leftovers-check.mjs` requires `yaml` lazily). `dew_flow_mcp`'s CI runs the same tests at the
   same mount pin `d23a2ea` with only this line.
2. **B8, the audit's teeth.** After the `Test` step (`:42-43`), add the template's step with benchmark's names (from
   `dew_flow_mcp` `.github/workflows/ci.yml` at `21553ce`, lines 48-71): `MCP_LEFTOVERS_SELFTEST` → `BENCH_LEFTOVERS_SELFTEST`;
   `./tests/Mcp.Tests/bin/Release/net10.0/Mcp.Tests` → `./tests/Bench.Tests/bin/Release/net10.0/Bench.Tests`;
   `mcp tests:` → `bench tests:`; `-mcp-$what-` → `-bench-$what-` (twice: the grep and the `find`). Keep its comment
   block and its `set -o pipefail`. Measured in a throw-away copy: this step adds no finding to either Node guard profile.

### 11.3 Not in S2

The Node guard CI step and the C# repository guard test (S7); a Windows leg (none, plan `:234-235`).

## 12. `research/module_tests.md`

Add a section `## Test leftovers — the C# helper, the start-of-run sweep, the audit (E6)` before
`## What the harness does NOT prove` (`:629`). Prose and bullets. **Pitfall:** `ModuleTestsCatalogTests` parses every
markdown table row of this file whose first cell starts with a backticked `bench `, `model-arms ` or an HTTP verb
(`tests/Bench.Tests/Cli/ModuleTestsCatalogTests.cs:141`). Do not write such a row in the new section.

The section says, in short sentences:
- the helper: an adapted copy of `dew_flow_mcp` `tests/Mcp.Tests/Leftovers/` at `21553ce`, in `tests/Bench.Tests/Leftovers/`;
  the manifest and `TheAdaptedCopyTests`; "never edit a copied file — change the manifest by a reviewed op";
  `TempDir.For(what)` for the later stories;
- the start-of-run sweep: once per process, the gate every test waits on, what it collects (this user's `bench` owner
  form, dead record, 10 minutes) and what it lists; it never fails the run;
- the audit: what fails a run, its first line, the CI step;
- the limits: F29 closed by copy (`SweepLock`); F31 by its true condition (the sentence of § 5.1, word for word); the
  Windows read-only git objects (§ 8, by reading, to observe); the repository guard test is off until S7;
- the commands of § 14 that run these classes, and `--explicit only` for the guard.

## 13. The guards in S2 (measured for this brief)

- **The Node guard reads every text file under `tests/` with `--profile node`** (header of
  `.agents/conventions/tools/leftovers-check.mjs`), `.cs` included. It has NO allowance for a C# helper: its only helper
  file is `owned.mjs` (`:633`), and `FOLDING_ALLOWANCES` apply only to the conventions repository's own scan (header).
- **Measured** in a throw-away copy (`git archive HEAD` of `239e078e`, the mount copied in, `git init`, the 53 template
  files written with the namespace, token and path ops): `--profile node` **209 before, 209 after** — the copy adds no
  finding. Why: the template already passes its own guard — its one temp-root read carries an inline exemption
  (T:`Owned.cs:119`, `// testsToDelete-exempt: raw-temp — the helper is the one place a test learns the temp root`), and its
  fixtures spell the detector words in pieces. A planted `Path.GetTempPath()` in a `.cs` of the same folder WAS a finding
  (210), so the folder is read. The default profile stays **3** (it does not read `tests/` `.cs`).
- **The benchmark-own files** must keep that count at 209: the case-15 and 15c `worktree add` lines carry the inline
  `raw-worktree` exemption (§ 8); `TempDir` uses the helper; `SweepPlants`/the race test spell the quarantine word in two
  pieces as coai does. If the count after S2 is not 209, fix the NEW file (exemption with a real reason, or a rewrite).
  Never a baseline (D10), never an edit of a copied file.
- **The C# guard** (copied) is off in S2. Run it once by hand (§ 14) and record its finding count on the PR as information
  for S3–S6; it is expected to be red on today's tests (the Node guard finds 120 sites in `tests/Bench.Tests` today, plan § 3; the C# count is to be measured).

## 14. Commands (from the worktree root; WSL has no `.exe`, no Docker)

```bash
# build (PROJECT.md; one build at a time on this machine)
node .agents/conventions/tools/low-priority.mjs dotnet build dew_flow_benchmark.slnx -c Release -m:4
dotnet format dew_flow_benchmark.slnx --verify-no-changes --no-restore
git diff --check

T=./tests/Bench.Tests/bin/Release/net10.0/Bench.Tests
# the new classes first
node .agents/conventions/tools/low-priority.mjs $T --filter-class "*TheAdaptedCopyTests"
node .agents/conventions/tools/low-priority.mjs $T --filter-class "*StartOfRunSweepTests"
node .agents/conventions/tools/low-priority.mjs $T --filter-class "*LeftoversSweepCase15Tests"
node .agents/conventions/tools/low-priority.mjs $T --filter-class "*TwoSweepsAtOnceTests"
node .agents/conventions/tools/low-priority.mjs $T --filter-class "*TempDirTests"
# the whole copied namespace
node .agents/conventions/tools/low-priority.mjs $T --filter-namespace "Bench.Tests.Leftovers"
# the audit's teeth, by hand (expect a NON-zero exit and the first line "bench tests: this run left 1 folder(s) it claimed — 1 never disposed")
BENCH_LEFTOVERS_SELFTEST=1 node .agents/conventions/tools/low-priority.mjs $T --filter-class "*LeftoversSelfTest"; echo "exit $?"
BENCH_LEFTOVERS_SELFTEST=failed-dispose node .agents/conventions/tools/low-priority.mjs $T --filter-class "*LeftoversSelfTest"; echo "exit $?"
# the C# guard, by hand, information only (S7 turns it on)
node .agents/conventions/tools/low-priority.mjs $T --explicit only --filter-class "*LeftoversRepositoryGuardTests"
# the whole suite
node .agents/conventions/tools/low-priority.mjs $T
# the Node guard, before (at 239e078e) and after: expect 209 and 3, both times
node .agents/conventions/tools/leftovers-check.mjs --repo . --profile node --token bench --identity "the measuring harness" | head -1
node .agents/conventions/tools/leftovers-check.mjs --repo . --token bench --identity "the measuring harness" | head -1
# plans
node .agents/conventions/tools/plan-lifecycle.mjs
```
- Check `$T --help` once for the exact spelling of `--explicit` and `--filter-namespace` in this xUnit v3 build (the
  `explicit` option is present in `xunit.v3.core.mtp-v2` 4.0.3).
- **The whole suite locally:** the Postgres classes (`PostgresFixture`) fail without Docker on this machine, as on `main`.
  Run the whole suite once on `239e078e` and once on your head and compare the FAILED test lists: no new failure. The
  green whole suite is CI's `build-test` on `ubuntu-latest` (Done-when).
- Binaries must be the ones just built: build, then run; never run a suite over a stale `bin/`.

## 15. Size, the commit series, and how to split the coding

**One PR, one story.** Commit series (each commit builds and its new tests pass, except commit 1's intended RED):

1. RED — `template-manifest.json` (all 53 entries), `TheAdaptedCopyTests.cs`, the copied `RepositoryRoot.cs`, the
   `AllowUnsafeBlocks` line. `TheAdaptedCopyTests`: 52 rows red (target missing). Record the count.
2. GREEN — the 52 other copied files, `SweepLock.cs`, `StartOfRunSweep.cs`, `StartOfRunOrderAttribute.cs` (the copy cannot
   compile without the last three: the manifest ops call them). M1 green; the copied tests green; M2–M4 by hand.
3. `StartOfRunSweepTests.cs` (G1–G4, REDs by hand).
4. `SweepPlants.cs`, `SuiteChild.cs`, `TwoSweepsAtOnceTests.cs`.
5. `LeftoversSweepCase15Tests.cs` (R15-1, R15-2 by hand).
6. `TempDir.cs`, `TempDirTests.cs`, `WindowsNewFileOwnerLimitTests.cs` (add it to the own list written in commit 1).
7. `ci.yml`, `research/module_tests.md` (and `.gitattributes` if Q1 says yes).

**Size.** About 8,600 copied lines (mechanical, script-made) and about 1,300 lines of benchmark-own code, most of it
adapted from coai. One Sonnet coder can do it. If the coordinator wants it faster, split the CODING (not the story):
- **Coder A** — commits 1–2 (the copy, the manifest, the check, the wiring). Everything else depends on it.
- then in parallel on disjoint files, each in its own worktree branched from A's commit 2 (one `dotnet build` at a time on
  the machine: take turns):
  - **Coder B** — commits 3–4 (`StartOfRunSweepTests.cs`, `SweepPlants.cs`, `SuiteChild.cs`, `TwoSweepsAtOnceTests.cs`);
  - **Coder C** — commits 5–7 (`LeftoversSweepCase15Tests.cs`, `TempDir.cs`, `TempDirTests.cs`,
    `WindowsNewFileOwnerLimitTests.cs`, `ci.yml`,
    `research/module_tests.md`).
  B and C do not touch `TheAdaptedCopyTests.cs` (A wrote the whole own list) or the manifest.
- **One integration pass** (A, or the main session): cherry-pick B and C onto `feat/e6-s2-adapted-copy`, build, format,
  the namespace and the whole suite, the Node guard counts, the teeth by hand, the PR body.

## 16. Done when, and what the PR body quotes

- `TheAdaptedCopyTests` green; the manifest names `21553ce…`, 53 entries; M1 RED (commit 1) and M2–M4 reds quoted.
- G1–G4 green, with their by-hand REDs quoted; `StartOfRunSweep.Runs == 1` in the full run.
- Case 15 (15a–c) green on `ubuntu-latest`; R15-1 and R15-2 quoted.
- The race test: B8 green on Linux; the Windows tests skipped with their reasons (quote the skip lines).
- The audit's teeth: the CI step green on the PR's head (quote it), and the two local runs (exit codes and first lines).
- `build-test` checks out the mount and is green; the whole suite green on `ubuntu-latest`.
- Node guard: 209 and 3 before and after (quote both). The C# guard's by-hand count (information).
- The F31 pin compiled and skipped on `ubuntu-latest` with its reason (quote the skip line); it names coai's test as its source.
- G3 also asserts `StartOfRunSweep.OrderViolations` is empty; a by-hand RED records one violation (make `Before` skip the
  wait once) and G3 fails.
- `research/module_tests.md` section written; F31 stated by its true condition in the manifest's `about` and there.
- `dotnet format` clean; build at 0 warnings.

## 17. Notes for the parent (a finding about the template goes to the parent as a note, plan § 9)

- **N1 — Windows and read-only files (by reading, not measured here).** The template's `Removal.Quietly` removes a folder
  with `Directory.Delete(recursive: true)` (T:`Removal.cs:50-54`). On Windows that refuses a read-only file, and git
  marks its object files read-only, so a claimed folder that holds a clone is neither removed by its `Dispose` nor
  collected by the sweep there; it stays claimed and the audit names it. Coai measured the refusal itself
  (C:`src_mcp/tests_leftovers/TempDir.cs:17-21`). E6-c holds on Linux and macOS only until the template clears the flag
  (without following links). S5's `DatedGitRepo` meets the same limit on Windows.

## 18. Open questions (the coordinator answers; the brief's default is in brackets)

1. **Q1 — `.gitattributes`.** The diff check compares raw bytes. A Windows checkout with `core.autocrlf=true` makes every
   row red there. Coai and creds added one `-text` line per copied file. `.gitattributes` is not in S2's Files list
   (`:236-237`). [Default: add the 53 `-text` lines (coai's form, never a glob) and record it on the PR as an addition to
   the Files list; if the coordinator counts that as a plan change, leave it out and state the limit in
   `module_tests.md`.]
2. **Q2 — F31 pin.** Plan § 2 (`:54-55`) says "a pin of this limit in this suite reads who owns a new file, as coai #962's
   test does". S2's scenarios do not list it. [Default: not in S2; S7 or a later Windows run decides. Benchmark has no
   Windows leg, so such a pin would only run on the owner's machine.]
3. **Q3 — "red before the copy's sweep existed"** (`:238`). [Default: read as R15-1 (§ 8); stated on the PR; not a plan change.]
4. **Q4 — the start-of-run switch.** Coai's sweep has a list-only switch (its D11); benchmark's does not (B5). [Default: no
   switch; rule §4 asks the sweep to collect, and no E6 decision asks for one.]
5. **Q5 — merge order.** S1 is not merged at `239e078e`, and the Windows-machine statement (plan § 12 item 3) is open.
   Both block S2's merge, not its build.

## 19. The coordinator's decisions on the open questions (Opus main session, 2026-11-12)

1. `.gitattributes` `-text` lines for the copied files and the manifest: **yes**. The diff check compares raw bytes, so a
   line-end conversion on a Windows checkout must not change them. Recorded on the PR as a file outside S2's list, with
   this reason.
2. The F31 pin: **yes, in S2.** The plan's § 2 asks for "a pin of this limit in this suite [that] reads who owns a new file,
   as coai #962's test does, and skips with its reason where the caller owns it". S2 is where the helper is copied, so S2
   writes `WindowsNewFileOwnerLimitTests.cs` (§ 4.3): the check of coai's #962 test without its child run, its source
   named in its header; it skips on every OS but Windows, and on Windows where the caller owns a new file.
3. Case 15's RED is the mutation RED of § 8 (B7): accepted.
4. No start-of-run switch (B5): accepted.
5. Precondition 2 (§ 2) is MET: the owner stated on 2026-11-12 that the Windows machine builds and tests neither this
   repository nor rag_qln (`dew_flow_conventions · research/security/machines.md`, merged as #166, `113e9c22`).
   Precondition 1 stands: S1 (#205) merges first.
6. Note N1 (the template's `Removal` and read-only git objects on Windows) goes to the parent as a note; case 15 skips on
   Windows with that reason.

## 20. Plan gate (coai, 2026-11-12)

Round 1 on `a5531caa`: **proceed**, 2 of 2 reviewers, 4 findings, all accepted: the F31 pin is named in § 4.3, the own
list (twelve files), the commit series, the coders' split and the done-when; `Before` never throws, a violation is recorded
and G3 asserts none; `EndedAt` is set on every path of `Gate.Await()`; the coordinator's section is § 19 (it was a second
§ 17).
