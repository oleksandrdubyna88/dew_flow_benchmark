# SR4 t2 — code notes (planner kimi arm, KimiConsultant flag refusal)

Commit: `c17d1dda` on `exp/kimi-e56-sonnet-split-haiku-review-4` (not pushed).

## What I did

1. Tests first (`src_mcp/tests/ConfinementPlannerTests.cs`):
   - S5.6.3 `Kimi_None_StandsInAnEmptyScratch_Disk_InTheCopyOfItsRoots_NoFlagsEver_AndWebIsRefused`.
   - Theory row `[InlineData("kimi", Capability.Web, "not been measured")]`.
   - Unknown-runtime sentence now `... vibe, kimi`.
   - S5.6.5 `AKimiQuestionLaunch_IsTheShimRequest_InItsFolderOrItsCopy_OnTheRowsHome`.
   - S5.6.6 `APlanWithFlags_IsRefusedForKimi`.
   - Private helper `KimiConsultantOn(KimiFixture)` (the two-line construction).
2. Red run, before any production edit: 3 failures, as the document predicts (see below).
3. Planner arm `"kimi" => Kimi(grant, flag)` and the `Kimi` method after `Vibe`, exactly as the contract (`ConfinementPlanner.cs`).
4. `MustCarryNoFlags` in `KimiConsultant.cs`, called right after `MustBeLaunchable`, placed after `OwnedFolder`, exactly as the contract. `Flags` and `Leading` are `IReadOnlyList<string>`, so `.Count` is used (no `.Length`).
5. Green runs (below). Format check. Commit.

## Red messages (before production change)

- S5.6.3: `Expected disk.Cwd to be CwdKind.Snapshot … but found CwdKind.Scratch.`
  The document says `plan.Cwd`. The test variable is `disk`, so the text differs only in the name. Same reason.
- S5.6.5: `Expected disk.Request.WorkingDirectory to be the same string … but they differ at index 10`, actual `/tmp/coai-scratch-…`, expected `/tmp/coai-question-…` (RowTree).
  Same reason as the document (the Snapshot cwd arm was missing). The `none` part was green on arrival, as the document says.
- S5.6.6: `Expected a <System.ArgumentException> to be thrown, but no exception was thrown.` Exactly as the document says.

## Green runs (after production change)

- `ConfinementPlannerTests`: 35 total, 0 failed.
- `KimiConsultantTests`: 12 total, 0 failed.
- `CapabilityMatrixTests`: 39 total, 0 failed.
- `dotnet format … --verify-no-changes --include` on my three files: clean (exit 0).
- Build: 0 warnings, 0 errors.

## Deviations and notes

- **Test runner path.** The document's command runs `./src_mcp/tests/bin/Debug/net10.0/CoaiMcp.Tests.exe`. That file does not exist on this Linux host (the `.exe` is a Windows-only name). The runnable file is `./src_mcp/tests/bin/Debug/net10.0/CoaiMcp.Tests` (no extension). I used that, with the same `--filter-class` argument.
- **Commit vs "do not commit".** The task Rules say to commit. The document's *Out of bounds* section and its opening line say "No commit". I followed the Rules and committed my three files by name. If the "no commit" line was meant to hold, this commit can be reset with `git reset --soft HEAD~1`.
- **Format check scope.** I ran `dotnet format --verify-no-changes` with `--include` for my three files only, not on the whole solution, because other tasks edit their files at the same time.
- **Known failures.** The document names two failing tests on this host: `KimiRuntimeTests` and `KimiHomeTests`. Neither class is in my filter, so I did not run them. Not my tests.
- **Not run.** I did not run the whole suite (the task says not to).

## Left

Nothing in t2's scope is left.
