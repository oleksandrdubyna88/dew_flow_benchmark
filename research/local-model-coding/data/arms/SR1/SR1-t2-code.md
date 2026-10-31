# SR1 t2 — the planner arm for kimi (E5.6, S5.6.3)

Commit: `9c15634a` on `exp/kimi-e56-sonnet-split-haiku-review-1`. Not pushed.
Files changed (only these two): `src_mcp/core/QuestionConsult/ConfinementPlanner.cs`, `src_mcp/tests/ConfinementPlannerTests.cs`.

## What I did
1. Test first: added `Kimi_None_StandsInAnEmptyFolder_Disk_InTheCopyOfItsRoots_NoFlagEver_AndWebIsRefused` after the Vibe test, and the `kimi`/`web` row to the refusal theory.
2. Red run: `Expected disk.Cwd to be CwdKind.Snapshot … but found CwdKind.Scratch` — exactly the message the document gives. Only that test failed.
3. Production code: added `"kimi" => Kimi(grant, flag),` after the `"vibe"` arm, and the `Kimi` method (with its remarks) after `Vibe`. `SandboxVocabulary`, `NeverSent`, `VibeReadOnly` and other arms untouched.
4. Green: `ConfinementPlannerTests` 33/33, `CapabilityMatrixTests` 39/39.
5. `NoAdapter_SendsASandboxFlag_ThePlanDidNotGiveIt` stayed green with the existing `Planned("kimi", grant)` helper. No helper change was needed.
6. Teeth:
   - `CwdKind.Snapshot` → `Scratch` on the disk arm: `ConfinementPlannerTests` goes red (`Expected disk.Cwd to be CwdKind.Snapshot … found CwdKind.Scratch`). Reverted.
   - `"--yolo"` added to the disk `Flags`: red on `Expected disk.Flags to be empty … found {"--yolo"}`, and `NoAdapter_SendsASandboxFlag_ThePlanDidNotGiveIt` also red. Reverted.
   - After both reverts: build 0 errors, both classes green.

## Differences from the document
- The document says the Vibe test "ends at line 150". In the file it ends at line 144. I inserted after the Vibe test's closing brace, which is the intent.
- The document's "Out of bounds" section says "No commit". Your instructions say to commit with a conventional message. I followed your instructions and committed only the two files, by name.
- The commands in the document use `./src_mcp/tests/bin/Debug/net10.0/CoaiMcp.Tests.exe`. That file does not exist on this Linux host, and the launcher fails with ENOENT. I ran the same test host as `dotnet ./src_mcp/tests/bin/Debug/net10.0/CoaiMcp.Tests.dll --filter-class …` instead. The filter and the results are the same.

## Format check: NOT clean (not caused by t2)
`dotnet format dew_flow_connect_other_ais.slnx --verify-no-changes` exits 2. Full output: `/home/jinx/git/coai-exp/notes/SR1-t2-format.txt`.
All 54 error lines are IL2026/IL3050 analyzer errors in `src_bugs/src/AdminApi.cs`, `src_bugs/src/Program.cs`, and several `src_server/src/*` files. None are in `ConfinementPlanner.cs` or `ConfinementPlannerTests.cs`. I did not check these against the parent commit, so I cannot say they predate t2 by a test run. They are in files t2 never touches.

## Left undone / not verified
- `CapabilityMatrixTests` and `ConfinementPlannerTests` are green. The `dotnet format` check is not clean, as above.
- The known failures `KimiRuntimeTests.ALaunchThatNamesItsHome_…` and `KimiHomeTests.AHomeWhoseDataPathPasses260_…` are outside these classes, and I did not run them.
- Scratch note: the commit message file `/home/jinx/git/coai-exp/notes/SR1-t2-commitmsg.txt` is still in the notes folder. It is not part of the repo.
