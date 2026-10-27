# O3-t3 — the consultant check of a Kimi row (S5.5.1–S5.5.4, E5.5)

Commit: e476115a (test only). Only `src_mcp/tests/AKimiConsultantCheckTests.cs` was added.
Production (`ConsultantCheck.cs`, `ConsultCheckScratch.cs`) is unchanged, as decision 1 asks.
Not pushed.

## What was done

- New class `AKimiConsultantCheckTests` with four contracts:
  1. `AKimiCheck_RunsInACopy_OnAOneShotHome` (Fact)
  2. `AfterTheCheck_TheHomeIsGone_JunctionFirst` (Theory: answered, failed)
  3. `ACheckCancelledMidTurn_StillDisposesItsHome` (Fact)
  4. `ARootlessCheck_KeepsTheHomeInItsScratch_AndDisposesItJunctionFirst` (Fact)
- Extra assertions, beyond the contract text:
  - Theory: `state` equals the ending, so each arm does what its name says.
  - Canary key is `not-attempted` (the key the Vibe check uses for a CANNOT answer).
  - Rootless test: `state` is `answered`, so the `--home` check is meaningful.
- Helpers copied from `ConsultantCheckTests.cs` (not edited). The stand-in uses a copied `Scopes` class.
- The recorder is `GitScratch.AfterUnlink`. It is set in the constructor and reset in `Dispose`.

## Red and green

- Written first, then built. All five cases were GREEN on arrival (as the document expected).
- Teeth, each temporary break reverted before commit:
  1. `NeedsASanitisedSnapshot` forced to false (`PlaceAsync` → Checkout). Expected: `--cwd` red.
     Actual: the test is RED for another reason, earlier than the assertion:
     `state: failed`, `failureKind: record-failed`, `failureWhat: "a Kimi consultant stands in the
     conversation's sanitised snapshot (Workdir), and none was made — the checkout is never its working folder (D6)"`.
     Production refuses the Checkout placement itself. So the `--cwd` assertion is not the first guard.
     This test is still red, so the scenario has teeth, but the red message differs from the document.
  2. Deleter break: `ConsultCheckScratch.Delete` skips `KimiHomesDisposed`, and `VibeHomes.EmptiedUnderItsLock`
     skips `KimiHome.DisposeAllUnder`. Done in one build. Reds, all with the recorder message:
     `Expected _unlinkedByTheDeleter to be empty … but found at least one item … /home/credentials`
     for the answered, failed, cancelled and rootless cases. The first-test (`--cwd`/home) case stayed green, as expected.
  3. `finally` dispose removed in `TurnAsync`. Reds:
     `Expected Directory.EnumerateFileSystemEntries(OneShotFolder) to be empty … but found … coai-vh/<8>`
     for cancelled, answered and failed.

## Test results

- Build: 0 warnings, 0 errors (`dotnet build … -m:4 --disable-build-servers`).
- `AKimiConsultantCheckTests`: 5/5 green.
- `ConsultantCheckTests`: 30 passed, 2 skipped (Vibe, Windows only). Green.
- `AOneShotKimiHomeIsDisposedJunctionFirstTests`: 10/10 green.
- `dotnet format --verify-no-changes`: no output (no changes needed). The exit code I saw was from `tail`, so the result is by output, not exit code.
- Whole MCP suite: 10539 total, 10450 passed, 5 failed, 84 skipped. Log: `/home/jinx/git/coai-exp/notes/O3-t3-suite.log`.
  - Known failures (not mine): `KimiRuntimeTests.ALaunchThatNamesItsHome_…` and `KimiHomeTests.AHomeWhoseDataPathPasses260_IsCheckedAndDisposed`.
  - `QuestionConsultScenarioTests.ACodexQuestionRow_…` and `AskHumanScenarioTests.RequireRefuses…`: failed in the full run, PASS when their class runs alone. Likely load-related. Not investigated further.
  - `TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow`: NOT in the known list. Fails when its class runs (1 ms deadline, a timing test); passes when run alone by method filter. I did not run it against a tree without my file, so I cannot prove it is pre-existing. My change adds only a test file, which does not touch it.
- The Vitest extension was not part of this task.

## Notes for the owner

- Branch: the checkout is `tmp/O3-t3`, not `exp/kimi-e56-opus-split-haiku-3` as the brief says. I committed on the current branch.
- `GitScratch.AfterUnlink` is a static. My test sets it, as `AOneShotKimiHomeIsDisposedJunctionFirstTests` does, and the two classes are not in one xUnit collection. They may interfere if they run at the same time. No interference was seen in these runs.
- Not done: the live (paid) check is J2 and is the owner's. Not run.
- Nothing left in the scope of t3.
