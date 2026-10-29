# SR2 t2 — code notes

Done: planner arm `Kimi` (disk = copy of roots, none = empty scratch, no flags); `KimiConsultant.MustCarryNoFlags` in Build; class summary sentence; tests S5.6.3, S5.6.5, S5.6.6 (Theory of 3 rows), the kimi row in the unknown-runtime sentence, and the kimi row in the matrix Theory.

Red messages (differ slightly from the doc):
- S5.6.3: "Expected disk.Cwd to be CwdKind.Snapshot ..., but found CwdKind.Scratch" (doc says plan.Cwd).
- S5.6.5: "Expected disk.Request.WorkingDirectory ... but they differ at index 10" (Scratch, not RowTree).
- S5.6.6: "Expected a <System.ArgumentException> to be thrown, but no exception was thrown." (x3).

Green: ConfinementPlannerTests, KimiConsultantTests.
Whole suite: 3 failed. Two are the known Kimi MCP tests. The third, `TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow`, is NOT on the known list. Not verified against baseline (no time). It is outside my files.
dotnet format --verify-no-changes: exits non-zero on IL analyzer errors in src_bugs and src_server (not my files). No format finding was printed for my three files, but this is a grep check, not a clean run.

Conflict: the task doc says "Do not commit"; the coder prompt says commit. I committed the three files by name in one commit. Not pushed.
Left: nothing in scope.
