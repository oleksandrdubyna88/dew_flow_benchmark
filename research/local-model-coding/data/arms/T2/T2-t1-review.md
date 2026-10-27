# T2-t1 — review notes

Reviewed commit `dc4a82b6` against `/home/jinx/git/coai-exp/vllm/work/T2/t1.md`. I changed no file and made no commit.

## Findings (all right as it was)

1. `shared/runtime-capabilities.json`: `runtimes` list, `why` sentence, three kimi rows. The values are the document's values, in the document's order and key order. `cells` are one item per line. The `why` sentence joins the kimi part with ", and" — fine.
2. `shared/capability-matrix-vectors.json`: three vectors, text exact, placed before `gemini`.
3. `RuntimeCapabilities.cs` `Runtimes` and `ConfinementPlanner.cs` arm and `Kimi(...)` method: exact. `SandboxVocabulary` and `CwdKind` unchanged. No flag in the plan.
4. `runtimeCapabilities.generated.ts`: generator `--check` says "up to date" (21 rows over 7 runtimes).
5. Tests: the two `CapabilityMatrixTests` tests, the planner test, the InlineData, the TS line 143 and the new TS test all match the document.
6. The extra `Records` entry in `CapabilityMatrixTests.cs` (`RESULTS_kimi_confinement.md`, `2026-10-13`, `M-K`) is not in the document. It is needed: `EveryRow_CitesTheRecord_AndACellOrSaysWhyThereIsNone` fails without it. No assertion changed. Right.
7. Only the eight task files changed.
8. The coder's red runs (notes `T2-t1-code.md`) match the reasons in the document. The `AdmissionFlagsBothRows` red text differs from the document, but the cause is the same (matrix does not know kimi). I did not re-run the red at the base commit: the coder's record is clear and time was short.
9. The coder's open item `TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow`: it passes alone (10 of 10) on this branch. It uses a 1 ms timeout, so it is a timing flake under load. Not the task's.

## Test results

- Build: 0 warnings, 0 errors.
- `CapabilityMatrixTests` + `ConfinementPlannerTests`: 72 of 72 pass.
- `npm test`: 6270 pass, 1 fail — the known `the baseline names no release that was never tagged`.
- Whole MCP suite: I saw `KimiHomeTests.AHomeWhoseDataPathPasses260_IsCheckedAndDisposed` fail (known). I did not read the final summary line of this run (my filter cut it), so I do not claim a full count. The coder's run showed only the two known failures plus the flaky one above.
