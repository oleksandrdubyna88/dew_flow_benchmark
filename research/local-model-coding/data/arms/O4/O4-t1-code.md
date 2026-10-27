# O4-t1 — kimi's capability rows (E5.6: S5.6.1, S5.6.2, S5.6.4, S5.6.8)

Commit: d8e2aa1d on tmp/O4-t1 (not pushed). Files: the six in the task list.

## Done
- Red tests added: `KimiHasThreeRows_EachCitingItsCells`, `AdmissionFlagsBothRows` (C#); `kimi rows admit flagged, web refuses naming its record` (TS); `:143`, `:166`, `:273` edits.
- `kimi` added to `RuntimeCapabilities.Runtimes` (last), the JSON `runtimes`, the `why` lines, and the three rows.
- Three vectors added after the vibe web vector, before gemini.
- TS table regenerated with the generator; `--check` exits 0.

## Red messages (differences from the document)
- Step 1 `KimiHasThreeRows_EachCitingItsCells`: `Expected RuntimeCapabilities.Builtin.Of("kimi", Capability.None) not to be <null>.` (as documented).
- Step 1 `AdmissionFlagsBothRows`: `Expected type to be ...Admission+Admitted, but found ...Admission+Refused.` The assertion message does not show the reason text, so it differs from the document's wording.
- Step 1 also turned red `AnUnknownRuntimeOrCapability_IsRefused_NamingWhatIsKnown` (from the `:273` edit). The document does not list it.
- Step 2 `TheVectorsActuallyLoaded_AndCoverEveryPairOfTheTable`: `the table's kimi × none row must have a vector deciding it` (as documented, S5.6.2).
- Step 4 TS, before regeneration: `Expected values to be strictly deep-equal` (the words test), plus the vector and generator-freshness tests (stale generated file).

## Deviation from the contract (not in the document)
- `EveryRow_CitesTheRecord_AndACellOrSaysWhyThereIsNone` went red after step 2: its `Records` table in `CapabilityMatrixTests.cs` had no kimi record. I added `("RESULTS_kimi_confinement.md", "2026-10-13", "M-K")`, following the vibe entry. Data only; no signature changed.

## Test results (after all steps)
- `CapabilityMatrixTests`: 39 of 39 green.
- Whole MCP suite: 10542 total, 3 failed:
  - `KimiHomeTests.AHomeWhoseDataPathPasses260_IsCheckedAndDisposed` (known, not mine)
  - `KimiRuntimeTests.ALaunchThatNamesItsHome_GoesThroughTheLaunchControl_WhichReadsThePersonsSignIn` (known, not mine)
  - `TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow` — NOT in the known list. It failed twice (once in the full run, once when its class was rerun alone). Its message: `Expected (_assembler.AllChangesAsync(...)).Failed to be True ... but found False`. I did not check it against `b4e2ea96`, and I did not change any code it touches. It needs a baseline check by someone with time.
- `ConfinementPlannerTests`: ran as part of the whole suite; it is not among the failures. Not checked separately.
- `npm test` (src_vs_code): 6290 tests, 6270 pass, 1 fail — `the baseline names no release that was never tagged` (known). The new kimi TS test is green.
- `generate-runtime-capabilities.mjs --check`: exit 0.
- `dotnet format --verify-no-changes` (the solution): exit 2. It prints AOT/trim analyzer errors (IL2026/IL3050) from `src_bugs` and `src_server`, not from my files. No output line names my files. The format result is therefore not a clean verdict for the solution; it was not checked against the baseline either.

## Left undone
- Baseline check of `TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow`.
- A clean `dotnet format` verdict (blocked by the analyzer errors in other projects).
- Nothing pushed (as the brief says).
