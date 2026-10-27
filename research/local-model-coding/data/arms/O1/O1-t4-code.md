# O1 t4 — code notes (A Kimi disk question row reads its copies and leaves nothing behind, S5.6.7)

## What I did
- Added `src_mcp/tests/AKimiQuestionRowTests.cs`: class `AKimiQuestionRowTests : IAsyncLifetime`, `[Collection("fakecli-env")]`,
  one `[Theory]` `AKimiDiskRow_ReadsItsCopies_AndLeavesNothingBehind` with `answered`, `refused`, `cancelled`.
- No production change. `src_mcp/src/Server/QuestionConsult/QuestionRowLaunch.cs` is unchanged (`git diff` empty).
- Commit: the new test file only, by name.

## Deviations from the document (and why)
1. **`refused` arm.** The document says to change the fake person's sign-in before the ask. I tried that first (the S5.4.16 config swap).
   It did NOT refuse: a fresh question row rebuilds its home from the person's current config, so the row was answered. The S5.4.16
   refusal needs an EXISTING conversation home, which a one-shot row does not have. I replaced it with a root outside any Git work tree
   (`PlainFolder()`), refused before any launch with `not inside a Git work tree`. This is the same refused-before-launch shape the Vibe class
   uses (`ARowRefusedAtItsCopy_GivesItsOneShotFolderBack`). Asserted: status `refused`, reason, no shim started, nothing left.
2. **Recorders.** The document says `GitScratch.AfterUnlink` and `KimiHome.AfterUnlink` are "never invoked". `KimiHome.AfterUnlink` fires on
   EVERY normal junction-first unlink (`KimiHome.cs` `Unlinking`, line ~361), which is the behaviour S5.5.2 asserts. So I assert:
   `GitScratch` recorder empty on every ending (the deleter never met a link); `KimiHome` recorder empty for `refused` (no home was made),
   and exactly one link ending `home\credentials` for `answered` and `cancelled`.
3. **Scratch folder for the `refused` arm.** No launch, so no `--cwd` to read the scratch from. The Vibe class checks nothing there either.
   The scratch check runs only when a shim launched. The `coai-vh` folder, the recorders and the person's folder are checked on all three arms.
4. **`CallingAs`** is not used: `ask_consultants` takes no caller, so the StandIn has no caller scope (the document allows this).
5. **Copies** are read in the `Turn` callback (they exist only while the row runs), then checked against the prompt after the row.

## Red / green
- Green as written: `AKimiQuestionRowTests`, 3 of 3 (before the mutation, after the refused-arm fix).
- Red-first on the first draft: the `refused` arm failed (`status` was `answered`), which is deviation 1. Then the recorder assertion
  failed on an empty collection (fixed as deviation 2). Both red messages differed from the document's plan, and I fixed the test, not production.
- Mutation check (Step 1): in `QuestionRowLaunch.cs` `finally`, I removed `Release(homes);`. All 3 arms went red with the
  document's message: `Expected Directory.EnumerateFileSystemEntries(Path.Combine(Homes, VibeHomes.OneShotFolder)) to be empty because the row's
  coai-vh folder is gone on every way out, but found at least one item …`.
- **Restore:** I restored the file with `git checkout -- src_mcp/src/Server/QuestionConsult/QuestionRowLaunch.cs`. The file was clean
  (`git status` at start was clean for this repo's own work), and the restored file matches HEAD (`git diff` empty). If another task had
  uncommitted edits to that file at that moment, `git checkout` would have discarded them. I found none: the tree showed only my new file
  after the restore. Recorded here so the owner of that file can check.

## Full MCP suite (after the restore, rebuilt)
- Command: `low-priority.mjs ./src_mcp/tests/bin/Debug/net10.0/CoaiMcp.Tests` (raw output: `/home/jinx/git/coai-exp/notes/O1-t4-suite-raw.txt`)
- total 10555, failed 2, succeeded 10469, skipped 84.
- Failed: `KimiHomeTests.AHomeWhoseDataPathPasses260_IsCheckedAndDisposed` and `KimiRuntimeTests.ALaunchThatNamesItsHome_GoesThroughTheLaunchControl_WhichReadsThePersonsSignIn`.
  These are the two known Linux-host failures; not mine, not skipped.

## Left
- Nothing in scope. The `refused` arm of S5.6.7 is covered by the copy refusal, not by the launch control (deviation 1); a launch-control
  refusal on a fresh row would need a seam that does not exist (the Kimi `--version` probe is always answered by `PlayedKimi`).
- Build/test commands used: the document's; the solution build reports "Build succeeded" with 0 errors.
