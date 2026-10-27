# T1-t1 — code notes (E5.6: kimi's three rows, the vectors, the generated table)

Commit: `e7d64be6` on branch `tmp/T1-t1` (the worktree's branch; the brief named `exp/kimi-e56-tasks-1`, and I did not switch branches). Not pushed.

## Files changed (exactly the six allowed)
- `src_mcp/core/QuestionConsult/RuntimeCapabilities.cs` — `Runtimes` gains `"kimi"` (line 84).
- `shared/runtime-capabilities.json` — `runtimes` gains `kimi`; the `why` lines name the kimi record; three kimi rows (none unconfined, disk unconfined, web unmeasured).
- `shared/capability-matrix-vectors.json` — three kimi vectors, after vibe × web and before gemini.
- `src_vs_code/src/runtimeCapabilities.generated.ts` — produced by `node src_vs_code/scripts/generate-runtime-capabilities.mjs`; `--check` reports "up to date". Not hand-edited.
- `src_mcp/tests/CapabilityMatrixTests.cs` — two new facts, `KimiHasThreeRows_EachCitingItsCells` and `AdmissionFlagsBothRows`, placed directly after `TheVibeRows_RestOnTheVibeProbe_…`; plus one line in the `Records` list (see deviation 1).
- `src_vs_code/src/test/capabilityAdmission.test.ts` — the `RUNTIMES` assertion, the gemini refusal regex, and the new test `kimi rows admit flagged, web refuses naming its record`.

## Red and green

C# (`CoaiMcp.Tests.CapabilityMatrixTests`):
- Red before production change, step 1:
  - `KimiHasThreeRows_EachCitingItsCells` — `Expected none not to be <null>.` The brief quoted `Expected RuntimeCapabilities.Builtin.Of("kimi", Capability.None) not to be <null>.`; FluentAssertions names the variable here.
  - `AdmissionFlagsBothRows` — `Expected type to be CoaiMcp.Core.QuestionConsult.Admission+Admitted, but found …Refused.` Matches the brief.
- After step 2 (table + runtime list), `TheVectorsActuallyLoaded_AndCoverEveryPairOfTheTable` is red on `kimi × none` (S5.6.2 red, as the brief expects). Both new facts are green.
- After step 3 (vectors): `CapabilityMatrixTests` 39 of 39 passed, 0 failed, 0 skipped.
- `ConfinementPlannerTests`: 31 of 31 passed, 0 skipped.

Extension (`npm --prefix src_vs_code test`, run through the low-priority wrapper):
- Result: 6290 tests, 6270 pass, 1 fail, 19 skipped. The one failure is the known `the baseline names no release that was never tagged`.
- `the words are the file’s words…`, `a refusal says what cures it…`, `a flagged admission carries the caveat…` and the new `kimi rows admit flagged, web refuses naming its record` all pass.
- **Gap:** I did not run the TypeScript tests red before the generator ran. I edited the test file first, then made the table change, then ran them green. The red messages in the brief (`strictly deep-equal` and `'kimi' is not a runtime…`) are therefore not observed here.

## Deviations from the brief
1. **Not in the brief: `Records` list in `CapabilityMatrixTests.cs`.** The test `EveryRow_CitesTheRecord_AndACellOrSaysWhyThereIsNone` (existing, unchanged) kept a fixed list of two records. Once kimi rows cite `RESULTS_kimi_confinement.md`, it failed with `kimi × none: a row cites exactly one record, but the collection is empty.` I added `("RESULTS_kimi_confinement.md", "2026-10-13", "M-K")` to that list and extended its doc comment. This is test data in a file the brief allowed. No production code changed for it. The brief said the task's "Done when" needs `CapabilityMatrixTests` green, so I took this path.
2. **Placement of the two new facts.** The brief says both "at the end of the class" and "directly after `TheVibeRows…`". I used the second, more specific placement.
3. **Message text** of the first red fact differs as noted above.

## Skips
None. No test in my classes skipped on this host.

## Not run
- The whole MCP suite was not run (the brief says not to). The two known MCP failures (`KimiRuntimeTests…`, `KimiHomeTests…`) were not checked.

## Left
Nothing from the brief is left undone, except the TypeScript red step noted above.
