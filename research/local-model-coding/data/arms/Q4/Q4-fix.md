# Q4 — fixes after the review of E5.5 and E5.6 (CODER, Sonnet)

Branch `exp/kimi-e56-skeleton-4`. Started 11:44 UTC. Deadline 12:14 UTC.
Commits: `7fe3d505` (R1, R2, R3 and the plan), `3d60ea0f` (R4, R5). Nothing pushed.

All five findings are minor. I checked each one against the code and the record first. None is wrong.

| Id | Result | What I did |
|---|---|---|
| R1 | **fixed** | Checked: RESULTS header and §3, §16 say 2026-10-13 for the M-K cells the rows cite; 2026-10-16 is M-K16 (§29), and no row cites it. Red first: I changed the `Records` tuple in `CapabilityMatrixTests.cs` to `2026-10-13`; `EveryRow_CitesTheRecord_AndACellOrSaysWhyThereIsNone` failed on the rows' date (line 174). Then I changed the three rows (`measuredWith.date`) and regenerated `runtimeCapabilities.generated.ts`. The test is green. Deviation 47 and 55 in the plan are corrected. |
| R2 | **fixed** | Checked: §25 (RESULTS line 561) does not name `WebSearch` or `FetchURL`; §7 (lines 226-231) lists them as announced, and §14 (line 360) names them. I cited "§7, §14" in the `none` and `disk` notes and "§7 and §14" in the `web` resultRef. TS table regenerated. Deviation 47 corrected. No test for this: a test would have to parse RESULTS section numbers; I did not add one. |
| R3 | **fixed** | One clause added to the `why` header: the kimi rows cite the M-K cells of `research/RESULTS_kimi_confinement.md` (Windows, 3 of 3, kimi 2.1.1). The generator does not copy the header (the generated file differs only in the dates and notes). |
| R4 | **fixed** | In `AKimiQuestionRowTests.cs` the "copy is gone" assertion now runs for `answered` and `cancelled`; the prompt and `--session` assertions stay on `answered` only. `refused` still checks that nothing was launched. The strengthened assertion was green at once (the cancelled arm already cleaned its copy), so I have no red for it. I did not run a teeth check (removing the cleanup) because of time. |
| R5 | **fixed** | `Build` now calls `MustCarryNoFlag(launch)` with no outer guard (its own pattern lets a consult launch through); braces on its `if`. Behaviour is the same; no new test, the S5.6.6 test covers the guard and it is green. |

## Results

- Build (`-m:4 --disable-build-servers`, lowered): 0 warnings, 0 errors.
- Changed classes (`CapabilityMatrixTests`, `AKimiQuestionRowTests`, `ConfinementPlannerTests`, `KimiConsultant*`): 92 of 92 pass.
- Whole MCP suite: see the end of this file.
- Format: `dotnet format --verify-no-changes --no-restore --include` the three changed `.cs` files: no output (no diff). The whole-solution run was not repeated; it fails on the AOT analyzer noise in `src_bugs` and `src_server`, as the coder's notes say.
- Generator: `generate-runtime-capabilities.mjs --check` — up to date (21 rows, 7 runtimes × 3).
- TS tests: 6290 tests, 6270 pass, 1 fail = the known `the baseline names no release that was never tagged`. Same counts as before the fixes.

## Whole MCP suite (once, 7m20s)

10554 total, 10468 pass, 84 skipped, **2 failed** — exactly the two known ones:
`KimiRuntimeTests.ALaunchThatNamesItsHome_GoesThroughTheLaunchControl_WhichReadsThePersonsSignIn` and
`KimiHomeTests.AHomeWhoseDataPathPasses260_IsCheckedAndDisposed`.
`TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow`, which failed in the coder's run, passed this time: it is a timing test (1 ms deadline), not caused by this diff.

Nothing remains. Finished before the deadline; working tree clean after the two commits.
