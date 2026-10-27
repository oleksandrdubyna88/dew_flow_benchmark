# O3-t1 — kimi capability rows (E5.6 decisions 2 and 4)

## Done
- shared/runtime-capabilities.json: runtimes + kimi; why sentence; three kimi rows (none, disk unconfined; web unmeasured).
- shared/capability-matrix-vectors.json: three kimi vectors.
- src_mcp/core/QuestionConsult/RuntimeCapabilities.cs: Runtimes list + "kimi".
- src_mcp/tests/CapabilityMatrixTests.cs: two new tests (S5.6.1, S5.6.4); the :273 string; a Records entry for RESULTS_kimi_confinement.md (2026-10-13, prefix "M-K").
- src_vs_code/src/test/capabilityAdmission.test.ts: runtimes list, the two regex strings, new kimi test.
- src_vs_code/src/runtimeCapabilities.generated.ts: regenerated (not hand-edited); --check passes.

## Red messages
- S5.6.1: "Expected none not to be <null>" (as the document says).
- S5.6.4: "Expected type to be Admission+Admitted, but found Admission+Refused" (the document's quoted reason text was not shown by FluentAssertions; the Refused reason is the one from the unknown-runtime path).
- S5.6.2 (after rows, before vectors): "the table's kimi × none row must have a vector" — names the missing pairs.
- TS: the kimi test failed as expected; I did not capture the runtimes deepStrictEqual line separately.

## Deviation (not in the document)
- EveryRow_CitesTheRecord_... failed on kimi rows (no record entry). Added the RESULTS_kimi_confinement.md entry to the Records table in CapabilityMatrixTests.cs.

## Results
- CapabilityMatrixTests 39/39, ConfinementPlannerTests 31/31 green.
- Whole C# suite: 10542 total, 3 failed: the two known Kimi tests, and TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow (not in the known list; fails alone too; not in my files; I did not check it against the baseline — the baseline check would need a stash or a worktree I did not make in time).
- npm: typecheck and lint clean; npm test 6270 pass, 1 fail = the known tag test.
- dotnet format --verify-no-changes exits 2 on src_server (IL3050/IL2026 analyzer errors, not my files); none of my files is listed.

## Not done / open
- Branch: the document names exp/kimi-e56-opus-split-haiku-3; work is committed on tmp/O3-t1 as instructed by the worktree.
- Not pushed.
