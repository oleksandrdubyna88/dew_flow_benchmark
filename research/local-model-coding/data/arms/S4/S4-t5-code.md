# S4-t5 — the consultant check on Kimi: code notes

Worktree: /home/jinx/git/coai-exp/wt/S4-t5 (branch tmp/S4-t5). File added: src_mcp/tests/AKimiConsultantCheckTests.cs (the only file changed).

## What was done
- Added class AKimiConsultantCheckTests with the four tests S5.5.1–S5.5.4, names exact.
- Production code not touched (it was already in place at b4e2ea96).

## Results
- Class run (test host, --filter-class): 4 of 4 green on arrival, as the document predicted.
- Teeth breaks (test side only, each reverted):
  - S5.5.1: "own" changed to "mine" in the CwdOf assertion. Red: `Expected CwdOf(shim) "…/own/tree/snapshot-…" to contain "mine/tree" …`
    (the document shows `mine\tree`; this host uses `/`).
  - Deleter recorder: `_unlinkedByTheDeleter.Add("x")` at the top of TheHomeIsGone. Red with "the deleter's unlink recorder must not be invoked…" in AfterTheCheck_TheHomeIsGone_JunctionFirst, ARootlessCheck_… and ACheckCancelledMidTurn_…. S5.5.1 does not call TheHomeIsGone, so it stays green.
  - Person's folder: `File.WriteAllText(Path.Combine(_person.PersonsHome, "x"), "x")` in TheHomeIsGone before the Hash assertion. Red on the Hash assertion in the same three tests.
  - S5.5.3 leftover: `Directory.CreateDirectory(…coai-vh\leftover)` before the check. Red: `Expected Directory.EnumerateFileSystemEntries(…coai-vh) to be empty, but found …/coai-vh/leftover`.
- Full suite (CoaiMcp.Tests host, no filter): 10538 total, 2 failed, 10452 passed, 84 skipped. The two failures are the known ones:
  KimiRuntimeTests.ALaunchThatNamesItsHome_GoesThroughTheLaunchControl_WhichReadsThePersonsSignIn and
  KimiHomeTests.AHomeWhoseDataPathPasses260_IsCheckedAndDisposed.
- dotnet format: the whole-solution `--verify-no-changes` exits with AOT analyzer errors (IL2026/IL3050) in src_server/src/SessionEndpoints.cs and src_bugs/src/Program.cs. None are in the new file. With `--include src_mcp/tests/AKimiConsultantCheckTests.cs` the check is clean.

## Deviations from the document (and why)
- HomeOf / CwdOf take a ProcessRequest, not a PlayedKimi.Launch: the Turn and During seams hand over the request.
- S5.5.4 reads the home from `played.Shims` after the check, not during the shim's life. The scratch is gone after the check, but the launch record keeps the request, and the test asserts only the parent folder name and file name. No During seam was needed.
- S5.5.3: the check returned state "failed" with failureKind "cancelled"; no OperationCanceledException escaped, so the plain assertion form was kept.
- The person's-folder teeth write sits inside TheHomeIsGone (after the check) rather than right after `_before`; it still breaks the Hash assertion.

## Left undone
- Nothing in scope. Not pushed (per the brief).
