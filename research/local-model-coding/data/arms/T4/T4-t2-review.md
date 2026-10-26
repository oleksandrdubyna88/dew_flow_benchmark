# T4 t2 — review notes

Reviewed commit `26f08710` against `vllm/work/T4/t2.md`. Result: no change needed, nothing committed.

## Findings (all right as they were)

- `ConfinementPlanner.cs`: the `"kimi" => Kimi(grant, flag)` arm and `Kimi(...)` match Contract 1 character for
  character (place, remarks, `SnapshotSources = grant.Roots`, Scratch default). `SandboxVocabulary`, `Plan`, other arms untouched.
- `KimiConsultant.cs`: `MustCarryNoPlannedFlag(launch)` is the second line of `Build`; method is right after `Build`; message exact;
  property pattern compiles; complexity of each method <= 4.
- `ConfinementPlannerTests.cs`: the three tests have the exact names and bodies from (a), (b), (c); the extra InlineData line and
  the widened unknown-runtime list are there; no `using` added; the existing structural tests are unchanged and green.
- Only the three allowed files changed.
- Red reason, checked by me: with the two production files at `44bea5fe`, the three new tests fail for the expected reasons
  - S5.6.3: `Expected disk.Cwd to be CwdKind.Snapshot ... but found CwdKind.Scratch.`
  - S5.6.5: `Expected disk.Request.WorkingDirectory to be the same string ... they differ at index 10`
  - S5.6.6: `Expected a <System.ArgumentException> to be thrown, but no exception was thrown.`
  Files restored to HEAD after; tree clean.

## Tests (at HEAD)
- Build: 0 warnings, 0 errors.
- ConfinementPlannerTests 35/35, KimiConsultantTests 12/12, CapabilityMatrixTests 39/39.
- Not re-run by me: whole suite and `dotnet format` (the coder reported 3 failures in the whole suite: the 2 known and
  a 1 ms-deadline git flake, `TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow`, not related to this task).
