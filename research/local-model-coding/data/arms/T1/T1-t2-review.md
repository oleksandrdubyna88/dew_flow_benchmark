# T1 t2 review — planner's kimi arm (S5.6.3, E5.6)

Result: no change needed. No commit made.

## Findings (all right as it was)
- `ConfinementPlanner.cs`: the `"kimi" => Kimi(grant, flag),` arm is after the `vibe` arm. `Kimi(...)` is after `Vibe(...)`, before `AddDirs`. Text is the same as the contract, including the `<remarks>`. `SandboxVocabulary` and `CwdKind` are unchanged.
- `ConfinementPlannerTests.cs`: the fact `ThePlannerArm_IsACopyForDisk_AnEmptyFolderForNone_AndRefusesWeb` is the same as the contract. It has the `S5.6.3` summary. It is after the Vibe fact. The `kimi`/`Web` InlineData is after the `vibe` line.
- Only the two task files changed (`git diff --stat`).
- Red reason: I ran the new tests against the old planner file (`e6f68b17`). 1 of 33 failed: `Expected disk.Cwd to be CwdKind.Snapshot ... but found CwdKind.Scratch`. The coder's note is right: `none.Flag` was already `Unconfined` from the matrix (t1), so that line is not red. The old file was restored (`git checkout`); the tree is clean.

## Tests (Linux, low-priority wrapper, build `-m:4 --disable-build-servers`: 0 warnings, 0 errors)
- `ConfinementPlannerTests`: 33 total, 33 passed, 0 skipped.
- `CapabilityMatrixTests`: 39 total, 39 passed, 0 skipped.
