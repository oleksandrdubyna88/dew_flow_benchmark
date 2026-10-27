# O2 fix notes — review of E5.5 and E5.6 (branch exp/kimi-e56-opus-split-haiku-2)

Review: notes/O2-review.json. The coder notes named in the brief (notes/O2-code.md) do not exist; the coder's notes are notes/O2-t1-code.md to O2-t5-code.md.

## Findings

- R1 (major) — FIXED. Plan status line said `implemented`, which plan-lifecycle reads as promoted. Now `in progress`. Commit 245aa60e. plan-lifecycle: clean.
- R2 (major) — FIXED. Whitespace error at AKimiQuestionRowTests.cs:145 in the During lambda. Ran `dotnet format --include` on the touched C# files; the file was rewritten. Commit 9af53dc2 (test).
- R3 (minor) — FIXED. Shared caveat text "a shell is always there" is false for Kimi. Replaced in C# (CapabilityMatrix.cs) and TS (capabilityAdmission.ts) with "nothing coai sets confines what it reads". The TS test still matches /can read this machine/. Commit 94af0197.
- R4 (minor) — FIXED. MustBeTheShimsConfinement: the three-way test moved into predicate CarriesArgv (no branch operators). Commit bc3b196c.
- R5 (minor) — FIXED. S5.6.6 is now a Theory with three arms: Flags, Leading, AddDirs. Each must throw ArgumentException naming the shim. Commit 9af53dc2.
- R6 (minor) — FIXED. (a) answered arm asserts the copy has no AGENTS.md and no .kimi-code; (b) cancelled arm asserts the started shim's WorkingDirectory parent is gone; (c) refused arm asserts no new coai-question-* folder in the temp root. Commit 9af53dc2.
  Caveat: (c) compares the temp root before and after, so a parallel test that makes a coai-question-* folder could in theory flake it.
- R7 (minor) — FIXED. _data and _temp in AKimiConsultantCheckTests use TempDir.For with testsToDelete- prefixes, disposed in Dispose. Commit 9af53dc2.

No finding was wrong. None was left unfixed.

## Teeth checks

Test-only fixes (R5, R6, R7) were not run red first against a mutated product. Not done for lack of time before the 23:14 UTC deadline. The R4 refactor is behaviour-neutral and is covered by the S5.6.6 Theory.

## Results

See the final report below (filled in at the end of the run).

### Results (run 2026-10-26, about 22:45–22:55 UTC)

- Build (Debug, -m:4 --disable-build-servers): succeeded, 0 warnings, 0 errors.
- Changed test classes (AKimiQuestionRowTests, AKimiConsultantCheckTests, ConfinementPlannerTests): 44 of 44 passed.
- Whole MCP suite, once (notes/O2-fix-suite.log): total 10557, failed 3, succeeded 10470, skipped 84.
  - KimiRuntimeTests.ALaunchThatNamesItsHome_GoesThroughTheLaunchControl_WhichReadsThePersonsSignIn — known, host (brief).
  - KimiHomeTests.AHomeWhoseDataPathPasses260_IsCheckedAndDisposed — known, host (brief).
  - TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow — NOT in the brief's list. It also fails when its class runs alone, and it appears in earlier logs (notes/D2-fix2-suite.log, C2-full-suite.txt, H1-fix-suite.txt, O1-fix-suite.txt, T2-fix-suite.txt), so it predates this round. Not investigated further; I did not change any file it touches.
- Format (`dotnet format --verify-no-changes`, notes/O2-fix-format.log): exit 2 with 54 IL AOT-analyzer errors, all in src_server/src/SessionEndpoints.cs (CoaiServer.csproj). The same 54 errors appear in notes/C2-fix-format.txt. No whitespace or style finding in any file this round touched.
- Generator check (`node scripts/generate-runtime-capabilities.mjs --check` in src_vs_code): up to date, exit 0.
- TS tests (`npm test` in src_vs_code, notes/O2-fix-ts.log): 6290 tests, 6270 passed, 1 failed — "the baseline names no release that was never tagged" (the known tag failure from the brief).
- plan-lifecycle.mjs: clean.
