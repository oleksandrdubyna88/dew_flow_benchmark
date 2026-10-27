# T3-t3 — code notes (E5.5, consultant check for Kimi, tests only)

## What I did
- Added `src_mcp/tests/AKimiConsultantCheckTests.cs`: `public sealed class AKimiConsultantCheckTests : IDisposable`,
  namespace `CoaiMcp.Tests`, five cases (S5.5.1–S5.5.4):
  - `AKimiCheck_RunsInACopy_OnAOneShotHome` (S5.5.1)
  - `AfterTheCheck_TheHomeIsGone_JunctionFirst` [Theory: passed, exit70] (S5.5.2)
  - `ACheckCancelledMidTurn_StillDisposesItsHome` (S5.5.3)
  - `ARootlessCheck_KeepsTheHomeInItsScratch_AndDisposesItJunctionFirst` (S5.5.4)
- Every case starts with `Assert.SkipUnless(OperatingSystem.IsWindows(), "D18: Kimi is refused on every host but Windows");`.
- No production file changed. No other file changed.

## Choices where the document left room
- `_data` is created in the constructor as well as `_temp` (the document only says `_temp` is created). Harmless.
- `OneShotEntries(homes)` treats an ABSENT `coai-vh` folder as empty. The document's `EnumerateFileSystemEntries` would
  throw on an absent folder; an absent folder also means the home is gone.
- `Scopes` is a private nested class (copied from `AKimiConsultationScenarioTests`), not shared.
- `Check` returns `(int Code, JsonElement Said, string Err)` and parses stdout, as `ConsultantCheckTests.Check` does.
- No `[Collection("fakecli-env")]`: none of these cases sets a fake-CLI environment variable.

## Test results (Linux host, WSL2)
- Build (`dew_flow_connect_other_ais.slnx`, Debug): succeeded, 0 warnings, 0 errors.
- `--filter-class CoaiMcp.Tests.AKimiConsultantCheckTests`: 5 skipped, all with the D18 reason. Compiles.
- Whole suite: total 10539, failed 3, succeeded 10447, skipped 89.
  - `KimiRuntimeTests.ALaunchThatNamesItsHome_GoesThroughTheLaunchControl_WhichReadsThePersonsSignIn`: known (in the document).
  - `KimiHomeTests.AHomeWhoseDataPathPasses260_IsCheckedAndDisposed`: known (in the document). PathTooLongException on this host.
  - `TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow`: NOT in the document's known list.
    It fails when run alone with `--filter-class CoaiMcp.Tests.TheRoundsNumstatsAreCountedTests` (1 failed, 9 passed), so it
    does not depend on my class. Not investigated further (outside my scope).
  - The document's extension test (`the baseline names no release that was never tagged`) is a TypeScript test in
    `src_vs_code/src/test/`, not part of this .NET run.
- `dotnet format <slnx> --verify-no-changes` (whole solution): exit 2. The errors are IL2026/IL3050 trimming and AOT
  analyzer warnings in `src_bugs/src/*.cs` and `src_server/src/*.cs`, not in my file.
- `dotnet format --verify-no-changes --include src_mcp/tests/AKimiConsultantCheckTests.cs`: exit 0.

## Red / green
- NOT observed. Every case skips on Linux (D18), so there is no red message and no green. The document's mutation check
  (expect `_unlinked` non-empty, then revert) needs Windows and was not run. Recorded as "skipped on Linux; red/green not observed".

## Left undone
- Red/green and the one-line mutation proofs on Windows.
- The `TheRoundsNumstatsAreCountedTests` failure above is unexplained.
- Whole-solution `dotnet format --verify-no-changes` is red because of analyzer errors in other projects (not mine).
