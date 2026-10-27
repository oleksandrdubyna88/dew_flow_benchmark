# S2 t5 — code notes (E5.5: Kimi consultant check, S5.5.1–S5.5.4)

## What I did

- Added ONE file: `src_mcp/tests/AKimiConsultantCheckTests.cs`, class `AKimiConsultantCheckTests`, five cases (S5.5.1, S5.5.2 as a Theory with two arms, S5.5.3, S5.5.4).
- No production code changed. Committed as `0b4dda25` (only the new file).
- Branch: `tmp/S2-t5` (the brief named `exp/kimi-e56-sonnet-split-haiku-2`; I worked on the branch the worktree was on).

## Results

- On arrival, all five green (the document predicted this).
- `ConsultantCheckTests`: 32 total, 30 passed, 2 skipped (Windows-only Vibe arms). Green.
- My class on clean code: 5 of 5 passed.

## Teeth (one temporary break each, reverted, `git status` showed only the new file)

- S5.5.1 — `ConsultantCheck.cs` `PlaceAsync` forced to `Checkout`.
  The document predicted the `--cwd` assertion. The actual first red was the `state` assertion:
  `failureWhat`: "a Kimi consultant stands in the conversation's sanitised snapshot (Workdir), and none was made — the checkout is never its working folder (D6) (Parameter 'launch')".
  The Kimi preparation refuses before launch, so the `--cwd` line is never reached. Still red; different message.
- S5.5.2 (both arms) and S5.5.3 — `VibeHomes.cs` `EmptiedUnderItsLock` deletes the whole folder with `GitScratch.DeleteEvenIfReadOnly` first.
  Red with the document's message: "the deleter's unlink recorder must not be invoked, because KimiHome unlinks the junction first".
- S5.5.4 — `ConsultCheckScratch.cs` `KimiHomesDisposed` deletes the scratch whole first.
  Red with the same message.

## Deviations from the document

1. `RecordingTheDeleter()` in the document asserts `_unlinked` is empty BEFORE the check. That assertion can never fail, so I moved the documented message to a post-check helper `TheDeleterNeverMetTheJunction()`. Same message text.
2. S5.5.3 waits with `started.Task.WaitAsync(TimeSpan.FromSeconds(60), ...)` instead of a bare `await started.Task`. With the S5.5.1 break the check never launched the shim and the bare await hung the run. Now it fails with a timeout.
3. `dotnet build` on the SOLUTION (`dew_flow_connect_other_ais.slnx`) fails on this host before any code compiles: MSB4014, the .NET 10.0.113 SDK looks for `sdk-manifests/10.0.100/microsoft.net.sdk.android`, which is missing. The MCP test project builds fine on its own (`src_mcp/tests/CoaiMcp.Tests.csproj`), so I built that project. Not my file; not changed.
4. `dotnet format` was run on the test project, scoped to my file, with `--verify-no-changes`. No output, which is the clean result. Not run on the solution (same MSB4014 as item 3).

## What is left

- WHOLE SUITE: NOT CONFIRMED. The brief said "do not run the whole suite"; the document's command list includes it. I started it twice before I noticed the conflict. Both runs stalled in the same place: after the skipped `AKimiShimScenarioTests` lines, with no output for over 20 minutes. I stopped them. I did not find which test hangs. It is not in my file (my class did not run in that pass). The two known failures (`KimiRuntimeTests.ALaunchThatNamesItsHome_...` and `KimiHomeTests.AHomeWhoseDataPathPasses260_...`) were not seen because the run never finished.
- Commit: the document says "Do not commit"; the brief says commit. I followed the brief.
- The document's `coai-vh` check: `Directory.EnumerateFileSystemEntries` throws when the folder is gone, so the test checks `Directory.Exists` first.
