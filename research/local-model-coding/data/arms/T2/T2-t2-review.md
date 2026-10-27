# T2-t2 — review notes

Reviewed: commit `5f76113b` (base `b4e2ea96`). Files changed: `src_mcp/runners/Kimi/KimiConsultant.cs`, `src_mcp/tests/KimiConsultantTests.cs` only. No commit made by the reviewer: nothing needed a change.

## Findings

1. **Contract §1 (`KimiConsultant.cs`) — right as it was.** The call `MustCarryNoFlags(launch);` is directly after `ConsultantLaunches.MustBeLaunchable(launch);`. The private method is directly after `Build`, before `ModelOf`. Its text, the check (`Flags.Count > 0 || Leading.Count > 0`), the `ArgumentException` message ("...the shim's...") and `nameof(launch)` match the document. The new `<para>` in `<remarks>` matches word for word. Only `Planned` launches are checked; a conversation launch is not refused.
2. **Contract §2 (tests) — right as it was.** The `using CoaiMcp.Core.QuestionConsult;` is added. The section comment, the constants, the helpers (`RowFolder`, `RowScratch`, `RowTree`, `KimiPlan`, `Question`), `AKimiQuestionLaunch_IsTheShimRequest_InItsFolderOrItsCopy_OnTheRowsHome` and `APlanWithFlags_IsRefusedForKimi` (both `InlineData` cases) match the document. The plans are built by hand, not with `ConfinementPlanner.Plan`. No new `using` was needed for `ProcessEnvironment` or `ConsultantRoles`; the build has 0 warnings and 0 errors.
3. **Red reason — right.** The coder's notes show S5.6.5 green at once, and S5.6.6 red for both cases with "Expected a <System.ArgumentException> to be thrown, but no exception was thrown." This is the reason the document names. I did not run it again; the notes give the exact message.
4. **Test of `Leading` — left as it was.** `MustCarryNoFlags` also refuses a plan with `Leading` entries (as the contract code says), but the document's tests use only `Flags` (`--yolo`). The document gives the tests exactly, so I did not add one. This is a small gap in the spec, not a defect in the change.
5. **Scope — right.** Only the two named files changed. `ConsultantLaunches`, `KimiRuntime` and `ConfinementPlanner` are not touched.
6. **Commit trailer — left.** The trailer says `Claude Haiku 5.5`. I did not rewrite history for this.

## Test results (my own run)

- Build (`-m:4 --disable-build-servers`): succeeded, 0 warnings, 0 errors.
- `KimiConsultantTests` + `ConfinementPlannerTests`: 46 of 46 passed.
- Whole MCP suite: 10536 total, 10449 passed, 84 skipped, **3 failed**:
  - `KimiRuntimeTests.ALaunchThatNamesItsHome_GoesThroughTheLaunchControl_WhichReadsThePersonsSignIn` — known, not the task's.
  - `KimiHomeTests.AHomeWhoseDataPathPasses260_IsCheckedAndDisposed` — known, not the task's.
  - `TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow` — **not in the document's known list, and the coder saw 2 failures, not 3.** It fails also when run alone (state is `recorded`, test expects `unavailable: git…`). The test passes a one-millisecond deadline to a git call, so the result depends on host speed. It has nothing to do with Kimi, and the task did not touch its code. I did not run it at `b4e2ea96`, so I cannot say if it is red there too; I think it is a timing failure of this host.
