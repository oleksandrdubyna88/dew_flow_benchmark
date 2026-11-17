# E6.S4 brief — `ModelArms/`: the owned folder and no legacy name (rows E6-i, E6-b by hand; claim C8)

> Status: **plan only, nothing implemented yet, 2026-11-17; the item 8.1 observations made before its plan gate (§ 11).** The brief of story S4 of `todo/PLAN_test_leftovers_E6.md` (§ 6 S4 `:470-483`; rows E6-i `:79` and E6-b `:66`; claim C8 `:137`). Its scope is S4's Files line, `tests/Bench.Tests/ModelArms/**`, as named paths (§ 0.1): a file outside them would be the parent's next counted change, which after entry 10.1 is the TENTH FULL STOP, and the work STOPS there. Three dispositions this brief needs are put to the coordinator before the gate (§ 14, Q1–Q3).
>
> Judge's brief (Opus) for a Sonnet coder. Written 2026-11-17. Read-only inputs, all verified for this brief: benchmark worktree
> `/home/jinx/git/wt/bench-e6s4`, branch `feat/e6-s4-modelarms` from `main` `b348bb33` (#219: E6.S3 merged; #218: F42-c, the
> adapted copy at mcp `53f65130`; #217: the stories amended to the parent's epoch 10), mount `.agents/conventions` at `b1746499`.
> The parent is `dew_flow_conventions · todo/PLAN_test_leftovers_rollout.md` at conventions `main` `87f065b` (§ *Epoch 10*:
> items 3.2 (C8), 8.1, 8.2, 8.3; ledger: entry 10.1 only, state open). "Stories" is `todo/PLAN_test_leftovers_E6.md`; every
> `file:line` is at `b348bb33`. **[M]** is measured for this brief, read-only; **[O]** a claim OBSERVED for this brief in a scratch
> clone, with the six things item 8.1 asks for (§ 11); **[R]** read from code and NOT run (never observed; written open-ended,
> "red or not, the run says which"); **[D]** taken from a named document.

## 0. What you build, in one paragraph

`TempFolder` (`tests/Bench.Tests/ModelArms/TempFolder.cs:10`) stops calling `Directory.CreateTempSubdirectory("testsToDelete-modelarms-")`
and makes its folder through the copied helper, `OwnedDir.Create("modelarms")`: the name `testsToDelete-<owner>-bench-modelarms-<id>`,
claimed first (`<folder>.owner` beside it), made exclusively with mode 0700, removed with its claim by the exact path. It keeps its
`OpenForDeleting` walk (the owned removal does not restore modes) and its whole public surface, so its 112 construction sites do not
change. Every other site under `ModelArms/` that either guard reports is cleared: the 15 `legacy-name` lines (and the 16 `owner-blind`
findings the copied C# guard gives on the same lines), the 14 raw temp API calls, the 17 `"/tmp"` path literals the C# guard reads,
the 10 real `worktree add` lines (one inline `raw-worktree` exemption each, naming the owned folder that holds the clone) and the 10
assertion strings that spell `worktree add` (the `~` precedent). Two new tests prove the folder: its name and claim, and that a
killed test process's folder is collected by the next sweep. ONE pull request, in this repository only.

## 0.1 Scope — S4's Files line as named paths, each with the dependency that needs it

The stories' S4 *Files* (`:482`) is `tests/Bench.Tests/ModelArms/**`. Every path below is inside it, except the plan records.

| Path | Needed for | Bound |
|---|---|---|
| `ModelArms/TempFolder.cs` | E6-i: the owned folder (K1) | § 4 row 1 |
| `ModelArms/TempFolderTests.cs` | scenarios (1), (2): name, claim, killed child (K7) | § 5 |
| `ModelArms/BriefFillerTests.cs` | `:156`, `:184`, `:231` legacy + raw temp (K2) | those three blocks |
| `ModelArms/InitVerbTests.cs` | `:138` legacy (K2); `:227` `"/tmp"` literal (K3) | those two lines |
| `ModelArms/ModelArmsProgramTests.cs` | `:83` legacy + raw temp (K2) | that block |
| `ModelArms/PinnedConsumer.cs` | `:109-110`, `:126-137` the kept scratch (K6) | `Preserved` and its summary |
| `ModelArms/PosixFolderIsolationTests.cs` | `:321` legacy (K2) | that block |
| `ModelArms/TimingLineTests.cs` | `:311-312` legacy + `"/tmp"` (K2, Q3) | those two lines |
| `ModelArms/KernelFactsTests.cs` | `:27`, `:135` raw temp (K4) | those two sites |
| `ModelArms/ValidateVerbTests.cs` | `:752`, `:755` raw temp (K4) | those two lines |
| `ModelArms/M2PilotScaffold.cs`, `ModelArms/M3PilotScaffold.cs` | `M2:39-40`, `:78`; `M3:34`, `:37`, `:105` (K5, Q1) | the names and their summaries |
| `ModelArms/{ArmChildEnvironmentTests,ArmGitTaskQuestionsTests,ArmGitWideningTests,ExecutableResolutionTests,PathEntryRefusalTests,RootFactsTests,StepLaunchTests,TargetEnvironmentTests,TaskWorktreesRoadTests}.cs` | the 17 `"/tmp"` literals (K3, Q2) | the lines of § 4's K3 table |
| `ModelArms/{ArmGitTaskQuestionsTests,TaskWorktreesPlantTests,TaskWorktreesReviewTests}.cs` | the 10 real `worktree add` lines (K8) | one marker line (or trailing marker) per line |
| `ModelArms/{TaskWorktreesReviewTests,TaskWorktreesRoadTests,TaskWorktreesTests}.cs` | the 10 assertion strings (K8) | one `static readonly` per file and the 10 lines |
| `ModelArms/ExperimentRoot.cs` | `:22` the doc comment that names the old folder form | that comment only |
| the plan records: this brief, its `todo/README.md` row, its promotion (`research/…`, `research/README.md`), the stories' progress line | `common/planning-docs.md` | Q5 |

**Not touched, by name:** `tests/Bench.Tests/Leftovers/**` (every manifest target, `template-manifest.json`, `TheAdaptedCopyTests.cs`,
`TempDir.cs`, `SuiteChild.cs`: used, never edited), `research/module_tests.md` (not on S4's Files line: Q5), `.github/workflows/ci.yml`,
`src/`, `hosts/`, `tools/` (`tools/model-arms/` is the product under test; nothing there changes), every other `tests/` folder (S5's).
**A file the build, a test or a check needs outside the table above is the parent's next counted change, the TENTH FULL STOP (entry
10.1 is taken): STOP, write the finding into this brief, and hand it to the coordinator; do not add the file.**

## 1. Rules you must read first

- `.agents/PROJECT.md` (commands; never `dotnet test`; exe + argv with a timeout; warnings are errors).
- `.agents/conventions/common/test-leftovers.md`, its detail (§1 the name and the last item: what lives INSIDE an owned folder
  needs no name of its own; §2 the helper, "never throws"; §4 the sweep's two keys; §7 the guard classes and the exemption grammar)
  and `common/testing.md` (RED first; revert the line the behaviour depends on).
- The stories: rows E6-b (`:66`), E6-i (`:79`); claim C8 (`:137`); D5 (`:249-252`), D11 (`:290-293`); § 5 (`:303`, S4 after S3);
  § 6 S4 (`:470-483`); § 7 (`:544-563`); § 11 (`:601-613`).
- The parent § *Epoch 10*: item 8.1 (the six things; a plant must be able to fail; an exclusion proved from both sides).
- The S3 brief `research/PLAN_test_leftovers_E6_S3_brief.md` § 11 (how an observation is written) and the S2 brief § 14 (commands).
- No Python anywhere. No Docker. No `git stash`. Commit only; the coordinator pushes and opens the PR.

## 2. Preconditions and merge conditions

1. S3 merged (#219, `b348bb33`), so F41-b (mount `b1746499`) and F42-c (the copy at mcp `53f65130`, `TheAdaptedCopyTests.cs:83`)
   are in: met **[M]** (`git log --oneline -1`; `git -C .agents/conventions rev-parse HEAD` → `b1746499…`).
2. Every claim this brief relies on is observed before its plan gate: § 11 (C8 and the helper calls). The stories' text stays as it is
   (it is written open-ended).
3. At most three open PRs here, or the owner's allowance for E6 PRs over the cap (2026-11-16).
4. CI checks of the head SHA exist and are green (`build-test`, `contract`, `plans`, `workflows`); the coai code gate and the Opus review
   are done; every RED of § 5 quoted on the PR.

## 3. Decisions of this brief (each with its reason)

- **K1 — `TempFolder` wraps `OwnedDir.Create("modelarms")`.** Field `private readonly OwnedDir _owned = OwnedDir.Create("modelarms");`
  (`OwnedDir.cs:35`, public; `using Bench.Tests.Leftovers;`), `Path => _owned.Path`. `Dispose`: `try { OpenForDeleting(); } finally
  { _owned.Dispose(); }`; `Open` walks `new DirectoryInfo(Path)`. Why `OwnedDir` and not `TempDir.For`: `TempDir` hides the
  `OwnedDir` it holds, and the mode walk must run first; both make the same folder. Why the walk stays: the owned removal
  (`Removal.Quietly`, `Removal.cs:18-39`) clears read-only ATTRIBUTES on Windows only and never restores POSIX modes, so a folder a test
  closed (`TempFolderTests.cs:29`, mode 0500) would be left claimed with a warning. The folder's mode is unchanged: `mkdir(…, 0700)`
  (`Native.cs:129`), as `CreateTempSubdirectory` makes it **[R]**. `"modelarms"` is 9 characters; the room under the longest owner is
  24 (`Owned.Room`, `Owned.cs:82`). **The one behaviour change:** a removal that fails no longer throws out of `Dispose`
  (`OwnedDir.Dispose` never throws, rule §2): it warns once, keeps the folder claimed, and the end-of-run audit
  (`LeftoversAudit`, run from `LeftoversSweepFixture.cs:63-75`) fails the run naming it (Q6).
- **K2 — The 15 legacy literals, site by site** (the stories' two dispositions: "moves to the helper", or the `~` precedent where the
  test is about the old name itself; no site here is about the old name, so none takes `~`):

| Site | Today | S4 |
|---|---|---|
| `TempFolder.cs:10` | `CreateTempSubdirectory("testsToDelete-modelarms-")` | K1 |
| `BriefFillerTests.cs:156`, `:184`, `:231` | `CreateTempSubdirectory("testsToDelete-briefs-")` in `try`/`finally` | `using var folder = new TempFolder();` and `folder.Path` for `folder.FullName`; the `finally` delete goes. `TempFolder`, not `TempDir`: `:180-184` lowers modes |
| `InitVerbTests.cs:138` | `Path.Combine(Cli.Repository.Root, "testsToDelete-modelarms-init-refused")`, a name that must NOT be created | `Path.Combine(Cli.Repository.Root, Owned.NameFor("init-refused"))`: the helper names it (owner form, never created; the `finally` removes it by that exact path if a regression made it) |
| `ModelArmsProgramTests.cs:83` | `Path.Combine(Path.GetTempPath(), "testsToDelete-modelarms-absent-root")` | `using var absent = new TempFolder();` and `absent.Combine("absent-root")`: a path inside an owned folder that does not exist |
| `PinnedConsumer.cs:129`, `:135` | the kept scratch moved beside the `TempFolder` | K6 |
| `PosixFolderIsolationTests.cs:321` | `Path.Combine(scratch, "testsToDelete-modelarms-" + …)` on the 9p mount `MODEL_ARMS_DRVFS_SCRATCH` | `using var owned = OwnedDir.Create("drvfs", scratch);` and `owned.Path` (claimed beside it on that mount; Q7) |
| `TimingLineTests.cs:311-312` | a sample path `"/tmp/testsToDelete-x_1/arm.d ~y"` and its slug, never created | a neutral sample `"/srv/x_1/arm.d ~y"` → `"-srv-x-1-arm-d--y"` (the same characters for the slug rule: a dot, an underscore, a space, a tilde; Q3) |
| `M2PilotScaffold.cs:39`, `:40`; `M3PilotScaffold.cs:37`, `:105` | `testsToDelete-m2-…`, `testsToDelete-m3-…` under the temp root, meant to OUTLIVE the test | K5, Q1 |

- **K3 — The 17 `"/tmp"` path literals the copied C# guard reads** (`TempFolderLiteral`, `LeftoversGuard.cs:137`, read as `raw-temp` in a
  test `.cs`, O3; the Node guard does not read them). Each is a string the test passes to a pure function or as a refused option, never
  a folder it makes. Rewritten to a path no guard reads, keeping what the line tests (no inline exemption: the stories' DoD lists
  exemptions by file and reason, and these would be 17 more; Q2):

| Line | Today | S4 | Keep |
|---|---|---|---|
| `ArmChildEnvironmentTests.cs:185`, `:186`, `:188` | `"/tmp/claude-1000"`, `"/tmp/other"` | `"/scratch/claude-1000"`, `"/scratch/other"` | any parent `TMPDIR` is replaced |
| `ArmGitTaskQuestionsTests.cs:87`; `ArmGitWideningTests.cs:50` | `"--output=/tmp/x"` (a refused option) | `"--output=" + _temp.Combine("x")` at `:87`; at `:50` (an `[InlineData]` constant) `"--output=x"` | refused before git runs; if the refusal regressed, git writes inside the owned folder, never the temp root |
| `ExecutableResolutionTests.cs:124`, `:125`, `:127`, `:130` | `"/tmp\0x"` (a root holding a NUL) and `.Contain("tmp\\0x")` | `"/srv\0x"` and `.Contain("srv\\0x")` | the NUL refusal; `"/t"` at `:125` stays (a prefix of neither) |
| `InitVerbTests.cs:227` | `"/tmp/a\0b"` | `"/srv/a\0b"` | the NUL refusal |
| `PathEntryRefusalTests.cs:77` | `Refusal("/tmp/bin", roots: ["/t/"])` | `Refusal("/tea/bin", roots: ["/t/"])` | the prefix trap: `/tea` starts with `/t`, is not under `/t/` |
| `RootFactsTests.cs:243` | `"/tmp/experiment\0.json"` | `"/srv/experiment\0.json"` | the NUL refusal |
| `StepLaunchTests.cs:131`; `TargetEnvironmentTests.cs:23`, `:33` | `TMPDIR` values `"/tmp/claude-1000"`, `"/tmp/op"` | `"/scratch/claude-1000"`, `"/scratch/op"` (`:23` and `:33` together) | a parent value carried or replaced |
| `TaskWorktreesRoadTests.cs:27` | `SSH_AUTH_SOCK = "/tmp/agent.sock"` | `"/run/agent.sock"` | a socket variable dropped |
| `TimingLineTests.cs:311` | K2 | K2 | — |

  Each changed assertion is read once more against the code it tests; a test whose meaning depended on `/tmp` itself (none found by
  reading **[R]**) is a STOP and a report, not an exemption.
- **K4 — The raw temp API calls that are not names.** `KernelFactsTests.cs:27`: the `stat` witness's working directory
  `Path.GetTempPath()` → `TempDir.Root` (coai's shape G: the same string, from the helper's one door, `TempDir.cs:24`; clean in both
  guards, O6). `KernelFactsTests.cs:135`: `CreateTempSubdirectory("birth-")` + `finally` → `using var parent = new TempFolder();`.
  `ValidateVerbTests.cs:752`, `:755` (paths in an experiment that is only read, never made) → `TempDir.Root`. `PinnedConsumer.cs:134`
  goes with K6. `M2:78` and `M3:34` go with K5.
- **K5 — The M2 and M3 pilot scaffolds (`Explicit = true`; the suite never runs them).** What they make is MEANT to outlive the test
  (`M3PilotScaffold.cs:9`: "the pilot's last step removes it"; M2's target is printed for the pilot, `:54`, and removed only when the scaffold fails, `:62-66`). Rule §1's *Never*: "never
  give the prefix to something that must survive the run", and the owner form would be wrong in a worse way: a claimed folder whose
  process (the scaffold's test run) has ended is `gone` and is collected by the next run's sweep ten minutes later, under an attended
  pilot. So the recommendation (Q1) is the rule's own: the names lose the prefix — `bench-m2-target-<id>`, `bench-m2-root-<id>`,
  `bench-m3-<what>-<id>`, `bench-m3-manifest-<id>.json`, `bench-<pilot>-probe-<id>` — built on `TempDir.Root`; the manifest, its
  exclusive create and its by-name teardown stay; the probe of `M2:78` (removed in `finally`, so a real test temp) becomes a
  `TempFolder`. Neither guard reads a path built on `TempDir.Root` (O6), so the summaries say: kept by design, removed by the pilot's
  manifest, invisible to both guards (a limit, as shape G is).
- **K6 — `PinnedConsumer`'s kept scratch moves INSIDE the run's owned folder** (the stories' S4 *What*). The scratch is already inside
  one: readers run in `ExperimentRoot.Scratch(…)` = `_temp.Folder(…)` (`ExperimentRoot.cs:157`; `ConsumerReaders.cs:48`), HOME and
  the working directory are that scratch, the copy is inside it (`ConsumerReaders.cs:78-79`, `PinnedConsumer.cs:110-112`) **[R]**.
  `Preserved` (made `internal static` for its test) walks up to the folder whose name matches `Owned.FullName()` (`Owned.cs:95`) and
  moves the scratch to `<that folder>/kept-reader-<name>-<id>`: a plain name, inside, removed with the folder. The timed-out reader's
  tree is already killed by `ProcessRunner` before the move **[R]**. The summary (`:109-110`) says so, and that the failure message
  (with `stderr so far`) is the record. No walk reaches the temp root: no folder matching → the scratch stays where it is (never
  `Path.GetTempPath()`).
- **K7 — The killed child (rule §4: dead record).** A child of this executable (`SuiteChild.StartInfoFor(Environment.ProcessPath!, …)`,
  `SuiteChild.cs:44`, with `BENCH_E6S4_KILL_CHILD_OUT=<TempDir>/child.txt` and `TESTSTODELETE_RUN` REMOVED (`null`), so the claim
  records the child itself, `Owner.cs:27-37`) runs only `*KillChild_makes_a_TempFolder_and_waits_to_be_killed`, which returns at once
  when that variable is unset (green in every normal run); it makes a `TempFolder`, writes its path (temp file, then `File.Move`), and
  waits up to 120 s. The parent reads the path, checks folder and claim exist, `Kill()`s the child (SIGKILL), waits for its exit, then
  runs ONE sweep of the real temp root restricted to that folder through the kernel-owner seam:
  `LeftoversSweep.Run(new SweepOptions(TempDir.Root, DateTimeOffset.UtcNow, TimeSpan.Zero, Liveness.ProbeProcess, Mine: p => (p == folder || p == folder + ".owner") && Scope.IsMine(p)))`
  (`LeftoversSweep.cs:51-52`, `:84`; `Liveness.cs:36`). It asserts `Removed` holds the folder and both entries are gone. `finally`:
  kill the child's tree; remove the folder and claim by exact path if still there. Skips on Windows (SIGKILL; F31). No `TempFolder`
  parent seam: the `Mine` predicate keeps the sweep off every other entry (reported as foreign, never removed) (Q8).
- **K8 — `worktree add`: 10 real lines, 10 assertion strings** (row E6-b; claim C8, observed § 11 O1–O2). Each real line gets one
  marker, on its own line directly above it or trailing on the same line, never two lines above (that does not clear, O2):
  `// testsToDelete-exempt: raw-worktree — inside the owned folder <x> that holds the clone`, `<x>` named per file:
  `ArmGitTaskQuestionsTests.cs:228`, `:234`, `:242`, `:304`, `:305`, `:307`, `:421`, `:438` → `_temp (the ArmGitTestBase TempFolder)`
  (`Worktree(clone, name)` is the clone's sibling, `:16`; the clone is `_temp.Path/clone`, `ArmGitTestBase.cs:13`, `:35-41`);
  `TaskWorktreesPlantTests.cs:47` → `kit.Temp (the WorktreeKit TempFolder)` (`live` is `kit.Temp.Combine("live-wt")`, `:46`;
  `WorktreeKit.cs:36`; ONE marker clears both `"worktree", "add"` of that ternary line, O2 (e));
  `TaskWorktreesReviewTests.cs:27` → `the ExperimentRoot TempFolder of kit.Root` (`kit.Wt(id)` is `Paths.Worktree(id)` under
  `kit.Root.Root` = `_temp.Folder("example", "root")`, `WorktreeKit.cs:38-40`, `:54`; `ExperimentRoot.cs:44`). The 10 assertion strings (`TaskWorktreesReviewTests.cs:40`, `:73`,
  `:91`; `TaskWorktreesRoadTests.cs:79`; `TaskWorktreesTests.cs:70`, `:190`, `:265`, `:455`, `:522`, `:570`) take the `~` precedent
  (coai `src_mcp/tests/Leftovers/LeftoversBaselineTests.cs:256-260`, at coai `origin/main` `e829bba8`): per file ONE
  `private static readonly string WorktreeAdd = "work~tree add".Replace("~", string.Empty, StringComparison.Ordinal);` and each line
  interpolates `{WorktreeAdd}` or passes it — never a marker. Both forms clean in both guards (O2).

## 4. The changes, file by file

1. `TempFolder.cs` — K1. The summary (`:5-7`) says: the owned folder `testsToDelete-<owner>-bench-modelarms-<id>`, claimed beside it, made
   by the copied helper (plan D5); `Dispose` opens every closed folder, then removes folder and claim by the exact path and never throws
   (a removal that gives up is named by the end-of-run audit).
2. `TempFolderTests.cs` — scenarios (1) and (2) of § 5 beside the existing test.
3. The K2, K3, K4, K5, K6 and K8 sites, each exactly as its table says.
4. `ExperimentRoot.cs:22` — the doc comment's folder form.

Nothing else. Every test of the namespace keeps its name and its assertions, except the changed literals of K3 (their meaning kept).

## 5. The scenarios — RED, GREEN, RED again

"RED again" is the defect put back once after GREEN, by hand, and quoted on the PR (the stories' § 7).

| # | Test (file) | GREEN asserts | RED (first) / RED again |
|---|---|---|---|
| (1) E6-i | `A_TempFolder_is_an_owned_folder_claimed_beside_it_and_removed_with_its_claim` (`TempFolderTests`) | the leaf matches `Owned.FullName()` and holds `-modelarms-`; `<path>.owner` exists and `Owner.Parse` of its text equals `Owner.RunRecord()`; the folder's mode is 0700 (Linux); after `Dispose` neither folder nor claim exists | RED first: on today's `TempFolder` (legacy name, no claim) the name and claim assertions fail. RED again: put `:10` back → red |
| (2) rule §4 | `A_TempFolder_of_a_killed_test_process_is_collected_by_the_next_sweep` and its child `KillChild_makes_a_TempFolder_and_waits_to_be_killed` (`TempFolderTests`) | K7: before the kill the folder and its claim exist; after SIGKILL the sweep's `Removed` holds the folder, folder and claim are gone; skipped on Windows with its reason | RED first: on today's `TempFolder` the child's folder is `testsToDelete-modelarms-…`, which the sweep LISTS as legacy and never removes (rule §1) → red naming it. RED again: put `:10` back → red |
| (3) | `Dispose_removes_a_folder_holding_a_link_to_its_own_parent_and_a_folder_closed_to_its_owner` (existing, ten rounds) | still green: the mode walk runs before the owned removal | RED again: drop `OpenForDeleting()` from `Dispose` → the closed folder stays, the round fails (`Directory.Exists`) |
| (4) K6 | `A_timed_out_readers_scratch_is_kept_inside_the_owned_folder` (`ConsumerCompatibilityTests` or a new `PinnedConsumerTests` in `ModelArms/`) | `Preserved` over `<TempFolder>/readers/x` returns a path under `TempFolder.Path` named `kept-reader-…`, the scratch's files moved with it; with no owned folder above, the scratch stays put | RED first: today's `Preserved` returns a sibling of the `TempFolder` in the temp root → red |
| (5) E6-b | the guards over the head, by hand (§ 6) | no finding under `ModelArms/` in either guard; the 10 markers valid | RED again: remove ONE marker (`ArmGitTaskQuestionsTests.cs:228`'s) → one `raw-worktree` at that line in each guard; put a `"worktree add"` assertion string back → one in each |
| (6) | the ModelArms namespace, then the whole suite | `--filter-namespace "Bench.Tests.ModelArms"` green; its time against the base (§ 11 O8: 2100 tests, 2 m 10.7 s) recorded on the PR, growth within the claim's cost; the whole suite's FAILED list equal to the base's | — |

## 6. The guards in S4

- **The Node guard** (`--profile node --token bench --identity "the measuring harness"`): base **209** with **49** under `ModelArms/`
  (15 `legacy-name`, 14 `raw-temp`, 20 `raw-worktree`, § 11 O4). Expected on the head: **160**, none under `ModelArms/`; the
  default profile **3**, unchanged.
- **The copied C# guard by hand** (`--explicit only --filter-class "*LeftoversRepositoryGuardTests"`, information; S7 turns it on): base
  **173** findings over 1320 files, **82** under `ModelArms/` (15 `legacy-name`, 16 `owner-blind`, 31 `raw-temp`, 20 `raw-worktree`,
  § 11 O3). Expected on the head: **91**, none under `ModelArms/`. A count that differs is read finding by finding before anything else.
- Watch for, in new lines: the prefix word in any literal (the C# guard reads `ToDelete` alone as a piece, O5), `"/tmp"` in a literal,
  `Owned.TempRoot` outside `Leftovers/` (`raw-temp`, O6; use `TempDir.Root`), `Owned.Prefix` outside `Leftovers/` (`owner-blind`), a
  marker two lines above its line.

## 7. Commands (from the worktree root; WSL, no `.exe`, no Docker; one build at a time)

```bash
nice -n 19 dotnet build dew_flow_benchmark.slnx -c Release -m:4 -p:AppHostRuntimeIdentifier=linux-x64 --disable-build-servers
nice -n 19 dotnet format dew_flow_benchmark.slnx --verify-no-changes --no-restore
git diff --check
T=./tests/Bench.Tests/bin/Release/net10.0/Bench.Tests
nice -n 19 $T --filter-class "*TempFolderTests"
nice -n 19 $T --filter-namespace "Bench.Tests.ModelArms"          # time it; compare with O8
nice -n 19 $T --filter-class "*TheAdaptedCopyTests"                # untouched copy: still green
nice -n 19 $T --explicit only --filter-class "*LeftoversRepositoryGuardTests"   # information: 91 expected
nice -n 19 $T                                                       # the whole suite: compare the FAILED list with the base's
node .agents/conventions/tools/leftovers-check.mjs --repo . --profile node --token bench --identity "the measuring harness" | head -1   # 160
node .agents/conventions/tools/leftovers-check.mjs --repo . --token bench --identity "the measuring harness" | head -1                  # 3
ls -d /tmp/testsToDelete-*-bench-modelarms-* /tmp/testsToDelete-modelarms-* 2>/dev/null | wc -l   # before and after a namespace run: no growth
node .agents/conventions/tools/plan-lifecycle.mjs
```

## 8. The commit series

**One PR, one story.** Each commit builds and its tests pass.

1. `TempFolderTests` (1) and (2) committed with a skip-free RED observed by hand on today's `TempFolder` (quoted), then `TempFolder.cs`
   (K1) in the same commit; (3) re-run.
2. The legacy sites of K2 (not M2/M3) and K6 with (4).
3. K4 and the 17 literals of K3.
4. K5 (M2/M3), as Q1 is answered.
5. K8: the 10 markers and the 10 `~` strings; § 6's counts on the head.
6. `ExperimentRoot.cs:22`; this brief's progress line.

**Size.** About 400 changed lines in about 30 files, most of them one-line edits; the two new tests ~150 lines. One Sonnet coder.

## 9. Done when, and what the PR body quotes

- (1), (2), (4): GREEN, each RED first and RED again quoted; (3) green and its RED again quoted.
- § 6: the Node guard 160 / 3, the C# guard 91, none under `ModelArms/` in either; the marker RED of (5) quoted from each guard.
- The ModelArms namespace green with its time beside O8's; the whole suite's FAILED list equal to the base's; no growth of
  `/tmp/testsToDelete-*modelarms*` across a namespace run (O9).
- 0 warnings; `dotnet format` clean; `TheAdaptedCopyTests` green (no manifest target touched); `plan-lifecycle.mjs` clean;
  `git diff --name-only b348bb33..HEAD` within § 0.1.

## 10. What this story does NOT prove

The 9p half (`PosixFolderIsolationTests.cs:321` runs only with `MODEL_ARMS_DRVFS_SCRATCH`, skipped everywhere else); Windows (no leg);
the M2/M3 scaffolds (Explicit, never run by the suite: their change is read, not run, unless the coordinator asks for one run); that a
`worktree add` marker's named folder really holds the clone (a guard reads the spelling, never the folder: the code review does).

## 11. Observations — made before this brief's plan gate (the parent's item 8.1)

**Common to all rows:** consumer **benchmark `b348bb33`** (`main`: #219, #218, #217), cloned (`git clone --no-hardlinks`) from this
worktree into a scratchpad folder outside every worktree and checked out at that SHA, the mount copied in as files; **upstream pin mcp
`53f65130`** (the copy's manifest commit, `TheAdaptedCopyTests.cs:83`) and the mount **`b1746499`** (with its `node_modules`); Node
`v26.12.1`, .NET SDK `10.0.114`, WSL `Ubuntu`, uid 1000, no Docker. One scratch file was added to the clone and never committed:
`tests/Bench.Tests/Infrastructure/S4ScratchObservations.cs` (two facts: `Obs_Repository` calls `LeftoversGuard.Scan(root, root)` over the
clone and writes every finding; `Obs_Plants` calls `LeftoversGuard.Scan(tree, tree)` over each private tree). Build: `nice -n 19 dotnet
build tests/Bench.Tests/Bench.Tests.csproj -c Release -m:4 -p:AppHostRuntimeIdentifier=linux-x64 --disable-build-servers` → 0
warnings, 0 errors, 1 m 29 s. Run: `nice -n 19 ./tests/Bench.Tests/bin/Release/net10.0/Bench.Tests --filter-class
"*S4ScratchObservations"` (2 of 2 passed). The private trees were written by a scratchpad Node script, one tree per plant, each holding
ONE file at the fixture path named in the row, the plant inside a minimal class body; the same script ran the mounted guard's
`scanText(<fixture path>, <text>)` and `isScanned(<fixture path>, 'node')`. The scratch is not kept (a reboot clears it); the results
are here.

| # | Claim (where) | Invocation | Fixture path, plant | Expected | Observed |
|---|---|---|---|---|---|
| O1 | **C8**, both halves (stories `:137`, `:66`): the C# copy reads `worktree add` only outside `src/` | `Scan` and `scanText`/`isScanned` | (a) `tests/Bench.Tests/ModelArms/ArmGitTaskQuestionsTests.cs`: `await TargetBench.GitAsync(clone, "worktree", "add", "-q", plain, "-b", "tmp/plain", "HEAD");` (b) `tests/Bench.Tests/Infrastructure/HarvestGatesTests.cs`: the same line (S5's kind) (c) `src/Bench.Infrastructure/Git/ScratchWorktree.cs`: `new[] { "worktree", "add", "--detach", "--force", scratch, at }` and the string `"git worktree add --detach"` (S5's seam) (d) `tools/model-arms/ModelArms/Adapters/ArmGit.cs`: the product's argv and the same words | (a), (b) red in both; (c) not read by the C# copy (**[R]** `HitsOutsideSource`, `LeftoversGuard.cs:205-213`), not scanned by Node; (d) not read by either | (a) C# 1 `raw-worktree` (`"worktree", "add"`), Node 1; (b) C# 1, Node 1; (c) C# scanned 1 file, **0** findings; Node `isScanned` **false** (its `scanText` alone would report 2, so the zero is the walk's, not the pattern's); (d) C# scanned **0** files (not discovered before S7's operation, claim C10), Node `isScanned` **false**. **C8 holds as written**, and `tools/` is read by neither today |
| O2 | E6-b's marker and the `~` precedent (S4 *What*; K8) | `Scan` and `scanText` | `tests/Bench.Tests/ModelArms/…`: (a) O1's line with `// testsToDelete-exempt: raw-worktree — inside the owned folder TempFolder _temp of this test` on the line above (b) the same marker trailing the line (c) the marker with the reason `needed` (d) the marker two lines above (a line between) (e) the ternary line of `TaskWorktreesPlantTests.cs:47` (two `"worktree", "add"`; planted at the path `…/TaskWorktreesReviewTests.cs`, the same kind) under one marker (f) `$"worktree add -q --no-checkout {kit.Wt("t1")} tmp/t1"` (g) `var add = "work~tree add".Replace("~", string.Empty, StringComparison.Ordinal);` with `$"{add} -q …"` (h) a `StartsWith(Add, …)` over a `"work~tree add".Replace(…)` field | (a), (b), (e), (g), (h) clean; (c) `exemption` + `raw-worktree`; (d) and (f) red | both guards alike: (a) **0**, (b) **0**, (c) 1 `exemption` ("carries no reason") at the marker + 1 `raw-worktree` at the line, (d) **1** `raw-worktree` (a marker clears its own line and the next only), (e) **0**, (f) 1 `raw-worktree` (`worktree add`), (g) **0**, (h) **0** |
| O3 | the C# copy on `ModelArms/` today (E6-i; the stories count the Node guard only) | `Obs_Repository`: `Scan(clone, clone)` | the clone at `b348bb33` | — | scanned **1320**, findings **173** (F42-c's count, S3 brief § 9); **82** under `ModelArms/`: 15 `legacy-name` (the Node guard's 15 lines), **16 `owner-blind`** (`testsToDelete-mo`, `-br`, `-m2`, `-m3`, `-x_`, `-x-`, `-{p`: the same 15 lines and `M2PilotScaffold.cs:78`), **31 `raw-temp`** (the Node guard's 14 API calls + **17 `"/tmp"` literals**: K3's table), 20 `raw-worktree` (the Node guard's 20 lines) |
| O4 | the Node guard on `ModelArms/` today | `node .agents/conventions/tools/leftovers-check.mjs --repo . --profile node --token bench --identity "the measuring harness"`, in this worktree | the repository at `b348bb33` | 209 (S3 brief O13) | **209**; **49** under `ModelArms/`: 15 `legacy-name` (`BriefFillerTests.cs:156`, `:184`, `:231`; `InitVerbTests.cs:138`; `M2PilotScaffold.cs:39`, `:40`; `M3PilotScaffold.cs:37`, `:105`; `ModelArmsProgramTests.cs:83`; `PinnedConsumer.cs:129`, `:135`; `PosixFolderIsolationTests.cs:321`; `TempFolder.cs:10`; `TimingLineTests.cs:311`, `:312`), 14 `raw-temp` (`BriefFillerTests.cs:156`, `:184`, `:231`; `KernelFactsTests.cs:27`, `:135`; `M2PilotScaffold.cs:39`, `:40`, `:78`; `M3PilotScaffold.cs:34`; `ModelArmsProgramTests.cs:83`; `PinnedConsumer.cs:134`; `TempFolder.cs:10`; `ValidateVerbTests.cs:752`, `:755`), 20 `raw-worktree` (K8's lines); default profile **3**, none under `ModelArms/` |
| O5 | K2: no site needs `~` for the prefix; if one ever does, which spelling | `Scan` and `scanText` | `tests/Bench.Tests/ModelArms/X.cs`: (a) `"tests~ToDelete-modelarms-".Replace("~", …)` (b) `"tests~To~Delete-modelarms-".Replace("~", …)` | — | (a) C#: 1 `owner-blind` (`ToDelete (a piece of the prefix word)`, `LeftoversGuardLiterals.cs:61-64`), Node `[]`; (b) **0** in both |
| O6 | the helper calls S4 uses, in a `ModelArms/` `.cs` (K1, K2, K4, K5, K6) | `Scan` and `scanText` | at the files of § 4: `private readonly OwnedDir _folder = OwnedDir.Create("modelarms");`; `using var folder = TempDir.For("briefs");` and `new TempFolder()`; `Path.Combine(Cli.Repository.Root, Owned.NameFor("init-refused"))`; `Path.Combine(owned, $"reader-{…}-{…}")` and `Owned.FullName().IsMatch(owner.Name)`; `ProcessRunner.RunAsync("stat", …, TempDir.Root, …)`; `OwnedDir.Create("drvfs", scratch)`; `Path.Combine(TempDir.Root, $"bench-m2-target-{id}")`; M3's names on `TempDir.Root`; `TempDir.Root.Replace(…)` (ValidateVerb); the neutral TimingLine sample. Controls: `Owned.TempRoot()`; today's `TempFolder.cs:10`; today's TimingLine lines; `"/tmp/claude-1000"` | every planned line clean; controls red | every planned line **0** in both. Controls: `Owned.TempRoot()` → C# 1 `raw-temp` ("outside the helper's folder"), Node `[]`; today's `TempFolder.cs:10` → C# `legacy-name` + `owner-blind` + `raw-temp`, Node `legacy-name` + `raw-temp`; today's TimingLine → C# `legacy-name` + `owner-blind` + `raw-temp` (`"/tmp"`) on `:1`, `legacy-name` + `owner-blind` on `:2`, Node `legacy-name` on each; `"/tmp/claude-1000"` → C# 1 `raw-temp`, Node `[]` |
| O7 | the helper's signatures at `main` (K1, K4, K6, K7) | read **[R]** at `b348bb33` | `tests/Bench.Tests/Leftovers/` | — | `OwnedDir.Create(string what, string? parent = null, Action<string>? warn = null)` public (`OwnedDir.cs:35`), `Path`, `Dispose` never throws (`:65-72`); `TempDir.For(what)`, `TempDir.Root`, `At` (`TempDir.cs:24-33`, namespace `Bench.Tests`); `Owned.NameFor(what)` (`Owned.cs:126`), `Owned.FullName(owner?)` (`:95`), `Owned.Room` (`:82`); `LeftoversSweep.Run(SweepOptions)` (`LeftoversSweep.cs:84`) with `SweepOptions(Tmp, Now, MinAge, Probe, Mine?, Hooks?, SweepLockWait?)` (`:51-52`); `Liveness.ProbeProcess` public (`Liveness.cs:36`); `SuiteChild.Run`/`StartInfoFor` (`SuiteChild.cs:21`, `:44`); the folder made `mkdir 0700` on Linux (`Native.cs:129`). All compiled in O1's build (the scratch file uses `LeftoversGuard.Scan`; the rest are read) |
| O8 | the base time of the namespace (S4 *Scenarios*: "its time does not grow by more than the claim's cost") | `nice -n 19 $T --filter-namespace "Bench.Tests.ModelArms" --no-ansi` under `/usr/bin/time -v`, in the clone | `b348bb33` | — | **2100** tests, 0 failed, 2094 passed, 6 skipped; duration **2 m 10.7 s** (wall 2:11.61, max RSS 889 MB); `/tmp/.git` absent before and after |
| O9 | no `ModelArms` folder left by a namespace run (the stories' E6-k kind, for this folder) | `ls -d /tmp/testsToDelete-modelarms-* \| wc -l` before and after O8 | the machine's `/tmp` | — | **0** and **0** |

**Written open-ended, not observed here (red or not, the PR's run says which):** (1) and (2) on today's code (their RED) and on the
head; the namespace time on the head; the 9p site (Q7); the whole-suite FAILED list on the head.

## 12. The `/tmp/.git` finding (asked by the coordinator; recorded, not fixed)

**What refuses.** `RootFacts.GitTopOf` (`tools/model-arms/ModelArms/Adapters/RootFacts.cs:19-23`) is the nearest ancestor-or-self that
holds a `.git` FILE or FOLDER (`Path.Exists`); with `/tmp/.git` present, every experiment root under `/tmp` is "inside the git checkout
/tmp" (`ExperimentFile.cs:70`). The scouts' roots refuse the same way (`ScoutRoots.cs:67`, `ProbeRoots.cs:41`,
`FileSystemGateArtifactStore.cs:45` name the checkout they found). So one `.git` entry in the temp root, made by anything, makes those
tests refuse for as long as it exists.

**What was searched, in benchmark at `b348bb33` (read, not run):**
- every `git init` argv in `tests/`, `src/`, `hosts/`, `tools/` (`git grep -n -E '\["init"|"init", "-|, "init"' -- '*.cs'`, 40 sites):
  each runs in a folder made first, below a `TempFolder`, an owned folder, a `bench-*` folder or a product root
  (`TargetBench.cs:214-218`, `StepBench.cs:243-245`, `ExperimentRoot.cs:75`, `:172`, `ArmGitTestBase.cs:48`, `:66`,
  `DatedGitRepo.cs:40-41`, `GitCheckoutTests.cs:167-170`, `ScoutTrees.cs:271-288` with `ScoutAccessPlanting.cs:48`, the copied
  `OwnedTests.cs:27` and `LeftoversSweepCase15Tests.cs:116`); none has the temp root as its folder;
- every `.git` written by hand (`git grep -n -E 'CreateDirectory\(.*"\.git"\)|Combine\([^)]*"\.git"\)' -- tests`, 15 writing sites):
  each below a test folder;
- the temp root as a WORKING DIRECTORY: `ProcessRunnerTests.cs` (git `cat-file -e`, `rev-parse --verify`, `hash-object --stdin`
  without `-w`, `--version`: none writes), `KernelFactsTests.cs:27` (`stat`), `ScoutLaunchProcessTests.cs:107` (the fake CLI, which
  runs no git: `tests/FakeCli/Program.cs`), `AuthoringPassTests.cs:280`, `:292` (an echoing fake agent), `CliAgentLiveTests.cs:83`
  (a live CLI, not run by the suite here); no `Directory.SetCurrentDirectory`, no in-process `GIT_DIR`/`HOME`/`TMPDIR` change
  (`git grep` → nothing); `StepGitTests.cs:26` sets `GIT_DIR` only in a child's environment, at a repository the test made;
- the spikes' Node tests: every `git … init` names its repository (`-C <dir>` or a cwd the test made); `git -C "" init` would init
  the current directory, and no site with an empty `<dir>` was found by reading.

**Not checked:** other repositories' tests and every other process on this shared machine (`/tmp` holds about 2 500 entries of many
sessions); when the entry appeared (`/tmp/.git` is absent now, before and after O8's run of the 2100 ModelArms tests, and no record of
its creation exists).

**What S4 changes about it: nothing, with evidence.** No cause was found in `ModelArms/`, so S4 removes none; and S4 does not reduce
the exposure: the owned folders are made in `Owned.TempRoot()` = `Path.GetTempPath()` (`Owned.cs:117-121`), the same `/tmp`, so a
`/tmp/.git` still refuses those roots after S4. What would remove the exposure is outside S4's Files line: a run-scoped temp root
outside `/tmp` (a `TMPDIR` the runner sets, or a check at the start-of-run sweep that names `<temp root>/.git` loudly instead of letting
each test refuse). Recorded as a todo of benchmark's (the parent's *Found on the way*), not built here.

## 13. Notes for the parent and the stories (findings, not changes)

- **N1 — Row E6-i's count is the Node guard's.** The copied C# guard reads 33 more findings in `ModelArms/` (O3: 16 `owner-blind` on the
  legacy lines, 17 `"/tmp"` literals). S4's Done-when ("no `legacy-name`, `raw-temp` or unmarked `raw-worktree` finding under
  `ModelArms/`") and § 11's DoD ("in either guard") already require them gone; this brief clears them (K2, K3; Q2).
- **N2 — The construction count.** At `b348bb33` `TempFolder` is constructed at 112 sites: 108 `new TempFolder()` in 25 files and 4
  `TempFolder _temp = new()` fields (`ArmGitTestBase.cs:13`, `ExperimentRoot.cs:39`, `ShellObjectiveReader.cs:133`, `StepBench.cs:76`);
  35 files name the type **[M]**. The stories' "112 `new TempFolder()` in 26 files" (`:79`) counts the sites, not the spelling.
- **N3 — C8 observed, and C10's "without" half seen on the way:** a `.cs` under `tools/model-arms/` is not discovered by the C# copy
  (scanned 0, O1 (d)); S7's brief observes the "with" half.
- **N4 — Shape G is invisible to both guards** (O6): a path built on `TempDir.Root` is never read as a temp path. K4 uses it for paths
  that are never made; K5 uses it for scaffolds that are made on purpose and kept, which is why their summaries must say so.

## 14. Open questions — each with a recommendation, for the coordinator before the gate

1. **Q1 — The M2 and M3 scaffolds (K5): drop the prefix.** Their output must outlive the test, so the owner form is wrong (the next
   run's sweep collects a dead record's folder after ten minutes) and the legacy form is what rule §1 forbids. The stories' S4 *What*
   names two dispositions ("moves to the helper", or `~` where the test is about the old name) and this is neither. [Recommend:
   unprefixed `bench-m2-…`/`bench-m3-…` names on `TempDir.Root`, the manifest teardown kept, the summaries naming the limit (N4), as
   the rule's own *Never*. **In doubt it is a change**: if the coordinator counts it, it is the tenth full stop and S4 stops before
   these two files; the alternative inside the text is to leave M2/M3 for the re-review, with S4's Done-when unmet for their 4
   `legacy-name` and 4 `owner-blind` lines and 4 `raw-temp` calls.]
2. **Q2 — The C# guard's 33 extra findings (N1) are S4's.** [Recommend: not a change, a fix that only satisfies the plan as written
   (S4's Done-when, § 11's DoD); the 17 literals rewritten (K3), never exempted, since an exemption must be listed by the stories.]
3. **Q3 — `TimingLineTests.cs:311-312` is sample data, not a name a test makes.** [Recommend: a neutral sample (K2), not a change: the
   line creates nothing, and its slug rule is kept character for character.]
4. **Q4 — `PinnedConsumer`'s kept scratch (K6)** is already inside `ExperimentRoot`'s `TempFolder`, so "moves INSIDE … under a plain
   name" becomes a rename within it, removed with the folder at `Dispose`; the failure message is the record. [Recommend: as the
   stories write it (K6); the diagnostic loss is the stories' decision, stated in the summary.]
5. **Q5 — The plan records and `research/module_tests.md`.** S4's Files line names neither. [Recommend: the brief, its `todo/README.md`
   row, its promotion with a `research/README.md` row and the stories' progress line are within the PR, as for S3 (its Q2); no
   `module_tests.md` line in S4 (S7 closes the docs, the stories' S7 *What*).]
6. **Q6 — `TempFolder.Dispose` no longer throws on a failed removal** (K1). [Recommend: accept; rule §2 says teardown never throws, and
   the end-of-run audit fails the run naming the folder, so the failure is still loud, one level later.]
7. **Q7 — `PosixFolderIsolationTests.cs:321` on the 9p mount** gets `OwnedDir.Create("drvfs", scratch)` (claim beside it on the mount;
   no sweep reads that parent). Not observable here or in CI (the variable is unset). [Recommend: as K2; the PR says it was not run.]
8. **Q8 — The killed-child test sweeps the real temp root through the `Mine` seam** (K7) instead of giving `TempFolder` a parent
   seam. [Recommend: the seam-free form; the sweep then reports every other entry as foreign and removes nothing else, and the start-of-
   run sweep of the same root has already run in this process.]
