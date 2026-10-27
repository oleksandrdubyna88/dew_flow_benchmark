# S4-t1 — Kimi rows, vectors and the loader's list (C#)

Commit: `b4612224` on `tmp/S4-t1` (four files, added by name, not pushed).

## Done
- `shared/runtime-capabilities.json`: `runtimes` gains `kimi`; the `why` array gains the kimi sentence after the vibe sentence; three kimi rows (`none` unconfined, `disk` unconfined, `web` unmeasured) appended after the vibe rows, per the document.
- `shared/capability-matrix-vectors.json`: three kimi vectors inserted after the vibe vectors and before `gemini`.
- `src_mcp/core/QuestionConsult/RuntimeCapabilities.cs:84`: `Runtimes` gains `"kimi"` at the end.
- `src_mcp/tests/CapabilityMatrixTests.cs`: `Records` gains the kimi tuple and its summary; new tests `KimiHasThreeRows_EachCitingItsCells` and `AdmissionFlagsBothRows_AndWebIsRefusedNamingItsRecord`; the runtime literal in `AnUnknownRuntimeOrCapability_IsRefused_NamingWhatIsKnown` now lists `kimi`.

## Red checks (run before the production change)
- `KimiHasThreeRows_EachCitingItsCells` — red as the document says, but the text differs: `Expected none not to be <null>.` (FluentAssertions names the local variable, not the call).
- `AdmissionFlagsBothRows_AndWebIsRefusedNamingItsRecord` — red, but the text differs: `Expected type to be CoaiMcp.Core.QuestionConsult.Admission+Admitted, but found CoaiMcp.Core.QuestionConsult.Admission+Refused.` The document's wording was not produced. The failing line is the loop's first `Admit("kimi", None)` (line 228).
- `TheVectorsActuallyLoaded_AndCoverEveryPairOfTheTable` — red with the vectors removed, reason as the document: `the table's kimi × none row must have a vector deciding it`. The first pass added the seed and the vectors together, so this red was produced by a temporary removal of the three vector lines, then restored.

## Green
- `CapabilityMatrixTests`: 39 of 39 after the change.
- `Builtin.Rows` holds 21 rows (7 × 3), `kimi` last.

## Full suite
- The run was started with `CoaiMcp.Tests` (no `.exe`; this Linux host builds the binary without the suffix, so the `.exe` path in the document and in PROJECT.md does not exist here).
- Known, not mine: `KimiRuntimeTests.ALaunchThatNamesItsHome_…` and `KimiHomeTests.AHomeWhoseDataPathPasses260_…` — both fail as the document says.
- Not in the document: `TheRoundsNumstatsAreCountedTests.AnUnknownCommit_IsGitFailed_NeverAThrow` fails with `… should be an empty, successful numstat, but found False`. It runs `AllChangesAsync` with a 1 ms timeout (`oneMillisecond`) and does not touch the kimi table or the runtime list. Its cause was not investigated, and I did not confirm it fails at `b4e2ea96`.
- Final suite summary: total 10538, failed 3, succeeded 10451 (exit code 2).
- The numstat failure is deterministic: its class alone gives 10 total, 1 failed (`AnUnknownCommit_IsGitFailed_NeverAThrow`), so it is not a timing flake. Still not confirmed as pre-existing at `b4e2ea96`.

## Left
- Nothing in my four files.
- Outside my scope: `src_vs_code` generated file and its tests (t2), the planner and consultant (t3).
