# SR3-t4 — code notes (E5.5, S5.5.1–S5.5.4)

## Done
- Added `src_mcp/tests/AKimiConsultantCheckTests.cs` (commit 8d2148fc): five test cases
  (S5.5.1 one; S5.5.2 theory with two arms; S5.5.3; S5.5.4). All green on arrival.
- No production change. Production files match HEAD.
- `ConsultantCheckTests` (32 total, 2 skipped on Linux by design): green.
- Build: succeeded, 0 errors, 0 warnings.

## Teeth (each reverted; each red message recorded)
(a) `held.Home.Dispose();` removed from `ConsultantCheck.cs` finally.
    Red on S5.5.2 (both arms) and S5.5.3:
    "Expected Directory.EnumerateFileSystemEntries(...coai-vh) to be empty ... but found ... coai-vh\<8>".
    Matches the document.
(b) `PlaceAsync` made to always return `ConsultantPlace.Checkout` (ternary forced to `false`).
    Red on S5.5.1, S5.5.3, S5.5.4 — NOT the message the document predicts.
    The check does not run in the repository; production refuses instead:
    state `failed`, failureKind `record-failed`, failureWhat "... a Kimi consultant stands in the
    conversation's sanitised snapshot (Workdir), and none was made ... (Parameter 'launch')".
    The guard in `LaunchAsync`/`Build` turns the missing copy into a failure, so the red is a
    failed check, not a `--cwd` mismatch.
(c) `ConsultCheckScratch.Delete`: `own` dropped from the disposal list (`new[] { root }`).
    Red on S5.5.4 — NOT the "scratch not empty" message the document implies:
    "Expected recorded to be empty ... found .../coai-check-*/own/kh-*/home/credentials".
    The generic deleter (`GitScratch.DeleteEvenIfReadOnly`) met the junction inside `own` first.

## Full MCP suite
10,539 total, 84 skipped, 3 failed:
- `KimiRuntimeTests.ALaunchThatNamesItsHome_...` — known, not fixed.
- `KimiHomeTests.AHomeWhoseDataPathPasses260_...` — known, not fixed.
- `TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow` — NOT on the known list.
  Fails on its own too (1 of 10). Assertion: `AllChangesAsync(_repo, unknown, head).Failed` expected true, got false.
  My change touches no numstat or git-diff code. I did NOT prove it fails on HEAD without my file
  (a move of the file in and out of the tree was refused by the shell, so I stopped there).
  Needs a check by someone who owns the numstat code (possibly a git version on this host).

## Format
- Solution-wide `dotnet format --verify-no-changes`: exit 2, 54 IL-analyzer errors, all in
  `src_bugs/src/CoaiBugs.csproj` (16) and `src_server/src/CoaiServer.csproj` (38). None in MCP.
  Not my files; not fixed.
- Targeted `dotnet format src_mcp/tests/CoaiMcp.Tests.csproj --verify-no-changes --include` on my
  test file and the two production files: exit 0.

## Left / not done
- Numstat test failure above (needs an owner's look).
- Solution-wide format errors in CoaiBugs and CoaiServer (outside this task).
- No pushes. No notes committed (notes live outside the repo).
