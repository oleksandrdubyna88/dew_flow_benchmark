# Q1 — fixes after the review (CODER, Sonnet)

Branch `exp/kimi-e56-skeleton-1`. Never pushed. No `kimi` or `vibe` was run. Review verdict: fix-then-ready; 0 blocking, 0 major, 5 minor.
Commits: `d53663ee` (data), `e584e302` (tests, plan), and one for the generated TS file.

## Findings

| Id | Result | What |
|---|---|---|
| R1 | **fixed** | Checked: RESULTS has §6 M-K3, §7 M-K4, §9 M-K6, §12 M-K9, §21 M-K9 (e); there is no `§M-K6`. The `none` and `disk` rows now cite `§9 M-K6`, `§6 M-K3`, `§7 M-K4`, `§12 M-K9`, `§21 M-K9 (e)`. Generator run, TS file regenerated. Plan deviation 49 corrected (RESULTS was only read). |
| R2 | **not fixed** (gap recorded) | The finding is right: on the refused arm `played.Shims` is empty, so the scratch loop does not run. No cheap way to see the scratch there: `PanelService` has no seam for `QuestionRowLaunch.MakeScratch` (internal init property), and a sweep of the machine's temp folder was ruled out (deviation 57). Recorded as plan deviation 59, and the test's summary now says it. The `finally` on a copy refusal is pinned at `QuestionRowLaunch` level by `AVibeQuestionRowReadsACopyOfItsRootsTests.ARowRefusedAtItsCopy_GivesItsOneShotFolderBack`. |
| R3 | **fixed** | The web row's note now ends `(kimi 2.1.1)`. Generator run. |
| R4 | **fixed** | New test `ConfinementPlannerTests.APlanWithLeadingArguments_IsRefusedForKimi`. Red shown by mutation: without `\|\| planned.Plan.Leading.Count > 0` it fails with *Expected a ArgumentException to be thrown, but no exception was thrown*. Mutation reverted (`git diff` of `src_mcp/runners` is empty). Plan deviation 60. |
| R5 | **fixed** | `KimiStandIn.Of(person, params IDisposable[] more)`; `AKimiConsultationScenarioTests.StandIn` now is `KimiStandIn.Of(person, CallingAs(...))`; its private `Scopes` copy is deleted. The caller scope is now created before the other three (argument order), and disposed last; the scopes are independent async-local values, and the E5.4 scenarios stay green. Plan deviations 50 and 58 corrected. |

No finding was wrong.

## Results

- Build (`dotnet build dew_flow_connect_other_ais.slnx -c Debug -m:4 --disable-build-servers`): succeeded, 0 warnings, 0 errors.
- Changed classes (`ConfinementPlannerTests`, `AKimiQuestionRowTests`, `AKimiConsultationScenarioTests`, `CapabilityMatrixTests`, `AKimiConsultantCheckTests`): 94 of 94 pass.
- Whole MCP suite, once: 10557 total, 10470 passed, 84 skipped, 3 failed — the two known ones (`KimiRuntimeTests.ALaunchThatNamesItsHome_…`, `KimiHomeTests.AHomeWhoseDataPathPasses260_…`) and `TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow` (a one-millisecond git timeout; it failed in a full run and in one of two isolated runs, and passed in the other; not related to this change, same as in Q1-code.md).
- Format: `dotnet format --verify-no-changes --no-restore` shows no whitespace/style diff (only the IL2026/IL3050 AOT messages for `src_server`, untouched code).
- Generator: `generate-runtime-capabilities.mjs` (it lives in `src_vs_code/scripts/`) wrote the file; `--check` says up to date (21 rows, 7 runtimes × 3).
- TS tests (`npm test` in `src_vs_code`): 6290 tests, 6270 pass, 1 fail — the known `the baseline names no release that was never tagged` (no git tags in this clone). The last `npm test` run was not through `low-priority.mjs` (the first one was).

## Not done

- I read the rules `common.reuse-first` and `common.test-leftovers` only in part, and not every rule the resolver selected (31 rules, 283 KB) — the deadline was close. The fixes follow the rules named in the review (reuse first, a red test first, deviations recorded).
