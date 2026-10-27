# T1 t5 — code notes (S5.6.7, E5.6)

Commit: 617326f4 — only `src_mcp/tests/AKimiQuestionRowTests.cs` (new). No production code changed.
Branch base seen: HEAD dac76033 (common.md names b4e2ea96; t1+t2 commits were already in the tree).

## What was done
- New class `CoaiMcp.Tests.AKimiQuestionRowTests` with the contracted fields, helpers and theory
  `AKimiDiskRow_ReadsItsCopies_AndLeavesNothingBehind` (Answered / RefusedBeforeTheLaunch / CancelledMidTurn).

## Deviations from the document (all in the test file)
1. RefusedBeforeTheLaunch: the document says `File.Delete(_person.Credentials)`. `KimiFixture.Credentials` is the
   credentials FOLDER (`KimiFixture.cs:50`), so File.Delete cannot remove it. First try: I deleted the sign-in file
   `credentials/kimi-code-env-x.json` instead — the row still ANSWERED (red: `expected "failed" but was "answered"`),
   because the launch control refuses only a missing credentials FOLDER (`KimiLaunch.cs:194`).
   Fix: clear the read-only bits and `Directory.Delete(_person.Credentials, recursive: true)`.
2. The same arm: the document takes `before = _person.Hash()` first and removes the sign-in after it. That cannot
   hold — removing the folder changes the person's folder. So the removal now runs BEFORE the snapshot, and the
   snapshot stays the proof that the ROW changes nothing in the person's folder.
   Note: the read-only clearing (File.SetAttributes) is there for Windows; not run on Windows here.

## Results (Linux, WSL2, Debug)
- Build: 0 warnings, 0 errors.
- `AKimiQuestionRowTests`: 3 total, 3 passed, 0 skipped (none Windows-only; all three arms run here).
- Red seen before the fix (arm 1 deviation above): `Expected ... "failed" ... but "answered"` at the status line.
- Bite check (temporary): `_unlinkedByTheDeleter.Should().NotBeEmpty(...)` → 3 of 3 arms FAILED
  (`Expected _unlinkedByTheDeleter not to be empty ...`). Undone; re-run green.
- Whole MCP suite: 10556 total, 10469 passed, 84 skipped, 3 failed:
  - KimiRuntimeTests.ALaunchThatNamesItsHome_GoesThroughTheLaunchControl_WhichReadsThePersonsSignIn — KNOWN (common.md)
  - KimiHomeTests.AHomeWhoseDataPathPasses260_IsCheckedAndDisposed — KNOWN (common.md)
  - TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow — NOT in common.md. Fails in the
    full run and alone (`Expected (_assembler.AllChangesAsync(..., oneMillisecond)).Failed ... found False`).
    It does not touch my file. Not fixed (out of scope). Needs an owner.
- Skipped tests of mine: none.

## Left
- Nothing of t5 remains. The Windows run (the sign-in read-only clearing, junction disposal) is not verified here.
