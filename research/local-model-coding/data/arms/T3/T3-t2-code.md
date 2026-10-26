# T3 t2 — code notes

Branch `exp/kimi-e56-tasks-3`. Commit `42691822`. Files: `src_mcp/core/QuestionConsult/ConfinementPlanner.cs`,
`src_mcp/runners/Kimi/KimiConsultant.cs`, `src_mcp/tests/ConfinementPlannerTests.cs`. Nothing pushed.

## What I did

1. Red first. Wrote the three Facts and the Kimi InlineData, changed the list string at `ConfinementPlannerTests.cs:167`.
   Built. Ran `ConfinementPlannerTests`: 3 failed.
2. Green. Added `"kimi" => Kimi(grant, flag)` to `Fragments`, the `Kimi` arm after `Vibe`, and `MustCarryNoFlags` in
   `KimiConsultant` (called after `MustBeLaunchable`). Built with 0 warnings and 0 errors.
3. Ran `ConfinementPlannerTests` (35/35), `KimiConsultantTests` (12/12), and the whole suite.

## Red messages (compared with the document)

- S5.6.3 (`ThePlannerArm_…`): `Expected disk.Cwd to be CwdKind.Snapshot … but found CwdKind.Scratch`. The document wrote
  `plan.Cwd`; the test variable is `disk`. Same failure.
- S5.6.6 (`APlanWithFlags_IsRefusedForKimi`): `Expected a <System.ArgumentException> to be thrown, but no exception was thrown.`
  Same as the document.
- S5.6.5 (`AKimiQuestionLaunch_…`) was RED, not green. The document said it is green if E5.4's planned cwd holds. It does not
  hold for disk: the plan was Scratch, so `disk.Request.WorkingDirectory` was `/tmp/coai-scratch-…` and not `/tmp/coai-question-…`
  (the row tree). The none branch and the home checks were not reached. After the planner arm, it is green.
- The web InlineData for `kimi` was already green before the change: the matrix refuses `web`, so the planner never reaches
  `Fragments`. Kept it as the document asks.

## Test runs

- Test host on this Linux machine: `./src_mcp/tests/bin/Debug/net10.0/CoaiMcp.Tests` (the `.exe` in common.md does not exist here;
  `ENOENT`). I ran that file.
- `ConfinementPlannerTests`: 35 total, 0 failed. None skipped. The Kimi Facts run on Linux because they build requests and read
  no launch.
- `KimiConsultantTests`: 12 total, 0 failed.
- Whole suite: 10552 total, 10461 succeeded, 89 skipped, 2 failed. The 2 failures are the known ones:
  `KimiRuntimeTests.ALaunchThatNamesItsHome_…` and `KimiHomeTests.AHomeWhoseDataPathPasses260_IsCheckedAndDisposed`.
- `dotnet format dew_flow_connect_other_ais.slnx --verify-no-changes`: exit 2, NOT clean. Every error is an analyzer error in
  `src_server` (IL2026 / IL3050 on `MapGet`, for example `src_server/src/SessionEndpoints.cs:56`, `UsageEndpoints.cs:39`,
  `CatalogEndpoints.cs:20`). None names my three files. I did not touch `src_server`. I did not check whether this also fails
  at `b4e2ea96`.

## Conflict in instructions

`common.md` says "Do not commit." Task t2 says "commit with a conventional message". I followed t2 and committed the three
files by name. The commit is local only. Tell me if you wanted it left uncommitted, and I will reset it with `git reset --soft HEAD~1`.

## Left undone

- Nothing in the contracts left undone.
- `dotnet format` on my files is not verified clean because the tool exits on `src_server` errors.
