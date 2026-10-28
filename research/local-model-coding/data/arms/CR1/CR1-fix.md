# CR1 — fixes after the review (Sonnet), E5.5 and E5.6

Branch `exp/kimi-e56-classic-haiku-review-1`. Review: `CR1-review.json` (verdict `ready`, six minor findings, no blocking, no major).
Started 12:06 UTC. Commits: `ab2d191a`, `11a7e23c`, `4a94acfd` (on top of `96d935ed`). Not pushed. No PR.

## Per finding

| Id | Verdict | What I did |
|---|---|---|
| R1 | **fixed** | The caveat was wrong for Kimi: the shim refuses every shell command. I changed it in both halves to `unconfined: can read this machine, and no flag removes it` (`CapabilityMatrix.cs:33`, `capabilityAdmission.ts:60`). Tests first: `TheUnconfinedCaveat_ClaimsNoShell_BecauseKimiHasNone` (C#) and a `doesNotMatch(/shell/)` line in the TS `kimi rows admit flagged…` test. Red seen on both: TS with the old sentence put back (`Kimi has no shell (M-K3, M-K4): its caveat may not claim one`); C# with a temporary break of the sentence. Reverted. The shared vectors do not carry the caveat text, so they did not change. The `note` of the codex `none` row in `shared/runtime-capabilities.json` still says "a shell is always there" — it is about codex and is true. |
| R2 | **fixed** | Test renamed to `AdmissionFlagsBothRows`, the plan table's name (`CapabilityMatrixTests.cs:230`). It is in the same commit as R1. |
| R3 | **fixed** | S5.6.6 is now a Theory with three arms: `Flags`, `Leading`, `AddDirs`. Each expects the `ArgumentException` that names the shim. Teeth: I cut `MustCarryNoFlag` back to `Flags` only; the `Leading` and `AddDirs` arms went red (*Expected a <System.ArgumentException> to be thrown, but no exception was thrown*), the `Flags` arm stayed green. Reverted. |
| R4 | **partly fixed** | In the *answered* and *cancelled* endings the test now finds the row's `coai-question-*` folder (the ancestor of the copy) and asserts it is gone. In the *refused before the launch* ending it still asserts nothing about the scratch. Why: there is no request in that ending to read the folder from; the `MakeScratch` seam is internal to `QuestionRowLaunch`, and `PanelService` builds that class itself (`QuestionFanOut.cs:64`), so the test cannot reach it; a TEMP listing would also see the scratch of other tests' rows and fail by chance. A full fix needs a seam on `PanelService`/`QuestionFanOut` (a production change) or a second test that builds `QuestionRowLaunch` by hand. I did not do either in the time. Teeth: with `Remove(scratch)` taken out of `finally`, both endings went red, but on the older copy assertion first, because the copy lives inside the scratch. So the new scratch assertion is not proved red on its own. Reverted. |
| R5 | **fixed** | `--cwd` must start with the check's own scratch (`_temp.Path`) and contain `own/tree/`. The review's fix said `EndsWith(own/tree)`; that is wrong: the real path is `…/own/tree/snapshot-<id>` (the first run of my test failed on it), and the plan says "under". I used "contains `own/tree/`" instead. |
| R6 | **fixed in the plan, the question stays open** | Deviation 59 records the model change (Sonnet coded both stories and these fixes; Haiku reviewed and made `96d935ed`) against §0, §0.2 and the DoD. I did not invent a reason or an approval: the repository holds neither. The deviation says that Opus must re-verify both stories before the DoD items are ticked, or the operator records the approval. The status line says the same. Deviation 60 records the five minor fixes. |

None of the findings was wrong.

## Not from the review

- The Haiku review notes (`CR1-hreview.md`) leave **H2** open: `GitScratch.AfterUnlink` is a process-wide static written by several classes outside the `fakecli-env` collection. It is outside the task ("fix every finding of the review"); I did not touch it. It may explain the intermittent `AVibeReviewerRunsThroughAFakeVibeTests` failure the first coder saw. Unproven.

## Results

- Build (`dotnet build dew_flow_connect_other_ais.slnx -c Debug -m:4 --disable-build-servers`): 0 warnings, 0 errors. (The first try died with an `Internal CLR error 0x80131506`; the second try, the same command, passed.)
- Changed classes (`AKimiConsultantCheckTests`, `AKimiQuestionRowTests`, `ConfinementPlannerTests`, `CapabilityMatrixTests`): 84 total, 84 passed.
- Whole MCP suite, once (7m23s, output `suite-fix.txt`): 10558 total, 10455 passed, 84 skipped, **19 failed**. The same 19 as in the review's run (`suite-run1.txt`): the 2 known host failures, and 17 in six classes that are not Kimi (`ACodeRoundRecordsItsSizeTests` 7, `EndToEndTests` 3, `ARoundsNumberComesFromTheJournalTests` 3, `ARoundIsMintedAKeyTests` 2, `ThePersonsCommandsReachTheCallerTests` 1, `SplitOrderTests` 1). The review's notes say the same six classes fail the same way at base commit `b4e2ea96`; I did not repeat that check. No Kimi, matrix or planner test failed. The Vibe test that failed once for the first coder passed this time. The 3 new tests are in the 10558 (it was 10555).
- Format (`dotnet format --verify-no-changes --no-restore --include` over the 5 changed C# files): clean. The whole-solution form fails on this host with analyzer errors in `src_server` that were there before (see CR1-code.md).
- Generator (`generate-runtime-capabilities.mjs --check`): up to date, 21 rows over 7 runtimes × 3 capabilities. The generated file did not change.
- TS tests (`npm test` in `src_vs_code`): 6390 tests, 6369 passed, 20 skipped, 1 failed — only the known `the baseline names no release that was never tagged`.
