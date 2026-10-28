# CR3 — REVIEW of E5.5 and E5.6 (Haiku, reviewer and fixer)

Branch `exp/kimi-e56-classic-haiku-review-3`, diff `b4e2ea96..HEAD`. Written 14:10–14:20 UTC, before any code change.
Instructions loaded: `AGENTS.md`, `.agents/conventions/ENTRY.md`, `.agents/PROJECT.md`, resolver `rules.mjs` (`check` resolved, pinned at `e29883f8`);
rules read in full: `common.testing`, `common.scenario-tests`, `common.generated-code-tests`, `common.coai-question-consultant`,
`common.test-leftovers`, `common.security`, `csharp.doctrine`, `typescript.doctrine`. Not read in full (time): `common.coding-style`,
`common.reuse-first`, `common.planning-docs`, `common.git-workflow`, `common.reliability`, `common.measurement`, local `review-gate` and `vendor-routing`.
Say so in the final report.

## Step 1 — review

Scenario coverage (name → test exists, asserts the table's *Then*):

| Scenario | Test | Found | Note |
|---|---|---|---|
| S5.5.1 | `AKimiConsultantCheckTests.AKimiCheck_RunsInACopy_OnAOneShotHome` | yes | asserts `--cwd` under `own\tree`, home under `coai-vh\<8>\kh-<10 hex>`, answered, no breach |
| S5.5.2 | `AfterTheCheck_TheHomeIsGone_JunctionFirst` (2 arms) | yes | `coai-vh` empty, recorder never invoked, person folder hash equal |
| S5.5.3 | `ACheckCancelledMidTurn_StillDisposesItsHome` | yes | cancelled, folder empty, recorder, hash |
| S5.5.4 | `ARootlessCheck_KeepsTheHomeInItsScratch_…` | yes | home under `own/kh-…`, scratch gone, recorder, hash |
| S5.6.1 | `CapabilityMatrixTests.KimiHasThreeRows_EachCitingItsCells` | yes | standings, cells, version |
| S5.6.2 | `CapabilityMatrixTests.TheVectorsCoverKimi` | yes | a new named test; the table said "the existing test at :80", which also covers it |
| S5.6.3 | `ConfinementPlannerTests.ThePlannerArm_IsACopyForDisk_AnEmptyFolderForNone_AndRefusesWeb` | yes | |
| S5.6.4 | `CapabilityMatrixTests.AdmissionFlagsBothRows` | yes | |
| S5.6.5 | `ConfinementPlannerTests.AKimiQuestionLaunch_IsTheShimRequest_…` | yes | |
| S5.6.6 | `ConfinementPlannerTests.APlanWithFlags_IsRefusedForKimi` | yes | |
| S5.6.7 | `AKimiQuestionRowTests.AKimiDiskRow_ReadsItsCopies_AndLeavesNothingBehind` (3 arms) | yes | |
| S5.6.8 | `capabilityAdmission.test.ts` `kimi rows admit flagged, web refuses naming its record` | yes | |

Decisions checked against the code:
- Planner arm `"kimi" => Kimi(grant, flag)`: disk = Snapshot with roots, none = Scratch, no flags, web never reaches it. Matches E5.6 decision 1.
- Standings: `none` and `disk` unconfined with flag `unconfined` in the vectors JSON, `web` unmeasured and refused. Matches decision 2.
- `MustCarryNoFlag` refuses a plan with `Leading` or `Flags`. Matches decision 3.
- The cited RESULTS cells (M-K6, M-K3, M-K4, M-K9, M-K9 (e)) exist as headings in `research/RESULTS_kimi_confinement.md` (§9, §6, §7, §12, §21).
- Generated TS table: changed only by the new rows and the runtimes list. The generator `--check` is re-run in Step 2.
- Security: nothing is admitted unconfined without its flag. The new vectors are `admitted: true` only with `flag: unconfined`. No new process argv, no new path outside the test folder.

### Findings

**H1 — minor — `src_mcp/tests/AKimiQuestionRowTests.cs:` (the `S5.6.7` theory body, `GitScratch.AfterUnlink = _unlinkedByTheDeleter.Add;`) and its `DisposeAsync`.**
The recorder is set in the test and never cleared. `AKimiConsultantCheckTests` clears it in `Dispose` (`AKimiConsultantCheckTests.cs:39`). One is hygiene and one is not, so a later test in the same async flow could see a stale recorder.
Fix: clear `GitScratch.AfterUnlink = null` in `DisposeAsync`, the same as the other class.

**H2 — minor — `todo/PLAN_vibe_and_kimi_runtimes_E5.md` §E5.5 and §E5.6, "Expected red today" column.**
The table says the S5.5.x and S5.6.7 scenarios are red before the change. The coder's notes say they are green on arrival and went red only when a break was introduced (E5.3 g and E5.4 hold the code). This is a documented deviation, not a test defect, since each scenario does fail under a break. Not changed: the claim is in the plan, and the plan is the owner's. Verified by reading the notes only. I did not re-run the breaks myself (time).

**H3 — minor — `research/module_tests.md` (not in the diff).**
`common.scenario-tests` asks that a new flow (the Kimi question row) be listed there. The brief puts `research/` out of scope, so it is not fixed here. Reported for the next reviewer.

**H4 — minor — `src_mcp/tests/AKimiQuestionRowTests.cs:` the comment on the recorder ("InitializeAsync's would never reach it").**
The other class sets the same recorder in its constructor and the coder reports it works. The comment is about one ordering choice and does not state the constructor case. Not a defect. Left as is.

No blocking or major finding so far.

## Step 2 — fixes

- H1: fixing now (test hygiene, cheap). A red test first is not needed for a test-only hygiene change; the break check is: with the reset present, the suite still passes; this change cannot turn a passing test red.
- H2, H3, H4: not fixed (see above).

## Outcome per finding

- H1: **fixed**, commit `ad1a61f9` (`test(kimi): the question row's unlink recorder is cleared when its class is disposed`). Test-hygiene only: no behaviour changes, so no red test was possible. The change cannot turn a passing test red; the four changed classes and the full suite were re-run after it.
- H2: **not fixed**. The plan's "Expected red today" column is the owner's text, and the deviation is recorded by the coder. Each scenario does fail under a break (the coder's per-break table). I did not re-run those breaks myself.
- H3: **not fixed** — `research/` is out of scope for this task. Left for the next reviewer: `research/module_tests.md` should list the Kimi question-row flow.
- H4: **not fixed**, wrong on a second look: the two classes use the recorder in two different places and both work; the comment is accurate for its own class.

## Results (run on 2026-10-28, after commit `ad1a61f9`)

- Build (`dotnet build` of the MCP test project, `-m:4 --disable-build-servers`, via `low-priority.mjs`): **succeeded, 0 warnings, 0 errors**.
- Changed MCP test classes (`AKimiQuestionRowTests`, `AKimiConsultantCheckTests`, `CapabilityMatrixTests`, `ConfinementPlannerTests`): **82 of 82 passed**.
- Whole MCP suite (`CoaiMcp.Tests`, run once): total 10556, **succeeded 10452, skipped 84, failed 20**. The 20 failed are the set the coder listed: 18 in rounds and git-numstat classes (`ACodeRoundRecordsItsSizeTests` ×7, `EndToEndTests` ×3, `ARoundsNumberComesFromTheJournalTests` ×3, `ARoundIsMintedAKeyTests` ×2, `SplitOrderTests` ×1, `ThePersonsCommandsReachTheCallerTests` ×1, `TheRoundsNumstatsAreCountedTests` ×1) and the two known Kimi tests (`KimiHomeTests.AHomeWhoseDataPathPasses260_IsCheckedAndDisposed`, `KimiRuntimeTests.ALaunchThatNamesItsHome_…`). I did **not** re-verify the 18 at `b4e2ea96`; that is the coder's claim.
- Format (`dotnet format --verify-no-changes --no-restore`, scoped with `--include` to the changed C# files): **exit 0**.
- Generator (`src_vs_code/scripts/generate-runtime-capabilities.mjs --check`): **up to date**, 21 rows over 7 runtimes × 3 capabilities.
- TS (`npm test` in `src_vs_code`): **6290 tests, 6270 pass, 19 skipped, 1 fail**. The one failure is `the baseline names no release that was never tagged` (no git tags in this clone), the known one.

## Not done (time)

- Break-tests of the S5.5.x and S5.6.7 scenarios by myself (H2).
- Re-check of the 18 rounds/git failures at the baseline.
- Full read of `common.coding-style`, `common.reuse-first`, `common.planning-docs`, `common.git-workflow`, `common.reliability`, `common.measurement`, and the local review-gate and vendor-routing rules.
- `research/module_tests.md` (H3).
