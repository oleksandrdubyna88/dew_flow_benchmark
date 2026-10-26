# T3 t2 — review notes

Branch `exp/kimi-e56-tasks-3`. Reviewed commit `42691822` against `/home/jinx/git/coai-exp/vllm/work/T3/t2.md`.
Result: NO change needed. I committed nothing.

## Findings (all "right as it was")

1. **Files changed.** Only the three task files: `ConfinementPlanner.cs`, `KimiConsultant.cs`, `ConfinementPlannerTests.cs`.
   Right: nothing out of bounds is touched.
2. **`ConfinementPlanner.cs`.** The `"kimi" => Kimi(grant, flag)` arm is after `"vibe"`. The `Kimi` method is after `Vibe`, with the
   exact switch of the document and a `<summary>` in the style of its neighbours. `SandboxVocabulary` is unchanged. Right.
   The coder added one comment line about `web` (the matrix refuses it before this arm). It is correct and harmless.
3. **`KimiConsultant.cs`.** `MustCarryNoFlags(launch)` is called right after `MustBeLaunchable`, before `inner.Build`.
   Body and message equal the document (`"...the shim's; ..."`, `nameof(launch)`). Right.
4. **Tests.** The list string at the old `:167` now ends with `, vibe, kimi`. The Theory has the `kimi/Web/"not been measured"` row.
   The three Facts have the exact names, the exact assertions and the exact place (the planner Fact after the Vibe arm test,
   the two launch Facts after the Vibe launch test). Right.
5. **Red reason.** I put the two production files back to `b52bffa7` and ran `ConfinementPlannerTests`: 3 failed, 32 passed.
   - S5.6.3: `Expected disk.Cwd to be CwdKind.Snapshot … but found CwdKind.Scratch`.
   - S5.6.6: `Expected a <System.ArgumentException> to be thrown, but no exception was thrown.`
   - S5.6.5: also red (disk `WorkingDirectory` was the scratch, not the row tree). The document said it might be green already;
     the coder's note is right that it is not. The reason is the missing planner arm, not a test fault.
   Then I restored the files with `git checkout HEAD --`; the tree is clean again.
6. **Conventions.** `dotnet format --verify-no-changes --include <the 3 files>` is clean. Build: 0 warnings, 0 errors.
   The coder's whole-solution `dotnet format` failure is in `src_server` (analyzer IL2026/IL3050), outside this task.
7. **Commit trailer.** The commit ends with `Co-Authored-By: Claude Haiku 5.5`. The session attribution says Sonnet 5.5.
   Right as it was: the coder was a different model; I did not rewrite history.
8. **`common.md` vs t2 on "commit".** The coder followed t2 and committed locally, nothing pushed. Right.

## Test results (after restore)

- `CoaiMcp.Tests.ConfinementPlannerTests`: 35 total, 35 passed, 0 failed, 0 skipped.
- `CoaiMcp.Tests.KimiConsultantTests`: 12 total, 12 passed.
- Whole suite not re-run (the task asks only for the task's classes). The coder's run: 10552 total, 2 failed (the known
  `KimiRuntimeTests.ALaunchThatNamesItsHome_…` and `KimiHomeTests.AHomeWhoseDataPathPasses260_IsCheckedAndDisposed`).
