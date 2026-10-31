# DR4 — fix notes (Haiku): E5.5 and E5.6 review

Branch: `exp/kimi-e56-haiku-direct-review-4`. Base of this pass: `8d194f82`.
No push, no PR, no live `kimi` or `vibe` run.

## Commits

- `981a5437` test(kimi): R1, R4, R5, R6 (tests only, plus `KimiStandIn.PersonFolder` removed).
- `d33c874f` refactor(kimi): R3 (the flag guard is `MustCarryNoFlags`).
- `fd5f6549` docs(kimi): R7, R8, R9 (plan text, `shared/runtime-capabilities.json` cite sentence).

## Per finding

| ID | Sev | Result | Note |
|---|---|---|---|
| R1 | major | fixed | S5.6.7's three arms now assert the copy (`launch.Request.WorkingDirectory`) and the `coai-question-*` scratch are gone after an answer and a cancel. The refused arm asserts no new `coai-question-*` folder. Check: the scratch set is compared with a snapshot taken before the ask, so a concurrent test that makes a `coai-question-*` folder could fail it. |
| R2 | major | partly fixed | Teeth shown for S5.6.6 (flag check removed: *Expected a <System.ArgumentException> to be thrown, but no exception was thrown.*) and for the check's S5.5.2 to S5.5.4 path (check disposes no folder: *Expected unlinks.Seen to be empty … \credentials*). Both restored; both classes green again. **Not done:** S5.5.1, S5.6.2, S5.6.7 (the question row's `DisposeAllUnder` at `QuestionRowLaunch.cs:431` not broken), S5.6.8 (TS). The review's named break `VibeHomes.EmptiedUnderItsLock` does not exist in the source, so I broke the check's own call instead. The as-built section records these. |
| R3 | minor | fixed | `MustCarryNoFlags` is a private static helper; `Build` now has one guard call. |
| R4 | minor | fixed | S5.6.5 asserts `StartWith(RowFolder + separator)` and `^kh-[0-9a-f]{10}$`. |
| R5 | minor | fixed | `KimiStandIn.PersonFolder` is deleted. All its callers use `person.Hash()` (SHA-256 and attributes). |
| R6 | minor | fixed | The check's temp folders are `testsToDelete-kimi-check-data-` and `testsToDelete-kimi-check-temp-`. |
| R7 | minor | fixed | Recorded as deviation 8 in the plan: S5.6.7 is three `Fact`s, not one `Theory`. |
| R8 | minor | fixed | Header sentence added. The generator `--check` is current; the generated TS did not change. |
| R9 | minor | fixed | Deviation 6 now says "after `MustBeLaunchable`, before the request is built". |

## Checks

- Build (`dotnet build dew_flow_connect_other_ais.slnx -c Debug -m:4 --disable-build-servers`, through `low-priority.mjs`): succeeded after each step, 0 warnings, 0 errors.
- Changed classes (`AKimiQuestionRowTests`, `AKimiConsultantCheckTests`, `ConfinementPlannerTests`, `AKimiConsultationScenarioTests`): 55 of 55 passed before the break; 40 of 40 for the two classes after the break was restored.
- Generator: `generate-runtime-capabilities.mjs --check` exit 0, up to date (21 rows, 7 runtimes × 3).
- TS (`npm test`, `src_vs_code`): 6290 tests, 6270 pass, 19 skipped, 1 fail. The fail is `the baseline names no release that was never tagged` (known, no tags here).
- Whole MCP suite: see the end of this file.
- Format: see the end of this file.

## Not done / open

- R2: the rows listed above (S5.5.1, S5.6.2, S5.6.7, S5.6.8).
- The rule files the coder notes said were not read in full. I did not re-read them in this pass; the resolver was not re-run for this brief.
- The homes-root wording (deviation 7 in the plan) needs a decision. Not in scope.
- The unknown `TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow` from the earlier suite was not looked at in this pass.

## Final results (17:38 UTC)

- Whole MCP suite: total 10557, succeeded 10471, skipped 84, failed 2. Both failures are on the known list (`KimiRuntimeTests.ALaunchThatNamesItsHome…`, `KimiHomeTests.AHomeWhoseDataPathPasses260…`). `TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow`, which failed in the earlier run, passed this time.
- Format: `dotnet format dew_flow_connect_other_ais.slnx --verify-no-changes --no-restore` exit 2. All 54 output lines are IL trim diagnostics (IL2026/IL3050) in `src_bugs` and `src_server`, which I did not touch. No line is in `src_mcp`. Not verified clean overall.
- Generator check: exit 0. TS: 6290 tests, 6270 pass, 19 skipped, 1 fail (known no-tags test).
