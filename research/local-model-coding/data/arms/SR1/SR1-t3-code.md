# SR1-t3 — code notes (KimiConsultant, planned launch)

Commit: `6ffb0b8a` on branch `tmp/SR1-t3` (not pushed). Files: `src_mcp/runners/Kimi/KimiConsultant.cs`, `src_mcp/tests/KimiQuestionLaunchTests.cs` (new).

## What was done
- Added `MustCarryNoFlags(launch)` to `KimiConsultant.Build`, right after `MustBeLaunchable`. It throws `ArgumentException` ("…the shim…") when a planned launch's plan has any entry in `Flags`, `Leading` or `AddDirs`. Nothing else in `Build` changed.
- Added `KimiQuestionLaunchTests` with the three tests from the task document (S5.6.5 shim request; S5.6.6 `[Theory]` over flags / leading / add-dirs; one-shot handle check).

## Deviations from the document
1. **Test 1 argv order.** The document asserts `ContainInConsecutiveOrder("--ask-acp", "--profile", "kimi", "--cwd", …)`. That cannot hold: `Build` always adds `--model <default>` after `--profile kimi`, so it failed on arrival. I changed it to `"--ask-acp", "--profile", "kimi", "--model", KimiModels.Default, "--cwd", request.WorkingDirectory`, the same shape `KimiConsultantTests` uses (line 77). Test intent unchanged.
2. **Extra using.** The document's usings omit `CoaiMcp.Runners.Reviewers` (needed for `ReviewerSettings`). Added. The first build failed on this (CS0246).
3. **Commit vs. "No commit".** The task document's out-of-bounds line says "No commit"; the brief for this run says commit and never push. I followed the brief.
4. **Branch.** The brief names `exp/kimi-e56-sonnet-split-haiku-review-1`; the worktree is on `tmp/SR1-t3`. I committed on the worktree's branch and did not switch.

## Red / green
- Before production change: test 1 red (argv order, deviation 1 above); test 3 green on arrival; test 2 red for all three cases with `Expected a <System.ArgumentException> to be thrown, but no exception was thrown.` (matches the document).
- After: `KimiQuestionLaunchTests` 5/5, `KimiConsultantTests` 12/12, `ConfinementPlannerTests` 31/31.
- Runner: tests were run as `dotnet ./src_mcp/tests/bin/Debug/net10.0/CoaiMcp.Tests.dll --filter-class …` (the `.exe` named in the document does not exist on Linux; `low-priority.mjs` reports ENOENT for it).
- The two known pre-existing failures named in the document were not in the classes I ran.

## Teeth runs (document Step 3)
- (a) `Cwd` returns `launch.Workdir` for every planned launch → **red**: `none.Request.WorkingDirectory` differs at index 54 (S5.6.5 `none` assertion).
- (b) drop `ConsultantLaunches.ForQuestion` from `Build` → **still green** (5/5). Not a tooth. `KimiEnvironment.cs:55-56` already sets `InheritsEnvironment = false` and `Passthrough = Minimal` on every Kimi request, so the envelope is redundant on this path. The test cannot see it. Not changed; flagged.
- (c) pass `launch.Settings` without `OwnedScratch = OwnedFolder(launch)` → **still green**. In the test, `launch.Settings.OwnedScratch` is already `RowFolder`, which is what `OwnedFolder` returns, so the break is a no-op for the test. Variant (c′): `OwnedScratch = launch.ScratchDir` → **red**: `Path.GetDirectoryName(home)` differs at index 53 (home-folder assertion). So the home assertion does have teeth; the document's literal break does not.
- All production edits reverted; the file was restored from a backup and the diff checked against the commit.

## Format
- `dotnet format <solution> --verify-no-changes` exits 2 on this host, but only on AOT/trimming analyzer diagnostics (IL2026/IL3050) in `src_bugs` and `src_server`, which this task does not touch.
- `dotnet format whitespace` with `--verify-no-changes` on the two changed files: clean.

## Left undone
- Nothing from the task's scope. Not pushed.
- Teeth (b) and the document's teeth (c) do not catch their breaks (see above). Test changes to close that gap were out of scope; a reviewer may want them.
