# S2 t6 — coder notes (2026-10-26, Haiku 5.5)

Commit: 3e613cac (four files by name: todo/PLAN_vibe_and_kimi_runtimes_E5.md, research/module_core.md, research/module_tests.md, research/module_runners.md).

## Done
- Plan: section "E5.5–E5.6 as built" (deviations 50–55) after E5.7 as built; "Verified by" filled in.
- Plan status line: says the code is in and NOT yet green (one red beyond the known three). It does not say "implemented", because the document allows that word only after a green run.
- module_core.md: 21 rows (7 runtimes × 3) in the Mermaid diagram; Kimi paragraph (unconfined none/disk, web unmeasured, M-K6, D15 planner arm, refusal ends `…, vibe, kimi`); the "eighteen since S3" count now says twenty-one since E5.6.
- module_tests.md: a new E5.5–E5.6 section with AKimiConsultantCheckTests and AKimiQuestionRowTests (covered, with what they do not prove), and the moved pins.
- module_runners.md: the planned-launch sentence; the stale "refused until E5.6" sentence corrected.

## Verification (see the plan's "Verified by")
- plan-lifecycle: clean.
- build: succeeded, 0 warnings, 0 errors.
- CoaiMcp.Tests: 10470 pass, 84 skipped, 3 fail. Two are the known Kimi tests. The third, `TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow`, is NOT on the known list and is unexplained. Not fixed (out of scope; reported).
- generate-runtime-capabilities --check: up to date, 21 rows.
- npm test: 6290 tests, 1 fail = the known tagless-checkout case.

## Deviations from the task document
- The named `CoaiMcp.Tests.exe` does not exist on Linux (`ENOENT`); ran `dotnet test --project … --no-build` on the same assembly.
- The document says "Do not commit" under Out of bounds, but the Rules say commit. I committed only my four files. Please check that this matches what you meant.

## Left open
- The `AnUnknownCommit_IsGitFailed_NeverAThrow` red: explain or fix it, then change the plan status line to "implemented".
- Live runs (RESULTS §31) are still owed, as the document says.
