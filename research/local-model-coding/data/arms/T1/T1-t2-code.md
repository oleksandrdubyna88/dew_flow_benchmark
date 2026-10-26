# T1 t2 — planner's kimi arm (S5.6.3, E5.6)

## Done
- `src_mcp/core/QuestionConsult/ConfinementPlanner.cs`: added `"kimi" => Kimi(grant, flag),` in `Fragments`, and the `Kimi(...)` method after `Vibe(...)`, as the contract says. `SandboxVocabulary` and `CwdKind` unchanged.
- `src_mcp/tests/ConfinementPlannerTests.cs`: added fact `ThePlannerArm_IsACopyForDisk_AnEmptyFolderForNone_AndRefusesWeb` (with the S5.6.3 summary) and `[InlineData("kimi", Capability.Web, "not been measured")]`.

## Red
- Differs from the document: `none.Flag` already passed (the matrix supplies `Unconfined`). The first failure was:
  `Expected disk.Cwd to be CwdKind.Snapshot {value: 2} because Kimi loads its configuration from its working folder, so it reads a copy (D6, M-K9 (e)), but found CwdKind.Scratch {value: 0}.`
- t1 was already in the branch (`86531ca2`), so there was no cast failure.

## Green
- Build (solution, -m:4 --disable-build-servers): succeeded, 0 errors.
- `ConfinementPlannerTests`: Passed (no skips reported).
- `CapabilityMatrixTests`: Passed (no skips reported).

## Commit
- `dac76033`, two files only, conventional message. Not pushed.

## Left
- Nothing in t2 scope.
