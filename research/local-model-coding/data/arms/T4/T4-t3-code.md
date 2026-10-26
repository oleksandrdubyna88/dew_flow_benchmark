# T4-t3 — code notes (E5.5 consultant check for Kimi)

Commit: db2baa45 on branch tmp/T4-t3 (worktree /home/jinx/git/coai-exp/wt/T4-t3). Only file added: src_mcp/tests/AKimiConsultantCheckTests.cs. No production file changed; PlayedKimi.cs untouched.

## Implemented
- AKimiConsultantCheckTests with the four scenarios: S5.5.1 (fact), S5.5.2 ([Theory], answered true/false), S5.5.3 (cancel mid-turn), S5.5.4 (rootless). 5 test cases. No Assert.SkipUnless.

## Results
- Class `CoaiMcp.Tests.AKimiConsultantCheckTests`: 5/5 pass on Linux, on arrival (as the document predicted).
- Whole MCP suite: total 10539, failed 4, succeeded 10451, skipped 84.
  - KimiRuntimeTests.ALaunchThatNamesItsHome_GoesThroughTheLaunchControl_WhichReadsThePersonsSignIn — documented Linux failure.
  - KimiHomeTests.AHomeWhoseDataPathPasses260_IsCheckedAndDisposed — documented Linux failure.
  - TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow — NOT in the document. Not checked against baseline (no time). Not in my file.
  - AVibeReviewerRunsThroughAFakeVibeTests.WhatARunLeavesInItsHome_IsRefusedBeforeTheRepairStarts — NOT in the document. Not checked against baseline (no time). Not in my file.
- `dotnet format --verify-no-changes`: exit 2. Errors are IL2026/IL3050 trimming-analyzer errors in src_bugs/src/Program.cs, AdminApi.cs and src_server/src/*.cs. None in my file. Not checked against baseline.

## Teeth breaks (each applied, seen red, reverted with git checkout; tree clean after each)
- Break 1 (ConsultantCheck.cs PlaceAsync: `turn.Runtime.NeedsASanitisedSnapshot` -> `false`). DIFFERENT from the document. Check ends `failed`, failureKind `record-failed`, failureWhat "...a Kimi consultant stands in the conversation's sanitised snapshot (Workdir), and none was made — the checkout is never its working folder (D6) (Parameter 'launch')". No shim launched, so the `--cwd` assertion is never reached. S5.5.3 then timed out waiting for the shim to start (60 s). Red does exist, but the document's expected message is not what we got.
- Break 2 (VibeHomes.cs:223 DisposeAllUnder guarded by `if (folder.Length < 0)`; `if (false)` does not compile, unreachable-code error CS0162 is treated as an error): S5.5.2 both arms and S5.5.3 red. Message: "Expected _unlinkedByTheDeleter to be empty because KimiHome unlinked the junction first, so the deleter never met it, but found at least one item {…/coai-vh/<8>/kh-dd693ddaf6/home/credentials}." (S5.5.3: "...because junction-first on a cancel too, but found ..."). Matches the document.
- Break 3 (ConsultantCheck.cs:232 held.Home.Dispose() removed): S5.5.2 both arms and S5.5.3 red. Message: "Expected Directory.Exists(Path.GetDirectoryName(home)) to be False because coai-vh/<8> goes when the check ends, but found True." Matches the document.
- Break 4 (ConsultCheckScratch.cs:131 iterate only root): S5.5.4 red. Message: "Expected _unlinkedByTheDeleter to be empty because ConsultCheckScratch.Delete disposed <root>/own junction-first (E5.3 g, R6), but found at least one item {…/coai-tmp…/own/kh-dd693ddaf6/home/credentials}." Matches the document.

## Notes for the reader
- Break 1 red message differs from the document (see above). Worth a look by whoever owns T4 notes: the document's expected `--cwd` red is not reachable with this break, because the production code refuses before launch.
- Revert via `git checkout --` on each production file. Between the Edit and the revert, the harness reported "changed on disk" for production files; these were my own edits and reverts, and `git status` showed only the new test file after each revert.
- Commands: `dotnet build` with `-m:4 --disable-build-servers` through low-priority.mjs; test runs with `./src_mcp/tests/bin/Debug/net10.0/CoaiMcp.Tests --filter-class ...` (no .exe on Linux).

## Left
- Baseline check of the two undocumented suite failures (TheRoundsNumstatsAreCountedTests, AVibeReviewerRunsThroughAFakeVibeTests) — not done.
- `dotnet format --verify-no-changes` clean — not achieved on this host (analyzer errors in other projects; not checked against baseline).
