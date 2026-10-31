# CH3 — fix notes (Haiku) — review of E5.5 and E5.6

Branch `exp/kimi-e56-classic-haiku-review-haiku-fix-3`. Commits made by this fix round:

- `3a599312` test(kimi): the two scenario names match the table, and the kimi notes name the version (R2, R4)
- `55aa9cd3` docs(plan): E5.5–E5.6 review fixes — renames, refused-arm gap, coder question (R2, R3, R5)

No push, no PR. `kimi` and `vibe` were not run. `research/` was not touched.

## Per finding

| ID | Severity | Result | Why |
|---|---|---|---|
| R1 | major | **not fixed (out of scope)** | The finding is real: `research/module_tests.md` has no E5.5–E5.6 section, `module_core.md` has no Kimi planner arm, and `module_server.md` has no check or row path. But the task puts `research/` out of scope. Needs a follow-up task. |
| R2 | minor | **fixed** (`3a599312`) | `ConfinementPlannerTests` method renamed to `ThePlannerArm_IsACopyForDisk_AnEmptyFolderForNone_AndRefusesWeb` (S5.6.3). `CapabilityMatrixTests` method renamed to `AdmissionFlagsBothRows` (S5.6.4). Deviation 50 records the renames. |
| R3 | minor | **not fixed** (gap recorded in deviation 56, `55aa9cd3`) | The finding is right: in the refused arm, `TheShimWasToldTheCopiesNotTheRoot` returns early, so the row's `coai-question-*` scratch is never checked. The fix needs `QuestionRowLaunch.MakeScratch` observable from the test. `QuestionFanOut` builds that launch and `PanelService` cannot inject it. Adding a seam is more than a cheap change, so I recorded the gap. |
| R4 | minor | **fixed** (`3a599312`) | The three Kimi notes in `shared/runtime-capabilities.json` now start with "measured on kimi 2.1.1". The web note says the row itself is not measured. `src_vs_code/src/runtimeCapabilities.generated.ts` was regenerated; `--check` says up to date (21 rows, 7 runtimes). |
| R5 | minor | **not fixed; open for the operator** (deviation 60, `55aa9cd3`) | I cannot tell who allowed Sonnet as the coder for E5.5–E5.6 from the files I read. The plan's §0.2 names Opus. I did not invent an approval. Deviation 60 says so and asks the operator to confirm Sonnet or have Opus re-check the two stories. |

Other edits in `55aa9cd3`: the status line now says "deviations 50–60".

## Checks

- **Build**: `dotnet build dew_flow_connect_other_ais.slnx -c Debug -m:4 --disable-build-servers` — exit 0. Log: `/home/jinx/git/coai-exp/notes/CH3/build.log`.
- **Changed test classes**: `ConfinementPlannerTests`, `CapabilityMatrixTests`, `AKimiQuestionRowTests`, `AKimiConsultantCheckTests` — 83 total, 83 passed, 0 failed, 0 skipped.
- **Whole MCP suite** (once, after all edits): 10 557 total, **20 failed**, 10 453 passed, 84 skipped. Log: `/home/jinx/git/coai-exp/notes/CH3/suite.log`.
  - The 2 known: `KimiRuntimeTests.ALaunchThatNamesItsHome…`, `KimiHomeTests.AHomeWhoseDataPathPasses260…`.
  - The 17 rounds-database tests from the earlier run (`ACodeRoundRecordsItsSizeTests` ×7, `EndToEndTests` ×3, `ARoundsNumberComesFromTheJournalTests` ×3, `ARoundIsMintedAKeyTests` ×2, `ThePersonsCommandsReachTheCallerTests` ×1, `SplitOrderTests` ×1). Not from this round; not fixed.
  - **1 new failure this run, not in the earlier run:** `QuestionRowLookupScenarioTests.ALookupOutsideTheRoot_IsRefusedInTheNextTurn_AndNothingOutsideIsRead` — `Sequence contains more than one matching element` in `TurnOf`. **Run alone it passes (6 of 6).** Cause: five test classes (`AskHumanScenarioTests`, `AVibeReviewerRunsThroughAFakeVibeTests`, `QuestionRowLookupScenarioTests`, `QuestionConsultScenarioTests`, `QuestionRowOnAgyScenarioTests`) share one fixed steering file, `Path.GetTempPath()/fakecli-minimal.json`, and run in parallel. This is a pre-existing race. No line of this round's diff touches it. Not fixed here; it needs a per-test steering path.
- **Format**: `dotnet format whitespace` and `dotnet format style --verify-no-changes` over the two changed C# test files — exit 0 each. The whole-solution format check was not rerun. It failed on this host before this round (IL2026/IL3050 in `src_server` and `src_bugs`, not touched here).
- **Generator**: `src_vs_code/scripts/generate-runtime-capabilities.mjs --check` — up to date.
- **TS tests** (`npm test` in `src_vs_code`): 6270 pass, 1 fail, 19 skipped (6290 total). The one fail is `the baseline names no release that was never tagged`, the known tag issue on this clone. `kimi rows admit flagged, web refuses naming its record` passes.
- **Known failures**: `KimiRuntimeTests.ALaunchThatNamesItsHome_…` and `KimiHomeTests.AHomeWhoseDataPathPasses260_…` — not rerun in isolation; they are in the suite run.
- **Plan lifecycle**: `plan-lifecycle.mjs` — clean.

## Not done (for the next person)

1. R1: the `research/` docs (module_tests, module_core, module_server). Out of scope for this round.
2. R3: a seam for the row's scratch, so the refused arm can check it.
3. R5: the operator's decision on the coder of E5.5–E5.6 (deviation 60).
4. The Windows run of the Kimi tests is still owed (deviation 57). The 17 rounds-database failures in the suite are a separate question, as in deviation 59.
