# SR3 t2 — code notes

Task: `/home/jinx/git/coai-exp/vllm/work/SR3/t2.md` (E5.6 decisions 1 and 3; S5.6.3, S5.6.5, S5.6.6).
Commit: `962f8b0c` on `exp/kimi-e56-sonnet-split-haiku-review-3`. Not pushed.

## What I did

1. Tests first (`src_mcp/tests/ConfinementPlannerTests.cs`):
   - `APairTheMatrixRefuses…` gained the `kimi`/`Web` row.
   - The unknown-runtime sentence now names `kimi`.
   - New `ThePlannerArm_IsACopyForDisk_AnEmptyFolderForNone_AndRefusesWeb` (S5.6.3).
   - New `AKimiQuestionLaunch_IsTheShimRequest_InItsFolderOrItsCopy_OnTheRowsHome` (S5.6.5).
   - New `APlanWithFlags_IsRefusedForKimi` (S5.6.6).
2. Red run against the code with t1 only. Build passed.
3. Production code:
   - `ConfinementPlanner.cs`: `"kimi" => Kimi(grant, flag),` arm and the `Kimi` method (no flags, Disk = Snapshot with `SnapshotSources`, default = Scratch).
   - `KimiConsultant.cs`: `MustCarryNoPlanFlags` called first in `Build`, after `MustBeLaunchable`.
4. Green run.

## Red messages (differ from the document in wording only)

- S5.6.3: `Expected disk.Cwd to be CwdKind.Snapshot {value: 2}, but found CwdKind.Scratch {value: 0}.`
  The document said `plan.Cwd`; the test variable is `disk`.
- S5.6.5: `Expected disk.Request.WorkingDirectory to be the same string … but they differ at index 10: actual "/tmp/coai-scratch-ef…", expected "/tmp/coai-question-6…"`.
  Same cause as the document (default plan is a scratch cwd). The none half passed first.
- S5.6.6: `Expected a <System.ArgumentException> to be thrown, but no exception was thrown.` (matches the document).

## Green results

- `ConfinementPlannerTests`: 35 of 35 pass.
- `KimiConsultantTests`: 12 of 12 pass.
- Whole MCP suite (`CoaiMcp.Tests`): 10552 total, 10465 pass, 84 skipped, 3 fail:
  - `KimiHomeTests.AHomeWhoseDataPathPasses260_IsCheckedAndDisposed`: a known failure (PathTooLongException).
  - `KimiRuntimeTests.ALaunchThatNamesItsHome_GoesThroughTheLaunchControl_WhichReadsThePersonsSignIn`: a known failure. Rerun alone: 1 failed, 23 passed.
  - `TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow`: NOT on the known list. It failed once in the full run, then passed alone (10 of 10). Its 1 ms timeout looks flaky under load. Not caused by this change, which touches no git code. Not investigated further.
  - The document says two MCP failures are known; the full run showed three. The third is the flake above.
- `dotnet format … --verify-no-changes` on the whole solution exits 2. It shows IL2026/IL3050 trimming-analyzer errors in `src_bugs/src/*` and `src_server/src/*`, which I did not touch. Scoped to my three files with `--include`, it is clean (no output, exit 0).

## Deviations and notes

- Build: the first build attempt failed with `Fatal error. Internal CLR error. (0x80131506)`. The retry succeeded. Not investigated.
- The document says the test runner is `…/CoaiMcp.Tests.exe`. On this Linux host the file is `…/bin/Debug/net10.0/CoaiMcp.Tests` (no `.exe`). I ran that path.
- The document said "Do not commit" at the top of the task, but the rules section asks for a commit. I committed the three files by name, as the rules ask.

## Left undone

- Nothing from the contracts. Every contract was implemented as written.
- Not pushed (per the rules).
