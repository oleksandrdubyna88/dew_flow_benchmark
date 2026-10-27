# T3 / t4 — review notes

Scope: `src_mcp/tests/AKimiQuestionRowTests.cs` only (checked: the diff touches no other file).

## Right as it was
- Class, namespace, `IAsyncLifetime`, fields, `InitializeAsync`/`DisposeAsync` (resets `GitScratch.AfterUnlink`, disposes person and dirs).
- `StandIn` without `CallingAs`, `Scopes`, `ServiceFor` row shape, `RepositoryAsync`, `AskAsync`, `TheRow`: match the Vibe class and the document.
- Theory name, three InlineData, first line (`Assert.SkipUnless ... D18`), `before` taken AFTER the re-login write in `refused`.
- `answered`: status `RowOutcomes.Answered`, one shim launch, stdin contains `<homes>/coai-vh`, does not contain `Repo/src`.
- `refused`: status `RowOutcomes.Failed`, `played.Shims` empty. `cancelled`: `During` cancels and throws; `ThrowAsync<OperationCanceledException>`.
- Every arm: `coai-vh` empty, `_unlinked` empty, person hash equal.
- Coder's other deviations (own `_homes` field, helper methods, named `asking` local) are fine: same behaviour, complexity rule.

## Fixed
- Deviation 1 of the coder (a `Directory.Exists` guard before enumerating `coai-vh`): removed. Kimi's `HomesFolder` is non-empty
  (`KimiConsultant.cs:40`), so `QuestionRowLaunch.RunAsync` reserves the one-shot folder (line 101) BEFORE any refusal or launch;
  `coai-vh` is there on every ending (the Vibe class enumerates it unguarded in the same cases). The guard let the check pass with
  nothing to look at. Now the spec's one line.

## Results
- Build: 0 warnings, 0 errors.
- `--filter-class CoaiMcp.Tests.AKimiQuestionRowTests`: 3 skipped (D18, Linux), 0 failed. Red/green not observed (Windows only).
- `dotnet format whitespace --verify-no-changes --include` on the file: clean.
- Whole suite not re-run (only a test-file edit of one helper). The coder's run: 4 failures, 2 known + 2 `QuestionRowOnAgyScenarioTests`
  that pass alone — not this task's.
