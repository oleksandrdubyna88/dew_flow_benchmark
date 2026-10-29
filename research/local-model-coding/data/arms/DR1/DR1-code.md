# DR1 — coder notes (Haiku), stories E5.5 and E5.6

Branch: exp/kimi-e56-haiku-direct-review-1. Commits: 564e4d55 (E5.6), c15e4df1 (E5.5), 111f205b (plan).
Nothing pushed. No PR. No live `kimi`, `vibe` or other paid call was run.

## Scenarios

| # | Test | Red seen? | Green now? | Note |
|---|---|---|---|---|
| S5.5.1 | AKimiConsultantCheckTests.AKimiCheck_RunsInACopy_OnAOneShotHome | no (green on arrival) | yes | E5.3 g and E5.4 already give the one-shot home. The table's red ("before E5.4") no longer applies. |
| S5.5.2 | AfterTheCheck_TheHomeIsGone_JunctionFirst (Theory, 2 arms) | no | yes | Recorder on GitScratch.AfterUnlink never fires. |
| S5.5.3 | ACheckCancelledMidTurn_StillDisposesItsHome | no | yes | Cancelled through the check's token. |
| S5.5.4 | ARootlessCheck_KeepsTheHomeInItsScratch_AndDisposesItJunctionFirst | no | yes | |
| S5.6.1 | KimiHasThreeRows_EachCitingItsCells | yes: *Expected none not to be <null>* | yes | Matches the table. |
| S5.6.2 | CapabilityMatrixTests.TheVectorsActuallyLoaded… | existing test | yes | Green after the seed. |
| S5.6.3 | ThePlannerArm_IsACopyForDisk… | yes: *Expected type to be Planned, but found Refused* | yes | Differs from the table (see D2). |
| S5.6.4 | AdmissionFlagsBothRows | yes: *Expected type to be Admitted, but found Refused* | yes | Matches the table in spirit. |
| S5.6.5 | AKimiQuestionLaunch_IsTheShimRequest… | yes: InvalidCast (planned launch was Refused) | yes | Table predicted a WorkingDirectory message. |
| S5.6.6 | APlanWithFlags_IsRefusedForKimi | yes: InvalidCast | yes | Table predicted "no exception". |
| S5.6.7 | AKimiDiskQuestionRowTests (3 facts: answered, refused, cancelled) | written, not watched red | yes | Found 3 test-side bugs on first run; fixed (see D5). |
| S5.6.8 | capabilityAdmission.test.ts "kimi rows admit flagged…" | no (green on arrival) | yes | The table was regenerated before the test ran. |

Red was watched for the C# S5.6 scenarios by reverting the five production files to HEAD (copies kept in scratchpad), building, running, then restoring. Restore verified with git diff stat.

## Results

- Whole MCP suite (final run, after all commits): total 10556, failed 2, succeeded 10470, skipped 84.
  - The 2 failures are the ones the brief lists at the start: `KimiHomeTests.AHomeWhoseDataPathPasses260_IsCheckedAndDisposed` and `KimiRuntimeTests.ALaunchThatNamesItsHome_…`. Not mine.
- Extension tests (`npm --prefix src_vs_code test`): 6290 tests, 6270 pass, 1 fail. The fail is the known `the baseline names no release that was never tagged` (clone has no tags). The S5.6.8 test passes.
- Generator: `generate-runtime-capabilities.mjs` wrote the file; `--check` prints "up to date" (21 rows over 7 runtimes × 3 capabilities).
- plan-lifecycle.mjs: clean.
- dotnet format: `--verify-no-changes --no-restore` exits 2. Its log contains only IL analyzer errors in `src_server/src/*.cs` (AOT / trimming warnings on ASP.NET Map* calls). I did not touch src_server. I did NOT verify these errors are pre-existing at HEAD, so treat this as an open question. No format complaint about any file I changed.

## Deviations (also in the plan, deviations 31–40)

- D1. Kimi rows added to the seed, with cells M-K6, M-K3, M-K4, M-K9, M-K9 (e). none and disk are `unconfined`; web is `unmeasured`.
- D2. At HEAD, `ConfinementPlanner.Plan("kimi", …)` returns Refused (the matrix refuses an unknown runtime). So S5.6.3 and S5.6.5 fail with type/cast messages, not the table's messages.
- D3. Added `RESULTS_kimi_confinement.md` to the citation list in CapabilityMatrixTests (not in the spec). Without it `EveryRow_CitesTheRecord` cannot cite the kimi rows.
- D4. `gemini` refusal regex in capabilityAdmission.test.ts now lists `kimi` (the refusal sentence lists the runtimes).
- D5. Disk-row tests: first run had 3 test-side bugs, all fixed in the test only:
  1. Cancellation throws out of `AskConsultantsAsync`; the test now catches `OperationCanceledException`.
  2. `coai-vh` stays as an empty parent folder; the test checks it holds nothing, not that it is gone.
  3. The prompt names the copy's folder, not the file; the test checks the folder.
- D6. Windows-only skips removed from the disk-row tests. They run on this Linux host through the service stand-in (E5.4 deviation 23).
- D7. KimiConsultant refuses planned flags with an ArgumentException naming the shim.
- D8. `none` is `unconfined` (E5.6 decision 2), not `confined` as Vibe's is.

## What remains

- Not run: the live J2 checks and RESULTS §31 entries (no spending).
- Not run: M-K17 (out of scope).
- Not done: a red watch for S5.6.7 and S5.6.8 (written and passed; the TS red was not watched).
- Open: whether dotnet format's src_server IL errors are pre-existing at HEAD.
- Not done: any push, PR, merge of `main`, or release (out of scope).
