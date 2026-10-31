# DR1 — fix notes (Haiku coder), review of E5.5 and E5.6

Branch: exp/kimi-e56-haiku-direct-review-1. Commits after the review base: fa002a37 (tests), 555546f7 (seed and plan).
Nothing pushed. No PR. No live `kimi` or `vibe` run. Deadline 12:34 UTC.

Review verdict was `fix-then-ready`. There were no blocking findings. The one major finding is R1. Every minor finding was checked against the code. None was wrong.

## Per finding

| ID | Severity | Result | What was done |
|---|---|---|---|
| R1 | major | **fixed** | The disk-row test folder is now `testsToDelete-kimi-qrow-`, so the leftovers sweep finds it. Commit fa002a37. |
| R2 | minor | **not fixed** | I did not add the "credentials link exists during the turn" assertion and did not run the teeth check (break `KimiHome.DisposeAllUnder` and watch the recorder test go red). No time left in the window. The junction-first tests may still pass when the link was never made. Open. |
| R3 | minor | **partly fixed** | S5.5.3 now asserts `failureKind == "cancelled"`. The session_lost arm of S5.5.2 is not asserted: the review names no failure-kind string, and `ConsultantCheck.cs` does not show one for a lost session. Open: find the string the check writes for exit 70, then assert it. |
| R4 | minor | **fixed** | The answered and cancelled disk-row tests record the row's `WorkingDirectory` in `During` and assert that the folder is gone after the row ends. The refused arm is not changed: the row never launches, so there is no copy to record. |
| R5 | minor | **partly fixed** | The none launch now asserts `KimiEnvironment.Refusal(...)` is empty. The one-element `ContainInConsecutiveOrder("--home")` check is removed. The disk launch has no refusal assertion yet. Open, one line. |
| R6 | minor | **partly fixed** | The collection comment now says the hooks are `AsyncLocal` and the collection is a guard. It also says not every class that sets a hook has joined it, which the old comment got wrong. The collection and the `[Collection]` attributes are NOT removed: I did not check whether any other shared state needs them. Open: decide and then remove or keep. |
| R7 | minor | **fixed** | The disk-row vendor is `"kimi"`, not `"mistral"`. The changed classes were rebuilt and passed after this edit. |
| R8 | minor | **fixed** | The E5.5–E5.6 deviation list is renumbered 47–56. E5.7 already uses 40–46, so no number collides. The plan status line is updated. Deviation 55 now says S5.6.2 is covered by `TheVectorsActuallyLoaded_AndCoverEveryPairOfTheTable`. Commit 555546f7. |
| R9 | minor | **fixed** | The seed `why` in `shared/runtime-capabilities.json` now names `research/RESULTS_kimi_confinement.md` (Kimi Code 2.1.1) for the kimi rows. The generator was run and `--check` says up to date. The generated TS did not change, because the `why` text is not emitted. Commit 555546f7. |

No finding was judged wrong. No finding was left out without a reason.

## Checks

- Build: `dotnet build dew_flow_connect_other_ais.slnx -c Debug -m:4 --disable-build-servers` — 0 warnings, 0 errors.
- Changed Kimi test classes (`*AKimi*`, `*KimiDeleter*`): 42 total, 0 failed, 30 passed, 12 skipped (Windows-only, D18).
- Format: `dotnet format --verify-no-changes --no-restore` exits 2. Its output contains only IL analyzer errors in `src_bugs/src/*.cs` and `src_server/src/*.cs`. None of them is in `src_mcp`, and this branch changed neither project. I did not check whether they exist at `b4e2ea96`.
- Generator: wrote the file, then `--check`: "up to date" (21 rows over 7 runtimes × 3 capabilities).
- Full MCP suite and TS tests: see the results section below.

## Results (filled in at the end)

- Whole MCP suite (run after commit 555546f7, 12:07–12:14 UTC): total 10557, failed 3, succeeded 10470, skipped 84. Exit 2.
  - `KimiHomeTests.AHomeWhoseDataPathPasses260_IsCheckedAndDisposed`: known, not ours.
  - `KimiRuntimeTests.ALaunchThatNamesItsHome_GoesThroughTheLaunchControl_WhichReadsThePersonsSignIn`: known, not ours.
  - `TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow`: NOT on the brief's list. It fails on a rerun alone (`AllChangesAsync` returns not Failed). No commit on this branch changes `src_mcp/src` (checked against b4e2ea96). I did NOT check whether it fails at b4e2ea96, so its cause is open. Treat it as a new failure to investigate.
- TS tests (`npm test` in src_vs_code): 6290 total, 6270 pass, 1 fail = `the baseline names no release that was never tagged` (no tags in this clone). Known.
- Format: exit 2, IL analyzer errors only in `src_bugs` and `src_server`, none in `src_mcp`. Not checked against b4e2ea96.

## Still open (not done in the window)

- R2 teeth run and the link assertion in `During`.
- R3 session_lost arm: the failure-kind string is not in the code I checked.
- R5 disk-launch refusal assertion.
- R6 collection: keep or remove, once it is known whether shared state needs it.
- The numstat test (above).
