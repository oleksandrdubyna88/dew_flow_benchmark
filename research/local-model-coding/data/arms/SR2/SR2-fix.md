# SR2-fix — applying the review of E5.5 and E5.6 (branch exp/kimi-e56-sonnet-split-haiku-review-2)

Input: notes/SR2-review.json (verdict ready; 0 blocking, 0 major, 5 minor). The coder's notes are SR2-t1..t5-code.md
(there is no SR2-code.md). Every finding was checked against the code before any change.

## Per finding

- R1 (minor) — FIXED (commit a79969b3). `AKimiConsultantCheckTests.AKimiCheck_RunsInACopy_OnAOneShotHome`: the `--home` check now
  asserts the home's grand-parent is `<homes>/coai-vh` and that the name starts `kh-`. The `--cwd` check now asserts the path,
  relative to the check's temp folder, starts `coai-check-…` and then has `own`, `tree`. First try was wrong (the disk arm's cwd
  ends in `snapshot-…`, so `tree` is not the last segment); corrected, then the class was rerun green.
- R2 (minor) — FIXED (commit d32a9f85). Deviation 56 in the E5 plan now records the rename of S5.6.4 from `AdmissionFlagsBothRows`
  to `AdmissionFlagsBothKimiRows_AndRefusesItsWeb_NamingItsRecord`. The module_tests.md row already names the new test.
- R3 (minor) — FIXED by saying it in the test (commit a79969b3). `AKimiQuestionRowTests` now has a comment: only the two arms that
  reserve a home can show the recorder; the refused arm has no junction. No new teeth run was done in this task. The earlier
  teeth evidence for this (review H2 in SR2-hreview) is in the previous fix log, not re-run here.
- R4 (minor) — NOT FIXED. The `Scopes` class and `StandIn()` are copied across three test classes. A shared helper would
  change the existing `AKimiConsultationScenarioTests.cs`, which the brief keeps out of this fix. Left for the owner, as the
  SR2-hreview H4 note said.
- R5 (minor) — FIXED (commit a79969b3). The `kimi` entry of `ConfinementPlannerTests.Adapters()` now calls `KimiForRows()`,
  so the two seams cannot drift apart.

## Checks (after the fixes)

- Build (Debug, -m:4 --disable-build-servers): succeeded, 0 warnings, 0 errors.
- Changed classes (AKimiConsultantCheckTests, AKimiQuestionRowTests, ConfinementPlannerTests, CapabilityMatrixTests): 84 total, 84 passed, 0 failed.
- Whole MCP suite (once, after the fixes): 10558 total, 10472 pass, 2 fail, 84 skipped. The two failures are the known ones below. Log: SR2-fix-suite.txt.
- Format (scoped to the three changed test files, dotnet format --verify-no-changes): no output, no file flagged.
- Generator check (src_vs_code/scripts/generate-runtime-capabilities.mjs --check): up to date, exit 0.
- TS tests (npm test in src_vs_code): 6290 tests, 6270 pass, 1 fail, 19 skipped. The one failure is
  "the baseline names no release that was never tagged" (no git tags in this clone). Log: SR2-fix-ts.log.

## Known failures not caused by this change

- KimiRuntimeTests.ALaunchThatNamesItsHome_GoesThroughTheLaunchControl_WhichReadsThePersonsSignIn
- KimiHomeTests.AHomeWhoseDataPathPasses260_IsCheckedAndDisposed
- TS: the baseline names no release that was never tagged
