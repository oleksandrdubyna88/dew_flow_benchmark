# D3 — coder notes: E5.5 and E5.6 (Kimi check and Kimi question row)

Branch `exp/kimi-e56-haiku-direct-3`, started at `b4e2ea96`. Deadline 00:36 UTC.

## Scenarios

| # | Test | Red seen? | Green? | Note |
|---|------|-----------|--------|------|
| S5.5.1 | `AKimiConsultantCheckTests.AKimiCheck_RunsInACopy_OnAOneShotHome` | no | yes | green on arrival (E5.3 and E5.4 already in); table's red predates them |
| S5.5.2 | `AKimiConsultantCheckTests.AfterTheCheck_TheHomeIsGone_JunctionFirst` (Theory: passed; exit 70) | no | yes | green on arrival |
| S5.5.3 | `AKimiConsultantCheckTests.ACheckCancelledMidTurn_StillDisposesItsHome` | no | yes | green on arrival |
| S5.5.4 | `AKimiConsultantCheckTests.ARootlessCheck_KeepsTheHomeInItsScratch_AndDisposesItJunctionFirst` | no | yes | green on arrival |
| S5.6.1 | `CapabilityMatrixTests.KimiHasThreeRows_EachCitingItsCells` | not watched | yes | written after the seed |
| S5.6.2 | `CapabilityMatrixTests.TheVectorsActuallyLoaded_AndCoverEveryPairOfTheTable` (existing) | not watched | yes | green once the vectors exist; `EveryRow_CitesTheRecord…` needed the Kimi record (see deviation 49) |
| S5.6.3 | `ConfinementPlannerTests.ThePlannerArm_IsACopyForDisk_AnEmptyFolderForNone_AndRefusesWeb` | teeth: yes | yes | break: disk arm to Scratch → `Expected disk.Cwd to be CwdKind.Snapshot … but found CwdKind.Scratch`; reverted |
| S5.6.4 | `CapabilityMatrixTests.KimiRowsAreAdmittedFlagged_AndWebIsRefused` | not watched | yes | |
| S5.6.5 | `AKimiQuestionRowTests.AKimiQuestionLaunch_IsTheShimRequest_InItsFolderOrItsCopy_OnTheRowsHome` | not watched | yes | builds from a hand-made plan (deviation 51) |
| S5.6.6 | `AKimiQuestionRowTests.APlanWithFlags_IsRefusedForKimi` | teeth: yes | yes | break: `MustBeFlagless` call removed → `no exception was thrown`; reverted |
| S5.6.7 | `AKimiQuestionRowScenarioTests.AKimiDiskRow_ReadsItsCopies_AndLeavesNothingBehind` (Theory ×3) | not watched | yes | the prompt's copy is `…\tree\snapshot-<hash>`; first draft had the asking repo missing for the refused arm (test bug, fixed) |
| S5.6.8 | `capabilityAdmission.test.ts`: `kimi rows admit flagged, web refuses naming its record` | not watched | see below | |

## Deviations (also in the plan, *E5.5–E5.6 as built*, items 47–56)

- Red was not kept first for E5.6 code: the seed, the vectors, the planner arm, the flag refusal and the generated table were written before
  their tests. Teeth were checked afterwards for S5.6.3 and S5.6.6 (each red, each reverted).
- E5.5 is green on arrival, so no teeth were run for it.
- The matrix's record table gained the Kimi record (the one existing test the seed turned red).
- The plan's §5 anchor for M-K6 is §9 in `RESULTS_kimi_confinement.md`. Cited sections were checked against the headings.
- S5.6.7 is in its own class, `AKimiQuestionRowScenarioTests`.

## Not run, on purpose

- The ConnectOtherAIs review gate (`review_code` etc.). The brief says never run anything that spends money, and these reviewers are paid calls.
- Live checks J2 (`--check-consultant` and the question rows against the real `kimi`) and M-K17. Out of scope.
- `research/module_*.md`, `research/RESULTS_*.md`, merge of `main`, push, PR, release.

## Environment

- Linux (WSL2). Kimi and Vibe tests skip on non-Windows; the service stand-in (`WindowsStandIn`) runs them on this host.
- Known reds at start, not mine: `KimiRuntimeTests.ALaunchThatNamesItsHome_…` and `KimiHomeTests.AHomeWhoseDataPathPasses260_…`; extension test `the baseline names no release that was never tagged`.

## Format

- `dotnet format … --verify-no-changes --no-restore`: no finding under `src_mcp`. Exit 2 comes from AOT analyzer output in `src_server` and `src_bugs`, which this work does not touch.

## Results

- Full MCP suite, run 1: total 10556, failed 3, skipped 84. Failures: the two known Linux reds (`KimiRuntimeTests.ALaunchThatNamesItsHome…`, `KimiHomeTests.AHomeWhoseDataPathPasses260…`) and `SecuritySourcesCharacterizationTests.The_collection_deadline_ends_the_file_it_interrupts_and_every_later_one` (30 s). That last one passes alone (7/7) and does not touch this work: a load-timing flake, not yet proven.
- Full MCP suite, run 2: total 10556, failed 5, skipped 84. Known two, plus `QuestionConsultScenarioTests` (codex release ×2, cached tokens ×1). That class passes alone 6/6.
- Full MCP suite, run 3: total 10556, failed 3, skipped 84. Known two, plus `TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow`. It failed alone too (1 of 10): a 1 ms git deadline gave `recorded` where `unavailable` was expected. A race, in code this work does not touch. It passed in runs 1 and 2.
- So three different tests failed across three runs, each passing alone or in other runs: timing-sensitive tests under full-suite load. Not proven at baseline; no baseline run was made.
- Extension: typecheck clean; lint clean; `npm test` total 6290, pass 6270, fail 1, skipped 19. The one fail is the known `the baseline names no release that was never tagged`. S5.6.8 passes. `generate-runtime-capabilities.mjs --check` is up to date (21 rows, 7 runtimes).
- Plan lifecycle: clean.

## Remaining

- Opus review of this work (the plan says the review is by Opus; not done here).
- Live checks J2 and M-K17 (out of scope).
