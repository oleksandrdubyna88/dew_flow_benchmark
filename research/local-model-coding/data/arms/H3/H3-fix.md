# H3 — fix of the review (E5.5 and E5.6), 2026-10-26

Branch `exp/kimi-e56-skeleton-haiku-3`. One commit: `9bae38c0` (nothing pushed). Finished about 13:26 UTC, before the 13:43 deadline.

## Findings

| id | severity | result |
|---|---|---|
| R1 | major | FIXED. `research/module_core.md`: new paragraph "Kimi's arm (E5.6, 2026-10-26)" after Vibe's arm (seventh runtime, `none` and `disk` unconfined on M-K6, `disk` in a Snapshot with `SnapshotSources`, no flags, `web` unmeasured and refused naming `RESULTS_kimi_confinement.md`, `KimiConsultant` refuses a plan with flags). `research/module_tests.md`: new section "Kimi's check and question rows — E5.5 and E5.6" with every new test, what it holds and the red or pin (deviations 56–58), and what is NOT covered. I checked each claim against `shared/runtime-capabilities.json` and `KimiConsultant.MustCarryNoPlanFlags`. I wrote one claim too strong at first (that the copy keeps reads inside); I corrected it before the commit: an empty or copied folder confines no read, because Grep and Glob read the disk. |
| R2 | minor | FIXED. `ConfinementPlannerTests.AKimiQuestionLaunch_…` now asserts `KimiEnvironment.Refusal(request, KimiPlaces.ForShim(home, …))` is empty for `none` and `disk`. The review said "the places the seam makes for the home"; `ForShim(home, persons-bin)` is exactly what the test's own `KimiConsultantOf` seam makes. It is a pin (green at once); I did not break the production to see it red. |
| R3 | minor | FIXED. `AKimiConsultantCheckTests.AKimiCheck_RunsInACopy_OnAOneShotHome`: removed `code.Should().Be(0)` (the mode exits 0 for every classified outcome); added `canary == CanaryReadings.NotAttempted`; added the `--cwd` shape: `snapshot-*` in `tree` in `own` in a `coai-check-*` scratch that is a direct child of the temp root. My first version of the shape check was wrong (it missed the `snapshot-*` level); the test run showed it and I corrected it. A pin, no red seen. |
| R4 | minor | FIXED. `capabilityAdmission.test.ts`: the message now says the pair is admitted flagged unconfined because Grep and Glob read the disk directly (M-K6). |

No finding was wrong. No "red first" run was possible for R2 to R4: all four change a test or a doc, and the production they pin is already right. For R2 and R3 I did not do a temporary break of the production to see them red.

## Results

- Build (`-m:4 --disable-build-servers`, through `low-priority.mjs`): 0 warnings, 0 errors.
- Changed classes (`AKimiConsultantCheckTests`, `ConfinementPlannerTests`, `CapabilityMatrixTests`, `AKimiQuestionRowTests`): 82 of 82 pass.
- Whole MCP suite: 10557 total, 10470 passed, 84 skipped, 3 failed:
  - the two known ones: `KimiRuntimeTests.ALaunchThatNamesItsHome_…`, `KimiHomeTests.AHomeWhoseDataPathPasses260_…`;
  - `TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow` — NOT mine. It uses a 1 ms git deadline. It fails again when run alone, so it is host-dependent. This branch changes neither the test nor its code (`git diff b4e2ea96 HEAD` is empty for it). I did not run it on the base commit.
- Format: `dotnet format --verify-no-changes --include` on the two changed C# files: clean. The whole-solution run prints IL2026/IL3050 errors from `src_server` (known, not this change); I did not repeat it.
- Generator: `generate-runtime-capabilities.mjs --check` is up to date (21 rows over 7 runtimes × 3 capabilities).
- TS `npm test` in `src_vs_code`: the only failing test I saw is the known `the baseline names no release that was never tagged`. I did not copy the totals.
- Not run: `plan-lifecycle.mjs` (no plan file changed in this step).

## Not done

- Instruction loading is not fully proven. The resolver selected 299 KB of rules for this scope. I ran `check` and `explain`, and read ENTRY.md, PROJECT.md and `common.knowledge-base` fully. I did NOT read the other rule payloads in full (30 minutes available).
- Out of scope, untouched: `research/` RESULTS files, live checks, merging `main`, push, pull request.
