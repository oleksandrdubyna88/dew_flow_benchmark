# S2 fix notes — review of E5.5 and E5.6 (Haiku coder)

Review: `/home/jinx/git/coai-exp/notes/S2-review.json` (verdict fix-then-ready, 0 blocking, 0 major, 6 minor).
Coder notes: `S2-code.md` does not exist; the per-task notes `S2-t1-code.md` … `S2-t6-code.md` were used instead.
No defect was found, so no red-first test was needed. Two changes are test-only (rename, line layout).

## Per finding

- **R1 (minor) — FIXED.** Status line in `todo/PLAN_vibe_and_kimi_runtimes_E5.md` now says E5.5–E5.6 are
  implemented; it names the two known MCP failures and the known extension failure. The `AnUnknownCommit`
  claim is removed. Note: my own earlier run (in the plan's "Verified by") had `AnUnknownCommit` failing,
  and the reviewer's run had it passing. It is timing-dependent and the file is not touched by this change.
  Its result in my final run is in the report below.
- **R2 (minor) — see final run.** The "Verified by" block still names the old `dotnet test --project` command.
  It is replaced with the MTP run result in the final commit.
- **R3 (minor) — PARTLY FIXED (honest correction).** Deviations 50 and 55 and the module_tests rows said
  "teeth by temporary breaks", but no break and no red message is recorded anywhere. I changed the text to
  say the teeth are NOT recorded, and I list it as an open item. I did NOT run the temporary breaks in the
  time left (deadline), so the teeth are still unproven.
- **R4 (minor) — FIXED.** `research/module_runners.md` line ~3717: the broken sentence is now one sentence:
  "…hold it, and since E5.6 the capability matrix admits a Kimi question row (none and disk, flagged unconfined; web refused)."
- **R5 (minor) — NOT FIXED (open item).** `AKimiQuestionRowTests` makes its scratch as `coai-question-*`
  in the system TEMP through `QuestionRowLaunch.MakeScratch`. The fix needs a seam through `PanelSettings`
  or the service, which is a production change. It is out of time for this round. Recorded as an open item in
  `research/module_tests.md` (the E5.5–E5.6 table) and in deviation 55.
- **R6 (minor) — FIXED.** The planner test is renamed to the plan's scenario name
  `ThePlannerArm_IsACopyForDisk_AnEmptyFolderForNone_AndRefusesWeb`, and each assertion is on its own line.
  `research/module_tests.md` now ties S5.6.1 (`KimiHasThreeRows_EachCitingItsCells`), S5.6.2
  (`TheVectorsActuallyLoaded_AndCoverEveryPairOfTheTable`; the plan's name `TheVectorsCoverKimi` is the older name)
  and S5.6.4 (`AdmissionFlagsBothRows`) to the CapabilityMatrixTests row.

## Final results (2026-10-26 UTC, before the 23:35 deadline)

- **R2 — FIXED** (commit `2c133b05`). The "Verified by" block names the MTP executable run and its counts.
- Build: `dotnet build dew_flow_connect_other_ais.slnx -c Debug -m:4 --disable-build-servers`: exit 0.
- MCP suite, whole, MTP executable: total 10557, succeeded 10471, skipped 84, failed 2. Both failures are known: `KimiRuntimeTests.ALaunchThatNamesItsHome_…` and `KimiHomeTests.AHomeWhoseDataPathPasses260_…`. `AnUnknownCommit_IsGitFailed_NeverAThrow` passed in this run.
- Format: `dotnet format src_mcp/tests/CoaiMcp.Tests.csproj --verify-no-changes`: exit 0.
- Generator: `generate-runtime-capabilities.mjs --check`: up to date (21 rows, 7 runtimes × 3 capabilities).
- TypeScript (`npm --prefix src_vs_code test`): 6290 tests, 6270 pass, 1 fail, the known `the baseline names no release that was never tagged`.
- Open items (not fixed): R3 teeth for S5.5.1–S5.5.4 and S5.6.7 are not recorded; R5 the question-row scratch is outside `testsToDelete-`.
