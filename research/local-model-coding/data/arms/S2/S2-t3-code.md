# S2 t3 — code notes (E5.6: planner arm for kimi, KimiConsultant refuses flags)

Commit: cc562780 (branch tmp/S2-t3). Not pushed.

## What was done

- `src_mcp/core/QuestionConsult/ConfinementPlanner.cs`: `"kimi" => Kimi(grant, flag)` in `Fragments`; new `Kimi` arm after `Vibe`.
  Disk → `CwdKind.Snapshot`, `SnapshotSources = grant.Roots`, no flags. None → `CwdKind.Scratch`, no flags. Web is not reached (matrix refuses it).
- `src_mcp/runners/Kimi/KimiConsultant.cs`: `MustCarryNoFlags(launch)` called in `Build` right after `MustBeLaunchable`. Throws `ArgumentException` ("…the shim's…") for a planned launch with any flag or leading flag.
- Tests (as the document specifies):
  - `ConfinementPlannerTests`: `KimiPlannerArm_IsACopyForDisk_AnEmptyFolderForNone_AndRefusesWeb` (S5.6.3); theory row `kimi`/`Web`/`not been measured`; unknown-runtime sentence now names `kimi`.
  - `KimiConsultantTests`: `AKimiQuestionLaunch_IsTheShimRequest_InItsFolderOrItsCopy_OnTheRowsHome` (S5.6.5); `APlanWithFlags_IsRefusedForKimi` as a Theory with two cases, flag in `Flags` (`--yolo`) and in `Leading` (`--search`) (S5.6.6). Added `using CoaiMcp.Core.QuestionConsult;`.
  - The S5.6.6 second case is a Theory `[InlineData(false)]`/`[InlineData(true)]` with a bool, not a second hand-written Fact.

## Red messages

- S5.6.3 (before arm): `Expected disk.Cwd to be CwdKind.Snapshot … because Kimi's roots are copied, never its cwd (D6), but found CwdKind.Scratch.` Matches the document.
- S5.6.6 (before `MustCarryNoFlags`): `Expected a <System.ArgumentException> to be thrown, but no exception was thrown.` Matches, for both cases.
- S5.6.5 (before arm), NOT as the document predicted ("green on arrival"). It was red: `Expected disk.Request.WorkingDirectory to be … /tree, but was …/cwd`. The disk row fell through to the default arm (Scratch), so the disk launch stood in the scratch folder. Not a mismatch in the test; the arm was missing.
- Teeth check (after arm, before revert): `KimiConsultant.Cwd` changed to ignore the plan (`SnapshotOf(launch)`). Red: `Expected none.Request.WorkingDirectory to be the same string because a none row stands in its own empty scratch folder`. Reverted; the diff for `KimiConsultant.cs` now shows only the `MustCarryNoFlags` change.

## Results

- Build: 0 errors, 0 warnings (`slnx`, Debug, `-m:4 --disable-build-servers`).
- `ConfinementPlannerTests`: 33 / 33 pass.
- `KimiConsultantTests`: 15 / 15 pass (includes `NoAdapter_…` and the consult-launch Kimi test when run as part of the suite).
- Whole suite (`CoaiMcp.Tests`): 10549 total, 2 failed, 10463 passed. The 2 failures are the known Kimi ones:
  - `KimiRuntimeTests.ALaunchThatNamesItsHome_GoesThroughTheLaunchControl_WhichReadsThePersonsSignIn`
  - `KimiHomeTests.AHomeWhoseDataPathPasses260_IsCheckedAndDisposed`
  The extension test (no git tags) is named in the document as failing before any change; I did not run it separately, and the suite run above did not report it as a failure.
- `dotnet format --verify-no-changes`: exit 2. It reports analyzer errors (IL2026/IL3050) only in `src_bugs/` and `src_server/`. No file under `src_mcp/` is listed, and none of my four files appear. These look pre-existing and outside this task; I did not touch them.

## Things to note

- The document's "Out of bounds" section says "Do not commit." The coder rules say commit with a conventional message. I followed the coder rules and committed the four files by name. If the document's line is meant literally, `git reset --soft HEAD~1` undoes it.
- A stray file may exist at `/tmp/scratch_unused` (an early `cp` of `KimiConsultant.cs` that I meant to delete). I could not delete it without approval. It is not in the repo.
- `src_vs_code/node_modules` was untracked before I started; left alone.

## Left

- Nothing from the task's steps is left. Not pushed, by rule.
