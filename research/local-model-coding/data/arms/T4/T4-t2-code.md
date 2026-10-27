# T4 t2 — code notes: the planner's `kimi` arm and `KimiConsultant` refusing a planned launch with flags

Branch `exp/kimi-e56-tasks-4`. Commit `26f08710` (three files, by name). Not pushed.
Spec: `vllm/work/T4/t2.md` (E5.6, decisions 1 and 3, S5.6.3, S5.6.5, S5.6.6).

## What was done

1. `src_mcp/core/QuestionConsult/ConfinementPlanner.cs`
   - `Fragments`: added `"kimi" => Kimi(grant, flag),` after the `vibe` arm.
   - `Kimi(...)`: directly after `Vibe(...)`. No flags on any capability. `disk` → `CwdKind.Snapshot` with
     `SnapshotSources = grant.Roots`; everything else → `CwdKind.Scratch`. Text as in Contract 1.
   - `SandboxVocabulary`, `Plan` and other arms unchanged.
2. `src_mcp/runners/Kimi/KimiConsultant.cs`
   - `Build`: `MustCarryNoPlannedFlag(launch);` as the second line.
   - `MustCarryNoPlannedFlag(ConsultantLaunch)`: directly after `Build`. Pattern
     `launch.Confinement is LaunchConfinement.Planned { Plan: var plan }` compiled as written (no fallback needed).
     Message is the exact sentence from Contract 2.
3. `src_mcp/tests/ConfinementPlannerTests.cs`
   - (a) `ThePlannerArm_IsACopyForDisk_AnEmptyFolderForNone_AndRefusesWeb` after the Vibe disk/none test.
   - (a) `[InlineData("kimi", Capability.Web, "not been measured")]` added to the matrix-refusal theory.
   - (a) unknown-runtime sentence widened to `... vibe, kimi`.
   - (b) `AKimiQuestionLaunch_IsTheShimRequest_InItsFolderOrItsCopy_OnTheRowsHome`, after the Vibe question-launch test.
   - (c) `APlanWithFlags_IsRefusedForKimi`, directly after (b).
   - No `using` added; fully qualified names as the document says.

## Red before the production change (Step 2)

All three were red for the reason the document gives. The wording differs from the document's paraphrase
(FluentAssertions prints the variable name and a diff); the cause is the same.

- S5.6.3 `ThePlannerArm_IsACopyForDisk_AnEmptyFolderForNone_AndRefusesWeb`:
  `Expected disk.Cwd to be CwdKind.Snapshot {value: 2} because Kimi loads AGENTS.md, skills and .mcp.json from its cwd (M-K9), so it reads a copy (D6), but found CwdKind.Scratch {value: 0}.`
  (The `none` asserts and the `web` refusal passed with t1, as expected.)
- S5.6.5 `AKimiQuestionLaunch_IsTheShimRequest_InItsFolderOrItsCopy_OnTheRowsHome`:
  `Expected disk.Request.WorkingDirectory to be the same string ... but they differ at index 10:` actual `/tmp/coai-scratch-…`, expected `/tmp/coai-question-…` (RowTree). Same cause as the document: the default arm planned Scratch for disk.
- S5.6.6 `APlanWithFlags_IsRefusedForKimi`:
  `Expected a <System.ArgumentException> to be thrown, but no exception was thrown.` (matches the document).

## Green after the change

- Build (`dew_flow_connect_other_ais.slnx`, Debug, `-m:4 --disable-build-servers`): succeeded, 0 warnings, 0 errors.
- `ConfinementPlannerTests`: 35 / 35.
- `KimiConsultantTests`: 12 / 12.
- `CapabilityMatrixTests`: 39 / 39.
- Full MCP suite (`CoaiMcp.Tests`): total 10552, succeeded 10465, skipped 84, failed 3.
  - `KimiRuntimeTests.ALaunchThatNamesItsHome_GoesThroughTheLaunchControl_WhichReadsThePersonsSignIn`: known, in the document.
  - `KimiHomeTests.AHomeWhoseDataPathPasses260_IsCheckedAndDisposed`: known, in the document.
  - `TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow`: NOT on the known list. It failed once in the full run and once when the class was run alone (`Expected (…DeltaAsync(_repo, parent, head, null, Ct, oneMillisecond)).State`). It uses a 1 ms git deadline and does not touch the planner or Kimi code. A rerun of the class alone passed 10/10. Treated as a timing flake on this host. NOT verified against a baseline build at `b4e2ea96`, so "pre-existing" is not proven.
- Structural tests `DisallowedTools_AppearsInNoPlan` and `NoAdapter_SendsASandboxFlag_ThePlanDidNotGiveIt` are green with kimi now reached.
- `KimiEnvironment.Refusal` for the S5.6.5 request was empty on Linux (test did not need a skip).
- `--cwd` in the kimi argv is spelled as `WorkingDirectory` (the test's `ContainInConsecutiveOrder("--cwd", request.WorkingDirectory)` passed).

## dotnet format

`dotnet format dew_flow_connect_other_ais.slnx --verify-no-changes` exits 2. All 54 output lines are
IL2026 / IL3050 trimming/AOT analyzer diagnostics in untouched projects (`src_server`, `src_bugs`, and others).
None name `ConfinementPlanner.cs`, `KimiConsultant.cs` or `ConfinementPlannerTests.cs`. Not verified against
a baseline; these projects were not edited by this task.

## Left / not done

- Did not establish whether the `TheRoundsNumstatsAreCountedTests` flake exists at `b4e2ea96` (would need a baseline worktree build). Flagged above.
- Did not fix the `dotnet format` analyzer exit code; outside this task's files.
- Nothing pushed (per task).
