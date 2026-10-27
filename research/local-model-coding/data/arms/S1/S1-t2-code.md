# S1 t2 — code notes (E5.6: a Kimi question row, its flag refusal, the one-shot row proved through the service)

Branch: `exp/kimi-e56-sonnet-split-haiku-1`. Base: `b4e2ea96`.

## What I did

1. `src_mcp/tests/AKimiQuestionRowTests.cs` (new). Class `AKimiQuestionRowTests`, `[Collection("fakecli-env")]`, `IAsyncLifetime`.
   - S5.6.5 `AKimiQuestionLaunch_IsTheShimRequest_InItsFolderOrItsCopy_OnTheRowsHome` (pure).
   - S5.6.6 `APlanWithFlags_IsRefusedForKimi` ×3 (`flags`, `leading`, `adddirs`).
   - S5.6.7 `AKimiDiskRow_ReadsItsCopies_AndLeavesNothingBehind` ×3 (`answered`, `refused`, `cancelled`), through `PanelService.AskConsultantsAsync`.
2. `src_mcp/runners/Kimi/KimiConsultant.cs`: `MustCarryNoFlagsOfAPlan(launch)` added after `MustBeLaunchable` in `Build`, one `<para>` sentence in the class `<remarks>`. Nothing else changed.

## Red and teeth runs

Red, S5.6.6 (before the production change), all three shapes:
- `Expected a <System.ArgumentException> to be thrown, but no exception was thrown.` — matches the document.

Red, S5.6.5 teeth (temporary break: `Cwd` returns `launch.RepoPath` for a planned launch):
- `Expected none.Request.WorkingDirectory to be the same string, but they differ at index 31: ...`
- The document expected `Expected Request.WorkingDirectory to be "<Scratch>", but found …`. The wording differs (FluentAssertions `Be` on a string names the property with its full path and reports the first differing index). Same assertion, same reason. Reverted.

Red, S5.6.7 teeth (a): `Release(homes);` commented out in `QuestionRowLaunch.cs` `finally`:
- `Expected Directory.EnumerateFileSystemEntries(oneShot) to be empty because the row's one-shot folder is gone, whatever ended the row, but found at least one item {".../homes/coai-vh/<8>"}` — red for `answered`, `refused` and `cancelled`. Reverted with `git checkout`.

Red, S5.6.7 teeth (b): in `VibeHomes.cs` `EmptiedUnderItsLock`, the `KimiHome.DisposeAllUnder(...)` call replaced by `GitScratch.DeleteEvenIfReadOnly(folder);`. The call is in the shared half that `OneShotHome` uses, not in `OneShotHome.Dispose` itself. Used as the nearest equivalent:
- `Expected _unlinkedByTheDeleter to be empty because the deleter's unlink recorder must not be invoked, because KimiHome unlinks the junction first, but found at least one item {".../homes/coai-vh/<8>/kh-<10>/home/credentials"}` — red for `answered` and `cancelled`. Reverted with `git checkout`.

## Deviations from the document

- **Bin in the pure test's adapter.** The document builds the kimi adapter with `KimiPlaces.ForShim(root, Path.Combine(RowFolder, "persons-bin"))`, the same as `ConfinementPlannerTests.Adapters()`. With that, `KimiEnvironment.Refusal(request, _person.PlacesAt(home))` is NOT empty: the `PATH` begins with `row/persons-bin`, not the person's bin, and the launch is refused (D19). I used `_person.Bin` for the shim bin so the refusal check tests the real thing. This is a test-only choice; the production code is unchanged.
- **Repo location.** The document gives `Repo = <_person.Folder>/checkout` for S5.6.5 and also has `InitializeAsync` create the git repository `Repo`. I used one property for both: the checkout lives under the person's fixture folder, outside `persons/.kimi-code`, so `Hash()` does not see it.
- **Kimi config path in the fixture repo.** Used `src/.kimi-code/config.toml` (Kimi's folder), not `.vibe/hooks.toml`.
- **Copy name.** The document asks for the copy under `request.WorkingDirectory`. I asserted `request.StdIn` contains `request.WorkingDirectory` and does not contain the granted root. I did not assert the copy's own folder name.
- **Commit despite out-of-bounds line.** The document's out-of-bounds section says "Do not run `git commit`"; the task prompt says commit with a conventional message. I followed the task prompt. Commit is made, not pushed.

## Metric note

`MustCarryNoFlagsOfAPlan` is written exactly as the document gives it. By the Roslyn count (1 + `if` + `&&` + two `||`) its cyclomatic complexity is 5, not ≤ 4 as the document asks. I did not reshape it, because the document fixes the code. If the ≤ 4 rule is meant strictly, rewrite the condition as `plan.Leading.Count + plan.Flags.Count + plan.AddDirs.Count > 0` (complexity 3). Not done.

## Test results (host: Linux, WSL2)

Built with `dotnet build dew_flow_connect_other_ais.slnx -c Debug -m:4 --disable-build-servers`: 0 errors, 0 warnings.

Run through `low-priority.mjs`, runner `./src_mcp/tests/bin/Debug/net10.0/CoaiMcp.Tests --filter-class ...` (the host has no `.exe`):
- `AKimiQuestionRowTests`: 7 total, 7 passed, 0 failed.
- `KimiConsultantTests`: 12 total, 12 passed.
- `ConfinementPlannerTests`: 33 total, 33 passed.
- `AKimiConsultationScenarioTests`: 12 total, 12 passed.

The two known failures in the document (`KimiRuntimeTests.ALaunchThatNamesItsHome_...`, `KimiHomeTests.AHomeWhoseDataPathPasses260_...`) were not in the four classes I ran, so I did not see them. None of the S5.6.x tests were skipped on this host (the stand-in runs them).

`dotnet format dew_flow_connect_other_ais.slnx --verify-no-changes`: did NOT run clean on this host. The solution-wide run stops on IL2026/IL3050 analyzer errors in `src_server/src/*` (ReviewEndpoints, CatalogEndpoints, UsageEndpoints, SessionEndpoints). Those files are not mine. As a scoped check I ran `dotnet format <project> --verify-no-changes --include <file>` for `src_mcp/tests/AKimiQuestionRowTests.cs` and `src_mcp/runners/Kimi/KimiConsultant.cs`; both reported no changes.

## git status at the end

Only the two files are changed: `src_mcp/runners/Kimi/KimiConsultant.cs` (modified) and `src_mcp/tests/AKimiQuestionRowTests.cs` (new). `QuestionRowLaunch.cs` and `VibeHomes.cs` were broken and reverted with `git checkout`; both are clean.

## Left

- Nothing of the contracts is missing.
- Two items above need a decision: the complexity count (5 vs ≤ 4) and whether a scoped format check is enough for the document's solution-wide `dotnet format` command, which this host cannot run for reasons outside t2.
- Not pushed.
