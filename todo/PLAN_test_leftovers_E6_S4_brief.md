# E6.S4 brief — `ModelArms/`: the owned folder, no legacy name, the pilots' output outside the family (rows E6-i, E6-l, E6-b by hand; claim C8)

> Status: **plan only, nothing implemented yet; reworked 2026-11-17 to the parent's epoch 11 (opened by the owner, `Narrowed: yes`), with the item 8.1 observations (§ 11) — ONE open STOP finding for the coordinator before the plan gate (§ 15: doc lines that describe what S4 changes and lie outside item 4.2's *Files*; it may be entry 11.1).** The brief of story S4 of `todo/PLAN_test_leftovers_E6.md` as the epoch-11 amendment writes it (§ 6 S4 `:561-610`; rows E6-b `:94`, E6-i `:107`, E6-l `:110`; claim C8 `:166`; § 11 `:806-834`). Its scope is S4's *Files* line, the parent's § *Epoch 11* item 4.2, as named paths (§ 0.1). The questions of the first version (Q1–Q8) are answered by item 4.2's table (§ 14).
>
> Was (the first version, `4dc04820`): Status: **plan only, nothing implemented yet, 2026-11-17; the item 8.1 observations made before its plan gate (§ 11).** … a file outside them would be the parent's next counted change, which after entry 10.1 is the TENTH FULL STOP … Three dispositions this brief needs are put to the coordinator before the gate (§ 14, Q1–Q3). (That version found entry 10.2; epoch 10 stopped, and epoch 11 starts at 0 changes.)
>
> Judge's brief (Opus) for a Sonnet coder. Written 2026-11-17, reworked the same day by NEW commits (the parent's item 8.3 and its
> judge's cut row for this brief). Read-only inputs, all verified: benchmark worktree `/home/jinx/git/wt/bench-e6s4`, branch
> `feat/e6-s4-modelarms` from `main` `b348bb33` (#219: E6.S3; #218: F42-c, the copy at mcp `53f65130`), mount `.agents/conventions`
> at `b1746499`. **The parent** is `dew_flow_conventions · todo/PLAN_test_leftovers_rollout.md` § *Epoch 11*, on the branch
> `docs/rollout-epoch-11` (PR #176) at `43527cf` (epoch 11 opened by the owner 2026-11-17, `Narrowed: yes`, 0 changes; its
> items 3.1 (rows E6-i `:3726`, E6-l `:3727`), 3.3 (O-b `:3770`, O-i `:3777`, O-j `:3778`, O-l `:3780`), 4.2 (`:3823`, *Files*
> `:3841`, *Done when* `:3863`, the answered questions `:3868-3879`), 4.3 (`:3881`), 4.5 (`:4008`), 7 (`:4061`, the owner's 9p answer
> `:4071`), 8.1 (`:4081`), 8.2 (`:4103`); the judge's rows for this brief `:809-810`). **"Stories"** is
> `todo/PLAN_test_leftovers_E6.md` as amended on `docs/e6-stories-epoch-11` (PR #220) at `47cab130`; when #220 merges, the
> coordinator brings this branch up to date. Every code `file:line` is at `b348bb33`. **[M]** is measured for this brief, read-only;
> **[O]** a claim OBSERVED in a scratch clone with the six things of item 8.1 (§ 11); **[P]** observed by the parent's item 3.3 (its
> O-row named); **[R]** read from code and NOT run (written open-ended, "red or not, the run says which"); **[D]** taken from a named
> document.

## 0. What you build, in one paragraph

`TempFolder` (`tests/Bench.Tests/ModelArms/TempFolder.cs:10`) stops calling `Directory.CreateTempSubdirectory("testsToDelete-modelarms-")`
and makes its folder through the copied helper, `OwnedDir.Create("modelarms")`: the name `testsToDelete-<owner>-bench-modelarms-<id>`,
claimed first (`<folder>.owner` beside it), made exclusively with mode 0700, removed with its claim by the exact path. It keeps its
`OpenForDeleting` walk (the owned removal does not restore modes) and its whole public surface, so its 112 construction sites do not
change. Every site S4 disposes of is classified by its LIFETIME (item 4.2's table) and takes that class's ONE disposition: the run's
sites go to the helper; the kept reader's scratch stays inside the owned folder under a plain name; names never made, paths only read
and sample text take a helper name, a neutral literal or `TempDir.Root` (a path only read); the 17 `"/tmp"` literals the C# guard reads
are rewritten; and the two attended-pilot scaffolds, whose output must OUTLIVE the run, write it only under the operator-named folder
`BENCH_PILOT_OUT`, resolved by ONE new function `PilotOut` and pinned by seven ordinary facts, each scaffold writing its manifest
first (row E6-l). The 9p test of `PosixFolderIsolationTests` accepts a validated scratch folder BELOW the drvfs mount, sweeps it at
start, and is proved once on the real mount (the owner's answer, item 7). The 10 real `worktree add` lines take one inline
`raw-worktree` exemption each (listed in § 3a), the 10 assertion strings the `~` precedent. Then a docs commit (the model-arms module
document and dated notes in two IMPLEMENTED plans). ONE pull request, in this repository only.

## 0.1 Scope — item 4.2's *Files* as named paths, each with the dependency that needs it

The parent's item 4.2 *Files* (`:3841-3861`; the stories' S4 *Files* `:588-600`): `tests/Bench.Tests/ModelArms/**`; three research
documents at the named lines; the plan records. Every path below is inside it.

| Path | Needed for | Bound |
|---|---|---|
| `ModelArms/TempFolder.cs` | E6-i: the owned folder (K1) | § 4 row 1 |
| `ModelArms/TempFolderTests.cs` | scenarios (1), (2): name, claim, killed child (K7) | § 5 |
| `ModelArms/PilotOut.cs` (NEW), `ModelArms/PilotOutTests.cs` (NEW) | row E6-l: the one resolver of `BENCH_PILOT_OUT` and its seven facts (K5) | § 5 (7) |
| `ModelArms/M2PilotScaffold.cs`, `ModelArms/M3PilotScaffold.cs` | row E6-l: `M2:39-40`, the summary `:11-16`, M2's new manifest, the probe `:72-78` (+ its `finally` `:118-122`); `M3:34`, `:37`, `:105`, the summary `:9-11` (K5) | the names, the folder, the manifest, the probe, the summaries |
| `ModelArms/PilotScaffoldKillTests.cs` (NEW) | row E6-l: the killed-after-first-child check of EACH scaffold (K5c) | § 5 (8) |
| `ModelArms/BriefFillerTests.cs` | `:156`, `:184`, `:231` legacy + raw temp (K2) | those three blocks |
| `ModelArms/InitVerbTests.cs` | `:138` legacy (K2); `:227` `"/tmp"` literal (K3) | those two lines |
| `ModelArms/ModelArmsProgramTests.cs` | `:83` legacy + raw temp (K2) | that block |
| `ModelArms/PinnedConsumer.cs` | `:109-110`, `:126-137` the kept scratch (K6) | `Preserved` and its summary |
| `ModelArms/PosixFolderIsolationTests.cs` | the 9p site, WIDENED (item 4.2's *Files* bullet and Q7; the parent's O-l): the check `:317-319` (a validated scratch folder BELOW a 9p mount that holds nothing at the start but owner-form entries of this repository and their claim files — what a killed run leaves; anything else refuses, so nothing foreign is ever touched: CodeRabbit on #176), the start-of-test sweep over that folder, the owned folder `:321-322` and its removal (`:335-338`), and the killed-run → next-sweep check with its child (K9) | `:314-339` and the new test methods and helper of K9 in the same file |
| `ModelArms/TimingLineTests.cs` | `:311-312` legacy + `"/tmp"` (K2) | those two lines |
| `ModelArms/KernelFactsTests.cs` | `:27`, `:135` raw temp (K4) | those two sites |
| `ModelArms/ValidateVerbTests.cs` | `:752`, `:755` raw temp (K4) | those two lines |
| `ModelArms/{ArmChildEnvironmentTests,ArmGitTaskQuestionsTests,ArmGitWideningTests,ExecutableResolutionTests,PathEntryRefusalTests,RootFactsTests,StepLaunchTests,TargetEnvironmentTests,TaskWorktreesRoadTests}.cs` | the 17 `"/tmp"` literals (K3) | the lines of K3's table |
| `ModelArms/{ArmGitTaskQuestionsTests,TaskWorktreesPlantTests,TaskWorktreesReviewTests}.cs` | the 10 real `worktree add` lines (K8, § 3a) | one marker per line |
| `ModelArms/{TaskWorktreesReviewTests,TaskWorktreesRoadTests,TaskWorktreesTests}.cs` | the 10 assertion strings (K8) | one `static readonly` per file and the 10 lines |
| `ModelArms/ExperimentRoot.cs` | `:22` the doc comment that names the old folder form | that comment only |
| `research/module_model_arms.md` | `:1462` (`TempFolder`'s folder form), `:2114` (a timed-out reader's scratch, now kept inside the owned folder), `:2250-2256` (M2's procedure: step 1's printed paths and `BENCH_PILOT_OUT`, the log's removal sentence) (K10) | those lines; the run logs `:2262`, `:2322`, `:2370` keep their observed paths as history |
| `research/PLAN_model_arms_E6.md` (IMPLEMENTED) | one dated note each beside `:338-341` (the names), `:381` (T4's name check), `:746`, `:819`, `:821` (the kept reader's name; the growth rows) (K10) | notes only; the old text kept |
| `research/PLAN_model_arms_E3.md` (IMPLEMENTED) | one dated note each beside `:810-811`, `:876`, `:885`, `:1146` (K10) | notes only; the old text kept |
| the plan records (the stories' § 6 first paragraph, E11-10): this brief, its `todo/README.md` row, its promotion (`research/PLAN_test_leftovers_E6_S4_brief.md`, `research/README.md`), the stories' progress line, and the copy of § 3a's exemption list into the stories' § 11 (item 4.3) | `common/planning-docs.md`; item 4.3 | those records |

**Read and NOT on the line** (item 4.2, `:3858-3861`): `research/module_tests.md` and `research/PLAN_model_arms.md` (they print no pilot
path, no kept reader's name and no `TempFolder` form); `research/module_model_arms.md:1308` and the IMPLEMENTED model-arms plans that
name `testsToDelete-modelarms-…` (E1 `:1166`, `:1187`; E3 `:80`, `:152`; E4 `:1139`) record what was built then and stay. **Found
outside the line by this rework: § 15 (a STOP finding).**

**Not touched, by name:** `tests/Bench.Tests/Leftovers/**` (every manifest target, `template-manifest.json`, `TheAdaptedCopyTests.cs`,
`TempDir.cs`, `SuiteChild.cs`, `LeftoversSweep.cs`: used, never edited), `.github/workflows/ci.yml`, `src/`, `hosts/`, `tools/`
(`tools/model-arms/` is the product under test; `RootFacts`, `ExecutableResolution` are used, not changed), every other `tests/` folder
(S5's). **A file the build, a test or a check needs outside the table above is a change of the parent's epoch 11 (it would be entry
11.1): STOP, write the finding into this brief (§ 15), and hand it to the coordinator; do not add the file.**

## 1. Rules you must read first

- `.agents/PROJECT.md` (commands; never `dotnet test`; exe + argv with a timeout; warnings are errors).
- `.agents/conventions/common/test-leftovers.md`, its detail (§1 the name, its last item: what lives INSIDE an owned folder needs no
  name of its own, and § *Never*: "never give the prefix to something that must survive the run"; §2 the helper, "never throws"; §3
  the record, the only evidence where the kernel owner is always the caller; §4 the sweep's two keys and age; §7 the guard classes and
  the exemption grammar) and `common/testing.md` (RED first; revert the line the behaviour depends on).
- The stories (at `47cab130`): rows E6-b `:94`, E6-i `:107`, E6-l `:110`; claim C8 `:166`; § 6 S4 `:561-610`; § 7; § 11 `:806-834` (the
  exemption-list rule and the ONE documented exemption of row E6-l).
- The parent § *Epoch 11*: items 3.3 (O-b, O-i, O-j, O-l), 4.2, 4.3, 7, 8.1 (every site classified by lifetime; both guards with a
  count per class; shape G only for a path nothing creates or keeps; the brief lists its exemptions), 8.2.
- The S3 brief `research/PLAN_test_leftovers_E6_S3_brief.md` § 11 (how an observation is written) and the S2 brief § 14 (commands).
- No Python anywhere. No Docker. No `git stash`. Commit only; the coordinator pushes and opens the PR.

## 2. Preconditions and merge conditions

1. S3 merged (#219, `b348bb33`), so F41-b (mount `b1746499`) and F42-c (the copy at mcp `53f65130`, `TheAdaptedCopyTests.cs:83`) are
   in: met **[M]**.
2. The stories amendment (#220) merged before this brief's plan gate (the parent's item 2 point 2); the coordinator brings this branch
   up to date then.
3. § 15's STOP finding answered by the coordinator before the plan gate (entry 11.1, or read as history).
4. At most three open PRs here, or the owner's allowance for E6 PRs over the cap (2026-11-16).
5. **The 9p merge condition** (the parent's O-l and item 7, the owner's answer 2026-11-17: "(1), once"): K9's killed-run → next-sweep
   check PASSED on the real drvfs mount, run ONCE with `MODEL_ARMS_DRVFS_SCRATCH` = a new empty folder under
   `/mnt/c/Users/strug/AppData/Local/Temp/`, that folder removed by exact path after; the run's output quoted on the PR. That record is
   the only real evidence there (drvfs gives every file to uid 1000, so the kernel-owner key cannot tell owners apart; § 11 O11).
6. CI checks of the head SHA exist and are green (`build-test`, `contract`, `plans`, `workflows`); the coai code gate and the Opus
   review are done; every RED of § 5 quoted on the PR.

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
  (`LeftoversAudit`, run from `LeftoversSweepFixture.cs:63-75`) fails the run naming it (Q6, decided).
- **K2 — Every other site S4 disposes of, by LIFETIME** (the parent's item 4.2 table, `:3826-3833`; no site is "a test about the old
  name itself", so none takes `~` for the prefix):

| Site | Lifetime (item 4.2) | Today | S4 |
|---|---|---|---|
| `TempFolder.cs:10` | the run | `CreateTempSubdirectory("testsToDelete-modelarms-")` | K1 |
| `BriefFillerTests.cs:156`, `:184`, `:231` | the run | `CreateTempSubdirectory("testsToDelete-briefs-")` in `try`/`finally` | `using var folder = new TempFolder();` and `folder.Path` for `folder.FullName`; the `finally` delete goes. `TempFolder`, not `TempDir`: `:180-184` lowers modes |
| `ModelArmsProgramTests.cs:83` | the run (a path inside an owned folder that does not exist) | `Path.Combine(Path.GetTempPath(), "testsToDelete-modelarms-absent-root")` | `using var absent = new TempFolder();` and `absent.Combine("absent-root")` |
| `KernelFactsTests.cs:135` | the run | `CreateTempSubdirectory("birth-")` + `finally` | `using var parent = new TempFolder();` (K4) |
| `PosixFolderIsolationTests.cs:321` | the run (on the operator's 9p mount) | `Path.Combine(scratch, "testsToDelete-modelarms-" + …)` | K9 |
| `M2PilotScaffold.cs:72-78` (the validation probe; M3 calls it, `M3PilotScaffold.cs:54`) | the run | `Path.Combine(Path.GetTempPath(), $"testsToDelete-{pilot}-probe-{id}")`, removed in `finally` (`:118-122`) | K5 (b) |
| `PinnedConsumer.cs:126-137` | inside an owned folder | the kept scratch moved beside the `TempFolder` | K6 |
| `InitVerbTests.cs:138` | a name never made | `Path.Combine(Cli.Repository.Root, "testsToDelete-modelarms-init-refused")` | `Path.Combine(Cli.Repository.Root, Owned.NameFor("init-refused"))` (owner form from the helper, never created; the `finally` removes that exact path if a regression made it) |
| `TimingLineTests.cs:311-312` | sample text | `"/tmp/testsToDelete-x_1/arm.d ~y"` and its slug | `"/srv/x_1/arm.d ~y"` → `"-srv-x-1-arm-d--y"` (a dot, an underscore, a space, a tilde, as before; Q3, decided) |
| `KernelFactsTests.cs:27`; `ValidateVerbTests.cs:752`, `:755` | a path only read | `Path.GetTempPath()` | `TempDir.Root` (K4; within E11-4's bound) |
| the 17 `"/tmp"` literals | a path only read / sample text | K3 | K3 |
| `M2PilotScaffold.cs:39`, `:40`; `M3PilotScaffold.cs:34`, `:37`, `:105` | must OUTLIVE the run (row E6-l) | `testsToDelete-m2-…`, `testsToDelete-m3-…` under the temp root | K5 |

- **K3 — The 17 `"/tmp"` path literals the copied C# guard reads** (`TempFolderLiteral`, `LeftoversGuard.cs:137`, read as `raw-temp` in a
  test `.cs`, O3; the Node guard does not read them). Each is a string the test passes to a pure function or as a refused option, never
  a folder it makes. Rewritten to a path no guard reads, keeping what the line tests; never exempted (Q2, decided):

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
  `Path.GetTempPath()` → `TempDir.Root` (a path only read: `stat` writes nothing there; within E11-4's bound; clean in both guards, O6).
  `KernelFactsTests.cs:135`: `CreateTempSubdirectory("birth-")` + `finally` → `using var parent = new TempFolder();`.
  `ValidateVerbTests.cs:752`, `:755` (paths in an experiment that is only read, never made) → `TempDir.Root`. `PinnedConsumer.cs:134`
  goes with K6.
- **K5 — The pilots' output outside the family (row E6-l; the parent's items 4.2 last row and 4.3; [P] O-b, O-i, O-j).** Both scaffolds
  stay `[Fact(Explicit = true)]`; the suite never runs them.
  - **(a) `PilotOut.cs` (new, `namespace Bench.Tests.ModelArms`):** `internal static Outcome<string> Resolve(string? value, string tempRoot,
    Func<string, string> gitTopOf)`, PURE, and `internal static string Required()`, which reads `BENCH_PILOT_OUT`, `TempDir.Root` (a path
    only read, for the comparison) and `RootFacts.GitTopOf` (`tools/model-arms/ModelArms/Adapters/RootFacts.cs:19-20`) and throws
    `InvalidOperationException` with `Resolve`'s reason when it fails. `Resolve` refuses, in this order and each with a sentence naming
    `BENCH_PILOT_OUT`: unset; empty; relative; not an existing folder; resolved (both paths through `ExecutableResolution.RealPathClimbing`,
    links followed) AT or UNDER the resolved temp root; inside a git checkout (`gitTopOf` non-empty, the checkout named; `init` refuses a
    root there, `ExperimentFile.cs:70`). Otherwise the folder's full path. **[P] O-i**: this shape, 7 of 7 facts green, both files clean
    in both guards. The refusal texts follow the precedent `BENCH_ARTIFACT_ROOT` (`hosts/Cli/ProbesInputs.cs:75-78`, `:97`).
  - **(b) M2's validation probe** (`M2PilotScaffold.cs:72-78`, called by M3 at `:54`) becomes `using var probe = new TempFolder();` with
    `root`, `bin`, `home` inside it; the `finally` delete (`:118-122`) goes (the `TempFolder` restores modes and removes by exact path).
    If the `pilot` parameter then names nothing, it goes and M3's argument with it.
  - **(c) The names and the manifests.** Each scaffold's first statement after its platform skip is `var output = PilotOut.Required();`
    (before anything is made). **M3**: `ClaimManifest(output)` (`M3PilotScaffold.cs:100-112`, its exclusive create kept) as
    `bench-m3-manifest-<id>.json`; every path `Path.Combine(output, $"bench-m3-{name}-{id}")` (`:37`); the manifest still written
    before anything else is made (`:41-47`); `:105` follows. **M2** (new, the parent's G3): the same shape as M3 — draw `<id>`, claim
    `bench-m2-manifest-<id>.json` in `output` exclusively (`FileMode.CreateNew`, a taken name draws a new id), write into it the id, the
    uid and the EXACT paths of `bench-m2-target-<id>` and `bench-m2-root-<id>` BEFORE either exists, then make the target
    (`:39-40`'s names now under `output`). M2's failure path (`:62-68`) removes by the manifest's paths, the manifest last, as M3's
    does (`ScaffoldCleanup.Remove`, `M3PilotScaffold.cs:87`). Never `Path.GetTempPath()` or `TempDir.Root` for this output, and no
    exemption marker in either file ([P] O-b: each planned line `[]` in the C# copy; a rename under `Path.GetTempPath()` stays
    `raw-temp`; under `TempDir.Root` it is `[]` only because no guard traces callers, the blind spot row E6-l forbids).
  - **(d) The summaries** (`M2:11-16`, `M3:9-11`): the output is made only under `BENCH_PILOT_OUT` (outside every checkout and outside
    the temp root, which a WSL restart empties), unprefixed, named by a manifest written first, collected by no sweep, removed only by
    the pilot's own teardown (M2: the manifest's paths, the manifest last; M3: its manifest, T1–T5, T4 reading `bench-m3-*-<id>`); the
    ONE documented exemption of the stories' § 11 and the parent's item 4.3.
  - **(e) The killed-after-first-child check, for EACH scaffold** (`PilotScaffoldKillTests.cs`, two ordinary `[Fact]`s that the suite
    runs; skipped on Windows with its reason). The parent makes TWO `TempFolder`s, `childTemp` and `pilotOut`, and starts the scaffold as
    a child of this executable (`SuiteChild.StartInfoFor(Environment.ProcessPath!, ["--no-ansi", "--explicit", "only", "--filter-method",
    <the scaffold's method>], env)`, `SuiteChild.cs:44`) with `TMPDIR = childTemp.Path` (so the CHILD's temp root is `childTemp`, and
    `pilotOut`, a sibling in the parent's temp root, is outside it: `PilotOut` accepts it **[R]**), `BENCH_PILOT_OUT = pilotOut.Path`, and
    `TESTSTODELETE_RUN` removed. It polls `pilotOut` every 10 ms (ceiling 120 s) for the first child folder (`bench-m2-target-*`,
    `bench-m3-target-*`), then `Kill()` (SIGKILL) and waits for the exit. It asserts: the child ended by the signal (exit 137; one that
    exited on its own fails the test as "not interrupted": no green on a run that was not killed); exactly ONE manifest
    `bench-mN-manifest-<id>.json` in `pilotOut`; it parses, its uid is this user's, and its paths are EVERY intended child (M2: target and
    root; M3: the ten names) under `pilotOut` with that `<id>`; every entry in `pilotOut` other than the manifest is one of its paths.
    `finally`: kill the child's tree; both `TempFolder`s disposed (whatever the killed child left — its output, its probe, its
    `clr-debug-pipe-*` FIFOs — is inside them; § 11 O10). **Why a private `TMPDIR`:** observed (O10), it moves every temp path of the
    child, scaffold output and .NET's own debug pipes included, into a folder the parent owns; a killed .NET child leaves
    `clr-debug-pipe-<pid>-…-in`/`-out` in its temp root. RED first (both): on today's code the scaffolds write under the CHILD's temp
    root, so `pilotOut` stays empty and the poll's ceiling fails the test naming it; then, for M2 after its names move but before its
    manifest exists, no manifest → red ("RED first on today's M2", the stories' *Done when*). The timing window is wide enough on this
    host (O10: first child at 4.4–4.7 s and end at 8.1 s for M2; 1.8–3.0 s and 4.2 s for M3; SIGKILL landed 1 ms after the child was seen).
  - **(f) The scaffolds RUN** (row E6-l (1)–(2); the parent's O-j shape): each once with `--explicit only` and `BENCH_PILOT_OUT` unset,
    empty, relative (`x`), missing, `/tmp`, an existing folder below `/tmp`, and a folder inside this checkout → each fails before it
    makes anything, its message naming `BENCH_PILOT_OUT` (RED first: on today's code each writes under the temp root instead); then each
    once with it set to a NEW folder outside the temp root and every checkout (`/home/jinx/.cache/e6s4-pilot-out-<8 hex>`, as O-j) →
    passes, every printed path under it and named as row E6-l says, nothing of theirs in the temp root (`ls -d /tmp/bench-m* /tmp/testsToDelete-m[23]-*` → 0);
    then the output removed by exact path (M2: the manifest's paths, the manifest last; M3: its manifest's paths one by one, each checked
    to be `<folder>/bench-m3-…-<id>`, the manifest last) and the folder removed when empty. Quoted on the PR.
- **K6 — `PinnedConsumer`'s kept scratch moves INSIDE the run's owned folder** (lifetime: inside an owned folder; Q4, decided). The
  scratch is already inside one: readers run in `ExperimentRoot.Scratch(…)` = `_temp.Folder(…)` (`ExperimentRoot.cs:157`;
  `ConsumerReaders.cs:48`), HOME and the working directory are that scratch, the copy is inside it (`ConsumerReaders.cs:78-79`,
  `PinnedConsumer.cs:110-112`) **[R]**. `Preserved` (made `internal static` for its test) walks up to the folder whose name matches
  `Owned.FullName()` (`Owned.cs:95`) and moves the scratch to `<that folder>/kept-reader-<name>-<id>`: a plain name, inside, removed with
  the folder. The timed-out reader's tree is already killed by `ProcessRunner` before the move **[R]**. The summary (`:109-110`) says so,
  and that the failure message (with `stderr so far`) is the record. No walk reaches the temp root: no folder matching → the scratch
  stays where it is (never `Path.GetTempPath()`).
- **K7 — The killed child (rule §4: dead record).** A child of this executable (`SuiteChild.StartInfoFor(Environment.ProcessPath!, …)`,
  with `BENCH_E6S4_KILL_CHILD_OUT=<TempDir>/child.txt`, `TESTSTODELETE_RUN` REMOVED (`null`), so the claim records the child itself,
  `Owner.cs:27-37`, and — added by this rework, O10 — `TMPDIR` = a second owned folder of the parent's, so the killed child's
  `clr-debug-pipe-*` FIFOs land there and not in `/tmp`) runs only `*KillChild_makes_a_TempFolder_and_waits_to_be_killed`, which returns
  at once when that variable is unset (green in every normal run). Because the child's temp root is that folder, its `TempFolder` is made
  THERE: the parent sweeps that folder, not `/tmp`. It writes its path (temp file, then `File.Move`) and waits up to 120 s. The parent
  reads the path, checks folder and claim exist, `Kill()`s the child (SIGKILL), waits for its exit, then runs ONE sweep restricted to
  that folder and claim through the kernel-owner seam:
  `LeftoversSweep.Run(new SweepOptions(childTemp, DateTimeOffset.UtcNow, TimeSpan.Zero, Liveness.ProbeProcess, Mine: p => (p == folder || p == folder + ".owner") && Scope.IsMine(p)))`
  (`LeftoversSweep.cs:51-52`, `:84`; `Liveness.cs:36`). It asserts `Removed` holds exactly those two (the parent's Q8, `:3879`) and both
  entries are gone. `finally`: kill the child's tree; dispose both owned folders. Skips on Windows (SIGKILL; F31). *Was:* the sweep over
  the real temp root (`TempDir.Root`) with the same `Mine` filter; moved to the child's own temp root so nothing of a killed child ever
  lands in `/tmp`.
- **K8 — `worktree add`: 10 real lines, 10 assertion strings** (row E6-b; claim C8, observed § 11 O1–O2). Each real line gets one
  marker, on its own line directly above it or trailing on the same line, never two lines above (that does not clear, O2): the list is
  § 3a. The 10 assertion strings (`TaskWorktreesReviewTests.cs:40`, `:73`, `:91`; `TaskWorktreesRoadTests.cs:79`;
  `TaskWorktreesTests.cs:70`, `:190`, `:265`, `:455`, `:522`, `:570`) take the `~` precedent (coai
  `src_mcp/tests/Leftovers/LeftoversBaselineTests.cs:256-260`, at coai `origin/main` `e829bba8`): per file ONE
  `private static readonly string WorktreeAdd = "work~tree add".Replace("~", string.Empty, StringComparison.Ordinal);` and each line
  interpolates `{WorktreeAdd}` or passes it — never a marker. Both forms clean in both guards (O2).
- **K9 — The 9p site (`PosixFolderIsolationTests.cs:314-339`; item 4.2's Q7 as amended by CodeRabbit on #176; the parent's O-l; the
  owner's answer, item 7).**
  - **The check, widened** (`:317-319` today: the variable must BE the mount point). A small helper in the same file,
    `DrvfsScratch.Check(string? value, IEnumerable<string> mountLines)` → `Outcome<string>`, PURE apart from reading the folder's
    entries: the variable unset or empty → the test SKIPS with today's sentence (the operator's opt-in, unchanged); otherwise it must be
    absolute and an existing folder; the LONGEST mount point in `mountLines` (`/proc/mounts`, its second field, compared path segment by
    segment, never by string prefix) at or above the folder must be of type `9p`; and the folder must hold NOTHING but owner-form
    entries of this repository and their claim files — a name `Owned.FullName()` matches, or `<such a name>.owner` — what a killed run
    of this test leaves, which the start sweep then judges. Anything else (a foreign file or folder, a quarantine sibling a killed sweep
    left, another owner's or another repository's name) REFUSES: the test FAILS naming that entry and writes nothing, so nothing
    foreign is ever touched. A failure, not a skip: the operator named the folder, and a silent skip would hide that it cannot be used.
  - **The start-of-test sweep:** after the check passes, `LeftoversSweep.Run(new SweepOptions(scratch, DateTimeOffset.UtcNow,
    LeftoversSweep.MinAge, Liveness.ProbeProcess))` over that folder only (the caller's scope, the two keys of rule §4; `Mine` the
    default `Scope.IsMine`, always true on drvfs: the record decides); its report written to the test output.
  - **The owned folder:** `using var owned = OwnedDir.Create("drvfs", scratch);` and `owned.Path` where `:321-322` made the legacy name;
    the `finally` delete (`:335-338`) goes (the `OwnedDir` removes folder and claim by exact path). Every write of the test stays inside
    `scratch`.
  - **The killed-run → next-sweep check** (a new `[Fact]` in the same class, gated by the same check, so it SKIPS when the variable is
    unset, which is every run but the one below): a child of this executable (`TMPDIR` = an owned folder of the parent in `/tmp`, for its
    debug pipes, O10; `TESTSTODELETE_RUN` removed; `BENCH_E6S4_DRVFS_CHILD_OUT=<owned>/child.txt`) runs a gated child method that
    passes the check, makes `OwnedDir.Create("drvfs", scratch)`, writes its path and waits; the parent kills it (SIGKILL); then it runs
    the CHECK again (it must ACCEPT the folder now holding the dead child's folder and claim: the CodeRabbit point) and the start sweep
    with `MinAge = TimeSpan.Zero` (the age selection is the copied sweep's own tested behaviour; a real next run would wait ten minutes):
    `Removed` holds the dead child's folder and claim, both gone. And a live counterexample in the same run: this process's own
    `OwnedDir.Create("drvfs-live", scratch)`, present during that sweep, is KEPT with the reason `live` (its record is this process),
    the evidence that the record, not the uid, decided. `finally`: kill the child's tree; remove what is left by exact path.
  - **The ordinary facts** (the suite runs them everywhere, no 9p needed; RED first on today's code, which has no `DrvfsScratch`):
    `DrvfsScratch.Check` over a `TempFolder` and hand-written mount lines — accepts an empty folder below a `9p` mount point; accepts a
    folder holding an owner-form folder and its claim; refuses a folder holding a foreign file (naming it), a quarantine sibling, another
    owner's name; refuses when the longest mount point above is not `9p` (a `9p` mount at `/mnt/c` and an `ext4` mount at `/mnt/c/x`,
    the folder below `/mnt/c/x`); refuses the mount point string-prefix trap (`/mnt/cx/…` is not under `/mnt/c`); refuses a relative or
    missing path.
  - **The one real run** (item 2 point 5): `mkdir /mnt/c/Users/strug/AppData/Local/Temp/bench-e6s4-9p-<8 hex>` (new, empty; the name is
    not of the prefix family); `MODEL_ARMS_DRVFS_SCRATCH=<it> nice -n 19 $T --filter-class "*PosixFolderIsolationTests" --no-ansi`; the
    output quoted on the PR (the kill check's lines: removed, kept `live`); then `ls -A <it>` (expected empty) and `rmdir <it>` by exact
    path, both quoted. Once, by the owner's word; not repeated without a new one.

## 3a. The inline exemptions S4 adds (listed here before the plan gate; the PR copies this list into the stories' § 11, item 4.3)

Each a comment on its own line directly above the line (or trailing it), class `raw-worktree`, at `b348bb33`:

| File:line | Reason (after `testsToDelete-exempt: raw-worktree — `) |
|---|---|
| `ArmGitTaskQuestionsTests.cs:228`, `:234`, `:242`, `:304`, `:305`, `:307`, `:421`, `:438` | `inside the owned folder _temp (the ArmGitTestBase TempFolder) that holds the clone` (`Worktree(clone, name)` is the clone's sibling, `:16`; the clone is `_temp.Path/clone`, `ArmGitTestBase.cs:13`, `:35-41`) |
| `TaskWorktreesPlantTests.cs:47` | `inside the owned folder kit.Temp (the WorktreeKit TempFolder) that holds the clone` (`live` is `kit.Temp.Combine("live-wt")`, `:46`; `WorktreeKit.cs:36`; ONE marker clears both `"worktree", "add"` of that ternary line, O2 (e)) |
| `TaskWorktreesReviewTests.cs:27` | `inside the owned folder of kit.Root (the ExperimentRoot TempFolder) that holds the clone` (`kit.Wt(id)` is `Paths.Worktree(id)` under `kit.Root.Root` = `_temp.Folder("example", "root")`, `WorktreeKit.cs:38-40`, `:54`; `ExperimentRoot.cs:44`) |

No other exemption: none for `raw-temp`, `legacy-name` or `owner-blind` (the last takes none), and none in the scaffolds (row E6-l).

## 4. The changes, file by file

1. `TempFolder.cs` — K1. The summary (`:5-7`) says: the owned folder `testsToDelete-<owner>-bench-modelarms-<id>`, claimed beside it, made
   by the copied helper (plan D5); `Dispose` opens every closed folder, then removes folder and claim by the exact path and never throws
   (a removal that gives up is named by the end-of-run audit).
2. `TempFolderTests.cs` — scenarios (1) and (2) of § 5 beside the existing test.
3. `PilotOut.cs`, `PilotOutTests.cs` (new), `M2PilotScaffold.cs`, `M3PilotScaffold.cs`, `PilotScaffoldKillTests.cs` (new) — K5.
4. `PosixFolderIsolationTests.cs` — K9.
5. The K2, K3, K4, K6 and K8 sites, each exactly as its table says.
6. `ExperimentRoot.cs:22` — the doc comment's folder form.
7. The docs of K10 (§ 8 commit 7).

Nothing else. Every existing test of the namespace keeps its name and its assertions, except the changed literals of K3 (their meaning
kept) and the 9p test's check (K9).

- **K10 — The docs** (item 4.2's *Files*; every line read at `b348bb33` **[M]**). `research/module_model_arms.md`: `:1462` "Temp folders
  are `testsToDelete-modelarms-…` (`TempFolder`)" → the owner form through the copied helper; `:2114`'s sentence on a timed-out reader
  (its scratch PRESERVED as a sibling) → kept INSIDE the owned folder as `kept-reader-<name>-<id>`, removed with it, the failure message
  the record; `:2250-2256`, M2's procedure: `BENCH_PILOT_OUT` set to a folder outside every checkout and outside `/tmp`, step 1 printing
  `<out>/bench-m2-target-<id>`, `<out>/bench-m2-root-<id>` and the manifest, the log's removal sentence → the manifest's paths, the
  manifest last; the run logs (`:2262`, `:2322`, `:2370`) unchanged. `research/PLAN_model_arms_E6.md` and `research/PLAN_model_arms_E3.md`
  (both IMPLEMENTED): ONE dated note each, `*Note 2026-11-<dd> (test-leftovers E6.S4):* …`, on its own line directly after each named
  line or block, saying what the code does since S4 (the names under `BENCH_PILOT_OUT`, M2's manifest, the kept reader inside the owned
  folder, the growth rows' places), never a rewrite of the old text.

## 5. The scenarios — RED, GREEN, RED again

"RED again" is the defect put back once after GREEN, by hand, and quoted on the PR (the stories' § 7).

| # | Test (file) | GREEN asserts | RED (first) / RED again |
|---|---|---|---|
| (1) E6-i | `A_TempFolder_is_an_owned_folder_claimed_beside_it_and_removed_with_its_claim` (`TempFolderTests`) | the leaf matches `Owned.FullName()` and holds `-modelarms-`; `<path>.owner` exists and `Owner.Parse` of its text equals `Owner.RunRecord()`; the folder's mode is 0700 (Linux); after `Dispose` neither folder nor claim exists | RED first: on today's `TempFolder` (legacy name, no claim) the name and claim assertions fail. RED again: put `:10` back → red |
| (2) rule §4 | `A_TempFolder_of_a_killed_test_process_is_collected_by_the_next_sweep` and its child (`TempFolderTests`) | K7: before the kill the folder and its claim exist; after SIGKILL the sweep's `Removed` holds exactly the folder and its claim, both gone; nothing of the child in `/tmp`; skipped on Windows | RED first: on today's `TempFolder` the child's folder is `testsToDelete-modelarms-…`, which the sweep LISTS as legacy and never removes (rule §1) → red naming it. RED again: put `:10` back → red |
| (3) | `Dispose_removes_a_folder_holding_a_link_to_its_own_parent_and_a_folder_closed_to_its_owner` (existing, ten rounds) | still green: the mode walk runs before the owned removal | RED again: drop `OpenForDeleting()` from `Dispose` → the closed folder stays, the round fails |
| (4) K6 | `A_timed_out_readers_scratch_is_kept_inside_the_owned_folder` (a new `PinnedConsumerTests` in `ModelArms/`) | `Preserved` over `<TempFolder>/readers/x` returns a path under `TempFolder.Path` named `kept-reader-…`, the scratch's files moved with it; with no owned folder above, the scratch stays put | RED first: today's `Preserved` returns a sibling of the `TempFolder` in the temp root → red |
| (5) E6-b, E6-i | the guards over the head, by hand (§ 6) | per class, per guard, as § 6; the 10 markers of § 3a valid | RED again: remove ONE marker (`ArmGitTaskQuestionsTests.cs:228`'s) → one `raw-worktree` at that line in each guard; put a `"worktree add"` assertion string back → one in each |
| (6) | the ModelArms namespace, then the whole suite | `--filter-namespace "Bench.Tests.ModelArms"` green; its time against the base (O8: 2100 tests, 2 m 10.7 s) recorded, growth within the claim's cost and the new kill checks; the whole suite's FAILED list equal to the base's | — |
| (7) E6-l | `PilotOutTests` (seven ordinary facts: unset, empty, relative, missing, at and under an injected temp root and at the real `TempDir.Root`, inside a checkout naming it, accepted) | each refusal names `BENCH_PILOT_OUT` or its reason; an existing folder outside both is accepted ([P] O-i: 7 of 7) | RED first: before `PilotOut` exists the class does not compile, so the RED is each fact against a `Resolve` that accepts everything (a stub), quoted; RED again: drop the at-or-under clause → the two temp-root facts red |
| (8) E6-l | `PilotScaffoldKillTests`: `The_M2_scaffold_killed_after_its_first_child_leaves_a_manifest_naming_every_child`, the same for M3 | K5 (e) | RED first: today's scaffolds write under the child's temp root → the poll's ceiling fails naming `pilotOut`; M2 with its names moved but no manifest yet → "no manifest" red. RED again: M2 writes its manifest AFTER the target → red (the manifest is missing or names fewer paths at the kill) |
| (9) E6-l (1)–(2) | the scaffolds run by hand (K5 (f)) | refusals before anything is made; with the folder, every printed path under it; the temp root holds none of theirs; the output removed by exact path | RED first: today's scaffolds with the variable unset write `testsToDelete-m2-…`/`-m3-…` under the temp root (O10, under a private `TMPDIR`) |
| (10) K9 | `DrvfsScratch` facts (ordinary) and, ONCE on the real mount, the 9p test and the killed-run → next-sweep check | K9 | RED first: the facts against today's check (equality with the mount point): the folder below the mount is not accepted → red. RED again, on the ordinary facts only: a check that requires the folder to be EMPTY refuses the folder holding an owner-form folder and its claim → that fact red. The one run on the mount is GREEN only; it is never repeated for a RED |

## 6. The guards in S4 — both guards, a count per class, at the base `b348bb33` (row E6-i; the parent's item 8.1)

| Guard | Base, all | Base, under `ModelArms/` (per class) | Head, all | Head, under `ModelArms/` |
|---|---|---|---|---|
| Node (`--profile node --token bench --identity "the measuring harness"`) | **209** (O4) | 49: 15 `legacy-name`, 14 `raw-temp`, 20 `raw-worktree` | **160** | 0 in every class (the 10 marked lines clear) |
| Node, default profile | **3** | 0 | **3** | 0 |
| copied C# (`--explicit only --filter-class "*LeftoversRepositoryGuardTests"`, information; S7 turns it on) | **173** over 1320 files (O3; [P] item 8.4) | 82: 15 `legacy-name`, 16 `owner-blind`, 31 `raw-temp` (17 of them `"/tmp"` literals), 20 `raw-worktree` | **91** | 0 in every class |

No finding added anywhere else; new files (`PilotOut.cs`, `PilotOutTests.cs`, `PilotScaffoldKillTests.cs`, the `DrvfsScratch` helper)
add none ([P] O-i for the first two; O6 and O12 for the shapes the others use). A count that differs is read finding by finding before
anything else. Watch for, in new lines: the prefix word in any literal (the C# guard reads `ToDelete` alone as a piece, O5), `"/tmp"`
in a literal, `Owned.TempRoot` outside `Leftovers/` (`raw-temp`, O6; use `TempDir.Root`), `Owned.Prefix` outside `Leftovers/`
(`owner-blind`), a marker two lines above its line.

## 7. Commands (from the worktree root; WSL, no `.exe`, no Docker; one build at a time)

```bash
nice -n 19 dotnet build dew_flow_benchmark.slnx -c Release -m:4 -p:AppHostRuntimeIdentifier=linux-x64 --disable-build-servers
nice -n 19 dotnet format dew_flow_benchmark.slnx --verify-no-changes --no-restore
git diff --check
T=./tests/Bench.Tests/bin/Release/net10.0/Bench.Tests
nice -n 19 $T --filter-class "*TempFolderTests"
nice -n 19 $T --filter-class "*PilotOutTests"
nice -n 19 $T --filter-class "*PilotScaffoldKillTests"
nice -n 19 $T --filter-class "*PosixFolderIsolationTests"         # the DrvfsScratch facts run; the 9p tests skip (variable unset)
nice -n 19 $T --filter-namespace "Bench.Tests.ModelArms"          # time it; compare with O8
nice -n 19 $T --filter-class "*TheAdaptedCopyTests"                # untouched copy: still green
nice -n 19 $T --explicit only --filter-class "*LeftoversRepositoryGuardTests"   # information: 91 expected
nice -n 19 $T                                                       # the whole suite: compare the FAILED list with the base's
# row E6-l (1)-(2), K5 (f): each scaffold, refusals then one real run, output removed by exact path
for v in "" x /nonexistent /tmp "$PWD"; do BENCH_PILOT_OUT="$v" nice -n 19 $T --explicit only --filter-method "*Writes_the_scratch_target_and_root_for_the_pilot" --no-ansi | tail -3; done
env -u BENCH_PILOT_OUT nice -n 19 $T --explicit only --filter-method "*Writes_the_scratch_target_and_root_for_the_pilot" --no-ansi | tail -3
O=/home/jinx/.cache/e6s4-pilot-out-$(head -c4 /dev/urandom | od -An -tx1 | tr -d ' \n'); mkdir "$O"
BENCH_PILOT_OUT="$O" nice -n 19 $T --explicit only --filter-method "*Writes_the_scratch_target_and_root_for_the_pilot" --no-ansi
BENCH_PILOT_OUT="$O" nice -n 19 $T --explicit only --filter-method "*Writes_the_scratch_target_root_home_and_claude_for_the_pilot" --no-ansi
# (the same refusal loop for M3; a folder below /tmp: an existing scratch folder there)
# K9's one real run (item 2 point 5): see K9, *The one real run*
node .agents/conventions/tools/leftovers-check.mjs --repo . --profile node --token bench --identity "the measuring harness" | head -1   # 160
node .agents/conventions/tools/leftovers-check.mjs --repo . --token bench --identity "the measuring harness" | head -1                  # 3
ls -d /tmp/testsToDelete-*-bench-modelarms-* /tmp/testsToDelete-modelarms-* /tmp/bench-m* /tmp/clr-debug-pipe-* 2>/dev/null | wc -l   # before and after: no growth
node .agents/conventions/tools/plan-lifecycle.mjs
```

## 8. The commit series

**One PR, one story.** Each commit builds and its tests pass.

1. `TempFolderTests` (1) and (2) with their RED observed by hand on today's `TempFolder` (quoted), then `TempFolder.cs` (K1) in the
   same commit; (3) re-run.
2. The run-lifetime and never-made sites of K2 (not the scaffolds, not the 9p site) and K6 with (4).
3. K4 and the 17 literals of K3.
4. K5: `PilotOut.cs` and `PilotOutTests.cs` (7); the scaffolds' folder, names, M2's manifest, the probe's `TempFolder`, the summaries;
   `PilotScaffoldKillTests` (8); the RED of (8) and (9) quoted, then the runs of K5 (f).
5. K9: `DrvfsScratch` and its facts (RED first), the widened 9p test, its start sweep and the kill check; then the ONE real run, quoted.
6. K8: the 10 markers (§ 3a) and the 10 `~` strings; § 6's counts on the head.
7. **The docs commit:** K10 (`research/module_model_arms.md`, the dated notes in `research/PLAN_model_arms_E6.md` and `_E3.md`);
   `ExperimentRoot.cs:22`; § 3a's list copied into the stories' § 11; this brief's progress line.

**Size.** About 700 changed lines in about 35 files, most of them one-line edits; the new tests ~450 lines. One Sonnet coder.

## 9. Done when, and what the PR body quotes (the parent's item 4.2 *Done when*, `:3863-3866`, and the stories' S4 *Done when* `:601-610`)

- Rows E6-i (S4's half) and E6-l met by their checks: § 6's table on the head, per guard and per class; row E6-l (1)–(3): the
  refusals and one run of each scaffold (K5 (f)), both guards clean on both scaffold files with no marker.
- `PilotOutTests` green in the suite, each fact RED first; `PilotScaffoldKillTests` green for EACH scaffold, M2 and M3, RED first on
  today's M2 (K5 (e)).
- **The 9p merge condition:** K9's killed-run → next-sweep check PASSED on the real mount in its one run (the dead child's folder and
  claim removed, the live one kept `live`), the folder then empty and removed by exact path, all quoted; the `DrvfsScratch` facts green.
- (1), (2), (4): GREEN, each RED first and RED again quoted; (3) green and its RED again quoted; the marker RED of (5) from each guard.
- The ModelArms namespace green with its time beside O8's; the whole suite's FAILED list equal to the base's; no growth of
  `/tmp/testsToDelete-*modelarms*`, `/tmp/bench-m*` or `/tmp/clr-debug-pipe-*` across a namespace run (O9, O10).
- 0 warnings; `dotnet format` clean; `TheAdaptedCopyTests` green (no manifest target touched); `plan-lifecycle.mjs` clean;
  `git diff --name-only <base>..HEAD` within § 0.1; § 3a copied into the stories' § 11.

## 10. What this story does NOT prove

Windows (no leg; every kill test skips there). The 9p collection beyond its ONE run (the drvfs facts are proved off the mount; the
mount itself once, by the owner's word). That the owner's pilot itself runs (row E6-l: the pilot is the owner's, by hand); the
scaffolds themselves ARE run here (K5 (f)) and killed in the suite (K5 (e)). That a `worktree add` marker's named folder really holds
the clone (a guard reads the spelling, never the folder: the code review does). That no other test, script or CI step uses row E6-l's
exemption (a review item of each PR, item 4.3).

## 11. Observations — made before this brief's plan gate (the parent's item 8.1)

**Common to O1–O9 (the first version):** consumer **benchmark `b348bb33`** (`main`: #219, #218, #217), cloned (`git clone
--no-hardlinks`) from this worktree into a scratchpad folder outside every worktree and checked out at that SHA, the mount copied in as
files; **upstream pin mcp `53f65130`** (the copy's manifest commit, `TheAdaptedCopyTests.cs:83`) and the mount **`b1746499`** (with its
`node_modules`); Node `v26.12.1`, .NET SDK `10.0.114`, WSL `Ubuntu`, uid 1000, no Docker. One scratch file was added to the clone and
never committed: `tests/Bench.Tests/Infrastructure/S4ScratchObservations.cs` (two facts: `Obs_Repository` calls `LeftoversGuard.Scan(root,
root)` over the clone and writes every finding; `Obs_Plants` calls `LeftoversGuard.Scan(tree, tree)` over each private tree). Build:
`nice -n 19 dotnet build tests/Bench.Tests/Bench.Tests.csproj -c Release -m:4 -p:AppHostRuntimeIdentifier=linux-x64
--disable-build-servers` → 0 warnings, 0 errors, 1 m 29 s. Run: `nice -n 19 ./tests/Bench.Tests/bin/Release/net10.0/Bench.Tests
--filter-class "*S4ScratchObservations"` (2 of 2 passed). The private trees were written by a scratchpad Node script, one tree per
plant, each holding ONE file at the fixture path named in the row, the plant inside a minimal class body; the same script ran the
mounted guard's `scanText(<fixture path>, <text>)` and `isScanned(<fixture path>, 'node')`. **Common to O10–O12 (this rework):** the
same consumer, pin and mount, a SECOND scratch clone made the same way, nothing added to it, built the same way (0 warnings, 0 errors,
4 m 8 s on a loaded host). The scratch is not kept (a reboot clears it); the results are here.

| # | Claim (where) | Invocation | Fixture path, plant | Expected | Observed |
|---|---|---|---|---|---|
| O1 | **C8**, both halves (stories `:166`, `:94`): the C# copy reads `worktree add` only outside `src/` | `Scan` and `scanText`/`isScanned` | (a) `tests/Bench.Tests/ModelArms/ArmGitTaskQuestionsTests.cs`: `await TargetBench.GitAsync(clone, "worktree", "add", "-q", plain, "-b", "tmp/plain", "HEAD");` (b) `tests/Bench.Tests/Infrastructure/HarvestGatesTests.cs`: the same line (S5's kind) (c) `src/Bench.Infrastructure/Git/ScratchWorktree.cs`: `new[] { "worktree", "add", "--detach", "--force", scratch, at }` and the string `"git worktree add --detach"` (S5's seam) (d) `tools/model-arms/ModelArms/Adapters/ArmGit.cs`: the product's argv and the same words | (a), (b) red in both; (c) not read by the C# copy (**[R]** `HitsOutsideSource`, `LeftoversGuard.cs:205-213`), not scanned by Node; (d) not read by either | (a) C# 1 `raw-worktree` (`"worktree", "add"`), Node 1; (b) C# 1, Node 1; (c) C# scanned 1 file, **0** findings; Node `isScanned` **false** (its `scanText` alone would report 2, so the zero is the walk's, not the pattern's); (d) C# scanned **0** files (not discovered before S7's operation, claim C10), Node `isScanned` **false**. **C8 holds as written**, and `tools/` is read by neither today (the parent's E11-11 takes this as S4's half of C8, observed) |
| O2 | E6-b's marker and the `~` precedent (K8, § 3a) | `Scan` and `scanText` | `tests/Bench.Tests/ModelArms/…`: (a) O1's line with `// testsToDelete-exempt: raw-worktree — inside the owned folder TempFolder _temp of this test` on the line above (b) the same marker trailing the line (c) the marker with the reason `needed` (d) the marker two lines above (a line between) (e) the ternary line of `TaskWorktreesPlantTests.cs:47` (two `"worktree", "add"`; planted at the path `…/TaskWorktreesReviewTests.cs`, the same kind) under one marker (f) `$"worktree add -q --no-checkout {kit.Wt("t1")} tmp/t1"` (g) `var add = "work~tree add".Replace("~", string.Empty, StringComparison.Ordinal);` with `$"{add} -q …"` (h) a `StartsWith(Add, …)` over a `"work~tree add".Replace(…)` field | (a), (b), (e), (g), (h) clean; (c) `exemption` + `raw-worktree`; (d) and (f) red | both guards alike: (a) **0**, (b) **0**, (c) 1 `exemption` ("carries no reason") at the marker + 1 `raw-worktree` at the line, (d) **1** `raw-worktree` (a marker clears its own line and the next only), (e) **0**, (f) 1 `raw-worktree` (`worktree add`), (g) **0**, (h) **0** |
| O3 | the C# copy on `ModelArms/` today (row E6-i) | `Obs_Repository`: `Scan(clone, clone)` | the clone at `b348bb33` | — | scanned **1320**, findings **173** (F42-c's count; the parent's item 8.4 the same, class for class); **82** under `ModelArms/`: 15 `legacy-name` (the Node guard's 15 lines), **16 `owner-blind`** (`testsToDelete-mo`, `-br`, `-m2`, `-m3`, `-x_`, `-x-`, `-{p`: the same 15 lines and `M2PilotScaffold.cs:78`), **31 `raw-temp`** (the Node guard's 14 API calls + **17 `"/tmp"` literals**: K3's table), 20 `raw-worktree` (the Node guard's 20 lines) |
| O4 | the Node guard on `ModelArms/` today | `node .agents/conventions/tools/leftovers-check.mjs --repo . --profile node --token bench --identity "the measuring harness"`, in this worktree | the repository at `b348bb33` | 209 (S3 brief O13) | **209**; **49** under `ModelArms/`: 15 `legacy-name` (`BriefFillerTests.cs:156`, `:184`, `:231`; `InitVerbTests.cs:138`; `M2PilotScaffold.cs:39`, `:40`; `M3PilotScaffold.cs:37`, `:105`; `ModelArmsProgramTests.cs:83`; `PinnedConsumer.cs:129`, `:135`; `PosixFolderIsolationTests.cs:321`; `TempFolder.cs:10`; `TimingLineTests.cs:311`, `:312`), 14 `raw-temp` (`BriefFillerTests.cs:156`, `:184`, `:231`; `KernelFactsTests.cs:27`, `:135`; `M2PilotScaffold.cs:39`, `:40`, `:78`; `M3PilotScaffold.cs:34`; `ModelArmsProgramTests.cs:83`; `PinnedConsumer.cs:134`; `TempFolder.cs:10`; `ValidateVerbTests.cs:752`, `:755`), 20 `raw-worktree` (K8's lines); default profile **3**, none under `ModelArms/` |
| O5 | K2: no site needs `~` for the prefix; if one ever does, which spelling | `Scan` and `scanText` | `tests/Bench.Tests/ModelArms/X.cs`: (a) `"tests~ToDelete-modelarms-".Replace("~", …)` (b) `"tests~To~Delete-modelarms-".Replace("~", …)` | — | (a) C#: 1 `owner-blind` (`ToDelete (a piece of the prefix word)`, `LeftoversGuardLiterals.cs:61-64`), Node `[]`; (b) **0** in both (item 4.2's table takes (b) as the `~` form) |
| O6 | the helper calls S4 uses, in a `ModelArms/` `.cs` (K1, K2, K4, K6, K9) | `Scan` and `scanText` | at the files of § 4: `private readonly OwnedDir _folder = OwnedDir.Create("modelarms");`; `using var folder = TempDir.For("briefs");` and `new TempFolder()`; `Path.Combine(Cli.Repository.Root, Owned.NameFor("init-refused"))`; `Path.Combine(owned, $"reader-{…}-{…}")` and `Owned.FullName().IsMatch(owner.Name)`; `ProcessRunner.RunAsync("stat", …, TempDir.Root, …)`; `OwnedDir.Create("drvfs", scratch)`; `TempDir.Root.Replace(…)` (ValidateVerb); the neutral TimingLine sample. Controls: `Owned.TempRoot()`; today's `TempFolder.cs:10`; today's TimingLine lines; `"/tmp/claude-1000"`. *Superseded by the parent's O-b, by this rework:* the first version's scaffold plants on `TempDir.Root` (`Path.Combine(TempDir.Root, $"bench-m2-target-{id}")`, M3's names on `TempDir.Root`) were clean in both guards ONLY because no guard traces callers; row E6-l forbids that form, and O-b observed the replacement lines (under `output` from `BENCH_PILOT_OUT`) | every planned line clean; controls red | every planned line **0** in both. Controls: `Owned.TempRoot()` → C# 1 `raw-temp` ("outside the helper's folder"), Node `[]`; today's `TempFolder.cs:10` → C# `legacy-name` + `owner-blind` + `raw-temp`, Node `legacy-name` + `raw-temp`; today's TimingLine → C# `legacy-name` + `owner-blind` + `raw-temp` (`"/tmp"`) on `:1`, `legacy-name` + `owner-blind` on `:2`, Node `legacy-name` on each; `"/tmp/claude-1000"` → C# 1 `raw-temp`, Node `[]` |
| O7 | the helper's signatures at `main` (K1, K4, K6, K7, K9) | read **[R]** at `b348bb33` | `tests/Bench.Tests/Leftovers/` | — | `OwnedDir.Create(string what, string? parent = null, Action<string>? warn = null)` public (`OwnedDir.cs:35`), `Path`, `Dispose` never throws (`:65-72`); `TempDir.For(what)`, `TempDir.Root`, `At` (`TempDir.cs:24-33`, namespace `Bench.Tests`); `Owned.NameFor(what)` (`Owned.cs:126`), `Owned.FullName(owner?)` (`:95`), `Owned.Room` (`:82`); `LeftoversSweep.Run(SweepOptions)` (`LeftoversSweep.cs:84`) with `SweepOptions(Tmp, Now, MinAge, Probe, Mine?, Hooks?, SweepLockWait?)` (`:51-52`), `LeftoversSweep.MinAge` (`:81`); `Liveness.ProbeProcess` public (`Liveness.cs:36`); `SuiteChild.Run`/`StartInfoFor` (`SuiteChild.cs:21`, `:44`); the folder made `mkdir 0700` on Linux (`Native.cs:129`). All compiled in O1's build |
| O8 | the base time of the namespace | `nice -n 19 $T --filter-namespace "Bench.Tests.ModelArms" --no-ansi` under `/usr/bin/time -v`, in the clone | `b348bb33` | — | **2100** tests, 0 failed, 2094 passed, 6 skipped; duration **2 m 10.7 s** (wall 2:11.61, max RSS 889 MB); `/tmp/.git` absent before and after |
| O9 | no `ModelArms` folder left by a namespace run | `ls -d /tmp/testsToDelete-modelarms-* \| wc -l` before and after O8 | the machine's `/tmp` | — | **0** and **0** |
| O10 | K5 (e), K7, K9: a scaffold run as a child of the test executable can be SIGKILLed right after its first child exists, before it ends, and a private `TMPDIR` moves every temp path of that child into a folder the parent names (new for this rework; the parent's O-j ran the scaffolds to the end only) | a scratch Node script spawning `nice -n 19 <T> --explicit only --filter-method <scaffold> --no-ansi` with `TMPDIR=<private folder>`, polling that folder every 5 ms for `testsToDelete-mN-target-*`, then `SIGKILL` (mode kill), or letting it end (mode full); today's scaffolds, unchanged | four private folders under the scratchpad, one per run | the scaffolds' output under the private folder, none in `/tmp`; a kill window of more than a few ms; M3's manifest (written first today) naming every path at the kill | **M2 full:** exit 0, first child at **4652 ms**, end **8081 ms**, output `testsToDelete-m2-target-6ca8390d` under the private folder. **M2 kill:** child seen at 4416 ms, SIGKILL at 4417 ms, exit by the signal at 4473 ms; left: the target only (**no manifest**: today's M2 writes none, the RED of (8)) and `clr-debug-pipe-<pid>-…-in`, `-out`. **M3 full:** exit 0, first child at **2962 ms**, end **4241 ms**, ten entries + the manifest. **M3 kill:** seen 1751 ms, SIGKILL 1752 ms, exit 1767 ms; left the manifest, the target and the two `clr-debug-pipe-*` FIFOs; the manifest names **11** paths (target, root, home, old, bin, fake, d28, deleg, home14, keep, manifest), uid 1000. In every run `/tmp/testsToDelete-m2-*`, `-m3-*` → **0**. **Found:** a SIGKILLed .NET test process leaves its `clr-debug-pipe-*` FIFOs in its temp root (a normal exit leaves none) — so every kill test of S4 gives its child an owned `TMPDIR` (K5 (e), K7, K9). The four folders removed by exact path after |
| O11 | K9 and item 2 point 5: the drvfs mount and the owner's folder (read-only) | `grep " /mnt/c " /proc/mounts`; `awk '$2 ~ "^/mnt/c"' /proc/mounts`; `ls -ld /mnt/c/Users/strug/AppData/Local/Temp` | the machine | `/mnt/c` of type `9p`, no deeper mount; the folder present | `C:\134 /mnt/c 9p rw,noatime,aname=drvfs;path=C:\;uid=1000;gid=1000;symlinkroot=/mnt/,…` — ONE mount at or under `/mnt/c`, type `9p`, every file uid 1000; the folder exists, `drwxrwxrwx jinx jinx` (drvfs shows mode 777: a mode set there is not kept — `OwnedDir`'s `mkdir 0700` and the copied removal on drvfs are **[R]**, observed only by the one run). Nothing was written to the mount |
| O12 | K5 (e), K9: the planned kill-test and 9p lines are clean in the Node guard (the C# half **[R]**: they spell no `RawTemp` pattern, no `"/tmp"`, no piece of the prefix word; checked again by § 6 on the head) | `scanText('tests/Bench.Tests/ModelArms/PilotScaffoldKillTests.cs', …)` of the mounted guard | (a) `new Dictionary<string, string?> { ["TMPDIR"] = childTemp.Path, ["BENCH_PILOT_OUT"] = pilotOut.Path, ["TESTSTODELETE_RUN"] = null }` (b) `env["TMPDIR"] = childTemp.Path;` (c) `File.ReadLines("/proc/mounts")` (d) `name.EndsWith(".owner", …) && Owned.FullName().IsMatch(name[..^6])` (e) `LeftoversSweep.Run(new SweepOptions(scratch, DateTimeOffset.UtcNow, LeftoversSweep.MinAge, Liveness.ProbeProcess))` | `[]` each | `[]` each |

**Observed by the parent, relied on here [P]** (§ *Epoch 11* item 3.3, consumer `b348bb33`, upstream mcp `53f65130`): **O-b** (`:3770`)
row E6-l (3)'s C# half: each planned scaffold line `[]` in the copy; a rename under `Path.GetTempPath()` `raw-temp`; today's line
`legacy-name`, `owner-blind`, `raw-temp`; under `TempDir.Root` `[]` (the blind spot). **O-i** (`:3777`) `PilotOut` and its seven facts,
7 of 7 green, both files clean in both guards. **O-j** (`:3778`) both scaffolds run end to end with the resolver: unset and `/tmp`
refused before anything is made; with `/home/jinx/.cache/e11-pilot-out-<8 hex>` M2 and M3 passed, every printed path under it, M3's
manifest 11 paths, nothing in the temp root; removed by exact path. **O-l** (`:3780`) the 9p site: **blocked** on the operator's
variable — now answered by the owner (item 7, "(1), once"), so K9's run is S4's merge condition.

**Written open-ended, not observed here (red or not, the PR's run says which):** (1), (2), (4), (7), (8), (10) on today's code (their
RED) and on the head; `PilotOut` accepting the parent's `pilotOut` when the child's `TMPDIR` is `childTemp` (K5 (e), **[R]**, from O-i's
`Resolve` comparing with the child's own temp root); the 9p run itself (once); the namespace time on the head; the whole-suite FAILED
list on the head.

## 12. The `/tmp/.git` finding (the parent's item 4.5: a benchmark todo, not built in S4)

**What refuses.** `RootFacts.GitTopOf` (`tools/model-arms/ModelArms/Adapters/RootFacts.cs:19-23`) is the nearest ancestor-or-self that
holds a `.git` FILE or FOLDER (`Path.Exists`); with `/tmp/.git` present, every experiment root under `/tmp` is "inside the git checkout
/tmp" (`ExperimentFile.cs:70`). The scouts' roots refuse the same way (`ScoutRoots.cs:67`, `ProbeRoots.cs:41`,
`FileSystemGateArtifactStore.cs:45` name the checkout they found). So one `.git` entry in the temp root, made by anything, makes those
tests refuse for as long as it exists — and, since this rework, `PilotScaffoldKillTests` too (its `pilotOut` sits in `/tmp`, and
`PilotOut` refuses a folder inside a checkout).

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
sessions); when the entry appeared (`/tmp/.git` absent before and after O8's run and during O10).

**What S4 changes about it: nothing.** No cause in `ModelArms/`; the owned folders stay in `Owned.TempRoot()` = `Path.GetTempPath()`
(`Owned.cs:117-121`). The todo (item 4.5, recorded in the stories' § 12 by the amendment): name a `<temp root>/.git` loudly at the
start-of-run sweep, or run under a run-scoped temp root outside `/tmp`; and find the creator on the machine.

## 13. Notes for the parent and the stories (findings, not changes)

- **N1 — Row E6-i's counts read both guards** (O3, O4; the parent's row E6-i takes them as its baseline).
- **N2 — The construction count.** At `b348bb33` `TempFolder` is constructed at 112 sites: 108 `new TempFolder()` in 25 files and 4
  `TempFolder _temp = new()` fields (`ArmGitTestBase.cs:13`, `ExperimentRoot.cs:39`, `ShellObjectiveReader.cs:133`, `StepBench.cs:76`);
  35 files name the type **[M]** (the parent's item 1 *Corrections* took it).
- **N3 — C8 observed, and C10's "without" half seen on the way** (O1 (d); the parent's E11-11).
- **N4 — Shape G is a blind spot, not a proof** ([P] O-b: a retained path built on `TempDir.Root` is `[]` in both guards because no
  guard traces callers). S4 uses `TempDir.Root` ONLY for paths nothing creates or keeps (K4's three lines; `PilotOut`'s comparison,
  [P] O-i), within E11-4's bound; the pilots' output never touches it (K5). *Was:* "K5 uses it for scaffolds that are made on purpose
  and kept, which is why their summaries must say so" — the first version's K5, which row E6-l forbids.
- **N5 — A SIGKILLed .NET child leaves `clr-debug-pipe-<pid>-…-in`/`-out` FIFOs in its temp root** (O10). Any test in the family that
  kills a .NET child whose `TMPDIR` is the shared temp root leaves two such entries per kill there, unnamed and unclaimed. S4's kill
  tests give the child an owned `TMPDIR`; for the parent's *Found on the way* (other consumers' kill tests: coai's, creds', E6.S2's
  `TwoSweepsAtOnceTests` and `StartOfRunSweepTests` children, which run with the shared temp root — not read here). **[M]**
  2026-11-17 ~12:10, on this machine: `ls /tmp | grep -c '^clr-debug-pipe-'` → **1282** (641 pairs, the oldest 2026-11-16 22:42, the
  newest 10:54, before O10's runs, whose pipes went to their private folders); whose they are was not traced, and none was removed.

## 14. Questions — Q1–Q8 of the first version ANSWERED by the parent's item 4.2 (`:3868-3879`); one new one

| Q | Answer (item 4.2's table) | Where in this brief |
|---|---|---|
| Q1 | disposition B: the operator-named folder `BENCH_PILOT_OUT`, row E6-l; M2's probe a `TempFolder` (`:3872`) | K5 |
| Q2 | the C# guard's 33 extra findings are S4's; the literals rewritten, never exempted; `owner-blind` takes no exemption (`:3873`) | K3, § 6 |
| Q3 | `TimingLineTests.cs:311-312` a neutral sample, brief-level (`:3874`) | K2 |
| Q4 | the kept scratch INSIDE the owned folder under a plain name; the failure message the record; the docs on S4's *Files* (`:3875`) | K6, K10 |
| Q5 | the plan records within every story's PR (E11-10); `research/module_tests.md` not on S4's line; the model-arms docs are (`:3876`) | § 0.1 |
| Q6 | `TempFolder.Dispose` no longer throws; the audit names the folder (`:3877`) | K1 |
| Q7 | the 9p site in a validated folder BELOW the mount, swept at start, the merge condition on the real mount or the owner's exception (`:3878`, amended by CodeRabbit on #176: the folder may hold this repository's owner-form entries and their claims, anything else refuses); the owner answered "(1), once" (item 7, `:4071`) | K9, § 2 point 5 |
| Q8 | the killed-child test's sweep restricted through `Mine` to its own folder and claim, `Removed` holding exactly those two (`:3879`) | K7 (its folder moved to the child's private `TMPDIR`, O10: the same seam, a different root) |

**Q9 (new, brief-level, for the coordinator):** K7's sweep root moves from the real temp root to the child's private `TMPDIR` (O10's
finding), which item 4.2's Q8 wrote as "the real temp root". [Recommend: brief-level, not a change: the same seam and assertion, the
sweep over a folder the parent owns, so a killed child leaves nothing in `/tmp`; if the coordinator reads it as a change of Q8's text,
keep the real temp root and accept the two `clr-debug-pipe-*` FIFOs per run there, named in § 10.]

## 15. STOP finding — doc lines that describe what S4 changes, outside item 4.2's *Files* (for the coordinator; possibly entry 11.1)

The parent's item 8.1 asks every *Files* line to name "the docs that describe what it changes, found by a `git grep` of the old
spellings over `*.md`", each with what it says now. Read at `b348bb33` with item 4.2's own pattern
(`git grep -n -E "testsToDelete-(m2|m3)|modelarms-reader|testsToDelete-modelarms"` over `*.md`, plus `m3-manifest`, `m2-probe`,
`TempFolder`, `PilotScaffold`, `DRVFS`) **[M]**, these lines describe S4's sites in the PRESENT tense and are neither on the *Files* line
nor on its "read and NOT on the line" list:

| Line | What it says now | After S4 |
|---|---|---|
| `research/module_model_arms.md:2314` (the living module document, M3's *What it is*) | "It claims a manifest `testsToDelete-m3-manifest-<id>.json` exclusively … Names are `/tmp/testsToDelete-m3-<what>-<id>`, M2's form — the legacy form of `common.test-leftovers` §1 (this repository has not reached the producer stage …)" | wrong: the names are `bench-m3-…` under `BENCH_PILOT_OUT`, and the parent's item 7 says "`research/module_model_arms.md` says so after S4" for M2 AND M3, while item 4.2 names only M2's procedure (`:2250-2256`) |
| `research/PLAN_model_arms_E3.md:1127` (IMPLEMENTED, item 47 "M2's scaffold and what it is not") | "It writes `/tmp/testsToDelete-m2-target-<id>` … and names `/tmp/testsToDelete-m2-root-<id>` … a throwaway root (`testsToDelete-m2-probe-<id>` … removed in `finally`)" | a description of M2's output, as `:810-811`, `:876`, `:885`, `:1146` are (which get dated notes); not in the "stay" list (`:80`, `:152`) |
| `research/PLAN_model_arms_E6.md:344`, `:346` (IMPLEMENTED, M3's *The scaffold* paragraph) | "the manifest `testsToDelete-m3-manifest-<id>.json` …", "`<target>` (`testsToDelete-m3-target-<id>` …" | the paragraph after `:338-341` (*The names*, which gets a note); not named |
| `research/PLAN_model_arms_E6.md:818` (IMPLEMENTED, the growth table's `ConsumerArms` row) | "the fixture's `DisposeAsync` (the `TempFolder`, …) \| a killed test run leaves one temp folder …" | after S4 a killed run's `TempFolder` is collected by the next run's sweep; `:819`, `:821` (the next rows) get notes, `:818` is not named |

**What this brief does:** NOT add them (the instruction: a file or line S4 needs outside item 4.2's *Files* is the parent's next change,
entry 11.1). S4 does not go to its plan gate until the coordinator decides, per line: (a) on the *Files* line (a counted change, 11.1,
with its consult), or (b) read as history and stays (as item 4.2 reads `:1308` and the E1/E3/E4 lines), written into item 4.2's "read and
NOT on the line" list. [Recommend: `module_model_arms.md:2314` is NOT history (present tense, the living document, and item 7 promises
the module document says so after S4): (a). The three IMPLEMENTED-plan lines: a dated note each, as their neighbours get, so (a) too —
one cause (item 4.2's *Files* missed four lines of the same grep), one entry.]
