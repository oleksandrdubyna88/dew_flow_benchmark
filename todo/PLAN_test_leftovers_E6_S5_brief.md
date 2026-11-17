# E6.S5 brief — the rest of `Bench.Tests`, the fakes and the product seams (rows E6-k, E6-b seam; D4, D5, D6; claim C8's S5 half)

> Status: **plan only, nothing implemented yet, 2026-11-17; the item 8.1 observations made before its plan gate (§ 11); its § 13 finding (six doc lines in five files outside S5's *Files*) CLOSED by the owner 2026-11-17: history, (b), not a change and not entry 11.2, their update filed as follow-up F45 in the conventions follow-ups plan; Q1–Q5 answered (§ 14); ready for its plan gate.** The brief of story S5 of `todo/PLAN_test_leftovers_E6.md` as the epoch-11 amendment writes it (§ 6 S5 `:612-662`; rows E6-b `:94`, E6-k `:109`; claim C8 `:166`; D4 `:268-276`, D5 `:278-284`, D6 `:286-335`; § 6's first paragraph `:412-416`; § 11 `:806-842`). Its scope is S5's *Files* line (`:653-655`) as named paths (§ 0.1). The parent is at epoch 11 with ONE change (entry 11.1): **the next accepted change is the parent's ELEVENTH FULL STOP.**
>
> Judge's brief (Opus) for a Sonnet coder. Written 2026-11-17. Read-only inputs, all verified: benchmark worktree `/home/jinx/git/wt/bench-e6s5`, branch
> `feat/e6-s5-rest` from `main` `e967df53` (#221: E6.S4; #220: the stories amended to epoch 11; #219: E6.S3; #218: F42-c, the copy at mcp
> `53f65130`), mount `.agents/conventions` at `b1746499`. **The parent** is `dew_flow_conventions · todo/PLAN_test_leftovers_rollout.md` at
> conventions `main` `488bdce2` (#177): ledger entry 11.1 `:900` (state open; the next accepted change is the eleventh full stop); § *Epoch 11*
> items 3.2 (E11-3 `:3746`, E11-4 `:3747`, E11-5 `:3748`, E11-10 `:3753`, E11-11 `:3754`), 3.3 (O-a `:3776`, O-c `:3778`, O-g `:3782`, O-h
> `:3783`), 4.3 (`:3888-3933`: the brief lists its exemptions), 8.1 (`:4088-4108`), 8.2 (S5's row `:4115`; *Found on the way* `:4120-4123`).
> **"Stories"** is `todo/PLAN_test_leftovers_E6.md` on `main` `e967df53`. Every code `file:line` is at `e967df53` (no file outside
> `ModelArms/` changed between `b348bb33` and `e967df53`, § 11 O1, so the parent's line numbers hold). **[M]** is measured for this brief,
> read-only; **[O]** a claim OBSERVED in a scratch clone with the six things of item 8.1 (§ 11); **[P]** observed by the parent's item 3.3 (its
> O-row named); **[R]** read from code and NOT run (written open-ended, "red or not, the run says which"); **[D]** taken from a named document.

## 0. What you build, in one paragraph

Every test outside `ModelArms/` and `Leftovers/` that makes a folder or a file outside its process makes it inside ONE owned folder from
the copied helper (`TempDir.For(what)`, or `OwnedDir.Create(what)` where the folder's own name is the identity), claimed first and removed
by its exact path (D5): the private temp classes (`TempSuite`, `TempBank`, `TempRoot`, `TempGitRepo`, `DatedGitRepo`, …) become thin
wrappers over it and their siblings move inside; the eight never-removed sites of row E6-k (and a ninth this brief found, § 11 O9) are
removed with it. The 29 lines that only NAME a path nothing creates read `TempDir.Root` (shape G, within E11-4's bound); the two lines whose
callee cannot be proved take an owned folder. The fake CLI recognises an owner-form model id by its segments and never makes its home (the
first write into a missing home exits 3), with ONE listed `raw-temp` exemption; the probe driver mints that id from an owned folder. The fake
coai refuses only where it first needs its data folder, and its test driver sets `COAI_DATA_DIR` inside its owned root (D6). The product
gains a root seam for its scratch worktrees (`HarvestGates`, `SolutionSignals`, `ScratchWorktree.DefaultRoot`) with no change for its own
callers (D4), and `DatedGitRepo` keeps its repository and its scratch root in one owned folder. Three C#-only lines and the `.ndjson` line
are rewritten so no guard reads them. Then a docs commit (`research/module_tests.md`, the stories' § 11 list). ONE pull request, in this
repository only.

## 0.1 Scope — S5's *Files* as named paths, each with the dependency that needs it

The stories' S5 *Files* (`:653-655`): `tests/Bench.Tests/**` outside `ModelArms/` and `Leftovers/`; `tests/FakeCli/Program.cs`,
`tests/FakeCoai/Program.cs`; `src/Bench.Infrastructure/Git/{ScratchWorktree,HarvestGates,SolutionSignals}.cs`; `research/module_tests.md`; the
plan records (§ 6's first paragraph). Every path below is inside it.

| Path | Needed for | Bound |
|---|---|---|
| the 48 files under `tests/Bench.Tests/` (outside `ModelArms/`, `Leftovers/`) that hold § 4's guard findings, and `Gate/Assessment/FindingAssessorTests.cs`, `Infrastructure/HarvestGatesTests.cs`, `Infrastructure/SolutionSignalsTests.cs` | the 73 / 75 guard findings (E11-5), E6-k, D5, D6, D4's tests | the sites of § 4 and their class's construction and `Dispose`; K2's `:44`; the calls of K9 |
| NEW test files under `tests/Bench.Tests/` (outside `ModelArms/`, `Leftovers/`): `Infrastructure/TempLeftoverChildRunTests.cs`, `Probes/Driver/FakeCliHomeTests.cs`, `Gate/Driver/FakeCoaiDataFolderTests.cs`, `Infrastructure/ScratchRootSeamTests.cs`, `Infrastructure/DatedGitRepoKillTests.cs` | the scenarios of § 5 | new files only (the coordinator ruled for S4 that a new file inside the *Files* glob is brief-level) |
| `tests/Bench.Tests/Fixtures/scouts/claude-2.1.289-two-children-models.ndjson` | the Node guard's `raw-worktree` on `:2` (K5) | the two `worktree add` spellings on line 2 only |
| `tests/FakeCli/Program.cs` | D6, the fake CLI (K6) | `FakeHome` (`:657-670`), `Events.Record` (`:677-680`) and their summaries |
| `tests/FakeCoai/Program.cs` | D6, the fake coai (K7) | `DataDir` (`:417`) and its summary |
| `src/Bench.Infrastructure/Git/ScratchWorktree.cs`, `HarvestGates.cs`, `SolutionSignals.cs` | D4 (K8) | `DefaultRoot`, the 4-parameter `AddAsync` (`:13-16`); one overload each of `RunAsync` (`HarvestGates.cs:50`, `SolutionSignals.cs:53`) |
| `research/module_tests.md` | the fakes, the seams and what they do not prove (K12) | § *Test leftovers* (`:632-672`) and the harness paragraph `:19` |
| the plan records (§ 6's first paragraph, E11-10): this brief, its `todo/README.md` row, its promotion (`research/PLAN_test_leftovers_E6_S5_brief.md`, `research/README.md`), the stories' progress line, and the copy of § 3a's list into the stories' § 11 (item 4.3) | `common/planning-docs.md`; item 4.3 | those records |

**Not touched, by name:** `tests/Bench.Tests/ModelArms/**` (S4's; `StepBench.cs:64` stays, § 12 N1), `tests/Bench.Tests/Leftovers/**`
(every manifest target, `template-manifest.json`, `TheAdaptedCopyTests.cs`, `TempDir.cs`, `OwnedDir.cs`, `SuiteChild.cs`: used, never
edited), `hosts/` (`HarvestCommand.cs:242`, `SolveCommand.cs:184` keep the old signatures), every other `src/` file (the product's own temp
sites, such as `FileSystemGateAssessmentFiles.cs:33`, stay the product's: § 12 N3), `tools/`, `spikes/` (S6), `.github/` (S7),
`http/README.md` (S7), and every `research/` document other than `module_tests.md` (§ 13). **A file the build, a test or a check needs
outside the table above, or a decision outside the parent's § *Epoch 11*, is a change of the parent's epoch 11, and after entry 11.1 the next
one is the ELEVENTH FULL STOP: STOP, write the finding into this brief, and hand it to the coordinator; do not add the file.**

## 1. Rules you must read first

- `.agents/PROJECT.md` (commands; never `dotnet test`; exe + argv with a timeout; warnings are errors; files under 800 lines).
- `.agents/conventions/common/test-leftovers.md`, its detail (§1 the name, its last item: what lives INSIDE an owned folder carries no prefix of
  its own; §2 the helper, the child's `TMPDIR`, "never throws"; §4 the sweep; §7 the guard classes and the exemption grammar) and
  `common/testing.md` (RED first; revert the line the behaviour depends on; a structural test needs a live companion).
- The stories (at `e967df53`): rows E6-b `:94`, E6-k `:109`; claim C8 `:166`; D4–D6 `:268-335`; § 6 S5 `:612-662`; § 7; § 11 `:806-842`.
- The parent § *Epoch 11*: items 3.2 (E11-3 to E11-5, E11-10, E11-11), 3.3 (O-a, O-c, O-g, O-h), 4.3, 8.1 (every site classified by
  lifetime; both guards with a count per class; shape G only for a path nothing creates or keeps; the brief lists its exemptions), 8.2.
- The S4 brief `todo/PLAN_test_leftovers_E6_S4_brief.md` (K7: the killed-child pattern this brief reuses; § 11: how an observation is
  written) and the S3 brief `research/PLAN_test_leftovers_E6_S3_brief.md` § 11.
- No Python anywhere. No Docker here (the Docker-bound classes run on CI, § 2 point 5). No `git stash`. Never set `MODEL_ARMS_DRVFS_SCRATCH`.
  Commit only; the coordinator pushes and opens the PR.

## 2. Preconditions and merge conditions

1. S3 merged (#219), so F41-b (mount `b1746499`) and F42-c (the copy at mcp `53f65130`) are in; S4 merged (#221, `e967df53`): met **[M]**.
2. The stories amendment (#220) merged before this brief's plan gate (the parent's item 2 point 2): met **[M]**.
3. § 13's STOP finding answered before the plan gate: met, the owner 2026-11-17, (b) history, follow-up F45 (§ 13).
4. At most three open PRs here, or the owner's allowance for E6 PRs over the cap (2026-11-16).
5. **The Docker-bound classes run on S5's CI** (`build-test`, Docker on `ubuntu-latest`): `GateProtocolTests`, `GateCampaignTests`,
   `GateDriverCommandTests` (the fake coai's launches, [P] O-g), `ScoutsOutOfProcessTests` (the fake CLI's launch with a test-given `TEMP`,
   [P] O-h), `VettingPassTests` (two E6-k sites) and the other 65 Postgres-bound classes (§ 11 O8: all 572 local failures are those 70
   classes). They are **blocked** here; a red there is S5's own fix in `tests/`, inside § 0.1.
6. CI checks of the head SHA exist and are green (`build-test`, `contract`, `plans`, `workflows`); the coai code gate and the Opus review
   are done; every RED of § 5 quoted on the PR.

## 3. Decisions of this brief (each with its reason)

- **K1 — The helper calls, and one rule for the path they replace.** `TempDir.For(what)` (`tests/Bench.Tests/Leftovers/TempDir.cs:28`,
  namespace `Bench.Tests`, so every `Bench.Tests.*` file sees it without a `using`): one claimed folder `testsToDelete-<owner>-bench-<what>-<id>`,
  `.Path`, `.At(parts)`, `Dispose` never throws. `OwnedDir.Create(what)` (`OwnedDir.cs:35`, `using Bench.Tests.Leftovers;`) only where the
  folder's own leaf is the identity (the fake CLI's model id, K6) or where the S4 brief's shape is copied. `TempDir.Root` only for shape G
  (K3). **The rule:** the path a site made becomes a CHILD of the owned folder with the SAME existence as before — made where the old code
  made it, absent where the old code left it absent (a product or a test creates it later, or asserts it is missing) — and the class's
  `Dispose` becomes the owned folder's `Dispose`, after any link-aware walk the class already has (`FileSystemLinkTargetsTests.cs:16-25`,
  `ScoutCampaignKeyTests.cs:16-24`, `GateStoreFixtures.cs:73-…`: they delete links first and keep doing so). Where the old code created the
  folder itself at once (`CreateTempSubdirectory`, `Directory.CreateDirectory` of the same path), the owned folder's `.Path` may stand for it
  ONLY if nothing walks to its parent (K2: `FilesystemEngineTests.cs:63` does). One owned folder per instance, never a second top-level
  folder (D5); a class with several paths puts them all inside (`TempRoot.Sibling`, `DatedGitRepo.ScratchRoot`). Each `what` is at most 24
  characters with no separator (`Owned.Room` under the longest owner, `Owned.cs:82`); § 4 names each. Every planned call is clean in both
  guards (§ 11 O6).
- **K2 — Row E6-k: the eight never-removed sites, and a ninth.** `DailyRunFileSinkTests.cs:92`, `FilesystemEngineTests.cs:304`,
  `QlnEngineTests.cs:287`, `VettingPassTests.cs:326`, `:339`, `TracePortTests.cs:166`, `:183`, `:268` (the stories' list) move into an owned
  folder that the test class disposes: the class gains `: IDisposable` and ONE `private readonly TempDir _work = TempDir.For(<what>)` (xUnit
  makes an instance per test, so a folder per test), and the static helper becomes an instance method making a fresh CHILD inside it
  (`Directory.CreateDirectory(_work.At(Guid.NewGuid().ToString("N"))).FullName`); `TracePortTests`' three fixtures take `using var fixture =
  TempDir.For("trace-fixture")` in the test. The root of `FilesystemEngineTests.Build` MUST be such a child, never `_work.Path`: the test at
  `:57-63` writes `Path.Combine(Directory.GetParent(root)!.FullName, "outside.txt")`, which today lands IN THE TEMP ROOT and is never removed
  (§ 11 O8; neither guard reads that line). **The ninth:** `Gate/Assessment/FindingAssessorTests.cs:44` constructs the product's
  `FileSystemGateAssessmentFiles` (which makes `<temp>/bench-assess-<guid>`, `src/…/FileSystemGateAssessmentFiles.cs:33`) and never
  disposes it; `using var files = …` there (§ 11 O9: two `bench-assess-*` per run of that class). It is the same condition as row E6-k's
  ("a test that makes a temp path removes it"), in a file on S5's *Files*: brief-level, Q2.
- **K3 — Shape G, within E11-4's bound.** The 29 lines O-c read as creating nothing ([P] O-c; the stories' S5 (d) `:637-645`) replace
  `Path.GetTempPath()` by `TempDir.Root`, character for character (`TempDir.Root` = `Owned.TempRoot()` = `Path.GetTempPath()`,
  `Owned.cs:117-121`, so every equality a test asserts holds, `ScoutLaunchProcessTests.cs:100` included). The two O-c could not prove
  take an owned folder: `Cli/GateDriverCommandTests.cs:33` (`--artifact-root`) and, in the same statement, `:32`'s missing suite file:
  `using var artifacts = TempDir.For("gate-artifacts")`, `artifacts.At("no-such-suite.json")`, `"--artifact-root", artifacts.Path`;
  `Models/CliAgentLiveTests.cs:83` (a real CLI's working directory): the class gains `: IDisposable` with `TempDir.For("cli-live")` and
  `Ask` reads its `.Path`. Never `TempDir.Root` for a path something creates or keeps (row E6-l, E11-4).
- **K4 — The three C#-only lines (E11-5), rewritten, never exempted.** `Gate/Driver/GateReplyParserTests.cs:125`, `:132`: every `/tmp/` in
  the sample stderr and in the expected prompt file becomes `/srv/` (`/srv/coai-answers-1/api-1.prompt.md`, `/srv/s.json`,
  `/srv/coai-answers-1/api-1.answer.json`); the parser's pattern does not read the folder (`--prompt-file (\S+) --schema-file \S+ --out (\S+)`,
  `src/Bench.Domain/Gate/GateReplies.cs:175` **[R]**). `Probes/ProbeContractsGuardTests.cs:19`: the entry `"ProbeCellReportDto.LatestState"`
  becomes `nameof(ProbeCellReportDto) + "." + nameof(ProbeCellReportDto.LatestState)` — a CONCATENATION. **Not** the interpolated
  `$"{nameof(ProbeCellReportDto)}.{nameof(ProbeCellReportDto.LatestState)}"`, which the C# copy still reads as `owner-blind` (`testSt`): § 11
  O7. The product's member names do not change; the set's other entries stay literals.
- **K5 — The `.ndjson` line (the stories' S5 *What* `:618-620`).** Line 2 of `Fixtures/scouts/claude-2.1.289-two-children-models.ndjson` is a
  `system`/`hook_response` event whose `output` quotes this family's instructions; it spells `worktree add` twice. No test reads a
  `hook_response` (`git grep -n 'hook_response\|hook_event' -- src tests` → nothing **[M]**); the seven readers of the fixture read its
  `assistant` and `user` events (`ClaudeSubagentsTests`, `ScoutReadBackTests`, `ScoutModelFallbackTests` **[R]**). Each `worktree add` on that
  line becomes `worktree add`: a JSON escape, so the parsed string is byte for byte the same and no test can see it, while the file's
  text no longer spells the two words together (§ 11 O6: Node `[]`; the C# copy does not read `.ndjson`). Re-run the three reader classes.
- **K6 — The fake CLI (D6 as restated after the parent's independent review; [P] O-a, O-h).** In `tests/FakeCli/Program.cs`: `FakeHome.For`
  becomes three-way — an id of the owner form recognised by its SEGMENTS, without the prefix word
  (`private static readonly Regex OwnerForm = new("^[A-Za-z]+-[uw][0-9]{1,10}-bench-", RegexOptions.CultureInvariant);`) → `<TEMP>/<id>`;
  any other id → `<TEMP>/bench-fake-cli/<model>` as today (`FAKE_CLI_HOME` keeps winning in `Of`, `:668-669`). Its ONE temp-root line carries
  the § 3a marker on the line directly above it. The fake NEVER makes its home: `Events.Record` (`:677`), when `FAKE_CLI_EVENTS` is unset and
  the home does not exist, writes one line (`fake-cli: no home <home> for the model id <model>: a test makes it first`) and exits 3 before
  any `CreateDirectory`; `--version` writes nothing and still answers ([P] O-h: a refusal in `FakeHome.For` itself broke
  `ScoutPinReaderTests`). The summary (`:657-661`) says so. In `tests/Bench.Tests/Probes/Driver/FakeCli.cs`: `private readonly OwnedDir _owned
  = OwnedDir.Create("fakecli");` `Model = Path.GetFileName(_owned.Path)` (`testsToDelete-u1000-bench-fakecli-<6>`, 40 characters, which
  `ModelRef.Parse` and `ProbeSubject.Parse` accept, [P] O-a), `Home = _owned.Path`, `Dispose() => _owned.Dispose()`; the summary (`:5-9`)
  names the owned folder. `Scouts/ScoutLaunchProcessTests.cs:129`: the class gains `: IDisposable` with `TempDir.For("scout-fake")`, and
  `Home()` returns a fresh child path inside it (`ScoutFake` makes it, `ScoutFake.cs:18`). Observed with every user of the driver,
  ModelArms included: § 11 O10.
- **K7 — The fake coai (D6; [P] O-g).** In `tests/FakeCoai/Program.cs`: `DataDir()` (`:417`) has no fallback: with `COAI_DATA_DIR` unset or
  empty it writes one line (`fake-coai: COAI_DATA_DIR is unset; the fake makes no data folder of its own`), flushes and exits 4 — reached only
  where the folder is first needed (`:247`, `:359`, `:378`), never at start; `--version` (`:18`) and `--probe-api` never call it. In
  `tests/Bench.Tests/Gate/Driver/FakeCoai.cs`: `private readonly OwnedDir _owned = OwnedDir.Create("gate-fake");` `Root = _owned.Path` (the
  events folder and the script inside it as today), `Environment` sets `env["COAI_DATA_DIR"] = Path.Combine(Root, "data")` BEFORE the
  `extra` loop (`:58`), so a test's own value still wins, and `Dispose() => _owned.Dispose()`. No product file changes.
- **K8 — The product seams (D4).** `ScratchWorktree`: `public static string DefaultRoot { get; } = Path.Combine(Path.GetTempPath(),
  "bench-scratch");` — today's string, now named — and the 4-parameter `AddAsync` (`:13-16`) delegates with it. `HarvestGates.RunAsync`
  (`:50`) and `SolutionSignals.RunAsync` (`:53`) each gain an overload with a trailing `string scratchRoot`, passed to
  `ScratchWorktree.AddAsync(…, underRoot)` (`:21-22`); the existing signatures delegate with `ScratchWorktree.DefaultRoot`. Product callers do
  not change (`hosts/Cli/HarvestCommand.cs:242`, `SolveCommand.cs:184`; `CliSubjectRuntime.cs:88` already passes its root), so a benchmark
  run names its worktrees as today. The C# copy reads `src/` as the `Source` kind, which reports no raw temp spelling (claim C4) and no
  `worktree add` (C8): the new lines are `[]` there, and the Node guard does not scan `src/` (§ 11 O6). Complexity stays at most 4.
- **K9 — `DatedGitRepo` (`Infrastructure/DatedGitRepo.cs:22`).** `private readonly TempDir _dir = TempDir.For("dated-git");`
  `Root => _dir.At("repo")` (made by `InitAsync`, `:40`, as today), a new `public string ScratchRoot => _dir.At("scratch")` (made by git's
  `worktree add`), `Dispose() => _dir.Dispose()` (the copied removal clears read-only files on Windows, F42's N1; on Linux the owner removes
  read-only files of its own folder). `Url` keeps `new Uri(Root)`. `HarvestGatesTests` and `SolutionSignalsTests` pass `_repo.ScratchRoot` to the
  new overloads; a test's worktree is then INSIDE the folder that also holds its repository (rule §1, last item), and a kill is collected
  with the folder (case 15's real-world twin, § 5 (5)).
- **K10 — The private temp classes (D5).** Each becomes a wrapper as § 4 says: `TempCatalog`, the four `TempSuite`s, `TempLogRoot`,
  `TempFixBank`, `TempStatement`, the two `TempBank`s, `TempRunSuite`, `TempSuiteFile`, `TempSpool`, `TempRubrics`, `TempRoot`
  (`GateStoreFixtures.cs:61`), `TempGitRepo` (`GitCheckoutTests.cs:147-…`), and the fields and locals of § 4. `TempRoot.Sibling(name)`
  (`:71`) is unchanged and now lands INSIDE the owned folder, because `Path` becomes `_dir.At("root")`; today its siblings (`<guid>-mirrors`,
  `<guid>-work`) are left in the temp root (§ 11 O8). `TempGitRepo.CreateAsync` makes `TempDir.For("git-src")`, the repository at
  `.At("repo")`, and disposes the `TempDir` if `git init` or a commit throws; `TempGitRepo.DeleteTree` stays (other callers).
- **K11 — RED first for row E6-k, as a lasting test (the stories' S5 *Scenarios* `:649-650`).** `Infrastructure/TempLeftoverChildRunTests.cs`,
  one `[Theory]` over the classes that left a temp path: `DailyRunFileSinkTests`, `FilesystemEngineTests`, `QlnEngineTests`, `TracePortTests`,
  `FindingAssessorTests`. Per class: an owned `TempDir.For("leftover-run")` as the CHILD's temp root, `SuiteChild.Run(["--no-ansi",
  "--filter-class", "*." + name], env: { TMPDIR = that folder, TESTSTODELETE_RUN = null }, timeout: 5 min)` (`SuiteChild.cs:21`); assert exit 0
  and that the folder holds NO entry. Observed today: the same command leaves 23, 14, 5, 3 and 2 entries (§ 11 O8, O9), and a normal exit
  leaves no runtime file there (O8). `VettingPassTests` (Docker) is the same theory's sixth row, gated as the Docker proofs are: the coder
  reads `FixtureContainerTests`' gate (FAIL under Actions without a daemon, skip elsewhere) and reuses it, never a new rule.
- **K12 — The docs.** `research/module_tests.md` § *Test leftovers*: a paragraph *The fakes and the scratch seams* (the fake CLI's three-way
  home, the refusal at the first write, the probe driver's owner-form id; the fake coai's refusal at the point of need and the driver's
  `COAI_DATA_DIR`; `ScratchWorktree.DefaultRoot` and the overloads, the product's own runs unchanged; `DatedGitRepo.ScratchRoot`), the new
  tests in its command list, and, in *The limits*, what § 10 says S5 does not prove. `:19`'s harness sentence gains "the fake's home is the
  test's owned folder". No other `research/` file (§ 13).

## 3a. The inline exemptions S5 adds (listed here before the plan gate; the PR copies this list into the stories' § 11, item 4.3)

| File:line | Class | Reason (after `testsToDelete-exempt: raw-temp — `) |
|---|---|---|
| `tests/FakeCli/Program.cs`, the one `Path.GetTempPath()` line inside `FakeHome.For` (today `:664`; the head's line number recorded on the PR) | `raw-temp` | `the fake's TEMP is the one its launcher passed, and the home under it is a folder the test made first` |

The marker stands on its own line directly above that line (a marker clears its own line and the next only, S4 O2 (d)). No other
exemption: none for `raw-worktree` (the `.ndjson` line is rewritten), none for `legacy-name`, none for `owner-blind` (it takes none).

## 4. The changes, file by file (every guard finding of S5's area, each with its lifetime)

Lifetime classes (the parent's item 4.2 table): **run** (the test makes it and removes it) → the helper; **inside** (inside an owned folder)
→ a plain name inside; **never made / read only / sample** → shape G or a neutral literal; **outlive** → none in S5; **old name** → none in S5.
`C#`/`N` = the finding is in the C# copy's list / the Node guard's list at `e967df53` (§ 11 O2–O4).

| # | Site (`tests/Bench.Tests/` unless named) | C# N | Lifetime | Today | S5 |
|---|---|---|---|---|---|
| 1 | `ArchitectureTests.cs:357` | C N | run | fixed `<temp>/bench-arch-planted-vendors-literal`, deleted in `finally` | `using var work = TempDir.For("arch-planted"); var root = work.At("root");` the `finally` delete goes; the summary's "a fixed directory per scenario … runs reuse one artefact" becomes "an owned folder per run" |
| 2 | `Authoring/PromptCatalogTests.cs:132`, `:138` (`TempCatalog`) | C N ×2 | run | `bench-prompts-<guid>` | field `TempDir _dir = TempDir.For("prompts")`; `Root = _dir.At("catalog")` (made by `Copy`/`CreateDirectory` as today); `Dispose` → `_dir.Dispose()` |
| 3 | `Authoring/VettingPassTests.cs:326`, `:339` | C N ×2 | run (E6-k) | `CreateTempSubdirectory("bench-empty-tree")`, `("bench-anchored-tree")`, never removed | K2: the class `: IDisposable`, `TempDir.For("vetting")`; `Empty()` and `Anchored()` make children inside it. Docker-bound: CI |
| 4 | `Cli/CliContractTests.cs:107` (`TempSuite`) | C N | run | `bench-suite-<guid>.json` | `TempDir.For("suite")`, `Path = _dir.At("suite.json")`, `Dispose` → `_dir.Dispose()` |
| 5 | `Cli/CliLoggingTests.cs:127` (`TempLogRoot`) | C N | run | `CreateDirectory(bench-logs-<guid>)` | `TempDir.For("logs")`, `Path = _dir.Path`; `Dispose` → `_dir.Dispose()` |
| 6 | `Cli/FixRunPlanningTests.cs:96` (`TempFixBank`) | C N | run | `bench-fixbank-<suffix>.json` | `TempDir.For("fixbank")`, `_dir.At($"fixbank-{suffix}.json")`. Docker-bound |
| 7 | `Cli/HarvestLandingTests.cs:193` (`TempStatement`) | C N | run | `bench-stmt-<guid>.md` | `TempDir.For("stmt")`, `_dir.At("statement.md")`. Docker-bound |
| 8 | `Cli/ModelsCommandTests.cs:237` (`TempSuite`) | C N | run | `bench-models-suite-<guid>.json` | `TempDir.For("models-suite")`, `.At("suite.json")`. Docker-bound |
| 9 | `Cli/QuestionsCommandTests.cs:156` (`TempBank`) | C N | run | `bench-bank-<suffix>.json` | `TempDir.For("bank")`, `.At($"bank-{suffix}.json")`. Docker-bound |
| 10 | `Cli/RunCheckoutTests.cs:25` (`_cacheRoot`) | C N | run (the product's cache inside) | `bench-run-cache-<guid>`, made by the product, `DeleteTree` at the end | field `TempDir _cache = TempDir.For("run-cache")`, `_cacheRoot => _cache.At("cache")`; `DisposeAsync` → `_cache.Dispose()`. Docker-bound |
| 11 | `Cli/RunCheckoutTests.cs:116` (`TempSuite`) | C N | run | `bench-checkout-suite-<guid>.json` | `TempDir.For("checkout-suite")`, `.At("suite.json")` |
| 12 | `Cli/RunCommandTests.cs:115` (`TempRunSuite`) | C N | run | `bench-run-suite-<guid>.json` | `TempDir.For("run-suite")`, `.At("suite.json")` |
| 13 | `Cli/RunRetrievalTests.cs:246` (`TempSuiteFile`) | C N | run | `bench-retrieval-suite-<guid>.json` | `TempDir.For("retrieval-suite")`, `.At("suite.json")`. Docker-bound |
| 14 | `Cli/SweepRecoveryTests.cs:115` (`TempSuite`) | C N | run | `bench-sweep-suite-<guid>.json` | `TempDir.For("sweep-suite")`, `.At("suite.json")`. Docker-bound |
| 15 | `Cli/TelemetryCommandTests.cs:292` (`TempSpool`) | C N | run | `CreateTempSubdirectory("bench-spool")` | `TempDir.For("spool")`, `Path = _dir.Path` |
| 16 | `Cli/TempBank.cs:16` | C N | run | `bench-lanebank-<suffix>.json` | `TempDir.For("lanebank")`, `.At($"lanebank-{suffix}.json")`; `Dispose` → `_dir.Dispose()` |
| 17 | `Diagnostics/DailyRunFileSinkTests.cs:92` | C N | run (E6-k) | `CreateTempSubdirectory("bench-daily-log")`, never removed | K2: `TempDir.For("daily-log")`, `TempRoot()` an instance method making a child |
| 18 | `Engines/FilesystemEngineTests.cs:304` | C N | run (E6-k) | `CreateTempSubdirectory("bench-fs-engine")`, never removed; `:63` writes `outside.txt` into its parent = the temp root | K2: `TempDir.For("fs-engine")`; `Build()` an instance method whose root is a CHILD, so `:63`'s file lands inside the owned folder |
| 19 | `Engines/QlnEngineTests.cs:287` | C N | run (E6-k) | `CreateTempSubdirectory("bench-qln")`, never removed | K2: `TempDir.For("qln-engine")`, `Build()` makes a child |
| 20 | `Gate/Assessment/GateRubricsTests.cs:75` (`TempRubrics`) | C N | run | `bench-rubrics-<guid>`, made by the constructor | `TempDir.For("rubrics")`, `Path = _dir.At("rubrics")` |
| 21 | `Gate/Driver/FakeCoai.cs:16` | C N | run | `<temp>/bench-gate-fake/<guid>` (the parent never removed, O8) | K7 |
| 22 | `Gate/Driver/SettingsCheckTests.cs:12` (`_data`) | C N | run | `<temp>/bench-gate-settings/<guid>` | `TempDir.For("gate-settings")`, `_data = _dir.At("data")` (absent until the test writes, as today); `Dispose` → `_dir.Dispose()` |
| 23 | `Infrastructure/CliSubjectRuntimeTests.cs:65` | C N | run | `bench-subject-guard-<guid>`, deleted in `finally` | `using var work = TempDir.For("subject-guard"); var root = work.At("root");` the delete goes |
| 24 | `Infrastructure/DatedGitRepo.cs:22` | C N | run | `<temp>/bench-dated-git/<id>` | K9 |
| 25 | `Infrastructure/FileSystemLinkTargetsTests.cs:14` | C N | run | `CreateTempSubdirectory("scout-links-")` | `TempDir _dir = TempDir.For("scout-links")`, `_root = new DirectoryInfo(_dir.Path)`; `Dispose`: the link loop, then `_dir.Dispose()` |
| 26 | `Infrastructure/GateStoreFixtures.cs:65` (`TempRoot`) | C N | run, siblings inside | `<temp>/bench-gate-tests/<guid>` and siblings `<guid>-<name>` | K10: `TempDir.For("gate-tests")`, `Path = _dir.At("root")` (made), `Sibling` unchanged; `Dispose`: `RemoveLinks` over `Path` and every sibling present, then `_dir.Dispose()` |
| 27 | `Infrastructure/GitCheckoutLocalSourceTests.cs:23` (`_cache`) | C N | run | `bench-cache-<guid>` | `TempDir.For("local-cache")`, `.At("cache")` |
| 28 | `Infrastructure/GitCheckoutTests.cs:20` (`_root`) | C N | run | `bench-cache-<guid>` | `TempDir.For("checkout-cache")`, `.At("cache")` |
| 29 | `Infrastructure/GitCheckoutTests.cs:167` (`TempGitRepo`) | C N | run | `bench-src-<guid>` | K10 |
| 30 | `Infrastructure/ScoutCampaignKeyTests.cs:14` | C N | run | `CreateTempSubdirectory("scout-key-")` | as 25, `TempDir.For("scout-key")` |
| 31 | `Models/WorkspaceTrustTests.cs:21` (`_home`) | C N | run | `bench-trust-<guid>` | `TempDir.For("trust")`, `_home = _dir.At("home")` |
| 32 | `Probes/Driver/FakeCli.cs:15` | C N | run | `<temp>/bench-fake-cli/fake-<guid>` (the parent never removed) | K6 |
| 33 | `Scouts/ScoutLaunchProcessTests.cs:129` (`Home()`) | C N | run | `<temp>/bench-scout-fake/<guid>` | K6 |
| 34 | `Scouts/ScoutScrubberTests.cs:56` | C N | run | `CreateTempSubdirectory("scout-hygiene-")`, `finally` delete | `using var work = TempDir.For("scout-hygiene"); var folder = new DirectoryInfo(work.Path);` the delete goes |
| 35 | `Trace/TracePortTests.cs:166`, `:183`, `:268` | C N ×3 | run (E6-k) | `CreateTempSubdirectory("bench-fixture")`, never removed | K2: `using var fixture = TempDir.For("trace-fixture"); var path = fixture.At("<name>.json");` |
| 36 | `tests/FakeCli/Program.cs:664` | C N | the launcher's `TEMP` | `<TEMP>/bench-fake-cli/<model>` | K6, § 3a |
| 37 | `tests/FakeCoai/Program.cs:417` | C N | run (the driver's root) | fallback `<TEMP>/fake-coai-data` | K7: no fallback |
| 38 | `Cli/GateDriverCommandTests.cs:32`, `:33` | C N ×2 | not provable (`:33`) → run | `GetTempPath()` as the missing suite's folder and as `--artifact-root` | K3: one `TempDir.For("gate-artifacts")` for both. Docker-bound class |
| 39 | `Models/CliAgentLiveTests.cs:83` | C N | not provable → run | a real CLI's working directory | K3: `TempDir.For("cli-live")` |
| 40 | the other 28 shape-G lines: `Authoring/AuthoringPassTests.cs:280`, `:292`; `Cli/GateAaCommandTests.cs:214`; `Cli/GateAssessCommandTests.cs:48`; `Engines/FilesystemEngineTests.cs:184`; `Engines/QlnEngineTests.cs:301`; `Engines/QlnEngineLiveTests.cs:174`; `Gate/Driver/ProductPinReaderTests.cs:72`; `Infrastructure/FileSystemGateArtifactStoreTests.cs:237`, `:238`, `:246`; `Infrastructure/GitCheckoutLocalSourceTests.cs:93`; `Infrastructure/ProcessRunnerCancellationTests.cs:28`; `Infrastructure/ProcessRunnerStderrSinkTests.cs:22`; `Infrastructure/ProcessRunnerTests.cs:15`, `:26`, `:39`, `:56`, `:75`, `:92`, `:107`, `:138`; `Models/AssessorArgvTests.cs:78`; `Scouts/ScoutLaunchProcessTests.cs:100`, `:107`; `Suites/AnchorCheckTests.cs:103`, `:110`; `Trace/TracePortTests.cs:257` | C N ×28 | never made / read only ([P] O-c) | `Path.GetTempPath()` | K3: `TempDir.Root` |
| 41 | `Gate/Driver/GateReplyParserTests.cs:125`, `:132` | C only ×2 | sample text | `"/tmp/…"` | K4: `/srv/…` |
| 42 | `Probes/ProbeContractsGuardTests.cs:19` | C only (`owner-blind`) | not a path | the literal `"ProbeCellReportDto.LatestState"` | K4: `nameof(ProbeCellReportDto) + "." + nameof(ProbeCellReportDto.LatestState)` |
| 43 | `Fixtures/scouts/claude-2.1.289-two-children-models.ndjson:2` | N only (`raw-worktree`) | sample text | `worktree add` ×2 | K5: `worktree add` |

Count check: rows 1–37 hold the 41 lines O-c reads as creating (rows 36–37 the fakes); rows 38–39 the 2 not provable and, in `:33`'s
statement, `:32` (one of the 29 shape-G lines); row 40 the other 28 shape-G lines; rows 41–42 the 3 non-paths: 75 for the C# copy. Less
the 3 C#-only lines and plus row 43: 73 for the Node guard (§ 11 O4).

Beside the sites, in files on § 0.1: `Gate/Assessment/FindingAssessorTests.cs:44` (K2, the ninth; no guard reads it);
`Infrastructure/HarvestGatesTests.cs`, `SolutionSignalsTests.cs` (the new overloads with `_repo.ScratchRoot`); the three `src/` files (K8);
the summaries of the files K6, K7, K9 change. Every existing test keeps its name and its assertions, except K4's literals (their meaning kept).

## 5. The scenarios — RED, GREEN, RED again

"RED again" is the defect put back once after GREEN, by hand, and quoted on the PR (the stories' § 7). Every child process a scenario starts
gets an owned `TMPDIR` (S4's O10: a SIGKILLed .NET child leaves `clr-debug-pipe-*` FIFOs in its temp root; and a regression then writes
inside a folder the test owns, never in `/tmp`).

| # | Test (file) | GREEN asserts | RED (first) / RED again |
|---|---|---|---|
| (1) E6-k | `A_class_that_made_temp_paths_leaves_its_temp_root_empty` (`TempLeftoverChildRunTests`, K11), one row per class | the child exits 0; its owned `TMPDIR` holds no entry | RED first, on today's code: `FilesystemEngineTests` 23 entries (`bench-fs-engine*` and `outside.txt`), `QlnEngineTests` 14, `DailyRunFileSinkTests` 5, `TracePortTests` 3, `FindingAssessorTests` 2 `bench-assess-*` (§ 11 O8, O9: these counts are observations, the test asserts zero). RED again: put `TracePortTests.cs:166`'s `CreateTempSubdirectory` back → that row red naming the entry |
| (2) D6 | `An_owner_form_model_id_whose_folder_is_missing_is_refused_at_the_first_write` and `The_version_answers_without_a_home` (`FakeCliHomeTests`) | the fake launched (`ProcessRunner`, argv `-p --model <Owned.NameFor("fakecli")>`, `TMPDIR` an owned folder) exits 3, its stderr names the home; neither `<TMPDIR>/<id>` nor `<TMPDIR>/bench-fake-cli` exists; `--version` with the same id exits 0 | RED first: today's fake exits 0 and makes `<TMPDIR>/bench-fake-cli/<id>/events`. RED again: drop the refusal in `Events.Record` → red |
| (2b) D6 | `A_probe_subject_lives_in_its_owned_folder` (`FakeCliHomeTests`) | `new FakeCli()`: `Owned.FullName().IsMatch(leaf)`, `<Home>.owner` exists, `Model == leaf`; one launch with `--model fake.Model` logs its call in `fake.Home/events`; after `Dispose` folder and claim are gone | RED first: today's driver's `Home` is `<temp>/bench-fake-cli/fake-<guid>` → not owned → red. RED again: put `:15` back → red |
| (3) D6 | `A_review_through_the_driver_writes_its_data_inside_the_drivers_root` and `The_fake_coai_with_no_data_folder_refuses_where_it_first_needs_one` (`FakeCoaiDataFolderTests`; Docker-free, the shape of [P] O-g) | (a) `new FakeCoai()`, `OpenAsync(extra: { TMPDIR = <an owned folder> })`, one `review_plan` answered; `<Root>/data/sessions` exists; the extra folder holds nothing. (b) `extra: { COAI_DATA_DIR = "" , TMPDIR = <owned> }`: `review_plan` fails, the stderr file holds `COAI_DATA_DIR is unset`; `--version` (`ProductPinReader`) and `--probe-api` still answer; nothing under the extra `TMPDIR` | RED first: today's driver sets no `COAI_DATA_DIR`, so (a)'s data lands in `<extra TMPDIR>/fake-coai-data` (red: `<Root>/data` missing) and (b)'s review answers. RED again: put the fallback back → (b) red |
| (4) D4 | `The_default_scratch_root_is_the_products_own` and `The_old_signature_still_makes_its_worktree_under_the_default_root` and `A_gate_worktree_lives_inside_the_tests_owned_folder` (`ScratchRootSeamTests`) | (a) `ScratchWorktree.DefaultRoot == Path.Combine(TempDir.Root, "bench-scratch")`. (b) a CHILD of this executable (`SuiteChild`, `TMPDIR` = an owned folder, a gated child method as S4's K7 shape) calls the 4-parameter `AddAsync` over a `DatedGitRepo`, reports the path, removes the worktree; the parent asserts the path is `<child TMPDIR>/bench-scratch/<32 hex>` and that nothing is left under it. (c) `HarvestGates.RunAsync(…, _repo.ScratchRoot)` with a build command that prints its top-level folder and fails (`git rev-parse --show-toplevel HEAD~999`, **[R]**: the coder confirms the detail carries the path; if git does not print first, any failing command that prints its working directory, never a shell string): `RedDetail` names a path under `_repo.ScratchRoot`; afterwards `git worktree list` holds only the repository | RED first: before the overloads, (c) calls the old signature → the path is under `<temp>/bench-scratch` → red. RED again: the new overload ignores its root → (c) red |
| (5) rule §4 | `A_killed_DatedGitRepo_user_is_collected_by_the_next_sweep` and its child (`DatedGitRepoKillTests`) | S4's K7 shape: the child (`TMPDIR` = the parent's owned `childTemp`, `TESTSTODELETE_RUN` removed) makes a `DatedGitRepo`, `InitAsync`, adds a worktree under `ScratchRoot` (`ScratchWorktree.AddAsync(…, ScratchRoot)`), reports the owned folder, waits; the parent SIGKILLs it; `LeftoversSweep.Run(new SweepOptions(childTemp, now, TimeSpan.Zero, Liveness.ProbeProcess, Mine: p => (p == folder \|\| p == folder + ".owner") && Scope.IsMine(p)))`: `Removed` is `[folder]`, folder and claim gone (S4's deviation (a): a claim is listed only when it is an orphan's). Reuses `ModelArms.TempFolderTests.WaitForChildFolderAsync` (`internal static`, `TempFolderTests.cs:179`; reuse-first, the file is not edited); skipped on Windows | RED first: on today's `DatedGitRepo` the child's folder is `<childTemp>/bench-dated-git/<id>`, unclaimed: no owned folder to report → red. RED again: `Root` back under `GetTempPath()` → red |
| (6) E6-b, E11-5 | the guards over the head, by hand (§ 6) | per class, per guard, as § 6; § 3a's marker valid | RED again: remove the § 3a marker → one `raw-temp` at that line in each guard; put `"/tmp/coai-answers-1/…"` back at `:132` → one C# `raw-temp`; the interpolated `nameof` at `:19` → one C# `owner-blind` (O7); the `.ndjson`'s raw `worktree add` → one Node `raw-worktree` |
| (7) | the launchers and the whole suite | every class of § 11 O10's list green with the new drivers; the whole suite's FAILED list equal to the base's (here: the same 572 Docker-bound tests, O8; on CI: none); a whole-suite run under an owned `TMPDIR` leaves no entry of S5's families there (§ 11 O8's list: `bench-*`, `outside.txt`, `fake-coai-data`), the `clr-debug-pipe-*` FIFOs of other stories' kill tests excepted and counted (§ 12 N4) | — |

## 6. The guards in S5 — both guards, a count per class, at the base `e967df53` (E11-5; the parent's item 8.1)

| Guard | Base, all | Base, S5's area (`tests/` outside `ModelArms/`, `Leftovers/`), per class | Head, all | Head, S5's area |
|---|---|---|---|---|
| Node (`--profile node --token bench --identity "the measuring harness"`) | **160** (O2: 2 `research/`, 85 `spikes/`, 73 `tests/`) | **73**: 72 `raw-temp` (70 in `Bench.Tests`, 2 in the fakes), 1 `raw-worktree` (the `.ndjson` line) | **87** | 0 in every class (§ 3a's marker clears its line) |
| Node, default profile | **3** | 0 | **3** | 0 |
| copied C# (`--explicit only --filter-class "*LeftoversRepositoryGuardTests"`, information; S7 turns it on) | **91** over 1328 files (O3: 2 `research/` `raw-worktree`; 14 `spikes/`: 10 `raw-temp`, 3 `legacy-name`, 1 `owner-blind`; 75 here; 0 under `ModelArms/` and `Leftovers/`) | **75**: 74 `raw-temp` (72 in `Bench.Tests`, the two `"/tmp"` literals among them; 2 in the fakes), 1 `owner-blind` (`ProbeContractsGuardTests.cs:19`) | **16** | 0 in every class |

No finding added anywhere else; the new test files add none (their shapes are O6's planted lines). A count that differs is read finding by
finding before anything else. Watch for, in new lines: the prefix word or a piece of it (five characters or more, `testSt`, `ToDelete`) in
ANY literal, an interpolated string included (O7); `"/tmp"` in a literal; `Owned.TempRoot` outside `Leftovers/` (`raw-temp`, O6 C6; use
`TempDir.Root`); `Owned.Prefix` outside `Leftovers/` (`owner-blind`); a marker two lines above its line. **C8's S5 half** (stories `:166`,
E11-11) is observed here (§ 11 O6): a `worktree add` argv in a `tests/` `.cs` is one `raw-worktree` in each guard; the same argv in
`src/Bench.Infrastructure/Git/ScratchWorktree.cs` is read by the C# copy as `Source` and gives nothing, and the Node guard does not scan it.

## 7. Commands (from the worktree root; WSL, no `.exe`, no Docker; one build at a time)

```bash
nice -n 19 dotnet build dew_flow_benchmark.slnx -c Release -m:4 -p:AppHostRuntimeIdentifier=linux-x64 --disable-build-servers
nice -n 19 dotnet format dew_flow_benchmark.slnx --verify-no-changes --no-restore
git diff --check
T=./tests/Bench.Tests/bin/Release/net10.0/Bench.Tests
nice -n 19 $T --filter-class "*TempLeftoverChildRunTests"        # (1); the Vetting row skips here (no daemon)
nice -n 19 $T --filter-class "*FakeCliHomeTests"
nice -n 19 $T --filter-class "*FakeCoaiDataFolderTests"
nice -n 19 $T --filter-class "*ScratchRootSeamTests"
nice -n 19 $T --filter-class "*DatedGitRepoKillTests"
for c in ProcessRunnerStderrSinkTests CliSubjectRuntimeTests CliProbeRunnerTests FakeCliByCallTests FileGateLockTests ScoutLaunchProcessTests \
         ScoutPinReaderTests McpStdioClientTests ProductPinReaderTests CoaiApiProbeRunnerTests HarvestGatesTests SolutionSignalsTests \
         ClaudeSubagentsTests ScoutReadBackTests ScoutModelFallbackTests GateReplyParserTests ProbeContractsGuardTests; do
  nice -n 19 $T --no-ansi --filter-class "*.$c" | grep -E '^\s+(total|failed):'; done
nice -n 19 $T --filter-namespace "Bench.Tests.ModelArms"          # the drivers' other users: green (O10)
nice -n 19 $T --filter-class "*TheAdaptedCopyTests"                # untouched copy: still green
nice -n 19 $T --explicit only --filter-class "*LeftoversRepositoryGuardTests"   # information: 16 expected, none in S5's area
P=/home/jinx/.cache/e6s5-whole-$(head -c4 /dev/urandom | od -An -tx1 | tr -d ' \n'); mkdir "$P"   # new, empty, outside /tmp; removed by exact path after
env -u MODEL_ARMS_DRVFS_SCRATCH -u BENCH_PILOT_OUT -u TESTSTODELETE_RUN TMPDIR="$P" nice -n 19 $T --no-ansi > whole.txt   # FAILED list vs O8's
ls -A "$P" | grep -v '^clr-debug-pipe-' ; ls -A "$P" | grep -c '^clr-debug-pipe-'
node .agents/conventions/tools/leftovers-check.mjs --repo . --profile node --token bench --identity "the measuring harness" | head -1   # 87
node .agents/conventions/tools/leftovers-check.mjs --repo . --token bench --identity "the measuring harness" | head -1                  # 3
node .agents/conventions/tools/plan-lifecycle.mjs
```


## 8. The commit series

**One PR, one story.** Each commit builds and its tests pass.

1. **The fakes (K6, K7):** `FakeCliHomeTests` (2), (2b) and `FakeCoaiDataFolderTests` (3) with their RED observed by hand on today's fakes and
   drivers (quoted), then `tests/FakeCli/Program.cs`, `Probes/Driver/FakeCli.cs`, `Scouts/ScoutLaunchProcessTests.cs:129`,
   `tests/FakeCoai/Program.cs`, `Gate/Driver/FakeCoai.cs` with § 3a's marker; every launcher of § 7's loop and the ModelArms namespace re-run;
   each RED again quoted.
2. **The seams (K8, K9):** `ScratchRootSeamTests` (4) RED first, then the three `src/` files and `DatedGitRepo`; `HarvestGatesTests` and
   `SolutionSignalsTests` on `_repo.ScratchRoot`; `DatedGitRepoKillTests` (5).
3. **Row E6-k (K2, K11):** `TempLeftoverChildRunTests` (1) RED first (quoted, with the counts), then the eight sites and
   `FindingAssessorTests.cs:44`; RED again.
4. **The private temp classes (K10, § 4 rows 1–2, 4–16, 20, 22–23, 25–31, 34).**
5. **Shape G and the rest (K3, K4, K5):** § 4 rows 38–43; § 6's counts on the head in both guards; (6)'s RED again.
6. **The docs commit:** K12 (`research/module_tests.md`); § 3a's list copied into the stories' § 11; the stories' progress line; this
   brief's progress line.

**Size.** About 60 changed files, most of them a few lines each; five new test files of about 450 lines in all. One Sonnet coder.

## 9. Done when, and what the PR body quotes (the stories' S5 *Done when* `:656-662`, E11-5)

- Both guards against the base `e967df53`, in S5's area: the Node guard 73 → 0 and the C# guard 75 → 0, every class, except § 3a's ONE
  listed exemption; the totals 160 → 87 (Node), 3 → 3 (default profile), 91 → 16 (C#); no finding added elsewhere (§ 6).
- (1)–(5): GREEN, each RED first and RED again quoted; (6)'s RED again from each guard.
- The product's own worktree names unchanged: (4) (a) and (b) green.
- Every launcher of § 7's loop and the ModelArms namespace green with the new drivers; the whole suite's FAILED list equal to the base's
  (locally the 572 Docker-bound tests of O8; on CI `build-test` green, so the Docker-bound classes of § 2 point 5 pass there).
- A whole-suite run under an owned `TMPDIR` leaves no `bench-fake-cli`, `bench-gate-fake`, `bench-dated-git`, `bench-gate-tests`,
  `bench-fs-engine*`, `bench-qln*`, `bench-daily-log*`, `bench-fixture*`, `bench-assess-*`, `fake-coai-data` or `outside.txt` there
  (today: O8's list), listed before and after on the PR, with the count of other stories' `clr-debug-pipe-*` FIFOs beside it (§ 12 N4).
- The PR body lists the six doc lines § 13 reads as history (`research/PLAN_question_consultant_probes.md:317`, `research/PLAN_model_arms_E1.md:517`, `:649`, `research/PLAN_model_arms.md:344`, `research/PLAN_model_arms_E5.md:83`, `research/module_model_arms.md:922-923`) and names follow-up F45, which updates them; none is touched by S5.
- 0 warnings; `dotnet format` clean; `TheAdaptedCopyTests` green (no manifest target touched); `plan-lifecycle.mjs` clean;
  `git diff --name-only <base>..HEAD` within § 0.1; § 3a copied into the stories' § 11.

## 10. What this story does NOT prove

Windows (no leg; the kill tests skip there). That an owned folder's contents are what a site's comment says (a guard reads the spelling, never
the data flow: shape G is a blind spot both guards share, consult `1f60c647`; O-c's reading of the 29 callees is the evidence). That the
product's own temp sites are removed on every path (`FileSystemGateAssessmentFiles`, `SolutionSignals.cs:179`, `ScoutTrees.cs:178`,
`GateToolsCommand.cs:136`: the product's, § 3 of the stories; § 12 N3). That a killed fake CLI or fake coai leaves nothing (their
`clr-debug-pipe-*` FIFOs land in their launcher's temp root; § 12 N4). That `ScoutsOutOfProcessTests` and the gate driver's launches work
with the fake coai's refusal: they run only on CI (§ 2 point 5).

## 11. Observations — made before this brief's plan gate (the parent's item 8.1)

**Common to O1–O11:** consumer **benchmark `e967df53`** (`main`: #221, #220, #219), cloned (`git clone --no-hardlinks` of
`/home/jinx/git/dew_flow_benchmark`, checked out at `e967df53`) into
`/tmp/claude-1000/-home-jinx-git-dew-flow-conventions/89003c31-0493-4c3d-b402-69ecdb3a49e8/scratchpad/bench-s5obs`, outside every worktree,
never committed and never pushed; the mount **`b1746499`** copied in as files with its `node_modules`; **upstream pin mcp `53f65130`** (the
copy's manifest commit). ONE scratch test file was added and never committed: `tests/Bench.Tests/Infrastructure/S5ScratchObservations.cs`
(`Obs_Repository`: `LeftoversGuard.Scan(root, root)` over the clone, every finding written; `Obs_Plants`: `LeftoversGuard.Scan(tree, tree)`
over one private tree per plant, the plants read from a JSON file outside the clone, so the scratch file itself spells nothing). Build:
`nice -n 19 dotnet build tests/Bench.Tests/Bench.Tests.csproj -c Release -m:4 -p:AppHostRuntimeIdentifier=linux-x64 --disable-build-servers`
→ 0 warnings, 0 errors, 75.8 s. Node v26.12.1, .NET SDK 10.0.114, WSL Ubuntu, uid 1000, no Docker. The Node half ran in this worktree with
the mounted guard's `scanText(<fixture path>, <text>)` and `isScanned(<fixture path>, 'node')`, one call per plant, the same plants and the
same wrapping (the line inside a method body). For O10 the clone got the four scratch edits it names and was built a second time (0 warnings,
30.9 s). The scratch is not kept (a reboot clears it); the results are here.

| # | Claim (where) | Invocation | Fixture path, plant | Expected | Observed |
|---|---|---|---|---|---|
| O1 | the parent's line numbers and O-c's reading hold at the new base | `git diff --stat b348bb33 e967df53 -- tests src ':!tests/Bench.Tests/ModelArms'`; the C# finding list of S5's area at `e967df53` against the parent's list at `b348bb33` | — | nothing changed | **nothing** (empty diff); the two lists of 75 lines are IDENTICAL (`diff` empty) **[M]**. So O-c's per-line classes stand without a re-read, and every `file:line` of the parent and the stories is today's |
| O2 | the Node guard today (E11-5) | `node .agents/conventions/tools/leftovers-check.mjs --repo . --profile node --token bench --identity "the measuring harness"` | this worktree at `e967df53` | 73 in S5's area | **160** (2 `research/`, 85 `spikes/`, 73 `tests/`); S5's area **73**: 72 `raw-temp` (70 `Bench.Tests`, 2 the fakes `FakeCli/Program.cs:664`, `FakeCoai/Program.cs:417`), 1 `raw-worktree` (`Fixtures/scouts/claude-2.1.289-two-children-models.ndjson:2`); 0 under `ModelArms/` and `Leftovers/`; default profile **3** (the two `t-lib.sh` lines, `prove.sh:7`) |
| O3 | the C# copy today (E11-5) | `Obs_Repository` | the clone | 75 in S5's area | scanned **1328** files, **91** findings: 2 `research/` (`raw-worktree`), 14 `spikes/` (10 `raw-temp`, 3 `legacy-name`, 1 `owner-blind`), **75** in S5's area (74 `raw-temp`, 1 `owner-blind`), **0** under `ModelArms/` and `Leftovers/` (S4 brought 82 there to 0) |
| O4 | which lines only one guard reads | `comm` of the two lists of S5's area | — | 3 C#-only, 1 Node-only | C#-only: `GateReplyParserTests.cs:125`, `:132` (`"/tmp"`), `ProbeContractsGuardTests.cs:19` (`owner-blind`); Node-only: the `.ndjson` line (the copy does not discover `.ndjson`, O6 P20: scanned 0); the other **72** in both |
| O5 | every site's lifetime class (item 8.1) | O-c's classes ([P] `:3778`) at the unchanged lines (O1), checked against the run-time evidence of O8/O9 | — | 41 create, 29 none, 2 not provable, 3 not paths | as O-c, and the run confirms the "create" half where it ran: every family O8 found left over is a "create" site of § 4 (rows 3, 17–19, 21–22, 24, 26, 32–33, 35) or a sibling of one (row 26's `-mirrors`, `-work`; row 18's `outside.txt`); no shape-G line made anything O8 could see. Two sites no guard reads, found by the run: `FilesystemEngineTests.cs:63` (`outside.txt` in the temp root) and `FindingAssessorTests.cs:44` (the product's `bench-assess-*`), both "run" lifetime (K2) |
| O6 | the exact lines S5 writes, through both guards; claim C8's S5 half | `Obs_Plants` and `scanText`/`isScanned` | one private tree per plant, the file at the path named: P1 `GateReplyParserTests.cs` the `:125` sample with `/srv/`; P2 `:132` with `/srv/`; P4 `CliContractTests.cs` `_dir = TempDir.For("suite"); Path = _dir.At("suite.json");`; P5 `ArchitectureTests.cs` `using var work = TempDir.For("arch-planted"); var root = work.At("root");`; P6/P7 `Gate/Driver/FakeCoai.cs` `_owned = OwnedDir.Create("gate-fake"); Root = _owned.Path;` and `env["COAI_DATA_DIR"] = Path.Combine(Root, "data");`; P8 `ProcessRunnerTests.cs` a run with `TempDir.Root` as its working directory; P9 `TracePortTests.cs` `Path.Combine(TempDir.Root, "absent-" + …)`; P10 `GateAaCommandTests.cs` `Path.Combine(TempDir.Root, Owned.NameFor("aa-missing"))`; P11 `DatedGitRepo.cs` K9's three members; P12 `Probes/Driver/FakeCli.cs` K6's three lines; P13–P15 `tests/FakeCli/Program.cs` the three-way home WITH the § 3a marker above `var temp = Path.GetTempPath();`, the `OwnerForm` regex, the refusal; P16 `tests/FakeCoai/Program.cs` the refusal; P17 `src/…/ScratchWorktree.cs` `DefaultRoot`; P18 `src/…/HarvestGates.cs` the overload's `AddAsync(…, scratchRoot)`; P19 `HarvestGatesTests.cs` the call with `_repo.ScratchRoot`; P20 the `.ndjson` line with `worktree add`; P21 `GitCheckoutTests.cs` `TempGitRepo`'s `TempDir.For("git-src")`; P22 `ScoutLaunchProcessTests.cs` `TempDir.For("scout-fake")`. Controls: C1/C2 today's `:125`/`:132`; C3 today's `:19`; C4 the fake's `var temp = Path.GetTempPath();` WITHOUT the marker; C5 today's fake-coai `:417`; C6 `Owned.TempRoot()` in a test; C7 `Path.GetTempPath()` in a test; C8b `"worktree", "add"` in `HarvestGatesTests.cs`; C8c the same argv in `src/…/ScratchWorktree.cs`; C9 today's `.ndjson` line | every planned line clean in both; each control red in the guard that reads it | every planned line **`[]` in both guards** (C#: scanned 1 each, P20 scanned 0; Node: `isScanned` true for every `tests/` path and false for `src/`, `[]` each). Controls: C1, C2 → C# `raw-temp` (`"/tmp"`), Node `[]`; C3 → C# `owner-blind` (`testSt`), Node `[]`; C4 → `raw-temp` (`GetTempPath`) in both (the marker is what clears P13); C5 → `raw-temp` in both; C6 → C# `raw-temp` ("`Owned.TempRoot` outside the helper's folder"), Node `[]`; C7 → `raw-temp` in both; **C8b → `raw-worktree` in both; C8c → C# scanned 1 file, `[]` (the `Source` kind), Node `isScanned` false (its `scanText` alone reports `raw-worktree`, so the zero is the walk's): C8's S5 half holds as written**; C9 → Node `raw-worktree`, C# scanned 0. P17 in `src/`: C# `[]`, Node not scanned (its `scanText` alone would say `raw-temp`: claim C4's limit) |
| O7 | E11-5's example for `:19` ("for example through `nameof`") | `Obs_Plants` and `scanText` | `tests/Bench.Tests/Probes/ProbeContractsGuardTests.cs`: P3 the entry as `$"{nameof(ProbeCellReportDto)}.{nameof(ProbeCellReportDto.LatestState)}"` on the set's line; P3e the same on a line of its own; P3b `nameof(ProbeCellReportDto) + "." + nameof(ProbeCellReportDto.LatestState)` on the set's line; P3c the same on its own line; P3d a `Key<ProbeCellReportDto>(nameof(…LatestState))` helper; P3f `nameof(ProbeCellReportDto.LatestState)` alone; C3b the identifier `dto.LatestState` outside any literal | — (open-ended) | **the interpolated form is RED in the C# copy**: P3 and P3e → `owner-blind` (`testSt (a piece of the prefix word)`): the copy reads an interpolated string's holes as literal text. P3b, P3c, P3d, P3f, C3b → `[]`. The Node guard: `[]` for all. So K4 takes the CONCATENATION (P3b) |
| O8 | the whole suite today, and what it leaves (E6-k's RED; the base FAILED list; the families S5 retires) | `env -u MODEL_ARMS_DRVFS_SCRATCH -u BENCH_PILOT_OUT -u TESTSTODELETE_RUN TMPDIR=<P> nice -n 19 <T> --no-ansi --filter-not-class "*S5ScratchObservations"`, then `ls -A <P>` | `<P>` = a new empty folder in the scratchpad (outside `/tmp`'s shared root and every checkout) | — | 5878 tests: **572 failed**, 5257 passed, 49 skipped, 2 m 45.7 s (wall 166.6 s). The 572 are exactly the tests of **70 classes, each Postgres-bound** (`[Collection("postgres")]` or a `PostgresFixture` parameter, checked class by class): no Docker here, so that is the base FAILED list. Left in `<P>`: **189** entries — **134 `clr-debug-pipe-*`** FIFOs (67 killed .NET children; § 12 N4), 23 `bench-fs-engine*`, 14 `bench-qln*`, 5 `bench-daily-log*`, 3 `bench-fixture*` (row E6-k's four Docker-free classes), 2 `bench-assess-*` (O9), `outside.txt` (`FilesystemEngineTests.cs:63`), and the empty parents `bench-dated-git`, `bench-fake-cli`, `bench-gate-fake`, `bench-gate-settings`, `bench-scout-fake`, `bench-scratch` (each class removes its child, never the parent), and `bench-gate-tests` holding `<guid>-mirrors`, `<guid>-work` siblings `TempRoot.Dispose` never removes. No `dotnet-diagnostic-*` socket was left in `<P>`: a normal exit leaves none (K11's premise). Not ours: the shared `/tmp` gained 7 claimed `testsToDelete-u1000-bench-tree-*` folders during the run, whose records name a live `spikes/lang-explore/explore.mjs` process of another worktree, and `MSBuildTemp*` folders of other builds |
| O9 | where the two `bench-assess-*` come from | the same command per class, each with its own new `<P>`: `GateImportCommandTests`, `FileSystemGateAssessmentFilesTests`, `FindingAssessorTests`, `CalibImportTests`, `GateAssessCommandTests` | — | — | `FindingAssessorTests` (2 of 2 passed) leaves **2 `bench-assess-<guid>`** (each holding `batches`) and an empty `bench-gate-tests`; `FileSystemGateAssessmentFilesTests` leaves only the empty `bench-gate-tests`; the other three are Postgres-bound (all failed, nothing left). The reading: `FindingAssessorTests.cs:43-44` makes `new FileSystemGateAssessmentFiles(root.Path)` without `using`; the product's workspace (`FileSystemGateAssessmentFiles.cs:33`) is removed only by its `Dispose`. **[M]** on this machine's shared `/tmp`: 8 older `bench-assess-*` (2026-11-16 22:42 to 2026-11-17 00:06), 92 `bench-fs-engine*`, 56 `bench-qln*`, 20 `bench-daily-log*`, 12 `bench-fixture*` — none removed (D11) |
| O10 | D6 as built: the probe driver's owner-form id and the fakes' refusals, with EVERY user of the two drivers (the parent's O-h ran the fakes' edits with the OLD driver; no observation had the driver mint the owner-form id for all its users) | the clone with four scratch edits — `tests/FakeCli/Program.cs` and `tests/FakeCoai/Program.cs` as the parent's O-h and O-g made them (their diffs applied from that scratch clone), `Probes/Driver/FakeCli.cs` and `Gate/Driver/FakeCoai.cs` as K6 and K7 write them — rebuilt; then the O8 command with a new `<P>` | — | the same 572 failed and nothing else; no `bench-fake-cli`, `bench-gate-fake` or `fake-coai-data` in `<P>` | 5878 tests, **572 failed — the IDENTICAL list** (`diff` of the two sorted FAILED lists empty), 5257 passed, 49 skipped, 2 m 52.2 s. So every Docker-free user of both drivers is green with the owner-form id and the refusals: the probes (`CliProbeRunnerTests`, `ProbeLiveTranscriptTests`), `CliSubjectRuntimeTests`, `ProcessRunnerStderrSinkTests`, `ScoutLaunchProcessTests`, the scouts' Docker-free classes, `McpStdioClientTests`, `ProductPinReaderTests`, `CoaiApiProbeRunnerTests`, and the ModelArms namespace (`FakeCliByCallTests`, `RunArmSchemeTests`, `RunnerIdentityTests`, `PosixFolderIsolationTests`' Docker-free facts, …). In `<P>`: **no `bench-fake-cli`, no `bench-gate-fake`, no `fake-coai-data`**; the rest as O8 (the E6-k families, `bench-assess-*` ×2, `outside.txt`, the empty parents `bench-dated-git`, `bench-gate-settings`, `bench-scout-fake`, `bench-scratch`, `bench-gate-tests`) and 130 `clr-debug-pipe-*`. The Postgres-bound users of the drivers (`ProbesCliSetup`, `ScoutsScenarioTests`, `ScoutsAnalysisCommandTests`, `ScoutsPrivateTaskTests`, `GateProtocolTests`, `GateCampaignTests`) are among the 572 and stay **blocked** here (§ 2 point 5) |
| O11 | where O8's `clr-debug-pipe-*` FIFOs come from (N4) | the O10 build, the O8 command with `--filter-namespace "Bench.Tests.<ns>*"` per namespace, each with its own new `<P>` | — | — | FIFOs left: `Leftovers` 0 (671 tests), `ModelArms` **110** (2132), `Infrastructure` **12** (425), `Scouts` **4** (388), `Probes` **2** (217), `Gate` **2** (539), `Cli` 0 (294, Postgres-bound mostly failed): 130 in all, O10's count. Nothing else of S5's families in any of them but O10's list |

**Observed by the parent, relied on here [P]** (§ *Epoch 11* item 3.3, consumer `b348bb33`, upstream mcp `53f65130`; O1 shows the S5 area
unchanged since): **O-a** (`:3776`) an owner-form model id names an owned folder and passes `ModelRef.Parse` and `ProbeSubject.Parse`; the
probe attempt answers, its call log inside the folder; the probe child's allow list passes `TMPDIR`, `TEMP`, `TMP`. **O-c** (`:3778`) the
callees of the 75 lines: 41 create, 29 create nothing, 2 not provable, 3 not paths. **O-g** (`:3782`) the fake coai's refusal at the point
of need and `FakeCoai.Environment`'s variable: RED then GREEN; `McpStdioClientTests` 5/5, `ProductPinReaderTests` 4/4,
`CoaiApiProbeRunnerTests` 7/7. **O-h** (`:3783`) the fake CLI's lines clean in both guards; the refusal at the first write keeps `--version`;
every Docker-free launcher and ModelArms green.

**Written open-ended, not observed here (red or not, the PR's run says which):** the RED of (2)–(5) on today's code and their GREEN on the
head; (4) (c)'s build command printing its top level; the Docker-bound classes (§ 2 point 5), `VettingPassTests`' two E6-k sites among
them; the namespace and whole-suite times on the head.

## 12. Notes for the parent and the stories (findings, not changes)

- **N1 — `tests/Bench.Tests/ModelArms/StepBench.cs:64` is outside S5's *Files*.** Its doc comment still says "a scratch root named
  `testsToDelete-modelarms-…`", the form S4 retired. `ModelArms/**` is S4's *Files* (merged as #221) and S5's line excludes it; S5 does not
  touch it. A one-line fix forward is S4's plan as written (`ModelArms/**`, a comment that only satisfies the plan): Q4, answered: the
  coordinator handles it at S4's promotion or as a follow-up.
- **N2 — E11-5's example spelling is red in the C# copy** (O7): `$"{nameof(…)}.{nameof(…)}"` is `owner-blind`; the concatenation is clean.
  The stories say "for example", so K4's choice is brief-level; for the follow-ups plan's next record, beside the parent's *Found on the way*
  (`PrefixPieces` reads an interpolated string's holes as literal text).
- **N3 — The product's own temp workspace leaks through a test** (O9): `FindingAssessorTests.cs:44` never disposes
  `FileSystemGateAssessmentFiles`, whose workspace is `<temp>/bench-assess-<guid>`. S5 adds the `using` (K2, Q2). The product itself
  (`src/…/FileSystemGateAssessmentFiles.cs:33`) stays the product's: a killed run still leaves its workspace unclaimed; no guard reads
  `src/` raw temp (claim C4).
- **N4 — Killed .NET children leave `clr-debug-pipe-*` FIFOs in their temp root** (S4's N5, measured again: 134 per whole run, O8). They come
  from children killed by tests whose temp root is the run's; S4's kill tests already give theirs an owned `TMPDIR`. By namespace (§ 11 O11):
  `ModelArms` 110 (S4's area), `Infrastructure` 12, `Scouts` 4, `Probes` 2, `Gate` 2 (20 in S5's area: killed fakes, most likely the hung
  fake CLI of `ProcessRunnerStderrSinkTests` and the scouts' launch tests, not traced by class), `Leftovers` 0, `Cli` 0 (its Postgres-bound
  classes did not run); not S5's stories text and read by no guard; for the parent's *Found on the way*, beside S4's N5. S5's own kill tests (5) and child runs (1), (4) (b) give their children an
  owned `TMPDIR`.
- **N5 — `FilesystemEngineTests.cs:63` wrote into the temp root itself** (O8: `outside.txt`), a site no guard reads (`Directory.GetParent`
  of a temp folder). K2 fixes it by making the engine's root a child of the owned folder.

## 13. STOP finding — doc lines that describe what S5 changes, outside S5's *Files* — CLOSED: history, (b); follow-up F45

**Closed 2026-11-17 by the owner, in the coordinator's session: answer (b).** The six lines are history: `research/PLAN_question_consultant_probes.md:317`, `research/PLAN_model_arms_E1.md:517`, `:649`, `research/PLAN_model_arms.md:344`, `research/PLAN_model_arms_E5.md:83`, `research/module_model_arms.md:922-923`. None goes on S5's *Files* line; that is not a change of the parent and not entry 11.2 (epoch 11 stays at ONE change, 11.1). Their update is filed by the coordinator as follow-up **F45** in the conventions follow-ups plan, outside the rollout plan. S5 does not touch them; **S5's PR body lists all six lines and names F45** (§ 9). The finding as written:

The parent's item 8.1 (`:4098-4100`) asks every *Files* line to name "the docs that describe what it changes, found by a `git grep` of the
old spellings over `*.md` at the brief's base, each doc line with what it says now; an IMPLEMENTED plan gets a dated note beside the old
text". S5's *Files* (`:653-655`) names `research/module_tests.md` only. Read at `e967df53` with the old spellings of S5's sites
(`git grep -n -E 'bench-fake-cli|fake-coai-data|bench-gate-fake|bench-dated-git|bench-gate-tests|…|scout-(links|key|hygiene)-|FakeHome' --
'*.md'`, the whole pattern in § 11's style, plus `DatedGitRepo|TempGitRepo|FakeCoai.Environment|COAI_DATA_DIR|ScratchWorktree|bench-scratch|FAKE_CLI_HOME`)
**[M]**, these lines describe what S5 changes and are neither on the *Files* line nor read by any list as history:

| Line | What it says now | After S5 |
|---|---|---|
| `research/PLAN_question_consultant_probes.md:317` (IMPLEMENTED 2026-10-01; a design note of its S2) | "*The fake CLI finds its script by the MODEL ID the argv pins* (`%TEMP%/bench-fake-cli/<model>/`), because the probe runner launches with the harness's own environment" | still found by the model id; but the probe driver's ids are then the owner form, whose home is `<TEMP>/<id>` (K6). The folder named is no longer where a probe test's script lives |
| `research/PLAN_model_arms_E1.md:517`, `:649` (IMPLEMENTED) | "the fake finds that home from `--model` (`Program.cs:13-31`, `FakeHome.For` `:554`)"; "Every launch goes to `tests/FakeCli` through the model-keyed handle (`FakeCli`, no environment needed: the fake finds its script under `%TEMP%/bench-fake-cli/<model>/` from `--model`)" | the handle's home becomes `<TEMP>/<id>` (its owned folder) |
| `research/PLAN_model_arms.md:344`, `research/PLAN_model_arms_E5.md:83` (IMPLEMENTED) | "scripted per model id (`FAKE_CLI_SCRIPT` or `%TEMP%/bench-fake-cli/<model>/script.json`)"; "script from `FAKE_CLI_SCRIPT` (else `<temp>/bench-fake-cli/<model>/`)" | true for a model id NOT in the owner form (K6 keeps that branch); incomplete for the handle's ids |
| `research/module_model_arms.md:922-923` (the living module document, a numbered deviation of an E1 story) | "`InitVerbTests…` makes its checkout in a `TempFolder` … and not with `TempGitRepo`, whose `bench-src-<guid>` folder carries no leftover prefix." | false in the present tense: `TempGitRepo`'s folder becomes an owned folder (K10) |

Read and NOT on the list: `research/module_probes.md:318-319` and `research/module_tests.md:19` say only "by the model id" (still true;
`module_tests.md` is on S5's line anyway); `research/PLAN_model_arms_E2.md:137`, `:139`, `:218`, `:1526`, `:1551`, `:1849` and
`research/PLAN_model_arms_E1.md:55` name `TempGitRepo`, `FAKE_CLI_HOME` or the fake's code by file and line only (unchanged by S5);
`research/PLAN_coai_gate_model_benchmark.md` describes the product's `COAI_DATA_DIR` (not the fake's); `research/PLAN_test_leftovers_E6_S2_brief.md:316`
lists legacy names for D11 (history); `http/README.md:32` is S7's.

*(Was, before the owner's answer:)* **What this brief does:** NOT add them (the instruction: a file S5 needs outside its *Files* is the parent's next change, which after entry
11.1 is the eleventh full stop). S5 does not go to its plan gate until the coordinator decides, per line: (a) on the *Files* line (a counted
change, 11.2, the eleventh full stop), or (b) read as history and left as they are, recorded in this brief's "read and NOT on the line"
list. [Recommend **(b)** for all of them, with this reasoning for the coordinator to check: every line is in an IMPLEMENTED plan or in a
numbered deviation of a story that shipped (the class S4's item 4.2 itself read as history: `module_model_arms.md:1308`, the E1/E3/E4 lines,
"record what was built then, and stay"); the fake's lookup by model id, which they all describe, is unchanged, and its folder for a non-owner
id is the same; none is a procedure an operator follows (unlike S4's M2/M3 steps, which entry 11.1 put on S4's line); and the living
documents that describe the fake today (`module_probes.md:318-319`, `module_tests.md:19`) stay true. If the coordinator reads ANY of them as
a description a reader would follow, (a) applies to that line, and it is ONE cause (S5's *Files* missed the docs item 8.1's grep finds),
one entry.]

## 14. Questions — ALL ANSWERED by the owner, 2026-11-17 (each as recommended)

| Q | Question | Recommendation | Answer (the owner, 2026-11-17) |
|---|---|---|---|
| Q1 | § 13: the six doc lines (five files) outside S5's *Files* — history (b), or on the line (a: entry 11.2, the eleventh full stop)? | (b), for the reasons in § 13; the coordinator decides before the plan gate | **(b)**: the six lines are history; not a change, not 11.2; their update is follow-up F45; S5's PR body lists them and names F45 (§ 13, § 9) |
| Q2 | K2's ninth site, `FindingAssessorTests.cs:44` (`using` on the product's workspace): brief-level, as "a fix that only satisfies the plan as written" (row E6-k's condition, a file on S5's line)? | yes, brief-level: no file and no decision outside § 0.1 and row E6-k; the PR lists it beside the stories' eight | **accepted**: `FindingAssessorTests.cs:44` takes a `using`, at brief level (K2) |
| Q3 | K11 as a lasting child-run theory (five Docker-free rows and a Docker-gated sixth, ~5 child runs per suite run) rather than a by-hand RED only | the theory: the stories ask for "a test", and a by-hand RED would not catch the next never-removed site in those classes; its cost is a few seconds per row (O8's per-class runs). If the coordinator prefers no child runs in the suite, the RED and GREEN are quoted by hand on the PR and the theory is dropped (brief-level either way) | **accepted**: E6-k's RED test is the lasting child-run theory (K11) |
| Q4 | N1, `StepBench.cs:64`'s stale doc comment (S4's file, outside S5's line) | not in S5; a one-line fix forward under S4's *Files* (`ModelArms/**`, a comment that only satisfies S4's plan), or carried to S4's promotion record as a deviation; the coordinator picks | **accepted**: not S5's; the coordinator handles it at S4's promotion or as a follow-up (N1) |
| Q5 | (4) (b): pin the old signature by a child run with an owned `TMPDIR` (so the pinned `bench-scratch` is made inside an owned folder, never in `/tmp`), rather than calling it in-process (which makes `/tmp/bench-scratch/<uuid7>`, the product's shared folder, from a test) | the child run; an in-process call would make a test write the product's shared folder, which D11 says is the product's | **accepted**: the child run with an owned `TMPDIR` (§ 5 (4) (b)) |
