# D2 — fix notes: review of E5.5 and E5.6 (Haiku coder, branch exp/kimi-e56-haiku-direct-2)

Start of the fix pass: 31e866ef. Commits: 0841b09f (R2, R4, R5 in part, R7), 88bdbe79 (R1, R3). Nothing pushed, no PR.
Known reds at start (not mine): KimiRuntimeTests.ALaunchThatNamesItsHome_…, KimiHomeTests.AHomeWhoseDataPathPasses260_…, and the extension's "the baseline names no release that was never tagged".

## Per finding

| ID | Severity | Result | Note |
|---|---|---|---|
| R1 | blocking | FIXED | Commit 88bdbe79. S5.6.7 is written as src_mcp/tests/AKimiDiskRowLeavesNothingBehindTests.cs: a Theory with three arms (answered, refused at its copy, cancelled mid-turn). It goes through the service (AskConsultantsAsync) with a QuestionConsult row, PlayedKimi, a git root in the test's folder and the GitScratch recorder. It checks: the prompt names the copy and never the root; the row's home and folder are gone after each ending; the recorder is never invoked; the person's sign-in is whole. Green 3/3. NOT watched red first: the arms pass on arrival, because the adapter and homes are already in place (same as plan H6). The file is new; H3 covers only the planner arm. |
| R2 | major | PARTLY FIXED | Commit 0841b09f. The GitScratch.AfterUnlink recorder is installed (try/finally, cleared) in S5.5.2 and S5.5.4; both assert it is never invoked. Renamed to the table names (AfterTheCheck_TheHomeIsGone_JunctionFirst; ARootlessCheck_KeepsTheHomeInItsScratch_AndDisposesItJunctionFirst). NOT done: the exit-70 arm of S5.5.2, and the ReadOnly attribute check. NOT done: the teeth check (sending the disposal through the bare deleter). |
| R3 | major | FIXED | Commit 88bdbe79. S5.5.3 (ACheckCancelledMidTurn_StillDisposesItsHome) is written: the played shim cancels and blocks; the output says cancelled; the one-shot home and the scratch are gone; the recorder is never invoked. Green 1/1 (AKimiConsultantCheckTests: 4/4). Green on arrival: the finally in ConsultantCheck.TurnAsync already disposes the home. NOT done: the teeth check (breaking that finally). |
| R4 | minor | FIXED | Commit 0841b09f. KimiConsultant refuses Plan.Flags, Plan.Leading or Plan.AddDirs (message updated). APlanWithFlags_IsRefusedForKimi_NamingTheShim is now a 3-arm Theory. Red first: the leading and addDirs arms failed; the flags arm already passed. Green after the fix (ConfinementPlannerTests 36/36). |
| R5 | minor | PARTLY FIXED | Commit 0841b09f. The disk request's --home is now asserted under RowFolder. NOT done: the KimiEnvironment.Refusal(request, places) assertion, because the planner test has no KimiPlaces value to pass. |
| R6 | minor | PARTLY FIXED | The renames of S5.5.2 and S5.5.4 are done (under R2). NOT done: the renames of S5.6.3, S5.6.4, S5.6.6 and S5.6.8. H3 still records where those tests sit. |
| R7 | minor | FIXED | Commit 0841b09f. The D13 summary sits on TheFlags_AreD13s_… again; the Kimi tests have no summary of their own (CapabilityMatrixTests 39/39). |
| R8 | minor | NOT FIXED (out of scope) | research/ is out of scope for this brief. The owner must add the Kimi consultant check (S5.5.x) and the Kimi question row (S5.6.x) to research/module_tests.md, with their covering tests. |

## Plan status (not edited)

The plan's status line and H8 (todo/PLAN_vibe_and_kimi_runtimes_E5.md) still say S5.5.3 and S5.6.7 are not written. Both are now written (88bdbe79). The owner must update the status line and H8. I did not edit the plan in this pass.

## Checks

- Build: `dotnet build dew_flow_connect_other_ais.slnx -c Debug -m:4 --disable-build-servers`: 0 errors (last run, after 88bdbe79).
- Changed classes: ConfinementPlannerTests 36/36; AKimiConsultantCheckTests 4/4; AKimiDiskRowLeavesNothingBehindTests 3/3; CapabilityMatrixTests 39/39.
- Format: `dotnet format` with `--verify-no-changes`: CoaiMcp.Tests exit 0 (last run, after 88bdbe79). CoaiMcp.Runners exit 0 (run after 0841b09f; it has not changed since). Log: notes/D2-fix-format.log.
- Generator: `node scripts/generate-runtime-capabilities.mjs --check` run from src_vs_code (the script lives there): up to date, exit 0.
- Extension npm test (TS): 6290 tests, 6270 pass, 1 fail, 19 skipped. The one failure is "the baseline names no release that was never tagged" (known). Log: notes/D2-fix-ts.log.

## Results: full MCP suite

- Run 1 (after 0841b09f, before the R1 and R3 tests): 10551 total, 10463 passed, 2 failed (the two known reds), 84 skipped. Log: notes/D2-fix-suite.log.
- Run 2 (after 88bdbe79, the final code): 10556 total, 10469 passed, 3 failed, 84 skipped. Log: notes/D2-fix2-suite.log.
  - KimiRuntimeTests.ALaunchThatNamesItsHome_… (known red)
  - KimiHomeTests.AHomeWhoseDataPathPasses260_… (known red)
  - TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow: NEW in run 2, not in run 1. It sets a 1 ms git deadline and fails under load. Run alone, the class is 10/10 green. Run 2 overlapped with the TS run, so I read it as timing flake, not a regression from my change. Not re-run in a quiet state, so this is not proven.
