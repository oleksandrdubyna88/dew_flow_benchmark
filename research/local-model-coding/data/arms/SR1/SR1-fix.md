# SR1 fix notes — E5.5 / E5.6 review

Branch: exp/kimi-e56-sonnet-split-haiku-review-1
Input: /home/jinx/git/coai-exp/notes/SR1-review.json (4 findings, all minor; no blocking or major)
Note: /home/jinx/git/coai-exp/notes/SR1-code.md does not exist, so there was no coder note to read.

## Per finding

- R1 (minor) — FIXED. Added "kimi 2.1.1 - " to the three kimi `note` strings in shared/runtime-capabilities.json. Ran src_vs_code/scripts/generate-runtime-capabilities.mjs; a re-run leaves no diff. Commit 4376e1f3.
- R2 (minor) — PARTLY FIXED. NothingIsLeftBehind now requires coai-vh\<8> to exist (the reservation is made before the copy, QuestionRowLaunch.cs:101), and then requires it to be empty. NOT FIXED: the refused-before-launch arm does not check the row's scratch. The scratch path is set inside the try, after the reservation, and the test has no seam to read it; this needs a seam or a log read. Also, no red run was done to prove the stronger assertion fails without the fix. Commit d8f97678.
- R3 (minor) — FIXED. The header now says: kimi 2.1.1, 3 of 3 per arm, and the .mcp.json arm of M-K9 (e) 1 of 1 (checked against research/RESULTS_kimi_confinement.md §21). Commit 4376e1f3.
- R4 (minor) — FIXED. Removed the duplicate kimi None NotBeNull assertion in KimiHasThreeRows_EachCitingItsCells. Commit 2892dfcc. A whitespace slip in that edit was caught by the format check and fixed in a separate style commit.

No finding was wrong.

## Results (after the final commit)

- Build (Debug, -m:4 --disable-build-servers): succeeded, 0 warnings, 0 errors. Log: notes/SR1-fix-build.log
- Changed test classes (CapabilityMatrixTests, AKimiQuestionRowTests): 50 of 50 passed, after the final rebuild. Log: notes/SR1-fix-changed.log
- Whole MCP suite (run before the last whitespace-only commit): 10561 total, 10474 passed, 84 skipped, 3 failed:
  - KimiRuntimeTests.ALaunchThatNamesItsHome_GoesThroughTheLaunchControl_WhichReadsThePersonsSignIn — known host failure.
  - KimiHomeTests.AHomeWhoseDataPathPasses260_IsCheckedAndDisposed — known host failure.
  - TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow — NOT on the known list. It fails on every run here, also when run alone. It is in a file I did not change, and its check at TheRoundsNumstatsAreCountedTests.cs:261 uses a 1 ms deadline. NOT verified on base b4e2ea96. Log: notes/SR1-fix-suite.log
- The extension test "the baseline names no release that was never tagged" is not in the MCP suite. npm test in src_vs_code: 3 of 3 passed. Log: notes/SR1-fix-ts.log
- Format (dotnet format whitespace --verify-no-changes on the two changed test files): clean after one fix. Log: notes/SR1-fix-format.log
- Generator check (generate-runtime-capabilities.mjs): re-run, no diff.

## Not done

- The full suite was not re-run after the final whitespace-only commit; only the two changed classes were.
- The format check covers only the two changed test files, not the whole solution.
- No live checks, no merge, no push, no PR, no `kimi` or `vibe` run; research/ not touched.
