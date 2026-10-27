# S2 t4 — code notes: a Kimi `disk` question row through the service (S5.6.7)

## What I did
- New file only: `src_mcp/tests/AKimiQuestionRowTests.cs` (class `AKimiQuestionRowTests`, one `[Theory]` with arms
  `answered`, `refused`, `cancelled`, test name `AKimiDiskRow_ReadsItsCopies_AndLeavesNothingBehind`).
- No production change. Helpers copied from the Vibe twin (`AVibeQuestionRowReadsACopyOfItsRootsTests`); Kimi seams and
  scopes as the document says.

## Deviations from the document (all in the test file)
1. Added `[Collection("fakecli-env")]` on the class. The document's class declaration does not have it. The scopes touch
   statics (`GitScratch.AfterUnlink`, `KimiCredentialsLock.Final`), and `AKimiConsultationScenarioTests` runs in that
   collection for the same reason. Remove it if the lead wants the exact declaration.
2. The copy assertion searches the copy RECURSIVELY. The copy is `<cwd>/<snapshot>/...`, so `Mailer.cs` is not a direct
   child of `copies[0]`. The contents are captured inside the shim's `Turn`, because the row deletes the copy before the
   test body continues. The checks (`Mailer.cs` present; `.kimi-code` and `AGENTS.md` absent) keep the document's intent.

## Test results
- Build (Debug, `dew_flow_connect_other_ais.slnx`): succeeded, 0 errors, 0 warnings.
- `AKimiQuestionRowTests` (3 arms): 3 passed.
- Whole suite (`CoaiMcp.Tests`): 10557 total, 10471 passed, 84 skipped, 2 failed.
  The 2 failures are the known ones: `KimiRuntimeTests.ALaunchThatNamesItsHome_GoesThroughTheLaunchControl_WhichReadsThePersonsSignIn`
  and `KimiHomeTests.AHomeWhoseDataPathPasses260_IsCheckedAndDisposed`.
- Format: `dotnet format whitespace` and `dotnet format style`, each with `--include src_mcp/tests/AKimiQuestionRowTests.cs
  --verify-no-changes`: clean (no output).
- Solution-wide `dotnet format ... --verify-no-changes` exits 2. Its output is only IL2026/IL3050 analysis errors in
  `src_server/src/*.cs` (ASP.NET `MapGet`/`MapPost` trim and AOT warnings), not in my file. I did not check this against a
  clean checkout.

## Teeth (each reverted; `git status` showed only the new file afterwards)
1. `ConfinementPlanner.Kimi` Disk changed to `CwdKind.Scratch`:
   - answered: `Expected copies to contain a single item because the one granted root is one copy, but the collection is empty.` (as the document expects)
   - refused: also RED (`Expected row.GetProperty("status") ... "refused"`). Not in the document: a scratch plan skips the git-work-tree check.
   - cancelled: passed.
2. `OneShotHome.Dispose` deletes the whole folder with `GitScratch.DeleteEvenIfReadOnly` before `Emptied()`:
   - answered: `Expected _recorded to be empty because the deleter's unlink recorder must not be invoked: KimiHome unlinks the junction first, but found at least one item {".../coai-vh/.../kh-.../home/credentials"}.` (as the document expects)
   - cancelled: RED with the same recorder message. Not in the document.
   - refused: passed.
3. `QuestionRowLaunch.RunAsync` `finally` without `Release(homes);`:
   - all three arms RED: `Expected Directory.EnumerateFileSystemEntries(.../coai-vh) to be empty ... but found at least one item`. Not only the cancelled arm: `Release` is also the normal path, so the answered and refused arms also leave the folder.

## Commit
- Committed only `src_mcp/tests/AKimiQuestionRowTests.cs`, by name.
- The document's "Out of bounds" says "Do not commit", but the coder brief says "Commit with a conventional message". I followed the brief. If the lead wants no commit, undo the commit with `git reset --soft HEAD~1` (the file stays in place).
- Attribution line per the session's instruction: `Co-Authored-By: Claude Haiku 5.5 <noreply@anthropic.com>`.

## Left undone
- Nothing from the contracts. Not pushed.
