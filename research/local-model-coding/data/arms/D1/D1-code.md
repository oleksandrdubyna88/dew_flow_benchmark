# D1 coder notes — E5.5 and E5.6 (Haiku, exp/kimi-e56-haiku-direct-1)

Written 2026-10-26, after the work. Times UTC.

## Scenarios

| Scenario | Test | Red seen? | Green now? | Note |
|---|---|---|---|---|
| S5.5.1 | `AKimiConsultantCheckTests.AKimiCheck_RunsInACopy_OnAOneShotHome` | no (green on arrival, H1) | yes | teeth: `HomesFolder` emptied → red |
| S5.5.2 | `AfterTheCheck_TheHomeIsGone_JunctionFirst` (Theory, 2 arms) | no (green on arrival, H1) | yes | no separate tooth |
| S5.5.3 | `ACheckCancelledMidTurn_StillDisposesItsHome` | no (green on arrival) | yes | asserts `failureKind: cancelled` (H2) |
| S5.5.4 | `ARootlessCheck_KeepsTheHomeInItsScratch_AndDisposesItJunctionFirst` | no (green on arrival) | yes | no tooth run |
| S5.6.1 | `CapabilityMatrixTests.KimiHasThreeRows_EachCitingItsCells` | NOT OBSERVED — I changed the table before running this class, so I did not see its red | yes | |
| S5.6.2 | `CapabilityMatrixTests.TheVectorsActuallyLoaded_AndCoverEveryPairOfTheTable` (existing) | NOT OBSERVED as a red of its own; its only failure after the change was `EveryRow_CitesTheRecord` (H7) | yes | plus the record list (H7) |
| S5.6.3 | `ConfinementPlannerTests.ThePlannerArm_IsACopyForDisk_AnEmptyFolderForNone_AndRefusesWeb` | yes: "Expected type to be Planned, but found Refused" (matrix refused kimi, H3) | yes | teeth: disk arm → `Scratch`: "Expected disk.Cwd to be CwdKind.Snapshot … but found CwdKind.Scratch" |
| S5.6.4 | `CapabilityMatrixTests.KimiAdmissionFlagsBothRows_AndRefusesWeb` | NOT OBSERVED (same reason as S5.6.1) | yes | |
| S5.6.5 | `ConfinementPlannerTests.AKimiQuestionLaunch_IsTheShimRequest_InItsFolderOrItsCopy_OnTheRowsHome` | yes (planner refused kimi) | yes | no `KimiEnvironment.Refusal` assertion (H6) |
| S5.6.6 | `ConfinementPlannerTests.APlanWithFlags_IsRefusedForKimi` | yes (planner refused kimi) | yes | |
| S5.6.7 | `AKimiQuestionRowTests` (3 facts: answered, refused before launch, cancelled) | no (green on arrival, H4) | yes | teeth via H5 break |
| S5.6.8 | TS `kimi rows admit flagged, and web refuses naming its record` (`capabilityAdmission.test.ts`) | NOT OBSERVED — the TS test was added after the data change, and `npm test` ran only once, after the change | yes | |

Red messages that differ from the tables: S5.6.3, S5.6.5, S5.6.6 (H3). The reds I did observe are S5.6.3, S5.6.5, S5.6.6 (on the
matrix refusal, first run), and S5.6.2's neighbour `EveryRow_CitesTheRecord` (missing record). S5.6.1, S5.6.2, S5.6.4 and S5.6.8 were
not seen red; I changed the data first. This is a gap against the brief's "watch each test fail first". The table's "against a stub" reds for S5.5 are not reproduced
because the code they stub already exists (H1).

## Decisions I made

1. The Kimi `none` row is `unconfined`, the `disk` row `unconfined`, the `web` row `unmeasured` — as E5.6 decision 2 says.
2. Cells: `none` → `M-K6`, `M-K3`, `M-K4`; `disk` → those plus `M-K9`, `M-K9 (e)`; `web` → none (note explains).
3. `resultRef`s cite `research/RESULTS_kimi_confinement.md` sections (§9 M-K6, §6 M-K3, §7 M-K4, §12 and §21 M-K9); the RESULTS file is not edited.
4. The row `date` is `2026-10-13` (the day the cells were measured, as E5.4 decision 8 uses).
5. The planner's Kimi arm: `disk` → Snapshot with the roots as sources, `none` → Scratch, no flags (decision 1).
6. `KimiConsultant` refuses a planned launch that carries any flag (decision 3), with a message naming the shim.
7. Test placement: S5.6.1/S5.6.4 in `CapabilityMatrixTests`; S5.6.3/S5.6.5/S5.6.6 in `ConfinementPlannerTests` (both use their helpers);
   S5.6.7 in a new `AKimiQuestionRowTests`; E5.5 in a new `AKimiConsultantCheckTests`.

## Commits (on exp/kimi-e56-haiku-direct-1, no push)

See `git log` on the branch; one commit per part (E5.6 product and table; E5.5 test class; E5.6 row test; plan status).

## Verification

- Build: `dotnet build dew_flow_connect_other_ais.slnx -c Debug -m:4 --disable-build-servers` — succeeded, 0 errors, 0 warnings, at the baseline and after each change.
- Targeted: `ConfinementPlannerTests` 34 of 34; `CapabilityMatrixTests` 39 of 39; `AKimiConsultantCheckTests` 5 of 5; `AKimiQuestionRowTests` 3 of 3.
- Whole MCP suite (run after all code changes, before the commits): total 10555, passed 10469, skipped 84, failed 2. The two failures are the two that already failed at the start on this host: `KimiHomeTests.AHomeWhoseDataPathPasses260_IsCheckedAndDisposed` and `KimiRuntimeTests.ALaunchThatNamesItsHome_GoesThroughTheLaunchControl_WhichReadsThePersonsSignIn`. Nothing new failed.
- Generator: `node scripts/generate-runtime-capabilities.mjs` wrote 21 rows over 7 runtimes × 3 capabilities; `--check` clean.
- Extension: `npm test` exit 0 (its own run, after the generator and TS test edits). The start-of-run failure ("the baseline names no release that was never tagged") is not in the tail I read; see the final report for what I could confirm.
- Format: `dotnet format --verify-no-changes --no-restore --include` over the seven C# files I changed or added: exit 0. Solution-wide run printed only pre-existing IL analyzer warnings in `src_server`, which I did not touch.

## Remaining

- Live checks (J2): the Kimi check and the disk question row on a real `kimi` — not run (money). RESULTS §31 not written (out of scope).
- M-K17 (the web tools) — not run (out of scope). `web` stays refused.
- Teeth for S5.5.2's junction-first order and S5.5.4 — not run (time).
- The two tests that failed at start on this host (`KimiRuntimeTests.ALaunchThatNamesItsHome…`, `KimiHomeTests.AHomeWhoseDataPathPasses260…`) — not my change; their status is in the final MCP run.
- The extension's start-of-run failure (`the baseline names no release that was never tagged`) — not touched.
