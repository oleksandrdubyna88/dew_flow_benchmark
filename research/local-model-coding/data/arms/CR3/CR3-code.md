# CR3 — CODER notes (Sonnet), stories E5.5 and E5.6

Branch `exp/kimi-e56-classic-haiku-review-3`, started at `b4e2ea96`. No push. Deviations are also in the plan, `todo/PLAN_vibe_and_kimi_runtimes_E5.md`, items 50–58.

## Commits (in order)

1. `62ee29de` the Kimi rows: `shared/runtime-capabilities.json`, vectors, `RuntimeCapabilities.Runtimes`, the planner arm, `KimiConsultant` flag refusal; S5.6.1–S5.6.6.
2. `c0e26084` the generated TS table, regenerated; `capabilityAdmission.test.ts` (S5.6.8).
3. `f2b0aaac` `AKimiConsultantCheckTests` (S5.5.1–S5.5.4).
4. `c5b6d4b8`, `e7326590` `AKimiQuestionRowTests` (S5.6.7).
5. `20391a8e` the plan's status line and deviations 50–58.

## Per scenario: red seen? green?

| Scenario | Red seen | Red message vs table | Green |
|---|---|---|---|
| S5.6.1 `KimiHasThreeRows_EachCitingItsCells` | yes | *Expected none not to be <null>* — as the table | yes |
| S5.6.2 `TheVectorsCoverKimi` | yes | the loader message *… kimi × none row must have a vector deciding it* — as the table | yes |
| S5.6.3 `ThePlannerArm_…` | yes | first: *Expected type to be Confinement+Planned, but found Refused* (the matrix refuses kimi before the default arm). After the seed rows: *Expected disk.Cwd to be Snapshot … found Scratch* — as the table | yes |
| S5.6.4 `AdmissionFlagsBothRows` | yes | *Expected type to be Admission+Admitted, but found Admission+Refused* (type assertion, not the `Reason` text) | yes |
| S5.6.5 `AKimiQuestionLaunch_…` | yes | first the same Planned/Refused message; then *Expected disk.Request.WorkingDirectory to be the same string … differ at index 10* | yes |
| S5.6.6 `APlanWithFlags_IsRefusedForKimi` | yes | first Planned/Refused; then *Expected a <System.ArgumentException> to be thrown, but no exception was thrown* — as the table | yes |
| S5.6.7 `AKimiDiskRow_ReadsItsCopies_AndLeavesNothingBehind` (3 arms) | by break only (green on arrival) | scratch still there / `coai-vh/<8>` entry still there / recorder saw `…/home/credentials` | yes |
| S5.6.8 TS `kimi rows admit flagged, web refuses naming its record` | yes | *Expected values to be strictly deep-equal* (runtimes list), then *'kimi' is not a runtime the question consultant can launch for 'none'* — as the table | yes |
| S5.5.1 `AKimiCheck_RunsInACopy_OnAOneShotHome` | by break only | `NeedsASanitisedSnapshot=false` → state not `answered` (the `Workdir` guard fires first); differs from the table | yes |
| S5.5.2 `AfterTheCheck_TheHomeIsGone_JunctionFirst` (2 arms) | by break only | `coai-vh/<8>` kept (finally without Dispose); recorder saw the junction (DisposeAllUnder removed) | yes |
| S5.5.3 `ACheckCancelledMidTurn_StillDisposesItsHome` | by break only | same two breaks | yes |
| S5.5.4 `ARootlessCheck_…` | by break only | recorder saw `<scratch>/own/kh-…/home/credentials` when `KimiHomesDisposed` was removed from `ConsultCheckScratch.Delete` | yes |

S5.5.x and S5.6.7 were green on arrival because E5.3 (decision g) and E5.4 already hold all the code. Each break was reverted; the tree was clean after each (`git status`).

## Decisions

- The three Kimi rows cite `RESULTS_kimi_confinement.md` cells: none = M-K6, M-K3, M-K4; disk = M-K6, M-K9, M-K9 (e), M-K3, M-K4; web = no cell (note says why). Version 2.1.1, date 2026-10-13 (E5.4 decision 8). The `none` row is `unconfined`, as E5.6 decision 2 says.
- `KimiConsultant.MustCarryNoFlag` refuses a planned launch whose plan has any `Leading` or `Flags` entry, with the plan's sentence.
- Kimi has no flags in the planner (`"kimi" => Kimi(grant, flag)`): `disk` = Snapshot with `SnapshotSources = roots`; `none` = Scratch.
- S5.6.7's refusal arm is a root outside any Git work tree.
- The `AfterUnlink` recorder is `AsyncLocal`: it must be set in the test's own flow (a trap I fell in once; see plan item 56).

## Results

- MCP suite (whole, 6m39s): total 10556, succeeded 10452, skipped 84, **failed 20**. None of the 20 is in a file this work touched or tests it added. Two are the known ones (`KimiHomeTests.AHomeWhoseDataPathPasses260…`, `KimiRuntimeTests.ALaunchThatNamesItsHome…`). The other **18 are NOT in the brief's list**: `TheRoundsNumstatsAreCountedTests` (1), `ARoundIsMintedAKeyTests` (2), `SplitOrderTests` (1), `ThePersonsCommandsReachTheCallerTests` (1), `ARoundsNumberComesFromTheJournalTests` (3), `ACodeRoundRecordsItsSizeTests` (7), `EndToEndTests` (3). They are rounds/git numstat tests. I built `b4e2ea96` in a separate worktree and ran those classes there: the same tests fail at the baseline (7 of 12 in `ACodeRoundRecordsItsSizeTests`; 11 of 54 in the other six classes). So they are pre-existing on this host (git 2.53.0 here), not caused by this work. I did not find their cause (out of scope). The worktree is removed.
- All new/changed tests pass: `CapabilityMatrixTests` 40/40, `ConfinementPlannerTests` 34/34, `AKimiConsultantCheckTests` 5/5, `AKimiQuestionRowTests` 3/3.
- `npm run lint` (extension): clean.
- Extension `npm test`: 6290 tests, 6270 pass, 1 fail = the known `the baseline names no release that was never tagged` (no tags in this clone). The rest of the 6290 are in the other test files (the total includes all of them).
- Generator: `generate-runtime-capabilities.mjs` wrote 21 rows over 7 runtimes × 3 capabilities; `--check` is clean.
- Format: whole-solution `dotnet format --verify-no-changes --no-restore` exits 2 on this host with only IL2026/IL3050 analyzer errors in `src_server` and `src_bugs` (files not touched). Scoped to every changed C# file with `--include`: exit 0.
- `plan-lifecycle.mjs`: clean.

## What remains

- A review of these two stories (the plan says Opus reviews).
- The live checks (J2), recorded in RESULTS §31, and M-K17 — out of scope here.
- Nothing in the code is left red by this work.
- I read the rule list the resolver selects only in part (289 KB of rules selected; I applied the scenario-test, test-leftovers, csharp and reuse conventions from the surrounding code, not from a full read of every rule). Say so to the reviewer.
