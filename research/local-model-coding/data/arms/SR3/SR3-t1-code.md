# SR3-t1 — code notes (kimi rows, vectors, generated TS table)

Commit: e7060e5b on branch tmp/SR3-t1 (not pushed).

## Done
- shared/runtime-capabilities.json: runtimes gain "kimi"; `why` gains the blank item and the kimi sentence; three kimi rows (none, disk, web) appended after the vibe rows.
- shared/capability-matrix-vectors.json: three kimi vectors after `vibe web`.
- src_mcp/core/QuestionConsult/RuntimeCapabilities.cs: Runtimes list gains "kimi".
- src_vs_code/src/runtimeCapabilities.generated.ts: regenerated with the generator; `--check` exits 0.
- src_mcp/tests/CapabilityMatrixTests.cs: Records gains the kimi tuple; list string updated; new facts KimiHasThreeRows_EachCitingItsCells and AdmissionFlagsBothRows.
- src_vs_code/src/test/capabilityAdmission.test.ts: list updates; new test `kimi rows admit flagged, web refuses naming its record`.

## Red messages (as seen)
- S5.6.1 KimiHasThreeRows: `Expected none not to be <null>.`
- S5.6.4 AdmissionFlagsBothRows: `Expected type to be CoaiMcp.Core.QuestionConsult.Admission+Admitted, but found CoaiMcp.Core.QuestionConsult.Admission+Refused.`
- list-string test (AnUnknownRuntimeOrCapability_IsRefused): `Expected runtime.Reason "'gemini' is not a runtime ... it launches on: claude, codex, antigravity, local, api, vibe" to contain "claude, codex, antigravity, local, api, vibe, kimi"`.
- S5.6.2 TheVectorsActuallyLoaded (run with the vectors file temporarily at HEAD): `failed ... Expected Vectors ... ` (the table's kimi × none row had no vector).
- S5.6.8 extension: `AssertionError: 'kimi' is not a runtime the question consultant can launch for 'none' — it launches on: claude, codex, antigravity, local, api, vibe`.
- Extension list test: deepStrictEqual failed on RUNTIMES; regex test failed on `/claude, codex, antigravity, local, api, vibe, kimi/u`.

## Green
- CapabilityMatrixTests: 39 passed, 0 failed.
- Full CoaiMcp.Tests: 10456 succeeded, 84 skipped, 2 failed — both known: KimiRuntimeTests.ALaunchThatNamesItsHome… and KimiHomeTests.AHomeWhoseDataPathPasses260….
- Extension: typecheck and lint ran clean; npm test: 6270 pass, 1 fail — the known `the baseline names no release that was never tagged`.
- generatedFilesAreCurrent-style tests (`the generated catalog is the seed`, `the generated table is the seed`) green.

## Deviations from the document
1. ConfinementPlannerTests.AnUnknownRuntime_IsRefused_NamingTheOnesItKnows did NOT go red. Its assertion is `Contain("claude, codex, antigravity, local, api, vibe")`, a substring of the new list, so it still passes. The document expected red. Not edited (out of bounds). t2 should decide whether it needs no change.
2. The document says "Do not commit" in one place, but the coder rules say commit with a conventional message. I committed per the coder rules.
3. The MCP binary on Linux is `src_mcp/tests/bin/Debug/net10.0/CoaiMcp.Tests` (no `.exe`); the document's `.exe` path does not exist here.
4. S5.6.2 red was produced by temporarily restoring the HEAD vectors file (the document's step order was not followed exactly because the vectors were added in the same edit batch); the file was restored and then committed with the new vectors.
5. `dotnet format --verify-no-changes` fails with IL2026/IL3050 analyzer errors in src_server (CoaiServer.csproj), files not mine. No error names a file I changed. Not fixed.

## Left
- Nothing in t1 scope. Notes logs are in /home/jinx/git/coai-exp/notes/SR3-t1-*.log.
