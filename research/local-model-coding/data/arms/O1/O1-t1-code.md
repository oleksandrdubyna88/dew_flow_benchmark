# O1-t1 — kimi in the capability table (E5.6)

Commit: 7774b2ba on branch tmp/O1-t1 (worktree /home/jinx/git/coai-exp/wt/O1-t1). Not pushed.

## Done
- `RuntimeCapabilities.cs:84`: `kimi` added to `Runtimes`.
- `shared/runtime-capabilities.json`: `kimi` in `runtimes`; the kimi sentence added to `why`; three rows
  (none unconfined, disk unconfined, web unmeasured) after the last vibe row.
- `shared/capability-matrix-vectors.json`: three kimi vectors after the vibe vectors.
- `src_vs_code/src/runtimeCapabilities.generated.ts`: regenerated with the generator (21 rows, 7 runtimes × 3). `--check` reports up to date.
- Tests added: `KimiHasThreeRows_EachCitingItsCells`, `AdmissionFlagsBothRows` (C#); `kimi rows admit flagged, web refuses naming its record` (TS); the `runtimes` assertion now lists `kimi`.

## Deviations from the document
1. `CapabilityMatrixTests.cs`: the existing test `EveryRow_CitesTheRecord_AndACellOrSaysWhyThereIsNone` failed for kimi
   (no record matched). Its `Records` table lacked the kimi record. I added
   `("RESULTS_kimi_confinement.md", "2026-10-13", "M-K")` and extended its doc comment. The file is in scope.
2. Red messages differed from the document:
   - S5.6.1 failed with `NullReferenceException` at the `.Of(...)!` line, not FluentAssertions' "not to be <null>" (the `!` skips the null check, as the contract specifies).
   - S5.6.4 failed with "Expected type ... Admitted, but found ... Refused", not the quoted refusal text.
   Both fail for the document's reason (no kimi row).
3. The loader accepted the cell `"M-K9 (e)"` as written; no change needed.

## Test results (after change)
- `CapabilityMatrixTests`: 39 of 39 pass.
- Full MCP suite: 10538 total, 3 failed, 10451 passed.
  - Known: `KimiRuntimeTests.ALaunchThatNamesItsHome_...`, `KimiHomeTests.AHomeWhoseDataPathPasses260_...`.
  - NOT in the known list: `TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow`. It also fails when run alone ("found False" for `Failed`). It is a git numstat test and touches none of my files. I did NOT verify it fails on the base commit.
- Extension `npm test` (run from `src_vs_code`): 1 failure, the known `the baseline names no release that was never tagged`.
  `kimi rows admit flagged, web refuses naming its record` passes. No other TS test pins the runtimes list.

## Left
- Verify `TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow` on the base commit.

## Notes
- The task named branch `exp/kimi-e56-opus-split-haiku-1`; this worktree is on `tmp/O1-t1` at the same base commit. I committed on `tmp/O1-t1`.
- A message from session "vllm fix" asked that no GPU or vLLM work start while the R9700 is in use. This task runs no GPU work.
