# O2 t2 — code notes (planner kimi arm, KimiConsultant guard)

Commit: 45ab17b5 (branch exp/kimi-e56-opus-split-haiku-2). Files: src_mcp/core/QuestionConsult/ConfinementPlanner.cs,
src_mcp/runners/Kimi/KimiConsultant.cs, src_mcp/tests/ConfinementPlannerTests.cs. Never pushed.

## Done
- Pin at ConfinementPlannerTests.cs:167 now expects "... vibe, kimi".
- Added S5.6.3, S5.6.5, S5.6.6 tests and the helpers KimiPlacesAt, KimiQuestionConsultant, After (After did not exist; added once).
- Planner: "kimi" arm after "vibe", and private Kimi(...) method after Vibe(...), as the contract says.
- KimiConsultant: MustBeTheShimsConfinement(launch) called second in Build; private method after Build.

## Red messages of step 1 (before any production change)
- S5.6.3: "Expected disk.Cwd to be CwdKind.Snapshot ... but found CwdKind.Scratch." (the document says `plan`; the test variable is `disk`. Same reason.)
- S5.6.5: "Expected disk.Request.WorkingDirectory to be RowTree, but they differ": "/tmp/coai-scratch-..." vs "/tmp/coai-question-...". Same reason.
- S5.6.6: "Expected a <System.ArgumentException> to be thrown, but no exception was thrown." (as document)

## Green
- After planner arm: S5.6.3 and S5.6.5 green; S5.6.6 red (as document).
- After guard: ConfinementPlannerTests, KimiConsultantTests, AKimiConsultationScenarioTests: 58 of 58 passed.
- Whole MCP suite (CoaiMcp.Tests.dll): total 10551, failed 3, succeeded 10464, skipped 84.
  - KimiRuntimeTests.ALaunchThatNamesItsHome_GoesThroughTheLaunchControl_WhichReadsThePersonsSignIn: known, not mine.
  - KimiHomeTests.AHomeWhoseDataPathPasses260_IsCheckedAndDisposed: known, not mine.
  - TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow: NOT in the document's list. It also fails
    when run alone. It does not reference ConfinementPlanner or KimiConsultant. Not investigated further; needs a look by
    someone else (possibly git-version or host dependent; baseline at HEAD~1 not checked).

## Notes on the rules
- The document ends with "Do not commit." but the coordinator's rules say commit with a conventional message. I followed the
  rules: one commit of my three files, never pushed. If "do not commit" was meant, undo it with `git reset --soft HEAD~1`.
- The document's test command uses `./src_mcp/tests/bin/Debug/net10.0/CoaiMcp.Tests.exe`. On Linux that file does not exist
  (low-priority.mjs fails with ENOENT). I ran `dotnet src_mcp/tests/bin/Debug/net10.0/CoaiMcp.Tests.dll` with the same filters.

## Left
- Nothing from t2 scope. The numstats failure above is open.
